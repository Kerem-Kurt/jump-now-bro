namespace JumpNowBro.Networking
{
    /// What this app instance is doing this session. SinglePlayer = today's solo flow, unchanged.
    /// Set on NetworkManager before play (Inspector) until the connection UI replaces that at runtime.
    public enum GameRole { SinglePlayer, Hosting, Client }
}
