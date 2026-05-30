namespace JumpNowBro.Util
{
    /// Client-side tick-indexed history that v1.5 prediction + reconciliation stand on. Two parallel rings:
    /// the locally-sampled input per tick (replayed after a STATE reseed) and the predicted MovementState per
    /// tick (so the reconciler can measure misprediction at a given tick). Sized for ~2.1 s at 60 Hz —
    /// comfortably longer than any LAN RTT plus a brief stall.
    ///
    /// Index is tick % Capacity; each slot stores the tick it holds so a wrapped-over (stale) slot is rejected
    /// rather than read as a real entry. The reconciler relies on TryGet/HasInput returning false for a tick
    /// that was overwritten or never recorded — a silent stale read would replay the wrong input and desync.
    public sealed class ClientHistory
    {
        public const int Capacity = 128;

        struct InputSlot { public uint tick; public bool occupied; public PlayerInputFrame frame; }
        struct StateSlot { public uint tick; public bool occupied; public MovementState state; }

        readonly InputSlot[] inputs = new InputSlot[Capacity];
        readonly StateSlot[] states = new StateSlot[Capacity];

        public void RecordInput(uint tick, in PlayerInputFrame frame) =>
            inputs[(int)(tick % Capacity)] = new InputSlot { tick = tick, occupied = true, frame = frame };

        public void RecordPredicted(uint tick, in MovementState state) =>
            states[(int)(tick % Capacity)] = new StateSlot { tick = tick, occupied = true, state = state };

        public bool TryGetInput(uint tick, out PlayerInputFrame frame)
        {
            var s = inputs[(int)(tick % Capacity)];
            if (s.occupied && s.tick == tick) { frame = s.frame; return true; }
            frame = default;
            return false;
        }

        public bool TryGetPredicted(uint tick, out MovementState state)
        {
            var s = states[(int)(tick % Capacity)];
            if (s.occupied && s.tick == tick) { state = s.state; return true; }
            state = default;
            return false;
        }

        /// True iff the local input for `tick` is still in the ring. The reconciler checks this across the whole
        /// replay window and hard-snaps on the first hole instead of replaying a default-input step.
        public bool HasInput(uint tick) => TryGetInput(tick, out _);
    }
}
