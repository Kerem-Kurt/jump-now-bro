using NUnit.Framework;
using JumpNowBro.Util;

namespace JumpNowBro.Tests
{
    public class NetworkInputRingTests
    {
        static PlayerInputFrame Frame(bool jump = false, bool dash = false, bool right = false, bool held = false) =>
            new PlayerInputFrame { jumpPressed = jump, dashPressed = dash, moveRight = right, jumpHeld = held };

        [Test]
        public void Initial_TryConsumeNewest_ReturnsFalse()
        {
            var ring = new NetworkInputRing();
            Assert.IsFalse(ring.TryConsumeNewest(out _, out _));
            Assert.AreEqual(0u, ring.LastConsumedClientTick);
        }

        [Test]
        public void SingleFrame_ConsumedAndAdvancesLastTick()
        {
            var ring = new NetworkInputRing();
            ring.Enqueue(5, Frame(jump: true));
            Assert.IsTrue(ring.TryConsumeNewest(out var f, out var tick));
            Assert.AreEqual(5u, tick);
            Assert.IsTrue(f.jumpPressed);
            Assert.AreEqual(5u, ring.LastConsumedClientTick);
        }

        [Test]
        public void Consumed_NoLongerReturned()
        {
            var ring = new NetworkInputRing();
            ring.Enqueue(7, Frame(jump: true));
            Assert.IsTrue(ring.TryConsumeNewest(out _, out _));
            Assert.IsFalse(ring.TryConsumeNewest(out _, out _));    // consume-once
        }

        [Test]
        public void NewestUnconsumed_NotOldest_Wins()
        {
            var ring = new NetworkInputRing();
            ring.Enqueue(1, Frame(jump: true));
            ring.Enqueue(2, Frame(dash: true));
            ring.Enqueue(3, Frame(right: true));
            Assert.IsTrue(ring.TryConsumeNewest(out var f, out var tick));
            Assert.AreEqual(3u, tick);                              // newest, not oldest
            Assert.IsTrue(f.moveRight);
            Assert.IsFalse(f.jumpPressed);
        }

        [Test]
        public void RedundancyWindow_DropsAlreadyConsumed_AcceptsNew()
        {
            var ring = new NetworkInputRing();
            // First packet: [5..10]
            for (uint t = 5; t <= 10; t++) ring.Enqueue(t, Frame(held: true));
            Assert.IsTrue(ring.TryConsumeNewest(out _, out var tick1));
            Assert.AreEqual(10u, tick1);

            // Second packet: [8..13] — overlap on 8/9/10 (already consumed); 11/12/13 are new
            for (uint t = 8; t <= 13; t++) ring.Enqueue(t, Frame(jump: true));
            Assert.IsTrue(ring.TryConsumeNewest(out var f, out var tick2));
            Assert.AreEqual(13u, tick2);
            Assert.IsTrue(f.jumpPressed);
        }

        [Test]
        public void DuplicateTick_IsIdempotent()
        {
            var ring = new NetworkInputRing();
            ring.Enqueue(5, Frame(jump: true));
            ring.Enqueue(5, Frame(dash: true));                    // second enqueue at same tick: ignored
            Assert.IsTrue(ring.TryConsumeNewest(out var f, out _));
            Assert.IsTrue(f.jumpPressed);                          // first frame wins
            Assert.IsFalse(f.dashPressed);
        }

        [Test]
        public void OlderTick_DoesNotOverwriteNewerInSameSlot()
        {
            // tick=2 and tick=10 both map to slot 2 (% 8); tick=10 arrives first; tick=2 must NOT clobber.
            var ring = new NetworkInputRing();
            ring.Enqueue(10, Frame(jump: true));
            ring.Enqueue(2,  Frame(dash: true));
            Assert.IsTrue(ring.TryConsumeNewest(out var f, out var tick));
            Assert.AreEqual(10u, tick);
            Assert.IsTrue(f.jumpPressed);
        }

        [Test]
        public void NewerTick_OverwritesConsumedSlotInhabitant()
        {
            // Consumed slot must still be available to accept a newer-tick frame on ring-wrap.
            var ring = new NetworkInputRing();
            ring.Enqueue(10, Frame(jump: true));
            Assert.IsTrue(ring.TryConsumeNewest(out _, out _));   // consumes tick 10 in slot 2
            ring.Enqueue(18, Frame(dash: true));                  // tick 18 -> slot 2 again (18 % 8 == 2)
            Assert.IsTrue(ring.TryConsumeNewest(out var f, out var tick));
            Assert.AreEqual(18u, tick);
            Assert.IsTrue(f.dashPressed);
        }

        [Test]
        public void OutOfOrderEnqueue_StillReturnsNewest()
        {
            var ring = new NetworkInputRing();
            ring.Enqueue(9, Frame(jump: true));
            ring.Enqueue(7, Frame(dash: true));
            ring.Enqueue(8, Frame(right: true));
            Assert.IsTrue(ring.TryConsumeNewest(out var f, out var tick));
            Assert.AreEqual(9u, tick);
            Assert.IsTrue(f.jumpPressed);
        }

        [Test]
        public void StaleFrameAfterConsume_Rejected()
        {
            var ring = new NetworkInputRing();
            ring.Enqueue(10, Frame());
            ring.TryConsumeNewest(out _, out _);                   // LastConsumed = 10
            ring.Enqueue(5, Frame(jump: true));                    // older — must reject
            Assert.IsFalse(ring.TryConsumeNewest(out _, out _));
        }

        [Test]
        public void RingWrap_CapacityFrames_NewestSurvives()
        {
            var ring = new NetworkInputRing();
            for (uint t = 1; t <= 16; t++) ring.Enqueue(t, Frame(held: true));   // 2× capacity inserts
            Assert.IsTrue(ring.TryConsumeNewest(out _, out var tick));
            Assert.AreEqual(16u, tick);                                          // newest survives ring-wrap
        }

        [Test]
        public void Starvation_AfterConsume_ReturnsFalse_PreservingLastTick()
        {
            var ring = new NetworkInputRing();
            ring.Enqueue(5, Frame());
            Assert.IsTrue(ring.TryConsumeNewest(out _, out _));
            Assert.IsFalse(ring.TryConsumeNewest(out _, out _));   // no new frames
            Assert.AreEqual(5u, ring.LastConsumedClientTick);      // unchanged on starvation
        }
    }
}
