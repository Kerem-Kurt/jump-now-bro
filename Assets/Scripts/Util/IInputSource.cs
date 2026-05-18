namespace JumpNowBro.Util
{
    /// Source of player input; JumpPressed/DashPressed are edge-triggered, consumed by Tick().
    public interface IInputSource
    {
        bool MoveLeft { get; }
        bool MoveRight { get; }
        bool JumpPressed { get; }
        bool JumpHeld { get; }
        bool DashPressed { get; }
        void Tick();
    }
}
