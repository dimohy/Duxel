using System.Runtime.InteropServices;

namespace Duxel.Platform.Windows;

public sealed partial class WindowsPlatformBackend
{
    [LibraryImport("kernel32.dll")]
    private static partial ulong GetTickCount64();

    private static double MessageTimeSeconds(uint postedMilliseconds, ulong nowMilliseconds)
        => (nowMilliseconds - unchecked((uint)nowMilliseconds - postedMilliseconds)) / 1000d;

    private void DispatchQueuedInput(ref Msg message)
    {
        var previous = _inputBackend.BeginMessageTime(message.time);
        try
        {
            _ = TranslateMessage(ref message);
            _ = DispatchMessageW(ref message);
        }
        finally { _inputBackend.RestoreMessageTime(previous); }
    }

    private sealed partial class WindowsInputBackend
    {
        private double? _messageTime;
        private double EventTimeSeconds => _messageTime ?? GetTickCount64() / 1000d;
        internal double? BeginMessageTime(uint postedMilliseconds)
        {
            var previous = _messageTime;
            if (orderedInput)
                _messageTime = MessageTimeSeconds(postedMilliseconds, GetTickCount64());
            return previous;
        }
        internal void RestoreMessageTime(double? previous) => _messageTime = previous;
    }
}
