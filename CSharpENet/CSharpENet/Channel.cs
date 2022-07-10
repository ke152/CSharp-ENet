namespace ENet;

class ENetChannel
{
    public uint outReliableSeqNum = 0;
    public uint outUnreliableSeqNum = 0;
    public int usedReliableWindows = 0;
    public uint[] reliableWindows = new uint[(int)ENetDef.PeerReliableWindows];
    public uint inReliableSeqNum = 0;
    public uint inUnreliableSeqNum = 0;
    public LinkedList<ENetInCmd> inReliableCmds = new();
    public LinkedList<ENetInCmd> inUnreliableCmds = new();

    public ENetChannel()
    {

    }
}
