using System.Buffers.Binary;

namespace ZamboniDedicated.Protocol.Packets.SubPackets;

public sealed class RosterUpdateSubPacket : ISubPacket
{
    public SubPacketType Kind => SubPacketType.RosterUpdate;
    public SyncBlock? Sync { get; set; }
    private byte Version { get; set; }
    private uint Mask { get; set; }
    public int EncodedSize => 5;

    public RosterUpdateSubPacket(Roster roster)
    {
        Version = roster.Version;
        Mask = roster.Mask;
    }

    public void Decode(ReadOnlySpan<byte> payload)
    {
        Version = payload[0];
        Mask = BinaryPrimitives.ReadUInt32BigEndian(payload[1..5]);
    }

    public void Encode(Span<byte> destination, out int bytesWritten)
    {
        destination[0] = Version;
        BinaryPrimitives.WriteUInt32BigEndian(destination[1..5], Mask);
        bytesWritten = 5;
    }

    public override string ToString()
    {
        return "RosterUpdateSubPacket {"
               + $"\n      Version = {Version},"
               + $"\n      Mask = 0x{Mask:X8}"
               + (Sync is { } s ? $",\n      Sync = {s}" : "")
               + "\n    }";
    }
}