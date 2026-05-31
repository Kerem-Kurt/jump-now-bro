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

        // ---- reconciliation (#81) ----

        // Client owns Move (P2) so its right input drives X in both the live prediction and the replay.
        static ControlMap ClientOwnsMove() => ControlMap.WithSwap(ControlMap.Default, PlayerAction.MoveHorizontal);

        [Test]
        public void Reconcile_HostConfirmedThroughNow_TakesAuthority_NoReplay()
        {
            var auth = Grounded();
            auth.posX = 4f;
            var r = ClientPrediction.Reconcile(auth, lastConsumedClientTick: 10, currentClientTick: 10,
                new ClientHistory(), _ => ControlMap.Default, Idle, Tuning(), Dt, new FakeFlatGroundWorld());
            Assert.IsFalse(r.HardSnapped);
            Assert.AreEqual(0, r.ReplayedTicks);
            Assert.AreEqual(4f, r.State.posX, 0.0001f);
        }

        [Test]
        public void Reconcile_ReplaysBufferedInputs_ToCurrentTick()
        {
            // Buffer 5 ticks of "hold right" (client owns Move), then reconcile from an authoritative seed at C=10.
            var map = ClientOwnsMove();
            var t = Tuning();
            var w = new FakeFlatGroundWorld();
            var history = new ClientHistory();
            for (uint tk = 11; tk <= 15; tk++) history.RecordInput(tk, Frame(right: true));

            var auth = Grounded();                                  // authoritative at C=10, x=0
            var r = ClientPrediction.Reconcile(auth, 10, 15, history, _ => map, Idle, t, Dt, w);

            Assert.IsFalse(r.HardSnapped);
            Assert.AreEqual(5, r.ReplayedTicks);

            // Independently step the same 5 right-inputs from the same seed → replay must match exactly.
            var expected = auth;
            for (int i = 0; i < 5; i++)
                (expected, _) = ClientPrediction.PredictStep(expected, map, Frame(right: true), Idle, t, Dt, w);
            Assert.AreEqual(expected.posX, r.State.posX, 0.0001f, "replayed state == fresh stepping of the buffered inputs");
            Assert.Greater(r.State.posX, auth.posX, "held-right replay advanced X past the authoritative seed");
        }

        [Test]
        public void Reconcile_WindowOverCap_HardSnaps_NoReplay()
        {
            var auth = Grounded();
            var r = ClientPrediction.Reconcile(auth, 0, (uint)(ClientPrediction.DefaultReplayCap + 1),
                new ClientHistory(), _ => ControlMap.Default, Idle, Tuning(), Dt, new FakeFlatGroundWorld());
            Assert.IsTrue(r.HardSnapped);
            Assert.AreEqual(0, r.ReplayedTicks);
            Assert.AreEqual(auth.posX, r.State.posX, 0.0001f);
        }

        [Test]
        public void Reconcile_HoleInWindow_HardSnaps_ToAuthoritative()
        {
            // Record ticks 11,12 but leave 13 missing — the reconciler must bail rather than step a default input.
            var history = new ClientHistory();
            history.RecordInput(11, Frame(right: true));
            history.RecordInput(12, Frame(right: true));
            var auth = Grounded();
            var r = ClientPrediction.Reconcile(auth, 10, 14, history, _ => ClientOwnsMove(), Idle, Tuning(), Dt, new FakeFlatGroundWorld());
            Assert.IsTrue(r.HardSnapped, "a hole in the replay window forces a hard snap");
            Assert.AreEqual(auth.posX, r.State.posX, 0.0001f);
        }

        [Test]
        public void Reconcile_OnBeforeStep_FiresOncePerReplayedTick()
        {
            var history = new ClientHistory();
            for (uint tk = 11; tk <= 13; tk++) history.RecordInput(tk, Frame(right: true));
            int calls = 0;
            ClientPrediction.Reconcile(Grounded(), 10, 13, history, _ => ClientOwnsMove(), Idle, Tuning(), Dt,
                new FakeFlatGroundWorld(), onBeforeStep: _ => calls++);
            Assert.AreEqual(3, calls, "onBeforeStep runs before each replayed Movement.Step (cast-origin re-establish)");
        }

        [Test]
        public void Reconcile_SwapBoundaryInWindow_RoutesPerTickMap()
        {
            // A swap (Move P1→P2) takes effect at tick 13, mid replay window (11..15). The mapAt resolver must
            // route pre-boundary ticks under the host-owned map (client's right input ignored → no X gain) and
            // post-boundary ticks under the client-owned map (right input drives X). #84 MapAtTick integration.
            var t = Tuning();
            var w = new FakeFlatGroundWorld();
            var hostOwnsMove   = ControlMap.Default;     // ticks < 13: host owns move
            var clientOwnsMove = ClientOwnsMove();       // ticks >= 13: client owns move
            System.Func<uint, ControlMap> mapAt = tick => tick >= 13 ? clientOwnsMove : hostOwnsMove;

            var history = new ClientHistory();
            for (uint tk = 11; tk <= 15; tk++) history.RecordInput(tk, Frame(right: true));

            var auth = Grounded();
            var r = ClientPrediction.Reconcile(auth, 10, 15, history, mapAt, Idle, t, Dt, w);
            Assert.IsFalse(r.HardSnapped);
            Assert.AreEqual(5, r.ReplayedTicks);

            // Independent reference: step the same buffered inputs under the SAME per-tick map.
            var expected = auth;
            for (uint tk = 11; tk <= 15; tk++)
                (expected, _) = ClientPrediction.PredictStep(expected, mapAt(tk), Frame(right: true), Idle, t, Dt, w);
            Assert.AreEqual(expected.posX, r.State.posX, 0.0001f, "replay must resolve the map per tick, not use one map for the window");

            // Routing the whole window under the OLD (host-owned) map keeps the client's right input inert → no X.
            var allOld = ClientPrediction.Reconcile(auth, 10, 15, history, _ => hostOwnsMove, Idle, t, Dt, w);
            Assert.AreEqual(auth.posX, allOld.State.posX, 0.0001f);
            Assert.Greater(r.State.posX, allOld.State.posX, "post-boundary client-owned ticks must advance X past the all-old-map baseline");
        }
    }
}
