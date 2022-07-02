
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
    public uint duplicatePeers;              /**< optional number of allowed peers from duplicate IPs, defaults to Proto_MAXIMUMPEERID */
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


    public void ProtoRemoveSent_unreliableCommands(ENetPeer peer)
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
                outgoingCommand?.command.header.channelID == channelID)
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

                if ((outgoingCommand?.command.header.command & (int)ENetProtoFlag.CmdFlagAck) != 0)
                    continue;

                if (outgoingCommand?.sendAttempts < 1) return (int)ENetProtoCmdType.None;

                if (outgoingCommand?.reliableSequenceNumber == reliableSequenceNumber &&
                    outgoingCommand?.command.header.channelID == channelID)
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

        commandNumber = (ENetProtoCmdType)(outgoingCommand.command.header.command & (int)ENetProtoCmdType.Mask);

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

    public ENetPeer? ProtoHandleConnect(ENetProtoHeader header, ENetProto command)
    {
        uint incomingSessionID, outgoingSessionID;
        uint mtu, windowSize;
        ENetChannel channel;
        uint channelCount, duplicatePeers = 0;
        ENetPeer? currentPeer, peer = null;

        channelCount = Utils.NetToHostOrder(command.connect.channelCount);

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
                currentPeer.address.Address.GetAddressBytes() == this.receivedAddress?.Address.GetAddressBytes())
            {
                if (currentPeer.address.Port == this.receivedAddress.Port &&
                    currentPeer.connectID == command.connect.connectID)
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
        peer.connectID = command.connect.connectID;
        peer.address = this.receivedAddress;
        peer.outgoingPeerID = Utils.NetToHostOrder(command.connect.outgoingPeerID);
        peer.incomingBandwidth = Utils.NetToHostOrder(command.connect.incomingBandwidth);
        peer.outgoingBandwidth = Utils.NetToHostOrder(command.connect.outgoingBandwidth);
        peer.packetThrottleInterval = Utils.NetToHostOrder(command.connect.packetThrottleInterval);
        peer.packetThrottleAcceleration = Utils.NetToHostOrder(command.connect.packetThrottleAcceleration);
        peer.packetThrottleDeceleration = Utils.NetToHostOrder(command.connect.packetThrottleDeceleration);
        peer.@eventData = Utils.NetToHostOrder(command.connect.data);

        incomingSessionID = command.connect.incomingSessionID == 0xFF ? peer.outgoingSessionID : command.connect.incomingSessionID;
        incomingSessionID = (incomingSessionID + 1) & ((int)ENetProtoFlag.HeaderSessionMask >> (int)ENetProtoFlag.HeaderSessionShift);
        if (incomingSessionID == peer.outgoingSessionID)
            incomingSessionID = (incomingSessionID + 1) & ((int)ENetProtoFlag.HeaderSessionMask >> (int)ENetProtoFlag.HeaderSessionShift);
        peer.outgoingSessionID = incomingSessionID;

        outgoingSessionID = command.connect.outgoingSessionID == 0xFF ? peer.incomingSessionID : command.connect.outgoingSessionID;
        outgoingSessionID = (outgoingSessionID + 1) & ((int)ENetProtoFlag.HeaderSessionMask >> (int)ENetProtoFlag.HeaderSessionShift);
        if (outgoingSessionID == peer.incomingSessionID)
            outgoingSessionID = (outgoingSessionID + 1) & ((int)ENetProtoFlag.HeaderSessionMask >> (int)ENetProtoFlag.HeaderSessionShift);
        peer.incomingSessionID = outgoingSessionID;

        mtu = Utils.NetToHostOrder(command.connect.mtu);

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

        if (windowSize > Utils.NetToHostOrder(command.connect.windowSize))
            windowSize = Utils.NetToHostOrder(command.connect.windowSize);

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

    public int
   ProtoHandleIncomingCommands(ENetEvent* @event)
    {
        ENetProtoHeader* header;
        ENetProto* command;
        ENetPeer peer;
        uint* currentData;
        uint headerSize;
        uint peerID, flags;
        uint sessionID;

        if (this.receivedDataLength < (uint)&((ENetProtoHeader*)0)->sentTime)
            return 0;

        header = (ENetProtoHeader*)this.receivedData;

        peerID = Utils.NetToHostOrder(header->peerID);
        sessionID = (peerID & (int)ENetProtoFlag.HeaderSessionMask) >> (int)ENetProtoFlag.HeaderSessionShift;
        flags = peerID & (int)ENetProtoFlag.HeaderFalgMASK;
        peerID &= ~((int)ENetProtoFlag.HeaderFalgMASK | (int)ENetProtoFlag.HeaderSessionMask);

        headerSize = (flags & (int)ENetProtoFlag.HeaderFalgSentTime ? sizeof(ENetProtoHeader) : (uint)&((ENetProtoHeader*)0)->sentTime);
        if (this.checksum != null)
            headerSize += sizeof(uint);

        if (peerID == (int)ENetDef.ProtoMaxPeerID)
            peer = null;
        else
        if (peerID >= this.peerCount)
            return 0;
        else
        {
            peer = &this.peers[peerID];

            if (peer.state == (int)ENetPeerState.Disconnected ||
                peer.state == (int)ENetPeerState.Zombie ||
                ((this.receivedAddress.host != peer.address.host ||
                  this.receivedAddress.port != peer.address.port) &&
                  peer.address.host != ENetHOST_BROADCAST) ||
                (peer.outgoingPeerID < (int)ENetDef.ProtoMaxPeerID &&
                 sessionID != peer.incomingSessionID))
                return 0;
        }

        if (flags & (int)ENetProtoFlag.HeaderFalgCompressed)
        {
            uint originalSize;
            if (this.compressor.context == null || this.compressor.decompress == null)
                return 0;

            originalSize = this.compressor.decompress(this.compressor.context,
                                        this.receivedData + headerSize,
                                        this.receivedDataLength - headerSize,
                                        this.packetData[1] + headerSize,
                                        sizeof(host ->packetData[1]) - headerSize);
            if (originalSize <= 0 || originalSize > sizeof(host ->packetData[1]) -headerSize)
             return 0;

            memcpy(this.packetData[1], header, headerSize);
            this.receivedData = this.packetData[1];
            this.receivedDataLength = headerSize + originalSize;
        }

        if (this.checksum != null)
        {
            uint* checksum = (uint*)&this.receivedData[headerSize - sizeof(uint)],
                        desiredChecksum = *checksum;
            ENetBuffer buffer;

            *checksum = peer != null ? peer.connectID : 0;

            buffer.data = this.receivedData;
            buffer.dataLength = this.receivedDataLength;

            if (this.checksum(&buffer, 1) != desiredChecksum)
                return 0;
        }

        if (peer != null)
        {
            peer.address.host = this.receivedAddress.host;
            peer.address.port = this.receivedAddress.port;
            peer.incomingDataTotal += this.receivedDataLength;
        }

        currentData = this.receivedData + headerSize;

        while (currentData < &this.receivedData[this.receivedDataLength])
        {
            uint commandNumber;
            uint commandSize;

            command = (ENetProto*)currentData;

            if (currentData + sizeof(ENetProtoCmdHeader) > &this.receivedData[this.receivedDataLength])
                break;

            commandNumber = command.header.command & (int)ENetProtoCmdType.Mask;
            if (commandNumber >= (int)ENetProtoCmdType.Count)
                break;

            commandSize = commandSizes[commandNumber];
            if (commandSize == 0 || currentData + commandSize > &this.receivedData[this.receivedDataLength])
                break;

            currentData += commandSize;

            if (peer == null && commandNumber != (int)ENetProtoCmdType.Connect)
                break;

            command.header.reliableSequenceNumber = Utils.NetToHostOrder(command.header.reliableSequenceNumber);

            switch (commandNumber)
            {
                case (int)ENetProtoCmdType.Ack:
                    if (ProtoHandle_acknowledge(host, @event, peer, command))
                        goto commandError;
                    break;

                case (int)ENetProtoCmdType.Connect:
                    if (peer != null)
                        goto commandError;
                    peer = ProtoHandleConnect(host, header, command);
                    if (peer == null)
                        goto commandError;
                    break;

                case (int)ENetProtoCmdType.VerifyConnect:
                    if (ProtoHandle_verifyConnect(host, @event, peer, command))
                        goto commandError;
                    break;

                case (int)ENetProtoCmdType.Disconnect:
                    if (ProtoHandleDisconnect(host, peer, command))
                        goto commandError;
                    break;

                case (int)ENetProtoCmdType.Ping:
                    if (ProtoHandlePing(host, peer, command))
                        goto commandError;
                    break;

                case (int)ENetProtoCmdType.SendReliable:
                    if (ProtoHandleSendReliable(host, peer, command, &currentData))
                        goto commandError;
                    break;

                case (int)ENetProtoCmdType.SendUnreliable:
                    if (ProtoHandleSend_unreliable(host, peer, command, &currentData))
                        goto commandError;
                    break;

                case (int)ENetProtoCmdType.SendUnseq:
                    if (ProtoHandleSend_unsequenced(host, peer, command, &currentData))
                        goto commandError;
                    break;

                case (int)ENetProtoCmdType.SendFragment:
                    if (ProtoHandleSend_fragment(host, peer, command, &currentData))
                        goto commandError;
                    break;

                case (int)ENetProtoCmdType.BandwidthLimit:
                    if (ProtoHandle_bandwidthLimit(host, peer, command))
                        goto commandError;
                    break;

                case (int)ENetProtoCmdType.ThrottleConfig:
                    if (ProtoHandle_throttleConfigure(host, peer, command))
                        goto commandError;
                    break;

                case (int)ENetProtoCmdType.SendUnreliableFragment:
                    if (ProtoHandleSend_unreliable_fragment(host, peer, command, &currentData))
                        goto commandError;
                    break;

                default:
                    goto commandError;
            }

            if (peer != null &&
                (command.header.command & (int)ENetProtoFlag.CmdFlagAck) != 0)
            {
                uint sentTime;

                if (!(flags & (int)ENetProtoFlag.HeaderFalgSentTime))
                    break;

                sentTime = Utils.NetToHostOrder(header->sentTime);

                switch (peer.state)
                {
                    case (int)ENetPeerState.Disconnecting:
                    case (int)ENetPeerState.AckConnect:
                    case (int)ENetPeerState.Disconnected:
                    case (int)ENetPeerState.Zombie:
                        break;

                    case (int)ENetPeerState.AckDisconnect:
                        if ((command.header.command & (int)ENetProtoCmdType.Mask) == (int)ENetProtoCmdType.Disconnect)
                            enetPeerQueue_acknowledgement(peer, command, sentTime);
                        break;

                    default:
                        enetPeerQueue_acknowledgement(peer, command, sentTime);
                        break;
                }
            }
        }

    commandError:
        if (@event != null && @event.type != ENetEventType.None)
            return 1;

        return 0;
    }

    public int ProtoHandleSendReliable(ENetPeer peer, ENetProto command, ref byte[] currentData)
    {
        uint dataLength;

        if (command.header.channelID >= peer.channelCount ||
            (peer.state != ENetPeerState.Connected && peer.state != ENetPeerState.DisconnectLater))
            return -1;

        dataLength = Utils.NetToHostOrder(command.sendReliable.dataLength);

        //currentData  += dataLength;用下面这段话替代//TODO:看能不能优化
        if (currentData.Length < dataLength)
        {
            return -1;
        }

        byte[] newData = new byte[currentData.Length - dataLength];
        for (uint i = dataLength; i < currentData.Length; i++)
        {
            newData[i - dataLength] = currentData[i];
        }
        currentData = newData;

        if (dataLength > this.maximumPacketSize)
            return -1;

        // TODO: currentData可能需要考虑重构一下
        //if (*currentData < this.receivedData ||
        //    *currentData > &this.receivedData[this.receivedDataLength])
        //    return -1;

        //TODO: 有用得代码，后面再回来改他
        //if (peer.QueueInCmd(command, command + sizeof(new ENetProtoSendReliable()), dataLength, (int)ENetPacketFlag.Reliable, 0) == null)
        // return -1;

        return 0;
    }
    /*

   public int 
   ProtoHandleSend_unsequenced(ENetPeer peer, const ENetProto* command, uint** currentData)
   {
       uint unsequencedGroup, index;
       uint dataLength;

       if (command.header.channelID >= peer.channelCount ||
           (peer.state != (int)ENetPeerState.Connected && peer.state != (int)ENetPeerState.DisconnectLater))
           return -1;

       dataLength = Utils.NetToHostOrder(command.sendUnsequenced.dataLength);
       *currentData += dataLength;
       if (dataLength > this.maximumPacketSize ||
           *currentData < this.receivedData ||
           *currentData > &this.receivedData[this.receivedDataLength])
           return -1;

       unsequencedGroup = Utils.NetToHostOrder(command.sendUnsequenced.unsequencedGroup);
       index = unsequencedGroup % (uint)ENetDef.PeerUnseqWindowSize;

       if (unsequencedGroup < peer.incomingUnsequencedGroup)
           unsequencedGroup += 0x10000;

       if (unsequencedGroup >= (uint)peer.incomingUnsequencedGroup + (uint)ENetDef.PeerUnseqWindows * (uint)ENetDef.PeerUnseqWindowSize)
           return 0;

       unsequencedGroup &= 0xFFFF;

       if (unsequencedGroup - index != peer.incomingUnsequencedGroup)
       {
           peer.incomingUnsequencedGroup = unsequencedGroup - index;

           memset(peer.unsequencedWindow, 0, sizeof(peer ->unsequencedWindow));
       }
       else
       if (peer.unsequencedWindow[index / 32] & (1 << (index % 32)))
       return 0;

   if (enetPeerQueueIncomingCommand(peer, command, (const uint*) command + sizeof(ENetProtoSendUnsequenced), dataLength, (int)ENetPacketFlag.UnSeq, 0) == null)
         return -1;

   peer.unsequencedWindow[index / 32] |= 1 << (index % 32);

   return 0;
   }

   public int 
   ProtoHandleSend_unreliable(ENetPeer peer, const ENetProto* command, uint** currentData)
   {
       uint dataLength;

       if (command.header.channelID >= peer.channelCount ||
           (peer.state != (int)ENetPeerState.Connected && peer.state != (int)ENetPeerState.DisconnectLater))
           return -1;

       dataLength = Utils.NetToHostOrder(command.sendUnreliable.dataLength);
       *currentData += dataLength;
       if (dataLength > this.maximumPacketSize ||
           *currentData < this.receivedData ||
           *currentData > &this.receivedData[this.receivedDataLength])
           return -1;

       if (enetPeerQueueIncomingCommand(peer, command, (const uint*) command + sizeof(ENetProtoSendUnreliable), dataLength, 0, 0) == null)
         return -1;

   return 0;
   }

   public int 
   ProtoHandleSend_fragment(ENetPeer peer, const ENetProto* command, uint** currentData)
   {
       uint fragmentNumber,
              fragmentCount,
              fragmentOffset,
              fragmentLength,
              startSequenceNumber,
              totalLength;
       ENetChannel channel;
       uint startWindow, currentWindow;
       ENetListIterator currentCommand;
       ENetIncomingCommand* startCommand = null;

       if (command.header.channelID >= peer.channelCount ||
           (peer.state != (int)ENetPeerState.Connected && peer.state != (int)ENetPeerState.DisconnectLater))
           return -1;

       fragmentLength = Utils.NetToHostOrder(command.sendFragment.dataLength);
       *currentData += fragmentLength;
       if (fragmentLength > this.maximumPacketSize ||
           *currentData < this.receivedData ||
           *currentData > &this.receivedData[this.receivedDataLength])
           return -1;

       channel = &peer.channels[command.header.channelID];
       startSequenceNumber = Utils.NetToHostOrder(command.sendFragment.startSequenceNumber);
       startWindow = startSequenceNumber / (uint)ENetDef.PeerReliableWindowSize;
       currentWindow = channel.incomingReliableSequenceNumber / (uint)ENetDef.PeerReliableWindowSize;

       if (startSequenceNumber < channel.incomingReliableSequenceNumber)
           startWindow += (uint)ENetDef.PeerReliableWindows;

       if (startWindow < currentWindow || startWindow >= currentWindow + (uint)ENetDef.PeerFreeReliableWindows - 1)
           return 0;

       fragmentNumber = Utils.NetToHostOrder(command.sendFragment.fragmentNumber);
       fragmentCount = Utils.NetToHostOrder(command.sendFragment.fragmentCount);
       fragmentOffset = Utils.NetToHostOrder(command.sendFragment.fragmentOffset);
       totalLength = Utils.NetToHostOrder(command.sendFragment.totalLength);

       if (fragmentCount > (int)ENetDef.ProtoMaxFragmentCount ||
           fragmentNumber >= fragmentCount ||
           totalLength > this.maximumPacketSize ||
           fragmentOffset >= totalLength ||
           fragmentLength > totalLength - fragmentOffset)
           return -1;

       for (currentCommand = enetListPrevious(enetListEnd(&channel.incomingReliableCommands));
            currentCommand != enetListEnd(&channel.incomingReliableCommands);
            currentCommand = enetListPrevious(currentCommand))
       {
           ENetIncomingCommand* incomingCommand = (ENetIncomingCommand*)currentCommand;

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

               if ((incomingcommand.command.header.command & (int)ENetProtoCmdType.Mask) != (int)ENetProtoCmdType.SendFragment ||
                   totalLength != incomingcommand.packet.dataLength ||
                   fragmentCount != incomingcommand.fragmentCount)
                   return -1;

               startCommand = incomingCommand;
               break;
           }
       }

       if (startCommand == null)
       {
           ENetProto hostCommand = *command;

           hostCommand.header.reliableSequenceNumber = startSequenceNumber;

           startCommand = enetPeerQueueIncomingCommand(peer, &hostCommand, null, totalLength, (int)ENetPacketFlag.Reliable, fragmentCount);
           if (startCommand == null)
               return -1;
       }

       if ((startcommand.fragments[fragmentNumber / 32] & (1 << (fragmentNumber % 32))) == 0)
       {
           --startcommand.fragmentsRemaining;

           startcommand.fragments[fragmentNumber / 32] |= (1 << (fragmentNumber % 32));

           if (fragmentOffset + fragmentLength > startcommand.packet.dataLength)
               fragmentLength = startcommand.packet.dataLength - fragmentOffset;

           memcpy(startcommand.packet.data + fragmentOffset,
                   (uint*)command + sizeof(ENetProtoSendFragment),
                   fragmentLength);

           if (startcommand.fragmentsRemaining <= 0)
               enetPeerDispatchIncomingReliableCommands(peer, channel, null);
       }

       return 0;
   }

   public int 
   ProtoHandleSend_unreliable_fragment(ENetPeer peer, const ENetProto* command, uint** currentData)
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
       ENetListIterator currentCommand;
       ENetIncomingCommand* startCommand = null;

       if (command.header.channelID >= peer.channelCount ||
           (peer.state != (int)ENetPeerState.Connected && peer.state != (int)ENetPeerState.DisconnectLater))
           return -1;

       fragmentLength = Utils.NetToHostOrder(command.sendFragment.dataLength);
       *currentData += fragmentLength;
       if (fragmentLength > this.maximumPacketSize ||
           *currentData < this.receivedData ||
           *currentData > &this.receivedData[this.receivedDataLength])
           return -1;

       channel = &peer.channels[command.header.channelID];
       reliableSequenceNumber = command.header.reliableSequenceNumber;
       startSequenceNumber = Utils.NetToHostOrder(command.sendFragment.startSequenceNumber);

       reliableWindow = reliableSequenceNumber / (uint)ENetDef.PeerReliableWindowSize;
       currentWindow = channel.incomingReliableSequenceNumber / (uint)ENetDef.PeerReliableWindowSize;

       if (reliableSequenceNumber < channel.incomingReliableSequenceNumber)
           reliableWindow += (uint)ENetDef.PeerReliableWindows;

       if (reliableWindow < currentWindow || reliableWindow >= currentWindow + (uint)ENetDef.PeerFreeReliableWindows - 1)
           return 0;

       if (reliableSequenceNumber == channel.incomingReliableSequenceNumber &&
           startSequenceNumber <= channel.incomingUnreliableSequenceNumber)
           return 0;

       fragmentNumber = Utils.NetToHostOrder(command.sendFragment.fragmentNumber);
       fragmentCount = Utils.NetToHostOrder(command.sendFragment.fragmentCount);
       fragmentOffset = Utils.NetToHostOrder(command.sendFragment.fragmentOffset);
       totalLength = Utils.NetToHostOrder(command.sendFragment.totalLength);

       if (fragmentCount > (int)ENetDef.ProtoMaxFragmentCount ||
           fragmentNumber >= fragmentCount ||
           totalLength > this.maximumPacketSize ||
           fragmentOffset >= totalLength ||
           fragmentLength > totalLength - fragmentOffset)
           return -1;

       for (currentCommand = enetListPrevious(enetListEnd(&channel.incomingUnreliableCommands));
            currentCommand != enetListEnd(&channel.incomingUnreliableCommands);
            currentCommand = enetListPrevious(currentCommand))
       {
           ENetIncomingCommand* incomingCommand = (ENetIncomingCommand*)currentCommand;

           if (reliableSequenceNumber >= channel.incomingReliableSequenceNumber)
           {
               if (incomingcommand.reliableSequenceNumber < channel.incomingReliableSequenceNumber)
                   continue;
           }
           else
           if (incomingcommand.reliableSequenceNumber >= channel.incomingReliableSequenceNumber)
               break;

           if (incomingcommand.reliableSequenceNumber < reliableSequenceNumber)
               break;

           if (incomingcommand.reliableSequenceNumber > reliableSequenceNumber)
               continue;

           if (incomingcommand.unreliableSequenceNumber <= startSequenceNumber)
           {
               if (incomingcommand.unreliableSequenceNumber < startSequenceNumber)
                   break;

               if ((incomingcommand.command.header.command & (int)ENetProtoCmdType.Mask) != (int)ENetProtoCmdType.SendUnreliableFragment ||
                   totalLength != incomingcommand.packet.dataLength ||
                   fragmentCount != incomingcommand.fragmentCount)
                   return -1;

               startCommand = incomingCommand;
               break;
           }
       }

       if (startCommand == null)
       {
           startCommand = enetPeerQueueIncomingCommand(peer, command, null, totalLength, (int)ENetPacketFlag.UnreliableFragment, fragmentCount);
           if (startCommand == null)
               return -1;
       }

       if ((startcommand.fragments[fragmentNumber / 32] & (1 << (fragmentNumber % 32))) == 0)
       {
           --startcommand.fragmentsRemaining;

           startcommand.fragments[fragmentNumber / 32] |= (1 << (fragmentNumber % 32));

           if (fragmentOffset + fragmentLength > startcommand.packet.dataLength)
               fragmentLength = startcommand.packet.dataLength - fragmentOffset;

           memcpy(startcommand.packet.data + fragmentOffset,
                   (uint*)command + sizeof(ENetProtoSendFragment),
                   fragmentLength);

           if (startcommand.fragmentsRemaining <= 0)
               enetPeerDispatchIncoming_unreliableCommands(peer, channel, null);
       }

       return 0;
   }

   public int 
   ProtoHandlePing(ENetPeer peer, const ENetProto* command)
   {
       if (peer.state != (int)ENetPeerState.Connected && peer.state != (int)ENetPeerState.DisconnectLater)
           return -1;

       return 0;
   }

   public int 
   ProtoHandle_bandwidthLimit(ENetPeer peer, const ENetProto* command)
   {
       if (peer.state != (int)ENetPeerState.Connected && peer.state != (int)ENetPeerState.DisconnectLater)
           return -1;

       if (peer.incomingBandwidth != 0)
           --this.bandwidthLimitedPeers;

       peer.incomingBandwidth = Utils.NetToHostOrder(command.bandwidthLimit.incomingBandwidth);
       peer.outgoingBandwidth = Utils.NetToHostOrder(command.bandwidthLimit.outgoingBandwidth);

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

   public int 
   ProtoHandle_throttleConfigure(ENetPeer peer, const ENetProto* command)
   {
       if (peer.state != (int)ENetPeerState.Connected && peer.state != (int)ENetPeerState.DisconnectLater)
           return -1;

       peer.packetThrottleInterval = Utils.NetToHostOrder(command.throttleConfigure.packetThrottleInterval);
       peer.packetThrottleAcceleration = Utils.NetToHostOrder(command.throttleConfigure.packetThrottleAcceleration);
       peer.packetThrottleDeceleration = Utils.NetToHostOrder(command.throttleConfigure.packetThrottleDeceleration);

       return 0;
   }

   public int 
   ProtoHandleDisconnect(ENetPeer peer, const ENetProto* command)
   {
       if (peer.state == (int)ENetPeerState.Disconnected || peer.state == (int)ENetPeerState.Zombie || peer.state == (int)ENetPeerState.AckDisconnect)
           return 0;

       enetPeerResetQueues(peer);

       if (peer.state == (int)ENetPeerState.ConnectionSucceed || peer.state == (int)ENetPeerState.Disconnecting || peer.state == (int)ENetPeerState.Connecting)
           ProtoDispatchState(host, peer, (int)ENetPeerState.Zombie);
       else
       if (peer.state != (int)ENetPeerState.Connected && peer.state != (int)ENetPeerState.DisconnectLater)
       {
           if (peer.state == (int)ENetPeerState.ConnectionPending) this.recalculateBandwidthLimits = 1;

           enetPeerReset(peer);
       }
       else
       if (command.header.command & (int)ENetProtoFlag.CmdFlagAck)
           ProtoChangeState(host, peer, (int)ENetPeerState.AckDisconnect);
       else
           ProtoDispatchState(host, peer, (int)ENetPeerState.Zombie);

       if (peer.state != (int)ENetPeerState.Disconnected)
           peer.@eventData = Utils.NetToHostOrder(command.disconnect.data);

       return 0;
   }

   public int 
   ProtoHandle_acknowledge(ENetEvent* @event, ENetPeer peer, const ENetProto* command)
   {
       uint roundTripTime,
              receivedSentTime,
              receivedReliableSequenceNumber;
       ENetProtoCmd commandNumber;

       if (peer.state == (int)ENetPeerState.Disconnected || peer.state == (int)ENetPeerState.Zombie)
           return 0;

       receivedSentTime = Utils.NetToHostOrder(command.acknowledge.receivedSentTime);
       receivedSentTime |= this.serviceTime & 0xFFFF0000;
       if ((receivedSentTime & 0x8000) > (this.serviceTime & 0x8000))
           receivedSentTime -= 0x10000;

       if (ENet_TIMELESS(this.serviceTime, receivedSentTime))
           return 0;

       roundTripTime = ENet_TIMEDIFFERENCE(this.serviceTime, receivedSentTime);
       roundTripTime = Math.Max(roundTripTime, 1);

       if (peer.lastReceiveTime > 0)
       {
           enetPeer_throttle(peer, roundTripTime);

           peer.roundTripTimeVariance -= peer.roundTripTimeVariance / 4;

           if (roundTripTime >= peer.roundTripTime)
           {
               uint diff = roundTripTime - peer.roundTripTime;
               peer.roundTripTimeVariance += diff / 4;
               peer.roundTripTime += diff / 8;
           }
           else
           {
               uint diff = peer.roundTripTime - roundTripTime;
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
           ENet_TIMEDIFFERENCE(this.serviceTime, peer.packetThrottleEpoch) >= peer.packetThrottleInterval)
       {
           peer.lastRoundTripTime = peer.lowestRoundTripTime;
           peer.lastRoundTripTimeVariance = Math.Max(peer.highestRoundTripTimeVariance, 1);
           peer.lowestRoundTripTime = peer.roundTripTime;
           peer.highestRoundTripTimeVariance = peer.roundTripTimeVariance;
           peer.packetThrottleEpoch = this.serviceTime;
       }

       peer.lastReceiveTime = Math.Max(this.serviceTime, 1);
       peer.earliestTimeout = 0;

       receivedReliableSequenceNumber = Utils.NetToHostOrder(command.acknowledge.receivedReliableSequenceNumber);

       commandNumber = ProtoRemoveSentReliableCommand(peer, receivedReliableSequenceNumber, command.header.channelID);

       switch (peer.state)
       {
           case (int)ENetPeerState.AckConnect:
               if (commandNumber != (int)ENetProtoCmdType.VerifyConnect)
                   return -1;

               ProtoNotifyConnect(host, peer, @event);
               break;

           case (int)ENetPeerState.Disconnecting:
               if (commandNumber != (int)ENetProtoCmdType.Disconnect)
                   return -1;

               ProtoNotifyDisconnect(host, peer, @event);
               break;

           case (int)ENetPeerState.DisconnectLater:
               if (enetListEmpty(&peer.outgoingCommands) &&
                   enetListEmpty(&peer.sentReliableCommands))
                   enetPeerDisconnect(peer, peer.@eventData);
               break;

           default:
               break;
       }

       return 0;
   }

   public int 
   ProtoHandle_verifyConnect(ENetEvent* @event, ENetPeer peer, const ENetProto* command)
   {
       uint mtu, windowSize;
       uint channelCount;

       if (peer.state != (int)ENetPeerState.Connecting)
           return 0;

       channelCount = Utils.NetToHostOrder(command.verifyConnect.channelCount);

       if (channelCount < (int)ENetDef.ProtoMinChannelCount || channelCount > (int)ENetDef.ProtoMaxChannelCount ||
           Utils.NetToHostOrder(command.verifyConnect.packetThrottleInterval) != peer.packetThrottleInterval ||
           Utils.NetToHostOrder(command.verifyConnect.packetThrottleAcceleration) != peer.packetThrottleAcceleration ||
           Utils.NetToHostOrder(command.verifyConnect.packetThrottleDeceleration) != peer.packetThrottleDeceleration ||
           command.verifyConnect.connectID != peer.connectID)
       {
           peer.@eventData = 0;

           ProtoDispatchState(host, peer, (int)ENetPeerState.Zombie);

           return -1;
       }

       ProtoRemoveSentReliableCommand(peer, 1, 0xFF);

       if (channelCount < peer.channelCount)
           peer.channelCount = channelCount;

       peer.outgoingPeerID = Utils.NetToHostOrder(command.verifyConnect.outgoingPeerID);
       peer.incomingSessionID = command.verifyConnect.incomingSessionID;
       peer.outgoingSessionID = command.verifyConnect.outgoingSessionID;

       mtu = Utils.NetToHostOrder(command.verifyConnect.mtu);

       if (mtu < (int)ENetDef.ProtoMinMTU)
           mtu = (int)ENetDef.ProtoMinMTU;
       else
       if (mtu > (int)ENetDef.ProtoMaxMTU)
           mtu = (int)ENetDef.ProtoMaxMTU;

       if (mtu < peer.mtu)
           peer.mtu = mtu;

       windowSize = Utils.NetToHostOrder(command.verifyConnect.windowSize);

       if (windowSize < (int)ENetDef.ProtoMinWindowSize)
           windowSize = (int)ENetDef.ProtoMinWindowSize;

       if (windowSize > (int)ENetDef.ProtoMaxWindowSize)
           windowSize = (int)ENetDef.ProtoMaxWindowSize;

       if (windowSize < peer.windowSize)
           peer.windowSize = windowSize;

       peer.incomingBandwidth = Utils.NetToHostOrder(command.verifyConnect.incomingBandwidth);
       peer.outgoingBandwidth = Utils.NetToHostOrder(command.verifyConnect.outgoingBandwidth);

       ProtoNotifyConnect(host, peer, @event);
       return 0;
   }

   

   public int 
   ProtoReceiveIncomingCommands(ENetEvent* @event)
   {
       int packets;

       for (packets = 0; packets < 256; ++packets)
       {
           int receivedLength;
           ENetBuffer buffer;

           buffer.data = this.packetData[0];
           buffer.dataLength = sizeof(host ->packetData[0]);

   receivedLength = enetSocketReceive(this.socket,
                                         &this.receivedAddress,
                                         &buffer,
                                         1);

   if (receivedLength < 0)
       return -1;

   if (receivedLength == 0)
       return 0;

   this.receivedData = this.packetData[0];
   this.receivedDataLength = receivedLength;

   this.totalReceivedData += receivedLength;
   this.totalReceivedPackets++;

   if (this.intercept != null)
   {
       switch (this.intercept(host, @event))
       {
           case 1:
               if (@event != null && @event.type != ENetEventType.None)
                  return 1;

               continue;

           case -1:
               return -1;

           default:
               break;
       }
   }

   switch (ProtoHandleIncomingCommands(host, @event))
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

   public void 
   ProtoSend_acknowledgements(ENetPeer peer)
   {
       ENetProto* command = &this.commands[this.commandCount];
       ENetBuffer* buffer = &this.buffers[this.bufferCount];
       ENetAcknowledgement* acknowledgement;
       ENetListIterator currentAcknowledgement;
       uint reliableSequenceNumber;

       currentAcknowledgement = enetList_begin(&peer.acknowledgements);

       while (currentAcknowledgement != enetListEnd(&peer.acknowledgements))
       {
           if (command >= &this.commands[sizeof(host ->commands) / sizeof(ENetProto)] ||
        buffer >= &this.buffers[sizeof(host ->buffers) / sizeof(ENetBuffer)] ||
   peer.mtu - this.packetSize < sizeof(ENetProtoAcknowledge))
          {
       this.continueSending = 1;

       break;
   }

   acknowledgement = (ENetAcknowledgement*)currentAcknowledgement;

   currentAcknowledgement = enetListNext(currentAcknowledgement);

   buffer->data = command;
   buffer->dataLength = sizeof(ENetProtoAcknowledge);

   this.packetSize += buffer->dataLength;

   reliableSequenceNumber = (uint)IPAddress.HostToNetworkOrder(acknowledgement->command.header.reliableSequenceNumber);

   command.header.command = (int)ENetProtoCmdType.Ack;
   command.header.channelID = acknowledgement->command.header.channelID;
   command.header.reliableSequenceNumber = reliableSequenceNumber;
   command.acknowledge.receivedReliableSequenceNumber = reliableSequenceNumber;
   command.acknowledge.receivedSentTime = (uint)IPAddress.HostToNetworkOrder(acknowledgement->sentTime);

   if ((acknowledgement->command.header.command & (int)ENetProtoCmdType.Mask) == (int)ENetProtoCmdType.Disconnect)
       ProtoDispatchState(host, peer, (int)ENetPeerState.Zombie);

   enetListRemove(&acknowledgement->acknowledgementList);
   enet_free(acknowledgement);

   ++command;
   ++buffer;
       }

       this.commandCount = command - this.commands;
   this.bufferCount = buffer - this.buffers;
   }

   public int 
   ProtoCheck_timeouts(ENetPeer peer, ENetEvent* @event)
   {
       ENetOutCmd outgoingCommand;
       ENetListIterator currentCommand, insertPosition;

       currentCommand = enetList_begin(&peer.sentReliableCommands);
       insertPosition = enetList_begin(&peer.outgoingCommands);

       while (currentCommand != enetListEnd(&peer.sentReliableCommands))
       {
           outgoingCommand = (ENetOutCmd)currentCommand;

           currentCommand = enetListNext(currentCommand);

           if (ENet_TIMEDIFFERENCE(this.serviceTime, outgoingCommand.sentTime) < outgoingCommand.roundTripTimeout)
               continue;

           if (peer.earliestTimeout == 0 ||
               ENet_TIMELESS(outgoingCommand.sentTime, peer.earliestTimeout))
               peer.earliestTimeout = outgoingCommand.sentTime;

           if (peer.earliestTimeout != 0 &&
                 (ENet_TIMEDIFFERENCE(this.serviceTime, peer.earliestTimeout) >= peer.timeoutMaximum ||
                   (outgoingCommand.roundTripTimeout >= outgoingCommand.roundTripTimeoutLimit &&
                     ENet_TIMEDIFFERENCE(this.serviceTime, peer.earliestTimeout) >= peer.timeoutMinimum)))
           {
               ProtoNotifyDisconnect(host, peer, @event);

               return 1;
           }

           if (outgoingCommand.packet != null)
               peer.reliableDataInTransit -= outgoingCommand.fragmentLength;

           ++peer.packetsLost;

           outgoingCommand.roundTripTimeout *= 2;

           enetListInsert(insertPosition, enetListRemove(&outgoingCommand.outgoingCommandList));

           if (currentCommand == enetList_begin(&peer.sentReliableCommands) &&
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

       currentCommand = enetList_begin(&peer.outgoingCommands);

       while (currentCommand != enetListEnd(&peer.outgoingCommands))
       {
           outgoingCommand = (ENetOutCmd)currentCommand;

           if (outgoingCommand.command.header.command & (int)ENetProtoFlag.CmdFlagAck)
           {
               channel = outgoingCommand.command.header.channelID < peer.channelCount ? &peer.channels[outgoingCommand.command.header.channelID] : null;
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

           commandSize = commandSizes[outgoingCommand.command.header.command & (int)ENetProtoCmdType.Mask];
           if (command >= &this.commands[sizeof(host ->commands) / sizeof(ENetProto)] ||
        buffer + 1 >= &this.buffers[sizeof(host ->buffers) / sizeof(ENetBuffer)] ||
   peer.mtu - this.packetSize < commandSize ||
   (outgoingCommand.packet != null &&
   (uint)(peer.mtu - this.packetSize) < (uint)(commandSize + outgoingCommand.fragmentLength)))
          {
       this.continueSending = 1;

       break;
   }

   currentCommand = enetListNext(currentCommand);

   if (outgoingCommand.command.header.command & (int)ENetProtoFlag.CmdFlagAck)
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
                   enet_free(outgoingCommand);

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
   if (!(outgoingCommand.command.header.command & (int)ENetProtoFlag.CmdFlagAck))
       enet_free(outgoingCommand);

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
   ProtoSendOutgoingCommands(ENetEvent* @event, int checkForTimeouts)
   {
       uint headerData[sizeof(ENetProtoHeader) + sizeof(uint)];
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
               this.packetSize = sizeof(ENetProtoHeader);

               if (!enetListEmpty(&currentPeer.acknowledgements))
                   ProtoSend_acknowledgements(host, currentPeer);

               if (checkForTimeouts != 0 &&
                   !enetListEmpty(&currentPeer.sentReliableCommands) &&
                   ENet_TIME_GREATEREQUAL(this.serviceTime, currentPeer.nextTimeout) &&
                   ProtoCheck_timeouts(host, currentPeer, @event) == 1)
               {
                   if (@event != null && @event.type != ENetEventType.None)
                 return 1;
               else
       continue;
           }

           if ((enetListEmpty(&currentPeer.outgoingCommands) ||
                 ProtoCheckOutgoingCommands(host, currentPeer)) &&
               enetListEmpty(&currentPeer.sentReliableCommands) &&
               ENet_TIMEDIFFERENCE(this.serviceTime, currentPeer.lastReceiveTime) >= currentPeer.pingInterval &&
               currentPeer.mtu - this.packetSize >= sizeof(ENetProtoPing))
   {
       enetPeerPing(currentPeer);
       ProtoCheckOutgoingCommands(host, currentPeer);
   }

   if (this.commandCount == 0)
       continue;

   if (currentPeer.packetLossEpoch == 0)
       currentPeer.packetLossEpoch = this.serviceTime;
   else
   if (ENet_TIMEDIFFERENCE(this.serviceTime, currentPeer.packetLossEpoch) >= (uint)ENetDef.PeerPacketLossInterval &&
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
       header->sentTime = (uint)IPAddress.HostToNetworkOrder(this.serviceTime & 0xFFFF);

       this.buffers->dataLength = sizeof(ENetProtoHeader);
   }
   else
       this.buffers->dataLength = (uint) & ((ENetProtoHeader*)0)->sentTime;

   shouldCompress = 0;
   if (this.compressor.context != null && this.compressor.compress != null)
   {
       uint originalSize = this.packetSize - sizeof(ENetProtoHeader),
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
   header->peerID = (uint)IPAddress.HostToNetworkOrder(currentPeer.outgoingPeerID | this.headerFlags);
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

   ProtoRemoveSent_unreliableCommands(currentPeer);

   if (sentLength < 0)
       return -1;

   this.totalSentData += sentLength;
   this.totalSentPackets++;
       }

       return 0;
   }


   int
   enetHostCheckEvents(ENetEvent* @event)
   {
       if (@event == null) return -1;

   @event.type = ENetEventType.None;
       @event.peer = null;
       @event.packet = null;

       return ProtoDispatchIncomingCommands (host, @event);
   }

   int
   enetHostService(ENetEvent* @event, uint timeout)
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

   this.serviceTime = enet_time_get();

   timeout += this.serviceTime;

   do
   {
       if (ENet_TIMEDIFFERENCE(this.serviceTime, this.bandwidthThrottleEpoch) >= (uint)ENetDef.HostBandwidthThrottleInterval)
           enetHost_bandwidth_throttle(host);

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

       if (ENet_TIME_GREATEREQUAL(this.serviceTime, timeout))
           return 0;

       do
       {
           this.serviceTime = enet_time_get();

           if (ENet_TIME_GREATEREQUAL(this.serviceTime, timeout))
               return 0;

           waitCondition = (int)ENetSocketWait.Recv | (int)ENetSocketWait.Interrupt;

           if (enetSocket_wait(this.socket, &waitCondition, ENet_TIMEDIFFERENCE(timeout, this.serviceTime)) != 0)
               return -1;
       }
       while (waitCondition & (int)ENetSocketWait.Interrupt);

       this.serviceTime = enet_time_get();
   } while (waitCondition & (int)ENetSocketWait.Recv);

   return 0; 
   }



   */
    #endregion


}
