using JumpNowBro.Util;

namespace JumpNowBro.Tests.CollisionWorlds
{
    /// A solid quadrant platform occupying x >= wallX and y <= topY. A 1x1 body falling and moving
    /// right wedges on the platform's top-left corner: SweepY-down blocks (body over the top) and
    /// SweepX-right blocks (body low enough to hit the left face), so both report blocked with no
    /// resolved motion — the #102 corner-on-corner stick. A small upward lift clears the left face
    /// (the body rises above topY), which is exactly what the corner correction probes for.
    internal sealed class FakeCornerStick : ICollisionWorld
    {
        const float wallX = 5f;     // platform left face
        const float topY  = 2f;     // platform top surface
        const float half  = 0.5f;   // player half-extent (1x1 collider)
        const float kSkin = 0.001f;

        public bool Grounded(float x, float y) => false;   // the wedge is airborne

        public void SweepX(float x, float y, float dx, out float resolvedDx, out bool blocked)
        {
            // Rightward move hits the left face only while the body's bottom is at/below the platform top.
            if (dx > 0f && (y - half) <= topY + kSkin)
            {
                float maxDx = wallX - (x + half);
                if (maxDx <= kSkin) { resolvedDx = 0f;    blocked = true; return; }
                if (dx > maxDx)     { resolvedDx = maxDx; blocked = true; return; }
            }
            resolvedDx = dx; blocked = false;
        }

        public void SweepY(float x, float y, float dy, out float resolvedDy, out bool blocked)
        {
            // Downward move lands on the platform top only while the body overlaps it horizontally.
            if (dy < 0f && (x + half) >= wallX - kSkin)
            {
                float maxDown = (y - half) - topY;
                if (maxDown <= kSkin) { resolvedDy = 0f;       blocked = true; return; }
                if (-dy > maxDown)    { resolvedDy = -maxDown; blocked = true; return; }
            }
            resolvedDy = dy; blocked = false;
        }
    }
}
