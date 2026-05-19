using UnityEngine;

namespace JumpNowBro.Gameplay
{
    [RequireComponent(typeof(Collider2D))]
    public class Hazard : MonoBehaviour
    {
        void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.TryGetComponent<PlayerController>(out var player)) return;
            if (player.IsInvulnerable) return;
            player.Die();
        }
    }
}
