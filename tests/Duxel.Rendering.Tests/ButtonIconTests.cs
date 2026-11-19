using Duxel.Core;

internal static class ButtonIconTests
{
    private static UiWidget Create(int index,string text,Action? activated=null)=>index switch
    {
        0=>new IconInputBeforeSmall(text,activated),
        1=>new IconInputBeforeMedium(text,activated),
        2=>new IconInputBeforeLarge(text,activated),
        3=>new IconInputAfterSmall(text,activated),
        4=>new IconInputAfterMedium(text,activated),
        5=>new IconInputAfterLarge(text,activated),
        _=>throw new ArgumentOutOfRangeException(nameof(index))
    };
    internal static void Run(Action<bool,string> check)
    {
        using var context=new UiContext(new(1,1,UiTextureFormat.Rgba8Unorm,[255,255,255,255]),new(1),new(2));
        ControlFontFixture.Configure(context);
        var idle=new UiInputState(new(-100,-100),false,false,false,false,false,false,0,0,[],[],default,default);
        var input=idle;var disabled=false;var actual=default(UiRect);
        UiDrawData Frame(IUiView widget)
        {
            context.NewFrame(new(1f/60,new(320,240),new(1,1)),input,new(0,0,320,240),UiTextSettings.Default);
            context.Render(new Screen(ui=>
            {
                ui.SetCursorScreenPos(new(20,20));ui.BeginDisabled(disabled);
                try{widget.Render(ui);var p=ui.GetItemRectMin();var s=ui.GetItemRectSize();actual=new(p.X,p.Y,s.X,s.Y);}
                finally{ui.EndDisabled();}
            }));
            return context.GetDrawData();
        }
        for(var index=0;index<6;index++)
        {
            var calls=0;using var widget=Create(index,"UI",()=>calls++);
            using(var data=Frame(widget)){}
            var size=index%3;var height=size==0?24:size==1?32:40;var edge=size==2?24:20;
            check(actual.Height==height&&actual.Width>=(size==0?64:96)&&IconLayoutProbe.Bounds.Width==edge&&IconLayoutProbe.Bounds.Height==edge,
                "compiled icon Button selects actual icon size and typography padding: "+index);
            check(index<3?IconLayoutProbe.Bounds.X+edge/2<actual.X+actual.Width/2:IconLayoutProbe.Bounds.X+edge/2>actual.X+actual.Width/2,
                "compiled before/after icon positions preserve centered combined content: "+index);
            var point=new UiVector2(actual.X+actual.Width/2,actual.Y+actual.Height/2);
            input=idle with {MousePosition=point,LeftMouseDown=true,LeftMousePressed=true};using(var data=Frame(widget)){}
            input=input with {LeftMousePressed=false};using(var data=Frame(widget)){}
            check(calls==0&&IconLayoutProbe.Interaction.Held,"icon receives held state while the common release behavior waits: "+index);
            input=input with {LeftMouseDown=false,LeftMouseReleased=true};using(var data=Frame(widget)){}
            check(calls==1,"icon Button invokes its common callback once on release: "+index);
            input=idle;disabled=true;using(var data=Frame(widget)){}
            check(IconLayoutProbe.Color==UiFluentButtonPalette.Primary(context.GetStyle().FluentColorScheme,UiButtonVisualState.Disabled).Foreground,
                "disabled icon receives the actual disabled foreground: "+index);
            disabled=false;
        }
        for(var index=0;index<6;index++)
        {
            using var widget=Create(index,"");using(var data=Frame(widget)){}
            var size=index%3;var edge=size==0?24:size==1?32:40;
            check(actual.Width==edge&&actual.Height==edge&&MathF.Abs(IconLayoutProbe.Bounds.X+IconLayoutProbe.Bounds.Width/2-(actual.X+edge/2))<.001f,
                "empty-caption icon-only Button uses the official square size without a text gap: "+index);
        }
        using(var hidden=Create(0,"##hidden-icon"))using(var data=Frame(hidden))
            check(actual.Width==24&&actual.Height==24,"hidden input suffixes do not turn an icon-only Button into a text Button");
        using(var throwing=new IconThrowingButton("UI"))
        using(var data=Frame(Dux.View(ui=>
        {
            var before=new UiControlContext(ui).ClipRect;ui.PushFontSize(31);var observed=false;
            try{throwing.Render(ui);}catch(InvalidOperationException e) when(e.Message=="Declared icon failure."){observed=true;}
            check(observed&&new UiControlContext(ui).ClipRect==before&&ui.GetFontSize()==31,
                "icon exceptions restore both the control clip and surrounding font without hiding the failure");
            ui.PopFontSize();
        }))){}
        using var noTextContext=new UiContext(new(1,1,UiTextureFormat.Rgba8Unorm,[255,255,255,255]),new(1),new(2));
        using var noText=Create(0,"");
        noTextContext.NewFrame(new(1f/60,new(320,240),new(1,1)),idle,new(0,0,320,240),UiTextSettings.Default);
        noTextContext.Render(new Screen(noText.Render));noTextContext.GetDrawData().ReleasePooled();
        check(true,"icon-only vector content needs no text or font backend");
    }
    private sealed class Screen(Action<UiImmediateContext> render):UiScreen{public override void Render(UiImmediateContext ui)=>render(ui);}
}

internal readonly struct IconLayoutProbe:IUiButtonIcon
{
    internal static UiRect Bounds;
    internal static UiColor Color;
    internal static UiButtonInteraction Interaction;
    public static void Paint(UiControlContext context,UiRect bounds,UiColor color,UiButtonInteraction interaction)
    {
        Bounds=bounds;Color=color;Interaction=interaction;
        context.Frame(new(bounds.X+bounds.Width/2-2,bounds.Y+bounds.Height/2-2,4,4),color,default,0,0);
    }
}
internal readonly struct ThrowingButtonIcon:IUiButtonIcon
{
    public static void Paint(UiControlContext context,UiRect bounds,UiColor color,UiButtonInteraction interaction)=>
        throw new InvalidOperationException("Declared icon failure.");
}
[UiButtonStyle(typeof(UiReleaseButtonBehavior),typeof(UiFluentIconButtonPresentation<UiFluentMediumButtonIconSize,UiFluentPrimaryButtonAppearance,ThrowingButtonIcon,UiButtonIconBefore>))]
internal partial class IconThrowingButton;
[UiButtonStyle(typeof(UiReleaseButtonBehavior),typeof(UiFluentIconButtonPresentation<UiFluentSmallButtonIconSize,UiFluentPrimaryButtonAppearance,IconLayoutProbe,UiButtonIconBefore>))]
internal partial class IconInputBeforeSmall;
[UiButtonStyle(typeof(UiReleaseButtonBehavior),typeof(UiFluentIconButtonPresentation<UiFluentMediumButtonIconSize,UiFluentPrimaryButtonAppearance,IconLayoutProbe,UiButtonIconBefore>))]
internal partial class IconInputBeforeMedium;
[UiButtonStyle(typeof(UiReleaseButtonBehavior),typeof(UiFluentIconButtonPresentation<UiFluentLargeButtonIconSize,UiFluentPrimaryButtonAppearance,IconLayoutProbe,UiButtonIconBefore>))]
internal partial class IconInputBeforeLarge;
[UiButtonStyle(typeof(UiReleaseButtonBehavior),typeof(UiFluentIconButtonPresentation<UiFluentSmallButtonIconSize,UiFluentPrimaryButtonAppearance,IconLayoutProbe,UiButtonIconAfter>))]
internal partial class IconInputAfterSmall;
[UiButtonStyle(typeof(UiReleaseButtonBehavior),typeof(UiFluentIconButtonPresentation<UiFluentMediumButtonIconSize,UiFluentPrimaryButtonAppearance,IconLayoutProbe,UiButtonIconAfter>))]
internal partial class IconInputAfterMedium;
[UiButtonStyle(typeof(UiReleaseButtonBehavior),typeof(UiFluentIconButtonPresentation<UiFluentLargeButtonIconSize,UiFluentPrimaryButtonAppearance,IconLayoutProbe,UiButtonIconAfter>))]
internal partial class IconInputAfterLarge;
