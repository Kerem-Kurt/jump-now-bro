namespace JumpNowBro.Util
{
    /// Pure, engine-free client-prediction step. The MonoBehaviour wrapper (Networking/Runtime/ClientPredictor)
    /// owns frame timing, the Rigidbody2D, and the collision world; this is the replayable core so the same
    /// symbol drives both the per-tick forward prediction (#80) and the reconciliation replay loop (#81), and
    /// so it can be unit-tested in the no-Unity CI against the fake collision worlds.
    ///
    /// Ownership routing is the v1.4 ControlMap.Route (host = P1, client-local = P2). The host frame is
    /// dead-reckoned before routing: host EDGE bits are stripped (never predict a host jump/dash — a false
    /// positive launches the shared body then yanks it back), and host jumpHeld is forced true so a host-owned
    /// jump's variable-cut never fires on the client. Net effect: the client actively predicts only the
    /// horizontal axis it can know for host-owned actions; host-owned VERTICAL is authoritative-only, advancing
    /// by gravity from the STATE-seeded velocity until the next snapshot corrects it.
    public static class ClientPrediction
    {
        /// Strip host edges and force jumpHeld so host-owned vertical stays authoritative-only (see class doc).
        /// moveLeft/moveRight pass through — host-owned horizontal IS dead-reckoned (bounded ~runSpeed*dt/tick).
        public static PlayerInputFrame DeadReckonHost(in PlayerInputFrame host) => new PlayerInputFrame
        {
            moveLeft    = host.moveLeft,
            moveRight   = host.moveRight,
            jumpPressed = false,   // never predict a host edge
            dashPressed = false,   // never predict a host edge
            jumpHeld    = true,    // suppress the variable-jump cut for a host-owned jump
        };

        /// One predicted tick. `localFrame` is the client's fresh P2 frame (full edges + held); `hostFrame` is the
        /// last STATE's host P1 frame, dead-reckoned here. Returns the post-step state and its EdgeFlags (the
        /// caller may drive juice off them in a later milestone; v1.5 ignores them).
        public static (MovementState, EdgeFlags) PredictStep(
            in MovementState s, ControlMap map,
            in PlayerInputFrame localFrame, in PlayerInputFrame hostFrame,
            in MovementTuning t, float dt, ICollisionWorld world)
        {
            var host  = DeadReckonHost(hostFrame);
            var input = ControlMap.Route(map, host, localFrame);   // p1 = host, p2 = client-local
            return Movement.Step(s, input, t, dt, world);
        }

        public const int DefaultReplayCap = PredictionTuning.ReplayCap;

        public readonly struct ReconcileResult
        {
            public readonly MovementState State;
            public readonly bool HardSnapped;     // true = bailed to authoritative (hole or over-cap) → caller teleports
            public readonly int ReplayedTicks;
            public ReconcileResult(MovementState state, bool hardSnapped, int replayedTicks)
            {
                State = state; HardSnapped = hardSnapped; ReplayedTicks = replayedTicks;
            }
        }

        /// Reconcile prediction against an authoritative STATE (#81). Reseed to the authoritative snapshot, then
        /// replay the client's buffered local inputs for ticks (lastConsumedClientTick, currentClientTick] so the
        /// owned-input prediction survives the correction. Returns the rebuilt "predicted at currentClientTick".
        ///
        /// `lastConsumedClientTick` IS the client-tick anchor: the host's authoritative state already reflects it
        /// applying client input through that tick, so the replay window starts at the next tick. Host-owned
        /// actions are dead-reckoned from the single latest `hostFrame` (a known approximation between snapshots).
        ///
        /// `mapAt(tick)` resolves the ControlMap in effect at each replayed tick, so a scheduled swap whose
        /// apply-tick falls inside the window routes pre-boundary ticks under the old map and post-boundary ticks
        /// under the new one (v1.6 #84). The caller caches the delegate (one alloc) — Reconcile runs per snapshot.
        ///
        /// Hard-snap (return authoritative, HardSnapped=true) when: the window exceeds `replayCap` (post-stall
        /// catch-up storm — bounded so we never spike CPU or read a ring-wrapped tick), OR any tick in the window
        /// is missing from history (a hole from a skipped FixedUpdate — never replay a default-input step).
        ///
        /// `onBeforeStep` lets the Unity caller re-establish the rb cast origin before each replayed Movement.Step
        /// (rb.position = state.pos; SyncTransforms). Pure callers (CI tests on fake worlds) pass null.
        public static ReconcileResult Reconcile(
            in MovementState authoritative, uint lastConsumedClientTick, uint currentClientTick,
            ClientHistory history, System.Func<uint, ControlMap> mapAt, in PlayerInputFrame hostFrame,
            in MovementTuning t, float dt, ICollisionWorld world,
            System.Action<MovementState> onBeforeStep = null, int replayCap = DefaultReplayCap)
        {
            if (currentClientTick <= lastConsumedClientTick)
                return new ReconcileResult(authoritative, false, 0);          // host already confirmed through now

            if (currentClientTick - lastConsumedClientTick > (uint)replayCap)
                return new ReconcileResult(authoritative, true, 0);           // too far to replay → snap

            var state = authoritative;
            int replayed = 0;
            for (uint tick = lastConsumedClientTick + 1; tick <= currentClientTick; tick++)
            {
                if (!history.TryGetInput(tick, out var local))
                    return new ReconcileResult(authoritative, true, replayed); // hole → snap

                onBeforeStep?.Invoke(state);
                (state, _) = PredictStep(state, mapAt(tick), local, hostFrame, t, dt, world);
                history.RecordPredicted(tick, state);
                replayed++;
            }
            return new ReconcileResult(state, false, replayed);
        }
    }
}
