using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using NLog;
using ZamboniDedicated.Protocol.Packets;
using ZamboniDedicated.Protocol.Packets.Signaling;
using ZamboniDedicated.Protocol.Packets.SubPackets;

namespace ZamboniDedicated.Protocol;

public class ReliableConnection
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private Stopwatch Clock { get; }
    private List<Packet> SentUnacknowledgedPackets { get; } = new();
    private UdpClient ServerUdpClient { get; }
    private IPEndPoint Destination { get; }
    private bool HasAcceptedAnyPacket { get; set; }
    private int SentSinceLastSync { get; set; }

    public long LastSyncSentMs { get; private set; }
    public uint LastEcho { get; set; }
    public long LastEchoReceivedTick { get; set; }

    private uint CurrentOutgoingSequence { get; set; } = 256;
    private uint NextExpectedIncomingSequence { get; set; } = 256;
    public static uint SequenceMax => 16777215;

    public long LastAnySendMs { get; private set; }
    public long LastReceiveMs { get; set; }

    public ReliableConnection(UdpClient serverUdpClient, IPEndPoint destination, Stopwatch clock)
    {
        ServerUdpClient = serverUdpClient;
        Destination = destination;
        Clock = clock;
    }

    public void Send(IReadOnlyList<ISubPacket> subPackets)
    {
        uint ack = ComputeAck();
        var header = new PacketHeader(0, 0, CurrentOutgoingSequence, ack);
        var packet = new Packet { Header = header, SubPackets = subPackets };

        Transmit(packet, false);
        SentUnacknowledgedPackets.Add(packet);
        CurrentOutgoingSequence++;
        SentSinceLastSync++;
    }

    private uint ComputeAck()
    {
        return HasAcceptedAnyPacket ? NextExpectedIncomingSequence - 1 : SequenceMax;
    }

    public void RetransmitAllUnackedPackets()
    {
        foreach (var pending in SentUnacknowledgedPackets)
        {
            Transmit(pending, true);
        }
    }

    private void Transmit(Packet packet, bool retransmission)
    {
        byte[] bytes = PacketEncoder.Encode(packet);

        LastAnySendMs = Clock.ElapsedMilliseconds;
        ServerUdpClient.Send(bytes, bytes.Length, Destination);
        Logger.Trace($"Sending to: {Destination.Address} retransmission: {retransmission} {packet}");
    }

    public void AcknowledgeUpTo(uint ackValue)
    {
        if (ackValue >= SequenceMax)
        {
            return;
        }

        while (SentUnacknowledgedPackets.Count > 0 && SentUnacknowledgedPackets[0].Header.SequenceNumber <= ackValue)
        {
            SentUnacknowledgedPackets.RemoveAt(0);
        }
    }

    public bool TryAcceptIncoming(uint incomingSeq, out uint missingSeq)
    {
        missingSeq = 0;
        if (incomingSeq == NextExpectedIncomingSequence)
        {
            NextExpectedIncomingSequence++;
            HasAcceptedAnyPacket = true;
            return true;
        }

        if (incomingSeq > NextExpectedIncomingSequence)
        {
            missingSeq = NextExpectedIncomingSequence;
        }

        return false;
    }

    public void SendSignalPacket(ISignalPacket signalPacket)
    {
        var packet = signalPacket.ToPacket();
        ServerUdpClient.Send(PacketEncoder.Encode(packet), Destination);
        Logger.Trace("Sending to: " + Destination.Address + " " + packet);
    }

    public void BuildAndSendSyncPing()
    {
        var now = Clock.ElapsedMilliseconds;
        uint reflectedTimestamp = LastEcho + (uint)(now - LastEchoReceivedTick);
        uint localTimestamp = (uint)now;
        byte psls = TakeSentSinceLastSync();

        var sync = new SyncSubPacket { Sync = new SyncBlock(reflectedTimestamp, localTimestamp, ReportedLatencyMs: 0, PacketsSinceLastSync: psls) };
        Send([sync]);
        LastSyncSentMs = now;
    }

    private byte TakeSentSinceLastSync()
    {
        int n = Math.Min(255, SentSinceLastSync);
        SentSinceLastSync = 0;
        return (byte)n;
    }
}