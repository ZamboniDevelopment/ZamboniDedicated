using System.Buffers.Binary;
using ZamboniDedicated.Protocol.Packets;

namespace ZamboniDedicated.Protocol;

public static class PacketEncoder
{
    private static int HeaderSize => 8;
    private static int MeasureSubPacket(ISubPacket subPacket) => subPacket.EncodedSize + (subPacket.Sync is not null ? SyncBlock.Size : 0) + 1;

    public static byte[] Encode(Packet packet)
    {
        var total = HeaderSize;
        foreach (var sp in packet.SubPackets) total += MeasureSubPacket(sp);

        var buffer = new byte[total];
        var span = buffer.AsSpan();
        BinaryPrimitives.WriteUInt32BigEndian(span[..4], packet.Header.ToRawSeq());
        BinaryPrimitives.WriteUInt32BigEndian(span[4..8], packet.Header.Acknowledgment);

        int pos = HeaderSize;
        foreach (var sp in packet.SubPackets)
        {
            EncodeSubPacket(sp, span[pos..], out int written);
            pos += written;
        }

        return buffer;
    }

    private static void EncodeSubPacket(ISubPacket subPacket, Span<byte> destination, out int bytesWritten)
    {
        subPacket.Encode(destination, out int payloadLength);
        int offset = payloadLength;

        byte kindByte = (byte)subPacket.Kind;
        if (subPacket.Sync is { } sync)
        {
            sync.Encode(destination.Slice(offset, SyncBlock.Size));
            offset += SyncBlock.Size;
            kindByte |= SyncBlock.SyncFlag;
        }

        destination[offset] = kindByte;
        bytesWritten = offset + 1;
    }
}