using System;

namespace TapDefense.Core
{
    public enum ElementKind { None = -1, Fire = 0, Lightning = 1, Ice = 2, Wind = 3 }

    [Serializable]
    public sealed class ElementDefinition
    {
        public ElementKind kind;
        public int capacity = 3;
        public float rechargeSeconds = 6f;
        public float radius = 0.8f;
        public float damageFraction = 0.5f;
        public float duration = 2f;
        public float burnDamagePerSecond = 0.18f;
        public int lightningTargets = 3;
        public float slowMultiplier = 0.5f;
        public int freezeHits = 3;
        public float freezeSeconds = 0.6f;
        public float pullSpeed = 1.6f;

        public static ElementDefinition[] Defaults()
        {
            return new[]
            {
                new ElementDefinition { kind = ElementKind.Fire, radius = 0.75f },
                new ElementDefinition { kind = ElementKind.Lightning, rechargeSeconds = 7f, radius = 1.3f, damageFraction = 0.55f },
                new ElementDefinition { kind = ElementKind.Ice, radius = 0.65f, damageFraction = 0f },
                new ElementDefinition { kind = ElementKind.Wind, rechargeSeconds = 7f, radius = 0.9f, duration = 0.75f, damageFraction = 0.2f }
            };
        }
    }

    public sealed class ElementCharge
    {
        public ElementDefinition Definition { get; }
        public bool Unlocked { get; internal set; }
        public int Count { get; internal set; }
        public float RechargeLeft { get; internal set; }
        public ElementCharge(ElementDefinition definition) { Definition = definition; }
    }

    // Charge recovery advances only when the controller advances combat time.
    public sealed class ElementInventory
    {
        private readonly ElementCharge[] slots = new ElementCharge[4];
        public ElementKind Selected { get; private set; } = ElementKind.None;
        public int UnlockedCount { get; private set; }
        public ElementInventory(ElementDefinition[] definitions)
        {
            if (definitions == null || definitions.Length != 4) throw new ArgumentException("Four elements are required.");
            foreach (var definition in definitions)
            {
                int i = definition == null ? -1 : (int)definition.kind;
                if (i < 0 || i >= 4 || slots[i] != null || definition.capacity < 1 || definition.rechargeSeconds <= 0)
                    throw new ArgumentException("Invalid or duplicate element definition.");
                slots[i] = new ElementCharge(definition);
            }
        }
        public ElementCharge Get(ElementKind kind)
        { int i = (int)kind; return i >= 0 && i < 4 ? slots[i] : null; }
        public bool Unlock(ElementKind kind)
        {
            var charge = Get(kind);
            if (charge == null || charge.Unlocked) return false;
            charge.Unlocked = true; charge.Count = charge.Definition.capacity; UnlockedCount++; return true;
        }
        public bool Select(ElementKind kind)
        {
            if (kind == ElementKind.None || kind == Selected) { Selected = ElementKind.None; return true; }
            var charge = Get(kind);
            if (charge == null || !charge.Unlocked || charge.Count == 0) return false;
            Selected = kind; return true;
        }
        public bool ConsumeSelected()
        {
            if (Selected == ElementKind.None) return true;
            var charge = Get(Selected);
            if (charge.Count <= 0) return false;
            charge.Count--;
            if (charge.RechargeLeft <= 0) charge.RechargeLeft = charge.Definition.rechargeSeconds;
            if (charge.Count == 0) Selected = ElementKind.None;
            return true;
        }
        public void Step(float dt)
        {
            if (dt <= 0) return;
            foreach (var charge in slots)
            {
                if (!charge.Unlocked || charge.Count == charge.Definition.capacity) continue;
                charge.RechargeLeft -= dt;
                while (charge.RechargeLeft <= 0 && charge.Count < charge.Definition.capacity)
                { charge.Count++; charge.RechargeLeft += charge.Definition.rechargeSeconds; }
                if (charge.Count == charge.Definition.capacity) charge.RechargeLeft = 0;
            }
        }
    }

    public static class ElementBarLayout
    {
        public static float Offset(int visibleIndex, int visibleCount, float spacing)
        { return (visibleIndex - (visibleCount - 1) * 0.5f) * spacing; }
    }

    // Immutable per-click snapshot: upgrades/switches during a burst cannot change already fired attacks.
    public struct ArrowAttack
    {
        public float damage, criticalChance, criticalMultiplier, radius, spacing, sideDamage, burstInterval;
        public int arrows, bursts, pierce;
        public ElementKind element;
    }
}
