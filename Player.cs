using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using ZamboniDedicated.Protocol;
using ZamboniDedicated.Protocol.Packets.SubPackets;

namespace ZamboniDedicated;

public class Player
{
    private uint PlayerIdentifier { get; }
    public IPEndPoint IpEndpoint { get; }
    public ReliableConnection ReliableConnection { get; }
    public bool Ready { get; set; }
    private List<PlayerInputSubPacket> PendingInputs { get; } = new();
    public bool HasEverSentInput { get; private set; }
    public bool HasPendingInput => PendingInputs.Count > 0;
    public byte InputsConsumedSinceLastTick { get; private set; }

    public Player(uint playerIdentifier, IPEndPoint endpoint, UdpClient serverUdpClient, Stopwatch clock)
    {
        PlayerIdentifier = playerIdentifier;
        ReliableConnection = new ReliableConnection(serverUdpClient, endpoint, clock);
        IpEndpoint = endpoint;
    }

    public void ResetConsumedCount() => InputsConsumedSinceLastTick = 0;

    public void EnqueueInput(PlayerInputSubPacket input)
    {
        HasEverSentInput = true;
        if (PendingInputs.Count > 0 && PendingInputs[^1].InputKind == InputPayloadKind.Supersedable)
        {
            PendingInputs[^1] = input;
            InputsConsumedSinceLastTick++;
        }
        else
        {
            PendingInputs.Add(input);
        }
    }

    public PlayerInputSubPacket TakeInputForTick()
    {
        if (PendingInputs.Count == 0)
        {
            return new PlayerInputSubPacket(InputPayloadKind.Empty, Array.Empty<byte>());
        }

        var input = PendingInputs[0];
        PendingInputs.RemoveAt(0);
        InputsConsumedSinceLastTick++;
        return input;
    }

    public override string ToString()
    {
        return $"[[{PlayerIdentifier}][{IpEndpoint}]]";
    }
}