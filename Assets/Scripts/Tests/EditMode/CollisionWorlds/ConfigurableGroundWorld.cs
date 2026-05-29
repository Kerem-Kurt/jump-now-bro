using JumpNowBro.Util;

namespace JumpNowBro.Tests.CollisionWorlds
{
    /// Toggle-driven grounded state — for coyote / jump-buffer / land-transition tests that need to
    /// flip the grounded-ness of the world between ticks without moving the player or scripting geometry.
    internal sealed class ConfigurableGroundWorld : ICollisionWorld
    {
        public bool isGrounded;

        public bool Grounded(float x, float y) => isGrounded;
        public void SweepX(float x, float y, float dx, out float resolvedDx, out bool blocked)
            { resolvedDx = dx; blocked = false; }
        public void SweepY(float x, float y, float dy, out float resolvedDy, out bool blocked)
            { resolvedDy = dy; blocked = false; }
    }
}
