using Duxel.Core;
using Duxel.Platform.Windows;

internal static class BadgeWidgetTests
{
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static (long Bytes,bool SameReference) MeasureCounterReads(ref UiBadgeTextCache cache,string expected,bool varying,bool invalidate=false)
    {
        var sameReference=true;
        var before=GC.GetAllocatedBytesForCurrentThread();
        for(var i=0;i<10000;i++)
        {
            if(invalidate)cache.Clear();
            var content=UiCounterBadgeBehavior.Resolve(new(varying?100+i:124),ref cache);
            sameReference&=ReferenceEquals(expected,content.Text);
        }
        return(GC.GetAllocatedBytesForCurrentThread()-before,sameReference);
    }
    internal static void Run(Action<bool,string> check)
    {
        var cache=new UiBadgeTextCache();
        UiBadgeContent Counter(UiCounterBadgeValue value)=>UiCounterBadgeBehavior.Resolve(value,ref cache);
        check(!Counter(default).Visible&&Counter(new(0,ShowZero:true)).Text=="0","CounterBadge hides zero unless showZero is requested");
        check(Counter(new(99)).Text=="99"&&Counter(new(100)).Text=="99+"&&Counter(new(int.MaxValue)).Text=="99+",
            "CounterBadge uses the official99 overflow default including a default source value");
        check(Counter(new(-12)).Text=="-12"&&Counter(new(1,0)).Text=="0+"&&Counter(new(5,4)).Text=="4+",
            "CounterBadge preserves signed values and an explicit zero overflow limit");
        var dot=Counter(new(0,Dot:true));
        check(dot.Visible&&dot.Dot&&dot.Text.Length==0&&dot.AccessibleLabel=="0","CounterBadge dot remains visible and retains its count description");
        check(Counter(new(0,Text:"New")).Text=="New"&&Counter(new(12,Text:"")).Text=="12",
            "explicit CounterBadge content is preserved while an empty override uses counter behavior");
        var first=Counter(new(124)).Text;
        check(ReferenceEquals(first,Counter(new(124)).Text),"unchanged counter formatting retains one bounded numeric string");
        check(ReferenceEquals(first,Counter(new(125)).Text),"changing an overflowed count reuses its unchanged displayed label");
        check(System.Runtime.CompilerServices.Unsafe.SizeOf<UiTextBadgeBehavior>()==1
            &&!System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<UiTextBadgeBehavior>(),
            "static badge behavior has no counter memo or GC reference fields");
        // Warm up the exact measured method, not a shorter loop in this large scenario method.
        var coldUnchanged=MeasureCounterReads(ref cache,first,false);
        var coldOverflow=MeasureCounterReads(ref cache,first,true);
        var unchanged=MeasureCounterReads(ref cache,first,false);
        check(unchanged.SameReference&&unchanged.Bytes==0,
            $"10000 unchanged counter behavior reads allocate no managed storage; measured={unchanged.Bytes}B");
        var overflow=MeasureCounterReads(ref cache,first,true);
        check(overflow.SameReference&&overflow.Bytes==0,
            $"10000 changing overflowed counts allocate nothing while their displayed label remains99+; measured={overflow.Bytes}B");
        var invalidated=MeasureCounterReads(ref cache,first,false,true);
        check(!invalidated.SameReference&&invalidated.Bytes>0,
            "the same allocation probe detects real formatting allocations when the counter memo is invalidated");
        Console.WriteLine($"Badge counter allocation: cold unchanged={coldUnchanged.Bytes}B, cold overflow={coldOverflow.Bytes}B, steady unchanged={unchanged.Bytes}B, steady overflow={overflow.Bytes}B, invalidated={invalidated.Bytes}B.");
        cache.Clear();
        check(Counter(new(7)).Text=="7","counter memo can be released and reused without stale content");
        static bool Reject(Action action){try{action();return false;}catch(ArgumentException){return true;}}
        check(Reject(()=>UiCounterBadgeBehavior.ValidateOptions(new(Appearance:UiBadgeAppearance.Tint)))
            &&Reject(()=>UiCounterBadgeBehavior.ValidateOptions(new(Shape:UiBadgeShape.Square)))
            &&Reject(()=>UiCounterBadgeBehavior.ValidateOptions(new(Color:UiBadgeColor.Success))),
            "CounterBadge rejects variants absent from its official contract");
        check(Reject(()=>new UiFluentBadgeOptions(Size:(UiBadgeSize)99).Validate()),"invalid badge variants fail explicitly");
        check(UiFluentBadgePalette.Resolve(UiBadgeColorScheme.Light,UiBadgeAppearance.Filled,UiBadgeColor.Brand).Background==new UiColor(0x0f,0x6c,0xbd)
            &&UiFluentBadgePalette.Resolve(UiBadgeColorScheme.Dark,UiBadgeAppearance.Filled,UiBadgeColor.Brand).Background==new UiColor(0x11,0x5e,0xa3),
            "Fluent Badge brand colors use actual brandWeb tokens rather than stale alias comments");
        check(UiFluentBadgePalette.Resolve(UiBadgeColorScheme.Dark,UiBadgeAppearance.Ghost,UiBadgeColor.Danger).Foreground==new UiColor(0xdc,0x5e,0x62)
            &&UiFluentBadgePalette.Resolve(UiBadgeColorScheme.Dark,UiBadgeAppearance.Tint,UiBadgeColor.Danger).Background==new UiColor(0x3f,0x10,0x11),
            "dark semantic foreground/background tokens use their distinct tint and shade values");
        check(UiCompiledDesign.Windows11Dark.Theme.BadgeColorScheme==UiBadgeColorScheme.Dark
            &&UiCompiledDesign.Windows11.Theme.BadgeColorScheme==UiBadgeColorScheme.Light,"built-in light/dark designs carry explicit badge palette metadata");
        check(Fluent.ComponentNames.Contains("Badge")&&Fluent.ComponentNames.Contains("CounterBadge")
            &&!Fluent.ComponentNames.Contains("LegacyBadge"),"generated discovery exposes Badge/CounterBadge and excludes the legacy badge");
        using(var fontless=new UiContext(new(1,1,UiTextureFormat.Rgba8Unorm,[255,255,255,255]),new(1),new(2)))
        {
            var input=new UiInputState(default,false,false,false,false,false,false,0,0,[],[],default,default);
            fontless.NewFrame(new(1f/60,new(320,240),new(1,1)),input,new(0,0,320,240),UiTextSettings.Default);
            using var dotOnly=Fluent.CounterBadge(0,dot:true);
            fontless.Render(new Screen(dotOnly.Render));
            using var dotData=fontless.GetDrawData();
            check(dotData.DrawLists.Sum(l=>l.Commands.Count)>0,"a dot-only counter uses vector geometry without requiring a text backend");
        }
        using var context=new UiContext(new(1,1,UiTextureFormat.Rgba8Unorm,[255,255,255,255]),new(1),new(2));
        var fonts=Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        context.SetPlatformTextBackend(WindowsPlatformTextBackend.Instance);context.SetDirectTextEnabled(true);
        context.SetDirectTextPrimaryFontPath(Path.Combine(fonts,"segoeui.ttf"));context.SetDirectTextBaseFontSize(14);
        context.SetTypographyFonts(new(Path.Combine(fonts,"segoeui.ttf"),Path.Combine(fonts,"seguisb.ttf"),Path.Combine(fonts,"segoeuib.ttf")));
        var idle=new UiInputState(new(-100,-100),false,false,false,false,false,false,0,0,[],[],default,default);
        UiDrawData Frame(Action<UiImmediateContext> draw)
        {
            context.NewFrame(new(1f/60,new(320,240),new(1,1)),idle,new(0,0,320,240),UiTextSettings.Default);
            context.Render(new Screen(draw));return context.GetDrawData();
        }
        using var factory=Dux.Badge("Ready");
        check(factory is UiFluentBadge&&DuxelView.Display.Badge("Ready") is UiFluentBadge,
            "default badge factories consume generated inheritable widgets");
        var frameRequests=0;var publishedSources=new List<string>();
        context.SetRequestFrameCallback(()=>{frameRequests++;publishedSources.Add(factory.Source);});
        using(var data=Frame(factory.Render)){}
        var beforeRequests=frameRequests;
        factory.Source="Updated";factory.Source="Updated";
        factory.Options=new(Shape:UiBadgeShape.Rounded);factory.Options=new(Shape:UiBadgeShape.Rounded);
        check(frameRequests-beforeRequests==2&&publishedSources.TakeLast(2).SequenceEqual(new[]{"Updated","Updated"}),
            "source/options changes publish before requesting a frame once while identical assignments remain idle");
        using var emptyText=Fluent.Badge((string)null!);
        check(emptyText.Source.Length==0&&Reject(()=>Fluent.Badge((Func<string>)null!)),
            "badge text normalizes null while a missing getter fails at construction");
        foreach(var size in Enum.GetValues<UiBadgeSize>())
        foreach(var shape in Enum.GetValues<UiBadgeShape>())
        {
            using var data=Frame(ui=>{ui.SetCursorScreenPos(new(20,20));ui.Badge("8",new(size,shape));});
            var rect=data.DrawLists.SelectMany(l=>l.RectFilledPrimitives is {} primitives?primitives.AsSpan().ToArray():Array.Empty<UiRectFilledPrimitive>()).First();
            var expectedHeight=UiBadgeMetrics.FromSize(size).Height;
            var expectedRadius=shape==UiBadgeShape.Circular?expectedHeight/2:shape==UiBadgeShape.Square?0:
                size is UiBadgeSize.Small or UiBadgeSize.Tiny or UiBadgeSize.ExtraSmall?2:4;
            check(rect.Rect.Height==expectedHeight&&rect.CornerRadius==expectedRadius,
                $"Badge actual retained geometry follows {size}/{shape} dimensions and curvature");
        }
        using(var empty=Frame(ui=>ui.Badge("",new(Appearance:UiBadgeAppearance.Ghost))))
            check(empty.DrawLists.Sum(l=>l.Commands.Count)==0,"an empty ghost badge advances layout without emitting transparent frame draws");
        using var hidden=Fluent.CounterBadge(0);
        var before=default(UiVector2);var after=default(UiVector2);
        using(var data=Frame(ui=>{before=ui.GetCursorScreenPos();hidden.Render(ui);after=ui.GetCursorScreenPos();})){}
        check(before==after,"hidden zero CounterBadge consumes no layout space");
        using var icon=new BadgeValidationIconWidget("");
        check(Reject(()=>{using var data=Frame(icon.Render);}),"icon-only badge requires an explicit accessible label");
        icon.AccessibleLabel="Completed";
        using(var data=Frame(icon.Render))check(data.DrawLists.Sum(l=>l.Commands.Count)>0,"generated user icon presentation renders real vector content");
        var reads=0;
        using var dynamic=Fluent.CounterBadge(()=>{reads++;return new(108);});
        using(var data=Frame(dynamic.Render)){}
        using(var data=Frame(dynamic.Render)){}
        check(reads==2,"a retained dynamic counter reads current data once each UI frame");
        using var replacement=new UiBadge<UiCounterBadgeValue,UiCounterBadgeBehavior,BadgeValidationBoxPresentation>(new(108));
        using(var data=Frame(replacement.Render))check(BadgeValidationBoxPresentation.LastText=="99+"
            &&data.DrawLists.SelectMany(l=>l.RectFilledPrimitives is {} primitives?primitives.AsSpan().ToArray():Array.Empty<UiRectFilledPrimitive>()).Any(r=>r.Rect.Width==72&&r.Rect.Height==24),
            "the same counter behavior supports a different composition and drawing algorithm");
        using(var data=Frame(ui=>{factory.Render(ui);check(ui.ControlNavigationStorage.References==0,"passive badges introduce no keyboard tab stop");})){}
        var light=context.GetStyle();light.BadgeColorScheme=UiBadgeColorScheme.Light;context.SetTheme(light);
        var replay=false;
        void Layer(UiImmediateContext ui)
        {
            var options=new UiLayerOptions(true,1,default);
            replay=ui.TryReplayLayer("badge-palette",options);
            if(!replay){ui.BeginLayer("badge-palette",options);factory.Render(ui);ui.EndLayer();}
        }
        using(var data=Frame(Layer)){}
        using(var data=Frame(Layer)){}
        check(replay,"unchanged badge static layer replays without reconstructing its paint");
        light.BadgeColorScheme=UiBadgeColorScheme.Dark;context.SetTheme(light);
        using(var data=Frame(Layer)){}
        check(!replay,"badge palette metadata invalidates retained static layers even if all legacy color fields are unchanged");
        dynamic.Dispose();
        var disposedRejected=false;
        try{dynamic.Source=()=>new(7);}catch(ObjectDisposedException){disposedRejected=true;}
        check(dynamic.Source is null&&disposedRejected,"disposing a dynamic badge releases its source/memo and rejects new source ownership");
    }
    private sealed class Screen(Action<UiImmediateContext> draw):UiScreen {public override void Render(UiImmediateContext ui)=>draw(ui);}
}

internal readonly struct BadgeValidationIcon:IUiBadgeIcon
{
    public static bool IsPresent=>true;
    public static void Paint(UiControlContext context,UiRect bounds,UiColor color)=>
        context.Circle(new(bounds.X+bounds.Width/2,bounds.Y+bounds.Height/2),bounds.Width/3,color);
}
[UiWidgetStyle(typeof(UiBadge<,,>),typeof(string),typeof(UiTextBadgeBehavior),typeof(UiFluentBadgePresentation<BadgeValidationIcon>))]
internal partial class BadgeValidationIconWidget;
internal readonly struct BadgeValidationBoxPresentation:IUiBadgePresentation
{
    internal static string? LastText;
    public static void Render(UiControlContext context,UiBadgeContent content,UiFluentBadgeOptions options)
    {
        LastText=content.Text;
        var layout=context.PlaceText(new(72,24));
        if(layout.Visible)context.Frame(new(layout.Position.X,layout.Position.Y,72,24),new(0xff3f2f1f),new(0xffffffff),0,1);
    }
}
