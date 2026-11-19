using System.Runtime.InteropServices;
using Duxel.Core;
using System.Collections.Generic;
using System.Runtime.Versioning;
using System.IO;
using System.Text;

namespace Duxel.Platform.Windows;

[SupportedOSPlatform("windows")]
public sealed partial class WindowsImeHandler : IUiImeHandler, IDisposable
{
    private readonly nint _hwnd;
    private readonly Action? _requestFrame;
    private readonly Action? _resetCharacterSequence;
    private bool _isEnabled = true;
    private readonly int _ownerThreadId;
    private readonly WndProcDelegate _wndProc;
    private nint _previousWndProc;
    private string? _compositionText;
    private readonly object _imeStateLock = new();
    private readonly Dictionary<string, string> _committedByInput = new(StringComparer.Ordinal);
    private string? _recentCommittedText;
    private bool _isComposing;
    private bool _compositionHadResult;
    private string? _lastCompositionResultText;
    private string? _activeInputId;
    private string? _compositionOwnerId;
    private CharacterMessageState _characterMessages, _imeCharacterMessages;
    private struct CharacterMessageState
    {
        internal WindowsCharacterDecoder Decoder;
        internal string? Owner;
    }
    private TsfUiElementSuppressor? _tsfUiElementSuppressor;
    private PendingCaretPlacement? _pendingCaretPlacement;
    private bool _disposed;
    private readonly string? _diagnosticsLogPath = Environment.GetEnvironmentVariable("DUXEL_IME_DIAG_LOG");
    private readonly bool _diagnosticsEnabled = ReadDiagnosticsEnabled();
    private readonly object _diagnosticsLock = new();

    public WindowsImeHandler(nint hwnd, Action? requestFrame = null) : this(hwnd, requestFrame, null)
    {
    }

    internal WindowsImeHandler(nint hwnd, Action? requestFrame, Action? resetCharacterSequence,
        Action<uint, uint, IUiInputDispatch, string?>? orderedInput = null)
    {
        if (hwnd == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(hwnd), "Window handle must be non-zero.");
        }

        _hwnd = hwnd;
        _requestFrame = requestFrame;
        _orderedInput = orderedInput;
        _resetCharacterSequence = resetCharacterSequence;
        _ownerThreadId = Environment.CurrentManagedThreadId;
        _wndProc = WindowProc;
        InstallWindowHook();
        _tsfUiElementSuppressor = TsfUiElementSuppressor.TryCreate(DiagLog);
        DiagLog($"ctor hwnd=0x{_hwnd:X} tsfSink={(_tsfUiElementSuppressor is not null ? "on" : "off")}");
    }

    public void SetCaretRect(UiRect caretRect, UiRect inputRect, float fontPixelHeight, float fontPixelWidth)
    {
        QueueCaretPlacement(caretRect, inputRect, fontPixelHeight, fontPixelWidth);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        lock (_imeStateLock)
        {
            ResetCharacterMessages();
            _pendingCaretPlacement = null;
            _completionRequestedSession = null;
        }
        _tsfUiElementSuppressor?.Dispose();
        _tsfUiElementSuppressor = null;
        if (_previousWndProc != 0)
        {
            _ = User32.SetWindowLongPtr(_hwnd, GwlpWndProc, _previousWndProc);
            _previousWndProc = 0;
        }
    }

    private bool ApplyCaretRect(UiRect caretRect, UiRect inputRect, float fontPixelHeight, float fontPixelWidth)
    {
        var validationHandled = false;
        var validationApplied = false;
        ApplyValidationCaretPlacement(caretRect, inputRect, fontPixelHeight, fontPixelWidth, ref validationHandled, ref validationApplied);
        if (validationHandled)
            return validationApplied;
        var himc = Imm32.ImmGetContext(_hwnd);
        if (himc == 0)
        {
            return false;
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
                return false;
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
                return false;
            }

            var candidatePosY = caretY + Math.Max(1, (int)MathF.Round(caretRect.Height));
            var candidateForm = new CANDIDATEFORM
            {
                dwIndex = 0,
                dwStyle = CfsExclude,
                ptCurrentPos = new POINT(caretX, candidatePosY),
                rcArea = new RECT(
                    (int)MathF.Round(inputRect.X),
                    (int)MathF.Round(inputRect.Y),
                    (int)MathF.Round(inputRect.X + inputRect.Width),
                    (int)MathF.Round(inputRect.Y + inputRect.Height)
                )
            };

            for (uint index = 0; index < 4; index++)
            {
                candidateForm.dwIndex = index;
                _ = Imm32.ImmSetCandidateWindow(himc, ref candidateForm);
            }
            return true;
        }
        finally
        {
            Imm32.ImmReleaseContext(_hwnd, himc);
        }
    }

    public string? GetCompositionText()
    {
        ThrowIfImeOperationFailed();
        if (!_isEnabled)
        {
            return null;
        }

        lock (_imeStateLock)
        {
            if (_isComposing && (_compositionOwnerId is null
                || !string.Equals(_compositionOwnerId, _activeInputId, StringComparison.Ordinal)))
                return null;
            return _compositionText;
        }
    }

    public void SetCompositionOwner(string? inputId)
    {
        UpdateCompositionOwner(inputId);
    }

    public string? ConsumeCommittedText(string inputId)
    {
        ThrowIfImeOperationFailed();
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
        ThrowIfImeOperationFailed();
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
        if (msg == WmDuxelImeCaretPlacement)
        {
            DispatchCaretPlacement();
            return 0;
        }
        if (msg == WmDuxelImeCompleteComposition)
        {
            DispatchCompositionCompletion((uint)wParam);
            return 0;
        }
        if (msg == WmDuxelImeCompletionFailure)
        {
            DispatchCompositionFailure((uint)wParam, lParam != 0);
            return 0;
        }

        if (msg == WmImeSetContext)
        {
            DiagLog($"WM_IME_SETCONTEXT wParam={wParam} lParam=0x{(nuint)lParam:X}");
            var adjustedLParam = wParam != 0
                ? (nint)((nuint)lParam & ~IscShowUiCompositionWindow)
                : lParam;

            if (_previousWndProc != 0)
            {
                return User32.CallWindowProc(_previousWndProc, hwnd, msg, wParam, adjustedLParam);
            }

            return User32.DefWindowProc(hwnd, msg, wParam, adjustedLParam);
        }

        if (_isEnabled && TryCaptureOrderedInput(msg, (nuint)wParam, lParam, out var handledOrderedInput))
        {
            if (handledOrderedInput)
                return 0;
        }
        else if (_isEnabled)
        {
            switch (msg)
            {
                case WmChar:
                    if (AppendCommittedMessage(msg, (uint)wParam, WindowsInputRepetition.Count((nuint)lParam)))
                        RequestFrame();

                    break;
                case WmImeChar:
                    if (AppendCommittedMessage(msg, (uint)wParam))
                        RequestFrame();

                    return 0;
                case WmImeStartComposition:
                    lock (_imeStateLock)
                    {
                        _compositionSession = unchecked(_compositionSession + 1);
                        _completionRequestedSession = null;
                        ResetCharacterMessages();
                        _isComposing = true;
                        _compositionHadResult = false;
                        _lastCompositionResultText = null;
                        _compositionText = string.Empty;
                        _compositionOwnerId = _activeInputId;
                    }
                    RequestFrame();
                    return 0;
                case WmImeComposition:
                    UpdateCompositionFromImmContext((nuint)lParam);
                    RequestFrame();
                    return 0;
                case WmImeEndComposition:
                    lock (_imeStateLock)
                    {
                        ResetCharacterMessages();
                        _isComposing = false;
                        _compositionHadResult = false;
                        _lastCompositionResultText = null;
                        _compositionText = null;
                        _compositionOwnerId = null;
                        _completionRequestedSession = null;
                    }
                    QueueDeferredCaretPlacement();
                    RequestFrame();
                    return 0;
                case WmKillFocus:
                    lock (_imeStateLock)
                        ResetCharacterMessages();
                    break;
            }
        }

        if (_previousWndProc != 0)
        {
            return User32.CallWindowProc(_previousWndProc, hwnd, msg, wParam, lParam);
        }

        return User32.DefWindowProc(hwnd, msg, wParam, lParam);
    }

    private void UpdateCompositionFromImmContext(nuint flags)
    {
        var hasResult = (flags & GcsResultStr) != 0;
        var hasComposition = (flags & GcsCompStr) != 0;
        var result = hasResult ? ReadCompositionString(GcsResultStr) : null;
        var composition = hasComposition ? ReadCompositionString(GcsCompStr) : null;
        ApplyComposition(flags, result, composition);
    }

    private void ApplyComposition(nuint flags, string? result, string? composition)
    {
        var hasResult = (flags & GcsResultStr) != 0;
        var hasComposition = (flags & GcsCompStr) != 0;
        lock (_imeStateLock)
        {
            // A result completes the previous segment; COMPSTR can start the next in the same message.
            if ((flags & GcsNotificationMask) == 0)
                ResetCharacterMessages();
            if (!string.IsNullOrEmpty(result))
            {
                if (!_compositionHadResult || !string.IsNullOrEmpty(_compositionText)
                    || !string.Equals(result, _lastCompositionResultText, StringComparison.Ordinal))
                    AppendCommittedText(result);
                _compositionHadResult = true;
                _lastCompositionResultText = result;
            }
            if (hasComposition)
                _compositionText = composition ?? string.Empty;
            else if (hasResult || (flags & GcsNotificationMask) == 0)
                _compositionText = string.Empty;
        }
    }

    partial void ReadValidationCompositionString(uint index, ref bool handled, ref string? text);
    partial void ApplyValidationCaretPlacement(UiRect caret, UiRect input, float height, float width, ref bool handled, ref bool applied);
    partial void NotifyValidationComposition(uint action, ref bool handled, ref bool accepted);

    private string? ReadCompositionString(uint index)
    {
        var validationHandled = false;
        string? validationText = null;
        ReadValidationCompositionString(index, ref validationHandled, ref validationText);
        if (validationHandled)
            return validationText;
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
            var ownerId = _isComposing ? _compositionOwnerId : _activeInputId;
            if (string.IsNullOrEmpty(ownerId))
            {
                return;
            }

            AppendCommittedTextForInput(ownerId, text);
        }
    }

    private void AppendCommittedTextForInput(string inputId, string text)
    {
        if (_committedByInput.TryGetValue(inputId, out var existing))
        {
            _committedByInput[inputId] = string.Concat(existing, text);
        }
        else
        {
            _committedByInput[inputId] = text;
        }

        _recentCommittedText = string.IsNullOrEmpty(_recentCommittedText)
            ? text
            : string.Concat(_recentCommittedText, text);
    }

    private void AppendCommittedMessageText(string inputId, string text, int repeat)
    {
        lock (_imeStateLock)
        {
            if (!_isComposing)
            {
                AppendCommittedTextForInput(inputId, WindowsInputRepetition.Repeat(text, repeat));

                return;
            }

            if (_compositionHadResult)
            {
                if (!string.Equals(text, _lastCompositionResultText, StringComparison.Ordinal)
                    && (repeat == 1 || !WindowsInputRepetition.Matches(_lastCompositionResultText, text, repeat)))
                {
                    AppendCommittedTextForInput(inputId, WindowsInputRepetition.Repeat(text, repeat));
                }

                return;
            }

            var targetId = inputId;

            if (text == " " && !string.IsNullOrEmpty(_compositionText))
            {
                var fallbackResultText = _compositionText;
                AppendCommittedTextForInput(targetId, _compositionText);
                AppendCommittedTextForInput(targetId, WindowsInputRepetition.Repeat(text, repeat));
                _compositionText = string.Empty;
                _lastCompositionResultText = fallbackResultText;
            }
            else
            {
                AppendCommittedTextForInput(targetId, WindowsInputRepetition.Repeat(text, repeat));
                _compositionText = string.Empty;
                _lastCompositionResultText = text;
            }

            _compositionHadResult = true;
        }
    }

    private void ResetCharacterMessages()
    {
        _characterMessages = _imeCharacterMessages = default;
        if (_orderedInput is null)
            _resetCharacterSequence?.Invoke();
    }

    private bool AppendCommittedMessage(uint message, uint codepoint, int repeat = 1)
    {
        lock (_imeStateLock)
        {
            ref var state = ref (message == WmChar ? ref _characterMessages : ref _imeCharacterMessages);
            var completing = codepoint <= char.MaxValue && char.IsLowSurrogate((char)codepoint)
                && state.Decoder.HasPendingHighSurrogate;
            var owner = completing ? state.Owner : _isComposing ? _compositionOwnerId : _activeInputId;
            if (!state.Decoder.TryDecode(codepoint, repeat, out var scalar, out var scalarRepeat))
            {
                state.Owner = state.Decoder.HasPendingHighSurrogate ? owner : null;
                return false;
            }
            state.Owner = null;
            if (owner is null)
                return false;
            AppendCommittedMessageText(owner, new Rune((int)scalar).ToString(), scalarRepeat);
            return true;
        }
    }

    private void RequestFrame()
    {
        _requestFrame?.Invoke();
    }

    private static bool ReadDiagnosticsEnabled()
    {
        var explicitPath = Environment.GetEnvironmentVariable("DUXEL_IME_DIAG_LOG");
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            return true;
        }

        var raw = Environment.GetEnvironmentVariable("DUXEL_IME_DIAG");
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        return raw == "1"
            || raw.Equals("true", StringComparison.OrdinalIgnoreCase)
            || raw.Equals("on", StringComparison.OrdinalIgnoreCase)
            || raw.Equals("yes", StringComparison.OrdinalIgnoreCase);
    }

    private void DiagLog(string message)
    {
        if (!_diagnosticsEnabled)
        {
            return;
        }

        var line = $"[{DateTime.Now:HH:mm:ss.fff}] IME {message}";
        try
        {
            if (!string.IsNullOrWhiteSpace(_diagnosticsLogPath))
            {
                lock (_diagnosticsLock)
                {
                    var directory = Path.GetDirectoryName(_diagnosticsLogPath);
                    if (!string.IsNullOrWhiteSpace(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    File.AppendAllText(_diagnosticsLogPath, line + Environment.NewLine);
                }
            }
            else
            {
                Console.WriteLine(line);
            }
        }
        catch (Exception ex)
        {
            try
            {
                var fallback = Path.Combine(Path.GetTempPath(), "duxel-ime-diag-fallback.log");
                var fallbackLine = $"[{DateTime.Now:HH:mm:ss.fff}] IME_LOG_FAIL {ex.GetType().Name}: {ex.Message} | {line}";
                File.AppendAllText(fallback, fallbackLine + Environment.NewLine);
            }
            catch
            {
            }
        }
    }

    private const uint CfsPoint = 0x0002;
    private const uint CfsForcePosition = 0x0020;
    private const uint CfsExclude = 0x0080;
    private const uint GcsCompStr = 0x0008;
    private const uint GcsResultStr = 0x0800;
    // All GCS_* notification flags in imm.h; CS_INSERTCHAR/CS_NOMOVECARET are separate.
    private const uint GcsNotificationMask = 0x1FBF;
    private const byte DefaultCharset = 1;
    private const int GwlpWndProc = -4;
    private const uint WmImeStartComposition = 0x010D;
    private const uint WmImeComposition = 0x010F;
    private const uint WmImeEndComposition = 0x010E;
    private const uint WmImeSetContext = 0x0281;
    private const uint WmImeChar = 0x0286;
    private const uint WmChar = 0x0102;
    private const uint WmKillFocus = 0x0008;
    private const uint WmDuxelImeCaretPlacement = 0x8000 + 0x3D;
    private const nuint IscShowUiCompositionWindow = 0x80000000u;

    private delegate nint WndProcDelegate(nint hwnd, uint msg, nint wParam, nint lParam);

    private readonly record struct PendingCaretPlacement(
        UiRect CaretRect,
        UiRect InputRect,
        float FontPixelHeight,
        float FontPixelWidth,
        string Input,
        uint FocusVersion,
        uint Session);

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

    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    [SupportedOSPlatform("windows")]
    private sealed class TsfUiElementSuppressor : ITfUIElementSink, IDisposable
    {
        private readonly ITfThreadMgr _threadManager;
        private readonly ITfUIElementMgr _uiElementManager;
        private readonly ITfSource _source;
        private uint _cookie;

        private TsfUiElementSuppressor(ITfThreadMgr threadManager, ITfUIElementMgr uiElementManager, ITfSource source, uint cookie)
        {
            _threadManager = threadManager;
            _uiElementManager = uiElementManager;
            _source = source;
            _cookie = cookie;
        }

        public static TsfUiElementSuppressor? TryCreate(Action<string>? diagLog)
        {
            try
            {
                var coInitHr = Ole32.CoInitializeEx(0, Ole32.CoinitApartmentThreaded);
                diagLog?.Invoke($"CoInitializeEx hr=0x{coInitHr:X8}");
                if (coInitHr < 0 && coInitHr != Ole32.RpcEChangedMode)
                {
                    return null;
                }

                var clsid = ClsidTfThreadMgr;
                var iidThreadMgr = IidTfThreadMgr;
                var createHr = Ole32.CoCreateInstance(ref clsid, nint.Zero, Ole32.ClsctxInprocServer, ref iidThreadMgr, out var threadManagerPtr);
                diagLog?.Invoke($"CoCreateInstance(ITfThreadMgr) hr=0x{createHr:X8}");
                if (createHr < 0 || threadManagerPtr == nint.Zero)
                {
                    return null;
                }

                ITfThreadMgr? threadManager;
                try
                {
                    threadManager = Marshal.GetObjectForIUnknown(threadManagerPtr) as ITfThreadMgr;
                }
                finally
                {
                    _ = Marshal.Release(threadManagerPtr);
                }

                if (threadManager is null)
                {
                    return null;
                }

                var activateHr = threadManager.Activate(out _);
                diagLog?.Invoke($"ITfThreadMgr.Activate hr=0x{activateHr:X8}");
                if (activateHr < 0)
                {
                    return null;
                }

                if (threadManager is not ITfUIElementMgr uiElementManager)
                {
                    return null;
                }

                if (uiElementManager is not ITfSource source)
                {
                    return null;
                }

                var sink = new TsfUiElementSuppressor(threadManager, uiElementManager, source, 0);
                var iid = IidTfUiElementSink;
                var adviseHr = source.AdviseSink(ref iid, sink, out var cookie);
                diagLog?.Invoke($"ITfSource.AdviseSink(ITfUIElementSink) hr=0x{adviseHr:X8}");
                if (adviseHr < 0)
                {
                    return null;
                }

                sink._cookie = cookie;
                return sink;
            }
            catch
            {
                diagLog?.Invoke("TSF create exception");
                return null;
            }
        }

        public int BeginUIElement(uint elementId, [MarshalAs(UnmanagedType.Bool)] out bool show)
        {
            show = false;
            return 0;
        }

        public int UpdateUIElement(uint elementId)
        {
            return 0;
        }

        public int EndUIElement(uint elementId)
        {
            return 0;
        }

        public void Dispose()
        {
            if (_cookie != 0)
            {
                _ = _source.UnadviseSink(_cookie);
                _cookie = 0;
            }

            _ = _threadManager.Deactivate();
        }

        private static readonly Guid ClsidTfThreadMgr = new("529A9E6B-6587-4F23-AB9E-9C7D683E3C50");
        private static readonly Guid IidTfThreadMgr = new("AA80E801-2021-11D2-93E0-0060B067B86E");
        private static readonly Guid IidTfUiElementSink = new("EA1EA136-19DF-11D7-A6D2-00065B84435C");
    }

}

internal static partial class Ole32
{
    internal const uint CoinitApartmentThreaded = 0x2;
    internal const uint ClsctxInprocServer = 0x1;
    internal const int RpcEChangedMode = unchecked((int)0x80010106);

    [LibraryImport("ole32.dll")]
    internal static partial int CoInitializeEx(nint pvReserved, uint coInit);

    [LibraryImport("ole32.dll")]
    internal static partial int CoCreateInstance(
        ref Guid rclsid,
        nint pUnkOuter,
        uint dwClsContext,
        ref Guid riid,
        out nint ppv);
}

[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("AA80E801-2021-11D2-93E0-0060B067B86E")]
internal interface ITfThreadMgr
{
    int Activate(out uint clientId);
    int Deactivate();
}

[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("EA1EA135-19DF-11D7-A6D2-00065B84435C")]
internal interface ITfUIElementMgr
{
}

[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("4EA48A35-60AE-446F-8FD6-E6A8D82459F7")]
internal interface ITfSource
{
    int AdviseSink(ref Guid riid, [MarshalAs(UnmanagedType.IUnknown)] object sink, out uint cookie);
    int UnadviseSink(uint cookie);
}

[ComVisible(true)]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("EA1EA136-19DF-11D7-A6D2-00065B84435C")]
internal interface ITfUIElementSink
{
    int BeginUIElement(uint elementId, [MarshalAs(UnmanagedType.Bool)] out bool show);
    int UpdateUIElement(uint elementId);
    int EndUIElement(uint elementId);
}

internal static partial class User32
{
    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    internal static partial nint SetWindowLongPtr(nint hWnd, int nIndex, nint dwNewLong);

    [LibraryImport("user32.dll", EntryPoint = "CallWindowProcW")]
    internal static partial nint CallWindowProc(nint lpPrevWndFunc, nint hWnd, uint msg, nint wParam, nint lParam);

    [LibraryImport("user32.dll", EntryPoint = "DefWindowProcW")]
    internal static partial nint DefWindowProc(nint hWnd, uint msg, nint wParam, nint lParam);

    [LibraryImport("user32.dll", EntryPoint = "SendMessageW")]
    internal static partial nint SendMessage(nint hWnd, uint Msg, nuint wParam, nint lParam);

    [LibraryImport("user32.dll", EntryPoint = "PostMessageW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool PostMessage(nint hWnd, uint Msg, nuint wParam, nint lParam);
}

internal static partial class Imm32
{
    [LibraryImport("imm32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool ImmNotifyIME(nint hIMC, uint action, uint index, uint value);
    [LibraryImport("imm32.dll", SetLastError = true)]
    internal static partial nint ImmGetDefaultIMEWnd(nint hWnd);

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

    [LibraryImport("imm32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool ImmSetStatusWindowPos(nint hIMC, ref WindowsImeHandler.POINT lpptPos);

    [LibraryImport("imm32.dll", EntryPoint = "ImmGetCompositionStringW", SetLastError = true)]
    internal static partial int ImmGetCompositionString(nint hIMC, uint dwIndex, nint lpBuf, uint dwBufLen);
}

