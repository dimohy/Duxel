using Duxel.Core;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;

namespace Duxel.Vulkan;

public sealed unsafe partial class VulkanRendererBackend
{
    private static readonly string[] BaseDeviceExtensions =
    [
        "VK_KHR_swapchain",
        "VK_KHR_dynamic_rendering",
        "VK_KHR_depth_stencil_resolve",
        "VK_KHR_create_renderpass2",
    ];

    private readonly IVulkanSurfaceSource _surfaceSource;
    private KhrSurface _khrSurface = null!;
    private KhrSwapchain _khrSwapchain = null!;
    private KhrDynamicRendering _khrDynamicRendering = null!;
    private Instance _instance;
    private SurfaceKHR _surface;
    private PhysicalDevice _physicalDevice;
    private Device _device;
    private Queue _graphicsQueue;
    private Queue _transferQueue;
    private uint _graphicsQueueFamily;
    private uint _transferQueueFamily;
    private uint _dedicatedTransferQueueFamily;
    private VulkanDevicePolicy _devicePolicy;

    private void ConfigureMsaaSampleCount()
    {
        var requested = _options.MsaaSamples;
        if (requested is not (1 or 2 or 4 or 8))
        {
            requested = 4;
        }

        var supported = _devicePolicy.FramebufferColorSampleCounts;

        _msaaSampleCount = requested switch
        {
            >= 8 when (supported & SampleCountFlags.Count8Bit) != 0 => SampleCountFlags.Count8Bit,
            >= 4 when (supported & SampleCountFlags.Count4Bit) != 0 => SampleCountFlags.Count4Bit,
            >= 2 when (supported & SampleCountFlags.Count2Bit) != 0 => SampleCountFlags.Count2Bit,
            _ => SampleCountFlags.Count1Bit,
        };
    }

    private unsafe void CreateInstance()
    {
        var requiredExtensions = _surfaceSource.RequiredInstanceExtensions;
        var extensionList = new List<string>(requiredExtensions.Count);
        for (var i = 0; i < requiredExtensions.Count; i++)
        {
            extensionList.Add(requiredExtensions[i]);
        }

        var extensions = extensionList.ToArray();

        var appInfo = new ApplicationInfo
        {
            SType = StructureType.ApplicationInfo,
            ApiVersion = Vk.Version12,
            PApplicationName = VulkanMarshaling.StringToPtr("Duxel"),
            PEngineName = VulkanMarshaling.StringToPtr("Duxel"),
            EngineVersion = new Version32(1, 0, 0),
            ApplicationVersion = new Version32(1, 0, 0),
        };

        var extensionPtr = VulkanMarshaling.StringArrayToPtr(extensions);

        try
        {
            var createInfo = new InstanceCreateInfo
            {
                SType = StructureType.InstanceCreateInfo,
                PApplicationInfo = &appInfo,
                EnabledExtensionCount = (uint)extensions.Length,
                PpEnabledExtensionNames = (byte**)extensionPtr,
            };

            fixed (Instance* instance = &_instance)
            {
                Check(_vk.CreateInstance(&createInfo, null, instance));
            }
        }
        finally
        {
            VulkanMarshaling.Free((nint)appInfo.PApplicationName);
            VulkanMarshaling.Free((nint)appInfo.PEngineName);
            VulkanMarshaling.FreeStringArray(extensionPtr, extensions.Length);
        }
    }

    private void LoadInstanceExtensions()
    {
        if (!KhrSurface.TryCreate(_vk, _instance, out _khrSurface))
        {
            throw new InvalidOperationException("Failed to load VK_KHR_surface extension.");
        }
    }

    private void CreateSurface()
    {
        var surfaceHandle = _surfaceSource.CreateSurface((nuint)_instance.Handle);
        _surface = new SurfaceKHR((ulong)surfaceHandle);
    }

    private unsafe void PickPhysicalDevice()
    {
        uint deviceCount = 0;
        Check(_vk.EnumeratePhysicalDevices(_instance, &deviceCount, null));

        if (deviceCount is 0)
        {
            throw new InvalidOperationException("No Vulkan physical devices found.");
        }

        var devices = stackalloc PhysicalDevice[(int)deviceCount];
        Check(_vk.EnumeratePhysicalDevices(_instance, &deviceCount, devices));

        for (var i = 0; i < deviceCount; i++)
        {
            var device = devices[i];
            if (TryFindGraphicsQueue(device, out var queueFamily, out var graphicsQueueTimestampValidBits))
            {
                _physicalDevice = device;
                _graphicsQueueFamily = queueFamily;
                _dedicatedTransferQueueFamily = FindDedicatedTransferQueueFamily(device, queueFamily);
                _vk.GetPhysicalDeviceProperties(device, out var properties);
                _devicePolicy = CreateDevicePolicy(
                    properties,
                    graphicsQueueTimestampValidBits,
                    _graphicsQueueFamily,
                    _dedicatedTransferQueueFamily);
                _transferQueueFamily = _devicePolicy.UseGraphicsQueueForUploads
                    ? _graphicsQueueFamily
                    : _dedicatedTransferQueueFamily;
                _pipelineCachePath = CreatePipelineCachePath(_devicePolicy);
                return;
            }
        }

        throw new InvalidOperationException("No suitable Vulkan physical device found.");
    }

    private unsafe bool TryFindGraphicsQueue(
        PhysicalDevice device,
        out uint queueFamily,
        out uint timestampValidBits)
    {
        uint count = 0;
        _vk.GetPhysicalDeviceQueueFamilyProperties(device, &count, null);
        var families = stackalloc QueueFamilyProperties[(int)count];
        _vk.GetPhysicalDeviceQueueFamilyProperties(device, &count, families);

        for (var i = 0u; i < count; i++)
        {
            var props = families[i];
            if ((props.QueueFlags & QueueFlags.GraphicsBit) is 0)
            {
                continue;
            }

            Bool32 supportsPresent;
            Check(_khrSurface.GetPhysicalDeviceSurfaceSupport(device, i, _surface, &supportsPresent));

            if (supportsPresent.Value is 0)
            {
                continue;
            }

            queueFamily = i;
            timestampValidBits = props.TimestampValidBits;
            return true;
        }

        queueFamily = 0;
        timestampValidBits = 0;
        return false;
    }

    private unsafe uint FindDedicatedTransferQueueFamily(PhysicalDevice device, uint graphicsQueueFamily)
    {
        uint count = 0;
        _vk.GetPhysicalDeviceQueueFamilyProperties(device, &count, null);
        var families = stackalloc QueueFamilyProperties[(int)count];
        _vk.GetPhysicalDeviceQueueFamilyProperties(device, &count, families);

        for (var i = 0u; i < count; i++)
        {
            if (i == graphicsQueueFamily)
            {
                continue;
            }

            var flags = families[i].QueueFlags;
            if ((flags & QueueFlags.TransferBit) == 0)
            {
                continue;
            }

            if ((flags & QueueFlags.GraphicsBit) != 0)
            {
                continue;
            }

            return i;
        }

        return graphicsQueueFamily;
    }

    private unsafe void CreateDevice()
    {
        var queuePriority = 1.0f;
        var queueCreateInfos = stackalloc DeviceQueueCreateInfo[2];
        var queueCreateInfoCount = 0u;

        queueCreateInfos[queueCreateInfoCount++] = new DeviceQueueCreateInfo
        {
            SType = StructureType.DeviceQueueCreateInfo,
            QueueFamilyIndex = _graphicsQueueFamily,
            QueueCount = 1,
            PQueuePriorities = &queuePriority,
        };

        if (_transferQueueFamily != _graphicsQueueFamily)
        {
            queueCreateInfos[queueCreateInfoCount++] = new DeviceQueueCreateInfo
            {
                SType = StructureType.DeviceQueueCreateInfo,
                QueueFamilyIndex = _transferQueueFamily,
                QueueCount = 1,
                PQueuePriorities = &queuePriority,
            };
        }

        var extensionPtr = VulkanMarshaling.StringArrayToPtr(BaseDeviceExtensions);

        try
        {
            RequireGpuDrivenRendererFeatures();

            var enabledFeatures = new PhysicalDeviceFeatures
            {
                DualSrcBlend = 1,
                ShaderSampledImageArrayDynamicIndexing = 1,
            };

            var dynamicRenderingFeatures = new PhysicalDeviceDynamicRenderingFeatures
            {
                SType = StructureType.PhysicalDeviceDynamicRenderingFeatures,
                DynamicRendering = 1,
            };

            var bufferDeviceAddressFeatures = new PhysicalDeviceBufferDeviceAddressFeatures
            {
                SType = StructureType.PhysicalDeviceBufferDeviceAddressFeatures,
                PNext = &dynamicRenderingFeatures,
                BufferDeviceAddress = 1,
            };

            var descriptorIndexingFeatures = new PhysicalDeviceDescriptorIndexingFeatures
            {
                SType = StructureType.PhysicalDeviceDescriptorIndexingFeatures,
                PNext = &bufferDeviceAddressFeatures,
                DescriptorBindingSampledImageUpdateAfterBind = 1,
                DescriptorBindingUpdateUnusedWhilePending = 1,
                DescriptorBindingPartiallyBound = 1,
                RuntimeDescriptorArray = 1,
            };

            var deviceCreateInfo = new DeviceCreateInfo
            {
                SType = StructureType.DeviceCreateInfo,
                PNext = &descriptorIndexingFeatures,
                QueueCreateInfoCount = queueCreateInfoCount,
                PQueueCreateInfos = queueCreateInfos,
                EnabledExtensionCount = (uint)BaseDeviceExtensions.Length,
                PpEnabledExtensionNames = (byte**)extensionPtr,
                PEnabledFeatures = &enabledFeatures,
            };

            fixed (Device* device = &_device)
            {
                Check(_vk.CreateDevice(_physicalDevice, &deviceCreateInfo, null, device));
            }

            _vk.GetDeviceQueue(_device, _graphicsQueueFamily, 0, out _graphicsQueue);
            _vk.GetDeviceQueue(_device, _transferQueueFamily, 0, out _transferQueue);
        }
        finally
        {
            VulkanMarshaling.FreeStringArray(extensionPtr, BaseDeviceExtensions.Length);
        }
    }

    private void RequireGpuDrivenRendererFeatures()
    {
        var dynamicRenderingFeatures = new PhysicalDeviceDynamicRenderingFeatures
        {
            SType = StructureType.PhysicalDeviceDynamicRenderingFeatures,
        };
        var bufferDeviceAddressFeatures = new PhysicalDeviceBufferDeviceAddressFeatures
        {
            SType = StructureType.PhysicalDeviceBufferDeviceAddressFeatures,
            PNext = &dynamicRenderingFeatures,
        };
        var descriptorIndexingFeatures = new PhysicalDeviceDescriptorIndexingFeatures
        {
            SType = StructureType.PhysicalDeviceDescriptorIndexingFeatures,
            PNext = &bufferDeviceAddressFeatures,
        };
        var features2 = new PhysicalDeviceFeatures2
        {
            SType = StructureType.PhysicalDeviceFeatures2,
            PNext = &descriptorIndexingFeatures,
        };
        _vk.GetPhysicalDeviceFeatures2(_physicalDevice, &features2);

        if (features2.Features.ShaderSampledImageArrayDynamicIndexing is 0
            || descriptorIndexingFeatures.DescriptorBindingSampledImageUpdateAfterBind is 0
            || descriptorIndexingFeatures.DescriptorBindingUpdateUnusedWhilePending is 0
            || descriptorIndexingFeatures.DescriptorBindingPartiallyBound is 0
            || descriptorIndexingFeatures.RuntimeDescriptorArray is 0
            || bufferDeviceAddressFeatures.BufferDeviceAddress is 0
            || dynamicRenderingFeatures.DynamicRendering is 0)
        {
            throw new InvalidOperationException(
                "Selected GPU does not support the Vulkan features required for the GPU-driven renderer "
                + "(shaderSampledImageArrayDynamicIndexing, descriptorBindingSampledImageUpdateAfterBind, "
                + "descriptorBindingUpdateUnusedWhilePending, descriptorBindingPartiallyBound, runtimeDescriptorArray, "
                + "bufferDeviceAddress, dynamicRendering).");
        }
    }

    private void LoadDeviceExtensions()
    {
        if (!KhrSwapchain.TryCreate(_vk, _instance, _device, out _khrSwapchain))
        {
            throw new InvalidOperationException("Failed to load VK_KHR_swapchain extension.");
        }

        if (!KhrDynamicRendering.TryCreate(_vk, _instance, _device, out _khrDynamicRendering))
        {
            throw new InvalidOperationException("Failed to load VK_KHR_dynamic_rendering extension.");
        }
    }

    private enum VulkanGpuVendor
    {
        Unknown,
        Nvidia,
        Amd,
        Intel,
    }

    private enum VulkanUploadQueueMode
    {
        Auto,
        Graphics,
        Transfer,
    }

    private enum VulkanStaticPrimitiveTriangleMode
    {
        Auto,
        Enabled,
        Disabled,
    }

    private enum VulkanStaticGeometryUpdateMode
    {
        Auto,
        Replace,
        InPlace,
        Rotating,
    }

    private readonly VulkanStaticPrimitiveTriangleMode _staticPrimitiveTriangleMode = ParseStaticPrimitiveTriangleMode();
    private readonly VulkanStaticGeometryUpdateMode _staticGeometryUpdateMode = ParseStaticGeometryUpdateMode();

    private bool _staticPrimitiveTrianglesEnabled;
    private bool _staticGeometryInPlaceUpdateEnabled;
    private bool _staticGeometryRotatingUpdateEnabled;
    private VulkanStaticGeometryUpdateMode _resolvedStaticGeometryUpdateMode;

    private readonly record struct VulkanDevicePolicy(
        VulkanGpuVendor Vendor,
        uint VendorId,
        uint DeviceId,
        PhysicalDeviceType DeviceType,
        string DeviceName,
        string PipelineCacheUuid,
        SampleCountFlags FramebufferColorSampleCounts,
        uint GraphicsQueueTimestampValidBits,
        float TimestampPeriodNanoseconds,
        bool UseGraphicsQueueForUploads,
        bool DedicatedTransferQueueCandidate,
        int StaticSecondaryMinDrawCount)
    {
        public bool SupportsGraphicsQueueTimestamps =>
            GraphicsQueueTimestampValidBits > 0 && TimestampPeriodNanoseconds > 0f;
    }

    private static VulkanDevicePolicy CreateDevicePolicy(
        PhysicalDeviceProperties properties,
        uint graphicsQueueTimestampValidBits,
        uint graphicsQueueFamily,
        uint transferQueueFamily)
    {
        var vendor = ClassifyGpuVendor(properties.VendorID);
        var uploadQueueMode = ParseUploadQueueMode();
        return new VulkanDevicePolicy(
            vendor,
            properties.VendorID,
            properties.DeviceID,
            properties.DeviceType,
            GetPhysicalDeviceName(properties),
            GetPipelineCacheUuidHex(properties),
            properties.Limits.FramebufferColorSampleCounts,
            graphicsQueueTimestampValidBits,
            properties.Limits.TimestampPeriod,
            UseGraphicsQueueForUploads: ResolveUseGraphicsQueueForUploads(
                uploadQueueMode,
                graphicsQueueFamily,
                transferQueueFamily),
            DedicatedTransferQueueCandidate: transferQueueFamily != graphicsQueueFamily,
            StaticSecondaryMinDrawCount: GetStaticSecondaryMinDrawCount(vendor));
    }

    private static VulkanUploadQueueMode ParseUploadQueueMode()
    {
        var raw = Environment.GetEnvironmentVariable("DUXEL_VK_UPLOAD_QUEUE");
        if (string.IsNullOrWhiteSpace(raw))
        {
            return VulkanUploadQueueMode.Auto;
        }

        var value = raw.Trim();
        if (string.Equals(value, "transfer", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "dedicated-transfer", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "xfer", StringComparison.OrdinalIgnoreCase))
        {
            return VulkanUploadQueueMode.Transfer;
        }

        if (string.Equals(value, "graphics", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "gfx", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "0", StringComparison.Ordinal)
            || string.Equals(value, "false", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "off", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "no", StringComparison.OrdinalIgnoreCase))
        {
            return VulkanUploadQueueMode.Graphics;
        }

        return VulkanUploadQueueMode.Auto;
    }

    private static bool ResolveUseGraphicsQueueForUploads(
        VulkanUploadQueueMode uploadQueueMode,
        uint graphicsQueueFamily,
        uint transferQueueFamily)
    {
        return uploadQueueMode switch
        {
            VulkanUploadQueueMode.Transfer => transferQueueFamily == graphicsQueueFamily,
            _ => true,
        };
    }

    private static VulkanGpuVendor ClassifyGpuVendor(uint vendorId)
    {
        return vendorId switch
        {
            0x10DE => VulkanGpuVendor.Nvidia,
            0x1002 => VulkanGpuVendor.Amd,
            0x1022 => VulkanGpuVendor.Amd,
            0x8086 => VulkanGpuVendor.Intel,
            _ => VulkanGpuVendor.Unknown,
        };
    }

    private static int GetStaticSecondaryMinDrawCount(VulkanGpuVendor vendor)
    {
        return vendor switch
        {
            VulkanGpuVendor.Nvidia => 10,
            VulkanGpuVendor.Amd => 16,
            VulkanGpuVendor.Intel => 24,
            _ => 16,
        };
    }

    private static VulkanStaticPrimitiveTriangleMode ParseStaticPrimitiveTriangleMode()
    {
        var raw = Environment.GetEnvironmentVariable("DUXEL_VK_STATIC_PRIMITIVE_TRIANGLES");
        if (string.IsNullOrWhiteSpace(raw))
        {
            return VulkanStaticPrimitiveTriangleMode.Auto;
        }

        var value = raw.Trim();
        if (string.Equals(value, "auto", StringComparison.OrdinalIgnoreCase))
        {
            return VulkanStaticPrimitiveTriangleMode.Auto;
        }

        if (string.Equals(value, "1", StringComparison.Ordinal)
            || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "on", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase))
        {
            return VulkanStaticPrimitiveTriangleMode.Enabled;
        }

        if (string.Equals(value, "0", StringComparison.Ordinal)
            || string.Equals(value, "false", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "off", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "no", StringComparison.OrdinalIgnoreCase))
        {
            return VulkanStaticPrimitiveTriangleMode.Disabled;
        }

        return VulkanStaticPrimitiveTriangleMode.Auto;
    }

    private static VulkanStaticGeometryUpdateMode ParseStaticGeometryUpdateMode()
    {
        var raw = Environment.GetEnvironmentVariable("DUXEL_VK_STATIC_GEOMETRY_UPDATE");
        if (!string.IsNullOrWhiteSpace(raw))
        {
            return ParseStaticGeometryUpdateModeValue(raw);
        }

        var hasLegacyRotating = TryParseBooleanEnvironmentFlag("DUXEL_VK_STATIC_GEOMETRY_ROTATING_UPDATE", out var legacyRotating);
        if (hasLegacyRotating && legacyRotating)
        {
            return VulkanStaticGeometryUpdateMode.Rotating;
        }

        var hasLegacyInPlace = TryParseBooleanEnvironmentFlag("DUXEL_VK_STATIC_GEOMETRY_INPLACE_UPDATE", out var legacyInPlace);
        if (hasLegacyInPlace && legacyInPlace)
        {
            return VulkanStaticGeometryUpdateMode.InPlace;
        }

        return hasLegacyRotating || hasLegacyInPlace
            ? VulkanStaticGeometryUpdateMode.Replace
            : VulkanStaticGeometryUpdateMode.Auto;
    }

    private static VulkanStaticGeometryUpdateMode ParseStaticGeometryUpdateModeValue(string raw)
    {
        var value = raw.Trim();
        if (string.Equals(value, "auto", StringComparison.OrdinalIgnoreCase))
        {
            return VulkanStaticGeometryUpdateMode.Auto;
        }

        if (string.Equals(value, "rotating", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "rotate", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "reuse", StringComparison.OrdinalIgnoreCase))
        {
            return VulkanStaticGeometryUpdateMode.Rotating;
        }

        if (string.Equals(value, "inplace", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "in-place", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "overwrite", StringComparison.OrdinalIgnoreCase))
        {
            return VulkanStaticGeometryUpdateMode.InPlace;
        }

        if (string.Equals(value, "replace", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "0", StringComparison.Ordinal)
            || string.Equals(value, "false", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "off", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "no", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "disabled", StringComparison.OrdinalIgnoreCase))
        {
            return VulkanStaticGeometryUpdateMode.Replace;
        }

        return VulkanStaticGeometryUpdateMode.Auto;
    }

    private static bool ResolveStaticPrimitiveTrianglesEnabled(
        VulkanStaticPrimitiveTriangleMode mode,
        VulkanDevicePolicy policy)
    {
        return mode switch
        {
            VulkanStaticPrimitiveTriangleMode.Enabled => true,
            VulkanStaticPrimitiveTriangleMode.Disabled => false,
            _ => ShouldEnableStaticPrimitiveTrianglesByDefault(policy),
        };
    }

    private static bool ShouldEnableStaticPrimitiveTrianglesByDefault(VulkanDevicePolicy policy)
    {
        if (policy.DeviceType != PhysicalDeviceType.DiscreteGpu)
        {
            return false;
        }

        return policy.Vendor is VulkanGpuVendor.Nvidia or VulkanGpuVendor.Amd;
    }

    private static VulkanStaticGeometryUpdateMode ResolveStaticGeometryUpdateMode(
        VulkanStaticGeometryUpdateMode mode,
        VulkanDevicePolicy policy)
    {
        return mode switch
        {
            VulkanStaticGeometryUpdateMode.Replace => VulkanStaticGeometryUpdateMode.Replace,
            VulkanStaticGeometryUpdateMode.InPlace => VulkanStaticGeometryUpdateMode.InPlace,
            VulkanStaticGeometryUpdateMode.Rotating => VulkanStaticGeometryUpdateMode.Rotating,
            _ => ShouldEnableRotatingStaticGeometryUpdateByDefault(policy)
                ? VulkanStaticGeometryUpdateMode.Rotating
                : VulkanStaticGeometryUpdateMode.Replace,
        };
    }

    private static bool ShouldEnableRotatingStaticGeometryUpdateByDefault(VulkanDevicePolicy policy)
    {
        if (policy.DeviceType != PhysicalDeviceType.DiscreteGpu)
        {
            return false;
        }

        return policy.Vendor == VulkanGpuVendor.Nvidia;
    }

    private static string CreatePipelineCachePath(VulkanDevicePolicy policy)
    {
        var vendorName = policy.Vendor.ToString().ToLowerInvariant();
        var fileName = "vulkan_pipeline_cache_"
            + vendorName
            + "_"
            + policy.VendorId.ToString("x4", CultureInfo.InvariantCulture)
            + "_"
            + policy.DeviceId.ToString("x4", CultureInfo.InvariantCulture)
            + "_"
            + policy.PipelineCacheUuid
            + ".bin";

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Duxel",
            fileName);
    }

    private static string GetPhysicalDeviceName(PhysicalDeviceProperties properties)
    {
        byte* name = properties.DeviceName;
        return Marshal.PtrToStringUTF8((nint)name) ?? string.Empty;
    }

    private static string GetPipelineCacheUuidHex(PhysicalDeviceProperties properties)
    {
        var bytes = new byte[16];
        byte* uuid = properties.PipelineCacheUuid;
        Marshal.Copy((nint)uuid, bytes, 0, bytes.Length);

        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

