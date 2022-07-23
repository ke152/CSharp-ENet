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
        cmdSizeList.Add(Convert.ToUInt32(Marshal.SizeOf<ENetProtoAck>()));
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
    public int cmdFlag = 0;
    public uint channelID;
    public uint reliableSeqNum;
};

struct ENetProtoAck
{
    public ENetProtoCmdHeader header = new();
    public uint receivedReliableSeqNum = 0;
    public uint receivedSentTime = 0;

    public ENetProtoAck()
    {
        
    }
};

struct ENetProtoConnect
{
    public ENetProtoCmdHeader header = new();
    public uint outPeerID = 0;
    public uint inSessionID = 0;
    public uint outSessionID = 0;
    public uint mtu = 0;
    public uint windowSize = 0;
    public uint channelCount = 0;
    public uint inBandwidth = 0;
    public uint outBandwidth = 0;
    public uint packetThrottleInterval = 0;
    public uint packetThrottleAcceleration = 0;
    public uint packetThrottleDeceleration = 0;
    public uint connectID = 0;
    public uint data = 0;

    public ENetProtoConnect()
    {

    }
};

struct ENetProtoVerifyConnect
{
    public ENetProtoCmdHeader header = new();
    public uint outPeerID = 0;
    public uint inSessionID = 0;
    public uint outSessionID = 0;
    public uint mtu = 0;
    public uint windowSize = 0;
    public uint channelCount = 0;
    public uint inBandwidth = 0;
    public uint outBandwidth = 0;
    public uint packetThrottleInterval = 0;
    public uint packetThrottleAcceleration = 0;
    public uint packetThrottleDeceleration = 0;
    public uint connectID = 0;

    public ENetProtoVerifyConnect()
    {

    }
};

struct ENetProtoBandwidthLimit
{
    public ENetProtoCmdHeader header = new();
    public uint inBandwidth = 0;
    public uint outBandwidth = 0;

    public ENetProtoBandwidthLimit()
    {

    }
};

struct ENetProtoThrottleConfigure
{
    public ENetProtoCmdHeader header = new();
    public uint packetThrottleInterval = 0;
    public uint packetThrottleAcceleration = 0;
    public uint packetThrottleDeceleration = 0;

    public ENetProtoThrottleConfigure()
    {

    }
};

struct ENetProtoDisconnect
{
    public ENetProtoCmdHeader header = new();
    public uint data = 0;

    public ENetProtoDisconnect()
    {

    }
};

struct ENetProtoPing
{
    public ENetProtoCmdHeader header = new();

    public ENetProtoPing()
    {

    }
};

struct ENetProtoSendReliable
{
    public ENetProtoCmdHeader header = new();
    public uint dataLength = 0;

    public ENetProtoSendReliable()
    {

    }
};

struct ENetProtoSendUnReliable
{
    public ENetProtoCmdHeader header = new();
    public uint unreliableSeqNum = 0;
    public uint dataLength = 0;

    public ENetProtoSendUnReliable()
    {

    }
};

struct ENetProtoSendUnsequenced
{
    public ENetProtoCmdHeader header = new();
    public uint unseqGroup = 0;
    public uint dataLength = 0;

    public ENetProtoSendUnsequenced()
    {

    }
};

struct ENetProtoSendFragment
{
    public ENetProtoCmdHeader header = new();
    public uint startSeqNum = 0;
    public uint dataLength = 0;
    public uint fragmentCount = 0;
    public uint fragmentNum = 0;
    public uint totalLength = 0;
    public uint fragmentOffset = 0;

    public ENetProtoSendFragment()
    {

    }
};

class ENetProto
{
    public ENetProtoCmdHeader header = new();
    public ENetProtoAck ack = new();
    public ENetProtoConnect connect = new();
    public ENetProtoVerifyConnect verifyConnect = new();
    public ENetProtoDisconnect disconnect = new();
    public ENetProtoPing ping = new();
    public ENetProtoSendReliable sendReliable = new();
    public ENetProtoSendUnReliable sendUnReliable = new();
    public ENetProtoSendUnsequenced sendUnseq = new();
    public ENetProtoSendFragment sendFragment = new();
    public ENetProtoBandwidthLimit bandwidthLimit = new();
    public ENetProtoThrottleConfigure throttleConfigure = new();
};
