using UnityEngine;
using JumpNowBro.Gameplay;
using JumpNowBro.Util;

namespace JumpNowBro.Networking
{
    /// Client-side predictor (#80). Each FixedUpdate it runs Movement.Step locally so the client's OWNED inputs
    /// feel instant instead of waiting a round-trip, and drives the (kinematic) Rigidbody2D from the predicted
    /// state. The pure step lives in Util/ClientPrediction; this is the Unity glue: frame timing, the shared
    /// collision world, and the rb-position cast-origin invariant.
    ///
    /// v1.5 scope split: this commit reseeds the predicted state to the latest authoritative snapshot whenever a
    /// new STATE arrives (no replay yet — on a clean LAN snapshotTick ≈ now, so this is already correct; under
    /// latency it leaves a small per-STATE step that #81's replay reconciliation removes). #82 then smooths any
    /// residual onto a render child. Predicted EdgeFlags are produced but juice is deferred to v1.6 #94.
    ///
    /// Execution order −40: after ClientInputSender (−50) so LastSampledFrame is fresh, after TickClock (−100).
    [DefaultExecutionOrder(-40)]
    public sealed class ClientPredictor : MonoBehaviour
    {
        ClientInputSender sender;
        ClientStateRenderer stateRenderer;
        TickClock tickClock;
        ControlMapStore mapStore;
        Rigidbody2D rb;
        ICollisionWorld world;
        PlayerTuning tuning;
        float fallLimitY;

        readonly ClientHistory history = new ClientHistory();
        MovementState predicted;
        bool seeded;
        bool wasDead;
        uint lastReseedSnapshot;

        public void Bind(ClientInputSender sender, ClientStateRenderer stateRenderer, TickClock tickClock,
                         ControlMapStore mapStore, Rigidbody2D rb, ICollisionWorld world,
                         PlayerTuning tuning, float fallLimitY)
        {
            this.sender = sender;
            this.stateRenderer = stateRenderer;
            this.tickClock = tickClock;
            this.mapStore = mapStore;
            this.rb = rb;
            this.world = world;
            this.tuning = tuning;
            this.fallLimitY = fallLimitY;
        }

        void FixedUpdate()
        {
            if (sender == null || stateRenderer == null || tickClock == null || rb == null || world == null || tuning == null) return;
            if (LevelManager.Instance != null && LevelManager.Instance.IsLoading) return;
            if (!stateRenderer.HasState) return;                       // nothing authoritative to predict from yet

            var authoritative = stateRenderer.CurrentState;

            // Host-authoritative death: hold at the authoritative pose and do NOT predict, so local input can't
            // walk the body through the hazard during the host's death freeze. Hard-snap (transform +
            // SyncTransforms, matching the host's respawn teleport) so the spike→checkpoint jump cuts cleanly
            // instead of streaking under Rigidbody2D interpolation.
            if (authoritative.isDead)
            {
                predicted = authoritative;
                seeded = true;
                wasDead = true;
                lastReseedSnapshot = stateRenderer.SnapshotTick;
                HardSnapTo(authoritative);
                return;
            }

            // First live tick after a death: snap to the authoritative respawn pose, then resume prediction next tick.
            if (wasDead)
            {
                predicted = authoritative;
                wasDead = false;
                lastReseedSnapshot = stateRenderer.SnapshotTick;
                HardSnapTo(authoritative);
                return;
            }

            // Reseed to authority on a fresh snapshot. #81 replaces this with replay from LastConsumedClientTick.
            if (!seeded || stateRenderer.SnapshotTick != lastReseedSnapshot)
            {
                predicted = authoritative;
                lastReseedSnapshot = stateRenderer.SnapshotTick;
                seeded = true;
            }

            uint tick = tickClock.Current;
            if (sender.LastSampledTick != tick) return;               // R5: only this tick's sample; never record a stale frame
            var local = sender.LastSampledFrame;

            var map = mapStore != null ? mapStore.Current : ControlMap.Default;
            var t = tuning.AsMovementTuning(Time.fixedDeltaTime, fallLimitY);

            // R2: re-establish the cast origin so Movement.Step's sweep (rb.Cast) originates at the predicted
            // position — the host invariant, reproduced on the client so collision is bit-for-bit comparable.
            rb.position = new Vector2(predicted.posX, predicted.posY);
            Physics2D.SyncTransforms();

            var (next, _) = ClientPrediction.PredictStep(predicted, map, local, stateRenderer.LastRemoteHostFrame,
                                                          t, Time.fixedDeltaTime, world);
            predicted = next;

            history.RecordInput(tick, local);
            history.RecordPredicted(tick, next);

            rb.MovePosition(new Vector2(next.posX, next.posY));        // kinematic mover; also fires trigger contacts
        }

        // Hard teleport for host-authoritative jumps (death hold, respawn). transform.position + SyncTransforms
        // (not MovePosition) so the kinematic body cuts instantly without interpolation streak — the v1.3
        // respawn-teleport pattern, mirrored on the client.
        void HardSnapTo(in MovementState s)
        {
            transform.position = new Vector3(s.posX, s.posY, transform.position.z);
            Physics2D.SyncTransforms();
        }
    }
}
