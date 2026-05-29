using UnityEngine;
using JumpNowBro.Gameplay;
using JumpNowBro.Util;

namespace JumpNowBro.Networking
{
    /// Client-side INPUT pump. Samples the local IInputSource each FixedUpdate at the TickClock's tick,
    /// stores it in a K=6 redundancy ring, and emits an INPUT body to the host on the unreliable channel.
    /// The redundancy is what protects an edge press against ~K-1 consecutive packet losses — no retransmit
    /// on the unreliable channel (DESIGN §8: INPUT/STATE are never sent reliably).
    ///
    /// Execution order −50 matches NetworkRemoteInputSource so any future client-side pre-tick consumers
    /// see the sampled frame before they run; PlayerController is destroyed on the client per #78, so the
    /// ordering chiefly matters relative to KeyboardInputSource's Tick() at end-of-frame.
    ///
    /// Wiring to a live transport + IInputSource is the #78 spawner's job; Bind() is the seam.
    [DefaultExecutionOrder(-50)]
    public sealed class ClientInputSender : MonoBehaviour
    {
        readonly InputSendRing ring = new InputSendRing();
        readonly byte[] sendBuffer = new byte[InputBody.HeaderSize + InputSendRing.K];

        IInputSource source;
        IReliableTransport transport;
        TickClock tickClock;

        public void Bind(IInputSource source, IReliableTransport transport, TickClock tickClock)
        {
            this.source = source;
            this.transport = transport;
            this.tickClock = tickClock;
        }

        void FixedUpdate()
        {
            if (source == null || transport == null || tickClock == null) return;

            uint tick = tickClock.Current;
            var f = new PlayerInputFrame
            {
                moveLeft    = source.MoveLeft,
                moveRight   = source.MoveRight,
                jumpPressed = source.JumpPressed,
                jumpHeld    = source.JumpHeld,
                dashPressed = source.DashPressed,
            };

            int n = ring.Sample(tick, f, sendBuffer);
            transport.Send(Channel.Unreliable, MessageType.Input, new System.ReadOnlySpan<byte>(sendBuffer, 0, n));

            source.Tick();                              // clears local edge bits — mirrors KeyboardInputSource's pattern
        }
    }
}
