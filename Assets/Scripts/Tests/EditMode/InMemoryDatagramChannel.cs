using System;
using System.Collections.Generic;
using JumpNowBro.Networking;

namespace JumpNowBro.Tests
{
    /// In-memory IDatagramChannel for transport tests: paired so each Send lands in the peer's queue.
    /// Lossless + in-order by default; the two hooks let a test drop or reorder deterministically.
    sealed class InMemoryDatagramChannel : IDatagramChannel
    {
        readonly List<byte[]> inbox = new List<byte[]>();
        InMemoryDatagramChannel peer;

        public int DropNextSends;     // while > 0, the next outgoing datagram is silently dropped
        public bool Lifo;             // drain newest-first, to simulate reordering

        public static (InMemoryDatagramChannel a, InMemoryDatagramChannel b) Pair()
        {
            var a = new InMemoryDatagramChannel();
            var b = new InMemoryDatagramChannel();
            a.peer = b;
            b.peer = a;
            return (a, b);
        }

        public void Send(ReadOnlySpan<byte> datagram)
        {
            if (DropNextSends > 0) { DropNextSends--; return; }
            peer.inbox.Add(datagram.ToArray());
        }

        public bool TryReceive(out byte[] datagram)
        {
            if (inbox.Count == 0) { datagram = null; return false; }
            int i = Lifo ? inbox.Count - 1 : 0;
            datagram = inbox[i];
            inbox.RemoveAt(i);
            return true;
        }
    }
}
