using ZamboniDedicated.Protocol.Packets.SubPackets;

namespace ZamboniDedicated.Protocol.Packets;

public interface ISubPacket
{
    SubPacketType Kind { get; }
    SyncBlock? Sync { get; set; }
    int EncodedSize { get; }
    void Decode(ReadOnlySpan<byte> payload);
    void Encode(Span<byte> destination, out int bytesWritten);
}