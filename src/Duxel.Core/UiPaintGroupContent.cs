using System.Runtime.CompilerServices;

namespace Duxel.Core;

public enum UiPaintGroupTextMode
{
    PhysicalLcd, Projected
}

/// <summary>A copied paint-only snapshot shared by queued frames and the renderer. Exposed geometry is immutable by contract.</summary>
public sealed class UiPaintGroupContent : IDisposable
{
    private static long _nextIdentity;
    private readonly UiDrawList[] _lists;
    private int _references = 1;
    private int _ownerDisposed;
    public UiPaintGroupTextMode TextMode
    {
        get;
    }
    public long Identity { get; } = Interlocked.Increment(ref _nextIdentity);
    public UiRect Bounds
    {
        get;
    }
    public UiVector2 FramebufferScale
    {
        get;
    }
    public int PixelWidth
    {
        get;
    }
    public int PixelHeight
    {
        get;
    }
    public IReadOnlyList<UiDrawList> DrawLists
    {
        get;
    }
    public IReadOnlyList<UiTextureId> TextureDependencies
    {
        get;
    }

    public UiPaintGroupContent(UiRect bounds, UiVector2 framebufferScale, IReadOnlyList<UiDrawList> lists, UiPaintGroupTextMode textMode = UiPaintGroupTextMode.PhysicalLcd)
        : this(bounds, framebufferScale, lists, textMode, takeOwnedBuffers: false)
    {
    }

    private UiPaintGroupContent(UiRect bounds, UiVector2 framebufferScale, IReadOnlyList<UiDrawList> lists,
        UiPaintGroupTextMode textMode, bool takeOwnedBuffers)
    {
        ArgumentNullException.ThrowIfNull(lists);
        if ((uint)textMode > 1)
            throw new ArgumentOutOfRangeException(nameof(textMode));
        TextMode = textMode;
        var (width, height) = ValidateViewport(bounds, framebufferScale);
        long bytes = 0;
        var dependencies = new HashSet<UiTextureId>();
        foreach (var list in lists)
        {
            if (list.Scene3D is not null)
                throw new NotSupportedException("A paint-group source must be two-dimensional; transform its composite result instead.");
            bytes = checked(bytes + (long)list.Vertices.Count * Unsafe.SizeOf<UiDrawVertex>()
                + (long)list.Indices.Count * sizeof(uint) + (long)list.Commands.Count * Unsafe.SizeOf<UiDrawCommand>()
                + (long)(list.RectFilledPrimitives?.Count ?? 0) * Unsafe.SizeOf<UiRectFilledPrimitive>()
                + (long)(list.CircleFilledPrimitives?.Count ?? 0) * Unsafe.SizeOf<UiCircleFilledPrimitive>());
            if (bytes > 64 * 1024 * 1024)
                throw new ArgumentOutOfRangeException(nameof(lists), "Paint snapshot geometry exceeds64MiB.");
            foreach (var command in list.Commands)
            {
                if (command.Callback is not null || command.UserData is UiGpuParticleFrame)
                    throw new NotSupportedException("External draw callbacks and mutable GPU particle frames cannot execute twice inside a paint group.");
                if (command.ElementCount == 0)
                    continue;
                dependencies.Add(command.TextureId);
                if (command.WhiteBackdropTextureId is { } white)
                    dependencies.Add(white);
            }
        }
        Bounds = bounds;
        FramebufferScale = framebufferScale;
        PixelWidth = (int)width;
        PixelHeight = (int)height;
        _lists = new UiDrawList[lists.Count];
        for (var i = 0; i < lists.Count; i++)
        {
            var list = lists[i];
            _lists[i] = takeOwnedBuffers && list.OwnsBuffers
                ? new(list.Vertices, list.Indices, list.Commands,
                    RectFilledPrimitives: list.RectFilledPrimitives, CircleFilledPrimitives: list.CircleFilledPrimitives)
                : new(UiPooledList<UiDrawVertex>.RentAndCopy(list.Vertices.AsSpan()),
                    UiPooledList<uint>.RentAndCopy(list.Indices.AsSpan()),
                    UiPooledList<UiDrawCommand>.RentAndCopy(list.Commands.AsSpan()),
                    RectFilledPrimitives: list.RectFilledPrimitives is { } rectangles ? UiPooledList<UiRectFilledPrimitive>.RentAndCopy(rectangles.AsSpan()) : null,
                    CircleFilledPrimitives: list.CircleFilledPrimitives is { } circles ? UiPooledList<UiCircleFilledPrimitive>.RentAndCopy(circles.AsSpan()) : null);
        }
        DrawLists = Array.AsReadOnly(_lists);
        TextureDependencies = Array.AsReadOnly(dependencies.OrderBy(id => id.Value).ToArray());
    }

    // Build has consumed the builder's pending draw lists. Their owned buffers
    // can become the snapshot's buffers without exposing borrowed geometry.
    internal static UiPaintGroupContent ConsumeCapture(UiRect bounds, UiVector2 scale,
        UiPooledList<UiDrawList> lists, UiPaintGroupTextMode textMode)
    {
        var consumed = false;
        try
        {
            var content = new UiPaintGroupContent(bounds, scale, lists, textMode, takeOwnedBuffers: true);
            consumed = true;
            return content;
        }
        finally
        {
            if (!consumed)
                foreach (var list in lists)
                    list.ReleasePooled();
            lists.Return();
        }
    }

    internal static (int Width, int Height) ValidateViewport(UiRect bounds, UiVector2 framebufferScale)
    {
        UiDrawListBuilder.ValidateBackdropBounds(bounds, 1);
        if (bounds.Width <= 0 || bounds.Height <= 0 || !float.IsFinite(framebufferScale.X)
            || !float.IsFinite(framebufferScale.Y) || framebufferScale.X <= 0 || framebufferScale.Y <= 0)
            throw new ArgumentOutOfRangeException(nameof(framebufferScale));
        var width = Math.Ceiling((double)bounds.Width * framebufferScale.X);
        var height = Math.Ceiling((double)bounds.Height * framebufferScale.Y);
        if (width > int.MaxValue || height > int.MaxValue || width * height * 8 > 64 * 1024 * 1024)
            throw new ArgumentOutOfRangeException(nameof(bounds), "The two paint targets must fit64MiB.");
        return ((int)width, (int)height);
    }

    public UiPaintGroupLease Acquire()
    {
        var count = Volatile.Read(ref _references);
        while (count > 0)
        {
            var observed = Interlocked.CompareExchange(ref _references, checked(count + 1), count);
            if (observed == count)
                return new(this);
            count = observed;
        }
        throw new ObjectDisposedException(nameof(UiPaintGroupContent));
    }
    internal void Release()
    {
        if (Interlocked.Decrement(ref _references) != 0)
            return;
        foreach (var list in _lists)
            list.ReleasePooled();
    }
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _ownerDisposed, 1) == 0)
            Release();
    }
}

/// <summary>One independently disposable reference to immutable paint geometry.</summary>
public sealed class UiPaintGroupLease : IDisposable
{
    private UiPaintGroupContent? _content;
    internal UiPaintGroupLease(UiPaintGroupContent content) => _content = content;
    public UiPaintGroupContent Content => Volatile.Read(ref _content) ?? throw new ObjectDisposedException(nameof(UiPaintGroupLease));
    public void Dispose() => Interlocked.Exchange(ref _content, null)?.Release();
}
