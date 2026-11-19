using System.Security.Cryptography;
using Duxel.Core;
using Duxel.Svg.Managed;

namespace Duxel.Svg;

public readonly record struct SvgStoreStatistics(long SourceReads, long XmlParses, long SceneCompiles,
    long Rasterizations, long RasterCacheHits, long CachedRasterBytes, int Documents, long GpuSceneCompiles = 0);

/// <summary>Owns parsed SVG scenes and a bounded LRU of raster results. Load is an in-memory lookup after first use.</summary>
public sealed class SvgStore : IDisposable
{
    private readonly object _gate = new();
    private readonly Dictionary<string, SvgAsset> _documents = new(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
    private readonly Dictionary<(SvgAsset Asset, long Revision, int Width, int Height), LinkedListNode<RasterEntry>> _rasters = [];
    private readonly LinkedList<RasterEntry> _lru = [];
    private readonly long _rasterBudget, _sourceLimit;
    private long _reads, _parses, _compiles, _rasterizations, _hits, _cachedBytes;
    private long _gpuCompiles;
    private bool _disposed;
    private readonly SvgFontCatalog? _fonts;
    internal SvgFontCatalog? FontCatalog => _fonts;
    private sealed record RasterEntry((SvgAsset Asset, long Revision, int Width, int Height) Key, UiSvgRaster Raster);

    /// <param name="maximumRasterBytes">Maximum retained raster memory; an individual oversized request fails before allocation.</param>
    /// <param name="maximumSourceBytes">Maximum accepted source size, checked before reading XML.</param>
    public SvgStore(long maximumRasterBytes = 64 * 1024 * 1024, long maximumSourceBytes = 16 * 1024 * 1024,
        SvgFontOptions? fonts = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumRasterBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumSourceBytes);
        (_rasterBudget, _sourceLimit) = (maximumRasterBytes, maximumSourceBytes);
        if (fonts is not null)
            _fonts = new(fonts);
    }
    public SvgStoreStatistics Statistics
    {
        get
        {
            lock (_gate)
                return new(_reads, _parses, _compiles, _rasterizations, _hits, _cachedBytes, _documents.Count, _gpuCompiles);
        }
    }
    public SvgFontStatistics FontStatistics
    {
        get
        {
            lock (_gate)
                return _fonts?.Statistics ?? default;
        }
    }
    public SvgAsset Load(string path)
    {
        var canonical = Path.GetFullPath(path);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_documents.TryGetValue(canonical, out var existing))
                return existing;
            var (scene, hash) = Read(canonical);
            var asset = new SvgAsset(this, canonical, scene, hash);
            _documents.Add(canonical, asset);
            return asset;
        }
    }
    /// <summary>Explicit source invalidation. Identical bytes keep the scene, revision and cached rasters.</summary>
    public SvgAsset Reload(string path)
    {
        var canonical = Path.GetFullPath(path);
        SvgAsset asset;
        Action? changed;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_documents.TryGetValue(canonical, out asset!))
                return Load(canonical);
            var bytes = ReadBytes(canonical);
            var hash = Convert.ToHexString(SHA256.HashData(bytes));
            if (hash == asset.Hash)
                return asset;
            var scene = Compile(bytes);
            var old = asset.Scene;
            asset.Scene = scene;
            asset.Hash = hash;
            asset.Version++;
            foreach (var key in _rasters.Keys.Where(key => ReferenceEquals(key.Asset, asset)).ToArray())
                Remove(_rasters[key]);
            old.Dispose();
            changed = asset.Notification;
        }
        changed?.Invoke();
        return asset;
    }
    private (SvgScene Scene, string Hash) Read(string path)
    {
        var bytes = ReadBytes(path);
        return (Compile(bytes), Convert.ToHexString(SHA256.HashData(bytes)));
    }
    private byte[] ReadBytes(string path)
    {
        using var stream = File.OpenRead(path);
        if (stream.Length > _sourceLimit)
            throw new InvalidDataException("SVG source exceeds the configured byte limit.");
        var bytes = new byte[checked((int)stream.Length)];
        stream.ReadExactly(bytes);
        _reads++;
        return bytes;
    }
    private SvgScene Compile(byte[] bytes)
    {
        _parses++;
        var xml = SvgSource.Parse(bytes, _sourceLimit, _fonts is not null);
        var renderer = SvgManagedScene.Compile(xml,
            _fonts is null ? null : new SvgManagedTextCompiler(_fonts), new SvgEmbeddedImageDecoder());
        _compiles++;
        return new(renderer);
    }
    internal UiVector2 Size(SvgAsset asset)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return new(asset.Scene.Bounds.Width, asset.Scene.Bounds.Height);
        }
    }
    internal long Version(SvgAsset asset)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return asset.Version;
        }
    }
    internal UiGpuVectorScene GpuScene(SvgAsset asset)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var prepared = asset.Scene.HasGpuScene;
            var scene = asset.Scene.GpuScene;
            if (!prepared)
                _gpuCompiles++;
            return scene;
        }
    }
    internal UiSvgRaster Rasterize(SvgAsset asset, int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        var bytes = checked((long)width * height * 4);
        if (bytes > _rasterBudget || bytes > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(width), "SVG raster exceeds the configured memory budget.");
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var key = (asset, asset.Version, width, height);
            if (_rasters.TryGetValue(key, out var cached))
            {
                _hits++;
                _lru.Remove(cached);
                _lru.AddLast(cached);
                return cached.Value.Raster;
            }
            var pixels = asset.Scene.Renderer.Render(width, height);
            var raster = new UiSvgRaster(width, height, pixels);
            while (_cachedBytes > _rasterBudget - bytes)
                Remove(_lru.First!);
            var node = _lru.AddLast(new RasterEntry(key, raster));
            _rasters.Add(key, node);
            _cachedBytes += bytes;
            _rasterizations++;
            return raster;
        }
    }
    private void Remove(LinkedListNode<RasterEntry> node)
    {
        _cachedBytes -= node.Value.Raster.Pixels.Length;
        _rasters.Remove(node.Value.Key);
        _lru.Remove(node);
    }
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
                return;
            _disposed = true;
            foreach (var asset in _documents.Values)
            {
                asset.Scene.Dispose();
                asset.ClearNotifications();
            }
            _documents.Clear();
            _rasters.Clear();
            _lru.Clear();
            _cachedBytes = 0;
            _fonts?.Dispose();
        }
    }
}

internal sealed class SvgScene(SvgManagedScene renderer) : IDisposable
{
    private UiGpuVectorScene? _gpu;
    internal bool HasGpuScene => _gpu is not null;
    internal UiGpuVectorScene GpuScene
    {
        get
        {
            if (_gpu is not null)
                return _gpu;
            var compiled = Renderer.CompileGpu();
            static UiGpuVectorScene Convert(SvgGpuScene scene) => new(new(scene.Width, scene.Height), scene.Data, scene.CommandCount,
                scene.Filters.Select(filter => new UiGpuVectorFilter(Convert(filter.Scene),
                    new(filter.Bounds.X, filter.Bounds.Y, filter.Bounds.Z, filter.Bounds.W),
                    new(filter.Horizontal.X, filter.Horizontal.Y), new(filter.Vertical.X, filter.Vertical.Y), filter.LinearRgb)).ToArray(),
                scene.TileOffset, scene.TileColumns, scene.TileRows,
                scene.Patterns.Select(pattern => new UiGpuVectorPattern(Convert(pattern.Scene), pattern.Mapping)).ToArray());
            return _gpu = Convert(compiled);
        }
    }
    private SvgManagedScene? _renderer = renderer;
    internal SvgManagedScene Renderer => _renderer ?? throw new ObjectDisposedException(nameof(SvgScene));
    internal UiRect Bounds { get; } = new(0, 0, renderer.Width, renderer.Height);
    internal bool IsDisposed => _renderer is null;
    public void Dispose()
    {
        _renderer = null;
        _gpu = null;
    }
}
/// <summary>Borrowed store-owned SVG scene. Reload updates this identity and notifies consumers once.</summary>
public sealed class SvgAsset : IUiGpuSvgDocument
{
    public UiGpuVectorScene GpuScene => _store.GpuScene(this);
    private readonly SvgStore _store;
    public string SourcePath
    {
        get;
    }
    internal SvgScene Scene;
    internal string Hash;
    internal long Version;
    public UiVector2 IntrinsicSize => _store.Size(this);
    public long Revision => _store.Version(this);
    public event Action? Changed;
    internal Action? Notification => Changed;
    internal void ClearNotifications() => Changed = null;
    internal SvgAsset(SvgStore store, string path, SvgScene scene, string hash) => (_store, SourcePath, Scene, Hash) = (store, path, scene, hash);
    public UiSvgRaster Rasterize(int pixelWidth, int pixelHeight) => _store.Rasterize(this, pixelWidth, pixelHeight);
}
