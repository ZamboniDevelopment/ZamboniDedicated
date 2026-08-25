using System.Buffers.Binary;

namespace ZamboniDedicated.Protocol.Packets;

public readonly record struct SyncBlock
{
    public static byte SyncFlag { get; } = 0x40;
    public static byte Size { get; } = 12;
    public uint ReflectedTimestamp { get; }
    public uint LocalTimestamp { get; }
    public short ReportedLatencyMs { get; }
    public byte PacketsSinceLastSync { get; }

    public SyncBlock(uint ReflectedTimestamp, uint LocalTimestamp, short ReportedLatencyMs, byte PacketsSinceLastSync)
    {
        this.ReflectedTimestamp = ReflectedTimestamp;
        this.LocalTimestamp = LocalTimestamp;
        this.ReportedLatencyMs = ReportedLatencyMs;
        this.PacketsSinceLastSync = PacketsSinceLastSync;
    }

    public static SyncBlock Decode(ReadOnlySpan<byte> data)
    {
        uint reflectedTimestamp = BinaryPrimitives.ReadUInt32BigEndian(data[..4]);
        uint localTimestamp = BinaryPrimitives.ReadUInt32BigEndian(data[4..8]);
        short reportedLatencyMs = BinaryPrimitives.ReadInt16BigEndian(data[8..10]);
        byte packetsSinceLastSync = data[10];
        return new SyncBlock(reflectedTimestamp, localTimestamp, reportedLatencyMs, packetsSinceLastSync);
    }

    public void Encode(Span<byte> destination)
    {
        BinaryPrimitives.WriteUInt32BigEndian(destination[..4], ReflectedTimestamp);
        BinaryPrimitives.WriteUInt32BigEndian(destination[4..8], LocalTimestamp);
        BinaryPrimitives.WriteInt16BigEndian(destination[8..10], ReportedLatencyMs);
        destination[10] = PacketsSinceLastSync;
        destination[11] = Size;
    }
}