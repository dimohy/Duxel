using Duxel.Core;
using Duxel.Platform.Windows;

internal static class CardAccessibilityNativeHost
{
    internal static void Run(Action<bool, string> check, bool floatingAction = false)
    {
        using var platform = new WindowsPlatformBackend(new()
        {
            Width = 640, Height = 480, Title = "Duxel owned Card fixture",
            KeyRepeatSettingsProvider = new WindowsKeyRepeatSettingsProvider(), TrayOptions = new()
        });
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        context.SetAccessibilityTree(platform.AccessibilityTree);
        var executions = 0;
        var headerExecutions = 0;
        var rootExecutions = 0;
        var selected = false;
        var selectionWrites = 0;
        var text = "Original";
        var finished = false;
        var hidden = false;
        var floating = floatingAction ? Fluent.Checkbox("Floating native selection", new UiBinding<bool>(() => selected,
            value => { selected = value; selectionWrites++; })) : null;
        using var card = Fluent.Card("Native Card", Dux.Column(new(Spacing: 8, MainAxisSize: UiMainAxisSize.Min),
            Fluent.CardHeader(Fluent.Body1Strong("Title"), Fluent.Caption1("Info"),
                Fluent.Avatar("정세일", new(UiAvatarSize.Size24)), Fluent.Button("Header info", () => headerExecutions++)),
            Fluent.CardFooter([Fluent.Caption1("Actions")], Fluent.Button("Card child action", () => executions++)),
            Fluent.Input("Card child input", new UiBinding<string>(() => text, value => text = value), 100, 200)), new(Width: 260), floatingAction: floating);
        using var rename = Fluent.Button("Rename card", () => card.Label = "Renamed Native Card");
        using var resize = Fluent.Button("Restyle card", () => card.Options = new(UiCardSize.Small, UiCardAppearance.Outline, Width: 240));
        using var focus = Fluent.Button("Enable card focus", () => card.Options = card.Options with { FocusMode = UiFocusGroupMode.NoTab });
        using var disable = Fluent.Button("Disable card root", () => card.Options = card.Options with { Disabled = true });
        using var enable = Fluent.Button("Enable card root", () => card.Options = card.Options with { Disabled = false });
        using var activate = Fluent.Button("Enable card action", () => card.Activated = () => rootExecutions++);
        using var select = Fluent.Button("Enable card selection", () => card.Selection = new(() => selected,
            value => { selected = value; selectionWrites++; }));
        using var hide = Fluent.Button("Hide card", () => hidden = true);
        using var finish = Fluent.Button("Finish", () => finished = true);
        using var controls = Dux.Wrap(new(Spacing: 8, RunSpacing: 8),
            rename, resize, hide, finish, focus, disable, enable, activate, select);
        var ready = false;
        var mediumGeometryChecked = false;
        var smallGeometryChecked = false;
        var deadline = Environment.TickCount64 + 60000;
        while (!finished && !platform.ShouldClose && Environment.TickCount64 < deadline)
        {
            platform.PollEvents();
            context.NewFrame(new(1f / 60, new(640, 480), new(1, 1)),
                new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default),
                new(0, 0, 640, 480), UiTextSettings.Default);
            context.Render(new Screen(ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding: 0);
                controls.Render(ui);
                if (!hidden) card.Render(ui);
            }));
            using var data = context.GetDrawData();
            var small = card.Options.Size == UiCardSize.Small;
            if (!hidden && (!small && !mediumGeometryChecked || small && !smallGeometryChecked))
            {
                UiRect? bodyBounds = null;
                UiRect? actionBounds = null;
                UiRect? headerBounds = null;
                UiRect? floatingBounds = null;
                void VisitBounds(string? id)
                {
                    while (id is not null && platform.AccessibilityTree.TryGet(id, out var element))
                    {
                        if (element.Node.Name is "Native Card" or "Renamed Native Card") bodyBounds = element.Node.Bounds;
                        if (element.Node.Name == "Card child action") actionBounds = element.Node.Bounds;
                        if (element.Node.Name == "Header info") headerBounds = element.Node.Bounds;
                        if (element.Node.Name == "Floating native selection") floatingBounds = element.Node.Bounds;
                        VisitBounds(element.FirstChild);
                        id = element.Next;
                    }
                }
                VisitBounds(platform.AccessibilityTree.Root.First);
                if (bodyBounds is not { } body || actionBounds is not { } action)
                    throw new InvalidOperationException("Native Footer geometry is missing.");
                check(MathF.Abs(action.X + action.Width - body.X - body.Width + (small ? 8 : 12)) < .0001f,
                    "native host Footer exact logical edge matches source padding : " + (small ? "Small" : "Medium"));
                if (headerBounds is not { } headerAction) throw new InvalidOperationException("Native Header action geometry is missing.");
                check(MathF.Abs(headerAction.X + headerAction.Width - body.X - body.Width + (small ? 8 : 12)) < .0001f,
                    "native host Header exact logical edge matches source padding : " + (small ? "Small" : "Medium"));
                if (floatingAction)
                {
                    if (floatingBounds is not { } floatingRect) throw new InvalidOperationException("Native floating action geometry is missing.");
                    check(MathF.Abs(floatingRect.X + floatingRect.Width - body.X - body.Width + 4) < .0001f
                        && floatingRect.Y == body.Y + 4, "native floating action exact logical bounds preserve source4px inset : " + (small ? "Small" : "Medium"));
                }
                if (small) smallGeometryChecked = true; else mediumGeometryChecked = true;
            }
            if (!ready)
            {
                void PrintGeometry(string? id)
                {
                    while (id is not null && platform.AccessibilityTree.TryGet(id, out var element))
                    {
                        if (element.Node.Name is "Native Card" or "Card child action")
                            Console.WriteLine($"UIA_CARD_HOST_GEOMETRY {element.Node.Name} : {element.Node.Bounds}");
                        PrintGeometry(element.FirstChild);
                        id = element.Next;
                    }
                }
                PrintGeometry(platform.AccessibilityTree.Root.First);
                ready = true;
                Console.WriteLine((floatingAction ? "UIA_CARD_FLOATING_HOST_READY " : "UIA_CARD_HOST_READY ") + platform.WindowHandle);
                Console.Out.Flush();
            }
            Thread.Sleep(1);
        }
        check(finished && hidden && executions == 1 && headerExecutions == 1 && rootExecutions == 1 && selectionWrites == 2 && !selected && text == "Edited by native client",
            "external Card client invokes and edits actual owned controls then completes removal");
        check(card.Options.Width == 240 && card.Label == "Renamed Native Card", "external Card client changes the live surface options and name");
    }
    private sealed class Screen(Action<UiImmediateContext> draw) : UiScreen { public override void Render(UiImmediateContext ui) => draw(ui); }
}
