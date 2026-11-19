using Duxel.Core;
using Duxel.Platform.Windows;

internal static class DividerWidgetTests
{
    internal static void Run(Action<bool,string> check)
    {
        using var context=new UiContext(new(1,1,UiTextureFormat.Rgba8Unorm,[255,255,255,255]),new(1),new(2));
        var input=new UiInputState(default,false,false,false,false,false,false,0,0,[],[],default,default);
        UiDrawData Frame(Action<UiImmediateContext> render)
        {
            context.NewFrame(new(1f/60,new(320,240),new(1,1)),input,new(0,0,320,240),UiTextSettings.Default);
            context.Render(new Screen(render));return context.GetDrawData();
        }
        static UiRectFilledPrimitive[] Rectangles(UiDrawData data)=>data.DrawLists.SelectMany(list=>
            list.RectFilledPrimitives is {} items?items.AsSpan().ToArray():Array.Empty<UiRectFilledPrimitive>()).ToArray();
        static bool Equal(UiDrawData a,UiDrawData b)=>a.DrawLists.Count==b.DrawLists.Count&&a.DrawLists.Zip(b.DrawLists).All(pair=>
            pair.First.Vertices.AsSpan().SequenceEqual(pair.Second.Vertices.AsSpan())&&pair.First.Indices.AsSpan().SequenceEqual(pair.Second.Indices.AsSpan())
            &&pair.First.Commands.AsSpan().SequenceEqual(pair.Second.Commands.AsSpan()))&&Rectangles(a).SequenceEqual(Rectangles(b));
        using(var plain=Frame(ui=>ui.Divider(options:new(Length:180))))
            check(Rectangles(plain) is [var line]&&line.Rect.Width==180&&line.Rect.Height==1,
                "a contentless Divider renders one180x1 line without a font backend");
        var origin=default(UiVector2);
        using(var vertical=Frame(ui=>{origin=ui.GetCursorScreenPos();ui.Divider(options:new(Vertical:true,Inset:true,Length:100));}))
            check(Rectangles(vertical) is [var line]&&line.Rect.Width==1&&line.Rect.Height==100&&line.Rect.Y==origin.Y+12,
                "vertical inset Divider preserves100px line length with12px external margins");
        var noTextFocus=0;
        using(var data=Frame(ui=>{ui.Divider();noTextFocus=ui.ControlNavigationStorage.References;})){}
        check(noTextFocus==0,"a passive Divider creates no keyboard tab stop");
        var expectedClip=default(UiRect);
        using(var table=Frame(ui=>
        {
            ui.BeginTable("divider-cell",1);ui.TableSetupColumn("Column",80,0);ui.TableNextRow();
            var cell=ui.GetCursorScreenPos();expectedClip=new(cell.X,cell.Y,80,ui.GetFrameHeight());
            ui.Divider(options:new(Appearance:UiDividerAppearance.Brand,Length:200));ui.EndTable();
        }))
        {
            var strokeClips=new List<UiRect>();
            var stroke=UiFluentDividerPalette.Resolve(context.GetStyle().FluentColorScheme,UiDividerAppearance.Brand).Line;
            foreach(var list in table.DrawLists)
            foreach(var command in list.Commands.Where(command=>command.Kind==UiDrawCommandKind.RectFilledPrimitives))
            for(var index=command.IndexOffset;index<command.IndexOffset+command.ElementCount;index++)
                if(list.RectFilledPrimitives!.AsSpan()[(int)index].Color==stroke)strokeClips.Add(command.ClipRect);
            if(strokeClips.Count!=1||strokeClips[0]!=expectedClip)
                throw new InvalidOperationException($"Divider table clip expected={expectedClip}; actual={string.Join(';',strokeClips)}");
            check(true,"Divider strokes use the actual table-cell clip rather than the surrounding window clip");
        }
        var fontRejected=false;
        try{using var data=Frame(ui=>ui.Divider("Required metrics"));}catch(NotSupportedException){fontRejected=true;}
        check(fontRejected,"text Divider fails explicitly without typography metrics");
        var fonts=Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        context.SetPlatformTextBackend(WindowsPlatformTextBackend.Instance);context.SetDirectTextEnabled(true);
        context.SetDirectTextPrimaryFontPath(Path.Combine(fonts,"segoeui.ttf"));context.SetDirectTextBaseFontSize(16);
        context.SetTypographyFonts(new(Path.Combine(fonts,"segoeui.ttf"),Path.Combine(fonts,"seguisb.ttf"),Path.Combine(fonts,"segoeuib.ttf")));
        foreach(var vertical in new[]{false,true})
        foreach(var alignment in Enum.GetValues<UiDividerAlignment>())
        foreach(var appearance in Enum.GetValues<UiDividerAppearance>())
        {
            var options=new UiDividerOptions(vertical,alignment,appearance,Length:180);
            using var widget=Fluent.Divider("Section",options);
            using var immediate=Frame(ui=>ui.Divider("Section",options));using var compiled=Frame(widget.Render);
            check(Equal(immediate,compiled),$"generated Divider shares exact layout/commands with immediate rendering: {vertical}/{alignment}/{appearance}");
        }
        using(var start=Frame(ui=>ui.Divider("Start",new(Alignment:UiDividerAlignment.Start,Length:200))))
            check(Rectangles(start)[0].Rect.Width==8,"start-aligned Divider retains the official8px leading segment");
        using(var end=Frame(ui=>ui.Divider("End",new(Alignment:UiDividerAlignment.End,Length:200))))
            check(Rectangles(end)[1].Rect.Width==8,"end-aligned Divider retains the official8px trailing segment");
        using(var rtl=Frame(ui=>ui.Divider("Start",new(Alignment:UiDividerAlignment.Start,Length:200,RightToLeft:true))))
            check(Rectangles(rtl)[1].Rect.Width==8,"RTL maps logical start to the right-hand short segment");
        var wrappedHeight=0f;
        using(var data=Frame(ui=>{ui.Divider("Several words wrap across a narrow divider",new(Length:100));wrappedHeight=ui.GetItemRectSize().Y;})){}
        check(wrappedHeight>16&&wrappedHeight%16==0,"Divider content wraps in actual12px type with16px line boxes without truncation");
        var restoredSize=0f;
        using(var data=Frame(ui=>{ui.PushFontSize(28);ui.Divider("Font scope");restoredSize=ui.GetFontSize();ui.PopFontSize();})){}
        check(restoredSize==28,"Divider typography restores the surrounding font scope");
        using var derived=new DerivedDivider("Inherited",new(Length:200));
        using(var expected=Frame(ui=>ui.Divider("Inherited!",derived.Options)))
        using(var actual=Frame(derived.Render))check(Equal(expected,actual),"a user-derived Divider shares the kernel and can override content");
        using var replacement=new UiDivider<string,UiStaticTextSource,BoxPresentation>("Custom");
        using(var data=Frame(replacement.Render))check(Rectangles(data) is [var box]&&box.Rect.Width==72&&box.Rect.Height==24,
            "Divider content sourcing supports a different statically selected composition/paint algorithm");
        var requests=0;var published="";
        context.SetRequestFrameCallback(()=>{requests++;published=derived.Source;});
        using(var data=Frame(derived.Render)){}
        var before=requests;derived.Source="Updated";derived.Source="Updated";
        derived.Options=derived.Options with {Inset=true};derived.Options=derived.Options;
        check(requests-before==2&&published=="Updated","Divider content/options publish before waking once per change");
        var reads=0;using var dynamic=Fluent.Divider(()=>{reads++;return "Dynamic";});
        using(var data=Frame(dynamic.Render)){}
        using(var data=Frame(dynamic.Render)){}
        check(reads==2,"a dynamic Divider reads its content once every rendered frame");
        static bool Reject(Action action){try{action();return false;}catch(ArgumentException){return true;}}
        check(Reject(()=>Fluent.Divider("Bad",new(Length:float.NaN)))&&Reject(()=>Fluent.Divider("Bad",new(Length:-1)))
            &&Reject(()=>Fluent.Divider("Bad",new(Appearance:(UiDividerAppearance)99))),"invalid Divider options fail before rendering");
        dynamic.Dispose();var disposed=false;try{dynamic.Source=()=>"After";}catch(ObjectDisposedException){disposed=true;}
        check(dynamic.Source is null&&disposed,"disposing Divider releases its getter and rejects new ownership");
        var theme=context.GetStyle();theme.FluentColorScheme=UiFluentColorScheme.Dark;
        check(theme.BadgeColorScheme==UiBadgeColorScheme.Dark,"common Fluent color scheme preserves the Badge compatibility alias");
        theme.BadgeColorScheme=UiBadgeColorScheme.Light;
        check(theme.FluentColorScheme==UiFluentColorScheme.Light,"Badge compatibility writes update the single common Fluent scheme");
        check(Fluent.ComponentNames.Contains("Divider")&&!Fluent.MissingComponentNames.Contains("Divider"),
            "the real generated Divider factory consumes its existing official-site requirement");
    }
    private sealed class Screen(Action<UiImmediateContext> draw):UiScreen{public override void Render(UiImmediateContext ui)=>draw(ui);}
    private sealed class DerivedDivider(string text,UiDividerOptions options):UiFluentDivider(text,options)
    {protected override string ReadText()=>base.ReadText()+"!";}
    private readonly struct BoxPresentation:IUiDividerPresentation
    {
        public static void Render(UiControlContext context,string text,UiDividerOptions options)
        {var layout=context.PlaceText(new(72,24));if(layout.Visible)context.Frame(new(layout.Position.X,layout.Position.Y,72,24),new(0xff123456),new(0xff123456),0,0);}
    }
}
