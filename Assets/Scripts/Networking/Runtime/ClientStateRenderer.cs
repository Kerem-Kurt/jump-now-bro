using UnityEngine;
using JumpNowBro.Gameplay;
using JumpNowBro.Util;

namespace JumpNowBro.Networking
{
    /// Client-side STATE consumer. v1.4 is **pre-prediction**: the client doesn't run Movement.Step;
    /// it teleports the Player to the host's authoritative position on every newer STATE. ControlMap
    /// changes mirror to ControlMapStore so HUD reads stay role-agnostic; deathCount increments fire
    /// DeathNotifier so camera shake and HUD respond on the client.
    ///
    /// `CurrentState` mirrors the post-step MovementState in the same shape PlayerController holds —
    /// v1.5's predictor inherits this field as its seed without a wire-format change.
    ///
    /// NetworkManager owns the dispatch closure that calls ApplyPayload; this component just stores
    /// the bound target + the most-recent state. Respawn-time handler-nulling races avoided.
    public sealed class ClientStateRenderer : MonoBehaviour
    {
        Transform target;
        uint lastSeenSnapshotTick;
        bool haveSeen;

        public MovementState CurrentState { get; private set; }
        /// Cached so v1.5's predictor can dead-reckon host-owned inputs between snapshots.
        public PlayerInputFrame LastRemoteHostFrame { get; private set; }

        public void Bind(Transform target) => this.target = target;

        public void ApplyPayload(byte[] payload)
        {
            if (!StateBody.TryRead(payload, out var body)) return;
            // Staleness gate: latest-wins on u32 snapshotTick. The `haveSeen` sentinel lets the very
            // first STATE through regardless of LastConsumedClientTick=0 ambiguity.
            if (haveSeen && !SeqMath.IsNewer32(body.snapshotTick, lastSeenSnapshotTick)) return;
            haveSeen = true;
            lastSeenSnapshotTick = body.snapshotTick;

            if (target != null)
            {
                target.position = new Vector3(body.movementState.posX, body.movementState.posY, target.position.z);
                Physics2D.SyncTransforms();                                  // m_AutoSyncTransforms=0 — push the pose into Physics2D so the camera follow reads it
            }

            CurrentState        = body.movementState;
            LastRemoteHostFrame = body.remoteInputFrame;

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
