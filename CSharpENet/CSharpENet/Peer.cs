using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace ENet;

class ENetPeer
{

    // ENetListNode dispatchList;
    //public ENetHost? host;
    public uint outPeerID;
    public uint inPeerID;
    public uint connectID;
    public uint outSessionID;
    public uint inSessionID;
    public IPEndPoint? address;
    public byte[]? data;               /**< Application private data, may be freely modified */
    public ENetPeerState state;
    public ENetChannel[]? channels;
    public int channelCount 
    {
        get 
        {
            return this.channels == null ? 0 : this.channels.Length;
        }
    }
    public uint inBandwidth;  /**< Downstream bandwidth of the client in bytes/second */
    public uint outBandwidth;  /**< Upstream bandwidth of the client in bytes/second */
    public uint inBandwidthThrottleEpoch;
    public uint outBandwidthThrottleEpoch;
    public int inDataTotal;
    public int outDataTotal;
    public long lastSendTime;
    public long lastReceiveTime;
    public long nextTimeout;
    public long earliestTimeout;
    public long packetLossEpoch;
    public uint packetsSent;
    public uint packetsLost;
    public long packetLoss;          /**< mean packet loss of reliable packets as a ratio with respect to the constant ENET_PEER_PACKET_LOSS_SCALE */
    public long packetLossVariance;
    public uint packetThrottle;
    public uint packetThrottleLimit;
    public uint packetThrottleCounter;
    public long packetThrottleEpoch;
    public uint packetThrottleAcceleration;
    public uint packetThrottleDeceleration;
    public uint packetThrottleInterval;
    public uint pingInterval;
    public uint timeoutLimit;
    public long timeoutMinimum;
    public long timeoutMaximum;
    public long lastRoundTripTime;
    public long lowestRoundTripTime;
    public long lastRTTVariance;
    public long highestRoundTripTimeVariance;
    public long rtt;            /**< mean round trip time (RTT), in milliseconds, between sending a reliable packet and receiving its acknowledgement */
    public long rttVariance;
    public uint mtu;
    public uint windowSize;
    public uint reliableDataInTransit;
    public uint outReliableSeqNum;
    public LinkedList<ENetAckCmd> acknowledgements = new();
    public LinkedList<ENetOutCmd> sentReliableCmds = new();
    public LinkedList<ENetOutCmd> sentUnreliableCmds = new();
    public LinkedList<ENetOutCmd> outCmds = new();
    public LinkedList<ENetInCmd> dispatchedCmnds = new();
    public bool needDispatch = false;//flags
    public uint reserved;
    public uint inUnseqGroup;
    public uint outUnSeqGroup;
    public uint[] unseqWindow = new uint[ENetDef.PeerUnseqWindowSize / 32];
    public uint eventData;
    public uint totalWaitingData;

    //TODO:直接Clear不需要进行函数调用
    public void ResetCmds(LinkedList<ENetOutCmd> list)
    {
        list.Clear();
        GC.Collect();
    }

    public void RemoveInCmds(LinkedList<ENetInCmd> list, ENetInCmd? startCmd, ENetInCmd? endCmd, ENetInCmd excludeCmd)
    {
        if (list == null || startCmd == null || endCmd == null) return;
        if (list.Count == 0) return;

        for (LinkedListNode<ENetInCmd>? currNode = list.First; currNode != null && currNode.Value.Equals(endCmd); currNode = currNode?.Next)
        {
            if (currNode.Value.Equals(excludeCmd))
            {
                continue;
            }

            currNode = currNode.Previous;
            if (currNode != null && currNode.Next != null)
            {
                list.Remove(currNode.Next);
            }
        }

    }

    //TODO：要把自己从dispatchList里remove。看谁调用，从谁哪里remove
    public void ResetQueues()
    {
        //TODO: remove(this->dispatchList);
        needDispatch = false;

        acknowledgements.Clear();
        sentReliableCmds.Clear();
        sentUnreliableCmds.Clear();
        outCmds.Clear();
        dispatchedCmnds.Clear();

        channels = null;
    }

    public void OnConnect()
    {
        if (state != ENetPeerState.Connected && state != ENetPeerState.DisconnectLater)
        {
            if (inBandwidth != 0)
                ++ENetHost.Instance.bandwidthLimitedPeers;

            ++ENetHost.Instance.connectedPeers;
        }
    }
    public void OnDisconnect()
    {
        if (state == ENetPeerState.Connected || state == ENetPeerState.DisconnectLater)
        {
            if (inBandwidth != 0)
            {
                ENetHost.Instance.bandwidthLimitedPeers--;
            }

            ENetHost.Instance.connectedPeers--;
        }
    }

    //TODO：这个函数可能应该交给channel
    public void DispatchInUnreliableCmds(ENetChannel channel, ENetInCmd queuedCmd)
    {
        if (channel.inUnreliableCmds.Count == 0) return;
        if (channel.inUnreliableCmds == null) return;

        LinkedListNode<ENetInCmd>? startCmd = channel.inUnreliableCmds.First;
        LinkedListNode<ENetInCmd>? droppedCmd = startCmd;
        LinkedListNode<ENetInCmd>? currentCmd = startCmd;

        if (startCmd == null || currentCmd == null) return;

        for (;
             currentCmd != null && !ReferenceEquals(currentCmd, channel.inUnreliableCmds.Last?.Next);
             currentCmd = currentCmd.Next)//Last.Next不知道最后一个会不会执行
        {
            if (currentCmd == null) break;

            ENetInCmd inCmd = currentCmd.Value;

            if ((inCmd.cmdHeader.cmdFlag & (int)ENetProtoCmdType.Mask) == (int)ENetProtoCmdType.SendUnseq)
                continue;

            if (inCmd.reliableSeqNum  == channel.inReliableSeqNum)
            {
                if (inCmd.fragmentsRemaining <= 0)
                {
                    channel.inUnreliableSeqNum = inCmd.unreliableSeqNum;
                    continue;
                }

                if (startCmd != currentCmd)
                {
                    dispatchedCmnds.AddLastRange(startCmd, currentCmd.Previous);

                    if (!needDispatch)
                    {
                        ENetHost.Instance.dispatchQueue.AddLast(this);
                        needDispatch = true;
                    }

                    droppedCmd = currentCmd;
                }
                else
                if (droppedCmd != currentCmd)
                    droppedCmd = currentCmd.Previous;
            }
            else
            {
                ushort reliableWindow = (ushort)(inCmd.reliableSeqNum  / (ushort)ENetDef.PeerReliableWindowSize),
                            currentWindow = (ushort)(channel.inReliableSeqNum / (ushort)ENetDef.PeerReliableWindowSize);
                if (inCmd.reliableSeqNum  < channel.inReliableSeqNum)
                    reliableWindow += (ushort)ENetDef.PeerReliableWindows;
                if (reliableWindow >= currentWindow && reliableWindow < currentWindow + (ushort)ENetDef.PeerFreeReliableWindows - 1)
                    break;

                droppedCmd = currentCmd.Next;

                if (startCmd != currentCmd)
                {
                    dispatchedCmnds.AddLastRange(startCmd, currentCmd.Previous);

                    if (!needDispatch)
                    {
                        ENetHost.Instance.dispatchQueue.AddLast(this);
                        needDispatch = true;
                    }
                }
            }
        }

        if (startCmd != currentCmd)
        {
            dispatchedCmnds.AddLastRange(startCmd, currentCmd?.Previous);

            if (!needDispatch)
            {
                ENetHost.Instance.dispatchQueue.AddLast(this);
                needDispatch = true;
            }

            droppedCmd = currentCmd;
        }

        RemoveInCmds(channel.inUnreliableCmds, channel.inUnreliableCmds.First?.Value, droppedCmd?.Value, queuedCmd);
    }

    public void DispatchInReliableCmds(ENetChannel channel, ENetInCmd queuedCmd)
    {
        LinkedListNode<ENetInCmd>? currentCmd = channel.inReliableCmds.First;
        LinkedListNode<ENetInCmd>? startCmd = currentCmd;
        if (startCmd == null) return;

        for (;
             currentCmd != null;
             currentCmd = currentCmd?.Next)//Last.Next不知道最后一个会不会执行
        {
            if (currentCmd.Value.fragmentsRemaining > 0 ||
                currentCmd.Value.reliableSeqNum  != channel.inReliableSeqNum + 1)
                break;

            channel.inReliableSeqNum = currentCmd.Value.reliableSeqNum ;

            if (currentCmd.Value.fragmentCount > 0)
                channel.inReliableSeqNum += currentCmd.Value.fragmentCount - 1;
        }

        if (currentCmd == null) return;

        channel.inUnreliableSeqNum = 0;
        dispatchedCmnds.AddLastRange(startCmd, currentCmd.Previous);

        if (!this.needDispatch)
        {
            ENetHost.Instance.dispatchQueue.AddLast(this);
            needDispatch = true;
        }

        DispatchInUnreliableCmds(channel, queuedCmd);
    }

    public void QueueAck(ENetProtoCmdHeader cmdHeader, uint sentTime)
    {
        if (cmdHeader.channelID < channels?.Length)
        {
            ENetChannel channel = channels[cmdHeader.channelID];
            uint reliableWindow = cmdHeader.reliableSeqNum / Convert.ToUInt32(ENetDef.PeerReliableWindowSize),
                        currentWindow = channel.inReliableSeqNum / Convert.ToUInt32(ENetDef.PeerReliableWindowSize);

            if (cmdHeader.reliableSeqNum < channel.inReliableSeqNum)
                reliableWindow += Convert.ToUInt32(ENetDef.PeerReliableWindows);

            if (reliableWindow >= currentWindow + Convert.ToUInt32(ENetDef.PeerReliableWindows) - 1 && reliableWindow <= currentWindow + Convert.ToUInt32(ENetDef.PeerReliableWindows))
                return;
        }

        ENetAckCmd ack = new ENetAckCmd();
        ack.sentTime = sentTime;
        ack.cmdHeader = cmdHeader;

        this.outDataTotal += Marshal.SizeOf<ENetAckCmd>();

        acknowledgements.AddLast(ack);

    }

    public void QueueOutgoingCommand(ENetProto cmd, ENetPacket? packet, uint offset, uint length)
    {
        ENetOutCmd outCmd = new();
        outCmd.cmd = cmd;
        outCmd.fragmentOffset = offset;
        outCmd.fragmentLength = length;
        outCmd.packet = packet;

        SetupOutCmd(outCmd);
    }

    public void SetupOutCmd(ENetOutCmd outCmd)
    {
        unsafe
        {
            outDataTotal += (int)ENetProtoCmdSize.CmdSize[Convert.ToInt32(outCmd.cmdHeader.cmdFlag&(int)ENetProtoCmdType.Mask)] + (int)outCmd.fragmentLength;
        }

        if (outCmd.cmdHeader.channelID == 0xFF)
        {
            ++outReliableSeqNum;

            outCmd.reliableSeqNum = outReliableSeqNum;
            outCmd.unreliableSeqNum = 0;
        }
        else
        {
            ENetChannel channel = channels[outCmd.cmdHeader.channelID];

            if ((outCmd.cmdHeader.cmdFlag & (int)ENetProtoFlag.CmdFlagAck) != 0)
            {
                ++channel.outReliableSeqNum;
                channel.outUnreliableSeqNum = 0;

                outCmd.reliableSeqNum = channel.outReliableSeqNum;
                outCmd.unreliableSeqNum = 0;
            }
            else
            {
                if ((outCmd.cmdHeader.cmdFlag & (int)ENetProtoFlag.CmdFlagUnSeq) != 0)
                {
                    ++outUnSeqGroup;

                    outCmd.reliableSeqNum = 0;
                    outCmd.unreliableSeqNum = 0;
                }
                else
                {
                    if (outCmd.fragmentOffset == 0)
                        ++channel.outUnreliableSeqNum;

                    outCmd.reliableSeqNum = channel.outReliableSeqNum;
                    outCmd.unreliableSeqNum = channel.outUnreliableSeqNum;
                }
            }

        }

        outCmd.sendAttempts = 0;
        outCmd.sentTime = 0;
        outCmd.rttTimeout = 0;
        outCmd.rttTimeoutLimit = 0;
        outCmd.cmdHeader.reliableSeqNum = Utils.HostToNetOrder(outCmd.reliableSeqNum);

        switch (outCmd.cmdHeader.cmdFlag & (int)ENetProtoCmdType.Mask)
        {
            case (int)ENetProtoCmdType.SendUnreliable:
                outCmd.cmd.sendUnReliable.unreliableSeqNum = Utils.HostToNetOrder(outCmd.unreliableSeqNum);
                break;

            case (int)ENetProtoCmdType.SendUnseq:
                outCmd.cmd.sendUnseq.unseqGroup = Utils.HostToNetOrder(outUnSeqGroup);
                break;

            default:
                break;
        }
        outCmds.AddLast(outCmd);
    }

    public ENetInCmd? QueueInCmd(ENetProtoCmdHeader cmdHeader, byte[]? data, uint dataLength, int flags, uint fragmentCount, uint sendUnreliableSeqNum=0)
    {
        ENetInCmd dummyCmd = new();

        if (channels == null) return null;
        ENetChannel channel = channels[cmdHeader.channelID];
        uint unreliableSeqNum = 0, reliableSeqNum = 0;
        uint reliableWindow, currentWindow;
        ENetInCmd inCmd;
        LinkedListNode<ENetInCmd>? currCmd;
        ENetPacket packet;

        if (state == ENetPeerState.DisconnectLater)
            goto discardcmd;

        if ((cmdHeader.cmdFlag & (int)ENetProtoCmdType.SendUnseq) != 0)
        {
            reliableSeqNum = cmdHeader.reliableSeqNum;
            reliableWindow = reliableSeqNum / ENetDef.PeerReliableWindowSize;
            currentWindow = channel.inReliableSeqNum / ENetDef.PeerReliableWindowSize;

            if (reliableSeqNum < channel.inReliableSeqNum)
                reliableWindow += ENetDef.PeerReliableWindows;

            if (reliableWindow < currentWindow || reliableWindow >= currentWindow + ENetDef.PeerReliableWindows - 1)
                goto discardcmd;
        }


        switch (cmdHeader.cmdFlag & (int)ENetProtoCmdType.Mask)
        {
            case (int)ENetProtoCmdType.SendFragment:
            case (int)ENetProtoCmdType.SendReliable:
                if (reliableSeqNum == channel.inReliableSeqNum)
                    goto discardcmd;

                for (currCmd = channel.inReliableCmds.Last;
                     currCmd != null;
                     currCmd = currCmd.Previous)
                {
                    inCmd = currCmd.Value;

                    if (reliableSeqNum >= channel.inReliableSeqNum)
                    {
                        if (inCmd.reliableSeqNum  < channel.inReliableSeqNum)
                            continue;
                    }
                    else
                    if (inCmd.reliableSeqNum  >= channel.inReliableSeqNum)
                        break;

                    if (inCmd.reliableSeqNum  <= reliableSeqNum)
                    {
                        if (inCmd.reliableSeqNum  < reliableSeqNum)
                            break;

                        goto discardcmd;
                    }
                }
                break;

            case (int)ENetProtoCmdType.SendUnreliable:
            case (int)ENetProtoCmdType.SendUnreliableFragment:
                unreliableSeqNum = Utils.NetToHostOrder(sendUnreliableSeqNum);
                
                if (reliableSeqNum == channel.inReliableSeqNum &&
                    unreliableSeqNum <= channel.inUnreliableSeqNum)
                    goto discardcmd;

                for (currCmd = channel.inUnreliableCmds.Last;
                     currCmd != null;
                     currCmd = currCmd.Previous)
                {
                    inCmd = currCmd.Value;

                    if ((cmdHeader.cmdFlag & (int)ENetProtoCmdType.Mask) == (int)ENetProtoCmdType.SendUnreliable)
                        continue;

                    if (reliableSeqNum >= channel.inReliableSeqNum)
                    {
                        if (inCmd.reliableSeqNum  < channel.inReliableSeqNum)
                            continue;
                    }
                    else
                    if (inCmd.reliableSeqNum  >= channel.inReliableSeqNum)
                        break;

                    if (inCmd.reliableSeqNum  < reliableSeqNum)
                        break;

                    if (inCmd.reliableSeqNum  > reliableSeqNum)
                        continue;

                    if (inCmd.unreliableSeqNum <= unreliableSeqNum)
                    {
                        if (inCmd.unreliableSeqNum < unreliableSeqNum)
                            break;

                        goto discardcmd;
                    }
                }
                break;

            case (int)ENetProtoCmdType.SendUnseq:
                currCmd = channel.inUnreliableCmds.Last;
                break;

            default:
                goto discardcmd;
        }

        if (totalWaitingData >= ENetHost.Instance.maximumWaitingData)
            goto notifyError;

        packet = new ENetPacket(data, flags);
        if (packet == null)
            goto notifyError;

        inCmd = new();

        inCmd.reliableSeqNum  = cmdHeader.reliableSeqNum;
        inCmd.unreliableSeqNum = unreliableSeqNum & 0xFFFF;
        inCmd.cmdHeader = cmdHeader;
        inCmd.fragmentCount = fragmentCount;
        inCmd.fragmentsRemaining = fragmentCount;
        inCmd.packet = packet;
        inCmd.fragments = null;

        if (fragmentCount > 0 && fragmentCount <= ENetDef.ProtoMaxFragmentCount)
            inCmd.fragments = new uint[(fragmentCount + 31) / 32];

        if (packet != null && packet.Data != null)
        {
            totalWaitingData += Convert.ToUInt32(packet.Data.Length);
        }

        if (currCmd != null)
        {
            channel.inReliableCmds.AddAfter(currCmd, inCmd);
        }

        switch (cmdHeader.cmdFlag & (int)ENetProtoCmdType.Mask)
        {
            case (int)ENetProtoCmdType.SendFragment:
            case (int)ENetProtoCmdType.SendReliable:
                DispatchInReliableCmds(channel, inCmd);
                break;
            default:
                DispatchInUnreliableCmds(channel, inCmd);
                break;
        }

        return inCmd;

    discardcmd:
        if (fragmentCount > 0)
            goto notifyError;

        return dummyCmd;

    notifyError:
        return null;
    }

    public int Send(uint channelID, ENetPacket packet)
    {
        ENetChannel channel;
        ENetProto cmd = new();
        uint fragmentLength;


        if (this.state != ENetPeerState.Connected ||
            channelID >= this.channels?.Length ||
            packet.DataLength > ENetHost.Instance.maximumPacketSize)
        {
            return -1;
        }

        channel = channels[Convert.ToInt32(channelID)];
        fragmentLength = mtu - Convert.ToUInt32(Marshal.SizeOf(new ENetProtoHeader()) + Marshal.SizeOf(new ENetProtoSendFragment()));
        
        //分片
        if (packet.DataLength > fragmentLength)
        {
            uint fragmentCount = (packet.DataLength + fragmentLength - 1) / fragmentLength,
                    fragmentNumber,
                    fragmentOffset;
            int cmdNum;
            uint startSequenceNumber; 
            List<ENetOutCmd> fragments = new();
            ENetOutCmd fragment;

            if (fragmentCount > ENetDef.ProtoMaxFragmentCount)
                return -1;

            if ((packet.Flags & ((int)ENetPacketFlag.UnreliableFragment | (int)ENetPacketFlag.Reliable)) == (int)ENetPacketFlag.UnreliableFragment &&
                channel.outUnreliableSeqNum < 0xFFFF)
            {
                cmdNum = (int)ENetProtoCmdType.SendUnreliableFragment;
                startSequenceNumber = (uint)IPAddress.HostToNetworkOrder(channel.outUnreliableSeqNum + 1);
            }
            else
            {
                cmdNum = (int)ENetProtoCmdType.SendFragment | (int)ENetProtoFlag.CmdFlagAck;
                startSequenceNumber = (uint)IPAddress.HostToNetworkOrder(channel.outUnreliableSeqNum + 1);
            }
        
            for (fragmentNumber = 0,
                    fragmentOffset = 0;
                fragmentOffset < packet.DataLength;
                ++ fragmentNumber,
                    fragmentOffset += fragmentLength)
            {
                if (packet.DataLength - fragmentOffset < fragmentLength)
                fragmentLength = packet.DataLength - fragmentOffset;

                fragment = new();
                         
                fragment.fragmentOffset = fragmentOffset;
                fragment.fragmentLength = fragmentLength;
                fragment.packet = packet;
                fragment.cmdHeader.cmdFlag = cmdNum;
                fragment.cmdHeader.channelID = channelID;
                fragment.cmd.sendFragment.startSeqNum = startSequenceNumber;
                fragment.cmd.sendFragment.dataLength = (uint)IPAddress.HostToNetworkOrder(fragmentLength);
                fragment.cmd.sendFragment.fragmentCount = (uint)IPAddress.HostToNetworkOrder (fragmentCount);
                fragment.cmd.sendFragment.fragmentNum = (uint)IPAddress.HostToNetworkOrder (fragmentNumber);
                fragment.cmd.sendFragment.totalLength = (uint)IPAddress.HostToNetworkOrder (packet.DataLength);
                fragment.cmd.sendFragment.fragmentOffset = (uint)IPAddress.NetworkToHostOrder(fragmentOffset);

                fragments.Add(fragment);
            }

            while (fragments.Count > 0)
            {
                fragment = fragments[0];
                fragments.RemoveAt(0);

                SetupOutCmd(fragment);
            }

            return 0;
        }

        //不用分片的

        cmd.header.channelID = channelID;

        if ((packet.Flags & ((int)ENetPacketFlag.Reliable | (int)ENetPacketFlag.UnSeq)) == (int)ENetPacketFlag.UnSeq)
        {
            cmd.header.cmdFlag = (int)ENetProtoCmdType.SendUnseq | (int)ENetProtoFlag.CmdFlagUnSeq;
            cmd.sendUnseq.dataLength = (uint)IPAddress.HostToNetworkOrder(packet.DataLength);
        }
        else
        {
            if ((packet.Flags & (int)ENetPacketFlag.Reliable) != 0 || channel.outUnreliableSeqNum >= 0xFFFF)
            {
                cmd.header.cmdFlag = (int)ENetProtoCmdType.SendReliable | (int)ENetProtoFlag.CmdFlagAck;
                cmd.sendReliable.dataLength = (uint)IPAddress.HostToNetworkOrder(packet.DataLength);
            }
            else
            {
                cmd.header.cmdFlag = (int)ENetProtoCmdType.SendReliable;
                cmd.sendReliable.dataLength = (uint)IPAddress.HostToNetworkOrder(packet.DataLength);
            }
        }

        QueueOutgoingCommand(cmd, packet, 0, packet.DataLength);
        
        return 0;
    }

    public ENetPacket? Receive(ref uint channelID)
    {
           ENetInCmd inCmd;
           ENetPacket? packet;
   
           if (this.dispatchedCmnds.Count == 0 || this.dispatchedCmnds.First == null)
             return null;

           inCmd = this.dispatchedCmnds.First.Value;

           channelID = inCmd.cmdHeader.channelID;

           packet = inCmd.packet;

           this.totalWaitingData -= packet.DataLength;
        
        return packet;
    }

    public void Reset()
    {
        OnDisconnect();

        this.outPeerID = ENetDef.ProtoMaxPeerID;
        this.connectID = 0;

        this.state = ENetPeerState.Disconnected;

        this.inBandwidth = 0;
        this.outBandwidth = 0;
        this.inBandwidthThrottleEpoch = 0;
        this.outBandwidthThrottleEpoch = 0;
        this.inDataTotal = 0;
        this.outDataTotal = 0;
        this.lastSendTime = 0;
        this.lastReceiveTime = 0;
        this.nextTimeout = 0;
        this.earliestTimeout = 0;
        this.packetLossEpoch = 0;
        this.packetsSent = 0;
        this.packetsLost = 0;
        this.packetLoss = 0;
        this.packetLossVariance = 0;
        this.packetThrottle = ENetDef.PeerDefaultPacketThrottle;
        this.packetThrottleLimit = ENetDef.PeerPacketThrottleScale;
        this.packetThrottleCounter = 0;
        this.packetThrottleEpoch = 0;
        this.packetThrottleAcceleration = ENetDef.PeerPacketThrottleAcceleration;
        this.packetThrottleDeceleration = ENetDef.PeerPacketThrottleDeceleration;
        this.packetThrottleInterval = ENetDef.PeerPacketThrottleInterval;
        this.pingInterval = ENetDef.PeerPingInterval;
        this.timeoutLimit = ENetDef.PeerTimeoutLimit;
        this.timeoutMinimum = ENetDef.PeerTimeoutMin;
        this.timeoutMaximum = ENetDef.PeerTimeoutMax; 
        this.lastRoundTripTime = ENetDef.PeerDefaultRTT;
        this.lowestRoundTripTime = ENetDef.PeerDefaultRTT;
        this.lastRTTVariance = 0;
        this.highestRoundTripTimeVariance = 0;
        this.rtt = ENetDef.PeerDefaultRTT;
        this.rttVariance = 0;
        this.mtu = ENetHost.Instance.mtu;
        this.reliableDataInTransit = 0;
        this.outReliableSeqNum = 0;
        this.windowSize = ENetDef.ProtoMaxWindowSize;
        this.inUnseqGroup = 0;
        this.outUnSeqGroup = 0;
        this.eventData = 0;
        this.totalWaitingData = 0;
        this.needDispatch = false;

        Array.Clear(this.unseqWindow);

        ResetQueues();
    }

    public void Ping()
    {
            ENetProto command = new ENetProto();

            if (this.state != ENetPeerState.Connected)
                return;

            command.header.cmdFlag = (int)ENetProtoCmdType.Ping | (int)ENetProtoFlag.CmdFlagAck;
            command.header.channelID = 0xFF;

            QueueOutgoingCommand(command, null, 0, 0);
    }

    public void PingInterval(uint pingInterval)
    {
        this.pingInterval = pingInterval != 0 ? pingInterval : ENetDef.PeerPingInterval;
    }

    public void Timeout(uint timeoutLimit, uint timeoutMinimum, uint timeoutMaximum)
    {
        this.timeoutLimit = timeoutLimit !=0 ? timeoutLimit : ENetDef.PeerTimeoutLimit;
        this.timeoutMinimum = timeoutMinimum != 0 ? timeoutMinimum : ENetDef.PeerTimeoutMin;
        this.timeoutMaximum = timeoutMaximum != 0 ? timeoutMaximum : ENetDef.PeerTimeoutMax;
    }

    public void Disconnect(uint data)
    {
        ENetProto command = new ENetProto();

        if (this.state == ENetPeerState.Disconnecting ||
            this.state == ENetPeerState.Disconnected ||
            this.state == ENetPeerState.AckDisconnect ||
            this.state == ENetPeerState.Zombie)
            return;

        ResetQueues();

        command.header.cmdFlag = (int)ENetProtoCmdType.Disconnect;
        command.header.channelID = 0xFF;
        command.disconnect.data = (uint)IPAddress.HostToNetworkOrder(data);

        if (this.state == ENetPeerState.Connected || this.state == ENetPeerState.DisconnectLater)
            command.header.cmdFlag |= (int)ENetProtoFlag.CmdFlagAck;
        else
            command.header.cmdFlag |= (int)ENetProtoFlag.CmdFlagUnSeq;

        QueueOutgoingCommand(command, null, 0, 0);

        if (this.state == ENetPeerState.Connected || this.state == ENetPeerState.DisconnectLater)
        {
            OnDisconnect();

            this.state = ENetPeerState.Disconnecting;
        }
        else
        {
            ENetHost.Instance.Flush();
            Reset();
        }
    }

    public void DisconnectNow(uint data)
    {
        ENetProto command = new ENetProto();

        if (this.state == ENetPeerState.Disconnected)
            return;

        if (this.state != ENetPeerState.Zombie &&
            this.state != ENetPeerState.Disconnecting)
        {
            ResetQueues();

            command.header.cmdFlag = (int)ENetProtoCmdType.Disconnect | (int)ENetProtoFlag.CmdFlagUnSeq;
            command.header.channelID = 0xFF;
            command.disconnect.data = (uint)IPAddress.HostToNetworkOrder(data); 

            QueueOutgoingCommand(command, null, 0, 0);

            ENetHost.Instance.Flush();
        }

        Reset();
    }

    public void DisconnectLater(uint data)
    {
        if ((this.state == ENetPeerState.Connected || this.state == ENetPeerState.DisconnectLater) &&
            this.outCmds.Count != 0 && this.sentReliableCmds.Count == 0)
        {
            this.state = ENetPeerState.DisconnectLater;
            this.eventData = data;
        }
        else
            Disconnect(data);
    }

    public void ThrottleConfigure(uint interval, uint acceleration, uint deceleration)
    {
        ENetProto command = new ENetProto();

        this.packetThrottleInterval = interval;
        this.packetThrottleAcceleration = acceleration;
        this.packetThrottleDeceleration = deceleration;

        command.header.cmdFlag = (int)ENetProtoCmdType.ThrottleConfig | (int)ENetProtoFlag.CmdFlagAck;
        command.header.channelID = 0xFF;

        command.throttleConfigure.packetThrottleInterval = (uint)IPAddress.HostToNetworkOrder(interval);
        command.throttleConfigure.packetThrottleAcceleration = (uint)IPAddress.HostToNetworkOrder(acceleration);
        command.throttleConfigure.packetThrottleDeceleration = (uint)IPAddress.HostToNetworkOrder(deceleration);

        QueueOutgoingCommand(command, null, 0, 0);
    }
    
    public int Throttle(long rtt)
    {
        if (this.lastRoundTripTime <= this.lastRTTVariance)
        {
            this.packetThrottle = this.packetThrottleLimit;
        }
        else
        if (rtt <= this.lastRoundTripTime)
        {
            this.packetThrottle += this.packetThrottleAcceleration;

            if (this.packetThrottle > this.packetThrottleLimit)
                this.packetThrottle = this.packetThrottleLimit;

            return 1;
        }
        else
        if (rtt > this.lastRoundTripTime + 2 * this.lastRTTVariance)
        {
            if (this.packetThrottle > this.packetThrottleDeceleration)
                this.packetThrottle -= this.packetThrottleDeceleration;
            else
                this.packetThrottle = 0;

            return -1;
        }

        return 0;
    }

    
}


enum ENetPeerState
{
    Disconnected,
    Connecting,
    AckConnect,
    ConnectionPending,
    ConnectionSucceed,
    Connected,
    DisconnectLater,
    Disconnecting,
    AckDisconnect,
    Zombie,
};