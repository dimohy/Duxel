using Duxel.Core;
using Duxel.Core.Dsl;

internal static class PanelTests
{
    internal static void Run(Action<bool,string> check)
    {
        using var context=new UiContext(new(1,1,UiTextureFormat.Rgba8Unorm,[255,255,255,255]),new(1),new(2));
        var idle=new UiInputState(new(-100,-100),false,false,false,false,false,false,0,0,[],[],default,default);
        UiDrawData Frame(Action<UiImmediateContext> render)
        {
            context.NewFrame(new(1f/60,new(640,480),new(1,1)),idle,new(0,0,640,480),UiTextSettings.Default);
            context.Render(new Screen(render));return context.GetDrawData();
        }
        UiVector2 start=default;
        using var panel=Dux.Panel("Test panel",Dux.View(ui=>{start=ui.GetCursorScreenPos();ui.Dummy(new(80,50));}),
            new(Position:new(24,24),Size:new(320,240),PositionSpace:UiPanelPositionSpace.WorkArea));
        using(var data=Frame(ui=>{ui.SetViewportTopInset(48);ui.EnableRootViewportContentLayout(contentPadding:8);panel.Render(ui);}))
            check(context.State.TryGetWindowRect("Test panel",out var rect)&&rect.Y==72,"generated Panel reserves the native app title work area");
        check(panel is UiPanel<IUiView>,"generated default Panel inherits the same public generic control as user panels");
        using(var data=Frame(ui=>{ui.SetPanelCollapsed("Test panel",true);panel.Render(ui);}))
            check(context.State.TryGetWindowRect("Test panel",out var rect)&&rect.Height<100,"Panel collapse keeps a title-only surface");
        using(var data=Frame(ui=>{ui.SetPanelCollapsed("Test panel",false);panel.Render(ui);}))
            check(context.State.TryGetWindowRect("Test panel",out var rect)&&rect.Height==240,"Panel expand restores its stored content height");
        using(var data=Frame(ui=>{ui.SetViewportTopInset(48);ui.EnableRootViewportContentLayout(contentPadding:8);ui.Dummy(new(80,50));start=ui.GetCursorStartPos();}))
            check(start.Y==56&&!context.State.TryGetWindowRect("Root",out _),"direct root content starts below app chrome without creating a panel");
        foreach(var name in new[]{"Panel","Window","BeginWindow"})
        {
            var nodes=UiDslParser.Parse(name+" \"DSL panel\"\n  Text \"Body\"");
            check(nodes.Roots[0].Name=="BeginPanel","DSL "+name+" resolves to the shared Panel container");
        }
        check(!Fluent.ComponentNames.Contains("Panel")&&!Fluent.ComponentNames.Contains("Window"),"optional Panel and legacy Window do not enter the official Fluent component catalog");
        var reads=0;var value=true;
        using var bound=Dux.Panel("Bound panel",Dux.View(ui=>ui.Dummy(new(40,30))),new(Open:new UiBinding<bool>(()=>{reads++;return value;},next=>value=next)));
        using(var data=Frame(bound.Render)){}
        check(reads==1&&value,"Panel reads its open binding once while preserving close-state synchronization");
    }
    private sealed class Screen(Action<UiImmediateContext> render):UiScreen
    {public override void Render(UiImmediateContext ui)=>render(ui);}
}
