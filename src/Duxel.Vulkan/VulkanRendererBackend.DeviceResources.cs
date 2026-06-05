using System.Collections.Generic;
using Duxel.Core;

namespace Duxel.Vulkan;

public sealed unsafe partial class VulkanRendererBackend
{
    private static readonly string[] BaseDeviceExtensions =
    [
        "VK_KHR_swapchain",
    ];

    private readonly IVulkanSurfaceSource _surfaceSource;
    private KhrSurface _khrSurface = null!;
    private KhrSwapchain _khrSwapchain = null!;
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
            var enabledFeatures = new PhysicalDeviceFeatures
            {
                DualSrcBlend = 1,
            };

            var deviceCreateInfo = new DeviceCreateInfo
            {
                SType = StructureType.DeviceCreateInfo,
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

    private void LoadDeviceExtensions()
    {
        if (!KhrSwapchain.TryCreate(_vk, _instance, _device, out _khrSwapchain))
        {
            throw new InvalidOperationException("Failed to load VK_KHR_swapchain extension.");
        }
    }
}
