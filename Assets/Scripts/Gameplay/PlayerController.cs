using UnityEngine;
using JumpNowBro.Util;

namespace JumpNowBro.Gameplay
{
    public enum MoveState { Grounded, Jumping, Falling, Dashing }

    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] PlayerTuning tuning;
        [SerializeField] LayerMask groundLayers;
        [SerializeField] Transform groundCheckPoint;
        [SerializeField] float groundCheckRadius = 0.15f;

        Rigidbody2D rb;
        IInputSource p1;
        IInputSource p2;
        MoveState state;

        public void Inject(IInputSource player1, IInputSource player2)
        {
            p1 = player1;
            p2 = player2;
        }

        void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            state = MoveState.Falling;
        }

        void FixedUpdate()
        {
            if (p1 == null || p2 == null || tuning == null) return;

            bool grounded = IsGrounded();
            state = grounded ? MoveState.Grounded : MoveState.Falling;

            int dir = (p1.MoveRight ? 1 : 0) - (p1.MoveLeft ? 1 : 0);
            float speedMul = grounded ? 1f : tuning.airControlMultiplier;
            float targetVx = dir * tuning.runSpeed * speedMul;

            Vector2 v = rb.linearVelocity;
            v.x = targetVx;
            v.y -= tuning.gravity * Time.fixedDeltaTime;
            rb.linearVelocity = v;

            p1.Tick();
            p2.Tick();
        }

        bool IsGrounded()
        {
            if (groundCheckPoint == null) return false;
            return Physics2D.OverlapCircle(groundCheckPoint.position, groundCheckRadius, groundLayers);
        }
    }
}
