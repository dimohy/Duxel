using VkBuffer = Duxel.Vulkan.Buffer;

namespace Duxel.Vulkan;

public sealed unsafe partial class VulkanRendererBackend
{
    private const ulong CompiledMemoryPageBytes = 8UL * 1024 * 1024;
    private readonly List<StaticMemoryPage> _compiledMemoryPages = new();

    private void CreateCompiledBuffer(ulong bytes, BufferUsageFlags usage, out VkBuffer buffer,
        out StaticMemorySlice slice, out void* mapped)
    {
        const MemoryPropertyFlags properties = MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit;
        const MemoryPropertyFlags preferred = MemoryPropertyFlags.DeviceLocalBit;
        slice = default;
        buffer = CreateUnboundBuffer((nuint)bytes, usage);
        try
        {
            _vk.GetBufferMemoryRequirements(_device, buffer, out var limits);
            var memoryType = TryFindMemoryType(limits.MemoryTypeBits, properties | preferred, out var selected)
                ? selected : FindMemoryType(limits.MemoryTypeBits, properties);
            slice = RentCompiledMemoryRange(memoryType, limits.Size, limits.Alignment);
            Check(_vk.BindBufferMemory(_device, buffer, slice.Page!.Memory, slice.Offset));
            mapped = (byte*)slice.Page.Mapped + slice.Offset;
        }
        catch
        {
            _vk.DestroyBuffer(_device, buffer, null);
            if (slice.Page is not null)
                ReleaseCompiledMemoryRange(slice);
            throw;
        }
    }

    private StaticMemorySlice RentCompiledMemoryRange(uint memoryType, ulong bytes, ulong alignment)
    {
        var slice = TryRentMemoryRange(_compiledMemoryPages, memoryType, bytes, alignment);
        if (slice.Page is not null)
            return slice;

        var capacity = Math.Max(CompiledMemoryPageBytes, bytes);
        var memory = AllocateBufferMemory(capacity, memoryType, deviceAddress: true);
        void* mapped = null;
        var result = _vk.MapMemory(_device, memory, 0, (nuint)capacity, 0, &mapped);
        if (result != Result.Success)
        {
            _vk.FreeMemory(_device, memory, null);
            Check(result);
        }

        var page = new StaticMemoryPage(memory, memoryType, capacity, (nint)mapped);
        _compiledMemoryPages.Add(page);
        return ClaimStaticMemoryRange(page, 0, 0, bytes);
    }

    private void ReleaseCompiledMemoryRange(in StaticMemorySlice slice)
    {
        var page = slice.Page!;
        ReturnMemoryRange(slice);
        page.LiveRanges--;
        if (page.LiveRanges != 0)
            return;

        // Preserve one empty page for each memory type that has reusable ranges.
        if (page.Capacity == CompiledMemoryPageBytes)
        {
            var hasSpare = false;
            foreach (var candidate in _compiledMemoryPages)
            {
                if (!ReferenceEquals(page, candidate) && candidate.MemoryType == page.MemoryType
                    && candidate.Capacity == CompiledMemoryPageBytes && candidate.LiveRanges == 0)
                {
                    hasSpare = true;
                    break;
                }
            }

            if (!hasSpare)
                return;
        }

        DestroyCompiledMemoryPage(page);
    }

    private void DestroyCompiledMemoryPage(StaticMemoryPage page)
    {
        _vk.UnmapMemory(_device, page.Memory);
        DestroyBufferResource(new(default, page.Memory));
        _compiledMemoryPages.Remove(page);
    }

    private void DestroyCompiledMemoryPages()
    {
        while (_compiledMemoryPages.Count != 0)
        {
            var page = _compiledMemoryPages[^1];
            if (page.LiveRanges != 0)
                throw new InvalidOperationException("A compiled memory page has live buffers at shutdown.");
            DestroyCompiledMemoryPage(page);
        }
    }
}
