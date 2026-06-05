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
        public required string BaseStaticGeometryKey { get; init; }
        public required UiDrawCommand[] LocalCommands { get; init; }
        public required ulong LocalStaticGeometryStamp { get; set; }
        public required ulong LocalCommandScheduleShapeStamp { get; set; }
        public required UiPooledList<UiDrawVertex> LocalVertexList { get; init; }
        public required UiPooledList<uint> LocalIndexList { get; init; }
        public UiPooledList<UiRectFilledPrimitive>? LocalRectFilledPrimitiveList { get; init; }
        public UiPooledList<UiCircleFilledPrimitive>? LocalCircleFilledPrimitiveList { get; init; }

        public UiDrawCommand[]? ReplayCommands;
        public UiPooledList<UiDrawCommand>? ReplayCommandList;
        public UiDrawList? ReplayDrawList;
        public UiVector2 ReplayTranslation;
        public UiRect ReplayLocalClip;
        public float ReplayOpacity = -1f;
        public bool ReplayValid;
    }

    private sealed class UiLayerCacheEntry
    {
        public UiLayerCachedList[]? Lists;
        public bool Dirty = true;
    }

    private readonly record struct UiLayerFrame(string LayerId, bool ShouldDraw, UiLayerOptions Options, UiDrawListBuilder? LayerBuilder, bool LocalClipPushed, UiRect LocalClip);

    private sealed class UiLayerCacheStore
    {
        public Dictionary<string, UiLayerCacheEntry> Layers { get; } = new(StringComparer.Ordinal);
    }

    private static readonly ConditionalWeakTable<UiState, UiLayerCacheStore> LayerCacheStores = new();
    private readonly Stack<UiLayerFrame> _layerFrames = new();

    private Dictionary<string, UiLayerCacheEntry> LayerCaches => LayerCacheStores.GetOrCreateValue(_state).Layers;

    public bool BeginLayer(string layerId, bool staticCache = false, float opacity = 1f, UiVector2 translation = default)
        => BeginLayer(layerId, new UiLayerOptions(staticCache, opacity, translation));

    public bool BeginLayer(string layerId, UiLayerOptions options)
    {
        layerId ??= string.Empty;
        if (layerId.Length == 0)
        {
            throw new ArgumentException("Layer id must not be empty.", nameof(layerId));
        }

        var nextOpacity = Math.Clamp(options.Opacity, 0f, 1f);
        var nextOptions = options with { Opacity = nextOpacity };
        var layerCaches = LayerCaches;
        if (!layerCaches.TryGetValue(layerId, out var entry))
        {
            entry = new UiLayerCacheEntry();
            layerCaches[layerId] = entry;
        }

        var parentClip = CurrentClipRect;
        var layerLocalClip = new UiRect(
            parentClip.X - nextOptions.Translation.X,
            parentClip.Y - nextOptions.Translation.Y,
            parentClip.Width,
            parentClip.Height);

        if (nextOptions.StaticCache && !HasLayerClipArea(layerLocalClip))
        {
            _layerFrames.Push(new UiLayerFrame(layerId, ShouldDraw: false, nextOptions, LayerBuilder: null, LocalClipPushed: false, LocalClip: layerLocalClip));
            return false;
        }

        if (nextOptions.StaticCache && !entry.Dirty && entry.Lists is { Length: > 0 })
        {
            _layerFrames.Push(new UiLayerFrame(layerId, ShouldDraw: false, nextOptions, LayerBuilder: null, LocalClipPushed: false, LocalClip: layerLocalClip));
            return false;
        }

        var captureLocalClip = nextOptions.StaticCache ? CreateStaticLayerCaptureClip() : layerLocalClip;
        var layerBuilder = new UiDrawListBuilder(captureLocalClip);
        layerBuilder._SetDrawListSharedData(GetDrawListSharedData());
        layerBuilder.PushTexture(_whiteTexture);

        _builderStack.Push(_builder);
        _builder = layerBuilder;
        _clipStack.Push(captureLocalClip);
        _layerFrames.Push(new UiLayerFrame(layerId, ShouldDraw: true, nextOptions, layerBuilder, LocalClipPushed: true, LocalClip: layerLocalClip));
        return true;
    }

    public void EndLayer()
    {
        if (_layerFrames.Count == 0)
        {
            return;
        }

        var frame = _layerFrames.Pop();
        var layerCaches = LayerCaches;
        if (!layerCaches.TryGetValue(frame.LayerId, out var entry))
        {
            return;
        }

        if (!frame.ShouldDraw)
        {
            if (!HasLayerClipArea(frame.LocalClip))
            {
                return;
            }

            if (entry.Lists is { Length: > 0 })
            {
                for (var i = 0; i < entry.Lists.Length; i++)
                {
                    _builder.FlushCurrentDrawList();
                    var cached = entry.Lists[i];
                    EnsureReplay(cached, frame.Options.Opacity, frame.Options.Translation, frame.LocalClip);
                    _builder.AppendRetainedStaticReference(cached.ReplayDrawList!);
                    _builder.FlushCurrentDrawList();
                }
            }

            return;
        }

        var layerBuilder = frame.LayerBuilder;
        if (layerBuilder is null)
        {
            if (frame.LocalClipPushed && _clipStack.Count > 0)
            {
                _clipStack.Pop();
            }

            return;
        }

        var built = layerBuilder.Build();
        var drawLists = new UiDrawList[built.Count];
        built.CopyTo(drawLists);
        built.Return();

        if (frame.LocalClipPushed && _clipStack.Count > 0)
        {
            _clipStack.Pop();
        }

        _builder = _builderStack.Pop();

        if (frame.Options.StaticCache)
        {
            if (HasRenderableLayerContent(drawLists))
            {
                entry.Lists = CaptureLayerLists(frame.LayerId, drawLists, entry.Lists);
                entry.Dirty = false;
                ReleaseLayerDrawLists(drawLists);

                if (entry.Lists is { Length: > 0 })
                {
                    for (var i = 0; i < entry.Lists.Length; i++)
                    {
                        _builder.FlushCurrentDrawList();
                        var cached = entry.Lists[i];
                        EnsureReplay(cached, frame.Options.Opacity, frame.Options.Translation, frame.LocalClip);
                        _builder.AppendRetainedStaticReference(cached.ReplayDrawList!);
                        _builder.FlushCurrentDrawList();
                    }
                }
            }
            else
            {
                ReleaseLayerDrawLists(drawLists);
                entry.Dirty = true;
            }
        }
        else
        {
            for (var i = 0; i < drawLists.Length; i++)
            {
                _builder.Append(drawLists[i], frame.Options.Opacity, frame.Options.Translation);
            }

            ReleaseLayerDrawLists(drawLists);
            entry.Lists = null;
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

    public void MarkAllLayersDirty()
    {
        foreach (var pair in LayerCaches)
        {
            pair.Value.Dirty = true;
        }
    }

    private static UiLayerCachedList[] CaptureLayerLists(
        string layerId,
        UiDrawList[] drawLists,
        UiLayerCachedList[]? previousLists)
    {
        var result = previousLists is not null && previousLists.Length == drawLists.Length
            ? previousLists
            : new UiLayerCachedList[drawLists.Length];
        for (var i = 0; i < drawLists.Length; i++)
        {
            var list = drawLists[i];
            var previous = previousLists is not null && i < previousLists.Length ? previousLists[i] : null;
            if (previous is not null && TryRefreshLayerCachedList(previous, list))
            {
                result[i] = previous;
                continue;
            }

            var localVertices = list.Vertices.AsSpan().ToArray();
            var localIndices = list.Indices.AsSpan().ToArray();
            var localCommands = list.Commands.AsSpan().ToArray();
            var localRects = list.RectFilledPrimitives?.AsSpan().ToArray() ?? [];
            var localCircles = list.CircleFilledPrimitives?.AsSpan().ToArray() ?? [];
            var localStaticGeometryStamp = UiDrawList.ComputeStaticGeometryStamp(localVertices, localIndices, localRects, localCircles);
            var localCommandScheduleShapeStamp = UiDrawList.CommandScheduleShapeStampEnabled
                ? UiDrawList.ComputeCommandScheduleShapeStamp(localCommands)
                : 0UL;
            result[i] = new UiLayerCachedList
            {
                BaseStaticGeometryKey = string.Create(
                    CultureInfo.InvariantCulture,
                    $"{StaticLayerGeometryTagPrefix}{layerId}:list:{i}"),
                LocalCommands = localCommands,
                LocalStaticGeometryStamp = localStaticGeometryStamp,
                LocalCommandScheduleShapeStamp = localCommandScheduleShapeStamp,
                LocalVertexList = UiPooledList<UiDrawVertex>.FromArray(localVertices),
                LocalIndexList = UiPooledList<uint>.FromArray(localIndices),
                LocalRectFilledPrimitiveList = localRects.Length == 0 ? null : UiPooledList<UiRectFilledPrimitive>.FromArray(localRects),
                LocalCircleFilledPrimitiveList = localCircles.Length == 0 ? null : UiPooledList<UiCircleFilledPrimitive>.FromArray(localCircles),
            };
        }

        return result;
    }

    private static bool TryRefreshLayerCachedList(UiLayerCachedList cached, UiDrawList list)
    {
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
            || cached.LocalCommands.Length != localCommands.Length
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

        localCommands.CopyTo(cached.LocalCommands);
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

    private static bool HasRenderableLayerContent(UiDrawList[] drawLists)
    {
        for (var i = 0; i < drawLists.Length; i++)
        {
            var commands = drawLists[i].Commands;
            for (var j = 0; j < commands.Count; j++)
            {
                ref readonly var cmd = ref commands.ItemRef(j);
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
        if (replayCommands is null || replayCommands.Length != localCommands.Length)
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

            var clipMinX = MathF.Max(localClip.X, layerLocalClip.X);
            var clipMinY = MathF.Max(localClip.Y, layerLocalClip.Y);
            var clipMaxX = MathF.Min(localClip.X + localClip.Width, layerLocalClip.X + layerLocalClip.Width);
            var clipMaxY = MathF.Min(localClip.Y + localClip.Height, layerLocalClip.Y + layerLocalClip.Height);

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
                Translation = applyTranslation ? translation : default,
                Opacity = applyOpacity ? clampedOpacity * localCommand.Opacity : localCommand.Opacity
            };

            replayCommands[i] = replayCommand with { UserData = null };
        }

        var key = cached.BaseStaticGeometryKey;
        var replayCommandScheduleStamp = cached.LocalCommandScheduleShapeStamp;

        if (cached.ReplayDrawList is null
            || !ReferenceEquals(cached.ReplayDrawList.Vertices, replayVertexList)
            || !ReferenceEquals(cached.ReplayDrawList.Indices, cached.LocalIndexList)
            || !ReferenceEquals(cached.ReplayDrawList.Commands, cached.ReplayCommandList)
            || !ReferenceEquals(cached.ReplayDrawList.RectFilledPrimitives, replayRectFilledPrimitiveList)
            || !ReferenceEquals(cached.ReplayDrawList.CircleFilledPrimitives, replayCircleFilledPrimitiveList)
            || cached.ReplayDrawList.StaticGeometryStamp != replayStaticGeometryStamp
            || cached.ReplayDrawList.CommandScheduleStamp != replayCommandScheduleStamp
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
                CommandScheduleStamp: replayCommandScheduleStamp);
        }
        cached.ReplayOpacity = clampedOpacity;
        cached.ReplayTranslation = translation;
        cached.ReplayLocalClip = layerLocalClip;
        cached.ReplayValid = true;
    }

    private static void ReleaseLayerDrawLists(UiDrawList[]? drawLists)
    {
        if (drawLists is null)
        {
            return;
        }

        for (var i = 0; i < drawLists.Length; i++)
        {
            drawLists[i].ReleasePooled();
        }
    }

}
