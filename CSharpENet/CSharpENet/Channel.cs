namespace ENet;

struct ENetChannel
{
    public ushort outReliableSeqNumber = 0;
    public ushort outUnreliableSeqNumber = 0;
    public ushort usedReliableWindows = 0;
    public ushort[] rSeliableWindows = new ushort[(int)ENetDef.PeerReliableWindows];
    public ushort inReliableSeqNumber = 0;
    public ushort inUnreliableSeqNumber = 0;
    public LinkedList<ENetInCmd> inReliableCmds = new();
    public LinkedList<ENetInCmd> inUnreliableCmds = new();

    public ENetChannel()
    {

    }
}
