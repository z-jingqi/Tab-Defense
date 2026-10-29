using System;
using System.Collections.Generic;

namespace TapDefense.Core
{
    public enum RunState { Ready, Combat, Upgrade, Lost }
    public enum EnemyKind { Normal, Fast, Tank, Boss, Charger }
    public enum UpgradeKind { Damage, Radius, Cooldown, Chain, MaxHealth, Heal,
        MultiShot, Pierce, Burst, CriticalChance, CriticalDamage }

    [Serializable]
    public sealed class RunParameters
    {
        public float damage = 28f;
        public float radius = 1.05f;
        public float cooldown = 0.32f;
        public float minimumCooldown = 0.10f;
        public float maximumRadius = 2.8f;
        public float playerHealth = 120f;
        public int firstUpgradeCost = 12;
        public int upgradeCostGrowth = 6;
        public float damageGain = 0.28f;
        public float radiusGain = 0.13f;
        public float cooldownMultiplier = 0.9f;
        public float maxHealthGain = 25f;
        public float healFraction = 0.35f;
        public float chainRadius = 0.7f;
        public float chainDamageFraction = 0.48f;
        public float chainRadiusGain = 0.09f;
        public float chainDamageGain = 0.12f;
        public int chainMaxDepth = 3;
        public float criticalChance = 0.05f;
        public float criticalMultiplier = 1.5f;
        public float criticalChanceGain = 0.05f;
        public float criticalDamageGain = 0.25f;
        public int maxArrows = 3;
        public int maxBursts = 3;
        public int maxPierce = 3;
        public float burstInterval = 0.1f;
        public float arrowSpacing = 0.35f;
        public float sideArrowDamage = 0.65f;
    }

    [Serializable]
    public sealed class SpawnSettings
    {
        public float spawnInterval = 0.65f;
        public int maxAlive = 150;
        public float healthMultiplier = 1f;
        public float speedMultiplier = 1f;

    }

    // Engine-free run rules. Rendering, input, spawning and platform services live elsewhere.
    public sealed class RunModel
    {
        public RunState State { get; private set; } = RunState.Ready;
        public int Level { get; private set; }
        public int PendingUpgrades { get; private set; }
        public int ExperienceBank { get; private set; }
        public int TotalExperience { get; private set; }
        public int Kills { get; private set; }
        public float Health { get; private set; }
        public float MaxHealth { get; private set; }
        public float Damage { get; private set; }
        public float Radius { get; private set; }
        public float Cooldown { get; private set; }
        public int ChainStacks { get; private set; }
        public int Arrows { get; private set; } = 1;
        public int Bursts { get; private set; } = 1;
        public int Pierce { get; private set; }
        public float CriticalChance { get; private set; }
        public float CriticalMultiplier { get; private set; }
        public float ChainRadius => parameters.chainRadius + Math.Max(0, ChainStacks - 1) * parameters.chainRadiusGain;
        public float ChainDamage => Damage * (parameters.chainDamageFraction + Math.Max(0, ChainStacks - 1) * parameters.chainDamageGain);
        public int ChainMaxDepth => Math.Max(0, parameters.chainMaxDepth);
        public int NextUpgradeCost => Math.Max(1, parameters.firstUpgradeCost + Level * parameters.upgradeCostGrowth);
        private readonly RunParameters parameters;

        public RunModel(RunParameters parameters)
        {
            this.parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
            MaxHealth = Health = Math.Max(1, parameters.playerHealth);
            Damage = Math.Max(1, parameters.damage);
            Radius = Math.Max(0.1f, parameters.radius);
            Cooldown = Math.Max(parameters.minimumCooldown, parameters.cooldown);
            CriticalChance = parameters.criticalChance; CriticalMultiplier = parameters.criticalMultiplier;
        }

        public ArrowAttack CaptureAttack(float bulletRadius)
        {
            return new ArrowAttack { damage = Damage, criticalChance = CriticalChance, criticalMultiplier = CriticalMultiplier,
                radius = bulletRadius * Radius / parameters.radius, spacing = parameters.arrowSpacing,
                sideDamage = parameters.sideArrowDamage, burstInterval = parameters.burstInterval,
                arrows = Arrows, bursts = Bursts, pierce = Pierce };
        }

        public bool StartRun()
        {
            if (State != RunState.Ready) return false;
            State = RunState.Combat; return true;
        }

        public void RecordKill(int experience)
        {
            if (State != RunState.Combat) return;
            Kills++;
            int value = Math.Max(0, experience);
            ExperienceBank += value; TotalExperience += value;
        }

        public void DamagePlayer(float damage)
        {
            if (State != RunState.Combat) return;
            Health = Math.Max(0f, Health - Math.Max(0f, damage));
            if (Health <= 0f) State = RunState.Lost;
        }

        public bool SettleExperience()
        {
            if (State != RunState.Combat) return false;
            while (ExperienceBank >= NextUpgradeCost)
            { ExperienceBank -= NextUpgradeCost; Level++; PendingUpgrades++; }
            if (PendingUpgrades == 0) return false;
            State = RunState.Upgrade; return true;
        }

        public UpgradeKind[] GetChoices(Random random)
        {
            if (State != RunState.Upgrade) return Array.Empty<UpgradeKind>();
            var candidates = new List<UpgradeKind> { UpgradeKind.Damage, UpgradeKind.Chain, UpgradeKind.MaxHealth };
            if (Radius < parameters.maximumRadius - 0.001f) candidates.Add(UpgradeKind.Radius);
            if (Cooldown > parameters.minimumCooldown + 0.001f) candidates.Add(UpgradeKind.Cooldown);
            if (Health < MaxHealth - 0.001f) candidates.Add(UpgradeKind.Heal);
            if (Arrows < parameters.maxArrows) candidates.Add(UpgradeKind.MultiShot);
            if (Bursts < parameters.maxBursts) candidates.Add(UpgradeKind.Burst);
            if (Pierce < parameters.maxPierce) candidates.Add(UpgradeKind.Pierce);
            if (CriticalChance < 1f) candidates.Add(UpgradeKind.CriticalChance);
            candidates.Add(UpgradeKind.CriticalDamage);
            for (int i = candidates.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                UpgradeKind temporary = candidates[i]; candidates[i] = candidates[j]; candidates[j] = temporary;
            }
            var choices = new[] { candidates[0], candidates[1], candidates[2] };
            return choices;
        }

        public bool ApplyUpgrade(UpgradeKind kind)
        {
            if (State != RunState.Upgrade || PendingUpgrades <= 0) return false;
            switch (kind)
            {
                case UpgradeKind.Damage: Damage *= 1f + parameters.damageGain; break;
                case UpgradeKind.Radius: Radius = Math.Min(parameters.maximumRadius, Radius + parameters.radiusGain); break;
                case UpgradeKind.Cooldown: Cooldown = Math.Max(parameters.minimumCooldown, Cooldown * parameters.cooldownMultiplier); break;
                case UpgradeKind.Chain: ChainStacks++; break;
                case UpgradeKind.MaxHealth: MaxHealth += parameters.maxHealthGain; Health += parameters.maxHealthGain; break;
                case UpgradeKind.Heal: Health = Math.Min(MaxHealth, Health + MaxHealth * parameters.healFraction); break;
                case UpgradeKind.MultiShot: if (Arrows >= parameters.maxArrows) return false; Arrows++; break;
                case UpgradeKind.Burst: if (Bursts >= parameters.maxBursts) return false; Bursts++; break;
                case UpgradeKind.Pierce: if (Pierce >= parameters.maxPierce) return false; Pierce++; break;
                case UpgradeKind.CriticalChance: CriticalChance = Math.Min(1, CriticalChance + parameters.criticalChanceGain); break;
                case UpgradeKind.CriticalDamage: CriticalMultiplier += parameters.criticalDamageGain; break;
                default: return false;
            }
            PendingUpgrades--;
            State = PendingUpgrades > 0 ? RunState.Upgrade : RunState.Combat;
            return true;
        }
    }
}
