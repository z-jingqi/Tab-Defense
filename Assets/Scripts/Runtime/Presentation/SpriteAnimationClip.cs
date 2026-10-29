using System;
using UnityEngine;

namespace TapDefense
{
    /// <summary>Ordered imported sprites; independent of render pipeline and gameplay timing.</summary>
    [CreateAssetMenu(menuName = "Tap Defense/Presentation/Sprite Animation Clip")]
    public sealed class SpriteAnimationClip : ScriptableObject
    {
        public Sprite[] frames = Array.Empty<Sprite>();
        [Min(0.01f)] public float framesPerSecond = 12f;
        public bool loop = true;

        public double Duration => frames == null || framesPerSecond <= 0
            ? 0 : frames.Length / (double)framesPerSecond;

        public string ValidateData()
        {
            if (frames == null || frames.Length == 0) return "Sprite animation requires at least one frame.";
            if (float.IsNaN(framesPerSecond) || float.IsInfinity(framesPerSecond) || framesPerSecond <= 0)
                return "Sprite animation frame rate must be finite and positive.";
            foreach (var frame in frames)
                if (!frame) return "Sprite animation frames cannot be empty.";
            return null;
        }

        public int FrameIndexAt(double seconds)
        {
            if (frames == null || frames.Length == 0 || float.IsNaN(framesPerSecond)
                || float.IsInfinity(framesPerSecond) || framesPerSecond <= 0) return -1;
            if (double.IsNaN(seconds) || seconds <= 0) return 0;
            if (double.IsPositiveInfinity(seconds)) return loop ? 0 : frames.Length - 1;
            // Bound before the integer conversion, even for very long sessions.
            double frame = Math.Floor((loop ? seconds % Duration : Math.Min(seconds, Duration)) * framesPerSecond);
            return Math.Min(frames.Length - 1, (int)frame);
        }
    }
}
