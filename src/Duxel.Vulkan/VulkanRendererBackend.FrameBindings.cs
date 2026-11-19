namespace Duxel.Vulkan;

public sealed unsafe partial class VulkanRendererBackend
{
    // Keys are frame-local draw-list indices in [0, listCount). Null denotes a
    // dynamic or geometry-empty list. Clear releases all previous references,
    // including the truncated tail when the next frame contains fewer lists.
    private sealed class StaticGeometryFrameBindings
    {
        private StaticGeometryBuffer?[] _values = [];
        private int _length;

        public void Reset(int listCount)
        {
            Clear();
            if (_values.Length < listCount)
                _values = new StaticGeometryBuffer?[Math.Max(listCount, _values.Length * 2)];
            _length = listCount;
        }

        public void Clear()
        {
            _values.AsSpan(0, _length).Clear();
            _length = 0;
        }

        public bool ContainsKey(int index) => (uint)index < (uint)_length && _values[index] is not null;

        public bool TryGetValue(int index, out StaticGeometryBuffer value)
        {
            if ((uint)index < (uint)_length && _values[index] is { } binding)
            {
                value = binding;
                return true;
            }
            value = null!;
            return false;
        }

        public StaticGeometryBuffer this[int index]
        {
            get => TryGetValue(index, out var value) ? value : throw new KeyNotFoundException();
            set
            {
                if ((uint)index >= (uint)_length) throw new ArgumentOutOfRangeException(nameof(index));
                _values[index] = value;
            }
        }
    }
}
