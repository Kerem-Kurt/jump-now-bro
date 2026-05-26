using NUnit.Framework;
using JumpNowBro.Networking;

namespace JumpNowBro.Tests
{
    public class SeqMathTests
    {
        [TestCase((ushort)1, (ushort)0, true)]
        [TestCase((ushort)0, (ushort)1, false)]
        [TestCase((ushort)0, (ushort)65535, true)]     // wraparound: 0 is newer than 65535
        [TestCase((ushort)65535, (ushort)0, false)]
        [TestCase((ushort)5, (ushort)5, false)]        // equal is not newer
        [TestCase((ushort)0x8000, (ushort)1, true)]    // ahead by 0x7FFF (just under half) -> newer
        [TestCase((ushort)0x8001, (ushort)1, false)]   // ahead by exactly half -> not newer
        public void IsNewer_HandlesWraparound(ushort a, ushort b, bool expected)
        {
            Assert.AreEqual(expected, SeqMath.IsNewer(a, b));
        }

        [Test]
        public void IsNewer_IsAntisymmetric()
        {
            Assert.IsTrue(SeqMath.IsNewer(100, 99) ^ SeqMath.IsNewer(99, 100));
            Assert.IsTrue(SeqMath.IsNewer(0, 65535) ^ SeqMath.IsNewer(65535, 0));
        }

        [TestCase((ushort)5, (ushort)2, (ushort)3)]
        [TestCase((ushort)0, (ushort)65535, (ushort)1)]   // wraparound delta
        [TestCase((ushort)10, (ushort)10, (ushort)0)]
        public void Delta_IsForwardDistance(ushort a, ushort b, ushort expected)
        {
            Assert.AreEqual(expected, SeqMath.Delta(a, b));
        }
    }
}
