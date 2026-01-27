using System.Runtime.InteropServices;
using Dux.Core;

namespace Dux.Platform.Windows;

public sealed class WindowsImeHandler : IUiImeHandler
{
    private readonly nint _hwnd;
    private bool _isEnabled = true;
    private readonly int _ownerThreadId;

    public WindowsImeHandler(nint hwnd)
    {
        if (hwnd == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(hwnd), "Window handle must be non-zero.");
        }

        _hwnd = hwnd;
        _ownerThreadId = Environment.CurrentManagedThreadId;
    }

    public void SetCaretRect(UiRect caretRect, UiRect inputRect, float fontPixelHeight, float fontPixelWidth)
    {
        if (!_isEnabled)
        {
            return;
        }

        if (Environment.CurrentManagedThreadId != _ownerThreadId)
        {
            return;
        }

        var himc = Imm32.ImmGetContext(_hwnd);
        if (himc == 0)
        {
            _isEnabled = false;
            return;
        }

        try
        {
            var caretX = (int)MathF.Round(caretRect.X);
            var caretY = (int)MathF.Round(caretRect.Y);

            var compositionForm = new COMPOSITIONFORM
            {
                dwStyle = CfsPoint | CfsForcePosition,
                ptCurrentPos = new POINT(caretX, caretY),
                rcArea = new RECT(
                    (int)MathF.Round(inputRect.X),
                    (int)MathF.Round(inputRect.Y),
                    (int)MathF.Round(inputRect.X + inputRect.Width),
                    (int)MathF.Round(inputRect.Y + inputRect.Height)
                )
            };

            if (!Imm32.ImmSetCompositionWindow(himc, ref compositionForm))
            {
                _isEnabled = false;
                return;
            }

            var fontHeight = -Math.Abs((int)MathF.Round(fontPixelHeight));
            var width = (int)MathF.Round(MathF.Max(0f, fontPixelWidth));
            var logFont = new LOGFONT
            {
                lfHeight = fontHeight,
                lfWidth = width,
                lfCharSet = DefaultCharset
            };

            SetFaceName(ref logFont, "Malgun Gothic");

            if (!Imm32.ImmSetCompositionFont(himc, ref logFont))
            {
                _isEnabled = false;
                return;
            }

            var candidateForm = new CANDIDATEFORM
            {
                dwIndex = 0,
                dwStyle = CfsExclude,
                ptCurrentPos = new POINT(caretX, caretY),
                rcArea = compositionForm.rcArea
            };

            if (!Imm32.ImmSetCandidateWindow(himc, ref candidateForm))
            {
                _isEnabled = false;
            }
        }
        finally
        {
            Imm32.ImmReleaseContext(_hwnd, himc);
        }
    }

    private const uint CfsPoint = 0x0002;
    private const uint CfsForcePosition = 0x0020;
    private const uint CfsExclude = 0x0080;
    private const byte DefaultCharset = 1;

    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct POINT
    {
        public readonly int X;
        public readonly int Y;

        public POINT(int x, int y)
        {
            X = x;
            Y = y;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct RECT
    {
        public readonly int Left;
        public readonly int Top;
        public readonly int Right;
        public readonly int Bottom;

        public RECT(int left, int top, int right, int bottom)
        {
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct COMPOSITIONFORM
    {
        public uint dwStyle;
        public POINT ptCurrentPos;
        public RECT rcArea;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct CANDIDATEFORM
    {
        public uint dwIndex;
        public uint dwStyle;
        public POINT ptCurrentPos;
        public RECT rcArea;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct LOGFONT
    {
        public int lfHeight;
        public int lfWidth;
        public int lfEscapement;
        public int lfOrientation;
        public int lfWeight;
        public byte lfItalic;
        public byte lfUnderline;
        public byte lfStrikeOut;
        public byte lfCharSet;
        public byte lfOutPrecision;
        public byte lfClipPrecision;
        public byte lfQuality;
        public byte lfPitchAndFamily;

        public fixed char lfFaceName[32];
    }

    private static unsafe void SetFaceName(ref LOGFONT logFont, string faceName)
    {
        var length = Math.Min(faceName.Length, 31);
        for (var i = 0; i < length; i++)
        {
            logFont.lfFaceName[i] = faceName[i];
        }
        logFont.lfFaceName[length] = '\0';
    }

}

internal static partial class Imm32
{
    [LibraryImport("imm32.dll", SetLastError = true)]
    internal static partial nint ImmGetContext(nint hWnd);

    [LibraryImport("imm32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool ImmReleaseContext(nint hWnd, nint hIMC);

    [LibraryImport("imm32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool ImmSetCompositionWindow(nint hIMC, ref WindowsImeHandler.COMPOSITIONFORM lpCompForm);

    [LibraryImport("imm32.dll", EntryPoint = "ImmSetCompositionFontW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool ImmSetCompositionFont(nint hIMC, ref WindowsImeHandler.LOGFONT lplf);

    [LibraryImport("imm32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool ImmSetCandidateWindow(nint hIMC, ref WindowsImeHandler.CANDIDATEFORM lpCandidate);

    [LibraryImport("imm32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool ImmGetOpenStatus(nint hIMC);
}
