﻿using System.Net;
using System.Net.Sockets;

namespace ENet;



enum ENetAddressType
{
    HostAny = 0,//指定为默认server host
    //HostBroadcast = 0xFFFFFFFFU,//代表广播地址255.255.255.255
    //TODO：0xFFFFFFFFU报错，后面再看
}

enum ENetSocketOptType
{
    NonBlock = 1,
    Broadcast = 2,
    RcvBuf = 3,
    SendBuf = 4,
    ReuseAddr = 5,
    RcvTimeout = 6,
    SendTimeout = 7,
    Error = 8,
    NoDelay = 9
}

class ENetSocket
{
    public Socket? socket;

    public ENetSocket()
    {
        socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
    }

    ~ENetSocket()
    {
        socket?.Close();
    }

    public void SetOption(ENetSocketOptType type, int value)
    {
        if (socket == null) return;

        switch (type)
        {
            case ENetSocketOptType.NonBlock:
                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.UnblockSource, value);
                break;
            case ENetSocketOptType.Broadcast:
                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, value);
                break;
            case ENetSocketOptType.RcvBuf:
                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveBuffer, value);
                break;
            case ENetSocketOptType.SendBuf:
                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendBuffer, value);
                break;
            case ENetSocketOptType.ReuseAddr:
                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, value);
                break;
            case ENetSocketOptType.RcvTimeout:
                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, value);
                break;
            case ENetSocketOptType.SendTimeout:
                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendTimeout, value);
                break;
            case ENetSocketOptType.Error:
                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Error, value);
                break;
            case ENetSocketOptType.NoDelay:
                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.NoDelay, value);
                break;
            default:
                break;
        }
    }

    public void Shutdown(SocketShutdown how)
    {
        socket?.Shutdown(how);
    }

    public void Bind(string ip, int port)
    {
        socket?.Bind(new IPEndPoint(IPAddress.Parse(ip), port));//TODO：try-catch
    }
    
    public void Listen(int backlog = 100)
    {
        //TODO: 如果backlog小于0会怎么样？
        //TODO：如果backlog超过SocketOptionName.MaxConnections，会怎样？
        //TODO：是否应该增加try-except模块
        //TODO：使用Listen的无参调用，让socket自己设置。会有什么影响？
        socket?.Listen(backlog);
    }


}
