namespace ZamboniDedicated.Protocol.Packets.Signaling;

public readonly record struct ConnectionAcceptPacket(uint PlayerIdentifier) : ISignalPacket
{
    public Packet ToPacket() => new()
    {
        Header = new PacketHeader(
            additionalSubpackets: 0,
            unknown: 0,
            sequenceNumber: (uint)SignalType.ConnectionAccept,
            acknowledgment: PlayerIdentifier),
        SubPackets = Array.Empty<ISubPacket>()
    };
}