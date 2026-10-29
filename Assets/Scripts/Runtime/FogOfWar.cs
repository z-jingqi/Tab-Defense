using UnityEngine;
using TapDefense.Core;

namespace TapDefense
{
    // One opaque world-space cover above all combat renderers, below the screen-space HUD.
    public sealed class FogOfWar : MonoBehaviour
    {
        private Camera worldCamera;
        private SpriteRenderer cover;
        private FogRegion region;
        private float remaining;
        public bool Active => remaining > 0;
        public FogRegion Region => region;
        public float Remaining => remaining;
        public void Initialize(Camera camera, Sprite square)
        {
            worldCamera = camera;
            var go = new GameObject("Single Opaque Cloud"); go.transform.SetParent(transform, false);
            cover = go.AddComponent<SpriteRenderer>(); cover.sprite = square; cover.sortingOrder = 1000;
            cover.color = new Color(0.24f,0.29f,0.35f,1); cover.enabled = false;
        }
        public bool TryShow(float seconds, float centerX, float width, float height)
        {
            if (Active || seconds <= 0) return false;
            region = FogRegion.Create(centerX,width,height); remaining = seconds; cover.enabled = true; Fit(); return true;
        }
        public bool IsHidden(Vector2 point, float radius = 0)
        {
            if (!Active || !worldCamera) return false;
            Vector3 viewport = worldCamera.WorldToViewportPoint(point);
            float worldHeight = worldCamera.orthographicSize * 2;
            return region.Contains(viewport.x,viewport.y,radius/(worldHeight*worldCamera.aspect),radius/worldHeight);
        }
        public void Step(float dt)
        { if (Active) { remaining = Mathf.Max(0,remaining-dt); if (!Active) cover.enabled = false; } }
        public void Clear() { remaining = 0; if (cover) cover.enabled = false; }
        private void LateUpdate() { if (Active) Fit(); }
        private void Fit()
        {
            float z = -worldCamera.transform.position.z;
            Vector3 min = worldCamera.ViewportToWorldPoint(new Vector3(region.left,region.bottom,z));
            Vector3 max = worldCamera.ViewportToWorldPoint(new Vector3(region.left+region.width,1,z));
            cover.transform.position = (min+max)*0.5f;
            cover.transform.localScale = new Vector3(max.x-min.x,max.y-min.y,1);
        }
    }
}
