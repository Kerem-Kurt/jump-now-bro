using System;

namespace JumpNowBro.Networking
{
    /// The raw byte pipe under the transport: one datagram out, one datagram in, no interpretation.
    /// Splitting it out lets UdpReliableTransport run over a real UDP socket in play and an in-memory
    /// channel (with injected loss/reorder) in tests, without the transport knowing the difference.
    public interface IDatagramChannel
    {
        void Send(ReadOnlySpan<byte> datagram);

        // Drained on the main thread; returns false when nothing is queued.
        bool TryReceive(out byte[] datagram);
    }
}
