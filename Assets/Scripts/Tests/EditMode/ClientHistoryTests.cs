using NUnit.Framework;
using JumpNowBro.Util;

namespace JumpNowBro.Tests
{
    public class ClientHistoryTests
    {
        static PlayerInputFrame Frame(bool jump = false, bool right = false) =>
            new PlayerInputFrame { jumpPressed = jump, moveRight = right };

        static MovementState State(float x) => new MovementState { posX = x };

        [Test]
        public void RecordThenGet_RoundTripsInputAndState()
        {
            var h = new ClientHistory();
            h.RecordInput(5, Frame(jump: true));
            h.RecordPredicted(5, State(3f));

            Assert.IsTrue(h.TryGetInput(5, out var f));
            Assert.IsTrue(f.jumpPressed);
            Assert.IsTrue(h.TryGetPredicted(5, out var s));
            Assert.AreEqual(3f, s.posX);
        }

        [Test]
        public void UnseenTick_TryGetReturnsFalse()
        {
            var h = new ClientHistory();
            Assert.IsFalse(h.TryGetInput(9, out _));
            Assert.IsFalse(h.TryGetPredicted(9, out _));
            Assert.IsFalse(h.HasInput(9));
        }

        [Test]
        public void Wraparound_OldEntryOverwritten_StaleTickRejected()
        {
            // tick 5 and tick 5+Capacity map to the same slot; the newer write evicts the older.
            var h = new ClientHistory();
            h.RecordInput(5, Frame(jump: true));
            h.RecordInput(5 + (uint)ClientHistory.Capacity, Frame(right: true));

            Assert.IsFalse(h.TryGetInput(5, out _));                              // stale slot rejected, not read as real
            Assert.IsTrue(h.TryGetInput(5 + (uint)ClientHistory.Capacity, out var f));
            Assert.IsTrue(f.moveRight);
        }

        [Test]
        public void HasInput_TracksRecordedWindow_ForReplayHoleCheck()
        {
            var h = new ClientHistory();
            for (uint t = 10; t < 20; t++) h.RecordInput(t, Frame());
            for (uint t = 10; t < 20; t++) Assert.IsTrue(h.HasInput(t), $"tick {t} should be present");
            Assert.IsFalse(h.HasInput(20));                                       // a hole the reconciler must hard-snap on
        }
    }
}
