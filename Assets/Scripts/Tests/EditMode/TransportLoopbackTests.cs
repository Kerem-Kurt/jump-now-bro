using System.Collections.Generic;
using NUnit.Framework;
using JumpNowBro.Networking;

namespace JumpNowBro.Tests
{
    /// End-to-end transport guarantees over the condition simulator (loss + dup + jitter-reorder). Deterministic per seed.
    public class TransportLoopbackTests
    {
        static byte[] I32(int v) => new[] { (byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v };
        static int ToI32(byte[] b) => (b[0] << 24) | (b[1] << 16) | (b[2] << 8) | b[3];

        static void Drain(UdpReliableTransport t, List<int> events, List<int> states)
        {
            while (t.TryReceive(out var type, out var payload))
            {
                if (type == MessageType.Event) events?.Add(ToI32(payload));
                else if (type == MessageType.State) states?.Add(ToI32(payload));
            }
        }

        [TestCase(1)]
        [TestCase(7)]
        [TestCase(42)]
        public void Reliable_InOrderExactlyOnce_Unreliable_LatestWins_UnderLossReorderDup(int seed)
        {
            var (ia, ib) = InMemoryDatagramChannel.Pair();
            var ca = new NetworkConditionChannel(ia, jitterSeconds: 0.03, lossProb: 0.25, dupProb: 0.10, seed: seed);
            var cb = new NetworkConditionChannel(ib, jitterSeconds: 0.03, lossProb: 0.25, dupProb: 0.10, seed: seed * 31 + 1);
            var a = new UdpReliableTransport(ca);
            var b = new UdpReliableTransport(cb);

            bool aDisconnected = false, bDisconnected = false;
            a.OnDisconnected += () => aDisconnected = true;
            b.OnDisconnected += () => bDisconnected = true;

            const int eventCount = 30;
            const int eventEvery = 8;       // ticks between EVENTs
            const int totalTicks = 2000;    // generous: send window + retransmit/ack recovery under loss

            var events = new List<int>();
            var states = new List<int>();
            int nextEventId = 1;
            int stateMarker = 1;
            double now = 0;

            for (int tick = 0; tick < totalTicks; tick++)
            {
                now += 0.016;
                ca.Release(now);
                cb.Release(now);                                                   // deliver due datagrams to the inner channels

                a.Send(Channel.Unreliable, MessageType.State, I32(stateMarker++));  // A->B: ever-increasing markers
                b.Send(Channel.Unreliable, MessageType.State, I32(0));             // B->A: keeps acks piggybacking home
                if (tick % eventEvery == 0 && nextEventId <= eventCount)
                    a.Send(Channel.Reliable, MessageType.Event, I32(nextEventId++));

                a.Tick(0.016f);
                b.Tick(0.016f);

                Drain(a, null, null);
                Drain(b, events, states);
            }

            // Reliable: every EVENT delivered, in order, exactly once — no gaps, no dups, no extras.
            var expected = new List<int>();
            for (int i = 1; i <= eventCount; i++) expected.Add(i);
            CollectionAssert.AreEqual(expected, events);

            // Unreliable: latest-wins never regresses (gaps from loss are fine; going backwards is not).
            for (int i = 1; i < states.Count; i++)
                Assert.Greater(states[i], states[i - 1], $"STATE regressed at index {i} (seed {seed})");

            Assert.Greater(states.Count, 0, "no STATE delivered at all");
            Assert.IsFalse(aDisconnected, "A gave up / timed out");
            Assert.IsFalse(bDisconnected, "B gave up / timed out");
        }
    }
}
