namespace ENet;

class ENetChannel
{
    public uint outgoingReliableSequenceNumber = 0;
    public uint outgoingUnreliableSequenceNumber = 0;
    public int usedReliableWindows = 0;
    public uint[] reliableWindows = new uint[(int)ENetDef.PeerReliableWindows];
    public uint incomingReliableSequenceNumber = 0;
    public uint incomingUnreliableSequenceNumber = 0;
    public LinkedList<ENetInCmd> incomingReliableCommands = new();
    public LinkedList<ENetInCmd> incomingUnreliableCommands = new();

    public ENetChannel()
    {

    }
}
