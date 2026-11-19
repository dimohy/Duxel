using System.ComponentModel;
using System.Runtime.InteropServices;
using Duxel.Core;

namespace Duxel.Platform.Windows;

public sealed partial class WindowsImeHandler
{
    private const uint WmDuxelImeCompleteComposition = 0x8000 + 0x3E;
    private const uint WmDuxelImeCompletionFailure = 0x8000 + 0x3F;
    private uint _focusVersion, _compositionSession;
    private uint? _completionRequestedSession;
    private bool _caretPlacementPosted;
    private PendingCaretPlacement? _lastAppliedCaretPlacement;
    private Exception? _operationFailure;

    private void UpdateCompositionOwner(string? input)
    {
        if (!string.IsNullOrEmpty(input))
            ThrowIfImeOperationFailed();
        uint? complete = null;
        lock (_imeStateLock)
        {
            if (_disposed || !_isEnabled)
                return;
            input = string.IsNullOrEmpty(input) ? null : input;
            if (_activeInputId != input)
            {
                _activeInputId = input;
                _focusVersion = unchecked(_focusVersion + 1);
                _pendingCaretPlacement = null;
            }
            if (_operationFailure is null && _isComposing && NativeCompositionCurrent && _compositionOwnerId != _activeInputId && _completionRequestedSession != _compositionSession)
            {
                complete = _completionRequestedSession = _compositionSession;
            }
        }
        if (complete is { } session)
            PostImeCommand(WmDuxelImeCompleteComposition, session);
    }

    private void QueueCaretPlacement(UiRect caret, UiRect input, float height, float width)
    {
        ThrowIfImeOperationFailed();
        var post = false;
        lock (_imeStateLock)
        {
            if (_disposed || !_isEnabled || _activeInputId is null)
                return;
            var next = new PendingCaretPlacement(caret, input, height, width, _activeInputId, _focusVersion, _compositionSession);
            if (_lastAppliedCaretPlacement == next && _pendingCaretPlacement is null)
                return;
            _pendingCaretPlacement = next;
            if (!_caretPlacementPosted)
                post = _caretPlacementPosted = true;
        }
        if (post)
            PostImeCommand(WmDuxelImeCaretPlacement);
    }

    private void QueueDeferredCaretPlacement()
    {
        var post = false;
        lock (_imeStateLock)
            if (!_disposed && _pendingCaretPlacement is not null && !_caretPlacementPosted)
                post = _caretPlacementPosted = true;
        if (post)
            PostImeCommand(WmDuxelImeCaretPlacement);
    }

    private void DispatchCaretPlacement()
    {
        PendingCaretPlacement? accepted = null;
        lock (_imeStateLock)
        {
            _caretPlacementPosted = false;
            if (_disposed || _operationFailure is not null || _pendingCaretPlacement is not { } placement)
                return;
            if (!NativeCompositionCurrent)
                return;
            if (placement.FocusVersion != _focusVersion || placement.Session != _compositionSession || placement.Input != _activeInputId)
                _pendingCaretPlacement = null;
            else if (!_isComposing || _compositionOwnerId == placement.Input)
            {
                accepted = placement;
                _pendingCaretPlacement = null;
            }
        }
        if (accepted is { } current)
        {
            if (ApplyCaretRect(current.CaretRect, current.InputRect, current.FontPixelHeight, current.FontPixelWidth))
                lock (_imeStateLock)
                    _lastAppliedCaretPlacement = current;
        }
    }

    private void DispatchCompositionCompletion(uint session)
    {
        uint action;
        lock (_imeStateLock)
        {
            if (_disposed || _operationFailure is not null || !_isComposing || !NativeCompositionCurrent || session != _compositionSession
                || (_compositionOwnerId is not null && _activeInputId == _compositionOwnerId))
            {
                if (_completionRequestedSession == session)
                    _completionRequestedSession = null;
                return;
            }
            action = _compositionOwnerId is null ? 4u : 1u; // CPS_CANCEL / CPS_COMPLETE
        }
        // IMM may synchronously deliver result/end messages. Never call it while holding the state lock.
        var handled = false;
        var success = false;
        NotifyValidationComposition(action, ref handled, ref success);
        if (!handled)
        {
            var context = Imm32.ImmGetContext(_hwnd);
            if (context == 0)
            {
                PostImeCommand(WmDuxelImeCompletionFailure, session, 1);
                return;
            }
            try
            {
                success = Imm32.ImmNotifyIME(context, 0x0015, action, 0);
            } // NI_COMPOSITIONSTR
            finally { Imm32.ImmReleaseContext(_hwnd, context); }
        }
        if (!success)
            PostImeCommand(WmDuxelImeCompletionFailure, session);
    }

    private void DispatchCompositionFailure(uint session, bool missingContext)
    {
        lock (_imeStateLock)
        {
            // END may already be queued when IMM returns false; confirm against the live session after it.
            if (_disposed || !_isComposing || !NativeCompositionCurrent || session != _compositionSession
                || (_compositionOwnerId is not null && _activeInputId == _compositionOwnerId))
            {
                if (_completionRequestedSession == session)
                    _completionRequestedSession = null;
                return;
            }
        }
        RecordImeFailure(new InvalidOperationException(missingContext
            ? "The composing input has no native IMM context."
            : "The native IME rejected the composition completion request."));
    }

    private void PostImeCommand(uint message, uint session = 0, nint detail = 0)
    {
        if (!User32.PostMessage(_hwnd, message, session, detail))
        {
            var error = Marshal.GetLastPInvokeError();
            lock (_imeStateLock)
                if (_disposed)
                    return;
            RecordImeFailure(new Win32Exception(error, "Posting the Windows IME command failed."));
        }
    }

    private void RecordImeFailure(Exception error)
    {
        lock (_imeStateLock)
        {
            _operationFailure ??= error;
            _pendingCaretPlacement = null;
            _completionRequestedSession = null;
        }
        RequestFrame();
    }

    private void ThrowIfImeOperationFailed()
    {
        if (Volatile.Read(ref _operationFailure) is { } error)
            throw new InvalidOperationException("Windows IME operation failed.", error);
    }
}
