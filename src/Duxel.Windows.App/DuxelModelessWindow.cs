using Duxel.App;
using System.Runtime.Versioning;

namespace Duxel.Windows.App;

[SupportedOSPlatform("windows")]
public sealed class DuxelModelessWindow : IDisposable
{
    private readonly DuxelAppSession _session;
    private readonly Thread _thread;
    private readonly ManualResetEventSlim _closed = new(false);
    private nint _windowHandle;
    private int _isClosed;

    internal DuxelModelessWindow(DuxelAppSession session, Thread thread)
    {
        _session = session;
        _thread = thread;
    }

    public bool IsClosed => Volatile.Read(ref _isClosed) != 0;

    public nint WindowHandle => Interlocked.CompareExchange(ref _windowHandle, nint.Zero, nint.Zero);

    internal void AttachWindowHandle(nint windowHandle)
    {
        Interlocked.Exchange(ref _windowHandle, windowHandle);
    }

    internal void MarkClosed()
    {
        Interlocked.Exchange(ref _isClosed, 1);
        _closed.Set();
    }

    public void RequestClose()
    {
        if (IsClosed)
        {
            return;
        }

        _session.Exit();
    }

    public void Restore()
    {
        var windowHandle = WindowHandle;
        if (windowHandle != nint.Zero)
        {
            DuxelWindowsApp.RestoreWindow(windowHandle);
        }
    }

    public void Hide()
    {
        var windowHandle = WindowHandle;
        if (windowHandle != nint.Zero)
        {
            DuxelWindowsApp.HideWindow(windowHandle);
        }
    }

    public bool WaitForClose(int millisecondsTimeout = Timeout.Infinite)
        => _closed.Wait(millisecondsTimeout);

    public Task WaitForCloseAsync(CancellationToken cancellationToken = default)
    {
        if (IsClosed)
        {
            return Task.CompletedTask;
        }

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var registration = cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));

        ThreadPool.UnsafeRegisterWaitForSingleObject(
            _closed.WaitHandle,
            static (state, timedOut) =>
            {
                var (tcs, registration) = ((TaskCompletionSource, CancellationTokenRegistration))state!;
                registration.Dispose();
                tcs.TrySetResult();
            },
            (tcs, registration),
            Timeout.InfiniteTimeSpan,
            executeOnlyOnce: true);

        return tcs.Task;
    }

    public void Dispose()
    {
        RequestClose();

        if (Thread.CurrentThread.ManagedThreadId != _thread.ManagedThreadId)
        {
            _ = WaitForClose(3_000);
        }

        _closed.Dispose();
    }
}