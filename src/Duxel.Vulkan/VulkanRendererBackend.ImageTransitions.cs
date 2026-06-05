using System;
using System.Collections.Generic;

namespace Duxel.Vulkan;

public sealed unsafe partial class VulkanRendererBackend
{
    private readonly List<Image> _pendingTextureShaderReadTransitions = new();
    private readonly HashSet<ulong> _pendingTextureShaderReadTransitionImages = new();

    private int _profileImageTransitionCount;
    private int _profileImageTransitionToTransferDstCount;
    private int _profileImageTransitionToShaderReadCount;
    private int _profileImageTransitionPresentCount;
    private int _profileImageTransitionColorAttachmentCount;
    private int _profileImageTransitionTransferStageCompatibleCount;
    private int _profileImageTransitionGraphicsStageRequiredCount;
    private long _profileImageTransitionTicks;

    private readonly record struct ImageLayoutTransitionBarrier(
        AccessFlags SourceAccess,
        AccessFlags DestinationAccess,
        PipelineStageFlags SourceStage,
        PipelineStageFlags DestinationStage,
        bool TransferQueueStageCompatible);

    private void ResetImageTransitionProfileCounters()
    {
        _profileImageTransitionCount = 0;
        _profileImageTransitionToTransferDstCount = 0;
        _profileImageTransitionToShaderReadCount = 0;
        _profileImageTransitionPresentCount = 0;
        _profileImageTransitionColorAttachmentCount = 0;
        _profileImageTransitionTransferStageCompatibleCount = 0;
        _profileImageTransitionGraphicsStageRequiredCount = 0;
        _profileImageTransitionTicks = 0L;
    }

    private ImageLayout PrepareTextureUploadTransferLayout(Image image, ImageLayout initialLayout)
    {
        if (!UsesDedicatedTransferUploadQueue())
        {
            return initialLayout;
        }

        if (IsPendingTextureShaderReadTransition(image))
        {
            return ImageLayout.TransferDstOptimal;
        }

        if (initialLayout != ImageLayout.ShaderReadOnlyOptimal)
        {
            return initialLayout;
        }

        var prepareCommandBuffer = BeginTextureUploadPrepareCommands();
        TransitionImageLayout(
            prepareCommandBuffer,
            image,
            ImageLayout.ShaderReadOnlyOptimal,
            ImageLayout.TransferDstOptimal);
        return ImageLayout.TransferDstOptimal;
    }

    private void CompleteTextureUploadTransferLayout(CommandBuffer commandBuffer, Image image)
    {
        if (!UsesDedicatedTransferUploadQueue())
        {
            TransitionImageLayout(commandBuffer, image, ImageLayout.TransferDstOptimal, ImageLayout.ShaderReadOnlyOptimal);
            return;
        }

        QueuePendingTextureShaderReadTransition(image);
    }

    private void RecordPendingTextureShaderReadTransitions(CommandBuffer commandBuffer)
    {
        if (_pendingTextureShaderReadTransitions.Count is 0)
        {
            return;
        }

        for (var i = 0; i < _pendingTextureShaderReadTransitions.Count; i++)
        {
            TransitionImageLayout(
                commandBuffer,
                _pendingTextureShaderReadTransitions[i],
                ImageLayout.TransferDstOptimal,
                ImageLayout.ShaderReadOnlyOptimal);
        }

        _pendingTextureShaderReadTransitions.Clear();
        _pendingTextureShaderReadTransitionImages.Clear();
    }

    private void QueuePendingTextureShaderReadTransition(Image image)
    {
        if (_pendingTextureShaderReadTransitionImages.Add(image.Handle))
        {
            _pendingTextureShaderReadTransitions.Add(image);
        }
    }

    private bool IsPendingTextureShaderReadTransition(Image image)
    {
        return _pendingTextureShaderReadTransitionImages.Contains(image.Handle);
    }

    private void ForgetPendingTextureShaderReadTransition(Image image)
    {
        if (!_pendingTextureShaderReadTransitionImages.Remove(image.Handle))
        {
            return;
        }

        for (var i = 0; i < _pendingTextureShaderReadTransitions.Count; i++)
        {
            if (_pendingTextureShaderReadTransitions[i].Handle != image.Handle)
            {
                continue;
            }

            _pendingTextureShaderReadTransitions.RemoveAt(i);
            return;
        }
    }

    private void TransitionImageLayout(CommandBuffer commandBuffer, Image image, ImageLayout oldLayout, ImageLayout newLayout)
    {
        var transitionStart = BeginFrameProfileTiming(_profilingEnabled);
        var transition = ResolveImageLayoutTransitionBarrier(oldLayout, newLayout);
        var barrier = new ImageMemoryBarrier
        {
            SType = StructureType.ImageMemoryBarrier,
            OldLayout = oldLayout,
            NewLayout = newLayout,
            SrcAccessMask = transition.SourceAccess,
            DstAccessMask = transition.DestinationAccess,
            SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
            DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
            Image = image,
            SubresourceRange = new ImageSubresourceRange
            {
                AspectMask = ImageAspectFlags.ColorBit,
                BaseMipLevel = 0,
                LevelCount = 1,
                BaseArrayLayer = 0,
                LayerCount = 1,
            },
        };

        _vk.CmdPipelineBarrier(
            commandBuffer,
            transition.SourceStage,
            transition.DestinationStage,
            0,
            0,
            null,
            0,
            null,
            1,
            &barrier
        );

        RecordImageLayoutTransitionProfile(
            oldLayout,
            newLayout,
            transition.TransferQueueStageCompatible,
            EndFrameProfileTiming(_profilingEnabled, transitionStart));
    }

    private void RecordImageLayoutTransitionProfile(
        ImageLayout oldLayout,
        ImageLayout newLayout,
        bool transferQueueStageCompatible,
        long transitionTicks)
    {
        if (!_profilingEnabled)
        {
            return;
        }

        _profileImageTransitionCount++;
        _profileImageTransitionTicks += transitionTicks;

        if (newLayout == ImageLayout.TransferDstOptimal)
        {
            _profileImageTransitionToTransferDstCount++;
        }

        if (newLayout == ImageLayout.ShaderReadOnlyOptimal)
        {
            _profileImageTransitionToShaderReadCount++;
        }

        if (oldLayout == ImageLayout.PresentSrcKhr || newLayout == ImageLayout.PresentSrcKhr)
        {
            _profileImageTransitionPresentCount++;
        }

        if (oldLayout == ImageLayout.ColorAttachmentOptimal || newLayout == ImageLayout.ColorAttachmentOptimal)
        {
            _profileImageTransitionColorAttachmentCount++;
        }

        if (transferQueueStageCompatible)
        {
            _profileImageTransitionTransferStageCompatibleCount++;
        }
        else
        {
            _profileImageTransitionGraphicsStageRequiredCount++;
        }
    }

    private static ImageLayoutTransitionBarrier ResolveImageLayoutTransitionBarrier(
        ImageLayout oldLayout,
        ImageLayout newLayout)
    {
        if (oldLayout == ImageLayout.Undefined && newLayout == ImageLayout.TransferDstOptimal)
        {
            return CreateImageLayoutTransitionBarrier(
                0,
                AccessFlags.TransferWriteBit,
                PipelineStageFlags.TopOfPipeBit,
                PipelineStageFlags.TransferBit);
        }

        if (oldLayout == ImageLayout.ShaderReadOnlyOptimal && newLayout == ImageLayout.TransferDstOptimal)
        {
            return CreateImageLayoutTransitionBarrier(
                AccessFlags.ShaderReadBit,
                AccessFlags.TransferWriteBit,
                PipelineStageFlags.FragmentShaderBit,
                PipelineStageFlags.TransferBit);
        }

        if (oldLayout == ImageLayout.TransferDstOptimal && newLayout == ImageLayout.ShaderReadOnlyOptimal)
        {
            return CreateImageLayoutTransitionBarrier(
                AccessFlags.TransferWriteBit,
                AccessFlags.ShaderReadBit,
                PipelineStageFlags.TransferBit,
                PipelineStageFlags.FragmentShaderBit);
        }

        if (oldLayout == ImageLayout.PresentSrcKhr && newLayout == ImageLayout.TransferSrcOptimal)
        {
            return CreateImageLayoutTransitionBarrier(
                AccessFlags.MemoryReadBit,
                AccessFlags.TransferReadBit,
                PipelineStageFlags.BottomOfPipeBit,
                PipelineStageFlags.TransferBit);
        }

        if (oldLayout == ImageLayout.TransferSrcOptimal && newLayout == ImageLayout.PresentSrcKhr)
        {
            return CreateImageLayoutTransitionBarrier(
                AccessFlags.TransferReadBit,
                0,
                PipelineStageFlags.TransferBit,
                PipelineStageFlags.BottomOfPipeBit);
        }

        if (oldLayout == ImageLayout.Undefined && newLayout == ImageLayout.ColorAttachmentOptimal)
        {
            return CreateImageLayoutTransitionBarrier(
                0,
                AccessFlags.ColorAttachmentWriteBit,
                PipelineStageFlags.TopOfPipeBit,
                PipelineStageFlags.ColorAttachmentOutputBit);
        }

        if (oldLayout == ImageLayout.ColorAttachmentOptimal && newLayout == ImageLayout.TransferSrcOptimal)
        {
            return CreateImageLayoutTransitionBarrier(
                AccessFlags.ColorAttachmentWriteBit,
                AccessFlags.TransferReadBit,
                PipelineStageFlags.ColorAttachmentOutputBit,
                PipelineStageFlags.TransferBit);
        }

        if (oldLayout == ImageLayout.TransferSrcOptimal && newLayout == ImageLayout.ColorAttachmentOptimal)
        {
            return CreateImageLayoutTransitionBarrier(
                AccessFlags.TransferReadBit,
                AccessFlags.ColorAttachmentWriteBit,
                PipelineStageFlags.TransferBit,
                PipelineStageFlags.ColorAttachmentOutputBit);
        }

        if (oldLayout == ImageLayout.TransferDstOptimal && newLayout == ImageLayout.PresentSrcKhr)
        {
            return CreateImageLayoutTransitionBarrier(
                AccessFlags.TransferWriteBit,
                0,
                PipelineStageFlags.TransferBit,
                PipelineStageFlags.BottomOfPipeBit);
        }

        throw new InvalidOperationException("Unsupported image layout transition.");
    }

    private static ImageLayoutTransitionBarrier CreateImageLayoutTransitionBarrier(
        AccessFlags sourceAccess,
        AccessFlags destinationAccess,
        PipelineStageFlags sourceStage,
        PipelineStageFlags destinationStage)
    {
        return new ImageLayoutTransitionBarrier(
            sourceAccess,
            destinationAccess,
            sourceStage,
            destinationStage,
            IsTransferQueueStageCompatible(sourceStage)
                && IsTransferQueueStageCompatible(destinationStage));
    }

    private static bool IsTransferQueueStageCompatible(PipelineStageFlags stage)
    {
        const PipelineStageFlags transferQueueStages =
            PipelineStageFlags.TopOfPipeBit
            | PipelineStageFlags.TransferBit
            | PipelineStageFlags.BottomOfPipeBit;

        return (stage & ~transferQueueStages) == 0;
    }
}
