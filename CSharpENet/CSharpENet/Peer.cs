using System.Net;
using System.Net.Sockets;

namespace ENet;

class ENetPeer
{

    // ENetListNode dispatchList;
    // struct _ENetHost * host;
    //enet_uint16 outgoingPeerID;
    // enet_uint16 incomingPeerID;
    // enet_uint32 connectID;
    // enet_uint8 outgoingSessionID;
    // enet_uint8 incomingSessionID;
    // ENetAddress address;            /**< Internet address of the peer */
    // void* data;               /**< Application private data, may be freely modified */
    public ENetPeerState state = new();
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
    uint outUnSeqGroup;
    // enet_uint32 unsequencedWindow[ENET_PEER_UNSEQUENCED_WINDOW_SIZE / 32];
    // enet_uint32 eventData;
    // size_t totalWaitingData;

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
        if (!state.Connected && !state.DisconnectLater)
        {
            if (inBandwidth != 0)
                ++hostBandwidthLimitedPeers;

            ++hostConnectedPeers;
        }
    }
    public void OnDisconnect(ref uint hostBandwidthLimitedPeers, ref uint hostConnectedPeers)
    {
        if (state.Connected || state.DisconnectLater)
        {
            if (inBandwidth != 0)
            {
                hostBandwidthLimitedPeers--;
            }

            hostConnectedPeers--;
        }
    }

    //TODO：这个函数可能应该交给channel
    public void DispatchInUnreliableCmds(ENetChannel channel, ENetInCmd queuedCmd, ref LinkedList<ENetPeer> hostDispatchQueue)
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

            if (inCmd.reliableSeqNumber == channel.inReliableSeqNum)
            {
                if (inCmd.fragmentsRemaining <= 0)
                {
                    channel.inUnreliableSeqNum = inCmd.unreliableSeqNumber;
                    continue;
                }

                if (startCmd != currentCmd)
                {
                    dispatchedCmds.AddLastRange(startCmd, currentCmd.Previous);

                    if (!needDispatch)
                    {
                        hostDispatchQueue.AddLast(this);
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
                ushort reliableWindow = (ushort)(inCmd.reliableSeqNumber / (ushort)ENetDef.PeerReliableWindowSize),
                            currentWindow = (ushort)(channel.inReliableSeqNum / (ushort)ENetDef.PeerReliableWindowSize);
                if (inCmd.reliableSeqNumber < channel.inReliableSeqNum)
                    reliableWindow += (ushort)ENetDef.PeerReliableWindows;
                if (reliableWindow >= currentWindow && reliableWindow < currentWindow + (ushort)ENetDef.PeerFreeReliableWindows - 1)
                    break;

                droppedCmd = currentCmd.Next;

                if (startCmd != currentCmd)
                {
                    dispatchedCmds.AddLastRange(startCmd, currentCmd.Previous);

                    if (!needDispatch)
                    {
                        hostDispatchQueue.AddLast(this);
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
                hostDispatchQueue.AddLast(this);
                needDispatch = true;
            }

            droppedCmd = currentCmd;
        }

        RemoveInCmds(channel.inUnreliableCmds, channel.inUnreliableCmds.First?.Value, droppedCmd?.Value, queuedCmd);
    }

    public void DispatchInReliableCmds(ENetChannel channel, ENetInCmd queuedCmd, ref LinkedList<ENetPeer> hostDispatchQueue)
    {
        LinkedListNode<ENetInCmd>? currentCmd = channel.inReliableCmds.First;
        LinkedListNode<ENetInCmd>? startCmd = currentCmd;
        if (startCmd == null) return;

        for (;
             currentCmd != null;
             currentCmd = currentCmd?.Next)//Last.Next不知道最后一个会不会执行
        {
            if (currentCmd.Value.fragmentsRemaining > 0 ||
                currentCmd.Value.reliableSeqNumber != channel.inReliableSeqNum + 1)
                break;

            channel.inReliableSeqNum = currentCmd.Value.reliableSeqNumber;

            if (currentCmd.Value.fragmentCount > 0)
                channel.inReliableSeqNum += currentCmd.Value.fragmentCount - 1;
        }

        if (currentCmd == null) return;

        channel.inUnreliableSeqNum = 0;
        dispatchedCmds.AddLastRange(startCmd, currentCmd.Previous);

        if (!this.needDispatch)
        {
            hostDispatchQueue.AddLast(this);
            needDispatch = true;
        }

        DispatchInUnreliableCmds(channel, queuedCmd, ref hostDispatchQueue);
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
                outCmd.command.sendUnReliable.unReliableSeqNum = Utils.HostToNetOrder(outCmd.unreliableSeqNum);
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
        ENetInCmd? dummyCmd;

        ENetChannel channel = channels[cmd.header.channelID];
        uint unreliableSeqNum = 0, reliableSeqNum = 0;
        uint reliableWindow, currentWindow;
        ENetInCmd inCmd;
        LinkedListNode<ENetInCmd> currentCommand;
        ENetPacket packet;
        /*
         
ENetIncomingCommand *
enet_peer_queue_incoming_command (ENetPeer * peer, const ENetProtocol * command, const void * data, size_t dataLength, enet_uint32 flags, enet_uint32 fragmentCount)
{

    if (peer -> state == ENET_PEER_STATE_DISCONNECT_LATER)
      goto discardCommand;

    if ((cmd.header.command & ENET_PROTOCOL_COMMAND_MASK) != ENET_PROTOCOL_COMMAND_SEND_UNSEQUENCED)
    {
        reliableSeqNum = cmd.header.reliableSeqNum;
        reliableWindow = reliableSeqNum / ENET_PEER_RELIABLE_WINDOW_SIZE;
        currentWindow = channel -> incomingReliableSequenceNumber / ENET_PEER_RELIABLE_WINDOW_SIZE;

        if (reliableSeqNum < channel -> incomingReliableSequenceNumber)
           reliableWindow += ENET_PEER_RELIABLE_WINDOWS;

        if (reliableWindow < currentWindow || reliableWindow >= currentWindow + ENET_PEER_FREE_RELIABLE_WINDOWS - 1)
          goto discardCommand;
    }
                    
    switch (cmd.header.command & ENET_PROTOCOL_COMMAND_MASK)
    {
    case ENET_PROTOCOL_COMMAND_SEND_FRAGMENT:
    case ENET_PROTOCOL_COMMAND_SEND_RELIABLE:
       if (reliableSeqNum == channel -> incomingReliableSequenceNumber)
         goto discardCommand;
       
       for (currentCommand = enet_list_previous (enet_list_end (& channel -> incomingReliableCommands));
            currentCommand != enet_list_end (& channel -> incomingReliableCommands);
            currentCommand = enet_list_previous (currentCommand))
       {
          incomingCommand = (ENetIncomingCommand *) currentCommand;

          if (reliableSeqNum >= channel -> incomingReliableSequenceNumber)
          {
             if (incomingCommand -> reliableSeqNum < channel -> incomingReliableSequenceNumber)
               continue;
          }
          else
          if (incomingCommand -> reliableSeqNum >= channel -> incomingReliableSequenceNumber)
            break;

          if (incomingCommand -> reliableSeqNum <= reliableSeqNum)
          {
             if (incomingCommand -> reliableSeqNum < reliableSeqNum)
               break;

             goto discardCommand;
          }
       }
       break;

    case ENET_PROTOCOL_COMMAND_SEND_UNRELIABLE:
    case ENET_PROTOCOL_COMMAND_SEND_UNRELIABLE_FRAGMENT:
       unreliableSeqNum = ENET_NET_TO_HOST_16 (cmd.sendUnreliable.unreliableSeqNum);

       if (reliableSeqNum == channel -> incomingReliableSequenceNumber && 
           unreliableSeqNum <= channel -> incomingUnreliableSeqNum)
         goto discardCommand;

       for (currentCommand = enet_list_previous (enet_list_end (& channel -> incomingUnreliableCommands));
            currentCommand != enet_list_end (& channel -> incomingUnreliableCommands);
            currentCommand = enet_list_previous (currentCommand))
       {
          incomingCommand = (ENetIncomingCommand *) currentCommand;

          if ((cmd.header.command & ENET_PROTOCOL_COMMAND_MASK) == ENET_PROTOCOL_COMMAND_SEND_UNSEQUENCED)
            continue;

          if (reliableSeqNum >= channel -> incomingReliableSequenceNumber)
          {
             if (incomingCommand -> reliableSeqNum < channel -> incomingReliableSequenceNumber)
               continue;
          }
          else
          if (incomingCommand -> reliableSeqNum >= channel -> incomingReliableSequenceNumber)
            break;

          if (incomingCommand -> reliableSeqNum < reliableSeqNum)
            break;

          if (incomingCommand -> reliableSeqNum > reliableSeqNum)
            continue;

          if (incomingCommand -> unreliableSeqNum <= unreliableSeqNum)
          {
             if (incomingCommand -> unreliableSeqNum < unreliableSeqNum)
               break;

             goto discardCommand;
          }
       }
       break;

    case ENET_PROTOCOL_COMMAND_SEND_UNSEQUENCED:
       currentCommand = enet_list_end (& channel -> incomingUnreliableCommands);
       break;

    default:
       goto discardCommand;
    }

    if (peer -> totalWaitingData >= peer -> host -> maximumWaitingData)
      goto notifyError;

    packet = enet_packet_create (data, dataLength, flags);
    if (packet == NULL)
      goto notifyError;

    incomingCommand = (ENetIncomingCommand *) enet_malloc (sizeof (ENetIncomingCommand));
    if (incomingCommand == NULL)
      goto notifyError;

    incomingCommand -> reliableSeqNum = cmd.header.reliableSeqNum;
    incomingCommand -> unreliableSeqNum = unreliableSeqNum & 0xFFFF;
    incomingCommand -> command = * command;
    incomingCommand -> fragmentCount = fragmentCount;
    incomingCommand -> fragmentsRemaining = fragmentCount;
    incomingCommand -> packet = packet;
    incomingCommand -> fragments = NULL;
    
    if (fragmentCount > 0)
    { 
       if (fragmentCount <= ENET_PROTOCOL_MAXIMUM_FRAGMENT_COUNT)
         incomingCommand -> fragments = (enet_uint32 *) enet_malloc ((fragmentCount + 31) / 32 * sizeof (enet_uint32));
       if (incomingCommand -> fragments == NULL)
       {
          enet_free (incomingCommand);

          goto notifyError;
       }
       memset (incomingCommand -> fragments, 0, (fragmentCount + 31) / 32 * sizeof (enet_uint32));
    }

    if (packet != NULL)
    {
       ++ packet -> referenceCount;
      
       peer -> totalWaitingData += packet -> dataLength;
    }

    enet_list_insert (enet_list_next (currentCommand), incomingCommand);

    switch (cmd.header.command & ENET_PROTOCOL_COMMAND_MASK)
    {
    case ENET_PROTOCOL_COMMAND_SEND_FRAGMENT:
    case ENET_PROTOCOL_COMMAND_SEND_RELIABLE:
       enet_peer_dispatch_incoming_reliable_commands (peer, channel, incomingCommand);
       break;

    default:
       enet_peer_dispatch_incoming_unreliable_commands (peer, channel, incomingCommand);
       break;
    }

    return incomingCommand;

discardCommand:
    if (fragmentCount > 0)
      goto notifyError;

    if (packet != NULL && packet -> referenceCount == 0)
      enet_packet_destroy (packet);

    return & dummyCommand;

notifyError:
    if (packet != NULL && packet -> referenceCount == 0)
      enet_packet_destroy (packet);

    return NULL;
}

}
         */
    }


}


struct ENetPeerState
{

    public bool Disconnected = false;
    public bool Connecting = false;
    public bool AckConnect = false;
    public bool ConnectionPending = false;
    public bool ConnectionSucceed = false;
    public bool Connected = false;
    public bool DisconnectLater = false;
    public bool Disconnecting = false;
    public bool Disconnect = false;
    public bool Zombie = false;

    public ENetPeerState()
    {

    }
};