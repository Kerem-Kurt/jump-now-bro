using UnityEngine;

namespace JumpNowBro.Gameplay
{
    [CreateAssetMenu(menuName = "JumpNowBro/Player Tuning", fileName = "PlayerTuning")]
    public class PlayerTuning : ScriptableObject
    {
        [Header("Horizontal Movement")]
        public float runSpeed = 9f;
        public float airControlMultiplier = 0.85f;

        [Header("Jump")]
        public float jumpVelocity = 16f;
        public float gravity = 50f;
        public float coyoteTime = 0.1f;
        public float jumpBufferTime = 0.1f;
        public float variableJumpCutMultiplier = 0.5f;

        [Header("Dash")]
        public float dashDistance = 4f;
        public float dashDuration = 0.15f;
        public float dashFreezeFrameDuration = 0.05f;
        public float dashInvulnerabilityDuration = 0.15f;
    }
}
