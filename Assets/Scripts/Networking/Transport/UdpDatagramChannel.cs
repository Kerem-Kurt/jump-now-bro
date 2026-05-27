using System;
using System.Collections.Generic;
using System.Net;

namespace JumpNowBro.Networking
{
    /// Adapts a UdpSocket to the transport's IDatagramChannel seam. IDatagramChannel surfaces bytes only,
    /// so all endpoint handling lives here: send to a single fixed peer, and drop inbound datagrams from
    /// anyone else. Peer selection/validation happens ABOVE this (the listen phase) — this class is dumb.
    /// The pre-seed queue lets that layer inject the already-validated first datagram so the transport
    /// processes it immediately instead of waiting for a retransmit.
    public sealed class UdpDatagramChannel : IDatagramChannel
    {
        readonly UdpSocket socket;
        readonly Queue<byte[]> preSeed = new Queue<byte[]>();
        IPEndPoint peer;

        public UdpDatagramChannel(UdpSocket socket, IPEndPoint peer)
        {
            this.socket = socket;
            this.peer = peer;
        }

        public IPEndPoint Peer => peer;

        public void SetPeer(IPEndPoint newPeer) => peer = newPeer;     // host latches after validation; replaced on teardown

        public void PreSeed(byte[] datagram) => preSeed.Enqueue(datagram);

        public void Send(ReadOnlySpan<byte> datagram)
        {
            if (peer != null) socket.Send(datagram, peer);
        }

        public bool TryReceive(out byte[] datagram)
        {
            if (preSeed.Count > 0) { datagram = preSeed.Dequeue(); return true; }
            while (socket.Poll(out var data, out var from))
            {
                if (peer != null && peer.Equals(from)) { datagram = data; return true; }
                // datagram from a non-peer endpoint: drop it and keep draining
            }
            datagram = null;
            return false;
        }
    }
}
