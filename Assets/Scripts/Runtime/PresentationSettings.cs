using System;
using UnityEngine;

namespace TapDefense
{
    // Cosmetic budgets never change enemy counts, attack timing or damage rules.
    [Serializable]
    public sealed class PresentationBudget
    {
        [Range(30, 120)] public int targetFrameRate = 60;
        [Min(1)] public int effectPoolLimit = 96;
        [Min(1)] public int damageNumberPoolLimit = 48;
        [Range(0, 1)] public float screenShakeMultiplier = 1;

        public bool IsValid => targetFrameRate >= 30 && targetFrameRate <= 120
            && effectPoolLimit > 0 && damageNumberPoolLimit > 0
            && !float.IsNaN(screenShakeMultiplier)
            && screenShakeMultiplier >= 0 && screenShakeMultiplier <= 1;
    }

    [Serializable]
    public sealed class PresentationSettings
    {
        public PresentationBudget native = new PresentationBudget();
        public PresentationBudget web = new PresentationBudget
        {
            targetFrameRate = 30,
            effectPoolLimit = 48,
            damageNumberPoolLimit = 24,
            screenShakeMultiplier = 0.6f
        };

        public bool IsValid => native != null && web != null && native.IsValid && web.IsValid;
        public PresentationBudget Select(bool webPlayer) => webPlayer ? web : native;

        public PresentationBudget ForCurrentPlayer()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return Select(true);
#else
            return Select(false);
#endif
        }
    }
}
