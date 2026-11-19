using System.Diagnostics;

namespace Duxel.Vulkan;

public sealed unsafe partial class VulkanRendererBackend
{
    private long _validationTextureUpdateStarted;

    internal double ValidationTextureUpdateMicroseconds
    {
        get; private set;
    }
    internal string ValidationVectorDevicePolicy => CreateProfileDevicePolicyText();

    internal void ValidationDrainVectorWorkCache()
    {
        WaitForAllInFlightFrameFences();
        RecycleAllVectorWorkAfterFrameFences();
        DestroyVectorWorkCache();
    }

    partial void ObserveValidationTextureUpdates(bool begin)
    {
        if (begin)
            _validationTextureUpdateStarted = Stopwatch.GetTimestamp();
        else
            ValidationTextureUpdateMicroseconds = Stopwatch.GetElapsedTime(_validationTextureUpdateStarted).TotalMicroseconds;
    }
}
