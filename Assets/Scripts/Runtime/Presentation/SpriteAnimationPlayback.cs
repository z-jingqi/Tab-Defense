using System;
using UnityEngine;

namespace TapDefense
{
    /// <summary>Manually advanced so pause, slow and pooling use the game's clock, not Update.</summary>
    public sealed class SpriteAnimationPlayback
    {
        private SpriteAnimationClip clip;
        private Sprite fallback;
        private double elapsed;

        public bool HasAnimation => clip;
        public Sprite Current
        {
            get
            {
                if (!clip) return fallback;
                int index = clip.FrameIndexAt(elapsed);
                return index >= 0 && clip.frames[index] ? clip.frames[index] : fallback;
            }
        }

        public void Reset(SpriteAnimationClip animation, Sprite staticSprite)
        {
            clip = animation && animation.ValidateData() == null ? animation : null;
            fallback = staticSprite;
            elapsed = 0;
        }

        public Sprite Step(float deltaTime)
        {
            if (clip && deltaTime > 0 && !float.IsNaN(deltaTime) && !float.IsInfinity(deltaTime))
            {
                double duration = clip.Duration;
                if (duration > 0)
                {
                    elapsed += deltaTime;
                    elapsed = clip.loop ? elapsed % duration : Math.Min(elapsed, duration);
                }
            }
            return Current;
        }
    }
}
