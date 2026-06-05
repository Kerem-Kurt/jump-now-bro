using UnityEngine;

namespace JumpNowBro.Networking
{
    /// Connection-loss overlay. The entry menu (Solo/Host/Join/level select/Leave) moved to MainMenuUI; this keeps
    /// only the centered overlay shown while a peer drop is surfaced: the host waits (listening for a rejoin that
    /// resumes its preserved level); the client can Rejoin (reconnect + resume) or return to the menu. Still IMGUI —
    /// a small, proven recovery path; a later pass can fold it into MainMenuUI's UGUI.
    [RequireComponent(typeof(NetworkManager))]
    public sealed class ConnectionUI : MonoBehaviour
    {
        NetworkManager net;

        void Awake() => net = GetComponent<NetworkManager>();

        void OnGUI()
        {
            if (net.ConnectionLost) DrawConnectionLostOverlay();
        }

        void DrawConnectionLostOverlay()
        {
            const float w = 380f, h = 150f;
            GUILayout.BeginArea(new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h), GUI.skin.box);
            bool host = net.Role == GameRole.Hosting;
            GUILayout.Label(host ? "Partner disconnected" : "Connection lost");
            GUILayout.Label(ReasonText(net.LostReason));
            GUILayout.Space(8);
            if (host)
                GUILayout.Label("Waiting for the other player to rejoin...");
            else if (GUILayout.Button("Rejoin"))
                net.RejoinFromUi();
            if (GUILayout.Button("Return to menu"))
                net.EndSessionFromUi();
            GUILayout.EndArea();
        }

        static string ReasonText(Session.DisconnectReason r)
        {
            switch (r)
            {
                case Session.DisconnectReason.PeerLeft:        return "The other player left.";
                case Session.DisconnectReason.ConnectionLost:  return "The connection timed out.";
                case Session.DisconnectReason.HandshakeFailed: return "Could not reach the host.";
                default:                                       return "";
            }
        }
    }
}
