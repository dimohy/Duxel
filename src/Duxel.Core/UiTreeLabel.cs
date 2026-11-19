namespace Duxel.Core;

/// <summary>A presentation-owned literal label. Truncation refers to the latest measured allocation.</summary>
public abstract class UiTreeLabel : UiControlWidget
{
    private Func<UiTreeOptions>? _options;

    protected UiTreeLabel(string text, Func<UiTreeOptions> options) : base(UiWidgetIdentity.Parent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        ArgumentNullException.ThrowIfNull(options);
        Text = text;
        _options = options;
    }

    public string Text { get; }
    public abstract bool IsTruncated { get; }
    protected UiTreeOptions Options => (_options ?? throw new ObjectDisposedException(nameof(UiTreeLabel)))();
    protected UiTreeOptions LayoutOptions(UiControlContext context) => Options.WithInheritedDirection(context.RightToLeft);
    protected UiButtonInteraction Interaction { get; private set; }
    internal void SetInteraction(UiButtonInteraction interaction) => Interaction = interaction;

    private protected override void ReleaseOwnedResources()
    {
        _options = null;
        base.ReleaseOwnedResources();
    }
}

internal sealed class UiTreeLabelSlot : UiControlWidget
{
    private UiTreeLabel? _label;
    internal string Text { get; }
    internal UiTreeLabel Label => _label ?? throw new InvalidOperationException("A plain Tree label requires its owning Tree presentation.");
    internal UiTreeLabelSlot(string text) : base(UiWidgetIdentity.Parent) => Text = text;
    internal void Initialize(UiTreeLabel label)
    {
        if (_label is not null || label.Text != Text)
            throw new InvalidOperationException("A Tree presentation must create one label preserving the original text.");
        OwnChild("label", label);
        _label = label;
    }
    protected override bool CanRetainMeasurement => _label?.RetainsMeasurement == true;
    protected override UiVector2 MeasureContent(UiControlContext context) => context.MeasureConstrained(Label, context.LayoutConstraints);
    protected override void RenderControl(UiImmediateContext ui) => Label.Render(ui);
    private protected override void ReleaseOwnedResources()
    {
        _label = null;
        base.ReleaseOwnedResources();
    }
}
