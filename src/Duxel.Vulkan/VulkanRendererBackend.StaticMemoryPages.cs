using System;
using System.Collections.Generic;

namespace Duxel.Vulkan;

public sealed unsafe partial class VulkanRendererBackend
{
    private const ulong StaticMemoryPageBytes = 2 * 1024 * 1024;
    private readonly List<StaticMemoryPage> _staticMemoryPages = new();

    private sealed class StaticMemoryPage(DeviceMemory memory, uint memoryType)
    {
        public DeviceMemory Memory { get; } = memory;
        public uint MemoryType { get; } = memoryType;
        public List<StaticMemoryRange> Free { get; } = [new(0, StaticMemoryPageBytes)];
        public int LiveRanges { get; set; }
    }

    private readonly record struct StaticMemoryRange(ulong Begin, ulong End);
    private readonly record struct StaticMemorySlice(StaticMemoryPage? Page, ulong Offset, ulong Length);

    private StaticMemorySlice RentStaticMemoryRange(uint memoryType, ulong size, ulong alignment)
    {
        foreach (var page in _staticMemoryPages)
        {
            if (page.MemoryType != memoryType)
                continue;
            var free = page.Free;
            for (var i = 0; i < free.Count; i++)
            {
                var range = free[i];
                var offset = AlignStaticMemory(range.Begin, alignment);
                if (offset <= range.End && size <= range.End - offset)
                    return ClaimStaticMemoryRange(page, i, offset, size);
            }
        }

        var created = new StaticMemoryPage(AllocateBufferMemory(StaticMemoryPageBytes, memoryType, deviceAddress: true), memoryType);
        _staticMemoryPages.Add(created);
        return ClaimStaticMemoryRange(created, 0, 0, size);
    }

    private static StaticMemorySlice ClaimStaticMemoryRange(StaticMemoryPage page, int index, ulong offset, ulong size)
    {
        var free = page.Free;
        var range = free[index];
        var end = offset + size;
        if (range.Begin < offset)
        {
            free[index++] = range with { End = offset };
            if (end < range.End)
                free.Insert(index, range with { Begin = end });
        }
        else if (end < range.End)
        {
            free[index] = range with { Begin = end };
        }
        else
        {
            free.RemoveAt(index);
        }

        page.LiveRanges++;
        return new(page, offset, size);
    }

    private void ReleaseStaticMemoryRange(in StaticMemorySlice slice)
    {
        var page = slice.Page!;
        if (--page.LiveRanges == 0)
        {
            _staticMemoryPages.Remove(page);
            _vk.FreeMemory(_device, page.Memory, null);
            return;
        }

        var begin = slice.Offset;
        var end = begin + slice.Length;
        var free = page.Free;
        var index = 0;
        while (index < free.Count && free[index].Begin < begin)
            index++;
        if (index > 0 && free[index - 1].End == begin)
        {
            begin = free[--index].Begin;
            free.RemoveAt(index);
        }
        if (index < free.Count && free[index].Begin == end)
        {
            end = free[index].End;
            free.RemoveAt(index);
        }
        free.Insert(index, new(begin, end));
    }

    private static ulong AlignStaticMemory(ulong offset, ulong alignment)
        => checked(offset + alignment - 1) & ~(alignment - 1);
}
