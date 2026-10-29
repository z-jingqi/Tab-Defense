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
                default: return new Color(0.9f, 0.92f, 0.86f);
            }
        }
    }

    public sealed class BulletSystem
    {
        private sealed class Bullet
        {
            public SpriteRenderer renderer;
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
                renderer.sortingOrder = 15; go.SetActive(false);
                free.Push(new Bullet { renderer = renderer, hitIds = new int[config.progression.maxPierce + 1] });
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
                    bullet.renderer.gameObject.SetActive(false); free.Push(bullet); active.RemoveAt(i); continue;
                }
                bullet.position = next; bullet.renderer.transform.position = next;
            }
        }
        public void Clear()
        {
            foreach (var bullet in active) { bullet.renderer.gameObject.SetActive(false); free.Push(bullet); }
            active.Clear(); foreach (var burst in bursts) freeBursts.Push(burst); bursts.Clear();
        }
    }
}
