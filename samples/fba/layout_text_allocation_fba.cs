// FBA: actual text root allocations, wrapping, clipping and retained content.
#:property TargetFramework=net11.0-windows7.0
#:property OutputType=WinExe
#:property OptimizationPreference=Speed
#:property platform=windows
#:package Duxel.$(platform).App@*-*

using Duxel.Core;
using Duxel.App;
using Duxel.Windows.App;

using var page = new TextAllocationPage();
var options = DuxelApp.Options<TextAllocationDesign>(
    Dux.App(page),
    title : "Duxel / Allocated Text",
    width : 860,
    height : 600);

DuxelWindowsApp.Run(options with
{
    Window = options.Window with { MinWidth = 760, MinHeight = 560 },
    Debug = new() { LogStartupTimings = true }
});

public readonly struct TextAllocationDesign : IUiDesign
{
    public static UiCompiledDesign Create() => UiCompiledDesign.Windows11Dark;
}

sealed class TextAllocationPage : UiWidget
{
    protected override IUiView Build(UiBuildContext context) => context.Child("root", () =>
    {
        const string paragraph = "The parent controls wrapping and clipping. Font size and line height stay unchanged.\nThe final line remains readable when space allows.";
        var content = new UiState<string>(paragraph);
        var fixedText = Fluent.Text(paragraph);
        var dynamicText = Fluent.Text(() => content.Value);
        var fixedBox = Dux.SizedBox(fixedText, 100, 120);
        var dynamicBox = Dux.SizedBox(dynamicText, 100, 120);
        UiSizedBox[] boxes = [fixedBox, dynamicBox];

        var width = Fluent.Button("Width : 100");
        width.Activated = () =>
        {
            var next = fixedBox.Options.Width == 100 ? 240 : 100;
            foreach (var box in boxes)
            {
                box.Options = box.Options with { Width = next };
            }
            width.Label = $"Width : {next}";
        };
        var height = Fluent.Button("Height : 120");
        height.Activated = () =>
        {
            var next = fixedBox.Options.Height == 120 ? 40 : 120;
            foreach (var box in boxes)
            {
                box.Options = box.Options with { Height = next };
            }
            height.Label = $"Height : {next}";
        };

        return Dux.Padding(
            new UiColumn(
            [
                Fluent.Title2("Allocated text roots"),
                Fluent.Body1("Change the parent width and height. Short or empty text keeps the same assigned area."),
                Dux.Row(
                    new(Spacing : 24, MainAxisSize : UiMainAxisSize.Min),
                    new UiColumn(
                        [Fluent.Caption1("Static content"), fixedBox],
                        new(Spacing : 8, MainAxisSize : UiMainAxisSize.Min)),
                    new UiColumn(
                        [Fluent.Caption1("Getter content"), dynamicBox],
                        new(Spacing : 8, MainAxisSize : UiMainAxisSize.Min))),
                Dux.Row(
                    new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min),
                    width,
                    height),
                Dux.Row(
                    new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min),
                    Fluent.Button("Short", () => content.Value = "Short text"),
                    Fluent.Button("Empty", () => content.Value = string.Empty),
                    Fluent.Button("Reset", () => content.Value = paragraph))
            ],
            new(Spacing : 16, MainAxisSize : UiMainAxisSize.Min)),
            new(24));
    });
}
