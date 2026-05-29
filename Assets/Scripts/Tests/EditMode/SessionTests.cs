using System;
using NUnit.Framework;
using JumpNowBro.Networking;
using JumpNowBro.Util;

namespace JumpNowBro.Tests
{
    public class SessionTests
    {
        // A full reliable HELLO datagram: header(11) + message-seq(2) + body, magic/version configurable.
        static byte[] BuildHelloDatagram(uint magic, ushort version)
        {
            var dg = new byte[PacketHeader.Size + 2 + 6];
            new PacketHeader { type = MessageType.Hello, seq = 1 }.Write(dg);
            dg[PacketHeader.Size] = 0;            // message-seq hi
            dg[PacketHeader.Size + 1] = 1;        // message-seq lo
            new Hello { Magic = magic, Version = version }.Write(dg.AsSpan(PacketHeader.Size + 2));
            return dg;
        }

        [Test]
        public void Messages_RoundTrip()
        {
            var buf = new byte[24];

            int n = new Hello { Magic = 0xDEADBEEFu, Version = 7 }.Write(buf);
            Assert.IsTrue(Hello.TryRead(buf.AsSpan(0, n), out var h));
            Assert.AreEqual(0xDEADBEEFu, h.Magic);
            Assert.AreEqual(7, h.Version);

            var welcomeOut = new Welcome
            {
                Magic = 0x11223344u, Version = 9, Accepted = true, Reason = WelcomeReason.Accepted,
                PeerOwner = InputOwner.P2, CurrentSceneIndex = 2, HostTickAtWelcome = 123_456u,
            };
            n = welcomeOut.Write(buf);
            Assert.IsTrue(Welcome.TryRead(buf.AsSpan(0, n), out var w));
            Assert.AreEqual(0x11223344u, w.Magic);
            Assert.IsTrue(w.Accepted);
            Assert.AreEqual(WelcomeReason.Accepted, w.Reason);
            Assert.AreEqual(InputOwner.P2, w.PeerOwner);
            Assert.AreEqual(2, w.CurrentSceneIndex);
            Assert.AreEqual(123_456u, w.HostTickAtWelcome);

            n = new Goodbye { Reason = GoodbyeReason.Busy }.Write(buf);
            Assert.IsTrue(Goodbye.TryRead(buf.AsSpan(0, n), out var g));
            Assert.AreEqual(GoodbyeReason.Busy, g.Reason);
        }

        [Test]
        public void Welcome_OutOfRangePeerOwner_Rejected()
        {
            // Manually craft a Welcome body with PeerOwner = 9 (outside the InputOwner enum range).
            var bytes = new byte[14];
            new Welcome { Magic = SessionProtocol.Magic, Version = SessionProtocol.Version, Accepted = true }.Write(bytes);
            bytes[8] = 9;                                        // peerOwner byte position
            Assert.IsFalse(Welcome.TryRead(bytes, out _));
        }

        [Test]
        public void IsValidHello_AcceptsGood_RejectsBad()
        {
            Assert.IsTrue(SessionProtocol.IsValidHello(BuildHelloDatagram(SessionProtocol.Magic, SessionProtocol.Version)));
            Assert.IsFalse(SessionProtocol.IsValidHello(BuildHelloDatagram(0x00BADBADu, SessionProtocol.Version)));               // wrong magic
            Assert.IsFalse(SessionProtocol.IsValidHello(BuildHelloDatagram(SessionProtocol.Magic, (ushort)(SessionProtocol.Version + 1)))); // wrong version
            Assert.IsFalse(SessionProtocol.IsValidHello(new byte[5]));                                                            // truncated

            var wrongType = BuildHelloDatagram(SessionProtocol.Magic, SessionProtocol.Version);
            wrongType[0] = (byte)MessageType.State;
            Assert.IsFalse(SessionProtocol.IsValidHello(wrongType));                                                             // wrong type
        }

        [Test]
        public void Handshake_ReachesEstablished_OnBothEnds()
        {
            var (ca, cb) = InMemoryDatagramChannel.Pair();
            var client = new Session(new UdpReliableTransport(ca), isHost: false);
            var host = new Session(new UdpReliableTransport(cb), isHost: true);

            client.Start();
            host.Start();
            for (int i = 0; i < 40; i++) { client.Tick(0.05f); host.Tick(0.05f); }   // client leads each round (staggered)

            Assert.AreEqual(Session.SessionState.Established, client.State);
            Assert.AreEqual(Session.SessionState.Established, host.State);
        }

        [Test]
        public void Handshake_DeliversWelcome_WithPeerOwnerP2_AndSceneIndex()
        {
            var (ca, cb) = InMemoryDatagramChannel.Pair();
            var client = new Session(new UdpReliableTransport(ca), isHost: false);
            // Host providers: scene index 2 (mid-game join scenario), tick 9999.
            var host = new Session(new UdpReliableTransport(cb), isHost: true,
                sceneIndexProvider: () => (byte)2, hostTickProvider: () => 9999u);

            Welcome? received = null;
            client.OnWelcomeReceived += w => received = w;

            client.Start();
            host.Start();
            for (int i = 0; i < 40; i++) { client.Tick(0.05f); host.Tick(0.05f); }

            Assert.IsTrue(received.HasValue, "client never observed OnWelcomeReceived");
            Assert.AreEqual(InputOwner.P2, received.Value.PeerOwner);
            Assert.AreEqual(2, received.Value.CurrentSceneIndex);
            Assert.AreEqual(9999u, received.Value.HostTickAtWelcome);
        }

        [Test]
        public void Client_StuckConnecting_TimesOut()
        {
            var (ca, _) = InMemoryDatagramChannel.Pair();      // nothing on the other end
            var client = new Session(new UdpReliableTransport(ca), isHost: false);

            client.Start();
            for (int i = 0; i < 120; i++) client.Tick(0.05f);  // 6 s > the 4 s connect-attempt timeout

            Assert.AreEqual(Session.SessionState.Disconnected, client.State);
        }

        [Test]
        public void Host_RejectsWrongVersionHello()
        {
            var (ca, cb) = InMemoryDatagramChannel.Pair();
            var clientT = new UdpReliableTransport(ca);
            var host = new Session(new UdpReliableTransport(cb), isHost: true);
            host.Start();

            var bad = new byte[8];
            int n = new Hello { Magic = SessionProtocol.Magic, Version = (ushort)(SessionProtocol.Version + 1) }.Write(bad);
            clientT.Send(Channel.Reliable, MessageType.Hello, bad.AsSpan(0, n));
            for (int i = 0; i < 20; i++) { clientT.Tick(0.05f); host.Tick(0.05f); }

            Assert.AreEqual(Session.SessionState.Disconnected, host.State);
        }

        [Test]
        public void Goodbye_DisconnectsBothEnds()
        {
            var (ca, cb) = InMemoryDatagramChannel.Pair();
            var client = new Session(new UdpReliableTransport(ca), isHost: false);
            var host = new Session(new UdpReliableTransport(cb), isHost: true);

            client.Start();
            host.Start();
            for (int i = 0; i < 40; i++) { client.Tick(0.05f); host.Tick(0.05f); }
            Assert.AreEqual(Session.SessionState.Established, client.State, "precondition: handshake established");

            client.SendGoodbye(GoodbyeReason.Normal);
            Assert.AreEqual(Session.SessionState.Disconnected, client.State);          // immediate on the sender
            for (int i = 0; i < 20; i++) { client.Tick(0.05f); host.Tick(0.05f); }     // let the GOODBYE flush + arrive

            Assert.AreEqual(Session.SessionState.Disconnected, host.State);
        }
    }
}
