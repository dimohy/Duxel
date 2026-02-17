using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    private const string StaticLayerGeometryTagPrefix = "duxel.layer.static:";
    private const string DrawListBackendTagMarker = ":cbd";
    private const string TextureBackendTagMarker = ":cbt";

    private sealed class UiLayerCachedList
    {
        public required UiDrawVertex[] LocalVertices { get; init; }
        public required uint[] LocalIndices { get; init; }
        public required UiDrawCommand[] LocalCommands { get; init; }
        public required string StaticGeometryTag { get; init; }
        public required UiLayerCacheBackend CacheBackend { get; init; }

        public UiDrawVertex[]? ReplayVertices;
        public UiDrawCommand[]? ReplayCommands;
        public UiVector2 ReplayTranslation;
        public float ReplayOpacity = -1f;
        public bool ReplayValid;
    }

    private sealed class UiLayerCacheEntry
    {
        public UiLayerCachedList[]? Lists;
        public bool Dirty = true;
        public UiLayerCacheBackend CacheBackend = UiLayerCacheBackend.DrawList;
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

        if (entry.CacheBackend != nextOptions.CacheBackend)
        {
            entry.CacheBackend = nextOptions.CacheBackend;
            entry.Dirty = true;
        }

        var parentClip = CurrentClipRect;
        var layerLocalClip = new UiRect(
            parentClip.X - nextOptions.Translation.X,
            parentClip.Y - nextOptions.Translation.Y,
            parentClip.Width,
            parentClip.Height);

        if (nextOptions.StaticCache && !entry.Dirty && entry.Lists is { Length: > 0 })
        {
            _layerFrames.Push(new UiLayerFrame(layerId, ShouldDraw: false, nextOptions, LayerBuilder: null, LocalClipPushed: false, LocalClip: layerLocalClip));
            return false;
        }

        var layerBuilder = new UiDrawListBuilder(layerLocalClip);
        layerBuilder._SetDrawListSharedData(GetDrawListSharedData());
        layerBuilder.PushTexture(_fontTexture);

        _builderStack.Push(_builder);
        _builder = layerBuilder;
        _clipStack.Push(layerLocalClip);
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
            if (entry.Lists is { Length: > 0 })
            {
                for (var i = 0; i < entry.Lists.Length; i++)
                {
                    _builder.FlushCurrentDrawList();
                    var cached = entry.Lists[i];
                    EnsureReplay(cached, frame.Options.Opacity, frame.Options.Translation, frame.LocalClip);
                    _builder.Append(cached.ReplayVertices!, cached.LocalIndices, cached.ReplayCommands!);
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
                entry.Lists = CaptureLayerLists(frame.LayerId, drawLists, frame.Options.CacheBackend);
                entry.Dirty = false;
                ReleaseLayerDrawLists(drawLists);

                if (entry.Lists is { Length: > 0 })
                {
                    for (var i = 0; i < entry.Lists.Length; i++)
                    {
                        _builder.FlushCurrentDrawList();
                        var cached = entry.Lists[i];
                        EnsureReplay(cached, frame.Options.Opacity, frame.Options.Translation, frame.LocalClip);
                        _builder.Append(cached.ReplayVertices!, cached.LocalIndices, cached.ReplayCommands!);
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

    private static UiLayerCachedList[] CaptureLayerLists(string layerId, UiDrawList[] drawLists, UiLayerCacheBackend cacheBackend)
    {
        var result = new UiLayerCachedList[drawLists.Length];
        for (var i = 0; i < drawLists.Length; i++)
        {
            var list = drawLists[i];
            var staticTag = BuildStaticGeometryTag(layerId, i, list, cacheBackend);
            result[i] = new UiLayerCachedList
            {
                LocalVertices = list.Vertices.AsSpan().ToArray(),
                LocalIndices = list.Indices.AsSpan().ToArray(),
                LocalCommands = list.Commands.AsSpan().ToArray(),
                StaticGeometryTag = staticTag,
                CacheBackend = cacheBackend,
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

    private static string BuildStaticGeometryTag(string layerId, int listIndex, UiDrawList drawList, UiLayerCacheBackend cacheBackend)
    {
        var hash = 1469598103934665603UL;

        static void HashBytes(ref ulong hash, ReadOnlySpan<byte> bytes)
        {
            unchecked
            {
                for (var i = 0; i < bytes.Length; i++)
                {
                    hash ^= bytes[i];
                    hash *= 1099511628211UL;
                }
            }
        }

        static void HashUInt32(ref ulong hash, uint value)
        {
            Span<byte> bytes = stackalloc byte[sizeof(uint)];
            MemoryMarshal.Write(bytes, in value);
            HashBytes(ref hash, bytes);
        }

        static void HashInt32(ref ulong hash, int value)
        {
            Span<byte> bytes = stackalloc byte[sizeof(int)];
            MemoryMarshal.Write(bytes, in value);
            HashBytes(ref hash, bytes);
        }

        static void HashUInt64(ref ulong hash, ulong value)
        {
            Span<byte> bytes = stackalloc byte[sizeof(ulong)];
            MemoryMarshal.Write(bytes, in value);
            HashBytes(ref hash, bytes);
        }

        static void HashNUInt(ref ulong hash, nuint value)
        {
            if (IntPtr.Size == sizeof(ulong))
            {
                HashUInt64(ref hash, (ulong)value);
                return;
            }

            HashUInt32(ref hash, (uint)value);
        }

        HashInt32(ref hash, listIndex);

        var vertices = drawList.Vertices.AsSpan();
        HashInt32(ref hash, vertices.Length);
        HashBytes(ref hash, MemoryMarshal.AsBytes(vertices));

        var indices = drawList.Indices.AsSpan();
        HashInt32(ref hash, indices.Length);
        HashBytes(ref hash, MemoryMarshal.AsBytes(indices));

        var commands = drawList.Commands.AsSpan();
        HashInt32(ref hash, commands.Length);
        for (var i = 0; i < commands.Length; i++)
        {
            var cmd = commands[i];
            HashNUInt(ref hash, cmd.TextureId.Value);
            HashUInt32(ref hash, cmd.IndexOffset);
            HashUInt32(ref hash, cmd.ElementCount);
            HashUInt32(ref hash, cmd.VertexOffset);
            HashInt32(ref hash, BitConverter.SingleToInt32Bits(cmd.ClipRect.X));
            HashInt32(ref hash, BitConverter.SingleToInt32Bits(cmd.ClipRect.Y));
            HashInt32(ref hash, BitConverter.SingleToInt32Bits(cmd.ClipRect.Width));
            HashInt32(ref hash, BitConverter.SingleToInt32Bits(cmd.ClipRect.Height));
            HashInt32(ref hash, BitConverter.SingleToInt32Bits(cmd.Translation.X));
            HashInt32(ref hash, BitConverter.SingleToInt32Bits(cmd.Translation.Y));
            HashInt32(ref hash, cmd.Callback is null ? 0 : 1);
        }

        var backendMarker = cacheBackend == UiLayerCacheBackend.Texture
            ? TextureBackendTagMarker
            : DrawListBackendTagMarker;
        return $"{StaticLayerGeometryTagPrefix}{layerId}:{listIndex}:{hash:X16}{backendMarker}";
    }

    private static void EnsureReplay(UiLayerCachedList cached, float opacity, UiVector2 translation, UiRect layerLocalClip)
    {
        var clampedOpacity = Math.Clamp(opacity, 0f, 1f);
        var opacityBits = BitConverter.SingleToInt32Bits(clampedOpacity);
        var replayStaticTag = clampedOpacity >= 0.999f
            ? cached.StaticGeometryTag
            : $"{cached.StaticGeometryTag}:o{opacityBits:X8}";
        object? replayUserData = cached.CacheBackend == UiLayerCacheBackend.Texture
            ? replayStaticTag
            : null;
        if (cached.ReplayValid
            && MathF.Abs(cached.ReplayTranslation.X - translation.X) <= 0.0001f
            && MathF.Abs(cached.ReplayTranslation.Y - translation.Y) <= 0.0001f
            && MathF.Abs(cached.ReplayOpacity - clampedOpacity) <= 0.0001f)
        {
            return;
        }

        var localVertices = cached.LocalVertices;
        var replayVertices = cached.ReplayVertices;
        if (replayVertices is null || replayVertices.Length != localVertices.Length)
        {
            replayVertices = new UiDrawVertex[localVertices.Length];
            cached.ReplayVertices = replayVertices;
        }

        var applyAlpha = clampedOpacity < 0.999f;
        var applyTranslation = MathF.Abs(translation.X) > 0.0001f || MathF.Abs(translation.Y) > 0.0001f;
        for (var i = 0; i < localVertices.Length; i++)
        {
            var vertex = localVertices[i];

            if (applyTranslation)
            {
                vertex = vertex with
                {
                    Position = new UiVector2(vertex.Position.X + translation.X, vertex.Position.Y + translation.Y)
                };
            }

            if (applyAlpha)
            {
                var rgba = vertex.Color.Rgba;
                var alpha = (uint)((rgba >> 24) & 0xFFu);
                var nextAlpha = (uint)Math.Clamp((int)MathF.Round(alpha * clampedOpacity), 0, 255);
                vertex = vertex with { Color = new UiColor((rgba & 0x00FFFFFFu) | (nextAlpha << 24)) };
            }

            replayVertices[i] = vertex;
        }

        var localCommands = cached.LocalCommands;
        var replayCommands = cached.ReplayCommands;
        if (replayCommands is null || replayCommands.Length != localCommands.Length)
        {
            replayCommands = new UiDrawCommand[localCommands.Length];
            cached.ReplayCommands = replayCommands;
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
            if (applyTranslation)
            {
                replayCommand = replayCommand with
                {
                    ClipRect = new UiRect(clipMinX + translation.X, clipMinY + translation.Y, clipWidth, clipHeight)
                };
            }
            else
            {
                replayCommand = replayCommand with
                {
                    ClipRect = new UiRect(clipMinX, clipMinY, clipWidth, clipHeight)
                };
            }

            replayCommands[i] = replayCommand with
            {
                UserData = replayUserData
            };
        }

        cached.ReplayOpacity = clampedOpacity;
        cached.ReplayTranslation = translation;
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
