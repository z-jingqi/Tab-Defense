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
