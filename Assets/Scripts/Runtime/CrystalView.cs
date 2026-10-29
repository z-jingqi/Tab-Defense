using UnityEngine;

namespace TapDefense
{
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class CrystalView : MonoBehaviour
    {
        private SpriteRenderer body;
        private float flash, phase;
        private readonly Color normal = new Color(0.36f, 1f, 0.86f);
        private void Awake() { body = GetComponent<SpriteRenderer>(); }
        public void Hit() { flash = 1; }
        public void ResetVisual() { flash = 0; phase = 0; Step(0, 1); }
        public void Step(float dt, float healthFraction)
        {
            phase += dt * 2.5f; flash = Mathf.Max(0, flash - dt * 4);
            transform.localScale = Vector3.one * (0.85f + Mathf.Sin(phase) * 0.035f + flash * 0.12f);
            Color tint = Color.Lerp(new Color(1, 0.3f, 0.45f), normal, Mathf.Clamp01(healthFraction * 2));
            body.color = Color.Lerp(tint, Color.white, flash);
            transform.rotation = Quaternion.Euler(0, 0, Mathf.Sin(phase * 0.45f) * 7);
        }
    }
}
