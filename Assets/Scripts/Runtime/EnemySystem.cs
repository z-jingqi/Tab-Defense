using System;
using System.Collections.Generic;
using UnityEngine;
using TapDefense.Core;
using Random = UnityEngine.Random;

namespace TapDefense
{
    public sealed class EnemySystem
    {
        private struct Blast
        {
            public Vector2 position;
            public float radius, damage;
            public int depth;
        }
        private readonly List<EnemyView> active;
        private int nextSpawnId;
        private readonly ComponentPool<EnemyView> pool;
        private readonly Queue<Blast> chains;
        private readonly GameConfig config;
        private readonly FeedbackSystem feedback;
        private RunModel run;
        private readonly Action crystalHit;
        public Vector2 HeroPosition { get; set; } = new Vector2(0,-4.59f);
        public float PursuitStartFeetY => HeroPosition.y-0.49f+1.7f;
        public float Top { get; set; } = 9f;
        public float Bottom { get; set; } = -9f;
        public int AliveCount => active.Count;
        public int AliveBosses { get; private set; }
        public int AliveSummons { get; private set; }
        public event Action<EnemyView> BossRemoved;
        public float BossHealthFraction
        {
            get
            {
                float health = 0, max = 0;
                foreach (var enemy in active)
                    if (enemy.Definition.kind == EnemyKind.Boss) { health += enemy.Health; max += enemy.MaxHealth; }
                return max > 0 ? health / max : 0;
            }
        }
        public EnemySystem(GameConfig config, EnemyView prefab, Transform root, RunModel run, FeedbackSystem feedback, Action crystalHit)
        {
            this.config = config; this.run = run; this.feedback = feedback; this.crystalHit = crystalHit;
            active = new List<EnemyView>(config.enemyPoolSize);
            chains = new Queue<Blast>(config.chainQueueLimit);
            pool = new ComponentPool<EnemyView>(prefab, config.enemyPoolSize, root, enemy => enemy.Prepare(config.squareSprite, config.enemySpriteMaterial, config.hitFlashDuration, config.hitFlashStrength));
        }
        public void BindRun(RunModel model) { run = model; }
        public bool Spawn(EnemyKind kind, SpawnSettings settings)
        {
            if (kind == EnemyKind.Boss || active.Count - AliveBosses - AliveSummons >= settings.maxAlive) return false;
            var definition = config.Enemy(kind);
            float x = 0;
            // Avoid overlapping freshly spawned bodies; defer to the next spawn tick if the top is crowded.
            bool clear = false;
            for (int attempt = 0; attempt < 8 && !clear; attempt++)
            {
                x = Random.Range(-config.arenaHalfWidth + definition.radius, config.arenaHalfWidth - definition.radius); clear = true;
                foreach (var enemy in active)
                    if (Vector2.Distance(enemy.Position, new Vector2(x,Top + definition.radius)) < enemy.Definition.radius + definition.radius + 0.08f)
                    { clear = false; break; }
            }
            if (!clear) return false;
            Vector2 point = new Vector2(x,Top + definition.radius);
            return SpawnAt(kind,settings,point) != null;
        }
        public EnemyView SpawnBoss(SpawnSettings settings, float anchor)
        {
            if (AliveBosses > 0) return null;
            return SpawnAt(EnemyKind.Boss,settings,new Vector2(0,Top+config.Enemy(EnemyKind.Boss).radius),false,-1,anchor);
        }
        public EnemyView SpawnAt(EnemyKind kind, SpawnSettings settings, Vector2 point,
            bool summoned = false, int experience = -1, float anchor = 0)
        {
            if (kind == EnemyKind.Boss && AliveBosses > 0) return null;
            EnemyView enemy = pool.Rent(); if (!enemy) return null;
            enemy.Spawn(config.Enemy(kind),settings,point,++nextSpawnId,config.arenaHalfWidth,summoned,experience,anchor);
            active.Add(enemy); if (kind == EnemyKind.Boss) AliveBosses++; if (summoned) AliveSummons++;
            return enemy;
        }
        public void Step(float dt, float elapsed)
        {
            if(run.State!=RunState.Combat)return;
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var enemy = active[i];enemy.PursuitStartFeetY=PursuitStartFeetY;enemy.HeroFeet=HeroPosition+Vector2.down*0.49f;
                enemy.Step(dt);
            }
            // Local ground separation avoids bodies occupying the same point, without a rail.
            for(int pass=0;pass<3;pass++)
            for(int i=0;i<active.Count;i++)
            for(int j=i+1;j<active.Count;j++)
            {
                var a=active[i];var b=active[j];if(!a.PursuingHero||!b.PursuingHero)continue;
                float gap=a.Definition.radius+b.Definition.radius+0.04f;
                Vector2 delta=b.Feet-a.Feet;float distance=delta.magnitude;
                if(distance>=gap)continue;
                Vector2 direction=distance>.00001f?delta/distance:(a.SpawnId<b.SpawnId?Vector2.right:Vector2.left);
                Vector2 push=direction*((gap-distance)*0.5f);
                a.MoveFeet(a.Feet-push);b.MoveFeet(b.Feet+push);
            }
            // Check windup and strike range after separation, against this frame's hero position.
            for(int i=active.Count-1;i>=0;i--)
            {
                var enemy=active[i];
                if(enemy.StepMelee(dt)) { run.DamagePlayer(enemy.Definition.contactDamage);crystalHit(); }
                if(run.State==RunState.Lost)break;
            }
            int budget = config.chainBlastsPerFrame;
            while (run.State == RunState.Combat && budget-- > 0 && chains.Count > 0) ApplyBlast(chains.Dequeue());
        }
        // Swept segment; stable spawn IDs prevent piercing the same pooled instance twice.
        public bool HitBullet(Vector2 from, Vector2 to, ArrowAttack attack, int[] hitIds, int hitCount, out int id)
        {
            int target = -1; float nearest = float.PositiveInfinity; id = -1;
            for (int i = 0; i < active.Count; i++)
            {
                var enemy = active[i]; bool seen = false;
                for (int h = 0; h < hitCount; h++) if (hitIds[h] == enemy.SpawnId) { seen = true; break; }
                if (seen) continue;
                float reach = attack.radius + enemy.Definition.radius;
                Vector2 delta = to - from; float length = delta.magnitude;
                if (length < 0.00001f) continue;
                Vector2 direction = delta / length, offset = enemy.Position - from;
                float projection = Vector2.Dot(offset, direction);
                float perpendicularSq = Mathf.Max(0, offset.sqrMagnitude - projection * projection);
                if (perpendicularSq > reach * reach) continue;
                float half = Mathf.Sqrt(Mathf.Max(0, reach * reach - perpendicularSq));
                float entry = Mathf.Max(0, projection - half);
                if (projection + half < 0 || entry > length) continue;
                if (entry < nearest) { nearest = entry; target = i; }
            }
            if (target < 0) return false;
            id = active[target].SpawnId; Vector2 point = active[target].Position;
            float damage = attack.damage * (Random.value < attack.criticalChance ? attack.criticalMultiplier : 1);
            DamageResult result = DamageAt(target, damage, 0, DamageSource.Arrow);
            return true;
        }
        private DamageResult DamageAt(int index, float damage, int depth, DamageSource source = DamageSource.Area)
        {
            var enemy = active[index];
            DamageResult result = enemy.Receive(damage,source);
            if (result.bodyDamage > 0) feedback.Hit(enemy.Position,result.bodyDamage,depth > 0);
            if (!result.killed) return result;
            Vector2 point = enemy.Position;
            feedback.Death(point, enemy.Definition.color, enemy.Definition.radius);
            run.RecordKill(enemy.ExperienceReward); Despawn(index);
            if (run.ChainStacks > 0 && depth < run.ChainMaxDepth && chains.Count < config.chainQueueLimit)
                chains.Enqueue(new Blast { position = point, radius = run.ChainRadius, damage = run.ChainDamage, depth = depth + 1 });
            return result;
        }

        private void ApplyBlast(Blast blast)
        {
            feedback.Blast(blast.position, blast.radius, true);
            for (int i = active.Count - 1; i >= 0; i--)
            {
                float reach = blast.radius + active[i].Definition.radius;
                if ((active[i].Position - blast.position).sqrMagnitude <= reach * reach) DamageAt(i, blast.damage, blast.depth);
            }
        }
        private void Despawn(int index)
        {
            EnemyView enemy = active[index];
            bool boss = enemy.Definition.kind == EnemyKind.Boss;
            if (boss) AliveBosses--;
            if (enemy.Summoned) AliveSummons--;
            int last = active.Count - 1; active[index] = active[last]; active.RemoveAt(last); pool.Return(enemy);
            if (boss) BossRemoved?.Invoke(enemy);
        }
        public void Clear()
        { active.Clear(); chains.Clear(); pool.ReturnAll(); AliveBosses = 0; AliveSummons = 0; }
    }
}
