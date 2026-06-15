using UnityEngine;
using JumpNowBro.Gameplay;
using JumpNowBro.Util;

namespace JumpNowBro.Networking
{
    /// Client-side STATE decoder. As of v1.5 it no longer moves the Player — the ClientPredictor owns the pose
    /// (prediction + reconciliation drive the Rigidbody2D). This component decodes each STATE into the
    /// authoritative snapshot the predictor reseeds from (CurrentState + SnapshotTick + LastConsumedClientTick),
    /// caches the host's input frame for dead-reckoning, mirrors ControlMap to ControlMapStore (HUD), and fires
    /// DeathNotifier so camera shake + HUD respond on the client.
    ///
    /// NetworkManager owns the dispatch closure that calls ApplyPayload; this component just stores the decoded
    /// state. Respawn-time handler-nulling races avoided.
    public sealed class ClientStateRenderer : MonoBehaviour
    {
        uint lastSeenSnapshotTick;
        bool haveSeen;
        bool seededMap;
        MoveState lastState;                       // previous snapshot's MoveState — for the →Dashing edge that drives client dash juice
        PlayerEffects effects;

        void Awake() => effects = GetComponent<PlayerEffects>();   // survives the client's PlayerController destroy; null-safe below

        public MovementState CurrentState { get; private set; }
        /// Cached so v1.5's predictor can dead-reckon host-owned inputs between snapshots.
        public PlayerInputFrame LastRemoteHostFrame { get; private set; }
        /// True once at least one STATE has been applied — the predictor waits for this before predicting.
        public bool HasState => haveSeen;
        /// The snapshot tick of the most recent applied STATE; the predictor reseeds when this advances.
        public uint SnapshotTick => lastSeenSnapshotTick;
        /// The host's last-consumed client tick — the reconciliation replay anchor (#81).
        public uint LastConsumedClientTick { get; private set; }

        public void ApplyPayload(byte[] payload)
        {
            if (!StateBody.TryRead(payload, out var body)) return;
            // Staleness gate: latest-wins on u32 snapshotTick. The `haveSeen` sentinel lets the very
            // first STATE through regardless of LastConsumedClientTick=0 ambiguity.
            if (haveSeen && !SeqMath.IsNewer32(body.snapshotTick, lastSeenSnapshotTick)) return;
            haveSeen = true;
            lastSeenSnapshotTick = body.snapshotTick;

            // v1.5: the ClientPredictor owns the pose now (drives the Rigidbody2D from prediction + reconcile).
            // The v1.4 teleport to body.movementState.pos was removed here; this component is now the STATE
            // decoder + HUD/death side-effects, and the authoritative-state source the predictor reseeds from.

            // Dash juice from the authoritative MoveState transition INTO Dashing. STATE carries this for
            // every dash regardless of owner — unlike the predictor's EdgeFlags, which only fire for
            // client-owned actions (host-owned dash edges are stripped by DeadReckonHost). Edge, not
            // occupancy, so a multi-tick dash fires once. The host drives the same juice via PlayerController.OnDash.
            if (effects != null)
            {
                var st = body.movementState.state;                          // MoveState transitions drive juice for every owner
                if (st == MoveState.Dashing  && lastState != MoveState.Dashing)  effects.PlayDash();
                if (st == MoveState.Jumping  && lastState != MoveState.Jumping)  effects.PlayJump();
                if (st == MoveState.Grounded && lastState != MoveState.Grounded) effects.PlayLand();
            }
            lastState = body.movementState.state;

            CurrentState           = body.movementState;
            LastRemoteHostFrame    = body.remoteInputFrame;
            LastConsumedClientTick = body.lastConsumedClientTick;

            // Seed the map from the FIRST STATE only (mid-game join: adopt the host's current ownership).
            // Steady-state swaps arrive on the reliable SWAP EVENT and are applied by the scheduler at the
            // shared apply tick — mirroring every STATE would fight the scheduler after an early client flip.
            if (!seededMap && ControlMapStore.Instance != null)
            {
                ControlMapStore.Instance.Apply(body.controlMap);
                seededMap = true;
                // Freshly-loaded triggers default to armed; a mid-game join or a Leave/Rejoin into a progressed
                // level must grey the ones already swapped on the host (#105). This seed is the first moment the
                // authoritative map is known, and this renderer's Player spawned into the loaded scene, so the
                // triggers are already registered.
                SwapTrigger.ReconcileBannersTo(body.controlMap);
            }

            // Push the cumulative count into DeathNotifier on every STATE — Raise dedups on equal so the
            // 30 Hz traffic doesn't spam camera shake; the HUD picks up the change via the OnDeath event.
            // Side effect: mid-game join with host already dead N times triggers ONE shake when client
            // first syncs — acceptable UX (and arguably a useful "you're joining a death-prone partner" cue).
            DeathNotifier.Instance?.Raise(body.deathCount);
        }
    }
}
