using Duxel.Core;
using System.IO;

namespace Duxel.Vulkan;

public sealed unsafe partial class VulkanRendererBackend
{
    private static readonly byte[] VertexShaderSpirv = LoadEmbeddedShader("Duxel.Vulkan.Shaders.imgui.vert.spv");
    private static readonly byte[] FragmentShaderSpirv = LoadEmbeddedShader("Duxel.Vulkan.Shaders.imgui.frag.spv");
    private static readonly byte[] ColorFragmentShaderSpirv = LoadEmbeddedShader("Duxel.Vulkan.Shaders.imgui_color.frag.spv");
    private static readonly byte[] SolidVertexShaderSpirv = LoadEmbeddedShader("Duxel.Vulkan.Shaders.solid.vert.spv");
    private static readonly byte[] SubpixelFragmentShaderSpirv = LoadEmbeddedShader("Duxel.Vulkan.Shaders.imgui_subpixel.frag.spv");
    private static readonly byte[] PrimitiveVertexShaderSpirv = LoadEmbeddedShader("Duxel.Vulkan.Shaders.primitive.vert.spv");
    private static readonly byte[] PrimitiveFragmentShaderSpirv = LoadEmbeddedShader("Duxel.Vulkan.Shaders.primitive.frag.spv");
    private static readonly byte[] PrimitiveColorFragmentShaderSpirv = LoadEmbeddedShader("Duxel.Vulkan.Shaders.primitive_color.frag.spv");

    private DescriptorSetLayout _descriptorSetLayout;
    private PipelineLayout _pipelineLayout;
    private Sampler _fontSampler;
    private Sampler _imageSampler;
    private Pipeline _graphicsPipeline;
    private Pipeline _graphicsColorPipeline;
    private Pipeline _solidColorPipeline;
    private Pipeline _subpixelPipeline;
    private Pipeline _primitivePipeline;
    private Pipeline _primitiveColorPipeline;
    private ShaderModule _vertexShaderModule;
    private ShaderModule _fragmentShaderModule;
    private ShaderModule _colorFragmentShaderModule;
    private ShaderModule _solidVertexShaderModule;
    private ShaderModule _subpixelFragmentShaderModule;
    private ShaderModule _primitiveVertexShaderModule;
    private ShaderModule _primitiveFragmentShaderModule;
    private ShaderModule _primitiveColorFragmentShaderModule;
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
            DescriptorCount = 1,
            DescriptorType = DescriptorType.CombinedImageSampler,
            StageFlags = ShaderStageFlags.FragmentBit,
        };

        var descriptorLayoutInfo = new DescriptorSetLayoutCreateInfo
        {
            SType = StructureType.DescriptorSetLayoutCreateInfo,
            BindingCount = 1,
            PBindings = &samplerBinding,
        };

        fixed (DescriptorSetLayout* layout = &_descriptorSetLayout)
        {
            Check(_vk.CreateDescriptorSetLayout(_device, &descriptorLayoutInfo, null, layout));
        }

        var pushConstantRange = new PushConstantRange
        {
            StageFlags = ShaderStageFlags.VertexBit,
            Offset = 0,
            Size = (uint)(sizeof(float) * 5),
        };

        var pipelineLayoutInfo = new PipelineLayoutCreateInfo
        {
            SType = StructureType.PipelineLayoutCreateInfo,
            SetLayoutCount = 1,
            PushConstantRangeCount = 1,
            PPushConstantRanges = &pushConstantRange,
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

        const uint poolSize = 1000;
        const int poolSizeCount = 11;
        var poolSizes = stackalloc DescriptorPoolSize[poolSizeCount];
        poolSizes[0] = new DescriptorPoolSize(DescriptorType.Sampler, poolSize);
        poolSizes[1] = new DescriptorPoolSize(DescriptorType.CombinedImageSampler, poolSize);
        poolSizes[2] = new DescriptorPoolSize(DescriptorType.SampledImage, poolSize);
        poolSizes[3] = new DescriptorPoolSize(DescriptorType.StorageImage, poolSize);
        poolSizes[4] = new DescriptorPoolSize(DescriptorType.UniformTexelBuffer, poolSize);
        poolSizes[5] = new DescriptorPoolSize(DescriptorType.StorageTexelBuffer, poolSize);
        poolSizes[6] = new DescriptorPoolSize(DescriptorType.UniformBuffer, poolSize);
        poolSizes[7] = new DescriptorPoolSize(DescriptorType.StorageBuffer, poolSize);
        poolSizes[8] = new DescriptorPoolSize(DescriptorType.UniformBufferDynamic, poolSize);
        poolSizes[9] = new DescriptorPoolSize(DescriptorType.StorageBufferDynamic, poolSize);
        poolSizes[10] = new DescriptorPoolSize(DescriptorType.InputAttachment, poolSize);

        var poolInfo = new DescriptorPoolCreateInfo
        {
            SType = StructureType.DescriptorPoolCreateInfo,
            PoolSizeCount = (uint)poolSizeCount,
            PPoolSizes = poolSizes,
            MaxSets = poolSize * poolSizeCount,
            Flags = DescriptorPoolCreateFlags.FreeDescriptorSetBit,
        };

        fixed (DescriptorPool* pool = &_descriptorPool)
        {
            Check(_vk.CreateDescriptorPool(_device, &poolInfo, null, pool));
        }
    }

    private unsafe void CreateGraphicsPipeline()
    {
        _vertexShaderModule = CreateShaderModule(VertexShaderSpirv);
        _fragmentShaderModule = CreateShaderModule(FragmentShaderSpirv);
        _primitiveVertexShaderModule = CreateShaderModule(PrimitiveVertexShaderSpirv);
        _primitiveFragmentShaderModule = CreateShaderModule(PrimitiveFragmentShaderSpirv);
        _primitiveColorFragmentShaderModule = CreateShaderModule(PrimitiveColorFragmentShaderSpirv);

        var entryPoint = VulkanMarshaling.StringToPtr("main");

        try
        {
            var vertexStage = new PipelineShaderStageCreateInfo
            {
                SType = StructureType.PipelineShaderStageCreateInfo,
                Stage = ShaderStageFlags.VertexBit,
                Module = _vertexShaderModule,
                PName = entryPoint,
            };

            var fragmentStage = new PipelineShaderStageCreateInfo
            {
                SType = StructureType.PipelineShaderStageCreateInfo,
                Stage = ShaderStageFlags.FragmentBit,
                Module = _fragmentShaderModule,
                PName = entryPoint,
            };

            var stages = stackalloc PipelineShaderStageCreateInfo[2];
            stages[0] = vertexStage;
            stages[1] = fragmentStage;

            var bindingDescription = new VertexInputBindingDescription
            {
                Binding = 0,
                Stride = (uint)sizeof(UiVertex),
                InputRate = VertexInputRate.Vertex,
            };

            var attributeDescriptions = stackalloc VertexInputAttributeDescription[3];
            const uint positionOffset = 0;
            const uint uvOffset = 8;
            const uint colorOffset = 16;

            attributeDescriptions[0] = new VertexInputAttributeDescription
            {
                Location = 0,
                Binding = 0,
                Format = Format.R32G32Sfloat,
                Offset = positionOffset,
            };
            attributeDescriptions[1] = new VertexInputAttributeDescription
            {
                Location = 1,
                Binding = 0,
                Format = Format.R32G32Sfloat,
                Offset = uvOffset,
            };
            attributeDescriptions[2] = new VertexInputAttributeDescription
            {
                Location = 2,
                Binding = 0,
                Format = Format.R8G8B8A8Unorm,
                Offset = colorOffset,
            };

            var vertexInputInfo = new PipelineVertexInputStateCreateInfo
            {
                SType = StructureType.PipelineVertexInputStateCreateInfo,
                VertexBindingDescriptionCount = 1,
                PVertexBindingDescriptions = &bindingDescription,
                VertexAttributeDescriptionCount = 3,
                PVertexAttributeDescriptions = attributeDescriptions,
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

            var colorBlendAttachment = new PipelineColorBlendAttachmentState
            {
                BlendEnable = true,
                SrcColorBlendFactor = BlendFactor.SrcAlpha,
                DstColorBlendFactor = BlendFactor.OneMinusSrcAlpha,
                ColorBlendOp = BlendOp.Add,
                SrcAlphaBlendFactor = BlendFactor.One,
                DstAlphaBlendFactor = BlendFactor.OneMinusSrcAlpha,
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

            var pipelineInfo = new GraphicsPipelineCreateInfo
            {
                SType = StructureType.GraphicsPipelineCreateInfo,
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
                RenderPass = _renderPass,
                Subpass = 0,
            };

            fixed (Pipeline* pipeline = &_graphicsPipeline)
            {
                Check(_vk.CreateGraphicsPipelines(_device, _pipelineCache, 1, &pipelineInfo, null, pipeline));
            }

            if (_triangleColorPipelineEnabled)
            {
                _colorFragmentShaderModule = CreateShaderModule(ColorFragmentShaderSpirv);
                var colorFragmentStage = new PipelineShaderStageCreateInfo
                {
                    SType = StructureType.PipelineShaderStageCreateInfo,
                    Stage = ShaderStageFlags.FragmentBit,
                    Module = _colorFragmentShaderModule,
                    PName = entryPoint,
                };

                var colorStages = stackalloc PipelineShaderStageCreateInfo[2];
                colorStages[0] = vertexStage;
                colorStages[1] = colorFragmentStage;

                pipelineInfo.PStages = colorStages;
                fixed (Pipeline* pipeline = &_graphicsColorPipeline)
                {
                    Check(_vk.CreateGraphicsPipelines(_device, _pipelineCache, 1, &pipelineInfo, null, pipeline));
                }

                pipelineInfo.PStages = stages;
            }

            _subpixelFragmentShaderModule = CreateShaderModule(SubpixelFragmentShaderSpirv);

            var subpixelFragmentStage = new PipelineShaderStageCreateInfo
            {
                SType = StructureType.PipelineShaderStageCreateInfo,
                Stage = ShaderStageFlags.FragmentBit,
                Module = _subpixelFragmentShaderModule,
                PName = entryPoint,
            };

            var subpixelStages = stackalloc PipelineShaderStageCreateInfo[2];
            subpixelStages[0] = vertexStage;
            subpixelStages[1] = subpixelFragmentStage;

            var subpixelBlendAttachment = new PipelineColorBlendAttachmentState
            {
                BlendEnable = true,
                SrcColorBlendFactor = BlendFactor.One,
                DstColorBlendFactor = BlendFactor.OneMinusSrc1Color,
                ColorBlendOp = BlendOp.Add,
                SrcAlphaBlendFactor = BlendFactor.One,
                DstAlphaBlendFactor = BlendFactor.OneMinusSrc1Alpha,
                AlphaBlendOp = BlendOp.Add,
                ColorWriteMask = ColorComponentFlags.RBit | ColorComponentFlags.GBit | ColorComponentFlags.BBit | ColorComponentFlags.ABit,
            };

            var subpixelColorBlending = new PipelineColorBlendStateCreateInfo
            {
                SType = StructureType.PipelineColorBlendStateCreateInfo,
                LogicOpEnable = false,
                LogicOp = LogicOp.Copy,
                AttachmentCount = 1,
                PAttachments = &subpixelBlendAttachment,
            };

            var subpixelPipelineInfo = new GraphicsPipelineCreateInfo
            {
                SType = StructureType.GraphicsPipelineCreateInfo,
                StageCount = 2,
                PStages = subpixelStages,
                PVertexInputState = &vertexInputInfo,
                PInputAssemblyState = &inputAssembly,
                PViewportState = &viewportState,
                PRasterizationState = &rasterizer,
                PMultisampleState = &multisampling,
                PDepthStencilState = &depthStencil,
                PColorBlendState = &subpixelColorBlending,
                PDynamicState = &dynamicState,
                Layout = _pipelineLayout,
                RenderPass = _renderPass,
                Subpass = 0,
            };

            fixed (Pipeline* pipeline = &_subpixelPipeline)
            {
                Check(_vk.CreateGraphicsPipelines(_device, _pipelineCache, 1, &subpixelPipelineInfo, null, pipeline));
            }

            var primitiveVertexStage = new PipelineShaderStageCreateInfo
            {
                SType = StructureType.PipelineShaderStageCreateInfo,
                Stage = ShaderStageFlags.VertexBit,
                Module = _primitiveVertexShaderModule,
                PName = entryPoint,
            };

            var primitiveFragmentStage = new PipelineShaderStageCreateInfo
            {
                SType = StructureType.PipelineShaderStageCreateInfo,
                Stage = ShaderStageFlags.FragmentBit,
                Module = _primitiveFragmentShaderModule,
                PName = entryPoint,
            };

            var primitiveStages = stackalloc PipelineShaderStageCreateInfo[2];
            primitiveStages[0] = primitiveVertexStage;
            primitiveStages[1] = primitiveFragmentStage;

            var primitiveBindingDescription = new VertexInputBindingDescription
            {
                Binding = 1,
                Stride = (uint)sizeof(PrimitiveInstance),
                InputRate = VertexInputRate.Instance,
            };

            var primitiveAttributeDescriptions = stackalloc VertexInputAttributeDescription[3];
            primitiveAttributeDescriptions[0] = new VertexInputAttributeDescription
            {
                Location = 0,
                Binding = 1,
                Format = Format.R32G32B32Sfloat,
                Offset = 0,
            };
            primitiveAttributeDescriptions[1] = new VertexInputAttributeDescription
            {
                Location = 1,
                Binding = 1,
                Format = Format.R32Uint,
                Offset = 12,
            };
            primitiveAttributeDescriptions[2] = new VertexInputAttributeDescription
            {
                Location = 2,
                Binding = 1,
                Format = Format.R8G8B8A8Unorm,
                Offset = 16,
            };

            var primitiveVertexInputInfo = new PipelineVertexInputStateCreateInfo
            {
                SType = StructureType.PipelineVertexInputStateCreateInfo,
                VertexBindingDescriptionCount = 1,
                PVertexBindingDescriptions = &primitiveBindingDescription,
                VertexAttributeDescriptionCount = 3,
                PVertexAttributeDescriptions = primitiveAttributeDescriptions,
            };

            var primitivePipelineInfo = new GraphicsPipelineCreateInfo
            {
                SType = StructureType.GraphicsPipelineCreateInfo,
                StageCount = 2,
                PStages = primitiveStages,
                PVertexInputState = &primitiveVertexInputInfo,
                PInputAssemblyState = &inputAssembly,
                PViewportState = &viewportState,
                PRasterizationState = &rasterizer,
                PMultisampleState = &multisampling,
                PDepthStencilState = &depthStencil,
                PColorBlendState = &colorBlending,
                PDynamicState = &dynamicState,
                Layout = _pipelineLayout,
                RenderPass = _renderPass,
                Subpass = 0,
            };

            fixed (Pipeline* pipeline = &_primitivePipeline)
            {
                Check(_vk.CreateGraphicsPipelines(_device, _pipelineCache, 1, &primitivePipelineInfo, null, pipeline));
            }

            var primitiveColorFragmentStage = new PipelineShaderStageCreateInfo
            {
                SType = StructureType.PipelineShaderStageCreateInfo,
                Stage = ShaderStageFlags.FragmentBit,
                Module = _primitiveColorFragmentShaderModule,
                PName = entryPoint,
            };

            var primitiveColorStages = stackalloc PipelineShaderStageCreateInfo[2];
            primitiveColorStages[0] = primitiveVertexStage;
            primitiveColorStages[1] = primitiveColorFragmentStage;

            primitivePipelineInfo.PStages = primitiveColorStages;
            fixed (Pipeline* pipeline = &_primitiveColorPipeline)
            {
                Check(_vk.CreateGraphicsPipelines(_device, _pipelineCache, 1, &primitivePipelineInfo, null, pipeline));
            }

            if (_solidUnifiedPipelineEnabled && _triangleColorPipelineEnabled)
            {
                _solidVertexShaderModule = CreateShaderModule(SolidVertexShaderSpirv);
                var solidVertexStage = new PipelineShaderStageCreateInfo
                {
                    SType = StructureType.PipelineShaderStageCreateInfo,
                    Stage = ShaderStageFlags.VertexBit,
                    Module = _solidVertexShaderModule,
                    PName = entryPoint,
                };

                var solidStages = stackalloc PipelineShaderStageCreateInfo[2];
                solidStages[0] = solidVertexStage;
                solidStages[1] = primitiveColorFragmentStage;

                var solidBindingDescriptions = stackalloc VertexInputBindingDescription[2];
                solidBindingDescriptions[0] = bindingDescription;
                solidBindingDescriptions[1] = primitiveBindingDescription;

                var solidAttributeDescriptions = stackalloc VertexInputAttributeDescription[5];
                solidAttributeDescriptions[0] = new VertexInputAttributeDescription
                {
                    Location = 0,
                    Binding = 0,
                    Format = Format.R32G32Sfloat,
                    Offset = positionOffset,
                };
                solidAttributeDescriptions[1] = new VertexInputAttributeDescription
                {
                    Location = 2,
                    Binding = 0,
                    Format = Format.R8G8B8A8Unorm,
                    Offset = colorOffset,
                };
                solidAttributeDescriptions[2] = new VertexInputAttributeDescription
                {
                    Location = 3,
                    Binding = 1,
                    Format = Format.R32G32B32Sfloat,
                    Offset = 0,
                };
                solidAttributeDescriptions[3] = new VertexInputAttributeDescription
                {
                    Location = 4,
                    Binding = 1,
                    Format = Format.R32Uint,
                    Offset = 12,
                };
                solidAttributeDescriptions[4] = new VertexInputAttributeDescription
                {
                    Location = 5,
                    Binding = 1,
                    Format = Format.R8G8B8A8Unorm,
                    Offset = 16,
                };

                var solidVertexInputInfo = new PipelineVertexInputStateCreateInfo
                {
                    SType = StructureType.PipelineVertexInputStateCreateInfo,
                    VertexBindingDescriptionCount = 2,
                    PVertexBindingDescriptions = solidBindingDescriptions,
                    VertexAttributeDescriptionCount = 5,
                    PVertexAttributeDescriptions = solidAttributeDescriptions,
                };

                var solidPipelineInfo = primitivePipelineInfo;
                solidPipelineInfo.PStages = solidStages;
                solidPipelineInfo.PVertexInputState = &solidVertexInputInfo;

                fixed (Pipeline* pipeline = &_solidColorPipeline)
                {
                    Check(_vk.CreateGraphicsPipelines(_device, _pipelineCache, 1, &solidPipelineInfo, null, pipeline));
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

        byte[]? data = null;
        if (File.Exists(_pipelineCachePath))
        {
            data = File.ReadAllBytes(_pipelineCachePath);
            if (data.Length > 0)
            {
                _pipelineCacheSize = (nuint)data.Length;
                _pipelineCacheHash = ComputeHash(data);
            }
        }

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
            return;
        }

        var directory = Path.GetDirectoryName(_pipelineCachePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllBytes(_pipelineCachePath, data);
        _pipelineCacheSize = size;
        _pipelineCacheHash = hash;
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
