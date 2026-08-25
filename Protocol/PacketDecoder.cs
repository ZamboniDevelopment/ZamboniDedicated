using System.Buffers.Binary;
using ZamboniDedicated.Protocol.Packets;
using ZamboniDedicated.Protocol.Packets.SubPackets;

namespace ZamboniDedicated.Protocol;

public static class PacketDecoder
{
    public static Packet Decode(ReadOnlySpan<byte> data)
    {
        uint rawSeq = BinaryPrimitives.ReadUInt32BigEndian(data[..4]);
        uint ack = BinaryPrimitives.ReadUInt32BigEndian(data[4..8]);
        var header = PacketHeader.FromRaw(rawSeq, ack);

        if (header.IsSignalPacket)
        {
            return new Packet { Header = header, SubPackets = Array.Empty<ISubPacket>() };
        }

        var subPackets = DecodeReliableSubPackets(header, data[8..]);
        return new Packet { Header = header, SubPackets = subPackets };
    }

    private static IReadOnlyList<ISubPacket> DecodeReliableSubPackets(PacketHeader header, ReadOnlySpan<byte> body)
    {
        if (body.Length == 0)
        {
            return Array.Empty<ISubPacket>();
        }

        int bundleCount = header.AdditionalSubpackets;
        var subPackets = new ISubPacket[bundleCount + 1];
        ReadOnlySpan<byte> remaining = body;

        for (int index = bundleCount; index >= 1; index--)
        {
            int size = ReadTrailingSize(remaining, out int sizeFieldLength);
            remaining = remaining[..^sizeFieldLength];
            subPackets[index] = DecodeEntry(remaining[^size..]);
            remaining = remaining[..^size];
        }

        subPackets[0] = DecodeEntry(remaining);
        return subPackets;
    }

    private static int ReadTrailingSize(ReadOnlySpan<byte> span, out int fieldLength)
    {
        byte last = span[^1];
        if (last <= 0xFA)
        {
            fieldLength = 1;
            return last;
        }

        byte b0 = last;
        byte b1 = span[^2];
        fieldLength = 2;
        return 251 + (b1 | ((b0 - 251) << 8));
    }

    private static ISubPacket DecodeEntry(ReadOnlySpan<byte> entry)
    {
        byte kind = entry[^1];
        bool hasSync = (kind & SyncBlock.SyncFlag) != 0;

        ReadOnlySpan<byte> payload;
        SyncBlock? sync = null;

        if (hasSync)
        {
            int syncStart = entry.Length - (SyncBlock.Size + 1);
            sync = SyncBlock.Decode(entry.Slice(syncStart, SyncBlock.Size));
            payload = entry.Slice(0, syncStart);
        }
        else
        {
            payload = entry.Slice(0, entry.Length - 1);
        }

        var subPacketType = (SubPacketType)(kind & ~SyncBlock.SyncFlag);
        ISubPacket subPacket = subPacketType switch
        {
            SubPacketType.Sync => new SyncSubPacket(),
            SubPacketType.LobbyMessage => new LobbyMessageSubPacket(),
            SubPacketType.PlayerInput => new PlayerInputSubPacket(),
            SubPacketType.Readiness => new ReadinessSubPacket(),
            SubPacketType.CombinedInputs => throw new Exception("CombinedInputs is outbound only"),
            SubPacketType.RosterUpdate => throw new Exception("RosterUpdate is outbound only"),
            _ => throw new Exception($"Unknown sub-packet type 0x{kind:X2}")
        };
        subPacket.Decode(payload);
        subPacket.Sync = sync;
        return subPacket;
    }
}