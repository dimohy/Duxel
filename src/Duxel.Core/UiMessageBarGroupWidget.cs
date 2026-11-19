namespace Duxel.Core;

internal interface IUiMessageBarContent;

/// <summary>Owns message bars with controlled dismissal, reopening, exit paint and group reflow.</summary>
public class UiMessageBarGroupWidget : UiPresenceGroupWidget
{
    public UiMessageBarGroupWidget(UiWidget[] messages, UiPresenceGroupOptions? options = null,
        UiWidgetIdentity identity = UiWidgetIdentity.Instance, string[]? keys = null) : base(Validate(messages), options, identity, keys) { }
    protected override void ValidateItem(UiWidget widget)
    {
        if (widget is not IUiMessageBarContent)
            throw new ArgumentException("A MessageBarGroup requires actual MessageBar children.", nameof(widget));
    }
    private static UiWidget[] Validate(UiWidget[] messages)
    {
        ArgumentNullException.ThrowIfNull(messages);
        foreach (var message in messages)
            if (message is not IUiMessageBarContent)
                throw new ArgumentException("A MessageBarGroup requires actual MessageBar children.", nameof(messages));
        return messages;
    }
}

[UiWidgetStyle(typeof(UiMessageBarGroupWidget))]
[UiComponent("MessageBarGroup", UiComponentExposure.Fluent)]
public partial class UiFluentMessageBarGroup;
