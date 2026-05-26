using System;

namespace JumpNowBro.Networking
{
    /// Tracks which sequence numbers we've received from the peer — to fill the ack/ack_bits we
    /// piggyback on outgoing packets — and decodes a peer's ack/ack_bits into delivered seqs.
    /// seq 0 is reserved as "none": Latest starts at 0 and ForEachAcked ignores ack == 0.
    public sealed class AckSystem
    {
        uint history;   // bit n = received (Latest - 1 - n); low 16 bits become ack_bits

        public ushort Latest { get; private set; }   // highest seq seen from the peer; 0 = none yet

        public void OnReceived(ushort seq)
        {
            if (seq == 0) return;                          // reserved sentinel, never a real packet

            if (Latest == 0) { Latest = seq; return; }     // first packet: seed, no prior history

            if (SeqMath.IsNewer(seq, Latest))
            {
                int shift = SeqMath.Delta(seq, Latest);
                history = shift >= 32 ? 0u : (history << shift) | (1u << (shift - 1)); // old Latest received
                Latest = seq;
            }
            else
            {
                int back = SeqMath.Delta(Latest, seq);                      // how far behind Latest
                if (back >= 1 && back <= 32) history |= 1u << (back - 1);   // mark older seq (idempotent)
            }
        }

        public void GenerateAck(out ushort ack, out ushort ackBits)
        {
            ack = Latest;
            ackBits = (ushort)history;
        }

        public static void ForEachAcked(ushort ack, ushort ackBits, Action<ushort> onAcked)
        {
            if (ack == 0) return;                          // "none acknowledged"
            onAcked(ack);
            for (int n = 0; n < 16; n++)
            {
                if ((ackBits & (1u << n)) == 0) continue;
                ushort acked = (ushort)(ack - 1 - n);
                if (acked != 0) onAcked(acked);            // skip the reserved sentinel
            }
        }
    }
}
