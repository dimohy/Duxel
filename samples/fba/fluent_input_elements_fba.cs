// FBA: source text elements in shared single-line editing.
#:property TargetFramework=net11.0-windows7.0
#:property OutputType=WinExe
#:property OptimizationPreference=Speed
#:property platform=windows
#:package Duxel.$(platform).App@*-*

using Duxel.Core;
using Duxel.App;
using Duxel.Windows.App;

var value = new UiState<string>("A😀e\u0301Z");
const string longText = "A long editable note with more text than the field can show. Drag beyond either edge to select and scroll.";
var editor = Fluent.Input("Text", value.Binding, 128, 400);
editor.Options = new(Placeholder : "Type or paste text");
var readonlyValue = new UiState<string>("👩\u200d💻  e\u0301  α+β");
var readonlyInput = Fluent.Input("Readonly", readonlyValue.Binding, 128, 400);
readonlyInput.Options = new(UiInputTextFlags.ReadOnly);
var unicode = Fluent.Button("Load Unicode", () => value.Value = "A😀e\u0301Z");
var ascii = Fluent.Button("Load ASCII", () => value.Value = "An ASCII note");
var longValue = Fluent.Button("Load long text", () => value.Value = longText);
var readOnly = Fluent.Button("Read only : off");
readOnly.Activated = () =>
{
    var enabled = (editor.Options.Flags & UiInputTextFlags.ReadOnly) == 0;
    editor.Options = editor.Options with { Flags = enabled ? UiInputTextFlags.ReadOnly : UiInputTextFlags.None };
    readOnly.Label = enabled ? "Read only : on" : "Read only : off";
};
using var page = Dux.Padding(new UiColumn(
[
    Fluent.Title2("Single-line text elements"),
    Fluent.Body1("Arrows, selection and deletion keep an emoji or combining sequence together."),
    editor,
    Dux.Row(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min), unicode, ascii, longValue, readOnly),
    Fluent.Body1("Hold the mouse beyond either field edge to extend selection and scroll."),
    Fluent.Body1("Readonly text can be selected and copied."),
    readonlyInput,
    Fluent.Caption1("Ctrl+Backspace / Ctrl+Delete remove words. Ctrl+Z / Ctrl+Y undo / redo. Loading a value resets history.")
], new(Spacing : 16, MainAxisSize : UiMainAxisSize.Min)), new(24));
var exercise = Environment.GetEnvironmentVariable("DUXEL_SAMPLE_EXERCISE") == "1";
var previousStep = -1;
var options = DuxelApp.Options<ElementDesign>(Dux.App(Dux.View(ui =>
{
    if (exercise)
    {
        var step = (int)(ui.GetTime() * 2) % 4;
        if (step != previousStep)
        {
            previousStep = step;
            value.Value = step == 0 ? "A😀e\u0301Z" : step == 1 ? longText : step == 2 ? "👩\u200d💻  α+β" : string.Empty;
            readOnly.Activated?.Invoke();
            Console.WriteLine($"ElementExercise : {step}");
        }
        ui.RequestFrameAt(ui.GetTime() + 0.1);
    }
    page.Render(ui);
})), title : "Duxel / Single-line Text Elements", width : 900, height : 540);
var emojiFont = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "seguiemj.ttf");
if (!File.Exists(emojiFont))
    throw new FileNotFoundException("This sample requires an explicitly selected emoji font.", emojiFont);
DuxelWindowsApp.Run(options with
{
    Font = options.Font with
    {
        SecondaryFontPath = emojiFont,
        Typography = options.Font.Typography with { SecondaryRegular = emojiFont }
    },
    Window = options.Window with { MinWidth = 760, MinHeight = 480 },
    Debug = new() { LogStartupTimings = true }
});

public readonly struct ElementDesign : IUiDesign
{
    public static UiCompiledDesign Create() => UiCompiledDesign.Windows11Dark;
}
