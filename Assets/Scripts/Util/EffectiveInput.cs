namespace JumpNowBro.Util
{
    /// What Movement.Step actually consumes: the ControlMap-routed combination of the two raw
    /// PlayerInputFrames. `moveDir` is signed (-1/0/+1) so left+right cancels (matches the current
    /// FixedUpdate behavior); jump/dash fire only from their owning player's frame.
    public struct EffectiveInput
    {
        public int moveDir;
        public bool jumpPressed, jumpHeld, dashPressed;
    }
}
