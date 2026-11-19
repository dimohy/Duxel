namespace Duxel.Platform.Windows;

public sealed partial class WindowsImeHandler
{
    internal readonly record struct ValidationCaret(Duxel.Core.UiRect Caret, string? Input, int Thread);
    internal readonly record struct ValidationCompletion(uint Action, string? Owner, int Thread, bool LockHeld);
    internal List<ValidationCaret> ValidationCaretPlacements { get; } = [];
    internal List<ValidationCompletion> ValidationCompletions { get; } = [];
    internal Func<uint, bool>? ValidationCompositionNotification
    {
        get; set;
    }
    internal Func<bool>? ValidationCaretAcceptance
    {
        get; set;
    }
    partial void ApplyValidationCaretPlacement(Duxel.Core.UiRect caret, Duxel.Core.UiRect input, float height, float width, ref bool handled, ref bool applied)
    {
        handled = true;
        ValidationCaretPlacements.Add(new(caret, _activeInputId, Environment.CurrentManagedThreadId));
        applied = ValidationCaretAcceptance?.Invoke() ?? true;
    }
    partial void NotifyValidationComposition(uint action, ref bool handled, ref bool accepted)
    {
        if (!_validationCompositionStrings && ValidationCompositionNotification is null)
            return;
        handled = true;
        ValidationCompletions.Add(new(action, _compositionOwnerId, Environment.CurrentManagedThreadId,
            Monitor.IsEntered(_imeStateLock)));
        accepted = ValidationCompositionNotification?.Invoke(action) ?? true;
    }
    private bool _validationCompositionStrings;
    private string? _validationPreedit, _validationResult;
    // Injects only IMM extraction data; lifecycle/result messages still traverse the real HWND hook.
    internal void ValidationSetCompositionStrings(string preedit, string result)
    {
        lock (_imeStateLock)
        {
            if (!_isComposing && !(_orderedInput is not null && _capturedComposing))
                throw new InvalidOperationException("A native composition-start message is required.");
            _validationCompositionStrings = true;
            _validationPreedit = preedit;
            _validationResult = result;
        }
    }
    partial void ReadValidationCompositionString(uint index, ref bool handled, ref string? text)
    {
        lock (_imeStateLock)
        {
            if (!_validationCompositionStrings)
                return;
            handled = true;
            text = index switch
            {
                GcsCompStr => _validationPreedit,
                GcsResultStr => _validationResult,
                _ => throw new NotSupportedException("Unsupported validation IMM string.")
            };
        }
    }
}

internal sealed partial class WindowsDirectWriteGlyphRasterizer
{
    internal static Duxel.Interop.Windows.DirectWriteTextShaper.Glyph[] ShapeForValidation(string path, string text, float size, bool rtl = false)
    {
        lock (Sync)
        {
            if (!TryGetFontFace(path, out var face))
                throw new InvalidOperationException("Validation font face could not be loaded.");
            return Duxel.Interop.Windows.DirectWriteTextShaper.Shape(_factory, GetTextAnalyzer(), face, text, size, rtl);
        }
    }
}

// Compiled only into validation builds, never the shipped Windows backend.
public sealed partial class WindowsPlatformBackend
{
    internal static WindowsPlatformBackend CreatePixelSizedValidationWindow(WindowsPlatformBackendOptions options)
        => new(options, clientSizeInPhysicalPixels : true);

    internal float? ValidationInputCoordinateScale
    {
        get; set;
    }
    partial void OverrideValidationInputCoordinateScale(ref float scale)
    {
        if (ValidationInputCoordinateScale is { } value)
            scale = value;
    }
    internal void ValidationCaptureOrderedPointer(Duxel.Core.UiVector2 position, bool down, double? eventTime = null)
        => _inputBackend.CapturePointer(position, down, eventTime);
    internal static double ValidationInputMessageTime(uint postedMilliseconds, ulong nowMilliseconds)
        => MessageTimeSeconds(postedMilliseconds, nowMilliseconds);
    internal static double ValidationInputClockSeconds() => GetTickCount64() / 1000d;
    private int _validationResizeCommits, _validationUnpresentedResizeCommits;
    public int ValidationResizeCommits => Volatile.Read(ref _validationResizeCommits);
    public int ValidationUnpresentedResizeCommits => Volatile.Read(ref _validationUnpresentedResizeCommits);
    public bool ValidationResizePresentationWaitActive => Volatile.Read(ref _resizePresentationWaitActive);
    public long ValidationPresentedResizeSequence => Volatile.Read(ref _presentedResizeSequence);
    public bool ValidationResizeWaitCancelled => Volatile.Read(ref _interactiveResizeWaitCancelled) != 0;
    private int _validationResizeCancellations;
    public int ValidationResizeCancellations => Volatile.Read(ref _validationResizeCancellations);
    partial void ObserveResizeCancellation() => Interlocked.Increment(ref _validationResizeCancellations);
    public bool ValidationWindowShown => Volatile.Read(ref _windowShown) != 0;
    private int _validationResizeSystemMessages;
    public int ValidationResizeSystemMessages => Volatile.Read(ref _validationResizeSystemMessages);
    partial void ObserveResizeSystemMessage(uint message)
    {
        if (message == 0x047B)
            Interlocked.Increment(ref _validationResizeSystemMessages);
    }

    partial void SelectValidationResizePresentationWait(ref bool enabled)
    {
        var value = Environment.GetEnvironmentVariable("DUXEL_VALIDATION_RESIZE_PRESENT_WAIT");
        if (value is null)
            return;
        enabled = value switch
        {
            "0" => false,
            "1" => true,
            _ => throw new InvalidOperationException("Invalid resize presentation validation mode.")
        };
    }

    partial void ObserveResizeCommit(long committedSequence, long presentedSequence)
    {
        Interlocked.Increment(ref _validationResizeCommits);
        if (presentedSequence < committedSequence)
            Interlocked.Increment(ref _validationUnpresentedResizeCommits);
    }
}
