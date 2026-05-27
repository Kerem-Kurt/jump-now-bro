using System;
using System.Net;
using NUnit.Framework;
using JumpNowBro.Networking;

namespace JumpNowBro.Tests
{
    public class DiscoveryTests
    {
        [Test]
        public void LanBeacon_RoundTrips()
        {
            var buf = new byte[64];
            int n = new LanBeacon { Magic = SessionProtocol.Magic, GameName = "Kerem's game", GameplayPort = 7777 }.Write(buf);
            Assert.IsTrue(LanBeacon.TryRead(buf.AsSpan(0, n), out var b));
            Assert.AreEqual(SessionProtocol.Magic, b.Magic);
            Assert.AreEqual("Kerem's game", b.GameName);
            Assert.AreEqual(7777, b.GameplayPort);
        }

        [Test]
        public void DiscoveredHosts_DedupsByEndpoint()
        {
            var hosts = new DiscoveredHosts();
            var ep = new IPEndPoint(IPAddress.Parse("192.168.1.5"), 7777);
            hosts.Observe(ep, "A", 1.0);
            hosts.Observe(ep, "A", 2.0);                                              // same endpoint → still one
            hosts.Observe(new IPEndPoint(IPAddress.Parse("192.168.1.6"), 7777), "B", 2.0);
            Assert.AreEqual(2, hosts.Count);
        }

        [Test]
        public void DiscoveredHosts_ExpiresStale()
        {
            var hosts = new DiscoveredHosts();
            hosts.Observe(new IPEndPoint(IPAddress.Parse("192.168.1.5"), 7777), "A", 1.0);   // last seen 1.0
            hosts.Observe(new IPEndPoint(IPAddress.Parse("192.168.1.6"), 7777), "B", 5.0);   // last seen 5.0
            hosts.Expire(now: 6.0, ttlSeconds: 4.0);                                         // A is 5s stale → dropped; B (1s) kept
            Assert.AreEqual(1, hosts.Count);
        }

        [Test]
        public void BroadcastSocket_Constructs()
        {
            // exercises the broadcast ctor branch (unbound → SO_REUSEADDR → bind → EnableBroadcast)
            Assert.DoesNotThrow(() => { using var s = new UdpSocket(0, broadcast: true); });
        }
    }
}
