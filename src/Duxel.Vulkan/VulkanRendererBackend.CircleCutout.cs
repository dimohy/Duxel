namespace Duxel.Vulkan;

public sealed unsafe partial class VulkanRendererBackend
{
    private ShaderModule _circleCutoutVertexShaderModule;
    private ShaderModule _circleCutoutFragmentShaderModule;
    private readonly Pipeline[] _circleCutoutPipelines = new Pipeline[9];
    private readonly Pipeline[] _materialDepthMaskPipelines = new Pipeline[4];
    partial void ObserveValidationDepthMask4(int mode, bool selected);
    private readonly Pipeline[] _projectedCapturePipelines = new Pipeline[2];
    private bool _projectedPaintCapture;
    private readonly Pipeline[] _backdropPipelines = new Pipeline[18];
    private ShaderModule _backdropFragmentShaderModule;
    private ShaderModule _backdropCutoutFragmentShaderModule;

    private Pipeline GetUnmaskedPipeline(int mode) => mode switch
    {
        0 => _graphicsPipeline,
        1 => _depthReadPipeline,
        2 => _depthWritePipeline,
        3 => _compiledDepthReadPipeline,
        4 => _compiledDepthWritePipeline,
        5 => _sharedBasisDepthReadPipeline,
        6 => _sharedBasisDepthWritePipeline,
        7 => _sharedCameraDepthReadPipeline,
        8 => _sharedCameraDepthWritePipeline,
        _ => throw new ArgumentOutOfRangeException(nameof(mode))
    };

    private void ApplyPaintPipeline(CommandBuffer commandBuffer, bool cutout, bool backdrop, ref CommandPipelineState state, bool opaqueMaterial = false)
    {
        var mode = state.DepthMode;
        if (backdrop && mode is 2 or 4 or 6 or 8)
            throw new InvalidOperationException("Backdrop composition requires a depth-read material.");
        var projected = _projectedPaintCapture && !backdrop;
        if (projected && mode != 0)
            throw new InvalidOperationException("Projected capture requires flat source geometry.");
        var useMaterialDepthMask = opaqueMaterial && !cutout && !backdrop && !projected
            && mode is 2 or 4 or 6 or 8 && _materialDepthMaskPipelines[(mode - 2) / 2].Handle != 0;
        if (state.MaterialDepthMask == useMaterialDepthMask && state.CircleCutoutEnabled == cutout && state.BackdropCompositeEnabled == backdrop
            && state.ProjectedCaptureEnabled == projected)
            return;
        var pipeline = useMaterialDepthMask
            ? _materialDepthMaskPipelines[(mode - 2) / 2]
            : GetUnmaskedPipeline(mode);
        if (projected)
        {
            var index = cutout ? 1 : 0;
            if (_projectedCapturePipelines[index].Handle == 0)
                CreateGraphicsPipeline(circleCutout: cutout, projectedCapture: true);
            pipeline = _projectedCapturePipelines[index];
        }
        else if (backdrop)
        {
            var slot = mode + (cutout ? 9 : 0);
            if (_backdropPipelines[slot].Handle == 0)
                CreateGraphicsPipeline(createDepthVariants: mode != 0, compiledLayers: mode >= 3,
                    sharedBasis: mode is 5 or 6, sharedCamera: mode is 7 or 8, circleCutout: cutout,
                    backdropComposite: true);
            pipeline = _backdropPipelines[slot];
        }
        else if (cutout)
        {
            if (_circleCutoutPipelines[mode].Handle == 0)
                CreateGraphicsPipeline(createDepthVariants: mode != 0, compiledLayers: mode >= 3,
                    sharedBasis: mode is 5 or 6, sharedCamera: mode is 7 or 8, circleCutout: true);
            pipeline = _circleCutoutPipelines[mode];
        }
        _vk.CmdBindPipeline(commandBuffer, PipelineBindPoint.Graphics, pipeline);
        ObserveValidationDepthMask4(mode, useMaterialDepthMask);
        state.MaterialDepthMask = useMaterialDepthMask;
        state.CircleCutoutEnabled = cutout;
        state.BackdropCompositeEnabled = backdrop;
        state.ProjectedCaptureEnabled = projected;
    }

    private uint CommandTextureIndex(in Duxel.Core.UiDrawCommand command, in TextureResource black, bool font)
    {
        if (command.WhiteBackdropTextureId is not { } whiteId)
            return black.SlotIndex | (font ? SubpixelCoverageModeBit : 0u)
                | (_colorImageTextures.Contains(command.TextureId) ? PremultipliedImageModeBit : 0u);
        if (!_textures.TryGetValue(whiteId, out var white))
            throw new InvalidOperationException("Backdrop composition white reference texture is missing.");
        return black.SlotIndex | (white.SlotIndex << 16);
    }
}
