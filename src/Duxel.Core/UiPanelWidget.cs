namespace Duxel.Core;

public enum UiPanelPlacementCondition {Initial,Always}
public enum UiPanelPositionSpace {Screen,WorkArea}
public readonly record struct UiPanelOptions(UiVector2 Position=default,UiVector2 Size=default,
    UiBinding<bool>? Open=null,bool Focus=false,bool TopMost=false,
    UiPanelPlacementCondition Placement=UiPanelPlacementCondition.Initial,
    UiPanelPositionSpace PositionSpace=UiPanelPositionSpace.Screen,bool AutoHeight=false);

/// <summary>An optional internal collapsible panel; the native application owns the outer window.</summary>
public class UiPanel<TContent>:UiControlWidget where TContent:IUiView
{
    public UiPanel(string title,TContent content,UiPanelOptions options=default,UiWidgetIdentity identity=UiWidgetIdentity.Instance):base(identity)
    {
        ArgumentNullException.ThrowIfNull(title);ArgumentNullException.ThrowIfNull(content);
        Title=title;Content=content;Options=options;
    }
    public string Title {get;set;}
    public TContent Content {get;set;}
    public UiPanelOptions Options {get;set;}
    protected override void RenderControl(UiImmediateContext ui)=>DuxelPanelFactory.Render(ui,Title,Content,Options);
}

[UiWidgetStyle(typeof(UiPanel<>),typeof(IUiView))]
[UiComponent("Panel",UiComponentExposure.Infrastructure)]
public partial class UiDefaultPanel;

public sealed class DuxelPanelFactory
{
    internal DuxelPanelFactory(){}
    public UiDefaultPanel Panel(string title,IUiView content,UiPanelOptions options=default)=>new(title,content,options);
    internal static void Render<TContent>(UiImmediateContext ui,string title,TContent content,UiPanelOptions options) where TContent:IUiView
    {
        var open=options.Open?.Value;
        if(open is false){ui.SetPanelOpen(title,false);return;}
        var initial=options.Placement==UiPanelPlacementCondition.Always||!ui.HasWindowPlacement(title);
        if(initial&&options.Position!=default)
        {
            var origin=options.PositionSpace==UiPanelPositionSpace.WorkArea?ui.GetMainViewport().WorkPos:default;
            ui.SetNextPanelPos(new(options.Position.X+origin.X,options.Position.Y+origin.Y));
        }
        if(initial&&options.Size!=default)ui.SetNextPanelSize(options.Size);
        if(open is bool visible)ui.SetNextPanelOpen(visible);
        if(options.Focus)ui.SetNextPanelFocus();
        if(options.TopMost)ui.SetNextPanelTopMost();
        ui.SetNextPanelAutoHeight(options.AutoHeight);
        ui.BeginPanel(title);
        try{content.Render(ui);}finally{ui.EndPanel();}
        if(options.Open is not null)options.Open.Value=ui.GetPanelOpen(title);
    }
}
