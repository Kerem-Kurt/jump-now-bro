using NUnit.Framework;
using JumpNowBro.Util;

namespace JumpNowBro.Tests
{
    public class VisualSmoothingTests
    {
        [Test]
        public void Inject_SubThreshold_AccumulatesOffset()
        {
            var v = new VisualSmoothing();
            v.Inject(0.3f, -0.1f);
            Assert.AreEqual(0.3f, v.offsetX, 1e-6f);
            Assert.AreEqual(-0.1f, v.offsetY, 1e-6f);
        }

        [Test]
        public void Inject_AboveSnapThreshold_ZeroesOffset()
        {
            var v = new VisualSmoothing();
            v.Inject(PredictionTuning.SnapThreshold + 1f, 0f);  // above the snap threshold → hard cut (offset zeroed)
            Assert.AreEqual(0f, v.offsetX, 1e-6f);
            Assert.AreEqual(0f, v.offsetY, 1e-6f);
        }

        [Test]
        public void Decay_ShrinksTowardZero()
        {
            var v = new VisualSmoothing { offsetX = 1f, offsetY = 0f };
            v.Decay(0.30f);
            Assert.AreEqual(0.70f, v.offsetX, 1e-5f);       // one step keeps (1 - 0.30)
        }

        [Test]
        public void Decay_ConvergesToExactlyZero_NoLinger()
        {
            var v = new VisualSmoothing { offsetX = 0.5f, offsetY = 0.5f };
            for (int i = 0; i < 200; i++) v.Decay(0.30f);
            Assert.AreEqual(0f, v.offsetX, 1e-9f, "offset must clamp to exactly zero, not linger");
            Assert.AreEqual(0f, v.offsetY, 1e-9f);
        }

        [Test]
        public void NormalMotion_ZeroCorrection_KeepsOffsetUnchanged()
        {
            // A perfectly-predicted tick injects (0,0): render stays glued to sim, nothing to smooth.
            var v = new VisualSmoothing { offsetX = 0.2f, offsetY = 0f };
            v.Inject(0f, 0f);
            Assert.AreEqual(0.2f, v.offsetX, 1e-6f, "a zero correction must not perturb the existing offset");
        }

        [Test]
        public void Clear_ZeroesImmediately()
        {
            var v = new VisualSmoothing { offsetX = 0.9f, offsetY = -0.4f };
            v.Clear();
            Assert.AreEqual(0f, v.Magnitude, 1e-9f);
        }
    }
}
