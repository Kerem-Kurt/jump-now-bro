using UnityEngine;
using JumpNowBro.Gameplay;

namespace JumpNowBro.Networking
{
    /// Tiny IMGUI panel for picking Solo / Host / Join / Leave during v1.2 — the smallest thing that demos
    /// the netcode end to end. Slated to be polished to UGUI/TMP later (a v1.7 follow-up). Lives next to
    /// NetworkManager on the Bootstrap Manager.
    [RequireComponent(typeof(NetworkManager))]
    public sealed class ConnectionUI : MonoBehaviour
    {
        NetworkManager net;
        string ipInput = "127.0.0.1";

        void Awake()
        {
            net = GetComponent<NetworkManager>();
            FindAnyObjectByType<LevelManager>()?.SuppressAutoStart();   // UI is the entry point; Solo button drives LoadFirst
        }

        void OnGUI()
        {
            var area = new Rect(10, 10, 300, 220);
            GUILayout.BeginArea(area, GUI.skin.box);

            GUILayout.Label($"Mode: {net.Role}");
            var state = net.CurrentSessionState;
            if (state.HasValue)
            {
                GUILayout.Label($"Session: {state.Value}");
                if (state.Value == Session.SessionState.Established)
                {
                    GUILayout.Label($"RTT: {net.CurrentRtt * 1000f:F0} ms   reliable q: {net.PendingReliableCount}");
                    if (net.DroppedDatagrams > 0) GUILayout.Label($"dropped datagrams: {net.DroppedDatagrams}");
                }
            }
            GUILayout.Space(8);

            bool idle = (state == null || state == Session.SessionState.Disconnected) && net.Role == GameRole.SinglePlayer;
            if (idle)
            {
                if (GUILayout.Button("Solo (single-player)")) { net.BeginSoloFromUi(); enabled = false; }   // hide once the game starts
                if (GUILayout.Button("Host"))                  net.BeginHostingFromUi();
                GUILayout.Label("Host IP:");
                ipInput = GUILayout.TextField(ipInput);
                if (GUILayout.Button("Join"))                  net.BeginClientFromUi(ipInput);
#if UNITY_EDITOR
                // Editor-only lag-sim selector: cycle Clean/Fair/Stress before Host/Join. Read at channel build.
                if (GUILayout.Button($"Sim: {NetworkManager.EditorSimProfile} >"))
                    NetworkManager.EditorSimProfile = (NetworkManager.LagProfile)(((int)NetworkManager.EditorSimProfile + 1) % 3);
#endif
            }
            else
            {
                if (GUILayout.Button("Leave")) net.EndSessionFromUi();
            }

            GUILayout.EndArea();
        }
    }
}
