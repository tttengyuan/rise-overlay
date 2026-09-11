using System;
using System.Runtime.InteropServices;

namespace HunterPie.Core.Native.IPC.Utils;

public class MessageHelper
{
    public static byte[] Serialize<T>(T data) where T : struct
    {
        int size = Marshal.SizeOf<T>();
        IntPtr gAlloc = Marshal.AllocHGlobal(size);

        Marshal.StructureToPtr(data, gAlloc, true);

        byte[] buffer = new byte[size];
        Marshal.Copy(gAlloc, buffer, 0, buffer.Length);

        Marshal.FreeHGlobal(gAlloc);
        return buffer;
    }

    public static T Deserialize<T>(byte[] buffer) where T : struct
    {
        int size = Marshal.SizeOf<T>();
        IntPtr mAlloc = Marshal.AllocHGlobal(size);
        // Zero-fill so a shorter native payload cannot leave garbage in trailing fields
        // (e.g. managed SizeConst grown before the in-game Native DLL was replaced).
        Marshal.Copy(new byte[size], 0, mAlloc, size);

        int copy = Math.Min(buffer.Length, size);
        if (copy > 0)
            Marshal.Copy(buffer, 0, mAlloc, copy);

        T deserialized = Marshal.PtrToStructure<T>(mAlloc);

        Marshal.FreeHGlobal(mAlloc);

        return deserialized;
    }
}