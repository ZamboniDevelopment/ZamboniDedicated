namespace ZamboniDedicated.Protocol.Packets.SubPackets;

public enum SubPacketType : byte
{
    Sync = 0x00,
    LobbyMessage = 0x01,
    PlayerInput = 0x03,
    CombinedInputs = 0x04,
    Readiness = 0x09,
    RosterUpdate = 0x0B,
}