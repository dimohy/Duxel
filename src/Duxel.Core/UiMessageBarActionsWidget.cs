namespace Duxel.Core;

/// <summary>Owns real action widgets; narrow rows wrap with the shared layout engine.</summary>
public class UiMessageBarActionsWidget : UiWrapWidget<UiHorizontalAxis>
{
    public UiMessageBarActionsWidget(UiWidget[] children, UiWidgetIdentity identity = UiWidgetIdentity.Instance)
        : base(children, new(Spacing: 12, RunSpacing: 8, Alignment: UiMainAxisAlignment.End,
            CrossAlignment: UiWrapCrossAlignment.Center), identity)
    {
    }
}

[UiWidgetStyle(typeof(UiMessageBarActionsWidget))]
[UiComponent("MessageBarActions", UiComponentExposure.Fluent)]
public partial class UiFluentMessageBarActions;
