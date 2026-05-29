using JumpNowBro.Util;

namespace JumpNowBro.Tests.CollisionWorlds
{
    /// Ground at y=0 with a one-unit gap at x ∈ [3, 4]. Used to verify that crossing a same-line
    /// gap doesn't false-block (SweepX is free; SweepY only blocks when player x is outside the gap).
    internal sealed class FakeOneTileGap : ICollisionWorld
    {
        const float gapStartX = 3f;
        const float gapEndX = 4f;
        const float kSkin = 0.01f;

        static bool InGap(float x) => x >= gapStartX && x <= gapEndX;

        public bool Grounded(float x, float y) => (y - 1f) <= kSkin && !InGap(x);

        public void SweepX(float x, float y, float dx, out float resolvedDx, out bool blocked)
        {
            resolvedDx = dx; blocked = false;
        }

        public void SweepY(float x, float y, float dy, out float resolvedDy, out bool blocked)
        {
            if (dy >= 0f)  { resolvedDy = dy; blocked = false; return; }
            if (InGap(x))  { resolvedDy = dy; blocked = false; return; }    // no floor across the gap
            float newCenter = y + dy;
            if (newCenter < 1f) { resolvedDy = 1f - y; blocked = true; }
            else                { resolvedDy = dy;   blocked = false; }
        }
    }
}
