using System.Net;

namespace ENet;

class Utils
{
    private static DateTime timeStart = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static DateTime timeBase = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public static ulong TimeGet()
    {
        return (ulong)(DateTime.UtcNow - timeBase).TotalMilliseconds;
    }
    public static void TimeSet(ulong newTimeBase)
    {
        timeBase = timeStart.AddMilliseconds(newTimeBase);
    }
    public static uint RandomSeed()
    {
        return (uint)TimeGet();
    }

    public static uint HostToNetOrder(uint value)
    {
        return Convert.ToUInt32(IPAddress.HostToNetworkOrder(value));
    }
    public static uint NetToHostOrder(uint value)
    {
        return Convert.ToUInt32(IPAddress.NetworkToHostOrder(value));
    }
}


