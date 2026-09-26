using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using NLog;
using ZamboniDedicated.Protocol;
using ZamboniDedicated.Protocol.Packets;
using ZamboniDedicated.Protocol.Packets.Signaling;
using ZamboniDedicated.Protocol.Packets.SubPackets;

namespace ZamboniDedicated;

public sealed class Dedicated
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private Thread? _mainThread;
    private Thread? _networkThread;
    private readonly UdpClient _serverUdpClient;
    private readonly Player?[] _players;
    private readonly Roster _roster;

    private readonly bool _standalone;
    private readonly Dictionary<string, byte> _joiningPlayers = new();
    public event Action<byte> PlayerLeavingAction;

    private readonly ConcurrentQueue<(IPEndPoint Sender, Packet Packet)> _incomingPacketQueue = new();
    public event Action<Dedicated>? Stopped;
    private readonly CancellationTokenSource _cts = new();
    private readonly Stopwatch _clock = new();
    private readonly double _tickPeriodMs;
    private double _nextTickDeadlineMs;
    private long _ticksRun;

    private readonly int _maxClients;

    private const int SendConsecutiveEmptyTicksAmount = 4;
    private const int ServerShutdownAfterNConsecutiveEmptyTicks = 324000;
    private const int KeepaliveMs = 500;
    private const int SyncIntervalMs = 200;
    private const int StallKickMs = 5000;

    private int _consecutiveEmptyTicks;

    public Dedicated(int ticksPerSecond, int maxClients, int port, bool standalone)
    {
        _maxClients = maxClients;

        _serverUdpClient = new UdpClient(port);
        _players = new Player[_maxClients];
        _roster = new Roster();

        _tickPeriodMs = 1000.0 / ticksPerSecond;

        _standalone = standalone;
    }

    public void Start()
    {
        _networkThread = new Thread(() => ReceiveLoop(_cts.Token)) { IsBackground = true, Name = "NetworkThread" };
        _networkThread.Start();

        _mainThread = new Thread(() => MainLoop(_cts.Token)) { IsBackground = true, Name = "MainThread" };
        _mainThread.Start();
    }

    public void Stop()
    {
        _cts.Cancel();
    }

    private void MainLoop(CancellationToken cancellationToken)
    {
        _clock.Start();
        _nextTickDeadlineMs = _clock.Elapsed.TotalMilliseconds;

        Logger.Info("Server started!");

        while (!cancellationToken.IsCancellationRequested)
        {
            if (_clock.Elapsed.TotalMilliseconds >= _nextTickDeadlineMs)
            {
                Logger.Trace($"ServerTick: {_ticksRun} Starts");
                Tick();
                _nextTickDeadlineMs += _tickPeriodMs;
                Logger.Trace($"ServerTick: {_ticksRun} Ends");
                _ticksRun++;
            }
            else
            {
                Thread.Sleep(1);
            }
        }

        Cleanup();
    }

    private void Tick()
    {
        ProcessIncomingPackets();
        BuildAndSendCombinedInputs();
        KeepaliveConnections();
    }

    private void Cleanup()
    {
        foreach (var player in _players)
        {
            if (player == null) continue;
            player.ReliableConnection.SendSignalPacket(new DisconnectPacket());
        }

        _serverUdpClient.Close();

        _networkThread?.Join(TimeSpan.FromSeconds(1));
        Stopped?.Invoke(this);
        Logger.Info("Server stopped");
    }

    private void ReceiveLoop(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            UdpReceiveResult result;
            try
            {
                result = _serverUdpClient.ReceiveAsync(cancellationToken).AsTask().GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }

            try
            {
                var packet = PacketDecoder.Decode(result.Buffer);
                _incomingPacketQueue.Enqueue((result.RemoteEndPoint, packet));
                Logger.Trace($"Received from: {result.RemoteEndPoint.Address} {packet}");
            }
            catch (Exception ex)
            {
                Logger.Warn($"Error while parsing packet\n" +
                            $"From: {result.RemoteEndPoint.Address}\n " +
                            $"Data: {BitConverter.ToString(result.Buffer)}");
                Logger.Warn(ex);
            }
        }
    }

    private void ProcessIncomingPackets()
    {
        int count = _incomingPacketQueue.Count;
        for (var i = 0; i < count; i++)
        {
            try
            {
                if (!_incomingPacketQueue.TryDequeue(out var tuple))
                {
                    return;
                }

                var sender = tuple.Sender;
                var packet = tuple.Packet;
                var player = _players.FirstOrDefault(player => player != null && player.IpEndpoint.Equals(sender));

                if (player is not null) player.ReliableConnection.LastReceiveMs = _clock.ElapsedMilliseconds;

                if (packet.Header.IsSignalPacket)
                {
                    switch (packet.Header.SignalType)
                    {
                        case SignalType.ConnectionRequest:
                        {
                            if (_standalone)
                            {
                                var connectPacket = ConnectPacket.FromHeader(packet.Header);
                                if (_players.Count(p => p is not null) >= _maxClients)
                                {
                                    break;
                                }

                                if (player is not null)
                                {
                                    player.ReliableConnection.SendSignalPacket(new ConnectionAcceptPacket(connectPacket.PlayerIdentifier));
                                    break;
                                }

                                byte slot = (byte)Array.IndexOf(_players, null);
                                CreatePlayer(connectPacket.PlayerIdentifier, sender, slot);
                            }
                            else
                            {
                                var connectPacket = ConnectPacket.FromHeader(packet.Header);

                                if (player is not null)
                                {
                                    player.ReliableConnection.SendSignalPacket(new ConnectionAcceptPacket(connectPacket.PlayerIdentifier));
                                    break;
                                }

                                if (_joiningPlayers.TryGetValue(sender.Address.ToString(), out var slot))
                                {
                                    CreatePlayer(connectPacket.PlayerIdentifier, sender, slot);
                                    _joiningPlayers.Remove(sender.Address.ToString());
                                }
                                else
                                {
                                    Logger.Debug($"Player [{sender}] tried to join, but he is not in JoiningPlayers");
                                }
                            }

                            break;
                        }
                        case SignalType.Disconnect:
                        {
                            if (player != null)
                            {
                                RemovePlayer(player);
                                Logger.Info($"Player {player} has left the server");
                            }

                            break;
                        }
                        case SignalType.ResendRequest:
                        {
                            if (player is not null)
                            {
                                player.ReliableConnection.RetransmitAllUnackedPackets();
                            }

                            break;
                        }
                    }

                    continue;
                }

                if (!packet.Header.IsReliablePacket || player is null)
                {
                    continue;
                }

                player.ReliableConnection.AcknowledgeUpTo(packet.Header.Acknowledgment);

                long now = _clock.ElapsedMilliseconds;

                uint missingSeq = 0;
                for (int j = packet.SubPackets.Count - 1; j >= 0; j--)
                {
                    var subPacket = packet.SubPackets[j];
                    uint effectiveSeq = packet.Header.SequenceNumber - (uint)j;

                    bool isNewPacket = player.ReliableConnection.TryAcceptIncoming(effectiveSeq, out var gap);
                    missingSeq = gap;
                    if (!isNewPacket) continue;

                    if (subPacket.Sync is { } sync)
                    {
                        player.ReliableConnection.LastEcho = sync.LocalTimestamp;
                        player.ReliableConnection.LastEchoReceivedTick = now;
                    }

                    switch (subPacket)
                    {
                        case SyncSubPacket:
                            break;
                        case LobbyMessageSubPacket:
                            foreach (var peer in _players)
                            {
                                if (peer is null) continue;
                                if (!sender.Equals(peer.IpEndpoint)) peer.ReliableConnection.Send([subPacket]);
                            }

                            break;
                        case ReadinessSubPacket readiness:
                        {
                            if (readiness.Ready)
                            {
                                player.Ready = true;

                                bool nowAllReady = AllPlayersReady();
                                if (nowAllReady)
                                {
                                    Logger.Info("All players are done loading, the game will start now");
                                    foreach (var peer in _players)
                                    {
                                        if (peer is null) continue;
                                        peer.ReliableConnection.Send([new ReadinessSubPacket(true)]);
                                    }
                                }
                            }

                            break;
                        }
                        case PlayerInputSubPacket playerInput:
                            player.EnqueueInput(playerInput);
                            break;
                    }
                }

                if (missingSeq > 0)
                {
                    player.ReliableConnection.SendSignalPacket(new ResendRequest(missingSeq));
                }

                if (now - player.ReliableConnection.LastSyncSentMs > SyncIntervalMs)
                {
                    player.ReliableConnection.BuildAndSendSyncPing();
                }
            }
            catch (Exception ex)
            {
                Logger.Warn("Error while processing packet");
                Logger.Warn(ex);
            }
        }
    }

    private void BroadcastRosterUpdate()
    {
        _roster.UpdateRoster(_players);
        foreach (var player in _players)
        {
            if (player is null) continue;
            player.ReliableConnection.Send([new RosterUpdateSubPacket(_roster)]);
        }
    }

    private void KeepaliveConnections()
    {
        var now = _clock.ElapsedMilliseconds;
        foreach (var player in _players)
        {
            if (player is null) continue;

            if (now - player.ReliableConnection.LastAnySendMs > KeepaliveMs)
            {
                player.ReliableConnection.BuildAndSendSyncPing();
            }

            long silence = now - player.ReliableConnection.LastReceiveMs;

            if (player.ReliableConnection.LastReceiveMs != 0 && silence >= StallKickMs)
            {
                RemovePlayer(player);
                Logger.Info($"Player {player} stalled. Kicking from server.");
            }
        }
    }

    private void CreatePlayer(uint playerIdentifier, IPEndPoint ipEndPoint, byte slot)
    {
        Logger.Info($"{playerIdentifier},  {ipEndPoint}, {slot}");
        var newPlayer = new Player(playerIdentifier, ipEndPoint, _serverUdpClient, _clock);
        _players[slot] = newPlayer;

        newPlayer.ReliableConnection.SendSignalPacket(new ConnectionAcceptPacket(playerIdentifier));
        BroadcastRosterUpdate();
        Logger.Info($"Player {newPlayer} joined the server");
    }

    private void RemovePlayer(Player player)
    {
        byte index = (byte)Array.IndexOf(_players, player);
        _players[Array.IndexOf(_players, player)] = null;
        BroadcastRosterUpdate();
        if (!_players.Any(p => p is not null))
        {
            Stop();
        }

        if (!_standalone)
        {
            PlayerLeavingAction.Invoke(index);
        }
    }

    private bool AllPlayersReady()
    {
        var allReady = false;
        foreach (var player in _players)
        {
            if (player is null) continue;
            if (!player.Ready)
            {
                allReady = false;
                break;
            }

            allReady = true;
        }

        return allReady;
    }

    public bool AddJoiningPlayer(byte slot, string ipAddress)
    {
        if (_players.Count(p => p is not null) >= _maxClients)
        {
            return false;
        }
        _joiningPlayers.Add(ipAddress, slot);
        return true;
    }
    
    private void BuildAndSendCombinedInputs()
    {
        var connected = _players.Where(p => p is not null).Cast<Player>().ToList();
        if (connected.Count == 0 || !connected.Any(p => p.HasEverSentInput)) return;

        bool anyInputThisTick = connected.Any(p => p.HasPendingInput);
        _consecutiveEmptyTicks = anyInputThisTick ? 0 : _consecutiveEmptyTicks + 1;
        if (_consecutiveEmptyTicks >= SendConsecutiveEmptyTicksAmount)
        {
            if (_consecutiveEmptyTicks >= ServerShutdownAfterNConsecutiveEmptyTicks)
            {
                Stop();
            }

            return;
        }

        var inputs = connected.ToDictionary(p => p, p => p.TakeInputForTick());

        foreach (var recipient in connected)
        {
            var others = connected.Where(p => p != recipient).Select(p => inputs[p]).ToList();

            if (others.Count == 0) others.Add(new PlayerInputSubPacket(InputPayloadKind.Disconnected, Array.Empty<byte>()));

            var combined = new CombinedInputSubPacket
            {
                RecipientInputsConsumedSinceLastTick = recipient.InputsConsumedSinceLastTick,
                RosterVersion = _roster.Version,
                PlayerCount = Math.Max(2, connected.Count),
                OtherPlayers = others,
            };
            recipient.ReliableConnection.Send([combined]);
            recipient.ResetConsumedCount();
        }
    }
}