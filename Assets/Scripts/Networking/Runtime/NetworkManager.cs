using UnityEngine;
using JumpNowBro.Gameplay;

namespace JumpNowBro.Networking
{
    /// The session orchestrator on Bootstrap's Manager. Owns the gameplay socket, transport, session, and
    /// (optional) condition channel + discovery — built in the host/client lifecycles later. Drives the
    /// existing game managers directly (the Networking asmdef references Gameplay, so no dependency cycle).
    /// SinglePlayer mode is inert: the game runs from Bootstrap exactly as today.
    [DisallowMultipleComponent]
    public sealed class NetworkManager : MonoBehaviour
    {
        [SerializeField] GameRole startupRole = GameRole.SinglePlayer;   // Inspector-driven until the connection UI replaces it
        [SerializeField] ushort gameplayPort = 7777;                     // host binds this; client targets it
        [SerializeField] string manualHostIp = "127.0.0.1";              // client's target (manual IP is the always-works path)

        public GameRole Role { get; private set; }

        // Built by the host/client lifecycle (later issues); pump is null-safe until then.
        #pragma warning disable 0649
        UdpSocket gameplaySocket;
        UdpReliableTransport transport;
        NetworkConditionChannel condChannel;
        DiscoveryService discovery;
        Session session;
        #pragma warning restore 0649
        double clock;

        void Awake()
        {
            Role = startupRole;
            if (Role != GameRole.SinglePlayer)
            {
                var lm = FindFirstObjectByType<LevelManager>();
                if (lm != null) lm.SuppressAutoStart();                  // runs in Awake -> beats LevelManager.Start
            }
        }

        void Update()
        {
            if (Role == GameRole.SinglePlayer) return;                   // SinglePlayer is inert
            clock += Time.deltaTime;                                     // one Σdt clock fed to the lag-channel + transport + discovery
            condChannel?.Release(clock);
            transport?.Tick(Time.deltaTime);
            discovery?.Tick(clock);
        }

        void OnDestroy()
        {
            discovery?.Dispose();
            gameplaySocket?.Dispose();                                   // kills the background receive thread on play-stop
        }
    }
}
