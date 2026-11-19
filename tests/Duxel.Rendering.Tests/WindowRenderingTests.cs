using Duxel.Core;
using Duxel.Vulkan;

internal static class WindowRenderingTests
{
    internal static ulong MoveUploadBytes;
    internal static int MoveDrawCalls;
    internal static void Run(VulkanRendererBackend renderer,Func<string,Action<UiImmediateContext>,byte[]> capture,
        Action<UiInputState> setInput,Action<bool,string> check,int width,int height)
    {
        var idle=new UiInputState(new(-100,-100),false,false,false,false,false,false,0,0,[],[],default,default);
        var value=new UiState<float>(25);
        using var slider=Fluent.Slider("Window slider",value.Binding);slider.Size=new(220,32);
        using var text=Fluent.Body1("Visible window content");
        var actual=default(UiRect);
        var content=Dux.View(ui=>
        {
            var pos=ui.GetWindowPos();var size=ui.GetWindowSize();actual=new(pos.X,pos.Y,size.X,size.Y);
            text.Render(ui);slider.Render(ui);
        });
        var window=Dux.Window("Window title",content,new(Position:new(24,24),Size:new(260,700),
            PositionSpace:UiWindowPositionSpace.WorkArea,AutoHeight:true));
        void Draw(UiImmediateContext ui){ui.SetViewportTopInset(48);ui.EnableRootViewportContentLayout();window.Render(ui);}
        try
        {
            setInput(idle);
            var empty=capture("window-empty",static _=>{});
            capture("window-auto-height-warm",Draw);
            var stable=capture("window-auto-height",Draw);
            check(actual.Y==72&&actual.Height<200,"actual GPU window starts below48px chrome and fits its content");
            var outside=0;
            for(var y=(int)MathF.Ceiling(actual.Y+actual.Height)+3;y<height;y++)
                for(var x=0;x<width;x++)
                {var offset=(y*width+x)*4;if(!stable.AsSpan(offset,4).SequenceEqual(empty.AsSpan(offset,4)))outside++;}
            check(outside==0,"automatic window height does not leave a fixed-height GPU background below its content");
            setInput(idle with {MousePosition=new(150,84),LeftMousePressed=true,LeftMouseDown=true});
            capture("window-drag-press",Draw);
            setInput(idle with {MousePosition=new(166,84),LeftMouseDown=true});
            var moved=capture("window-drag-moved",Draw);
            MoveUploadBytes=renderer.LastFrameMetrics.DynamicUploadBytes;MoveDrawCalls=renderer.LastFrameMetrics.DrawCalls;
            check(actual.X==40,"actual GPU window geometry follows the current pointer displacement");
            setInput(idle with {MousePosition=new(166,84),LeftMouseReleased=true});
            var released=capture("window-drag-released",Draw);
            check(moved.SequenceEqual(released),"releasing an internal window preserves its entire GPU image without snapping back");
            Console.WriteLine($"Window move draws={MoveDrawCalls} dynamicUploadBytes={MoveUploadBytes}; not a timing benchmark.");
        }
        finally{setInput(idle);}
    }
}
