using System;

namespace JumpNowBro.Networking
{
    /// Fixed 11-byte packet header, big-endian on the wire. Field meanings: DESIGN §8.
    public struct PacketHeader
    {
        public const int Size = 11;

        public MessageType type;
        public ushort seq;
        public ushort ack;
        public ushort ackBits;
        public uint timestamp;

        public void Write(Span<byte> dst)
        {
            if (dst.Length < Size)
                throw new ArgumentException($"PacketHeader.Write needs {Size} bytes, got {dst.Length}.", nameof(dst));

            dst[0]  = (byte)type;
            dst[1]  = (byte)(seq >> 8);     dst[2]  = (byte)seq;
            dst[3]  = (byte)(ack >> 8);     dst[4]  = (byte)ack;
            dst[5]  = (byte)(ackBits >> 8); dst[6]  = (byte)ackBits;
            dst[7]  = (byte)(timestamp >> 24);
            dst[8]  = (byte)(timestamp >> 16);
            dst[9]  = (byte)(timestamp >> 8);
            dst[10] = (byte)timestamp;
        }

        // Returns false (never throws) on a short buffer, so a truncated datagram can't crash the receive loop.
        public static bool TryRead(ReadOnlySpan<byte> src, out PacketHeader h)
        {
            if (src.Length < Size)
            {
                h = default;
                return false;
            }

            h = new PacketHeader
            {
                type      = (MessageType)src[0],
                seq       = (ushort)((src[1] << 8) | src[2]),
                ack       = (ushort)((src[3] << 8) | src[4]),
                ackBits   = (ushort)((src[5] << 8) | src[6]),
                timestamp = ((uint)src[7] << 24) | ((uint)src[8] << 16) | ((uint)src[9] << 8) | src[10]
            };
            return true;
        }
    }
}
