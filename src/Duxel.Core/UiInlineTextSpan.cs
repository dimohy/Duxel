namespace Duxel.Core;

/// <summary>An owned literal span that also renders independently with the same identity and typography.</summary>
[UiComponent("InlineTextSpan", UiComponentExposure.Infrastructure)]
public class UiInlineTextSpan : UiControlWidget
{
    private readonly UiInlineTextSource _source = new();
    private readonly UiVisualTextLines _lines = new();
    private readonly string _suffix;
    private readonly bool _suffixOnEmpty;
    private string _text;
    private UiInlineTextWeight _weight;
    private string? _accessibilityIdentity;
    internal string? LastAccessibilityId
    {
        get; private set;
    }
    internal string DisplayText => _source.Source;
    internal bool IsInlineLiteral => Margin == default && Transform.IsIdentity;

    public UiInlineTextSpan(string text, UiInlineTextWeight weight = UiInlineTextWeight.Regular,
        UiWidgetIdentity identity = UiWidgetIdentity.Instance) : this(text, weight, string.Empty, identity) { }
    private protected UiInlineTextSpan(string text, UiInlineTextWeight weight, string suffix, UiWidgetIdentity identity)
        : this(text, weight, suffix, true, identity) { }
    private UiInlineTextSpan(string text, UiInlineTextWeight weight, string suffix, bool suffixOnEmpty, UiWidgetIdentity identity)
        : base(identity)
    {
        ArgumentNullException.ThrowIfNull(text);
        _source.Replace([new(text + (text.Length > 0 || suffixOnEmpty ? suffix : string.Empty), weight)]);
        _text = text;
        _weight = weight;
        _suffix = suffix;
        _suffixOnEmpty = suffixOnEmpty;
    }
    internal static UiInlineTextSpan WithTrailingSpace(string text) => new(text, UiInlineTextWeight.Regular, " ", false, UiWidgetIdentity.Instance);
    private string Display(string text) => text + (text.Length > 0 || _suffixOnEmpty ? _suffix : string.Empty);

    public string Text
    {
        get => _text;
        set
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(value);
            if (_text == value)
                return;
            ThrowIfBuilding();
            _source.Replace([new(Display(value), _weight)]);
            _text = value;
            RequestRebuild();
        }
    }
    public UiInlineTextWeight Weight
    {
        get => _weight;
        set
        {
            ThrowIfDisposed();
            if (_weight == value)
                return;
            ThrowIfBuilding();
            _source.Replace([new(Display(_text), value)]);
            _weight = value;
            RequestRebuild();
        }
    }
    protected override bool CanRetainMeasurement => true;
    protected sealed override UiVector2 MeasureContent(UiControlContext context) => context.MeasureInlineText(_source, _lines, true);
    protected sealed override void RenderControl(UiImmediateContext ui)
        => LastAccessibilityId = ui.RenderInlineText(_source, _lines, true, null, ref _accessibilityIdentity, _text);
    protected sealed override void RenderContent(UiImmediateContext ui) => RenderControl(ui);
    internal void RenderPrepared(UiImmediateContext ui, string text)
        => LastAccessibilityId = ui.CaptureInlineSpan(ref _accessibilityIdentity, text);

    private protected override void ReleaseOwnedResources()
    {
        _source.Clear();
        _lines.Clear();
        _text = string.Empty;
        _accessibilityIdentity = LastAccessibilityId = null;
        base.ReleaseOwnedResources();
    }
}
