using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using Duxel.Core;

namespace Duxel.Vulkan;

public sealed unsafe partial class VulkanRendererBackend
{
    private static readonly string? VulkanFailureLogPath = Environment.GetEnvironmentVariable("DUXEL_VK_FAIL_LOG");
    private static readonly object VulkanFailureLogLock = new();
    private static readonly object FontCommandDiagLogLock = new();

    private const bool _profilingEnabled = false;
    private const int _profilingLogInterval = 30;
    private const bool _fontCommandDiagEnabled = false;
    private const string? _fontCommandDiagLogPath = null;
    private const bool _fontCommandBoundsAssertEnabled = false;

    private int _profilingFrameCounter;

    private static int ParseProfileLogInterval()
    {
        var raw = Environment.GetEnvironmentVariable("DUXEL_VK_PROFILE_EVERY");
        if (int.TryParse(raw, out var value) && value > 0)
        {
            return value;
        }

        return 30;
    }

    private void LogProfileFrame(
        long uploadTicks,
        long recordTicks,
        long recordTextureLookupTicks,
        long recordClippingTicks,
        long recordDescriptorBindTicks,
        long recordDrawCallTicks,
        long submitTicks,
        long presentTicks)
    {
        _profilingFrameCounter++;
        if (_profilingFrameCounter % _profilingLogInterval != 0)
        {
            return;
        }

        var uploadUs = TicksToMicroseconds(uploadTicks);
        var recordUs = TicksToMicroseconds(recordTicks);
        var recordTextureLookupUs = TicksToMicroseconds(recordTextureLookupTicks);
        var recordClippingUs = TicksToMicroseconds(recordClippingTicks);
        var recordDescriptorBindUs = TicksToMicroseconds(recordDescriptorBindTicks);
        var recordDrawCallUs = TicksToMicroseconds(recordDrawCallTicks);
        var submitUs = TicksToMicroseconds(submitTicks);
        var presentUs = TicksToMicroseconds(presentTicks);
        Console.WriteLine(
            $"[duxel-vk-prof] upload={uploadUs:0.000} record={recordUs:0.000}(tex={recordTextureLookupUs:0.000} clip={recordClippingUs:0.000} desc={recordDescriptorBindUs:0.000} draw={recordDrawCallUs:0.000}) submit={submitUs:0.000} present={presentUs:0.000}");
    }

    private static double TicksToMicroseconds(long ticks)
    {
        return ticks * (1000000.0 / Stopwatch.Frequency);
    }

    private void EmitFontCommandDiag(string message)
    {
    }

    private static void Check(Result result)
    {
        if ((int)result >= 0 || IsSuboptimal(result))
        {
            return;
        }

        TraceVulkanFailure(result);

        throw new InvalidOperationException($"Vulkan call failed: {result}");
    }

    private bool TryWaitForDeviceIdle(string phase, bool throwOnFailure)
    {
        if (_device.Handle is 0)
        {
            return true;
        }

        var result = _vk.DeviceWaitIdle(_device);
        if ((int)result >= 0 || IsSuboptimal(result))
        {
            return true;
        }

        TraceVulkanFailure(result);
        if (throwOnFailure)
        {
            throw new InvalidOperationException($"Vulkan DeviceWaitIdle failed during {phase}: {result}");
        }

        return false;
    }

    private static void TraceVulkanFailure(Result result)
    {
        if (string.IsNullOrWhiteSpace(VulkanFailureLogPath))
        {
            return;
        }

        try
        {
            var path = VulkanFailureLogPath!;
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var sb = new StringBuilder(768);
            sb.AppendLine("==== VulkanFailure ====");
            sb.Append("Utc: ").AppendLine(DateTime.UtcNow.ToString("O"));
            sb.Append("Result: ").AppendLine(result.ToString());
            sb.Append("Pid: ").AppendLine(Environment.ProcessId.ToString());
            sb.Append("ThreadId: ").AppendLine(Environment.CurrentManagedThreadId.ToString());
            sb.Append("DUXEL_VK_UPLOAD_BATCH: ").AppendLine(Environment.GetEnvironmentVariable("DUXEL_VK_UPLOAD_BATCH") ?? "<null>");
            sb.Append("DUXEL_VK_PROFILE: ").AppendLine(Environment.GetEnvironmentVariable("DUXEL_VK_PROFILE") ?? "<null>");
            sb.AppendLine("Stack:");
            sb.AppendLine(Environment.StackTrace);

            lock (VulkanFailureLogLock)
            {
                File.AppendAllText(path, sb.ToString());
            }
        }
        catch
        {
        }
    }

    private static bool IsSuboptimal(Result result)
    {
        const Result suboptimalKhr = (Result)1000001003;
        if (result == Result.SuboptimalKhr || result == suboptimalKhr)
        {
            return true;
        }

        var name = result.ToString();
        return name.Contains("Suboptimal", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSurfaceLost(Result result)
    {
        const Result errorSurfaceLostKhr = (Result)(-1000000000);
        if (result == Result.ErrorSurfaceLostKhr || result == errorSurfaceLostKhr)
        {
            return true;
        }

        var name = result.ToString();
        return name.Contains("SurfaceLost", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSurfaceLostException(InvalidOperationException ex)
    {
        return ex.Message.Contains("ErrorSurfaceLostKhr", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("SurfaceLost", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRecoverableSwapchainException(InvalidOperationException ex)
    {
        var message = ex.Message;
        return message.Contains("SurfaceLost", StringComparison.OrdinalIgnoreCase)
            || message.Contains("OutOfDate", StringComparison.OrdinalIgnoreCase)
            || message.Contains("DeviceWaitIdle", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Vulkan call failed", StringComparison.OrdinalIgnoreCase);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void EnsureIndexedDrawCommandKind(in UiDrawCommand cmd)
    {
        if (cmd.Kind is UiDrawCommandKind.Triangles)
        {
            return;
        }

        throw new InvalidOperationException($"Vulkan indexed draw path does not support draw command kind '{cmd.Kind}'.");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool ShouldAnalyzeFontCommandBounds(bool isFontCommand)
    {
        return isFontCommand && (_fontCommandDiagEnabled || _fontCommandBoundsAssertEnabled);
    }

    private void AnalyzeAndValidateFontCommandBounds(
        UiDrawList drawList,
        in UiDrawCommand cmd,
        bool isFontCommand,
        uint imageIndex,
        int listIndex,
        int cmdIndex,
        ref int emittedFontCommandDiag,
        int maxFontCommandDiagPerPass)
    {
        if (!ShouldAnalyzeFontCommandBounds(isFontCommand))
        {
            return;
        }

        var indexStart = (int)cmd.IndexOffset;
        var indexEndExclusive = indexStart + (int)cmd.ElementCount;
        var indexCount = drawList.Indices.Count;
        var vertexCount = drawList.Vertices.Count;
        var indexRangeInvalid = indexStart < 0 || indexEndExclusive < indexStart || indexEndExclusive > indexCount;

        var rawIndexMin = int.MaxValue;
        var rawIndexMax = int.MinValue;
        var invalidVertexRefCount = 0;
        var uvMinX = float.PositiveInfinity;
        var uvMinY = float.PositiveInfinity;
        var uvMaxX = float.NegativeInfinity;
        var uvMaxY = float.NegativeInfinity;
        var uvOutOfRangeCount = 0;
        var scannedIndexCount = 0;

        if (!indexRangeInvalid)
        {
            for (var i = indexStart; i < indexEndExclusive; i++)
            {
                var localVertexIndex = (int)drawList.Indices[i];
                scannedIndexCount++;
                if (localVertexIndex < rawIndexMin)
                {
                    rawIndexMin = localVertexIndex;
                }

                if (localVertexIndex > rawIndexMax)
                {
                    rawIndexMax = localVertexIndex;
                }

                if ((uint)localVertexIndex >= (uint)vertexCount)
                {
                    invalidVertexRefCount++;
                    continue;
                }

                var vertex = drawList.Vertices[localVertexIndex];
                var uv = vertex.UV;
                if (uv.X < uvMinX)
                {
                    uvMinX = uv.X;
                }

                if (uv.Y < uvMinY)
                {
                    uvMinY = uv.Y;
                }

                if (uv.X > uvMaxX)
                {
                    uvMaxX = uv.X;
                }

                if (uv.Y > uvMaxY)
                {
                    uvMaxY = uv.Y;
                }

                if (uv.X < 0f || uv.X > 1f || uv.Y < 0f || uv.Y > 1f)
                {
                    uvOutOfRangeCount++;
                }
            }
        }

        var hasUvBounds = !float.IsPositiveInfinity(uvMinX) && !float.IsNegativeInfinity(uvMaxX);
        if ((_fontCommandDiagEnabled || _fontCommandBoundsAssertEnabled) && emittedFontCommandDiag < maxFontCommandDiagPerPass)
        {
            var uvBoundsText = hasUvBounds
                ? $"({uvMinX:0.######},{uvMinY:0.######})-({uvMaxX:0.######},{uvMaxY:0.######})"
                : "(n/a)";
            var rawIndexText = rawIndexMin <= rawIndexMax
                ? $"{rawIndexMin}..{rawIndexMax}"
                : "n/a";
            EmitFontCommandDiag(
                $"frame={_frameIndex} image={imageIndex} list={listIndex} cmd={cmdIndex} tex={cmd.TextureId.Value} isFont=1 bounds idxRange={indexStart}..{indexEndExclusive - 1} idxCount={indexCount} scanned={scannedIndexCount} rawIdx={rawIndexText} vtxCount={vertexCount} idxRangeInvalid={(indexRangeInvalid ? 1 : 0)} invalidVtxRef={invalidVertexRefCount} uvBounds={uvBoundsText} uvOutOfRange={uvOutOfRangeCount}");
            emittedFontCommandDiag++;
        }

        if (_fontCommandBoundsAssertEnabled && (indexRangeInvalid || invalidVertexRefCount > 0 || uvOutOfRangeCount > 0))
        {
            throw new InvalidOperationException(
                $"Font draw command bounds validation failed: frame={_frameIndex} image={imageIndex} list={listIndex} cmd={cmdIndex} tex={cmd.TextureId.Value} idxRangeInvalid={indexRangeInvalid} invalidVtxRef={invalidVertexRefCount} uvOutOfRange={uvOutOfRangeCount}");
        }
    }
}
