namespace JumpNowBro.Util
{
    /// IInputSource impl that replays a recorded PlayerInputFrame sequence by tick. Used by #72's
    /// MovementTraceRecorder (Editor menu) to drive A/B CSV captures; also handy in tests.
    public sealed class ScriptedInputSource : IInputSource
    {
        readonly PlayerInputFrame[] frames;
        int index;

        public ScriptedInputSource(PlayerInputFrame[] frames) { this.frames = frames; }

        public bool MoveLeft    => index < frames.Length && frames[index].moveLeft;
        public bool MoveRight   => index < frames.Length && frames[index].moveRight;
        public bool JumpPressed => index < frames.Length && frames[index].jumpPressed;
        public bool JumpHeld    => index < frames.Length && frames[index].jumpHeld;
        public bool DashPressed => index < frames.Length && frames[index].dashPressed;
        public void Tick() => index++;
    }
}
