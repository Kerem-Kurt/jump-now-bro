using UnityEngine;

namespace JumpNowBro.Gameplay
{
    [RequireComponent(typeof(Collider2D))]
    public class Checkpoint : MonoBehaviour
    {
        [SerializeField] Transform respawnPoint;

        void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.TryGetComponent<PlayerController>(out var player)) return;
            var pos = respawnPoint != null ? (Vector2)respawnPoint.position : (Vector2)transform.position;
            player.SetCheckpoint(pos);
        }
    }
}
