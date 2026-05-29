using System;
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
    [DefaultExecutionOrder(-100)]                                         // Awake before PlayerSpawner reads Instance.Role (#78 wiring)
    public sealed class NetworkManager : MonoBehaviour
    {
        public static NetworkManager Instance { get; private set; }

        [SerializeField] GameRole startupRole = GameRole.SinglePlayer;
        [SerializeField] ushort gameplayPort = 7777;
        [SerializeField] string manualHostIp = "127.0.0.1";
        [SerializeField] ushort discoveryPort = 47777;
        [SerializeField] string gameName = "Jump Now Bro!";

        public GameRole Role { get; private set; }
        public Session.SessionState? CurrentSessionState => session?.State;
        public float CurrentRtt => session?.RttSeconds ?? 0f;
        /// Exposed so the #78 spawner can hand the live transport to broadcasters/senders/receivers.
        public IReliableTransport CurrentTransport => transport;

        UdpSocket gameplaySocket;
        #pragma warning disable 0649   // wired later by a lag-sim toggle; null until then
        NetworkConditionChannel condChannel;
        #pragma warning restore 0649
        DiscoveryService discovery;
        Session session;
        UdpReliableTransport transport;                                   // kept on the manager so #78 broadcasters/senders can read it via Instance.CurrentTransport
        bool listening;                // host is in the listen-for-HELLO phase
        double clock;

        // Gameplay-message dispatch — Session.OnGameplayMessage forwards INPUT/STATE/EVENT here;
        // #77 (renderer/receiver) and #78 (host input) register handlers via SetStateHandler etc.
        Action<byte[]> stateHandler;
        Action<byte[]> eventHandler;
        Action<byte[]> inputHandler;

        public void SetStateHandler(Action<byte[]> h) => stateHandler = h;
        public void SetEventHandler(Action<byte[]> h) => eventHandler = h;
        public void SetInputHandler(Action<byte[]> h) => inputHandler = h;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            Role = startupRole;
            if (Role == GameRole.SinglePlayer) return;
            Application.runInBackground = true;                           // keep ticking while unfocused so PINGs flow between editors (else the 5s liveness fires)
            FindAnyObjectByType<LevelManager>()?.SuppressAutoStart();     // runs in Awake -> beats LevelManager.Start
            try
            {
                if (Role == GameRole.Hosting) BeginHosting();
                else if (Role == GameRole.Client) BeginClient();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"NetworkManager: failed to start as {Role} — {e.Message}");
                EndSessionFromUi();                                       // unwind any partial init and reset so the ConnectionUI stays usable
            }
        }

        void Update()
        {
            if (Role == GameRole.SinglePlayer) return;                    // SinglePlayer is inert
            clock += Time.deltaTime;                                      // one Σdt clock fed to lag-channel + session/transport + discovery
            if (Role == GameRole.Hosting && listening && gameplaySocket != null) PollForHello();
            condChannel?.Release(clock);
            session?.Tick(Time.deltaTime);                                // session pumps its transport internally — never tick transport directly
            discovery?.Tick(clock);
        }

        void OnDestroy()
        {
            var s = session;                                              // capture: SendGoodbye -> SetState -> OnStateChanged nulls the field
            if (s != null)
            {
                s.SendGoodbye(GoodbyeReason.Normal);                      // queue a graceful GOODBYE...
                s.Tick(0);                                                // ...and flush it (firstSend ignores RTO)
            }
            discovery?.Dispose();
            gameplaySocket?.Dispose();                                    // kills the background receive thread on play-stop
            if (Instance == this) Instance = null;
        }

        // ---- host lifecycle ----

        void BeginHosting()
        {
            gameplaySocket = new UdpSocket(gameplayPort);                 // gameplay socket: NOT broadcast (the discovery socket gets that)
            try
            {
                discovery = DiscoveryService.StartHost(discoveryPort, new LanBeacon
                {
                    Magic = SessionProtocol.Magic,
                    GameName = gameName,
                    GameplayPort = gameplayPort,
                });
            }
            catch                                                         // discovery is best-effort; unwind cleanly so Update doesn't see a half-built host
            {
                gameplaySocket.Dispose();
                gameplaySocket = null;
                throw;
            }
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
            transport = new UdpReliableTransport(ch, pingIntervalSeconds: 0.2);      // ~5 Hz keepalive — v1.2's only traffic until #76's Established hook flips to 1 Hz
            // Providers sampled at WELCOME-send time so currentSceneIndex reflects the actual scene
            // the host is on (mid-game join case); 0xFF sentinel means "no level loaded yet".
            session = new Session(transport, isHost: true,
                sceneIndexProvider: () => {
                    var idx = LevelManager.Instance != null ? LevelManager.Instance.CurrentLevelIndex : -1;
                    return idx >= 0 && idx < 0xFF ? (byte)idx : (byte)0xFF;
                },
                hostTickProvider: () => TickClock.Instance != null ? TickClock.Instance.Current : 0u);
            session.OnStateChanged += OnSessionStateChanged;
            session.OnGameplayMessage += OnGameplayMessageDispatch;
            session.Start();                                              // queues WELCOME; flushes on the next session.Tick
            listening = false;
        }

        // ---- client lifecycle ----

        void BeginClient()
        {
            gameplaySocket = new UdpSocket(0);                            // ephemeral port: host + client coexist on one machine
            discovery = DiscoveryService.StartClient(discoveryPort);      // host list builds for the connection UI
            var host = new IPEndPoint(IPAddress.Parse(manualHostIp), gameplayPort);
            var ch = new UdpDatagramChannel(gameplaySocket, host);
            transport = new UdpReliableTransport(ch, pingIntervalSeconds: 0.2);
            session = new Session(transport, isHost: false);
            session.OnStateChanged += OnSessionStateChanged;              // subscribe BEFORE Start so we observe Idle->Connecting
            session.OnWelcomeReceived += OnClientWelcomeReceived;         // mid-game join: load whichever scene host is on
            session.OnGameplayMessage += OnGameplayMessageDispatch;
            session.Start();                                              // sends HELLO; awaits WELCOME
        }

        void OnGameplayMessageDispatch(MessageType type, byte[] payload)
        {
            switch (type)
            {
                case MessageType.State: stateHandler?.Invoke(payload); break;
                case MessageType.Event: eventHandler?.Invoke(payload); break;
                case MessageType.Input: inputHandler?.Invoke(payload); break;
                // Ping/Pong are transport-internal — handled inside UdpReliableTransport, never bubble up here.
            }
        }

        // ---- shared ----

        void OnSessionStateChanged(Session.SessionState state)
        {
            Debug.Log($"[{Role}] Session: {state}");                      // visibility until ConnectionUI surfaces this
            if (state == Session.SessionState.Established)
            {
                transport?.SetPingInterval(1.0);                          // INPUT/STATE keep liveness warm — restore DESIGN §8 PING cadence
                if (Role == GameRole.Hosting && LevelManager.Instance != null && LevelManager.Instance.CurrentLevelIndex < 0)
                    LevelManager.Instance.LoadFirst();                    // initial-join: host starts the game once the wire is up
            }
            if (state != Session.SessionState.Disconnected) return;
            session = null;
            transport = null;
            if (Role == GameRole.Hosting) listening = true;               // re-arm for a fresh client; client just nulls
        }

        void OnClientWelcomeReceived(Welcome w)
        {
            if (w.PeerOwner != JumpNowBro.Util.InputOwner.P2)
                Debug.LogWarning($"[Client] WELCOME peerOwner={w.PeerOwner}, expected P2 — version skew?");
            // currentSceneIndex == 0xFF means host hasn't loaded yet; LoadByIndex is a no-op in that case.
            // The LEVEL_LOAD EVENT path (lands in #78) drives the initial-join client scene load.
            LevelManager.Instance?.LoadByIndex(w.CurrentSceneIndex);
        }

        // ---- UI API ----

        public void BeginSoloFromUi()
        {
            if (Role != GameRole.SinglePlayer || session != null) return;
            LevelManager.Instance?.LoadFirst();
        }

        public void BeginHostingFromUi()
        {
            if (Role != GameRole.SinglePlayer || session != null) return;
            Role = GameRole.Hosting;
            Application.runInBackground = true;
            try { BeginHosting(); }
            catch (System.Exception e) { Debug.LogError($"BeginHosting failed: {e.Message}"); EndSessionFromUi(); }
        }

        public void BeginClientFromUi(string hostIp)
        {
            if (Role != GameRole.SinglePlayer || session != null) return;
            if (string.IsNullOrWhiteSpace(hostIp)) return;
            Role = GameRole.Client;
            manualHostIp = hostIp;
            Application.runInBackground = true;
            try { BeginClient(); }
            catch (System.Exception e) { Debug.LogError($"BeginClient failed: {e.Message}"); EndSessionFromUi(); }
        }

        public void EndSessionFromUi()
        {
            var s = session;
            if (s != null) { s.SendGoodbye(GoodbyeReason.Normal); s.Tick(0); }
            session = null;
            transport = null;
            discovery?.Dispose(); discovery = null;
            gameplaySocket?.Dispose(); gameplaySocket = null;
            listening = false;
            Role = GameRole.SinglePlayer;
        }
    }
}
