using UnityEngine;
using JumpNowBro.Util;

namespace JumpNowBro.Gameplay
{
    [RequireComponent(typeof(Collider2D))]
    public class Checkpoint : MonoBehaviour
    {
        [SerializeField] Transform respawnPoint;
        bool fired;

        void OnTriggerEnter2D(Collider2D other)
        {
            if (!Authority.IsHost) return;                         // client respawns are driven by STATE, not local checkpoints
            if (fired) return;
            if (!other.TryGetComponent<PlayerController>(out var player)) return;
            fired = true;
            var pos = respawnPoint != null ? (Vector2)respawnPoint.position : (Vector2)transform.position;
            var map = ControlMapStore.Instance != null ? ControlMapStore.Instance.Current : ControlMap.Default;
            player.SetCheckpoint(pos, map);
            SwapTrigger.SaveCheckpointStates();
        }
    }
}
