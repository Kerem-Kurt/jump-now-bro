namespace JumpNowBro.Util
{
    /// Pure, engine-free client-prediction step. The MonoBehaviour wrapper (Networking/Runtime/ClientPredictor)
    /// owns frame timing, the Rigidbody2D, and the collision world; this is the replayable core so the same
    /// symbol drives both the per-tick forward prediction (#80) and the reconciliation replay loop (#81), and
    /// so it can be unit-tested in the no-Unity CI against the fake collision worlds.
    ///
    /// Ownership routing is the v1.4 ControlMap.Route (host = P1, client-local = P2). The host frame is
    /// dead-reckoned before routing: host EDGE bits are stripped (never predict a host jump/dash — a false
    /// positive launches the shared body then yanks it back), and host jumpHeld is forced true so a host-owned
    /// jump's variable-cut never fires on the client. Net effect: the client actively predicts only the
    /// horizontal axis it can know for host-owned actions; host-owned VERTICAL is authoritative-only, advancing
    /// by gravity from the STATE-seeded velocity until the next snapshot corrects it.
    public static class ClientPrediction
    {
        /// Strip host edges and force jumpHeld so host-owned vertical stays authoritative-only (see class doc).
        /// moveLeft/moveRight pass through — host-owned horizontal IS dead-reckoned (bounded ~runSpeed*dt/tick).
        public static PlayerInputFrame DeadReckonHost(in PlayerInputFrame host) => new PlayerInputFrame
        {
            moveLeft    = host.moveLeft,
            moveRight   = host.moveRight,
            jumpPressed = false,   // never predict a host edge
            dashPressed = false,   // never predict a host edge
            jumpHeld    = true,    // suppress the variable-jump cut for a host-owned jump
        };

        /// One predicted tick. `localFrame` is the client's fresh P2 frame (full edges + held); `hostFrame` is the
        /// last STATE's host P1 frame, dead-reckoned here. Returns the post-step state and its EdgeFlags (the
        /// caller may drive juice off them in a later milestone; v1.5 ignores them).
        public static (MovementState, EdgeFlags) PredictStep(
            in MovementState s, ControlMap map,
            in PlayerInputFrame localFrame, in PlayerInputFrame hostFrame,
            in MovementTuning t, float dt, ICollisionWorld world)
        {
            var host  = DeadReckonHost(hostFrame);
            var input = ControlMap.Route(map, host, localFrame);   // p1 = host, p2 = client-local
            return Movement.Step(s, input, t, dt, world);
        }
    }
}
