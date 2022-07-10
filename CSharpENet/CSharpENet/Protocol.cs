using System.Runtime.InteropServices;

namespace ENet;


enum ENetProtoFlag
{
    CmdFlagUnSeq = (1 << 6),
    CmdFlagAck = (1 << 7),

    HeaderFalgCompressed = (1 << 14),
    HeaderFalgSentTime = (1 << 15),
    HeaderFalgMASK = HeaderFalgCompressed | HeaderFalgSentTime,

    HeaderSessionMask = (3 << 12),
    HeaderSessionShift = 12
};


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
    Mask = 15
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

class ENetProtoCmdHeader
{
    public int cmdFlag;
    public uint channelID;
    public uint reliableSeqNum;
};

class ENetProtoAck
{
    public ENetProtoCmdHeader header;
    public uint receivedReliableSeqNum;
    public uint receivedSentTime;
};

class ENetProtoConnect
{
    public ENetProtoCmdHeader header;
    public uint outPeerID;
    public uint inSessionID;
    public uint outSessionID;
    public uint mtu;
    public uint windowSize;
    public uint channelCount;
    public uint inBandwidth;
    public uint outBandwidth;
    public uint packetThrottleInterval;
    public uint packetThrottleAcceleration;
    public uint packetThrottleDeceleration;
    public uint connectID;
    public uint data;
};

class ENetProtoVerifyConnect
{
    public ENetProtoCmdHeader header;
    public uint outPeerID;
    public uint inSessionID;
    public uint outSessionID;
    public uint mtu;
    public uint windowSize;
    public uint channelCount;
    public uint inBandwidth;
    public uint outBandwidth;
    public uint packetThrottleInterval;
    public uint packetThrottleAcceleration;
    public uint packetThrottleDeceleration;
    public uint connectID;
};

class ENetProtoBandwidthLimit
{
    public ENetProtoCmdHeader header;
    public uint inBandwidth;
    public uint outBandwidth;
};

class ENetProtoThrottleConfigure
{
    public ENetProtoCmdHeader header;
    public uint packetThrottleInterval;
    public uint packetThrottleAcceleration;
    public uint packetThrottleDeceleration;
};

class ENetProtoDisconnect
{
    public ENetProtoCmdHeader header;
    public uint data;
};

class ENetProtoPing
{
    public ENetProtoCmdHeader header;
};

class ENetProtoSendReliable
{
    public ENetProtoCmdHeader header;
    public uint dataLength;
};

class ENetProtoSendUnReliable
{
    public ENetProtoCmdHeader header;
    public uint unreliableSeqNum;
    public uint dataLength;
};

class ENetProtoSendUnsequenced
{
    public ENetProtoCmdHeader header;
    public uint unseqGroup;
    public uint dataLength;
};

class ENetProtoSendFragment
{
    public ENetProtoCmdHeader header;
    public uint startSeqNum;
    public uint dataLength;
    public uint fragmentCount;
    public uint fragmentNum;
    public uint totalLength;
    public uint fragmentOffset;
};

class ENetProto
{
    public ENetProtoCmdHeader header;
    public ENetProtoAck ack;
    public ENetProtoConnect connect;
    public ENetProtoVerifyConnect verifyConnect;
    public ENetProtoDisconnect disconnect;
    public ENetProtoPing ping;
    public ENetProtoSendReliable sendReliable;
    public ENetProtoSendUnReliable sendUnReliable;
    public ENetProtoSendUnsequenced sendUnseq;
    public ENetProtoSendFragment sendFragment;
    public ENetProtoBandwidthLimit bandwidthLimit;
    public ENetProtoThrottleConfigure throttleConfigure;
};
