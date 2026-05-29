namespace JumpNowBro.Util
{
    /// Engine-free projection of the values Movement.Step needs from PlayerTuning. Rebuilt at the
    /// top of every FixedUpdate (~16 floats; sub-microsecond) so the Inspector live-tune workflow
    /// keeps working — copying once at Awake would freeze constants until next play.
    public struct MovementTuning
    {
        public float runSpeed, airControlMultiplier;
        public float jumpVelocity, gravity, coyoteTime, jumpBufferTime, variableJumpCutMultiplier;
        public float dashDistance, dashDuration, dashInvulnerabilityDuration;
        public sbyte dashFreezeTicks;            // pre-converted from seconds at marshal time so Step can decrement by 1/tick
        public float fallLimitY;
    }
}
