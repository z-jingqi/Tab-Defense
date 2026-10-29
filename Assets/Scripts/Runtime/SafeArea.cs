using UnityEngine;

namespace TapDefense
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeArea : MonoBehaviour
    {
        private Rect previous;
        private Vector2 size;
        private void OnEnable() { Apply(); }
        private void Update()
        {
            if (Screen.safeArea != previous || size.x != Screen.width || size.y != Screen.height) Apply();
        }
        private void Apply()
        {
            if (Screen.width <= 0 || Screen.height <= 0) return;
            previous = Screen.safeArea; size = new Vector2(Screen.width, Screen.height);
            RectTransform rect = (RectTransform)transform;
            rect.anchorMin = previous.position / size;
            rect.anchorMax = (previous.position + previous.size) / size;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
