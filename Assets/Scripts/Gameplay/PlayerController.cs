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
        bool wasJumpHeld;

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
            bool jumpPressed = p1.JumpPressed;
            bool jumpHeld = p1.JumpHeld;

            switch (state)
            {
                case MoveState.Grounded:
                    if (jumpPressed) { Jump(); state = MoveState.Jumping; }
                    else if (!grounded) state = MoveState.Falling;
                    break;
                case MoveState.Jumping:
                    if (rb.linearVelocity.y > 0f && !jumpHeld && wasJumpHeld)
                        ApplyJumpCut();
                    if (rb.linearVelocity.y <= 0f) state = MoveState.Falling;
                    break;
                case MoveState.Falling:
                    if (grounded) state = MoveState.Grounded;
                    break;
            }

            int dir = (p1.MoveRight ? 1 : 0) - (p1.MoveLeft ? 1 : 0);
            float speedMul = (state == MoveState.Grounded) ? 1f : tuning.airControlMultiplier;
            float targetVx = dir * tuning.runSpeed * speedMul;

            Vector2 vel = rb.linearVelocity;
            vel.x = targetVx;
            vel.y -= tuning.gravity * Time.fixedDeltaTime;
            rb.linearVelocity = vel;

            wasJumpHeld = jumpHeld;
            p1.Tick();
            p2.Tick();
        }

        void Jump()
        {
            var v = rb.linearVelocity;
            v.y = tuning.jumpVelocity;
            rb.linearVelocity = v;
        }

        void ApplyJumpCut()
        {
            var v = rb.linearVelocity;
            v.y *= tuning.variableJumpCutMultiplier;
            rb.linearVelocity = v;
        }

        bool IsGrounded()
        {
            if (groundCheckPoint == null) return false;
            return Physics2D.OverlapCircle(groundCheckPoint.position, groundCheckRadius, groundLayers);
        }
    }
}
