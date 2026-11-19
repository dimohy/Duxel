namespace Duxel.Vulkan;

public sealed unsafe partial class VulkanRendererBackend
{
    // ONE_TO_ONE plus MIN gravity clips an oversized image at its top-left.
    // It never stretches or resamples UI pixels. Only live gestures reserve
    // capacity; the first settled frame returns to the exact native extent.
    private bool _oneToOnePresentation;
    private Extent2D _frameRenderExtent;
    private const uint PresentOneToOne=1,PresentGravityMin=1;

    private SurfacePresentScalingCapabilitiesEXT QueryPresentationScaling(PresentModeKHR presentMode)
    {
        var scaling=new SurfacePresentScalingCapabilitiesEXT {SType=StructureType.SurfacePresentScalingCapabilitiesEXT};
        if(!_maintenanceInstanceEnabled||!_swapchainMaintenanceEnabled)return scaling;
        var mode=new SurfacePresentModeEXT {SType=StructureType.SurfacePresentModeEXT,PresentMode=presentMode};
        var info=new PhysicalDeviceSurfaceInfo2KHR {SType=StructureType.PhysicalDeviceSurfaceInfo2Khr,PNext=&mode,Surface=_surface};
        var caps=new SurfaceCapabilities2KHR {SType=StructureType.SurfaceCapabilities2Khr,PNext=&scaling};
        Check(_khrSurface.GetPhysicalDeviceSurfaceCapabilities2(_physicalDevice,&info,&caps));
        return scaling;
    }

    private bool UsesPresentationCapacity()
    {
        var live=_platform.IsInteractingResize;
        SelectValidationPresentationCapacity(ref live);
        return live;
    }
    partial void SelectValidationPresentationCapacity(ref bool live);

    private Extent2D ChoosePresentationCapacity(Extent2D requested,PresentModeKHR mode,out bool oneToOne)
    {
        var support=QueryPresentationScaling(mode);
        oneToOne=(support.SupportedPresentScaling&PresentOneToOne)!=0
            &&(support.SupportedPresentGravityX&PresentGravityMin)!=0
            &&(support.SupportedPresentGravityY&PresentGravityMin)!=0;
        SelectValidationOneToOnePresentation(ref oneToOne);
        if(!oneToOne||!UsesPresentationCapacity())return requested;
        var maxWidth=Math.Min(support.MaxScaledImageExtent.Width,_maxImageDimension2D);
        var maxHeight=Math.Min(support.MaxScaledImageExtent.Height,_maxImageDimension2D);
        return new(
            Math.Max(support.MinScaledImageExtent.Width,RoundRenderTargetCapacity(requested.Width,maxWidth)),
            Math.Max(support.MinScaledImageExtent.Height,RoundRenderTargetCapacity(requested.Height,maxHeight)));
    }
    partial void SelectValidationOneToOnePresentation(ref bool enabled);
}
