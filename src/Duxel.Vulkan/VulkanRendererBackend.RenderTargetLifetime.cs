namespace Duxel.Vulkan;

public sealed unsafe partial class VulkanRendererBackend
{
    private FrameResources? _lastSubmittedFrame;
    private int _pendingRenderTargetRetirements;
    private ulong _pendingDepthTargetBytes,_pendingRenderTargetBytes;
    private ulong _msaaColorAllocationBytes;
    private Extent2D _msaaColorExtent;
    private uint _maxImageDimension2D;
    private static uint RoundRenderTargetCapacity(uint value,uint maximum)=>(uint)Math.Min((((ulong)value+63)/64)*64,maximum);
    private readonly record struct RetiredRenderTargets(ImageView[] SwapchainViews,Image MsaaImage,
        ImageView MsaaView,DeviceMemory MsaaMemory,ulong MsaaBytes,
        Image[] DepthImages,ImageView[] DepthViews,DeviceMemory[] DepthMemory,ulong DepthBytes);

    private void RetireRenderTargets(ImageView[] swapchainViews,bool includeMsaa=true)
    {
        var retired=new RetiredRenderTargets(swapchainViews,includeMsaa?_msaaColorImage:default,includeMsaa?_msaaColorImageView:default,
            includeMsaa?_msaaColorMemory:default,includeMsaa?_msaaColorAllocationBytes:0,
            _depthLayerImages,_depthLayerViews,_depthLayerMemory,_depthLayerAllocatedBytes);
        if(includeMsaa)ClearMsaaTargetHandles();
        _depthLayerImages=[];_depthLayerViews=[];_depthLayerMemory=[];_depthLayerAllocatedBytes=0;
        QueueRetiredRenderTargets(retired);
    }

    private void ClearMsaaTargetHandles()
    {
        _msaaColorImage=default;_msaaColorImageView=default;_msaaColorMemory=default;
        _msaaColorAllocationBytes=0;_msaaColorExtent=default;
    }

    private void QueueRetiredRenderTargets(RetiredRenderTargets retired)
    {
        if(_lastSubmittedFrame is null){DestroyRetiredRenderTargets(retired);return;}
        // The graphics queue is ordered: completion of its latest submission
        // proves that all commands referring to this generation have completed.
        _lastSubmittedFrame.PendingRenderTargets.Add(retired);
        _pendingRenderTargetRetirements++;
        _pendingDepthTargetBytes+=retired.DepthBytes;
        _pendingRenderTargetBytes+=retired.DepthBytes+retired.MsaaBytes;
    }

    private bool UsesResizableMsaaCapacity()
    {
        var live=_platform.IsInteractingResize;
        SelectValidationMsaaCapacity(ref live);
        return live;
    }
    partial void SelectValidationMsaaCapacity(ref bool live);

    private bool MsaaTargetExactlyFits()=>_msaaColorExtent.Width==_swapchainExtent.Width&&_msaaColorExtent.Height==_swapchainExtent.Height;

    private void TrimMsaaTargetAfterResize()
    {
        if(_msaaColorImage.Handle==0||UsesResizableMsaaCapacity()||MsaaTargetExactlyFits())return;
        var retired=new RetiredRenderTargets([],_msaaColorImage,_msaaColorImageView,_msaaColorMemory,_msaaColorAllocationBytes,[],[],[],0);
        ClearMsaaTargetHandles();QueueRetiredRenderTargets(retired);
        CreateMsaaColorImage();
        ResetDamageImages(_swapchainImages.Length);
    }

    private void CollectCompletedRenderTargets()
    {
        if(_pendingRenderTargetRetirements==0&&(!_swapchainMaintenanceEnabled||_retiredPresentations.Count==0))return;
        foreach(var frame in _frames)
        {
            if(frame.PendingRenderTargets.Count==0&&frame.SubmittedSequence<=_completedFrameSequence)continue;
            var fence=frame.InFlight;
            var result=_vk.WaitForFences(_device,1,&fence,true,0);
            if(result is Result.Timeout or Result.NotReady)continue;
            Check(result);
            RecordCompletedGraphicsFrame(frame);
            FlushRetiredRenderTargets(frame);
        }
    }

    private bool AreGraphicsFramesComplete()
    {
        var ready=true;
        OverrideValidationGraphicsReady(ref ready);
        if(!ready)return false;
        foreach(var frame in _frames)
        {
            var fence=frame.InFlight;
            var result=_vk.WaitForFences(_device,1,&fence,true,0);
            if(result is Result.Timeout or Result.NotReady)return false;
            Check(result);
            RecordCompletedGraphicsFrame(frame);
        }
        return true;
    }
    partial void OverrideValidationGraphicsReady(ref bool ready);

    private void FlushRetiredRenderTargets(FrameResources frame)
    {
        foreach(var retired in frame.PendingRenderTargets)
        {
            DestroyRetiredRenderTargets(retired);
            _pendingRenderTargetRetirements--;
            _pendingDepthTargetBytes-=retired.DepthBytes;
            _pendingRenderTargetBytes-=retired.DepthBytes+retired.MsaaBytes;
        }
        frame.PendingRenderTargets.Clear();
    }

    private void DestroyRetiredRenderTargets(RetiredRenderTargets retired)
    {
        DestroyImageViews(retired.SwapchainViews);
        if(retired.MsaaView.Handle!=0)_vk.DestroyImageView(_device,retired.MsaaView,null);
        if(retired.MsaaImage.Handle!=0)_vk.DestroyImage(_device,retired.MsaaImage,null);
        if(retired.MsaaMemory.Handle!=0)_vk.FreeMemory(_device,retired.MsaaMemory,null);
        for(var i=0;i<retired.DepthImages.Length;i++)
        {
            if(retired.DepthViews[i].Handle!=0)_vk.DestroyImageView(_device,retired.DepthViews[i],null);
            if(retired.DepthImages[i].Handle!=0)_vk.DestroyImage(_device,retired.DepthImages[i],null);
            if(retired.DepthMemory[i].Handle!=0)_vk.FreeMemory(_device,retired.DepthMemory[i],null);
        }
    }
}
