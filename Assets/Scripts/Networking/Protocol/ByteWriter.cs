using System;
using System.Text;

namespace JumpNowBro.Networking
{
    /// Big-endian writer over a caller-provided buffer (same wire convention as the 11-byte header).
    /// Writing past the end throws — the buffer is ours to size; it's the read side that must tolerate
    /// untrusted, possibly-truncated input. Strings are length-prefixed (ushort) UTF-8.
    public ref struct ByteWriter
    {
        readonly Span<byte> buf;

        public ByteWriter(Span<byte> buffer) { buf = buffer; Position = 0; }

        public int Position { get; private set; }
        public ReadOnlySpan<byte> Written => buf.Slice(0, Position);

        public void WriteByte(byte v) { Need(1); buf[Position++] = v; }

        public void WriteUShort(ushort v)
        {
            Need(2);
            buf[Position++] = (byte)(v >> 8);
            buf[Position++] = (byte)v;
        }

        public void WriteUInt(uint v)
        {
            Need(4);
            buf[Position++] = (byte)(v >> 24);
            buf[Position++] = (byte)(v >> 16);
            buf[Position++] = (byte)(v >> 8);
            buf[Position++] = (byte)v;
        }

        public void WriteInt(int v) => WriteUInt((uint)v);

        public void WriteBytes(ReadOnlySpan<byte> src)
        {
            Need(src.Length);
            src.CopyTo(buf.Slice(Position));
            Position += src.Length;
        }

        public void WriteString(string s)
        {
            byte[] utf8 = Encoding.UTF8.GetBytes(s ?? string.Empty);
            if (utf8.Length > ushort.MaxValue) throw new ArgumentException("string too long for a ushort length prefix");
            WriteUShort((ushort)utf8.Length);
            WriteBytes(utf8);
        }

        void Need(int n)
        {
            if (Position + n > buf.Length)
                throw new ArgumentException($"ByteWriter overflow: need {n} more bytes, {buf.Length - Position} left");
        }
    }
}
