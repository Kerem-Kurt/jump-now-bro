using JumpNowBro.Util;

namespace JumpNowBro.Tests.CollisionWorlds
{
    /// Flat ground at y=0 plus a horizontal ceiling at y=4. Player head at y+1 stops at ceiling.
    internal sealed class FakeCeilingAbove : ICollisionWorld
    {
        const float ceilingY = 4f;
        const float halfHeight = 1f;
        const float kSkin = 0.01f;

        public bool Grounded(float x, float y) => (y - 1f) <= kSkin;

        public void SweepX(float x, float y, float dx, out float resolvedDx, out bool blocked)
        {
            resolvedDx = dx; blocked = false;
        }

        public void SweepY(float x, float y, float dy, out float resolvedDy, out bool blocked)
        {
            if (dy == 0f) { resolvedDy = 0f; blocked = false; return; }
            if (dy < 0f)
            {
                float newCenter = y + dy;
                if (newCenter < 1f) { resolvedDy = 1f - y; blocked = true; }
                else                { resolvedDy = dy;   blocked = false; }
                return;
            }
            // dy > 0: clamp head at ceilingY
            float maxDy = ceilingY - (y + halfHeight);
            if (maxDy <= 0f)    { resolvedDy = 0f;    blocked = true;  }
            else if (dy > maxDy){ resolvedDy = maxDy; blocked = true;  }
            else                { resolvedDy = dy;   blocked = false; }
        }
    }
}
