// FBA: statically generated single-child layouts with real Fluent interactions.
#:property TargetFramework=net11.0-windows7.0
#:property OutputType=WinExe
#:property OptimizationPreference=Speed
#:property platform=windows
#:package Duxel.$(platform).App@*-*

using Duxel.Core;
using Duxel.App;
using Duxel.Windows.App;

using var page = new SingleChildPage();
var options = DuxelApp.Options<SingleChildDesign>(Dux.App(page),
    title : "Duxel / Single Child Layouts", width : 820, height : 440);
DuxelWindowsApp.Run(options with
{
    Window = options.Window with { MinWidth = 640, MinHeight = 420 },
    Debug = new() { LogStartupTimings = true }
});

public readonly struct SingleChildDesign : IUiDesign
{
    public static UiCompiledDesign Create() => UiCompiledDesign.Windows11Dark;
}

sealed class SingleChildPage : UiWidget
{
    protected override IUiView Build(UiBuildContext context) => context.Child("root", () =>
    {
        var count = 0;
        var counter = Fluent.Button("Count : 0");
        counter.Size = new(120, 32);
        counter.Activated = () =>
        {
            count++;
            counter.Label = $"Count : {count}";
        };
        var padded = Dux.Padding(Dux.Align(counter, UiAlignment.TopLeft), new(8));
        var inset = Fluent.Button("Inset : 8");
        inset.Size = new(120, 32);
        inset.Activated = () =>
        {
            var next = padded.Options.Left == 8 ? 16 : 8;
            padded.Options = new(next);
            inset.Label = $"Inset : {next}";
        };
        var reset = Fluent.Button("Reset");
        reset.Size = new(120, 32);
        reset.Activated = () =>
        {
            count = 0;
            counter.Label = "Count : 0";
            padded.Options = new(8);
            inset.Label = "Inset : 8";
        };
        var row = Dux.Row(new(Spacing : 16),
            Dux.Expanded(Dux.SizedBox(padded, height : 72)),
            Dux.Expanded(Dux.SizedBox(Dux.Center(inset), height : 72)));
        var constrained = Dux.ConstrainedBox(Dux.Center(reset), new(180, 300, 56, 72));
        var content = new UiColumn(
        [
            Fluent.Title2("Compiled single-child layouts"),
            Fluent.Body1("SizedBox > Padding > Align > Fluent button"),
            Fluent.Body1("Click Count, change Inset, then Reset. Tab / Space also work."),
            row,
            Fluent.Body1("ConstrainedBox > Center > Reset"),
            constrained,
            Fluent.Caption1("Shared ownership, measurement cache and live invalidation.")
        ], new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min));
        return Dux.Padding(content, new(24));
    });
}
