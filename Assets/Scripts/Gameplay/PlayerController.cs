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
            yield return new WaitForSeconds(RespawnDelay);
            transform.position = checkpointPosition;                                // Kinematic teleport: transform.position + explicit SyncTransforms() so the
            Physics2D.SyncTransforms();                                             // next FixedUpdate's cast queries see the new pose (m_AutoSyncTransforms=0).
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

            currentState.posX = rb.position.x;                                       // read live pos (DieRoutine teleport may have moved it); velocity is
            currentState.posY = rb.position.y;                                       // owned by Movement.Step — no rb.linearVelocity read at #70.

            var f1 = ReadFrame(p1);
            var f2 = ReadFrame(p2);
            var map = ControlMapStore.Instance != null ? ControlMapStore.Instance.Current : ControlMap.Default;
            var input = ControlMap.Route(map, f1, f2);

            var movementTuning = tuning.AsMovementTuning(dt, fallLimitY);            // rebuilt every tick so Inspector live-tune still works

            // sweep:true at #70 — body is Kinematic; Movement.Step's swept position is authoritative.
            var (newState, edges) = Movement.Step(currentState, input, movementTuning, dt, collisionWorld);

            if ((edges & EdgeFlags.DiedThisTick) != 0)                               // fall-limit → Die() routes through the existing 0.4s respawn coroutine
            {
                Die();
                return;
            }
            if ((edges & EdgeFlags.DashedThisTick) != 0) OnDash?.Invoke();

            currentState = newState;
            rb.MovePosition(new Vector2(newState.posX, newState.posY));              // Kinematic body — MovePosition is the only mover. The earlier "also set
                                                                                     // linearVelocity for trigger detection" pattern conflicts with MovePosition's
                                                                                     // internal velocity computation and stalls the body. UFKC=1 + MovePosition +
                                                                                     // moving Kinematic body is sufficient for OnTriggerEnter2D vs static triggers.
                                                                                     // Hard-snap (DieRoutine, v1.5 CSP correction) uses transform.position + SyncTransforms.

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
