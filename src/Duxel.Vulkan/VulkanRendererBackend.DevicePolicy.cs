using System.Globalization;
using System.Runtime.InteropServices;

namespace Duxel.Vulkan;

public sealed unsafe partial class VulkanRendererBackend
{
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

    private enum VulkanTriangleColorPipelineMode
    {
        Auto,
        Enabled,
        Disabled,
    }

    private enum VulkanSolidUnifiedPipelineMode
    {
        Auto,
        Enabled,
        Disabled,
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

    private readonly VulkanTriangleColorPipelineMode _triangleColorPipelineMode = ParseTriangleColorPipelineMode();
    private readonly VulkanSolidUnifiedPipelineMode _solidUnifiedPipelineMode = ParseSolidUnifiedPipelineMode();
    private readonly VulkanStaticPrimitiveTriangleMode _staticPrimitiveTriangleMode = ParseStaticPrimitiveTriangleMode();
    private readonly VulkanStaticGeometryUpdateMode _staticGeometryUpdateMode = ParseStaticGeometryUpdateMode();
    private readonly bool _solidUnifiedStaticEnabled = ParseBooleanEnvironmentFlag("DUXEL_VK_SOLID_UNIFIED_STATIC");

    private bool _triangleColorPipelineEnabled;
    private bool _solidUnifiedPipelineEnabled;
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

    private static VulkanTriangleColorPipelineMode ParseTriangleColorPipelineMode()
    {
        var raw = Environment.GetEnvironmentVariable("DUXEL_VK_TRIANGLE_COLOR_PIPELINE");
        if (string.IsNullOrWhiteSpace(raw))
        {
            return VulkanTriangleColorPipelineMode.Auto;
        }

        var value = raw.Trim();
        if (string.Equals(value, "auto", StringComparison.OrdinalIgnoreCase))
        {
            return VulkanTriangleColorPipelineMode.Auto;
        }

        if (string.Equals(value, "1", StringComparison.Ordinal)
            || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "on", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase))
        {
            return VulkanTriangleColorPipelineMode.Enabled;
        }

        if (string.Equals(value, "0", StringComparison.Ordinal)
            || string.Equals(value, "false", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "off", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "no", StringComparison.OrdinalIgnoreCase))
        {
            return VulkanTriangleColorPipelineMode.Disabled;
        }

        return VulkanTriangleColorPipelineMode.Auto;
    }

    private static VulkanSolidUnifiedPipelineMode ParseSolidUnifiedPipelineMode()
    {
        var raw = Environment.GetEnvironmentVariable("DUXEL_VK_SOLID_UNIFIED_PIPELINE");
        if (string.IsNullOrWhiteSpace(raw))
        {
            return VulkanSolidUnifiedPipelineMode.Auto;
        }

        var value = raw.Trim();
        if (string.Equals(value, "auto", StringComparison.OrdinalIgnoreCase))
        {
            return VulkanSolidUnifiedPipelineMode.Auto;
        }

        if (string.Equals(value, "1", StringComparison.Ordinal)
            || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "on", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase))
        {
            return VulkanSolidUnifiedPipelineMode.Enabled;
        }

        if (string.Equals(value, "0", StringComparison.Ordinal)
            || string.Equals(value, "false", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "off", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "no", StringComparison.OrdinalIgnoreCase))
        {
            return VulkanSolidUnifiedPipelineMode.Disabled;
        }

        return VulkanSolidUnifiedPipelineMode.Auto;
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

    private static bool ResolveTriangleColorPipelineEnabled(
        VulkanTriangleColorPipelineMode mode,
        VulkanDevicePolicy policy)
    {
        return mode switch
        {
            VulkanTriangleColorPipelineMode.Enabled => true,
            VulkanTriangleColorPipelineMode.Disabled => false,
            _ => ShouldEnableTriangleColorPipelineByDefault(policy),
        };
    }

    private static bool ShouldEnableTriangleColorPipelineByDefault(VulkanDevicePolicy policy)
    {
        if (policy.DeviceType != PhysicalDeviceType.DiscreteGpu)
        {
            return false;
        }

        return policy.Vendor is VulkanGpuVendor.Nvidia or VulkanGpuVendor.Amd;
    }

    private static bool ResolveSolidUnifiedPipelineEnabled(
        VulkanSolidUnifiedPipelineMode mode,
        VulkanDevicePolicy policy)
    {
        return mode switch
        {
            VulkanSolidUnifiedPipelineMode.Enabled => true,
            VulkanSolidUnifiedPipelineMode.Disabled => false,
            _ => ShouldEnableSolidUnifiedPipelineByDefault(policy),
        };
    }

    private static bool ShouldEnableSolidUnifiedPipelineByDefault(VulkanDevicePolicy policy)
    {
        // The unified shader lowers pipeline switches in some traces, but current NVIDIA
        // focused/broad gates are faster on the split color-primitive path.
        return false;
    }

    private static bool ResolveStaticPrimitiveTrianglesEnabled(
        VulkanStaticPrimitiveTriangleMode mode,
        VulkanDevicePolicy policy,
        bool triangleColorPipelineEnabled)
    {
        if (!triangleColorPipelineEnabled)
        {
            return false;
        }

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
