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

        // u32 overload anchors v1.4 INPUT.baseTick / STATE.snapshotTick staleness gates.
        // Same wraparound trick, half-range pivot at 0x80000000.
        [TestCase(1u, 0u, true)]
        [TestCase(0u, 1u, false)]
        [TestCase(0u, 0xFFFFFFFFu, true)]                    // wraparound: 0 is newer than max
        [TestCase(0xFFFFFFFFu, 0u, false)]
        [TestCase(5u, 5u, false)]                            // equal is not newer
        [TestCase(0x80000000u, 1u, true)]                    // ahead by just under half -> newer
        [TestCase(0x80000001u, 1u, false)]                   // ahead by exactly half -> not newer
        public void IsNewer32_HandlesWraparound(uint a, uint b, bool expected)
        {
            Assert.AreEqual(expected, SeqMath.IsNewer32(a, b));
        }

        [Test]
        public void IsNewer32_IsAntisymmetric()
        {
            Assert.IsTrue(SeqMath.IsNewer32(100u, 99u) ^ SeqMath.IsNewer32(99u, 100u));
            Assert.IsTrue(SeqMath.IsNewer32(0u, 0xFFFFFFFFu) ^ SeqMath.IsNewer32(0xFFFFFFFFu, 0u));
        }
    }
}
