using System;

namespace JumpNowBro.Networking
{
    /// The LAN discovery beacon a host broadcasts (~1 Hz). Carries the game magic (so clients ignore
    /// foreign traffic on the discovery port), a display name, and the gameplay port to connect to.
    public struct LanBeacon
    {
        public uint Magic;
        public string GameName;
        public ushort GameplayPort;

        public int Write(Span<byte> dst)
        {
            var w = new ByteWriter(dst);
            w.WriteUInt(Magic);
            w.WriteString(GameName);
            w.WriteUShort(GameplayPort);
            return w.Position;
        }

        public static bool TryRead(ReadOnlySpan<byte> src, out LanBeacon b)
        {
            b = default;
            var r = new ByteReader(src);
            if (!r.TryReadUInt(out b.Magic)) return false;
            if (!r.TryReadString(out b.GameName)) return false;
            if (!r.TryReadUShort(out b.GameplayPort)) return false;
            return true;
        }
    }
}
