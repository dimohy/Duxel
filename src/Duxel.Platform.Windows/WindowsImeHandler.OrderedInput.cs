using Duxel.Core;

namespace Duxel.Platform.Windows;

public sealed partial class WindowsImeHandler
{
    private readonly Action<uint, uint, IUiInputDispatch, string?>? _orderedInput;
    private uint _capturedSession;
    private bool _capturedComposing;

    private bool NativeCompositionCurrent => _orderedInput is null
        || (_capturedSession == _compositionSession && _capturedComposing == _isComposing);

    private bool TryCaptureOrderedInput(uint message, nuint character, nint flags, out bool handled)
    {
        handled = message != WmChar && message != WmKillFocus;
        if (_orderedInput is null || message is not (WmChar or WmImeChar or WmImeStartComposition or WmImeComposition or WmImeEndComposition or WmKillFocus))
            return false;
        string? result = null, composition = null;
        // IMM data belongs to this notification, not to the eventual UI dispatch time.
        if (message == WmImeComposition)
        {
            if (((nuint)flags & GcsResultStr) != 0)
                result = ReadCompositionString(GcsResultStr);
            if (((nuint)flags & GcsCompStr) != 0)
                composition = ReadCompositionString(GcsCompStr);
        }
        uint session;
        lock (_imeStateLock)
        {
            if (message == WmImeStartComposition)
            {
                _capturedSession = unchecked(_capturedSession + 1);
                _capturedComposing = true;
            }
            else if (message == WmImeEndComposition)
                _capturedComposing = false;
            session = _capturedSession;
        }
        if (message is WmImeStartComposition or WmImeEndComposition or WmKillFocus
            || (message == WmImeComposition && ((nuint)flags & GcsNotificationMask) == 0))
            _resetCharacterSequence?.Invoke();
        var glyphText = result is null ? composition : composition is null ? result : string.Concat(result, composition);
        _orderedInput(message, (uint)character, new CapturedInput(this, message, (uint)character, (nuint)flags, session, result, composition), glyphText);
        RequestFrame();
        return true;
    }

    private sealed class CapturedInput(WindowsImeHandler owner, uint message, uint character, nuint flags, uint session, string? result, string? composition) : IUiInputDispatch
    {
        public IUiImeHandler RequiredImeHandler => owner;
        public void Dispatch() => owner.DispatchCapturedInput(message, character, flags, session, result, composition);
    }

    private void DispatchCapturedInput(uint message, uint character, nuint flags, uint session, string? result, string? composition)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ThrowIfImeOperationFailed();
        switch (message)
        {
            case WmChar:
                AppendCommittedMessage(message, character, WindowsInputRepetition.Count(flags));
                break;
            case WmImeChar:
                AppendCommittedMessage(message, character);
                break;
            case WmImeStartComposition:
                lock (_imeStateLock)
                {
                    _compositionSession = session;
                    _completionRequestedSession = null;
                    ResetCharacterMessages();
                    _isComposing = true;
                    _compositionHadResult = false;
                    _lastCompositionResultText = null;
                    _compositionText = string.Empty;
                    _compositionOwnerId = _activeInputId;
                }
                break;
            case WmImeComposition:
                ApplyComposition(flags, result, composition);
                break;
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
                break;
            case WmKillFocus:
                lock (_imeStateLock)
                    ResetCharacterMessages();
                break;
        }
    }
}
