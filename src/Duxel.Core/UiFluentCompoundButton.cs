namespace Duxel.Core;

/// <summary>A Fluent button with a title and secondary description in one activation surface.</summary>
public class UiCompoundButton<TBehavior> : UiButton<TBehavior, UiFluentButtonPresentation>
    where TBehavior : IUiButtonBehavior
{
    private string _description;

    public UiCompoundButton(string label, string description, Action? activated = null,
        UiWidgetIdentity identity = UiWidgetIdentity.Instance) : base(label, activated, identity)
    {
        ValidateDescription(description);
        _description = description;
    }

    public string Description
    {
        get => _description;
        set
        {
            ThrowIfDisposed();
            ValidateDescription(value);
            if (string.Equals(_description, value, StringComparison.Ordinal))
                return;
            _description = value;
            RequestRebuild();
        }
    }

    protected override UiVector2 MeasureContent(UiControlContext context) =>
        UiImmediateContext.MeasureCompoundButton(context, Label, Description, Size).Size;

    private protected override bool RenderButton(UiImmediateContext ui, string id) =>
        ui.RenderCompoundButton<TBehavior>(id, Label, Description, Size);

    private static void ValidateDescription(string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        if (UiAccessibilityTree.ContainsControl(description, multiline : false))
            throw new ArgumentException("A compound button description must be single-line text.", nameof(description));
    }
}

[UiWidgetStyle(typeof(UiCompoundButton<>), typeof(UiReleaseButtonBehavior))]
[UiComponent("CompoundButton", UiComponentExposure.Fluent)]
public partial class UiFluentCompoundButton;

public sealed partial class UiImmediateContext
{
    private const float CompoundPaddingX = 12;
    private const float CompoundPaddingY = 8;
    private const float CompoundLineGap = 2;

    internal static (UiVector2 Size, UiVector2 Title, UiVector2 Description) MeasureCompoundButton(
        UiControlContext context, string label, string description, UiVector2 size)
    {
        UiVector2 titleSize;
        using (var titleFont = UiFluentButtonPresentation.BeginText(context, true))
            titleSize = new(context.MeasureText(GetDisplayLabel(label)).X, titleFont.LineHeight);
        UiVector2 descriptionSize;
        using (var descriptionFont = context.BeginTypography<UiRegularTypeface, UiFluentType200>())
            descriptionSize = new(context.MeasureText(description).X, descriptionFont.LineHeight);
        var width = MathF.Max(titleSize.X, descriptionSize.X) + CompoundPaddingX * 2;
        var height = titleSize.Y + descriptionSize.Y + CompoundLineGap + CompoundPaddingY * 2;
        return (new(size.X > 0 ? size.X : MathF.Max(96, width), size.Y > 0 ? size.Y : height), titleSize, descriptionSize);
    }

    internal bool RenderCompoundButton<TBehavior>(string id, string label, string description, UiVector2 size)
        where TBehavior : IUiButtonBehavior
    {
        var context = new UiControlContext(this);
        var layout = MeasureCompoundButton(context, label, description, size);
        var measured = ConstrainToLayout(layout.Size);
        var origin = AdvanceCursor(measured);
        var bounds = new UiRect(origin.X, origin.Y, measured.X, measured.Y);
        var interaction = ProcessPlacedButton<TBehavior>(context, id, bounds, out var resolvedId);
        var foreground = UiFluentSecondaryButtonAppearance.Paint(context, bounds, interaction);

        var inset = MathF.Min(CompoundPaddingX, bounds.Width * .5f);
        var inner = new UiRect(bounds.X + inset, bounds.Y, bounds.Width - inset * 2, bounds.Height);
        var height = layout.Title.Y + CompoundLineGap + layout.Description.Y;
        var y = inner.Y + MathF.Max(0, (inner.Height - height) * .5f);
        float LineX(float width) => context.RightToLeft ? inner.X + inner.Width - width : inner.X;
        using (context.Clip(inner))
        {
            using (UiFluentButtonPresentation.BeginText(context, true))
                context.TypographyButtonText(GetDisplayLabel(label), new(LineX(layout.Title.X), y, layout.Title.X, layout.Title.Y),
                    layout.Title, foreground);
            using (context.BeginTypography<UiRegularTypeface, UiFluentType200>())
                context.TypographyButtonText(description, new(LineX(layout.Description.X), y + layout.Title.Y + CompoundLineGap,
                    layout.Description.X, layout.Description.Y), layout.Description, foreground);
        }
        PublishPlacedButton<TBehavior>(resolvedId, label, bounds, description);
        return interaction.Activated;
    }
}
