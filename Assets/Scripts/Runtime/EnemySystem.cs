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
        private readonly int[] lightningIds = new int[9];
        private readonly ComponentPool<EnemyView> pool;
        private readonly Queue<Blast> chains;
        private readonly GameConfig config;
        private readonly FeedbackSystem feedback;
        private RunModel run;
        private readonly Action crystalHit;
        public float Top { get; set; } = 9f;
        public float Bottom { get; set; } = -9f;
        public int AliveCount => active.Count;
        public int AliveBosses { get; private set; }
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
            pool = new ComponentPool<EnemyView>(prefab, config.enemyPoolSize, root);
        }
        public void BindRun(RunModel model) { run = model; }
        public bool Spawn(EnemyKind kind, SpawnSettings settings)
        {
            if (active.Count >= settings.maxAlive) return false;
            EnemyView enemy = pool.Rent(); if (!enemy) return false;
            Vector2 point = new Vector2(Random.Range(-config.arenaHalfWidth,config.arenaHalfWidth), Top + config.Enemy(kind).radius);
            enemy.Spawn(config.Enemy(kind), settings, point, ++nextSpawnId); active.Add(enemy);
            if (kind == EnemyKind.Boss) AliveBosses++;
            return true;
        }
        public void Step(float dt, float elapsed)
        {
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var enemy = active[i]; float burn = enemy.Step(dt);
                if (burn > 0 && DamageAt(i, burn, 0)) continue;
                if (enemy.Position.y + enemy.Definition.radius >= Bottom) continue;
                run.DamagePlayer(enemy.Definition.contactDamage); crystalHit();
                Despawn(i);
                if (run.State == RunState.Lost) break;
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
                float reach = attack.radius + enemy.Definition.radius, dx = Mathf.Abs(enemy.Position.x - from.x);
                if (dx > reach) continue;
                float half = Mathf.Sqrt(Mathf.Max(0, reach * reach - dx * dx));
                float entry = enemy.Position.y - half;
                if (enemy.Position.y + half < from.y || entry > to.y) continue;
                if (entry < nearest) { nearest = entry; target = i; }
            }
            if (target < 0) return false;
            id = active[target].SpawnId; Vector2 point = active[target].Position;
            float damage = attack.damage * (Random.value < attack.criticalChance ? attack.criticalMultiplier : 1);
            DamageAt(target, damage, 0);
            ApplyElement(attack.element, point, attack.damage, id);
            return true;
        }
        private bool DamageAt(int index, float damage, int depth)
        {
            var enemy = active[index];
            feedback.Hit(enemy.Position, Mathf.Min(enemy.Health, damage), depth > 0);
            if (!enemy.Hit(damage)) return false;
            Vector2 point = enemy.Position;
            feedback.Death(point, enemy.Definition.color, enemy.Definition.radius);
            run.RecordKill(enemy.Definition.experience); Despawn(index);
            if (run.ChainStacks > 0 && depth < run.ChainMaxDepth && chains.Count < config.chainQueueLimit)
                chains.Enqueue(new Blast { position = point, radius = run.ChainRadius, damage = run.ChainDamage, depth = depth + 1 });
            return true;
        }
        private void ApplyElement(ElementKind kind, Vector2 point, float damage, int primaryId)
        {
            if (kind == ElementKind.None) return;
            ElementDefinition element = null;
            foreach (var candidate in config.elements) if (candidate.kind == kind) { element = candidate; break; }
            if (element == null) return;
            if (kind == ElementKind.Lightning)
            {
                lightningIds[0] = primaryId;
                for (int hop = 0; hop < element.lightningTargets; hop++)
                {
                    int target = -1; float nearest = element.radius * element.radius;
                    for (int i = 0; i < active.Count; i++)
                    {
                        bool seen = false;
                        for (int h = 0; h <= hop; h++) if (lightningIds[h] == active[i].SpawnId) { seen = true; break; }
                        float distance = (active[i].Position - point).sqrMagnitude;
                        if (!seen && distance <= nearest) { nearest = distance; target = i; }
                    }
                    if (target < 0) break;
                    point = active[target].Position; lightningIds[hop + 1] = active[target].SpawnId;
                    DamageAt(target, damage * element.damageFraction, 0);
                }
                return;
            }
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var enemy = active[i]; float reach = element.radius + enemy.Definition.radius;
                if ((enemy.Position - point).sqrMagnitude > reach * reach) continue;
                if (kind == ElementKind.Fire) enemy.Burn(damage * element.burnDamagePerSecond, element.duration);
                if (kind == ElementKind.Ice) enemy.Chill(element);
                if (kind == ElementKind.Wind) enemy.Pull(point, element);
                if (enemy.SpawnId != primaryId && element.damageFraction > 0) DamageAt(i, damage * element.damageFraction, 0);
            }
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
            if (enemy.Definition.kind == EnemyKind.Boss) AliveBosses--;
            int last = active.Count - 1; active[index] = active[last]; active.RemoveAt(last); pool.Return(enemy);
        }
        public void Clear()
        { active.Clear(); chains.Clear(); pool.ReturnAll(); AliveBosses = 0; }
    }
}
