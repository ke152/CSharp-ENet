namespace ENet;

struct ENetChannel
{
    public uint outReliableSeqNumber = 0;
    public uint outUnreliableSeqNum = 0;
    public uint usedReliableWindows = 0;
    public uint[] rSeliableWindows = new uint[(int)ENetDef.PeerReliableWindows];
    public uint inReliableSeqNum = 0;
    public uint inUnreliableSeqNum = 0;
    public LinkedList<ENetInCmd> inReliableCmds = new();
    public LinkedList<ENetInCmd> inUnreliableCmds = new();

    public ENetChannel()
    {

    }
}
