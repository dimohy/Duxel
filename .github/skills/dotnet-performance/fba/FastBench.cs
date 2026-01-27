// FBA: Fast/Focused Benchmark Approach (간단 내장 벤치마크)
// .NET 10 / C# 14 기준 템플릿

using System;
using System.Diagnostics;

public static class FastBench
{
    public static void Run(string name, int iterations, Action body)
    {
        // Warm-up
        body();

        // GC baseline
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        long before = GC.GetAllocatedBytesForCurrentThread();
        var sw = Stopwatch.StartNew();

        for (int i = 0; i < iterations; i++)
        {
            body();
        }

        sw.Stop();
        long after = GC.GetAllocatedBytesForCurrentThread();

        Console.WriteLine($"{name}: {sw.Elapsed.TotalMilliseconds:F3} ms, Alloc: {after - before} bytes");
    }
}
