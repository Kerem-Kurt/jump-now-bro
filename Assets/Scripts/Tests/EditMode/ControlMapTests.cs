using NUnit.Framework;
using JumpNowBro.Util;

namespace JumpNowBro.Tests
{
    public class ControlMapTests
    {
        [Test]
        public void Default_HasAllActionsOnP1()
        {
            var map = ControlMap.Default;
            Assert.AreEqual(InputOwner.P1, map.moveOwner);
            Assert.AreEqual(InputOwner.P1, map.jumpOwner);
            Assert.AreEqual(InputOwner.P1, map.dashOwner);
        }

        [Test]
        public void WithSwap_DefaultMap_FlipsOnlyJumpOwner_WhenActionIsJump()
        {
            var result = ControlMap.WithSwap(ControlMap.Default, PlayerAction.Jump);
            Assert.AreEqual(InputOwner.P1, result.moveOwner);
            Assert.AreEqual(InputOwner.P2, result.jumpOwner);
            Assert.AreEqual(InputOwner.P1, result.dashOwner);
        }

        [Test]
        public void WithSwap_DoubleSwap_RestoresOriginal()
        {
            var original = ControlMap.Default;
            var swapped = ControlMap.WithSwap(original, PlayerAction.Jump);
            var back = ControlMap.WithSwap(swapped, PlayerAction.Jump);
            Assert.AreEqual(original.moveOwner, back.moveOwner);
            Assert.AreEqual(original.jumpOwner, back.jumpOwner);
            Assert.AreEqual(original.dashOwner, back.dashOwner);
        }

        [Test]
        public void WithSwap_DoesNotMutateInput()
        {
            var original = ControlMap.Default;
            _ = ControlMap.WithSwap(original, PlayerAction.Jump);
            Assert.AreEqual(InputOwner.P1, original.moveOwner);
            Assert.AreEqual(InputOwner.P1, original.jumpOwner);
            Assert.AreEqual(InputOwner.P1, original.dashOwner);
        }

        [Test]
        public void WithSwap_EachAction_FlipsCorrectOwner()
        {
            var moveSwap = ControlMap.WithSwap(ControlMap.Default, PlayerAction.MoveHorizontal);
            Assert.AreEqual(InputOwner.P2, moveSwap.moveOwner);
            Assert.AreEqual(InputOwner.P1, moveSwap.jumpOwner);
            Assert.AreEqual(InputOwner.P1, moveSwap.dashOwner);

            var dashSwap = ControlMap.WithSwap(ControlMap.Default, PlayerAction.Dash);
            Assert.AreEqual(InputOwner.P1, dashSwap.moveOwner);
            Assert.AreEqual(InputOwner.P1, dashSwap.jumpOwner);
            Assert.AreEqual(InputOwner.P2, dashSwap.dashOwner);
        }
    }
}
