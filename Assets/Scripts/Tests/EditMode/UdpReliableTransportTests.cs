using NUnit.Framework;
using JumpNowBro.Networking;

namespace JumpNowBro.Tests
{
    public class UdpReliableTransportTests
    {
        // Step both transports n times at a fixed dt, carrying datagrams across the paired channels.
        static void Pump(UdpReliableTransport a, UdpReliableTransport b, int steps, float dt = 0.016f)
        {
            for (int i = 0; i < steps; i++) { a.Tick(dt); b.Tick(dt); }
        }

        [Test]
        public void Reliable_RoundTrips()
        {
            var (ca, cb) = InMemoryDatagramChannel.Pair();
            var a = new UdpReliableTransport(ca);
            var b = new UdpReliableTransport(cb);

            a.Send(Channel.Reliable, MessageType.Event, new byte[] { 1, 2, 3 });
            Pump(a, b, 3);

            Assert.IsTrue(b.TryReceive(out var type, out var payload));
            Assert.AreEqual(MessageType.Event, type);
            Assert.AreEqual(new byte[] { 1, 2, 3 }, payload);
            Assert.IsFalse(b.TryReceive(out _, out _));
        }

        [Test]
        public void Reliable_SurvivesADroppedDatagram_DeliveredExactlyOnce()
        {
            var (ca, cb) = InMemoryDatagramChannel.Pair();
            var a = new UdpReliableTransport(ca);
            var b = new UdpReliableTransport(cb);

            ca.DropNextSends = 1;                       // lose the EVENT's first transmit
            a.Send(Channel.Reliable, MessageType.Event, new byte[] { 9 });
            Pump(a, b, 90);                             // well past the retransmit timeout

            Assert.IsTrue(b.TryReceive(out _, out var payload));
            Assert.AreEqual(new byte[] { 9 }, payload);
            Assert.IsFalse(b.TryReceive(out _, out _));  // retransmits are de-duplicated
        }

        [Test]
        public void Reliable_AcksDrainTheSendQueue()
        {
            var (ca, cb) = InMemoryDatagramChannel.Pair();
            var a = new UdpReliableTransport(ca);
            var b = new UdpReliableTransport(cb);

            a.Send(Channel.Reliable, MessageType.Event, new byte[] { 5 });
            Pump(a, b, 10);                             // round trip + ack comes back

            Assert.AreEqual(0, a.PendingReliableCount); // b's ack removed it from a's queue
        }

        [Test]
        public void Unreliable_IsLatestWins_StalePacketDropped()
        {
            var (ca, cb) = InMemoryDatagramChannel.Pair();
            var a = new UdpReliableTransport(ca);
            var b = new UdpReliableTransport(cb);

            cb.Lifo = true;                             // b drains newest-first: the older packet arrives last
            a.Send(Channel.Unreliable, MessageType.State, new byte[] { 1 });   // packet seq 1
            a.Send(Channel.Unreliable, MessageType.State, new byte[] { 2 });   // packet seq 2
            b.Tick(0.016f);                             // processes seq 2 then seq 1

            Assert.IsTrue(b.TryReceive(out _, out var payload));
            Assert.AreEqual(new byte[] { 2 }, payload);  // newest delivered
            Assert.IsFalse(b.TryReceive(out _, out _));  // the stale seq 1 was dropped
        }

        [Test]
        public void Rtt_TracksPingPong()
        {
            var (ca, cb) = InMemoryDatagramChannel.Pair();
            var a = new UdpReliableTransport(ca);
            var b = new UdpReliableTransport(cb);

            Pump(a, b, 10);                             // a PING goes out, a PONG echoes back, RTT is sampled

            Assert.That(a.RttSeconds, Is.GreaterThan(0f).And.LessThan(0.1f)); // moved off the 0.1 s default to the tiny loopback RTT
        }

        [Test]
        public void UnknownType_Dropped_AfterAckHarvest()
        {
            var (ca, cb) = InMemoryDatagramChannel.Pair();
            var b = new UdpReliableTransport(cb);
            // 11-byte header with an out-of-range type byte (99), seq=1; inject raw into b's receive path.
            var dg = new byte[PacketHeader.Size + 1];
            dg[0] = 99;
            dg[2] = 1;                                  // seq lo = 1
            ca.Send(dg);
            b.Tick(0.016f);
            Assert.AreEqual(1, b.DroppedDatagrams);
            Assert.IsFalse(b.TryReceive(out _, out _));  // not dispatched
        }

        [Test]
        public void OversizedSend_CountsAndLogs_DoesNotThrow()
        {
            var (ca, _) = InMemoryDatagramChannel.Pair();
            var a = new UdpReliableTransport(ca);
            string logged = null;
            a.Logger = m => logged = m;
            Assert.DoesNotThrow(() => a.Send(Channel.Unreliable, MessageType.State, new byte[2000]));  // over the 1200 ceiling
            Assert.GreaterOrEqual(a.OversizedSends, 1);
            Assert.IsNotNull(logged);
        }

        [Test]
        public void Fuzz_RandomDatagrams_NeverThrow()
        {
            var (ca, cb) = InMemoryDatagramChannel.Pair();
            var b = new UdpReliableTransport(cb);
            var rng = new System.Random(999);
            Assert.DoesNotThrow(() =>
            {
                for (int i = 0; i < 5000; i++)
                {
                    var dg = new byte[rng.Next(0, 64)];
                    rng.NextBytes(dg);
                    ca.Send(dg);                        // inject raw garbage into b's receive path
                    b.Tick(0.016f);
                    while (b.TryReceive(out _, out _)) { }   // drain whatever parsed
                }
            });
        }
    }
}
