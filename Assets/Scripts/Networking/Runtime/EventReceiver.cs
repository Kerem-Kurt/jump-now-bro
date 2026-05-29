using UnityEngine;
using JumpNowBro.Gameplay;

namespace JumpNowBro.Networking
{
    /// Client-side reliable-EVENT consumer. v1.4 wires LEVEL_LOAD only — host calls LoadNext (which
    /// sends the EVENT in #78), this receiver picks it up and drives the client's own LevelManager
    /// to the matching scene. v1.6 will extend the kind discriminator for SWAP + DEATH with applyTick.
    public sealed class EventReceiver : MonoBehaviour
    {
        public void Bind()
        {
            if (NetworkManager.Instance != null) NetworkManager.Instance.SetEventHandler(OnEventBytes);
        }

        void OnDestroy()
        {
            if (NetworkManager.Instance != null) NetworkManager.Instance.SetEventHandler(null);
        }

        void OnEventBytes(byte[] payload)
        {
            if (!EventBody.TryRead(payload, out var body)) return;
            switch (body.kind)
            {
                case EventKind.LevelLoad:
                    LevelManager.Instance?.LoadByIndex(body.sceneIndex);
                    break;
                // Swap + Death land in v1.6 with applyTick scheduling.
            }
        }
    }
}
