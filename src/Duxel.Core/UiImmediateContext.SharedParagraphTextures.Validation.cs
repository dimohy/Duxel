namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    internal UiTextureId ProbeSharedMask(string name, int width, int height, float x,
        System.Buffers.IMemoryOwner<byte> owner)
    {
        var key = ProbeSharedMaskKey(name);
        var input = new DirectTextCacheEntry(null, width, height, 0, 0, 0, _state.FrameCount,
            owner.Memory, PixelOwner : owner, OriginX : x);
        StoreDirectTextEntry(key, input);
        var entry = EnsureDirectTextTexture(key, input);
        if (entry.OriginX != x)
            throw new InvalidOperationException("Sharing changed the entry's paint location.");
        return entry.TextureId!.Value;
    }

    internal void ProbeRemoveSharedMask(string name) => RemoveDirectTextCacheEntry(ProbeSharedMaskKey(name));

    private static DirectTextCacheKey ProbeSharedMaskKey(string name) => new("fixture", null, name, 14, 0)
    {
        ParagraphRun = new(0, "fixture", 14, 0, default, [])
    };
}