namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    internal Dictionary<UiTextureId, long> TextureContentVersions { get; set; } = [];

    internal static void RecordTextureContentChange(Dictionary<UiTextureId, long> versions, UiTextureUpdate update, long version)
    {
        if (update.Kind == UiTextureUpdateKind.Destroy)
            versions.Remove(update.TextureId);
        else
        {
            versions[update.TextureId] = version;
            if (update.WhitePaintTexture is { } white)
                versions[white] = version;
        }
    }

    private bool CapturedTexturesChanged(UiPaintGroup.Binding binding, UiPaintGroupContent content)
    {
        if (binding.CapturedTextureRevision == TextureContentRevision)
            return false;
        var versions = binding.CapturedTextureVersions;
        if (versions is null || versions.Length != content.TextureDependencies.Count)
            return true;
        for (var i = 0; i < versions.Length; i++)
            if (versions[i] != TextureContentVersions.GetValueOrDefault(content.TextureDependencies[i]))
                return true;
        return false;
    }

    internal long TextureContentRevision
    {
        get; set;
    }

    private readonly record struct InteractivePaintCapture(UiPaintGroup.Binding Binding, UiDrawListBuilder Parent,
        UiPaintGroup.Binding? ParentCapture, UiPaintGroupTextMode ParentTextMode, UiRect Bounds, UiPaintGroupTextMode TextMode);

    // Live controls run once at their displayed coordinates; only their paint is captured.
    private InteractivePaintCapture BeginInteractivePaintGroup(UiPaintGroup group, UiRect bounds, UiPaintGroupTextMode textMode = UiPaintGroupTextMode.Projected)
    {
        if ((uint)textMode > 1) throw new ArgumentOutOfRangeException(nameof(textMode));
        UiPaintGroupContent.ValidateViewport(bounds, new(ContentScale, ContentScale));
        var binding = group.For(this);
        if (binding.Capturing || binding.LastFrame == TextureFrame)
            throw new InvalidOperationException("An interactive paint group has one capture per owner per frame.");
        var capture = new InteractivePaintCapture(binding, _builder, _capturingPaintGroup, _capturedPaintTextMode, bounds, textMode);
        var builder = binding.Builder ??= new(bounds, retainScratchCapacity: true, poolScratchBuffers: false);
        builder._ResetForNewFrame();
        builder._SetDrawListSharedData(GetDrawListSharedData());
        builder._SetClipRect(bounds);
        binding.PendingTextKeys.Clear();
        binding.Capturing = true;
        binding.LastFrame = TextureFrame;
        _builder = builder;
        _capturingPaintGroup = binding;
        _capturedPaintTextMode = textMode;
        return capture;
    }

    private void EndInteractivePaintGroup(InteractivePaintCapture capture, bool completed, float opacity, UiRect? destination = null)
    {
        var binding = capture.Binding;
        _builder = capture.Parent;
        _capturingPaintGroup = capture.ParentCapture;
        _capturedPaintTextMode = capture.ParentTextMode;
        binding.Capturing = false;
        if (!completed)
        {
            binding.Builder!.DiscardDeferred();
            binding.PendingTextKeys.Clear();
            if (binding.DisposeAfterCapture)
                binding.Dispose();
            return;
        }
        var lists = binding.Builder!.Build();
        try
        {
            var scale = new UiVector2(ContentScale, ContentScale);
            var previous = binding.Content;
            if (previous is null || CapturedTexturesChanged(binding, previous)
                || previous.TextMode != capture.TextMode || previous.Bounds != capture.Bounds || previous.FramebufferScale != scale || !SamePaint(previous.DrawLists, lists))
            {
                var next = new UiPaintGroupContent(capture.Bounds, scale, lists, capture.TextMode);
                QueueTextureUpdate(new(UiTextureUpdateKind.RenderPaintGroup, binding.Black, UiTextureFormat.Rgba8Unorm,
                    next.PixelWidth, next.PixelHeight, ReadOnlyMemory<byte>.Empty)
                {
                    PaintGroup = next.Acquire(),
                    WhitePaintTexture = binding.White,
                    IsColorImage = true
                });
                binding.Content = next;
                binding.CapturedTextureVersions = next.TextureDependencies.Select(id => TextureContentVersions.GetValueOrDefault(id)).ToArray();
                previous?.Dispose();
            }
            binding.CapturedTextureRevision = TextureContentRevision;
            ReplaceRetainedTextDependencies(ref binding.TextKeys, ref binding.PendingTextKeys, retain: true);
            CompositeInteractivePaintGroup(binding, destination ?? capture.Bounds, opacity);
        }
        finally
        {
            foreach (var list in lists)
                list.ReleasePooled();
            if (binding.DisposeAfterCapture)
                binding.Dispose();
        }
    }

    private void CompositeInteractivePaintGroup(UiPaintGroup.Binding binding, UiRect destination, float opacity)
    {
        if (binding.Content is null)
            throw new InvalidOperationException("A paint snapshot must be captured before replay.");
        foreach (var key in binding.TextKeys)
        {
            TrackLayerTextDependency(key);
            _capturingPaintGroup?.PendingTextKeys.Add(key);
        }
        _builder.AddBackdropComposite(binding.Black, binding.White, destination, opacity);
    }

    private static bool SamePaint(IReadOnlyList<UiDrawList> previous, IReadOnlyList<UiDrawList> current)
    {
        if (previous.Count != current.Count)
            return false;
        for (var i = 0; i < previous.Count; i++)
        {
            var a = previous[i];
            var b = current[i];
            if (b.Scene3D is not null || !a.Vertices.AsSpan().SequenceEqual(b.Vertices.AsSpan())
                || !a.Indices.AsSpan().SequenceEqual(b.Indices.AsSpan()) || !a.Commands.AsSpan().SequenceEqual(b.Commands.AsSpan())
                || !SamePrimitives(a.RectFilledPrimitives, b.RectFilledPrimitives)
                || !SamePrimitives(a.CircleFilledPrimitives, b.CircleFilledPrimitives))
                return false;
        }
        return true;
    }

    private static bool SamePrimitives<T>(UiPooledList<T>? a, UiPooledList<T>? b) where T : struct
        => a is null ? b is null : b is not null && a.AsSpan().SequenceEqual(b.AsSpan());
}
