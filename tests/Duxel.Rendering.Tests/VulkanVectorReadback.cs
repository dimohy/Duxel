using Duxel.Core;

namespace Duxel.Vulkan;

public sealed unsafe partial class VulkanRendererBackend
{
    private UiTextureId? _validationVectorTexture;
    private int _validationVectorWidth, _validationVectorHeight;

    partial void SelectValidationVectorImageUsage(ref ImageUsageFlags usage) => usage |= ImageUsageFlags.TransferSrcBit;

    internal byte[] RenderVectorAndReadback(UiDrawData data, UiTextureId texture, int width, int height)
    {
        if (_validationBytes != 0 || _validationVectorTexture is not null)
            throw new InvalidOperationException("A validation capture is already active.");
        if (width <= 0 || height <= 0 || (long)width * height > 16 * 1024 * 1024)
            throw new ArgumentOutOfRangeException(nameof(width));

        var available = _vectorSceneOutputs.TryGetValue(texture, out var existing) && existing.Initialized;
        var actualWidth = existing?.Width ?? 0;
        var actualHeight = existing?.Height ?? 0;
        foreach (var update in data.TextureUpdates)
        {
            if (update.TextureId != texture)
                continue;
            available = update.Kind == UiTextureUpdateKind.RenderVectorScene && update.VectorScene is not null;
            actualWidth = update.Width;
            actualHeight = update.Height;
        }
        if (!available || width != actualWidth || height != actualHeight)
            throw new ArgumentException("Vector readback requires the exact current or submitted output extent.", nameof(texture));

        _validationVectorTexture = texture;
        _validationVectorWidth = width;
        _validationVectorHeight = height;
        try
        {
            return RenderAndReadback(data);
        }
        finally
        {
            _validationVectorTexture = null;
            _validationVectorWidth = _validationVectorHeight = 0;
        }
    }

    private void RecordValidationVectorReadback(CommandBuffer command, UiTextureId texture)
    {
        var source = _vectorSceneOutputs[texture];
        if (!source.Initialized || source.Width != _validationVectorWidth || source.Height != _validationVectorHeight)
            throw new InvalidOperationException("The vector output changed after readback preflight.");
        // Compute work buffers retire after image publication; read the retained texture itself.
        var image = _textures[texture].Image;
        var barrier = PaintGroupBarrier(image, ImageLayout.ShaderReadOnlyOptimal, ImageLayout.TransferSrcOptimal,
            AccessFlags.ShaderReadBit | AccessFlags.TransferWriteBit, AccessFlags.TransferReadBit);
        _vk.CmdPipelineBarrier(command, PipelineStageFlags.FragmentShaderBit | PipelineStageFlags.TransferBit,
            PipelineStageFlags.TransferBit, 0, 0, null, 0, null, 1, &barrier);
        var region = new BufferImageCopy
        {
            ImageSubresource = new() { AspectMask = ImageAspectFlags.ColorBit, LayerCount = 1 },
            ImageExtent = new((uint)source.Width, (uint)source.Height, 1)
        };
        _vk.CmdCopyImageToBuffer(command, image, ImageLayout.TransferSrcOptimal, _validationBuffer, 1, &region);
        barrier = PaintGroupBarrier(image, ImageLayout.TransferSrcOptimal, ImageLayout.ShaderReadOnlyOptimal,
            AccessFlags.TransferReadBit, AccessFlags.ShaderReadBit);
        _vk.CmdPipelineBarrier(command, PipelineStageFlags.TransferBit,
            PipelineStageFlags.FragmentShaderBit | PipelineStageFlags.ComputeShaderBit, 0, 0, null, 0, null, 1, &barrier);
        VectorSceneBarrier(command, PipelineStageFlags.TransferBit, AccessFlags.TransferWriteBit,
            PipelineStageFlags.HostBit, AccessFlags.HostReadBit);
    }
}
