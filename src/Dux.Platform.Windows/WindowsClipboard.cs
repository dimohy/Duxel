using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Dux.Core;

namespace Dux.Platform.Windows;

public sealed partial class WindowsClipboard : IUiClipboard
{
    private const uint CF_UNICODETEXT = 13;
    private const uint GMEM_MOVEABLE = 0x0002;

    public string GetText()
    {
        if (!OpenClipboard(nint.Zero))
        {
            throw new InvalidOperationException($"OpenClipboard failed. Error: {Marshal.GetLastPInvokeError()}");
        }

        try
        {
            var handle = GetClipboardData(CF_UNICODETEXT);
            if (handle == nint.Zero)
            {
                return string.Empty;
            }

            var locked = GlobalLock(handle);
            if (locked == nint.Zero)
            {
                throw new InvalidOperationException($"GlobalLock failed. Error: {Marshal.GetLastPInvokeError()}");
            }

            try
            {
                return Marshal.PtrToStringUni(locked) ?? string.Empty;
            }
            finally
            {
                _ = GlobalUnlock(handle);
            }
        }
        finally
        {
            _ = CloseClipboard();
        }
    }

    public void SetText(string text)
    {
        text ??= string.Empty;
        var bytes = Encoding.Unicode.GetBytes(text + "\0");

        if (!OpenClipboard(nint.Zero))
        {
            throw new InvalidOperationException($"OpenClipboard failed. Error: {Marshal.GetLastPInvokeError()}");
        }

        try
        {
            if (!EmptyClipboard())
            {
                throw new InvalidOperationException($"EmptyClipboard failed. Error: {Marshal.GetLastPInvokeError()}");
            }

            var hGlobal = GlobalAlloc(GMEM_MOVEABLE, (nuint)bytes.Length);
            if (hGlobal == nint.Zero)
            {
                throw new InvalidOperationException($"GlobalAlloc failed. Error: {Marshal.GetLastPInvokeError()}");
            }

            var locked = GlobalLock(hGlobal);
            if (locked == nint.Zero)
            {
                _ = GlobalFree(hGlobal);
                throw new InvalidOperationException($"GlobalLock failed. Error: {Marshal.GetLastPInvokeError()}");
            }

            try
            {
                Marshal.Copy(bytes, 0, locked, bytes.Length);
            }
            finally
            {
                _ = GlobalUnlock(hGlobal);
            }

            if (SetClipboardData(CF_UNICODETEXT, hGlobal) == nint.Zero)
            {
                _ = GlobalFree(hGlobal);
                throw new InvalidOperationException($"SetClipboardData failed. Error: {Marshal.GetLastPInvokeError()}");
            }
        }
        finally
        {
            _ = CloseClipboard();
        }
    }

    [LibraryImport("user32.dll", SetLastError = true)]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvStdcall) })]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool OpenClipboard(nint hWndNewOwner);

    [LibraryImport("user32.dll", SetLastError = true)]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvStdcall) })]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseClipboard();

    [LibraryImport("user32.dll", SetLastError = true)]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvStdcall) })]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool EmptyClipboard();

    [LibraryImport("user32.dll", SetLastError = true)]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static partial nint GetClipboardData(uint uFormat);

    [LibraryImport("user32.dll", SetLastError = true)]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static partial nint SetClipboardData(uint uFormat, nint hMem);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static partial nint GlobalAlloc(uint uFlags, nuint dwBytes);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static partial nint GlobalLock(nint hMem);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvStdcall) })]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GlobalUnlock(nint hMem);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static partial nint GlobalFree(nint hMem);
}
