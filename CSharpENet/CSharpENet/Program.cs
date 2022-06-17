using ENet;
using System.Net;
using System.Net.Sockets;

IPAddress ipAddr = IPAddress.Any;
int port = 30000;
IPEndPoint ep = new(ipAddr, port);
Socket socket = new(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
socket.Bind(ep);

IPEndPoint? newEP = (IPEndPoint?)socket.LocalEndPoint;



byte[]? data = newEP?.Address?.GetAddressBytes();
if (data != null)
{
    foreach (byte b in data)
    {
        Console.WriteLine($"data:{b}");
    }
}
