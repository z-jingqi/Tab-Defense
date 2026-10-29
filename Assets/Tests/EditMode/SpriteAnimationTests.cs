using NUnit.Framework;
using TapDefense.Core;
using UnityEngine;

namespace TapDefense.Tests
{
    public sealed class SpriteAnimationTests
    {
        private SpriteAnimationClip clip;
        private Sprite first, second;

        [SetUp]
        public void SetUp()
        {
            first = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f);
            second = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f);
            clip = ScriptableObject.CreateInstance<SpriteAnimationClip>();
            clip.frames = new[] { first, second };
            clip.framesPerSecond = 4;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(clip);
            Object.DestroyImmediate(first);
            Object.DestroyImmediate(second);
        }

        [Test]
        public void ClipSamplesLoopBoundariesAndLongSessions()
        {
            Assert.IsNull(clip.ValidateData());
            Assert.AreEqual(0, clip.FrameIndexAt(-1));
            Assert.AreEqual(0, clip.FrameIndexAt(0.249));
            Assert.AreEqual(1, clip.FrameIndexAt(0.25));
            Assert.AreEqual(0, clip.FrameIndexAt(0.5));
            Assert.AreEqual(1, clip.FrameIndexAt(1000000.25));
        }

        [Test]
        public void OneShotHoldsLastFrameAndResetStartsAtFirst()
        {
            clip.loop = false;
            var playback = new SpriteAnimationPlayback();
            playback.Reset(clip, first);
            Assert.AreSame(first, playback.Current);
            Assert.AreSame(second, playback.Step(0.25f));
            Assert.AreSame(second, playback.Step(100));
            playback.Reset(clip, first);
            Assert.AreSame(first, playback.Current);
            playback.Reset(null, second);
            Assert.IsFalse(playback.HasAnimation);
            Assert.AreSame(second, playback.Step(100));
        }

        [Test]
        public void PlaybackUsesOnlyExplicitFinitePositiveTime()
        {
            var playback = new SpriteAnimationPlayback();
            playback.Reset(clip, first);
            Assert.AreSame(first, playback.Step(0));
            Assert.AreSame(first, playback.Step(-1));
            Assert.AreSame(first, playback.Step(float.NaN));
            Assert.AreSame(first, playback.Step(float.PositiveInfinity));
            Assert.AreSame(second, playback.Step(0.25f));
            Assert.AreSame(second, playback.Step(0));
            Assert.AreSame(first, playback.Step(0.25f));
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void InvalidFrameRateIsRejected(float rate)
        {
            clip.framesPerSecond = rate;
            Assert.IsNotNull(clip.ValidateData());
            Assert.AreEqual(-1, clip.FrameIndexAt(1));
            var playback = new SpriteAnimationPlayback();
            playback.Reset(clip, first);
            Assert.IsFalse(playback.HasAnimation);
            Assert.AreSame(first, playback.Step(1));
        }

        [Test]
        public void MissingFramesAreRejected()
        {
            clip.frames = null;
            Assert.IsNotNull(clip.ValidateData());
            Assert.AreEqual(-1, clip.FrameIndexAt(1));
            clip.frames = new Sprite[0];
            Assert.IsNotNull(clip.ValidateData());
            clip.frames = new[] { first, (Sprite)null };
            Assert.IsNotNull(clip.ValidateData());
        }

        [Test]
        public void EnemyAnimationPausesForFreezeAndResetsForPoolReuse()
        {
            var go = new GameObject("Animated enemy", typeof(SpriteRenderer), typeof(EnemyView));
            try
            {
                var view = go.GetComponent<EnemyView>();
                var body = go.GetComponent<SpriteRenderer>();
                var definition = new EnemyDefinition { sprite = first, movementAnimation = clip };
                view.Spawn(definition, new SpawnSettings(), Vector2.zero, 1);
                view.Step(0.25f);
                Assert.AreSame(second, body.sprite);
                Assert.AreEqual(Quaternion.identity, go.transform.rotation);
                var ice = ElementDefinition.Defaults()[2];
                view.Chill(ice); view.Chill(ice); view.Chill(ice);
                Assert.IsTrue(view.Frozen);
                view.Step(0.1f);
                Assert.AreSame(second, body.sprite);
                view.Spawn(definition, new SpawnSettings(), Vector2.zero, 2);
                Assert.AreSame(first, body.sprite);
                Assert.IsFalse(view.Frozen);
                definition.movementAnimation = null;
                view.Spawn(definition, new SpawnSettings(), Vector2.zero, 3);
                view.Step(0.25f);
                Assert.AreSame(first, body.sprite);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void EnemySlowChangesAnimationRateAndStaggerPausesIt()
        {
            var go = new GameObject("Animated enemy", typeof(SpriteRenderer), typeof(EnemyView));
            try
            {
                var view = go.GetComponent<EnemyView>();
                var body = go.GetComponent<SpriteRenderer>();
                var definition = new EnemyDefinition { sprite = first, movementAnimation = clip };
                view.Spawn(definition, new SpawnSettings(), Vector2.zero, 1);
                var ice = ElementDefinition.Defaults()[2];
                ice.slowMultiplier = 0.5f;
                view.Chill(ice);
                view.Step(0.25f);
                Assert.AreSame(first, body.sprite);
                view.Step(0.25f);
                Assert.AreSame(second, body.sprite);
                view.Vitals.RaiseShield(1, 1, 1);
                view.Receive(1, DamageSource.Arrow);
                view.Step(0.25f);
                Assert.AreSame(second, body.sprite);
            }
            finally { Object.DestroyImmediate(go); }
        }
        [TestCase(RouteKind.StopGo)]
        [TestCase(RouteKind.Dash)]
        public void RouteStopOrWindupHoldsWalkingFrameEvenWhenPulled(RouteKind kind)
        {
            var go = new GameObject("Waiting enemy", typeof(SpriteRenderer), typeof(EnemyView));
            try
            {
                var view = go.GetComponent<EnemyView>();
                var body = go.GetComponent<SpriteRenderer>();
                var definition = new EnemyDefinition {
                    sprite = first, movementAnimation = clip,
                    route = new RouteDefinition { kind = kind, moveSeconds = 0.4f,
                        stopSeconds = 1, cruiseSeconds = 0.4f, windupSeconds = 1 }
                };
                view.Spawn(definition, new SpawnSettings(), Vector2.zero, 1);
                view.Step(0.25f);
                Assert.AreSame(second, body.sprite);
                Vector2 stoppedAt = view.Position;
                view.Step(0.25f);
                Assert.AreEqual(stoppedAt, view.Position);
                Assert.AreSame(second, body.sprite);
                var wind = ElementDefinition.Defaults()[3];
                view.Pull(new Vector2(2, stoppedAt.y), wind);
                view.Step(0.25f);
                Assert.Greater(view.Position.x, stoppedAt.x);
                Assert.AreSame(second, body.sprite);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void AnchoredBossHoldsWalkingFrame()
        {
            var go = new GameObject("Anchored boss", typeof(SpriteRenderer), typeof(EnemyView));
            try
            {
                var view = go.GetComponent<EnemyView>();
                var body = go.GetComponent<SpriteRenderer>();
                var definition = new EnemyDefinition {
                    kind = EnemyKind.Boss, sprite = first, movementAnimation = clip, speed = 1
                };
                view.Spawn(definition, new SpawnSettings(), new Vector2(0, 0.25f), 1, anchor: 0);
                view.Step(0.25f);
                Assert.AreEqual(0, view.Position.y);
                Assert.AreSame(second, body.sprite);
                view.Step(0.25f);
                Assert.AreSame(second, body.sprite);
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
