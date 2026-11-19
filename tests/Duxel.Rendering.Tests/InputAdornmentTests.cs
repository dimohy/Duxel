using Duxel.Core;

internal static class InputAdornmentTests
{
    internal static void Run(Action<bool,string> check)
    {
        using var context=new UiContext(new(1,1,UiTextureFormat.Rgba8Unorm,[255,255,255,255]),new(1),new(2));ControlFontFixture.Configure(context);
        var idle=new UiInputState(new(-100,-100),false,false,false,false,false,false,0,0,[],[],default,default);
        var value="123";var writes=0;
        using var field=new SlottedTestInput("Amount",new(()=>value,next=>{value=next;writes++;}),64,200);
        field.Options=new(){ContentBefore="USD",ContentAfter="kg"};
        void Step(UiInputState? input=null,Action<UiImmediateContext>? draw=null)
        {
            context.NewFrame(new(1f/60,new(320,240),new(1,1)),input??idle,new(0,0,320,240),UiTextSettings.Default);
            context.Render(new Screen(draw??(ui=>{ui.SetCursorScreenPos(new(10,10));field.Render(ui);})));using var data=context.GetDrawData();
        }
        Step();var layout=AdornmentProbe.Last;
        check(layout.Before is not null&&layout.After is not null&&layout.Editing is not null&&!layout.HasBeforeChild&&!layout.HasAfterChild,"default generated Input measures before/after text separately from editable content");
        var before=layout.Before!.Value;var after=layout.After!.Value;
        check(before.X==21&&MathF.Abs(layout.Content.X-(before.X+before.Width+4))<.001f&&MathF.Abs(after.X+after.Width-199)<.001f,
            "medium text adornments use pinned root10/input2/gap2 padding plus border1");
        check(layout.EditingBounds.X>before.X+before.Width&&layout.EditingBounds.X+layout.EditingBounds.Width<after.X,
            "text input hit region excludes both presentation adornments");
        Step(idle with{MousePosition=new(before.X+1,20),LeftMouseDown=true,LeftMousePressed=true});
        check(context.State.ActiveId is null,"noninteractive prefix does not masquerade as an editable input");
        Step();Step(idle with{MousePosition=new(layout.Content.X+2,20),LeftMouseDown=true,LeftMousePressed=true});Step();
        var id=context.State.ActiveId;check(id is not null,"clicking between adornments focuses the shared editor");
        Step(idle with{KeyEvents=[new(UiKey.End,true,default)]});Step(idle with{KeyEvents=[new(UiKey.End,false,default)]});Step(idle with{CharEvents=[new('4')]});
        check(value=="1234"&&writes==1,"adornments never enter the controlled value or duplicate its callback");
        field.Options=field.Options with{ContentBefore="EUR",ContentAfter="m"};Step();
        check(context.State.ActiveId==id&&value=="1234"&&writes==1,"changing adornment content preserves editing identity and value");
        after=AdornmentProbe.Last.After!.Value;
        Step(idle with{MousePosition=new(after.X+1,20),LeftMouseDown=true,LeftMousePressed=true});
        check(context.State.ActiveId is null,"clicking a noninteractive suffix blurs the text input");
        foreach(var width in new[]{1f,50f,400f,1f,200f}){field.Width=width;Step();var bounds=AdornmentProbe.Last.EditingBounds;check(bounds.Width>=0&&bounds.X>=10&&bounds.X+bounds.Width<=10+width+.001f,"adorned editor remains inside its root when resized to "+width);}
        var rejected=false;field.Options=new(){ContentBefore="two\nlines"};
        try{Step();}catch(ArgumentException){rejected=true;}
        check(rejected,"single-line text adornments reject unsupported line breaks before editing");
        field.Options=default;field.Width=200;Step();check(AdornmentProbe.Last.Editing is null&&AdornmentProbe.Last.Before is null&&AdornmentProbe.Last.After is null,"removing text adornments restores the original no-slot layout");
        CheckSize<UiFluentSmallInputSize>(7,25,27,177,146);
        CheckSize<UiFluentMediumInputSize>(11,33,35,169,130);
        CheckSize<UiFluentLargeInputSize>(13,43,49,163,102);
        void CheckSize<TSize>(float beforeX,float editX,float textX,float afterX,float textWidth) where TSize:IUiInputSize
        {
            using var owner=new UiTextInputOwner();
            Step(draw:ui=>
            {
                var text="";
                ui.SetCursorScreenPos(default);
                ui.InputText<AdornmentProbe,UiFluentInputPresentation<TSize,UiFluentOutlineInput,SizedAdornment,SizedAdornment>>("Sized",ref text,64,new UiInputOptions(),200,owner);
            });
            var actual=AdornmentProbe.Last;
            check(actual.Before!.Value.X==beforeX&&actual.EditingBounds.X==editX&&actual.Content.X==textX&&actual.After!.Value.X==afterX&&actual.Content.Width==textWidth,
                "independent pinned icon-slot geometry: "+typeof(TSize).Name);
        }
        using(var owner=new UiTextInputOwner())
        {
            Step(draw:ui=>{var text="";ui.SetCursorScreenPos(default);ui.InputText<AdornmentProbe,UiFluentInputPresentation<UiFluentMediumInputSize,UiFluentUnderlineInput,SizedAdornment,SizedAdornment>>("Underline",ref text,64,new UiInputOptions(),200,owner);});
            check(AdornmentProbe.Last.Before!.Value.Y==5.5f&&AdornmentProbe.Last.After!.Value.Y==5.5f&&AdornmentProbe.Last.Content.Y==5.5f,"underline adornments align to the content area above its one-pixel bottom border");
        }
        field.Dispose();check(field.Options.ContentBefore is null&&field.Options.ContentAfter is null&&context.State.TextInputStorage==(0,0),"disposing slotted Input releases option content and editing storage");
    }
    private sealed class Screen(Action<UiImmediateContext> draw):UiScreen{public override void Render(UiImmediateContext ui)=>draw(ui);}
}
internal readonly struct AdornmentProbe:IUiTextInputBehavior
{
    internal static UiInputLayout Last;
    public static UiInputEditResult Process(UiControlContext context,string id,ref string value,int maxLength,in UiInputLayout layout)
    {Last=layout;return UiFluentTextInputBehavior.Process(context,id,ref value,maxLength,layout);}
}
internal readonly struct SizedAdornment:IUiInputAdornment
{
    public static bool IsPresent(string? content)=>true;
    public static UiVector2 Measure(UiControlContext context,string? content,float iconSize)=>new(iconSize,iconSize);
    public static void Paint(UiControlContext context,string? content,UiRect bounds,UiColor color,in UiInputEditResult edit)=>context.Frame(bounds,color,new(0,0,0,0),0,0);
}
[UiWidgetStyle(typeof(UiInput<,>),typeof(AdornmentProbe),typeof(UiFluentInputPresentation<UiFluentMediumInputSize,UiFluentOutlineInput>))]
internal partial class SlottedTestInput;
