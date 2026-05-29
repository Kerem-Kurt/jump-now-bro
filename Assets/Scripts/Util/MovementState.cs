namespace JumpNowBro.Util
{
    /// Replayable per-tick snapshot of the shared character. Separate floats (no Vector2) keep Util
    /// engine-free, give us a stable wire-friendly layout for v1.4 STATE serialization, and let
    /// Movement.Step run identically on host and the v1.5 client predictor (same compiled symbol).
    /// Mutable struct — `in MovementState` on Step's parameter blocks accidental caller mutation;
    /// Step builds a fresh value internally and returns it.
    public struct MovementState
    {
        public float posX, posY, velX, velY;
        public MoveState state;
        public sbyte facing;                      // +1 / -1
        public float coyoteTimer, jumpBufferTimer, dashTimer, invulnTimer;
        public sbyte freezeTicksRemaining;        // tick counter (not seconds): survives v1.5 variable-dt rollback
        public bool dashChargeAvailable, wasJumpHeld, isDead;
    }
}
