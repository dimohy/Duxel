namespace Duxel.Platform.Windows;

public sealed partial class WindowsImeHandler
{
    public void ReleaseInput(string inputId)
    {
        ArgumentException.ThrowIfNullOrEmpty(inputId);
        uint? complete = null;
        lock (_imeStateLock)
        {
            _committedByInput.Remove(inputId);
            // Keep a pending UTF-16 unit ownerless so its low half cannot attach to later focus.
            if (_characterMessages.Owner == inputId)
                _characterMessages.Owner = null;
            if (_imeCharacterMessages.Owner == inputId)
                _imeCharacterMessages.Owner = null;
            if (_activeInputId == inputId)
            {
                _activeInputId = null;
                _focusVersion = unchecked(_focusVersion + 1);
            }
            if (_compositionOwnerId == inputId)
            {
                _compositionOwnerId = null;
                _compositionText = null;
            }
            if (_pendingCaretPlacement?.Input == inputId)
                _pendingCaretPlacement = null;
            if (_lastAppliedCaretPlacement?.Input == inputId)
                _lastAppliedCaretPlacement = null;
            if (!_disposed && _operationFailure is null && _isComposing && NativeCompositionCurrent
                && (_compositionOwnerId is null || _compositionOwnerId != _activeInputId) && _completionRequestedSession != _compositionSession)
                complete = _completionRequestedSession = _compositionSession;
        }
        if (complete is { } session)
            PostImeCommand(WmDuxelImeCompleteComposition, session);
    }
}
