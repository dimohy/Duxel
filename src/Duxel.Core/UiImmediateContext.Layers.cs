using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    private const string StaticLayerGeometryTagPrefix = "duxel.layer.static:";
    private const float StaticLayerCaptureClipExtent = 16_777_216f;

    private sealed class UiLayerCachedList
    {
        public required UiLayerFrameLifetime Lifetime
        {
            get; init;
        }
        public required string BaseStaticGeometryKey
        {
            get; init;
        }
        public UiDrawCommand[]? LocalCommandSnapshot
        {
            get; init;
        }
        public required ulong LocalStaticGeometryStamp
        {
            get; set;
        }
        public required ulong LocalCommandScheduleShapeStamp
        {
            get; set;
        }
        public required ulong LocalCommandTopologyStamp
        {
            get; set;
        }
        public UiLayer3D? LocalScene3D;
        public UiLayer3D? CapturedParentScene3D;
        public UiDrawList? OwnedSource;
        // The retained source keeps its original commands until the same retirement as its geometry.
        public ReadOnlySpan<UiDrawCommand> LocalCommands => OwnedSource is { } source
            ? source.Commands.AsSpan()
            : LocalCommandSnapshot.AsSpan();
        public required UiPooledList<UiDrawVertex> LocalVertexList
        {
            get; set;
        }
        public required UiPooledList<uint> LocalIndexList
        {
            get; set;
        }
        public UiPooledList<UiRectFilledPrimitive>? LocalRectFilledPrimitiveList
        {
            get; set;
        }
        public UiPooledList<UiCircleFilledPrimitive>? LocalCircleFilledPrimitiveList
        {
            get; set;
        }

        public UiDrawCommand[]? ReplayCommands;
        public UiPooledList<UiDrawCommand>? ReplayCommandList;
        public UiDrawList? ReplayDrawList;
        public UiDrawListScenePool? ScenePool;
        public UiVector2 ReplayTranslation;
        public UiRect ReplayLocalClip;
        public float ReplayOpacity = -1f;
        public bool ReplayValid;
    }

    private sealed class UiLayerCacheEntry
    {
        public UiLayerCachedList[]? Lists;
        public bool Dirty = true;
        public bool Removed;
        public bool VectorTextMode;
        public bool FontAtlasIndependent;
        public bool DependsOnFontAtlas = true;
        public UiDrawListBuilder? Builder;
        public bool BuilderInUse;
        public long? ContentVersion;
        public double? FrameDeadline;
        public bool UnboundedAnimation;
        public bool HasAnimationBounds;
        public UiRect AnimationBounds;
        public HashSet<DirectTextCacheKey> TextKeys = [];
        public HashSet<DirectTextCacheKey> PendingTextKeys = [];
    }

    private readonly record struct UiLayerFrame(string LayerId, bool ShouldDraw, UiLayerOptions Options, UiDrawListBuilder? LayerBuilder, bool LocalClipPushed, UiRect LocalClip, UiLayerCacheEntry Entry, ulong AnimationSerial = 0, int ParentCameraLayerDepth = 0);

    private sealed class UiLayerCacheStore
    {
        public UiLayerFrameLifetime Lifetime { get; } = new();
        public Dictionary<string, UiLayerCacheEntry> Layers { get; } = new(StringComparer.Ordinal);
        public List<UiDrawListBuilder?> CaptureBuilders { get; } = [];
        public Dictionary<DirectTextCacheKey, int> RetainedTextKeys { get; } = [];
        public ulong TextRetentionRevision;
    }

    private static readonly ConditionalWeakTable<UiState, UiLayerCacheStore> LayerCacheStores = new();
    private readonly Stack<UiLayerFrame> _layerFrames = new();
    // Depth of the innermost active camera. Screen portals compare this with their entry depth.
    private int _cameraLayerDepth;
    private readonly List<UiDrawListScenePool.Lease> _frameSceneLeases = [];
    private UiLayerFrameLifetime.Lease? _pendingLayerFrameLease;

    internal UiLayerFrameLifetime.Lease? TakeLayerFrameLease()
    {
        var lease = _pendingLayerFrameLease;
        _pendingLayerFrameLease = null;
        return lease;
    }

    internal UiPooledList<UiDrawListScenePool.Lease>? TakeFrameSceneLeases()
    {
        if (_frameSceneLeases.Count == 0)
            return null;
        var leases = UiPooledList<UiDrawListScenePool.Lease>.RentAndCopy(_frameSceneLeases);
        _frameSceneLeases.Clear();
        return leases;
    }

    internal void ReleasePendingFrameSceneLeases()
    {
        foreach (var lease in _frameSceneLeases)
            lease.Return();
        _frameSceneLeases.Clear();
        _pendingLayerFrameLease?.Return();
        _pendingLayerFrameLease = null;
    }

    private UiLayerCacheStore? _layerCacheStore;
    private UiLayerCacheStore LayerCache => _layerCacheStore ??= LayerCacheStores.GetOrCreateValue(_state);
    private Dictionary<string, UiLayerCacheEntry> LayerCaches => LayerCache.Layers;

    public bool BeginLayer(string layerId, bool staticCache = false, float opacity = 1f, UiVector2 translation = default)
        => BeginLayer(layerId, new UiLayerOptions(staticCache, opacity, translation));

    /// <summary>Submits unchanged retained content without a BeginLayer/EndLayer scope. On false, build it through the normal scope.</summary>
    public bool TryReplayLayer(string layerId, in UiLayerOptions options)
    {
        ArgumentException.ThrowIfNullOrEmpty(layerId);
        if ((!options.StaticCache && !options.ContentVersion.HasValue)
            || !LayerCaches.TryGetValue(layerId, out var entry) || entry.Dirty || entry.Removed
            || entry.BuilderInUse || entry.Lists is null
            || options.FontAtlasIndependent != entry.FontAtlasIndependent
            || NeedsLayerAnimationFrame(entry, options) || entry.FrameDeadline is { } deadline && deadline <= _state.TimeSeconds
            || (options.ContentVersion.HasValue && options.ContentVersion != entry.ContentVersion))
            return false;

        options.Scene3D?.Validate();
        var parentClip = CurrentClipRect;
        var localClip = new UiRect(parentClip.X - options.Translation.X, parentClip.Y - options.Translation.Y,
            parentClip.Width, parentClip.Height);
        if (HasLayerClipArea(localClip))
            ReplayLayerCache(entry, options with
            {
                Opacity = Math.Clamp(options.Opacity, 0f, 1f)
            }, localClip);
        return true;
    }

    private void ReplayLayerCache(UiLayerCacheEntry entry, in UiLayerOptions options, UiRect localClip)
    {
        if (entry.DependsOnFontAtlas)
            TrackLayerFontAtlasDependency();
        if (entry.FrameDeadline is { } deadline)
            RequestFrameAt(deadline);
        if (entry.HasAnimationBounds)
            TrackLayerAnimationBounds(entry.AnimationBounds with
            {
                X = entry.AnimationBounds.X + options.Translation.X,
                Y = entry.AnimationBounds.Y + options.Translation.Y
            });
        if (entry.Lists is not { Length: > 0 })
            return;
        if (_layerFrames.Count > 0)
            foreach (var key in entry.TextKeys)
                TrackLayerTextDependency(key);
        if (!IsPresentationPass)
            return;
        var replayClip = options.Scene3D.HasValue ? CreateStaticLayerCaptureClip() : localClip;
        foreach (var cached in entry.Lists)
        {
            EnsureReplay(cached, options.Opacity, options.Translation, replayClip);
            AppendLayerReplay(cached, options.Scene3D);
        }
    }

    public bool BeginLayer(string layerId, UiLayerOptions options)
    {
        layerId ??= string.Empty;
        if (layerId.Length == 0)
        {
            throw new ArgumentException("Layer id must not be empty.", nameof(layerId));
        }

        var nextOpacity = Math.Clamp(options.Opacity, 0f, 1f);
        options.Scene3D?.Validate();
        var nextOptions = options with
        {
            Opacity = nextOpacity
        };
        var layerCaches = LayerCaches;
        if (!layerCaches.TryGetValue(layerId, out var entry))
        {
            entry = new UiLayerCacheEntry();
            layerCaches[layerId] = entry;
        }
        var retainContent = nextOptions.StaticCache || nextOptions.ContentVersion.HasValue;
        var vectorTextMode = nextOptions.Scene3D.HasValue || IsVectorTextLayer();
        if (entry.VectorTextMode != vectorTextMode || entry.FontAtlasIndependent != nextOptions.FontAtlasIndependent)
            entry.Dirty = true;
        entry.VectorTextMode = vectorTextMode;
        entry.FontAtlasIndependent = nextOptions.FontAtlasIndependent;
        if (nextOptions.ContentVersion.HasValue && nextOptions.ContentVersion != entry.ContentVersion)
            entry.Dirty = true;
        if (NeedsLayerAnimationFrame(entry, nextOptions) || entry.FrameDeadline is { } deadline && deadline <= _state.TimeSeconds)
            entry.Dirty = true;

        var parentClip = CurrentClipRect;
        var layerLocalClip = new UiRect(
            parentClip.X - nextOptions.Translation.X,
            parentClip.Y - nextOptions.Translation.Y,
            parentClip.Width,
            parentClip.Height);

        if (retainContent && !HasLayerClipArea(layerLocalClip))
        {
            PushLayerFrame(new UiLayerFrame(layerId, ShouldDraw: false, nextOptions, LayerBuilder: null, LocalClipPushed: false, LocalClip: layerLocalClip, Entry: entry));
            return false;
        }

        if (retainContent && !entry.Dirty && entry.Lists is not null)
        {
            PushLayerFrame(new UiLayerFrame(layerId, ShouldDraw: false, nextOptions, LayerBuilder: null, LocalClipPushed: false, LocalClip: layerLocalClip, Entry: entry));
            return false;
        }

        var captureLocalClip = retainContent || nextOptions.Scene3D.HasValue ? CreateStaticLayerCaptureClip() : layerLocalClip;
        if (entry.BuilderInUse)
        {
            throw new InvalidOperationException("Nested layers must use distinct ids.");
        }
        var layerBuilder = retainContent
            ? GetRetainedLayerCaptureBuilder(captureLocalClip)
            : entry.Builder ??= new UiDrawListBuilder(captureLocalClip);
        layerBuilder._SetClipRect(captureLocalClip);
        layerBuilder._ResetForNewFrame();
        layerBuilder.SetLayerScene3D(nextOptions.Scene3D);
        entry.BuilderInUse = true;
        entry.DependsOnFontAtlas = !nextOptions.FontAtlasIndependent;
        entry.FrameDeadline = null;
        entry.UnboundedAnimation = false;
        entry.HasAnimationBounds = false;
        entry.PendingTextKeys.Clear();
        layerBuilder._SetDrawListSharedData(GetDrawListSharedData());
        layerBuilder.PushTexture(_whiteTexture);

        _builderStack.Push(_builder);
        _builder = layerBuilder;
        _clipStack.Push(captureLocalClip);
        PushLayerFrame(new UiLayerFrame(layerId, ShouldDraw: true, nextOptions, layerBuilder, LocalClipPushed: true, LocalClip: layerLocalClip, Entry: entry, AnimationSerial: _state.UnboundedAnimationRequestSerial));
        return true;
    }

    private UiDrawListBuilder GetRetainedLayerCaptureBuilder(UiRect clip)
    {
        // Build transfers the draw lists and their buffers. Only scratch
        // remains here, so sibling layer captures can share a builder.
        var builders = _layerCacheStore!.CaptureBuilders;
        var depth = _layerFrames.Count;
        while (builders.Count <= depth)
            builders.Add(null);
        return builders[depth] ??= new UiDrawListBuilder(clip);
    }

    private void PushLayerFrame(UiLayerFrame frame)
    {
        _layerFrames.Push(frame with { ParentCameraLayerDepth = _cameraLayerDepth });
        if (frame.Options.Scene3D.HasValue)
            _cameraLayerDepth = _layerFrames.Count;
    }

    public void EndLayer()
    {
        if (_layerFrames.Count == 0)
        {
            return;
        }

        var frame = _layerFrames.Pop();
        _cameraLayerDepth = frame.ParentCameraLayerDepth;
        var replayClip = frame.Options.Scene3D.HasValue ? CreateStaticLayerCaptureClip() : frame.LocalClip;
        var entry = frame.Entry;
        if (entry.Removed)
        {
            return;
        }

        if (!frame.ShouldDraw)
        {
            if (!HasLayerClipArea(frame.LocalClip))
            {
                return;
            }

            ReplayLayerCache(entry, frame.Options, frame.LocalClip);

            return;
        }

        var layerBuilder = frame.LayerBuilder;
        entry.UnboundedAnimation = _state.UnboundedAnimationRequestSerial != frame.AnimationSerial;
        if (layerBuilder is null)
        {
            if (frame.LocalClipPushed && _clipStack.Count > 0)
            {
                _clipStack.Pop();
            }

            return;
        }

        if (!IsPresentationPass)
        {
            layerBuilder._ResetForNewFrame();
            entry.BuilderInUse = false;
            entry.Dirty = true;
            entry.PendingTextKeys.Clear();
            if (frame.LocalClipPushed && _clipStack.Count > 0)
                _clipStack.Pop();
            _builder = _builderStack.Pop();
            return;
        }

        var built = layerBuilder.Build();
        entry.BuilderInUse = false;
        if (entry.DependsOnFontAtlas)
            TrackLayerFontAtlasDependency();
        if (frame.LocalClipPushed && _clipStack.Count > 0)
        {
            _clipStack.Pop();
        }

        _builder = _builderStack.Pop();

        try
        {
            CompleteLayer(frame, built.AsSpan(), replayClip);
        }
        finally
        {
            // Only recycle the list of references here. The layer cache or parent owns the geometry.
            built.Return();
        }
    }

    private void CompleteLayer(in UiLayerFrame frame, ReadOnlySpan<UiDrawList> drawLists, UiRect replayClip)
    {
        var entry = frame.Entry;
        if (frame.Options.StaticCache || frame.Options.ContentVersion.HasValue)
        {
            if (HasRenderableLayerContent(drawLists))
            {
                entry.Lists = CaptureLayerLists(frame.LayerId, drawLists, entry.Lists, frame.Options.ContentVersion.HasValue, frame.Options.Scene3D);
                entry.Dirty = false;
                entry.ContentVersion = frame.Options.ContentVersion;
                ReplaceLayerTextDependencies(entry, retain: true);
                if (!frame.Options.ContentVersion.HasValue)
                    ReleaseLayerDrawLists(drawLists);

                if (entry.Lists is { Length: > 0 })
                {
                    for (var i = 0; i < entry.Lists.Length; i++)
                    {
                        var cached = entry.Lists[i];
                        EnsureReplay(cached, frame.Options.Opacity, frame.Options.Translation, replayClip);
                        AppendLayerReplay(cached, frame.Options.Scene3D);
                    }
                }
            }
            else
            {
                ReplaceLayerTextDependencies(entry, retain: false);
                ReleaseOwnedLayerSources(entry.Lists);
                entry.Lists = frame.Options.ContentVersion.HasValue ? [] : null;
                entry.ContentVersion = frame.Options.ContentVersion;
                ReleaseLayerDrawLists(drawLists);
                entry.Dirty = !frame.Options.ContentVersion.HasValue;
            }
        }
        else
        {
            ReplaceLayerTextDependencies(entry, retain: false);
            for (var i = 0; i < drawLists.Length; i++)
            {
                _builder.AppendLayerReference(drawLists[i], frame.Options.Opacity, frame.Options.Translation, frame.Options.Scene3D);
            }

            // Geometry ownership moves to the parent and ultimately to UiDrawData.
            ReleaseOwnedLayerSources(entry.Lists);
            entry.Lists = null;
            entry.ContentVersion = null;
            entry.Dirty = true;
        }
    }

    public void MarkLayerDirty(string layerId)
    {
        if (string.IsNullOrWhiteSpace(layerId))
        {
            return;
        }

        if (LayerCaches.TryGetValue(layerId, out var entry))
        {
            entry.Dirty = true;
        }
    }

    private void TrackLayerFrameDeadline(double deadline)
    {
        foreach (var frame in _layerFrames)
            if (frame.ShouldDraw && (frame.Entry.FrameDeadline is not { } previous || deadline < previous))
                frame.Entry.FrameDeadline = deadline;
    }

    private bool NeedsLayerAnimationFrame(UiLayerCacheEntry entry, in UiLayerOptions options) =>
        entry.UnboundedAnimation || entry.HasAnimationBounds && options.Opacity > 0 && IsControlAnimationVisible(
            entry.AnimationBounds with
            {
                X = entry.AnimationBounds.X + options.Translation.X,
                Y = entry.AnimationBounds.Y + options.Translation.Y
            },
            options.Scene3D.HasValue);

    private void TrackLayerAnimationBounds(UiRect bounds)
    {
        foreach (var frame in _layerFrames)
        {
            if (frame.ShouldDraw)
            {
                var entry = frame.Entry;
                if (!entry.HasAnimationBounds)
                {
                    entry.AnimationBounds = bounds;
                    entry.HasAnimationBounds = true;
                }
                else
                {
                    var previous = entry.AnimationBounds;
                    var x = MathF.Min(previous.X, bounds.X);
                    var y = MathF.Min(previous.Y, bounds.Y);
                    entry.AnimationBounds = new(x, y, MathF.Max(previous.X + previous.Width, bounds.X + bounds.Width) - x,
                        MathF.Max(previous.Y + previous.Height, bounds.Y + bounds.Height) - y);
                }
            }
            bounds = bounds with
            {
                X = bounds.X + frame.Options.Translation.X,
                Y = bounds.Y + frame.Options.Translation.Y
            };
        }
    }

    private bool IsControlAnimationVisible(UiRect bounds, bool projected = false)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return false;
        foreach (var frame in _layerFrames)
        {
            if (frame.Options.Opacity <= 0)
                return false;
            projected |= frame.Options.Scene3D.HasValue;
        }
        // Projected visibility needs the scene transform; local 2D rectangles cannot reject it.
        if (projected)
            return true;
        bounds = IntersectRect(bounds, CurrentClipRect);
        foreach (var frame in _layerFrames)
        {
            bounds = IntersectRect(bounds, frame.LocalClip);
            if (!HasLayerClipArea(bounds))
                return false;
            bounds = bounds with
            {
                X = bounds.X + frame.Options.Translation.X,
                Y = bounds.Y + frame.Options.Translation.Y
            };
        }
        return HasLayerClipArea(bounds);
    }

    private void AppendLayerReplay(UiLayerCachedList cached, UiLayer3D? parentScene)
    {
        _pendingLayerFrameLease ??= cached.Lifetime.Acquire();
        var scene = cached.LocalScene3D.HasValue && cached.LocalScene3D != cached.CapturedParentScene3D
            ? cached.LocalScene3D : parentScene ?? cached.LocalScene3D;
        if (parentScene is { } current && cached.LocalScene3D is { } local
            && cached.CapturedParentScene3D is { } captured
            && local.WriteDepth != captured.WriteDepth && (local with
            {
                WriteDepth = captured.WriteDepth
            }) == captured)
            scene = current with
            {
                WriteDepth = local.WriteDepth
            };
        var replay = cached.ReplayDrawList!;
        if (replay.Scene3D == scene)
            _builder.AppendRetainedStaticReference(replay);
        else
        {
            if (cached.ScenePool is null || !ReferenceEquals(cached.ScenePool.Template, replay))
                cached.ScenePool = new(replay);
            var lease = cached.ScenePool.Rent(scene);
            _frameSceneLeases.Add(lease);
            _builder.AppendRetainedStaticReference(lease.List);
        }
    }

    public void MarkAllLayersDirty()
    {
        foreach (var pair in LayerCaches)
        {
            pair.Value.Dirty = true;
        }
    }

    private void MarkFontAtlasLayersDirty()
    {
        if (_layerCacheStore is null)
            return;
        foreach (var entry in _layerCacheStore.Layers.Values)
        {
            if (entry.DependsOnFontAtlas)
                entry.Dirty = true;
        }
    }

    private void TrackLayerFontAtlasDependency()
    {
        foreach (var frame in _layerFrames)
        {
            if (frame.ShouldDraw && frame.Options.FontAtlasIndependent)
                frame.Entry.DependsOnFontAtlas = true;
        }
    }

    public void RemoveLayer(string layerId)
    {
        if (!LayerCaches.TryGetValue(layerId, out var entry))
            return;
        if (entry.BuilderInUse)
            throw new InvalidOperationException("An active layer cannot be removed.");
        ReplaceLayerTextDependencies(entry, retain: false);
        ReleaseOwnedLayerSources(entry.Lists);
        entry.Removed = true;
        LayerCaches.Remove(layerId);
    }

    internal static void ReleaseLayerResources(UiState state)
    {
        if (!LayerCacheStores.TryGetValue(state, out var store))
            return;
        foreach (var entry in store.Layers.Values)
        {
            entry.Removed = true;
            entry.Builder?._ClearFreeMemory();
            ReleaseOwnedLayerSources(entry.Lists);
        }
        foreach (var builder in store.CaptureBuilders)
            builder?._ClearFreeMemory();
        store.Layers.Clear();
        store.RetainedTextKeys.Clear();
        store.TextRetentionRevision++;
        LayerCacheStores.Remove(state);
    }

    private static bool ReleaseOwnedLayerSources(UiLayerCachedList[]? lists)
    {
        var released = false;
        if (lists is null)
            return false;
        foreach (var list in lists)
        {
            if (list.OwnedSource is null)
                continue;
            list.Lifetime.Retire(list.OwnedSource);
            list.OwnedSource = null;
            released = true;
        }
        return released;
    }

    private void TrackLayerTextDependency(DirectTextCacheKey key)
    {
        if (_layerFrames.Count == 0)
            return;

        foreach (var frame in _layerFrames)
        {
            if (frame.ShouldDraw && (frame.Options.StaticCache || frame.Options.ContentVersion.HasValue))
                frame.Entry.PendingTextKeys.Add(key);
        }
    }

    private void ReplaceLayerTextDependencies(UiLayerCacheEntry entry, bool retain)
    {
        ReplaceRetainedTextDependencies(ref entry.TextKeys, ref entry.PendingTextKeys, retain);
        if (retain)
            foreach (var key in entry.TextKeys)
                TrackLayerTextDependency(key);
    }

    private void ReplaceRetainedTextDependencies(ref HashSet<DirectTextCacheKey> current,
        ref HashSet<DirectTextCacheKey> pending, bool retain)
    {
        if (current.Count == 0 && pending.Count == 0)
            return;
        var store = LayerCache;
        var references = store.RetainedTextKeys;
        store.TextRetentionRevision++;
        foreach (var key in current)
        {
            var count = references[key] - 1;
            if (count == 0)
                references.Remove(key);
            else
                references[key] = count;
        }
        current.Clear();
        if (!retain)
        {
            pending.Clear();
            return;
        }
        (current, pending) = (pending, current);
        foreach (var key in current)
            references[key] = references.GetValueOrDefault(key) + 1;
    }

    private UiLayerCachedList[] CaptureLayerLists(
        string layerId,
        ReadOnlySpan<UiDrawList> drawLists,
        UiLayerCachedList[]? previousLists,
        bool transferOwnership = false,
        UiLayer3D? parentScene = null)
    {
        var releasedOwnedSources = ReleaseOwnedLayerSources(previousLists);
        // Submitted frames reference replay lists and their leases, never this array of cache records.
        var result = previousLists is not null && previousLists.Length == drawLists.Length
            ? previousLists
            : new UiLayerCachedList[drawLists.Length];
        for (var i = 0; i < drawLists.Length; i++)
        {
            var list = drawLists[i];
            var previous = previousLists is not null && i < previousLists.Length ? previousLists[i] : null;
            if (!releasedOwnedSources && !transferOwnership && previous is not null && TryRefreshLayerCachedList(previous, list))
            {
                previous.CapturedParentScene3D = parentScene;
                result[i] = previous;
                continue;
            }

            var takeOwnership = transferOwnership && list.OwnsBuffers;
            var localVertices = list.Vertices.AsSpan();
            var localIndices = list.Indices.AsSpan();
            var localCommands = list.Commands.AsSpan();
            var localRects = list.RectFilledPrimitives is null ? ReadOnlySpan<UiRectFilledPrimitive>.Empty : list.RectFilledPrimitives.AsSpan();
            var localCircles = list.CircleFilledPrimitives is null ? ReadOnlySpan<UiCircleFilledPrimitive>.Empty : list.CircleFilledPrimitives.AsSpan();
            var localStaticGeometryStamp = UiDrawList.ComputeStaticGeometryStamp(localVertices, localIndices, localRects, localCircles);
            var localCommandScheduleShapeStamp = UiDrawList.CommandScheduleShapeStampEnabled
                ? UiDrawList.ComputeCommandScheduleShapeStamp(localCommands)
                : 0UL;
            var localCommandTopologyStamp = UiDrawList.ComputeCommandTopologyStamp(localCommands);
            if (takeOwnership && previous is { LocalCommandSnapshot: null })
            {
                // The previous source was retired above. Submitted frames keep their lists,
                // command arrays and leases independently of this cache record.
                previous.OwnedSource = list;
                previous.LocalVertexList = list.Vertices;
                previous.LocalIndexList = list.Indices;
                previous.LocalRectFilledPrimitiveList = list.RectFilledPrimitives;
                previous.LocalCircleFilledPrimitiveList = list.CircleFilledPrimitives;
                previous.LocalScene3D = list.Scene3D;
                previous.CapturedParentScene3D = parentScene;
                previous.LocalStaticGeometryStamp = localStaticGeometryStamp;
                previous.LocalCommandScheduleShapeStamp = localCommandScheduleShapeStamp;
                previous.LocalCommandTopologyStamp = localCommandTopologyStamp;
                previous.ReplayCommands = null;
                previous.ReplayCommandList = null;
                previous.ReplayDrawList = null;
                previous.ScenePool = null;
                previous.ReplayValid = false;
                result[i] = previous;
                continue;
            }
            result[i] = new UiLayerCachedList
            {
                Lifetime = LayerCache.Lifetime,
                BaseStaticGeometryKey = previous?.BaseStaticGeometryKey ?? string.Create(
                    CultureInfo.InvariantCulture,
                    $"{StaticLayerGeometryTagPrefix}{layerId}:list:{i}"),
                LocalCommandSnapshot = takeOwnership ? null : localCommands.ToArray(),
                LocalScene3D = list.Scene3D,
                CapturedParentScene3D = parentScene,
                OwnedSource = takeOwnership ? list : null,
                LocalStaticGeometryStamp = localStaticGeometryStamp,
                LocalCommandScheduleShapeStamp = localCommandScheduleShapeStamp,
                LocalCommandTopologyStamp = localCommandTopologyStamp,
                LocalVertexList = takeOwnership ? list.Vertices : UiPooledList<UiDrawVertex>.FromArray(localVertices.ToArray()),
                LocalIndexList = takeOwnership ? list.Indices : UiPooledList<uint>.FromArray(localIndices.ToArray()),
                LocalRectFilledPrimitiveList = takeOwnership ? list.RectFilledPrimitives
                    : localRects.IsEmpty ? null : UiPooledList<UiRectFilledPrimitive>.FromArray(localRects.ToArray()),
                LocalCircleFilledPrimitiveList = takeOwnership ? list.CircleFilledPrimitives
                    : localCircles.IsEmpty ? null : UiPooledList<UiCircleFilledPrimitive>.FromArray(localCircles.ToArray()),
            };
        }

        return result;
    }

    private static bool TryRefreshLayerCachedList(UiLayerCachedList cached, UiDrawList list)
    {
        if (cached.LocalCommandSnapshot is not { } commandSnapshot || cached.Lifetime.HasReaders)
            return false;
        var localVertices = list.Vertices.AsSpan();
        var localIndices = list.Indices.AsSpan();
        var localCommands = list.Commands.AsSpan();
        var localRects = list.RectFilledPrimitives is null
            ? ReadOnlySpan<UiRectFilledPrimitive>.Empty
            : list.RectFilledPrimitives.AsSpan();
        var localCircles = list.CircleFilledPrimitives is null
            ? ReadOnlySpan<UiCircleFilledPrimitive>.Empty
            : list.CircleFilledPrimitives.AsSpan();

        if (cached.LocalVertexList.Count != localVertices.Length
            || cached.LocalIndexList.Count != localIndices.Length
            || commandSnapshot.Length != localCommands.Length
            || (cached.LocalRectFilledPrimitiveList?.Count ?? 0) != localRects.Length
            || (cached.LocalCircleFilledPrimitiveList?.Count ?? 0) != localCircles.Length)
        {
            return false;
        }

        if (!cached.LocalVertexList.TryOverwriteSameLength(localVertices)
            || !cached.LocalIndexList.TryOverwriteSameLength(localIndices)
            || !TryOverwriteLayerPrimitiveList(cached.LocalRectFilledPrimitiveList, localRects)
            || !TryOverwriteLayerPrimitiveList(cached.LocalCircleFilledPrimitiveList, localCircles))
        {
            return false;
        }

        var topologyStamp = UiDrawList.ComputeCommandTopologyStamp(localCommands);
        if (topologyStamp != cached.LocalCommandTopologyStamp)
        {
            // Backend command caches key immutable command-list identities.
            // A material/offset change needs a new wrapper, not new geometry.
            cached.ReplayCommandList = null;
        }
        cached.LocalCommandTopologyStamp = topologyStamp;
        localCommands.CopyTo(commandSnapshot);
        cached.LocalScene3D = list.Scene3D;
        cached.LocalStaticGeometryStamp = UiDrawList.ComputeStaticGeometryStamp(
            localVertices,
            localIndices,
            localRects,
            localCircles);
        cached.LocalCommandScheduleShapeStamp = UiDrawList.CommandScheduleShapeStampEnabled
            ? UiDrawList.ComputeCommandScheduleShapeStamp(localCommands)
            : 0UL;
        cached.ReplayValid = false;
        return true;
    }

    private static bool TryOverwriteLayerPrimitiveList<T>(UiPooledList<T>? cached, ReadOnlySpan<T> source)
    {
        if (source.Length == 0)
        {
            return cached is null || cached.Count == 0;
        }

        return cached is not null && cached.TryOverwriteSameLength(source);
    }

    private static bool HasRenderableLayerContent(ReadOnlySpan<UiDrawList> drawLists)
    {
        for (var i = 0; i < drawLists.Length; i++)
        {
            var commands = drawLists[i].Commands;
            for (var j = 0; j < commands.Count; j++)
            {
                ref readonly var cmd = ref commands.ItemRef(j);
                if (cmd.Callback is not null)
                    return true;
                if (cmd.ElementCount == 0)
                {
                    continue;
                }

                if (cmd.ClipRect.Width > 0f && cmd.ClipRect.Height > 0f)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool HasLayerClipArea(UiRect rect)
    {
        return rect.Width > 0f && rect.Height > 0f;
    }

    private static UiRect CreateStaticLayerCaptureClip()
    {
        return new UiRect(
            -StaticLayerCaptureClipExtent,
            -StaticLayerCaptureClipExtent,
            StaticLayerCaptureClipExtent * 2f,
            StaticLayerCaptureClipExtent * 2f);
    }

    private static void EnsureReplay(UiLayerCachedList cached, float opacity, UiVector2 translation, UiRect layerLocalClip)
    {
        var clampedOpacity = Math.Clamp(opacity, 0f, 1f);
        if (cached.ReplayValid
            && MathF.Abs(cached.ReplayTranslation.X - translation.X) <= 0.0001f
            && MathF.Abs(cached.ReplayTranslation.Y - translation.Y) <= 0.0001f
            && MathF.Abs(cached.ReplayOpacity - clampedOpacity) <= 0.0001f
            && MathF.Abs(cached.ReplayLocalClip.X - layerLocalClip.X) <= 0.0001f
            && MathF.Abs(cached.ReplayLocalClip.Y - layerLocalClip.Y) <= 0.0001f
            && MathF.Abs(cached.ReplayLocalClip.Width - layerLocalClip.Width) <= 0.0001f
            && MathF.Abs(cached.ReplayLocalClip.Height - layerLocalClip.Height) <= 0.0001f)
        {
            return;
        }

        var applyOpacity = clampedOpacity < 0.999f;
        var applyTranslation = MathF.Abs(translation.X) > 0.0001f || MathF.Abs(translation.Y) > 0.0001f;
        var replayVertexList = cached.LocalVertexList;
        var replayRectFilledPrimitiveList = cached.LocalRectFilledPrimitiveList;
        var replayCircleFilledPrimitiveList = cached.LocalCircleFilledPrimitiveList;
        var replayStaticGeometryStamp = cached.LocalStaticGeometryStamp;

        var localCommands = cached.LocalCommands;
        var replayCommands = cached.ReplayCommands;
        if (replayCommands is null || replayCommands.Length != localCommands.Length || cached.Lifetime.HasReaders)
        {
            replayCommands = new UiDrawCommand[localCommands.Length];
            cached.ReplayCommands = replayCommands;
            cached.ReplayCommandList = UiPooledList<UiDrawCommand>.FromArray(replayCommands);
        }
        else if (cached.ReplayCommandList is null)
        {
            cached.ReplayCommandList = UiPooledList<UiDrawCommand>.FromArray(replayCommands);
        }

        for (var i = 0; i < localCommands.Length; i++)
        {
            var localCommand = localCommands[i];
            var localClip = localCommand.ClipRect;
            var commandLayerClip = layerLocalClip with
            {
                X = layerLocalClip.X - localCommand.Translation.X,
                Y = layerLocalClip.Y - localCommand.Translation.Y
            };

            var clipMinX = MathF.Max(localClip.X, commandLayerClip.X);
            var clipMinY = MathF.Max(localClip.Y, commandLayerClip.Y);
            var clipMaxX = MathF.Min(localClip.X + localClip.Width, commandLayerClip.X + commandLayerClip.Width);
            var clipMaxY = MathF.Min(localClip.Y + localClip.Height, commandLayerClip.Y + commandLayerClip.Height);

            if (clipMaxX < clipMinX)
            {
                clipMaxX = clipMinX;
            }

            if (clipMaxY < clipMinY)
            {
                clipMaxY = clipMinY;
            }

            var clipWidth = clipMaxX - clipMinX;
            var clipHeight = clipMaxY - clipMinY;

            var replayCommand = localCommand;
            replayCommand = replayCommand with
            {
                ClipRect = new UiRect(clipMinX, clipMinY, clipWidth, clipHeight),
                Translation = applyTranslation ? new(localCommand.Translation.X + translation.X, localCommand.Translation.Y + translation.Y) : localCommand.Translation,
                Opacity = applyOpacity ? clampedOpacity * localCommand.Opacity : localCommand.Opacity
            };

            replayCommands[i] = replayCommand with
            {
                UserData = null
            };
        }

        var key = cached.BaseStaticGeometryKey;
        var replayCommandScheduleStamp = cached.LocalCommandScheduleShapeStamp;
        var commandStateStamp = UiDrawList.ComputeCommandStateStamp(replayCommands);

        if (cached.ReplayDrawList is null
            || !ReferenceEquals(cached.ReplayDrawList.Vertices, replayVertexList)
            || !ReferenceEquals(cached.ReplayDrawList.Indices, cached.LocalIndexList)
            || !ReferenceEquals(cached.ReplayDrawList.Commands, cached.ReplayCommandList)
            || !ReferenceEquals(cached.ReplayDrawList.RectFilledPrimitives, replayRectFilledPrimitiveList)
            || !ReferenceEquals(cached.ReplayDrawList.CircleFilledPrimitives, replayCircleFilledPrimitiveList)
            || cached.ReplayDrawList.StaticGeometryStamp != replayStaticGeometryStamp
            || cached.ReplayDrawList.CommandScheduleStamp != replayCommandScheduleStamp
            || cached.ReplayDrawList.CommandStateStamp != commandStateStamp
            || !string.Equals(cached.ReplayDrawList.StaticGeometryKey, key, StringComparison.Ordinal))
        {
            cached.ReplayDrawList = new UiDrawList(
                replayVertexList,
                cached.LocalIndexList,
                cached.ReplayCommandList,
                OwnsBuffers: false,
                StaticGeometryKey: key,
                RectFilledPrimitives: replayRectFilledPrimitiveList,
                CircleFilledPrimitives: replayCircleFilledPrimitiveList,
                StaticGeometryStamp: replayStaticGeometryStamp,
                CommandScheduleStamp: replayCommandScheduleStamp)
            {
                Scene3D = cached.LocalScene3D,
                CommandStateStamp = commandStateStamp
            };
        }
        cached.ReplayOpacity = clampedOpacity;
        cached.ReplayTranslation = translation;
        cached.ReplayLocalClip = layerLocalClip;
        cached.ReplayValid = true;
    }

    private static void ReleaseLayerDrawLists(ReadOnlySpan<UiDrawList> drawLists)
    {
        for (var i = 0; i < drawLists.Length; i++)
        {
            drawLists[i].ReleasePooled();
        }
    }

}
