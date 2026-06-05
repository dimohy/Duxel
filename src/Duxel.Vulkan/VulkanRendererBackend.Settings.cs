using Duxel.Core;

namespace Duxel.Vulkan;

public sealed unsafe partial class VulkanRendererBackend
{
    private VulkanRendererOptions _options;
    private ClearColorValue _clearColorValue = new(0f, 0f, 0f, 1f);

    public void SetClearColor(UiColor color)
    {
        _clearColorValue = ToClearColorValue(color);
    }

    public void CreateDeviceObjects()
    {
        if (_swapchain.Handle is not 0)
        {
            return;
        }

        CreateSwapchainResources();
    }

    public void InvalidateDeviceObjects() => DestroySwapchainDependentResources();

    public void SetMinImageCount(int count)
    {
        var newCount = Math.Max(2, count);
        if (_minImageCount == newCount)
        {
            return;
        }

        _minImageCount = newCount;
        RecreateSwapchainAfterSettingsChange();
    }

    public void SetVSync(bool enable)
    {
        if (_options.EnableVSync == enable)
        {
            return;
        }

        _options = _options with { EnableVSync = enable };
        RecreateSwapchainAfterSettingsChange();
    }

    public void SetMsaaSamples(int samples)
    {
        var normalized = samples switch
        {
            <= 1 => 1,
            <= 2 => 2,
            <= 4 => 4,
            _ => 8,
        };

        if (_options.MsaaSamples == normalized)
        {
            return;
        }

        var previousMsaaSampleCount = _msaaSampleCount;
        _options = _options with { MsaaSamples = normalized };
        ConfigureMsaaSampleCount();

        if (_msaaSampleCount == previousMsaaSampleCount)
        {
            return;
        }

        RecreateSwapchainAfterSettingsChange();
    }

    private void RecreateSwapchainAfterSettingsChange()
    {
        if (_swapchain.Handle is 0)
        {
            return;
        }

        _frameIndex = 0;
        Array.Clear(_imagesInFlight, 0, _imagesInFlight.Length);
        _ = TryRecreateSwapchain();
    }

    private static bool ParseLegacyClipClampPathEnabled()
    {
        var raw = Environment.GetEnvironmentVariable("DUXEL_VK_LEGACY_CLIP_CLAMP");
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        return string.Equals(raw, "1", StringComparison.Ordinal)
            || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(raw, "on", StringComparison.OrdinalIgnoreCase);
    }

    private static ClearColorValue ToClearColorValue(UiColor color)
    {
        var rgba = color.Rgba;
        var r = ((rgba >> 16) & 0xFF) / 255f;
        var g = ((rgba >> 8) & 0xFF) / 255f;
        var b = (rgba & 0xFF) / 255f;
        var a = ((rgba >> 24) & 0xFF) / 255f;
        return new ClearColorValue(r, g, b, a);
    }
}
