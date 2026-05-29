using NUnit.Framework;
using JumpNowBro.Networking;

namespace JumpNowBro.Tests
{
    public class StateBroadcastTimingTests
    {
        [TestCase(0u,  false, true)]
        [TestCase(1u,  false, false)]                                     // odd tick → skipped (~30 Hz at 60 Hz sim)
        [TestCase(2u,  false, true)]
        [TestCase(60u, false, true)]
        [TestCase(0u,  true,  false)]                                     // IsLoading suppression beats parity
        [TestCase(2u,  true,  false)]
        public void ShouldBroadcast_GatesOnEvenTickAndNotLoading(uint hostTick, bool isLoading, bool expected)
        {
            Assert.AreEqual(expected, StateBroadcastTiming.ShouldBroadcast(hostTick, isLoading));
        }
    }
}
