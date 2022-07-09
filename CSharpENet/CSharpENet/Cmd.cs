using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ENet;

class ENetAckCmd
{
    public uint sentTime;
    public ENetProto cmd;
};

class ENetOutCmd
{
    public uint reliableSequenceNumber;
    public uint unreliableSeqNum;
    public uint sentTime;
    public uint roundTripTimeout;
    public uint roundTripTimeoutLimit;
    public uint fragmentOffset;
    public uint fragmentLength;
    public uint sendAttempts;
    public ENetProto command;
    public ENetPacket? packet;
};

struct ENetInCmd
{
    public uint reliableSeqNum;
    public uint unreliableSeqNum;
    public ENetProtoCmdHeader cmdHeader;
    public uint fragmentCount;
    public uint fragmentsRemaining;
    public uint[]? fragments;
    public ENetPacket packet;
};
