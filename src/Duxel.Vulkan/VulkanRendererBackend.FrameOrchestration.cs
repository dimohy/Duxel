using System.Diagnostics;
using Duxel.Core;
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
            double gpuRenderUs)
        {
            FrameSlot = frameSlot;
            FrameData = frameData;
            Semaphores = semaphores;
            ImageIndex = imageIndex;
            GpuRenderUs = gpuRenderUs;
        }

        public int FrameSlot { get; }
        public FrameResources FrameData { get; }
        public FrameSemaphores Semaphores { get; }
        public uint ImageIndex { get; }
        public double GpuRenderUs { get; }
    }

    private struct FrameProfileState
    {
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
        if (expectedFbWidth != (int)_swapchainExtent.Width || expectedFbHeight != (int)_swapchainExtent.Height)
        {
            if (!TryRecreateSwapchain())
            {
                return false;
            }

            if (expectedFbWidth != (int)_swapchainExtent.Width || expectedFbHeight != (int)_swapchainExtent.Height)
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

        var frameCount = _frames.Length;
        if (frameCount is 0 || _frameSemaphores.Length is 0)
        {
            return false;
        }

        var frameSlot = _frameIndex % frameCount;
        var frameData = _frames[frameSlot];
        WaitForFrameFence(frameData);

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

        if (!TryAcquireFrameImage(semaphores.ImageAvailable, out var imageIndex))
        {
            return false;
        }

        _lastImageIndex = (int)imageIndex;
        WaitForFrameImageFence(imageIndex, frameData);

        context = new ActiveFrameContext(frameSlot, frameData, semaphores, imageIndex, gpuRenderUs);
        return true;
    }

    private void WaitForFrameFence(FrameResources frameData)
    {
        fixed (Fence* fence = &frameData.InFlight)
        {
            Check(_vk.WaitForFences(_device, 1, fence, true, ulong.MaxValue));
        }
    }

    private bool TryAcquireFrameImage(VulkanSemaphore imageAvailable, out uint imageIndex)
    {
        imageIndex = 0;
        var acquiredImageIndex = 0u;
        var acquireResult = _khrSwapchain.AcquireNextImage(
            _device,
            _swapchain,
            ulong.MaxValue,
            imageAvailable,
            default,
            &acquiredImageIndex);
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
                ulong.MaxValue,
                imageAvailable,
                default,
                &acquiredImageIndex);
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

    private void WaitForFrameImageFence(uint imageIndex, FrameResources frameData)
    {
        if (_imagesInFlight.Length is 0)
        {
            return;
        }

        var imageFence = _imagesInFlight[imageIndex];
        if (imageFence.Handle is not 0 && imageFence.Handle != frameData.InFlight.Handle)
        {
            var fencePtr = stackalloc Fence[1];
            fencePtr[0] = imageFence;
            Check(_vk.WaitForFences(_device, 1, fencePtr, true, ulong.MaxValue));
        }

        _imagesInFlight[imageIndex] = frameData.InFlight;
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
}
