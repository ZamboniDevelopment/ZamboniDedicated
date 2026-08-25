namespace ZamboniDedicated;

public class Roster
{
    public byte Version { get; private set; }
    public uint Mask { get; private set; }

    public void UpdateRoster(Player?[] players)
    {
        Mask = 0;
        for (int i = 0; i < players.Length; i++)
        {
            if (players[i] is not null)
            {
                Mask |= 1u << i;
            }
        }

        Version = (byte)((Version + 1) % 32);
    }
}