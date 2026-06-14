using System;
using JumpNowBro.Util;

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
        /// Why the session ended — read by the UI to word the disconnect and decide whether to surface it.
        public enum DisconnectReason { None, LocalLeave, PeerLeft, ConnectionLost, HandshakeFailed }

        // The client re-probes HELLO at a steady cadence for the whole budget, so a host that starts late
        // (the join-before-host case, #120) is caught by the next probe instead of the client having gone
        // silent under the reliable queue's backoff. The budget is the only FAST teardown for a handshake
        // that never lands; it is long enough for a human to start the host after pressing Join.
        const double ConnectBudgetSeconds = 15.0;
        const double HelloProbeInterval = 0.5;      // client re-sends HELLO this often while Connecting
        // Re-probes reuse the seq the queued HELLO claimed (the reliable send-seq space starts at 1, so the
        // first queued message is 1). Holding it constant lets the host dedupe repeats; it also keeps the
        // client's GOODBYE/EVENT continuing from seq 2, which the host's in-order receive buffer requires.
        const ushort HelloSeq = 1;
        const int MaxBody = 24;                     // v1.4 Welcome body = 14 bytes; cushion for v1.5+ extensions

        readonly IReliableTransport transport;
        readonly bool isHost;
        readonly Func<byte> sceneIndexProvider;     // host only — read at WELCOME-send time so it reflects current scene
        readonly Func<uint> hostTickProvider;       // host only — sampled into WELCOME body for debug / future offset estimation
        readonly byte[] scratch = new byte[MaxBody];

        double clock;
        double connectingSince;
        double lastHelloAt;                         // client only — clock of the last HELLO probe

        public SessionState State { get; private set; } = SessionState.Idle;
        public GoodbyeReason LastGoodbye { get; private set; }
        public DisconnectReason LastDisconnect { get; private set; }
        public float RttSeconds => transport.RttSeconds;
        public event Action<SessionState> OnStateChanged;
        /// Raised on the CLIENT after a valid WELCOME is parsed — carries scene index for mid-game join + peer slot confirmation.
        public event Action<Welcome> OnWelcomeReceived;
        /// Raised for message types Session doesn't itself handle (INPUT/STATE/EVENT/PING/PONG bodies).
        /// NetworkManager subscribes and routes to whichever gameplay consumer is registered.
        public event Action<MessageType, byte[]> OnGameplayMessage;

        public Session(IReliableTransport transport, bool isHost,
                       Func<byte> sceneIndexProvider = null, Func<uint> hostTickProvider = null)
        {
            this.transport = transport;
            this.isHost = isHost;
            this.sceneIndexProvider = sceneIndexProvider;
            this.hostTickProvider = hostTickProvider;
            transport.OnDisconnected += () => Disconnect(DisconnectReason.ConnectionLost);
        }

        public void Start()
        {
            connectingSince = clock;
            lastHelloAt = clock;
            SetState(SessionState.Connecting);
            if (!isHost) SendHello(queued: true);     // client speaks first; the queued HELLO claims message-seq 1
        }

        public void Tick(float dt)
        {
            clock += dt;
            transport.Tick(dt);
            while (transport.TryReceive(out var type, out var payload))
                Handle(type, payload);

            if (State == SessionState.Connecting)
            {
                // Client keeps re-probing until WELCOME lands or the budget runs out (host may start late).
                if (!isHost && clock - lastHelloAt >= HelloProbeInterval) { SendHello(queued: false); lastHelloAt = clock; }
                if (clock - connectingSince > ConnectBudgetSeconds)
                    Disconnect(DisconnectReason.HandshakeFailed);
            }
        }

        public void SendGoodbye(GoodbyeReason reason)
        {
            if (State == SessionState.Disconnected) return;
            int n = new Goodbye { Reason = reason }.Write(scratch);
            transport.Send(Channel.Reliable, MessageType.Goodbye, scratch.AsSpan(0, n));
            Disconnect(DisconnectReason.LocalLeave);  // caller pumps one more Tick so the GOODBYE actually flushes
        }

        void Handle(MessageType type, byte[] payload)
        {
            switch (type)
            {
                case MessageType.Hello when isHost:
                    if (State != SessionState.Connecting) break;   // already established: ignore retried/duplicate HELLOs (no second WELCOME)
                    bool ok = Hello.TryRead(payload, out var hello)
                              && hello.Magic == SessionProtocol.Magic && hello.Version == SessionProtocol.Version;
                    SendWelcome(ok, ok ? WelcomeReason.Accepted : WelcomeReason.VersionMismatch);
                    if (ok) SetState(SessionState.Established);
                    else Disconnect(DisconnectReason.HandshakeFailed);
                    break;

                case MessageType.Welcome when !isHost:
                    bool wellFormed = Welcome.TryRead(payload, out var w);
                    bool accepted = wellFormed && w.Accepted
                                    && w.Magic == SessionProtocol.Magic && w.Version == SessionProtocol.Version;
                    if (accepted) OnWelcomeReceived?.Invoke(w);   // fires BEFORE state flip so subscribers see Established with welcome in hand
                    if (accepted) SetState(SessionState.Established);
                    else Disconnect(DisconnectReason.HandshakeFailed);
                    break;

                case MessageType.Goodbye:
                    if (Goodbye.TryRead(payload, out var g)) LastGoodbye = g.Reason;
                    Disconnect(DisconnectReason.PeerLeft);
                    break;

                default:
                    OnGameplayMessage?.Invoke(type, payload);             // INPUT/STATE/EVENT/PING/PONG — forward to NetworkManager dispatcher
                    break;
            }
        }

        // queued: the opening HELLO rides the send queue once, claiming message-seq 1 so the queue's next
        // reliable message (GOODBYE/EVENT) is seq 2 and the host delivers it in order. The steady re-probe
        // (queued: false) bypasses the queue under that same fixed seq, so the host dedupes the repeats and
        // nothing piles up; it is what keeps a late-starting host catching a HELLO within a probe interval.
        void SendHello(bool queued)
        {
            int n = new Hello { Magic = SessionProtocol.Magic, Version = SessionProtocol.Version }.Write(scratch);
            if (queued) transport.Send(Channel.Reliable, MessageType.Hello, scratch.AsSpan(0, n));
            else        transport.SendReliableFixedSeq(MessageType.Hello, HelloSeq, scratch.AsSpan(0, n));
        }

        void SendWelcome(bool accepted, WelcomeReason reason)
        {
            // v1.4 convention: host owns P1, client owns P2. The byte is confirmation, not configuration —
            // host enforces the binding regardless; client logs/asserts agreement.
            var welcome = new Welcome
            {
                Magic              = SessionProtocol.Magic,
                Version            = SessionProtocol.Version,
                Accepted           = accepted,
                Reason             = reason,
                PeerOwner          = InputOwner.P2,
                CurrentSceneIndex  = sceneIndexProvider != null ? sceneIndexProvider() : (byte)0xFF,
                HostTickAtWelcome  = hostTickProvider   != null ? hostTickProvider()   : 0u,
            };
            int n = welcome.Write(scratch);
            transport.Send(Channel.Reliable, MessageType.Welcome, scratch.AsSpan(0, n));
        }

        // Single disconnect choke point: stamp the reason (first wins — Disconnected is terminal) then transition.
        void Disconnect(DisconnectReason reason)
        {
            if (State == SessionState.Disconnected) return;
            LastDisconnect = reason;
            SetState(SessionState.Disconnected);
        }

        void SetState(SessionState s)
        {
            if (State == s || State == SessionState.Disconnected) return;   // Disconnected is terminal for this instance
            State = s;
            OnStateChanged?.Invoke(s);
        }
    }
}
