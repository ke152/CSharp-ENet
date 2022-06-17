using System.Runtime.InteropServices;
namespace ENet;

internal static class ByteConverter
{
    // 由结构体转换为byte数组
    public static byte[]? StructToByte<T>(T structure)
    {
        int size = Marshal.SizeOf(typeof(T));
        byte[] buffer = new byte[size];
        IntPtr bufferIntPtr = Marshal.AllocHGlobal(size);

        try
        {
            if (structure == null)
            {
                return null;
            }
            Marshal.StructureToPtr(structure, bufferIntPtr, true);
            Marshal.Copy(bufferIntPtr, buffer, 0, size);
        }
        finally
        {
            Marshal.FreeHGlobal(bufferIntPtr);
        }

        return buffer;
    }

    /// 由byte数组转换为结构体
    public static T? ByteToStruct<T>(byte[] dataBuffer)
    {
        object? structure = null;
        int size = Marshal.SizeOf(typeof(T));
        IntPtr allocIntPtr = Marshal.AllocHGlobal(size);

        try
        {
            Marshal.Copy(dataBuffer, 0, allocIntPtr, size);
            structure = Marshal.PtrToStructure(allocIntPtr, typeof(T));
        }
        finally
        {
            Marshal.FreeHGlobal(allocIntPtr);
        }

        return (T?)structure;
    }

}

