namespace ZamboniDedicated.Protocol.Packets.SubPackets;

public enum InputPayloadKind : byte
{
    Empty = 1,
    Guaranteed = 2,
    Supersedable = 3,
    Disconnected = 4,
}

public sealed class PlayerInputSubPacket : ISubPacket
{
    public SubPacketType Kind => SubPacketType.PlayerInput;
    public SyncBlock? Sync { get; set; }
    public InputPayloadKind InputKind { get; private set; }
    public byte[] Payload { get; private set; } = Array.Empty<byte>();
    public int EncodedSize => 1 + Payload.Length;


    public PlayerInputSubPacket()
    {
    }

    public PlayerInputSubPacket(InputPayloadKind inputKind, byte[] payload)
    {
        InputKind = inputKind;
        Payload = payload;
    }

    public void Decode(ReadOnlySpan<byte> payload)
    {
        InputKind = (InputPayloadKind)payload[0];
        Payload = payload[1..].ToArray();
    }

    public void Encode(Span<byte> destination, out int bytesWritten)
    {
        destination[0] = (byte)InputKind;
        Payload.CopyTo(destination[1..]);
        bytesWritten = 1 + Payload.Length;
    }

    public override string ToString() =>
        "PlayerInputSubPacket {"
        + $"\n      InputKind = {InputKind},"
        + $"\n      Payload = {Convert.ToHexString(Payload)}"
        + (Sync is { } s ? $",\n      Sync = {s}" : "")
        + "\n    }";
}