namespace ENet;

enum ENetDef
{
    HostRecvBufferSize = 256 * 1024,
    HostSendBufferSize = 256 * 1024,
    HostBandwidthThrottleInterval = 1000,
    HostDefaultMTU = 1400,
    HostDefaultMaxPacketSize = 32 * 1024 * 1024,
    HostDefaultMaxWaintingData = 32 * 1024 * 1024,

    PeerDefaultRTT = 500,
    PeerDefaultPacketThrottle = 32,
    PeerPacketThrottleScale = 32,
    PeerPacketThrottleCounter = 7,
    PeerPacketThrottleAcceleration = 2,
    PeerPacketThrottleDeceleration = 2,
    PeerPacketThrottleInterval = 5000,
    PeerPacketLossScale = (1 << 16),
    PeerPacketLossInterval = 10000,
    PeerWindowSizeScale = 64 * 1024,
    PeerTimeoutLimit = 32,
    PeerTimeoutMin = 5000,
    PeerTimeoutMax = 30000,
    PeerPingInterval = 500,
    PeerUnseqWindows = 64,
    PeerUnseqWindowSize = 1024,
    PeerFreeUnseqWindows = 32,
    PeerReliableWindows = 16,
    PeerReliableWindowSize = 0x1000,
    PeerFreeReliableWindows = 8
};
static class Def
{

}
