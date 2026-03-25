namespace Duxel.Vulkan;

public enum Result : int
{
    Success = 0,
    SuboptimalKhr = 1000001003,
    ErrorOutOfDateKhr = -1000001004,
    ErrorSurfaceLostKhr = -1000000000,
}

public enum StructureType : int
{
    ApplicationInfo = 0,
    InstanceCreateInfo = 1,
    DeviceQueueCreateInfo = 2,
    DeviceCreateInfo = 3,
    SubmitInfo = 4,
    MemoryAllocateInfo = 5,
    MappedMemoryRange = 6,
    BindSparseInfo = 7,
    FenceCreateInfo = 8,
    SemaphoreCreateInfo = 9,
    CommandBufferAllocateInfo = 40,
    CommandBufferInheritanceInfo = 41,
    CommandBufferBeginInfo = 42,
    RenderPassBeginInfo = 43,
    BufferCreateInfo = 12,
    ImageCreateInfo = 14,
    ImageViewCreateInfo = 15,
    ShaderModuleCreateInfo = 16,
    PipelineCacheCreateInfo = 17,
    PipelineShaderStageCreateInfo = 18,
    PipelineVertexInputStateCreateInfo = 19,
    PipelineInputAssemblyStateCreateInfo = 20,
    PipelineTessellationStateCreateInfo = 21,
    PipelineViewportStateCreateInfo = 22,
    PipelineRasterizationStateCreateInfo = 23,
    PipelineMultisampleStateCreateInfo = 24,
    PipelineDepthStencilStateCreateInfo = 25,
    PipelineColorBlendStateCreateInfo = 26,
    PipelineDynamicStateCreateInfo = 27,
    GraphicsPipelineCreateInfo = 28,
    PipelineLayoutCreateInfo = 30,
    SamplerCreateInfo = 31,
    DescriptorSetLayoutCreateInfo = 32,
    DescriptorPoolCreateInfo = 33,
    DescriptorSetAllocateInfo = 34,
    WriteDescriptorSet = 35,
    FramebufferCreateInfo = 37,
    RenderPassCreateInfo = 38,
    CommandPoolCreateInfo = 39,
    SwapchainCreateInfoKhr = 1000001000,
    PresentInfoKhr = 1000001001,
    DebugUtilsMessengerCreateInfoExt = 1000128004,
    ImageMemoryBarrier = 44,
}

[Flags]
public enum QueueFlags : uint
{
    GraphicsBit = 0x00000001,
}

[Flags]
public enum AccessFlags : uint
{
    None = 0,
    IndirectCommandReadBit = 0x00000001,
    IndexReadBit = 0x00000002,
    VertexAttributeReadBit = 0x00000004,
    UniformReadBit = 0x00000008,
    InputAttachmentReadBit = 0x00000010,
    ShaderReadBit = 0x00000020,
    ShaderWriteBit = 0x00000040,
    ColorAttachmentReadBit = 0x00000080,
    ColorAttachmentWriteBit = 0x00000100,
    TransferReadBit = 0x00000800,
    TransferWriteBit = 0x00001000,
    MemoryReadBit = 0x00008000,
}

[Flags]
public enum BufferUsageFlags : uint
{
    TransferSrcBit = 0x00000001,
    TransferDstBit = 0x00000002,
    UniformTexelBuffer = 0x00000004,
    StorageTexelBuffer = 0x00000008,
    UniformBuffer = 0x00000010,
    StorageBuffer = 0x00000020,
    IndexBufferBit = 0x00000040,
    VertexBufferBit = 0x00000080,
}

[Flags]
public enum ColorComponentFlags : uint
{
    RBit = 0x00000001,
    GBit = 0x00000002,
    BBit = 0x00000004,
    ABit = 0x00000008,
}

[Flags]
public enum CompositeAlphaFlagsKHR : uint
{
    OpaqueBitKhr = 0x00000001,
    PreMultipliedBitKhr = 0x00000002,
    PostMultipliedBitKhr = 0x00000004,
    InheritBitKhr = 0x00000008,
}

[Flags]
public enum DependencyFlags : uint
{
    ByRegionBit = 0x00000001,
}

[Flags]
public enum DescriptorPoolCreateFlags : uint
{
    FreeDescriptorSetBit = 0x00000001,
}

[Flags]
public enum FenceCreateFlags : uint
{
    SignaledBit = 0x00000001,
}

[Flags]
public enum ImageAspectFlags : uint
{
    ColorBit = 0x00000001,
}

[Flags]
public enum ImageUsageFlags : uint
{
    TransferSrcBit = 0x00000001,
    TransferDstBit = 0x00000002,
    SampledBit = 0x00000004,
    StorageBit = 0x00000008,
    ColorAttachmentBit = 0x00000010,
    TransientAttachmentBit = 0x00000040,
    InputAttachmentBit = 0x00000080,
}

[Flags]
public enum MemoryPropertyFlags : uint
{
    DeviceLocalBit = 0x00000001,
    HostVisibleBit = 0x00000002,
    HostCoherentBit = 0x00000004,
}

[Flags]
public enum PipelineStageFlags : uint
{
    TopOfPipeBit = 0x00000001,
    FragmentShaderBit = 0x00000080,
    ColorAttachmentOutputBit = 0x00000400,
    TransferBit = 0x00001000,
    BottomOfPipeBit = 0x00002000,
}

[Flags]
public enum SampleCountFlags : uint
{
    Count1Bit = 0x00000001,
    Count2Bit = 0x00000002,
    Count4Bit = 0x00000004,
    Count8Bit = 0x00000008,
}

[Flags]
public enum ShaderStageFlags : uint
{
    VertexBit = 0x00000001,
    FragmentBit = 0x00000010,
}

[Flags]
public enum CommandPoolCreateFlags : uint
{
    TransientBit = 0x00000001,
    ResetCommandBufferBit = 0x00000002,
}

[Flags]
public enum DeviceQueueCreateFlags : uint
{
    None = 0,
}

public enum AttachmentLoadOp : int
{
    Load = 0,
    Clear = 1,
    DontCare = 2,
}

public enum AttachmentStoreOp : int
{
    Store = 0,
    DontCare = 1,
}

public enum BlendFactor : int
{
    Zero = 0,
    One = 1,
    SrcAlpha = 6,
    OneMinusSrcAlpha = 7,
    ConstantAlpha = 12,
    OneMinusConstantAlpha = 13,
    Src1Color = 15,
    OneMinusSrc1Color = 16,
    Src1Alpha = 17,
    OneMinusSrc1Alpha = 18,
}

public enum BlendOp : int
{
    Add = 0,
}

public enum ColorSpaceKHR : int
{
    PaceSrgbNonlinearKhr = 0,
}

public enum CommandBufferLevel : int
{
    Primary = 0,
}

[Flags]
public enum CommandBufferUsageFlags : uint
{
    OneTimeSubmitBit = 0x00000001,
}

public enum CompareOp : int
{
    Always = 7,
}

public enum ComponentSwizzle : int
{
    Identity = 0,
}

[Flags]
public enum CullModeFlags : uint
{
    None = 0,
}

[Flags]
public enum DebugUtilsMessageSeverityFlagsEXT : uint
{
    WarningBitExt = 0x00000100,
    ErrorBitExt = 0x00001000,
}

[Flags]
public enum DebugUtilsMessageTypeFlagsEXT : uint
{
    GeneralBitExt = 0x00000001,
    ValidationBitExt = 0x00000002,
    PerformanceBitExt = 0x00000004,
}

public enum DescriptorType : int
{
    Sampler = 0,
    CombinedImageSampler = 1,
    SampledImage = 2,
    StorageImage = 3,
    UniformTexelBuffer = 4,
    StorageTexelBuffer = 5,
    UniformBuffer = 6,
    StorageBuffer = 7,
    UniformBufferDynamic = 8,
    StorageBufferDynamic = 9,
    InputAttachment = 10,
}

public enum DynamicState : int
{
    Viewport = 0,
    Scissor = 1,
    BlendConstants = 7,
}

public enum Filter : int
{
    Nearest = 0,
    Linear = 1,
}

public enum Format : int
{
    Undefined = 0,
    R32G32Sfloat = 103,
    R8G8B8A8Unorm = 37,
    R8G8B8A8Srgb = 43,
    B8G8R8A8Unorm = 44,
    B8G8R8A8Srgb = 50,
}

public enum FrontFace : int
{
    CounterClockwise = 1,
}

public enum ImageLayout : int
{
    Undefined = 0,
    ColorAttachmentOptimal = 2,
    ShaderReadOnlyOptimal = 5,
    TransferSrcOptimal = 6,
    TransferDstOptimal = 7,
    PresentSrcKhr = 1000001002,
}

public enum ImageTiling : int
{
    Optimal = 0,
}

public enum ImageType : int
{
    Type2D = 1,
}

public enum ImageViewType : int
{
    Type2D = 1,
}

public enum IndexType : int
{
    Uint32 = 1,
}

public enum LogicOp : int
{
    Copy = 3,
}

public enum PhysicalDeviceType : int
{
    Other = 0,
    IntegratedGpu = 1,
    DiscreteGpu = 2,
    VirtualGpu = 3,
    Cpu = 4,
}

public enum PipelineBindPoint : int
{
    Graphics = 0,
}

public enum PolygonMode : int
{
    Fill = 0,
}

public enum PresentModeKHR : int
{
    ImmediateKhr = 0,
    FifoKhr = 2,
}

public enum PrimitiveTopology : int
{
    TriangleList = 3,
}

public enum SamplerAddressMode : int
{
    ClampToEdge = 2,
}

public enum SamplerMipmapMode : int
{
    Nearest = 0,
    Linear = 1,
}

public enum SharingMode : int
{
    Exclusive = 0,
    Concurrent = 1,
}

public enum SubpassContents : int
{
    Inline = 0,
}

public enum VertexInputRate : int
{
    Vertex = 0,
}

[Flags]
public enum SurfaceTransformFlagsKHR : uint
{
    IdentityBitKhr = 0x00000001,
}

[Flags]
public enum MemoryHeapFlags : uint
{
    DeviceLocalBit = 0x00000001,
}
