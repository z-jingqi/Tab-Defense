using System;

namespace TapDefense.Core
{
    public enum BossSkill { Summon, Cloud, Heal, Armor, Shield }
    public enum DamageSource { Arrow, Area, Burn }

    [Serializable]
    public sealed class BossProfile
    {
        public string title = "WARDEN";
        public BossSkill[] skills = { BossSkill.Summon, BossSkill.Shield };
        public static BossProfile[] Defaults() => new[]
        {
            new BossProfile(),
            new BossProfile { title = "MISTCALLER", skills = new[] { BossSkill.Summon, BossSkill.Cloud } },
            new BossProfile { title = "REVENANT", skills = new[] { BossSkill.Armor, BossSkill.Heal } }
        };
    }

    [Serializable]
    public sealed class BossSettings
    {
        public bool enabled = true;
        public float firstSpawnSeconds = 60, repeatSeconds = 90;
        public float anchorFromTop = 0.42f;
        public float initialSkillDelay = 3, telegraphSeconds = 0.9f, skillInterval = 4;
        public int summonCount = 4, summonAliveLimit = 12, summonExperience = 0;
        public EnemyKind summonKind = EnemyKind.Normal;
        public float cloudSeconds = 6, cloudWidth = 0.65f, cloudHeight = 0.26f;
        public float healSeconds = 3, healPerSecond = 0.06f, healInterruptFraction = 0.08f;
        public int armorLayers = 8;
        public float shieldHealth = 180, shieldSeconds = 7, staggerSeconds = 1.5f;
        public bool IsValid => firstSpawnSeconds > 0 && repeatSeconds > 0 && anchorFromTop > 1f/3f && anchorFromTop < 0.5f
            && initialSkillDelay >= 0 && telegraphSeconds > 0 && skillInterval > 0 && summonCount > 0 && summonCount <= 32
            && summonAliveLimit > 0 && summonExperience >= 0 && summonKind != EnemyKind.Boss
            && cloudSeconds > 0 && cloudWidth >= 0.05f && cloudWidth <= 0.95f && cloudHeight > 0 && cloudHeight <= 1f/3f
            && healSeconds > 0 && healPerSecond > 0 && healPerSecond <= 1 && healInterruptFraction > 0 && healInterruptFraction <= 1
            && armorLayers > 0 && armorLayers <= 100 && shieldHealth > 0 && shieldSeconds > 0 && staggerSeconds > 0;
    }

    public struct DamageResult
    {
        public float bodyDamage;
        public bool blocked, killed, shieldBroken, interrupted;
    }

    // Reused by the pooled view. No object allocation per hit, no Unity dependencies.
    public sealed class EnemyVitals
    {
        public float Health { get; private set; }
        public float MaxHealth { get; private set; }
        public float Shield { get; private set; }
        public float MaxShield { get; private set; }
        public int Armor { get; private set; }
        public float StaggerLeft { get; private set; }
        public bool Healing { get; private set; }
        private float shieldLeft, healLeft, healRate, interruptAt, received, staggerDuration;
        public void Reset(float health)
        {
            Health = MaxHealth = Math.Max(1, health); Shield = MaxShield = shieldLeft = 0; Armor = 0;
            StaggerLeft = healLeft = received = 0; Healing = false;
        }
        public void RaiseShield(float hp, float duration, float stagger)
        { Shield = MaxShield = hp; shieldLeft = duration; staggerDuration = stagger; }
        public void RaiseArmor(int layers) { Armor = Math.Max(Armor, layers); }
        public void BeginHeal(float duration, float fractionPerSecond, float interruptFraction, float stagger)
        {
            Healing = true; healLeft = duration; healRate = fractionPerSecond * MaxHealth;
            interruptAt = interruptFraction * MaxHealth; received = 0; staggerDuration = stagger;
        }
        public void Step(float dt)
        {
            if (dt <= 0) return;
            StaggerLeft = Math.Max(0, StaggerLeft - dt);
            if (Shield > 0) { shieldLeft -= dt; if (shieldLeft <= 0) Shield = 0; }
            if (!Healing || Health <= 0) return;
            Health = Math.Min(MaxHealth, Health + Math.Min(healLeft, dt) * healRate);
            healLeft = Math.Max(0, healLeft - dt); if (healLeft == 0) Healing = false;
        }
        public DamageResult Receive(float damage, DamageSource source)
        {
            var result = new DamageResult();
            if (Health <= 0 || damage <= 0) return result;
            if (Shield > 0)
            {
                Shield = Math.Max(0, Shield - damage); result.blocked = true;
                if (Shield == 0) { result.shieldBroken = true; StaggerLeft = staggerDuration; Healing = false; }
                return result;
            }
            if (Armor > 0)
            { if (source == DamageSource.Arrow) Armor--; result.blocked = true; return result; }
            result.bodyDamage = Math.Min(Health, damage); Health -= result.bodyDamage;
            if (Healing)
            {
                received += result.bodyDamage;
                if (received >= interruptAt) { Healing = false; StaggerLeft = staggerDuration; result.interrupted = true; }
            }
            result.killed = Health <= 0; if (result.killed) Healing = false;
            return result;
        }
    }
}
