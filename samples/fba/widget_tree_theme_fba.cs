// FBA: retained Build tree and source-generated Fluent 2 behavior/presentation binding.
#:property TargetFramework=net10.0
#:property OutputType=WinExe
#:property OptimizationPreference=Speed
#:property platform=windows
#:package Duxel.$(platform).App@*-*

using Duxel.Core;
using Duxel.App;
using Duxel.Windows.App;

if (args.Length > 1)
{
    throw new ArgumentException("Use --buttons, --checkbox, --switch, --link, --input, --field, --spinner, --tooltip, --radio, --typography, --progress, --slider, --badge, --divider or --panel to open a component gallery.");
}
var mode = args.Length == 0 ? string.Empty : args[0];
using UiWidget page = mode switch
{
    "" => new WidgetPage(),
    "--buttons" => new ButtonsPage(),
    "--typography" => new TypographyPage(),
    "--progress" => new ProgressPage(),
    "--slider" => new SliderPage(),
    "--badge" => new BadgePage(),
    "--divider" => new DividerPage(),
    "--panel" => new PanelPage(),
    "--checkbox" => new CheckboxPage(),
    "--radio" => new RadioPage(),
    "--switch" => new SwitchPage(),
    "--link" => new LinkPage(),
    "--input" => new InputPage(),
    "--field" => new FieldPage(),
    "--spinner" => new SpinnerPage(),
    "--tooltip" => new TooltipPage(),
    _ => throw new ArgumentException("Use --buttons, --checkbox, --switch, --link, --input, --field, --spinner, --tooltip, --radio, --typography, --progress, --slider, --badge, --divider or --panel to open a component gallery.")
};
DuxelWindowsApp.Run(DuxelApp.Options<ShowcaseWidgetDesign>(Dux.App(page),
    title  : "Duxel Fluent 2 / Compiled Widgets", width  : 1040, height  : mode == "--typography" ? 1000 : 800)
    with
{
    Debug = new DuxelDebugOptions { LogStartupTimings = true }
});

public readonly struct ShowcaseWidgetDesign : IUiDesign
{
    public static UiCompiledDesign Create() => UiCompiledDesign.Windows11Dark;
}

sealed class TooltipPage : UiWidget
{
    protected override IUiView Build(UiBuildContext context) => context.Child("root", () =>
    {
        var normal = Fluent.Tooltip(Fluent.Button("Save"), "Save a local copy");
        var arrow = Fluent.Tooltip(Fluent.Button("With arrow"), "A tooltip with a target arrow",
            new() { Positioning = new() { ArrowHeight = 6 } });
        var label = Fluent.Tooltip(Fluent.Button("?"), "Show details",
            new() { Relationship = UiTooltipRelationship.Label });
        var controlled = Fluent.Tooltip(Fluent.Button("Controlled target"), "Caller-controlled tooltip");
        controlled.Visible = false;
        controlled.VisibleChangeRequested = change => controlled.Visible = change.Visible;
        var toggle = Fluent.Button("Show controlled tooltip");
        toggle.Activated = () =>
        {
            controlled.Visible = controlled.Visible != true;
            toggle.Label = controlled.Visible == true ? "Hide controlled tooltip" : "Show controlled tooltip";
        };
        return Dux.Padding(Dux.Column(new(Spacing : 24, MainAxisSize : UiMainAxisSize.Min),
            Fluent.Title2("Fluent Tooltip"),
            Fluent.Body1("Hover or focus a target. Escape dismisses the tooltip while preserving focus."),
            Dux.Row(new(Spacing : 24, MainAxisSize : UiMainAxisSize.Min), normal, arrow, label),
            Dux.Row(new(Spacing : 24, MainAxisSize : UiMainAxisSize.Min), controlled, toggle)), new(32));
    });
}

sealed class SpinnerPage : UiWidget
{
    protected override IUiView Build(UiBuildContext context)
    {
        var sizes = new[]
        {
            UiSpinnerSize.ExtraTiny, UiSpinnerSize.Tiny, UiSpinnerSize.ExtraSmall, UiSpinnerSize.Small,
            UiSpinnerSize.Medium, UiSpinnerSize.Large, UiSpinnerSize.ExtraLarge, UiSpinnerSize.Huge
        };
        var widgets = new UiWidget[sizes.Length];
        for (var i = 0; i < sizes.Length; i++)
        {
            var size = sizes[i];
            widgets[i] = context.Child("size-" + i, () => Fluent.Spinner("Size " + UiFluentSpinnerPresentation.Diameter(size), new(size, UiSpinnerLabelPosition.Below) { Width = 160 }));
        }
        var after = context.Child("after", () => Fluent.Spinner("After", new(LabelPosition  : UiSpinnerLabelPosition.After) { Width = 220 }));
        var before = context.Child("before", () => Fluent.Spinner("Before", new(LabelPosition  : UiSpinnerLabelPosition.Before) { Width = 220 }));
        var above = context.Child("above", () => Fluent.Spinner("Above", new(LabelPosition  : UiSpinnerLabelPosition.Above) { Width = 220 }));
        var below = context.Child("below", () => Fluent.Spinner("Below", new(LabelPosition  : UiSpinnerLabelPosition.Below) { Width = 220 }));
        var reduced = context.Child("reduced", () => Fluent.Spinner("Reduced motion", new(ReducedMotion  : true) { Width = 260 }));
        var rtl = context.Child("rtl", () => Fluent.Spinner("Right to left", new(RightToLeft  : true) { Width = 260 }));
        var delayed = context.Child("delayed", () => Fluent.Spinner("Shown after one second", new(DelaySeconds  : 1) { Width = 300 }));
        var inverted = context.Child("inverted", () => Fluent.Spinner("Downloading content …", new(Appearance  : UiSpinnerAppearance.Inverted) { Width = 300 }));
        return Dux.Column(16, Fluent.Title2("Spinner"), Fluent.Body1("Eight sizes, label placement and loading motion"),
            Dux.Row(12, widgets[0], widgets[1], widgets[2], widgets[3]), Dux.Row(12, widgets[4], widgets[5], widgets[6], widgets[7]),
            Fluent.Body1("Label position"), Dux.Row(12, after, before, above, below),
            Fluent.Body1("Motion and delayed visibility"), Dux.Row(12, reduced, rtl), delayed,
            Fluent.Body1("Inverted appearance on a strong surface"), Dux.View(ui =>
            {
                var p = ui.GetCursorScreenPos();
                ui.GetWindowDrawList().AddRectFilled(new(p.X, p.Y, 324, 64), new(15, 108, 189), ui.WhiteTextureId);
                ui.SetCursorScreenPos(new(p.X + 12, p.Y + 16));
                inverted.Render(ui);
                ui.SetCursorScreenPos(p);
                ui.Dummy(new(324, 64));
            }));
    }
}

sealed class FieldPage : UiWidget
{
    private readonly UiState<string> _value = new("");
    protected override IUiView Build(UiBuildContext context)
    {
        var value = context.Watch(_value);
        var options = new UiFieldOptions
        {
            Label = "Work email",
            Hint = "Use your work address",
            Required = true,
            ValidationMessage = value.Length < 3 ? "Enter at least three characters" : "Ready",
            ValidationState = value.Length < 3 ? UiFieldValidationState.Error : UiFieldValidationState.Success
        };
        var small = context.Child("small", () => new ShowcaseSmallField(new ShowcaseInput0("Email", _value.Binding, 256)) { Width = 220 });
        var medium = context.Child("medium", () => Fluent.Field(Fluent.Input("Email", _value.Binding, 256)));
        medium.Width = 220;
        var large = context.Child("large", () => new ShowcaseLargeField(new ShowcaseInput8("Email", _value.Binding, 256)) { Width = 220 });
        var horizontal = context.Child("horizontal", () => new ShowcaseHorizontalField(Fluent.Input("Email", _value.Binding, 256)) { Width = 330 });
        var disabled = context.Child("disabled", () => Fluent.Field(Fluent.Input("Email", _value.Binding, 256)));
        disabled.Width = 220;
        disabled.Options = new()
        {
            Label = "Disabled control",
            Hint = "The description remains readable",
            Disabled = true
        };
        var fluid = context.Child("fluid", () => Fluent.Field(Fluent.Input("Fluid", _value.Binding, 256)));
        fluid.Options = options with
        {
            Label = "Responsive full width",
            Hint = "Resize the application to change this field's available width"
        };
        small.Options = medium.Options = large.Options = horizontal.Options = options;
        return Dux.Column(Fluent.Title2("Field : labels, helper text and validation"), Dux.Text($"Value : {value}"),
            Dux.Text("Small / medium / large labels"), Dux.Row(small, medium, large),
            Dux.Text("Horizontal label and disabled control"), Dux.Row(horizontal, disabled),
            Dux.Text("Available width with naturally measured height"), fluid,
            Dux.Text("Click a label to focus its input. Tab and editing use the shared input engine."));
    }
}
[UiWidgetStyle(typeof(UiField<,>), typeof(UiLabelFieldBehavior), typeof(UiFluentFieldPresentation<UiFluentSmallFieldSize, UiVerticalFieldLayout>))]
partial class ShowcaseSmallField;
[UiWidgetStyle(typeof(UiField<,>), typeof(UiLabelFieldBehavior), typeof(UiFluentFieldPresentation<UiFluentLargeFieldSize, UiVerticalFieldLayout>))]
partial class ShowcaseLargeField;
[UiWidgetStyle(typeof(UiField<,>), typeof(UiLabelFieldBehavior), typeof(UiFluentFieldPresentation<UiFluentMediumFieldSize, UiHorizontalFieldLayout>))]
partial class ShowcaseHorizontalField;

[UiButtonStyle(typeof(UiPressButtonBehavior), typeof(UiFluentButtonPresentation))]
public partial class ShowcasePressButton;

[UiWidgetStyle(typeof(UiText<>), typeof(ShowcaseNoticePresentation))]
public partial class ShowcaseNoticeText;

[UiButtonStyle(typeof(UiReleaseButtonBehavior), typeof(UiFluentSmallButtonPresentation))]
public partial class ShowcaseSmallFluentButton;
[UiButtonStyle(typeof(UiReleaseButtonBehavior), typeof(UiFluentLargeButtonPresentation))]
public partial class ShowcaseLargeFluentButton;

[UiButtonStyle(typeof(UiReleaseButtonBehavior), typeof(UiFluentButtonPresentation<UiFluentPrimaryButtonAppearance>))]
public partial class ShowcasePrimaryButton;
[UiButtonStyle(typeof(UiReleaseButtonBehavior), typeof(UiFluentButtonPresentation<UiFluentOutlineButtonAppearance>))]
public partial class ShowcaseOutlineButton;
[UiButtonStyle(typeof(UiReleaseButtonBehavior), typeof(UiFluentButtonPresentation<UiFluentSubtleButtonAppearance>))]
public partial class ShowcaseSubtleButton;
[UiButtonStyle(typeof(UiReleaseButtonBehavior), typeof(UiFluentButtonPresentation<UiFluentTransparentButtonAppearance>))]
public partial class ShowcaseTransparentButton;

[UiButtonStyle(typeof(UiReleaseButtonBehavior), typeof(UiFluentSmallButtonPresentation<UiFluentSecondaryButtonAppearance<UiFluentSquareButtonShape>>))]
public partial class ShowcaseSmallSquareButton;
[UiButtonStyle(typeof(UiReleaseButtonBehavior), typeof(UiFluentButtonPresentation<UiFluentSecondaryButtonAppearance<UiFluentSquareButtonShape>>))]
public partial class ShowcaseSquareButton;
[UiButtonStyle(typeof(UiReleaseButtonBehavior), typeof(UiFluentLargeButtonPresentation<UiFluentSecondaryButtonAppearance<UiFluentSquareButtonShape>>))]
public partial class ShowcaseLargeSquareButton;
[UiButtonStyle(typeof(UiReleaseButtonBehavior), typeof(UiFluentSmallButtonPresentation<UiFluentSecondaryButtonAppearance<UiFluentCircularButtonShape>>))]
public partial class ShowcaseSmallCircularButton;
[UiButtonStyle(typeof(UiReleaseButtonBehavior), typeof(UiFluentButtonPresentation<UiFluentSecondaryButtonAppearance<UiFluentCircularButtonShape>>))]
public partial class ShowcaseCircularButton;
[UiButtonStyle(typeof(UiReleaseButtonBehavior), typeof(UiFluentLargeButtonPresentation<UiFluentSecondaryButtonAppearance<UiFluentCircularButtonShape>>))]
public partial class ShowcaseLargeCircularButton;

public readonly struct ShowcaseButtonCheckIcon : IUiButtonIcon
{
    public static void Paint(UiControlContext context, UiRect bounds, UiColor color, UiButtonInteraction interaction)
    {
        var width = interaction.Held ? 2.5f : 1.5f;
        context.Line(new(bounds.X + bounds.Width * .2f, bounds.Y + bounds.Height * .5f), new(bounds.X + bounds.Width * .43f, bounds.Y + bounds.Height * .73f), color, width);
        context.Line(new(bounds.X + bounds.Width * .43f, bounds.Y + bounds.Height * .73f), new(bounds.X + bounds.Width * .82f, bounds.Y + bounds.Height * .25f), color, width);
    }
}
[UiButtonStyle(typeof(UiReleaseButtonBehavior), typeof(UiFluentIconButtonPresentation<UiFluentMediumButtonIconSize, UiFluentPrimaryButtonAppearance, ShowcaseButtonCheckIcon, UiButtonIconBefore>))]
public partial class ShowcaseIconBeforeButton;
[UiButtonStyle(typeof(UiReleaseButtonBehavior), typeof(UiFluentIconButtonPresentation<UiFluentMediumButtonIconSize, UiFluentSubtleButtonAppearance, ShowcaseButtonCheckIcon, UiButtonIconAfter>))]
public partial class ShowcaseIconAfterButton;

[UiButtonStyle(typeof(UiReleaseButtonBehavior), typeof(UiFluentIconButtonPresentation<UiFluentMediumButtonIconSize, UiFluentPrimaryButtonAppearance, UiFluentAddIcon, UiButtonIconBefore>))]
public partial class ShowcaseOfficialAddButton;
[UiFluentIcon("add", UiFluentIconStyle.Interactive)]
public readonly partial struct ShowcaseCompiledAddIcon;
[UiButtonStyle(typeof(UiReleaseButtonBehavior), typeof(UiFluentIconButtonPresentation<UiFluentMediumButtonIconSize, UiFluentSubtleButtonAppearance, ShowcaseCompiledAddIcon, UiButtonIconAfter>))]
public partial class ShowcaseOfficialInteractiveAddButton;

sealed class ButtonsPage : UiWidget
{
    private readonly UiState<int> _count = new(0);
    protected override IUiView Build(UiBuildContext context)
    {
        var count = context.Watch(_count);
        var small = context.Child("small", () => new ShowcaseSmallFluentButton("Small", () => _count.Value++));
        var medium = context.Child("medium", () => Fluent.Button("Medium", () => _count.Value++));
        var large = context.Child("large", () => new ShowcaseLargeFluentButton("Large", () => _count.Value++));
        var disabledSmall = context.Child("disabled-small", static () => new ShowcaseSmallFluentButton("Small"));
        var disabledMedium = context.Child("disabled-medium", static () => Fluent.Button("Medium"));
        var disabledLarge = context.Child("disabled-large", static () => new ShowcaseLargeFluentButton("Large"));
        var primary = context.Child("primary", () => new ShowcasePrimaryButton("Primary", () => _count.Value++));
        var outline = context.Child("outline", () => new ShowcaseOutlineButton("Outline", () => _count.Value++));
        var subtle = context.Child("subtle", () => new ShowcaseSubtleButton("Subtle", () => _count.Value++));
        var transparent = context.Child("transparent", () => new ShowcaseTransparentButton("Transparent", () => _count.Value++));
        return Dux.Column(16, Fluent.Title2("Button"),
            Fluent.Body1("Hover, hold, release, or use Tab and Space to compare states."),
            Dux.Row(12, small, medium, large),
            Fluent.Body1("Primary, secondary, outline, subtle and transparent"),
            Dux.Row(12, primary, outline, subtle, transparent),
            Fluent.Body1("Square and circular : small, medium, large"),
            Dux.Row(12,
                context.Child("square-small", () => new ShowcaseSmallSquareButton("Small", () => _count.Value++)),
                context.Child("square-medium", () => new ShowcaseSquareButton("Medium", () => _count.Value++)),
                context.Child("square-large", () => new ShowcaseLargeSquareButton("Large", () => _count.Value++))),
            Dux.Row(12,
                context.Child("circular-small", () => new ShowcaseSmallCircularButton("Small", () => _count.Value++)),
                context.Child("circular-medium", () => new ShowcaseCircularButton("Medium", () => _count.Value++)),
                context.Child("circular-large", () => new ShowcaseLargeCircularButton("Large", () => _count.Value++))),
            Fluent.Body1("Add one : official Fluent icon before, after and icon-only"),
            Dux.Row(12,
                context.Child("icon-before", () => new ShowcaseOfficialAddButton("Add one", () => _count.Value++)),
                context.Child("icon-after", () => new ShowcaseOfficialInteractiveAddButton("Add one", () => _count.Value++)),
                context.Child("icon-only", () => new ShowcaseOfficialAddButton("", () => _count.Value++))),
            Fluent.Body1("Disabled"),
            Dux.View(ui =>
            {
                ui.BeginDisabled();
                try
                {
                    disabledSmall.Render(ui);
                    ui.SameLine(12);
                    disabledMedium.Render(ui);
                    ui.SameLine(12);
                    disabledLarge.Render(ui);
                }
                finally { ui.EndDisabled(); }
            }),
            Fluent.Body1("Activated : " + count),
            Fluent.Divider("Keyboard focus"),
            Fluent.Body1("Tab shows the focus ring. Enter activates on press; Space activates on release."));
    }
}

sealed class DividerPage : UiWidget
{
    protected override IUiView Build(UiBuildContext context)
    {
        var live = context.Child("divider-live", static () => Fluent.Divider("Ready", new(Appearance  : UiDividerAppearance.Brand)));
        return Dux.Column(12,
            Fluent.Title2("Divider"),
            Fluent.Body1("Content alignment, appearance, inset, orientation and retained updates."),
            Dux.Divider(),
            Fluent.Divider("Default"),
            Fluent.Divider("Subtle", new(Appearance  : UiDividerAppearance.Subtle)),
            Fluent.Divider("Strong", new(Appearance  : UiDividerAppearance.Strong)),
            Fluent.Divider("Brand", new(Appearance  : UiDividerAppearance.Brand)),
            Fluent.Divider("Start", new(Alignment  : UiDividerAlignment.Start, Inset  : true)),
            Fluent.Divider("End", new(Alignment  : UiDividerAlignment.End, Inset  : true)),
            Dux.Row(32,
                Fluent.Divider("Start", new(Vertical  : true, Alignment  : UiDividerAlignment.Start, Length  : 120)),
                Fluent.Divider("Center", new(Vertical  : true, Length  : 120)),
                Fluent.Divider("End", new(Vertical  : true, Alignment  : UiDividerAlignment.End, Length  : 120)),
                Fluent.Divider("Inset", new(Vertical  : true, Inset  : true, Length  : 120))),
            live,
            context.Child("divider-update", () => Fluent.Button("Update retained content", () => live.Source = live.Source == "Ready" ? "Updated" : "Ready")));
    }
}

public readonly struct ShowcaseNoticePresentation : IUiTextPresentation
{
    public static void Render(UiControlContext context, string text, UiColor? color)
    {
        var measured = context.MeasureText(text);
        var layout = context.PlaceText(new(measured.X + 32, measured.Y + 12));
        if (!layout.Visible)
            return;
        context.Frame(new(layout.Position.X, layout.Position.Y, layout.Size.X, layout.Size.Y),
            context.Theme.Button, context.Theme.ButtonBorder, 6, 1);
        context.Circle(new(layout.Position.X + 12, layout.Position.Y + layout.Size.Y / 2), 3, context.Theme.CheckMark);
        context.PaintText(text, layout with
        {
            Position = new(layout.Position.X + 24, layout.Position.Y + 6)
        }, color ?? context.Theme.Text);
    }
}

sealed class ChildActionButton(string label, Action activated) : UiFluentButton(label, activated)
{
    public int ActivationCount
    {
        get;
        private set;
    }
    protected override void OnActivated()
    {
        ActivationCount++;
        base.OnActivated();
    }
}

sealed class WidgetPage : UiWidget
{
    private readonly UiState<int> _count = new(0);
    private readonly UiState<bool> _showChild = new(true);
    private readonly UiState<bool> _enabled = new(true);
    private readonly UiState<bool?> _mixed = new(null);
    private readonly UiState<bool> _live = new(false);
    private readonly UiState<string> _name = new("Duxel");
    private readonly UiState<float> _gain = new(.5f);
    private int _builds;

    protected override IUiView Build(UiBuildContext context)
    {
        var count = context.Watch(_count);
        var showChild = context.Watch(_showChild);
        _builds++;
        var release = context.Child("release-action", () => Fluent.Button("Release action", () => _count.Value++));
        var press = context.Child("press-action", () => new ShowcasePressButton("Press action", () => _count.Value++));
        var selectedAction = context.Child("selected-action", () => Fluent.ToggleButton("Keep selected", _live.Binding));
        var notice = context.Child("custom-notice", static () => new ShowcaseNoticeText("User text presentation changes layout and composition."));
        var title = context.Child("fluent-title", static () => Fluent.Title1("Build once. Rebuild watched state."));
        var typography = context.Child("fluent-typography", static () => Fluent.Body1Strong("Segoe UI Semibold with the Fluent 20px line box."));
        return Dux.Column(10,
            title,
            typography,
            Fluent.Body1($"Count {count} | Build calls {_builds}"),
            Dux.Text("Fluent 2 is the first widget theme. Generated C# binds concrete implementations."),
            notice,
            Dux.TextWrapped("Generated standard catalog (migration in progress) : " + string.Join(", ", Fluent.ComponentNames.ToArray())),
            Dux.Row(12, release, press, selectedAction),
            Dux.Text("Both buttons share the palette. Their activation event differs."),
            Dux.Row(8, context.Child("ready-badge", static () => Fluent.Badge("Ready")),
                context.Child("action-count-badge", () => Fluent.CounterBadge(() => new(_count.Value, ShowZero  : true)))),
            Dux.Checkbox("Keep keyed child mounted", _showChild.Binding),
            showChild ? context.Child("stateful-counter", static () => new ChildCounter()) : Dux.Text("Child removed; its owned state has been released."),
            Dux.Heading("Existing controls remain interoperable", 20),
            Dux.TextField("Name", _name.Binding),
            Dux.Checkbox("Enabled", _enabled.Binding),
            Dux.Checkbox("Mixed selection", _mixed.Binding),
            Dux.Row(12, Dux.Text("Live setting"), Dux.Toggle("live-setting", _live.Binding)),
            context.Child("gain-slider", () => Fluent.Slider("Gain", _gain.Binding, 0, 1, .01f)),
            Dux.Text("Button, toggle button, checkbox and switch use compiled implementations; full Fluent fidelity remains in progress."));
    }
}

sealed class SliderPage : UiWidget
{
    private readonly UiState<float> _value = new(25);
    protected override IUiView Build(UiBuildContext context) => Dux.Column(14,
        context.Child("title", static () => Fluent.Title3("Slider : pointer, drag and keyboard")),
        context.Child("value", () => Fluent.Body1(() => "Value : " + _value.Value.ToString("0.##"))),
        context.Child("medium", () => Fluent.Slider("Medium", _value.Binding)),
        context.Child("small", () => new UiFluentSlider("Small steps", _value.Binding) { Options = new(0, 100, 10, Size  : UiSliderSize.Small) }),
        context.Child("reversed", () => new UiFluentSlider("Reversed", _value.Binding) { Options = new(0, 100, Reversed  : true) }),
        context.Child("vertical", () => new UiFluentSlider("Vertical", _value.Binding) { Options = new(0, 100, Vertical  : true), Size = new(32, 160) }),
        context.Child("disabled", () => Fluent.Slider("Disabled", _value.Binding)).Modifier(UiModifier.Disabled()),
        context.Child("help", static () => Fluent.Body1("Tab to focus. Use arrows, Home/End and PageUp/PageDown. All sliders share the value.")));
}

sealed class ProgressPage : UiWidget
{
    private readonly UiState<float> _value = new(25);
    protected override IUiView Build(UiBuildContext context) =>
        Dux.Column(16,
            context.Child("title", static () => Fluent.Title3("Progress and motion")),
            context.Child("value", () => Fluent.ProgressBar(() => new(_value.Value, 100))),
            context.Child("progress-slider", () => Fluent.Slider("Completed out of100", _value.Binding)),
            context.Child("success", static () => CreateStatus(UiProgressColor.Success)),
            context.Child("warning", static () => CreateStatus(UiProgressColor.Warning)),
            context.Child("error", static () => CreateStatus(UiProgressColor.Error)),
            context.Child("motion-label", static () => Fluent.Body1("Indeterminate : three-second sliding gradient")),
            context.Child("motion", static () => Fluent.ProgressBar((float?)null)),
            context.Child("reduced-label", static () => Fluent.Body1("Reduced motion : opacity pulse")),
            context.Child("reduced", static () => new UiFluentProgressBar(null) { ReducedMotion = true, Thickness = UiProgressThickness.Large }));
    private static UiFluentProgressBar CreateStatus(UiProgressColor color) =>
        new(.75f)
        {
            Color = color,
            Thickness = UiProgressThickness.Large,
            Shape = UiProgressShape.Square
        };
}

sealed class TypographyPage : UiWidget
{
    protected override IUiView Build(UiBuildContext context) =>
        Dux.Column(8,
            context.Child("body1", static () => Fluent.Body1("Body1 - Regular 14 / 20")),
            context.Child("body1-strong", static () => Fluent.Body1Strong("Body1Strong - Semibold 14 / 20")),
            context.Child("body1-stronger", static () => Fluent.Body1Stronger("Body1Stronger - Bold 14 / 20")),
            context.Child("body2", static () => Fluent.Body2("Body2 - Regular 16 / 22")),
            context.Child("caption1", static () => Fluent.Caption1(static () => "Caption1 - Regular 12 / 16 (getter source)")),
            context.Child("caption1-strong", static () => Fluent.Caption1Strong("Caption1Strong - Semibold 12 / 16")),
            context.Child("caption1-stronger", static () => Fluent.Caption1Stronger("Caption1Stronger - Bold 12 / 16")),
            context.Child("caption2", static () => Fluent.Caption2("Caption2 - Regular 10 / 14")),
            context.Child("caption2-strong", static () => Fluent.Caption2Strong("Caption2Strong - Semibold 10 / 14")),
            context.Child("subtitle1", static () => Fluent.Subtitle1("Subtitle1 - Semibold 20 / 28")),
            context.Child("subtitle2", static () => Fluent.Subtitle2("Subtitle2 - Semibold 16 / 22")),
            context.Child("subtitle2-stronger", static () => Fluent.Subtitle2Stronger("Subtitle2Stronger - Bold 16 / 22")),
            context.Child("title1", static () => Fluent.Title1("Title1 - Semibold 32 / 40")),
            context.Child("title2", static () => Fluent.Title2("Title2 - Semibold 28 / 36")),
            context.Child("title3", static () => Fluent.Title3("Title3 - Semibold 24 / 32")),
            context.Child("large-title", static () => Fluent.LargeTitle("LargeTitle - 40 / 52")),
            context.Child("display", static () => Fluent.Display("Display 68 / 92")));
}

sealed class ChildCounter : UiWidget
{
    private readonly UiState<int> _value = new(0);
    protected override IUiView Build(UiBuildContext context)
    {
        var value = context.Watch(_value);
        var action = context.Child("inherited-action", () => new ChildActionButton("Inherited Fluent action", () => _value.Value++));
        return Dux.Row(12, Fluent.Body1($"Keyed child count {value}"),
            action);
    }
}

sealed class PanelPage : UiWidget
{
    protected override IUiView Build(UiBuildContext context) =>
        context.Child("optional-panel", static () => Dux.Panel("Optional collapsible panel", Dux.Column(12,
            Fluent.Title3("Panel is optional"), Fluent.Body1("Drag the title, collapse/expand or resize. Default pages render directly in the app.")),
            new(Position  : new(24, 24), Size  : new(720, 300), PositionSpace  : UiPanelPositionSpace.WorkArea, AutoHeight  : true)));
}

[UiWidgetStyle(typeof(UiBadge<,,>), typeof(string), typeof(UiTextBadgeBehavior), typeof(UiFluentBadgePresentation<ShowcaseBadgeIcon>))]
public partial class ShowcaseIconBadge;
public readonly struct ShowcaseBadgeIcon : IUiBadgeIcon
{
    public static bool IsPresent => true;
    public static void Paint(UiControlContext context, UiRect bounds, UiColor color)
    {
        context.Line(new(bounds.X + bounds.Width * .2f, bounds.Y + bounds.Height * .5f),
            new(bounds.X + bounds.Width * .43f, bounds.Y + bounds.Height * .73f), color, 1.5f);
        context.Line(new(bounds.X + bounds.Width * .43f, bounds.Y + bounds.Height * .73f),
            new(bounds.X + bounds.Width * .82f, bounds.Y + bounds.Height * .25f), color, 1.5f);
    }
}
sealed class BadgePage : UiWidget
{
    private readonly UiState<int> _count = new(98);
    protected override IUiView Build(UiBuildContext context)
    {
        var colors = new List<IUiView>();
        foreach (var color in Enum.GetValues<UiBadgeColor>())
        {
            var row = new List<IUiView> { Fluent.Body1(color.ToString()) };
            foreach (var appearance in Enum.GetValues<UiBadgeAppearance>())
                row.Add(context.Child("badge-" + color + "-" + appearance, () => Fluent.Badge("Ready", new(Color  : color, Appearance  : appearance))));
            colors.Add(Dux.Row(12, row.ToArray()));
        }
        var sizes = Enum.GetValues<UiBadgeSize>().Select(size => (IUiView)context.Child("size-" + size, () => Fluent.Badge("8", new(size)))).ToArray();
        return Dux.Column(10,
            Fluent.Title2("Badge and CounterBadge"),
            Fluent.Body1("Status badges are passive. Filled, ghost, outline and tint appearances follow each row."),
            Dux.Column(4, colors.ToArray()),
            Fluent.Body1("Medium, tiny, extra-small, small, large, extra-large"), Dux.Row(12, sizes),
            Dux.Row(12, context.Child("increment", () => Fluent.Button("Add one", () => _count.Value++)),
                context.Child("clear", () => Fluent.Button("Reset to zero", () => _count.Value = 0)),
                context.Child("counter", () => Fluent.CounterBadge(() => new(_count.Value))),
                context.Child("zero", () => Fluent.CounterBadge(() => new(_count.Value, ShowZero  : true))),
                context.Child("dot", () => Fluent.CounterBadge(() => new(_count.Value, Dot  : true)))),
            Dux.Row(12, context.Child("icon-before", static () => new ShowcaseIconBadge("Complete")),
                context.Child("icon-after", static () => new ShowcaseIconBadge("Complete", new(IconPosition  : UiBadgeIconPosition.After))),
                context.Child("icon-only", static () => new ShowcaseIconBadge("") { AccessibleLabel = "Completed" })),
            Fluent.Body1("The count overflows at99. Reset hides the first counter; the zero and dot variants remain visible."));
    }
}

[UiWidgetStyle(typeof(UiToggle<,,,>), typeof(bool?), typeof(UiNullableToggleValue), typeof(UiSpaceButtonBehavior), typeof(UiFluentCheckboxPresentation<UiFluentMediumCheckboxSize, UiFluentSquareCheckboxShape, UiCheckboxLabelAfter>))]
public partial class ShowcaseCheckbox0;
[UiWidgetStyle(typeof(UiToggle<,,,>), typeof(bool?), typeof(UiNullableToggleValue), typeof(UiSpaceButtonBehavior), typeof(UiFluentCheckboxPresentation<UiFluentMediumCheckboxSize, UiFluentSquareCheckboxShape, UiCheckboxLabelBefore>))]
public partial class ShowcaseCheckbox1;
[UiWidgetStyle(typeof(UiToggle<,,,>), typeof(bool?), typeof(UiNullableToggleValue), typeof(UiSpaceButtonBehavior), typeof(UiFluentCheckboxPresentation<UiFluentMediumCheckboxSize, UiFluentCircularCheckboxShape, UiCheckboxLabelAfter>))]
public partial class ShowcaseCheckbox2;
[UiWidgetStyle(typeof(UiToggle<,,,>), typeof(bool?), typeof(UiNullableToggleValue), typeof(UiSpaceButtonBehavior), typeof(UiFluentCheckboxPresentation<UiFluentMediumCheckboxSize, UiFluentCircularCheckboxShape, UiCheckboxLabelBefore>))]
public partial class ShowcaseCheckbox3;
[UiWidgetStyle(typeof(UiToggle<,,,>), typeof(bool?), typeof(UiNullableToggleValue), typeof(UiSpaceButtonBehavior), typeof(UiFluentCheckboxPresentation<UiFluentLargeCheckboxSize, UiFluentSquareCheckboxShape, UiCheckboxLabelAfter>))]
public partial class ShowcaseCheckbox4;
[UiWidgetStyle(typeof(UiToggle<,,,>), typeof(bool?), typeof(UiNullableToggleValue), typeof(UiSpaceButtonBehavior), typeof(UiFluentCheckboxPresentation<UiFluentLargeCheckboxSize, UiFluentSquareCheckboxShape, UiCheckboxLabelBefore>))]
public partial class ShowcaseCheckbox5;
[UiWidgetStyle(typeof(UiToggle<,,,>), typeof(bool?), typeof(UiNullableToggleValue), typeof(UiSpaceButtonBehavior), typeof(UiFluentCheckboxPresentation<UiFluentLargeCheckboxSize, UiFluentCircularCheckboxShape, UiCheckboxLabelAfter>))]
public partial class ShowcaseCheckbox6;
[UiWidgetStyle(typeof(UiToggle<,,,>), typeof(bool?), typeof(UiNullableToggleValue), typeof(UiSpaceButtonBehavior), typeof(UiFluentCheckboxPresentation<UiFluentLargeCheckboxSize, UiFluentCircularCheckboxShape, UiCheckboxLabelBefore>))]
public partial class ShowcaseCheckbox7;

sealed class CheckboxPage : UiWidget
{
    private readonly UiState<bool?> _value = new(null);
    protected override IUiView Build(UiBuildContext context)
    {
        var value = context.Watch(_value);
        var choice0 = context.Child("checkbox-0", () => new ShowcaseCheckbox0("Medium square, label after", _value.Binding));
        var choice1 = context.Child("checkbox-1", () => new ShowcaseCheckbox1("Medium square, label before", _value.Binding));
        var choice2 = context.Child("checkbox-2", () => new ShowcaseCheckbox2("Medium circular, label after", _value.Binding));
        var choice3 = context.Child("checkbox-3", () => new ShowcaseCheckbox3("Medium circular, label before", _value.Binding));
        var choice4 = context.Child("checkbox-4", () => new ShowcaseCheckbox4("Large square, label after", _value.Binding));
        var choice5 = context.Child("checkbox-5", () => new ShowcaseCheckbox5("Large square, label before", _value.Binding));
        var choice6 = context.Child("checkbox-6", () => new ShowcaseCheckbox6("Large circular, label after", _value.Binding));
        var choice7 = context.Child("checkbox-7", () => new ShowcaseCheckbox7("Large circular, label before", _value.Binding));
        var disabled = context.Child("checkbox-disabled", () => Fluent.Checkbox("Disabled", _value.Binding));
        return Dux.Column(12, Fluent.Title2("Checkbox"),
            Fluent.Body1("Use the label or indicator, or focus with Tab and toggle with Space."),
            Fluent.Body1(value is null ? "Mixed selection" : value == true ? "Selected" : "Not selected"),
            choice0, choice1, choice2, choice3,
            choice4, choice5, choice6, choice7,
            disabled.Disabled());
    }
}

[UiWidgetStyle(typeof(UiRadioGroup<,>), typeof(UiFluentRadioGroupBehavior), typeof(UiFluentRadioGroupPresentation<UiRadioHorizontalLayout>))]
public partial class ShowcaseHorizontalRadioGroup;
[UiWidgetStyle(typeof(UiRadioGroup<,>), typeof(UiFluentRadioGroupBehavior), typeof(UiFluentRadioGroupPresentation<UiRadioHorizontalStackedLayout>))]
public partial class ShowcaseStackedRadioGroup;

sealed class RadioPage : UiWidget
{
    private readonly UiState<string?> _selected = new("balanced");
    private static readonly UiRadioOption[] Choices =
    [
        new("efficient", "Efficient", Description  : "Prefer shorter processing time."),
        new("balanced", "Balanced", Description  : "Keep a balance of speed and quality."),
        new("quality", "Quality", Description  : "Prefer maximum output quality."),
        new("unavailable", "Unavailable", false)
    ];
    protected override IUiView Build(UiBuildContext context)
    {
        var selected = context.Watch(_selected);
        var vertical = context.Child("radio-vertical", () => Fluent.RadioGroup("Vertical", _selected.Binding, Choices));
        var horizontal = context.Child("radio-horizontal", () => new ShowcaseHorizontalRadioGroup("Horizontal", _selected.Binding, Choices));
        var stacked = context.Child("radio-stacked", () => new ShowcaseStackedRadioGroup("Labels below", _selected.Binding, Choices));
        return Dux.Column(12, Fluent.Title2("Radio group"),
            Fluent.Body1("Tab visits each group once. Arrow keys move selection; Space selects the focused option."),
            Fluent.Body1("Selected : " + selected), vertical, horizontal, stacked);
    }
}

sealed class SwitchPage : UiWidget
{
    private readonly UiState<bool> _value = new(false);
    protected override IUiView Build(UiBuildContext context)
    {
        context.Watch(_value);
        var switch0 = context.Child("switch-0", () => new ShowcaseSwitch0("Medium, label after", _value.Binding));
        var switch1 = context.Child("switch-1", () => new ShowcaseSwitch1("Medium, label before", _value.Binding));
        var switch2 = context.Child("switch-2", () => new ShowcaseSwitch2("Medium, label above", _value.Binding));
        var switch3 = context.Child("switch-3", () => new ShowcaseSwitch3("Small, label after", _value.Binding));
        var switch4 = context.Child("switch-4", () => new ShowcaseSwitch4("Small, label before", _value.Binding));
        var switch5 = context.Child("switch-5", () => new ShowcaseSwitch5("Small, label above", _value.Binding));
        var disabled = context.Child("switch-disabled", () => Fluent.Switch("Disabled", _value.Binding));
        return Dux.Column(Fluent.Title2("Switch : pointer and keyboard"), Dux.Text($"Value : {_value.Value}"),
            switch0, switch1, switch2, switch3, switch4, switch5, disabled.Disabled(),
            Dux.Text("Tab to focus. Space to toggle. All switches share the value."));
    }
}

[UiWidgetStyle(typeof(UiToggle<,,,>), typeof(bool), typeof(UiBooleanToggleValue), typeof(UiSpaceButtonBehavior), typeof(UiFluentSwitchPresentation<UiFluentMediumSwitchSize, UiSwitchLabelAfter>))]
partial class ShowcaseSwitch0;

[UiWidgetStyle(typeof(UiToggle<,,,>), typeof(bool), typeof(UiBooleanToggleValue), typeof(UiSpaceButtonBehavior), typeof(UiFluentSwitchPresentation<UiFluentMediumSwitchSize, UiSwitchLabelBefore>))]
partial class ShowcaseSwitch1;

[UiWidgetStyle(typeof(UiToggle<,,,>), typeof(bool), typeof(UiBooleanToggleValue), typeof(UiSpaceButtonBehavior), typeof(UiFluentSwitchPresentation<UiFluentMediumSwitchSize, UiSwitchLabelAbove>))]
partial class ShowcaseSwitch2;

[UiWidgetStyle(typeof(UiToggle<,,,>), typeof(bool), typeof(UiBooleanToggleValue), typeof(UiSpaceButtonBehavior), typeof(UiFluentSwitchPresentation<UiFluentSmallSwitchSize, UiSwitchLabelAfter>))]
partial class ShowcaseSwitch3;

[UiWidgetStyle(typeof(UiToggle<,,,>), typeof(bool), typeof(UiBooleanToggleValue), typeof(UiSpaceButtonBehavior), typeof(UiFluentSwitchPresentation<UiFluentSmallSwitchSize, UiSwitchLabelBefore>))]
partial class ShowcaseSwitch4;

[UiWidgetStyle(typeof(UiToggle<,,,>), typeof(bool), typeof(UiBooleanToggleValue), typeof(UiSpaceButtonBehavior), typeof(UiFluentSwitchPresentation<UiFluentSmallSwitchSize, UiSwitchLabelAbove>))]
partial class ShowcaseSwitch5;

sealed class LinkPage : UiWidget
{
    private readonly UiState<string> _route = new("/");
    private void Navigate(Uri destination) => _route.Value = destination.OriginalString;
    protected override IUiView Build(UiBuildContext context)
    {
        context.Watch(_route);
        if (_route.Value != "/")
            return Dux.Column(
            Fluent.Title2(_route.Value == "/guide" ? "Guide" : "Reference"),
            Dux.Text("This is an actual internal navigation destination built from retained state."),
            context.Child("back", () => Fluent.Link("Back to the Link gallery", new Uri("/", UriKind.Relative), Navigate)));
        var link0 = context.Child("link-0", () => new ShowcaseLink0("Guide (Default, standalone)", new Uri("/guide", UriKind.Relative), Navigate));
        var link1 = context.Child("link-1", () => new ShowcaseLink1("Guide (Default, inline)", new Uri("/guide", UriKind.Relative), Navigate));
        var link2 = context.Child("link-2", () => new ShowcaseLink2("Guide (Subtle, standalone)", new Uri("/guide", UriKind.Relative), Navigate));
        var link3 = context.Child("link-3", () => new ShowcaseLink3("Guide (Subtle, inline)", new Uri("/guide", UriKind.Relative), Navigate));
        var link4 = context.Child("link-4", () => new ShowcaseLink4("Guide (Inverted, standalone)", new Uri("/guide", UriKind.Relative), Navigate));
        var link5 = context.Child("link-5", () => new ShowcaseLink5("Guide (Inverted, inline)", new Uri("/guide", UriKind.Relative), Navigate));
        var link6 = context.Child("link-6", () => new ShowcaseLink6("Guide (OnBrand, standalone)", new Uri("/guide", UriKind.Relative), Navigate));
        var link7 = context.Child("link-7", () => new ShowcaseLink7("Guide (OnBrand, inline)", new Uri("/guide", UriKind.Relative), Navigate));
        var disabled = context.Child("disabled", () => Fluent.Link("Disabled destination", new Uri("/reference", UriKind.Relative), Navigate));
        return Dux.Column(Fluent.Title2("Link : navigation, Enter and pointer"),
            Dux.Text("Each active link navigates to the guide. Space does not activate a navigation link."),
            link0, link1, link2, link3, link4, link5, link6, link7, disabled.Disabled());
    }
}

[UiWidgetStyle(typeof(UiLink<,>), typeof(UiLinkNavigationBehavior), typeof(UiFluentLinkPresentation<UiFluentDefaultLinkAppearance, UiStandaloneLinkUnderline>))]
partial class ShowcaseLink0;

[UiWidgetStyle(typeof(UiLink<,>), typeof(UiLinkNavigationBehavior), typeof(UiFluentLinkPresentation<UiFluentDefaultLinkAppearance, UiInlineLinkUnderline>))]
partial class ShowcaseLink1;

[UiWidgetStyle(typeof(UiLink<,>), typeof(UiLinkNavigationBehavior), typeof(UiFluentLinkPresentation<UiFluentSubtleLinkAppearance, UiStandaloneLinkUnderline>))]
partial class ShowcaseLink2;

[UiWidgetStyle(typeof(UiLink<,>), typeof(UiLinkNavigationBehavior), typeof(UiFluentLinkPresentation<UiFluentSubtleLinkAppearance, UiInlineLinkUnderline>))]
partial class ShowcaseLink3;

[UiWidgetStyle(typeof(UiLink<,>), typeof(UiLinkNavigationBehavior), typeof(UiFluentLinkPresentation<UiFluentInvertedLinkAppearance, UiStandaloneLinkUnderline>))]
partial class ShowcaseLink4;

[UiWidgetStyle(typeof(UiLink<,>), typeof(UiLinkNavigationBehavior), typeof(UiFluentLinkPresentation<UiFluentInvertedLinkAppearance, UiInlineLinkUnderline>))]
partial class ShowcaseLink5;

[UiWidgetStyle(typeof(UiLink<,>), typeof(UiLinkNavigationBehavior), typeof(UiFluentLinkPresentation<UiFluentOnBrandLinkAppearance, UiStandaloneLinkUnderline>))]
partial class ShowcaseLink6;

[UiWidgetStyle(typeof(UiLink<,>), typeof(UiLinkNavigationBehavior), typeof(UiFluentLinkPresentation<UiFluentOnBrandLinkAppearance, UiInlineLinkUnderline>))]
partial class ShowcaseLink7;

sealed class InputPage : UiWidget
{
    private readonly UiState<string> _value = new("");
    protected override IUiView Build(UiBuildContext context)
    {
        context.Watch(_value);
        var input0 = context.Child("input-0", () => new ShowcaseInput0("Small Outline", _value.Binding, 256, 220) { Options = new(Placeholder  : "Type here") });
        var input1 = context.Child("input-1", () => new ShowcaseInput1("Small Underline", _value.Binding, 256, 220) { Options = new(Placeholder  : "Type here") });
        var input2 = context.Child("input-2", () => new ShowcaseInput2("Small FilledDarker", _value.Binding, 256, 220) { Options = new(Placeholder  : "Type here") });
        var input3 = context.Child("input-3", () => new ShowcaseInput3("Small FilledLighter", _value.Binding, 256, 220) { Options = new(Placeholder  : "Type here") });
        var input4 = context.Child("input-4", () => new ShowcaseInput4("Medium Outline", _value.Binding, 256, 220) { Options = new(Placeholder  : "Type here") });
        var input5 = context.Child("input-5", () => new ShowcaseInput5("Medium Underline", _value.Binding, 256, 220) { Options = new(Placeholder  : "Type here") });
        var input6 = context.Child("input-6", () => new ShowcaseInput6("Medium FilledDarker", _value.Binding, 256, 220) { Options = new(Placeholder  : "Type here") });
        var input7 = context.Child("input-7", () => new ShowcaseInput7("Medium FilledLighter", _value.Binding, 256, 220) { Options = new(Placeholder  : "Type here") });
        var input8 = context.Child("input-8", () => new ShowcaseInput8("Large Outline", _value.Binding, 256, 220) { Options = new(Placeholder  : "Type here") });
        var input9 = context.Child("input-9", () => new ShowcaseInput9("Large Underline", _value.Binding, 256, 220) { Options = new(Placeholder  : "Type here") });
        var input10 = context.Child("input-10", () => new ShowcaseInput10("Large FilledDarker", _value.Binding, 256, 220) { Options = new(Placeholder  : "Type here") });
        var input11 = context.Child("input-11", () => new ShowcaseInput11("Large FilledLighter", _value.Binding, 256, 220) { Options = new(Placeholder  : "Type here") });
        var readOnly = context.Child("readonly", () => Fluent.Input("Readonly", _value.Binding, 256, 220));
        readOnly.Options = new(UiInputTextFlags.ReadOnly | UiInputTextFlags.AutoSelectAll, "Readonly");
        var disabled = context.Child("disabled", () => Fluent.Input("Disabled", _value.Binding, 256, 220));
        disabled.Options = new(Placeholder  : "Disabled");
        var amount = context.Child("adornment-amount", () => Fluent.Input("Amount", _value.Binding, 256, 220));
        amount.Options = new(Placeholder  : "Amount")
        {
            ContentBefore = "$",
            ContentAfter = "USD"
        };
        var search = context.Child("adornment-search", () => new ShowcaseSearchInput("Search", _value.Binding, 256, 220));
        search.Options = new(Placeholder  : "Search")
        {
            ContentAfter = "items"
        };
        var editable = context.Child("interactive-slot-input", () =>
        {
            var input = Fluent.Input("Clearable value", _value.Binding, 256, 220);
            input.Options = new(Placeholder  : "Type, then clear");
            var clear = new ShowcaseClearInputButton("##Clear", () => _value.Value = "") { Size = new(24, 24), AccessibleName = "Clear" };
            input.AfterSlot = new("clear", clear, new(24, 24));
            return input;
        });
        return Dux.Column(Fluent.Title2("Input : shared editing and static presentations"), Dux.Text($"Value : {_value.Value}"),
            Dux.Text("Outline : small / medium / large"), Dux.Row(input0, input4, input8),
            Dux.Text("Underline : small / medium / large"), Dux.Row(input1, input5, input9),
            Dux.Text("Filled darker : small / medium / large"), Dux.Row(input2, input6, input10),
            Dux.Text("Filled lighter : small / medium / large"), Dux.Row(input3, input7, input11),
            Dux.Text("Readonly selection/copy and disabled"), Dux.Row(readOnly, disabled.Disabled()),
            Dux.Text("Text, source-generated icon, and real clear button"), Dux.Row(amount, search, editable),
            Dux.Text("Tab/Shift+Tab to move focus. All editable inputs share the value."));
    }
}

[UiWidgetStyle(typeof(UiInput<,>), typeof(UiFluentTextInputBehavior), typeof(UiFluentInputPresentation<UiFluentSmallInputSize, UiFluentOutlineInput>))]
partial class ShowcaseInput0;

[UiWidgetStyle(typeof(UiInput<,>), typeof(UiFluentTextInputBehavior), typeof(UiFluentInputPresentation<UiFluentSmallInputSize, UiFluentUnderlineInput>))]
partial class ShowcaseInput1;

[UiWidgetStyle(typeof(UiInput<,>), typeof(UiFluentTextInputBehavior), typeof(UiFluentInputPresentation<UiFluentSmallInputSize, UiFluentFilledDarkerInput>))]
partial class ShowcaseInput2;

[UiWidgetStyle(typeof(UiInput<,>), typeof(UiFluentTextInputBehavior), typeof(UiFluentInputPresentation<UiFluentSmallInputSize, UiFluentFilledLighterInput>))]
partial class ShowcaseInput3;

[UiWidgetStyle(typeof(UiInput<,>), typeof(UiFluentTextInputBehavior), typeof(UiFluentInputPresentation<UiFluentMediumInputSize, UiFluentOutlineInput>))]
partial class ShowcaseInput4;

[UiWidgetStyle(typeof(UiInput<,>), typeof(UiFluentTextInputBehavior), typeof(UiFluentInputPresentation<UiFluentMediumInputSize, UiFluentUnderlineInput>))]
partial class ShowcaseInput5;

[UiWidgetStyle(typeof(UiInput<,>), typeof(UiFluentTextInputBehavior), typeof(UiFluentInputPresentation<UiFluentMediumInputSize, UiFluentFilledDarkerInput>))]
partial class ShowcaseInput6;

[UiWidgetStyle(typeof(UiInput<,>), typeof(UiFluentTextInputBehavior), typeof(UiFluentInputPresentation<UiFluentMediumInputSize, UiFluentFilledLighterInput>))]
partial class ShowcaseInput7;

[UiWidgetStyle(typeof(UiInput<,>), typeof(UiFluentTextInputBehavior), typeof(UiFluentInputPresentation<UiFluentLargeInputSize, UiFluentOutlineInput>))]
partial class ShowcaseInput8;

[UiWidgetStyle(typeof(UiInput<,>), typeof(UiFluentTextInputBehavior), typeof(UiFluentInputPresentation<UiFluentLargeInputSize, UiFluentUnderlineInput>))]
partial class ShowcaseInput9;

[UiWidgetStyle(typeof(UiInput<,>), typeof(UiFluentTextInputBehavior), typeof(UiFluentInputPresentation<UiFluentLargeInputSize, UiFluentFilledDarkerInput>))]
partial class ShowcaseInput10;

[UiWidgetStyle(typeof(UiInput<,>), typeof(UiFluentTextInputBehavior), typeof(UiFluentInputPresentation<UiFluentLargeInputSize, UiFluentFilledLighterInput>))]
partial class ShowcaseInput11;

[UiFluentIcon("search", UiFluentIconStyle.Regular, 20)]
public readonly partial struct ShowcaseSearchIcon;
[UiWidgetStyle(typeof(UiInput<,>), typeof(UiFluentTextInputBehavior), typeof(UiFluentInputPresentation<UiFluentMediumInputSize, UiFluentOutlineInput, UiInputIconAdornment<ShowcaseSearchIcon>, UiInputTextAdornment>))]
public partial class ShowcaseSearchInput;

[UiFluentIcon("dismiss", UiFluentIconStyle.Regular, 20)]
public readonly partial struct ShowcaseDismissIcon;
[UiWidgetStyle(typeof(UiButton<,>), typeof(UiReleaseButtonBehavior), typeof(UiFluentIconButtonPresentation<UiFluentSmallButtonIconSize, UiFluentTransparentButtonAppearance, ShowcaseDismissIcon, UiButtonIconBefore>))]
public partial class ShowcaseClearInputButton;
