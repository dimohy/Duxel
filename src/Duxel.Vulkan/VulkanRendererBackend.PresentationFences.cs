namespace Duxel.Vulkan;

public sealed unsafe partial class VulkanRendererBackend
{
    private List<Fence> _activePresentFences=[];
    private readonly Stack<Fence> _presentFencePool=[];
    private ulong _submittedFrameSequence,_completedFrameSequence;

    private void RecordCompletedGraphicsFrame(FrameResources frame)
        =>_completedFrameSequence=Math.Max(_completedFrameSequence,frame.SubmittedSequence);

    private Fence RentPresentFence()
    {
        if(_presentFencePool.Count>0)
        {
            var reused=_presentFencePool.Pop();
            Check(_vk.ResetFences(_device,1,&reused));
            return reused;
        }
        var info=new FenceCreateInfo {SType=StructureType.FenceCreateInfo};
        var created=default(Fence);
        Check(_vk.CreateFence(_device,&info,null,&created));
        return created;
    }

    private bool IsFenceComplete(Fence fence)
    {
        var result=_vk.WaitForFences(_device,1,&fence,true,0);
        if(result is Result.Timeout or Result.NotReady)return false;
        Check(result);return true;
    }

    private void RecyclePresentFences(List<Fence> fences)
    {
        foreach(var fence in fences)_presentFencePool.Push(fence);
        fences.Clear();
    }

    private void CollectPresentationFenceResources()
    {
        if(!_swapchainMaintenanceEnabled)return;
        var completed=0;
        // Presentation fences to one swapchain/queue signal in present order.
        while(completed<_activePresentFences.Count&&IsFenceComplete(_activePresentFences[completed]))completed++;
        for(var i=0;i<completed;i++)_presentFencePool.Push(_activePresentFences[i]);
        if(completed>0)_activePresentFences.RemoveRange(0,completed);
        for(var i=_retiredPresentations.Count-1;i>=0;i--)
        {
            var retired=_retiredPresentations[i];
            if(retired.LastSubmission>_completedFrameSequence)continue;
            if(retired.PresentFences.Count>0&&!IsFenceComplete(retired.PresentFences[^1]))continue;
            DestroyRetiredPresentation(retired);
            _retiredPresentations.RemoveAt(i);
        }
    }

    private void WaitForPresentationFencesAtShutdown()
    {
        if(!_swapchainMaintenanceEnabled)return;
        void Wait(List<Fence> fences)
        {
            if(fences.Count==0)return;
            var last=fences[^1];
            if(_profilingEnabled)WriteVulkanProfileLine($"[duxel-vk-shutdown] presentation-wait fence={last.Handle} count={fences.Count}");
            Check(_vk.WaitForFences(_device,1,&last,true,ulong.MaxValue));
            if(_profilingEnabled)WriteVulkanProfileLine("[duxel-vk-shutdown] presentation-wait complete");
        }
        Wait(_activePresentFences);
        foreach(var retired in _retiredPresentations)Wait(retired.PresentFences);
    }

    private void DestroyPresentationFencePool()
    {
        foreach(var fence in _activePresentFences)_vk.DestroyFence(_device,fence,null);
        _activePresentFences.Clear();
        while(_presentFencePool.TryPop(out var fence))_vk.DestroyFence(_device,fence,null);
    }
}
