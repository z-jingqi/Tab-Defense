using System.Collections.Generic;
using UnityEngine;
using TapDefense.Core;

namespace TapDefense
{
    public static class ElementAppearance
    {
        public static Color Tint(ElementKind kind)
        {
            switch (kind)
            {
                case ElementKind.Fire: return new Color(1f, 0.42f, 0.22f);
                case ElementKind.Lightning: return new Color(1f, 0.85f, 0.25f);
                case ElementKind.Ice: return new Color(0.35f, 0.8f, 1f);
                case ElementKind.Wind: return new Color(0.45f, 1f, 0.65f);
                default: return Color.white;
            }
        }
    }

    public sealed class BulletSystem
    {
        private sealed class Bullet
        {
            public SpriteRenderer renderer;
            public LineRenderer wake;
            public float travelled;
            public Vector2 position;
            public ArrowAttack attack;
            public int hitCount;
            public int[] hitIds;
        }
        private sealed class Burst
        {
            public Vector2 point;
            public ArrowAttack attack;
            public int left;
            public float timer;
        }
        private readonly Stack<Bullet> free;
        private readonly List<Bullet> active;
        private readonly Stack<Burst> freeBursts;
        private readonly List<Burst> bursts;
        private readonly GameConfig config;
        public int Count => active.Count;
        public float Top = 10f;
        public BulletSystem(GameConfig config, Transform parent)
        {
            this.config = config;
            active = new List<Bullet>(config.bulletPoolSize); free = new Stack<Bullet>(config.bulletPoolSize);
            bursts = new List<Burst>(config.pendingBurstLimit); freeBursts = new Stack<Burst>(config.pendingBurstLimit);
            for (int i = 0; i < config.pendingBurstLimit; i++) freeBursts.Push(new Burst());
            for (int i = 0; i < config.bulletPoolSize; i++)
            {
                var go = new GameObject("Pooled Arrow"); go.transform.SetParent(parent, false);
                var renderer = go.AddComponent<SpriteRenderer>(); renderer.sprite = config.arrowSprite;
                renderer.sortingOrder = 15;
                var wakeObject = new GameObject("Air Wake"); wakeObject.transform.SetParent(go.transform, false);
                var wake = wakeObject.AddComponent<LineRenderer>();
                // Reuse Unity's built-in sprite material: no new texture or per-arrow material.
                wake.sharedMaterial = renderer.sharedMaterial;
                wake.useWorldSpace = true; wake.alignment = LineAlignment.View;
                wake.sortingOrder = 14; wake.numCapVertices = 0; wake.numCornerVertices = 0;
                wake.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                wake.receiveShadows = false;
                wake.widthCurve = AnimationCurve.Linear(0, 1, 1, 0);
                wake.startColor = new Color(0.88f, 0.96f, 1f, 0.32f);
                wake.endColor = new Color(0.88f, 0.96f, 1f, 0);
                wake.positionCount = 0; wake.enabled = false; go.SetActive(false);
                free.Push(new Bullet { renderer = renderer, wake = wake, hitIds = new int[config.progression.maxPierce + 1] });
            }
        }
        public bool Fire(Vector2 point, ArrowAttack attack)
        {
            if (free.Count < attack.arrows || (attack.bursts > 1 && freeBursts.Count == 0)) return false;
            Volley(point, attack);
            if (attack.bursts > 1)
            {
                var burst = freeBursts.Pop(); burst.point = point; burst.attack = attack;
                burst.left = attack.bursts - 1; burst.timer = attack.burstInterval; bursts.Add(burst);
            }
            return true;
        }
        private void Volley(Vector2 point, ArrowAttack attack)
        {
            for (int i = 0; i < attack.arrows; i++)
            {
                var bullet = free.Pop(); bullet.attack = attack; bullet.hitCount = 0;
                bullet.travelled = 0; bullet.wake.enabled = false; bullet.wake.positionCount = 0;
                if (i != attack.arrows / 2) bullet.attack.damage *= attack.sideDamage;
                bullet.position = point + Vector2.right * ElementBarLayout.Offset(i, attack.arrows, attack.spacing);
                bullet.renderer.color = ElementAppearance.Tint(attack.element);
                bullet.renderer.transform.position = bullet.position;
                bullet.renderer.transform.localScale = Vector3.one * (attack.radius * 2);
                bullet.renderer.gameObject.SetActive(true); active.Add(bullet);
            }
        }
        public void Step(float dt, EnemySystem enemies)
        {
            for (int i = bursts.Count - 1; i >= 0; i--)
            {
                var burst = bursts[i]; burst.timer -= dt;
                if (burst.timer > 0 || free.Count < burst.attack.arrows) continue;
                Volley(burst.point, burst.attack); burst.left--; burst.timer = burst.attack.burstInterval;
                if (burst.left == 0) { bursts.RemoveAt(i); freeBursts.Push(burst); }
            }
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var bullet = active[i]; Vector2 next = bullet.position + Vector2.up * config.bulletSpeed * dt;
                bool spent = false;
                while (enemies.HitBullet(bullet.position, next, bullet.attack, bullet.hitIds, bullet.hitCount, out int id))
                {
                    bullet.hitIds[bullet.hitCount++] = id;
                    if (bullet.hitCount > bullet.attack.pierce) { spent = true; break; }
                }
                if (spent || next.y - bullet.attack.radius > Top)
                {
                    Recycle(bullet); active.RemoveAt(i); continue;
                }
                bullet.travelled += Vector2.Distance(bullet.position, next);
                bullet.position = next; bullet.renderer.transform.position = next;
                UpdateWake(bullet);
            }
        }
        private void UpdateWake(Bullet bullet)
        {
            // A short local air streak, not a retained path: reuse never connects two shots.
            float length = Mathf.Min(bullet.attack.radius * 1.8f, bullet.travelled);
            if (length <= 0) return;
            float rear = config.arrowSprite.bounds.extents.y * bullet.renderer.transform.localScale.y;
            Vector3 head = (Vector3)bullet.position + Vector3.down * rear;
            bullet.wake.widthMultiplier = bullet.attack.radius * 0.22f;
            bullet.wake.positionCount = 3;
            bullet.wake.SetPosition(0, head);
            bullet.wake.SetPosition(1, head + Vector3.down * (length * 0.45f));
            bullet.wake.SetPosition(2, head + Vector3.down * length);
            bullet.wake.enabled = true;
        }
        private void Recycle(Bullet bullet)
        {
            bullet.wake.enabled = false; bullet.wake.positionCount = 0; bullet.travelled = 0;
            bullet.renderer.gameObject.SetActive(false); free.Push(bullet);
        }
        public void Clear()
        {
            foreach (var bullet in active) Recycle(bullet);
            active.Clear(); foreach (var burst in bursts) freeBursts.Push(burst); bursts.Clear();
        }
    }
}
