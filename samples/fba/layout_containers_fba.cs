// FBA: generated layout containers with the same ownership, input and Fluent widgets.
#:property TargetFramework=net11.0-windows7.0
#:property OutputType=WinExe
#:property OptimizationPreference=Speed
#:property platform=windows
#:package Duxel.$(platform).App@*-*

using Duxel.Core;
using Duxel.App;
using Duxel.Windows.App;

using var page = new ContainerPage();
var options = DuxelApp.Options<ContainerDesign>(Dux.App(page),
    title : "Duxel / Layout Containers / Stack and Flex", width : 760, height : 460);
DuxelWindowsApp.Run(options with
{
    Window = options.Window with { MinWidth = 480, MinHeight = 400 },
    Debug = new() { LogStartupTimings = true }
});

public readonly struct ContainerDesign : IUiDesign
{
    public static UiCompiledDesign Create() => UiCompiledDesign.Windows11Dark;
}

sealed class ContainerPage : UiWidget
{
    private int _count;
    private UiFluentButton? _increment;
    protected override IUiView Build(UiBuildContext context) => context.Child("root", () =>
    {
        _increment = Fluent.Button("Count : 0", () =>
        {
            _count++;
            _increment!.Label = $"Count : {_count}";
        });
        _increment.Size = new(120, 32);
        var reset = Fluent.Button("Reset", () =>
        {
            _count = 0;
            _increment.Label = "Count : 0";
        });
        reset.Size = new(100, 32);
        var actions = new UiRow(
            [Dux.Expanded(_increment), Dux.FlexSpacer(), Dux.Expanded(reset, 2)],
            new(Spacing : 12, CrossAxisAlignment : UiCrossAxisAlignment.Center));
        var status = Fluent.Button("Move overlay");
        status.Size = new(140, 32);
        var overlay = Dux.Positioned(status, new(Right : 0, Top : 12, Width : 140, Height : 32));
        var moved = false;
        status.Activated = () =>
        {
            moved = !moved;
            overlay.Options = new(Right : moved ? 170 : 0, Top : 12, Width : 140, Height : 32);
        };
        var layered = Dux.Stack(new(Fit : UiStackFit.Expand),
            Fluent.Body1("Stack : click the overlay to move it."), overlay);
        var content = new UiColumn(
        [
            Fluent.Title2("Compiled layout containers"),
            Fluent.Body1("Expanded 1 : Spacer 1 : Expanded 2"),
            Fluent.Body1("Click anywhere on a button, or use Tab and Space."),
            actions,
            Dux.Container(layered, new(Height : 64)),
            Fluent.Caption1("Shared UiWidget lifetime; no internal Window or Panel.")
        ], new(Spacing : 16, MainAxisSize : UiMainAxisSize.Min));
        return new DemoContainer(content, new(Padding : new(24), Margin : new(12)));
    });
}

public readonly struct DemoContainerSurface : IUiContainerPresentation
{
    public static void Paint(UiControlContext context, UiRect bounds) =>
        context.Frame(bounds, context.Theme.WindowBg, context.Theme.Border, 8, 1);
}
[UiWidgetStyle(typeof(UiContainerBox<,>), typeof(UiBoxContainerLayout), typeof(DemoContainerSurface))]
partial class DemoContainer;
