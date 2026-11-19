// FBA: Fluent multiline editing, appearances and state options.
#:property TargetFramework=net11.0-windows7.0
#:property OutputType=WinExe
#:property OptimizationPreference=Speed
#:property platform=windows
#:package Duxel.$(platform).App@*-*

using Duxel.Core;
using Duxel.App;
using Duxel.Windows.App;

var value = new UiState<string>("Use Tab to focus, Enter for a new line, and Ctrl+Z to undo. Long notes wrap without adding model line breaks.");
var editor = Fluent.Textarea("Notes", value.Binding, 4096, 400, 140);
editor.Options = new() { Placeholder = "Write a note..." };
var field = Fluent.Field(editor);
field.Width = 400;
field.Options = new() { Label = "Notes", Hint = "Tab moves to the next control. Shift+Tab moves back." };
var smallValue = new UiState<string>("");
var small = new UiMultilineInput<UiFluentMultilineInputBehavior,
    UiFluentTextareaPresentation<UiFluentSmallTextareaSize, UiFluentFilledDarkerInput>>(
    "Small", smallValue.Binding, width : 400);
small.Options = new() { Placeholder = "Small / filled darker" };
var largeValue = new UiState<string>("Large / filled lighter\r\nHorizontal scrolling keeps this long readonly note on a single source line.\rOriginal CRLF and CR are preserved.");
var large = new UiMultilineInput<UiFluentMultilineInputBehavior,
    UiFluentTextareaPresentation<UiFluentLargeTextareaSize, UiFluentFilledLighterInput>>(
    "Large", largeValue.Binding, width : 400);
large.Options = new() { Flags = UiInputTextFlags.ReadOnly, Wrap = false };
var readOnly = Fluent.Button("Read only : off");
readOnly.Activated = () =>
{
    var enabled = (editor.Options.Flags & UiInputTextFlags.ReadOnly) == 0;
    editor.Options = editor.Options with { Flags = enabled ? UiInputTextFlags.ReadOnly : UiInputTextFlags.None };
    readOnly.Label = enabled ? "Read only : on" : "Read only : off";
};
var width = Fluent.Button("Width : 400");
width.Activated = () =>
{
    field.Width = field.Width == 400 ? 200 : 400;
    width.Label = $"Width : {field.Width}";
};
var clear = Fluent.Button("Clear", () => value.Value = string.Empty);
var wrap = Fluent.Button("Wrap : on");
wrap.Activated = () =>
{
    var enabled = editor.Options.Wrap == false;
    editor.Options = editor.Options with { Wrap = enabled };
    wrap.Label = enabled ? "Wrap : on" : "Wrap : off";
};
using var page = Dux.Padding(new UiColumn(
[
    Fluent.Title2("Fluent Textarea"),
    Fluent.Body1("Drag starts in an editor and continues outside its field until release."),
    field,
    Dux.Row(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min), readOnly, width, wrap, clear),
    small,
    large,
    Fluent.Caption1("Resize handles remain disabled.")
], new(Spacing : 16, MainAxisSize : UiMainAxisSize.Min)), new(24));
var exercise = Environment.GetEnvironmentVariable("DUXEL_SAMPLE_EXERCISE") == "1";
var previousStep = -1;
var options = DuxelApp.Options<TextareaDesign>(Dux.App(Dux.View(ui =>
{
    if (exercise)
    {
        var step = (int)(ui.GetTime() * 2) % 4;
        if (step != previousStep)
        {
            previousStep = step;
            width.Activated?.Invoke();
            readOnly.Activated?.Invoke();
            wrap.Activated?.Invoke();
            value.Value = step == 0 ? string.Empty : step == 2
                ? string.Join('\n', Enumerable.Range(1, 20).Select(index => $"Line {index} : long text wraps with the field."))
                : "A short note with e\u0301.\nAnother line.";
            field.Options = field.Options with { ValidationMessage = step == 2 ? "Review this long note" : null };
            Console.WriteLine($"TextareaExercise : {step}");
        }
        ui.RequestFrameAt(ui.GetTime() + 0.1);
    }
    page.Render(ui);
})), title : "Duxel / Fluent Textarea", width : 850, height : 740);
DuxelWindowsApp.Run(options with
{
    Window = options.Window with { MinWidth = 760, MinHeight = 680 },
    Debug = new() { LogStartupTimings = true }
});

public readonly struct TextareaDesign : IUiDesign
{
    public static UiCompiledDesign Create() => UiCompiledDesign.Windows11Dark;
}
