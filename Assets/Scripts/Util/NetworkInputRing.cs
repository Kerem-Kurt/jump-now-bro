namespace JumpNowBro.Util
{
    /// Tick-indexed N=8 ring of input frames the host's view of the client's input pulls from.
    /// Plan-locked semantic: serve the **newest** unconsumed frame on each consume — not the oldest.
    /// Host catching up after a hitch must use minimum-latency input, not replay stale frames.
    /// Edges fire exactly once per real frame (consume-once); held bits cache for starvation repeat
    /// in the caller. Wraparound is out of v1.4 scope (60 Hz × 2.27 years to overflow).
    public sealed class NetworkInputRing
    {
        public const int Capacity = 8;

        struct Slot
        {
            public uint clientTick;
            public PlayerInputFrame frame;
            public bool occupied;
            public bool consumed;
        }

        readonly Slot[] slots = new Slot[Capacity];
        bool hasConsumed;                                // sentinel: tick comparisons are inert until first consume
        public uint LastConsumedClientTick { get; private set; }

        /// Insert a frame received from the client. Idempotent on duplicate tick; rejects frames
        /// already past `LastConsumedClientTick`. When the slot is already occupied by a NEWER tick
        /// (mod-N collision), the incoming older frame is dropped — never overwrites a fresher input.
        public void Enqueue(uint clientTick, in PlayerInputFrame frame)
        {
            if (hasConsumed && clientTick <= LastConsumedClientTick) return;
            int slot = (int)(clientTick % Capacity);
            if (slots[slot].occupied && slots[slot].clientTick >= clientTick) return;   // keep newer of the two candidates
            slots[slot] = new Slot
            {
                clientTick = clientTick,
                frame      = frame,
                occupied   = true,
                consumed   = false,
            };
        }

        /// Pick the highest-tick unconsumed frame, mark it consumed, and advance LastConsumedClientTick.
        /// Returns false on starvation; caller is expected to repeat held bits and drop edges.
        public bool TryConsumeNewest(out PlayerInputFrame frame, out uint clientTick)
        {
            int bestSlot = -1;
            uint bestTick = 0;
            for (int i = 0; i < Capacity; i++)
            {
                ref var s = ref slots[i];
                if (!s.occupied || s.consumed) continue;
                if (hasConsumed && s.clientTick <= LastConsumedClientTick) continue;
                if (bestSlot < 0 || s.clientTick > bestTick)
                {
                    bestSlot = i;
                    bestTick = s.clientTick;
                }
            }
            if (bestSlot < 0)
            {
                frame = default;
                clientTick = 0;
                return false;
            }
            slots[bestSlot].consumed = true;
            LastConsumedClientTick   = bestTick;
            hasConsumed              = true;
            frame                    = slots[bestSlot].frame;
            clientTick               = bestTick;
            return true;
        }
    }
}
