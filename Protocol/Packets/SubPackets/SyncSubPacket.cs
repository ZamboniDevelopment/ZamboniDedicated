namespace ZamboniDedicated.Protocol.Packets.SubPackets;

public sealed class SyncSubPacket : ISubPacket
{
    public SubPacketType Kind => SubPacketType.Sync;
    public SyncBlock? Sync { get; set; }
    public int EncodedSize => 0;

    public void Decode(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != 0) throw new ArgumentException($"SyncSubPacket contains only SyncBlock");
    }

    public void Encode(Span<byte> destination, out int bytesWritten)
    {
        bytesWritten = 0;
    }

    public override string ToString()
    {
        return $"SyncSubPacket {{ Sync = {Sync?.ToString() ?? "null"} }}";
    }
}