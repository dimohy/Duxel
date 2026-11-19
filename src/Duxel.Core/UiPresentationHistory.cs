namespace Duxel.Core;

public interface IUiPresentationReader
{
    UiRendererFrameMetrics Latest { get; }
    ulong OverwrittenSamples { get; }
    int Drain(Span<UiRendererFrameMetrics> destination);
}

// One renderer publishes; the UI drains accepted presentations independently.
// Keeping every interval avoids losing samples when rendering advances twice
// while the next UI frame is still being built.
internal sealed class UiPresentationHistory(int capacity = 64) : IUiPresentationReader
{
    private readonly object _gate = new();
    private readonly UiRendererFrameMetrics[] _samples = new UiRendererFrameMetrics[
        capacity > 0 ? capacity : throw new ArgumentOutOfRangeException(nameof(capacity))];
    private UiRendererFrameMetrics _latest;
    private ulong _lastSequence, _overwritten;
    private int _head, _count;

    public UiRendererFrameMetrics Latest { get { lock (_gate) return _latest; } }
    public ulong OverwrittenSamples { get { lock (_gate) return _overwritten; } }

    internal void Publish(in UiRendererFrameMetrics metrics)
    {
        lock (_gate)
        {
            if (metrics.PresentationSequence < _lastSequence)
                throw new InvalidOperationException("Presentation sequence regressed within one renderer history.");
            _latest = metrics;
            if (metrics.PresentationSequence == _lastSequence) return;
            _lastSequence = metrics.PresentationSequence;
            if (_count == _samples.Length)
            {
                _head = (_head + 1) % _samples.Length;
                _count--;
                _overwritten++;
            }
            _samples[(_head + _count) % _samples.Length] = metrics;
            _count++;
        }
    }

    public int Drain(Span<UiRendererFrameMetrics> destination)
    {
        lock (_gate)
        {
            var count = Math.Min(destination.Length, _count);
            var first = Math.Min(count, _samples.Length - _head);
            _samples.AsSpan(_head, first).CopyTo(destination);
            _samples.AsSpan(0, count - first).CopyTo(destination[first..]);
            _head = (_head + count) % _samples.Length;
            _count -= count;
            return count;
        }
    }
}
