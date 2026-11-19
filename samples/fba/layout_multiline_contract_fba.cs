// FBA: compiled multiline behavior, presentation and owned editing lifetime.
#:property TargetFramework=net11.0-windows7.0
#:property OutputType=WinExe
#:property OptimizationPreference=Speed
#:property platform=windows
#:package Duxel.$(platform).App@*-*

using Duxel.Core;
using Duxel.App;
using Duxel.Windows.App;

var value = new UiState<string>("First line\nSecond line\nThird line");
var editor = new CompiledNotes("Notes", value.Binding, 4096, 360, 140);
var allocation = Dux.SizedBox(editor, 360, 160);
var width = Fluent.Button("Width : 360");
width.Activated = () =>
{
    var next = allocation.Options.Width == 360 ? 180 : 360;
    allocation.Options = allocation.Options with { Width = next };
    width.Label = $"Width : {next}";
};
var content = Fluent.Button("Add lines");
content.Activated = () => value.Value = string.Join('\n', Enumerable.Range(1, 20)
    .Select(index => $"Line {index} : " + new string('w', 40)));
var clear = Fluent.Button("Clear", () => value.Value = string.Empty);
using var page = Dux.Padding(new UiColumn(
[
    Fluent.Title2("Compiled multiline input"),
    Fluent.Body1("Edit several lines; Ctrl+Z keeps history as the field size changes."),
    allocation,
    Dux.Row(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min), width, content, clear),
    Fluent.Caption1("Line height : 20 / explicit newlines")
], new(Spacing : 16, MainAxisSize : UiMainAxisSize.Min)), new(24));
var exercise = Environment.GetEnvironmentVariable("DUXEL_SAMPLE_EXERCISE") == "1";
var previousStep = -1;
var options = DuxelApp.Options<MultilineDesign>(Dux.App(Dux.View(ui =>
{
    if (exercise)
    {
        var step = (int)(ui.GetTime() * 2) % 4;
        if (step != previousStep)
        {
            previousStep = step;
            width.Activated?.Invoke();
            if (step == 0)
                content.Activated?.Invoke();
            else if (step == 2)
                clear.Activated?.Invoke();
            Console.WriteLine($"MultilineExercise : {step}");
        }
        ui.RequestFrameAt(ui.GetTime() + 0.1);
    }
    page.Render(ui);
})), title : "Duxel / Compiled Multiline Contract", width : 820, height : 540);
DuxelWindowsApp.Run(options with
{
    Window = options.Window with { MinWidth = 760, MinHeight = 480 },
    Debug = new() { LogStartupTimings = true }
});

public readonly struct MultilineDesign : IUiDesign
{
    public static UiCompiledDesign Create() => UiCompiledDesign.Windows11Dark;
}

public readonly struct NotesPresentation : IUiMultilineInputPresentation
{
    public static UiControlTypographyScope BeginText(UiControlContext context) =>
        context.BeginTypography<UiRegularTypeface, UiFluentType300>();
    public static UiMultilineInputLayout Measure(UiControlContext context, float width, float height) =>
        Layout(width > 0 ? width : 180, height > 0 ? height : 90, context.TypographyLineHeight);
    public static UiMultilineInputLayout Arrange(UiControlContext context, in UiMultilineInputLayout natural, UiVector2 size) =>
        Layout(size.X, size.Y, natural.LineHeight);
    private static UiMultilineInputLayout Layout(float width, float height, float lineHeight) =>
        new(new(0, 0, width, height), lineHeight)
        {
            Content = new(8, 6, MathF.Max(0, width - 16), MathF.Max(0, height - 12))
        };
    public static void Paint(UiControlContext context, in UiMultilineInputLayout layout, in UiMultilineEditResult edit)
    {
        context.Frame(layout.Field, new(30, 50, 80), new(100, 160, 210), 4, 1);
        context.PaintEditableMultiline(layout, edit,
            new(new(240, 230, 180), new(80, 120, 180), new(255, 255, 255)) { Typography = true });
    }
}

[UiWidgetStyle(typeof(UiMultilineInput<,>), typeof(UiStandardMultilineInputBehavior), typeof(NotesPresentation))]
public partial class CompiledNotes;
