using System.Runtime.ExceptionServices;
using Duxel.Core;

namespace Duxel.App;

internal readonly record struct DuxelRenderWork(
    UiDrawData Data, bool VSync, int MsaaSamples, UiColor ClearColor,
    long ResizeSequence, int ProfileFrame, long NewFrameStart, long RenderStart,
    long RenderEnd, float Fps, DuxelFrameAllocationSample? AllocationSample);

internal readonly record struct DuxelFrameAllocationSample(long NewFrame, long Render, long DrawData);

// Single producer, single consumer. One waiting frame plus one executing frame.
// Successful Submit transfers exclusive ownership; shutdown drains without dropping.
internal sealed class DuxelRenderQueue : IDisposable
{
    private readonly object _gate = new();
    private readonly Action<DuxelRenderWork> _render;
    private readonly Action _failed;
    private readonly Thread _thread;
    private DuxelRenderWork _pending;
    private bool _hasPending;
    private bool _executing;
    private bool _complete;
    private ExceptionDispatchInfo? _error;

    internal DuxelRenderQueue(Action<DuxelRenderWork> render, Action failed)
    {
        _render = render;
        _failed = failed;
        _thread = new Thread(Consume) { IsBackground = true, Name = "DuxelGpuSubmit" };
        _thread.Start();
    }

    internal bool Submit(in DuxelRenderWork work)
    {
        // Callback closures can reference UI state. Finish such frames before the
        // producer resumes, preserving the existing UI/callback exclusion contract.
        var hasCallbacks = (work.Data.GetCommandFeatures() & UiDrawCommandFeatures.Callbacks) != 0;
        work.Data.CaptureTexturePayloads();
        lock (_gate)
        {
            while (_hasPending && _error is null && !_complete) Monitor.Wait(_gate);
            _error?.Throw();
            if (_complete) throw new InvalidOperationException("The render queue is complete.");
            _pending = work;
            _hasPending = true;
            Monitor.PulseAll(_gate);
        }
        // The caller must wait before generating the next UI for callback frames.
        return hasCallbacks;
    }

    internal void WaitForCapacity()
    {
        lock (_gate)
        {
            while (_hasPending && _error is null && !_complete) Monitor.Wait(_gate);
            _error?.Throw();
            if (_complete) throw new InvalidOperationException("The render queue is complete.");
        }
    }

    internal void WaitForIdle()
    {
        lock (_gate)
        {
            while ((_hasPending || _executing) && _error is null) Monitor.Wait(_gate);
            _error?.Throw();
        }
    }

    private void Consume()
    {
        try
        {
            while (true)
            {
                DuxelRenderWork work;
                lock (_gate)
                {
                    while (!_hasPending && !_complete) Monitor.Wait(_gate);
                    if (!_hasPending) return;
                    work = _pending;
                    _pending = default;
                    _hasPending = false;
                    _executing = true;
                    Monitor.PulseAll(_gate);
                }
                try { _render(work); }
                finally { work.Data.Dispose(); }
                lock (_gate)
                {
                    _executing = false;
                    Monitor.PulseAll(_gate);
                }
            }
        }
        catch (Exception error)
        {
            lock (_gate)
            {
                _error = ExceptionDispatchInfo.Capture(error);
                _executing = false;
                if (_hasPending) _pending.Data.Dispose();
                _pending = default;
                _hasPending = false;
                Monitor.PulseAll(_gate);
            }
            _failed();
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _complete = true;
            Monitor.PulseAll(_gate);
        }
        _thread.Join();
        _error?.Throw();
    }
}
