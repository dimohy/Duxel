using Duxel.Core;
using System.Buffers.Binary;
using System.Diagnostics;
using System.IO;
using System.Security;

namespace Duxel.Vulkan;

public sealed unsafe partial class VulkanRendererBackend
{
    private static readonly byte[] VertexShaderSpirv = LoadEmbeddedShader("Duxel.Vulkan.Shaders.imgui.vert.spv");
    private static readonly byte[] FragmentShaderSpirv = LoadFragmentShader();

    private string ResolveBlendFragmentResource(string resourceName)
        => _dualSourceBlendEnabled
            ? resourceName
            : resourceName.Replace(".frag.spv", ".single.frag.spv", StringComparison.Ordinal);

    private static byte[] LoadFragmentShader(string resourceName = "Duxel.Vulkan.Shaders.imgui.frag.spv")
    {
        var shader = LoadEmbeddedShader(resourceName);
        OverrideValidationFragmentShader(ref shader);
        return shader;
    }

    // Implemented only by the validation assembly; removed from production builds.
    static partial void OverrideValidationFragmentShader(ref byte[] shader);
    static partial void OverrideValidationOpaqueCoverage(ref bool enabled);

    private DescriptorSetLayout _descriptorSetLayout;
    private PipelineLayout _pipelineLayout;
    private Sampler _fontSampler;
    private Sampler _imageSampler;
    private DescriptorSet _bindlessTextureSet;

    /// <summary>
    /// Capacity of the global bindless texture array. Devices exposing the required
    /// descriptor indexing features guarantee at least 500k update-after-bind sampled
    /// images, so this fixed capacity is always within spec limits.
    /// </summary>
    private const uint BindlessTextureCapacity = 4096;
    private Pipeline _graphicsPipeline;
    private ShaderModule _vertexShaderModule;
    private ShaderModule _fragmentShaderModule;
    private ShaderModule _depthMask4VertexShaderModule;
    private ShaderModule _depthMask4FragmentShaderModule;
    private PipelineCache _pipelineCache;
    private string _pipelineCachePath;
    private nuint _pipelineCacheSize;
    private ulong _pipelineCacheHash;
    private DescriptorPool _descriptorPool;

    private unsafe void CreatePipelineLayouts()
    {
        if (_descriptorSetLayout.Handle is not 0 && _pipelineLayout.Handle is not 0 && _fontSampler.Handle is not 0 && _imageSampler.Handle is not 0)
        {
            return;
        }

        var samplerBinding = new DescriptorSetLayoutBinding
        {
            Binding = 0,
            DescriptorCount = BindlessTextureCapacity,
            DescriptorType = DescriptorType.CombinedImageSampler,
            StageFlags = ShaderStageFlags.FragmentBit,
        };

        var bindingFlags = DescriptorBindingFlags.UpdateAfterBindBit
            | DescriptorBindingFlags.UpdateUnusedWhilePendingBit
            | DescriptorBindingFlags.PartiallyBoundBit;
        var bindingFlagsInfo = new DescriptorSetLayoutBindingFlagsCreateInfo
        {
            SType = StructureType.DescriptorSetLayoutBindingFlagsCreateInfo,
            BindingCount = 1,
            PBindingFlags = &bindingFlags,
        };

        var descriptorLayoutInfo = new DescriptorSetLayoutCreateInfo
        {
            SType = StructureType.DescriptorSetLayoutCreateInfo,
            PNext = &bindingFlagsInfo,
            Flags = DescriptorSetLayoutCreateFlags.UpdateAfterBindPoolBit,
            BindingCount = 1,
            PBindings = &samplerBinding,
        };

        fixed (DescriptorSetLayout* layout = &_descriptorSetLayout)
        {
            Check(_vk.CreateDescriptorSetLayout(_device, &descriptorLayoutInfo, null, layout));
        }

        var pushConstantRanges = stackalloc PushConstantRange[2];
        // Vertex range: scale(8) + translate(8) + opacity(4) + drawMode(4)
        // + addresses(16), texture slot(4), circular-cutout offset(4), matrix(64) =112bytes.
        pushConstantRanges[0] = new PushConstantRange
        {
            StageFlags = ShaderStageFlags.VertexBit,
            Offset = 0,
            Size = 112,
        };
        // Fragment overlaps texture/mask/matrix bytes and includes the local clip at byte112.
        pushConstantRanges[1] = new PushConstantRange
        {
            StageFlags = ShaderStageFlags.FragmentBit,
            Offset = 40,
            Size = 88,
        };

        var pipelineLayoutInfo = new PipelineLayoutCreateInfo
        {
            SType = StructureType.PipelineLayoutCreateInfo,
            SetLayoutCount = 1,
            PushConstantRangeCount = 2,
            PPushConstantRanges = pushConstantRanges,
        };

        var setLayouts = stackalloc DescriptorSetLayout[1];
        setLayouts[0] = _descriptorSetLayout;
        pipelineLayoutInfo.PSetLayouts = setLayouts;

        fixed (PipelineLayout* pipelineLayout = &_pipelineLayout)
        {
            Check(_vk.CreatePipelineLayout(_device, &pipelineLayoutInfo, null, pipelineLayout));
        }

        var fontSamplerInfo = new SamplerCreateInfo
        {
            SType = StructureType.SamplerCreateInfo,
            MagFilter = _options.FontLinearSampling ? Filter.Linear : Filter.Nearest,
            MinFilter = _options.FontLinearSampling ? Filter.Linear : Filter.Nearest,
            MipmapMode = SamplerMipmapMode.Nearest,
            AddressModeU = SamplerAddressMode.ClampToEdge,
            AddressModeV = SamplerAddressMode.ClampToEdge,
            AddressModeW = SamplerAddressMode.ClampToEdge,
            MinLod = 0,
            MaxLod = 0,
            AnisotropyEnable = false,
            MaxAnisotropy = 1,
        };

        fixed (Sampler* sampler = &_fontSampler)
        {
            Check(_vk.CreateSampler(_device, &fontSamplerInfo, null, sampler));
        }

        var imageSamplerInfo = new SamplerCreateInfo
        {
            SType = StructureType.SamplerCreateInfo,
            MagFilter = Filter.Linear,
            MinFilter = Filter.Linear,
            MipmapMode = SamplerMipmapMode.Linear,
            AddressModeU = SamplerAddressMode.ClampToEdge,
            AddressModeV = SamplerAddressMode.ClampToEdge,
            AddressModeW = SamplerAddressMode.ClampToEdge,
            MinLod = 0,
            MaxLod = 0,
            AnisotropyEnable = false,
            MaxAnisotropy = 1,
        };

        fixed (Sampler* sampler = &_imageSampler)
        {
            Check(_vk.CreateSampler(_device, &imageSamplerInfo, null, sampler));
        }
    }

    private unsafe void CreateDescriptorPool()
    {
        if (_descriptorPool.Handle is not 0)
        {
            return;
        }

        var poolSizes = stackalloc DescriptorPoolSize[1];
        poolSizes[0] = new DescriptorPoolSize(DescriptorType.CombinedImageSampler, BindlessTextureCapacity);

        var poolInfo = new DescriptorPoolCreateInfo
        {
            SType = StructureType.DescriptorPoolCreateInfo,
            PoolSizeCount = 1,
            PPoolSizes = poolSizes,
            MaxSets = 1,
            Flags = DescriptorPoolCreateFlags.UpdateAfterBindBit,
        };

        fixed (DescriptorPool* pool = &_descriptorPool)
        {
            Check(_vk.CreateDescriptorPool(_device, &poolInfo, null, pool));
        }

        var layout = _descriptorSetLayout;
        var allocInfo = new DescriptorSetAllocateInfo
        {
            SType = StructureType.DescriptorSetAllocateInfo,
            DescriptorPool = _descriptorPool,
            DescriptorSetCount = 1,
            PSetLayouts = &layout,
        };

        fixed (DescriptorSet* set = &_bindlessTextureSet)
        {
            Check(_vk.AllocateDescriptorSets(_device, &allocInfo, set));
        }
    }

    private readonly record struct ProjectedGlyphStages(ShaderModule Vertex, ShaderModule Fragment);

    private unsafe void CreateGraphicsPipeline(bool createDepthVariants = false, bool compiledLayers = false, bool sharedBasis = false, bool sharedCamera = false, bool circleCutout = false, bool backdropComposite = false, bool projectedCapture = false, ProjectedGlyphStages? glyphStages = null, Pipeline* glyphPipeline = null)
    {
        if (!createDepthVariants && !circleCutout && !backdropComposite && !projectedCapture && glyphStages is null)
        {
            _vertexShaderModule = CreateShaderModule(_viewportClipPlanesEnabled
                ? LoadEmbeddedShader("Duxel.Vulkan.Shaders.imgui_viewport_clip.vert.spv")
                : VertexShaderSpirv);
            _fragmentShaderModule = CreateShaderModule(_dualSourceBlendEnabled
                ? FragmentShaderSpirv
                : LoadFragmentShader(ResolveBlendFragmentResource("Duxel.Vulkan.Shaders.imgui.frag.spv")));
        }

        if (circleCutout && _circleCutoutVertexShaderModule.Handle == 0)
        {
            _circleCutoutVertexShaderModule = CreateShaderModule(LoadEmbeddedShader(_viewportClipPlanesEnabled
                ? "Duxel.Vulkan.Shaders.imgui_cutout_viewport_clip.vert.spv"
                : "Duxel.Vulkan.Shaders.imgui_cutout.vert.spv"));
            _circleCutoutFragmentShaderModule = CreateShaderModule(LoadFragmentShader(ResolveBlendFragmentResource("Duxel.Vulkan.Shaders.imgui_cutout.frag.spv")));
        }

        if (backdropComposite && _backdropFragmentShaderModule.Handle == 0)
        {
            _backdropFragmentShaderModule = CreateShaderModule(LoadFragmentShader(ResolveBlendFragmentResource("Duxel.Vulkan.Shaders.imgui_backdrop.frag.spv")));
            _backdropCutoutFragmentShaderModule = CreateShaderModule(LoadFragmentShader(ResolveBlendFragmentResource("Duxel.Vulkan.Shaders.imgui_backdrop_cutout.frag.spv")));
        }
        var entryPoint = VulkanMarshaling.StringToPtr("main");

        try
        {
            var specializationValues = stackalloc uint[5] { createDepthVariants ? 1u : 0u, compiledLayers ? 1u : 0u, sharedBasis ? 1u : 0u, sharedCamera ? 1u : 0u, 0u };
            var specializationEntries = stackalloc SpecializationMapEntry[5];
            specializationEntries[0] = new()
            {
                ConstantID = 0,
                Size = sizeof(uint)
            };
            specializationEntries[1] = new()
            {
                ConstantID = 1,
                Offset = sizeof(uint),
                Size = sizeof(uint)
            };
            specializationEntries[2] = new()
            {
                ConstantID = 2,
                Offset = 2 * sizeof(uint),
                Size = sizeof(uint)
            };
            specializationEntries[3] = new()
            {
                ConstantID = 3,
                Offset = 3 * sizeof(uint),
                Size = sizeof(uint)
            };
            specializationEntries[4] = new()
            {
                ConstantID = 4,
                Offset = 4 * sizeof(uint),
                Size = sizeof(uint)
            };
            var specialization = new SpecializationInfo
            {
                MapEntryCount = 5,
                PMapEntries = specializationEntries,
                DataSize = 5 * sizeof(uint),
                PData = specializationValues,
            };
            // A flat capture keeps the ordinary vertex transform while its fragment
            // stage uses the existing projected-text coverage contract.
            var captureFragmentValues = stackalloc uint[5] { 1, 0, 0, 0, 0 };
            var captureFragmentSpecialization = specialization;
            captureFragmentSpecialization.PData = captureFragmentValues;
            var vertexStage = new PipelineShaderStageCreateInfo
            {
                SType = StructureType.PipelineShaderStageCreateInfo,
                Stage = ShaderStageFlags.VertexBit,
                Module = circleCutout ? _circleCutoutVertexShaderModule : _vertexShaderModule,
                PName = entryPoint,
                PSpecializationInfo = &specialization,
            };

            var fragmentStage = new PipelineShaderStageCreateInfo
            {
                SType = StructureType.PipelineShaderStageCreateInfo,
                Stage = ShaderStageFlags.FragmentBit,
                Module = backdropComposite ? (circleCutout ? _backdropCutoutFragmentShaderModule : _backdropFragmentShaderModule)
                    : (circleCutout ? _circleCutoutFragmentShaderModule : _fragmentShaderModule),
                PName = entryPoint,
                PSpecializationInfo = projectedCapture ? &captureFragmentSpecialization : &specialization,
            };

            var stages = stackalloc PipelineShaderStageCreateInfo[2];
            stages[0] = vertexStage;
            stages[1] = fragmentStage;

            // Vertex pulling: all geometry is read from buffer device addresses in
            // the vertex shader, so the pipeline has no vertex input state.
            var vertexInputInfo = new PipelineVertexInputStateCreateInfo
            {
                SType = StructureType.PipelineVertexInputStateCreateInfo,
                VertexBindingDescriptionCount = 0,
                VertexAttributeDescriptionCount = 0,
            };

            var inputAssembly = new PipelineInputAssemblyStateCreateInfo
            {
                SType = StructureType.PipelineInputAssemblyStateCreateInfo,
                Topology = PrimitiveTopology.TriangleList,
                PrimitiveRestartEnable = false,
            };

            var viewportState = new PipelineViewportStateCreateInfo
            {
                SType = StructureType.PipelineViewportStateCreateInfo,
                ViewportCount = 1,
                ScissorCount = 1,
            };

            var rasterizer = new PipelineRasterizationStateCreateInfo
            {
                SType = StructureType.PipelineRasterizationStateCreateInfo,
                DepthClampEnable = false,
                RasterizerDiscardEnable = false,
                PolygonMode = PolygonMode.Fill,
                LineWidth = 1.0f,
                CullMode = CullModeFlags.None,
                FrontFace = FrontFace.CounterClockwise,
                DepthBiasEnable = false,
                DepthBiasClamp = 0f,
                DepthBiasConstantFactor = 0f,
                DepthBiasSlopeFactor = 0f,
            };

            var multisampling = new PipelineMultisampleStateCreateInfo
            {
                SType = StructureType.PipelineMultisampleStateCreateInfo,
                RasterizationSamples = _msaaSampleCount,
                SampleShadingEnable = false,
                MinSampleShading = 1.0f,
                PSampleMask = null,
                AlphaToCoverageEnable = false,
                AlphaToOneEnable = false,
            };

            var depthStencil = new PipelineDepthStencilStateCreateInfo
            {
                SType = StructureType.PipelineDepthStencilStateCreateInfo,
                DepthTestEnable = false,
                DepthWriteEnable = false,
                DepthCompareOp = CompareOp.Always,
                DepthBoundsTestEnable = false,
                StencilTestEnable = false,
            };

            // Unified dual-source blend: shaders output premultiplied color plus a
            // per-channel blend factor. Standard draws emit blendFactor = alpha which
            // reproduces SrcAlpha/OneMinusSrcAlpha exactly; subpixel text emits
            // per-channel ClearType coverage.
            var colorBlendAttachment = new PipelineColorBlendAttachmentState
            {
                BlendEnable = true,
                SrcColorBlendFactor = BlendFactor.One,
                DstColorBlendFactor = _dualSourceBlendEnabled
                    ? BlendFactor.OneMinusSrc1Color : BlendFactor.OneMinusSrcAlpha,
                ColorBlendOp = BlendOp.Add,
                SrcAlphaBlendFactor = BlendFactor.One,
                DstAlphaBlendFactor = _dualSourceBlendEnabled
                    ? BlendFactor.OneMinusSrc1Alpha : BlendFactor.OneMinusSrcAlpha,
                AlphaBlendOp = BlendOp.Add,
                ColorWriteMask = ColorComponentFlags.RBit | ColorComponentFlags.GBit | ColorComponentFlags.BBit | ColorComponentFlags.ABit,
            };

            var colorBlending = new PipelineColorBlendStateCreateInfo
            {
                SType = StructureType.PipelineColorBlendStateCreateInfo,
                LogicOpEnable = false,
                LogicOp = LogicOp.Copy,
                AttachmentCount = 1,
                PAttachments = &colorBlendAttachment,
            };

            const uint dynamicStateCount = 2u;
            var dynamicStates = stackalloc DynamicState[2];
            dynamicStates[0] = DynamicState.Viewport;
            dynamicStates[1] = DynamicState.Scissor;

            var dynamicState = new PipelineDynamicStateCreateInfo
            {
                SType = StructureType.PipelineDynamicStateCreateInfo,
                DynamicStateCount = dynamicStateCount,
                PDynamicStates = dynamicStates,
            };

            // Dynamic rendering: the pipeline declares attachment formats directly
            // instead of referencing a render pass object.
            var swapchainFormat = _swapchainFormat;
            var pipelineRenderingInfo = new PipelineRenderingCreateInfo
            {
                SType = StructureType.PipelineRenderingCreateInfo,
                ColorAttachmentCount = 1,
                PColorAttachmentFormats = &swapchainFormat,
                DepthAttachmentFormat = Format.D32Sfloat,
            };

            var pipelineInfo = new GraphicsPipelineCreateInfo
            {
                SType = StructureType.GraphicsPipelineCreateInfo,
                PNext = &pipelineRenderingInfo,
                StageCount = 2,
                PStages = stages,
                PVertexInputState = &vertexInputInfo,
                PInputAssemblyState = &inputAssembly,
                PViewportState = &viewportState,
                PRasterizationState = &rasterizer,
                PMultisampleState = &multisampling,
                PDepthStencilState = &depthStencil,
                PColorBlendState = &colorBlending,
                PDynamicState = &dynamicState,
                Layout = _pipelineLayout,
                RenderPass = default,
                Subpass = 0,
            };

            if (glyphStages is { } glyph)
            {
                var sampleCount = (uint)_msaaSampleCount;
                var glyphSampleEntry = new SpecializationMapEntry { ConstantID = 0, Size = sizeof(uint) };
                var glyphSpecialization = new SpecializationInfo
                {
                    MapEntryCount = 1, PMapEntries = &glyphSampleEntry,
                    DataSize = sizeof(uint), PData = &sampleCount,
                };
                stages[0].Module = glyph.Vertex;
                stages[0].PSpecializationInfo = null;
                stages[1].Module = glyph.Fragment;
                stages[1].PSpecializationInfo = &glyphSpecialization;
                depthStencil.DepthTestEnable = true;
                depthStencil.DepthWriteEnable = true;
                depthStencil.DepthCompareOp = CompareOp.LessOrEqual;
                Check(_vk.CreateGraphicsPipelines(_device, _pipelineCache, 1, &pipelineInfo, null, glyphPipeline));
                return;
            }

            if (createDepthVariants)
            {
                depthStencil.DepthTestEnable = true;
                depthStencil.DepthCompareOp = CompareOp.LessOrEqual;
                var maskModeBase = sharedCamera ? 6 : sharedBasis ? 4 : compiledLayers ? 2 : 0;
                ref var readPipeline = ref (backdropComposite ? ref _backdropPipelines[maskModeBase + 1 + (circleCutout ? 9 : 0)] : ref (circleCutout ? ref _circleCutoutPipelines[maskModeBase + 1] : ref (sharedCamera ? ref _sharedCameraDepthReadPipeline : ref (sharedBasis ? ref _sharedBasisDepthReadPipeline : ref (compiledLayers ? ref _compiledDepthReadPipeline : ref _depthReadPipeline)))));
                fixed (Pipeline* pipeline = &readPipeline)
                {
                    Check(_vk.CreateGraphicsPipelines(_device, _pipelineCache, 1, &pipelineInfo, null, pipeline));
                }

                depthStencil.DepthWriteEnable = true;
                // Analytic edge coverage must mask color AND depth samples. Material
                // translucency uses the depth-read pipeline and keeps alpha blending.
                // Even one sample needs a coverage mask, rather than translucent
                // color with an opaque depth write. Its edge precision is one bit.
                var opaqueCoverage = true;
                OverrideValidationOpaqueCoverage(ref opaqueCoverage);
                multisampling.AlphaToCoverageEnable = opaqueCoverage;
                if (opaqueCoverage && !_dualSourceBlendEnabled)
                    colorBlendAttachment.DstColorBlendFactor = BlendFactor.Zero;
                specializationValues[4] = opaqueCoverage ? 1u : 0u;
                ref var writePipeline = ref (backdropComposite ? ref _backdropPipelines[maskModeBase + 2 + (circleCutout ? 9 : 0)] : ref (circleCutout ? ref _circleCutoutPipelines[maskModeBase + 2] : ref (sharedCamera ? ref _sharedCameraDepthWritePipeline : ref (sharedBasis ? ref _sharedBasisDepthWritePipeline : ref (compiledLayers ? ref _compiledDepthWritePipeline : ref _depthWritePipeline)))));
                fixed (Pipeline* pipeline = &writePipeline)
                {
                    Check(_vk.CreateGraphicsPipelines(_device, _pipelineCache, 1, &pipelineInfo, null, pipeline));
                }
                if (opaqueCoverage && CanUseDepthMask4 && !circleCutout && !backdropComposite)
                {
                    if (_depthMask4VertexShaderModule.Handle == 0)
                        _depthMask4VertexShaderModule = CreateShaderModule(LoadEmbeddedShader(_viewportClipPlanesEnabled
                            ? "Duxel.Vulkan.Shaders.imgui_depth_mask4.vert.spv"
                            : "Duxel.Vulkan.Shaders.imgui_depth_mask4_integer.vert.spv"));
                    if (_depthMask4FragmentShaderModule.Handle == 0)
                        _depthMask4FragmentShaderModule = CreateShaderModule(LoadEmbeddedShader(ResolveBlendFragmentResource("Duxel.Vulkan.Shaders.imgui_depth_mask4.frag.spv")));
                    multisampling.AlphaToCoverageEnable = false;
                    stages[0].Module = _depthMask4VertexShaderModule;
                    stages[1].Module = _depthMask4FragmentShaderModule;
                    fixed (Pipeline* pipeline = &_materialDepthMaskPipelines[maskModeBase / 2])
                    {
                        Check(_vk.CreateGraphicsPipelines(_device, _pipelineCache, 1, &pipelineInfo, null, pipeline));
                    }
                }
            }
            else
            {
                ref var flatPipeline = ref (projectedCapture ? ref _projectedCapturePipelines[circleCutout ? 1 : 0] : ref (backdropComposite ? ref _backdropPipelines[0 + (circleCutout ? 9 : 0)] : ref (circleCutout ? ref _circleCutoutPipelines[0] : ref _graphicsPipeline)));
                fixed (Pipeline* pipeline = &flatPipeline)
                {
                    Check(_vk.CreateGraphicsPipelines(_device, _pipelineCache, 1, &pipelineInfo, null, pipeline));
                }
            }
        }
        finally
        {
            VulkanMarshaling.Free((nint)entryPoint);
        }
    }

    private unsafe void CreatePipelineCache()
    {
        if (_pipelineCache.Handle is not 0)
        {
            return;
        }

        var data = TryLoadPipelineCacheData();

        fixed (byte* dataPtr = data)
        {
            var cacheInfo = new PipelineCacheCreateInfo
            {
                SType = StructureType.PipelineCacheCreateInfo,
                InitialDataSize = (nuint)(data?.Length ?? 0),
                PInitialData = dataPtr,
            };

            fixed (PipelineCache* cache = &_pipelineCache)
            {
                Check(_vk.CreatePipelineCache(_device, &cacheInfo, null, cache));
            }
        }
    }

    private unsafe void SavePipelineCache()
    {
        if (_pipelineCache.Handle is 0)
        {
            return;
        }

        nuint size = 0;
        Check(_vk.GetPipelineCacheData(_device, _pipelineCache, &size, null));
        if (size is 0)
        {
            return;
        }

        var data = new byte[(int)size];
        fixed (byte* dataPtr = data)
        {
            Check(_vk.GetPipelineCacheData(_device, _pipelineCache, &size, dataPtr));
        }

        var hash = ComputeHash(data);
        if (_pipelineCacheSize == size && _pipelineCacheHash == hash)
        {
            LogPipelineCache("PipelineCache[Unchanged]");
            return;
        }

        if (TrySavePipelineCacheData(data))
        {
            _pipelineCacheSize = size;
            _pipelineCacheHash = hash;
            LogPipelineCache("PipelineCache[Saved]");
        }
    }

    private byte[]? TryLoadPipelineCacheData()
    {
        if (string.IsNullOrWhiteSpace(_pipelineCachePath))
        {
            LogPipelineCache("PipelineCache[UnavailablePath]");
            return null;
        }

        try
        {
            if (!File.Exists(_pipelineCachePath))
            {
                LogPipelineCache("PipelineCache[Miss]");
                return null;
            }

            using var stream = new FileStream(
                _pipelineCachePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                bufferSize: 4096,
                FileOptions.SequentialScan);
            if (stream.Length is <= 0 or > int.MaxValue)
            {
                LogPipelineCache("PipelineCache[Incompatible]");
                return null;
            }

            var data = new byte[(int)stream.Length];
            stream.ReadExactly(data);
            if (!IsCompatiblePipelineCacheData(data))
            {
                LogPipelineCache("PipelineCache[Incompatible]");
                return null;
            }

            _pipelineCacheSize = (nuint)data.Length;
            _pipelineCacheHash = ComputeHash(data);
            LogPipelineCache("PipelineCache[Hit]");
            return data;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException)
        {
            LogPipelineCache($"PipelineCache[ReadUnavailable] {ex.Message}");
            return null;
        }
    }

    private bool IsCompatiblePipelineCacheData(ReadOnlySpan<byte> data)
    {
        const int headerSize = 32;
        const uint headerVersionOne = 1;
        if (data.Length < headerSize
            || BinaryPrimitives.ReadUInt32LittleEndian(data) != headerSize
            || BinaryPrimitives.ReadUInt32LittleEndian(data[4..]) != headerVersionOne
            || BinaryPrimitives.ReadUInt32LittleEndian(data[8..]) != _devicePolicy.VendorId
            || BinaryPrimitives.ReadUInt32LittleEndian(data[12..]) != _devicePolicy.DeviceId)
        {
            return false;
        }

        var expectedUuid = Convert.FromHexString(_devicePolicy.PipelineCacheUuid);
        return expectedUuid.Length is 16
            && data.Slice(16, expectedUuid.Length).SequenceEqual(expectedUuid);
    }

    private bool TrySavePipelineCacheData(ReadOnlySpan<byte> data)
    {
        if (string.IsNullOrWhiteSpace(_pipelineCachePath))
        {
            return false;
        }

        var directory = Path.GetDirectoryName(_pipelineCachePath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return false;
        }

        var temporaryPath = _pipelineCachePath
            + "."
            + Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture)
            + "."
            + Guid.NewGuid().ToString("N")
            + ".tmp";
        try
        {
            Directory.CreateDirectory(directory);
            using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                FileOptions.SequentialScan))
            {
                stream.Write(data);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, _pipelineCachePath, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException)
        {
            LogPipelineCache($"PipelineCache[PersistenceUnavailable] {ex.Message}");
            return false;
        }
        finally
        {
            try
            {
                File.Delete(temporaryPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException)
            {
                LogPipelineCache($"PipelineCache[TemporaryCleanupUnavailable] {ex.Message}");
            }
        }
    }

    private void LogPipelineCache(string message)
    {
        _options.DiagnosticsLog?.Invoke(message);
        Trace.WriteLine($"[Duxel.Vulkan] {message}");
    }

    private static ulong ComputeHash(ReadOnlySpan<byte> data)
    {
        const ulong offset = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;
        ulong hash = offset;
        for (var i = 0; i < data.Length; i++)
        {
            hash ^= data[i];
            hash *= prime;
        }

        return hash;
    }

    private void DestroyPipelineCache()
    {
        if (_pipelineCache.Handle is 0)
        {
            return;
        }

        _vk.DestroyPipelineCache(_device, _pipelineCache, null);
        _pipelineCache = default;
    }

    private ShaderModule CreateShaderModule(ReadOnlySpan<byte> code)
    {
        unsafe
        {
            fixed (byte* codePtr = code)
            {
                var createInfo = new ShaderModuleCreateInfo
                {
                    SType = StructureType.ShaderModuleCreateInfo,
                    CodeSize = (nuint)code.Length,
                    PCode = (uint*)codePtr,
                };

                ShaderModule module = default;
                Check(_vk.CreateShaderModule(_device, &createInfo, null, &module));
                return module;
            }
        }
    }

    private static byte[] LoadEmbeddedShader(string resourceName)
    {
        var assembly = typeof(VulkanRendererBackend).Assembly;
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded shader not found: {resourceName}.");

        if (stream.Length is > int.MaxValue)
        {
            throw new InvalidOperationException($"Embedded shader too large: {resourceName}.");
        }

        var buffer = new byte[(int)stream.Length];
        stream.ReadExactly(buffer);
        return buffer;
    }
}
