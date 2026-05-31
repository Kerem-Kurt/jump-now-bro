using System.Collections.Generic;
using JumpNowBro.Util;

namespace JumpNowBro.Networking
{
    /// Schedules ControlMap swaps to apply on a shared CLIENT-input-tick on both ends, so a swap flips at
    /// the same point in the input stream on host and client (DESIGN §8 + the v1.6 plan). Engine-free and
    /// CI-tested; the MonoBehaviour SwapScheduleDriver drives OnTick each FixedUpdate and performs the
    /// Unity-side ControlMapStore.Apply + banner update for each due swap.
    ///
    /// Each swap carries an ABSOLUTE map (the host composes it on PendingFinalMap so overlapping swaps
    /// compose rather than clobber). Absolute maps make application idempotent, which — together with the
    /// transport's exactly-once, in-order reliable delivery — is why no per-event dedup id is needed.
    ///
    /// Invariant: swaps are scheduled in non-decreasing applyTick order (the host derives applyTick from a
    /// monotonic clock plus a fixed lead). OnTick and MapAtTick rely on insertion order to resolve a
    /// same-applyTick collision to the last-inserted (most-composed) map.
    public sealed class PendingSwapScheduler
    {
        public readonly struct DueSwap
        {
            public readonly ControlMap Map;
            public readonly byte TriggerId;
            public DueSwap(ControlMap map, byte triggerId) { Map = map; TriggerId = triggerId; }
        }

        struct Entry { public uint applyTick; public ControlMap map; public byte triggerId; public bool applied; }

        readonly List<Entry> entries = new List<Entry>();
        readonly List<DueSwap> due = new List<DueSwap>();   // reused across OnTick calls — see OnTick remark
        ControlMap baseMap = ControlMap.Default;

        public void Schedule(uint applyTick, ControlMap absoluteMap, byte triggerId)
        {
            entries.Add(new Entry { applyTick = applyTick, map = absoluteMap, triggerId = triggerId, applied = false });
        }

        /// Apply (once) every not-yet-applied swap whose applyTick has been reached by currentApplyClock,
        /// in insertion order. The returned list is reused — consume it before the next OnTick call.
        public IReadOnlyList<DueSwap> OnTick(uint currentApplyClock)
        {
            due.Clear();
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                // Reached when applyTick <= clock, i.e. applyTick is NOT newer than the clock (wrap-safe).
                if (e.applied || SeqMath.IsNewer32(e.applyTick, currentApplyClock)) continue;
                e.applied = true;
                entries[i] = e;
                due.Add(new DueSwap(e.map, e.triggerId));
            }
            return due;
        }

        /// The map after every scheduled swap has applied — what the host composes the next swap on top of.
        /// Absolute maps reduce this to the last-scheduled entry's map, or `current` when nothing is pending.
        public ControlMap PendingFinalMap(ControlMap current) =>
            entries.Count > 0 ? entries[entries.Count - 1].map : current;

        /// The map in effect at client tick `tick`: the latest scheduled swap with applyTick <= tick, else
        /// the base. The reconcile replay calls this per tick so pre-boundary ticks route on the old map.
        public ControlMap MapAtTick(uint tick)
        {
            for (int i = entries.Count - 1; i >= 0; i--)
                if (!SeqMath.IsNewer32(entries[i].applyTick, tick))   // entries[i].applyTick <= tick
                    return entries[i].map;
            return baseMap;
        }

        /// Cancel all swaps and reset the base map. Called on death (base = checkpoint map) and on level
        /// load (base = Default) so a swap scheduled before the reset can never fire after it.
        public void ResetTo(ControlMap newBase)
        {
            entries.Clear();
            baseMap = newBase;
        }
    }
}
