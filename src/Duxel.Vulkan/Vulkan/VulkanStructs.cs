using System.Runtime.InteropServices;
using System.Text;

namespace Duxel.Vulkan;

[StructLayout(LayoutKind.Sequential)]
public unsafe struct AllocationCallbacks
{
    public void* PUserData;
    public nint PfnAllocation;
    public nint PfnReallocation;
    public nint PfnFree;
    public nint PfnInternalAllocation;
    public nint PfnInternalFree;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct ExtensionProperties
{
    public fixed byte ExtensionName[256];
    public uint SpecVersion;

    public readonly string GetExtensionName()
    {
        fixed (byte* name = ExtensionName)
        {
            return Marshal.PtrToStringUTF8((nint)name) ?? string.Empty;
        }
    }
}

[StructLayout(LayoutKind.Sequential)] public struct Offset2D(int x, int y) { public int X = x; public int Y = y; }
[StructLayout(LayoutKind.Sequential)] public struct Offset3D(int x, int y, int z) { public int X = x; public int Y = y; public int Z = z; }
[StructLayout(LayoutKind.Sequential)] public struct Extent2D(uint width, uint height) { public uint Width = width; public uint Height = height; }
[StructLayout(LayoutKind.Sequential)] public struct Extent3D(uint width, uint height, uint depth) { public uint Width = width; public uint Height = height; public uint Depth = depth; }
[StructLayout(LayoutKind.Sequential)] public struct Rect2D(Offset2D offset, Extent2D extent) { public Offset2D Offset = offset; public Extent2D Extent = extent; }
[StructLayout(LayoutKind.Sequential)] public struct ComponentMapping(ComponentSwizzle r, ComponentSwizzle g, ComponentSwizzle b, ComponentSwizzle a) { public ComponentSwizzle R = r; public ComponentSwizzle G = g; public ComponentSwizzle B = b; public ComponentSwizzle A = a; }
[StructLayout(LayoutKind.Sequential)] public struct Viewport(float x, float y, float width, float height, float minDepth, float maxDepth) { public float X = x; public float Y = y; public float Width = width; public float Height = height; public float MinDepth = minDepth; public float MaxDepth = maxDepth; }
[StructLayout(LayoutKind.Sequential)] public struct DescriptorPoolSize(DescriptorType type, uint count) { public DescriptorType Type = type; public uint DescriptorCount = count; }
[StructLayout(LayoutKind.Sequential)] public struct SurfaceFormatKHR(Format format, ColorSpaceKHR colorSpace) { public Format Format = format; public ColorSpaceKHR ColorSpace = colorSpace; }
[StructLayout(LayoutKind.Sequential)] public struct ImageSubresourceRange(ImageAspectFlags aspectMask, uint baseMipLevel, uint levelCount, uint baseArrayLayer, uint layerCount) { public ImageAspectFlags AspectMask = aspectMask; public uint BaseMipLevel = baseMipLevel; public uint LevelCount = levelCount; public uint BaseArrayLayer = baseArrayLayer; public uint LayerCount = layerCount; }

[StructLayout(LayoutKind.Explicit)]
public struct ClearColorValue
{
    [FieldOffset(0)] public float Float32_0;
    [FieldOffset(4)] public float Float32_1;
    [FieldOffset(8)] public float Float32_2;
    [FieldOffset(12)] public float Float32_3;

    public ClearColorValue(float r, float g, float b, float a) : this()
    {
        Float32_0 = r;
        Float32_1 = g;
        Float32_2 = b;
        Float32_3 = a;
    }
}

[StructLayout(LayoutKind.Sequential)]
public struct ClearDepthStencilValue
{
    public float Depth;
    public uint Stencil;
}

[StructLayout(LayoutKind.Explicit)]
public struct ClearValue
{
    [FieldOffset(0)] public ClearColorValue Color;
    [FieldOffset(0)] public ClearDepthStencilValue DepthStencil;
}

[StructLayout(LayoutKind.Sequential)]
public struct QueueFamilyProperties
{
    public QueueFlags QueueFlags;
    public uint QueueCount;
    public uint TimestampValidBits;
    public Extent3D MinImageTransferGranularity;
}

[StructLayout(LayoutKind.Sequential)]
public struct MemoryRequirements
{
    public ulong Size;
    public ulong Alignment;
    public uint MemoryTypeBits;
}

[StructLayout(LayoutKind.Sequential)]
public struct MemoryType
{
    public MemoryPropertyFlags PropertyFlags;
    public uint HeapIndex;
}

[StructLayout(LayoutKind.Sequential)]
public struct MemoryHeap
{
    public ulong Size;
    public MemoryHeapFlags Flags;
}

[StructLayout(LayoutKind.Explicit, Size = 520)]
public unsafe struct PhysicalDeviceMemoryProperties
{
    [FieldOffset(0)] public uint MemoryTypeCount;
    [FieldOffset(4)] private fixed byte _memoryTypes[256];
    [FieldOffset(260)] public uint MemoryHeapCount;
    [FieldOffset(264)] private fixed byte _memoryHeaps[256];

    public ref MemoryType GetMemoryType(int index)
    {
        if ((uint)index >= 32u)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        fixed (byte* memoryTypes = _memoryTypes)
        {
            return ref ((MemoryType*)memoryTypes)[index];
        }
    }
}

[StructLayout(LayoutKind.Sequential)]
public struct PhysicalDeviceSparseProperties
{
    public Bool32 ResidencyStandard2DBlockShape;
    public Bool32 ResidencyStandard2DMultisampleBlockShape;
    public Bool32 ResidencyStandard3DBlockShape;
    public Bool32 ResidencyAlignedMipSize;
    public Bool32 ResidencyNonResidentStrict;
}

[StructLayout(LayoutKind.Explicit, Size = 504)]
public struct PhysicalDeviceLimits
{
    [FieldOffset(376)] public SampleCountFlags FramebufferColorSampleCounts;
}

[StructLayout(LayoutKind.Explicit, Size = 824)]
public unsafe struct PhysicalDeviceProperties
{
    [FieldOffset(0)] public uint ApiVersion;
    [FieldOffset(4)] public uint DriverVersion;
    [FieldOffset(8)] public uint VendorID;
    [FieldOffset(12)] public uint DeviceID;
    [FieldOffset(16)] public PhysicalDeviceType DeviceType;
    [FieldOffset(20)] public fixed byte DeviceName[256];
    [FieldOffset(276)] public fixed byte PipelineCacheUuid[16];
    [FieldOffset(296)] public PhysicalDeviceLimits Limits;
}

[StructLayout(LayoutKind.Sequential)]
public struct SurfaceCapabilitiesKHR
{
    public uint MinImageCount;
    public uint MaxImageCount;
    public Extent2D CurrentExtent;
    public Extent2D MinImageExtent;
    public Extent2D MaxImageExtent;
    public uint MaxImageArrayLayers;
    public SurfaceTransformFlagsKHR SupportedTransforms;
    public SurfaceTransformFlagsKHR CurrentTransform;
    public CompositeAlphaFlagsKHR SupportedCompositeAlpha;
    public ImageUsageFlags SupportedUsageFlags;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct ApplicationInfo
{
    public StructureType SType;
    public void* PNext;
    public byte* PApplicationName;
    public uint ApplicationVersion;
    public byte* PEngineName;
    public uint EngineVersion;
    public uint ApiVersion;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct InstanceCreateInfo
{
    public StructureType SType;
    public void* PNext;
    public uint Flags;
    public ApplicationInfo* PApplicationInfo;
    public uint EnabledLayerCount;
    public byte** PpEnabledLayerNames;
    public uint EnabledExtensionCount;
    public byte** PpEnabledExtensionNames;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct DeviceQueueCreateInfo
{
    public StructureType SType;
    public void* PNext;
    public DeviceQueueCreateFlags Flags;
    public uint QueueFamilyIndex;
    public uint QueueCount;
    public float* PQueuePriorities;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct DeviceCreateInfo
{
    public StructureType SType;
    public void* PNext;
    public uint Flags;
    public uint QueueCreateInfoCount;
    public DeviceQueueCreateInfo* PQueueCreateInfos;
    public uint EnabledLayerCount;
    public byte** PpEnabledLayerNames;
    public uint EnabledExtensionCount;
    public byte** PpEnabledExtensionNames;
    public void* PEnabledFeatures;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct BufferCreateInfo
{
    public StructureType SType;
    public void* PNext;
    public uint Flags;
    public nuint Size;
    public BufferUsageFlags Usage;
    public SharingMode SharingMode;
    public uint QueueFamilyIndexCount;
    public uint* PQueueFamilyIndices;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct ImageCreateInfo
{
    public StructureType SType;
    public void* PNext;
    public uint Flags;
    public ImageType ImageType;
    public Format Format;
    public Extent3D Extent;
    public uint MipLevels;
    public uint ArrayLayers;
    public SampleCountFlags Samples;
    public ImageTiling Tiling;
    public ImageUsageFlags Usage;
    public SharingMode SharingMode;
    public uint QueueFamilyIndexCount;
    public uint* PQueueFamilyIndices;
    public ImageLayout InitialLayout;
}

[StructLayout(LayoutKind.Sequential)]
public struct MemoryAllocateInfo
{
    public StructureType SType;
    public nint PNext;
    public ulong AllocationSize;
    public uint MemoryTypeIndex;
}

[StructLayout(LayoutKind.Sequential)]
public struct ImageViewCreateInfo
{
    public StructureType SType;
    public nint PNext;
    public uint Flags;
    public Image Image;
    public ImageViewType ViewType;
    public Format Format;
    public ComponentMapping Components;
    public ImageSubresourceRange SubresourceRange;
}

[StructLayout(LayoutKind.Sequential)]
public struct AttachmentDescription
{
    public uint Flags;
    public Format Format;
    public SampleCountFlags Samples;
    public AttachmentLoadOp LoadOp;
    public AttachmentStoreOp StoreOp;
    public AttachmentLoadOp StencilLoadOp;
    public AttachmentStoreOp StencilStoreOp;
    public ImageLayout InitialLayout;
    public ImageLayout FinalLayout;
}

[StructLayout(LayoutKind.Sequential)]
public struct AttachmentReference
{
    public uint Attachment;
    public ImageLayout Layout;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct SubpassDescription
{
    public uint Flags;
    public PipelineBindPoint PipelineBindPoint;
    public uint InputAttachmentCount;
    public AttachmentReference* PInputAttachments;
    public uint ColorAttachmentCount;
    public AttachmentReference* PColorAttachments;
    public AttachmentReference* PResolveAttachments;
    public AttachmentReference* PDepthStencilAttachment;
    public uint PreserveAttachmentCount;
    public uint* PPreserveAttachments;
}

[StructLayout(LayoutKind.Sequential)]
public struct SubpassDependency
{
    public uint SrcSubpass;
    public uint DstSubpass;
    public PipelineStageFlags SrcStageMask;
    public PipelineStageFlags DstStageMask;
    public AccessFlags SrcAccessMask;
    public AccessFlags DstAccessMask;
    public DependencyFlags DependencyFlags;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct RenderPassCreateInfo
{
    public StructureType SType;
    public void* PNext;
    public uint Flags;
    public uint AttachmentCount;
    public AttachmentDescription* PAttachments;
    public uint SubpassCount;
    public SubpassDescription* PSubpasses;
    public uint DependencyCount;
    public SubpassDependency* PDependencies;
}

[StructLayout(LayoutKind.Sequential)]
public struct DescriptorSetLayoutBinding
{
    public uint Binding;
    public DescriptorType DescriptorType;
    public uint DescriptorCount;
    public ShaderStageFlags StageFlags;
    public nint PImmutableSamplers;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct DescriptorSetLayoutCreateInfo
{
    public StructureType SType;
    public void* PNext;
    public uint Flags;
    public uint BindingCount;
    public DescriptorSetLayoutBinding* PBindings;
}

[StructLayout(LayoutKind.Sequential)]
public struct PushConstantRange
{
    public ShaderStageFlags StageFlags;
    public uint Offset;
    public uint Size;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct PipelineLayoutCreateInfo
{
    public StructureType SType;
    public void* PNext;
    public uint Flags;
    public uint SetLayoutCount;
    public DescriptorSetLayout* PSetLayouts;
    public uint PushConstantRangeCount;
    public PushConstantRange* PPushConstantRanges;
}

[StructLayout(LayoutKind.Sequential)]
public struct SamplerCreateInfo
{
    public StructureType SType;
    public nint PNext;
    public uint Flags;
    public Filter MagFilter;
    public Filter MinFilter;
    public SamplerMipmapMode MipmapMode;
    public SamplerAddressMode AddressModeU;
    public SamplerAddressMode AddressModeV;
    public SamplerAddressMode AddressModeW;
    public float MipLodBias;
    public Bool32 AnisotropyEnable;
    public float MaxAnisotropy;
    public Bool32 CompareEnable;
    public CompareOp CompareOp;
    public float MinLod;
    public float MaxLod;
    public BorderColor BorderColor;
    public Bool32 UnnormalizedCoordinates;
}

public enum BorderColor : int
{
    FloatTransparentBlack = 0,
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct PipelineShaderStageCreateInfo
{
    public StructureType SType;
    public void* PNext;
    public uint Flags;
    public ShaderStageFlags Stage;
    public ShaderModule Module;
    public byte* PName;
    public void* PSpecializationInfo;
}

[StructLayout(LayoutKind.Sequential)]
public struct VertexInputBindingDescription
{
    public uint Binding;
    public uint Stride;
    public VertexInputRate InputRate;
}

[StructLayout(LayoutKind.Sequential)]
public struct VertexInputAttributeDescription
{
    public uint Location;
    public uint Binding;
    public Format Format;
    public uint Offset;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct PipelineVertexInputStateCreateInfo
{
    public StructureType SType;
    public void* PNext;
    public uint Flags;
    public uint VertexBindingDescriptionCount;
    public VertexInputBindingDescription* PVertexBindingDescriptions;
    public uint VertexAttributeDescriptionCount;
    public VertexInputAttributeDescription* PVertexAttributeDescriptions;
}

[StructLayout(LayoutKind.Sequential)]
public struct PipelineInputAssemblyStateCreateInfo
{
    public StructureType SType;
    public nint PNext;
    public uint Flags;
    public PrimitiveTopology Topology;
    public Bool32 PrimitiveRestartEnable;
}

[StructLayout(LayoutKind.Sequential)]
public struct PipelineViewportStateCreateInfo
{
    public StructureType SType;
    public nint PNext;
    public uint Flags;
    public uint ViewportCount;
    public nint PViewports;
    public uint ScissorCount;
    public nint PScissors;
}

[StructLayout(LayoutKind.Sequential)]
public struct PipelineRasterizationStateCreateInfo
{
    public StructureType SType;
    public nint PNext;
    public uint Flags;
    public Bool32 DepthClampEnable;
    public Bool32 RasterizerDiscardEnable;
    public PolygonMode PolygonMode;
    public CullModeFlags CullMode;
    public FrontFace FrontFace;
    public Bool32 DepthBiasEnable;
    public float DepthBiasConstantFactor;
    public float DepthBiasClamp;
    public float DepthBiasSlopeFactor;
    public float LineWidth;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct PipelineMultisampleStateCreateInfo
{
    public StructureType SType;
    public void* PNext;
    public uint Flags;
    public SampleCountFlags RasterizationSamples;
    public Bool32 SampleShadingEnable;
    public float MinSampleShading;
    public uint* PSampleMask;
    public Bool32 AlphaToCoverageEnable;
    public Bool32 AlphaToOneEnable;
}

[StructLayout(LayoutKind.Sequential)]
public struct StencilOpState
{
    public StencilOp FailOp;
    public StencilOp PassOp;
    public StencilOp DepthFailOp;
    public CompareOp CompareOp;
    public uint CompareMask;
    public uint WriteMask;
    public uint Reference;
}

public enum StencilOp : int
{
    Keep = 0,
}

[StructLayout(LayoutKind.Sequential)]
public struct PipelineDepthStencilStateCreateInfo
{
    public StructureType SType;
    public nint PNext;
    public uint Flags;
    public Bool32 DepthTestEnable;
    public Bool32 DepthWriteEnable;
    public CompareOp DepthCompareOp;
    public Bool32 DepthBoundsTestEnable;
    public Bool32 StencilTestEnable;
    public StencilOpState Front;
    public StencilOpState Back;
    public float MinDepthBounds;
    public float MaxDepthBounds;
}

[StructLayout(LayoutKind.Sequential)]
public struct PipelineColorBlendAttachmentState
{
    public Bool32 BlendEnable;
    public BlendFactor SrcColorBlendFactor;
    public BlendFactor DstColorBlendFactor;
    public BlendOp ColorBlendOp;
    public BlendFactor SrcAlphaBlendFactor;
    public BlendFactor DstAlphaBlendFactor;
    public BlendOp AlphaBlendOp;
    public ColorComponentFlags ColorWriteMask;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct PipelineColorBlendStateCreateInfo
{
    public StructureType SType;
    public void* PNext;
    public uint Flags;
    public Bool32 LogicOpEnable;
    public LogicOp LogicOp;
    public uint AttachmentCount;
    public PipelineColorBlendAttachmentState* PAttachments;
    public fixed float BlendConstants[4];
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct PipelineDynamicStateCreateInfo
{
    public StructureType SType;
    public void* PNext;
    public uint Flags;
    public uint DynamicStateCount;
    public DynamicState* PDynamicStates;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct GraphicsPipelineCreateInfo
{
    public StructureType SType;
    public void* PNext;
    public uint Flags;
    public uint StageCount;
    public PipelineShaderStageCreateInfo* PStages;
    public PipelineVertexInputStateCreateInfo* PVertexInputState;
    public PipelineInputAssemblyStateCreateInfo* PInputAssemblyState;
    public void* PTessellationState;
    public PipelineViewportStateCreateInfo* PViewportState;
    public PipelineRasterizationStateCreateInfo* PRasterizationState;
    public PipelineMultisampleStateCreateInfo* PMultisampleState;
    public PipelineDepthStencilStateCreateInfo* PDepthStencilState;
    public PipelineColorBlendStateCreateInfo* PColorBlendState;
    public PipelineDynamicStateCreateInfo* PDynamicState;
    public PipelineLayout Layout;
    public RenderPass RenderPass;
    public uint Subpass;
    public Pipeline BasePipelineHandle;
    public int BasePipelineIndex;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct PipelineCacheCreateInfo
{
    public StructureType SType;
    public void* PNext;
    public uint Flags;
    public nuint InitialDataSize;
    public void* PInitialData;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct DescriptorPoolCreateInfo
{
    public StructureType SType;
    public void* PNext;
    public DescriptorPoolCreateFlags Flags;
    public uint MaxSets;
    public uint PoolSizeCount;
    public DescriptorPoolSize* PPoolSizes;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct DescriptorSetAllocateInfo
{
    public StructureType SType;
    public void* PNext;
    public DescriptorPool DescriptorPool;
    public uint DescriptorSetCount;
    public DescriptorSetLayout* PSetLayouts;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct CommandBufferBeginInfo
{
    public StructureType SType;
    public void* PNext;
    public CommandBufferUsageFlags Flags;
    public void* PInheritanceInfo;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct RenderPassBeginInfo
{
    public StructureType SType;
    public void* PNext;
    public RenderPass RenderPass;
    public Framebuffer Framebuffer;
    public Rect2D RenderArea;
    public uint ClearValueCount;
    public ClearValue* PClearValues;
}

[StructLayout(LayoutKind.Sequential)]
public struct ClearAttachment
{
    public ImageAspectFlags AspectMask;
    public uint ColorAttachment;
    public ClearValue ClearValue;
}

[StructLayout(LayoutKind.Sequential)]
public struct ClearRect
{
    public Rect2D Rect;
    public uint BaseArrayLayer;
    public uint LayerCount;
}

[StructLayout(LayoutKind.Sequential)]
public struct ImageSubresource
{
    public ImageAspectFlags AspectMask;
    public uint MipLevel;
    public uint ArrayLayer;
}

[StructLayout(LayoutKind.Sequential)]
public struct ImageSubresourceLayers
{
    public ImageAspectFlags AspectMask;
    public uint MipLevel;
    public uint BaseArrayLayer;
    public uint LayerCount;
}

[StructLayout(LayoutKind.Sequential)]
public struct ImageCopy
{
    public ImageSubresourceLayers SrcSubresource;
    public Offset3D SrcOffset;
    public ImageSubresourceLayers DstSubresource;
    public Offset3D DstOffset;
    public Extent3D Extent;
}

[StructLayout(LayoutKind.Sequential)]
public struct BufferImageCopy
{
    public ulong BufferOffset;
    public uint BufferRowLength;
    public uint BufferImageHeight;
    public ImageSubresourceLayers ImageSubresource;
    public Offset3D ImageOffset;
    public Extent3D ImageExtent;
}

[StructLayout(LayoutKind.Sequential)]
public struct BufferCopy
{
    public ulong SrcOffset;
    public ulong DstOffset;
    public nuint Size;
}

[StructLayout(LayoutKind.Sequential)]
public struct DescriptorImageInfo
{
    public Sampler Sampler;
    public ImageView ImageView;
    public ImageLayout ImageLayout;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct WriteDescriptorSet
{
    public StructureType SType;
    public void* PNext;
    public DescriptorSet DstSet;
    public uint DstBinding;
    public uint DstArrayElement;
    public uint DescriptorCount;
    public DescriptorType DescriptorType;
    public DescriptorImageInfo* PImageInfo;
    public void* PBufferInfo;
    public void* PTexelBufferView;
}

[StructLayout(LayoutKind.Sequential)]
public struct CommandBufferAllocateInfo
{
    public StructureType SType;
    public nint PNext;
    public CommandPool CommandPool;
    public CommandBufferLevel Level;
    public uint CommandBufferCount;
}

[StructLayout(LayoutKind.Sequential)]
public struct ImageMemoryBarrier
{
    public StructureType SType;
    public nint PNext;
    public AccessFlags SrcAccessMask;
    public AccessFlags DstAccessMask;
    public ImageLayout OldLayout;
    public ImageLayout NewLayout;
    public uint SrcQueueFamilyIndex;
    public uint DstQueueFamilyIndex;
    public Image Image;
    public ImageSubresourceRange SubresourceRange;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct ShaderModuleCreateInfo
{
    public StructureType SType;
    public void* PNext;
    public uint Flags;
    public nuint CodeSize;
    public uint* PCode;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct FramebufferCreateInfo
{
    public StructureType SType;
    public void* PNext;
    public uint Flags;
    public RenderPass RenderPass;
    public uint AttachmentCount;
    public ImageView* PAttachments;
    public uint Width;
    public uint Height;
    public uint Layers;
}

[StructLayout(LayoutKind.Sequential)]
public struct CommandPoolCreateInfo
{
    public StructureType SType;
    public nint PNext;
    public CommandPoolCreateFlags Flags;
    public uint QueueFamilyIndex;
}

[StructLayout(LayoutKind.Sequential)]
public struct SemaphoreCreateInfo
{
    public StructureType SType;
    public nint PNext;
    public uint Flags;
}

[StructLayout(LayoutKind.Sequential)]
public struct FenceCreateInfo
{
    public StructureType SType;
    public nint PNext;
    public FenceCreateFlags Flags;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct SubmitInfo
{
    public StructureType SType;
    public void* PNext;
    public uint WaitSemaphoreCount;
    public Semaphore* PWaitSemaphores;
    public PipelineStageFlags* PWaitDstStageMask;
    public uint CommandBufferCount;
    public CommandBuffer* PCommandBuffers;
    public uint SignalSemaphoreCount;
    public Semaphore* PSignalSemaphores;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct PresentInfoKHR
{
    public StructureType SType;
    public void* PNext;
    public uint WaitSemaphoreCount;
    public Semaphore* PWaitSemaphores;
    public uint SwapchainCount;
    public SwapchainKHR* PSwapchains;
    public uint* PImageIndices;
    public Result* PResults;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct SwapchainCreateInfoKHR
{
    public StructureType SType;
    public void* PNext;
    public uint Flags;
    public SurfaceKHR Surface;
    public uint MinImageCount;
    public Format ImageFormat;
    public ColorSpaceKHR ImageColorSpace;
    public Extent2D ImageExtent;
    public uint ImageArrayLayers;
    public ImageUsageFlags ImageUsage;
    public SharingMode ImageSharingMode;
    public uint QueueFamilyIndexCount;
    public uint* PQueueFamilyIndices;
    public SurfaceTransformFlagsKHR PreTransform;
    public CompositeAlphaFlagsKHR CompositeAlpha;
    public PresentModeKHR PresentMode;
    public Bool32 Clipped;
    public SwapchainKHR OldSwapchain;
}

[StructLayout(LayoutKind.Sequential)]
public struct DebugUtilsMessengerCreateInfoEXT
{
    public StructureType SType;
    public nint PNext;
    public uint Flags;
    public DebugUtilsMessageSeverityFlagsEXT MessageSeverity;
    public DebugUtilsMessageTypeFlagsEXT MessageType;
    public nint PfnUserCallback;
    public nint PUserData;
}

[StructLayout(LayoutKind.Explicit, Size = 96)]
public unsafe struct DebugUtilsMessengerCallbackDataEXT
{
    [FieldOffset(40)] public byte* PMessage;
}

/// <summary>
/// VkPhysicalDeviceFeatures — 55 VkBool32 fields, 220 bytes total.
/// Only dualSrcBlend (offset 28) is exposed; the rest default to 0 (false).
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 220)]
public struct PhysicalDeviceFeatures
{
    [FieldOffset(28)] public uint DualSrcBlend;
}
