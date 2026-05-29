using JumpNowBro.Util;

namespace JumpNowBro.Tests.CollisionWorlds
{
    /// Infinite flat ground at world y=0. Player BoxCollider is 1×2 centered at (x, y),
    /// so feet are at y-1; resting on the floor means center y=1.
    internal sealed class FakeFlatGroundWorld : ICollisionWorld
    {
        const float kSkin = 0.01f;

        public bool Grounded(float x, float y) => (y - 1f) <= kSkin;

        public void SweepX(float x, float y, float dx, out float resolvedDx, out bool blocked)
        {
            resolvedDx = dx; blocked = false;
        }

        public void SweepY(float x, float y, float dy, out float resolvedDy, out bool blocked)
        {
            if (dy >= 0f) { resolvedDy = dy; blocked = false; return; }
            // moving down: clamp so feet land at y=0 (center at y=1)
            float newCenter = y + dy;
            if (newCenter < 1f) { resolvedDy = 1f - y; blocked = true; }
            else                { resolvedDy = dy;   blocked = false; }
        }
    }
}
