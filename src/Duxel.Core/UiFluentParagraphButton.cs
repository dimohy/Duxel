namespace Duxel.Core;

/// <summary>A literal paragraph label using the shared Fluent frame, activation and accessibility contracts.</summary>
internal sealed class UiFluentParagraphButton : UiControlWidget
{
    private Action? _activated;
    internal readonly UiInlineText Text;

    internal UiFluentParagraphButton(string label, Action activated)
    {
        _activated = activated;
        Text = new([new(label, UiInlineTextWeight.Semibold)]) { HorizontalAlignment = UiItemHorizontalAlign.Center };
        OwnChild("label", Text);
    }

    internal string Label
    {
        get => Text.Text;
        set
        {
            ThrowIfDisposed();
            if (Text.Text == value) return;
            Text.Runs = [new(value, UiInlineTextWeight.Semibold)];
            RequestRebuild();
        }
    }

    internal bool RightToLeft
    {
        get => Text.RightToLeft;
        set
        {
            ThrowIfDisposed();
            if (Text.RightToLeft == value) return;
            Text.RightToLeft = value;
            RequestRebuild();
        }
    }

    internal UiVector2 MeasureLabel(UiControlContext context, float width) =>
        context.MeasureParentDataChild(Text, null, new(0, MathF.Max(0, width - 26), 0, float.PositiveInfinity));

    internal UiVector2 MeasureButton(UiControlContext context)
    {
        var label = MeasureLabel(context, context.LayoutConstraints.MaxWidth);
        return context.ConstrainToLayout(UiFluentButtonPresentation.Measure(context, label, default));
    }

    internal void Activate() => _activated?.Invoke();
    protected override bool CanRetainMeasurement => true;
    protected override UiVector2 MeasureContent(UiControlContext context) => MeasureButton(context);
    protected override void RenderControl(UiImmediateContext ui) => ui.RenderFluentParagraphButton(this, PrimaryControlId);
    private protected override void ReleaseOwnedResources()
    {
        _activated = null;
        base.ReleaseOwnedResources();
    }
}

public sealed partial class UiImmediateContext
{
    internal void RenderFluentParagraphButton(UiFluentParagraphButton widget, string id)
    {
        var context = new UiControlContext(this);
        var size = widget.MeasureButton(context);
        var origin = AllocateLayoutBox(size);
        var bounds = new UiRect(origin.X, origin.Y, size.X, size.Y);
        var interaction = UiSpaceButtonBehavior.Process(context, id, bounds);
        var resolved = ResolveId(id);
        ClaimAccessibilityField(resolved);
        interaction = ApplyAccessibilityInvoke(resolved, bounds, interaction);
        var foreground = UiFluentSecondaryButtonAppearance.Paint(context, bounds, interaction);
        var label = widget.MeasureLabel(context, size.X);
        var position = new UiVector2(bounds.X + (bounds.Width - label.X) / 2, bounds.Y + (bounds.Height - label.Y) / 2);
        PaintInlineVisualLines(widget.Text.VisualLines, position, foreground, IntersectRect(CurrentClipRect, bounds),
            widget.Text.VisualLines.AlignmentOffsets(label.X, UiItemHorizontalAlign.Center));
        ExposeAccessibility(resolved, _accessibilityParent, UiAccessibilityRole.Button, widget.Label, bounds, focusable : true);
        FinishLayoutBox(origin, size);
        if (interaction.Activated)
        {
            using var activation = BeginControlActivation(id);
            widget.Activate();
        }
    }
}
