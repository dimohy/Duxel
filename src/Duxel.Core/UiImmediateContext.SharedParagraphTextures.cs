namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    private const int SharedParagraphTextureLimit = 768;
    private const int SharedParagraphTextureByteLimit = 8 * 1024 * 1024;

    private readonly record struct SharedParagraphTextureKey(int Width, int Height, int Hash);
    private sealed class SharedParagraphTexture(UiTextureId texture, byte[] pixels)
    {
        internal readonly UiTextureId Texture = texture;
        internal readonly byte[] Pixels = pixels;
        internal int References = 1;
    }

    private Dictionary<SharedParagraphTextureKey, SharedParagraphTexture>? _sharedParagraphTextures;
    private Dictionary<UiTextureId, SharedParagraphTextureKey>? _sharedParagraphTextureIds;
    private int _sharedParagraphTextureBytes;

    private DirectTextCacheEntry EnsureSharedParagraphTexture(DirectTextCacheEntry entry)
    {
        var hash = new HashCode();
        hash.AddBytes(entry.PendingPixels.Span);
        var key = new SharedParagraphTextureKey(entry.Width, entry.Height, hash.ToHashCode());
        var textures = _sharedParagraphTextures ??= [];
        var ids = _sharedParagraphTextureIds ??= [];
        UiTextureId id;
        if (textures.TryGetValue(key, out var shared) && entry.PendingPixels.Span.SequenceEqual(shared.Pixels))
        {
            id = shared.Texture;
            shared.References++;
            entry.PixelOwner?.Dispose();
        }
        else
        {
            id = AllocateOwnedTextureId();
            // Hash collisions never share storage. The snapshot also outlives pooled raster buffers.
            if (shared is null && textures.Count < SharedParagraphTextureLimit && entry.PendingPixels.Length <= SharedParagraphTextureByteLimit - _sharedParagraphTextureBytes)
            {
                var snapshot = entry.PendingPixels.ToArray();
                textures.Add(key, new(id, snapshot));
                ids.Add(id, key);
                _sharedParagraphTextureBytes += snapshot.Length;
                QueueTextureUpdate(new(UiTextureUpdateKind.Create, id, entry.TextureFormat, entry.Width, entry.Height, snapshot));
                entry.PixelOwner?.Dispose();
            }
            else
            {
                QueueTextureUpdate(new UiTextureUpdate(UiTextureUpdateKind.Create, id, entry.TextureFormat,
                    entry.Width, entry.Height, entry.PendingPixels) { PixelOwner = entry.PixelOwner });
            }
        }
        return entry with
        {
            TextureId = id,
            PendingPixels = ReadOnlyMemory<byte>.Empty,
            PixelOwner = null,
            AtlasPageIndex = -1,
            U0 = 0f,
            V0 = 0f,
            U1 = 1f,
            V1 = 1f
        };
    }

    private bool ReleaseSharedParagraphTexture(UiTextureId texture)
    {
        if (_sharedParagraphTextureIds is null || !_sharedParagraphTextureIds.TryGetValue(texture, out var key))
            return false;
        var shared = _sharedParagraphTextures![key];
        if (--shared.References > 0)
            return true;
        _sharedParagraphTextureBytes -= shared.Pixels.Length;
        _sharedParagraphTextures.Remove(key);
        _sharedParagraphTextureIds.Remove(texture);
        return false;
    }
}
