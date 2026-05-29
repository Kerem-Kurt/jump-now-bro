using System;
using JumpNowBro.Util;

namespace JumpNowBro.Networking
{
    /// v1.4 gameplay message bodies. Each rides on top of the 11-byte PacketHeader (reliable EVENT also
    /// adds the 2-byte reliable message-seq the transport injects). All multi-byte fields big-endian.
    /// Every reader is bounds-checked end-to-end and never throws on a short/malformed buffer — a
    /// truncated datagram returns false and the receive path drops it without crashing.

    /// INPUT (client → host, unreliable, ~60 Hz). Variable-length redundancy window so an edge press
    /// survives ~4 consecutive packet losses without retransmit on the unreliable channel.
    public static class InputBody
    {
        public const byte MaxFrameCount = 16;        // ample headroom over v1.4's K=6
        public const int HeaderSize    = 4 + 1;      // baseTick(u32) + count(u8)

        public static int Write(Span<byte> dst, uint baseTick, ReadOnlySpan<byte> packedFrames)
        {
            if (packedFrames.Length < 1 || packedFrames.Length > MaxFrameCount)
                throw new ArgumentException($"InputBody.Write: count must be 1..{MaxFrameCount}");
            var w = new ByteWriter(dst);
            w.WriteUInt(baseTick);
            w.WriteByte((byte)packedFrames.Length);
            w.WriteBytes(packedFrames);
            return w.Position;
        }

        public static bool TryRead(ReadOnlySpan<byte> src,
                                   out uint baseTick, out byte count, out ReadOnlySpan<byte> packedFrames)
        {
            baseTick = 0; count = 0; packedFrames = default;
            var r = new ByteReader(src);
            if (!r.TryReadUInt(out baseTick)) return false;
            if (!r.TryReadByte(out count)) return false;
            if (count < 1 || count > MaxFrameCount) return false;
            if (!r.TryReadBytes(count, out packedFrames)) return false;
            return true;
        }
    }

    /// STATE (host → client, unreliable, ~30 Hz, latest-wins). Carries everything the client needs to
    /// render the shared character plus the seeds v1.5's predictor will consume (remoteInputFrame for
    /// dead-reckoning host-owned actions; lastConsumedClientTick for the reconciliation anchor).
    public struct StateBody
    {
        public uint snapshotTick;
        public uint lastConsumedClientTick;
        public ushort deathCount;
        public byte sceneIndex;                       // 0xFF = pre-load sentinel
        public ControlMap controlMap;
        public PlayerInputFrame remoteInputFrame;     // the host's local input this tick (for v1.5 dead-reckon)
        public MovementState movementState;

        public const int Size =
            4 + 4 + 2 + 1 + ControlMap.PackedSize + 1 + MovementState.PackedSize;   // = 61

        public int Write(Span<byte> dst)
        {
            var w = new ByteWriter(dst);
            w.WriteUInt(snapshotTick);
            w.WriteUInt(lastConsumedClientTick);
            w.WriteUShort(deathCount);
            w.WriteByte(sceneIndex);
            ControlMap.Pack(controlMap, w.Reserve(ControlMap.PackedSize));
            w.WriteByte(PlayerInputFrame.Pack(remoteInputFrame));
            MovementState.Pack(movementState, w.Reserve(MovementState.PackedSize));
            return w.Position;
        }

        public static bool TryRead(ReadOnlySpan<byte> src, out StateBody body)
        {
            body = default;
            var r = new ByteReader(src);
            if (!r.TryReadUInt(out body.snapshotTick))           return false;
            if (!r.TryReadUInt(out body.lastConsumedClientTick)) return false;
            if (!r.TryReadUShort(out body.deathCount))           return false;
            if (!r.TryReadByte(out body.sceneIndex))             return false;
            if (!r.TryReadBytes(ControlMap.PackedSize, out var mapBytes))    return false;
            if (!ControlMap.TryUnpack(mapBytes, out body.controlMap))        return false;
            if (!r.TryReadByte(out var frameByte))                           return false;
            body.remoteInputFrame = PlayerInputFrame.Unpack(frameByte);
            // MovementState reader tolerates fewer trailing bytes (v1.6/v1.7 padding-reserve story).
            if (!r.TryReadBytes(r.Remaining, out var stateBytes))            return false;
            if (!MovementState.TryUnpack(stateBytes, out body.movementState)) return false;
            return true;
        }
    }

    /// EVENT.kind discriminator. v1.4 sends only LevelLoad; v1.6 turns on Swap + Death with applyTick.
    public enum EventKind : byte
    {
        LevelLoad = 0,
        Swap      = 1,   // reserved (v1.6)
        Death     = 2,   // reserved (v1.6)
    }

    /// EVENT (host → client, reliable). v1.4 supports the LevelLoad variant only; the reader rejects
    /// unknown kinds defensively so the client's reliable EVENT inbox stays clean of v1.6 message variants.
    public struct EventBody
    {
        public EventKind kind;
        public byte sceneIndex;                  // LevelLoad payload

        public const int Size = 1 + 1;

        public int Write(Span<byte> dst)
        {
            var w = new ByteWriter(dst);
            w.WriteByte((byte)kind);
            w.WriteByte(sceneIndex);
            return w.Position;
        }

        public static bool TryRead(ReadOnlySpan<byte> src, out EventBody body)
        {
            body = default;
            var r = new ByteReader(src);
            if (!r.TryReadByte(out var k))           return false;
            if (k > (byte)EventKind.Death)           return false;     // reject unknown kinds
            if (k != (byte)EventKind.LevelLoad)      return false;     // v1.4: LevelLoad only
            body.kind = (EventKind)k;
            if (!r.TryReadByte(out body.sceneIndex)) return false;
            return true;
        }
    }
}
