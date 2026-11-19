using Duxel.Core;
using System.Diagnostics;
using VulkanSemaphore = Duxel.Vulkan.Semaphore;

namespace Duxel.Vulkan;

public sealed unsafe partial class VulkanRendererBackend
{
    private int _frameIndex;
    partial void ObserveValidationWait(string phase);
    partial void ObserveValidationFrameAttempt(UiDrawData drawData);
    private int _lastImageIndex = -1;
    private long _profileGpuQueryReadTicks, _profileTextureRetireTicks, _profileBufferRetireTicks;

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

    private static PlatformSize GetDrawFramebufferSize(UiDrawData drawData)=>new(
        (int)MathF.Round(drawData.DisplaySize.X*drawData.FramebufferScale.X),
        (int)MathF.Round(drawData.DisplaySize.Y*drawData.FramebufferScale.Y));

    private bool TryEnsureFrameTarget(UiDrawData drawData)
    {
        if (drawData.DisplaySize.X <= 0 || drawData.DisplaySize.Y <= 0)
        {
            return false;
        }

        var expectedFramebuffer=GetDrawFramebufferSize(drawData);
        var expectedFbWidth=expectedFramebuffer.Width;
        var expectedFbHeight=expectedFramebuffer.Height;
        var committed=_platform.FramebufferSize;
        // A queued UI snapshot may predate the latest native resize. Do not
        // recreate the surface for that stale frame or paint it at another extent.
        if(expectedFbWidth!=committed.Width||expectedFbHeight!=committed.Height)return false;
        var hasFramebufferSizeMismatch = expectedFbWidth != (int)_swapchainExtent.Width
            || expectedFbHeight != (int)_swapchainExtent.Height;
        bool FitsPresentationCapacity()=>_oneToOnePresentation&&UsesPresentationCapacity()
            &&expectedFbWidth<=(int)_swapchainExtent.Width&&expectedFbHeight<=(int)_swapchainExtent.Height;
        if ((hasFramebufferSizeMismatch&&!FitsPresentationCapacity())||_swapchainRecreationPending)
        {
            if (!TryRecreateSwapchain())
            {
                return false;
            }
            // The native extent can advance while creating the new swapchain.
            if((expectedFbWidth!=(int)_swapchainExtent.Width||expectedFbHeight!=(int)_swapchainExtent.Height)&&!FitsPresentationCapacity())return false;
        }

        var fbWidth = (int)_swapchainExtent.Width;
        var fbHeight = (int)_swapchainExtent.Height;
        if (fbWidth <= 0 || fbHeight <= 0)
        {
            return false;
        }

        _frameRenderExtent=new((uint)expectedFbWidth,(uint)expectedFbHeight);

        TrimMsaaTargetAfterResize();

        // A frame without draw geometry still has to clear and present the swapchain image.
        return true;
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
        var queryReadStart = BeginFrameProfileTiming(_profilingEnabled);
        if (_gpuProfilingEnabled)
        {
            gpuRenderUs = TryReadGpuTimestampResult(frameData);
            frameData.TimestampQueryIssued = false;
        }
        _profileGpuQueryReadTicks = EndFrameProfileTiming(_profilingEnabled, queryReadStart);
        var retireStart = BeginFrameProfileTiming(_profilingEnabled);
        FlushRetiredTexturesAfterFrameFence(frameSlot);
        _profileTextureRetireTicks = EndFrameProfileTiming(_profilingEnabled, retireStart);
        retireStart = BeginFrameProfileTiming(_profilingEnabled);
        FlushRetiredBuffersAfterFrameFence(frameSlot);
        RecycleVectorWorkAfterFrameFence(frameSlot);
        _profileBufferRetireTicks = EndFrameProfileTiming(_profilingEnabled, retireStart);

        // Graphics completion proves that this frame's acquire wait was consumed.
        var imageAvailable = _frameSemaphores[frameSlot].ImageAvailable;

        var acquireStart = BeginFrameProfileTiming(_profilingEnabled);
        if (!TryAcquireFrameImage(imageAvailable, out var imageIndex))
        {
            return false;
        }
        // Reacquiring this image orders the previous present's semaphore wait.
        // A frame/ring index alone cannot prove presentation-engine completion.
        var semaphores = new FrameSemaphores(imageAvailable,_frameSemaphores[imageIndex].RenderFinished);
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
            ObserveValidationWait("frame-fence");
            var waitResult = _vk.WaitForFences(_device, 1, fence, true, _platform.IsInteractingResize ? 0UL : ulong.MaxValue);
            ObserveValidationWait("frame-fence-complete");
            if (waitResult is Result.Timeout or Result.NotReady)
            {
                return false;
            }

            Check(waitResult);
            RecordCompletedGraphicsFrame(frameData);
            FlushRetiredRenderTargets(frameData);
            CollectRetiredPresentations(frameData);
            return true;
        }
    }

    private bool TryAcquireFrameImage(VulkanSemaphore imageAvailable, out uint imageIndex)
    {
        imageIndex = 0;
        var notReady=false;
        OverrideValidationAcquireNotReady(ref notReady);
        if(notReady)return false;
        var acquiredImageIndex = 0u;
        var timeout = _platform.IsInteractingResize ? 0UL : ulong.MaxValue;
        ObserveValidationWait("acquire-image");
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
            _ = TryRecreateSwapchain();
            // Re-enter with a semaphore from the new swapchain generation.
            return false;
        }

        if (acquireResult != Result.Success && !IsSuboptimal(acquireResult))
        {
            Check(acquireResult);
        }

        ObserveValidationWait("acquire-image-complete");
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
            ObserveValidationWait("image-fence");
            Check(_vk.WaitForFences(_device, 1, fencePtr, true, ulong.MaxValue));
            ObserveValidationWait("image-fence-complete");
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

    private long BeginFrameProfileTiming(bool profileEnabled)
    {
        return profileEnabled && _cpuTimingsThisFrame ? Stopwatch.GetTimestamp() : 0L;
    }

    private static long EndFrameProfileTiming(bool profileEnabled, long startTicks)
    {
        return profileEnabled && startTicks != 0 ? Stopwatch.GetTimestamp() - startTicks : 0L;
    }

    private unsafe long SubmitFrame(
        CommandBuffer commandBuffer,
        VulkanSemaphore imageAvailable,
        VulkanSemaphore renderFinished,
        Fence inFlightFence)
    {
        SynchronizeUploadsBeforeFrame();

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
        var submitStart = BeginFrameProfileTiming(_profilingEnabled);
        Check(_vk.QueueSubmit(_graphicsQueue, 1, &submitInfo, inFlightFence));
        return _profilingEnabled ? EndFrameProfileTiming(true, submitStart) : 0L;
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

        var presentStart = BeginFrameProfileTiming(_profilingEnabled);
        var rejected=false;
        RejectValidationPresentation(ref rejected);
        var presentFence=default(Fence);
        var presentFenceInfo=new SwapchainPresentFenceInfoEXT
        {
            SType=StructureType.SwapchainPresentFenceInfoEXT,SwapchainCount=1,PFences=&presentFence,
        };
        if(_swapchainMaintenanceEnabled&&!rejected)
        {
            presentFence=RentPresentFence();presentInfo.PNext=&presentFenceInfo;
        }
        ObserveValidationWait("queue-present");
        var presentResult = rejected?Result.ErrorOutOfDateKhr:_khrSwapchain.QueuePresent(_graphicsQueue, &presentInfo);
        ObserveValidationWait("queue-present-complete");
        if(presentFence.Handle!=0)
        {
            if(presentResult is Result.Success or Result.SuboptimalKhr or Result.ErrorOutOfDateKhr or Result.ErrorSurfaceLostKhr)
                _activePresentFences.Add(presentFence);
            else _vk.DestroyFence(_device,presentFence,null);
        }
        presentTicks = _profilingEnabled ? EndFrameProfileTiming(true, presentStart) : 0L;
        return presentResult;
    }

    private void HandleFramePresentResult(Result presentResult)
    {
        if (IsSuboptimal(presentResult) && (_platform.IsInteractingResize
            ||(_oneToOnePresentation&&UsesPresentationCapacity())))
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

    partial void RejectValidationPresentation(ref bool rejected);
    partial void OverrideValidationAcquireNotReady(ref bool notReady);

    private void EmitFrameProfile(ulong drawDataFrameId, uint imageIndex, bool presented, in FrameProfileState profile)
    {
        LogProfileFrame(
            drawDataFrameId,
            imageIndex,
            presented,
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
            RecordCompletedGraphicsFrame(frame);
        }
    }

    private bool CompleteRecordedFrame(
        ActiveFrameContext frameContext,
        CommandBuffer commandBuffer,
        ref FrameProfileState profile)
    {
        var semaphores = frameContext.Semaphores;
        // A render fence does not prove the presentation engine consumed its
        // wait semaphore. Reacquiring this image does, so key present waits by image.
        var renderFinished = _frameSemaphores[frameContext.ImageIndex].RenderFinished;
        profile.SubmitTicks = SubmitFrame(
            commandBuffer,
            semaphores.ImageAvailable,
            renderFinished,
            frameContext.FrameData.InFlight);
        _lastSubmittedFrame=frameContext.FrameData;
        frameContext.FrameData.SubmittedSequence=++_submittedFrameSequence;
        ObservePresentationAcquireWait(frameContext.ImageIndex,frameContext.FrameData);

        var presentResult = PresentFrame(frameContext.ImageIndex, renderFinished, out profile.PresentTicks);
        var framePresented = presentResult == Result.Success || IsSuboptimal(presentResult);
        if(framePresented)ObserveAcceptedPresentation(frameContext.ImageIndex);
        HandleFramePresentResult(presentResult);

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
            out frameData.ProfiledVectorCounts,
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

