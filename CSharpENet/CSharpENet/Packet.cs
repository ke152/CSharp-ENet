namespace ENet;

enum ENetPacketFlag
{
    // TODO：if(flags & ENetPacketFlag.Reliable)  报错&结果为int，不能转bool
    //必须由对应peer接收；在确认发出前必须不断重发
    Reliable = (1 << 0),
    // 无序；和reliable互斥
    UnSeq = (1 << 1),
    // 数据部分由用户提供，程序不会分配内存
    NoAllocate = (1 << 2),
    // 如果超过MTU，数据包会分多个片段以unreliable（代替reliable）发出（TODO：这个替代的逻辑，没注意到）
    UnreliableFragment = (1 << 3),
    // packet在所有队列中的引用都被发出
    Sent = (1 << 8)
}

public class ENetPacket
{
    /**
    * An ENet data packet that may be sent to or received from a peer. The shown 
    * fields should only be read and never modified. The data field contains the 
    * allocated data for the packet. The dataLength fields specifies the length 
    * of the allocated data.  The flags field is either 0 (specifying no flags), 
    * or a bitwise-or of any combination of the following flags:
    */
    public uint Flags;           /**< bitwise-or of ENetPacketFlag constants */
    public byte[]? Data;            //allocated data for packet
    public uint DataLength;

    public ENetPacket(byte[]? data, uint dataLength, uint flags)
    {
        if ((flags & (uint)ENetPacketFlag.NoAllocate) != 0)
        {
            this.Data = data;
        }
        else
        {
            if (dataLength <= 0)
            {
                this.Data = null;
            }
            else
            {
                //this.Data = new byte[dataLength];//c#不需要new之后memcpy，直接都是传引用。
                //TODO: new完之后要不要判空
                this.Data = data;
            }
        }
        this.Flags = flags;
        this.DataLength = dataLength;

    }

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