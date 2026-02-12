using System.Runtime.InteropServices;
using Duxel.Core;
using System.Collections.Generic;

namespace Duxel.Platform.Windows;

public sealed class WindowsImeHandler : IUiImeHandler
{
    private readonly nint _hwnd;
    private bool _isEnabled = true;
    private readonly int _ownerThreadId;
    private readonly WndProcDelegate _wndProc;
    private nint _previousWndProc;
    private string? _compositionText;
    private readonly object _imeStateLock = new();
    private readonly Dictionary<string, string> _committedByInput = new(StringComparer.Ordinal);
    private string? _recentCommittedText;
    private bool _isComposing;
    private string? _activeInputId;
    private string? _compositionOwnerId;

    public WindowsImeHandler(nint hwnd)
    {
        if (hwnd == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(hwnd), "Window handle must be non-zero.");
        }

        _hwnd = hwnd;
        _ownerThreadId = Environment.CurrentManagedThreadId;
        _wndProc = WindowProc;
        InstallWindowHook();
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

    public string? GetCompositionText()
    {
        if (!_isEnabled)
        {
            return null;
        }

        lock (_imeStateLock)
        {
            return _compositionText;
        }
    }

    public void SetCompositionOwner(string? inputId)
    {
        lock (_imeStateLock)
        {
            _activeInputId = string.IsNullOrEmpty(inputId) ? null : inputId;
            if (!_isComposing)
            {
                _compositionOwnerId = _activeInputId;
            }
        }
    }

    public string? ConsumeCommittedText(string inputId)
    {
        if (string.IsNullOrEmpty(inputId))
        {
            return null;
        }

        lock (_imeStateLock)
        {
            if (!_committedByInput.TryGetValue(inputId, out var text))
            {
                return null;
            }

            _committedByInput.Remove(inputId);
            return text;
        }
    }

    public string? ConsumeRecentCommittedText()
    {
        lock (_imeStateLock)
        {
            if (string.IsNullOrEmpty(_recentCommittedText))
            {
                return null;
            }

            var text = _recentCommittedText;
            _recentCommittedText = null;
            return text;
        }
    }

    private void InstallWindowHook()
    {
        if (Environment.CurrentManagedThreadId != _ownerThreadId)
        {
            _isEnabled = false;
            return;
        }

        var wndProcPtr = Marshal.GetFunctionPointerForDelegate(_wndProc);
        Marshal.SetLastPInvokeError(0);
        _previousWndProc = User32.SetWindowLongPtr(_hwnd, GwlpWndProc, wndProcPtr);
        if (_previousWndProc == 0 && Marshal.GetLastPInvokeError() != 0)
        {
            _isEnabled = false;
        }
    }

    private nint WindowProc(nint hwnd, uint msg, nint wParam, nint lParam)
    {
        if (msg == WmImeSetContext)
        {
            if (wParam != 0)
            {
                var flags = (nuint)lParam;
                flags &= ~IscShowUiCompositionWindow;
                flags &= ~IscShowUiAllCandidateWindow;
                lParam = (nint)flags;
            }

            if (_previousWndProc != 0)
            {
                return User32.CallWindowProc(_previousWndProc, hwnd, msg, wParam, lParam);
            }

            return User32.DefWindowProc(hwnd, msg, wParam, lParam);
        }

        if (_isEnabled)
        {
            switch (msg)
            {
                case WmChar:
                    if (!_isComposing)
                    {
                        var codepoint = (uint)wParam;
                        if (codepoint is > 0 and <= 0xFFFF)
                        {
                            var ch = (char)codepoint;
                            if (!char.IsControl(ch))
                            {
                                AppendCommittedTextForActiveInput(ch.ToString());
                                return 0;
                            }
                        }
                    }
                    break;
                case WmImeStartComposition:
                    lock (_imeStateLock)
                    {
                        _isComposing = true;
                        _compositionText = string.Empty;
                        _compositionOwnerId = _activeInputId;
                    }
                    return 0;
                case WmImeComposition:
                    UpdateCompositionTextFromImmContext();
                    if (((nuint)lParam & GcsResultStr) != 0)
                    {
                        var committedText = ReadCompositionString(GcsResultStr);
                        if (!string.IsNullOrEmpty(committedText))
                        {
                            AppendCommittedText(committedText);
                        }

                        lock (_imeStateLock)
                        {
                            _compositionText = string.Empty;
                        }

                        return 0;
                    }

                    return 0;
                case WmImeEndComposition:
                    lock (_imeStateLock)
                    {
                        _isComposing = false;
                        _compositionText = null;
                        _compositionOwnerId = null;
                    }
                    return 0;
            }
        }

        if (_previousWndProc != 0)
        {
            return User32.CallWindowProc(_previousWndProc, hwnd, msg, wParam, lParam);
        }

        return User32.DefWindowProc(hwnd, msg, wParam, lParam);
    }

    private void UpdateCompositionTextFromImmContext()
    {
        var composition = ReadCompositionString(GcsCompStr) ?? string.Empty;
        lock (_imeStateLock)
        {
            _compositionText = composition;
        }
    }

    private string? ReadCompositionString(uint index)
    {
        var himc = Imm32.ImmGetContext(_hwnd);
        if (himc == 0)
        {
            return null;
        }

        try
        {
            var byteLength = Imm32.ImmGetCompositionString(himc, index, 0, 0);
            if (byteLength <= 0)
            {
                return string.Empty;
            }

            var charLength = byteLength / sizeof(char);
            var buffer = new char[charLength];
            unsafe
            {
                fixed (char* ptr = buffer)
                {
                    var copied = Imm32.ImmGetCompositionString(himc, index, (nint)ptr, (uint)byteLength);
                    if (copied <= 0)
                    {
                        return string.Empty;
                    }
                }
            }

            return new string(buffer);
        }
        finally
        {
            Imm32.ImmReleaseContext(_hwnd, himc);
        }
    }

    private void AppendCommittedText(string text)
    {
        lock (_imeStateLock)
        {
            if (string.IsNullOrEmpty(_compositionOwnerId))
            {
                return;
            }

            if (_committedByInput.TryGetValue(_compositionOwnerId, out var existing))
            {
                _committedByInput[_compositionOwnerId] = string.Concat(existing, text);
            }
            else
            {
                _committedByInput[_compositionOwnerId] = text;
            }

            _recentCommittedText = string.IsNullOrEmpty(_recentCommittedText)
                ? text
                : string.Concat(_recentCommittedText, text);
        }
    }

    private void AppendCommittedTextForActiveInput(string text)
    {
        lock (_imeStateLock)
        {
            if (string.IsNullOrEmpty(_activeInputId))
            {
                return;
            }

            if (_committedByInput.TryGetValue(_activeInputId, out var existing))
            {
                _committedByInput[_activeInputId] = string.Concat(existing, text);
            }
            else
            {
                _committedByInput[_activeInputId] = text;
            }

            _recentCommittedText = string.IsNullOrEmpty(_recentCommittedText)
                ? text
                : string.Concat(_recentCommittedText, text);
        }
    }

    private const uint CfsPoint = 0x0002;
    private const uint CfsForcePosition = 0x0020;
    private const uint CfsExclude = 0x0080;
    private const uint GcsCompStr = 0x0008;
    private const uint GcsResultStr = 0x0800;
    private const byte DefaultCharset = 1;
    private const int GwlpWndProc = -4;
    private const uint WmImeStartComposition = 0x010D;
    private const uint WmImeComposition = 0x010F;
    private const uint WmImeEndComposition = 0x010E;
    private const uint WmImeSetContext = 0x0281;
    private const uint WmChar = 0x0102;
    private const nuint IscShowUiAllCandidateWindow = 0x0000000Fu;
    private const nuint IscShowUiCompositionWindow = 0x80000000u;

    private delegate nint WndProcDelegate(nint hwnd, uint msg, nint wParam, nint lParam);

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

internal static partial class User32
{
    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    internal static partial nint SetWindowLongPtr(nint hWnd, int nIndex, nint dwNewLong);

    [LibraryImport("user32.dll", EntryPoint = "CallWindowProcW")]
    internal static partial nint CallWindowProc(nint lpPrevWndFunc, nint hWnd, uint msg, nint wParam, nint lParam);

    [LibraryImport("user32.dll", EntryPoint = "DefWindowProcW")]
    internal static partial nint DefWindowProc(nint hWnd, uint msg, nint wParam, nint lParam);
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

    [LibraryImport("imm32.dll", EntryPoint = "ImmGetCompositionStringW", SetLastError = true)]
    internal static partial int ImmGetCompositionString(nint hIMC, uint dwIndex, nint lpBuf, uint dwBufLen);
}

