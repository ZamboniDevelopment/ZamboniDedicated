namespace ZamboniDedicated.Protocol.Packets.Signaling;

public readonly record struct ConnectPacket(uint PlayerIdentifier) : ISignalPacket
{
    public Packet ToPacket() => new()
    {
        Header = new PacketHeader(
            additionalSubpackets: 0,
            unknown: 0,
            sequenceNumber: (uint)SignalType.ConnectionRequest,
            acknowledgment: PlayerIdentifier),
        SubPackets = Array.Empty<ISubPacket>()
    };

    public static ConnectPacket FromHeader(PacketHeader header)
    {
        return new ConnectPacket(header.Acknowledgment);
    }
}