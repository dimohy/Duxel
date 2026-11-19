using Duxel.Core;
using System.Numerics;

namespace Duxel.Vulkan;

public sealed unsafe partial class VulkanRendererBackend
{
    private Pipeline _depthReadPipeline;
    private Pipeline _depthWritePipeline;
    private Image[] _depthLayerImages = [];
    private ImageView[] _depthLayerViews = [];
    private DeviceMemory[] _depthLayerMemory = [];
    private ulong _depthLayerAllocatedBytes;
    private readonly record struct DepthLayerScope(int Id, UiRect? Viewport);
    private readonly HashSet<DepthLayerScope> _frameDepthScopes = [];
    private bool _frameUsesDepthLayers;

    private bool PrepareDepthLayers(UiDrawData drawData)
    {
        var usesDepth = false;
        _frameDepthScopes.Clear();
        DepthLayerScope? previousScope = null;
        for (var i = 0; i < drawData.DrawLists.Count; i++)
        {
            if (drawData.DrawLists[i].Scene3D is { } scene)
            {
                if (IsScreenAlignedScene(scene) && !scene.WriteDepth)
                {
                    continue;
                }
                usesDepth = true;
                var scope = new DepthLayerScope(scene.DepthScopeId, scene.Viewport);
                if (previousScope != scope)
                {
                    if (!_frameDepthScopes.Add(scope))
                        throw new InvalidOperationException("Draw lists in a 3D depth scope must be submitted contiguously.");
                    previousScope = scope;
                }
            }
        }

        if (!usesDepth)
        {
            return false;
        }

        if (_depthReadPipeline.Handle is 0)
        {
            _vk.GetPhysicalDeviceProperties(_physicalDevice, out var properties);
            if ((properties.Limits.FramebufferDepthSampleCounts & _msaaSampleCount) == 0)
                throw new NotSupportedException("The selected MSAA count is not supported by depth attachments.");
            CreateGraphicsPipeline(createDepthVariants: true);
        }

        if (_depthLayerImages.Length != 0)
        {
            return true;
        }

        // Depth is cleared at every rendering and never presented or preserved.
        // One graphics-queue attachment suffices: the early/late dependency below
        // orders the preceding rendering's reads/writes before the next clear.
        // Retirement remains tied to the latest submitted graphics fence.
        _depthLayerImages = new Image[1];
        _depthLayerViews = new ImageView[1];
        _depthLayerMemory = new DeviceMemory[1];
        for (var i = 0; i < _depthLayerImages.Length; i++)
        {
            var imageInfo = new ImageCreateInfo
            {
                SType = StructureType.ImageCreateInfo,
                ImageType = ImageType.Type2D,
                Extent = new Extent3D(_swapchainExtent.Width, _swapchainExtent.Height, 1),
                MipLevels = 1,
                ArrayLayers = 1,
                Format = Format.D32Sfloat,
                Tiling = ImageTiling.Optimal,
                InitialLayout = ImageLayout.Undefined,
                Usage = ImageUsageFlags.DepthStencilAttachmentBit,
                Samples = _msaaSampleCount,
                SharingMode = SharingMode.Exclusive,
            };
            Image image = default;
            Check(_vk.CreateImage(_device, &imageInfo, null, &image));
            _depthLayerImages[i] = image;
            _vk.GetImageMemoryRequirements(_device, image, out var requirements);
            _depthLayerAllocatedBytes += requirements.Size;
            var allocation = new MemoryAllocateInfo
            {
                SType = StructureType.MemoryAllocateInfo,
                AllocationSize = requirements.Size,
                MemoryTypeIndex = FindMemoryType(requirements.MemoryTypeBits, MemoryPropertyFlags.DeviceLocalBit),
            };
            DeviceMemory memory = default;
            Check(_vk.AllocateMemory(_device, &allocation, null, &memory));
            _depthLayerMemory[i] = memory;
            Check(_vk.BindImageMemory(_device, image, memory, 0));
            var viewInfo = new ImageViewCreateInfo
            {
                SType = StructureType.ImageViewCreateInfo,
                Image = image,
                ViewType = ImageViewType.Type2D,
                Format = Format.D32Sfloat,
                SubresourceRange = new ImageSubresourceRange
                {
                    AspectMask = ImageAspectFlags.DepthBit,
                    LevelCount = 1,
                    LayerCount = 1,
                },
            };
            ImageView view = default;
            Check(_vk.CreateImageView(_device, &viewInfo, null, &view));
            _depthLayerViews[i] = view;
        }
        return true;
    }

    private RenderingAttachmentInfo PrepareDepthLayerAttachment(CommandBuffer commandBuffer)
    {
        var barrier = new ImageMemoryBarrier
        {
            SType = StructureType.ImageMemoryBarrier,
            SrcAccessMask = AccessFlags.DepthStencilAttachmentReadBit | AccessFlags.DepthStencilAttachmentWriteBit,
            DstAccessMask = AccessFlags.DepthStencilAttachmentReadBit | AccessFlags.DepthStencilAttachmentWriteBit,
            OldLayout = ImageLayout.Undefined,
            NewLayout = ImageLayout.DepthStencilAttachmentOptimal,
            SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
            DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
            Image = _depthLayerImages[0],
            SubresourceRange = new ImageSubresourceRange
            {
                AspectMask = ImageAspectFlags.DepthBit,
                LevelCount = 1,
                LayerCount = 1,
            },
        };
        var depthStages = PipelineStageFlags.EarlyFragmentTestsBit | PipelineStageFlags.LateFragmentTestsBit;
        _vk.CmdPipelineBarrier(commandBuffer, depthStages, depthStages, 0, 0, null, 0, null, 1, &barrier);
        return new RenderingAttachmentInfo
        {
            SType = StructureType.RenderingAttachmentInfo,
            ImageView = _depthLayerViews[0],
            ImageLayout = ImageLayout.DepthStencilAttachmentOptimal,
            LoadOp = AttachmentLoadOp.Clear,
            StoreOp = AttachmentStoreOp.DontCare,
            ClearValue = new ClearValue { DepthStencil = new ClearDepthStencilValue { Depth = 1f } },
        };
    }

    private readonly record struct LayerViewportTransform(UiRect ScreenClip, UiRect PixelViewport, Matrix4x4 Transform,
        UiLayerPlaneModel? PlaneModel = null);
    private bool _hasNormalizedPlaneCamera;
    private Matrix4x4 _planeCamera;
    private LayerViewportTransform _normalizedPlaneCamera;

    private LayerViewportTransform GetLayerViewportTransform(UiLayer3D? scene3D, in CommandFrameContext context)
    {
        var displayWidth = 2f / context.ScaleX;
        var displayHeight = 2f / context.ScaleY;
        var displayBounds = new UiRect(context.ClipOffsetX, context.ClipOffsetY, displayWidth, displayHeight);
        var viewport = scene3D?.Viewport ?? displayBounds;
        var planeModel = scene3D?.PlaneModel;
        var camera = scene3D?.Transform ?? default;
        if (planeModel.HasValue && _hasNormalizedPlaneCamera && _planeCamera == camera
            && _normalizedPlaneCamera.ScreenClip == viewport)
            return _normalizedPlaneCamera with
            {
                PlaneModel = planeModel
            };
        var pixelViewport = new UiRect(
            (viewport.X - context.ClipOffsetX) * context.ClipScaleX,
            (viewport.Y - context.ClipOffsetY) * context.ClipScaleY,
            viewport.Width * context.ClipScaleX, viewport.Height * context.ClipScaleY);
        var transform = camera;
        if (scene3D?.Viewport is { } localViewport)
        {
            var normalize = Matrix4x4.CreateScale(displayWidth / localViewport.Width, displayHeight / localViewport.Height, 1f)
                * Matrix4x4.CreateTranslation(
                    (displayWidth + 2f * (context.ClipOffsetX - localViewport.X)) / localViewport.Width - 1f,
                    (displayHeight + 2f * (context.ClipOffsetY - localViewport.Y)) / localViewport.Height - 1f, 0f);
            transform = normalize * transform;
        }
        var result = new LayerViewportTransform(viewport, pixelViewport, transform, planeModel);
        if (planeModel.HasValue)
        {
            _hasNormalizedPlaneCamera = true;
            _planeCamera = camera;
            _normalizedPlaneCamera = result;
        }
        return result;
    }

    private static Matrix4x4 GetEffectiveLayerTransform(in LayerViewportTransform view, in CommandFrameContext context)
        => view.PlaneModel is { } model
            ? Matrix4x4.CreateTranslation(model.Translation.X * context.ScaleX, model.Translation.Y * context.ScaleY, 0f)
                * view.Transform * Matrix4x4.CreateTranslation(0f, 0f, model.DepthOffset)
            : view.Transform;

    private void ApplyDepthLayerState(UiLayer3D? scene3D, in CommandFrameContext context, in LayerViewportTransform layerView, ref CommandPipelineState state, bool compiled = false, bool sharedBasis = false, bool sharedCamera = false)
    {
        state.LayerScreenClip = layerView.ScreenClip;
        var pixelViewport = layerView.PixelViewport;
        if (!state.HasViewport || state.ViewportPixels != pixelViewport)
        {
            var nativeViewport = new Viewport(pixelViewport.X, pixelViewport.Y, pixelViewport.Width, pixelViewport.Height, 0f, 1f);
            _vk.CmdSetViewport(context.CommandBuffer, 0, 1, &nativeViewport);
            state.ViewportPixels = pixelViewport;
            state.HasViewport = true;
        }
        var presentationOnly = scene3D is { WriteDepth: false } presentation && IsScreenAlignedScene(presentation);
        if (scene3D is { } scene && !presentationOnly)
        {
            var scope = new DepthLayerScope(scene.DepthScopeId, scene.Viewport);
            if (!state.HasDepthScope || state.DepthScope != scope)
            {
                if (state.HasDepthScope || scene.Viewport.HasValue || scene.DepthScopeId != 0)
                    ClearDepthLayerScope(context, pixelViewport);
                state.DepthScope = scope;
                state.HasDepthScope = true;
            }
        }
        var mode = presentationOnly ? 0 : scene3D.HasValue ? (scene3D.Value.WriteDepth ? 2 : 1) + (sharedCamera ? 6 : sharedBasis ? 4 : compiled ? 2 : 0) : 0;
        if (state.DepthMode != mode)
        {
            var pipeline = GetUnmaskedPipeline(mode);
            _vk.CmdBindPipeline(context.CommandBuffer, PipelineBindPoint.Graphics, pipeline);
            state.DepthMode = mode;
            state.CircleCutoutEnabled = false;
            state.BackdropCompositeEnabled = false;
            state.ProjectedCaptureEnabled = false;
        }
        var transform = compiled ? layerView.Transform : GetEffectiveLayerTransform(in layerView, in context);
        if (presentationOnly)
        {
            // For an ordered, screen-aligned plane with no depth writes, its
            // constant in-range Z cannot affect coverage. Reuse the common
            // pipeline and hardware scissor rather than a redundant shader variant.
            state.Layer3DEnabled = false;
            return;
        }
        state.LayerTransform = transform;
        state.LayerModelTranslation = default;
        state.LayerModelDepth = 0;
        if (!state.HasLayerTransform || state.Layer3DEnabled != scene3D.HasValue
            || state.PushedLayerTransform != transform)
        {
            if (!state.HasLayerLocalClip)
            {
                var initialClip = default(UiRect);
                _vk.CmdPushConstants(context.CommandBuffer, _pipelineLayout, ShaderStageFlags.FragmentBit, 112, 16, &initialClip);
                state.HasLayerLocalClip = true;
                state.LayerLocalClip = initialClip;
            }
            var stages = ShaderStageFlags.VertexBit | ShaderStageFlags.FragmentBit;
            _vk.CmdPushConstants(context.CommandBuffer, _pipelineLayout, stages, 48, 64, &transform);
            _frameLayerTransformPushes++;
            state.PushedLayerTransform = transform;
            state.HasLayerTransform = true;
            state.Layer3DEnabled = scene3D.HasValue;
        }
    }

    private void ClearDepthLayerScope(in CommandFrameContext context, UiRect viewportPixels)
    {
        var left = Math.Max(0, (int)MathF.Floor(viewportPixels.X));
        var top = Math.Max(0, (int)MathF.Floor(viewportPixels.Y));
        var right = Math.Min(context.FramebufferWidthPixels, (int)MathF.Ceiling(viewportPixels.X + viewportPixels.Width));
        var bottom = Math.Min(context.FramebufferHeightPixels, (int)MathF.Ceiling(viewportPixels.Y + viewportPixels.Height));
        if (_damagePartial)
        {
            left = Math.Max(left, _damageArea.Offset.X);
            top = Math.Max(top, _damageArea.Offset.Y);
            right = Math.Min(right, _damageArea.Offset.X + (int)_damageArea.Extent.Width);
            bottom = Math.Min(bottom, _damageArea.Offset.Y + (int)_damageArea.Extent.Height);
        }
        if (right <= left || bottom <= top)
            return;
        var attachment = new ClearAttachment
        {
            AspectMask = ImageAspectFlags.DepthBit,
            ClearValue = new ClearValue { DepthStencil = new ClearDepthStencilValue { Depth = 1f } },
        };
        var rect = new ClearRect
        {
            Rect = new Rect2D(new Offset2D(left, top), new Extent2D((uint)(right - left), (uint)(bottom - top))),
            LayerCount = 1,
        };
        _vk.CmdClearAttachments(context.CommandBuffer, 1, &attachment, 1, &rect);
    }

    private void DestroyDepthLayerImages()
    {
        for (var i = 0; i < _depthLayerImages.Length; i++)
        {
            if (_depthLayerViews[i].Handle is not 0)
                _vk.DestroyImageView(_device, _depthLayerViews[i], null);
            if (_depthLayerImages[i].Handle is not 0)
                _vk.DestroyImage(_device, _depthLayerImages[i], null);
            if (_depthLayerMemory[i].Handle is not 0)
                _vk.FreeMemory(_device, _depthLayerMemory[i], null);
        }
        _depthLayerImages = [];
        _depthLayerViews = [];
        _depthLayerMemory = [];
        _depthLayerAllocatedBytes = 0;
    }
}
