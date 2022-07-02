
namespace ENet;

class ENetHost : Singleton<ENetHost>
{
    // public ENetSocket socket;
    // public ENetAddress address;                     /**< Internet address of the host */
    public uint incomingBandwidth;           /**< downstream bandwidth of the host */
    public uint outgoingBandwidth;           /**< upstream bandwidth of the host */
    public uint bandwidthThrottleEpoch;
    public uint mtu;
    public uint randomSeed;
    public int recalculateBandwidthLimits;
    public List<ENetPeer> peers = new List<ENetPeer>();                       /**< array of peers allocated for this host */
    public uint peerCount;                   /**< number of peers allocated for this host */
    public uint channelLimit;                /**< maximum number of channels allowed for connected peers */
    public uint serviceTime;
    public LinkedList<ENetPeer> dispatchQueue = new LinkedList<ENetPeer>();
    public int continueSending;
    public uint packetSize;
    public uint headerFlags;
    public ENetProto[] commands = new ENetProto[ENetDef.ProtoMaxPacketCmds];
    public uint commandCount;
    public byte[] buffers = new byte[ENetDef.BufferMax];
    public uint bufferCount;
    public byte[,] packetData = new byte[2,ENetDef.ProtoMaxMTU];
    //public ENetAddress receivedAddress;
    public byte[]? receivedData;
    public uint receivedDataLength;
    public uint totalSentData;               /**< total data sent, user should reset to 0 as needed to prevent overflow */
    public uint totalSentPackets;            /**< total UDP packets sent, user should reset to 0 as needed to prevent overflow */
    public uint totalReceivedData;           /**< total data received, user should reset to 0 as needed to prevent overflow */
    public uint totalReceivedPackets;        /**< total UDP packets received, user should reset to 0 as needed to prevent overflow */
    public uint connectedPeers;
    public uint bandwidthLimitedPeers;
    public uint duplicatePeers;              /**< optional number of allowed peers from duplicate IPs, defaults to ENET_PROTOCOL_MAXIMUM_PEER_ID */
    public uint maximumPacketSize;           /**< the maximum allowable packet size that may be sent or received on a peer */
    public uint maximumWaitingData;          /**< the maximum aggregate amount of buffer space a peer may use waiting for packets to be delivered */
}
