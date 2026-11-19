using Duxel.Core;
using Duxel.Platform.Windows;

internal static class ButtonTypographyTests
{
    internal static void Run(Action<bool,string> check)
    {
        using var context=new UiContext(new(1,1,UiTextureFormat.Rgba8Unorm,[255,255,255,255]),new(1),new(2));
        ControlFontFixture.Configure(context);
        var backend=new CountingBackend();context.SetPlatformTextBackend(backend);
        var idle=new UiInputState(new(-100,-100),false,false,false,false,false,false,0,0,[],[],default,default);
        UiImmediateContext? last=null;
        UiDrawData Frame(Action<UiImmediateContext> render)
        {
            context.NewFrame(new(1f/60,new(320,240),new(1,1)),idle,new(0,0,320,240),UiTextSettings.Default);
            context.Render(new Screen(ui=>{last=ui;render(ui);}));return context.GetDrawData();
        }
        using var button=Fluent.Button("Medium");var bounds=default(UiRect);
        using(var actual=Frame(ui=>{ui.PushFontSize(28);button.Render(ui);bounds=new(ui.GetItemRectMin().X,ui.GetItemRectMin().Y,ui.GetItemRectSize().X,ui.GetItemRectSize().Y);check(ui.GetFontSize()==28,"a Fluent button restores its caller's pushed font after painting");ui.PopFontSize();}))
            check(bounds.Width>=96&&bounds.Height==32,"the default medium button uses its20px line box and5px padding plus1px borders");
        check(backend.LastFont.EndsWith("seguisb.ttf",StringComparison.OrdinalIgnoreCase)&&backend.LastSize==14,
            "the generated default button requests actual14px Segoe UI Semibold instead of the surrounding font");
        var paintStates=new UiWidget[]{new ButtonPaintRestMedium("UI"),new ButtonPaintHoverMedium("UI"),new ButtonPaintHeldMedium("UI"),new ButtonPaintRestMedium("UI")};
        try
        {
            for(var state=0;state<paintStates.Length;state++)
            {
                using var painted=Frame(ui=>{ui.BeginDisabled(state==3);paintStates[state].Render(ui);ui.EndDisabled();});
                var expected=UiFluentButtonPalette.Resolve(context.GetStyle().FluentColorScheme,(UiButtonVisualState)state).Foreground;
                var colors=new List<UiColor>();
                foreach(var list in painted.DrawLists)
                foreach(var command in list.Commands)
                {
                    if(command.Kind!=UiDrawCommandKind.Triangles||command.TextureId==new UiTextureId(2))continue;
                    var vertices=list.Vertices.AsSpan();var indices=list.Indices.AsSpan();
                    for(var index=0;index<command.ElementCount;index++)colors.Add(vertices[(int)(indices[(int)command.IndexOffset+index]+command.VertexOffset)].Color);
                }
                check(colors.Count>0&&colors.All(color=>color==expected),"actual Button glyph vertices carry the selected foreground tint for "+(UiButtonVisualState)state);
            }
        }
        finally{foreach(var widget in paintStates)widget.Dispose();}
        using(var actual=Frame(ui=>{ui.PushFontSize(11);button.Render(ui);ui.PopFontSize();}))
            check(backend.MetricQueries==1,"unchanged compiled control typography reuses one context metrics slot across font scopes and frames");
        var exceptionObserved=false;
        using(var data=Frame(ui=>
        {
            ui.PushFontSize(28);
            try{ui.Button<UiReleaseButtonBehavior,ThrowingPresentation>("Fail");}catch(InvalidOperationException error) when(error.Message=="Declared measure failure."){exceptionObserved=true;}
            check(ui.GetFontSize()==28,"a failing compiled presentation also restores the caller font scope");ui.PopFontSize();
        })){}
        check(exceptionObserved,"a presentation failure remains observable without a font fallback");
        var many=Enumerable.Range(0,10000).Select(_=>Fluent.Button("Repeated")).ToArray();
        try
        {
            using(var data=Frame(ui=>{foreach(var widget in many){ui.SetCursorScreenPos(new(20,2000));widget.Render(ui);}})){}
            check(backend.MetricQueries==1,"ten thousand controls do not issue ten thousand font-metrics backend queries");
        }
        finally{foreach(var widget in many)widget.Dispose();}
        using var small=new TypographyValidationSmallButton("Small");using var large=new TypographyValidationLargeButton("Large");
        var smallSize=default(UiVector2);var largeSize=default(UiVector2);
        using(var data=Frame(ui=>{small.Render(ui);smallSize=ui.GetItemRectSize();large.Render(ui);largeSize=ui.GetItemRectSize();})){}
        check(smallSize==new UiVector2(64,24)&&largeSize.Y==40&&largeSize.X>=96,
            "source-generated small/large styles select their official12px regular/16px semibold sizing implementations");
        var mixed=Enumerable.Range(0,10000).Select(i=>(i%3) switch
        {0=>(UiWidget)new TypographyValidationSmallButton("Mixed"),1=>Fluent.Button("Mixed"),_=>new TypographyValidationLargeButton("Mixed")}).ToArray();
        var queries=backend.MetricQueries;
        try
        {
            using(var data=Frame(ui=>{foreach(var widget in mixed){ui.SetCursorScreenPos(new(20,2000));widget.Render(ui);}})){}
            check(backend.MetricQueries==queries,"ten thousand interleaved small/medium/large controls reuse bounded font metrics without backend queries");
        }
        finally{foreach(var widget in mixed)widget.Dispose();}
        queries=backend.MetricQueries;
        backend.TextLayoutVersion++;
        using(var data=Frame(button.Render)){}
        check(backend.MetricQueries==queries+1,"a backend revision invalidates the compiled control font-metrics slot");
        var replay=false;
        void Layer(UiImmediateContext ui)
        {
            var options=new UiLayerOptions(true,1,default);
            replay=ui.TryReplayLayer("button-font",options);
            if(!replay){ui.BeginLayer("button-font",options);button.Render(ui);ui.EndLayer();}
        }
        using(var data=Frame(Layer)){}
        using(var data=Frame(Layer)){}
        check(replay,"unchanged font-configured buttons support explicit static-layer replay");
        var directory=Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        context.SetTypographyFonts(new(Path.Combine(directory,"segoeui.ttf"),Path.Combine(directory,"segoeuib.ttf"),Path.Combine(directory,"segoeuib.ttf")));
        using(var data=Frame(Layer)){}
        check(!replay&&backend.LastFont.EndsWith("segoeuib.ttf",StringComparison.OrdinalIgnoreCase),
            "font-face configuration invalidates static layers and selects the new concrete face");
        check(last!.ControlTypographyRetainedCharacters<=4096,"control font-metrics retention is bounded to four short face-path slots");
        context.Dispose();check(last.ControlTypographyRetainedCharacters==0,"context disposal releases control typography memo references");
        using var unsupported=new UiContext(new(1,1,UiTextureFormat.Rgba8Unorm,[255,255,255,255]),new(1),new(2));
        unsupported.NewFrame(new(1f/60,new(100,100),new(1,1)),idle,new(0,0,100,100),UiTextSettings.Default);
        var rejected=false;try{unsupported.Render(new Screen(button.Render));}catch(NotSupportedException){rejected=true;}
        check(rejected,"text-bearing Fluent buttons reject missing typography metrics instead of substituting atlas glyphs");
        using var empty=Fluent.Button("");unsupported.NewFrame(new(1f/60,new(100,100),new(1,1)),idle,new(0,0,100,100),UiTextSettings.Default);
        unsupported.Render(new Screen(empty.Render));unsupported.GetDrawData().ReleasePooled();
        check(true,"an empty Fluent button does not require a text backend merely to draw its frame");
    }
    private sealed class Screen(Action<UiImmediateContext> draw):UiScreen{public override void Render(UiImmediateContext ui)=>draw(ui);}
    private readonly struct ThrowingPresentation:IUiButtonPresentation
    {
        public static UiControlTypographyScope BeginText(UiControlContext context,bool hasText)=>UiFluentButtonPresentation.BeginText(context,hasText);
        public static UiVector2 Measure(UiControlContext context,UiVector2 text,UiVector2 size)=>throw new InvalidOperationException("Declared measure failure.");
        public static void Paint(UiControlContext context,string text,UiVector2 size,UiRect bounds,UiButtonInteraction interaction){}
    }
    private sealed class CountingBackend:IPlatformTextBackend,IPlatformTextLayoutBackend,IPlatformFontMetricsBackend
    {
        internal int MetricQueries;internal string LastFont="";internal float LastSize;
        public ulong TextLayoutVersion {get;set;}
        public bool TryGetFontMetrics(string font,float size,out PlatformFontMetrics metrics)
        {MetricQueries++;return WindowsPlatformTextBackend.Instance.TryGetFontMetrics(font,size,out metrics);}
        public bool TryLayout(in PlatformTextMeasureRequest request,out PlatformTextLayoutResult result)
        {LastFont=request.FontPath;LastSize=request.FontSize;return WindowsPlatformTextBackend.Instance.TryLayout(request,out result);}
        public bool TryMeasure(in PlatformTextMeasureRequest request,out PlatformTextMeasureResult result)=>WindowsPlatformTextBackend.Instance.TryMeasure(request,out result);
        public bool TryRasterize(in PlatformTextRasterizeRequest request,out PlatformTextRasterizeResult result)
        {LastFont=request.FontPath;LastSize=request.FontSize;return WindowsPlatformTextBackend.Instance.TryRasterize(request,out result);}
    }
}

[UiButtonStyle(typeof(UiReleaseButtonBehavior),typeof(UiFluentSmallButtonPresentation))]
internal partial class TypographyValidationSmallButton;
[UiButtonStyle(typeof(UiReleaseButtonBehavior),typeof(UiFluentLargeButtonPresentation))]
internal partial class TypographyValidationLargeButton;
