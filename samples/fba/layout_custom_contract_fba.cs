// FBA: an external generated single-child layout with pure metadata and real input.
#:property TargetFramework=net11.0-windows7.0
#:property OutputType=WinExe
#:property OptimizationPreference=Speed
#:property platform=windows
#:package Duxel.$(platform).App@*-*

using Duxel.Core;
using Duxel.App;
using Duxel.Windows.App;

var value = new UiState<string>("Custom layout keeps this editor.");
var frame = new ConsumerFooter(Fluent.Input("Custom editor", value.Binding, 256), new(12, 24));
var footer = Fluent.Button("Footer : 24");
var footerTooltip = new ConsumerTooltip(footer,
    "Reserve space below the editor. Hover or focus to inspect the custom tooltip.",
    new()
    {
        Relationship = UiTooltipRelationship.Description,
        ShowScrollbars = true,
        Positioning = new()
        {
            Position = UiPopupPosition.AboveStart,
            MatchTargetWidth = true,
            AutoSize = UiPopupAutoSize.Both,
            ArrowHeight = 6,
            CrossAxisOffset = 8,
            ShiftToCoverTarget = true,
            OverflowBoundaryPadding = new(8),
            FallbackPositions = [UiPopupPosition.BelowStart, UiPopupPosition.AboveEnd, UiPopupPosition.BelowEnd]
        }
    })
{
    Visible = false
};
footerTooltip.VisibleChangeRequested = change => footerTooltip.Visible = change.Visible;
footer.Activated = () =>
{
    var next = frame.Options.Footer == 24 ? 44 : 24;
    frame.Options = frame.Options with { Footer = next };
    footer.Label = $"Footer : {next}";
};
var inset = Fluent.Button("Inset : 12");
inset.Activated = () =>
{
    var next = frame.Options.Inset == 12 ? 4 : 12;
    frame.Options = frame.Options with { Inset = next };
    inset.Label = $"Inset : {next}";
};
using var page = Dux.Padding(new UiColumn(
[
    Fluent.Title2("User-defined compiled layout"),
    Fluent.Body1("The layout reserves a footer and passes the remaining allocation to the same editor. The Footer button owns a custom tooltip."),
    Dux.SizedBox(frame, 360, 160),
    Dux.Row(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min), footerTooltip, inset)
], new(Spacing : 16, MainAxisSize : UiMainAxisSize.Min)), new(24));
var exercise = Environment.GetEnvironmentVariable("DUXEL_SAMPLE_EXERCISE") == "1";
var previousStep = -1;
var options = DuxelApp.Options<ConsumerContractDesign>(Dux.App(Dux.View(ui =>
{
    if (exercise)
    {
        var step = (int)(ui.GetTime() * 2) % 4;
        if (step != previousStep)
        {
            previousStep = step;
            footerTooltip.Visible = step is 1 or 2;
            if ((step & 1) == 0)
                footer.Activated?.Invoke();
            else
                inset.Activated?.Invoke();
            Console.WriteLine($"ContractExercise : {step}");
        }
        ui.RequestFrameAt(ui.GetTime() + 0.1);
    }
    page.Render(ui);
})), title : "Duxel / External Layout Contract", width : 820, height : 520);
DuxelWindowsApp.Run(options with
{
    Window = options.Window with { MinWidth = 760, MinHeight = 480 },
    Debug = new() { LogStartupTimings = true }
});

public readonly struct ConsumerContractDesign : IUiDesign
{
    public static UiCompiledDesign Create() => UiCompiledDesign.Windows11Dark;
}

// This consumer opts into the Fluent surface; full Tooltip conformance remains separate work.
public readonly struct ConsumerTooltipPaint : IUiTooltipPresentation
{
    public static UiPopupLayout Arrange(UiControlContext context, string text, UiRect target, UiRect viewport,
        UiTooltipOptions options) =>
        UiFluentTooltipPresentation.Arrange<ConsumerTooltipOffset>(context, text, target, viewport, options);

    public static void Paint(UiControlContext context, string text, UiPopupLayout layout) =>
        UiFluentTooltipPresentation.Paint(context, text, layout);
}

public readonly struct ConsumerTooltipOffset : IUiPopupOffset
{
    public static UiPopupOffset Resolve(UiPopupOffsetContext context) => new(context.Options.Gap ?? 4,
        context.Options.CrossAxisOffset + ((int)context.Position % 3 == 2 ? -4 : 0));
}

[UiWidgetStyle(typeof(UiTooltip<,>), typeof(UiHoverFocusTooltipBehavior), typeof(ConsumerTooltipPaint))]
public partial class ConsumerTooltip;

// CONTRACT DEFINITIONS BEGIN
public readonly record struct ConsumerFooterOptions(float Inset, float Footer)
{
    public ConsumerFooterOptions Validate()
    {
        if (!float.IsFinite(Inset) || Inset < 0 || !float.IsFinite(Footer) || Footer < 0
            || !float.IsFinite(2 * Inset + Footer))
            throw new ArgumentOutOfRangeException(nameof(ConsumerFooterOptions));
        return this;
    }
}

public readonly struct ConsumerFooterLayout : IUiSingleChildLayout<ConsumerFooterOptions>
{
    public static bool CanRetainMeasurement => true;
    public static void Validate(ConsumerFooterOptions options) => options.Validate();
    public static UiContainerLayoutResult Measure(UiControlContext context, UiWidget? child,
        ConsumerFooterOptions options, UiBoxConstraints constraints)
    {
        options.Validate();
        constraints.Validate();
        if (!float.IsFinite(constraints.MaxWidth) || !float.IsFinite(constraints.MaxHeight))
            throw new InvalidOperationException("ConsumerFooter requires a finite parent allocation.");
        var size = constraints.Constrain(new(constraints.MaxWidth, constraints.MaxHeight));
        var available = new UiVector2(MathF.Max(0, size.X - 2 * options.Inset),
            MathF.Max(0, size.Y - 2 * options.Inset - options.Footer));
        var content = child is null ? default : context.MeasureConstrained(child, UiBoxConstraints.Tight(available));
        return new(size, new(options.Inset, options.Inset, content.X, content.Y), new(0, 0, size.X, size.Y));
    }
}

public readonly struct ConsumerFramePaint : IUiContainerPresentation
{
    public static void Paint(UiControlContext context, UiRect bounds) =>
        context.Frame(bounds, new(30, 50, 70), new(100, 160, 210), 0, 1);
}

[UiWidgetStyle(typeof(UiSingleChildBox<,,>), typeof(ConsumerFooterLayout), typeof(ConsumerFooterOptions), typeof(ConsumerFramePaint))]
public partial class ConsumerFooter;
