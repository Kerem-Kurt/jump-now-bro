using System;

namespace JumpNowBro.Networking
{
    public enum WelcomeReason : byte { Accepted, VersionMismatch, Busy }
    public enum GoodbyeReason : byte { Normal, Busy, VersionMismatch, ProtocolError }

    /// Magic + version are negotiated in the HELLO/WELCOME bodies — the fixed 11-byte header has no room.
    public static class SessionProtocol
    {
        public const uint Magic = 0x4A4E4252;   // 'J' 'N' 'B' 'R'
        public const ushort Version = 1;

        /// Validate a raw inbound datagram as a well-formed, current-version HELLO — used by the host's
        /// listen phase before it commits to a peer. HELLO rides the reliable channel, so its body sits
        /// after the 11-byte header AND the 2-byte reliable message-seq (i.e. at offset 13).
        public static bool IsValidHello(ReadOnlySpan<byte> datagram)
        {
            if (!PacketHeader.TryRead(datagram, out var h) || h.type != MessageType.Hello) return false;
            int bodyOffset = PacketHeader.Size + 2;
            if (datagram.Length < bodyOffset) return false;
            return Hello.TryRead(datagram.Slice(bodyOffset), out var hello)
                   && hello.Magic == Magic && hello.Version == Version;
        }
    }

    public struct Hello
    {
        public uint Magic;
        public ushort Version;

        public int Write(Span<byte> dst)
        {
            var w = new ByteWriter(dst);
            w.WriteUInt(Magic);
            w.WriteUShort(Version);
            return w.Position;
        }

        public static bool TryRead(ReadOnlySpan<byte> src, out Hello h)
        {
            h = default;
            var r = new ByteReader(src);
            return r.TryReadUInt(out h.Magic) && r.TryReadUShort(out h.Version);
        }
    }

    public struct Welcome
    {
        public uint Magic;
        public ushort Version;
        public bool Accepted;
        public WelcomeReason Reason;

        public int Write(Span<byte> dst)
        {
            var w = new ByteWriter(dst);
            w.WriteUInt(Magic);
            w.WriteUShort(Version);
            w.WriteByte((byte)(Accepted ? 1 : 0));
            w.WriteByte((byte)Reason);
            return w.Position;
        }

        public static bool TryRead(ReadOnlySpan<byte> src, out Welcome w)
        {
            w = default;
            var r = new ByteReader(src);
            if (!r.TryReadUInt(out w.Magic) || !r.TryReadUShort(out w.Version)) return false;
            if (!r.TryReadByte(out var accepted) || !r.TryReadByte(out var reason)) return false;
            w.Accepted = accepted != 0;
            w.Reason = (WelcomeReason)reason;
            return true;
        }
    }

    public struct Goodbye
    {
        public GoodbyeReason Reason;

        public int Write(Span<byte> dst)
        {
            var w = new ByteWriter(dst);
            w.WriteByte((byte)Reason);
            return w.Position;
        }

        public static bool TryRead(ReadOnlySpan<byte> src, out Goodbye g)
        {
            g = default;
            var r = new ByteReader(src);
            if (!r.TryReadByte(out var reason)) return false;
            g.Reason = (GoodbyeReason)reason;
            return true;
        }
    }
}
