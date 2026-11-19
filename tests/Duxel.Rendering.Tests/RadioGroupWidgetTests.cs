using Duxel.Core;

internal static class RadioGroupWidgetTests
{
    internal static void Run(Action<bool,string> check)
    {
        using var context=new UiContext(new(1,1,UiTextureFormat.Rgba8Unorm,[255,255,255,255]),new(1),new(2));
        ControlFontFixture.Configure(context);
        UiRadioOption[] options=[new("a","Alpha"),new("b","Beta",false),new("c","Charlie"),new("d","Delta")];
        string? value="a";var changes=new List<string>();
        using var widget=new InheritedGroup(new(()=>value,next=>{value=next;changes.Add(next!);}),options);
        using var outside=Fluent.Button("Outside");
        var idle=new UiInputState(new(-100,-100),false,false,false,false,false,false,0,0,[],[],default,default);
        var disabled=false;var hidden=false;var focused=false;
        UiDrawData Frame(UiInputState? input=null,Action<UiImmediateContext>? render=null)
        {
            context.NewFrame(new(1f/60,new(320,240),new(1,1)),input??idle,new(0,0,320,240),UiTextSettings.Default);
            context.Render(new Screen(render??(ui=>
            {
                if(!hidden){ui.SetCursorScreenPos(new(10,10));ui.BeginDisabled(disabled);widget.Render(ui);ui.EndDisabled();focused=ui.IsItemFocused();}
                ui.SetCursorScreenPos(new(200,10));outside.Render(ui);
            })));
            return context.GetDrawData();
        }
        void Step(UiInputState? input=null){using var data=Frame(input);}
        void Key(UiKey key,KeyModifiers modifiers=default)=>Step(idle with{KeyEvents=[new(key,true,modifiers)]});
        void Click(float y){Step(idle with{MousePosition=new(20,y),LeftMouseDown=true,LeftMousePressed=true});Step(idle with{MousePosition=new(20,y),LeftMouseReleased=true});}
        Step();check(value=="a"&&changes.Count==0,"RadioGroup reads controlled initial selection without invoking callbacks");
        Key(UiKey.Tab);check(focused,"Tab enters the RadioGroup as one logical stop");
        Key(UiKey.DownArrow);check(value=="c"&&changes.SequenceEqual(new[]{"c"}),"RadioGroup arrow selection skips a disabled option");
        Key(UiKey.DownArrow);Key(UiKey.RightArrow);Key(UiKey.LeftArrow);
        check(value=="d"&&changes.SequenceEqual(new[]{"c","d","a","d"}),"RadioGroup arrows wrap in both directions without clearing selection");
        var before=changes.Count;
        Step(idle with{KeyEvents=[new(UiKey.UpArrow,true,default),new(UiKey.UpArrow,true,default)]});
        check(value=="a"&&changes.Skip(before).SequenceEqual(new[]{"c","a"}),"batched RadioGroup arrow events preserve both intermediate callback values and order");
        check(widget.Changes==changes.Count,"inherited RadioGroup observes exactly the generated binding changes");
        Key(UiKey.Tab);check(!focused,"Tab leaves the entire RadioGroup rather than visiting every option");
        before=changes.Count;Key(UiKey.DownArrow);check(changes.Count==before,"RadioGroup ignores arrows when another widget owns focus");
        Key(UiKey.Tab,KeyModifiers.Shift);check(focused,"Shift+Tab returns to the RadioGroup selection");
        value=null;Step();
        Step(idle with{KeyEvents=[new(UiKey.Space,true,default)]});check(value is null,"RadioGroup Space press waits for release");
        Step(idle with{KeyEvents=[new(UiKey.Space,false,default)]});
        check(value=="a","Space selects the first enabled option when controlled selection is empty");
        before=changes.Count;Key(UiKey.Enter);Click(50);
        check(value=="a"&&changes.Count==before,"Enter and clicking the selected radio never toggle it off or invoke a no-op callback");
        Click(82);check(value=="a"&&changes.Count==before,"disabled radio and its label do not change selection");
        Step(idle with{MousePosition=new(20,50),LeftMouseDown=true,LeftMousePressed=true});
        Step(idle with{MousePosition=new(20,114),LeftMouseReleased=true});
        check(value=="a"&&changes.Count==before,"releasing over another radio does not select the row that did not receive the press");
        Step(idle with{MousePosition=new(20,114),LeftMouseDown=true,LeftMousePressed=true});
        Step(idle with{MousePosition=new(20,114),LeftMouseDown=true,KeyEvents=[new(UiKey.Escape,true,default)]});
        check(context.State.ActiveId is null,"Escape cancels radio capture while the pointer is still held");
        Step(idle with{MousePosition=new(20,114),LeftMouseReleased=true});
        check(value=="a"&&changes.Count==before,"Escape cancels an active radio pointer selection");
        Step(idle with{MousePosition=new(20,114),LeftMouseDown=true,LeftMousePressed=true});
        check(value=="a","radio pointer press preserves selection until release");
        widget.Label="Renamed##group";
        Step(idle with{MousePosition=new(20,114),LeftMouseReleased=true});
        check(value=="c","generated group input identity survives a displayed heading change during a press");
        Step(idle with{MousePosition=new(20,146),LeftMouseDown=true,LeftMousePressed=true});
        widget.SetItems(new UiRadioOption[]{options[3],options[2],options[1],options[0]});
        Step(idle with{MousePosition=new(20,50),LeftMouseReleased=true});
        check(value=="d","reordering keyed radio options preserves an active option's identity at its new location");
        before=changes.Count;
        Step(idle with{MousePosition=new(20,50),LeftMouseDown=true,LeftMousePressed=true});
        widget.SetItems(new UiRadioOption[]{options[2],options[0]});
        Step(idle with{MousePosition=new(20,50),LeftMouseReleased=true});
        check(value=="d"&&changes.Count==before,"removing a pressed option cancels activation rather than choosing its replacement row");
        disabled=true;Click(50);check(changes.Count==before,"disabled RadioGroup rejects every option click");disabled=false;
        Step(idle with{MousePosition=new(20,50),LeftMouseDown=true,LeftMousePressed=true});hidden=true;
        Step(idle with{LeftMouseDown=true});
        check(context.State.ActiveId is null,"removing an active RadioGroup cancels the shared capture and owned focus");
        hidden=false;Step(idle with{MousePosition=new(20,50),LeftMouseReleased=true});
        check(changes.Count==before,"returning a removed RadioGroup does not replay an abandoned pointer release");
        var rejected=false;try{widget.SetItems(new UiRadioOption[]{new("x","One"),new("x","Duplicate")});}catch(ArgumentException){rejected=true;}
        check(rejected&&widget.Items.Length==2,"invalid duplicate options fail before replacing the existing group");
        options[0]=new("changed","Changed by caller");
        check(widget.Items[1].Value=="a","RadioGroup owns option snapshots independently of the caller array");
        using var content=new UiRadioGroupContent<UiFluentRadioGroupPresentation<UiRadioVerticalLayout>>(
            new UiRadioOption[]{new("x","A long label with several words that must wrap without an ellipsis",Description:"Additional context belongs to this choice.")});
        var directWidth=180f;string? selected="x";
        void Direct(UiImmediateContext ui){ui.SetCursorScreenPos(new(10,10));ui.RadioGroup<UiFluentRadioGroupBehavior,UiFluentRadioGroupPresentation<UiRadioVerticalLayout>>("Heading",content,ref selected,directWidth);}
        using(var data=Frame(render:Direct)){}
        var builds=content.LayoutBuildCount;var height=content.Layout.Size.Y;
        using(var data=Frame(render:Direct)){}
        check(content.LayoutBuildCount==builds,"idle RadioGroup reuses wrapped labels and measured layout in the same font context");
        directWidth=100;using(var data=Frame(render:Direct)){}
        check(content.LayoutBuildCount==builds+1&&content.Layout.Size.Y>height&&content.Layouts[0].Label.Contains('\n')&&!content.Layouts[0].Label.Contains('…'),
            "narrower RadioGroup invalidates layout and wraps complete label text without ellipsis");
        check(content.DerivedTextCharacters<=4096,"RadioGroup bounds additional retained wrapped strings independently of source options");
        check(content.Layouts[0].LabelBlock.LineCount>1&&content.Layouts[0].LabelBlock.Size.Y==content.Layouts[0].LabelBlock.LineCount*20,
            "wrapped Radio labels retain individually measured20px lines instead of centering a multiline string as one line");
        using(var emptyValue=new UiRadioGroupContent<UiFluentRadioGroupPresentation<UiRadioVerticalLayout>>(
            new UiRadioOption[]{new("","Explicit empty value")}))
            check(emptyValue.Items[0].Value=="","explicit empty option values remain distinct from absent nullable selection");
        builds=content.LayoutBuildCount;
        var fonts=Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        context.SetTypographyFonts(new(Path.Combine(fonts,"seguisb.ttf"),Path.Combine(fonts,"seguisb.ttf"),Path.Combine(fonts,"segoeuib.ttf")));
        using(var data=Frame(render:Direct)){}
        check(content.LayoutBuildCount==builds+1,"changing the actual regular font invalidates retained RadioGroup layout");
        ControlFontFixture.Configure(context);
        using var other=new UiContext(new(1,1,UiTextureFormat.Rgba8Unorm,[255,255,255,255]),new(1),new(2));
        ControlFontFixture.Configure(other);builds=content.LayoutBuildCount;
        other.NewFrame(new(1f/60,new(320,240),new(1,1)),idle,new(0,0,320,240),UiTextSettings.Default);
        other.Render(new Screen(Direct));other.GetDrawData().ReleasePooled();
        check(content.LayoutBuildCount==builds+1,"another UI context cannot reuse RadioGroup layout merely because numeric generations match");
        using var largeContent=new UiRadioGroupContent<UiFluentRadioGroupPresentation<UiRadioVerticalLayout>>(
            new UiRadioOption[]{new("large",string.Join(' ',Enumerable.Repeat("label",1500)))});
        using(var data=Frame(render:ui=>{ui.SetCursorScreenPos(new(10,10));ui.RadioGroup<UiFluentRadioGroupBehavior,UiFluentRadioGroupPresentation<UiRadioVerticalLayout>>("Large",largeContent,ref selected,100);})){}
        check(largeContent.DerivedTextCharacters==0&&largeContent.Layouts[0].Label is null,"oversized wrapped text remains correct for the frame without being retained in the layout cache");
        using var emptyLines=new UiRadioGroupContent<UiFluentRadioGroupPresentation<UiRadioVerticalLayout>>(
            new UiRadioOption[]{new("empty-lines",new string('\n',300))});
        using(var data=Frame(render:ui=>ui.RadioGroup<UiFluentRadioGroupBehavior,UiFluentRadioGroupPresentation<UiRadioVerticalLayout>>("Empty lines",emptyLines,ref selected,100))){}
        check(emptyLines.Layouts[0].Label is null,"RadioGroup does not retain an unbounded array of empty text lines with zero character count");
        var eventState=new UiRadioGroupInputState();eventState.Begin("first");
        for(var i=0;i<300;i++)eventState.Select(i%2==0?"second":"first");
        for(var i=0;i<121;i++){eventState.Begin("first");eventState.Select("second");}
        check(eventState.RetainedChangeCapacity<=16,"RadioGroup releases burst-sized event capacity after sustained small input frames");
        using var failureContent=new UiRadioGroupContent<FailingRadioPresentation>(new UiRadioOption[]{new("test","A label which wraps into multiple lines")});
        var failureWidth=180f;
        void Failing(UiImmediateContext ui){ui.SetCursorScreenPos(new(10,10));ui.RadioGroup<UiFluentRadioGroupBehavior,FailingRadioPresentation>("Failure",failureContent,ref selected,failureWidth);}
        using(var data=Frame(render:Failing)){}
        builds=failureContent.LayoutBuildCount;FailingRadioPresentation.Fail=true;failureWidth=100;var failed=false;
        try{using var data=Frame(render:Failing);}catch(InvalidOperationException error)when(error.Message=="Expected measurement failure"){failed=true;}
        finally{FailingRadioPresentation.Fail=false;}
        check(failed&&failureContent.Layouts[0].Label is null,"failed user layout clears partially written cached rows while preserving the exception");
        failureWidth=180;using(var data=Frame(render:Failing)){}
        check(failureContent.LayoutBuildCount==builds+1,"a failed layout cannot reuse an earlier cache key with partially changed rows");
        widget.Dispose();var disposed=false;try{_ = widget.Value;}catch(ObjectDisposedException){disposed=true;}
        check(disposed,"disposed RadioGroup releases its binding and rejects later value access");
        check(Fluent.ComponentNames.Contains("RadioGroup")&&!Fluent.MissingComponentNames.Contains("RadioGroup"),"generated official component catalog and actual RadioGroup factory agree");
    }
    private sealed class InheritedGroup(UiBinding<string?> value,UiRadioOption[] items):UiFluentRadioGroup("Mode##group",value,items,180)
    {
        internal int Changes;
        protected override void OnChanged(string value){Changes++;base.OnChanged(value);}
    }
    private sealed class Screen(Action<UiImmediateContext> render):UiScreen
    {public override void Render(UiImmediateContext ui)=>render(ui);}
}

internal readonly struct FailingRadioPresentation:IUiRadioGroupPresentation
{
    internal static bool Fail;
    public static bool ReuseLayout=>true;
    public static UiControlTypographyScope BeginText(UiControlContext context)=>context.BeginTypography<UiRegularTypeface,UiFluentType300>();
    public static UiRadioGroupLayout Measure(UiControlContext context,string label,ReadOnlySpan<UiRadioOption> items,Span<UiRadioOptionLayout> layouts,float width)
    {
        var result=UiFluentRadioGroupPresentation<UiRadioVerticalLayout>.Measure(context,label,items,layouts,width);
        if(Fail)throw new InvalidOperationException("Expected measurement failure");return result;
    }
    public static void PaintLabel(UiControlContext context,UiRadioGroupLayout layout,UiRect bounds)=>UiFluentRadioGroupPresentation<UiRadioVerticalLayout>.PaintLabel(context,layout,bounds);
    public static void PaintOption(UiControlContext context,UiRadioOptionLayout layout,bool selected,UiButtonInteraction interaction)=>UiFluentRadioGroupPresentation<UiRadioVerticalLayout>.PaintOption(context,layout,selected,interaction);
}
