namespace Duxel.Core;

/// <summary>Owns the inline title,body text and optional real trailing widget.</summary>
public class UiMessageBarBodyWidget : UiInlineFlowWidget
{
    private readonly int _textIndex;
    private UiMessageBarTitleWidget? _title;
    private UiWidget? _trailingContent;
    public UiMessageBarBodyWidget(string text, UiMessageBarTitleWidget? title = null, UiWidget? trailingContent = null,
        UiWidgetIdentity identity = UiWidgetIdentity.Instance) : base(Combine(text, title, trailingContent), identity)
    {
        _textIndex = title is null ? 0 : 1;
        _title = title;
        _trailingContent = trailingContent;
    }
    private static UiWidget[] Combine(string text, UiMessageBarTitleWidget? title, UiWidget? trailing)
    {
        ArgumentNullException.ThrowIfNull(text);
        var caption = trailing is null ? new UiInlineTextSpan(text) : UiInlineTextSpan.WithTrailingSpace(text);
        return title is null ? trailing is null ? [caption] : [caption, trailing]
            : trailing is null ? [title, caption] : [title, caption, trailing];
    }
    public UiMessageBarTitleWidget? Title
    {
        get
        {
            ThrowIfDisposed();
            return _title;
        }
    }
    public UiWidget? TrailingContent
    {
        get
        {
            ThrowIfDisposed();
            return _trailingContent;
        }
    }
    public string Text
    {
        get
        {
            ThrowIfDisposed();
            return ((UiInlineTextSpan)Children[_textIndex]).Text;
        }
        set
        {
            ThrowIfDisposed();
            ((UiInlineTextSpan)Children[_textIndex]).Text = value;
        }
    }
    private protected override void ReleaseOwnedResources()
    {
        _title = null;
        _trailingContent = null;
        base.ReleaseOwnedResources();
    }
}

public class UiMessageBarTitleWidget : UiInlineTextSpan
{
    public UiMessageBarTitleWidget(string text, UiWidgetIdentity identity = UiWidgetIdentity.Instance)
        : base(text, UiInlineTextWeight.Semibold, " ", identity) { }
}

[UiWidgetStyle(typeof(UiMessageBarBodyWidget))]
[UiComponent("MessageBarBody", UiComponentExposure.Fluent)]
public partial class UiFluentMessageBarBody;
[UiWidgetStyle(typeof(UiMessageBarTitleWidget))]
[UiComponent("MessageBarTitle", UiComponentExposure.Fluent)]
public partial class UiFluentMessageBarTitle;
