using System;
using NUnit.Framework;
using JumpNowBro.Networking;

namespace JumpNowBro.Tests
{
    public class ByteIoTests
    {
        [Test]
        public void RoundTrips_AllPrimitives()
        {
            var buf = new byte[64];
            var w = new ByteWriter(buf);
            w.WriteByte(0xAB);
            w.WriteUShort(0x1234);
            w.WriteUInt(0x89ABCDEFu);
            w.WriteInt(-42);
            w.WriteString("hi");
            w.WriteBytes(new byte[] { 1, 2, 3 });

            var r = new ByteReader(buf.AsSpan(0, w.Position));
            Assert.IsTrue(r.TryReadByte(out var b)); Assert.AreEqual(0xAB, b);
            Assert.IsTrue(r.TryReadUShort(out var us)); Assert.AreEqual(0x1234, us);
            Assert.IsTrue(r.TryReadUInt(out var ui)); Assert.AreEqual(0x89ABCDEFu, ui);
            Assert.IsTrue(r.TryReadInt(out var i)); Assert.AreEqual(-42, i);
            Assert.IsTrue(r.TryReadString(out var s)); Assert.AreEqual("hi", s);
            Assert.IsTrue(r.TryReadBytes(3, out var raw)); Assert.AreEqual(new byte[] { 1, 2, 3 }, raw.ToArray());
            Assert.AreEqual(0, r.Remaining);
        }

        [Test]
        public void BigEndian_MatchesHeaderConvention()
        {
            var u16 = new byte[2];
            new ByteWriter(u16).WriteUShort(0x0102);
            Assert.AreEqual(new byte[] { 0x01, 0x02 }, u16);          // high byte first

            var u32 = new byte[4];
            new ByteWriter(u32).WriteUInt(0x01020304u);
            Assert.AreEqual(new byte[] { 0x01, 0x02, 0x03, 0x04 }, u32);
        }

        [Test]
        public void Reader_Truncated_FailsGracefully_WithoutConsuming()
        {
            var r = new ByteReader(new byte[] { 0xFF });             // one byte, ask for two
            Assert.IsFalse(r.TryReadUShort(out _));
            Assert.AreEqual(1, r.Remaining);                          // not consumed on failure
            Assert.IsTrue(r.TryReadByte(out _));
            Assert.IsFalse(r.TryReadByte(out _));                     // now empty
        }

        [Test]
        public void Reader_TruncatedString_IsAllOrNothing()
        {
            // length prefix says 5, but only 2 body bytes follow
            var r = new ByteReader(new byte[] { 0x00, 0x05, 0x41, 0x42 });
            Assert.IsFalse(r.TryReadString(out var s));
            Assert.IsNull(s);
            Assert.AreEqual(4, r.Remaining);                          // prefix not consumed
        }

        [Test]
        public void Writer_Overflow_Throws()
        {
            Assert.Throws<System.ArgumentException>(() =>
            {
                var w = new ByteWriter(new byte[1]);
                w.WriteUShort(0x1234);
            });
        }

        [Test]
        public void EmptyString_RoundTrips()
        {
            var buf = new byte[8];
            var w = new ByteWriter(buf);
            w.WriteString("");
            var r = new ByteReader(buf.AsSpan(0, w.Position));
            Assert.IsTrue(r.TryReadString(out var s));
            Assert.AreEqual("", s);
        }
    }
}
