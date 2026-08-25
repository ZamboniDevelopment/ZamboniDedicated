namespace ZamboniDedicated.Protocol.Packets.SubPackets;

public sealed class LobbyMessageSubPacket : ISubPacket
{
    public SubPacketType Kind => SubPacketType.LobbyMessage;
    public SyncBlock? Sync { get; set; }
    private byte[] Payload { get; set; } = Array.Empty<byte>();
    public int EncodedSize => Payload.Length;

    public void Decode(ReadOnlySpan<byte> payload)
    {
        Payload = payload[0..].ToArray();
    }

    public void Encode(Span<byte> destination, out int bytesWritten)
    {
        Payload.CopyTo(destination[0..]);
        bytesWritten = Payload.Length;
    }

    public override string ToString()
    {
        return "LobbyMessageSubPacket {"
               + $"\n      Payload = {Convert.ToHexString(Payload)} ({Payload.Length}B)"
               + (Sync is { } s ? $",\n      Sync = {s}" : "")
               + "\n    }";
    }
}