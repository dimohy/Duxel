using Duxel.Core;
using System.Diagnostics;
using VulkanSemaphore = Duxel.Vulkan.Semaphore;

namespace Duxel.Vulkan;

public sealed unsafe partial class VulkanRendererBackend
{
    private int _frameIndex;
    private int _lastImageIndex = -1;

    private readonly struct ActiveFrameContext
    {
        public ActiveFrameContext(
            int frameSlot,
            FrameResources frameData,
            FrameSemaphores semaphores,
            uint imageIndex,
            double gpuRenderUs,
            long beginTicks,
            long frameFenceTicks,
            long acquireTicks,
            long imageFenceTicks)
        {
            FrameSlot = frameSlot;
            FrameData = frameData;
            Semaphores = semaphores;
            ImageIndex = imageIndex;
            GpuRenderUs = gpuRenderUs;
            BeginTicks = beginTicks;
            FrameFenceTicks = frameFenceTicks;
            AcquireTicks = acquireTicks;
            ImageFenceTicks = imageFenceTicks;
        }

        public int FrameSlot { get; }
        public FrameResources FrameData { get; }
        public FrameSemaphores Semaphores { get; }
        public uint ImageIndex { get; }
        public double GpuRenderUs { get; }
        public long BeginTicks { get; }
        public long FrameFenceTicks { get; }
        public long AcquireTicks { get; }
        public long ImageFenceTicks { get; }
    }

    private struct FrameProfileState
    {
        public long TargetTicks;
        public long BeginTicks;
        public long FrameFenceTicks;
        public long AcquireTicks;
        public long ImageFenceTicks;
        public long UploadTicks;
        public long RecordTicks;
        public long RecordTextureLookupTicks;
        public long RecordClippingTicks;
        public long RecordDescriptorBindTicks;
        public long RecordDrawCallTicks;
        public long SubmitTicks;
        public long PresentTicks;
        public double GpuRenderUs;
        public CommandRecordStats RecordStats;
    }

    private bool TryEnsureFrameTarget(UiDrawData drawData)
    {
        if (drawData.DisplaySize.X <= 0 || drawData.DisplaySize.Y <= 0)
        {
            return false;
        }

        var expectedFbWidth = (int)(drawData.DisplaySize.X * drawData.FramebufferScale.X);
        var expectedFbHeight = (int)(drawData.DisplaySize.Y * drawData.FramebufferScale.Y);
        var hasFramebufferSizeMismatch = expectedFbWidth != (int)_swapchainExtent.Width
            || expectedFbHeight != (int)_swapchainExtent.Height;
        var isInteractingResize = _platform.IsInteractingResize;

        if (hasFramebufferSizeMismatch && !isInteractingResize)
        {
            if (!TryRecreateSwapchain())
            {
                return false;
            }
        }

        var fbWidth = (int)_swapchainExtent.Width;
        var fbHeight = (int)_swapchainExtent.Height;
        if (fbWidth <= 0 || fbHeight <= 0)
        {
            return false;
        }

        return HasDrawableGeometry(drawData);
    }

    private static bool HasDrawableGeometry(UiDrawData drawData)
    {
        if (drawData.TotalVertexCount > 0 && drawData.TotalIndexCount > 0)
        {
            return true;
        }

        for (var listIndex = 0; listIndex < drawData.DrawLists.Count; listIndex++)
        {
            var drawList = drawData.DrawLists[listIndex];
            if ((drawList.RectFilledPrimitives?.Count ?? 0) > 0 || (drawList.CircleFilledPrimitives?.Count ?? 0) > 0)
            {
                return true;
            }
        }

        return false;
    }

    private bool TryBeginRenderFrame(out ActiveFrameContext context)
    {
        context = default;
        var beginStart = BeginFrameProfileTiming(_profilingEnabled);

        var frameCount = _frames.Length;
        if (frameCount is 0 || _frameSemaphores.Length is 0)
        {
            return false;
        }

        var frameSlot = _frameIndex % frameCount;
        var frameData = _frames[frameSlot];
        var frameFenceStart = BeginFrameProfileTiming(_profilingEnabled);
        if (!TryWaitForFrameFence(frameData))
        {
            return false;
        }
        var frameFenceTicks = EndFrameProfileTiming(_profilingEnabled, frameFenceStart);

        var gpuRenderUs = double.NaN;
        if (_gpuProfilingEnabled)
        {
            gpuRenderUs = TryReadGpuTimestampResult(frameData);
            frameData.TimestampQueryIssued = false;
        }

        FlushPendingTextureDestroys(frameSlot);
        FlushPendingBufferDestroys(frameSlot);

        var semaphores = _frameSemaphores[_semaphoreIndex];
        _semaphoreIndex = (_semaphoreIndex + 1) % _frameSemaphores.Length;

        var acquireStart = BeginFrameProfileTiming(_profilingEnabled);
        if (!TryAcquireFrameImage(semaphores.ImageAvailable, out var imageIndex))
        {
            return false;
        }
        var acquireTicks = EndFrameProfileTiming(_profilingEnabled, acquireStart);

        _lastImageIndex = (int)imageIndex;
        var imageFenceTicks = WaitForFrameImageFence(imageIndex, frameData);

        var beginTicks = EndFrameProfileTiming(_profilingEnabled, beginStart);
        context = new ActiveFrameContext(
            frameSlot,
            frameData,
            semaphores,
            imageIndex,
            gpuRenderUs,
            beginTicks,
            frameFenceTicks,
            acquireTicks,
            imageFenceTicks);
        return true;
    }

    private bool TryWaitForFrameFence(FrameResources frameData)
    {
        fixed (Fence* fence = &frameData.InFlight)
        {
            var waitResult = _vk.WaitForFences(_device, 1, fence, true, _platform.IsInteractingResize ? 0UL : ulong.MaxValue);
            if (waitResult is Result.Timeout or Result.NotReady)
            {
                return false;
            }

            Check(waitResult);
            return true;
        }
    }

    private bool TryAcquireFrameImage(VulkanSemaphore imageAvailable, out uint imageIndex)
    {
        imageIndex = 0;
        var acquiredImageIndex = 0u;
        var timeout = _platform.IsInteractingResize ? 0UL : ulong.MaxValue;
        var acquireResult = _khrSwapchain.AcquireNextImage(
            _device,
            _swapchain,
            timeout,
            imageAvailable,
            default,
            &acquiredImageIndex);
        if (acquireResult is Result.Timeout or Result.NotReady)
        {
            return false;
        }

        if (acquireResult == Result.ErrorOutOfDateKhr || IsSurfaceLost(acquireResult))
        {
            if (!TryRecreateSwapchain())
            {
                return false;
            }

            acquiredImageIndex = 0u;
            acquireResult = _khrSwapchain.AcquireNextImage(
                _device,
                _swapchain,
                timeout,
                imageAvailable,
                default,
                &acquiredImageIndex);
            if (acquireResult is Result.Timeout or Result.NotReady)
            {
                return false;
            }

            if (acquireResult == Result.ErrorOutOfDateKhr || IsSurfaceLost(acquireResult))
            {
                return false;
            }
        }

        if (acquireResult != Result.Success && !IsSuboptimal(acquireResult))
        {
            Check(acquireResult);
        }

        imageIndex = acquiredImageIndex;
        return true;
    }

    private long WaitForFrameImageFence(uint imageIndex, FrameResources frameData)
    {
        var waitStart = BeginFrameProfileTiming(_profilingEnabled);
        if (_imagesInFlight.Length is 0)
        {
            return EndFrameProfileTiming(_profilingEnabled, waitStart);
        }

        var imageFence = _imagesInFlight[imageIndex];
        if (imageFence.Handle is not 0 && imageFence.Handle != frameData.InFlight.Handle)
        {
            var fencePtr = stackalloc Fence[1];
            fencePtr[0] = imageFence;
            Check(_vk.WaitForFences(_device, 1, fencePtr, true, ulong.MaxValue));
        }

        _imagesInFlight[imageIndex] = frameData.InFlight;
        return EndFrameProfileTiming(_profilingEnabled, waitStart);
    }

    private static FrameProfileState CreateFrameProfileState(double gpuRenderUs)
    {
        return new FrameProfileState
        {
            GpuRenderUs = gpuRenderUs,
        };
    }

    private static long BeginFrameProfileTiming(bool profileEnabled)
    {
        return profileEnabled ? Stopwatch.GetTimestamp() : 0L;
    }

    private static long EndFrameProfileTiming(bool profileEnabled, long startTicks)
    {
        return profileEnabled ? Stopwatch.GetTimestamp() - startTicks : 0L;
    }

    private unsafe long SubmitFrame(
        CommandBuffer commandBuffer,
        VulkanSemaphore imageAvailable,
        VulkanSemaphore renderFinished,
        Fence inFlightFence)
    {
        WaitForPendingUploadWork();

        var waitStage = PipelineStageFlags.ColorAttachmentOutputBit;
        var submitInfo = new SubmitInfo
        {
            SType = StructureType.SubmitInfo,
            WaitSemaphoreCount = 1,
            PWaitSemaphores = &imageAvailable,
            PWaitDstStageMask = &waitStage,
            CommandBufferCount = 1,
            PCommandBuffers = &commandBuffer,
            SignalSemaphoreCount = 1,
            PSignalSemaphores = &renderFinished,
        };

        Check(_vk.ResetFences(_device, 1, &inFlightFence));
        var submitStart = _profilingEnabled ? Stopwatch.GetTimestamp() : 0;
        Check(_vk.QueueSubmit(_graphicsQueue, 1, &submitInfo, inFlightFence));
        return _profilingEnabled ? Stopwatch.GetTimestamp() - submitStart : 0L;
    }

    private unsafe Result PresentFrame(uint imageIndex, VulkanSemaphore renderFinished, out long presentTicks)
    {
        var swapchain = _swapchain;
        var presentInfo = new PresentInfoKHR
        {
            SType = StructureType.PresentInfoKhr,
            WaitSemaphoreCount = 1,
            PWaitSemaphores = &renderFinished,
            SwapchainCount = 1,
            PSwapchains = &swapchain,
            PImageIndices = &imageIndex,
        };

        var presentStart = _profilingEnabled ? Stopwatch.GetTimestamp() : 0;
        var presentResult = _khrSwapchain.QueuePresent(_graphicsQueue, &presentInfo);
        presentTicks = _profilingEnabled ? Stopwatch.GetTimestamp() - presentStart : 0L;
        return presentResult;
    }

    private void HandleFramePresentResult(Result presentResult)
    {
        if (IsSuboptimal(presentResult) && _platform.IsInteractingResize)
        {
            return;
        }

        if (presentResult == Result.ErrorOutOfDateKhr || IsSuboptimal(presentResult) || IsSurfaceLost(presentResult))
        {
            _ = TryRecreateSwapchain();
            return;
        }

        if (presentResult != Result.Success)
        {
            Check(presentResult);
        }
    }

    private void EmitFrameProfileIfEnabled(bool profileEnabled, in FrameProfileState profile)
    {
        if (!profileEnabled)
        {
            return;
        }

        LogProfileFrame(
            profile.TargetTicks,
            profile.BeginTicks,
            profile.FrameFenceTicks,
            profile.AcquireTicks,
            profile.ImageFenceTicks,
            profile.UploadTicks,
            profile.RecordTicks,
            profile.RecordTextureLookupTicks,
            profile.RecordClippingTicks,
            profile.RecordDescriptorBindTicks,
            profile.RecordDrawCallTicks,
            profile.SubmitTicks,
            profile.PresentTicks,
            profile.GpuRenderUs,
            in profile.RecordStats);
    }

    private unsafe void WaitForAllInFlightFrameFences()
    {
        if (_frames.Length == 0)
        {
            return;
        }

        for (var i = 0; i < _frames.Length; i++)
        {
            var frame = _frames[i];
            if (frame is null || frame.InFlight.Handle is 0)
            {
                continue;
            }

            var fence = frame.InFlight;
            Check(_vk.WaitForFences(_device, 1, &fence, true, ulong.MaxValue));
        }
    }

    private bool CompleteRecordedFrame(
        ActiveFrameContext frameContext,
        CommandBuffer commandBuffer,
        bool profileEnabled,
        ref FrameProfileState profile)
    {
        var semaphores = frameContext.Semaphores;
        var renderFinished = semaphores.RenderFinished;
        profile.SubmitTicks = SubmitFrame(
            commandBuffer,
            semaphores.ImageAvailable,
            renderFinished,
            frameContext.FrameData.InFlight);

        var presentResult = PresentFrame(frameContext.ImageIndex, renderFinished, out profile.PresentTicks);
        var framePresented = presentResult == Result.Success || IsSuboptimal(presentResult);
        HandleFramePresentResult(presentResult);
        EmitFrameProfileIfEnabled(profileEnabled, in profile);

        _frameIndex++;
        return framePresented;
    }

    private CommandBuffer RecordFrameCommandsForSubmission(
        UiDrawData drawData,
        uint imageIndex,
        FrameResources frameData,
        in FrameGeometryBuffers geometryBuffers,
        bool profileEnabled,
        ref FrameProfileState profile)
    {
        ResetFrameCommandPool(frameData.CommandPool);

        var commandBuffer = frameData.CommandBuffer;
        var recordStart = BeginFrameProfileTiming(profileEnabled);
        RecordCommandBuffer(
            commandBuffer,
            imageIndex,
            drawData,
            in geometryBuffers,
            _frameStaticBindings,
            _gpuProfilingEnabled ? frameData.TimestampQueryPool : default,
            out profile.RecordTextureLookupTicks,
            out profile.RecordClippingTicks,
            out profile.RecordDescriptorBindTicks,
            out profile.RecordDrawCallTicks,
            out profile.RecordStats);
        MarkFrameGpuTimestampQueryIssuedIfNeeded(frameData);
        profile.RecordTicks = EndFrameProfileTiming(profileEnabled, recordStart);

        return commandBuffer;
    }

    private void ResetFrameCommandPool(CommandPool commandPool)
    {
        Check(_vk.ResetCommandPool(_device, commandPool, 0));
    }

    private void MarkFrameGpuTimestampQueryIssuedIfNeeded(FrameResources frameData)
    {
        if (_gpuProfilingEnabled && frameData.TimestampQueryPool.Handle is not 0)
        {
            frameData.TimestampQueryIssued = true;
        }
    }
}

