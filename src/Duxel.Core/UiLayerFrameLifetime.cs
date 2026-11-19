using System.Collections.Generic;

namespace Duxel.Core;

// One lease protects all retained sources referenced by a CPU frame. Retirement
// is ordered by frame epoch, so a continuously overlapping pipeline can drain
// old sources without waiting for every outstanding frame to finish together.
internal sealed class UiLayerFrameLifetime
{
    internal readonly record struct Lease(UiLayerFrameLifetime Owner, ulong Epoch)
    {
        internal void Return() => Owner.Return(Epoch);
    }

    private readonly object _gate = new();
    private readonly List<ulong> _readers = new(4);
    private readonly Queue<(ulong Epoch, UiDrawList Source)> _retired = new();
    private ulong _epoch;

    internal bool HasReaders { get { lock (_gate) return _readers.Count != 0; } }
    internal (int Frames, int RetiredSources) State
    {
        get { lock (_gate) return (_readers.Count, _retired.Count); }
    }

    internal Lease Acquire()
    {
        lock (_gate)
        {
            var epoch = checked(++_epoch);
            _readers.Add(epoch);
            return new(this, epoch);
        }
    }

    internal void Retire(UiDrawList source)
    {
        lock (_gate)
        {
            if (_readers.Count == 0) source.ReleasePooled();
            else _retired.Enqueue((_epoch, source));
        }
    }

    private void Return(ulong epoch)
    {
        lock (_gate)
        {
            if (!_readers.Remove(epoch)) return;
            while (_retired.TryPeek(out var retired)
                && (_readers.Count == 0 || retired.Epoch < _readers[0]))
            {
                _retired.Dequeue();
                retired.Source.ReleasePooled();
            }
        }
    }
}
