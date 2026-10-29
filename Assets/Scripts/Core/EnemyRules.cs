using System;

namespace TapDefense.Core
{
    public enum EnemyRank { Normal, Elite, Boss }
    public enum RouteKind { Straight, Sine, StopGo, Dash }

    [Serializable]
    public sealed class RouteDefinition
    {
        public RouteKind kind;
        public float amplitude = 0.65f;
        public float period = 2.4f;
        public float moveSeconds = 1.6f;
        public float stopSeconds = 0.65f;
        public float cruiseSeconds = 2f;
        public float windupSeconds = 0.7f;
        public float dashSeconds = 0.45f;
        public float dashMultiplier = 3.2f;
        public bool IsValid => amplitude >= 0 && period > 0 && moveSeconds > 0 && stopSeconds >= 0
            && cruiseSeconds > 0 && windupSeconds > 0 && dashSeconds > 0 && dashMultiplier >= 1
            && (int)kind >= 0 && (int)kind <= 3;
    }

    public struct RouteStep
    {
        public float horizontal, verticalMultiplier;
        public bool warning;
    }

    // Route time is advanced by effective movement time, so freezes also freeze the telegraph.
    public static class EnemyRoutes
    {
        public static RouteStep Advance(RouteDefinition route, ref float clock, float dt)
        {
            float before = clock; clock += Math.Max(0, dt);
            var result = new RouteStep { verticalMultiplier = 1 };
            switch (route.kind)
            {
                case RouteKind.Sine:
                    result.horizontal = route.amplitude * ((float)Math.Sin(clock * Math.PI * 2 / route.period)
                        - (float)Math.Sin(before * Math.PI * 2 / route.period)); break;
                case RouteKind.StopGo:
                    result.verticalMultiplier = clock % (route.moveSeconds + route.stopSeconds) < route.moveSeconds ? 1 : 0; break;
                case RouteKind.Dash:
                    float phase = clock % (route.cruiseSeconds + route.windupSeconds + route.dashSeconds);
                    result.warning = phase >= route.cruiseSeconds && phase < route.cruiseSeconds + route.windupSeconds;
                    result.verticalMultiplier = result.warning ? 0 : phase < route.cruiseSeconds ? 1 : route.dashMultiplier; break;
            }
            return result;
        }
    }

    // Normalized viewport bounds, independent of camera aspect or screen safe area.
    public struct FogRegion
    {
        public float left, bottom, width, height;
        public static FogRegion Create(float centerX, float requestedWidth, float requestedHeight)
        {
            float width = Math.Max(0.05f, Math.Min(0.95f, requestedWidth));
            float height = Math.Max(0.01f, Math.Min(1f / 3f, requestedHeight));
            return new FogRegion { left = Math.Max(0, Math.Min(1 - width, centerX - width / 2)),
                bottom = 1 - height, width = width, height = height };
        }
        public bool Contains(float x, float y, float marginX = 0, float marginY = 0)
        { return x + marginX >= left && x - marginX <= left + width && y + marginY >= bottom && y - marginY <= 1; }
    }

    public sealed class BossSchedule
    {
        private float wait;
        private bool wasAlive;
        public BossSchedule(float firstDelay) { Reset(firstDelay); }
        public void Reset(float firstDelay) { wait = firstDelay; wasAlive = false; }
        public bool Step(float dt, bool alive, float repeatDelay)
        {
            if (alive) { wasAlive = true; return false; }
            if (wasAlive) { wait = repeatDelay; wasAlive = false; }
            wait -= Math.Max(0, dt); return wait <= 0;
        }
        // Pool exhaustion retries next frame, never queues multiple bosses.
        public void Spawned() { wasAlive = true; }
    }
}
