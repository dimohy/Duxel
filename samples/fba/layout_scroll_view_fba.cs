// FBA: owned constrained scroll widgets with real editors and stable child identity.
#:property TargetFramework=net11.0-windows7.0
#:property OutputType=WinExe
#:property OptimizationPreference=Speed
#:property platform=windows
#:package Duxel.$(platform).App@*-*

using Duxel.Core;
using Duxel.App;
using Duxel.Windows.App;

var text = new UiState<string>("Type here, scroll away, then return.");
var rows = Enumerable.Range(1, 40).Select(CreateRow).Cast<UiWidget>().ToArray();
var editor = Fluent.Input("Owned scroll editor", text.Binding, 256);
var optionalRow = new OptionalScrollRow();
var vertical = Dux.ScrollView(new UiColumn(
    [editor, optionalRow, .. rows], new(Spacing : 8, MainAxisSize : UiMainAxisSize.Min)),
    new(Width : 300, Height : 280));
var both = Dux.ScrollView(Dux.SizedBox(Dux.Padding(Dux.Row(new(Spacing : 36, MainAxisSize : UiMainAxisSize.Min),
    Dux.SizedBox(CreateRows(1), width : 280),
    Dux.SizedBox(CreateRows(41), width : 280)), new(12)), 620, 900),
    new(UiScrollAxis.Both, 300, 280));
vertical.AccessibleName = "Vertical scroll view";
both.AccessibleName = "Two-axis scroll view";

var width = Fluent.Button("Width : 300");
width.Activated = () =>
{
    var next = vertical.Options.Width == 300 ? 180 : 300;
    vertical.Options = vertical.Options with { Width = next };
    both.Options = both.Options with { Width = next };
    width.Label = $"Width : {next}";
};
var height = Fluent.Button("Height : 280");
height.Activated = () =>
{
    var next = vertical.Options.Height == 280 ? 160 : 280;
    vertical.Options = vertical.Options with { Height = next };
    both.Options = both.Options with { Height = next };
    height.Label = $"Height : {next}";
};

var exercise = Environment.GetEnvironmentVariable("DUXEL_SAMPLE_EXERCISE") == "1";
var previousStep = -1;
var direction = Fluent.Button("Direction : LTR");
direction.Activated = () =>
{
    var rtl = !both.Options.RightToLeft;
    both.Options = both.Options with { RightToLeft = rtl };
    both.Offset = default;
    direction.Label = rtl ? "Direction : RTL" : "Direction : LTR";
};
using var page = Dux.Padding(new UiColumn(
[
    Fluent.Title2("Owned scroll views"),
    Fluent.Body1("Edit, scroll, resize the viewport, then return. Child identity and undo stay intact."),
    Dux.Row(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min),
        width, height, direction, Fluent.Button("Toggle row", optionalRow.Toggle),
        Fluent.Button("Top", () =>
        {
            vertical.Offset = default;
            both.Offset = default;
        }),
        Fluent.Button("End", () =>
        {
            vertical.Offset = new(0, 10000);
            both.Offset = new(10000, 10000);
        })),
    new ScrollExerciseDriver(Exercise),
    Dux.Row(new(Spacing : 24, MainAxisSize : UiMainAxisSize.Min), vertical, both),
    Fluent.Caption1("Left : vertical / Right : both axes. This sample does not virtualize its rows.")
], new(Spacing : 16, MainAxisSize : UiMainAxisSize.Min)), new(24));

var options = DuxelApp.Options<ScrollViewDesign>(
    Dux.App(page), title : "Duxel / Owned Scroll Views", width : 860, height : 620);
DuxelWindowsApp.Run(options with
{
    Window = options.Window with { MinWidth = 800, MinHeight = 560 },
    Debug = new() { LogStartupTimings = true }
});

void Exercise(UiImmediateContext ui)
{
    if (exercise)
    {
        var step = (int)(ui.GetTime() * 2) % 6;
        if (step != previousStep)
        {
            previousStep = step;
            switch (step)
            {
                case 1:
                    width.Activated?.Invoke();
                    break;
                case 2:
                    height.Activated?.Invoke();
                    break;
                case 3:
                    vertical.Offset = new(0, 10000);
                    both.Offset = new(10000, 10000);
                    break;
                case 4:
                    vertical.Offset = default;
                    both.Offset = default;
                    break;
                case 5:
                    width.Activated?.Invoke();
                    height.Activated?.Invoke();
                    direction.Activated?.Invoke();
                    optionalRow.Toggle();
                    break;
            }
            Console.WriteLine($"ScrollExercise : {step}");
        }
        ui.RequestFrameAt(ui.GetTime() + 0.1);
    }
}

static UiColumn CreateRows(int start) => new(
    Enumerable.Range(start, 20).Select(CreateRow).ToArray(),
    new(Spacing : 8, MainAxisSize : UiMainAxisSize.Min));

static UiWidget CreateRow(int index)
{
    var clicks = 0;
    var row = Fluent.Button($"Row : {index}");
    row.Activated = () => row.Label = $"Row : {index} / Clicks : {++clicks}";
    return row;
}

public readonly struct ScrollViewDesign : IUiDesign
{
    public static UiCompiledDesign Create() => UiCompiledDesign.Windows11Dark;
}

sealed class OptionalScrollRow : UiControlWidget
{
    private static readonly UiVector2 RowSize = new(128, 32);
    private bool _visible;

    internal void Toggle()
    {
        _visible = !_visible;
        RequestRebuild();
    }

    protected override bool CanRetainMeasurement => true;
    protected override UiVector2 MeasureContent(UiControlContext context) => RowSize;

    protected override void RenderControl(UiImmediateContext ui)
    {
        if (_visible)
            ui.Button("Optional row", RowSize);
        else
            ui.Dummy(RowSize);
    }
}

sealed class ScrollExerciseDriver(Action<UiImmediateContext> exercise) : UiControlWidget
{
    protected override bool CanRetainMeasurement => true;
    protected override UiVector2 MeasureContent(UiControlContext context) => default;
    protected override void RenderControl(UiImmediateContext ui) => exercise(ui);
}
