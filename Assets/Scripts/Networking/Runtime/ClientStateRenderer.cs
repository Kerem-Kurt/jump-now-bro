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
    /// `currentState` mirrors the post-step MovementState in the same shape PlayerController holds —
    /// v1.5's predictor inherits this field as its seed without a wire-format change.
    public sealed class ClientStateRenderer : MonoBehaviour
    {
        Transform target;
        uint lastSeenSnapshotTick;
        bool haveSeen;
        ushort lastSeenDeathCount;
        bool deathCountInitialized;

        public MovementState CurrentState { get; private set; }
        /// Cached so v1.5's predictor can dead-reckon host-owned inputs between snapshots.
        public PlayerInputFrame LastRemoteHostFrame { get; private set; }

        public void Bind(Transform target)
        {
            this.target = target;
            if (NetworkManager.Instance != null) NetworkManager.Instance.SetStateHandler(OnStateBytes);
        }

        void OnDestroy()
        {
            if (NetworkManager.Instance != null) NetworkManager.Instance.SetStateHandler(null);
        }

        void OnStateBytes(byte[] payload)
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
                Physics2D.SyncTransforms();                                  // m_AutoSyncTransforms=0 — push the pose into Physics2D immediately so the camera follow reads it
            }

            CurrentState        = body.movementState;
            LastRemoteHostFrame = body.remoteInputFrame;

            // HUD ControlMap mirror — host's swap landed; bring our HUD in sync.
            if (ControlMapStore.Instance != null && !MapsEqual(body.controlMap, ControlMapStore.Instance.Current))
                ControlMapStore.Instance.Apply(body.controlMap);

            // Death delta — fire the synthetic OnDeath so HUD counter + camera shake respond identically
            // to the host path. First STATE seeds the baseline without raising (no fake death on join).
            if (!deathCountInitialized)
            {
                deathCountInitialized = true;
                lastSeenDeathCount = body.deathCount;
            }
            else if (body.deathCount != lastSeenDeathCount)
            {
                lastSeenDeathCount = body.deathCount;
                DeathNotifier.Instance?.Raise(body.deathCount);
            }
        }

        static bool MapsEqual(ControlMap a, ControlMap b) =>
            a.moveOwner == b.moveOwner && a.jumpOwner == b.jumpOwner && a.dashOwner == b.dashOwner;
    }
}
