using Duxel.Core;
using Duxel.Platform.Windows;

internal static class PersonaAccessibilityNativeHost
{
    internal static void Run(Action<bool, string> check)
    {
        using var platform = new WindowsPlatformBackend(new()
        {
            Width = 640,
            Height = 480,
            Title = "Duxel owned Persona fixture",
            KeyRepeatSettingsProvider = new WindowsKeyRepeatSettingsProvider(),
            TrayOptions = new()
        });
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        context.SetAccessibilityTree(platform.AccessibilityTree);
        using var persona = Fluent.Persona(new UiPersonaContent("Native Persona", SecondaryText: "Developer"));
        persona.Avatar.Options = new(Presence: new());
        persona.Avatar.PresenceText = "사용 가능";
        using var status = Fluent.Label("Persona view : avatar");
        var hidden = false;
        var disabled = false;
        var enabledAgain = false;
        var finished = false;
        using var controls = Dux.Wrap(new(Spacing: 8, RunSpacing: 8),
            Fluent.Button("Rename persona", () => persona.Content = new("정##세일", SecondaryText: "개발자")),
            Fluent.Button("Persona activity", () => { persona.Avatar.ActivityText = "활동 중"; persona.Avatar.Options = persona.Avatar.Options with { ShowActivityRing = true }; }),
            Fluent.Button("Persona presence only", () => { persona.Options = new(UiPersonaSize.ExtraSmall, PresenceOnly: true); status.Text = "Persona view : presence"; }),
            Fluent.Button("Hide persona media", () => persona.Options = persona.Options with { ShowMedia = false }),
            Fluent.Button("Disable persona", () => disabled = true),
            Fluent.Button("Enable persona", () => { disabled = false; enabledAgain = true; }),
            Fluent.Button("Hide persona", () => hidden = true), Fluent.Button("Finish", () => finished = true));
        var ready = false;
        var limit = Environment.TickCount64 + 60000;
        while (!finished && !platform.ShouldClose && Environment.TickCount64 < limit)
        {
            platform.PollEvents();
            context.NewFrame(new(0, new(640, 480), new(1, 1)), new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default), new(0, 0, 640, 480), UiTextSettings.Default);
            context.Render(new Screen(ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding: 0);
                controls.Render(ui);
                status.Render(ui);
                if (!hidden)
                {
                    ui.BeginDisabled(disabled);
                    try
                    {
                        persona.Render(ui);
                    }
                    finally { ui.EndDisabled(); }
                }
            }));
            using var data = context.GetDrawData();
            if (!ready)
            {
                ready = true;
                Console.WriteLine("UIA_PERSONA_HOST_READY " + platform.WindowHandle);
                Console.Out.Flush();
            }
            Thread.Sleep(1);
        }
        check(finished && hidden && enabledAgain && !disabled, "external Persona client completes provider disable/enable/retirement");
        check(persona.Content.Name == "정##세일" && persona.Content.SecondaryText == "개발자", "external Persona client changes actual person information");
        check(persona.Options.PresenceOnly && !persona.Options.ShowMedia, "external Persona client controls the actual media mode");
    }
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
