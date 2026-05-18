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
        float coyoteTimer;
        float jumpBufferTimer;
        float dashTimer;
        float freezeTimer;
        float invulnTimer;
        bool dashChargeAvailable = true;
        int facing = 1;

        public bool IsInvulnerable => invulnTimer > 0f;

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

            float dt = Time.fixedDeltaTime;
            coyoteTimer = Mathf.Max(0f, coyoteTimer - dt);
            jumpBufferTimer = Mathf.Max(0f, jumpBufferTimer - dt);
            invulnTimer = Mathf.Max(0f, invulnTimer - dt);

            bool grounded = IsGrounded();
            bool jumpPressed = p1.JumpPressed;
            bool jumpHeld = p1.JumpHeld;
            bool dashPressed = p1.DashPressed;

            int dir = (p1.MoveRight ? 1 : 0) - (p1.MoveLeft ? 1 : 0);
            if (dir != 0) facing = dir;

            if (jumpPressed) jumpBufferTimer = tuning.jumpBufferTime;
            bool jumpAllowed = InputForgiveness.CanJump(coyoteTimer, jumpBufferTimer, grounded, jumpPressed);

            switch (state)
            {
                case MoveState.Grounded:
                    if (dashPressed && dashChargeAvailable) FireDash();
                    else if (jumpAllowed) FireJump();
                    else if (!grounded)
                    {
                        state = MoveState.Falling;
                        coyoteTimer = tuning.coyoteTime;
                    }
                    break;
                case MoveState.Jumping:
                    if (dashPressed && dashChargeAvailable) FireDash();
                    else
                    {
                        if (rb.linearVelocity.y > 0f && !jumpHeld && wasJumpHeld) ApplyJumpCut();
                        if (rb.linearVelocity.y <= 0f) state = MoveState.Falling;
                    }
                    break;
                case MoveState.Falling:
                    if (dashPressed && dashChargeAvailable) FireDash();
                    else if (jumpAllowed) FireJump();
                    else if (grounded)
                    {
                        state = MoveState.Grounded;
                        dashChargeAvailable = true;
                    }
                    break;
                case MoveState.Dashing:
                    if (freezeTimer > 0f)
                    {
                        rb.linearVelocity = Vector2.zero;
                        freezeTimer = Mathf.Max(0f, freezeTimer - dt);
                        if (freezeTimer <= 0f)
                        {
                            float dashSpeed = tuning.dashDistance / tuning.dashDuration;
                            rb.linearVelocity = new Vector2(facing * dashSpeed, 0f);
                        }
                    }
                    else
                    {
                        dashTimer = Mathf.Max(0f, dashTimer - dt);
                        if (dashTimer <= 0f) state = MoveState.Falling;
                    }
                    break;
            }

            if (state != MoveState.Dashing)
            {
                float speedMul = (state == MoveState.Grounded) ? 1f : tuning.airControlMultiplier;
                float targetVx = dir * tuning.runSpeed * speedMul;

                Vector2 vel = rb.linearVelocity;
                vel.x = targetVx;
                vel.y -= tuning.gravity * dt;
                rb.linearVelocity = vel;
            }

            wasJumpHeld = jumpHeld;
            p1.Tick();
            p2.Tick();
        }

        void FireJump()
        {
            var v = rb.linearVelocity;
            v.y = tuning.jumpVelocity;
            rb.linearVelocity = v;
            state = MoveState.Jumping;
            jumpBufferTimer = 0f;
            coyoteTimer = 0f;
        }

        void ApplyJumpCut()
        {
            var v = rb.linearVelocity;
            v.y *= tuning.variableJumpCutMultiplier;
            rb.linearVelocity = v;
        }

        void FireDash()
        {
            state = MoveState.Dashing;
            freezeTimer = tuning.dashFreezeFrameDuration;
            dashTimer = tuning.dashDuration;
            invulnTimer = tuning.dashInvulnerabilityDuration;
            dashChargeAvailable = false;
            rb.linearVelocity = Vector2.zero;
        }

        bool IsGrounded()
        {
            if (groundCheckPoint == null) return false;
            return Physics2D.OverlapCircle(groundCheckPoint.position, groundCheckRadius, groundLayers);
        }
    }
}
