using System;
using System.Collections.Generic;
using JumpNowBro.Networking;

namespace JumpNowBro.Tests
{
    /// Paired IDatagramChannel for adversarial transport tests: drops, duplicates, and reorders outgoing
    /// datagrams from a seeded PRNG. The PRNG is a tiny LCG (not System.Random, whose algorithm differs
    /// between Unity's Mono runtime and the net8.0 CI) so the loss pattern is identical everywhere — same
    /// seed, same result, in both runtimes.
    sealed class LossyDatagramChannel : IDatagramChannel
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
            public int Next(int exclusiveMax) => (int)(NextDouble() * exclusiveMax);
        }

        readonly List<byte[]> inbox = new List<byte[]>();
        readonly Lcg rng;
        readonly double dropProb;
        readonly double dupProb;
        LossyDatagramChannel peer;

        LossyDatagramChannel(Lcg rng, double dropProb, double dupProb)
        {
            this.rng = rng;
            this.dropProb = dropProb;
            this.dupProb = dupProb;
        }

        public static (LossyDatagramChannel a, LossyDatagramChannel b) Pair(int seed, double dropProb, double dupProb)
        {
            var rng = new Lcg(seed);   // one stream shared by both ends; the fixed pump order keeps it deterministic
            var a = new LossyDatagramChannel(rng, dropProb, dupProb);
            var b = new LossyDatagramChannel(rng, dropProb, dupProb);
            a.peer = b;
            b.peer = a;
            return (a, b);
        }

        public void Send(ReadOnlySpan<byte> datagram)
        {
            if (rng.NextDouble() < dropProb) return;                 // drop
            var bytes = datagram.ToArray();
            Deliver(bytes);
            if (rng.NextDouble() < dupProb) Deliver(bytes);          // duplicate
        }

        void Deliver(byte[] datagram)
        {
            int at = rng.Next(peer.inbox.Count + 1);                 // random insert position -> reorder
            peer.inbox.Insert(at, datagram);
        }

        public bool TryReceive(out byte[] datagram)
        {
            if (inbox.Count == 0) { datagram = null; return false; }
            datagram = inbox[0];
            inbox.RemoveAt(0);
            return true;
        }
    }
}
