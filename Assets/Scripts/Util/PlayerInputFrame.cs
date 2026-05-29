namespace JumpNowBro.Util
{
    /// One player's raw per-tick intent. POD only in v1.3 — Pack/Unpack to a byte (and the bit layout)
    /// are co-designed with the v1.4 INPUT packet (tick + K-frame redundancy window), not here.
    public struct PlayerInputFrame
    {
        public bool moveLeft, moveRight, jumpPressed, jumpHeld, dashPressed;
    }
}
