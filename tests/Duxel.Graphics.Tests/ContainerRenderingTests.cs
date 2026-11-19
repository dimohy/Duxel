using Duxel.Core;

internal static class ContainerRenderingTests
{
    internal static void Run(Func<string,Action<UiImmediateContext>,byte[]> capture,Action<bool,string> check)
    {
        SingleChildRenderingTests.Run(capture,check);
        ContainerContractRenderingTests.Run(capture, check);
        var retainedFirst=Fluent.Button("A");var retainedSecond=Fluent.Button("B");
        using var retainedButtons=Dux.Wrap(new(Spacing:8),retainedFirst,retainedSecond);
        byte[] ReferenceButtons(string name)=>capture(name,ui=>
        {
            Root(ui);ui.Button<UiReleaseButtonBehavior,UiFluentButtonPresentation>(retainedFirst.Label);
            var first=ui.GetItemRectSize();ui.SetCursorScreenPos(new(first.X+8,0));
            ui.Button<UiReleaseButtonBehavior,UiFluentButtonPresentation>(retainedSecond.Label);
        });
        var retainedImage=capture("retained-buttons",ui=>{Root(ui);retainedButtons.Render(ui);});
        var retainedReference=ReferenceButtons("retained-buttons-reference");
        check(retainedImage.AsSpan().SequenceEqual(retainedReference),"retained Fluent button arrangement matches independent immediate button layout");
        var retainedReplay=true;
        for(var i=0;i<4;i++)retainedReplay&=retainedReference.AsSpan().SequenceEqual(capture("retained-buttons-replay-"+i,ui=>{Root(ui);retainedButtons.Render(ui);}));
        check(retainedReplay,"retained Fluent text and geometry survive acquired-image reuse without skipping actual rendering");
        retainedFirst.Label="Longer first label";
        retainedImage=capture("retained-label-changed",ui=>{Root(ui);retainedButtons.Render(ui);});
        retainedReference=ReferenceButtons("retained-label-changed-reference");
        check(retainedImage.AsSpan().SequenceEqual(retainedReference),"changing retained button text remeasures ancestor layout and preserves exact GPU glyph output");
        using var wrap=Dux.Wrap(new(Spacing:10,RunSpacing:7,CrossAlignment:UiWrapCrossAlignment.End),
            new Block(new(130,20),new(200,40,30)),new Block(new(100,40),new(0,160,50)),new Block(new(80,10),new(0,120,220)));
        using var wrapBox=Dux.Container(wrap,new(Width:300,Height:180));
        var wrapImage=capture("wrap-runs",ui=>{Root(ui);wrapBox.Render(ui);});
        var wrapReference=capture("wrap-runs-reference",ui=>
        {Root(ui);Paint(ui,new(0,20,130,20),new(200,40,30));Paint(ui,new(140,0,100,40),new(0,160,50));Paint(ui,new(0,47,80,10),new(0,120,220));});
        check(wrapImage.AsSpan().SequenceEqual(wrapReference),"Wrap run spacing and cross alignment match independent GPU rectangles");
        wrap.Options=wrap.Options with{Alignment=UiMainAxisAlignment.End,RunSpacing=9};
        var wrapUpdated=capture("wrap-live-update",ui=>{Root(ui);wrapBox.Render(ui);});
        var wrapUpdatedReference=capture("wrap-live-update-reference",ui=>
        {Root(ui);Paint(ui,new(0,20,130,20),new(200,40,30));Paint(ui,new(140,0,100,40),new(0,160,50));Paint(ui,new(160,49,80,10),new(0,120,220));});
        check(wrapUpdated.AsSpan().SequenceEqual(wrapUpdatedReference),"live Wrap alignment and run-spacing changes preserve the exact independent GPU layout");
        using var grid=Dux.UniformGrid(new(3,CrossAxisSpacing:6,MainAxisSpacing:8,ChildAspectRatio:2),
            new Block(new(1,1),new(200,40,30)),new Block(new(1,1),new(0,160,50)),new Block(new(1,1),new(0,120,220)),new Block(new(1,1),new(200,120,20)));
        using var gridBox=Dux.Container(grid,new(Width:300,Height:180));
        var gridImage=capture("grid-aspect",ui=>{Root(ui);gridBox.Render(ui);});
        var gridReference=capture("grid-aspect-reference",ui=>
        {Root(ui);Paint(ui,new(0,0,96,48),new(200,40,30));Paint(ui,new(102,0,96,48),new(0,160,50));Paint(ui,new(204,0,96,48),new(0,120,220));Paint(ui,new(0,56,96,48),new(200,120,20));});
        check(gridImage.AsSpan().SequenceEqual(gridReference),"Grid aspect ratio, gaps and partial final row match independent GPU cells");
        grid.Options=new(2,CrossAxisSpacing:10,MainAxisSpacing:8,MainAxisExtent:32);
        var gridUpdated=capture("grid-live-count",ui=>{Root(ui);gridBox.Render(ui);});
        var gridUpdatedReference=capture("grid-live-count-reference",ui=>
        {Root(ui);Paint(ui,new(0,0,145,32),new(200,40,30));Paint(ui,new(155,0,145,32),new(0,160,50));Paint(ui,new(0,40,145,32),new(0,120,220));Paint(ui,new(155,40,145,32),new(200,120,20));});
        check(gridUpdated.AsSpan().SequenceEqual(gridUpdatedReference),"live Grid count and explicit extent preserve the exact independent GPU cells");
        var flowReplay=true;
        for(var i=0;i<4;i++)
        {
            flowReplay&=wrapUpdatedReference.AsSpan().SequenceEqual(capture("wrap-update-replay-"+i,ui=>{Root(ui);wrapBox.Render(ui);}));
            flowReplay&=gridUpdatedReference.AsSpan().SequenceEqual(capture("grid-update-replay-"+i,ui=>{Root(ui);gridBox.Render(ui);}));
        }
        check(flowReplay,"updated Wrap and Grid geometry remains correct across all acquired frame images");
        using var verticalWrap=Dux.VerticalWrap(new(Spacing:10,RunSpacing:5,CrossAlignment:UiWrapCrossAlignment.End),
            new Block(new(10,100),new(200,40,30)),new Block(new(30,70),new(0,160,50)),new Block(new(20,40),new(0,120,220)));
        using var verticalBox=Dux.Container(verticalWrap,new(Width:300,Height:180));
        var verticalImage=capture("wrap-vertical",ui=>{Root(ui);verticalBox.Render(ui);});
        var verticalReference=capture("wrap-vertical-reference",ui=>
        {Root(ui);Paint(ui,new(20,0,10,100),new(200,40,30));Paint(ui,new(0,110,30,70),new(0,160,50));Paint(ui,new(35,0,20,40),new(0,120,220));});
        check(verticalImage.AsSpan().SequenceEqual(verticalReference),"generated vertical Wrap matches independent GPU column placement");
        using var horizontalGrid=Dux.HorizontalGrid(new(2,CrossAxisSpacing:4,MainAxisSpacing:6,ChildAspectRatio:2),
            new Block(new(1,1),new(200,40,30)),new Block(new(1,1),new(0,160,50)),new Block(new(1,1),new(0,120,220)));
        using var horizontalBox=Dux.Container(horizontalGrid,new(Width:300,Height:180));
        var horizontalImage=capture("grid-horizontal",ui=>{Root(ui);horizontalBox.Render(ui);});
        var horizontalReference=capture("grid-horizontal-reference",ui=>
        {Root(ui);Paint(ui,new(0,0,44,88),new(200,40,30));Paint(ui,new(0,92,44,88),new(0,160,50));Paint(ui,new(50,0,44,88),new(0,120,220));});
        check(horizontalImage.AsSpan().SequenceEqual(horizontalReference),"generated horizontal Grid matches independent GPU cell placement");
        using var stack=Dux.Stack(new(Alignment:UiAlignment.Center),new Block(new(100,60),new(200,40,30)),
            Dux.Positioned(new Block(new(1,1),new(0,160,50)),new(Left:10,Right:20,Top:5,Bottom:15)),
            new Block(new(20,10),new(0,120,220)));
        var stackImage=capture("stack-anchors",ui=>{Root(ui);stack.Render(ui);});
        var stackReference=capture("stack-anchors-reference",ui=>
        {Root(ui);Paint(ui,new(0,0,100,60),new(200,40,30));Paint(ui,new(10,5,70,40),new(0,160,50));Paint(ui,new(40,25,20,10),new(0,120,220));});
        check(stackImage.AsSpan().SequenceEqual(stackReference),"Stack anchors, alignment and insertion paint order match independent GPU rectangles");
        ((UiPositioned)stack.Children[1]).Options=new(Left:-10,Top:50,Width:120,Height:30);
        var overflowImage=capture("stack-clipped-update",ui=>{Root(ui);stack.Render(ui);});
        var overflowReference=capture("stack-clipped-update-reference",ui=>
        {Root(ui);Paint(ui,new(0,0,100,60),new(200,40,30));Paint(ui,new(0,50,100,10),new(0,160,50));Paint(ui,new(40,25,20,10),new(0,120,220));});
        check(overflowImage.AsSpan().SequenceEqual(overflowReference),"live Positioned updates clip negative offsets and overflow exactly to Stack bounds");
        var stackReplay=true;
        for(var i=0;i<4;i++)stackReplay&=overflowReference.AsSpan().SequenceEqual(capture("stack-update-replay-"+i,ui=>{Root(ui);stack.Render(ui);}));
        check(stackReplay,"updated Stack geometry stays correct across all acquired frame images");
        using var weighted=Dux.Row(new(Spacing:10,CrossAxisAlignment:UiCrossAxisAlignment.Stretch),
            new Block(new(40,10),new(200,40,30)),Dux.Expanded(new Block(new(5,20),new(0,160,50))),Dux.Expanded(new Block(new(5,30),new(0,120,220)),2));
        var weightedImage=capture("flex-weighted",ui=>{Root(ui);weighted.Render(ui);});
        var weightedReference=capture("flex-weighted-reference",ui=>
        {
            Root(ui);var available=ui.GetContentRegionAvail();var first=(available.X-60)/3;
            Paint(ui,new(0,0,40,available.Y),new(200,40,30));Paint(ui,new(50,0,first,available.Y),new(0,160,50));
            Paint(ui,new(60+first,0,available.X-60-first,available.Y),new(0,120,220));
        });
        check(weightedImage.AsSpan().SequenceEqual(weightedReference),"weighted Expanded and cross-axis stretch match independently allocated GPU rectangles");
        ((UiExpanded)weighted.Children[1]).Flex=2;
        var updatedImage=capture("flex-weight-updated",ui=>{Root(ui);weighted.Render(ui);});
        var updatedReference=capture("flex-weight-updated-reference",ui=>
        {
            Root(ui);var available=ui.GetContentRegionAvail();var half=(available.X-60)*.5f;
            Paint(ui,new(0,0,40,available.Y),new(200,40,30));Paint(ui,new(50,0,half,available.Y),new(0,160,50));
            Paint(ui,new(60+half,0,half,available.Y),new(0,120,220));
        });
        check(updatedImage.AsSpan().SequenceEqual(updatedReference),"a live flex weight update preserves the exact independent GPU layout");
        var replay=true;
        for(var i=0;i<4;i++)replay&=updatedReference.AsSpan().SequenceEqual(capture("flex-weight-replay-"+i,ui=>{Root(ui);weighted.Render(ui);}));
        check(replay,"updated weighted geometry remains correct across all acquired frame images");
        using var spacerRow=Dux.Row(default,Dux.Expanded(new Block(new(1,10),new(0,120,220))),Dux.FlexSpacer(2),Dux.Flexible(new Block(new(20,10),new(200,40,30))));
        var spacerImage=capture("flex-spacer-loose",ui=>{Root(ui);spacerRow.Render(ui);});
        var spacerReference=capture("flex-spacer-loose-reference",ui=>
        {Root(ui);var quarter=ui.GetContentRegionAvail().X/4;Paint(ui,new(0,0,quarter,10),new(0,120,220));Paint(ui,new(quarter*3,0,20,10),new(200,40,30));});
        check(spacerImage.AsSpan().SequenceEqual(spacerReference),"Spacer paints no geometry and loose Flexible preserves unused space exactly");
        using var expandedButton=Dux.Row(default,Dux.Expanded(Fluent.Button("Wide")));
        var buttonImage=capture("flex-button",ui=>{Root(ui);expandedButton.Render(ui);});
        var buttonReference=capture("flex-button-reference",ui=>{Root(ui);ui.Button<UiReleaseButtonBehavior,UiFluentButtonPresentation>("Wide",new(ui.GetContentRegionAvail().X,32));});
        check(buttonImage.AsSpan().SequenceEqual(buttonReference),"generated Expanded paints the same full-width Fluent button as an explicit allocation");
        using var box=new UiContainer(new Block(new(20,10),new(0,120,220)),new(Width:100,Height:60,Alignment:UiAlignment.Center));
        var actual=capture("container-center",ui=>{Root(ui);box.Render(ui);});
        var reference=capture("container-center-reference",ui=>{Root(ui);Paint(ui,new(40,25,20,10),new(0,120,220));});
        check(actual.AsSpan().SequenceEqual(reference),"generated Container positions the real GPU child exactly at the measured center");
        var boxReplay=true;
        for(var i=0;i<4;i++)boxReplay&=reference.AsSpan().SequenceEqual(capture("container-center-replay-"+i,ui=>{Root(ui);box.Render(ui);}));
        check(boxReplay,"retained Container child geometry stays correct across acquired frame images");
        box.Options=box.Options with{Width=140,Padding=new(10),Alignment=UiAlignment.BottomRight};
        actual=capture("container-live-options",ui=>{Root(ui);box.Render(ui);});
        reference=capture("container-live-options-reference",ui=>{Root(ui);Paint(ui,new(110,40,20,10),new(0,120,220));});
        check(actual.AsSpan().SequenceEqual(reference),"retained Container width padding and alignment updates match independent GPU geometry");
        using var row=new UiRow([new Block(new(20,10),new(200,40,30)),new Block(new(30,20),new(0,160,50))],
            new(Spacing:5,MainAxisSize:UiMainAxisSize.Min,CrossAxisAlignment:UiCrossAxisAlignment.End));
        actual=capture("container-row",ui=>{Root(ui);row.Render(ui);});
        reference=capture("container-row-reference",ui=>{Root(ui);Paint(ui,new(0,10,20,10),new(200,40,30));Paint(ui,new(25,0,30,20),new(0,160,50));});
        check(actual.AsSpan().SequenceEqual(reference),"generated Row matches the independently placed GPU child rectangles");
        using var text=Fluent.Body1("Container text");using var textBox=new UiContainer(text,new(Padding:new(10)));
        UiVector2 textSize=default;
        actual=capture("container-text",ui=>{Root(ui);textSize=text.MeasureConstrained(ui,UiBoxConstraints.Loose(new(200,100)));textBox.Render(ui);});
        check(textSize.X>0&&textSize.Y==20,"real Fluent typography measures its glyph width and official line height for containers");
        reference=capture("container-text-reference",ui=>
        {
            Root(ui);ui.SetCursorScreenPos(new(10,10));using var clip=new UiControlContext(ui).Clip(new(10,10,textSize.X,textSize.Y));
            ui.Text<UiFluentTypographyPresentation<UiRegularTypeface,UiFluentType300>>("Container text");
        });
        check(actual.AsSpan().SequenceEqual(reference),"Fluent text in a container preserves the direct GPU glyph image and typography");
    }
    private static void Root(UiImmediateContext ui){ui.EnableRootViewportContentLayout(contentPadding:0);ui.SetCursorScreenPos(default);}
    private static void Paint(UiImmediateContext ui,UiRect bounds,UiColor color)
    {using var clip=new UiControlContext(ui).Clip(bounds);ui.GetWindowDrawList().AddRectFilled(bounds,color,ui.WhiteTextureId);}
    private sealed class Block(UiVector2 size,UiColor color):UiControlWidget
    {
        protected override bool CanRetainMeasurement=>true;
        protected override UiVector2 MeasureContent(UiControlContext context)=>size;
        protected override void RenderControl(UiImmediateContext ui)
        {var p=ui.GetCursorScreenPos();var allocated=new UiControlContext(ui).ConstrainToLayout(size);Paint(ui,new(p.X,p.Y,allocated.X,allocated.Y),color);ui.Dummy(allocated);}
    }
}
