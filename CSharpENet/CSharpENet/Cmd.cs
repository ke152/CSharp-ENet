using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ENet;

class ENetAckCmd
{
    public uint sentTime;
    public ENetProtoCmdHeader cmdHeader;
};

class ENetOutCmd
{
    public uint reliableSeqNum;
    public uint unreliableSeqNum;
    public long sentTime;
    public long rttTimeout;
    public long rttTimeoutLimit;
    public uint fragmentOffset;
    public uint fragmentLength;
    public uint sendAttempts;
    public ENetProtoCmdHeader cmdHeader
    {
        get { return this.cmd.header; }
        set { this.cmd.header = value; }
    }
    public ENetProto cmd = new ENetProto();
    public ENetPacket? packet;
};

class ENetInCmd
{
    public uint reliableSeqNum ;
    public uint unreliableSeqNum;
    public ENetProtoCmdHeader cmdHeader;
    public uint fragmentCount;
    public uint fragmentsRemaining;
    public uint[]? fragments;
    public ENetPacket packet;
};
