
namespace ENet;

class ENetHost
{
    // public ENetSocket socket;
    // public ENetAddress address;                     /**< Internet address of the host */
    // public enet_uint32 incomingBandwidth;           /**< downstream bandwidth of the host */
    // public enet_uint32 outgoingBandwidth;           /**< upstream bandwidth of the host */
    // public enet_uint32 bandwidthThrottleEpoch;
    // public enet_uint32 mtu;
    // public enet_uint32 randomSeed;
    // public int recalculateBandwidthLimits;
    // public ENetPeer* peers;                       /**< array of peers allocated for this host */
    // public size_t peerCount;                   /**< number of peers allocated for this host */
    // public size_t channelLimit;                /**< maximum number of channels allowed for connected peers */
    // public enet_uint32 serviceTime;
    public LinkedList<ENetPeer> dispatchQueue = new LinkedList<ENetPeer>();
    // public int continueSending;
    // public size_t packetSize;
    // public enet_uint16 headerFlags;
    // public ENetProtocol commands[ENET_PROTOCOL_MAXIMUM_PACKET_COMMANDS];
    // public size_t commandCount;
    // public ENetBuffer buffers[ENET_BUFFER_MAXIMUM];
    // public size_t bufferCount;
    // public ENetChecksumCallback checksum;                    /**< callback the user can set to enable packet checksums for this host */
    // public ENetCompressor compressor;
    // public enet_uint8 packetData[2][ENET_PROTOCOL_MAXIMUM_MTU];
    //public ENetAddress receivedAddress;
    // public enet_uint8* receivedData;
    // public size_t receivedDataLength;
    // public enet_uint32 totalSentData;               /**< total data sent, user should reset to 0 as needed to prevent overflow */
    // public enet_uint32 totalSentPackets;            /**< total UDP packets sent, user should reset to 0 as needed to prevent overflow */
    // public enet_uint32 totalReceivedData;           /**< total data received, user should reset to 0 as needed to prevent overflow */
    // public enet_uint32 totalReceivedPackets;        /**< total UDP packets received, user should reset to 0 as needed to prevent overflow */
    // public ENetInterceptCallback intercept;                  /**< callback the user can set to intercept received raw UDP packets */
    // public size_t connectedPeers;
    // public size_t bandwidthLimitedPeers;
    // public size_t duplicatePeers;              /**< optional number of allowed peers from duplicate IPs, defaults to ENET_PROTOCOL_MAXIMUM_PEER_ID */
    // public size_t maximumPacketSize;           /**< the maximum allowable packet size that may be sent or received on a peer */
    // public size_t maximumWaitingData;          /**< the maximum aggregate amount of buffer space a peer may use waiting for packets to be delivered */
}
