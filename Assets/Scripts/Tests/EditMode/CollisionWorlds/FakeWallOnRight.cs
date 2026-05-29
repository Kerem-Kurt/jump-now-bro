using JumpNowBro.Util;

namespace JumpNowBro.Tests.CollisionWorlds
{
    /// Flat ground at y=0 plus a vertical wall at x=5. Player right edge at x+0.5 stops at wall.
    internal sealed class FakeWallOnRight : ICollisionWorld
    {
        const float wallX = 5f;
        const float halfWidth = 0.5f;
        const float kSkin = 0.01f;

        public bool Grounded(float x, float y) => (y - 1f) <= kSkin && (x + halfWidth) < wallX;

        public void SweepX(float x, float y, float dx, out float resolvedDx, out bool blocked)
        {
            if (dx <= 0f) { resolvedDx = dx; blocked = false; return; }
            float maxDx = wallX - (x + halfWidth);
            if (maxDx <= 0f)   { resolvedDx = 0f;   blocked = true;  }
            else if (dx > maxDx){ resolvedDx = maxDx; blocked = true; }
            else                { resolvedDx = dx;   blocked = false; }
        }

        public void SweepY(float x, float y, float dy, out float resolvedDy, out bool blocked)
        {
            if (dy >= 0f) { resolvedDy = dy; blocked = false; return; }
            float newCenter = y + dy;
            if (newCenter < 1f) { resolvedDy = 1f - y; blocked = true; }
            else                { resolvedDy = dy;   blocked = false; }
        }
    }
}
