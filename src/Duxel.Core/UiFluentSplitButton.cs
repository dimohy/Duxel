namespace Duxel.Core;

/// <summary>A Fluent button with a primary action and a separate menu trigger.</summary>
public class UiSplitButton<TBehavior> : UiMenu<UiFluentMenuPresentation> where TBehavior : IUiButtonBehavior
{
    private Action? _activate;
    public string MenuLabel { get; }

    public UiSplitButton(string label, string menuLabel, Action activate, ReadOnlySpan<UiMenuOption> items,
        Action<string> invoked, UiMenuOptions? options = null) : base(label, items, invoked, options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(menuLabel);
        ArgumentNullException.ThrowIfNull(activate);
        MenuLabel = menuLabel;
        _activate = activate;
    }

    protected override UiVector2 MeasureContent(UiControlContext context) => UiImmediateContext.MeasureSplitButton(context, Label).Size;

    private protected override string? RenderMenuControl(UiImmediateContext ui, UiMenuOption[] items,
        UiMenuOptions options, UiMenuInputState input)
    {
        var id = PrimaryInputId(Label);
        var result = ui.RenderSplitButton<TBehavior>(id, Label, MenuLabel, items, options, input);
        if (result.Activated)
        {
            using var activation = ui.BeginControlActivation(id);
            _activate?.Invoke();
        }
        return result.Item;
    }

    private protected override void ReleaseOwnedResources()
    {
        _activate = null;
        base.ReleaseOwnedResources();
    }
}

[UiWidgetStyle(typeof(UiSplitButton<>), typeof(UiReleaseButtonBehavior))]
[UiComponent("SplitButton", UiComponentExposure.Fluent)]
public partial class UiFluentSplitButton;

public sealed partial class UiImmediateContext
{
    private const float SplitMenuWidth = 32;

    internal static (UiVector2 Size, UiVector2 Text) MeasureSplitButton(UiControlContext context, string label)
    {
        using var typography = UiFluentButtonPresentation.BeginText(context, true);
        var text = context.MeasureText(GetDisplayLabel(label));
        var primary = UiFluentButtonPresentation.Measure(context, text, default);
        return (primary with { X = primary.X + SplitMenuWidth }, text);
    }

    private void PaintSplitSegment(UiControlContext context, UiRect bounds, UiRect segment, UiButtonInteraction interaction,
        string? label, UiVector2 textSize)
    {
        if (segment.Width <= 0 || segment.Height <= 0)
            return;

        using (context.Clip(segment))
        {
            var color = UiFluentSecondaryButtonAppearance.Paint(context, bounds, interaction with { FocusVisible = false });
            if (label is null)
                UiMenuButtonChevron.Paint(context, new(segment.X + (segment.Width - 16) * .5f,
                    segment.Y + (segment.Height - 16) * .5f, 16, 16), color, interaction);
            else
            {
                using var typography = UiFluentButtonPresentation.BeginText(context, true);
                context.TypographyButtonText(GetDisplayLabel(label), segment, textSize, color);
            }
        }

        if (context.IsEnabled && interaction.FocusVisible)
            context.FocusOutline(segment, 4, context.Theme.ForcedColors?.Highlight ?? context.Theme.Text);
    }

    internal (bool Activated, string? Item) RenderSplitButton<TBehavior>(string id, string label, string menuLabel,
        UiMenuOption[] items, UiMenuOptions options, UiMenuInputState input) where TBehavior : IUiButtonBehavior
    {
        var context = new UiControlContext(this);
        var layout = MeasureSplitButton(context, label);
        var size = ConstrainToLayout(layout.Size);
        var origin = AllocateLayoutBox(size);
        var bounds = new UiRect(origin.X, origin.Y, size.X, size.Y);
        var menuWidth = MathF.Min(SplitMenuWidth, size.X * .5f);
        var primaryWidth = size.X - menuWidth;
        var rtl = context.RightToLeft || options.RightToLeft;
        var primary = new UiRect(origin.X + (rtl ? menuWidth : 0), origin.Y, primaryWidth, size.Y);
        var menu = new UiRect(origin.X + (rtl ? 0 : primaryWidth), origin.Y, menuWidth, size.Y);

        var primaryAction = ProcessPlacedButton<TBehavior>(context, id, primary, out var resolvedId);
        PublishPlacedButton<TBehavior>(resolvedId, label, primary);
        var menuId = ResolveCachedIdentifier(id, "##split-menu", false);
        var widget = SwapAccessibilityWidget(null);
        UiButtonInteraction menuAction;
        try { menuAction = ProcessMenuTrigger(menuId, menuLabel, items, input, menu); }
        finally { SwapAccessibilityWidget(widget); }

        PaintSplitSegment(context, bounds, primary, primaryAction, label, layout.Text);
        PaintSplitSegment(context, bounds, menu, menuAction, null, default);
        var border = context.Theme.ForcedColors is { } colors
            ? UiFluentButtonPalette.ResolveForced(colors, UiFluentButtonFrame.State(context, primaryAction),
                UiFluentButtonAppearanceKind.Secondary).Border
            : UiFluentButtonPalette.Resolve(context.FluentColorScheme, UiFluentButtonFrame.State(context, menuAction)).Border;
        var divider = rtl ? primary.X : menu.X;
        if (menuWidth > 0 && size.Y > 2)
            context.Frame(new(divider, origin.Y + 1, 1, size.Y - 2), border, default, 0, 0);

        FinishLayoutBox(origin, size);
        var invoked = RenderMenuPopup<UiFluentMenuPresentation>(menuId, menuLabel, items, options, input, menu, menuAction);
        return (primaryAction.Activated, invoked);
    }
}
