namespace JumpNowBro.Util
{
    public static class InputForgiveness
    {
        public static bool CanJump(float coyoteRemaining, float bufferRemaining,
                                    bool isGrounded, bool jumpJustPressed)
            => (jumpJustPressed || bufferRemaining > 0f)
            && (isGrounded || coyoteRemaining > 0f);
    }
}
