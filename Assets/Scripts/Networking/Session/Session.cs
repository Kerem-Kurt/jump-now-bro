using System;

namespace JumpNowBro.Networking
{
    /// Application handshake on top of the reliable transport, and the SOLE authority on whether a
    /// session is up. It drives its state off validated HELLO/WELCOME via TryReceive — never the
    /// transport's low-level "first datagram seen" flag — but does honor the transport's OnDisconnected
    /// (reliable give-up / liveness timeout). Client sends HELLO and awaits WELCOME; host awaits the
    /// (already peer-validated, pre-seeded) HELLO and replies WELCOME.
    public sealed class Session
    {
        public enum SessionState { Idle, Connecting, Established, Disconnected }

        const double ConnectTimeoutSeconds = 4.0;   // the only FAST teardown for a stuck handshake (RTO exhaustion is far slower)
        const int MaxBody = 16;

        readonly IReliableTransport transport;
        readonly bool isHost;
        readonly byte[] scratch = new byte[MaxBody];

        double clock;
        double connectingSince;

        public SessionState State { get; private set; } = SessionState.Idle;
        public GoodbyeReason LastGoodbye { get; private set; }
        public float RttSeconds => transport.RttSeconds;
        public event Action<SessionState> OnStateChanged;

        public Session(IReliableTransport transport, bool isHost)
        {
            this.transport = transport;
            this.isHost = isHost;
            transport.OnDisconnected += () => SetState(SessionState.Disconnected);
        }

        public void Start()
        {
            connectingSince = clock;
            SetState(SessionState.Connecting);
            if (!isHost) SendHello();                 // client speaks first; host waits for the HELLO
        }

        public void Tick(float dt)
        {
            clock += dt;
            transport.Tick(dt);
            while (transport.TryReceive(out var type, out var payload))
                Handle(type, payload);

            if (State == SessionState.Connecting && clock - connectingSince > ConnectTimeoutSeconds)
                SetState(SessionState.Disconnected);
        }

        public void SendGoodbye(GoodbyeReason reason)
        {
            if (State == SessionState.Disconnected) return;
            int n = new Goodbye { Reason = reason }.Write(scratch);
            transport.Send(Channel.Reliable, MessageType.Goodbye, scratch.AsSpan(0, n));
            SetState(SessionState.Disconnected);      // caller pumps one more Tick so the GOODBYE actually flushes
        }

        void Handle(MessageType type, byte[] payload)
        {
            switch (type)
            {
                case MessageType.Hello when isHost:
                    bool ok = Hello.TryRead(payload, out var hello)
                              && hello.Magic == SessionProtocol.Magic && hello.Version == SessionProtocol.Version;
                    SendWelcome(ok, ok ? WelcomeReason.Accepted : WelcomeReason.VersionMismatch);
                    SetState(ok ? SessionState.Established : SessionState.Disconnected);
                    break;

                case MessageType.Welcome when !isHost:
                    bool accepted = Welcome.TryRead(payload, out var w) && w.Accepted
                                    && w.Magic == SessionProtocol.Magic && w.Version == SessionProtocol.Version;
                    SetState(accepted ? SessionState.Established : SessionState.Disconnected);
                    break;

                case MessageType.Goodbye:
                    if (Goodbye.TryRead(payload, out var g)) LastGoodbye = g.Reason;
                    SetState(SessionState.Disconnected);
                    break;
            }
        }

        void SendHello()
        {
            int n = new Hello { Magic = SessionProtocol.Magic, Version = SessionProtocol.Version }.Write(scratch);
            transport.Send(Channel.Reliable, MessageType.Hello, scratch.AsSpan(0, n));
        }

        void SendWelcome(bool accepted, WelcomeReason reason)
        {
            int n = new Welcome { Magic = SessionProtocol.Magic, Version = SessionProtocol.Version, Accepted = accepted, Reason = reason }.Write(scratch);
            transport.Send(Channel.Reliable, MessageType.Welcome, scratch.AsSpan(0, n));
        }

        void SetState(SessionState s)
        {
            if (State == s || State == SessionState.Disconnected) return;   // Disconnected is terminal for this instance
            State = s;
            OnStateChanged?.Invoke(s);
        }
    }
}
