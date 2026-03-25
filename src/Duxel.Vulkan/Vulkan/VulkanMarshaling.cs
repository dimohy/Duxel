using System.Runtime.InteropServices;
using System.Text;

namespace Duxel.Vulkan;

internal static unsafe class VulkanMarshaling
{
    public static byte* StringToPtr(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value + '\0');
        var ptr = (byte*)Marshal.AllocHGlobal(bytes.Length);
        Marshal.Copy(bytes, 0, (nint)ptr, bytes.Length);
        return ptr;
    }

    public static byte** StringArrayToPtr(IReadOnlyList<string> values)
    {
        var arrayPtr = (byte**)Marshal.AllocHGlobal(values.Count * sizeof(nint));
        for (var i = 0; i < values.Count; i++)
        {
            arrayPtr[i] = StringToPtr(values[i]);
        }

        return arrayPtr;
    }

    public static string PtrToString(nint ptr)
    {
        return Marshal.PtrToStringUTF8(ptr) ?? string.Empty;
    }

    public static void Free(nint ptr)
    {
        if (ptr != 0)
        {
            Marshal.FreeHGlobal(ptr);
        }
    }

    public static void FreeStringArray(byte** values, int count)
    {
        if (values is null)
        {
            return;
        }

        for (var i = 0; i < count; i++)
        {
            Free((nint)values[i]);
        }

        Free((nint)values);
    }
}
