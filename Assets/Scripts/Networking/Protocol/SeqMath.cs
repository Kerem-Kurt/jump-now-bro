namespace JumpNowBro.Networking
{
    /// Comparisons for 16-bit sequence numbers that wrap at 65535: `a` is newer than `b` when the
    /// forward distance from `b` to `a` falls in the first half of the range (the wraparound trick).
    public static class SeqMath
    {
        public static bool IsNewer(ushort a, ushort b) => a != b && (ushort)(a - b) < 0x8000;

        /// Forward distance from b to a, mod 2^16 (e.g. for indexing the ack-bits window).
        public static ushort Delta(ushort a, ushort b) => (ushort)(a - b);
    }
}
