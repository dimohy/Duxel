using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using Duxel.Core;

namespace Duxel.Vulkan;

public sealed unsafe partial class VulkanRendererBackend
{
    private static readonly string? VulkanFailureLogPath = Environment.GetEnvironmentVariable("DUXEL_VK_FAIL_LOG");
    private static readonly string? VulkanProfileLogPath = Environment.GetEnvironmentVariable("DUXEL_VK_PROFILE_OUT");
    private static readonly object VulkanFailureLogLock = new();
    private static readonly object VulkanProfileLogLock = new();
    private static readonly object FontCommandDiagLogLock = new();
    private static readonly object CommandDiagLogLock = new();

    private readonly bool _profilingEnabled = ParseProfilingEnabled();
    private readonly int _profilingLogInterval = ParseProfileLogInterval();
    private static readonly bool _fontCommandDiagEnabled = ParseBooleanEnvironmentFlag("DUXEL_VK_FONT_DIAG");
    private static readonly string? _fontCommandDiagLogPath = Environment.GetEnvironmentVariable("DUXEL_VK_FONT_DIAG_OUT");
    private static readonly bool _commandDiagEnabled = ParseBooleanEnvironmentFlag("DUXEL_VK_COMMAND_DIAG");
    private static readonly string? _commandDiagLogPath = Environment.GetEnvironmentVariable("DUXEL_VK_COMMAND_DIAG_OUT");
    private static readonly int _commandDiagLogInterval = ParsePositiveIntEnvironment("DUXEL_VK_COMMAND_DIAG_EVERY", 120);
    private static readonly int _commandDiagFrameLimit = ParsePositiveIntEnvironment("DUXEL_VK_COMMAND_DIAG_FRAMES", 1);
    private const bool _fontCommandBoundsAssertEnabled = false;

    private int _profilingFrameCounter;
    private int _commandDiagFrameCounter;
    private readonly bool _gpuProfilingRequested = ParseBooleanEnvironmentFlag("DUXEL_VK_GPU_PROFILE");
    private bool _gpuProfilingEnabled;

    private readonly struct CommandRecordStats
    {
        public readonly int StaticDrawListCount;
        public readonly int DynamicDrawListCount;
        public readonly int StaticSecondaryCandidateDrawListCount;
        public readonly int StaticSecondaryCandidateCommandCount;
        public readonly int StaticSecondaryCandidateDrawCallCount;
        public readonly int StaticCommandCount;
        public readonly int DynamicCommandCount;
        public readonly int StaticDrawCallCount;
        public readonly int DynamicDrawCallCount;
        public readonly int StaticPipelineBindCount;
        public readonly int DynamicPipelineBindCount;
        public readonly int StaticScissorComputeCount;
        public readonly int DynamicScissorComputeCount;
        public readonly int StaticScissorSetCount;
        public readonly int DynamicScissorSetCount;
        public readonly int StaticPushConstantCount;
        public readonly int DynamicPushConstantCount;
        public readonly int StaticGeometryBindCount;
        public readonly int DynamicGeometryBindCount;
        public readonly int StaticPrimitiveBindCount;
        public readonly int DynamicPrimitiveBindCount;
        public readonly int CommandCount;
        public readonly int DrawCallCount;
        public readonly int PipelineBindCount;
        public readonly int DescriptorBindCount;
        public readonly int ScissorSetCount;
        public readonly int ScissorComputeCount;
        public readonly int ScissorComputeReuseCount;
        public readonly int PushConstantCount;
        public readonly int GeometryBindCount;
        public readonly int PrimitiveBindCount;
        public readonly int TrianglePipelineBindCount;
        public readonly int FontPipelineBindCount;
        public readonly int RectPrimitivePipelineBindCount;
        public readonly int CirclePrimitivePipelineBindCount;
        public readonly int ActualFontPipelineBindCount;
        public readonly int ActualTexturedTrianglePipelineBindCount;
        public readonly int ActualTexturedPrimitivePipelineBindCount;
        public readonly int TriangleToPrimitiveTransitionCount;
        public readonly int PrimitiveToTriangleTransitionCount;
        public readonly int RectCircleTransitionCount;
        public readonly int SchedulerProbeCount;
        public readonly int SchedulerCacheHitCount;
        public readonly int SchedulerCacheMissCount;
        public readonly int SchedulerNoChangeCount;
        public readonly int SchedulerScheduledListCount;
        public readonly int SchedulerMergedCommandCount;
        public readonly long PipelineBindTicks;
        public readonly long DescriptorBindTicks;
        public readonly long BufferBindTicks;
        public readonly long PushConstantTicks;
        public readonly long ScissorSetTicks;
        public readonly long SchedulerTicks;

        public CommandRecordStats(
            int staticDrawListCount,
            int dynamicDrawListCount,
            int staticSecondaryCandidateDrawListCount,
            int staticSecondaryCandidateCommandCount,
            int staticSecondaryCandidateDrawCallCount,
            int staticCommandCount,
            int dynamicCommandCount,
            int staticDrawCallCount,
            int dynamicDrawCallCount,
            int staticPipelineBindCount,
            int dynamicPipelineBindCount,
            int staticScissorComputeCount,
            int dynamicScissorComputeCount,
            int staticScissorSetCount,
            int dynamicScissorSetCount,
            int staticPushConstantCount,
            int dynamicPushConstantCount,
            int staticGeometryBindCount,
            int dynamicGeometryBindCount,
            int staticPrimitiveBindCount,
            int dynamicPrimitiveBindCount,
            int commandCount,
            int drawCallCount,
            int pipelineBindCount,
            int descriptorBindCount,
            int scissorSetCount,
            int scissorComputeCount,
            int scissorComputeReuseCount,
            int pushConstantCount,
            int geometryBindCount,
            int primitiveBindCount,
            int trianglePipelineBindCount,
            int fontPipelineBindCount,
            int rectPrimitivePipelineBindCount,
            int circlePrimitivePipelineBindCount,
            int actualFontPipelineBindCount,
            int actualTexturedTrianglePipelineBindCount,
            int actualTexturedPrimitivePipelineBindCount,
            int triangleToPrimitiveTransitionCount,
            int primitiveToTriangleTransitionCount,
            int rectCircleTransitionCount,
            int schedulerProbeCount,
            int schedulerCacheHitCount,
            int schedulerCacheMissCount,
            int schedulerNoChangeCount,
            int schedulerScheduledListCount,
            int schedulerMergedCommandCount,
            long pipelineBindTicks,
            long descriptorBindTicks,
            long bufferBindTicks,
            long pushConstantTicks,
            long scissorSetTicks,
            long schedulerTicks)
        {
            StaticDrawListCount = staticDrawListCount;
            DynamicDrawListCount = dynamicDrawListCount;
            StaticSecondaryCandidateDrawListCount = staticSecondaryCandidateDrawListCount;
            StaticSecondaryCandidateCommandCount = staticSecondaryCandidateCommandCount;
            StaticSecondaryCandidateDrawCallCount = staticSecondaryCandidateDrawCallCount;
            StaticCommandCount = staticCommandCount;
            DynamicCommandCount = dynamicCommandCount;
            StaticDrawCallCount = staticDrawCallCount;
            DynamicDrawCallCount = dynamicDrawCallCount;
            StaticPipelineBindCount = staticPipelineBindCount;
            DynamicPipelineBindCount = dynamicPipelineBindCount;
            StaticScissorComputeCount = staticScissorComputeCount;
            DynamicScissorComputeCount = dynamicScissorComputeCount;
            StaticScissorSetCount = staticScissorSetCount;
            DynamicScissorSetCount = dynamicScissorSetCount;
            StaticPushConstantCount = staticPushConstantCount;
            DynamicPushConstantCount = dynamicPushConstantCount;
            StaticGeometryBindCount = staticGeometryBindCount;
            DynamicGeometryBindCount = dynamicGeometryBindCount;
            StaticPrimitiveBindCount = staticPrimitiveBindCount;
            DynamicPrimitiveBindCount = dynamicPrimitiveBindCount;
            CommandCount = commandCount;
            DrawCallCount = drawCallCount;
            PipelineBindCount = pipelineBindCount;
            DescriptorBindCount = descriptorBindCount;
            ScissorSetCount = scissorSetCount;
            ScissorComputeCount = scissorComputeCount;
            ScissorComputeReuseCount = scissorComputeReuseCount;
            PushConstantCount = pushConstantCount;
            GeometryBindCount = geometryBindCount;
            PrimitiveBindCount = primitiveBindCount;
            TrianglePipelineBindCount = trianglePipelineBindCount;
            FontPipelineBindCount = fontPipelineBindCount;
            RectPrimitivePipelineBindCount = rectPrimitivePipelineBindCount;
            CirclePrimitivePipelineBindCount = circlePrimitivePipelineBindCount;
            ActualFontPipelineBindCount = actualFontPipelineBindCount;
            ActualTexturedTrianglePipelineBindCount = actualTexturedTrianglePipelineBindCount;
            ActualTexturedPrimitivePipelineBindCount = actualTexturedPrimitivePipelineBindCount;
            TriangleToPrimitiveTransitionCount = triangleToPrimitiveTransitionCount;
            PrimitiveToTriangleTransitionCount = primitiveToTriangleTransitionCount;
            RectCircleTransitionCount = rectCircleTransitionCount;
            SchedulerProbeCount = schedulerProbeCount;
            SchedulerCacheHitCount = schedulerCacheHitCount;
            SchedulerCacheMissCount = schedulerCacheMissCount;
            SchedulerNoChangeCount = schedulerNoChangeCount;
            SchedulerScheduledListCount = schedulerScheduledListCount;
            SchedulerMergedCommandCount = schedulerMergedCommandCount;
            PipelineBindTicks = pipelineBindTicks;
            DescriptorBindTicks = descriptorBindTicks;
            BufferBindTicks = bufferBindTicks;
            PushConstantTicks = pushConstantTicks;
            ScissorSetTicks = scissorSetTicks;
            SchedulerTicks = schedulerTicks;
        }
    }

    private static bool ParseProfilingEnabled()
    {
        var raw = Environment.GetEnvironmentVariable("DUXEL_VK_PROFILE");
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        return !string.Equals(raw, "0", StringComparison.Ordinal)
            && !string.Equals(raw, "false", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(raw, "off", StringComparison.OrdinalIgnoreCase);
    }

    private static int ParseProfileLogInterval()
    {
        return ParsePositiveIntEnvironment("DUXEL_VK_PROFILE_EVERY", 30);
    }

    private static int ParsePositiveIntEnvironment(string name, int defaultValue)
    {
        var raw = Environment.GetEnvironmentVariable(name);
        if (int.TryParse(raw, out var value) && value > 0)
        {
            return value;
        }

        return defaultValue;
    }

    private static bool ParseBooleanEnvironmentFlag(string name)
    {
        var raw = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        return string.Equals(raw, "1", StringComparison.Ordinal)
            || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(raw, "on", StringComparison.OrdinalIgnoreCase)
            || string.Equals(raw, "yes", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryParseBooleanEnvironmentFlag(string name, out bool value)
    {
        var raw = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(raw))
        {
            value = false;
            return false;
        }

        value = string.Equals(raw, "1", StringComparison.Ordinal)
            || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(raw, "on", StringComparison.OrdinalIgnoreCase)
            || string.Equals(raw, "yes", StringComparison.OrdinalIgnoreCase);
        return true;
    }

    private void LogProfileFrame(
        long targetTicks,
        long beginTicks,
        long frameFenceTicks,
        long acquireTicks,
        long imageFenceTicks,
        long uploadTicks,
        long recordTicks,
        long recordTextureLookupTicks,
        long recordClippingTicks,
        long recordDescriptorBindTicks,
        long recordDrawCallTicks,
        long submitTicks,
        long presentTicks,
        double gpuRenderUs,
        in CommandRecordStats recordStats)
    {
        _profilingFrameCounter++;
        if (_profilingFrameCounter % _profilingLogInterval != 0)
        {
            return;
        }

        var targetUs = TicksToMicroseconds(targetTicks);
        var beginUs = TicksToMicroseconds(beginTicks);
        var frameFenceUs = TicksToMicroseconds(frameFenceTicks);
        var acquireUs = TicksToMicroseconds(acquireTicks);
        var imageFenceUs = TicksToMicroseconds(imageFenceTicks);
        var uploadUs = TicksToMicroseconds(uploadTicks);
        var recordUs = TicksToMicroseconds(recordTicks);
        var recordTextureLookupUs = TicksToMicroseconds(recordTextureLookupTicks);
        var recordClippingUs = TicksToMicroseconds(recordClippingTicks);
        var recordDescriptorBindUs = TicksToMicroseconds(recordDescriptorBindTicks);
        var recordDrawCallUs = TicksToMicroseconds(recordDrawCallTicks);
        var pipelineBindUs = TicksToMicroseconds(recordStats.PipelineBindTicks);
        var descriptorBindUs = TicksToMicroseconds(recordStats.DescriptorBindTicks);
        var bufferBindUs = TicksToMicroseconds(recordStats.BufferBindTicks);
        var pushConstantUs = TicksToMicroseconds(recordStats.PushConstantTicks);
        var scissorSetUs = TicksToMicroseconds(recordStats.ScissorSetTicks);
        var schedulerUs = TicksToMicroseconds(recordStats.SchedulerTicks);
        var imageTransitionUs = TicksToMicroseconds(_profileImageTransitionTicks);
        var uploadSubmitUs = TicksToMicroseconds(_profileUploadSubmitTicks);
        var uploadPrepareSubmitUs = TicksToMicroseconds(_profileUploadPrepareSubmitTicks);
        var uploadWaitUs = TicksToMicroseconds(_profileUploadWaitTicks);
        var submitUs = TicksToMicroseconds(submitTicks);
        var presentUs = TicksToMicroseconds(presentTicks);
        var gpuRenderText = double.IsNaN(gpuRenderUs) ? "n/a" : $"{gpuRenderUs:0.000}";
        var staticGeometryMemory = GetStaticGeometryMemoryStats();
        var line = $"[duxel-vk-prof] {CreateProfileDevicePolicyText()} target={targetUs:0.000} begin={beginUs:0.000}(frameFence={frameFenceUs:0.000} acquire={acquireUs:0.000} imageFence={imageFenceUs:0.000}) upload={uploadUs:0.000} record={recordUs:0.000}(tex={recordTextureLookupUs:0.000} clip={recordClippingUs:0.000} state={recordDescriptorBindUs:0.000} draw={recordDrawCallUs:0.000}) submit={submitUs:0.000} present={presentUs:0.000} gpuRender={gpuRenderText} "
            + $"lists(static={recordStats.StaticDrawListCount} dyn={recordStats.DynamicDrawListCount}) "
            + $"staticSec(cand={recordStats.StaticSecondaryCandidateDrawListCount} cmds={recordStats.StaticSecondaryCandidateCommandCount} draws={recordStats.StaticSecondaryCandidateDrawCallCount}) "
            + $"listWork(staticCmd={recordStats.StaticCommandCount} dynCmd={recordStats.DynamicCommandCount} staticDraw={recordStats.StaticDrawCallCount} dynDraw={recordStats.DynamicDrawCallCount} staticPipe={recordStats.StaticPipelineBindCount} dynPipe={recordStats.DynamicPipelineBindCount} staticClip={recordStats.StaticScissorComputeCount} dynClip={recordStats.DynamicScissorComputeCount} staticScissor={recordStats.StaticScissorSetCount} dynScissor={recordStats.DynamicScissorSetCount} staticPush={recordStats.StaticPushConstantCount} dynPush={recordStats.DynamicPushConstantCount} staticGeom={recordStats.StaticGeometryBindCount} dynGeom={recordStats.DynamicGeometryBindCount} staticPrim={recordStats.StaticPrimitiveBindCount} dynPrim={recordStats.DynamicPrimitiveBindCount}) "
            + $"staticGeom(hit={_profileStaticGeometryHitCount} create={_profileStaticGeometryCreateCount} replace={_profileStaticGeometryReplaceCount} update={_profileStaticGeometryUpdateCount} reuse={_profileStaticGeometryReuseCount} hash={_profileStaticGeometryHashCount}) "
            + $"staticMem(active={staticGeometryMemory.ActiveEntries} activeBytes={staticGeometryMemory.ActiveBytes} retired={staticGeometryMemory.RetiredEntries} retiredBytes={staticGeometryMemory.RetiredBytes}) "
            + $"staticPrim(expand={_profileStaticPrimitiveTriangleExpandedListCount} expandPrim={_profileStaticPrimitiveTriangleExpandedPrimitiveCount} layout={_profileStaticPrimitiveTriangleLayoutMaterializationCount} force={_profileStaticPrimitiveTriangleForcedListCount} autoSkip={_profileStaticPrimitiveTriangleAutoSkippedListCount} autoSkipPrim={_profileStaticPrimitiveTriangleAutoSkippedPrimitiveCount} autoSkipMut={_profileStaticPrimitiveTriangleAutoSkippedMutationListCount} expandBytes={_profileStaticPrimitiveTriangleExpandedBytes} autoSkipBytes={_profileStaticPrimitiveTriangleAutoSkippedBytes}) "
            + $"upSched(sub={_profileUploadSubmissionCount} prepSub={_profileUploadPrepareSubmissionCount} wait={_profileUploadWaitCount} flush={_profileUploadBatchFlushCount} bytes={_profileUploadStagingBytes} texRegions={_profileUploadTextureCopyRegionCount} bufCopies={_profileUploadBufferCopyCount} submitUs={uploadSubmitUs:0.000} prepUs={uploadPrepareSubmitUs:0.000} waitUs={uploadWaitUs:0.000}) "
            + $"imgTrans(total={_profileImageTransitionCount} toDst={_profileImageTransitionToTransferDstCount} toShader={_profileImageTransitionToShaderReadCount} present={_profileImageTransitionPresentCount} color={_profileImageTransitionColorAttachmentCount} xferStage={_profileImageTransitionTransferStageCompatibleCount} gfxStage={_profileImageTransitionGraphicsStageRequiredCount} us={imageTransitionUs:0.000}) "
            + $"cmds={recordStats.CommandCount} draws={recordStats.DrawCallCount} binds(pipe={recordStats.PipelineBindCount} tri={recordStats.TrianglePipelineBindCount} font={recordStats.FontPipelineBindCount} rect={recordStats.RectPrimitivePipelineBindCount} circle={recordStats.CirclePrimitivePipelineBindCount} desc={recordStats.DescriptorBindCount} geom={recordStats.GeometryBindCount} prim={recordStats.PrimitiveBindCount}) "
            + $"pipeClass(font={recordStats.ActualFontPipelineBindCount} texTri={recordStats.ActualTexturedTrianglePipelineBindCount} texPrim={recordStats.ActualTexturedPrimitivePipelineBindCount}) "
            + $"transitions(tri2prim={recordStats.TriangleToPrimitiveTransitionCount} prim2tri={recordStats.PrimitiveToTriangleTransitionCount} rectCircle={recordStats.RectCircleTransitionCount}) "
            + $"state(scissor={recordStats.ScissorSetCount} push={recordStats.PushConstantCount}) clipCache(calc={recordStats.ScissorComputeCount} reuse={recordStats.ScissorComputeReuseCount}) "
            + $"sched(probe={recordStats.SchedulerProbeCount} hit={recordStats.SchedulerCacheHitCount} miss={recordStats.SchedulerCacheMissCount} nochange={recordStats.SchedulerNoChangeCount} lists={recordStats.SchedulerScheduledListCount} merged={recordStats.SchedulerMergedCommandCount} us={schedulerUs:0.000}) "
            + $"stateUs(pipe={pipelineBindUs:0.000} desc={descriptorBindUs:0.000} buf={bufferBindUs:0.000} push={pushConstantUs:0.000} scissor={scissorSetUs:0.000})";
        if (string.IsNullOrWhiteSpace(VulkanProfileLogPath))
        {
            Console.WriteLine(line);
            return;
        }

        var path = VulkanProfileLogPath!;
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        lock (VulkanProfileLogLock)
        {
            File.AppendAllText(path, line + Environment.NewLine);
        }
    }

    private string CreateProfileDevicePolicyText()
    {
        var uploadQueue = _devicePolicy.UseGraphicsQueueForUploads ? "graphics" : "transfer";

        return $"device(vendor={_devicePolicy.Vendor} vid={_devicePolicy.VendorId:x4} did={_devicePolicy.DeviceId:x4} type={_devicePolicy.DeviceType} name={ToProfileToken(_devicePolicy.DeviceName)} gfxQ={_graphicsQueueFamily} uploadQ={_transferQueueFamily} xferCandQ={_dedicatedTransferQueueFamily} tsBits={_devicePolicy.GraphicsQueueTimestampValidBits} tsPeriodNs={_devicePolicy.TimestampPeriodNanoseconds:0.###} gpuTs={BoolToInt(_gpuProfilingEnabled)}) policy(upload={uploadQueue} transferCandidate={BoolToInt(_devicePolicy.DedicatedTransferQueueCandidate)} staticPrimTri={BoolToInt(_staticPrimitiveTrianglesEnabled)} staticUpdate={ToProfileModeToken(_resolvedStaticGeometryUpdateMode)} staticUpdateReq={ToProfileModeToken(_staticGeometryUpdateMode)} scheduler={ToProfileModeToken(_commandSchedulerMode)} schedWindow={_commandSchedulerMaxWindow} staticSecondaryMin={_devicePolicy.StaticSecondaryMinDrawCount})";
    }

    private static int BoolToInt(bool value) => value ? 1 : 0;

    private static string ToProfileModeToken(VulkanStaticGeometryUpdateMode mode)
    {
        return mode.ToString().ToLowerInvariant();
    }

    private static string ToProfileModeToken(VulkanCommandSchedulerMode mode)
    {
        return mode.ToString().ToLowerInvariant();
    }

    private static string ToProfileToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "unknown";
        }

        var sb = new StringBuilder(value.Length);
        for (var i = 0; i < value.Length; i++)
        {
            var ch = value[i];
            if (char.IsLetterOrDigit(ch) || ch is '.' or '_' or '-')
            {
                sb.Append(ch);
            }
            else
            {
                sb.Append('_');
            }
        }

        var token = sb.ToString().Trim('_');
        return token.Length is 0 ? "unknown" : token;
    }

    private static double TicksToMicroseconds(long ticks)
    {
        return ticks * (1000000.0 / Stopwatch.Frequency);
    }

    private double TryReadGpuTimestampResult(FrameResources frame)
    {
        if (frame.TimestampQueryPool.Handle is 0 || !frame.TimestampQueryIssued)
        {
            return double.NaN;
        }

        var timestamps = stackalloc ulong[(int)GpuProfileTimestampQueryCount];
        var result = _vk.GetQueryPoolResults(
            _device,
            frame.TimestampQueryPool,
            GpuProfileStartQuery,
            GpuProfileTimestampQueryCount,
            (nuint)(sizeof(ulong) * (int)GpuProfileTimestampQueryCount),
            timestamps,
            (ulong)sizeof(ulong),
            QueryResultFlags.Result64Bit);

        if (result == Result.NotReady)
        {
            return double.NaN;
        }

        if (result != Result.Success)
        {
            if ((int)result < 0)
            {
                TraceVulkanFailure(result);
                _gpuProfilingEnabled = false;
            }

            return double.NaN;
        }

        var delta = ComputeTimestampDelta(timestamps[0], timestamps[1], _devicePolicy.GraphicsQueueTimestampValidBits);
        if (delta is 0)
        {
            return 0;
        }

        return delta * ((double)_devicePolicy.TimestampPeriodNanoseconds / 1000.0);
    }

    private static ulong ComputeTimestampDelta(ulong start, ulong end, uint validBits)
    {
        if (validBits is > 0 and < 64)
        {
            var mask = (1UL << (int)validBits) - 1UL;
            var maskedStart = start & mask;
            var maskedEnd = end & mask;
            return maskedEnd >= maskedStart
                ? maskedEnd - maskedStart
                : (mask - maskedStart + 1UL) + maskedEnd;
        }

        return end >= start ? end - start : 0;
    }

    private void EmitCommandDiag(string message)
    {
        if (string.IsNullOrWhiteSpace(_commandDiagLogPath))
        {
            Console.WriteLine($"[duxel-vk-cmd] {message}");
            return;
        }

        var path = _commandDiagLogPath!;
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        lock (CommandDiagLogLock)
        {
            File.AppendAllText(path, $"[duxel-vk-cmd] {message}{Environment.NewLine}");
        }
    }

    private bool ShouldEmitCommandDiagFrame()
    {
        if (!_commandDiagEnabled)
        {
            return false;
        }

        _commandDiagFrameCounter++;
        if (_commandDiagFrameCounter % _commandDiagLogInterval != 0)
        {
            return false;
        }

        return (_commandDiagFrameCounter / _commandDiagLogInterval) <= _commandDiagFrameLimit;
    }

    private static string GetCommandDiagPipelineLabel(
        UiDrawCommandKind kind,
        bool isFontCommand)
    {
        return kind switch
        {
            UiDrawCommandKind.Triangles when isFontCommand => "font",
            UiDrawCommandKind.Triangles => "tri",
            UiDrawCommandKind.RectFilledPrimitives => "rect",
            UiDrawCommandKind.CircleFilledPrimitives => "circle",
            _ => kind.ToString(),
        };
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
            sb.Append("DUXEL_VK_UPLOAD_QUEUE: ").AppendLine(Environment.GetEnvironmentVariable("DUXEL_VK_UPLOAD_QUEUE") ?? "<null>");
            sb.Append("DUXEL_VK_PROFILE: ").AppendLine(Environment.GetEnvironmentVariable("DUXEL_VK_PROFILE") ?? "<null>");
            sb.Append("DUXEL_VK_GPU_PROFILE: ").AppendLine(Environment.GetEnvironmentVariable("DUXEL_VK_GPU_PROFILE") ?? "<null>");
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

}
