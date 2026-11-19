namespace Duxel.Core;

[UiWidgetStyle(typeof(UiToastWidget))]
[UiComponent("Toast", UiComponentExposure.Fluent)]
public partial class UiFluentToast;

public static partial class Fluent
{
    /// <summary>Creates the viewport host for a caller-owned notification controller; it is not an additional catalog component.</summary>
    public static UiToasterWidget Toaster(UiToastController controller, UiToasterOptions? options = null,
        UiWidgetIdentity identity = UiWidgetIdentity.Instance) => new(controller, options, identity);
}
