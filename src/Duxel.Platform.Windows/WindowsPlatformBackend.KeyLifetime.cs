using Duxel.Core;

namespace Duxel.Platform.Windows;

public sealed partial class WindowsPlatformBackend
{
    private sealed partial class WindowsInputBackend
    {
        // Ownership survives PollEvents, unlike the frame's transient key event list.
        private readonly HashSet<UiKey> _heldKeys = [];

        private void ReleaseHeldKeys()
        {
            var modifiers = GetModifiers();
            foreach (var key in _heldKeys)
                _keyEvents.Add(new(key, false, modifiers));
            _heldKeys.Clear();
        }
    }
}
