namespace Duxel.Vulkan;

public sealed unsafe partial class VulkanRendererBackend
{
    private const int TextTexturePoolLimit = 2048;
    private const ulong TextTexturePoolByteLimit = 16 * 1024 * 1024;
    private readonly List<TextureResource> _idleTextTextures = new(TextTexturePoolLimit);
    private ulong _idleTextTextureBytes;
    private long _textTextureReuses;
    private const ulong TextImageMemoryPageBytes = 2 * 1024 * 1024;
    private readonly List<StaticMemoryPage> _textImageMemoryPages = [];

    private StaticMemorySlice RentTextImageMemory(uint memoryType, ulong size, ulong alignment)
    {
        var slice = TryRentMemoryRange(_textImageMemoryPages, memoryType, size, alignment);
        if (slice.Page is not null)
            return slice;

        var memory = AllocateBufferMemory(TextImageMemoryPageBytes, memoryType, deviceAddress: false);
        var page = new StaticMemoryPage(memory, memoryType, TextImageMemoryPageBytes);
        _textImageMemoryPages.Add(page);
        return ClaimStaticMemoryRange(page, 0, 0, size);
    }

    // The image owns its slice through normal fence retirement and idle texture reuse.
    private void ReleaseTextImageMemory(in StaticMemorySlice slice)
    {
        var page = slice.Page!;
        if (--page.LiveRanges == 0)
        {
            _textImageMemoryPages.Remove(page);
            _vk.FreeMemory(_device, page.Memory, null);
        }
        else
        {
            ReturnMemoryRange(slice);
        }
    }

    private bool TryReuseTextTexture(int width, int height, Format format, out TextureResource resource)
    {
        for (var i = _idleTextTextures.Count - 1; i >= 0; i--)
        {
            var candidate = _idleTextTextures[i];
            if (candidate.Width != width || candidate.Height != height || candidate.Format != format)
                continue;
            _idleTextTextures.RemoveAt(i);
            _idleTextTextureBytes -= candidate.AllocationBytes;
            _textTextureReuses++;
            resource = candidate;
            return true;
        }
        resource = default;
        return false;
    }

    // Called only after the existing frame-fence retirement deadline. Descriptor,
    // image and memory ownership stay together until reuse or final destruction.
    private void RecycleCompletedTextTexture(TextureResource resource)
    {
        if (!resource.ReusableText || resource.AllocationBytes > TextTexturePoolByteLimit)
        {
            DestroyTextureResource(resource);
            return;
        }
        while (_idleTextTextures.Count >= TextTexturePoolLimit
            || _idleTextTextureBytes + resource.AllocationBytes > TextTexturePoolByteLimit)
        {
            var oldest = _idleTextTextures[0];
            _idleTextTextures.RemoveAt(0);
            _idleTextTextureBytes -= oldest.AllocationBytes;
            DestroyTextureResource(oldest);
        }
        _idleTextTextures.Add(resource);
        _idleTextTextureBytes += resource.AllocationBytes;
    }

    private void DestroyIdleTextTextures()
    {
        foreach (var texture in _idleTextTextures)
            DestroyTextureResource(texture);
        _idleTextTextures.Clear();
        _idleTextTextureBytes = 0;
    }
}
