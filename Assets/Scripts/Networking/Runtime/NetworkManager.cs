using System.Net;
using UnityEngine;
using JumpNowBro.Gameplay;

namespace JumpNowBro.Networking
{
    /// The session orchestrator on Bootstrap's Manager. Owns the gameplay socket, (optional) condition
    /// channel, discovery, and the Session (which itself owns the transport). In Hosting it binds the
    /// gameplay port, polls the raw socket for a validated HELLO, and on first match latches that peer
    /// and builds the wire. Client lifecycle lands in the client-lifecycle issue. SinglePlayer is inert
    /// — the game runs from Bootstrap exactly as today.
    [DisallowMultipleComponent]
    public sealed class NetworkManager : MonoBehaviour
    {
        [SerializeField] GameRole startupRole = GameRole.SinglePlayer;
        [SerializeField] ushort gameplayPort = 7777;
        #pragma warning disable 0414   // read by the client lifecycle in the next issue
        [SerializeField] string manualHostIp = "127.0.0.1";
        #pragma warning restore 0414
        [SerializeField] ushort discoveryPort = 47777;
        [SerializeField] string gameName = "Jump Now Bro!";

        public GameRole Role { get; private set; }

        UdpSocket gameplaySocket;
        #pragma warning disable 0649   // wired later by a lag-sim toggle; null until then
        NetworkConditionChannel condChannel;
        #pragma warning restore 0649
        DiscoveryService discovery;
        Session session;
        bool listening;                // host is in the listen-for-HELLO phase
        double clock;

        void Awake()
        {
            Role = startupRole;
            if (Role == GameRole.SinglePlayer) return;
            FindAnyObjectByType<LevelManager>()?.SuppressAutoStart();     // runs in Awake -> beats LevelManager.Start
            if (Role == GameRole.Hosting) BeginHosting();
        }

        void Update()
        {
            if (Role == GameRole.SinglePlayer) return;                    // SinglePlayer is inert
            clock += Time.deltaTime;                                      // one Σdt clock fed to lag-channel + session/transport + discovery
            if (Role == GameRole.Hosting && listening) PollForHello();
            condChannel?.Release(clock);
            session?.Tick(Time.deltaTime);                                // session pumps its transport internally — never tick transport directly
            discovery?.Tick(clock);
        }

        void OnDestroy()
        {
            discovery?.Dispose();
            gameplaySocket?.Dispose();                                    // kills the background receive thread on play-stop
        }

        // ---- host lifecycle ----

        void BeginHosting()
        {
            gameplaySocket = new UdpSocket(gameplayPort);                 // gameplay socket: NOT broadcast (the discovery socket gets that)
            discovery = DiscoveryService.StartHost(discoveryPort, new LanBeacon
            {
                Magic = SessionProtocol.Magic,
                GameName = gameName,
                GameplayPort = gameplayPort,
            });
            listening = true;
        }

        // Drains the raw socket while no peer is latched. Drops anything that isn't a valid HELLO; on the
        // first valid one, latches that sender as the peer and builds the channel + transport + session.
        void PollForHello()
        {
            while (gameplaySocket.Poll(out var data, out var from))
            {
                if (SessionProtocol.IsValidHello(data)) { LatchPeer(from, data); return; }
                // not a valid HELLO (junk / wrong magic / wrong version): drop and keep draining
            }
        }

        void LatchPeer(IPEndPoint peer, byte[] helloDatagram)
        {
            var ch = new UdpDatagramChannel(gameplaySocket, peer);
            ch.PreSeed(helloDatagram);                                    // transport processes the validated HELLO immediately
            var transport = new UdpReliableTransport(ch, pingIntervalSeconds: 0.2);   // ~5 Hz keepalive — v1.2's only traffic
            session = new Session(transport, isHost: true);
            session.OnStateChanged += OnSessionStateChanged;
            session.Start();                                              // queues WELCOME; flushes on the next session.Tick
            listening = false;
        }

        void OnSessionStateChanged(Session.SessionState state)
        {
            if (state != Session.SessionState.Disconnected) return;
            session = null;                                               // peer gone (GOODBYE / liveness timeout) — re-arm Listening for a fresh client
            listening = true;
        }
    }
}
