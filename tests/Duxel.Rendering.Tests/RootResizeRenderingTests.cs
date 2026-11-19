using System.Runtime.InteropServices;
using Duxel.Core;
using Duxel.Platform.Windows;
using Duxel.Vulkan;

internal static partial class RootResizeRenderingTests
{
    internal static void RunPresentationCapacity(VulkanRendererBackend renderer,WindowsPlatformBackend platform,
        Func<string,Action<UiImmediateContext>,byte[]> capture,Action<int,int> updateSize,Action<bool,string> check)
    {
        check(Marshal.SizeOf<PhysicalDeviceSurfaceInfo2KHR>()==24&&Marshal.SizeOf<SurfaceCapabilities2KHR>()==72
            &&Marshal.SizeOf<SurfacePresentScalingCapabilitiesEXT>()==48&&Marshal.SizeOf<SwapchainPresentScalingCreateInfoEXT>()==32,
            "presentation capacity bindings match the64-bit Vulkan ABI");
        using var title=Fluent.Title3("Native pixel capacity");
        using var slider=Fluent.Slider("Capacity slider",new UiState<float>(37).Binding);
        void Draw(UiImmediateContext ui){ui.EnableRootViewportContentLayout(contentPadding:8);title.Render(ui);slider.Render(ui);}
        void Size(int w,int h){if(!SetWindowPos(platform.WindowHandle,0,0,0,w,h,0x0002|0x0004|0x0010))throw new InvalidOperationException("Capacity window resize failed.");platform.PollEvents();var s=platform.FramebufferSize;updateSize(s.Width,s.Height);}
        var sizes=new[]{(960,700),(740,560),(560,420),(900,660),(480,340)};
        var pixels=new List<byte[]>();
        VulkanRendererBackend.ValidationResizablePresentationCapacity=true;
        try
        {
            Size(sizes[0].Item1,sizes[0].Item2);
            _=capture("capacity-warmup",Draw);
            check(renderer.ValidationOneToOnePresentation,"surface supports native1:1 pixels and top-left clipping");
            var handle=renderer.ValidationSwapchainHandle;
            for(var i=0;i<sizes.Length;i++)
            {
                Size(sizes[i].Item1,sizes[i].Item2);
                var partial=capture("capacity-native-"+i,Draw);pixels.Add(partial);
                check(renderer.ValidationSwapchainHandle==handle,"size changes inside capacity preserve the swapchain "+i);
                check(partial.Length==platform.FramebufferSize.Width*platform.FramebufferSize.Height*4,"readback uses actual native dimensions "+i);
                renderer.ValidationFullRepaint=true;
                try{check(partial.AsSpan().SequenceEqual(capture("capacity-full-"+i,Draw)),"capacity partial pixels equal full repaint "+i);}
                finally{renderer.ValidationFullRepaint=false;}
            }
            VulkanRendererBackend.ValidationExactPresentation=true;
            renderer.ValidationReplacePresentation();
            for(var i=0;i<sizes.Length;i++)
            {
                Size(sizes[i].Item1,sizes[i].Item2);
                check(pixels[i].AsSpan().SequenceEqual(capture("capacity-exact-reference-"+i,Draw)),"native1:1 capacity pixels equal exact unscaled swapchain "+i);
            }
            VulkanRendererBackend.ValidationExactPresentation=false;
            Size(sizes[0].Item1,sizes[0].Item2);renderer.ValidationReplacePresentation();
            _=capture("capacity-before-settle-wide",Draw);
            Size(sizes[^1].Item1,sizes[^1].Item2);_=capture("capacity-before-settle-small",Draw);
            check(renderer.ValidationPresentationExtent.Width>(uint)platform.FramebufferSize.Width,"live gesture retains excess capacity before settling");
        }
        finally{VulkanRendererBackend.ValidationResizablePresentationCapacity=false;VulkanRendererBackend.ValidationExactPresentation=false;}
        VulkanRendererBackend.ValidationResizableMsaaCapacity=false;
        _=capture("capacity-settled",Draw);
        var native=platform.FramebufferSize;var extent=renderer.ValidationPresentationExtent;
        check(extent.Width==native.Width&&extent.Height==native.Height,"settled presentation releases excess capacity");
        if(renderer.ValidationRequestedMsaaSamples>1)check(renderer.ValidationMsaaTarget.Width==native.Width&&renderer.ValidationMsaaTarget.Height==native.Height,"settled MSAA releases excess capacity");
    }
    internal static void Run(VulkanRendererBackend renderer,WindowsPlatformBackend platform,
        Func<Action<UiImmediateContext>,UiDrawData> build,Func<string,Action<UiImmediateContext>,byte[]> capture,
        Action<int,int> updateSize,Action<bool,string> check,string? output=null)
    {
        var state=new UiState<float>(25);
        using var slider=Fluent.Slider("Root resize slider",state.Binding);
        using var title=Fluent.Title3("Direct root content");
        var widths=new List<float>();
        void Draw(UiImmediateContext ui)
        {
            ui.SetViewportTopInset(48);ui.EnableRootViewportContentLayout(contentPadding:8);
            widths.Add(ui.GetContentRegionAvail().X);title.Render(ui);slider.Render(ui);
        }
        foreach(var requested in new[]{new UiVector2(520,380),new(760,510),new(420,320)})
        {
            var ageBeforeResize=renderer.ValidationFrameAge;
            if(!SetWindowPos(platform.WindowHandle,0,0,0,(int)requested.X,(int)requested.Y,0x0002|0x0004|0x0010))
                throw new InvalidOperationException("Owned GPU window resize failed.");
            platform.PollEvents();var size=platform.FramebufferSize;updateSize(size.Width,size.Height);
            var oldSwapchain=renderer.ValidationSwapchainHandle;
            renderer.ValidationGraphicsBusy=true;
            try
            {
                using var busyFrame=build(Draw);
                check(!renderer.TryRenderDrawData(busyFrame)&&renderer.ValidationSwapchainHandle==oldSwapchain,
                    "busy graphics defer WSI recreation without entering creation at "+requested);
                check(renderer.HasPendingPresentationWork,"deferred resize remains scheduled across idle at "+requested);
            }
            finally{renderer.ValidationGraphicsBusy=false;}
            // Recreate the actual target before allocating the sized readback.
            using(var frame=build(Draw))check(renderer.TryRenderDrawData(frame),"root frame presents after native resize "+requested);
            check(renderer.ValidationFrameAge>ageBeforeResize,"ordinary swapchain recreation preserves frame age for deferred frees and caches "+requested);
            for(var frame=0;frame<4;frame++)
            {
                var partial=capture($"root-resize-{requested.X}-{frame}",Draw);
                renderer.ValidationFullRepaint=true;
                byte[] full;
                try{full=capture($"root-resize-{requested.X}-{frame}-full",Draw);}
                finally{renderer.ValidationFullRepaint=false;}
                check(partial.AsSpan().SequenceEqual(full),"resized root retained pixels equal full repaint "+requested+" frame "+frame);
            }
            check(MathF.Abs(widths[^1]-(size.Width-16))<.01f,"root content width uses the committed framebuffer "+requested);
            if(VulkanRendererBackend.ValidationResizableMsaaCapacity)
            {
                var pooled=capture("root-msaa-capacity-"+requested.X,Draw);
                VulkanRendererBackend.ValidationResizableMsaaCapacity=false;
                try
                {
                    var exact=capture("root-msaa-exact-"+requested.X,Draw);
                    check(pooled.AsSpan().SequenceEqual(exact),"retained MSAA capacity preserves every resolved pixel at "+requested);
                }
                finally{VulkanRendererBackend.ValidationResizableMsaaCapacity=true;}
            }
        }
        check(widths.Max()>widths.Min()+200,"direct root layout responds to both growth and shrink");
        for(var attempt=0;attempt<3;attempt++)
        {
            // Stress cancellation before the present reaches WSI. Actual Vulkan
            // out-of-date returns still enqueue their semaphore waits.
            renderer.ValidationRejectNextPresentation=true;
            using(var frame=build(Draw))check(!renderer.TryRenderDrawData(frame),"out-of-date presentation is reported without a fabricated success "+attempt);
            var recovered=capture("root-rejected-present-"+attempt,Draw);
            renderer.ValidationFullRepaint=true;
            byte[] full;
            try{full=capture("root-rejected-present-full-"+attempt,Draw);}
            finally{renderer.ValidationFullRepaint=false;}
            check(recovered.AsSpan().SequenceEqual(full),"fresh swapchain synchronization recovers an unconsumed presentation semaphore "+attempt);
        }
        if(!GetWindowRect(platform.WindowHandle,out var outer))throw new InvalidOperationException("Owned window rect unavailable.");
        var before=platform.FramebufferSize;
        var nativeWidth=255+outer.Right-outer.Left-before.Width;
        if(!SetWindowPos(platform.WindowHandle,0,0,0,nativeWidth,outer.Bottom-outer.Top,0x0002|0x0004|0x0010))throw new InvalidOperationException("Owned fractional-scale resize failed.");
        platform.PollEvents();var scaledSize=platform.FramebufferSize;updateSize(scaledSize.Width,scaledSize.Height);
        using(var original=build(static _=>{}))
        {
            // 255/1.75*1.75 rounds slightly below255 in float. Truncation would
            // permanently reject a valid committed-size snapshot as stale.
            var scaled=original with {DisplaySize=new(scaledSize.Width/1.75f,scaledSize.Height/1.75f),FramebufferScale=new(1.75f,1.75f)};
            check(renderer.TryRenderDrawData(scaled),"fractional DPI extent round-off cannot strand a valid frame after resize");
        }
        var physical=capture("root-dpi-clip-reference",ui=>ui.GetForegroundDrawList().AddRectFilled(
            new(0,0,scaledSize.Width,scaledSize.Height),new UiColor(30,110,220),ui.WhiteTextureId,new(28,28,154,98)));
        using(var logical=build(ui=>ui.GetForegroundDrawList().AddRectFilled(
            new(0,0,scaledSize.Width/1.75f,scaledSize.Height/1.75f),new UiColor(30,110,220),ui.WhiteTextureId,new(16,16,88,56))))
        {
            var scaled=logical with{DisplaySize=new(scaledSize.Width/1.75f,scaledSize.Height/1.75f),FramebufferScale=new(1.75f,1.75f)};
            var actual=renderer.RenderAndReadback(scaled);
            if(output is not null)
            {File.WriteAllBytes(Path.Combine(output,"dpi-clip-reference.rgba"),physical);File.WriteAllBytes(Path.Combine(output,"dpi-clip-actual.rgba"),actual);}
            check(actual.AsSpan().SequenceEqual(physical),"fractional DPI clipping matches the same physical-pixel reference without a leaked edge column");
        }
        if(VulkanRendererBackend.ValidationResizableMsaaCapacity)
        {
            VulkanRendererBackend.ValidationResizableMsaaCapacity=false;
            capture("root-msaa-trim",Draw);
            var target=renderer.ValidationMsaaTarget;
            check(target.Width==scaledSize.Width&&target.Height==scaledSize.Height,"MSAA capacity trims to committed dimensions after interactive resizing ends");
        }
        for(var frame=0;frame<8;frame++)capture("root-retirement-drain-"+frame,Draw);
        check(!renderer.HasPendingPresentationWork,"old swapchains and presentation semaphores retire after new-image acquire completion");
        var acquireIndex=renderer.ValidationAcquireSemaphoreIndex;
        renderer.ValidationAcquireNotReadyAttempts=16;
        using(var frame=build(Draw))
        {
            for(var attempt=0;attempt<16;attempt++)
            {
                if(renderer.TryRenderDrawData(frame))throw new InvalidOperationException("A not-ready acquire cannot present.");
                if(renderer.ValidationAcquireSemaphoreIndex!=acquireIndex)
                    throw new InvalidOperationException("A failed acquire advanced the semaphore ring before its signal could be consumed.");
            }
            check(renderer.ValidationAcquireSemaphoreIndex==acquireIndex,"nonblocking failed acquisition preserves its fence-owned acquire semaphore");
            check(renderer.TryRenderDrawData(frame),"acquisition resumes with the same unsignaled semaphore after repeated not-ready results");
        }
    }
    [LibraryImport("user32.dll",SetLastError=true)]
    [return:MarshalAs(UnmanagedType.Bool)]private static partial bool SetWindowPos(nint window,nint after,int x,int y,int width,int height,uint flags);
    [StructLayout(LayoutKind.Sequential)]private struct Rect {internal int Left,Top,Right,Bottom;}
    [LibraryImport("user32.dll",SetLastError=true)]
    [return:MarshalAs(UnmanagedType.Bool)]private static partial bool GetWindowRect(nint window,out Rect rect);
}
