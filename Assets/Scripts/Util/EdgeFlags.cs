using System;

namespace JumpNowBro.Util
{
    /// Per-tick transition events emitted by Movement.Step. PlayerController fires OnDash / OnDeath
    /// from these flags rather than diffing pre/post MovementState — diffing can't distinguish
    /// "variable-jump-cut fired" from "bonked ceiling and lost vy".
    [Flags]
    public enum EdgeFlags : byte
    {
        None             = 0,
        JumpedThisTick   = 1 << 0,
        JumpCutThisTick  = 1 << 1,
        DashedThisTick   = 1 << 2,
        LandedThisTick   = 1 << 3,
        DiedThisTick     = 1 << 4,
    }
}
