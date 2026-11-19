using Duxel.Core;

internal static class WindowInteractionTests
{
    internal static void Run(Action<bool,string> check)
    {
        using var context=new UiContext(new(1,1,UiTextureFormat.Rgba8Unorm,[255,255,255,255]),new(1),new(2));
        var idle=new UiInputState(new(-100,-100),false,false,false,false,false,false,0,0,[],[],default,default);
        var view=Dux.Window("Drag window",Dux.View(ui=>ui.Dummy(new(100,60))),new(Position:new(24,80),Size:new(400,250)));
        UiRect Frame(UiInputState? input=null,IUiView? overrideView=null,string title="Drag window")
        {
            context.NewFrame(new(1f/60,new(1040,800),new(1,1)),input??idle,new(0,0,1040,800),UiTextSettings.Default);
            context.Render(new Screen(ui=>{ui.SetViewportTopInset(48);ui.EnableRootViewportContentLayout();(overrideView??view).Render(ui);}));
            context.GetDrawData().ReleasePooled();
            if(!context.State.TryGetWindowRect(title,out var rect))throw new InvalidOperationException("Missing window");
            return rect;
        }
        var start=Frame();Frame(idle with {MousePosition=new(150,96),LeftMousePressed=true,LeftMouseDown=true});
        var moved=Frame(idle with {MousePosition=new(250,146),LeftMouseDown=true});
        check(moved.X==start.X+100&&moved.Y==start.Y+50,"internal window follows the current pointer without position interpolation");
        var released=Frame(idle with {MousePosition=new(250,146),LeftMouseReleased=true});
        check(released.X==moved.X&&released.Y==moved.Y,"declarative initial placement does not reset the window on drag release");
        Frame(idle with {MousePosition=new(250,146),LeftMousePressed=true,LeftMouseDown=true});
        var constrained=Frame(idle with {MousePosition=new(250,10),LeftMouseDown=true});
        check(constrained.Y==48,"internal window dragging keeps the title below the app chrome work-area inset");
        Frame(idle with {LeftMouseReleased=true});
        var automatic=Dux.Window("Automatic window",Dux.View(ui=>ui.Dummy(new(100,60))),
            new(Position:new(24,24),Size:new(400,740),PositionSpace:UiWindowPositionSpace.WorkArea,AutoHeight:true));
        Frame(overrideView:automatic,title:"Automatic window");
        var fitted=Frame(overrideView:automatic,title:"Automatic window");
        check(fitted.Y==72,"work-area-relative initial placement reserves the native app title and requested margin");
        check(fitted.Height==140,"automatic window height removes unused fixed-height space while respecting the minimum");
    }
    private sealed class Screen(Action<UiImmediateContext> render):UiScreen
    {public override void Render(UiImmediateContext ui)=>render(ui);}
}
