namespace ZamboniDedicated.Protocol.Packets.SubPackets;

public sealed class CombinedInputSubPacket : ISubPacket
{
    public SubPacketType Kind => SubPacketType.CombinedInputs;
    public SyncBlock? Sync { get; set; }
    public byte RecipientInputsConsumedSinceLastTick { get; init; }
    public byte RosterVersion { get; init; }
    public int PlayerCount { get; init; }
    public IReadOnlyList<PlayerInputSubPacket> OtherPlayers { get; init; } = Array.Empty<PlayerInputSubPacket>();
    public int EncodedSize => HeaderBytes + (OtherPlayers.Count + 1) / 2 + (OtherPlayers.Count - 1) + OtherPlayers.Sum(p => p.Payload.Length);
    private static int HeaderBytes => 2;

    public void Decode(ReadOnlySpan<byte> payload)
    {
        throw new NotImplementedException();
    }

    public void Encode(Span<byte> destination, out int bytesWritten)
    {
        int entryCount = OtherPlayers.Count;

        destination[0] = RecipientInputsConsumedSinceLastTick;
        destination[1] = RosterVersion;

        int kindNibbleByteCount = (entryCount + 1) / 2;
        destination.Slice(HeaderBytes, kindNibbleByteCount).Clear();
        for (int entry = 0; entry < entryCount; entry++)
        {
            byte kindNibble = (byte)OtherPlayers[entry].InputKind;
            destination[HeaderBytes + entry / 2] |= (byte)(kindNibble << (entry % 2 * 4));
        }

        int lengthTableStart = HeaderBytes + kindNibbleByteCount;
        for (int entry = 0; entry < entryCount - 1; entry++)
        {
            int size = OtherPlayers[entry].Payload.Length;
            destination[lengthTableStart + entry] = (byte)size;
        }

        int payloadStart = lengthTableStart + (entryCount - 1);
        int writeCursor = payloadStart;
        foreach (var entry in OtherPlayers)
        {
            entry.Payload.CopyTo(destination[writeCursor..]);
            writeCursor += entry.Payload.Length;
        }

        bytesWritten = writeCursor;
    }

    public override string ToString()
    {
        var others = string.Join(", ", OtherPlayers.Select(p =>
            $"([{p.InputKind}][{Convert.ToHexString(p.Payload)}])"));
        return "CombinedInputSubPacket {"
               + $"\n      RecipientInputsConsumedSinceLastTick = {RecipientInputsConsumedSinceLastTick},"
               + $"\n      RosterVersion = {RosterVersion},"
               + $"\n      PlayerCount = {PlayerCount},"
               + $"\n      Others = [{others}]"
               + (Sync is { } s ? $",\n      Sync = {s}" : "")
               + "\n    }";
    }
}