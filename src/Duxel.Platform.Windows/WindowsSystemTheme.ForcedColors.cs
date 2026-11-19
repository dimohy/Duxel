using System.ComponentModel;
using System.Runtime.InteropServices;
using Duxel.Core;

namespace Duxel.Platform.Windows;

public static partial class WindowsSystemTheme
{
    /// <summary>Reads the user's current contrast palette without changing system settings.</summary>
    public static UiForcedColorPalette? GetForcedColors()
    {
        var contrast = new HighContrast { Size = (uint)Marshal.SizeOf<HighContrast>() };
        if (!SystemParametersInfoW(0x0042, contrast.Size, ref contrast, 0))
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "SPI_GETHIGHCONTRAST failed.");
        // Like Windows Forms' SystemInformation.HighContrast, inspect flags only.
        // The getter's scheme pointer is not owned here; freeing it corrupts the native heap.
        // https://github.com/dotnet/winforms/blob/main/src/System.Windows.Forms/System/Windows/Forms/SystemInformation.cs
        return (contrast.Flags & 1) == 0 ? null : new UiForcedColorPalette(
            Color(5), Color(8), Color(17), Color(13), Color(14), Color(26), Color(15), Color(18));
    }

    private static UiColor Color(int index)
    {
        var color = GetSysColor(index); // COLORREF is 0x00BBGGRR.
        return new((byte)color, (byte)(color >> 8), (byte)(color >> 16));
    }

    // https://learn.microsoft.com/windows/win32/api/winuser/ns-winuser-highcontrastw
    [StructLayout(LayoutKind.Sequential)]
    private struct HighContrast
    {
        internal uint Size, Flags;
        internal nint DefaultScheme;
    }
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SystemParametersInfoW(uint action, uint parameter, ref HighContrast value, uint flags);
    [LibraryImport("user32.dll")]
    private static partial uint GetSysColor(int index);
}
