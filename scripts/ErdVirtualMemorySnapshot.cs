#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Duxel.Diagnostics;

public static class ErdVirtualMemorySnapshot
{
    private const uint Commit = 0x1000;
    private const uint Reserve = 0x2000;
    private const uint Private = 0x20000;
    private const uint Mapped = 0x40000;
    private const uint Image = 0x1000000;

    public readonly record struct Allocation(string BaseAddress, ulong CommittedBytes, ulong ReservedBytes);
    public readonly record struct Mapping(string BaseAddress, ulong CommittedBytes, string? FileName, int? FileNameError);

    public readonly record struct Snapshot(ulong PrivateCommitBytes, ulong MappedCommitBytes,
        ulong ImageCommitBytes, ulong ReservedBytes, int Regions, double QueryMilliseconds, Allocation[] PrivateAllocations,
        Mapping[] MappedAllocations);

    public static Snapshot Read(IntPtr process, bool capturePrivateAllocations = false, bool captureMappedAllocations = false)
    {
        var clock = Stopwatch.StartNew();
        GetSystemInfo(out var system);
        var size = (nuint)Marshal.SizeOf<MemoryInformation>();
        ulong privateBytes = 0, mappedBytes = 0, imageBytes = 0, reservedBytes = 0;
        var regions = 0;
        var privateAllocations = capturePrivateAllocations
            ? new Dictionary<nuint, (ulong Committed, ulong Reserved)>()
            : null;
        var mappedAllocations = captureMappedAllocations ? new Dictionary<nuint, Mapping>() : null;
        var fileNameBuffer = captureMappedAllocations ? new StringBuilder(32768) : null;
        for (var address = system.MinimumAddress; address <= system.MaximumAddress;)
        {
            var returned = VirtualQueryEx(process, address, out var region, size);
            if (returned == 0)
                throw new Win32Exception(Marshal.GetLastWin32Error(), $"VirtualQueryEx failed at 0x{address:X}.");
            var next = checked(region.BaseAddress + region.Size);
            if (returned != size || region.BaseAddress > address || next <= address)
                throw new InvalidOperationException($"Invalid virtual memory region at 0x{address:X}.");
            regions++;
            if (region.State == Reserve)
                reservedBytes += region.Size;
            if (privateAllocations is not null && region.State is Commit or Reserve && region.Type == Private)
            {
                privateAllocations.TryGetValue(region.AllocationBase, out var allocation);
                if (region.State == Commit)
                    allocation.Committed += region.Size;
                else
                    allocation.Reserved += region.Size;
                privateAllocations[region.AllocationBase] = allocation;
            }
            if (region.State == Commit)
            {
                switch (region.Type)
                {
                    case Private: privateBytes += region.Size; break;
                    case Mapped:
                        mappedBytes += region.Size;
                        if (mappedAllocations is not null && fileNameBuffer is not null)
                        {
                            if (!mappedAllocations.TryGetValue(region.AllocationBase, out var mapping))
                                mapping = DescribeMapping(process, region, fileNameBuffer);
                            mappedAllocations[region.AllocationBase] = mapping with { CommittedBytes = mapping.CommittedBytes + region.Size };
                        }
                        break;
                    case Image: imageBytes += region.Size; break;
                    default: throw new InvalidOperationException($"Unknown committed region type 0x{region.Type:X}.");
                }
            }
            address = next;
        }
        var allocations = Array.Empty<Allocation>();
        if (privateAllocations is not null)
        {
            allocations = new Allocation[privateAllocations.Count];
            var index = 0;
            foreach (var (address, allocation) in privateAllocations)
                allocations[index++] = new($"0x{address:X}", allocation.Committed, allocation.Reserved);
        }
        Mapping[] mappings = mappedAllocations is null ? [] : [.. mappedAllocations.Values];
        return new(privateBytes, mappedBytes, imageBytes, reservedBytes, regions, clock.Elapsed.TotalMilliseconds, allocations, mappings);
    }

    private static Mapping DescribeMapping(IntPtr process, MemoryInformation region, StringBuilder buffer)
    {
        var length = GetMappedFileNameW(process, region.BaseAddress, buffer, (uint)buffer.Capacity);
        var address = $"0x{region.AllocationBase:X}";
        if (length == 0 || length >= buffer.Capacity)
            return new(address, 0, null, Marshal.GetLastPInvokeError());
        return new(address, 0, buffer.ToString(0, checked((int)length)), null);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryInformation
    {
        public nuint BaseAddress, AllocationBase;
        public uint AllocationProtect;
        public ushort PartitionId;
        public nuint Size;
        public uint State, Protect, Type;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemInformation
    {
        public uint ProcessorDescription, PageSize;
        public nuint MinimumAddress, MaximumAddress, ActiveProcessorMask;
        public uint NumberOfProcessors, ProcessorType, AllocationGranularity;
        public ushort ProcessorLevel, ProcessorRevision;
    }

    [DllImport("kernel32.dll", ExactSpelling = true)]
    private static extern void GetSystemInfo(out SystemInformation information);

    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
    private static extern nuint VirtualQueryEx(IntPtr process, nuint address, out MemoryInformation information, nuint length);

    // PowerShell Add-Type compiles this observer without SDK source generators.
    [DllImport("psapi.dll", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetMappedFileNameW(IntPtr process, nuint address, [Out] StringBuilder fileName, uint capacity);
}

public static class ErdObservedWindow
{
    public static bool RequestMaximize(IntPtr window) => ShowWindowAsync(window, 3) != 0;
    public static bool IsMaximized(IntPtr window) => IsZoomed(window) != 0;

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern int ShowWindowAsync(IntPtr window, int command);

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern int IsZoomed(IntPtr window);
}
