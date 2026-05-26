using System;

namespace JumpNowBro.Networking
{
    public enum Channel { Unreliable, Reliable }

    /// Wire message types; channel discipline and send rates are in DESIGN §8.
    public enum MessageType : byte
    {
        Hello, Welcome, Goodbye,
        Input, State, Event,
        Ping, Pong
    }

    /// Boundary between the transport (sockets, seq/ack, RTT, retransmit) and the application
    /// protocol, drafted before either side so they can be built independently. Callers honor the
    /// channel discipline in DESIGN §8.
    public interface IReliableTransport
    {
        void Send(Channel channel, MessageType type, ReadOnlySpan<byte> payload);

        // Drained on the main thread; reliable is in-order + de-duplicated, unreliable latest-wins.
        bool TryReceive(out MessageType type, out byte[] payload);

        float RttSeconds { get; }
        bool Connected { get; }

        event Action OnConnected;
        event Action OnDisconnected;

        // Pump retransmit / RTT / timeout timers; call once per step on the main thread.
        void Tick(float dt);
    }
}
