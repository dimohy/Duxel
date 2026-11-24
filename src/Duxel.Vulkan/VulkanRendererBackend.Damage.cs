using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Numerics;
using Duxel.Core;

namespace Duxel.Vulkan;

public sealed unsafe partial class VulkanRendererBackend
{
    // Preserve each acquired image independently: it may be several frames old.
    // Snapshots own only changing command bytes; resident geometry uses its revision contract.
    private sealed class DamageCommand
    {
        public UiDrawCommand Command;
        public UiWidgetDrawTransform? WidgetTransform;
        public UiLayer3D? ProjectedPlane;
        public long RetainedCommandRevision;
        public string? Key;
        public ulong Revision;
        public ulong TextureRevision, WhiteBackdropRevision;
        public uint IndexOrigin;
        public byte[] Geometry = [], Indices = [];
        public int GeometryLength, IndexLength, Readers;
        public UiRect Bounds;
        public UiRectFilledPrimitive[] PaintMasks = [];
        public int PaintMaskCount;
    }
    private sealed class DamageImage
    {
        public bool Initialized;
        public ulong Epoch;
        public readonly List<DamageCommand> Commands = [];
    }
    private readonly bool _damageEnabled = Environment.GetEnvironmentVariable("DUXEL_VK_DAMAGE") != "0";
    private readonly bool _projectedDamageEnabled = Environment.GetEnvironmentVariable("DUXEL_VK_PROJECTED_DAMAGE") == "1";
    private readonly record struct DamageCamera(Matrix4x4 Transform, UiRect? Viewport, int DepthScope);
    private readonly List<DamageCamera> _damageCameras = [];
    private readonly List<DamageCamera> _nextDamageCameras = [];
    private sealed record RetainedDamageLayer(bool CanGroup, UiTextureId Texture, UiRect Bounds);
    private readonly ConditionalWeakTable<UiPooledList<UiDrawCommand>, RetainedDamageLayer> _retainedDamageLayers = new();
    private readonly bool _sparseDamageEnabled = Environment.GetEnvironmentVariable("DUXEL_VK_SPARSE_DAMAGE") != "0";
    private DamageImage[] _damageImages = [];
    private readonly List<DamageCommand> _damageLatest = [];
    private readonly List<DamageCommand> _damageCurrent = [];
    private readonly Stack<DamageCommand> _damageFree = [];
    private ulong _damageEpoch;
    private (UiVector2 Position, UiVector2 Size, UiVector2 Scale) _damageDisplay;
    private bool _damageMsaaInitialized, _damagePartial, _damageEmpty;
    private string? _damagePlanReason;
    private Rect2D _damageArea;
    private UiRect _logicalDamageFullBounds;
    private Extent2D _logicalDamageFullExtent;
    private UiVector2 _damagePixelOffset, _damagePixelScale;

    private static int NextPaintMask(ReadOnlySpan<UiRectFilledPrimitive> records, int index)
    {
        var record = records[index];
        if (record.BorderThickness >= 0 || record.Color.Rgba == 0)
            return -1;
        var delta = unchecked((int)record.Color.Rgba);
        var parent = checked(index + delta);
        if (delta >= 0 || parent < 0)
            throw new InvalidOperationException("Paint mask parent must precede its child.");
        return parent;
    }
    private static bool PaintMasksEqual(DamageCommand snapshot, UiDrawList list, UiDrawCommand command)
    {
        var records = list.RectFilledPrimitives is { } primitives ? primitives.AsSpan() : ReadOnlySpan<UiRectFilledPrimitive>.Empty;
        var count = 0;
        for (var index = checked((int)command.CircleCutoutIndexPlusOne - 1); index >= 0; index = NextPaintMask(records, index))
        {
            var length = PaintMaskRecordCount(records[index]);
            if (length > snapshot.PaintMaskCount - count
                || !snapshot.PaintMasks.AsSpan(count, length).SequenceEqual(records.Slice(index, length)))
                return false;
            count += length;
        }
        return count == snapshot.PaintMaskCount;
    }
    private static int PaintMaskRecordCount(UiRectFilledPrimitive head)
        // Affine shapes include the following matrix record in their visual state.
        => head.BorderThickness is -3 or -4 or -5 ? 2 : 1;

    private static void CapturePaintMasks(DamageCommand snapshot, UiDrawList list, UiDrawCommand command)
    {
        var records = list.RectFilledPrimitives is { } primitives ? primitives.AsSpan() : ReadOnlySpan<UiRectFilledPrimitive>.Empty;
        var count = 0;
        for (var index = checked((int)command.CircleCutoutIndexPlusOne - 1); index >= 0; index = NextPaintMask(records, index))
            count += PaintMaskRecordCount(records[index]);
        if (snapshot.PaintMasks.Length < count)
            snapshot.PaintMasks = new UiRectFilledPrimitive[count];
        snapshot.PaintMaskCount = count;
        count = 0;
        for (var index = checked((int)command.CircleCutoutIndexPlusOne - 1); index >= 0; index = NextPaintMask(records, index))
        {
            var length = PaintMaskRecordCount(records[index]);
            records.Slice(index, length).CopyTo(snapshot.PaintMasks.AsSpan(count, length));
            count += length;
        }
    }
    private void ResetDamageImages(int count)
    {
        _presentedDamageValid = false;
        _damageImages = new DamageImage[count];
        for (var i = 0; i < count; i++)
            _damageImages[i] = new();
        _damageLatest.Clear();
        _damageCurrent.Clear();
        _damageFree.Clear();
        _damageMsaaInitialized = false;
        _repaintImageOutlines = [];
        _repaintMsaaOutline = default;
        _repaintUntil = 0;
    }

    private void PrepareLogicalDamageSnapshot(UiDrawData data)
    {
        var start = BeginFrameProfileTiming(_profilingEnabled);
        PrepareLogicalDamageSnapshotCore(data);
        _profileDamageSnapshotTicks += EndFrameProfileTiming(_profilingEnabled, start);
    }

    private void PrepareLogicalDamageSnapshotCore(UiDrawData data)
    {
        var unboundGeometry = default(FrameGeometryBuffers);
        var context = CreateCommandFrameContext(default, data, in unboundGeometry);
        _damagePixelOffset = new(context.ClipOffsetX, context.ClipOffsetY);
        _damagePixelScale = new(context.ClipScaleX, context.ClipScaleY);
        _logicalDamageFullBounds = new(0, 0, context.FramebufferWidth, context.FramebufferHeight);
        _logicalDamageFullExtent = new((uint)context.FramebufferWidthPixels, (uint)context.FramebufferHeightPixels);
        _damageArea = new(new(0, 0), new((uint)context.FramebufferWidthPixels, (uint)context.FramebufferHeightPixels));
        _damageEmpty = _damagePartial = false;
        _damageSnapshotPrepared = true;
        _damageSnapshotUsable = false;
        _damagePlanReason = "disabled";
        if (!_damageEnabled)
            return;
        var display = (data.DisplayPos, data.DisplaySize, data.FramebufferScale);
        if (_damageDisplay != display)
        {
            _damageEpoch++;
            _damageDisplay = display;
        }

        // Rebuild depth from all intersecting commands within each repair area.
        // Camera and depth-scope changes invalidate the entire image history.
        _nextDamageCameras.Clear();
        var hasOtherDepthScene = false;
        foreach (var list in data.DrawLists)
        {
            if (list.Scene3D is { } scene && !IsScreenAlignedScene(scene))
            {
                if (_projectedDamageEnabled && IsDamagePlane(scene))
                {
                    if (hasOtherDepthScene)
                    {
                        RejectProjectedDamage();
                        return;
                    }
                    var candidate = new DamageCamera(scene.Transform, scene.Viewport, scene.DepthScopeId);
                    if (_nextDamageCameras.Count == 0 || _nextDamageCameras[^1] != candidate)
                        _nextDamageCameras.Add(candidate);
                    if (GetRetainedDamageLayer(list) is { CanGroup: true })
                        continue;
                }
                else
                {
                    if (_nextDamageCameras.Count > 0 || !SupportsPartialDamage(scene, data.DisplayPos))
                    {
                        RejectProjectedDamage();
                        return;
                    }
                    hasOtherDepthScene = true;
                }
            }
            foreach (ref readonly var command in list.Commands.AsSpan())
                if (command.Callback is not null || command.UserData is not null)
                {
                    _damagePlanReason = command.Callback is not null ? "callback" : "command-user-data";
                    InvalidateLogicalDamage();
                    return;
                }
        }
        if (!CollectionsMarshal.AsSpan(_damageCameras).SequenceEqual(CollectionsMarshal.AsSpan(_nextDamageCameras)))
        {
            _damageCameras.Clear();
            _damageCameras.AddRange(_nextDamageCameras);
            _damageEpoch++;
        }

        _damageCurrent.Clear();
        foreach (var list in data.DrawLists)
        {
            var key = list.StaticGeometryKey;
            var revision = key is null ? 0 : list.GetStaticGeometryStamp();
            var widget = list.Scene3D?.WidgetTransform;
            var plane = _damageCameras.Count > 0 && list.Scene3D is { } projected && IsDamagePlane(projected) ? (UiLayer3D?)projected : null;
            if (plane is { } layer && GetRetainedDamageLayer(list) is { CanGroup: true } retained)
            {
                AppendRetainedDamageLayer(list, key!, revision, layer, retained, in context);
                continue;
            }
            foreach (ref readonly var command in list.Commands.AsSpan())
                if (command.ElementCount != 0)
                    AppendDamageCommand(list, in command, key, revision, widget, plane, in context);
        }

        _damageSnapshotUsable = true;
    }

    private void RejectProjectedDamage()
    {
        _damagePlanReason = "projected-scene";
        InvalidateLogicalDamage();
    }

    private RetainedDamageLayer? GetRetainedDamageLayer(UiDrawList list)
    {
        if (list.StaticGeometryKey is null || !list.HasOwnedOpacityRanges || list.CommandStateStamp == 0)
            return null;
        if (_retainedDamageLayers.TryGetValue(list.Commands, out var retained))
            return retained;
        UiTextureId? texture = null;
        var singleTexture = true;
        foreach (ref readonly var command in list.Commands.AsSpan())
        {
            if (command.Callback is not null || command.UserData is not null || command.WhiteBackdropTextureId.HasValue
                || command.ElementCount != 0 && texture is { } current && current != command.TextureId)
            {
                singleTexture = false;
                break;
            }
            if (command.ElementCount == 0)
                continue;
            texture = command.TextureId;
        }
        // A split texture dependency invalidates per command, not per layer.
        if (singleTexture && texture is { } single && TryGetLayerBounds(list, out var bounds))
            retained = new(true, single, bounds);
        else
            retained = new(false, default, default);
        _retainedDamageLayers.Add(list.Commands, retained);
        return retained;
    }

    private void AppendRetainedDamageLayer(UiDrawList list, string key, ulong revision, UiLayer3D plane,
        RetainedDamageLayer retained, in CommandFrameContext context)
    {
        var commandRevision = list.Commands.OpacityRevision;
        var textureRevision = _paintTextureRevisions.GetValueOrDefault(retained.Texture);
        var slot = _damageCurrent.Count;
        var previous = slot < _damageLatest.Count ? _damageLatest[slot] : null;
        if (previous is not null && previous.RetainedCommandRevision == commandRevision && previous.Key == key
            && previous.Revision == revision && previous.ProjectedPlane == plane && previous.TextureRevision == textureRevision)
        {
            _damageCurrent.Add(previous);
            return;
        }
        var view = GetLayerViewportTransform(plane, in context);
        var snapshot = _damageFree.TryPop(out var reusable) ? reusable : new DamageCommand();
        snapshot.Command = default;
        snapshot.WidgetTransform = null;
        snapshot.ProjectedPlane = plane;
        snapshot.RetainedCommandRevision = commandRevision;
        snapshot.Key = key;
        snapshot.Revision = revision;
        snapshot.Bounds = ProjectDamageBounds(retained.Bounds, default, in view, in context);
        snapshot.TextureRevision = textureRevision;
        snapshot.WhiteBackdropRevision = 0;
        snapshot.GeometryLength = snapshot.IndexLength = snapshot.PaintMaskCount = 0;
        snapshot.IndexOrigin = 0;
        _damageCurrent.Add(snapshot);
    }

    private void AppendDamageCommand(UiDrawList list, in UiDrawCommand command, string? key, ulong revision,
        UiWidgetDrawTransform? widget, UiLayer3D? plane, in CommandFrameContext context)
    {
        var slot = _damageCurrent.Count;
        var textureRevision = _paintTextureRevisions.GetValueOrDefault(command.TextureId);
        var whiteRevision = command.WhiteBackdropTextureId is { } white ? _paintTextureRevisions.GetValueOrDefault(white) : 0;
        var previous = slot < _damageLatest.Count ? _damageLatest[slot] : null;
        ReadOnlySpan<byte> geometry = [], indices = [];
        uint indexOrigin = 0;
        if (key is null)
            ReadStreamedDamageGeometry(list, in command, out geometry, out indices, out indexOrigin);

        if (previous is not null && previous.WidgetTransform == widget && previous.ProjectedPlane == plane && previous.Key == key
            && previous.Revision == revision && previous.TextureRevision == textureRevision
            && previous.WhiteBackdropRevision == whiteRevision && EquivalentDamageCommand(previous.Command, command, key is null)
            && PaintMasksEqual(previous, list, command) && geometry.SequenceEqual(previous.Geometry.AsSpan(0, previous.GeometryLength))
            && EquivalentDamageIndices(previous.Indices.AsSpan(0, previous.IndexLength), previous.IndexOrigin, indices, indexOrigin))
        {
            _damageCurrent.Add(previous);
            return;
        }

        var bounds = ScreenDamageBounds(in command, widget);
        // Include analytic coverage and fractional pixel rounding at both edges.
        var pixels = widget is null || bounds is { Width: > 0, Height: > 0 } ? new UiRect(
            (bounds.X - _damagePixelOffset.X) * _damagePixelScale.X - 2,
            (bounds.Y - _damagePixelOffset.Y) * _damagePixelScale.Y - 2,
            bounds.Width * _damagePixelScale.X + 4, bounds.Height * _damagePixelScale.Y + 4) : default;
        if (plane is { } projection)
        {
            var view = GetLayerViewportTransform(projection, in context);
            pixels = command.HasBounds
                ? ProjectDamageBounds(command.Bounds, command.Translation, in view, in context) : view.PixelViewport;
        }
        var snapshot = _damageFree.TryPop(out var reusable) ? reusable : new DamageCommand();
        snapshot.Command = command;
        snapshot.WidgetTransform = widget;
        snapshot.ProjectedPlane = plane;
        snapshot.RetainedCommandRevision = 0;
        snapshot.Key = key;
        snapshot.Revision = revision;
        snapshot.Bounds = pixels;
        snapshot.TextureRevision = textureRevision;
        snapshot.WhiteBackdropRevision = whiteRevision;
        snapshot.IndexOrigin = indexOrigin;
        CapturePaintMasks(snapshot, list, command);
        if (snapshot.Geometry.Length < geometry.Length)
            snapshot.Geometry = new byte[geometry.Length];
        if (snapshot.Indices.Length < indices.Length)
            snapshot.Indices = new byte[indices.Length];
        geometry.CopyTo(snapshot.Geometry);
        indices.CopyTo(snapshot.Indices);
        snapshot.GeometryLength = geometry.Length;
        snapshot.IndexLength = indices.Length;
        _damageCurrent.Add(snapshot);
    }

    private static void ReadStreamedDamageGeometry(UiDrawList list, scoped in UiDrawCommand command,
        out ReadOnlySpan<byte> geometry, out ReadOnlySpan<byte> indices, out uint indexOrigin)
    {
        var offset = checked((int)command.IndexOffset);
        var count = checked((int)command.ElementCount);
        indices = [];
        indexOrigin = 0;
        if (command.Kind == UiDrawCommandKind.Triangles)
        {
            var source = list.Indices.AsSpan().Slice(offset, count);
            uint min = uint.MaxValue, max = 0;
            foreach (var index in source)
            {
                min = Math.Min(min, index);
                max = Math.Max(max, index);
            }
            indices = MemoryMarshal.AsBytes(source);
            indexOrigin = min;
            geometry = MemoryMarshal.AsBytes(list.Vertices.AsSpan().Slice(
                checked((int)(min + command.VertexOffset)), checked((int)(max - min + 1))));
        }
        else
        {
            geometry = command.Kind == UiDrawCommandKind.RectFilledPrimitives
                ? MemoryMarshal.AsBytes(list.RectFilledPrimitives!.AsSpan().Slice(offset, count))
                : MemoryMarshal.AsBytes(list.CircleFilledPrimitives!.AsSpan().Slice(offset, count));
        }
    }

    private void InvalidateLogicalDamage()
    {
        _damageEpoch++;
        _damageCameras.Clear();
        _presentedDamageValid = false;
        ReleaseDamageCommands(_damageLatest);
        _damageCurrent.Clear();
        foreach (var old in _damageImages)
            ReleaseDamageCommands(old.Commands);
        _damageFree.Clear();
    }

    private void PrepareFrameDamage(UiDrawData data, uint imageIndex)
    {
        if (!_damageSnapshotPrepared)
            PrepareLogicalDamageSnapshot(data);
        var start = BeginFrameProfileTiming(_profilingEnabled);
        PlanAcquiredImageRepair(imageIndex);
        _profileDamageRepairTicks = EndFrameProfileTiming(_profilingEnabled, start);
    }

    private void PlanAcquiredImageRepair(uint imageIndex)
    {
        var image = _damageImages[imageIndex];
        var full = _logicalDamageFullBounds;
        _damageArea = new(new(0, 0), _logicalDamageFullExtent);
        _damageEmpty = _damagePartial = false;
        if (!_damageSnapshotUsable)
            return;
        var dirty = default(UiRect);
        if (!image.Initialized || image.Epoch != _damageEpoch)
        {
            _damagePlanReason = !image.Initialized ? "uninitialized-image" : "image-epoch";
            dirty = full;
        }
        else
        {
            _damagePlanReason = "compared";
            CompareDamageCommands(image.Commands, _damageCurrent, ref dirty);
            // The shared multisample attachment and this resolved image may hold
            // different historical frames. Repair the union before partial resolve.
            if (_msaaSampleCount != SampleCountFlags.Count1Bit)
                CompareDamageCommands(_damageLatest, _damageCurrent, ref dirty);
        }
        OverrideValidationDamage(ref dirty, in full);
        var left = (int)Math.Clamp(Math.Floor(dirty.X), 0, full.Width);
        var top = (int)Math.Clamp(Math.Floor(dirty.Y), 0, full.Height);
        var rightEdge = (int)Math.Clamp(Math.Ceiling(dirty.X + dirty.Width), 0, full.Width);
        var bottomEdge = (int)Math.Clamp(Math.Ceiling(dirty.Y + dirty.Height), 0, full.Height);
        _damageEmpty = rightEdge <= left || bottomEdge <= top;
        _damageArea = _damageEmpty ? new(new(0, 0), new(1, 1))
            : new(new(left, top), new((uint)(rightEdge - left), (uint)(bottomEdge - top)));
        _damagePartial = dirty != full;
        // Acquire new readers before releasing old ones: unchanged nodes may be shared.
        foreach (var command in _damageCurrent)
            command.Readers += 2;
        ReleaseDamageCommands(image.Commands);
        image.Commands.AddRange(_damageCurrent);
        image.Epoch = _damageEpoch;
        ReleaseDamageCommands(_damageLatest);
        _damageLatest.AddRange(_damageCurrent);
        // A shrinking scene must not keep the largest historical spare pool.
        TrimDamageSnapshotPool();
    }

    private void CompareDamageCommands(List<DamageCommand> previous, List<DamageCommand> current, ref UiRect dirty)
    {
        var first = 0;
        var beforeEnd = previous.Count;
        var afterEnd = current.Count;
        while (first < beforeEnd && first < afterEnd && EquivalentDamageSnapshot(previous[first], current[first]))
            first++;
        // Insertions must not shift the identity of an unchanged suffix.
        while (beforeEnd > first && afterEnd > first && EquivalentDamageSnapshot(previous[beforeEnd - 1], current[afterEnd - 1]))
        {
            beforeEnd--;
            afterEnd--;
        }

        if (_sparseDamageEnabled)
        {
            // Compare aligned slots before extending the union for inserted or deleted tails.
            var pairedEnd = Math.Min(beforeEnd, afterEnd);
            for (; first < pairedEnd; first++)
                IncludeChangedDamage(previous[first], current[first], ref dirty);
        }
        for (var i = first; i < beforeEnd; i++)
            IncludeDamageBounds(ref dirty, previous[i].Bounds);
        for (var i = first; i < afterEnd; i++)
            IncludeDamageBounds(ref dirty, current[i].Bounds);
    }

    private void IncludeChangedDamage(DamageCommand before, DamageCommand after, ref UiRect dirty)
    {
        if (EquivalentDamageSnapshot(before, after))
            return;
        if (before.ProjectedPlane is null && after.ProjectedPlane is null
            && TryGetPrimitiveDamage(before, after, out var primitiveDamage))
            IncludeDamageBounds(ref dirty, primitiveDamage);
        else
        {
            IncludeDamageBounds(ref dirty, before.Bounds);
            IncludeDamageBounds(ref dirty, after.Bounds);
        }
    }

    private static void IncludeDamageBounds(ref UiRect dirty, UiRect bounds)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return;
        if (dirty.Width == 0 || dirty.Height == 0)
        {
            dirty = bounds;
            return;
        }
        var x = Math.Min(dirty.X, bounds.X);
        var y = Math.Min(dirty.Y, bounds.Y);
        dirty = new(x, y, Math.Max(dirty.X + dirty.Width, bounds.X + bounds.Width) - x,
            Math.Max(dirty.Y + dirty.Height, bounds.Y + bounds.Height) - y);
    }

    private static bool EquivalentDamageCommand(UiDrawCommand before, UiDrawCommand after, bool streamed)
    {
        if (before == after)
            return true;
        if (!streamed)
            return false;
        // Compare referenced geometry independently of its position in a streamed buffer.
        before = before with
        {
            IndexOffset = 0,
            VertexOffset = before.Kind == UiDrawCommandKind.Triangles ? 0 : before.VertexOffset
        };
        after = after with
        {
            IndexOffset = 0,
            VertexOffset = after.Kind == UiDrawCommandKind.Triangles ? 0 : after.VertexOffset
        };
        return before == after;
    }

    private static bool EquivalentDamageIndices(ReadOnlySpan<byte> before, uint beforeOrigin, ReadOnlySpan<byte> after, uint afterOrigin)
    {
        if (beforeOrigin == afterOrigin)
            return before.SequenceEqual(after);
        if (before.Length != after.Length)
            return false;
        var left = MemoryMarshal.Cast<byte, uint>(before);
        var right = MemoryMarshal.Cast<byte, uint>(after);
        for (var i = 0; i < left.Length; i++)
            if (left[i] - beforeOrigin != right[i] - afterOrigin)
                return false;
        return true;
    }

    private static bool EquivalentDamageSnapshot(DamageCommand before, DamageCommand after)
    {
        if (ReferenceEquals(before, after))
            return true;
        return before.Key == after.Key && before.Revision == after.Revision
            && before.RetainedCommandRevision == after.RetainedCommandRevision
            && before.WidgetTransform == after.WidgetTransform && before.ProjectedPlane == after.ProjectedPlane
            && before.TextureRevision == after.TextureRevision && before.WhiteBackdropRevision == after.WhiteBackdropRevision
            && EquivalentDamageCommand(before.Command, after.Command, before.Key is null)
            && before.Geometry.AsSpan(0, before.GeometryLength).SequenceEqual(after.Geometry.AsSpan(0, after.GeometryLength))
            && EquivalentDamageIndices(before.Indices.AsSpan(0, before.IndexLength), before.IndexOrigin, after.Indices.AsSpan(0, after.IndexLength), after.IndexOrigin)
            && before.PaintMaskCount == after.PaintMaskCount
            && before.PaintMasks.AsSpan(0, before.PaintMaskCount).SequenceEqual(after.PaintMasks.AsSpan(0, after.PaintMaskCount));
    }

    private void TrimDamageSnapshotPool()
    {
        var spareLimit = (long)_damageLatest.Count * (_damageImages.Length + 1);
        while (_damageFree.Count > spareLimit)
            _damageFree.Pop();
    }

    private void ReleaseDamageCommands(List<DamageCommand> commands)
    {
        foreach (var command in commands)
            if (--command.Readers == 0)
                _damageFree.Push(command);
        commands.Clear();
    }

    partial void OverrideValidationDamage(ref UiRect dirty, in UiRect full);

    private static bool SupportsPartialDamage(UiLayer3D scene, UiVector2 displayPosition) =>
        IsScreenAlignedScene(scene) || (displayPosition == default
            && scene is { WriteDepth: false, Viewport: null, DepthScopeId: 0, WidgetTransform: { SourceScene: null } });

    private static bool IsDamagePlane(UiLayer3D scene)
    {
        var m = scene.Transform;
        return scene is { PlaneModel: not null, WidgetTransform: null, IsScreenSpacePresentation: false }
            && m.M12 == 0 && m.M21 == 0 && m.M14 == 0 && m.M24 == 0 && m.M44 == 1
            && float.IsFinite(m.M11) && float.IsFinite(m.M22)
            && float.IsFinite(m.M41) && float.IsFinite(m.M42);
    }

    private static UiRect ProjectDamageBounds(UiRect bounds, UiVector2 translation,
        in LayerViewportTransform view, in CommandFrameContext context)
    {
        var model = view.PlaneModel!.Value.Translation;
        var x = (bounds.X + translation.X + model.X) * context.ScaleX + context.TranslateX;
        var y = (bounds.Y + translation.Y + model.Y) * context.ScaleY + context.TranslateY;
        var a = Vector4.Transform(new Vector4(x, y, 0, 1), view.Transform);
        var b = Vector4.Transform(new Vector4(x + bounds.Width * context.ScaleX, y + bounds.Height * context.ScaleY, 0, 1), view.Transform);
        var viewport = view.PixelViewport;
        if (!float.IsFinite(a.X) || !float.IsFinite(a.Y) || !float.IsFinite(b.X) || !float.IsFinite(b.Y))
            return viewport;
        float PixelX(float value) => viewport.X + (value + 1) * .5f * viewport.Width;
        float PixelY(float value) => viewport.Y + (value + 1) * .5f * viewport.Height;
        var left = Math.Clamp(PixelX(MathF.Min(a.X, b.X)) - 2, viewport.X, viewport.X + viewport.Width);
        var top = Math.Clamp(PixelY(MathF.Min(a.Y, b.Y)) - 2, viewport.Y, viewport.Y + viewport.Height);
        var right = Math.Clamp(PixelX(MathF.Max(a.X, b.X)) + 2, viewport.X, viewport.X + viewport.Width);
        var bottom = Math.Clamp(PixelY(MathF.Max(a.Y, b.Y)) + 2, viewport.Y, viewport.Y + viewport.Height);
        return new(left, top, right - left, bottom - top);
    }

    private bool IsOutsideDamageArea(UiRect pixels) =>
        pixels.Width <= 0 || pixels.Height <= 0
        || pixels.X >= _damageArea.Offset.X + _damageArea.Extent.Width
        || pixels.Y >= _damageArea.Offset.Y + _damageArea.Extent.Height
        || pixels.X + pixels.Width <= _damageArea.Offset.X || pixels.Y + pixels.Height <= _damageArea.Offset.Y;

    private static UiRect ScreenDamageBounds(in UiDrawCommand command, UiWidgetDrawTransform? widget)
    {
        var clip = command.ClipRect;
        var local = command.HasBounds ? Intersect(command.Bounds, clip) : clip;
        var moved = local with { X = local.X + command.Translation.X, Y = local.Y + command.Translation.Y };
        if (widget is null)
            return moved;
        if (local.Width <= 0 || local.Height <= 0)
            return default;
        return Intersect(UiWidgetAffineLayout.MapBounds(moved, widget.ToParent), widget.ParentClip);

        static UiRect Intersect(UiRect first, UiRect second)
        {
            var x = MathF.Max(first.X, second.X);
            var y = MathF.Max(first.Y, second.Y);
            var right = MathF.Min(first.X + first.Width, second.X + second.Width);
            var bottom = MathF.Min(first.Y + first.Height, second.Y + second.Height);
            return new(x, y, MathF.Max(0, right - x), MathF.Max(0, bottom - y));
        }
    }

    private bool IsOutsideFrameDamage(in UiDrawCommand command, in CommandFrameContext context, UiLayer3D? scene)
    {
        if (!_damagePartial)
            return false;
        if (_damageEmpty)
            return true;
        if (_damageCameras.Count > 0 && scene is { } plane && IsDamagePlane(plane))
        {
            if (!command.HasBounds)
                return false;
            var view = GetLayerViewportTransform(plane, in context);
            return IsOutsideDamageArea(ProjectDamageBounds(command.Bounds, command.Translation, in view, in context));
        }
        var widget = scene?.WidgetTransform;
        var bounds = widget is null ? command.HasBounds ? command.Bounds : command.ClipRect
            : ScreenDamageBounds(in command, widget);
        if (widget is not null && (bounds.Width <= 0 || bounds.Height <= 0))
            return true;
        if (widget is null)
            bounds = bounds with { X = bounds.X + command.Translation.X, Y = bounds.Y + command.Translation.Y };
        var x = (bounds.X - context.ClipOffsetX) * context.ClipScaleX;
        var y = (bounds.Y - context.ClipOffsetY) * context.ClipScaleY;
        return x - 2 >= _damageArea.Offset.X + _damageArea.Extent.Width
            || y - 2 >= _damageArea.Offset.Y + _damageArea.Extent.Height
            || x + bounds.Width * context.ClipScaleX + 2 <= _damageArea.Offset.X
            || y + bounds.Height * context.ClipScaleY + 2 <= _damageArea.Offset.Y;
    }
}
