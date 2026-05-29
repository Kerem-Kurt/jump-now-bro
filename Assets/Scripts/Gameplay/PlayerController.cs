using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;
using JumpNowBro.Util;

namespace JumpNowBro.Gameplay
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] PlayerTuning tuning;
        [SerializeField, FormerlySerializedAs("groundLayers")] LayerMask solidLayers;
        [SerializeField] Transform groundCheckPoint;
        [SerializeField] float groundCheckRadius = 0.15f;
        [SerializeField] float fallLimitY = -20f;

        const float RespawnDelay = 0.4f;

        Rigidbody2D rb;
        ICollisionWorld collisionWorld;
        IInputSource p1;
        IInputSource p2;
        MovementState currentState;                                                 // all per-tick state (MoveState, timers, facing, dash charge, wasJumpHeld)
        Vector2 checkpointPosition;
        ControlMap checkpointControlMap = ControlMap.Default;
        bool isDead;

        public bool IsInvulnerable => currentState.invulnTimer > 0f;
        public bool IsDead => isDead;
        public Vector2 CheckpointPosition => checkpointPosition;
        public int DeathCount { get; private set; }

        public event System.Action<int> OnDeath;
        public event System.Action OnDash;

        public void ResetDeathCount() => DeathCount = 0;

        public void SetCheckpoint(Vector2 pos, ControlMap map)
        {
            checkpointPosition = pos;
            checkpointControlMap = map;
        }

        public void Die()
        {
            if (isDead) return;
            StartCoroutine(DieRoutine());
        }

        IEnumerator DieRoutine()
        {
            isDead = true;
            DeathCount++;
            OnDeath?.Invoke(DeathCount);                                            // fires immediately so juice (camera shake, death-count UI) lands before respawn
            rb.linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(RespawnDelay);
            rb.position = checkpointPosition;
            rb.linearVelocity = Vector2.zero;
            currentState = FreshSpawnState();
            if (ControlMapStore.Instance != null)
                ControlMapStore.Instance.Apply(checkpointControlMap);
            SwapTrigger.RestoreCheckpointStates();
            isDead = false;
        }

        public void Inject(IInputSource player1, IInputSource player2)
        {
            p1 = player1;
            p2 = player2;
        }

        void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            collisionWorld = new UnityCollisionWorld(rb, solidLayers, groundCheckPoint, groundCheckRadius);
            currentState = FreshSpawnState();
        }

        void FixedUpdate()
        {
            if (p1 == null || p2 == null || tuning == null) return;
            if (isDead) return;

            float dt = Time.fixedDeltaTime;

            currentState.posX = rb.position.x;                                       // at #69 read pos AND vel from rb every tick — Box2D modifies vel
            currentState.posY = rb.position.y;                                       // between ticks (contact response zeroes vel.y on landing, friction
            currentState.velX = rb.linearVelocity.x;                                 // damps vel.x). v0.4-mvp's FixedUpdate read vel.y the same way. At #70
            currentState.velY = rb.linearVelocity.y;                                 // (Kinematic) Movement.Step owns vel; this read drops out.

            var f1 = ReadFrame(p1);
            var f2 = ReadFrame(p2);
            var map = ControlMapStore.Instance != null ? ControlMapStore.Instance.Current : ControlMap.Default;
            var input = ControlMap.Route(map, f1, f2);

            var movementTuning = tuning.AsMovementTuning(dt, fallLimitY);            // rebuilt every tick so Inspector live-tune still works

            // sweep:false at #69 — Dynamic body, Box2D still handles collision blocking. #70 flips to sweep:true.
            var (newState, edges) = Movement.Step(currentState, input, movementTuning, dt, collisionWorld, sweep: false);

            if ((edges & EdgeFlags.DiedThisTick) != 0)                               // fall-limit → Die() routes through the existing 0.4s respawn coroutine
            {
                Die();
                return;
            }
            if ((edges & EdgeFlags.DashedThisTick) != 0) OnDash?.Invoke();

            currentState = newState;
            rb.linearVelocity = new Vector2(newState.velX, newState.velY);

            p1.Tick();
            p2.Tick();
        }

        static PlayerInputFrame ReadFrame(IInputSource s) =>
            new PlayerInputFrame
            {
                moveLeft    = s.MoveLeft,
                moveRight   = s.MoveRight,
                jumpPressed = s.JumpPressed,
                jumpHeld    = s.JumpHeld,
                dashPressed = s.DashPressed,
            };

        static MovementState FreshSpawnState() =>
            new MovementState
            {
                state               = MoveState.Falling,
                facing              = 1,
                dashChargeAvailable = true,
            };
    }
}
