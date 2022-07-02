using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ENet
{
    internal class ENetEvent
    {
        public ENetEventType type = ENetEventType.None;      /**< type of the event */
        public ENetPeer? peer = null;      /**< peer that generated a connect, disconnect or receive event */
        public uint channelID; /**< channel on the peer that generated the event, if appropriate */
        public uint data;      /**< data associated with the event, if appropriate */
        public ENetPacket? packet = null;    /**< packet associated with the event, if appropriate */
    }

    enum ENetEventType
    {
        None = 0,
        Connect = 1,
        Disconnect = 2,
        Recv = 3
    }
}
