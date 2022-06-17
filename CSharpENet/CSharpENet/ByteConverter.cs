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

    //测试代码///

    class Program

    {

        static void Main(string[] args)

        {

            Student student1 = new Student { Name = "胡昌俊", ID = 2 };

            Console.WriteLine("序列化前=> 姓名:{0} ID:{1}", student1.ID, student1.Name);

            byte[] bytes = MyConverter.StructureToByte<Student>(student1);

            Student sudent2 = MyConverter.ByteToStructure<Student>(bytes);

            Console.WriteLine("序列化后=> 姓名:{0} ID:{1}", sudent2.ID, sudent2.Name);

            Console.Read();

        }
}
