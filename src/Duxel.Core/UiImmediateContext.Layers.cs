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
        public required UiDrawVertex[] LocalVertices { get; init; }
        public required uint[] LocalIndices { get; init; }
        public required UiDrawCommand[] LocalCommands { get; init; }
        public UiRectFilledPrimitive[] LocalRectFilledPrimitives { get; init; } = [];
        public UiCircleFilledPrimitive[] LocalCircleFilledPrimitives { get; init; } = [];
        public required UiPooledList<UiDrawVertex> LocalVertexList { get; init; }
        public required UiPooledList<uint> LocalIndexList { get; init; }
        public UiPooledList<UiRectFilledPrimitive>? LocalRectFilledPrimitiveList { get; init; }
        public UiPooledList<UiCircleFilledPrimitive>? LocalCircleFilledPrimitiveList { get; init; }

        public UiDrawVertex[]? ReplayVertices;
        public UiRectFilledPrimitive[]? ReplayRectFilledPrimitives;
        public UiCircleFilledPrimitive[]? ReplayCircleFilledPrimitives;
        public UiDrawCommand[]? ReplayCommands;
        public UiPooledList<UiDrawVertex>? ReplayVertexList;
        public UiPooledList<UiRectFilledPrimitive>? ReplayRectFilledPrimitiveList;
        public UiPooledList<UiCircleFilledPrimitive>? ReplayCircleFilledPrimitiveList;
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
        public int Version;
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
        layerBuilder.PushTexture(_fontTexture);

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
                unchecked
                {
                    entry.Version++;
                }

                entry.Lists = CaptureLayerLists(frame.LayerId, entry.Version, drawLists);
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

    private static UiLayerCachedList[] CaptureLayerLists(string layerId, int version, UiDrawList[] drawLists)
    {
        var result = new UiLayerCachedList[drawLists.Length];
        for (var i = 0; i < drawLists.Length; i++)
        {
            var list = drawLists[i];
            var localVertices = list.Vertices.AsSpan().ToArray();
            var localIndices = list.Indices.AsSpan().ToArray();
            var localCommands = list.Commands.AsSpan().ToArray();
            var localRects = list.RectFilledPrimitives?.AsSpan().ToArray() ?? [];
            var localCircles = list.CircleFilledPrimitives?.AsSpan().ToArray() ?? [];
            result[i] = new UiLayerCachedList
            {
                BaseStaticGeometryKey = string.Create(
                    CultureInfo.InvariantCulture,
                    $"{StaticLayerGeometryTagPrefix}{layerId}:v:{version}:list:{i}"),
                LocalVertices = localVertices,
                LocalIndices = localIndices,
                LocalCommands = localCommands,
                LocalRectFilledPrimitives = localRects,
                LocalCircleFilledPrimitives = localCircles,
                LocalVertexList = UiPooledList<UiDrawVertex>.FromArray(localVertices),
                LocalIndexList = UiPooledList<uint>.FromArray(localIndices),
                LocalRectFilledPrimitiveList = localRects.Length == 0 ? null : UiPooledList<UiRectFilledPrimitive>.FromArray(localRects),
                LocalCircleFilledPrimitiveList = localCircles.Length == 0 ? null : UiPooledList<UiCircleFilledPrimitive>.FromArray(localCircles),
            };
        }

        return result;
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

        var localVertices = cached.LocalVertices;
        var applyAlpha = clampedOpacity < 0.999f;
        var applyTranslation = MathF.Abs(translation.X) > 0.0001f || MathF.Abs(translation.Y) > 0.0001f;
        var replayVertices = localVertices;
        var replayVertexList = cached.LocalVertexList;
        var replayRectFilledPrimitives = cached.LocalRectFilledPrimitives;
        var replayRectFilledPrimitiveList = cached.LocalRectFilledPrimitiveList;
        var replayCircleFilledPrimitives = cached.LocalCircleFilledPrimitives;
        var replayCircleFilledPrimitiveList = cached.LocalCircleFilledPrimitiveList;
        if (applyAlpha)
        {
            replayVertices = cached.ReplayVertices;
            if (replayVertices is null || replayVertices.Length != localVertices.Length)
            {
                replayVertices = new UiDrawVertex[localVertices.Length];
                cached.ReplayVertices = replayVertices;
                cached.ReplayVertexList = UiPooledList<UiDrawVertex>.FromArray(replayVertices);
            }
            else if (cached.ReplayVertexList is null)
            {
                cached.ReplayVertexList = UiPooledList<UiDrawVertex>.FromArray(replayVertices);
            }
            replayVertexList = cached.ReplayVertexList;

            for (var i = 0; i < localVertices.Length; i++)
            {
                var vertex = localVertices[i];
                var rgba = vertex.Color.Rgba;
                var alpha = (uint)((rgba >> 24) & 0xFFu);
                var nextAlpha = (uint)Math.Clamp((int)MathF.Round(alpha * clampedOpacity), 0, 255);
                replayVertices[i] = vertex with { Color = new UiColor((rgba & 0x00FFFFFFu) | (nextAlpha << 24)) };
            }

            var localRects = cached.LocalRectFilledPrimitives;
            replayRectFilledPrimitives = cached.ReplayRectFilledPrimitives;
            if (replayRectFilledPrimitives is null || replayRectFilledPrimitives.Length != localRects.Length)
            {
                replayRectFilledPrimitives = new UiRectFilledPrimitive[localRects.Length];
                cached.ReplayRectFilledPrimitives = replayRectFilledPrimitives;
                cached.ReplayRectFilledPrimitiveList = replayRectFilledPrimitives.Length == 0
                    ? null
                    : UiPooledList<UiRectFilledPrimitive>.FromArray(replayRectFilledPrimitives);
            }
            else if (cached.ReplayRectFilledPrimitiveList is null && replayRectFilledPrimitives.Length > 0)
            {
                cached.ReplayRectFilledPrimitiveList = UiPooledList<UiRectFilledPrimitive>.FromArray(replayRectFilledPrimitives);
            }
            replayRectFilledPrimitiveList = cached.ReplayRectFilledPrimitiveList;

            for (var i = 0; i < localRects.Length; i++)
            {
                var primitive = localRects[i];
                var rgba = primitive.Color.Rgba;
                var alpha = (uint)((rgba >> 24) & 0xFFu);
                var nextAlpha = (uint)Math.Clamp((int)MathF.Round(alpha * clampedOpacity), 0, 255);
                replayRectFilledPrimitives[i] = primitive with { Color = new UiColor((rgba & 0x00FFFFFFu) | (nextAlpha << 24)) };
            }

            var localCircles = cached.LocalCircleFilledPrimitives;
            replayCircleFilledPrimitives = cached.ReplayCircleFilledPrimitives;
            if (replayCircleFilledPrimitives is null || replayCircleFilledPrimitives.Length != localCircles.Length)
            {
                replayCircleFilledPrimitives = new UiCircleFilledPrimitive[localCircles.Length];
                cached.ReplayCircleFilledPrimitives = replayCircleFilledPrimitives;
                cached.ReplayCircleFilledPrimitiveList = replayCircleFilledPrimitives.Length == 0
                    ? null
                    : UiPooledList<UiCircleFilledPrimitive>.FromArray(replayCircleFilledPrimitives);
            }
            else if (cached.ReplayCircleFilledPrimitiveList is null && replayCircleFilledPrimitives.Length > 0)
            {
                cached.ReplayCircleFilledPrimitiveList = UiPooledList<UiCircleFilledPrimitive>.FromArray(replayCircleFilledPrimitives);
            }
            replayCircleFilledPrimitiveList = cached.ReplayCircleFilledPrimitiveList;

            for (var i = 0; i < localCircles.Length; i++)
            {
                var primitive = localCircles[i];
                var rgba = primitive.Color.Rgba;
                var alpha = (uint)((rgba >> 24) & 0xFFu);
                var nextAlpha = (uint)Math.Clamp((int)MathF.Round(alpha * clampedOpacity), 0, 255);
                replayCircleFilledPrimitives[i] = primitive with { Color = new UiColor((rgba & 0x00FFFFFFu) | (nextAlpha << 24)) };
            }
        }

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
                Translation = applyTranslation ? translation : default
            };

            replayCommands[i] = replayCommand with { UserData = null };
        }

        var key = string.Create(
            CultureInfo.InvariantCulture,
            $"{cached.BaseStaticGeometryKey}:op:{QuantizeLayerCacheValue(clampedOpacity)}");

        if (cached.ReplayDrawList is null
            || !ReferenceEquals(cached.ReplayDrawList.Vertices, replayVertexList)
            || !ReferenceEquals(cached.ReplayDrawList.Indices, cached.LocalIndexList)
            || !ReferenceEquals(cached.ReplayDrawList.Commands, cached.ReplayCommandList)
            || !ReferenceEquals(cached.ReplayDrawList.RectFilledPrimitives, replayRectFilledPrimitiveList)
            || !ReferenceEquals(cached.ReplayDrawList.CircleFilledPrimitives, replayCircleFilledPrimitiveList)
            || !string.Equals(cached.ReplayDrawList.StaticGeometryKey, key, StringComparison.Ordinal))
        {
            cached.ReplayDrawList = new UiDrawList(
                replayVertexList,
                cached.LocalIndexList,
                cached.ReplayCommandList,
                OwnsBuffers: false,
                StaticGeometryKey: key,
                RectFilledPrimitives: replayRectFilledPrimitiveList,
                CircleFilledPrimitives: replayCircleFilledPrimitiveList);
        }
        cached.ReplayOpacity = clampedOpacity;
        cached.ReplayTranslation = translation;
        cached.ReplayLocalClip = layerLocalClip;
        cached.ReplayValid = true;
    }

    private static int QuantizeLayerCacheValue(float value)
    {
        return (int)MathF.Round(value * 100f);
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
