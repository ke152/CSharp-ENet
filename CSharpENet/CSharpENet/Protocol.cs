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
struct ENetProtoHeader
{
    public ushort peerID;
    public ushort sentTime;
};

struct ENetProtoCmdHeader
{
    public ENetProtoCmdType command;
    public byte channelID;
    public ushort ReliableSequenceNumber;
};

struct ENetProtoAck
{
    public ENetProtoCmdHeader header;
    public ushort receivedReliableSequenceNumber;
    public ushort receivedSentTime;
};

struct ENetProtoConnect
{
    public ENetProtoCmdHeader header;
    public ushort outgoingPeerID;
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
    public ushort outgoingPeerID;
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
    public ushort dataLength;
};

struct ENetProtoSendUnReliable
{
    public ENetProtoCmdHeader header;
    public ushort unReliableSequenceNumber;
    public ushort dataLength;
};

struct ENetProtoSendUnsequenced
{
    public ENetProtoCmdHeader header;
    public ushort unsequencedGroup;
    public ushort dataLength;
};

struct ENetProtoSendFragment
{
    public ENetProtoCmdHeader header;
    public ushort startSequenceNumber;
    public ushort dataLength;
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
