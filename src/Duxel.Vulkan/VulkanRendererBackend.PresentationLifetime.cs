namespace Duxel.Vulkan;

public sealed unsafe partial class VulkanRendererBackend
{
    private readonly List<RetiredPresentation> _retiredPresentations=[];
    private int _retirementAnchorImage=-1;
    private FrameResources? _retirementProofFrame;
    private int _retirementProofCount;
    public bool HasPendingPresentationWork=>_swapchainRecreationPending||_retiredPresentations.Count!=0||_pendingRenderTargetRetirements!=0;
    private readonly record struct RetiredPresentation(SwapchainKHR Swapchain,FrameSemaphores[] Semaphores,
        List<Fence> PresentFences,ulong LastSubmission);

    private void RetirePresentation(SwapchainKHR swapchain,FrameSemaphores[] semaphores)
    {
        _retiredPresentations.Add(new(swapchain,semaphores,_activePresentFences,_submittedFrameSequence));
        _activePresentFences=[];
        _retirementAnchorImage=-1;
    }

    private void ObserveAcceptedPresentation(uint image)
    {
        if(_swapchainMaintenanceEnabled)return;
        if(_retiredPresentations.Count>0&&_retirementAnchorImage<0&&_retirementProofFrame is null)
            _retirementAnchorImage=(int)image;
    }

    private void ObservePresentationAcquireWait(uint image,FrameResources frame)
    {
        if(_swapchainMaintenanceEnabled)return;
        if(_retirementAnchorImage!=(int)image||_retirementProofFrame is not null)return;
        // This submit waits for acquisition of the first presented new-generation
        // image. Its completion proves that presentation and all older ones retired.
        _retirementProofFrame=frame;
        _retirementProofCount=_retiredPresentations.Count;
        _retirementAnchorImage=-1;
    }

    private void CollectRetiredPresentations(FrameResources? completedFrame)
    {
        if(_swapchainMaintenanceEnabled){CollectPresentationFenceResources();return;}
        if(completedFrame is null||!ReferenceEquals(completedFrame,_retirementProofFrame))return;
        for(var i=0;i<_retirementProofCount;i++)DestroyRetiredPresentation(_retiredPresentations[i]);
        _retiredPresentations.RemoveRange(0,_retirementProofCount);
        _retirementProofFrame=null;
        _retirementProofCount=0;
    }

    private void DestroyRetiredPresentation(RetiredPresentation retired)
    {
        RecyclePresentFences(retired.PresentFences);
        foreach(var pair in retired.Semaphores)
        {
            if(pair.ImageAvailable.Handle!=0)_vk.DestroySemaphore(_device,pair.ImageAvailable,null);
            if(pair.RenderFinished.Handle!=0)_vk.DestroySemaphore(_device,pair.RenderFinished,null);
        }
        if(retired.Swapchain.Handle!=0)_khrSwapchain.DestroySwapchain(_device,retired.Swapchain,null);
    }

    private void DestroyRetiredPresentationsAtShutdown()
    {
        foreach(var retired in _retiredPresentations)DestroyRetiredPresentation(retired);
        _retiredPresentations.Clear();
        _retirementAnchorImage=-1;_retirementProofFrame=null;_retirementProofCount=0;
    }
}
