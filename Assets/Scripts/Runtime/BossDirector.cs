using UnityEngine;
using TapDefense.Core;

namespace TapDefense
{
    // One active encounter. Skills use pooled enemies and the shared damage/defense model.
    public sealed class BossDirector
    {
        private enum Phase { Wait, Telegraph, Heal }
        private readonly GameConfig config;
        private readonly EnemySystem enemies;
        private readonly FogOfWar fog;
        private readonly FeedbackSystem feedback;
        private readonly BossSchedule schedule;
        private EnemyView boss;
        private BossProfile profile;
        private int encounter, skillIndex;
        private float timer;
        private Phase phase;
        private BossSkill skill;
        public EnemyView ActiveBoss => boss;
        public string Title => profile == null ? "BOSS" : profile.title;
        public BossDirector(GameConfig config, EnemySystem enemies, FogOfWar fog, FeedbackSystem feedback)
        {
            this.config = config; this.enemies = enemies; this.fog = fog; this.feedback = feedback;
            schedule = new BossSchedule(config.bosses.firstSpawnSeconds); enemies.BossRemoved += OnRemoved;
        }
        public void Reset()
        { boss = null; profile = null; encounter = 0; skillIndex = 0; timer = 0; schedule.Reset(config.bosses.firstSpawnSeconds); fog.Clear(); }
        private void OnRemoved(EnemyView removed)
        { if (boss == removed) { boss = null; fog.Clear(); } }
        public void Stop() { boss = null; profile = null; fog.Clear(); }
        public void Step(float dt, SpawnSettings settings)
        {
            if (!config.bosses.enabled) return;
            bool alive = boss && boss.gameObject.activeSelf;
            if (schedule.Step(dt,alive,config.bosses.repeatSeconds))
            {
                float anchor = enemies.Top-(enemies.Top-enemies.Bottom)*config.bosses.anchorFromTop;
                boss = enemies.SpawnBoss(settings,anchor);
                if (!boss) return;
                profile = config.bossProfiles[encounter++ % config.bossProfiles.Length]; skillIndex = 0;
                schedule.Spawned(); phase = Phase.Wait; timer = config.bosses.initialSkillDelay;
            }
            if (!boss || !boss.Arrived || boss.Frozen) return;
            if (boss.Vitals.StaggerLeft > 0)
            { phase = Phase.Wait; timer = config.bosses.skillInterval; boss.BossAction = ""; return; }
            if (phase == Phase.Heal)
            {
                if (!boss.Vitals.Healing) FinishSkill();
                return;
            }
            timer -= dt; if (timer > 0) return;
            if (phase == Phase.Wait)
            {
                skill = profile.skills[skillIndex++ % profile.skills.Length];
                boss.BossAction = "! " + skill.ToString().ToUpperInvariant();
                phase = Phase.Telegraph; timer = config.bosses.telegraphSeconds; return;
            }
            Execute(settings);
        }
        private void Execute(SpawnSettings settings)
        {
            BossSettings rules = config.bosses;
            switch (skill)
            {
                case BossSkill.Summon:
                    for (int i = 0; i < rules.summonCount && enemies.AliveSummons < rules.summonAliveLimit; i++)
                    {
                        float x = Mathf.Clamp(boss.Position.x+Random.Range(-1.6f,1.6f),-config.arenaHalfWidth+0.4f,config.arenaHalfWidth-0.4f);
                        float y = Mathf.Max((enemies.Top+enemies.Bottom)*0.5f,boss.Position.y-Random.Range(0.4f,1.2f));
                        if (!enemies.SpawnAt(rules.summonKind,settings,new Vector2(x,y),true,rules.summonExperience)) break;
                    }
                    break;
                case BossSkill.Cloud:
                    if (fog.TryShow(rules.cloudSeconds,Random.value,rules.cloudWidth,rules.cloudHeight)) feedback.Clear();
                    break;
                case BossSkill.Shield:
                    boss.Vitals.RaiseShield(rules.shieldHealth,rules.shieldSeconds,rules.staggerSeconds); break;
                case BossSkill.Armor: boss.Vitals.RaiseArmor(rules.armorLayers); break;
                case BossSkill.Heal:
                    boss.Vitals.BeginHeal(rules.healSeconds,rules.healPerSecond,rules.healInterruptFraction,rules.staggerSeconds);
                    boss.BossAction = ""; phase = Phase.Heal; boss.RefreshVisuals(); return;
            }
            FinishSkill();
        }
        private void FinishSkill()
        { phase = Phase.Wait; timer = config.bosses.skillInterval; if (boss) { boss.BossAction = ""; boss.RefreshVisuals(); } }
    }
}
