using NUnit.Framework;
using TapDefense.Core;
using UnityEditor;
using UnityEngine;

namespace TapDefense.Tests
{
    public sealed class ArtIntegrationTests
    {
        private const string ConfigPath = "Assets/Data/GameConfig.asset";

        [Test]
        public void ApprovedArtIsAssignedAndUsesSingleSprites()
        {
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
            Assert.IsNotNull(config);
            Assert.IsNull(config.ValidateData());
            Assert.IsFalse(ShaderUtil.ShaderHasError(config.enemySpriteMaterial.shader));
            Assert.IsNull(config.ElementIcon(ElementKind.None));
            for (int i = 0; i < 4; i++) Assert.IsNotNull(config.ElementIcon((ElementKind)i));
            Sprite soldier = config.Enemy(EnemyKind.Normal).sprite;
            foreach (var enemy in config.enemies)
            {
                if (enemy.kind == EnemyKind.Boss) continue;
                Assert.AreSame(soldier, enemy.sprite);
                Assert.IsNull(enemy.movementAnimation, "A single running pose is not an animation sheet.");
                Assert.IsFalse(enemy.swayWhileMoving);
            }
            foreach (var sprite in new[] { soldier, config.arrowSprite, config.battlefieldSprite,
                config.fireIcon, config.lightningIcon, config.iceIcon, config.windIcon })
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(sprite));
                Assert.AreEqual(TextureImporterType.Sprite, importer.textureType);
                Assert.AreEqual(SpriteImportMode.Single, importer.spriteImportMode);
                Assert.IsFalse(importer.mipmapEnabled);
                Assert.IsFalse(importer.isReadable);
                Assert.AreEqual(TextureWrapMode.Clamp, importer.wrapMode);
                Assert.AreEqual(FilterMode.Bilinear, importer.filterMode);
            }
        }

        [Test]
        public void SoftHitFlashLiftsRgbWithoutChangingTintAlphaOrSharedMaterial()
        {
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
            var go = new GameObject("Hit soldier", typeof(SpriteRenderer), typeof(EnemyView));
            var other = new GameObject("Other soldier", typeof(SpriteRenderer), typeof(EnemyView));
            try
            {
                var view = go.GetComponent<EnemyView>();
                var untouched = other.GetComponent<EnemyView>();
                view.Prepare(config.squareSprite, config.enemySpriteMaterial, config.hitFlashDuration, config.hitFlashStrength);
                untouched.Prepare(config.squareSprite, config.enemySpriteMaterial, config.hitFlashDuration, config.hitFlashStrength);
                var definition = config.Enemy(EnemyKind.Normal);
                view.Spawn(definition, new SpawnSettings(), Vector2.zero, 1);
                untouched.Spawn(definition, new SpawnSettings(), Vector2.one, 2);
                var body = go.GetComponent<SpriteRenderer>();
                var properties = new MaterialPropertyBlock();
                view.Receive(1, DamageSource.Arrow);body.GetPropertyBlock(properties);
                Assert.AreEqual(config.hitFlashStrength, properties.GetFloat("_HitFlash"), 0.0001f);
                Assert.AreEqual(definition.color, body.color);
                view.Receive(1, DamageSource.Arrow);body.GetPropertyBlock(properties);
                Assert.LessOrEqual(properties.GetFloat("_HitFlash"), config.hitFlashStrength);
                other.GetComponent<SpriteRenderer>().GetPropertyBlock(properties);
                Assert.AreEqual(0, properties.GetFloat("_HitFlash"));
                Assert.AreEqual(0, config.enemySpriteMaterial.GetFloat("_HitFlash"));
                view.Step(config.hitFlashDuration / 2);body.GetPropertyBlock(properties);
                Assert.AreEqual(config.hitFlashStrength / 2, properties.GetFloat("_HitFlash"), 0.0001f);
                Assert.AreEqual(definition.color.a, body.color.a);
                view.Step(config.hitFlashDuration);body.GetPropertyBlock(properties);
                Assert.AreEqual(0, properties.GetFloat("_HitFlash"));
                view.Receive(1, DamageSource.Arrow);
                view.Spawn(definition, new SpawnSettings(), Vector2.zero, 3);body.GetPropertyBlock(properties);
                Assert.AreEqual(0, properties.GetFloat("_HitFlash"));
                Assert.AreSame(config.enemySpriteMaterial, body.sharedMaterial);
            }
            finally { Object.DestroyImmediate(go); Object.DestroyImmediate(other); }
        }

        [Test]
        public void StaticSoldierKeepsItsPoseWhenMovingFrozenAndReused()
        {
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
            var definition = config.Enemy(EnemyKind.Normal);
            var go = new GameObject("Static soldier", typeof(SpriteRenderer), typeof(EnemyView));
            try
            {
                var view = go.GetComponent<EnemyView>();
                view.Spawn(definition, new SpawnSettings(), Vector2.zero, 1);
                view.Step(0.25f);
                Assert.Less(view.Position.y, 0);
                Assert.AreEqual(Quaternion.identity, go.transform.rotation);
                Assert.AreSame(definition.sprite, go.GetComponent<SpriteRenderer>().sprite);
                var ice = ElementDefinition.Defaults()[2];
                view.Chill(ice); view.Chill(ice); view.Chill(ice);
                Vector2 frozenAt = view.Position;
                view.Step(0.1f);
                Assert.AreEqual(frozenAt, view.Position);
                Assert.AreEqual(Quaternion.identity, go.transform.rotation);
                view.Spawn(definition, new SpawnSettings(), Vector2.one, 2);
                Assert.IsFalse(view.Frozen);
                Assert.AreEqual(Quaternion.identity, go.transform.rotation);
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
