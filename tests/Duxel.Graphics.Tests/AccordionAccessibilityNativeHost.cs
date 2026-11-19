using System.Collections.Immutable;
using Duxel.Core;
using Duxel.Platform.Windows;

internal static class AccordionAccessibilityNativeHost
{
    internal static void Run(Action<bool, string> check)
    {
        using var platform = new WindowsPlatformBackend(new()
        {
            Width = 720, Height = 640, Title = "Duxel owned Accordion fixture",
            KeyRepeatSettingsProvider = new WindowsKeyRepeatSettingsProvider(), TrayOptions = new()
        });
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context); context.SetAccessibilityTree(platform.AccessibilityTree);
        var value = ImmutableArray<string>.Empty;
        var text = new UiState<string>("Initial");
        var writes = 0; var actions = 0; var finish = false;
        using var accordion = Fluent.Accordion("Native Accordion", new(() => value, next => { value = next; writes++; }),
            new UiAccordionItem[]
            {
                new("one", "Native section one", Fluent.Input("Accordion native editor", text.Binding, 100, 300)),
                new("two", "Native section two", Fluent.Button("Accordion body action", () => actions++)),
                new("disabled", "Native disabled section", Fluent.Body1("Disabled content"), false)
            }, 560, new(Collapsible : true));
        using var deny = Fluent.Button("Deny final collapse", () => accordion.Options = accordion.Options with { Collapsible = false });
        using var allow = Fluent.Button("Allow final collapse", () => accordion.Options = accordion.Options with { Collapsible = true });
        using var multiple = Fluent.Button("Allow multiple panels", () => accordion.Options = accordion.Options with { Multiple = true });
        using var done = Fluent.Button("Finish", () => finish = true);
        var ready = false;
        var deadline = Environment.TickCount64 + 60000;
        while (!finish && !platform.ShouldClose && Environment.TickCount64 < deadline)
        {
            platform.PollEvents();
            context.NewFrame(new(1f / 60, new(720, 640), new(1, 1)),
                new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default),
                new(0, 0, 720, 640), UiTextSettings.Default);
            context.Render(new Screen(ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding : 0);
                ui.SetCursorScreenPos(new(20, 20));
                deny.Render(ui); allow.Render(ui); multiple.Render(ui); done.Render(ui);
                accordion.Render(ui);
            }));
            using var draw = context.GetDrawData();
            if (!ready) { ready = true; Console.WriteLine("UIA_ACCORDION_HOST_READY " + platform.WindowHandle); Console.Out.Flush(); }
            Thread.Sleep(1);
        }
        check(finish && actions == 1, "external Accordion client invokes its actual retained panel content");
        check(text.Value == "Native saved text", "external Accordion edits persist across collapse and reopen");
        check(value.SequenceEqual(["two"]) && writes == 5, "external Accordion expansion actions update the original binding exactly five times");
    }
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    { public override void Render(UiImmediateContext ui) => render(ui); }
}
