namespace JumpNowBro.Networking
{
    /// Wraparound-safe sequence/tick comparisons. ushort overload anchors v1.1 packet-seq + reliable-msg-seq
    /// arithmetic; uint overload anchors v1.4 simulation-tick staleness gates on INPUT/STATE. Same trick:
    /// `a` is newer than `b` when the forward distance from `b` to `a` lies in the first half of the range.
    public static class SeqMath
    {
        public static bool IsNewer(ushort a, ushort b) => a != b && (ushort)(a - b) < 0x8000;

        public static bool IsNewer32(uint a, uint b) => a != b && (uint)(a - b) < 0x80000000u;

        /// Forward distance from b to a, mod 2^16 (e.g. for indexing the ack-bits window).
        public static ushort Delta(ushort a, ushort b) => (ushort)(a - b);
    }
}
