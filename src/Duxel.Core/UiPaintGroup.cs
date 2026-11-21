namespace Duxel.Core;

/// <summary>Owns per-context paint snapshots and output textures. Versions describe paint content, not placement or final opacity.</summary>
public sealed class UiPaintGroup : IDisposable
{
    private readonly List<Binding> _bindings = [];
    private bool _disposed;
    internal void ValidateOwner() => ObjectDisposedException.ThrowIf(_disposed, this);
    internal Binding For(UiImmediateContext ui)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        foreach (var binding in _bindings)
            if (ReferenceEquals(binding.Owner, ui))
                return binding;
        _bindings.RemoveAll(binding => binding.Owner is null);
        var created = new Binding(ui, ui.AllocateOwnedTextureId(), ui.AllocateOwnedTextureId());
        _bindings.Add(created);
        ui.RegisterPaintGroupBinding(created);
        return created;
    }
    public void Dispose()
    {
        if (_disposed)
            return;
        foreach (var binding in _bindings)
            binding.Dispose();
        _bindings.Clear();
        _disposed = true;
    }
    internal sealed class Binding(UiImmediateContext owner, UiTextureId black, UiTextureId white) : IDisposable
    {
        internal UiImmediateContext? Owner = owner;
        internal readonly UiTextureId Black = black, White = white;
        internal UiPaintGroupContent? Content;
        internal UiDrawListBuilder? Builder;
        internal ulong Version;
        internal long CapturedTextureRevision;
        internal long[]? CapturedTextureVersions;
        internal int LastFrame = int.MinValue;
        internal bool Capturing;
        internal bool DisposeAfterCapture;
        internal HashSet<UiImmediateContext.DirectTextCacheKey> TextKeys = [];
        internal HashSet<UiImmediateContext.DirectTextCacheKey> PendingTextKeys = [];
        internal void Detach()
        {
            Owner = null;
            TextKeys.Clear();
            PendingTextKeys.Clear();
            Content?.Dispose();
            Content = null;
            CapturedTextureVersions = null;
            Builder = null;
        }
        public void Dispose()
        {
            if (Capturing)
            {
                DisposeAfterCapture = true;
                return;
            }
            if (Owner is { } ui)
            {
                ui.UnregisterPaintGroupBinding(this);
                ui.DeferPaintGroupDestroy(Black, White);
            }
            Detach();
        }
    }
}

public readonly partial struct UiControlContext
{
    /// <summary>The callback paints only; it must not perform layout, input processing or application side effects.</summary>
    public void PaintGroup(UiPaintGroup group, UiRect bounds, ulong contentVersion, Action<UiControlContext> paint,
        float opacity = 1, UiRect? destination = null, float rasterScale = 1, UiPaintGroupTextMode? textMode = null) => _ui.PaintGroup(group, bounds, contentVersion, paint, opacity, destination, rasterScale, textMode);

    public void PaintGroup(UiPaintGroup group, UiRect bounds, ulong contentVersion, Action<UiControlContext> paint,
        UiHorizontalOpacityMask mask, float opacity = 1, UiRect? destination = null, float rasterScale = 1,
        UiPaintGroupTextMode? textMode = null) => _ui.PaintGroup(group, bounds, contentVersion, paint, mask, opacity, destination, rasterScale, textMode);

    public void PaintGroup(UiPaintGroup group, UiRect bounds, ulong contentVersion, Action<UiControlContext> paint,
        UiImageUvTransform uv, UiHorizontalOpacityMask mask, float opacity = 1, UiRect? destination = null,
        float rasterScale = 1, UiPaintGroupTextMode? textMode = null) => _ui.PaintGroup(group, bounds, contentVersion, paint, uv, mask, opacity, destination, rasterScale, textMode);
}

public sealed partial class UiImmediateContext
{
    private UiPaintGroupTextMode _capturedPaintTextMode;
    private UiPaintGroup.Binding? _capturingPaintGroup;
    private readonly HashSet<UiPaintGroup.Binding> _paintGroupBindings = [];
    private readonly List<(UiTextureId Black, UiTextureId White)> _pendingPaintGroupDestroys = [];
    internal void DeferPaintGroupDestroy(UiTextureId black, UiTextureId white) => _pendingPaintGroupDestroys.Add((black, white));
    private void FlushPendingPaintGroupDestroys()
    {
        foreach (var pair in _pendingPaintGroupDestroys)
        {
            QueueTextureUpdate(new(UiTextureUpdateKind.Destroy, pair.Black, UiTextureFormat.Rgba8Unorm, 0, 0, ReadOnlyMemory<byte>.Empty));
            QueueTextureUpdate(new(UiTextureUpdateKind.Destroy, pair.White, UiTextureFormat.Rgba8Unorm, 0, 0, ReadOnlyMemory<byte>.Empty));
        }
        _pendingPaintGroupDestroys.Clear();
    }

    internal void RegisterPaintGroupBinding(UiPaintGroup.Binding binding) => _paintGroupBindings.Add(binding);
    internal void UnregisterPaintGroupBinding(UiPaintGroup.Binding binding)
    {
        ReplaceRetainedTextDependencies(ref binding.TextKeys, ref binding.PendingTextKeys, retain: false);
        _paintGroupBindings.Remove(binding);
    }
    private void ReleaseOwnedPaintGroups()
    {
        foreach (var binding in _paintGroupBindings)
            binding.Detach();
        _paintGroupBindings.Clear();
        _pendingPaintGroupDestroys.Clear();
    }
    /// <summary>Captures a pure paint callback once per content version, size and DPI; composites its cached GPU result once.</summary>
    public void PaintGroup(UiPaintGroup group, UiRect bounds, ulong contentVersion, Action<UiControlContext> paint,
        float opacity = 1, UiRect? destination = null, float rasterScale = 1, UiPaintGroupTextMode? textMode = null)
        => PaintGroupCore(group, bounds, contentVersion, paint, opacity, destination, rasterScale, textMode, null);

    public void PaintGroup(UiPaintGroup group, UiRect bounds, ulong contentVersion, Action<UiControlContext> paint,
        UiHorizontalOpacityMask mask, float opacity = 1, UiRect? destination = null, float rasterScale = 1,
        UiPaintGroupTextMode? textMode = null)
        => PaintGroupCore(group, bounds, contentVersion, paint, opacity, destination, rasterScale, textMode, mask);

    public void PaintGroup(UiPaintGroup group, UiRect bounds, ulong contentVersion, Action<UiControlContext> paint,
        UiImageUvTransform uv, UiHorizontalOpacityMask mask, float opacity = 1, UiRect? destination = null,
        float rasterScale = 1, UiPaintGroupTextMode? textMode = null)
        => PaintGroupCore(group, bounds, contentVersion, paint, opacity, destination, rasterScale, textMode, mask, uv);

    private void PaintGroupCore(UiPaintGroup group, UiRect bounds, ulong contentVersion, Action<UiControlContext> paint,
        float opacity, UiRect? destination, float rasterScale, UiPaintGroupTextMode? textMode,
        UiHorizontalOpacityMask? mask, UiImageUvTransform? uv = null)
    {
        EnsureNotMeasuring();
        mask?.Validate();
        uv?.Validate();
        ArgumentNullException.ThrowIfNull(group);
        group.ValidateOwner();
        ArgumentNullException.ThrowIfNull(paint);
        UiDrawListBuilder.ValidateBackdropBounds(bounds, opacity);
        var output = destination ?? bounds;
        UiDrawListBuilder.ValidateBackdropBounds(output, opacity);
        if (bounds.Width == 0 || bounds.Height == 0 || output.Width == 0 || output.Height == 0 || opacity == 0)
            return;
        var visible = IntersectRect(CurrentClipRect, output);
        if (visible.Width <= 0 || visible.Height <= 0)
            return;
        if (!float.IsFinite(rasterScale) || rasterScale <= 0)
            throw new ArgumentOutOfRangeException(nameof(rasterScale));
        var mode = textMode ?? _capturedPaintTextMode;
        if ((uint)mode > 1)
            throw new ArgumentOutOfRangeException(nameof(textMode));
        var effectiveScale = ContentScale * rasterScale;
        var scale = new UiVector2(effectiveScale, effectiveScale);
        UiPaintGroupContent.ValidateViewport(bounds, scale);
        if (!IsPresentationPass)
            return;
        var binding = group.For(this);
        if (binding.Capturing)
            throw new InvalidOperationException("A paint group cannot recursively capture itself.");
        var previous = binding.Content;
        var changed = previous is null || binding.Version != contentVersion || previous.Bounds.Width != bounds.Width
            || previous.Bounds.Height != bounds.Height || previous.FramebufferScale != scale || previous.TextMode != mode;
        if (changed && previous is not null && binding.LastFrame == TextureFrame)
            throw new InvalidOperationException("A paint group has one content definition per context per frame; use separate owners for different definitions.");
        binding.LastFrame = TextureFrame;
        if (changed)
        {
            var parent = _builder;
            var parentScale = _contentScale;
            var parentTextMode = _capturedPaintTextMode;
            var parentTextCapture = _capturingPaintGroup;
            binding.PendingTextKeys.Clear();
            _capturingPaintGroup = binding;
            _contentScale = effectiveScale;
            _capturedPaintTextMode = mode;
            var builder = binding.Builder ??= new(bounds, retainScratchCapacity: true, poolScratchBuffers: false);
            builder._ResetForNewFrame();
            builder._SetDrawListSharedData(GetDrawListSharedData());
            builder._SetClipRect(bounds);
            binding.Capturing = true;
            _builder = builder;
            PushClipRect(bounds, false);
            UiPaintGroupContent next;
            var captured = false;
            try
            {
                paint(new(this));
                next = UiPaintGroupContent.ConsumeCapture(bounds, scale, builder.Build(), mode);
                captured = true;
            }
            finally
            {
                PopClipRect();
                _builder = parent;
                _contentScale = parentScale;
                _capturedPaintTextMode = parentTextMode;
                _capturingPaintGroup = parentTextCapture;
                binding.Capturing = false;
                if (!captured && binding.DisposeAfterCapture)
                    binding.Dispose();
            }
            QueueTextureUpdate(new(UiTextureUpdateKind.RenderPaintGroup, binding.Black, UiTextureFormat.Rgba8Unorm,
                next.PixelWidth, next.PixelHeight, ReadOnlyMemory<byte>.Empty)
            {
                PaintGroup = next.Acquire(),
                WhitePaintTexture = binding.White,
                IsColorImage = true
            });
            ReplaceRetainedTextDependencies(ref binding.TextKeys, ref binding.PendingTextKeys, retain: true);
            binding.Content = next;
            binding.Version = contentVersion;
            previous?.Dispose();
        }
        foreach (var key in binding.TextKeys)
        {
            TrackLayerTextDependency(key);
            _capturingPaintGroup?.PendingTextKeys.Add(key);
        }
        if (uv is { } source) _builder.AddBackdropComposite(binding.Black, binding.White, output, source, mask ?? default, opacity);
        else if (mask is { } edges) _builder.AddBackdropComposite(binding.Black, binding.White, output, edges, opacity);
        else _builder.AddBackdropComposite(binding.Black, binding.White, output, opacity);
        if (binding.DisposeAfterCapture)
            binding.Dispose();
    }
}
