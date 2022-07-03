using System.Net;
using System.Runtime.Serialization.Formatters.Binary;

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


    public static byte[]? Serialize<T>(T msg)
    {
        using (MemoryStream ms = new MemoryStream())
        {
            try
            {
                BinaryFormatter bf = new BinaryFormatter();
                bf.Serialize(ms, msg);
                ms.Seek(0, SeekOrigin.Begin);
                return ms.ToArray();
            }
            catch (Exception e)
            {
                return null;
            }
        }
    }

    public static T? DeSerialize<T>(byte[] bytes)
    {
        using (MemoryStream ms = new MemoryStream(bytes))
        {
            try
            {
                BinaryFormatter bf = new BinaryFormatter();
                T msg = (T)bf.Deserialize(ms);
                return msg;
            }
            catch (Exception e)
            {
                return default;
            }
        }
    }

    public static byte[] SubBytes(byte[] data, int start, int length)
    {
        if (start < 0 || start >= data.Length || (start == 0 && length >= data.Length))
        {
            return data;
        }

        if (start + length > data.Length)
        {
            length = data.Length - start;
        }

        byte[] result = new byte[length];

        for (int i = start; i < start + length; i++)
        {
            result[i-start] = data[i];
        }

        return result;
    }
}


