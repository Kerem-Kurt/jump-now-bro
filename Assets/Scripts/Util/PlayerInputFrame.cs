namespace JumpNowBro.Util
{
    /// One player's raw per-tick intent. Packs to 1 byte on the wire (bit layout below) — the v1.4 INPUT
    /// packet carries a K=6 frame redundancy window so an edge press survives a few consecutive losses.
    /// Bits 5–7 are reserved (zero on write, ignored on read) so v1.7+ can extend without a wire bump.
    public struct PlayerInputFrame
    {
        public bool moveLeft, moveRight, jumpPressed, jumpHeld, dashPressed;

        // bit 0 moveLeft | 1 moveRight | 2 jumpPressed | 3 jumpHeld | 4 dashPressed | 5–7 reserved
        public static byte Pack(in PlayerInputFrame f)
        {
            byte b = 0;
            if (f.moveLeft)    b |= 1 << 0;
            if (f.moveRight)   b |= 1 << 1;
            if (f.jumpPressed) b |= 1 << 2;
            if (f.jumpHeld)    b |= 1 << 3;
            if (f.dashPressed) b |= 1 << 4;
            return b;
        }

        public static PlayerInputFrame Unpack(byte b) => new PlayerInputFrame
        {
            moveLeft    = (b & (1 << 0)) != 0,
            moveRight   = (b & (1 << 1)) != 0,
            jumpPressed = (b & (1 << 2)) != 0,
            jumpHeld    = (b & (1 << 3)) != 0,
            dashPressed = (b & (1 << 4)) != 0,
        };
    }
}
