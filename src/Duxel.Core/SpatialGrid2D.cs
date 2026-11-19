namespace Duxel.Core;

/// <summary>A reusable dense grid for bounded 2D broad-phase queries. Rebuild invalidates all links.</summary>
public sealed class SpatialGrid2D(int maximumCells = 1_048_576)
{
    private int[] _heads = [];
    private int[] _next = [];
    private int _minX, _minY, _width, _height, _count;
    public ReadOnlySpan<int> Next => _next.AsSpan(0, _count);
    public float InverseCellSize { get; private set; }

    public void Rebuild(ReadOnlySpan<UiVector2> positions, float cellSize)
    {
        if (!float.IsFinite(cellSize) || cellSize <= 0) throw new ArgumentOutOfRangeException(nameof(cellSize));
        InverseCellSize = 1f / cellSize;
        _count = positions.Length;
        if (positions.IsEmpty) { _width = _height = 0; return; }
        var minX = int.MaxValue;
        var minY = int.MaxValue;
        var maxX = int.MinValue;
        var maxY = int.MinValue;
        foreach (var position in positions)
        {
            var x = checked((int)MathF.Floor(position.X * InverseCellSize));
            var y = checked((int)MathF.Floor(position.Y * InverseCellSize));
            minX = Math.Min(minX, x); minY = Math.Min(minY, y);
            maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y);
        }
        var width = checked(maxX - minX + 1);
        var height = checked(maxY - minY + 1);
        var cells = checked((long)width * height);
        if (cells > maximumCells) throw new ArgumentOutOfRangeException(nameof(positions), "Spatial grid cell budget exceeded.");
        _minX = minX; _minY = minY; _width = width; _height = height;
        if (_heads.Length < cells) _heads = new int[(int)cells];
        if (_next.Length < positions.Length) _next = new int[positions.Length];
        Array.Fill(_heads, -1, 0, (int)cells);
        for (var i = 0; i < positions.Length; i++)
        {
            var x = (int)MathF.Floor(positions[i].X * InverseCellSize) - minX;
            var y = (int)MathF.Floor(positions[i].Y * InverseCellSize) - minY;
            ref var head = ref _heads[y * width + x];
            _next[i] = head;
            head = i;
        }
    }

    public int First(int cellX, int cellY)
    {
        var x = (long)cellX - _minX;
        var y = (long)cellY - _minY;
        return (ulong)x < (uint)_width && (ulong)y < (uint)_height ? _heads[(int)y * _width + (int)x] : -1;
    }
}
