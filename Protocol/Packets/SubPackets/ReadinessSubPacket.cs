namespace ZamboniDedicated.Protocol.Packets.SubPackets;

public sealed class ReadinessSubPacket : ISubPacket
{
    public SubPacketType Kind => SubPacketType.Readiness;
    public SyncBlock? Sync { get; set; }
    public bool Ready { get; private set; }
    private byte[] Padding { get; set; } = Array.Empty<byte>();
    public int EncodedSize => 7;

    public ReadinessSubPacket()
    {
    }

    public ReadinessSubPacket(bool ready)
    {
        Ready = ready;
        Padding = [0, 0, 0, 0, 0];
    }

    public void Decode(ReadOnlySpan<byte> payload)
    {
        Ready = payload[0] != 0 && payload[1] != 0;
        Padding = payload[2..].ToArray();
    }

    public void Encode(Span<byte> destination, out int bytesWritten)
    {
        destination[0] = (byte)(Ready ? 1 : 0);
        destination[1] = (byte)(Ready ? 1 : 0);

        Padding.CopyTo(destination[2..]);
        bytesWritten = 2 + Padding.Length;
    }

    public override string ToString()
    {
        return "ReadinessSignalSubPacket {"
               + $"\n      Ready = {Ready},"
               + $"\n      Padding = {Convert.ToHexString(Padding)}"
               + (Sync is { } s ? $",\n      Sync = {s}" : "")
               + "\n    }";
    }
}