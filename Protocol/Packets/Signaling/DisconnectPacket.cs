namespace ZamboniDedicated.Protocol.Packets.Signaling;

public readonly record struct DisconnectPacket(uint PlayerIdentifier) : ISignalPacket
{
    public Packet ToPacket() => new()
    {
        Header = new PacketHeader(
            additionalSubpackets: 0,
            unknown: 0,
            sequenceNumber: (uint)SignalType.Disconnect,
            acknowledgment: PlayerIdentifier),
        SubPackets = Array.Empty<ISubPacket>()
    };
}