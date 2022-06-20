﻿using System.Runtime.InteropServices;

namespace ENet;


enum ENetProtoCmdType
{
    None = 0,
    Ack = 1,
    Connect = 2,
    VerifyConnect = 3,
    Disconnect = 4,
    Ping = 5,
    SendReliable = 6,
    SendUnreliable = 7,
    SendFragment= 8,
    SendUnseq = 9,
    BandwidthLimit = 10,
    ThrottleConfig = 11,
    SendUnreliableFragment= 12,
    Count = 13,
};


static class ENetProtoCmdSize
{
    public static List<uint> CmdSize = Init();
           

    private static List<uint> Init()
    {
        List<uint> cmdSizeList = new List<uint>();

        cmdSizeList.Add(0);
        cmdSizeList.Add(Convert.ToUInt32(Marshal.SizeOf(new ENetProtoAck())));
        cmdSizeList.Add(Convert.ToUInt32(Marshal.SizeOf(new ENetProtoConnect())));
        cmdSizeList.Add(Convert.ToUInt32(Marshal.SizeOf(new ENetProtoVerifyConnect())));
        cmdSizeList.Add(Convert.ToUInt32(Marshal.SizeOf(new ENetProtoDisconnect())));
        cmdSizeList.Add(Convert.ToUInt32(Marshal.SizeOf(new ENetProtoPing())));
        cmdSizeList.Add(Convert.ToUInt32(Marshal.SizeOf(new ENetProtoSendReliable())));
        cmdSizeList.Add(Convert.ToUInt32(Marshal.SizeOf(new ENetProtoSendUnReliable())));
        cmdSizeList.Add(Convert.ToUInt32(Marshal.SizeOf(new ENetProtoSendUnsequenced())));
        cmdSizeList.Add(Convert.ToUInt32(Marshal.SizeOf(new ENetProtoSendUnsequenced())));
        cmdSizeList.Add(Convert.ToUInt32(Marshal.SizeOf(new ENetProtoBandwidthLimit())));
        cmdSizeList.Add(Convert.ToUInt32(Marshal.SizeOf(new ENetProtoThrottleConfigure())));
        cmdSizeList.Add(Convert.ToUInt32(Marshal.SizeOf(new ENetProtoSendFragment())));

        return cmdSizeList;
    }
};

struct ENetProtoHeader
{
    public uint peerID;
    public uint sentTime;
    
};

struct ENetProtoCmdHeader
{
    public ENetProtoCmdType command;
    public byte channelID;
    public uint reliableSeqNum;
};

struct ENetProtoAck
{
    public ENetProtoCmdHeader header;
    public uint receivedReliableSequenceNumber;
    public uint receivedSentTime;
};

struct ENetProtoConnect
{
    public ENetProtoCmdHeader header;
    public uint outgoingPeerID;
    public byte incomingSessionID;
    public byte outgoingSessionID;
    public uint mtu;
    public uint windowSize;
    public uint channelCount;
    public uint incomingBandwidth;
    public uint outgoingBandwidth;
    public uint packetThrottleInterval;
    public uint packetThrottleAcceleration;
    public uint packetThrottleDeceleration;
    public uint connectID;
    public uint data;
};

struct ENetProtoVerifyConnect
{
    public ENetProtoCmdHeader header;
    public uint outgoingPeerID;
    public byte incomingSessionID;
    public byte outgoingSessionID;
    public uint mtu;
    public uint windowSize;
    public uint channelCount;
    public uint incomingBandwidth;
    public uint outgoingBandwidth;
    public uint packetThrottleInterval;
    public uint packetThrottleAcceleration;
    public uint packetThrottleDeceleration;
    public uint connectID;
};

struct ENetProtoBandwidthLimit
{
    public ENetProtoCmdHeader header;
    public uint incomingBandwidth;
    public uint outgoingBandwidth;
};

struct ENetProtoThrottleConfigure
{
    public ENetProtoCmdHeader header;
    public uint packetThrottleInterval;
    public uint packetThrottleAcceleration;
    public uint packetThrottleDeceleration;
};

struct ENetProtoDisconnect
{
    public ENetProtoCmdHeader header;
    public uint data;
};

struct ENetProtoPing
{
    public ENetProtoCmdHeader header;
};

struct ENetProtoSendReliable
{
    public ENetProtoCmdHeader header;
    public uint dataLength;
};

struct ENetProtoSendUnReliable
{
    public ENetProtoCmdHeader header;
    public uint unReliableSequenceNumber;
    public uint dataLength;
};

struct ENetProtoSendUnsequenced
{
    public ENetProtoCmdHeader header;
    public uint unsequencedGroup;
    public uint dataLength;
};

struct ENetProtoSendFragment
{
    public ENetProtoCmdHeader header;
    public uint startSequenceNumber;
    public uint dataLength;
    public uint fragmentCount;
    public uint fragmentNumber;
    public uint totalLength;
    public uint fragmentOffset;
};

struct ENetProto
{
    public ENetProtoCmdHeader header;
    public ENetProtoAck acknowledge;
    public ENetProtoConnect connect;
    public ENetProtoVerifyConnect verifyConnect;
    public ENetProtoDisconnect disconnect;
    public ENetProtoPing ping;
    public ENetProtoSendReliable sendReliable;
    public ENetProtoSendUnReliable sendUnReliable;
    public ENetProtoSendUnsequenced sendUnsequenced;
    public ENetProtoSendFragment sendFragment;
    public ENetProtoBandwidthLimit bandwidthLimit;
    public ENetProtoThrottleConfigure throttleConfigure;
};