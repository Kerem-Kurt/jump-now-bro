using System;
using System.Net;
using UnityEngine;
using JumpNowBro.Gameplay;
using JumpNowBro.Util;

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
        readonly byte[] eventSendScratch = new byte[EventBody.Size];      // LEVEL_LOAD send buffer; one body fits easily

        // Per-spawn role-aware components — re-bound each PlayerSpawner.OnPlayerSpawned. The dispatch
        // closures (state/input handlers) close over `this`, then read these fields fresh each call, so
        // a Player respawn (level transition) hot-swaps the target without any handler nulling races.
        NetworkRemoteInputSource currentHostRemote;
        ClientStateRenderer currentClientRenderer;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            // Register the Authority.IsHost check used by the four trigger gates. Default is "act as host";
            // we register a real check that returns false on Client. SinglePlayer + Hosting still pass.
            Authority.RegisterIsHost(
                () => Instance == null || Instance.Role == GameRole.SinglePlayer || Instance.Role == GameRole.Hosting);
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

        void Start()
        {
            // Subscriptions live for the manager's lifetime — fires for any role. Handlers themselves
            // check Role at call time (so a BeginHostingFromUi flip later in the session works).
            var spawner = FindAnyObjectByType<PlayerSpawner>();
            if (spawner != null) spawner.OnPlayerSpawned += OnPlayerSpawnedDispatch;
            if (LevelManager.Instance != null) LevelManager.Instance.OnBeforeLevelLoad += OnLevelLoadBegin;
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
                case MessageType.State:
                    if (currentClientRenderer != null) currentClientRenderer.ApplyPayload(payload);
                    break;
                case MessageType.Input:
                    if (currentHostRemote != null
                        && InputBody.TryRead(payload, out var baseTick, out _, out var packed))
                        currentHostRemote.EnqueueFromInputBody(baseTick, packed);
                    break;
                case MessageType.Event:
                    if (EventBody.TryRead(payload, out var ev) && ev.kind == EventKind.LevelLoad)
                        LevelManager.Instance?.LoadByIndex(ev.sceneIndex);
                    break;
                // Ping/Pong are transport-internal — handled inside UdpReliableTransport, never bubble up here.
            }
        }

        // ---- spawn-time role-aware wiring ----

        // Called for every PlayerSpawner.OnPlayerSpawned (every level load). SinglePlayer leaves the
        // prefab's PlayerBootstrap to wire local keyboards; Host/Client swap in the network-aware ones.
        void OnPlayerSpawnedDispatch(GameObject instance)
        {
            if (Role == GameRole.SinglePlayer) return;

            var bootstrap = instance.GetComponent<PlayerBootstrap>();
            if (bootstrap != null) Destroy(bootstrap);

            if (Role == GameRole.Hosting) WireHosting(instance);
            else if (Role == GameRole.Client) WireClient(instance);
        }

        void WireHosting(GameObject instance)
        {
            var ctrl  = instance.GetComponent<PlayerController>();
            var keyP1 = instance.GetComponent<KeyboardInputSource_P1>();
            var keyP2 = instance.GetComponent<KeyboardInputSource_P2>();
            if (keyP2 != null) Destroy(keyP2);                            // host's P2 input comes over the wire

            currentHostRemote = instance.AddComponent<NetworkRemoteInputSource>();
            if (ctrl != null) ctrl.Inject(keyP1, currentHostRemote);

            var bcast = instance.AddComponent<NetworkStateBroadcaster>();
            // Note: transport intentionally NOT passed — broadcaster reads NetworkManager.CurrentTransport
            // dynamically so a client-rejoin (new transport, same host Player) auto-picks up the new endpoint.
            bcast.Bind(ControlMapStore.Instance, LevelManager.Instance,
                       () => currentHostRemote != null ? currentHostRemote.LastConsumedClientTick : 0u);
        }

        void WireClient(GameObject instance)
        {
            // Client predicts via ClientPredictor (v1.5) but the host stays authoritative. PlayerController is
            // removed — capture the tuning it carries FIRST, since the predictor needs the same MovementTuning
            // and the serialized fields vanish with the component. The collision config + Rigidbody2D survive.
            var ctrl = instance.GetComponent<PlayerController>();
            PlayerTuning tuning = ctrl != null ? ctrl.Tuning : null;
            float fallLimitY    = ctrl != null ? ctrl.FallLimitY : -20f;
            if (ctrl != null) Destroy(ctrl);
            var keyP1 = instance.GetComponent<KeyboardInputSource_P1>();
            if (keyP1 != null) Destroy(keyP1);

            // Client = P2 by convention; sampler reads the local P2 keyboard.
            var keyP2 = instance.GetComponent<KeyboardInputSource_P2>();

            var sender = instance.AddComponent<ClientInputSender>();
            sender.Bind(keyP2, transport, TickClock.Instance);

            currentClientRenderer = instance.AddComponent<ClientStateRenderer>();

            var rb = instance.GetComponent<Rigidbody2D>();
            var collisionConfig = instance.GetComponent<PlayerCollisionConfig>();
            var predictor = instance.AddComponent<ClientPredictor>();
            predictor.Bind(sender, currentClientRenderer, TickClock.Instance, ControlMapStore.Instance,
                           rb, collisionConfig != null ? collisionConfig.CreateWorld(rb) : null,
                           tuning, fallLimitY);
        }

        void OnLevelLoadBegin(int sceneIndex)
        {
            if (Role != GameRole.Hosting || transport == null) return;
            var body = new EventBody { kind = EventKind.LevelLoad, sceneIndex = (byte)sceneIndex };
            int n = body.Write(eventSendScratch);
            transport.Send(Channel.Reliable, MessageType.Event, new ReadOnlySpan<byte>(eventSendScratch, 0, n));
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
            if (Role == GameRole.Hosting)
            {
                listening = true;
                Debug.Log("[Hosting] Listening for a new client...");     // surface the re-arm so the host knows it's still discoverable
            }
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

            // Tear down the spawned Player BEFORE disposing the socket. ClientInputSender and
            // NetworkStateBroadcaster fire on every FixedUpdate; if they're still alive when the
            // socket goes away, they'd throw ObjectDisposedException on next Send. Destroying the
            // GameObject removes all of them in one stroke.
            if (PlayerSpawner.Instance != null && PlayerSpawner.Instance.CurrentPlayerInstance != null)
                Destroy(PlayerSpawner.Instance.CurrentPlayerInstance);
            currentHostRemote = null;
            currentClientRenderer = null;

            // Reset LevelManager's index so the next Solo/Host/Join isn't tricked into a no-op by
            // LoadByIndex's idempotence check (which keys on currentLevelIndex + currentlyLoadedScene).
            // currentlyLoadedScene stays so LoadLevelRoutine can yield on the unload before re-loading.
            LevelManager.Instance?.ResetIndex();

            session = null;
            transport = null;
            discovery?.Dispose(); discovery = null;
            gameplaySocket?.Dispose(); gameplaySocket = null;
            listening = false;
            Role = GameRole.SinglePlayer;
        }
    }
}
