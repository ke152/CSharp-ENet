using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace ENet;

class ENetPeer
{

    // ENetListNode dispatchList;
    public ENetHost? host;
    public uint outgoingPeerID;
    public uint incomingPeerID;
    public uint connectID;
    public uint outgoingSessionID;
    public uint incomingSessionID;
    // ENetAddress address;            /**< Internet address of the peer */
    // void* data;               /**< Application private data, may be freely modified */
    public ENetPeerState state;
    public List<ENetChannel> channels = new();
    public uint incomingBandwidth;  /**< Downstream bandwidth of the client in bytes/second */
    public uint outgoingBandwidth;  /**< Upstream bandwidth of the client in bytes/second */
    public uint incomingBandwidthThrottleEpoch;
    public uint outgoingBandwidthThrottleEpoch;
    public uint incomingDataTotal;
    public uint outgoingDataTotal;
    public uint lastSendTime;
    public uint lastReceiveTime;
    public uint nextTimeout;
    public uint earliestTimeout;
    public uint packetLossEpoch;
    public uint packetsSent;
    public uint packetsLost;
    public uint packetLoss;          /**< mean packet loss of reliable packets as a ratio with respect to the constant ENET_PEER_PACKET_LOSS_SCALE */
    public uint packetLossVariance;
    public uint packetThrottle;
    public uint packetThrottleLimit;
    public uint packetThrottleCounter;
    public uint packetThrottleEpoch;
    public uint packetThrottleAcceleration;
    public uint packetThrottleDeceleration;
    public uint packetThrottleInterval;
    public uint pingInterval;
    public uint timeoutLimit;
    public uint timeoutMinimum;
    public uint timeoutMaximum;
    public uint lastRoundTripTime;
    public uint lowestRoundTripTime;
    public uint lastRoundTripTimeVariance;
    public uint highestRoundTripTimeVariance;
    public uint roundTripTime;            /**< mean round trip time (RTT), in milliseconds, between sending a reliable packet and receiving its acknowledgement */
    public uint roundTripTimeVariance;
    public uint mtu;
    public uint windowSize;
    public uint reliableDataInTransit;
    public uint outReliableSeqNum;
    public LinkedList<ENetAckCmd> ackCmds = new();
    public LinkedList<ENetOutCmd> sentReliableCmds = new();
    public LinkedList<ENetOutCmd> sentUnreliableCmds = new();
    public LinkedList<ENetOutCmd> outCmds = new();
    public LinkedList<ENetInCmd> dispatchedCmds = new();
    public bool needDispatch = false;//flags
    public uint reserved;
    public uint incomingUnsequencedGroup;
    public uint outUnSeqGroup;
    public uint[] unsequencedWindow = new uint[ENetDef.PeerUnseqWindowSize / 32];
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

        ackCmds.Clear();
        sentReliableCmds.Clear();
        sentUnreliableCmds.Clear();
        outCmds.Clear();
        dispatchedCmds.Clear();

        channels.Clear();
    }

    public void OnConnect(ref uint hostBandwidthLimitedPeers, ref uint hostConnectedPeers)
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

            if ((inCmd.cmd.header.cmdFlag & (int)ENetProtoCmdType.Mask) == (int)ENetProtoCmdType.SendUnseq)
                continue;

            if (inCmd.reliableSeqNum == channel.inReliableSeqNum)
            {
                if (inCmd.fragmentsRemaining <= 0)
                {
                    channel.inUnreliableSeqNum = inCmd.unreliableSeqNum;
                    continue;
                }

                if (startCmd != currentCmd)
                {
                    dispatchedCmds.AddLastRange(startCmd, currentCmd.Previous);

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
                ushort reliableWindow = (ushort)(inCmd.reliableSeqNum / (ushort)ENetDef.PeerReliableWindowSize),
                            currentWindow = (ushort)(channel.inReliableSeqNum / (ushort)ENetDef.PeerReliableWindowSize);
                if (inCmd.reliableSeqNum < channel.inReliableSeqNum)
                    reliableWindow += (ushort)ENetDef.PeerReliableWindows;
                if (reliableWindow >= currentWindow && reliableWindow < currentWindow + (ushort)ENetDef.PeerFreeReliableWindows - 1)
                    break;

                droppedCmd = currentCmd.Next;

                if (startCmd != currentCmd)
                {
                    dispatchedCmds.AddLastRange(startCmd, currentCmd.Previous);

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
            dispatchedCmds.AddLastRange(startCmd, currentCmd?.Previous);

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
                currentCmd.Value.reliableSeqNum != channel.inReliableSeqNum + 1)
                break;

            channel.inReliableSeqNum = currentCmd.Value.reliableSeqNum;

            if (currentCmd.Value.fragmentCount > 0)
                channel.inReliableSeqNum += currentCmd.Value.fragmentCount - 1;
        }

        if (currentCmd == null) return;

        channel.inUnreliableSeqNum = 0;
        dispatchedCmds.AddLastRange(startCmd, currentCmd.Previous);

        if (!this.needDispatch)
        {
            ENetHost.Instance.dispatchQueue.AddLast(this);
            needDispatch = true;
        }

        DispatchInUnreliableCmds(channel, queuedCmd);
    }

    public void QueueAck(ENetProto cmd, uint sentTime)
    {
        if (cmd.header.channelID < channels.Count)
        {
            ENetChannel channel = channels[cmd.header.channelID];
            uint reliableWindow = cmd.header.reliableSeqNum / Convert.ToUInt32(ENetDef.PeerReliableWindowSize),
                        currentWindow = channel.inReliableSeqNum / Convert.ToUInt32(ENetDef.PeerReliableWindowSize);

            if (cmd.header.reliableSeqNum < channel.inReliableSeqNum)
                reliableWindow += Convert.ToUInt32(ENetDef.PeerReliableWindows);

            if (reliableWindow >= currentWindow + Convert.ToUInt32(ENetDef.PeerReliableWindows) - 1 && reliableWindow <= currentWindow + Convert.ToUInt32(ENetDef.PeerReliableWindows))
                return;
        }

        ENetAckCmd ack;
        ack.sentTime = sentTime;
        ack.cmd = cmd;

        unsafe
        {
            outDataTotal += Convert.ToUInt32(sizeof(ENetAckCmd));
        }

        ackCmds.AddLast(ack);

    }

    public void QueueOutCmd(ENetProto cmd, ENetPacket packet, uint offset, uint length)
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
            outDataTotal += ENetProtoCmdSize.CmdSize[Convert.ToInt32(outCmd.cmd.header.cmdFlag&(int)ENetProtoCmdType.Mask)] + outCmd.fragmentLength;
        }

        if (outCmd.cmd.header.channelID == 0xFF)
        {
            ++outReliableSeqNum;

            outCmd.reliableSeqNum = outReliableSeqNum;
            outCmd.unreliableSeqNum = 0;
        }
        else
        {
            ENetChannel channel = channels[outCmd.cmd.header.channelID];

            if ((outCmd.cmd.header.cmdFlag & (int)ENetProtoFlag.CmdFlagAck) != 0)
            {
                ++channel.outReliableSeqNumber;
                channel.outUnreliableSeqNum = 0;

                outCmd.reliableSeqNum = channel.outReliableSeqNumber;
                outCmd.unreliableSeqNum = 0;
            }
            else
            {
                if ((outCmd.cmd.header.cmdFlag & (int)ENetProtoFlag.CmdFlagUnSeq) != 0)
                {
                    ++outUnSeqGroup;

                    outCmd.reliableSeqNum = 0;
                    outCmd.unreliableSeqNum = 0;
                }
                else
                {
                    if (outCmd.fragmentOffset == 0)
                        ++channel.outUnreliableSeqNum;

                    outCmd.reliableSeqNum = channel.outReliableSeqNumber;
                    outCmd.unreliableSeqNum = channel.outUnreliableSeqNum;
                }
            }

        }

        outCmd.sendAttempts = 0;
        outCmd.sentTime = 0;
        outCmd.roundTripTimeout = 0;
        outCmd.roundTripTimeoutLimit = 0;
        outCmd.cmd.header.reliableSeqNum = Utils.HostToNetOrder(outCmd.reliableSeqNum);

        switch (outCmd.cmd.header.cmdFlag & (int)ENetProtoCmdType.Mask)
        {
            case (int)ENetProtoCmdType.SendUnreliable:
                outCmd.cmd.sendUnReliable.unreliableSeqNum = Utils.HostToNetOrder(outCmd.unreliableSeqNum);
                break;

            case (int)ENetProtoCmdType.SendUnseq:
                outCmd.cmd.sendUnsequenced.unsequencedGroup = Utils.HostToNetOrder(outUnSeqGroup);
                break;

            default:
                break;
        }
        outCmds.AddLast(outCmd);
    }

    public ENetInCmd? QueueInCmd(ENetProto cmd, byte[] data, uint dataLength, int flags, uint fragmentCount)
    {
        ENetInCmd dummyCmd = new();

        ENetChannel channel = channels[cmd.header.channelID];
        uint unreliableSeqNum = 0, reliableSeqNum = 0;
        uint reliableWindow, currentWindow;
        ENetInCmd inCmd;
        LinkedListNode<ENetInCmd>? currCmd;
        ENetPacket packet;

        if (state == ENetPeerState.DisconnectLater)
            goto discardcmd;

        if ((cmd.header.cmdFlag & (int)ENetProtoCmdType.SendUnseq) != 0)
        {
            reliableSeqNum = cmd.header.reliableSeqNum;
            reliableWindow = reliableSeqNum / ENetDef.PeerReliableWindowSize;
            currentWindow = channel.inReliableSeqNum / ENetDef.PeerReliableWindowSize;

            if (reliableSeqNum < channel.inReliableSeqNum)
                reliableWindow += ENetDef.PeerReliableWindows;

            if (reliableWindow < currentWindow || reliableWindow >= currentWindow + ENetDef.PeerReliableWindows - 1)
                goto discardcmd;
        }


        switch (cmd.header.cmdFlag & (int)ENetProtoCmdType.Mask)
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
                        if (inCmd.reliableSeqNum < channel.inReliableSeqNum)
                            continue;
                    }
                    else
                    if (inCmd.reliableSeqNum >= channel.inReliableSeqNum)
                        break;

                    if (inCmd.reliableSeqNum <= reliableSeqNum)
                    {
                        if (inCmd.reliableSeqNum < reliableSeqNum)
                            break;

                        goto discardcmd;
                    }
                }
                break;

            case (int)ENetProtoCmdType.SendUnreliable:
            case (int)ENetProtoCmdType.SendUnreliableFragment:
                unreliableSeqNum = Utils.NetToHostOrder(cmd.sendUnReliable.unreliableSeqNum);

                if (reliableSeqNum == channel.inReliableSeqNum &&
                    unreliableSeqNum <= channel.inUnreliableSeqNum)
                    goto discardcmd;

                for (currCmd = channel.inUnreliableCmds.Last;
                     currCmd != null;
                     currCmd = currCmd.Previous)
                {
                    inCmd = currCmd.Value;

                    if ((cmd.header.cmdFlag & (int)ENetProtoCmdType.Mask) == (int)ENetProtoCmdType.SendUnreliable)
                        continue;

                    if (reliableSeqNum >= channel.inReliableSeqNum)
                    {
                        if (inCmd.reliableSeqNum < channel.inReliableSeqNum)
                            continue;
                    }
                    else
                    if (inCmd.reliableSeqNum >= channel.inReliableSeqNum)
                        break;

                    if (inCmd.reliableSeqNum < reliableSeqNum)
                        break;

                    if (inCmd.reliableSeqNum > reliableSeqNum)
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

        if (totalWaitingData >= host?.maximumWaitingData)
            goto notifyError;

        packet = new ENetPacket(data, flags);
        if (packet == null)
            goto notifyError;

        inCmd = new();

        inCmd.reliableSeqNum = cmd.header.reliableSeqNum;
        inCmd.unreliableSeqNum = unreliableSeqNum & 0xFFFF;
        inCmd.cmd = cmd;
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

        switch (cmd.header.cmdFlag & (int)ENetProtoCmdType.Mask)
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

    public int Send(int channelID, ENetPacket packet)
    {
        ENetChannel channel;
        ENetProto cmd = new();
        uint fragmentLength;


        if (this.state != ENetPeerState.Connected ||
            channelID >= this.channels.Count ||
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
                fragment.cmd.header.cmdFlag = cmdNum;
                fragment.cmd.header.channelID = channelID;
                fragment.cmd.sendFragment.startSequenceNumber = startSequenceNumber;
                fragment.cmd.sendFragment.dataLength = (uint)IPAddress.HostToNetworkOrder(fragmentLength);
                fragment.cmd.sendFragment.fragmentCount = (uint)IPAddress.HostToNetworkOrder (fragmentCount);
                fragment.cmd.sendFragment.fragmentNumber = (uint)IPAddress.HostToNetworkOrder (fragmentNumber);
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
            cmd.sendUnsequenced.dataLength = (uint)IPAddress.HostToNetworkOrder(packet.DataLength);
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

        QueueOutCmd(cmd, packet, 0, packet.DataLength);
        
        return 0;
    }

    public ENetPacket? Receive(ref int channelID)
    {
           ENetInCmd inCmd;
           ENetPacket? packet;
   
           if (this.dispatchedCmds.Count == 0 || this.dispatchedCmds.First == null)
             return null;

           inCmd = this.dispatchedCmds.First.Value;

           channelID = inCmd.cmd.header.channelID;

           packet = inCmd.packet;

           this.totalWaitingData -= packet.DataLength;
        
        return packet;
    }

    public void Reset()
    {
        OnDisconnect();

        this.outgoingPeerID = ENetDef.ProtoMaxPeerID;
        this.connectID = 0;

        this.state = ENetPeerState.Disconnected;

        this.incomingBandwidth = 0;
        this.outgoingBandwidth = 0;
        this.incomingBandwidthThrottleEpoch = 0;
        this.outgoingBandwidthThrottleEpoch = 0;
        this.incomingDataTotal = 0;
        this.outgoingDataTotal = 0;
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
        this.lastRoundTripTimeVariance = 0;
        this.highestRoundTripTimeVariance = 0;
        this.roundTripTime = ENetDef.PeerDefaultRTT;
        this.roundTripTimeVariance = 0;
        this.mtu = ENetHost.Instance.mtu;
        this.reliableDataInTransit = 0;
        this.outReliableSeqNum = 0;
        this.windowSize = ENetDef.ProtoMaxWindowSize;
        this.incomingUnsequencedGroup = 0;
        this.outUnSeqGroup = 0;
        this.eventData = 0;
        this.totalWaitingData = 0;
        this.needDispatch = false;

        Array.Clear(this.unsequencedWindow);

        ResetQueues();
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
    Disconnect,
    Zombie,
};