using NUnit.Framework;
using JumpNowBro.Util;

namespace JumpNowBro.Tests
{
    public class SwapDiffTests
    {
        [Test]
        public void IdenticalMaps_NoChange()
        {
            Assert.IsNull(SwapDiff.FirstChange(ControlMap.Default, ControlMap.Default));
        }

        [Test]
        public void JumpSwapped_ReportsJumpToP2()
        {
            var after = ControlMap.WithSwap(ControlMap.Default, PlayerAction.Jump);
            var ch = SwapDiff.FirstChange(ControlMap.Default, after);
            Assert.IsTrue(ch.HasValue);
            Assert.AreEqual(PlayerAction.Jump, ch.Value.action);
            Assert.AreEqual(InputOwner.P2, ch.Value.newOwner);
        }

        [Test]
        public void MoveSwapped_ReportsMoveToP2()
        {
            var after = ControlMap.WithSwap(ControlMap.Default, PlayerAction.MoveHorizontal);
            var ch = SwapDiff.FirstChange(ControlMap.Default, after);
            Assert.IsTrue(ch.HasValue);
            Assert.AreEqual(PlayerAction.MoveHorizontal, ch.Value.action);
            Assert.AreEqual(InputOwner.P2, ch.Value.newOwner);
        }

        [Test]
        public void DashSwapped_ReportsDashToP2()
        {
            var after = ControlMap.WithSwap(ControlMap.Default, PlayerAction.Dash);
            var ch = SwapDiff.FirstChange(ControlMap.Default, after);
            Assert.IsTrue(ch.HasValue);
            Assert.AreEqual(PlayerAction.Dash, ch.Value.action);
            Assert.AreEqual(InputOwner.P2, ch.Value.newOwner);
        }

        [Test]
        public void SwapBack_ReportsReturnToP1()
        {
            var swapped = ControlMap.WithSwap(ControlMap.Default, PlayerAction.Jump);   // P1 -> P2
            var back = ControlMap.WithSwap(swapped, PlayerAction.Jump);                 // P2 -> P1
            var ch = SwapDiff.FirstChange(swapped, back);
            Assert.IsTrue(ch.HasValue);
            Assert.AreEqual(PlayerAction.Jump, ch.Value.action);
            Assert.AreEqual(InputOwner.P1, ch.Value.newOwner);
        }

        [Test]
        public void DistinctActions_ReportedIndependently()
        {
            // Move on P2, jump still P1: a map differing only in jump reports jump, not move.
            var moveOnP2 = ControlMap.WithSwap(ControlMap.Default, PlayerAction.MoveHorizontal);
            var alsoJumpP2 = ControlMap.WithSwap(moveOnP2, PlayerAction.Jump);
            var ch = SwapDiff.FirstChange(moveOnP2, alsoJumpP2);
            Assert.IsTrue(ch.HasValue);
            Assert.AreEqual(PlayerAction.Jump, ch.Value.action);
            Assert.AreEqual(InputOwner.P2, ch.Value.newOwner);
        }
    }
}
