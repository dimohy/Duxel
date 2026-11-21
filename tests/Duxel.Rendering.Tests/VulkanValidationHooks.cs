using System.Runtime.CompilerServices;
using Duxel.Core;

[assembly: InternalsVisibleTo("Duxel.Rendering.Tests")]
[assembly: InternalsVisibleTo("Duxel.Graphics.Tests")]
[assembly: InternalsVisibleTo("Duxel.Vulkan.Linux.Tests")]
[assembly: InternalsVisibleTo("Duxel.Platform.Linux.Tests")]

namespace Duxel.Vulkan;

public sealed unsafe partial class VulkanRendererBackend
{
    internal long? ValidationNativePresentationSequence;
    internal bool ValidationReuseReadback;
    internal bool ValidationViewportClipPlanes => _viewportClipPlanesEnabled;
    internal bool ValidationDepthMask4Supported => CanUseDepthMask4;
    internal bool ValidationStandardSampleLocations
    {
        get
        {
            _vk.GetPhysicalDeviceProperties(_physicalDevice, out var properties);
            return properties.Limits.StandardSampleLocations != 0;
        }
    }
    partial void SelectValidationViewportClipPlanes(ref bool enabled)
        => enabled &= Environment.GetEnvironmentVariable("DUXEL_TEST_DISABLE_VIEWPORT_CLIP") != "1";
    partial void SelectValidationStandardSampleLocations(ref bool enabled)
        => enabled &= Environment.GetEnvironmentVariable("DUXEL_TEST_DISABLE_STANDARD_SAMPLE_LOCATIONS") != "1";
    internal Action<int, bool>? ValidationDepthMask4;
    partial void ObserveValidationDepthMask4(int mode, bool selected)
    {
        ValidationDepthMask4?.Invoke(mode, selected);
        if (ValidationTraceDepthMask4 && mode is 2 or 4 or 6 or 8)
            Console.Error.WriteLine($"[depth-material] mode={mode} explicit={selected}");
    }
    private static readonly bool ValidationTraceDepthMask4 =
        Environment.GetEnvironmentVariable("DUXEL_TRACE_MATERIAL_DEPTH_MASK") == "1";
    internal Action? ValidationBeforePresentation;
    private static readonly bool ValidationBlockingUpload = Environment.GetEnvironmentVariable("DUXEL_TEST_BLOCKING_UPLOAD") == "1";
    partial void RequireValidationUploadFrameWait(ref bool required) => required |= ValidationBlockingUpload;
    internal Action<UiDrawCommand, Rect2D, Rect2D>? ValidationScissor;

    partial void ObserveValidationScissor(in UiDrawCommand command, int x, int y, uint width, uint height)
    {
        ValidationScissor?.Invoke(command, new(new(x, y), new(width, height)), _damageArea);
    }
    private byte[]? _validationPresentedPixels;
    private UiTextureId? _validationPresentedPixelTexture;
    private ulong _validationPresentedPixelSequence;
    internal int ValidationLastAcquiredImageIndex => _lastImageIndex;

    internal (int Count, ulong Bytes, long Reuses) ValidationTextTexturePool
        => (_idleTextTextures.Count, _idleTextTextureBytes, _textTextureReuses);

    internal ulong ValidationTextureAllocationBytes(UiTextureId id) => _textures[id].AllocationBytes;

    internal (int Active, ulong ActiveBytes, int Pending, ulong PendingBytes) ValidationSquareTextureAllocations(int firstExtent, int secondExtent)
    {
        var result = (Active: 0, ActiveBytes: 0UL, Pending: 0, PendingBytes: 0UL);
        foreach (var texture in _textures.Values)
        {
            if (!IsFixtureTexture(texture))
                continue;
            result.Active++;
            result.ActiveBytes += texture.AllocationBytes;
        }
        foreach (var frame in _frames)
            foreach (var entry in frame.PendingTextureDestroys)
            {
                if (!IsFixtureTexture(entry.Resource))
                    continue;
                result.Pending++;
                result.PendingBytes += entry.Resource.AllocationBytes;
            }
        return result;

        bool IsFixtureTexture(TextureResource texture) =>
            texture.Width == texture.Height && (texture.Width == firstExtent || texture.Width == secondExtent);
    }

    internal bool ValidationDamageOwnershipIsConsistent()
    {
        if (_damageCurrent.Count != 0 || _damageSnapshotPrepared || _damageSnapshotUsable)
            return false;
        var readers = new Dictionary<DamageCommand, int>();
        foreach (var image in _damageImages)
            foreach (var command in image.Commands)
                readers[command] = readers.GetValueOrDefault(command) + 1;
        foreach (var command in _damageLatest)
            readers[command] = readers.GetValueOrDefault(command) + 1;
        foreach (var pair in readers)
            if (pair.Key.Readers != pair.Value)
                return false;
        var free = new HashSet<DamageCommand>();
        foreach (var command in _damageFree)
            if (command.Readers != 0 || readers.ContainsKey(command) || !free.Add(command))
                return false;
        return _damageFree.Count <= (long)_damageLatest.Count * (_damageImages.Length + 1);
    }


    partial void SelectValidationPresentationReuse(ref bool allowed)
    {
        if (ValidationFullRepaint)
            allowed = false;
        if (_validationBytes != 0 && (!ValidationReuseReadback || _validationPresentedPixels is null
            || _validationPresentedPixels.Length != _validationBytes
            || _validationPresentedPixelSequence != _presentationSequence
            || _validationPresentedPixelTexture != _validationVectorTexture))
            allowed = false;
    }

    partial void OverrideValidationPresentationSequence(ref long? sequence)
    {
        if (ValidationNativePresentationSequence is { } token)
            sequence = token;
    }

    internal static string ValidationWaitPhase = "not-started";
    internal static bool ValidationResizeDiagnostics;
    internal static int ValidationSwapchainCreates;
    internal static string ValidationResizeSnapshot = "not-started";
    internal static string? ValidationResizeEventPath;
    internal static StreamWriter? ValidationResizeEventWriter;
    internal string ValidationPresentationScaling()
    {
        string Describe(PresentModeKHR mode)
        {
            var s = QueryPresentationScaling(mode);
            return $"mode={mode};scaling={s.SupportedPresentScaling};gravityX={s.SupportedPresentGravityX};gravityY={s.SupportedPresentGravityY};min={s.MinScaledImageExtent.Width}x{s.MinScaledImageExtent.Height};max={s.MaxScaledImageExtent.Width}x{s.MaxScaledImageExtent.Height}";
        }
        return Describe(_activePresentMode) + Environment.NewLine + Describe(PresentModeKHR.FifoKhr);
    }
    internal static bool ValidationResizablePresentationCapacity, ValidationExactPresentation;
    internal bool ValidationOneToOnePresentation => _oneToOnePresentation;
    internal (uint Width, uint Height) ValidationPresentationExtent => (_swapchainExtent.Width, _swapchainExtent.Height);
    internal void ValidationReplacePresentation() => _swapchainRecreationPending = true;
    partial void SelectValidationPresentationCapacity(ref bool live)
    {
        if (ValidationExactPresentation)
            live = false;
        else if (ValidationResizablePresentationCapacity)
            live = true;
    }
    partial void SelectValidationOneToOnePresentation(ref bool enabled)
    {
        if (ValidationExactPresentation)
            enabled = false;
    }
    internal static int ValidationResizeEventBudget = 2048;
    private int _validationResizeEventCount;
    private long _validationResizeDiagnosticTime;
    partial void ObserveValidationFrameAttempt(UiDrawData drawData)
    {
        if (!ValidationResizeDiagnostics)
            return;
        var now = System.Diagnostics.Stopwatch.GetTimestamp();
        if (now - _validationResizeDiagnosticTime < System.Diagnostics.Stopwatch.Frequency)
            return;
        _validationResizeDiagnosticTime = now;
        var fences = new List<string>();
        foreach (var frame in _frames)
        {
            var fence = frame.InFlight;
            fences.Add($"{frame.SubmittedSequence}:{_vk.WaitForFences(_device, 1, &fence, true, 0)}");
        }
        ValidationResizeSnapshot = $"ui={drawData.DisplaySize} scale={drawData.FramebufferScale} native={_platform.FramebufferSize} swap={_swapchainExtent.Width}x{_swapchainExtent.Height} resize={_platform.IsInteractingResize} submitted={_submittedFrameSequence} completed={_completedFrameSequence} fences={string.Join(',', fences)} retired={_retiredPresentations.Count} pending={_pendingRenderTargetRetirements} bytes={_pendingRenderTargetBytes}";
        Console.WriteLine("Resize GPU: " + ValidationResizeSnapshot);
    }
    partial void ObserveValidationWait(string phase)
    {
        if (phase == "queue-present")
            ValidationBeforePresentation?.Invoke();
        if (phase == "create-swapchain-complete")
            Interlocked.Increment(ref ValidationSwapchainCreates);
        System.Threading.Volatile.Write(ref ValidationWaitPhase, phase);
        if (!ValidationResizeDiagnostics || ValidationResizeEventPath is not { } path
            || phase is not ("surface-capabilities" or "surface-formats" or "present-modes" or "create-swapchain"))
            return;
        if (++_validationResizeEventCount > ValidationResizeEventBudget)
            throw new InvalidOperationException($"Focused WSI diagnostic budget exhausted{ValidationResizeEventBudget}events.");
        var fences = new List<string>();
        foreach (var frame in _frames)
        {
            var fence = frame.InFlight;
            fences.Add($"{frame.SubmittedSequence}:{_vk.WaitForFences(_device, 1, &fence, true, 0)}");
        }
        var present = "none";
        if (_activePresentFences.Count > 0)
        {
            var fence = _activePresentFences[^1];
            present = _vk.WaitForFences(_device, 1, &fence, true, 0).ToString();
        }
        (ValidationResizeEventWriter ?? throw new InvalidOperationException("Resize diagnostic writer is not open.")).WriteLine($"{DateTime.UtcNow:O} entry={phase} age={_frameIndex} submitted={_submittedFrameSequence} completed={_completedFrameSequence} graphics={string.Join(',', fences)} lastPresent={present} retired={_retiredPresentations.Count} pending={_pendingRenderTargetRetirements} native={_platform.FramebufferSize} swap={_swapchainExtent.Width}x{_swapchainExtent.Height}");
    }
    internal bool ValidationGraphicsBusy;
    internal ulong ValidationSwapchainHandle => _swapchain.Handle;
    partial void OverrideValidationGraphicsReady(ref bool ready)
    {
        if (ValidationGraphicsBusy)
            ready = false;
    }
    internal int ValidationFrameResourceCount => _frames.Length;
    internal int ValidationDepthAttachmentCount => _depthLayerImages.Length;
    internal int ValidationRequestedMsaaSamples => _options.MsaaSamples;
    internal static bool ValidationImageAcquisitionOnly;
    internal static bool ValidationResizableMsaaCapacity;
    internal (uint Width, uint Height, ulong Bytes) ValidationMsaaTarget => (_msaaColorExtent.Width, _msaaColorExtent.Height, _msaaColorAllocationBytes);
    partial void SelectValidationMsaaCapacity(ref bool live)
    {
        if (ValidationResizableMsaaCapacity)
            live = true;
    }
    partial void SelectValidationSwapchainMaintenance(ref bool enabled)
    {
        if (ValidationImageAcquisitionOnly)
            enabled = false;
    }
    internal int ValidationAcquireSemaphoreIndex => _frameIndex % _frames.Length;
    internal int ValidationFrameAge => _frameIndex;
    internal int ValidationAcquireNotReadyAttempts
    {
        get; set;
    }
    partial void OverrideValidationAcquireNotReady(ref bool notReady)
    {
        notReady = ValidationAcquireNotReadyAttempts > 0;
        if (notReady)
            ValidationAcquireNotReadyAttempts--;
    }
    internal bool ValidationRejectNextPresentation
    {
        get; set;
    }
    partial void RejectValidationPresentation(ref bool rejected)
    {
        rejected = ValidationRejectNextPresentation;
        ValidationRejectNextPresentation = false;
    }
    internal void ValidationInvalidateDamage() => _damageEpoch++;
    internal int ValidationSwapchainImageCount => _swapchainImages.Length;
    internal bool ValidationFullRepaint;
    internal long ValidationRepaintBorderPixels => CountRepaintBorderPixels();
    partial void OverrideValidationDamage(ref UiRect dirty, in UiRect full)
    {
        if (ValidationFullRepaint)
            dirty = full;
        ValidationFullRepaint = false;
    }
    internal ulong ValidationStagingCapacity => (ulong)_stagingBufferSize;
    internal (bool Partial, bool Empty, uint Width, uint Height) ValidationDamage()
        => (_damagePartial, _damageEmpty, _damageArea.Extent.Width, _damageArea.Extent.Height);
    internal (int Commands, long Bytes, bool ReadersValid) ValidationDamageMemory()
    {
        var nodes = new HashSet<DamageCommand>(_damageLatest);
        foreach (var image in _damageImages)
            nodes.UnionWith(image.Commands);
        nodes.UnionWith(_damageFree);
        var bytes = 0L;
        var valid = true;
        foreach (var node in nodes)
        {
            bytes += node.Geometry.Length + node.Indices.Length;
            var readers = _damageLatest.Count(candidate => ReferenceEquals(candidate, node));
            foreach (var image in _damageImages)
                readers += image.Commands.Count(candidate => ReferenceEquals(candidate, node));
            valid &= node.Readers == readers && (_damageFree.Contains(node) == (readers == 0));
        }
        return (nodes.Count, bytes, valid);
    }
    internal static string ValidationFragmentShaderSha256 =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(FragmentShaderSpirv));
    internal static string ValidationParticleShaderSha256 =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(LoadParticleShader()));

    static partial void OverrideValidationParticleShader(ref byte[] shader)
    {
        var path = Environment.GetEnvironmentVariable("DUXEL_VALIDATION_PARTICLE_SHADER");
        if (path is not null)
            shader = File.ReadAllBytes(path);
    }

    static partial void OverrideValidationFragmentShader(ref byte[] shader)
    {
        var path = Environment.GetEnvironmentVariable("DUXEL_VALIDATION_FRAGMENT_SHADER");
        if (path is not null)
            shader = File.ReadAllBytes(path);
    }

    static partial void OverrideValidationOpaqueCoverage(ref bool enabled)
    {
        enabled = Environment.GetEnvironmentVariable("DUXEL_VALIDATION_OPAQUE_COVERAGE") switch
        {
            null => enabled,
            "0" => false,
            "1" => enabled,
            _ => throw new InvalidOperationException("DUXEL_VALIDATION_OPAQUE_COVERAGE must be 0 or 1."),
        };
    }

    internal static int ValidationParseCompiledIndexLimit() => ParseCompiledIndexLimit();
    internal static bool ValidationParseAxisAlignedCulling() => ParseAxisAlignedLayerCulling();
    internal static int ValidationClassifyLayerBounds(UiRect bounds, UiVector2 translation,
        System.Numerics.Matrix4x4 transform, UiRect viewport, bool classifyInside, float depthOffset, bool specialized)
    {
        var context = new CommandFrameContext(default, 0, default, 0,
            2f / viewport.Width, 2f / viewport.Height, -1f, -1f, 0, 0, 0, 0, 1, 1,
            new(viewport.Width, viewport.Height), (int)viewport.Width, (int)viewport.Height, false, false);
        return (int)ClassifyLayerBounds(bounds, translation, in context, in transform, viewport,
            classifyInside, depthOffset, specialized);
    }
    internal static bool ValidationFrameBindingsMatch()
    {
        var bindings = new StaticGeometryFrameBindings();
        int[] sizes = [0, 1, 5000, 3, 0, 512, 17, 5000, 0];
        foreach (var size in sizes)
        {
            bindings.Reset(size);
            for (var i = 0; i < 5001; i++)
                if (bindings.ContainsKey(i) || bindings.TryGetValue(i, out _))
                    return false;
            var expected = new Dictionary<int, StaticGeometryBuffer>();
            for (var i = 0; i < size; i += 3)
            {
                var buffer = StaticGeometryBuffer.Empty with
                {
                    ContentHash = (ulong)i + 1
                };
                bindings[i] = buffer;
                expected.Add(i, buffer);
            }
            for (var i = -1; i <= 5000; i++)
            {
                var found = bindings.TryGetValue(i, out var actual);
                if (found != expected.TryGetValue(i, out var value) || found != bindings.ContainsKey(i))
                    return false;
                if (found && (!ReferenceEquals(actual, value) || !ReferenceEquals(bindings[i], value)))
                    return false;
            }
            bindings.Clear();
            for (var i = 0; i < size; i++)
                if (bindings.TryGetValue(i, out _))
                    return false;
        }
        return true;
    }
    internal static bool ValidationParseCpuTimingSampling() => ParseCpuTimingSampling();
    internal (long Start, long Elapsed, long EmptyElapsed) ValidationCpuTimingClock()
    {
        var start = BeginFrameProfileTiming(true);
        return (start, EndFrameProfileTiming(true, start), EndFrameProfileTiming(true, 0));
    }
    private Buffer _validationBuffer;
    private DeviceMemory _validationMemory;
    private int _validationBytes;
    private int _validationCaptureStride, _validationCaptureIndex, _validationCaptureCount;
    private Rect2D _validationCaptureRect;

    internal void BeginValidationCapture(int x, int y, int width, int height, int count)
    {
        if (_validationBytes != 0)
            throw new InvalidOperationException("A validation capture is already active.");
        _validationCaptureRect = new(new(x, y), new((uint)width, (uint)height));
        _validationCaptureStride = checked(width * height * 4);
        _validationCaptureCount = count;
        _validationCaptureIndex = 0;
        _validationBytes = checked(_validationCaptureStride * count);
        CreateBuffer((nuint)_validationBytes, BufferUsageFlags.TransferDstBit,
            MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit,
            out _validationBuffer, out _validationMemory);
    }

    internal byte[][] EndValidationCapture()
    {
        try
        {
            WaitForAllInFlightFrameFences();
            if (_validationCaptureIndex != _validationCaptureCount)
                throw new InvalidOperationException("Incomplete asynchronous validation capture.");
            void* mapped;
            Check(_vk.MapMemory(_device, _validationMemory, 0, (nuint)_validationBytes, 0, &mapped));
            try
            {
                var frames = new byte[_validationCaptureCount][];
                for (var frame = 0; frame < frames.Length; frame++)
                {
                    frames[frame] = new ReadOnlySpan<byte>((byte*)mapped + frame * _validationCaptureStride, _validationCaptureStride).ToArray();
                    if (_swapchainFormat is Format.B8G8R8A8Unorm or Format.B8G8R8A8Srgb)
                        for (var i = 0; i < frames[frame].Length; i += 4)
                            (frames[frame][i], frames[frame][i + 2]) = (frames[frame][i + 2], frames[frame][i]);
                }
                return frames;
            }
            finally { _vk.UnmapMemory(_device, _validationMemory); }
        }
        finally
        {
            _vk.DestroyBuffer(_device, _validationBuffer, null);
            _vk.FreeMemory(_device, _validationMemory, null);
            _validationBuffer = default;
            _validationMemory = default;
            _validationBytes = 0;
            _validationCaptureStride = _validationCaptureIndex = _validationCaptureCount = 0;
            _validationCaptureRect = default;
        }
    }
    internal static bool ValidationCommandSpanProfilesMatch()
    {
        UiDrawCommandKind[] kinds = [UiDrawCommandKind.Triangles, UiDrawCommandKind.RectFilledPrimitives, UiDrawCommandKind.CircleFilledPrimitives];
        var commands = new UiDrawCommand[6];
        static CompiledCommandSpan Summarize(ReadOnlySpan<UiDrawCommand> commands)
        {
            var profile = new CommandRecordProfileState();
            var first = default(UiDrawCommandKind);
            foreach (ref readonly var command in commands)
            {
                if (command.ElementCount == 0)
                    continue;
                if (profile.CommandCount == 0)
                    first = command.Kind;
                RecordCommandProfile(in command, true, ref profile);
            }
            return new(profile.CommandCount, first, profile.LastRecordedKind,
                profile.TriangleToPrimitiveTransitionCount, profile.PrimitiveToTriangleTransitionCount, profile.RectCircleTransitionCount);
        }
        for (var pattern = 0; pattern < 729; pattern++)
        {
            var code = pattern;
            for (var i = 0; i < commands.Length; i++)
            {
                commands[i] = default(UiDrawCommand) with
                {
                    Kind = kinds[code % 3],
                    ElementCount = i == 2 ? 0u : 3u
                };
                code /= 3;
            }
            var expected = Summarize(commands);
            for (var split = 0; split <= commands.Length; split++)
            {
                var merged = new CommandRecordProfileState();
                var first = Summarize(commands.AsSpan(0, split));
                var second = Summarize(commands.AsSpan(split));
                RecordCompiledSpanProfile(in first, ref merged);
                RecordCompiledSpanProfile(in second, ref merged);
                if (merged.CommandCount != expected.Count || merged.LastRecordedKind != expected.LastKind
                    || merged.TriangleToPrimitiveTransitionCount != expected.TriangleToPrimitive
                    || merged.PrimitiveToTriangleTransitionCount != expected.PrimitiveToTriangle
                    || merged.RectCircleTransitionCount != expected.RectCircle)
                    return false;
            }
        }
        return true;
    }
    internal bool ValidationSrgb => _swapchainFormat is Format.B8G8R8A8Srgb or Format.R8G8B8A8Srgb;
    internal void ValidationWaitForGraphicsFrames() => WaitForAllInFlightFrameFences();
    internal UiGpuParticle[] ValidationReadParticleState(UiGpuParticleSystem system)
    {
        WaitForAllInFlightFrameFences();
        var source = _particleResources[system];
        var size = checked((nuint)source.Count * 48);
        CreateBuffer(size, BufferUsageFlags.TransferDstBit, MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit,
            out var readBuffer, out var readMemory);
        var poolInfo = new CommandPoolCreateInfo { SType = StructureType.CommandPoolCreateInfo, QueueFamilyIndex = _graphicsQueueFamily };
        CommandPool pool;
        Check(_vk.CreateCommandPool(_device, &poolInfo, null, &pool));
        var fenceInfo = new FenceCreateInfo { SType = StructureType.FenceCreateInfo };
        Fence fence;
        Check(_vk.CreateFence(_device, &fenceInfo, null, &fence));
        try
        {
            var allocate = new CommandBufferAllocateInfo { SType = StructureType.CommandBufferAllocateInfo, CommandPool = pool, Level = CommandBufferLevel.Primary, CommandBufferCount = 1 };
            CommandBuffer command;
            Check(_vk.AllocateCommandBuffers(_device, &allocate, &command));
            BeginCommandBufferRecording(command);
            ParticleBarrier(command, PipelineStageFlags.ComputeShaderBit | PipelineStageFlags.TransferBit,
                AccessFlags.ShaderWriteBit | AccessFlags.TransferWriteBit,
                PipelineStageFlags.TransferBit, AccessFlags.TransferReadBit);
            var copy = new BufferCopy { Size = size };
            _vk.CmdCopyBuffer(command, source.Current.Buffer, readBuffer, 1, &copy);
            ParticleBarrier(command, PipelineStageFlags.TransferBit, AccessFlags.TransferWriteBit,
                PipelineStageFlags.HostBit, AccessFlags.HostReadBit);
            Check(_vk.EndCommandBuffer(command));
            var submit = new SubmitInfo { SType = StructureType.SubmitInfo, CommandBufferCount = 1, PCommandBuffers = &command };
            Check(_vk.QueueSubmit(_graphicsQueue, 1, &submit, fence));
            Check(_vk.WaitForFences(_device, 1, &fence, true, 30_000_000_000));
            void* mapped;
            Check(_vk.MapMemory(_device, readMemory, 0, size, 0, &mapped));
            try
            {
                return new ReadOnlySpan<UiGpuParticle>(mapped, source.Count).ToArray();
            }
            finally { _vk.UnmapMemory(_device, readMemory); }
        }
        finally
        {
            _vk.DestroyFence(_device, fence, null);
            _vk.DestroyCommandPool(_device, pool, null);
            _vk.DestroyBuffer(_device, readBuffer, null);
            _vk.FreeMemory(_device, readMemory, null);
        }
    }
    internal (ulong StagingBytes, ulong DynamicBytes, int Creates, int Updates, int Hits) ValidationUploads
        => (_profileUploadStagingBytes, _profileDynamicGeometryUploadBytes,
            _profileStaticGeometryCreateCount + _profileStaticGeometryReplaceCount,
            _profileStaticGeometryUpdateCount + _profileStaticGeometryReuseCount, _profileStaticGeometryHitCount);

    internal bool ValidationUsesRotatingStaticGeometry => _staticGeometryRotatingUpdateEnabled;
    internal (int Count, ulong Bytes) ValidationExtendedStaticGeometryReuse
        => (_extendedStaticGeometryReuseCount, _extendedStaticGeometryReuseBytes);

    internal int ValidationStaticPageCount => _staticMemoryPages.Count;

    internal (ulong Page, ulong Offset, ulong Length) ValidationStaticMemory(string layerId)
    {
        var slice = _staticGeometryBuffers[$"duxel.layer.static:{layerId}:list:0"].StaticMemory;
        return (slice.Page?.Memory.Handle ?? 0, slice.Offset, slice.Length);
    }

    internal void ValidationRetireUnusedStaticGeometry()
        => PruneUnusedStaticGeometryBuffers(_frameIndex + StaticGeometryLruGraceFrames + 1);

    internal ulong ValidationStaticVertexBuffer(string layerId)
        => _staticGeometryBuffers[$"duxel.layer.static:{layerId}:list:0"].VertexBuffer.Handle;

    internal void ValidationPruneRetiredStaticGeometryAfterFrameGrace(bool elapsed)
    {
        WaitForAllInFlightFrameFences();
        var frame = _frameIndex + _frames.Length + StaticGeometryRetiredReuseGraceFrames;
        PruneRetiredStaticGeometryBuffers(frame, elapsed ? long.MaxValue : 0);
    }

    internal (int Vertices, int Indices, int Primitives, bool Expanded) ValidationResidentShape(UiDrawList list)
    {
        if (!TryGetStaticDrawListTag(list, out var tag, out _))
            throw new ArgumentException("A resident draw list is required.");
        var buffer = _staticGeometryBuffers[tag];
        return (buffer.VertexCount, buffer.IndexCount, buffer.PrimitiveCount, buffer.HasExpandedPrimitiveGeometry);
    }

    internal int ValidationPaintGeometryEntries => _staticGeometryBuffers.Keys.Count(key => key.StartsWith("duxel.paint:", StringComparison.Ordinal));

    internal (ulong Black, ulong White, ulong Msaa) ValidationPaintTargetHandles(UiTextureId black)
    {
        var entry = _paintGroups[black];
        return (_textures[black].Image.Handle, _textures[entry.White].Image.Handle, entry.Msaa.Handle);
    }

    internal byte[] RenderAndReadback(UiDrawData data)
    {
        if (_validationBytes != 0)
            throw new InvalidOperationException("A validation capture is already active.");
        // Readback is deliberately synchronous. The product rendering path
        // instead returns false and asks the app to retry a fresh UI frame.
        WaitForAllInFlightFrameFences();
        if (!TryEnsureFrameTarget(data))
            throw new InvalidOperationException($"Validation frame target is unavailable: draw={data.DisplaySize}, scale={data.FramebufferScale}, native={_platform.FramebufferSize}, swapchain={_swapchainExtent.Width}x{_swapchainExtent.Height}, pending={_swapchainRecreationPending}.");
        _validationBytes = _validationVectorTexture is null
            ? checked((int)(_frameRenderExtent.Width * _frameRenderExtent.Height * 4))
            : checked(_validationVectorWidth * _validationVectorHeight * 4);
        CreateBuffer((nuint)_validationBytes, BufferUsageFlags.TransferDstBit,
            MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit,
            out _validationBuffer, out _validationMemory);
        try
        {
            if (!TryRenderDrawData(data))
                throw new InvalidOperationException("Validation frame was not presented.");
            if (LastFrameMetrics.PresentationReused)
            {
                // Reuse leaves the actual previous presentation untouched. Its
                // independently captured bytes are the pixel witness, not an
                // uninitialized readback buffer or a second rendering.
                return _validationPresentedPixels!.AsSpan().ToArray();
            }
            WaitForAllInFlightFrameFences();
            void* mapped;
            Check(_vk.MapMemory(_device, _validationMemory, 0, (nuint)_validationBytes, 0, &mapped));
            byte[] pixels;
            try
            {
                pixels = new ReadOnlySpan<byte>(mapped, _validationBytes).ToArray();
            }
            finally
            {
                _vk.UnmapMemory(_device, _validationMemory);
            }
            if (_validationVectorTexture is null && (_swapchainFormat is Format.B8G8R8A8Unorm or Format.B8G8R8A8Srgb))
            {
                for (var i = 0; i < pixels.Length; i += 4)
                    (pixels[i], pixels[i + 2]) = (pixels[i + 2], pixels[i]);
            }
            if (ValidationReuseReadback)
            {
                _validationPresentedPixels = pixels.AsSpan().ToArray();
                _validationPresentedPixelTexture = _validationVectorTexture;
                _validationPresentedPixelSequence = _presentationSequence;
            }
            return pixels;
        }
        finally
        {
            _vk.DestroyBuffer(_device, _validationBuffer, null);
            _vk.FreeMemory(_device, _validationMemory, null);
            _validationBuffer = default;
            _validationMemory = default;
            _validationBytes = 0;
        }
    }

    partial void RecordValidationReadback(CommandBuffer commandBuffer, uint imageIndex)
    {
        if (_validationBytes == 0)
            return;
        if (_validationVectorTexture is { } texture)
        {
            RecordValidationVectorReadback(commandBuffer, texture);
            return;
        }
        var barrier = new ImageMemoryBarrier
        {
            SType = StructureType.ImageMemoryBarrier,
            SrcAccessMask = AccessFlags.ColorAttachmentWriteBit,
            DstAccessMask = AccessFlags.TransferReadBit,
            OldLayout = ImageLayout.ColorAttachmentOptimal,
            NewLayout = ImageLayout.TransferSrcOptimal,
            SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
            DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
            Image = _swapchainImages[imageIndex],
            SubresourceRange = new ImageSubresourceRange { AspectMask = ImageAspectFlags.ColorBit, LevelCount = 1, LayerCount = 1 },
        };
        _vk.CmdPipelineBarrier(commandBuffer, PipelineStageFlags.ColorAttachmentOutputBit, PipelineStageFlags.TransferBit,
            0, 0, null, 0, null, 1, &barrier);
        var region = new BufferImageCopy
        {
            ImageSubresource = new ImageSubresourceLayers { AspectMask = ImageAspectFlags.ColorBit, LayerCount = 1 },
            ImageExtent = new Extent3D(_frameRenderExtent.Width, _frameRenderExtent.Height, 1),
        };
        if (_validationCaptureStride != 0)
        {
            if (_validationCaptureIndex >= _validationCaptureCount)
                throw new InvalidOperationException("Validation capture capacity exceeded.");
            region.BufferOffset = checked((ulong)_validationCaptureIndex++ * (ulong)_validationCaptureStride);
            region.ImageOffset = new(_validationCaptureRect.Offset.X, _validationCaptureRect.Offset.Y, 0);
            region.ImageExtent = new(_validationCaptureRect.Extent.Width, _validationCaptureRect.Extent.Height, 1);
        }
        _vk.CmdCopyImageToBuffer(commandBuffer, _swapchainImages[imageIndex], ImageLayout.TransferSrcOptimal,
            _validationBuffer, 1, &region);
        barrier.SrcAccessMask = AccessFlags.TransferReadBit;
        barrier.DstAccessMask = AccessFlags.ColorAttachmentWriteBit;
        barrier.OldLayout = ImageLayout.TransferSrcOptimal;
        barrier.NewLayout = ImageLayout.ColorAttachmentOptimal;
        _vk.CmdPipelineBarrier(commandBuffer, PipelineStageFlags.TransferBit, PipelineStageFlags.ColorAttachmentOutputBit,
            0, 0, null, 0, null, 1, &barrier);
    }
}
