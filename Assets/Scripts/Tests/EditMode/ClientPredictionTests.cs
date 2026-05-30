using NUnit.Framework;
using JumpNowBro.Util;
using JumpNowBro.Tests.CollisionWorlds;

namespace JumpNowBro.Tests
{
    public class ClientPredictionTests
    {
        // Mirrors PlayerTuning's default asset values (same source as MovementTests).
        static MovementTuning Tuning() => new MovementTuning
        {
            runSpeed                    = 9f,
            airControlMultiplier        = 0.85f,
            jumpVelocity                = 16f,
            gravity                     = 50f,
            coyoteTime                  = 0.1f,
            jumpBufferTime              = 0.1f,
            variableJumpCutMultiplier   = 0.5f,
            dashDistance                = 4f,
            dashDuration                = 0.15f,
            dashInvulnerabilityDuration = 0.15f,
            dashFreezeTicks             = 3,
            fallLimitY                  = -20f,
        };

        // Feet at y=0 in FakeFlatGroundWorld means center y=1 (BoxCollider 1×2).
        static MovementState Grounded() => new MovementState
        {
            state = MoveState.Grounded, facing = 1, dashChargeAvailable = true, posY = 1f,
        };

        static PlayerInputFrame Frame(bool left = false, bool right = false, bool jump = false, bool held = false, bool dash = false) =>
            new PlayerInputFrame { moveLeft = left, moveRight = right, jumpPressed = jump, jumpHeld = held, dashPressed = dash };

        const float Dt = 1f / 60f;
        static readonly PlayerInputFrame Idle = Frame();

        [Test]
        public void DeadReckonHost_StripsEdges_ForcesJumpHeld()
        {
            var dr = ClientPrediction.DeadReckonHost(Frame(right: true, jump: true, held: false, dash: true));
            Assert.IsTrue(dr.moveRight,    "held level bits pass through");
            Assert.IsFalse(dr.jumpPressed, "host jump edge stripped");
            Assert.IsFalse(dr.dashPressed, "host dash edge stripped");
            Assert.IsTrue(dr.jumpHeld,     "jumpHeld forced true so a host-owned jump's variable-cut never fires");
        }

        [Test]
        public void ClientOwnedMove_AdvancesPosition_NoRoundTrip()
        {
            // Move swapped to the client (P2); host idle. The client's own right input must move the body now.
            var map = ControlMap.WithSwap(ControlMap.Default, PlayerAction.MoveHorizontal);
            var s = Grounded();
            var (next, _) = ClientPrediction.PredictStep(s, map, Frame(right: true), Idle, Tuning(), Dt, new FakeFlatGroundWorld());
            Assert.Greater(next.posX, s.posX, "client-owned right input advances X with no host involvement");
        }

        [Test]
        public void HostOwnedJumpEdge_NeverPredicted()
        {
            // Default map: host owns jump. Host frame asserts a jump edge — it must NOT produce a client jump.
            var s = Grounded();
            var (next, edges) = ClientPrediction.PredictStep(s, ControlMap.Default, Idle, Frame(jump: true), Tuning(), Dt, new FakeFlatGroundWorld());
            Assert.IsFalse((edges & EdgeFlags.JumpedThisTick) != 0, "host-owned jump edge must not be predicted");
            Assert.AreNotEqual(MoveState.Jumping, next.state);
        }

        [Test]
        public void ClientOwnedJump_IsPredicted()
        {
            // Jump swapped to the client (P2); the client's own jump edge fires immediately (instant feel).
            var map = ControlMap.WithSwap(ControlMap.Default, PlayerAction.Jump);
            var s = Grounded();
            var (next, edges) = ClientPrediction.PredictStep(s, map, Frame(jump: true), Idle, Tuning(), Dt, new FakeFlatGroundWorld());
            Assert.IsTrue((edges & EdgeFlags.JumpedThisTick) != 0, "client-owned jump edge predicts a jump");
            Assert.AreEqual(MoveState.Jumping, next.state);
        }

        [Test]
        public void HostOwnedJump_VariableCut_DoesNotFireOnClient()
        {
            // Mid-jump, host owns jump, and the host frame reads "released" (jumpHeld=false). A naive dead-reckon
            // would fire the variable-jump cut and corrupt the client's vertical; DeadReckonHost forces jumpHeld
            // true so the cut never fires — host-owned vertical stays authoritative-only.
            var s = new MovementState { state = MoveState.Jumping, facing = 1, velY = 10f, wasJumpHeld = true };
            var sky = new ConfigurableGroundWorld { isGrounded = false };
            var (next, edges) = ClientPrediction.PredictStep(s, ControlMap.Default, Idle, Frame(held: false), Tuning(), Dt, sky);
            Assert.IsFalse((edges & EdgeFlags.JumpCutThisTick) != 0, "host-owned jump's variable-cut must not fire on the client");
            Assert.Greater(next.velY, 10f - Tuning().gravity * Dt - 0.001f, "velY only loses gravity, not a half-cut");
        }
    }
}
