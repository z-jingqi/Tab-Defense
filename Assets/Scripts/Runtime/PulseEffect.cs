using UnityEngine;

namespace TapDefense
{
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class PulseEffect : MonoBehaviour
    {
        private SpriteRenderer body;
        private Color tint;
        private float age, life, from, to;
        private Vector2 velocity;
        public void Play(Sprite sprite, Vector2 position, Color color, float start, float end, float duration, Vector2 movement)
        {
            if (!body) body = GetComponent<SpriteRenderer>();
            body.sprite = sprite; body.color = color; tint = color;
            transform.position = position; transform.rotation = Quaternion.identity;
            transform.localScale = Vector3.one * start;
            from = start; to = end; life = Mathf.Max(0.01f, duration); age = 0; velocity = movement;
        }
        public bool Step(float dt)
        {
            age += dt; float t = Mathf.Clamp01(age / life);
            transform.position += (Vector3)(velocity * dt);
            transform.localScale = Vector3.one * Mathf.Lerp(from, to, 1 - (1 - t) * (1 - t));
            body.color = new Color(tint.r, tint.g, tint.b, tint.a * (1 - t));
            return age < life;
        }
    }
}
