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

            CurrentState           = body.movementState;
            LastRemoteHostFrame    = body.remoteInputFrame;
            LastConsumedClientTick = body.lastConsumedClientTick;

            // HUD ControlMap mirror — host's swap landed; bring our HUD in sync.
            if (ControlMapStore.Instance != null && !MapsEqual(body.controlMap, ControlMapStore.Instance.Current))
                ControlMapStore.Instance.Apply(body.controlMap);

            // Push the cumulative count into DeathNotifier on every STATE — Raise dedups on equal so the
            // 30 Hz traffic doesn't spam camera shake; the HUD picks up the change via the OnDeath event.
            // Side effect: mid-game join with host already dead N times triggers ONE shake when client
            // first syncs — acceptable UX (and arguably a useful "you're joining a death-prone partner" cue).
            DeathNotifier.Instance?.Raise(body.deathCount);
        }

        static bool MapsEqual(ControlMap a, ControlMap b) =>
            a.moveOwner == b.moveOwner && a.jumpOwner == b.jumpOwner && a.dashOwner == b.dashOwner;
    }
}
