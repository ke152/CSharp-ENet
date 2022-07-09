
using System.Net;
using System.Runtime.InteropServices;

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
    public long serviceTime;
    public LinkedList<ENetPeer> dispatchQueue = new LinkedList<ENetPeer>();
    public int continueSending;
    public uint packetSize;
    public uint headerFlags;
    public ENetProto[] commands = new ENetProto[ENetDef.ProtoMaxPacketCmds];
    public uint commandCount;
    public byte[] buffers = new byte[ENetDef.BufferMax];
    public uint bufferCount;
    public byte[][] packetData;
    public IPEndPoint? receivedAddress;
    public byte[]? receivedData;
    public int receivedDataLength;
    public uint totalSentData;               /**< total data sent, user should reset to 0 as needed to prevent overflow */
    public uint totalSentPackets;            /**< total UDP packets sent, user should reset to 0 as needed to prevent overflow */
    public int totalReceivedData;           /**< total data received, user should reset to 0 as needed to prevent overflow */
    public uint totalReceivedPackets;        /**< total UDP packets received, user should reset to 0 as needed to prevent overflow */
    public uint connectedPeers;
    public uint bandwidthLimitedPeers;
    public uint duplicatePeers;              /**< optional number of allowed peers from duplicate IPs, defaults to Proto_MAXIMUMPEERID */
    public uint maximumPacketSize;           /**< the maximum allowable packet size that may be sent or received on a peer */
    public uint maximumWaitingData;          /**< the maximum aggregate amount of buffer space a peer may use waiting for packets to be delivered */

    public ENetHost()
    {
        this.packetData = new byte[2][];
        this.packetData[0] = new byte[ENetDef.ProtoMaxMTU];
        this.packetData[1] = new byte[ENetDef.ProtoMaxMTU];
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

        currentPeer?.QueueOutgoingCommand(command, null, 0, 0);

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
           bandwidth = uint.MaxValue,
           throttle = 0,
           bandwidthLimit = 0;
        int dataTotal = int.MaxValue;
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
                throttle = (bandwidth * (uint)ENetDef.PeerPacketThrottleScale) / (uint)dataTotal;

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
                                                    ENetDef.PeerPacketThrottleScale) / (uint)peer.outgoingDataTotal;

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
                throttle = (bandwidth * ENetDef.PeerPacketThrottleScale) / (uint)dataTotal;

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

                peer.QueueOutgoingCommand(command, null, 0, 0);
            }
        }
    }


    #region proto

    public void ProtoChangeState(ENetPeer peer, ENetPeerState state)
    {
        if (state == ENetPeerState.Connected || state == ENetPeerState.DisconnectLater)
            peer.OnConnect();
        else
            peer.OnDisconnect();

        peer.state = state;
    }

    public void ProtoDispatchState(ENetPeer peer, ENetPeerState state)
    {
        ProtoChangeState(peer, state);

        if (!(peer.needDispatch))
        {
            this.dispatchQueue.AddLast(peer);
            peer.needDispatch = true;
        }
    }


    public int ProtoDispatchIncomingCommands(ref ENetEvent @event)
    {
        while (this.dispatchQueue?.Count != 0)
        {
            ENetPeer? peer = this.dispatchQueue?.First?.Value;
            if (peer == null)
                continue;

            this.dispatchQueue?.RemoveFirst();

            peer.needDispatch = false;

            switch (peer.state)
            {
                case ENetPeerState.ConnectionPending:
                case ENetPeerState.ConnectionSucceed:
                    ProtoChangeState(peer, ENetPeerState.Connected);
                    @event.type = ENetEventType.Connect;
                    @event.peer = peer;
                    @event.data = peer.eventData;
                    return 1;
                case ENetPeerState.Zombie:
                    this.recalculateBandwidthLimits = 1;

                    @event.type = ENetEventType.Disconnect;
                    @event.peer = peer;
                    @event.data = peer.@eventData;

                    peer.Reset();
                    return 1;

                case ENetPeerState.Connected:
                    if (peer.dispatchedCommands.Count == 0)
                        continue;

                    @event.packet = peer.Receive(ref @event.channelID);
                    if (@event.packet == null)
                        continue;


                    @event.type = ENetEventType.Recv;
                    @event.peer = peer;

                    if (peer.dispatchedCommands.Count != 0)
                    {
                        peer.needDispatch = true;

                        this.dispatchQueue?.AddLast(peer);
                    }

                    return 1;

                default:
                    break;
            }
        }

        return 0;
    }

    public void ProtoNotifyConnect(ENetPeer peer, ENetEvent @event)
    {
        this.recalculateBandwidthLimits = 1;

        if (@event != null)
        {
            ProtoChangeState(peer, ENetPeerState.Connected);

            @event.type = ENetEventType.Connect;
            @event.peer = peer;
            @event.data = peer.@eventData;
        }
        else
            ProtoDispatchState(peer, peer.state == ENetPeerState.Connecting ? ENetPeerState.ConnectionSucceed : ENetPeerState.ConnectionPending);
    }

    public void ProtoNotifyDisconnect(ENetPeer peer, ENetEvent @event)
    {
        if (peer.state >= ENetPeerState.ConnectionPending)
            this.recalculateBandwidthLimits = 1;

        if (peer.state != ENetPeerState.Connecting && peer.state < ENetPeerState.ConnectionSucceed)
            peer.Reset();
        else
        if (@event != null)
        {
            @event.type = ENetEventType.Disconnect;
            @event.peer = peer;
            @event.data = 0;

            peer.Reset();
        }
        else
        {
            peer.@eventData = 0;

            ProtoDispatchState(peer, ENetPeerState.Zombie);
        }
    }


    public void ProtoRemoveSentUnreliableCommands(ENetPeer peer)
    {
        if (peer.sentUnreliableCommands.Count == 0)
            return;

        peer.sentUnreliableCommands?.Clear();

        if (peer.state == ENetPeerState.DisconnectLater &&
            peer.outgoingCommands.Count == 0 &&
            peer.sentReliableCommands.Count == 0)
            peer.Disconnect(peer.@eventData);
    }

    public ENetProtoCmdType ProtoRemoveSentReliableCommand(ENetPeer peer, uint reliableSequenceNumber, uint channelID)
    {
        ENetOutCmd? outgoingCommand = null;
        ENetProtoCmdType commandNumber;
        int wasSent = 1;

        foreach (var currentCommand in peer.sentReliableCommands)
        {
            outgoingCommand = currentCommand;

            if (outgoingCommand?.reliableSequenceNumber == reliableSequenceNumber &&
                outgoingCommand?.commandHeader.header.channelID == channelID)
            {
                peer.sentReliableCommands.Remove(currentCommand);
                break;
            }
        }

        if (outgoingCommand == null)
        {
            foreach (var currentCommand in peer.outgoingCommands)
            {
                outgoingCommand = currentCommand;

                if ((outgoingCommand?.commandHeader.header.command & (int)ENetProtoFlag.CmdFlagAck) != 0)
                    continue;

                if (outgoingCommand?.sendAttempts < 1) return (int)ENetProtoCmdType.None;

                if (outgoingCommand?.reliableSequenceNumber == reliableSequenceNumber &&
                    outgoingCommand?.commandHeader.header.channelID == channelID)
                {
                    peer.outgoingCommands.Remove(currentCommand);
                    break;
                }
            }

            if (outgoingCommand == null)
                return ENetProtoCmdType.None;

            wasSent = 0;
        }

        if (outgoingCommand == null)
            return ENetProtoCmdType.None;

        if (channelID < peer.channelCount && peer.channels != null)
        {
            ENetChannel channel = peer.channels[channelID];
            uint reliableWindow = reliableSequenceNumber / (uint)ENetDef.PeerReliableWindowSize;
            if (channel.reliableWindows[reliableWindow] > 0)
            {
                --channel.reliableWindows[reliableWindow];
                if (channel.reliableWindows[reliableWindow] != 0)
                    channel.usedReliableWindows &= ~(1 << (int)reliableWindow);
            }
        }

        commandNumber = (ENetProtoCmdType)(outgoingCommand.commandHeader.header.command & (int)ENetProtoCmdType.Mask);

        if (outgoingCommand.packet != null)
        {
            if (wasSent != 0)
                peer.reliableDataInTransit -= outgoingCommand.fragmentLength;

            outgoingCommand.packet = null;
        }

        if (peer.sentReliableCommands.Count == 0)
            return commandNumber;

        outgoingCommand = peer.sentReliableCommands.First?.Value;

        if (outgoingCommand != null)
        {
            peer.nextTimeout = outgoingCommand.sentTime + outgoingCommand.roundTripTimeout;
        }

        return commandNumber;
    }


    public int ProtoReceiveIncomingCommands(ENetEvent @event)
    {
        int packets;

        for (packets = 0; packets < 256; ++packets)
        {
            int receivedLength;

            if (this.socket == null)
                return -1;

            receivedLength = this.socket.Receive(this.packetData[0], ref this.receivedAddress);

            if (receivedLength < 0)
                return -1;

            if (receivedLength == 0)
                return 0;

            this.receivedData = this.packetData[0];
            this.receivedDataLength = receivedLength;

            this.totalReceivedData += receivedLength;
            this.totalReceivedPackets++;

            switch (ProtoHandleIncomingCommands(@event))
            {
                case 1:
                    return 1;

                case -1:
                    return -1;

                default:
                    break;
            }
        }

        return 0;
    }


    #region Proto Handle

    public int ProtoHandleIncomingCommands(ENetEvent @event)
    {
        ENetProtoHeader header;
        ENetProtoCmdHeader? commandHeader;
        ENetPeer? peer = null;
        int currentDataIdx = 0;
        int headerSize = Marshal.SizeOf<ENetProtoHeader>();
        uint peerID, flags;
        uint sessionID;

        if (this.receivedData == null)
        {
            return -1;
        }

        if (headerSize > this.receivedDataLength)
        {
            return 0;
        }

        byte[] headerBytes = Utils.SubBytes(this.receivedData, 0, headerSize);

        header = Utils.DeSerialize<ENetProtoHeader>(headerBytes);

        peerID = Utils.NetToHostOrder(header.peerID);
        sessionID = (peerID & (int)ENetProtoFlag.HeaderSessionMask) >> (int)ENetProtoFlag.HeaderSessionShift;
        flags = peerID & (int)ENetProtoFlag.HeaderFalgMASK;
        peerID &= ~((uint)ENetProtoFlag.HeaderFalgMASK | (uint)ENetProtoFlag.HeaderSessionMask);

        //TODO：这里最后得0，是原先(uint)&((ENetProtoHeader*)0)->sentTime;，不是很理解，先用0代替
        headerSize = (flags & (int)ENetProtoFlag.HeaderFalgSentTime) != 0 ? (int)headerSize : 0;

        if (peerID == (int)ENetDef.ProtoMaxPeerID)
            peer = null;
        else
        if (peerID >= this.peerCount)
            return 0;
        else
        {
            if (this.peers != null && peerID < this.peers.Length)
            {

                peer = this.peers[peerID];
                byte[]? hostIP = peer.address?.Address?.GetAddressBytes();
                bool isBroadcast = false;
                if (hostIP != null)
                {
                    isBroadcast = hostIP[0] != 255 && hostIP[1] != 255 && hostIP[2] != 255 && hostIP[3] != 255;
                }

                if (peer.state == ENetPeerState.Disconnected ||
                    peer.state == ENetPeerState.Zombie ||
                    ((this.receivedAddress?.Address?.GetAddressBytes() != peer.address?.Address?.GetAddressBytes() ||
                      this.receivedAddress?.Port != peer.address?.Port) &&
                      !isBroadcast) ||
                    (peer.outgoingPeerID < (int)ENetDef.ProtoMaxPeerID &&
                     sessionID != peer.incomingSessionID))
                    return 0;
            }
        }


        if (peer != null && peer.address != null && this.receivedAddress != null)
        {
            peer.address.Address = this.receivedAddress.Address;
            peer.address.Port = this.receivedAddress.Port;
            peer.incomingDataTotal += this.receivedDataLength;
        }

        currentDataIdx = headerSize;

        while (currentDataIdx < this.receivedDataLength)
        {
            int commandNumber;
            int commandSize;


            if (currentDataIdx + Marshal.SizeOf<ENetProtoCmdHeader>() > this.receivedDataLength)//TODO:所有sizeof都用这种形式
                break;

            byte[] objBytes = Utils.SubBytes(this.receivedData, currentDataIdx, Marshal.SizeOf<ENetProtoCmdHeader>());
            commandHeader = Utils.DeSerialize<ENetProtoCmdHeader>(objBytes);
            if (commandHeader == null)
            {
                continue;
            }
            commandNumber = commandHeader.command & (int)ENetProtoCmdType.Mask;
            if (commandNumber >= (int)ENetProtoCmdType.Count)
                break;

            commandSize = Convert.ToInt32(ENetProtoCmdSize.CmdSize[commandNumber]);
            if (commandSize == 0 || currentDataIdx + commandSize > this.receivedDataLength)
                break;

            int commandStartIdx = currentDataIdx;
            currentDataIdx += commandSize;

            if (peer == null && commandNumber != (int)ENetProtoCmdType.Connect)
                break;

            commandHeader.reliableSequenceNumber = Utils.NetToHostOrder(commandHeader.reliableSequenceNumber);

            switch (commandNumber)
            {
                case (int)ENetProtoCmdType.Ack:
                    if (peer == null || ProtoHandleAcknowledge(@event, peer, commandHeader, commandStartIdx, commandSize) != 0)
                        goto commandError;
                    break;

                case (int)ENetProtoCmdType.Connect:
                    if (peer != null)
                        goto commandError;
                    peer = ProtoHandleConnect(commandHeader, commandStartIdx, commandSize);
                    if (peer == null)
                        goto commandError;
                    break;

                case (int)ENetProtoCmdType.VerifyConnect:
                    if (peer == null || ProtoHandleVerifyConnect(@event, peer, commandStartIdx, commandSize) != 0)
                        goto commandError;
                    break;

                case (int)ENetProtoCmdType.Disconnect:
                    if (peer == null || ProtoHandleDisconnect(commandHeader, peer, commandStartIdx, commandSize) != 0)
                        goto commandError;
                    break;

                case (int)ENetProtoCmdType.Ping:
                    if (peer == null || ProtoHandlePing(peer) != 0)
                        goto commandError;
                    break;

                case (int)ENetProtoCmdType.SendReliable:
                    if (peer == null || ProtoHandleSendReliable(commandHeader, peer, commandStartIdx, commandSize, ref currentDataIdx) != 0)
                        goto commandError;
                    break;

                case (int)ENetProtoCmdType.SendFragment:
                    if (peer == null || ProtoHandleSendFragment(commandHeader, peer, commandStartIdx, commandSize, ref currentDataIdx) != 0)
                        goto commandError;
                    break;

                case (int)ENetProtoCmdType.SendUnreliable:
                    if (peer == null || ProtoHandleSendUnreliable(commandHeader, peer, commandStartIdx, commandSize, ref currentDataIdx) != 0)
                        goto commandError;
                    break;

                case (int)ENetProtoCmdType.SendUnreliableFragment:
                    if (peer == null || ProtoHandleSendUnreliableFragment(commandHeader, peer, commandStartIdx, commandSize, ref currentDataIdx) != 0)
                        goto commandError;
                    break;

                case (int)ENetProtoCmdType.SendUnseq:
                    if (peer == null || ProtoHandleSendUnsequenced(commandHeader, peer, commandStartIdx, commandSize, ref currentDataIdx) != 0)
                        goto commandError;
                    break;

                case (int)ENetProtoCmdType.BandwidthLimit:
                    if (peer == null || ProtoHandleBandwidthLimit(commandHeader, peer, commandStartIdx, commandSize) != 0)
                        goto commandError;
                    break;

                case (int)ENetProtoCmdType.ThrottleConfig:
                    if (peer == null || ProtoHandleThrottleConfigure(commandHeader, peer, commandStartIdx, commandSize) != 0)
                        goto commandError;
                    break;

                default:
                    goto commandError;
            }

            if (peer != null &&
                (commandHeader.command & (int)ENetProtoFlag.CmdFlagAck) != 0)
            {
                uint sentTime;

                if ((flags & (int)ENetProtoFlag.HeaderFalgSentTime) == 0)
                    break;

                sentTime = Utils.NetToHostOrder(header.sentTime);

                switch (peer.state)
                {
                    case ENetPeerState.Disconnecting:
                    case ENetPeerState.AckConnect:
                    case ENetPeerState.Disconnected:
                    case ENetPeerState.Zombie:
                        break;

                    case ENetPeerState.AckDisconnect:
                        if ((commandHeader.command & (int)ENetProtoCmdType.Mask) == (int)ENetProtoCmdType.Disconnect)
                            peer.QueueAck(commandHeader, sentTime);
                        break;

                    default:
                        peer.QueueAck(commandHeader, sentTime);
                        break;
                }
            }
        }

    commandError:
        if (@event != null && @event.type != ENetEventType.None)
            return 1;

        return 0;
    }

    public int ProtoHandleAcknowledge(ENetEvent @event, ENetPeer peer, ENetProtoCmdHeader commandHeader, int commandStartIdx, int commandSize)
    {
        long roundTripTime,
               receivedSentTime;
        uint receivedReliableSequenceNumber;
        ENetProtoCmdType commandNumber;

        if (this.receivedData == null) return 0;
        ENetProtoAck? ackCmd = Utils.DeSerialize<ENetProtoAck>(Utils.SubBytes(this.receivedData, commandStartIdx, commandSize));
        if (ackCmd == null) return -1;

        if (peer.state == ENetPeerState.Disconnected || peer.state == ENetPeerState.Zombie)
            return 0;

        receivedSentTime = Utils.NetToHostOrder(ackCmd.receivedSentTime);
        receivedSentTime |= this.serviceTime & 0xFFFF0000;
        if ((receivedSentTime & 0x8000) > (this.serviceTime & 0x8000))
            receivedSentTime -= 0x10000;

        if (this.serviceTime < receivedSentTime)
            return 0;

        roundTripTime = Math.Abs(this.serviceTime - receivedSentTime);
        roundTripTime = Math.Max(roundTripTime, 1);

        if (peer.lastReceiveTime > 0)
        {
            peer.Throttle(roundTripTime);

            peer.roundTripTimeVariance -= peer.roundTripTimeVariance / 4;

            if (roundTripTime >= peer.roundTripTime)
            {
                long diff = roundTripTime - peer.roundTripTime;
                peer.roundTripTimeVariance += diff / 4;
                peer.roundTripTime += diff / 8;
            }
            else
            {
                long diff = peer.roundTripTime - roundTripTime;
                peer.roundTripTimeVariance += diff / 4;
                peer.roundTripTime -= diff / 8;
            }
        }
        else
        {
            peer.roundTripTime = roundTripTime;
            peer.roundTripTimeVariance = (roundTripTime + 1) / 2;
        }

        if (peer.roundTripTime < peer.lowestRoundTripTime)
            peer.lowestRoundTripTime = peer.roundTripTime;

        if (peer.roundTripTimeVariance > peer.highestRoundTripTimeVariance)
            peer.highestRoundTripTimeVariance = peer.roundTripTimeVariance;

        if (peer.packetThrottleEpoch == 0 ||
            Math.Abs(this.serviceTime - peer.packetThrottleEpoch) >= peer.packetThrottleInterval)
        {
            peer.lastRoundTripTime = peer.lowestRoundTripTime;
            peer.lastRoundTripTimeVariance = Math.Max(peer.highestRoundTripTimeVariance, 1);
            peer.lowestRoundTripTime = peer.roundTripTime;
            peer.highestRoundTripTimeVariance = peer.roundTripTimeVariance;
            peer.packetThrottleEpoch = this.serviceTime;
        }

        peer.lastReceiveTime = Math.Max(this.serviceTime, 1);
        peer.earliestTimeout = 0;

        receivedReliableSequenceNumber = Utils.NetToHostOrder(ackCmd.receivedReliableSequenceNumber);

        commandNumber = ProtoRemoveSentReliableCommand(peer, receivedReliableSequenceNumber, commandHeader.channelID);

        switch (peer.state)
        {
            case ENetPeerState.AckConnect:
                if (commandNumber != ENetProtoCmdType.VerifyConnect)
                    return -1;

                ProtoNotifyConnect(peer, @event);
                break;

            case ENetPeerState.Disconnecting:
                if (commandNumber != ENetProtoCmdType.Disconnect)
                    return -1;

                ProtoNotifyDisconnect(peer, @event);
                break;

            case ENetPeerState.DisconnectLater:
                if (peer.outgoingCommands.Count == 0 &&
                    peer.sentReliableCommands.Count == 0)
                    peer.Disconnect(peer.@eventData);
                break;

            default:
                break;
        }

        return 0;
    }

    public ENetPeer? ProtoHandleConnect(ENetProtoCmdHeader commandHeader, int commandStartIdx, int commandSize)
    { 
        uint incomingSessionID, outgoingSessionID;
        uint mtu, windowSize;
        ENetChannel channel;
        uint channelCount, duplicatePeers = 0;
        ENetPeer? peer = null;

        if (this.receivedData == null) return null;
        ENetProtoConnect? connectCmd = Utils.DeSerialize<ENetProtoConnect>(Utils.SubBytes(this.receivedData, commandStartIdx, commandSize));
        if (connectCmd == null) return null;

        channelCount = Utils.NetToHostOrder(connectCmd.channelCount);

        if (channelCount < (int)ENetDef.ProtoMinChannelCount ||
            channelCount > (int)ENetDef.ProtoMaxChannelCount)
            return null;

        if (this.peers == null)
        {
            return null;
        }

        foreach (var currentPeer in this.peers)
        {
            if (currentPeer.state == (int)ENetPeerState.Disconnected)
            {
                if (peer == null)
                    peer = currentPeer;
            }
            else
            if (currentPeer.state != ENetPeerState.Connecting &&
                currentPeer.address?.Address.GetAddressBytes() == this.receivedAddress?.Address.GetAddressBytes())
            {
                if (currentPeer?.address?.Port == this.receivedAddress?.Port &&
                    currentPeer?.connectID == connectCmd.connectID)
                    return null;

                ++duplicatePeers;
            }
        }

        if (peer == null || duplicatePeers >= this.duplicatePeers)
            return null;

        if (channelCount > this.channelLimit)
            channelCount = this.channelLimit;

        peer.channels = new ENetChannel[channelCount];

        peer.state = ENetPeerState.AckConnect;
        peer.connectID = connectCmd.connectID;
        peer.address = this.receivedAddress;
        peer.outgoingPeerID = Utils.NetToHostOrder(connectCmd.outgoingPeerID);
        peer.incomingBandwidth = Utils.NetToHostOrder(connectCmd.incomingBandwidth);
        peer.outgoingBandwidth = Utils.NetToHostOrder(connectCmd.outgoingBandwidth);
        peer.packetThrottleInterval = Utils.NetToHostOrder(connectCmd.packetThrottleInterval);
        peer.packetThrottleAcceleration = Utils.NetToHostOrder(connectCmd.packetThrottleAcceleration);
        peer.packetThrottleDeceleration = Utils.NetToHostOrder(connectCmd.packetThrottleDeceleration);
        peer.@eventData = Utils.NetToHostOrder(connectCmd.data);

        incomingSessionID = connectCmd.incomingSessionID == 0xFF ? peer.outgoingSessionID : connectCmd.incomingSessionID;
        incomingSessionID = (incomingSessionID + 1) & ((int)ENetProtoFlag.HeaderSessionMask >> (int)ENetProtoFlag.HeaderSessionShift);
        if (incomingSessionID == peer.outgoingSessionID)
            incomingSessionID = (incomingSessionID + 1) & ((int)ENetProtoFlag.HeaderSessionMask >> (int)ENetProtoFlag.HeaderSessionShift);
        peer.outgoingSessionID = incomingSessionID;

        outgoingSessionID = connectCmd.outgoingSessionID == 0xFF ? peer.incomingSessionID : connectCmd.outgoingSessionID;
        outgoingSessionID = (outgoingSessionID + 1) & ((int)ENetProtoFlag.HeaderSessionMask >> (int)ENetProtoFlag.HeaderSessionShift);
        if (outgoingSessionID == peer.incomingSessionID)
            outgoingSessionID = (outgoingSessionID + 1) & ((int)ENetProtoFlag.HeaderSessionMask >> (int)ENetProtoFlag.HeaderSessionShift);
        peer.incomingSessionID = outgoingSessionID;

        mtu = Utils.NetToHostOrder(connectCmd.mtu);

        if (mtu < (int)ENetDef.ProtoMinMTU)
            mtu = (int)ENetDef.ProtoMinMTU;
        else
        if (mtu > (int)ENetDef.ProtoMaxMTU)
            mtu = (int)ENetDef.ProtoMaxMTU;

        peer.mtu = mtu;

        if (this.outgoingBandwidth == 0 &&
            peer.incomingBandwidth == 0)
            peer.windowSize = (int)ENetDef.ProtoMaxWindowSize;
        else
        if (this.outgoingBandwidth == 0 ||
            peer.incomingBandwidth == 0)
            peer.windowSize = (Math.Max(this.outgoingBandwidth, peer.incomingBandwidth) /
                                          (uint)ENetDef.PeerWindowSizeScale) *
                                            (int)ENetDef.ProtoMinWindowSize;
        else
            peer.windowSize = (Math.Min(this.outgoingBandwidth, peer.incomingBandwidth) /
                                          (uint)ENetDef.PeerWindowSizeScale) *
                                            (int)ENetDef.ProtoMinWindowSize;

        if (peer.windowSize < (int)ENetDef.ProtoMinWindowSize)
            peer.windowSize = (int)ENetDef.ProtoMinWindowSize;
        else
        if (peer.windowSize > (int)ENetDef.ProtoMaxWindowSize)
            peer.windowSize = (int)ENetDef.ProtoMaxWindowSize;

        if (this.incomingBandwidth == 0)
            windowSize = (int)ENetDef.ProtoMaxWindowSize;
        else
            windowSize = (this.incomingBandwidth / (uint)ENetDef.PeerWindowSizeScale) *
                           (int)ENetDef.ProtoMinWindowSize;

        if (windowSize > Utils.NetToHostOrder(connectCmd.windowSize))
            windowSize = Utils.NetToHostOrder(connectCmd.windowSize);

        if (windowSize < (int)ENetDef.ProtoMinWindowSize)
            windowSize = (int)ENetDef.ProtoMinWindowSize;
        else
        if (windowSize > (int)ENetDef.ProtoMaxWindowSize)
            windowSize = (int)ENetDef.ProtoMaxWindowSize;


        ENetProto verifyCommand = new ENetProto();
        verifyCommand.header.command = (int)ENetProtoCmdType.VerifyConnect | (int)ENetProtoFlag.CmdFlagAck;
        verifyCommand.header.channelID = 0xFF;
        verifyCommand.verifyConnect.outgoingPeerID = (uint)IPAddress.HostToNetworkOrder(peer.incomingPeerID);
        verifyCommand.verifyConnect.incomingSessionID = incomingSessionID;
        verifyCommand.verifyConnect.outgoingSessionID = outgoingSessionID;
        verifyCommand.verifyConnect.mtu = (uint)IPAddress.HostToNetworkOrder(peer.mtu);
        verifyCommand.verifyConnect.windowSize = (uint)IPAddress.HostToNetworkOrder(windowSize);
        verifyCommand.verifyConnect.channelCount = (uint)IPAddress.HostToNetworkOrder(channelCount);
        verifyCommand.verifyConnect.incomingBandwidth = (uint)IPAddress.HostToNetworkOrder(this.incomingBandwidth);
        verifyCommand.verifyConnect.outgoingBandwidth = (uint)IPAddress.HostToNetworkOrder(this.outgoingBandwidth);
        verifyCommand.verifyConnect.packetThrottleInterval = (uint)IPAddress.HostToNetworkOrder(peer.packetThrottleInterval);
        verifyCommand.verifyConnect.packetThrottleAcceleration = (uint)IPAddress.HostToNetworkOrder(peer.packetThrottleAcceleration);
        verifyCommand.verifyConnect.packetThrottleDeceleration = (uint)IPAddress.HostToNetworkOrder(peer.packetThrottleDeceleration);
        verifyCommand.verifyConnect.connectID = peer.connectID;

        peer.QueueOutgoingCommand(verifyCommand, null, 0, 0);

        return peer;
    }

    public int ProtoHandleVerifyConnect(ENetEvent @event, ENetPeer peer, int commandStartIdx, int commandSize)
    {
        if (this.receivedData == null) return -1;
        ENetProtoVerifyConnect? verifyConnectCmd = Utils.DeSerialize<ENetProtoVerifyConnect>(Utils.SubBytes(this.receivedData, commandStartIdx, commandSize));
        if (verifyConnectCmd == null) return 0;

        uint mtu, windowSize;
        uint channelCount;

        if (peer.state != ENetPeerState.Connecting)
            return 0;

        channelCount = Utils.NetToHostOrder(verifyConnectCmd.channelCount);

        if (channelCount < (int)ENetDef.ProtoMinChannelCount || channelCount > (int)ENetDef.ProtoMaxChannelCount ||
            Utils.NetToHostOrder(verifyConnectCmd.packetThrottleInterval) != peer.packetThrottleInterval ||
         Utils.NetToHostOrder(verifyConnectCmd.packetThrottleAcceleration) != peer.packetThrottleAcceleration ||
         Utils.NetToHostOrder(verifyConnectCmd.packetThrottleDeceleration) != peer.packetThrottleDeceleration ||
         verifyConnectCmd.connectID != peer.connectID)
        {
            peer.@eventData = 0;
            ProtoDispatchState(peer, ENetPeerState.Zombie);
            return -1;
        }

        ProtoRemoveSentReliableCommand(peer, 1, 0xFF);

        peer.outgoingPeerID = Utils.NetToHostOrder(verifyConnectCmd.outgoingPeerID);
        peer.incomingSessionID = verifyConnectCmd.incomingSessionID;
        peer.outgoingSessionID = verifyConnectCmd.outgoingSessionID;

        mtu = Utils.NetToHostOrder(verifyConnectCmd.mtu);

        if (mtu < (int)ENetDef.ProtoMinMTU)
            mtu = (int)ENetDef.ProtoMinMTU;
        else
        if (mtu > (int)ENetDef.ProtoMaxMTU)
            mtu = (int)ENetDef.ProtoMaxMTU;

        if (mtu < peer.mtu)
            peer.mtu = mtu;

        windowSize = Utils.NetToHostOrder(verifyConnectCmd.windowSize);

        if (windowSize < (int)ENetDef.ProtoMinWindowSize)
            windowSize = (int)ENetDef.ProtoMinWindowSize;

        if (windowSize > (int)ENetDef.ProtoMaxWindowSize)
            windowSize = (int)ENetDef.ProtoMaxWindowSize;

        if (windowSize < peer.windowSize)
            peer.windowSize = windowSize;

        peer.incomingBandwidth = Utils.NetToHostOrder(verifyConnectCmd.incomingBandwidth);
        peer.outgoingBandwidth = Utils.NetToHostOrder(verifyConnectCmd.outgoingBandwidth);

        ProtoNotifyConnect(peer, @event);
        return 0;
    }
    
    public int ProtoHandleDisconnect(ENetProtoCmdHeader commandHeader, ENetPeer peer, int commandStartIdx, int commandSize)
   {
       if (peer.state == (int) ENetPeerState.Disconnected || peer.state == ENetPeerState.Zombie || peer.state == ENetPeerState.AckDisconnect)
           return 0;

        if (this.receivedData == null) return -1;
        ENetProtoDisconnect? disconnectCmd = Utils.DeSerialize<ENetProtoDisconnect>(Utils.SubBytes(this.receivedData, commandStartIdx, commandSize));
        if (disconnectCmd == null) return 0;

        peer.ResetQueues();

       if (peer.state == ENetPeerState.ConnectionSucceed || peer.state == ENetPeerState.Disconnecting || peer.state == ENetPeerState.Connecting)
           ProtoDispatchState(peer, ENetPeerState.Zombie);
       else
       if (peer.state != ENetPeerState.Connected && peer.state != ENetPeerState.DisconnectLater)
    {
        if (peer.state == ENetPeerState.ConnectionPending) this.recalculateBandwidthLimits = 1;

            peer.Reset();
    }
       else
       if ((commandHeader.command & (int) ENetProtoFlag.CmdFlagAck)!=0)
           ProtoChangeState(peer, ENetPeerState.AckDisconnect);
       else
           ProtoDispatchState(peer, ENetPeerState.Zombie);

       if (peer.state != (int)ENetPeerState.Disconnected)
        {
            peer.@eventData = Utils.NetToHostOrder(disconnectCmd.data);
        }

       return 0;
   }


    public int ProtoHandlePing(ENetPeer peer)
   {
       if (peer.state != ENetPeerState.Connected && peer.state != ENetPeerState.DisconnectLater)
           return -1;

       return 0;
   }

    public int ProtoHandleSendReliable(ENetProtoCmdHeader commandHeader, ENetPeer peer, int commandStartIdx, int commandSize, ref int currentDataIdx)
    {
        if (this.receivedData == null) return -1;
        ENetProtoSendReliable? sendReliableCmd = Utils.DeSerialize<ENetProtoSendReliable>(Utils.SubBytes(this.receivedData, commandStartIdx, commandSize));
        if (sendReliableCmd == null) return 0;

        uint dataLength;

        if (commandHeader.channelID >= peer.channelCount ||
            (peer.state != ENetPeerState.Connected && peer.state != ENetPeerState.DisconnectLater))
            return -1;

        dataLength = Utils.NetToHostOrder(sendReliableCmd.dataLength);
        if (dataLength > this.maximumPacketSize)
            return -1;

        byte[] packetData = Utils.SubBytes(this.receivedData, currentDataIdx, (int)dataLength);
        currentDataIdx += (int)dataLength;

        if (this.receivedData.Length <= currentDataIdx)
            return -1;

        if (peer.QueueInCmd(commandHeader, packetData, dataLength, (int)ENetPacketFlag.Reliable, 0) == null)
            return -1;

        return 0;
    }

    public int ProtoHandleSendFragment(ENetProtoCmdHeader commandHeader, ENetPeer peer, int commandStartIdx, int commandSize, ref int currentDataIdx)
    {
        uint fragmentNumber,
               fragmentCount,
               fragmentOffset,
               fragmentLength,
               startSequenceNumber,
               totalLength;
        ENetChannel channel;
        uint startWindow, currentWindow;

        if (commandHeader.channelID >= peer.channelCount ||
            (peer.state != ENetPeerState.Connected && peer.state != ENetPeerState.DisconnectLater))
            return -1;


        if (this.receivedData == null) return -1;
        ENetProtoSendFragment? sendFragmentCmd = Utils.DeSerialize<ENetProtoSendFragment>(Utils.SubBytes(this.receivedData, commandStartIdx, commandSize));
        if (sendFragmentCmd == null) return 0;

        fragmentLength = Utils.NetToHostOrder(sendFragmentCmd.dataLength);
        currentDataIdx += (int)fragmentLength;
        if (fragmentLength > this.maximumPacketSize ||
            currentDataIdx > this.receivedDataLength)
            return -1;


        if (peer.channels == null) return -1;
        channel = peer.channels[commandHeader.channelID];
        startSequenceNumber = Utils.NetToHostOrder(sendFragmentCmd.startSequenceNumber);
        startWindow = startSequenceNumber / (uint)ENetDef.PeerReliableWindowSize;
        currentWindow = channel.incomingReliableSequenceNumber / (uint)ENetDef.PeerReliableWindowSize;

        if (startSequenceNumber < channel.incomingReliableSequenceNumber)
            startWindow += (uint)ENetDef.PeerReliableWindows;

        if (startWindow < currentWindow || startWindow >= currentWindow + (uint)ENetDef.PeerFreeReliableWindows - 1)
            return 0;

        fragmentNumber = Utils.NetToHostOrder(sendFragmentCmd.fragmentNumber);
        fragmentCount = Utils.NetToHostOrder(sendFragmentCmd.fragmentCount);
        fragmentOffset = Utils.NetToHostOrder(sendFragmentCmd.fragmentOffset);
        totalLength = Utils.NetToHostOrder(sendFragmentCmd.totalLength);

        if (fragmentCount > (int)ENetDef.ProtoMaxFragmentCount ||
            fragmentNumber >= fragmentCount ||
            totalLength > this.maximumPacketSize ||
            fragmentOffset >= totalLength ||
            fragmentLength > totalLength - fragmentOffset)
            return -1;

        LinkedListNode<ENetInCmd>? currentCommand;
        ENetInCmd? startCommand = null;
        for (currentCommand = channel.incomingReliableCommands.Last; currentCommand != null; currentCommand = currentCommand.Previous)
        {
            ENetInCmd incomingcommand = currentCommand.Value;

            if (startSequenceNumber >= channel.incomingReliableSequenceNumber)
            {
                if (incomingcommand.reliableSequenceNumber < channel.incomingReliableSequenceNumber)
                    continue;
            }
            else
            if (incomingcommand.reliableSequenceNumber >= channel.incomingReliableSequenceNumber)
                break;

            if (incomingcommand.reliableSequenceNumber <= startSequenceNumber)
            {
                if (incomingcommand.reliableSequenceNumber < startSequenceNumber)
                    break;

                if ((incomingcommand.commandHeader.command & (int)ENetProtoCmdType.Mask) != (int)ENetProtoCmdType.SendFragment ||
                    totalLength != incomingcommand.packet.DataLength ||
                     fragmentCount != incomingcommand.fragmentCount)
                    return -1;

                startCommand = incomingcommand;
                break;
            }
        }

        if (startCommand == null)
        {

            commandHeader.reliableSequenceNumber = startSequenceNumber;

            startCommand = peer.QueueInCmd(commandHeader, null, totalLength, (int)ENetPacketFlag.Reliable, fragmentCount);
            if (startCommand == null)
                return -1;
        }

        if (startCommand.fragments != null && (startCommand.fragments[fragmentNumber / 32] & (uint)(1 << ((int)fragmentNumber % 32))) == 0)
        {
            --startCommand.fragmentsRemaining;

            startCommand.fragments[fragmentNumber / 32] |= (uint)(1 << ((int)fragmentNumber % 32));

            if (fragmentOffset + fragmentLength > startCommand.packet.DataLength)
                fragmentLength = startCommand.packet.DataLength - fragmentOffset;




            if (startCommand.packet.Data != null)
            {
                Array.Copy(startCommand.packet.Data, fragmentOffset, this.receivedData, currentDataIdx, fragmentLength);
            }

            if (startCommand.fragmentsRemaining <= 0)
                peer.DispatchInReliableCmds(channel, null);
        }

        return 0;
    }

    public int ProtoHandleSendUnreliable(ENetProtoCmdHeader commandHeader, ENetPeer peer, int commandStartIdx, int commandSize, ref int currentDataIdx)
    {
        uint dataLength;

        if (commandHeader.channelID >= peer.channelCount ||
            (peer.state != ENetPeerState.Connected && peer.state != ENetPeerState.DisconnectLater))
            return -1;

        if (this.receivedData == null) return -1;
        ENetProtoSendUnReliable? sendUnReliableCmd = Utils.DeSerialize<ENetProtoSendUnReliable>(Utils.SubBytes(this.receivedData, commandStartIdx, commandSize));
        if (sendUnReliableCmd == null) return 0;

        dataLength = Utils.NetToHostOrder(sendUnReliableCmd.dataLength);
        byte[] packetData = Utils.SubBytes(this.receivedData, currentDataIdx, (int)dataLength);
        currentDataIdx += (int)dataLength;

        if (dataLength > this.maximumPacketSize ||
            currentDataIdx > this.receivedDataLength)
            return -1;

        if (peer.QueueInCmd(commandHeader, packetData, dataLength, 0, 0, sendUnReliableCmd.unreliableSeqNum) == null)
            return -1;

        return 0;
    }

    public int ProtoHandleSendUnreliableFragment(ENetProtoCmdHeader commandHeader, ENetPeer peer, int commandStartIdx, int commandSize, ref int currentDataIdx)
    {
        uint fragmentNumber,
               fragmentCount,
               fragmentOffset,
               fragmentLength,
               reliableSequenceNumber,
               startSequenceNumber,
               totalLength;
        uint reliableWindow, currentWindow;
        ENetChannel channel;

        if (commandHeader.channelID >= peer.channelCount ||
            (peer.state != ENetPeerState.Connected && peer.state != ENetPeerState.DisconnectLater))
            return -1;

        if (this.receivedData == null) return -1;
        ENetProtoSendFragment? sendFragmentCmd = Utils.DeSerialize<ENetProtoSendFragment>(Utils.SubBytes(this.receivedData, commandStartIdx, commandSize));
        if (sendFragmentCmd == null) return 0;

        fragmentLength = Utils.NetToHostOrder(sendFragmentCmd.dataLength);
        currentDataIdx += (int)fragmentLength;
        if (fragmentLength > this.maximumPacketSize ||
            currentDataIdx > this.receivedDataLength)
            return -1;

        if (peer.channels == null) return -1;
        channel = peer.channels[commandHeader.channelID];

        reliableSequenceNumber = commandHeader.reliableSequenceNumber;
        startSequenceNumber = Utils.NetToHostOrder(sendFragmentCmd.startSequenceNumber);

        reliableWindow = reliableSequenceNumber / (uint)ENetDef.PeerReliableWindowSize;
        currentWindow = channel.incomingReliableSequenceNumber / (uint)ENetDef.PeerReliableWindowSize;

        if (reliableSequenceNumber < channel.incomingReliableSequenceNumber)
            reliableWindow += (uint)ENetDef.PeerReliableWindows;

        if (reliableWindow < currentWindow || reliableWindow >= currentWindow + (uint)ENetDef.PeerFreeReliableWindows - 1)
            return 0;

        if (reliableSequenceNumber == channel.incomingReliableSequenceNumber &&
            startSequenceNumber <= channel.incomingUnreliableSequenceNumber)
            return 0;

        fragmentNumber = Utils.NetToHostOrder(sendFragmentCmd.fragmentNumber);
        fragmentCount = Utils.NetToHostOrder(sendFragmentCmd.fragmentCount);
        fragmentOffset = Utils.NetToHostOrder(sendFragmentCmd.fragmentOffset);
        totalLength = Utils.NetToHostOrder(sendFragmentCmd.totalLength);

        if (fragmentCount > (int)ENetDef.ProtoMaxFragmentCount ||
            fragmentNumber >= fragmentCount ||
            totalLength > this.maximumPacketSize ||
            fragmentOffset >= totalLength ||
            fragmentLength > totalLength - fragmentOffset)
            return -1;

        LinkedListNode<ENetInCmd>? currentCommand;
        ENetInCmd? startCommand = null;

        for (currentCommand = channel.incomingUnreliableCommands.Last; currentCommand != null; currentCommand = currentCommand.Previous)
        {
            ENetInCmd inCmd = currentCommand.Value;

            if (reliableSequenceNumber >= channel.incomingReliableSequenceNumber)
            {
                if (inCmd.reliableSequenceNumber < channel.incomingReliableSequenceNumber)
                    continue;
            }
            else
            if (inCmd.reliableSequenceNumber >= channel.incomingReliableSequenceNumber)
                break;

            if (inCmd.reliableSequenceNumber < reliableSequenceNumber)
                break;

            if (inCmd.reliableSequenceNumber > reliableSequenceNumber)
                continue;

            if (inCmd.unreliableSequenceNumber <= startSequenceNumber)
            {
                if (inCmd.unreliableSequenceNumber < startSequenceNumber)
                    break;

                if ((inCmd.commandHeader.command & (int)ENetProtoCmdType.Mask) != (int)ENetProtoCmdType.SendUnreliableFragment ||
                    totalLength != inCmd.packet.DataLength ||
                     fragmentCount != inCmd.fragmentCount)
                    return -1;

                startCommand = inCmd;
                break;
            }
        }

        if (startCommand == null)
        {
            startCommand = peer.QueueInCmd(commandHeader, null, totalLength, (int)ENetPacketFlag.UnreliableFragment, fragmentCount, sendFragmentCmd.startSequenceNumber);
            if (startCommand == null)
                return -1;
        }

        if (startCommand.fragments != null && (startCommand.fragments[fragmentNumber / 32] & (1 << ((int)fragmentNumber % 32))) == 0)
        {
            --startCommand.fragmentsRemaining;

            startCommand.fragments[fragmentNumber / 32] |= (uint)(1 << ((int)fragmentNumber % 32));

            if (fragmentOffset + fragmentLength > startCommand.packet.DataLength)
                fragmentLength = startCommand.packet.DataLength - fragmentOffset;

            if (startCommand.packet.Data != null)
            {
                Array.Copy(startCommand.packet.Data, fragmentOffset, this.receivedData, currentDataIdx, fragmentLength);
            }

            if (startCommand.fragmentsRemaining <= 0)
                peer.DispatchInUnreliableCmds(channel, null);
        }

        return 0;
    }

    public int ProtoHandleSendUnsequenced(ENetProtoCmdHeader commandHeader, ENetPeer peer, int commandStartIdx, int commandSize, ref int currentDataIdx)
    {
        uint unsequencedGroup, index;
        uint dataLength;

        if (commandHeader.channelID >= peer.channelCount ||
            (peer.state != ENetPeerState.Connected && peer.state != ENetPeerState.DisconnectLater))
            return -1;

        if (this.receivedData == null) return -1;
        ENetProtoSendUnsequenced? sendUnseq = Utils.DeSerialize<ENetProtoSendUnsequenced>(Utils.SubBytes(this.receivedData, commandStartIdx, commandSize));
        if (sendUnseq == null) return 0;

        dataLength = Utils.NetToHostOrder(sendUnseq.dataLength);
        currentDataIdx += (int)dataLength;
        if (dataLength > this.maximumPacketSize ||
             currentDataIdx > this.receivedDataLength)
            return -1;

        unsequencedGroup = Utils.NetToHostOrder(sendUnseq.unsequencedGroup);
        index = unsequencedGroup % (uint)ENetDef.PeerUnseqWindowSize;

        if (unsequencedGroup < peer.incomingUnsequencedGroup)
            unsequencedGroup += 0x10000;

        if (unsequencedGroup >= (uint)peer.incomingUnsequencedGroup + (uint)ENetDef.PeerUnseqWindows * (uint)ENetDef.PeerUnseqWindowSize)
            return 0;

        unsequencedGroup &= 0xFFFF;

        if (unsequencedGroup - index != peer.incomingUnsequencedGroup)
        {
            peer.incomingUnsequencedGroup = unsequencedGroup - index;
            Array.Clear(peer.unsequencedWindow);
        }
        else
        if ((peer.unsequencedWindow[index / 32] & (uint)(1 << (int)(index % 32))) != 0)
            return 0;


        byte[] packetData = Utils.SubBytes(this.receivedData, currentDataIdx, (int)dataLength);
        currentDataIdx += (int)dataLength;

        if (peer.QueueInCmd(commandHeader, packetData, dataLength, (int)ENetPacketFlag.UnSeq, 0) == null)
            return -1;

        peer.unsequencedWindow[index / 32] |= (uint)(1 << ((int)index % 32));

        return 0;
    }

    public int ProtoHandleBandwidthLimit(ENetProtoCmdHeader commandHeader, ENetPeer peer, int commandStartIdx, int commandSize)
    {
        if (peer.state != ENetPeerState.Connected && peer.state != ENetPeerState.DisconnectLater)
            return -1;

        if (peer.incomingBandwidth != 0)
            --this.bandwidthLimitedPeers;

        if (this.receivedData == null) return -1;
        ENetProtoBandwidthLimit? bandwidthLimitCmd = Utils.DeSerialize<ENetProtoBandwidthLimit>(Utils.SubBytes(this.receivedData, commandStartIdx, commandSize));
        if (bandwidthLimitCmd == null) return 0;

        peer.incomingBandwidth = Utils.NetToHostOrder(bandwidthLimitCmd.incomingBandwidth);
        peer.outgoingBandwidth = Utils.NetToHostOrder(bandwidthLimitCmd.outgoingBandwidth);

        if (peer.incomingBandwidth != 0)
            ++this.bandwidthLimitedPeers;

        if (peer.incomingBandwidth == 0 && this.outgoingBandwidth == 0)
            peer.windowSize = (int)ENetDef.ProtoMaxWindowSize;
        else
        if (peer.incomingBandwidth == 0 || this.outgoingBandwidth == 0)
            peer.windowSize = (Math.Max(peer.incomingBandwidth, this.outgoingBandwidth) /
                                   (uint)ENetDef.PeerWindowSizeScale) * (int)ENetDef.ProtoMinWindowSize;
        else
            peer.windowSize = (Math.Min(peer.incomingBandwidth, this.outgoingBandwidth) /
                                   (uint)ENetDef.PeerWindowSizeScale) * (int)ENetDef.ProtoMinWindowSize;

        if (peer.windowSize < (int)ENetDef.ProtoMinWindowSize)
            peer.windowSize = (int)ENetDef.ProtoMinWindowSize;
        else
        if (peer.windowSize > (int)ENetDef.ProtoMaxWindowSize)
            peer.windowSize = (int)ENetDef.ProtoMaxWindowSize;

        return 0;
    }

    public int ProtoHandleThrottleConfigure(ENetProtoCmdHeader commandHeader, ENetPeer peer, int commandStartIdx, int commandSize)
    {
        if (peer.state != ENetPeerState.Connected && peer.state != ENetPeerState.DisconnectLater)
            return -1;

        if (this.receivedData == null) return -1;
        ENetProtoThrottleConfigure? throttleConfigCmd = Utils.DeSerialize<ENetProtoThrottleConfigure>(Utils.SubBytes(this.receivedData, commandStartIdx, commandSize));
        if (throttleConfigCmd == null) return 0;

        peer.packetThrottleInterval = Utils.NetToHostOrder(throttleConfigCmd.packetThrottleInterval);
        peer.packetThrottleAcceleration = Utils.NetToHostOrder(throttleConfigCmd.packetThrottleAcceleration);
        peer.packetThrottleDeceleration = Utils.NetToHostOrder(throttleConfigCmd.packetThrottleDeceleration);

        return 0;
    }

    #endregion
    
    
    //ENetEvent @event, ENetPeer peer, ENetProtoCmdHeader commandHeader, int commandStartIdx, int commandSize
    /*
    if (this.receivedData == null) return -1;
    ENetProtoConnect? connectCmd = Utils.DeSerialize<ENetProtoConnect>(Utils.SubBytes(this.receivedData, commandStartIdx, commandSize));
        if (connectCmd == null) return 0;
    */

    /*

   public void 
   ProtoSendAcknowledgements(ENetPeer peer)
   {
       ENetProto* command = &this.commands[this.commandCount];
       ENetBuffer* buffer = &this.buffers[this.bufferCount];
       ENetAcknowledgement* acknowledgement;
       ENetListIterator currentAcknowledgement;
       uint reliableSequenceNumber;

       currentAcknowledgement = enetListBegin(&peer.acknowledgements);

       while (currentAcknowledgement != enetListEnd(&peer.acknowledgements))
       {
           if (command >= &this.commands[sizeof(host ->commands) / Marshal.SizeOf<ENetProto>()] ||
        buffer >= &this.buffers[sizeof(host ->buffers) / Marshal.SizeOf<ENetBuffer>()] ||
   peer.mtu - this.packetSize < Marshal.SizeOf<ENetProtoAcknowledge>())
          {
       this.continueSending = 1;

       break;
   }

   acknowledgement = (ENetAcknowledgement*)currentAcknowledgement;

   currentAcknowledgement = enetListNext(currentAcknowledgement);

   buffer->data = command;
   buffer->dataLength = Marshal.SizeOf<ENetProtoAcknowledge>();

   this.packetSize += buffer->dataLength;

   reliableSequenceNumber = (uint)IPAddress.HostToNetworkOrder(acknowledgement->commandHeader.reliableSequenceNumber);

   commandHeader.command = (int)ENetProtoCmdType.Ack;
   commandHeader.channelID = acknowledgement->commandHeader.channelID;
   commandHeader.reliableSequenceNumber = reliableSequenceNumber;
   command.acknowledge.receivedReliableSequenceNumber = reliableSequenceNumber;
   command.acknowledge.receivedSentTime = (uint)IPAddress.HostToNetworkOrder(acknowledgement->sentTime);

   if ((acknowledgement->commandHeader.command & (int)ENetProtoCmdType.Mask) == (int)ENetProtoCmdType.Disconnect)
       ProtoDispatchState(host, peer, (int)ENetPeerState.Zombie);

   enetListRemove(&acknowledgement->acknowledgementList);
   enetFree(acknowledgement);

   ++command;
   ++buffer;
       }

       this.commandCount = command - this.commands;
   this.bufferCount = buffer - this.buffers;
   }

   public int 
   ProtoCheckTimeouts(ENetPeer peer, ENetEvent @event)
   {
       ENetOutCmd outgoingCommand;
       ENetListIterator currentCommand, insertPosition;

       currentCommand = enetListBegin(&peer.sentReliableCommands);
       insertPosition = enetListBegin(&peer.outgoingCommands);

       while (currentCommand != enetListEnd(&peer.sentReliableCommands))
       {
           outgoingCommand = (ENetOutCmd)currentCommand;

           currentCommand = enetListNext(currentCommand);

           if (ENetTIMEDIFFERENCE(this.serviceTime, outgoingCommand.sentTime) < outgoingCommand.roundTripTimeout)
               continue;

           if (peer.earliestTimeout == 0 ||
               ENetTIMELESS(outgoingCommand.sentTime, peer.earliestTimeout))
               peer.earliestTimeout = outgoingCommand.sentTime;

           if (peer.earliestTimeout != 0 &&
                 (ENetTIMEDIFFERENCE(this.serviceTime, peer.earliestTimeout) >= peer.timeoutMaximum ||
                   (outgoingCommand.roundTripTimeout >= outgoingCommand.roundTripTimeoutLimit &&
                     ENetTIMEDIFFERENCE(this.serviceTime, peer.earliestTimeout) >= peer.timeoutMinimum)))
           {
               ProtoNotifyDisconnect(host, peer, @event);

               return 1;
           }

           if (outgoingCommand.packet != null)
               peer.reliableDataInTransit -= outgoingCommand.fragmentLength;

           ++peer.packetsLost;

           outgoingCommand.roundTripTimeout *= 2;

           enetListInsert(insertPosition, enetListRemove(&outgoingCommand.outgoingCommandList));

           if (currentCommand == enetListBegin(&peer.sentReliableCommands) &&
               !enetListEmpty(&peer.sentReliableCommands))
           {
               outgoingCommand = (ENetOutCmd)currentCommand;

               peer.nextTimeout = outgoingCommand.sentTime + outgoingCommand.roundTripTimeout;
           }
       }

       return 0;
   }

   public int 
   ProtoCheckOutgoingCommands(ENetPeer peer)
   {
       ENetProto* command = &this.commands[this.commandCount];
       ENetBuffer* buffer = &this.buffers[this.bufferCount];
       ENetOutCmd outgoingCommand;
       ENetListIterator currentCommand;
       ENetChannel channel = null;
       uint reliableWindow = 0;
       uint commandSize;
       int windowExceeded = 0, windowWrap = 0, canPing = 1;

       currentCommand = enetListBegin(&peer.outgoingCommands);

       while (currentCommand != enetListEnd(&peer.outgoingCommands))
       {
           outgoingCommand = (ENetOutCmd)currentCommand;

           if (outgoingCommand.commandHeader.command & (int)ENetProtoFlag.CmdFlagAck)
           {
               channel = outgoingCommand.commandHeader.channelID < peer.channelCount ? &peer.channels[outgoingCommand.commandHeader.channelID] : null;
               reliableWindow = outgoingCommand.reliableSequenceNumber / (uint)ENetDef.PeerReliableWindowSize;
               if (channel != null)
               {
                   if (!windowWrap &&
                        outgoingCommand.sendAttempts < 1 &&
                        !(outgoingCommand.reliableSequenceNumber % (uint)ENetDef.PeerReliableWindowSize) &&
                        (channel.reliableWindows[(reliableWindow + (uint)ENetDef.PeerReliableWindows - 1) % (uint)ENetDef.PeerReliableWindows] >= (uint)ENetDef.PeerReliableWindowSize ||
                          channel.usedReliableWindows & ((((1 << ((uint)ENetDef.PeerFreeReliableWindows + 2)) - 1) << reliableWindow) |
                            (((1 << ((uint)ENetDef.PeerFreeReliableWindows + 2)) - 1) >> ((uint)ENetDef.PeerReliableWindows - reliableWindow)))))
                       windowWrap = 1;
                   if (windowWrap)
                   {
                       currentCommand = enetListNext(currentCommand);

                       continue;
                   }
               }

               if (outgoingCommand.packet != null)
               {
                   if (!windowExceeded)
                   {
                       uint windowSize = (peer.packetThrottle * peer.windowSize) / (uint)ENetDef.PeerPacketThrottleScale;

                       if (peer.reliableDataInTransit + outgoingCommand.fragmentLength > Math.Max(windowSize, peer.mtu))
                           windowExceeded = 1;
                   }
                   if (windowExceeded)
                   {
                       currentCommand = enetListNext(currentCommand);

                       continue;
                   }
               }

               canPing = 0;
           }

           commandSize = ENetProtoCmdSize.CmdSize[outgoingCommand.commandHeader.command & (int)ENetProtoCmdType.Mask];
           if (command >= &this.commands[sizeof(host ->commands) / Marshal.SizeOf<ENetProto>()] ||
        buffer + 1 >= &this.buffers[sizeof(host ->buffers) / Marshal.SizeOf<ENetBuffer>()] ||
   peer.mtu - this.packetSize < commandSize ||
   (outgoingCommand.packet != null &&
   (uint)(peer.mtu - this.packetSize) < (uint)(commandSize + outgoingCommand.fragmentLength)))
          {
       this.continueSending = 1;

       break;
   }

   currentCommand = enetListNext(currentCommand);

   if (outgoingCommand.commandHeader.command & (int)ENetProtoFlag.CmdFlagAck)
   {
       if (channel != null && outgoingCommand.sendAttempts < 1)
       {
           channel.usedReliableWindows |= 1 << reliableWindow;
           ++channel.reliableWindows[reliableWindow];
       }

       ++outgoingCommand.sendAttempts;

       if (outgoingCommand.roundTripTimeout == 0)
       {
           outgoingCommand.roundTripTimeout = peer.roundTripTime + 4 * peer.roundTripTimeVariance;
           outgoingCommand.roundTripTimeoutLimit = peer.timeoutLimit * outgoingCommand.roundTripTimeout;
       }

       if (enetListEmpty(&peer.sentReliableCommands))
           peer.nextTimeout = this.serviceTime + outgoingCommand.roundTripTimeout;

       enetListInsert(enetListEnd(&peer.sentReliableCommands),
                         enetListRemove(&outgoingCommand.outgoingCommandList));

       outgoingCommand.sentTime = this.serviceTime;

       this.headerFlags |= (int)ENetProtoFlag.HeaderFalgSentTime;

       peer.reliableDataInTransit += outgoingCommand.fragmentLength;
   }
   else
   {
       if (outgoingCommand.packet != null && outgoingCommand.fragmentOffset == 0)
       {
           peer.packetThrottleCounter += (uint)ENetDef.PeerPacketThrottleCounter;
           peer.packetThrottleCounter %= (uint)ENetDef.PeerPacketThrottleScale;

           if (peer.packetThrottleCounter > peer.packetThrottle)
           {
               uint reliableSequenceNumber = outgoingCommand.reliableSequenceNumber,
                           unreliableSequenceNumber = outgoingCommand.unreliableSequenceNumber;
               for (; ; )
               {
                   --outgoingCommand.packet.referenceCount;

                   if (outgoingCommand.packet.referenceCount == 0)
                       enetPacketDestroy(outgoingCommand.packet);

                   enetListRemove(&outgoingCommand.outgoingCommandList);
                   enetFree(outgoingCommand);

                   if (currentCommand == enetListEnd(&peer.outgoingCommands))
                       break;

                   outgoingCommand = (ENetOutCmd)currentCommand;
                   if (outgoingCommand.reliableSequenceNumber != reliableSequenceNumber ||
                       outgoingCommand.unreliableSequenceNumber != unreliableSequenceNumber)
                       break;

                   currentCommand = enetListNext(currentCommand);
               }

               continue;
           }
       }

       enetListRemove(&outgoingCommand.outgoingCommandList);

       if (outgoingCommand.packet != null)
           enetListInsert(enetListEnd(&peer.sentUnreliableCommands), outgoingCommand);
   }

   buffer->data = command;
   buffer->dataLength = commandSize;

   this.packetSize += buffer->dataLength;

   *command = outgoingCommand.command;

   if (outgoingCommand.packet != null)
   {
       ++buffer;

       buffer->data = outgoingCommand.packet.data + outgoingCommand.fragmentOffset;
       buffer->dataLength = outgoingCommand.fragmentLength;

       this.packetSize += outgoingCommand.fragmentLength;
   }
   else
   if (!(outgoingCommand.commandHeader.command & (int)ENetProtoFlag.CmdFlagAck))
       enetFree(outgoingCommand);

   ++peer.packetsSent;

   ++command;
   ++buffer;
       }

       this.commandCount = command - this.commands;
   this.bufferCount = buffer - this.buffers;

   if (peer.state == (int)ENetPeerState.DisconnectLater &&
       enetListEmpty(&peer.outgoingCommands) &&
       enetListEmpty(&peer.sentReliableCommands) &&
       enetListEmpty(&peer.sentUnreliableCommands))
       enetPeerDisconnect(peer, peer.@eventData);

   return canPing;
   }

   public int 
   ProtoSendOutgoingCommands(ENetEvent @event, int checkForTimeouts)
   {
       uint headerData[Marshal.SizeOf<ENetProtoHeader>() + sizeof(uint)];
       ENetProtoHeader* header = (ENetProtoHeader*)headerData;
       ENetPeer currentPeer;
       int sentLength;
       uint shouldCompress = 0;

       this.continueSending = 1;

       while (this.continueSending)
           for (this.continueSending = 0,
                  currentPeer = this.peers;
                currentPeer < &this.peers[this.peerCount];
                ++currentPeer)
           {
               if (currentPeer.state == (int)ENetPeerState.Disconnected ||
                   currentPeer.state == (int)ENetPeerState.Zombie)
                   continue;

               this.headerFlags = 0;
               this.commandCount = 0;
               this.bufferCount = 1;
               this.packetSize = Marshal.SizeOf<ENetProtoHeader>();

               if (!enetListEmpty(&currentPeer.acknowledgements))
                   ProtoSendAcknowledgements(host, currentPeer);

               if (checkForTimeouts != 0 &&
                   !enetListEmpty(&currentPeer.sentReliableCommands) &&
                   ENetTIME_GREATEREQUAL(this.serviceTime, currentPeer.nextTimeout) &&
                   ProtoCheckTimeouts(host, currentPeer, @event) == 1)
               {
                   if (@event != null && @event.type != ENetEventType.None)
                 return 1;
               else
       continue;
           }

           if ((enetListEmpty(&currentPeer.outgoingCommands) ||
                 ProtoCheckOutgoingCommands(host, currentPeer)) &&
               enetListEmpty(&currentPeer.sentReliableCommands) &&
               ENetTIMEDIFFERENCE(this.serviceTime, currentPeer.lastReceiveTime) >= currentPeer.pingInterval &&
               currentPeer.mtu - this.packetSize >= Marshal.SizeOf<ENetProtoPing>())
   {
       enetPeerPing(currentPeer);
       ProtoCheckOutgoingCommands(host, currentPeer);
   }

   if (this.commandCount == 0)
       continue;

   if (currentPeer.packetLossEpoch == 0)
       currentPeer.packetLossEpoch = this.serviceTime;
   else
   if (ENetTIMEDIFFERENCE(this.serviceTime, currentPeer.packetLossEpoch) >= (uint)ENetDef.PeerPacketLossInterval &&
       currentPeer.packetsSent > 0)
   {
       uint packetLoss = currentPeer.packetsLost * (uint)ENetDef.PeerPacketLossScale / currentPeer.packetsSent;



       currentPeer.packetLossVariance = (currentPeer.packetLossVariance * 3 + ENetDIFFERENCE(packetLoss, currentPeer.packetLoss)) / 4;
       currentPeer.packetLoss = (currentPeer.packetLoss * 7 + packetLoss) / 8;

       currentPeer.packetLossEpoch = this.serviceTime;
       currentPeer.packetsSent = 0;
       currentPeer.packetsLost = 0;
   }

   this.buffers->data = headerData;
   if (this.headerFlags & (int)ENetProtoFlag.HeaderFalgSentTime)
   {
       header.sentTime = (uint)IPAddress.HostToNetworkOrder(this.serviceTime & 0xFFFF);

       this.buffers->dataLength = Marshal.SizeOf<ENetProtoHeader>();
   }
   else
       this.buffers->dataLength = (uint) & ((ENetProtoHeader*)0)->sentTime;

   shouldCompress = 0;
   if (this.compressor.context != null && this.compressor.compress != null)
   {
       uint originalSize = this.packetSize - Marshal.SizeOf<ENetProtoHeader>(),
              compressedSize = this.compressor.compress(this.compressor.context,
                                   &this.buffers[1], this.bufferCount - 1,
                                   originalSize,
                                   this.packetData[1],
                                   originalSize);
       if (compressedSize > 0 && compressedSize < originalSize)
       {
           this.headerFlags |= (int)ENetProtoFlag.HeaderFalgCompressed;
           shouldCompress = compressedSize;

       }
   }

   if (currentPeer.outgoingPeerID < (int)ENetDef.ProtoMaxPeerID)
       this.headerFlags |= currentPeer.outgoingSessionID << (int)ENetProtoFlag.HeaderSessionShift;
   header.peerID = (uint)IPAddress.HostToNetworkOrder(currentPeer.outgoingPeerID | this.headerFlags);
   if (this.checksum != null)
   {
       uint* checksum = (uint*)&headerData[this.buffers->dataLength];
       *checksum = currentPeer.outgoingPeerID < (int)ENetDef.ProtoMaxPeerID ? currentPeer.connectID : 0;
       this.buffers->dataLength += sizeof(uint);
       *checksum = this.checksum(this.buffers, this.bufferCount);
   }

   if (shouldCompress > 0)
   {
       this.buffers[1].data = this.packetData[1];
       this.buffers[1].dataLength = shouldCompress;
       this.bufferCount = 2;
   }

   currentPeer.lastSendTime = this.serviceTime;

   sentLength = enetSocketSend(this.socket, &currentPeer.address, this.buffers, this.bufferCount);

   ProtoRemoveSentUnreliableCommands(currentPeer);

   if (sentLength < 0)
       return -1;

   this.totalSentData += sentLength;
   this.totalSentPackets++;
       }

       return 0;
   }


   int
   enetHostCheckEvents(ENetEvent @event)
   {
       if (@event == null) return -1;

   @event.type = ENetEventType.None;
       @event.peer = null;
       @event.packet = null;

       return ProtoDispatchIncomingCommands (host, @event);
   }

   int
   enetHostService(ENetEvent @event, uint timeout)
   {
       uint waitCondition;

       if (@event != null)
       {
           @event.type = ENetEventType.None;
           @event.peer = null;
           @event.packet = null;

       switch (ProtoDispatchIncomingCommands(host, @event))
       {
           case 1:
               return 1;

           case -1:


               return -1;

           default:
               break;
       }
   }

   this.serviceTime = enetTime_get();

   timeout += this.serviceTime;

   do
   {
       if (ENetTIMEDIFFERENCE(this.serviceTime, this.bandwidthThrottleEpoch) >= (uint)ENetDef.HostBandwidthThrottleInterval)
           enetHostBandwidthThrottle(host);

       switch (ProtoSendOutgoingCommands(host, @event, 1))
       {
           case 1:
               return 1;

           case -1:


               return -1;

           default:
               break;
       }

       switch (ProtoReceiveIncomingCommands(host, @event))
       {
           case 1:
               return 1;

           case -1:


               return -1;

           default:
               break;
       }

       switch (ProtoSendOutgoingCommands(host, @event, 1))
       {
           case 1:
               return 1;

           case -1:


               return -1;

           default:
               break;
       }

       if (@event != null)
          {
           switch (ProtoDispatchIncomingCommands(host, @event))
           {
               case 1:
                   return 1;

               case -1:


                   return -1;

               default:
                   break;
           }
       }

       if (ENetTIME_GREATEREQUAL(this.serviceTime, timeout))
           return 0;

       do
       {
           this.serviceTime = enetTime_get();

           if (ENetTIME_GREATEREQUAL(this.serviceTime, timeout))
               return 0;

           waitCondition = (int)ENetSocketWait.Recv | (int)ENetSocketWait.Interrupt;

           if (enetSocket_wait(this.socket, &waitCondition, ENetTIMEDIFFERENCE(timeout, this.serviceTime)) != 0)
               return -1;
       }
       while (waitCondition & (int)ENetSocketWait.Interrupt);

       this.serviceTime = enetTime_get();
   } while (waitCondition & (int)ENetSocketWait.Recv);

   return 0; 
   }



   */
    #endregion


}
