namespace Duxel.Core;

/// <summary>Compiled hover/focus tooltip with Fluent text and surface presentation.</summary>
[UiWidgetStyle(typeof(UiTooltip<,>), typeof(UiHoverFocusTooltipBehavior), typeof(UiFluentTooltipPresentation))]
[UiComponent("Tooltip", UiComponentExposure.Fluent)]
public partial class UiFluentTooltip;
