using System;
using UnityEngine;
using TapDefense.Core;

namespace TapDefense
{
    [Serializable]
    public sealed class EnemyDefinition
    {
        public EnemyKind kind;
        public float health = 18f;
        public float speed = 0.65f;
        public float contactDamage = 5f;
        public int experience = 1;
        public float radius = 0.18f;
        public EnemyRank rank;
        public RouteDefinition route = new RouteDefinition();
        [Min(0)] public float availableAfterSeconds;
        [Min(0)] public int spawnWeight = 65;
        [Range(0,1)] public float controlResistance;
        public Color color = Color.white;
        public Sprite sprite;
        [Tooltip("Optional movement frames. Leave empty to use the static placeholder sprite.")]
        public SpriteAnimationClip movementAnimation;
    }

    [CreateAssetMenu(menuName = "Tap Defense/Game Config")]
    public sealed class GameConfig : ScriptableObject
    {
        public RunParameters progression = new RunParameters();
        public PresentationSettings presentation = new PresentationSettings();
        public ElementDefinition[] elements = ElementDefinition.Defaults();
        [Min(1)] public int pendingBurstLimit = 24;
        public Sprite arrowSprite;
        public SpawnSettings spawning = new SpawnSettings();
        public float bulletSpeed = 11f;
        public float bulletRadius = 0.13f;
        public int bulletPoolSize = 96;
        public float difficultyGrowthPerMinute = 0.15f;
        public EnemyDefinition[] enemies;
        public BossSettings bosses = new BossSettings();
        public BossProfile[] bossProfiles = BossProfile.Defaults();
        [Header("Arena / performance")]
        [Min(1f)] public float arenaHalfWidth = 4.5f;
        [Min(1f)] public float arenaHalfHeight = 6.4f;
        [Min(0.1f)] public float crystalRadius = 0.48f;
        [Min(1)] public int enemyPoolSize = 180;
        [Min(1)] public int effectPoolSize = 96;
        [Min(1)] public int numberPoolSize = 48;
        [Min(1)] public int chainBlastsPerFrame = 12;
        [Min(1)] public int chainQueueLimit = 256;
        [Min(0.1f)] public float bossAttackInterval = 1f;
        [Range(0f, 0.15f)] public float screenShake = 0.045f;
        public Sprite circleSprite;
        public Sprite ringSprite;
        public Sprite diamondSprite;
        public Sprite squareSprite;

        public EnemyDefinition Enemy(EnemyKind kind)
        {
            foreach (var enemy in enemies) if (enemy.kind == kind) return enemy;
            throw new InvalidOperationException("Missing enemy definition: " + kind);
        }

        public bool PickEnemy(float seconds, System.Random random, out EnemyKind kind)
        {
            int total = 0; kind = EnemyKind.Normal;
            foreach (var enemy in enemies)
                if (enemy.kind != EnemyKind.Boss && seconds >= enemy.availableAfterSeconds) total += enemy.spawnWeight;
            if (total <= 0) return false;
            int pick = random.Next(total);
            foreach (var enemy in enemies)
            {
                if (enemy.kind == EnemyKind.Boss || seconds < enemy.availableAfterSeconds) continue;
                pick -= enemy.spawnWeight; if (pick < 0) { kind = enemy.kind; return true; }
            }
            return false;
        }

        public string ValidateData()
        {
            if (presentation == null || !presentation.IsValid) return "Invalid presentation settings.";
            if (progression == null || spawning == null || spawning.healthMultiplier <= 0 || spawning.speedMultiplier <= 0 || spawning.spawnInterval <= 0 || spawning.maxAlive <= 0 || spawning.maxAlive > enemyPoolSize || bulletSpeed <= 0 || bulletRadius <= 0 || bulletPoolSize < 1 || difficultyGrowthPerMinute < 0) return "Invalid survival settings.";
            if (pendingBurstLimit < 1 || !arrowSprite || elements == null || elements.Length != 4) return "Invalid arrow/element configuration.";
            var elementKinds = new bool[4];
            foreach (var element in elements)
            {
                int i = element == null ? -1 : (int)element.kind;
                if (i < 0 || i >= 4 || elementKinds[i]) return "Element kinds must be unique.";
                elementKinds[i] = true;
                if (element.capacity < 1 || element.rechargeSeconds <= 0 || element.radius <= 0 || element.damageFraction < 0 || element.duration <= 0 || element.burnDamagePerSecond < 0 || element.lightningTargets < 1 || element.lightningTargets > 8 || element.slowMultiplier <= 0 || element.slowMultiplier > 1 || element.freezeHits < 1 || element.freezeSeconds < 0 || element.pullSpeed < 0) return "Invalid element settings.";
            }
            if (progression.maxArrows < 1 || progression.maxArrows > 9 || progression.maxBursts < 1 || progression.maxBursts > 9 || progression.maxPierce < 0 || progression.maxPierce > 16 || progression.burstInterval <= 0 || progression.arrowSpacing < 0 || progression.sideArrowDamage <= 0 || progression.criticalChance < 0 || progression.criticalChance > 1 || progression.criticalMultiplier < 1 || progression.criticalChanceGain <= 0 || progression.criticalDamageGain <= 0 || bulletPoolSize < progression.maxArrows) return "Invalid arrow upgrades.";
            if (enemies == null || enemies.Length != 5) return "Five enemy definitions are required.";
            var seen = new bool[5];
            foreach (var enemy in enemies)
            {
                if (enemy == null || (int)enemy.kind < 0 || (int)enemy.kind >= 5 || seen[(int)enemy.kind]) return "Enemy kinds must be unique.";
                seen[(int)enemy.kind] = true;
                if (enemy.route == null || !enemy.route.IsValid || enemy.availableAfterSeconds < 0 || enemy.spawnWeight < 0 || enemy.controlResistance < 0 || enemy.controlResistance > 1 || (int)enemy.rank < 0 || (int)enemy.rank > 2 || (enemy.kind == EnemyKind.Boss) != (enemy.rank == EnemyRank.Boss)) return "Invalid enemy behavior.";
                if (enemy.movementAnimation && enemy.movementAnimation.ValidateData() != null)
                    return "Invalid movement animation for " + enemy.kind + ": " + enemy.movementAnimation.ValidateData();
                if (enemy.health <= 0 || enemy.speed <= 0 || enemy.radius <= 0 || enemy.contactDamage <= 0 || enemy.experience < 0 || !enemy.sprite) return "Invalid enemy attributes.";
            }
            bool openingEnemy = false;
            foreach (var enemy in enemies) if (enemy.kind != EnemyKind.Boss && enemy.availableAfterSeconds == 0 && enemy.spawnWeight > 0) openingEnemy = true;
            if (!openingEnemy) return "An opening enemy is required.";
            if (bosses == null || !bosses.IsValid || (int)bosses.summonKind < 0 || (int)bosses.summonKind > 4 || bossProfiles == null || bossProfiles.Length == 0) return "Invalid boss configuration.";
            foreach (var profile in bossProfiles)
            {
                if (profile == null || string.IsNullOrEmpty(profile.title) || profile.skills == null || profile.skills.Length == 0) return "Empty boss profile.";
                foreach (var skill in profile.skills) if ((int)skill < 0 || (int)skill > 4) return "Invalid boss skill.";
            }
            if (progression.damage <= 0 || progression.radius <= 0 || progression.playerHealth <= 0 || progression.minimumCooldown <= 0 || progression.cooldown < progression.minimumCooldown || progression.maximumRadius < progression.radius || progression.firstUpgradeCost <= 0 || progression.upgradeCostGrowth < 0) return "Invalid progression settings.";
            if (progression.damageGain <= 0 || progression.radiusGain <= 0 || progression.cooldownMultiplier <= 0 || progression.cooldownMultiplier >= 1 || progression.maxHealthGain <= 0 || progression.healFraction <= 0 || progression.healFraction > 1 || progression.chainMaxDepth < 1 || progression.chainRadius <= 0 || progression.chainDamageFraction <= 0 || progression.chainRadiusGain < 0 || progression.chainDamageGain < 0) return "Invalid upgrade settings.";
            if (effectPoolSize < 1 || numberPoolSize < 1 || chainBlastsPerFrame < 1 || chainQueueLimit < 1 || bossAttackInterval <= 0 || arenaHalfWidth <= 0 || arenaHalfHeight <= 0 || crystalRadius <= 0 || !circleSprite || !ringSprite || !diamondSprite || !squareSprite) return "Invalid arena, pool or sprite settings.";
            return null;
        }
    }
}
