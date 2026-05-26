using System;
using System.Collections.Generic;

namespace JumpNowBro.Networking
{
    /// Send side of the reliable channel: holds each reliable message under a stable message-seq
    /// until the peer acks it, resends on an RTT-derived timer (with backoff), and signals failure
    /// past the retry/in-flight cap. The carrying packet's header seq may change on resend, but the
    /// message-seq does not — so acks (and receive-side dedupe/order) key off it unambiguously.
    public sealed class ReliableSendQueue
    {
        public delegate void SendFn(ushort messageSeq, MessageType type, ReadOnlySpan<byte> payload);

        sealed class Pending
        {
            public ushort Seq;
            public MessageType Type;
            public byte[] Payload;
            public double LastSent;   // NegativeInfinity = not sent yet
            public int Retries;
        }

        const float RtoFactor = 2f;
        const float RtoMin = 0.05f;
        const float RtoMax = 0.5f;
        const int MaxRetries = 8;
        const int MaxInFlight = 64;

        readonly List<Pending> pending = new List<Pending>();
        ushort nextSeq = 1;   // 0 reserved as "none"

        public int PendingCount => pending.Count;
        public event Action OnDeliveryFailed;

        public void Queue(MessageType type, ReadOnlySpan<byte> payload)
        {
            if (pending.Count >= MaxInFlight) { OnDeliveryFailed?.Invoke(); return; }
            pending.Add(new Pending
            {
                Seq = nextSeq,
                Type = type,
                Payload = payload.ToArray(),
                LastSent = double.NegativeInfinity,
                Retries = 0
            });
            nextSeq = NextSeq(nextSeq);
        }

        public void Tick(double now, float rtt, SendFn send)
        {
            float baseRto = Clamp(RtoFactor * rtt, RtoMin, RtoMax);
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                var p = pending[i];
                bool firstSend = double.IsNegativeInfinity(p.LastSent);
                double rto = baseRto * (1 << Math.Min(p.Retries, 4));   // exponential backoff, capped
                if (!firstSend && now - p.LastSent < rto) continue;

                if (!firstSend && p.Retries >= MaxRetries)
                {
                    pending.RemoveAt(i);
                    OnDeliveryFailed?.Invoke();
                    continue;
                }

                send(p.Seq, p.Type, p.Payload);
                p.LastSent = now;
                if (!firstSend) p.Retries++;
            }
        }

        public void OnAck(ushort messageSeq)
        {
            for (int i = pending.Count - 1; i >= 0; i--)
                if (pending[i].Seq == messageSeq) { pending.RemoveAt(i); return; }
        }

        static ushort NextSeq(ushort s) { s++; return s == 0 ? (ushort)1 : s; }   // skip reserved 0 on wrap
        static float Clamp(float v, float lo, float hi) => v < lo ? lo : (v > hi ? hi : v);
    }
}
