using ZamboniDedicated.Protocol.Packets.Signaling;

namespace ZamboniDedicated.Protocol.Packets;

public readonly struct PacketHeader
{
    public byte AdditionalSubpackets { get; }
    private byte Unknown { get; }
    public uint SequenceNumber { get; }
    public uint Acknowledgment { get; }

    public bool IsSignalPacket => SequenceNumber is >= 1 and <= 4;
    public bool IsReliablePacket => SequenceNumber >= 256;
    public SignalType? SignalType => IsSignalPacket ? (SignalType)SequenceNumber : null;

    public PacketHeader(byte additionalSubpackets, byte unknown, uint sequenceNumber, uint acknowledgment)
    {
        if (additionalSubpackets > 0xF) throw new ArgumentOutOfRangeException(nameof(additionalSubpackets));
        if (unknown > 0xF) throw new ArgumentOutOfRangeException(nameof(unknown));
        if (sequenceNumber > ReliableConnection.SequenceMax) throw new ArgumentOutOfRangeException(nameof(sequenceNumber));

        AdditionalSubpackets = additionalSubpackets;
        Unknown = unknown;
        SequenceNumber = sequenceNumber;
        Acknowledgment = acknowledgment;
    }

    public static PacketHeader FromRaw(uint rawSeq, uint ack)
    {
        byte extra = (byte)(rawSeq >> 28);
        byte meta = (byte)((rawSeq >> 24) & 0xF);
        uint seqn = rawSeq & 0x00FFFFFF;
        return new PacketHeader(extra, meta, seqn, ack);
    }

    public uint ToRawSeq()
    {
        return ((uint)AdditionalSubpackets << 28) | ((uint)Unknown << 24) | SequenceNumber;
    }

    public override string ToString() =>
        $"PacketHeader {{ Extra = {AdditionalSubpackets}, Unknown = {Unknown}, " +
        $"Seq = {SequenceNumber}, Ack = {Acknowledgment}" +
        $"{(SignalType is { } ct ? $", SignalType = {ct}" : "")} }}";
}