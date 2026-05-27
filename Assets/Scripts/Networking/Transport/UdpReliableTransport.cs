using System;
using System.Collections.Generic;

namespace JumpNowBro.Networking
{
    /// Concrete IReliableTransport. Frames messages over an IDatagramChannel, piggybacks message-level
    /// acks on every datagram, retransmits reliable messages on an RTT timer, delivers the reliable
    /// channel in order + de-duplicated, and gates the unreliable channel to latest-wins. Engine-free so
    /// it runs under the no-Unity CI. The handshake, real-socket binding, and discovery belong to later
    /// milestones; the peer-silence timeout that fires OnDisconnected lives here (armed on first inbound).
    public sealed class UdpReliableTransport : IReliableTransport
    {
        const int MaxDatagram = 1200;                 // MTU-safe ceiling; oversized sends are dropped

        readonly IDatagramChannel channel;
        readonly AckSystem ackTracker = new AckSystem();              // over received reliable message-seqs
        readonly ReliableSendQueue sendQueue = new ReliableSendQueue();
        readonly ReliableReceiveBuffer recvBuffer = new ReliableReceiveBuffer();
        readonly RttEstimator rtt = new RttEstimator();
        readonly Queue<(MessageType type, byte[] payload)> inbox = new Queue<(MessageType, byte[])>();
        readonly byte[] scratch = new byte[MaxDatagram];
        readonly double pingInterval;                   // keepalive cadence (v1.2 runs it fast — PING is the only traffic)
        readonly double silenceTimeout;                 // peer-silence → OnDisconnected

        ushort nextPacketSeq = 1;                      // 0 reserved; stamps every datagram, drives unreliable latest-wins
        ushort highestPacketSeq;                       // 0 = none seen yet
        double clock;                                   // seconds since construction; advanced by Tick
        double lastPingAt = double.NegativeInfinity;
        double lastReceivedAt;                          // clock of the last inbound datagram (liveness baseline)
        bool connected;
        bool livenessArmed;                             // the silence timeout only runs after the first inbound

        public UdpReliableTransport(IDatagramChannel channel, double pingIntervalSeconds = 1.0, double silenceTimeoutSeconds = 5.0)
        {
            this.channel = channel;
            pingInterval = pingIntervalSeconds;
            silenceTimeout = silenceTimeoutSeconds;
            sendQueue.OnDeliveryFailed += Disconnect;   // a reliable message giving up means the peer is gone
        }

        public float RttSeconds => rtt.RttSeconds;
        public bool Connected => connected;
        public int PendingReliableCount => sendQueue.PendingCount;   // for tests/diagnostics; not on the interface
        public event Action OnConnected;
        public event Action OnDisconnected;

        public void Send(Channel ch, MessageType type, ReadOnlySpan<byte> payload)
        {
            // Reliable rides the send queue (assigned a stable message-seq, flushed next Tick); unreliable
            // goes out immediately. Type and channel must agree per the DESIGN §8 discipline.
            if (ch == Channel.Reliable) sendQueue.Queue(type, payload);
            else SendFramed(type, 0, payload, NowMs());
        }

        public bool TryReceive(out MessageType type, out byte[] payload)
        {
            if (inbox.Count > 0) { (type, payload) = inbox.Dequeue(); return true; }
            type = default; payload = null; return false;
        }

        public void Tick(float dt)
        {
            clock += dt;
            while (channel.TryReceive(out var datagram)) Process(datagram);
            sendQueue.Tick(clock, rtt.RttSeconds, ReliableSend);
            if (clock - lastPingAt >= pingInterval)
            {
                SendFramed(MessageType.Ping, 0, ReadOnlySpan<byte>.Empty, NowMs());
                lastPingAt = clock;
            }
            if (livenessArmed && clock - lastReceivedAt > silenceTimeout) Disconnect();
        }

        // The queue's SendFn: first send and every retransmit reuse the same stable message-seq.
        void ReliableSend(ushort messageSeq, MessageType type, ReadOnlySpan<byte> payload)
            => SendFramed(type, messageSeq, payload, NowMs());

        // One datagram: [header][message-seq if reliable][body]. Current acks ride along on all of them.
        void SendFramed(MessageType type, ushort messageSeq, ReadOnlySpan<byte> body, uint timestamp)
        {
            bool reliable = IsReliable(type);
            int size = PacketHeader.Size + (reliable ? 2 : 0) + body.Length;
            if (size > scratch.Length) return;          // oversized: drop (full hardening is the v1.7 pass)

            ackTracker.GenerateAck(out var ack, out var ackBits);
            var header = new PacketHeader { type = type, seq = nextPacketSeq, ack = ack, ackBits = ackBits, timestamp = timestamp };
            nextPacketSeq = NextSeq(nextPacketSeq);

            header.Write(scratch);
            int offset = PacketHeader.Size;
            if (reliable) { scratch[offset++] = (byte)(messageSeq >> 8); scratch[offset++] = (byte)messageSeq; }
            body.CopyTo(scratch.AsSpan(offset));
            channel.Send(scratch.AsSpan(0, offset + body.Length));
        }

        void Process(byte[] datagram)
        {
            if (!PacketHeader.TryRead(datagram, out var h)) return;   // truncated/malformed: drop

            lastReceivedAt = clock;                                   // any inbound keeps the link alive
            livenessArmed = true;
            if (!connected) { connected = true; OnConnected?.Invoke(); }

            AckSystem.ForEachAcked(h.ack, h.ackBits, sendQueue.OnAck);  // acks ride on every inbound datagram

            // Packet-seq latest-wins bookkeeping. (DESIGN §7 gates on the packet seq; the precise per-message
            // tick gate lands with the INPUT/STATE payload formats in v1.4.)
            bool newestPacket = highestPacketSeq == 0 || SeqMath.IsNewer(h.seq, highestPacketSeq);
            if (newestPacket) highestPacketSeq = h.seq;

            int offset = PacketHeader.Size;
            bool reliable = IsReliable(h.type);
            ushort messageSeq = 0;
            if (reliable)
            {
                if (datagram.Length < offset + 2) return;             // reliable but no room for the message-seq: drop
                messageSeq = (ushort)((datagram[offset] << 8) | datagram[offset + 1]);
                offset += 2;
            }
            var body = new ReadOnlySpan<byte>(datagram, offset, datagram.Length - offset);

            switch (h.type)
            {
                case MessageType.Ping:
                    SendFramed(MessageType.Pong, 0, ReadOnlySpan<byte>.Empty, h.timestamp);   // echo the sender's stamp
                    break;
                case MessageType.Pong:
                    rtt.AddSample(SecondsSince(h.timestamp));
                    break;
                default:
                    if (reliable)
                    {
                        ackTracker.OnReceived(messageSeq);
                        recvBuffer.Accept(messageSeq, h.type, body);
                        while (recvBuffer.TryNext(out var t, out var p)) inbox.Enqueue((t, p));
                    }
                    else if (newestPacket)            // unreliable: deliver only the newest packet seen, drop the rest
                    {
                        inbox.Enqueue((h.type, body.ToArray()));
                    }
                    break;
            }
        }

        void Disconnect()
        {
            if (!connected) return;
            connected = false;
            OnDisconnected?.Invoke();
        }

        uint NowMs() => (uint)(clock * 1000.0);
        float SecondsSince(uint stampMs) => (uint)(NowMs() - stampMs) / 1000f;   // wrap-safe unsigned subtraction

        // Channel discipline (DESIGN §8): these ride the reliable channel and carry a message-seq prefix.
        static bool IsReliable(MessageType t) =>
            t == MessageType.Event || t == MessageType.Hello || t == MessageType.Welcome || t == MessageType.Goodbye;

        static ushort NextSeq(ushort s) { s++; return s == 0 ? (ushort)1 : s; }
    }
}
