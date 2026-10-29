using UnityEngine;

namespace TapDefense
{
    [RequireComponent(typeof(TextMesh))]
    public sealed class DamageNumber : MonoBehaviour
    {
        private TextMesh label;
        private float age;
        private Color tint;
        public void Play(Vector2 position, float damage, Color color, Font font)
        {
            if (!label) label = GetComponent<TextMesh>();
            label.font = font; label.fontSize = 48; label.characterSize = 0.048f;
            label.anchor = TextAnchor.MiddleCenter; label.alignment = TextAlignment.Center;
            label.text = Mathf.CeilToInt(damage).ToString(); label.color = color; tint = color;
            var renderer = GetComponent<MeshRenderer>();
            renderer.sharedMaterial = font.material; renderer.sortingOrder = 30;
            transform.position = position + new Vector2(Random.Range(-0.14f, 0.14f), 0.12f);
            transform.localScale = Vector3.one; age = 0;
        }
        public bool Step(float dt)
        {
            age += dt;
            transform.position += Vector3.up * dt * 0.8f;
            label.color = new Color(tint.r, tint.g, tint.b, Mathf.Clamp01(1 - age / 0.55f));
            return age < 0.55f;
        }
    }
}
