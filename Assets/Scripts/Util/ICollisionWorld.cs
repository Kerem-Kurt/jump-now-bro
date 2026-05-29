namespace JumpNowBro.Util
{
    /// Engine-free collision-query seam. Caller passes (x, y) so Movement.Step is replayable from
    /// arbitrary state in v1.5; the Unity impl honors the v1.3 invariant that rb.position == (x, y)
    /// at every call (the FixedUpdate marshal sets this up).
    public interface ICollisionWorld
    {
        bool Grounded(float x, float y);
        void SweepX(float x, float y, float dx, out float resolvedDx, out bool blocked);
        void SweepY(float x, float y, float dy, out float resolvedDy, out bool blocked);
    }
}
