using System;
using System.Collections.Generic;
using NUnit.Framework;
using JumpNowBro.Networking;

namespace JumpNowBro.Tests
{
    public class NetworkConditionChannelTests
    {
        sealed class CountingSink : IDatagramChannel
        {
            public int Count;
            public void Send(ReadOnlySpan<byte> d) => Count++;
            public bool TryReceive(out byte[] d) { d = null; return false; }
        }

        [Test]
        public void InjectedLatency_ShowsUpInRtt()
        {
            var (ia, ib) = InMemoryDatagramChannel.Pair();
            const double L = 0.1;   // 100 ms one way
            var ca = new NetworkConditionChannel(ia, latencySeconds: L);
            var cb = new NetworkConditionChannel(ib, latencySeconds: L);
            var a = new UdpReliableTransport(ca, pingIntervalSeconds: 0.2);
            var b = new UdpReliableTransport(cb, pingIntervalSeconds: 0.2);

            double now = 0;
            for (int i = 0; i < 200; i++)
            {
                now += 0.016;
                ca.Release(now);
                cb.Release(now);
                a.Tick(0.016f);
                b.Tick(0.016f);
            }

            Assert.That(a.RttSeconds, Is.EqualTo(2 * L).Within(0.05));   // ~200 ms round trip, ± ms/tick quantization
        }

        [Test]
        public void Loss_IsDeterministicPerSeed_AndActuallyDrops()
        {
            int Delivered(int seed)
            {
                var sink = new CountingSink();
                var ch = new NetworkConditionChannel(sink, lossProb: 0.5, seed: seed);
                double clock = 0;
                for (int i = 0; i < 200; i++) { ch.Send(new byte[] { (byte)i }); clock += 0.016; ch.Release(clock); }
                return sink.Count;
            }

            Assert.AreEqual(Delivered(7), Delivered(7));   // same seed -> identical pattern
            int d = Delivered(7);
            Assert.Less(d, 200);                           // loss actually happened
            Assert.Greater(d, 0);                          // but not everything was dropped
        }
    }
}
