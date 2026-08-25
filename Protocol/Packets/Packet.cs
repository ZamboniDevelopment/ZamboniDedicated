using System.Text;

namespace ZamboniDedicated.Protocol.Packets;

public sealed class Packet
{
    public PacketHeader Header { get; init; }
    public IReadOnlyList<ISubPacket> SubPackets { get; init; } = Array.Empty<ISubPacket>();

    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.Append("Packet {");
        sb.Append($"\n  Header = {Header},");
        if (SubPackets.Count == 0)
        {
            sb.Append("\n  SubPackets = []");
        }
        else
        {
            sb.Append("\n  SubPackets = [");
            for (int i = 0; i < SubPackets.Count; i++)
            {
                sb.Append("\n    ").Append(SubPackets[i]);
                if (i < SubPackets.Count - 1) sb.Append(',');
            }

            sb.Append("\n  ]");
        }

        sb.Append("\n}");
        return sb.ToString();
    }
}