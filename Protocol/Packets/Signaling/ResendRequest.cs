namespace ZamboniDedicated.Protocol.Packets.Signaling;

public readonly record struct ResendRequest(uint MissingSequenceNumber) : ISignalPacket
{
    public Packet ToPacket() => new()
    {
        Header = new PacketHeader(
            additionalSubpackets: 0,
            unknown: 0,
            sequenceNumber: (uint)SignalType.ResendRequest,
            acknowledgment: MissingSequenceNumber),
        SubPackets = Array.Empty<ISubPacket>()
    };

    public static ResendRequest FromHeader(PacketHeader header)
    {
        return new ResendRequest(header.Acknowledgment);
    }
}