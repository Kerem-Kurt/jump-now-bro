using System;
using System.Net;
using System.Text;
using System.Threading;
using NUnit.Framework;
using JumpNowBro.Networking;

namespace JumpNowBro.Tests
{
    public class UdpSocketTests
    {
        [Test]
        public void SendReceive_OverLoopback_DeliversDatagramAndSender()
        {
            using var a = new UdpSocket(0);
            using var b = new UdpSocket(0);

            var payload = Encoding.ASCII.GetBytes("hello-udp");
            a.Send(payload, new IPEndPoint(IPAddress.Loopback, b.LocalPort));

            // Receive runs on a background thread, so drain with a bounded retry rather than a single Poll.
            byte[] got = null;
            IPEndPoint from = null;
            var deadline = DateTime.UtcNow.AddSeconds(2);
            while (DateTime.UtcNow < deadline)
            {
                if (b.Poll(out got, out from)) break;
                Thread.Sleep(5);
            }

            Assert.IsNotNull(got, "datagram not received within timeout");
            Assert.AreEqual(payload, got);
            Assert.AreEqual(a.LocalPort, from.Port);
        }

        [Test]
        public void Poll_WhenEmpty_ReturnsFalse()
        {
            using var s = new UdpSocket(0);
            Assert.IsFalse(s.Poll(out _, out _));
        }

        [Test]
        public void Dispose_IsCleanAndIdempotent()
        {
            var s = new UdpSocket(0);
            Assert.DoesNotThrow(() => s.Dispose());
            Assert.DoesNotThrow(() => s.Dispose());
        }
    }
}
