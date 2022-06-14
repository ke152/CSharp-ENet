namespace ENet;

enum EMENetPacketFlag
{
    //必须由对应peer接收；在确认发出前必须不断重发
    ENET_PACKET_FLAG_RELIABLE = (1 << 0),
    // 无序；和reliable互斥
    ENET_PACKET_FLAG_UNSEQUENCED = (1 << 1),
    // 数据部分由用户提供，程序不会分配内存
    ENET_PACKET_FLAG_NO_ALLOCATE = (1 << 2),
    // 如果超过MTU，数据包会分多个片段以unreliable（代替reliable）发出（TODO：这个替代的逻辑，没注意到）
    ENET_PACKET_FLAG_UNRELIABLE_FRAGMENT = (1 << 3),
    // packet在所有队列中的引用都被发出
    ENET_PACKET_FLAG_SENT = (1 << 8)
}

public class ENetPacket:IDisposable
{
    /**
    * An ENet data packet that may be sent to or received from a peer. The shown 
    * fields should only be read and never modified. The data field contains the 
    * allocated data for the packet. The dataLength fields specifies the length 
    * of the allocated data.  The flags field is either 0 (specifying no flags), 
    * or a bitwise-or of any combination of the following flags:
    */
    public uint RefCount;  /**< internal use only *///TODO: c#引用gc机制，可能不需要这个
    public uint Flags;           /**< bitwise-or of ENetPacketFlag constants */
    public byte[]? Data;            //allocated data for packet
    public uint DataLength;

    public void Dispose()//TODO: 有没有办法手动gc
    {
        Console.WriteLine("dispose packet");
        return;
    }
    //ENetPacketFreeCallback freeCallback;    /**< function to be called when the packet is no longer in use */

    /** @defgroup Packet ENet packet functions 
        @{ 
*/

    /** Creates a packet that may be sent to a peer.
        @param data         initial contents of the packet's data; the packet's data will remain uninitialized if data is NULL.
        @param dataLength   size of the data allocated for this packet
        @param flags        flags for this packet as described for the ENetPacket structure.
        @returns the packet on success, NULL on failure
*/
    //    ENetPacket*
    //    Create(const void* data, size_t dataLength, enet_uint32 flags)
    //{
    //    ENetPacket* packet = (ENetPacket*)enet_malloc(sizeof(ENetPacket));
    //    if (packet == NULL)
    //      return NULL;

    //    if (flags & ENET_PACKET_FLAG_NO_ALLOCATE)
    //      packet -> data = (enet_uint8*) data;
    //    else
    //    if (dataLength <= 0)
    //      packet -> data = NULL;
    //    else
    //    {
    //       packet -> data = (enet_uint8*) enet_malloc(dataLength);
    //       if (packet -> data == NULL)
    //       {
    //          enet_free(packet);
    //          return NULL;
    //       }

    //       if (data != NULL)
    //         memcpy(packet -> data, data, dataLength);
    //    }

    //    packet->referenceCount = 0;
    //packet->flags = flags;
    //packet->dataLength = dataLength;
    //packet->freeCallback = NULL;
    //packet->userData = NULL;

    //return packet;
    //}

    ///** Destroys the packet and deallocates its data.
    //    @param packet packet to be destroyed
    //*/
    //void
    //enet_packet_destroy(ENetPacket* packet)
    //{
    //    if (packet == NULL)
    //        return;

    //    if (packet->freeCallback != NULL)
    //        (*packet->freeCallback)(packet);
    //    if (!(packet->flags & ENET_PACKET_FLAG_NO_ALLOCATE) &&
    //        packet->data != NULL)
    //        enet_free(packet->data);
    //    enet_free(packet);
    //}

    ///** Attempts to resize the data in the packet to length specified in the 
    //    dataLength parameter 
    //    @param packet packet to resize
    //    @param dataLength new size for the packet data
    //    @returns 0 on success, < 0 on failure
    //*/
    //public int Resize(ENetPacket* packet, size_t dataLength)
    //{
    //    enet_uint8* newData;

    //    if (dataLength <= packet->dataLength || (packet->flags & ENET_PACKET_FLAG_NO_ALLOCATE))
    //    {
    //        packet->dataLength = dataLength;

    //        return 0;
    //    }

    //    newData = (enet_uint8*)enet_malloc(dataLength);
    //    if (newData == NULL)
    //        return -1;

    //    memcpy(newData, packet->data, packet->dataLength);
    //    enet_free(packet->data);

    //    packet->data = newData;
    //    packet->dataLength = dataLength;

    //    return 0;
    //}

}