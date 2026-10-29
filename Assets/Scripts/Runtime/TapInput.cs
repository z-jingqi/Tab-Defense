using UnityEngine;

namespace TapDefense
{
    public interface ITapInput { bool TryGetTap(out Vector2 position); }

    public sealed class UnityTapInput : ITapInput
    {
        public bool TryGetTap(out Vector2 position)
        {
            position = default;
            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                if (touch.phase != TouchPhase.Began) return false;
                position = touch.position; return true;
            }
            if (Input.GetMouseButtonDown(0)) { position = Input.mousePosition; return true; }
            return false;
        }
    }
}
