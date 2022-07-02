
using System.Net;

namespace ENet;

class ENetHost : Singleton<ENetHost>
{
    public ENetSocket? socket;
    public IPEndPoint? address;                     /**< Internet address of the host */
    public uint incomingBandwidth;           /**< downstream bandwidth of the host */
    public uint outgoingBandwidth;           /**< upstream bandwidth of the host */
    public uint bandwidthThrottleEpoch;
    public uint mtu;
    public uint randomSeed;
    public int recalculateBandwidthLimits;
    public ENetPeer[]? peers;                       /**< array of peers allocated for this host */
    public uint peerCount;                   /**< number of peers allocated for this host */
    public uint channelLimit;                /**< maximum number of channels allowed for connected peers */
    public ulong serviceTime;
    public LinkedList<ENetPeer> dispatchQueue = new LinkedList<ENetPeer>();
    public int continueSending;
    public uint packetSize;
    public uint headerFlags;
    public ENetProto[] commands = new ENetProto[ENetDef.ProtoMaxPacketCmds];
    public uint commandCount;
    public byte[] buffers = new byte[ENetDef.BufferMax];
    public uint bufferCount;
    public byte[,] packetData = new byte[2, ENetDef.ProtoMaxMTU];
    public IPEndPoint? receivedAddress;
    public byte[]? receivedData;
    public uint receivedDataLength;
    public uint totalSentData;               /**< total data sent, user should reset to 0 as needed to prevent overflow */
    public uint totalSentPackets;            /**< total UDP packets sent, user should reset to 0 as needed to prevent overflow */
    public uint totalReceivedData;           /**< total data received, user should reset to 0 as needed to prevent overflow */
    public uint totalReceivedPackets;        /**< total UDP packets received, user should reset to 0 as needed to prevent overflow */
    public uint connectedPeers;
    public uint bandwidthLimitedPeers;
    public uint duplicatePeers;              /**< optional number of allowed peers from duplicate IPs, defaults to Proto_MAXIMUM_PEERID */
    public uint maximumPacketSize;           /**< the maximum allowable packet size that may be sent or received on a peer */
    public uint maximumWaitingData;          /**< the maximum aggregate amount of buffer space a peer may use waiting for packets to be delivered */

    public ENetHost()
    {

    }
    public void Create(IPEndPoint address, uint peerCount, uint channelLimit, uint incomingBandwidth, uint outgoingBandwidth)
    {//TODO:转化到单例得构造函数中
        if (peerCount > (int)ENetDef.ProtoMaxPeerID)
            return;

        this.peers = new ENetPeer[peerCount];

        this.socket = new ENetSocket();
        this.socket.Bind(address);

        this.socket.SetOption(ENetSocketOptType.NonBlock, 1);
        this.socket.SetOption(ENetSocketOptType.Broadcast, 1);
        this.socket.SetOption(ENetSocketOptType.RcvBuf, (int)ENetDef.HostRecvBufferSize);
        this.socket.SetOption(ENetSocketOptType.SendBuf, (int)ENetDef.HostSendBufferSize);

        this.address = address;

        if (channelLimit != 0 || channelLimit > (int)ENetDef.ProtoMaxChannelCount)
            channelLimit = (int)ENetDef.ProtoMaxChannelCount;
        else
        if (channelLimit < (int)ENetDef.ProtoMinChannelCount)
            channelLimit = (int)ENetDef.ProtoMinChannelCount;

        this.randomSeed = (uint)(new Random()).Next(0);
        this.randomSeed += Utils.RandomSeed();
        this.randomSeed = (this.randomSeed << 16) | (this.randomSeed >> 16);
        this.channelLimit = channelLimit;
        this.incomingBandwidth = incomingBandwidth;
        this.outgoingBandwidth = outgoingBandwidth;
        this.bandwidthThrottleEpoch = 0;
        this.recalculateBandwidthLimits = 0;
        this.mtu = (int)ENetDef.HostDefaultMTU;
        this.peerCount = peerCount;
        this.commandCount = 0;
        this.bufferCount = 0;
        this.receivedAddress = new IPEndPoint(IPAddress.Any, 0);
        this.receivedData = null;
        this.receivedDataLength = 0;

        this.totalSentData = 0;
        this.totalSentPackets = 0;
        this.totalReceivedData = 0;
        this.totalReceivedPackets = 0;

        this.connectedPeers = 0;
        this.bandwidthLimitedPeers = 0;
        this.duplicatePeers = (int)ENetDef.ProtoMaxPeerID;
        this.maximumPacketSize = (int)ENetDef.HostDefaultMaxPacketSize;
        this.maximumWaitingData = (int)ENetDef.HostDefaultMaxWaintingData;

        this.dispatchQueue.Clear();


        if (this.peers == null)
        {
            return;
        }

        for (uint i = 0; i < this.peers.Length; i++)
        {
            var currentPeer = this.peers[i];
            currentPeer.incomingPeerID = i;
            currentPeer.outgoingSessionID = currentPeer.incomingSessionID = 0xFF;
            currentPeer.data = null;

            currentPeer.acknowledgements.Clear();
            currentPeer.sentReliableCommands.Clear();
            currentPeer.sentUnreliableCommands.Clear();
            currentPeer.outgoingCommands.Clear();
            currentPeer.dispatchedCommands.Clear();

            currentPeer.Reset();
        }
    }

    private void ProtoSendOutCmds(object? p, int v)
    {
        throw new NotImplementedException();
    }
    public void Flush()
    {
        this.serviceTime = Utils.TimeGet();

        ProtoSendOutCmds(null, 0);
    }


    public void Destroy()
    {
        this.socket = null;

        if (this.peers == null)
        {
            return;
        }

        foreach (var peer in this.peers)
        {
            peer.Reset();
        }

        this.peers = null;
    }

    public uint Random()
    {
        uint n = (this.randomSeed += 0x6D2B79F5U);
        n = (n ^ (n >> 15)) * (n | 1U);
        n ^= n + (n ^ (n >> 7)) * (n | 61U);
        return n ^ (n >> 14);
    }


    public ENetPeer? Connect(IPEndPoint address, uint channelCount, uint data)
    {
        ENetPeer? currentPeer = null;

        if (channelCount < (int)ENetDef.ProtoMinChannelCount)
            channelCount = (int)ENetDef.ProtoMinChannelCount;
        else
        if (channelCount > (int)ENetDef.ProtoMaxChannelCount)
            channelCount = (int)ENetDef.ProtoMaxChannelCount;

        if (this.peers == null)
        {
            return null;
        }

        foreach (var tmpPeer in this.peers)
        {
            if (tmpPeer.state == ENetPeerState.Disconnected)
            {
                currentPeer = tmpPeer;
                break;
            }
        }

        if (currentPeer == null)
            return null;

        currentPeer.channels = new ENetChannel[channelCount];

        currentPeer.state = ENetPeerState.Connecting;
        currentPeer.address = address;
        currentPeer.connectID = Random();

        if (this.outgoingBandwidth == 0)
            currentPeer.windowSize = (int)ENetDef.ProtoMaxWindowSize;
        else
            currentPeer.windowSize = (this.outgoingBandwidth /
                                          (uint)ENetDef.PeerWindowSizeScale) *
                                            (int)ENetDef.ProtoMinWindowSize;

        if (currentPeer.windowSize < (int)ENetDef.ProtoMinWindowSize)
            currentPeer.windowSize = (int)ENetDef.ProtoMinWindowSize;
        else
        if (currentPeer.windowSize > (int)ENetDef.ProtoMaxWindowSize)
            currentPeer.windowSize = (int)ENetDef.ProtoMaxWindowSize;

        if (currentPeer.channels != null)
        {
            for (int i = 0; i < currentPeer.channels.Length; i++)
            {
                var channel = currentPeer.channels[i];

                channel.outgoingReliableSequenceNumber = 0;
                channel.outgoingUnreliableSequenceNumber = 0;
                channel.incomingReliableSequenceNumber = 0;
                channel.incomingUnreliableSequenceNumber = 0;

                channel.incomingReliableCommands.Clear();
                channel.incomingUnreliableCommands.Clear();

                channel.usedReliableWindows = 0;
            }
        }

        ENetProto command = new ENetProto();
        command.header.command = (int)ENetProtoCmdType.Connect | (int)ENetProtoFlag.CmdFlagAck;
        command.header.channelID = 0xFF;
        command.connect.outgoingPeerID = (uint)IPAddress.HostToNetworkOrder(currentPeer.incomingPeerID);
        if (currentPeer != null)
        {
            command.connect.incomingSessionID = currentPeer.incomingSessionID;
            command.connect.outgoingSessionID = currentPeer.outgoingSessionID;
            command.connect.mtu = (uint)IPAddress.HostToNetworkOrder(currentPeer.mtu);
            command.connect.windowSize = (uint)IPAddress.HostToNetworkOrder(currentPeer.windowSize);
            command.connect.packetThrottleInterval = (uint)IPAddress.HostToNetworkOrder(currentPeer.packetThrottleInterval);
            command.connect.packetThrottleAcceleration = (uint)IPAddress.HostToNetworkOrder(currentPeer.packetThrottleAcceleration);
            command.connect.packetThrottleDeceleration = (uint)IPAddress.HostToNetworkOrder(currentPeer.packetThrottleDeceleration);
            command.connect.connectID = currentPeer.connectID;
        }
        command.connect.channelCount = (uint)IPAddress.HostToNetworkOrder(channelCount);
        command.connect.incomingBandwidth = (uint)IPAddress.HostToNetworkOrder(this.incomingBandwidth);
        command.connect.outgoingBandwidth = (uint)IPAddress.HostToNetworkOrder(this.outgoingBandwidth);
        command.connect.data = (uint)IPAddress.HostToNetworkOrder(data);

        currentPeer?.QueueOutCmd(command, null, 0, 0);

        return currentPeer;
    }


    public void Broadcast(ENetHost host, uint channelID, ENetPacket packet)
    {
        if (this.peers == null)
        {
            return;
        }

        foreach (var currentPeer in this.peers)
        {
            if (currentPeer.state != ENetPeerState.Connected)
                continue;

            currentPeer.Send(channelID, packet);
        }
    }


    public void ChannelLimit(ENetHost host, uint channelLimit)
    {
        if (channelLimit > 0 || channelLimit > (int)ENetDef.ProtoMaxChannelCount)
            channelLimit = (int)ENetDef.ProtoMaxChannelCount;
        else
        if (channelLimit < (int)ENetDef.ProtoMinChannelCount)
            channelLimit = (int)ENetDef.ProtoMinChannelCount;

        this.channelLimit = channelLimit;
    }


    public void BandwidthLimit(ENetHost host, uint incomingBandwidth, uint outgoingBandwidth)
    {
        this.incomingBandwidth = incomingBandwidth;
        this.outgoingBandwidth = outgoingBandwidth;
        this.recalculateBandwidthLimits = 1;
    }

    public void BandwidthThrottle(ENetHost host)
    {
        uint timeCurrent = (uint)Utils.TimeGet();
        uint elapsedTime = timeCurrent - this.bandwidthThrottleEpoch,
           peersRemaining = (uint)this.connectedPeers,
           dataTotal = uint.MaxValue,
           bandwidth = uint.MaxValue,
           throttle = 0,
           bandwidthLimit = 0;
        int needsAdjustment = this.bandwidthLimitedPeers > 0 ? 1 : 0;

        if (elapsedTime < (uint)ENetDef.HostBandwidthThrottleInterval)
            return;

        this.bandwidthThrottleEpoch = timeCurrent;

        if (peersRemaining == 0)
            return;

        if (this.outgoingBandwidth != 0)
        {
            dataTotal = 0;
            bandwidth = (this.outgoingBandwidth * elapsedTime) / 1000;

            if (this.peers != null)
            {
                foreach (var peer in this.peers)
                {
                    if (peer.state != ENetPeerState.Connected && peer.state != ENetPeerState.DisconnectLater)
                        continue;

                    dataTotal += peer.outgoingDataTotal;
                }
            }
        }

        while (peersRemaining > 0 && needsAdjustment != 0)
        {
            needsAdjustment = 0;

            if (dataTotal <= bandwidth)
                throttle = (uint)ENetDef.PeerPacketThrottleScale;
            else
                throttle = (bandwidth * (uint)ENetDef.PeerPacketThrottleScale) / dataTotal;

            if (this.peers != null)
            {
                foreach (var peer in this.peers)
                {
                    uint peerBandwidth;

                    if ((peer.state != ENetPeerState.Connected && peer.state != ENetPeerState.DisconnectLater) ||
                        peer.incomingBandwidth == 0 ||
                        peer.outgoingBandwidthThrottleEpoch == timeCurrent)
                        continue;

                    peerBandwidth = (peer.incomingBandwidth * elapsedTime) / 1000;
                    if ((throttle * peer.outgoingDataTotal) / (uint)ENetDef.PeerPacketThrottleScale <= peerBandwidth)
                        continue;

                    peer.packetThrottleLimit = (peerBandwidth *
                                                    (uint)ENetDef.PeerPacketThrottleScale) / peer.outgoingDataTotal;

                    if (peer.packetThrottleLimit == 0)
                        peer.packetThrottleLimit = 1;

                    if (peer.packetThrottle > peer.packetThrottleLimit)
                        peer.packetThrottle = peer.packetThrottleLimit;

                    peer.outgoingBandwidthThrottleEpoch = timeCurrent;

                    peer.incomingDataTotal = 0;
                    peer.outgoingDataTotal = 0;

                    needsAdjustment = 1;
                    --peersRemaining;
                    bandwidth -= peerBandwidth;
                    dataTotal -= peerBandwidth;
                }
            }
        }

        if (peersRemaining > 0)
        {
            if (dataTotal <= bandwidth)
                throttle = (uint)ENetDef.PeerPacketThrottleScale;
            else
                throttle = (bandwidth * (uint)ENetDef.PeerPacketThrottleScale) / dataTotal;

            if (this.peers != null)
            {
                foreach (var peer in this.peers)
                {
                    if ((peer.state != ENetPeerState.Connected && peer.state != ENetPeerState.DisconnectLater) ||
                        peer.outgoingBandwidthThrottleEpoch == timeCurrent)
                        continue;

                    peer.packetThrottleLimit = throttle;

                    if (peer.packetThrottle > peer.packetThrottleLimit)
                        peer.packetThrottle = peer.packetThrottleLimit;

                    peer.incomingDataTotal = 0;
                    peer.outgoingDataTotal = 0;
                }
            }
        }

        if (this.recalculateBandwidthLimits != 0)
        {
            this.recalculateBandwidthLimits = 0;

            peersRemaining = (uint)this.connectedPeers;
            bandwidth = this.incomingBandwidth;
            needsAdjustment = 1;

            if (bandwidth == 0)
                bandwidthLimit = 0;
            else
                while (peersRemaining > 0 && needsAdjustment != 0)
                {
                    needsAdjustment = 0;
                    bandwidthLimit = bandwidth / peersRemaining;

                    foreach (var peer in this.peers)
                    {
                        if ((peer.state != ENetPeerState.Connected && peer.state != ENetPeerState.DisconnectLater) ||
                            peer.incomingBandwidthThrottleEpoch == timeCurrent)
                            continue;

                        if (peer.outgoingBandwidth > 0 &&
                            peer.outgoingBandwidth >= bandwidthLimit)
                            continue;

                        peer.incomingBandwidthThrottleEpoch = timeCurrent;

                        needsAdjustment = 1;
                        --peersRemaining;
                        bandwidth -= peer.outgoingBandwidth;
                    }
                }

            foreach (var peer in this.peers)
            {
                if (peer.state != ENetPeerState.Connected && peer.state != ENetPeerState.DisconnectLater)
                    continue;

                ENetProto command = new ENetProto();
                command.header.command = (int)ENetProtoCmdType.BandwidthLimit | (int)ENetProtoFlag.CmdFlagAck;
                command.header.channelID = 0xFF;
                command.bandwidthLimit.outgoingBandwidth = (uint)IPAddress.HostToNetworkOrder(this.outgoingBandwidth);

                if (peer.incomingBandwidthThrottleEpoch == timeCurrent)
                    command.bandwidthLimit.incomingBandwidth = (uint)IPAddress.HostToNetworkOrder(peer.outgoingBandwidth);
                else
                    command.bandwidthLimit.incomingBandwidth = (uint)IPAddress.HostToNetworkOrder(bandwidthLimit);

                peer.QueueOutCmd(command, null, 0, 0);
            }
        }
    }



}
