// FBA: Link parent allocation, wrapping, clipping and application-owned navigation.
#:property TargetFramework=net11.0-windows7.0
#:property OutputType=WinExe
#:property OptimizationPreference=Speed
#:property platform=windows
#:package Duxel.$(platform).App@*-*

using Duxel.Core;
using Duxel.App;
using Duxel.Windows.App;

using var page = new LinkAllocationPage();
var options = DuxelApp.Options<LinkAllocationDesign>(
    Dux.App(page),
    title : "Duxel / Allocated Link",
    width : 860,
    height : 600);

DuxelWindowsApp.Run(options with
{
    Window = options.Window with { MinWidth = 760, MinHeight = 560 },
    Debug = new() { LogStartupTimings = true }
});

public readonly struct LinkAllocationDesign : IUiDesign
{
    public static UiCompiledDesign Create() => UiCompiledDesign.Windows11Dark;
}

sealed class LinkAllocationPage : UiWidget
{
    protected override IUiView Build(UiBuildContext context) => context.Child("root", () =>
    {
        const string paragraph = "Read the details about allocated links.\nCaptions wrap at the actual parent width.";
        var status = new UiState<string>("No navigation yet");
        Action<Uri> navigate = destination => status.Value = $"Visited : {destination}";
        var first = Fluent.Link("Open details", new("/details", UriKind.Relative), navigate, width : 220);
        var second = Fluent.Link(paragraph, new("/guide", UriKind.Relative), navigate, width : 220);
        var firstBox = Dux.SizedBox(first, 100, 120);
        var secondBox = Dux.SizedBox(second, 100, 120);
        UiSizedBox[] boxes = [firstBox, secondBox];

        var width = Fluent.Button("Width : 100");
        width.Activated = () =>
        {
            var next = firstBox.Options.Width == 100 ? 240 : 100;
            foreach (var box in boxes)
            {
                box.Options = box.Options with { Width = next };
            }
            width.Label = $"Width : {next}";
        };
        var height = Fluent.Button("Height : 120");
        height.Activated = () =>
        {
            var next = firstBox.Options.Height == 120 ? 8 : 120;
            foreach (var box in boxes)
            {
                box.Options = box.Options with { Height = next };
            }
            height.Label = $"Height : {next}";
        };

        return Dux.Padding(
            new UiColumn(
            [
                Fluent.Title2("Allocated Link roots"),
                Fluent.Body1("Click the assigned area or focus a link and press Enter. Navigation updates the status below."),
                Dux.Row(
                    new(Spacing : 24, MainAxisSize : UiMainAxisSize.Min),
                    new UiColumn(
                        [Fluent.Caption1("Short label"), firstBox],
                        new(Spacing : 8, MainAxisSize : UiMainAxisSize.Min)),
                    new UiColumn(
                        [Fluent.Caption1("Wrapped label"), secondBox],
                        new(Spacing : 8, MainAxisSize : UiMainAxisSize.Min))),
                Dux.Row(
                    new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min),
                    width,
                    height,
                    Fluent.Button("Short", () => second.Label = "Open guide"),
                    Fluent.Button("Long", () => second.Label = paragraph)),
                Fluent.Text(() => status.Value)
            ],
            new(Spacing : 16, MainAxisSize : UiMainAxisSize.Min)),
            new(24));
    });
}
