namespace Duxel.Core;

/// <summary>Queued texture work and cache residency; these are not GPU completion or process memory counters.</summary>
public readonly record struct UiRasterTextureStatistics(long Uploads, long UploadedBytes, int ResidentTextures, long ResidentBytes, long GpuRenderRequests = 0);

internal interface IUiRasterTextureProvider<TSource> where TSource : class
{
    static abstract long Revision(TSource source);
    static abstract UiTextureUpdate Prepare(TSource source, int width, int height, UiTextureId texture, UiGpuVectorCamera? camera);
}

internal readonly struct UiSvgTextureProvider : IUiRasterTextureProvider<IUiSvgDocument>
{
    public static long Revision(IUiSvgDocument source) => source.Revision;
    public static UiTextureUpdate Prepare(IUiSvgDocument source, int width, int height, UiTextureId texture, UiGpuVectorCamera? camera)
    {
        if (source is not IUiGpuSvgDocument gpu)
            throw new NotSupportedException("SVG widgets require a retained GPU scene. Use Rasterize explicitly for bitmap output.");
        return new(UiTextureUpdateKind.RenderVectorScene, texture, UiTextureFormat.Rgba8Unorm, width, height,
            ReadOnlyMemory<byte>.Empty)
        {
            VectorScene = gpu.GpuScene,
            VectorCamera = camera,
            IsColorImage = true
        };
    }
}

/// <summary>Shared per-context GPU identities. Live widget leases pin retained-layer dependencies.</summary>
internal sealed class UiRasterTextureCache<TSource, TProvider>(UiImmediateContext ui, int maximumEntries = 128) : IDisposable
    where TSource : class
    where TProvider : IUiRasterTextureProvider<TSource>
{
    private const long MaximumBytes = 64 * 1024 * 1024;
    private readonly int _maximumEntries = maximumEntries > 0 ? maximumEntries : throw new ArgumentOutOfRangeException(nameof(maximumEntries));
    private readonly List<Entry> _entries = [];
    private readonly HashSet<Lease> _leases = [];
    private UiImmediateContext? _ui = ui;
    private long _bytes;
    private long _uploads;
    private long _uploadedBytes;
    private long _gpuRenderRequests;
    internal UiRasterTextureStatistics Statistics => new(_uploads, _uploadedBytes, _entries.Count, _bytes, _gpuRenderRequests);

    internal Lease Acquire(TSource document, int width, int height, UiGpuVectorCamera? camera = null, UiColor background = default)
        => Borrow(FindOrPrepare(document, width, height, camera, background));

    internal Lease Replace(Lease previous, TSource document, int width, int height, UiGpuVectorCamera? camera, UiColor background)
    {
        if (!ReferenceEquals(previous.Owner, this))
            throw new InvalidOperationException("Texture replacement requires a live lease in this cache.");
        var next = Borrow(FindOrPrepare(document, width, height, camera, background, previous.TextureEntry));
        previous.Dispose();
        return next;
    }
    /// <summary>Uses a frame-owned command without allocating a persistent widget lease.</summary>
    internal UiTextureId Use(TSource document, int width, int height, UiGpuVectorCamera? camera = null, UiColor background = default)
    {
        var entry = FindOrPrepare(document, width, height, camera, background);
        entry.LastFrame = _ui!.TextureFrame;
        return entry.Texture;
    }
    private Entry FindOrPrepare(TSource document, int width, int height, UiGpuVectorCamera? camera, UiColor background,
        Entry? replaced = null)
    {
        ObjectDisposedException.ThrowIf(_ui is null, this);
        foreach (var cached in _entries)
            if (cached.Matches(document, width, height, camera, background))
                return cached;
        var bytes = checked((long)width * height * 4);
        if (width <= 0 || height <= 0 || bytes > MaximumBytes)
            throw new InvalidOperationException("Raster texture exceeds the context's 64 MiB raster budget.");
        Entry? retireOnCommit = null;
        // Keep current-frame and retained-layer commands valid even when a widget changes its texture.
        while (_entries.Count >= _maximumEntries || _bytes + bytes > MaximumBytes)
        {
            Entry? oldest = null;
            foreach (var candidate in _entries)
                if (candidate.References == 0 && CanRetire(candidate) &&
                    (oldest is null || candidate.LastFrame < oldest.LastFrame))
                    oldest = candidate;
            if (oldest is null)
            {
                if (replaced is not { References: 1 } || !CanRetire(replaced) || _bytes - replaced.Bytes + bytes > MaximumBytes)
                    throw new InvalidOperationException("Live raster widget textures exceed the context cache budget; dispose unused widgets or reduce resolution.");
                // The retiring lease stays usable until the replacement's source has been prepared.
                retireOnCommit = replaced;
                break;
            }
            Retire(oldest);
        }
        var revision = TProvider.Revision(document);
        var entry = new Entry(document, revision, width, height, _ui.AllocateOwnedTextureId(), camera, background);
        var update = TProvider.Prepare(document, width, height, entry.Texture, camera);
        if (background != default && update.Kind != UiTextureUpdateKind.RenderVectorScene)
            throw new NotSupportedException("A target background requires a GPU vector scene.");
        update = update with { VectorBackground = background };
        if (TProvider.Revision(document) != revision)
            throw new InvalidOperationException("Raster source changed during texture preparation; reload and render must be serialized on the UI thread.");
        if (retireOnCommit is not null)
            Retire(retireOnCommit);
        _ui.QueueTextureUpdate(update);
        _entries.Add(entry);
        _bytes += bytes;
        if (update.Kind == UiTextureUpdateKind.RenderVectorScene)
            _gpuRenderRequests++;
        else
        {
            _uploads++;
            _uploadedBytes += bytes;
        }
        return entry;
    }

    private bool CanRetire(Entry entry) => entry.LastFrame != _ui!.TextureFrame && !_ui.IsTextureRetained(entry.Texture);

    private void Retire(Entry entry)
    {
        _ui!.QueueTextureUpdate(new(UiTextureUpdateKind.Destroy, entry.Texture, UiTextureFormat.Rgba8Unorm,
            entry.Width, entry.Height, ReadOnlyMemory<byte>.Empty));
        _entries.Remove(entry);
        _bytes -= entry.Bytes;
    }

    private Lease Borrow(Entry entry)
    {
        entry.References++;
        var lease = new Lease(this, entry);
        _leases.Add(lease);
        return lease;
    }
    public void Dispose()
    {
        foreach (var lease in _leases)
            lease.Owner = null;
        _leases.Clear();
        _entries.Clear();
        _bytes = 0;
        // Renderer teardown owns native texture destruction after context teardown.
        _ui = null;
    }
    internal sealed class Entry(TSource document, long revision, int width, int height, UiTextureId texture, UiGpuVectorCamera? camera, UiColor background)
    {
        internal readonly TSource Document = document;
        internal readonly long Revision = revision;
        internal readonly int Width = width;
        internal readonly int Height = height;
        internal long Bytes => (long)Width * Height * 4;
        internal readonly UiTextureId Texture = texture;
        internal readonly UiGpuVectorCamera? Camera = camera;
        internal readonly UiColor Background = background;
        internal int References;
        internal int LastFrame;
        internal bool Matches(TSource source, int w, int h, UiGpuVectorCamera? view, UiColor background) =>
            ReferenceEquals(Document, source) && Revision == TProvider.Revision(source) && Width == w && Height == h && Camera == view && Background == background;
    }
    internal sealed class Lease(UiRasterTextureCache<TSource, TProvider> owner, Entry entry) : IDisposable
    {
        internal UiRasterTextureCache<TSource, TProvider>? Owner = owner;
        internal Entry TextureEntry { get; } = entry;
        internal bool Matches(TSource source, int width, int height, UiGpuVectorCamera? camera = null, UiColor background = default)
            => TextureEntry.Matches(source, width, height, camera, background);
        internal UiTextureId Use()
        {
            ObjectDisposedException.ThrowIf(Owner is null, this);
            TextureEntry.LastFrame = Owner._ui!.TextureFrame;
            return TextureEntry.Texture;
        }
        public void Dispose()
        {
            if (Owner is null)
                return;
            TextureEntry.References--;
            Owner._leases.Remove(this);
            Owner = null;
        }
    }
}
