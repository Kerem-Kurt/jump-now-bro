using System;
using System.Collections.Generic;

namespace JumpNowBro.Networking
{
    /// The one network-condition simulator: an IDatagramChannel decorator that injects latency, jitter,
    /// loss, duplication, and (via jitter) reorder. Engine-free and seeded by a small hand-rolled LCG —
    /// NOT System.Random, whose algorithm differs between Unity's Mono and the net8.0 CI — so a seed
    /// reproduces the same pattern everywhere. Outbound datagrams are held and released by Release(now),
    /// which must be driven by the SAME accumulated-dt clock as the transport, called BEFORE its tick.
    public sealed class NetworkConditionChannel : IDatagramChannel
    {
        sealed class Lcg
        {
            ulong s;
            public Lcg(int seed) { s = (ulong)seed * 6364136223846793005UL + 1442695040888963407UL; }
            public double NextDouble()
            {
                s = s * 6364136223846793005UL + 1442695040888963407UL;
                return (s >> 11) * (1.0 / 9007199254740992.0);   // top 53 bits -> [0,1)
            }
        }

        readonly IDatagramChannel inner;
        readonly Lcg rng;
        readonly double latency;
        readonly double jitter;
        readonly double lossProb;
        readonly double dupProb;
        readonly List<(double at, byte[] data)> pending = new List<(double, byte[])>();
        double now;

        public NetworkConditionChannel(IDatagramChannel inner, double latencySeconds = 0, double jitterSeconds = 0,
                                       double lossProb = 0, double dupProb = 0, int seed = 1)
        {
            this.inner = inner;
            latency = latencySeconds;
            jitter = jitterSeconds;
            this.lossProb = lossProb;
            this.dupProb = dupProb;
            rng = new Lcg(seed);
        }

        public void Send(ReadOnlySpan<byte> datagram)
        {
            if (rng.NextDouble() < lossProb) return;                 // drop
            var bytes = datagram.ToArray();
            Schedule(bytes);
            if (rng.NextDouble() < dupProb) Schedule(bytes);         // duplicate
        }

        /// Advance the channel clock and deliver datagrams whose latency has elapsed. Call once per frame,
        /// BEFORE the transport tick, from the transport's own accumulated-dt clock.
        public void Release(double nowSeconds)
        {
            now = nowSeconds;
            for (int i = 0; i < pending.Count; )
            {
                if (pending[i].at <= now) { inner.Send(pending[i].data); pending.RemoveAt(i); }
                else i++;
            }
        }

        public bool TryReceive(out byte[] datagram) => inner.TryReceive(out datagram);

        void Schedule(byte[] bytes)
        {
            double delay = latency;
            if (jitter > 0) delay += (rng.NextDouble() * 2 - 1) * jitter;   // jitter varies release times -> reorder
            if (delay < 0) delay = 0;
            pending.Add((now + delay, bytes));
        }
    }
}
