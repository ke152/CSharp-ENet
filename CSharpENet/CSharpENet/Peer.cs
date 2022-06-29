using System.Net;
using System.Net.Sockets;

namespace ENet;

class ENetPeer
{

    // ENetListNode dispatchList;
    public ENetHost? host;
    //enet_uint16 outgoingPeerID;
    // enet_uint16 incomingPeerID;
    // enet_uint32 connectID;
    // enet_uint8 outgoingSessionID;
    // enet_uint8 incomingSessionID;
    // ENetAddress address;            /**< Internet address of the peer */
    // void* data;               /**< Application private data, may be freely modified */
    public ENetPeerState state;
    public List<ENetChannel> channels = new();
    public uint inBandwidth;  /**< Downstream bandwidth of the client in bytes/second */
    // enet_uint32 outgoingBandwidth;  /**< Upstream bandwidth of the client in bytes/second */
    // enet_uint32 incomingBandwidthThrottleEpoch;
    // enet_uint32 outgoingBandwidthThrottleEpoch;
    // enet_uint32 incomingDataTotal;
    public uint outDataTotal = 0;
    // enet_uint32 lastSendTime;
    // enet_uint32 lastReceiveTime;
    // enet_uint32 nextTimeout;
    // enet_uint32 earliestTimeout;
    // enet_uint32 packetLossEpoch;
    // enet_uint32 packetsSent;
    // enet_uint32 packetsLost;
    // enet_uint32 packetLoss;          /**< mean packet loss of reliable packets as a ratio with respect to the constant ENET_PEER_PACKET_LOSS_SCALE */
    // enet_uint32 packetLossVariance;
    // enet_uint32 packetThrottle;
    // enet_uint32 packetThrottleLimit;
    // enet_uint32 packetThrottleCounter;
    // enet_uint32 packetThrottleEpoch;
    // enet_uint32 packetThrottleAcceleration;
    // enet_uint32 packetThrottleDeceleration;
    // enet_uint32 packetThrottleInterval;
    // enet_uint32 pingInterval;
    // enet_uint32 timeoutLimit;
    // enet_uint32 timeoutMinimum;
    // enet_uint32 timeoutMaximum;
    // enet_uint32 lastRoundTripTime;
    // enet_uint32 lowestRoundTripTime;
    // enet_uint32 lastRoundTripTimeVariance;
    // enet_uint32 highestRoundTripTimeVariance;
    // enet_uint32 roundTripTime;            /**< mean round trip time (RTT), in milliseconds, between sending a reliable packet and receiving its acknowledgement */
    // enet_uint32 roundTripTimeVariance;
    // enet_uint32 mtu;
    // enet_uint32 windowSize;
    // enet_uint32 reliableDataInTransit;
    public uint outReliableSeqNum;
    public LinkedList<ENetAckCmd> ackCmds = new();
    public LinkedList<ENetOutCmd> sentReliableCmds = new();
    public LinkedList<ENetOutCmd> sentUnreliableCmds = new();
    public LinkedList<ENetOutCmd> outCmds = new();
    public LinkedList<ENetInCmd> dispatchedCmds = new();
    public bool needDispatch = false;
    // enet_uint16 reserved;
    // enet_uint16 incomingUnsequencedGroup;
    public uint outUnSeqGroup;
    // enet_uint32 unsequencedWindow[ENET_PEER_UNSEQUENCED_WINDOW_SIZE / 32];
    // enet_uint32 eventData;
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
                ++hostBandwidthLimitedPeers;

            ++hostConnectedPeers;
        }
    }
    public void OnDisconnect(ref uint hostBandwidthLimitedPeers, ref uint hostConnectedPeers)
    {
        if (state == ENetPeerState.Connected || state == ENetPeerState.DisconnectLater)
        {
            if (inBandwidth != 0)
            {
                hostBandwidthLimitedPeers--;
            }

            hostConnectedPeers--;
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

            if (inCmd.command.header.cmdType == ENetProtoCmdType.SendUnseq)
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
        ack.command = cmd;

        unsafe
        {
            outDataTotal += Convert.ToUInt32(sizeof(ENetAckCmd));
        }

        ackCmds.AddLast(ack);

    }

    public void QueueOutCmd(ENetProto command, ENetPacket packet, uint offset, uint length)
    {
        ENetOutCmd outCmd = new();
        outCmd.command = command;
        outCmd.fragmentOffset = offset;
        outCmd.fragmentLength = length;
        outCmd.packet = packet;

        SetupOutCmd(outCmd);
    }

    public void SetupOutCmd(ENetOutCmd outCmd)
    {
        unsafe
        {
            outDataTotal += ENetProtoCmdSize.CmdSize[Convert.ToInt32(outCmd.command.header.cmdType)] + outCmd.fragmentLength;
        }

        if (outCmd.command.header.channelID == 0xFF)
        {
            ++outReliableSeqNum;

            outCmd.reliableSeqNum = outReliableSeqNum;
            outCmd.unreliableSeqNum = 0;
        }
        else
        {
            ENetChannel channel = channels[outCmd.command.header.channelID];

            if ((outCmd.command.header.protoFlag & ENetProtoFlag.CmdFlagAck) != 0)
            {
                ++channel.outReliableSeqNumber;
                channel.outUnreliableSeqNumber = 0;

                outCmd.reliableSeqNum = channel.outReliableSeqNumber;
                outCmd.unreliableSeqNum = 0;
            }
            else
            {
                if ((outCmd.command.header.protoFlag & ENetProtoFlag.CmdFlagUnSeq) != 0)
                {
                    ++outUnSeqGroup;

                    outCmd.reliableSeqNum = 0;
                    outCmd.unreliableSeqNum = 0;
                }
                else
                {
                    if (outCmd.fragmentOffset == 0)
                        ++channel.outUnreliableSeqNumber;

                    outCmd.reliableSeqNum = channel.outReliableSeqNumber;
                    outCmd.unreliableSeqNum = channel.outUnreliableSeqNumber;
                }
            }

        }

        outCmd.sendAttempts = 0;
        outCmd.sentTime = 0;
        outCmd.roundTripTimeout = 0;
        outCmd.roundTripTimeoutLimit = 0;
        outCmd.command.header.reliableSeqNum = Utils.HostToNetOrder(outCmd.reliableSeqNum);

        switch (outCmd.command.header.cmdType)
        {
            case ENetProtoCmdType.SendUnreliable:
                outCmd.command.sendUnReliable.unreliableSeqNum = Utils.HostToNetOrder(outCmd.unreliableSeqNum);
                break;

            case ENetProtoCmdType.SendUnseq:
                outCmd.command.sendUnsequenced.unsequencedGroup = Utils.HostToNetOrder(outUnSeqGroup);
                break;

            default:
                break;
        }
        outCmds.AddLast(outCmd);
    }

    public ENetInCmd? QueueInCmd(ENetProto cmd, byte[] data, uint dataLength, uint flags, uint fragmentCount)
    {
        ENetInCmd dummyCmd = new();

        ENetChannel channel = channels[cmd.header.channelID];
        uint unreliableSeqNum = 0, reliableSeqNum = 0;
        uint reliableWindow, currentWindow;
        ENetInCmd inCmd;
        LinkedListNode<ENetInCmd>? currCmd;
        ENetPacket packet;

        if (state == ENetPeerState.DisconnectLater)
            goto discardCommand;

        if (cmd.header.cmdType != ENetProtoCmdType.SendUnseq)
        {
            reliableSeqNum = cmd.header.reliableSeqNum;
            reliableWindow = reliableSeqNum / ENetDef.PeerReliableWindowSize;
            currentWindow = channel.inReliableSeqNum / ENetDef.PeerReliableWindowSize;

            if (reliableSeqNum < channel.inReliableSeqNum)
                reliableWindow += ENetDef.PeerReliableWindows;

            if (reliableWindow < currentWindow || reliableWindow >= currentWindow + ENetDef.PeerReliableWindows - 1)
                goto discardCommand;
        }


        switch (cmd.header.cmdType)
        {
            case ENetProtoCmdType.SendFragment:
            case ENetProtoCmdType.SendReliable:
                if (reliableSeqNum == channel.inReliableSeqNum)
                    goto discardCommand;

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

                        goto discardCommand;
                    }
                }
                break;

            case ENetProtoCmdType.SendUnreliable:
            case ENetProtoCmdType.SendUnreliableFragment:
                unreliableSeqNum = Utils.NetToHostOrder(cmd.sendUnReliable.unreliableSeqNum);

                if (reliableSeqNum == channel.inReliableSeqNum &&
                    unreliableSeqNum <= channel.inUnreliableSeqNum)
                    goto discardCommand;

                for (currCmd = channel.inUnreliableCmds.Last;
                     currCmd != null;
                     currCmd = currCmd.Previous)
                {
                    inCmd = currCmd.Value;

                    if (cmd.header.cmdType == ENetProtoCmdType.SendUnreliable)
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

                        goto discardCommand;
                    }
                }
                break;

            case ENetProtoCmdType.SendUnseq:
                currCmd = channel.inUnreliableCmds.Last;
                break;

            default:
                goto discardCommand;
        }

        if (totalWaitingData >= host?.maximumWaitingData)
            goto notifyError;

        packet = new ENetPacket(data, flags);
        if (packet == null)
            goto notifyError;

        inCmd = new();

        inCmd.reliableSeqNum = cmd.header.reliableSeqNum;
        inCmd.unreliableSeqNum = unreliableSeqNum & 0xFFFF;
        inCmd.command = cmd;
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

        switch (cmd.header.cmdType)
        {
            case ENetProtoCmdType.SendFragment:
            case ENetProtoCmdType.SendReliable:
                DispatchInReliableCmds(channel, inCmd);
                break;
            default:
                DispatchInUnreliableCmds(channel, inCmd);
                break;
        }

        return inCmd;

    discardCommand:
        if (fragmentCount > 0)
            goto notifyError;

        return dummyCmd;

    notifyError:
        return null;
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