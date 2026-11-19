using Duxel.Core;

internal static class ToastFactoryTests
{
    internal static void Run(Action<bool, string> check)
    {
        check(Fluent.ComponentNames.Contains("Toast") && !Fluent.MissingComponentNames.Contains("Toast"),
            "Toast is registered as an implemented canonical Fluent factory");
        check(!Fluent.ComponentNames.Contains("Toaster") && !Fluent.RequiredComponentNames.Contains("Toaster"),
            "Toaster is a helper rather than an extra canonical catalog component");
        check(Fluent.RequiredComponentNames.Length == 55,
            "adding the Toaster helper preserves the official component denominator");
        var closed = 0;
        using UiFluentToast inline = Fluent.Toast(new("Factory notification"), () => closed++);
        inline.Hide(); inline.Hide();
        check(!inline.IsOpen && closed == 1, "generated Fluent.Toast forwards lifecycle callbacks and inherited composition behavior");
        inline.Show();
        check(inline.IsOpen, "generated Fluent.Toast preserves its reusable Show contract");
        var controller = new UiToastController();
        using UiToasterWidget host = Fluent.Toaster(controller, new(Width : 160, RightToLeft : true, ReducedMotion : true));
        check(ReferenceEquals(host.Controller, controller) && host.Options.Width == 160 && host.Options.RightToLeft,
            "Fluent.Toaster forwards caller ownership and host configuration without a duplicate engine");
    }
}
