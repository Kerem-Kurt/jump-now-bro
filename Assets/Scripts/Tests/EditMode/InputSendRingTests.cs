using System;
using NUnit.Framework;
using JumpNowBro.Networking;
using JumpNowBro.Util;

namespace JumpNowBro.Tests
{
    public class InputSendRingTests
    {
        static PlayerInputFrame Frame(bool jump = false, bool dash = false, bool right = false, bool held = false) =>
            new PlayerInputFrame { jumpPressed = jump, dashPressed = dash, moveRight = right, jumpHeld = held };

        [Test]
        public void Tick0_WindowSize1_BaseTick0()
        {
            var ring = new InputSendRing();
            var buf = new byte[InputBody.HeaderSize + InputSendRing.K];
            int n = ring.Sample(0, Frame(jump: true), buf);

            Assert.IsTrue(InputBody.TryRead(buf.AsSpan(0, n), out var baseTick, out var count, out var frames));
            Assert.AreEqual(0u, baseTick);
            Assert.AreEqual(1, count);
            Assert.IsTrue(PlayerInputFrame.Unpack(frames[0]).jumpPressed);
        }

        [Test]
        public void Tick4_WindowSize5_BaseTick0()
        {
            // Before the ring fills (tick < K-1 = 5), the window starts at 0 and grows to tick+1.
            var ring = new InputSendRing();
            var buf = new byte[InputBody.HeaderSize + InputSendRing.K];
            for (uint t = 0; t <= 4; t++) ring.Sample(t, Frame(), buf);
            int n = ring.Sample(4, Frame(jump: true), buf);

            Assert.IsTrue(InputBody.TryRead(buf.AsSpan(0, n), out var baseTick, out var count, out _));
            Assert.AreEqual(0u, baseTick);
            Assert.AreEqual(5, count);
        }

        [Test]
        public void Tick10_FullWindowK6_BaseTickIsTickMinus5()
        {
            var ring = new InputSendRing();
            var buf = new byte[InputBody.HeaderSize + InputSendRing.K];
            for (uint t = 0; t <= 10; t++) ring.Sample(t, Frame(), buf);
            int n = ring.Sample(10, Frame(), buf);

            Assert.IsTrue(InputBody.TryRead(buf.AsSpan(0, n), out var baseTick, out var count, out _));
            Assert.AreEqual(5u, baseTick);                     // 10 - (K-1) = 10 - 5 = 5
            Assert.AreEqual(InputSendRing.K, count);
        }

        [Test]
        public void FramesEmitted_InTickOrder()
        {
            var ring = new InputSendRing();
            var buf = new byte[InputBody.HeaderSize + InputSendRing.K];

            // Stamp tick T with a unique bit pattern via held (bit 3) toggling on even/odd ticks.
            for (uint t = 0; t <= 5; t++)
                ring.Sample(t, Frame(held: t % 2 == 0), buf);

            int n = ring.Sample(5, Frame(held: true), buf);
            Assert.IsTrue(InputBody.TryRead(buf.AsSpan(0, n), out var baseTick, out var count, out var frames));
            Assert.AreEqual(0u, baseTick);
            Assert.AreEqual(6, count);
            for (int i = 0; i < count; i++)
            {
                var f = PlayerInputFrame.Unpack(frames[i]);
                bool expectedHeld = (baseTick + (uint)i) % 2 == 0 || (baseTick + (uint)i) == 5;
                Assert.AreEqual(expectedHeld, f.jumpHeld, $"frame[{i}] (tick {baseTick + i})");
            }
        }

        // ----- end-to-end edge survival -----

        [Test]
        public void DocumentsV14Limit_EdgeInOlderFrame_LostUnderNewestUnconsumed()
        {
            // v1.4 semantic NOTE: newest-unconsumed prefers minimum input latency over edge preservation.
            // Under K-1 consecutive drops, the late-arriving packet carries the old edge frame, but the
            // host picks the newest in-window and the older edge is discarded. Acceptable on LAN
            // (5% loss → ~3e-8 chance of K consecutive drops); revisit in v1.5/v1.6 if stress-testing
            // surfaces it (OR-edges-across-unconsumed-window is the standard fix).
            var sendRing = new InputSendRing();
            var hostRing = new NetworkInputRing();
            var buf = new byte[InputBody.HeaderSize + InputSendRing.K];

            for (uint t = 0; t <= 10; t++)
            {
                var f = (t == 5) ? Frame(jump: true) : Frame();
                int n = sendRing.Sample(t, f, buf);
                bool delivered = t == 10;                            // simulate drops 5..9, deliver only packet 10
                if (delivered)
                {
                    Assert.IsTrue(InputBody.TryRead(buf.AsSpan(0, n), out var baseTick, out var count, out var frames));
                    for (int i = 0; i < count; i++)
                        hostRing.Enqueue(baseTick + (uint)i, PlayerInputFrame.Unpack(frames[i]));
                }
            }

            Assert.IsTrue(hostRing.TryConsumeNewest(out var picked, out var tick));
            Assert.AreEqual(10u, tick);
            Assert.IsFalse(picked.jumpPressed);                      // edge dropped — documented v1.4 limit
        }

        [Test]
        public void EdgePress_NoDrops_DeliversOnFirstPacket()
        {
            // Sanity check: no drops, host's first consume on tick T applies tick T's edge.
            var sendRing = new InputSendRing();
            var hostRing = new NetworkInputRing();
            var buf = new byte[InputBody.HeaderSize + InputSendRing.K];

            int n = sendRing.Sample(3, Frame(jump: true), buf);
            Assert.IsTrue(InputBody.TryRead(buf.AsSpan(0, n), out var baseTick, out var count, out var frames));
            for (int i = 0; i < count; i++)
                hostRing.Enqueue(baseTick + (uint)i, PlayerInputFrame.Unpack(frames[i]));

            Assert.IsTrue(hostRing.TryConsumeNewest(out var picked, out var tick));
            Assert.AreEqual(3u, tick);
            Assert.IsTrue(picked.jumpPressed);
        }
    }
}
