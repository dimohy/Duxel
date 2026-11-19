using Duxel.Core;
using Duxel.Platform.Windows;

internal static partial class WindowsImeOwnershipTests
{
    internal static void RunInputOrderProbe(Action<bool, string> check)
    {
        using var platform = new WindowsPlatformBackend(new WindowsPlatformBackendOptions
        {
            Width = 400,
            Height = 300,
            Title = "Duxel owned input ordering probe",
            TrayOptions = new(),
            KeyRepeatSettingsProvider = new WindowsKeyRepeatSettingsProvider()
        });
        var ime = (WindowsImeHandler)platform.ImeHandler!;
        foreach (var kind in new[] { "Input", "Textarea", "Markdown" })
            foreach (var reverse in new[] { false, true })
                foreach (var native in new[] { false, true })
                    foreach (var charFirst in new[] { false, true })
                    {
                        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
                        using var tree = new UiAccessibilityTree();
                        ControlFontFixture.Configure(context);
                        context.SetAccessibilityTree(tree);
                        if (native)
                            context.SetImeHandler(ime);
                        var first = "a";
                        var second = "b";
                        var binding = new UiBinding<string>(() => first, value => first = value);
                        using UiWidget a = kind switch
                        {
                            "Input" => Fluent.Input("Order A", binding, 256, 240),
                            "Textarea" => Fluent.Textarea("Order A", binding, 256, 240, 90),
                            "Markdown" => new UiMarkdownEditor("Order A", binding, new() { Width = 240, Height = 90 }),
                            _ => throw new InvalidOperationException("Unknown order consumer.")
                        };
                        using var b = Fluent.Input("Order B", new UiBinding<string>(() => second, value => second = value), 256, 240);
                        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
                        void Frame(UiInputState? input = null)
                        {
                            context.NewFrame(new(1f / 60, new(400, 300), new(1, 1)), input ?? idle, new(0, 0, 400, 300), UiTextSettings.Default);
                            context.Render(new Screen(ui =>
                            {
                                ui.EnableRootViewportContentLayout(contentPadding: 0);
                                void DrawA()
                                {
                                    ui.SetCursorScreenPos(default);
                                    a.Render(ui);
                                }
                                void DrawB()
                                {
                                    ui.SetCursorScreenPos(new(0, 150));
                                    b.Render(ui);
                                }
                                if (reverse)
                                {
                                    DrawB();
                                    DrawA();
                                }
                                else
                                {
                                    DrawA();
                                    DrawB();
                                }
                            }));
                            using var data = context.GetDrawData();
                        }
                        string FindA()
                        {
                            string? found = null;
                            void Visit(string? id)
                            {
                                while (id is not null && tree.TryGet(id, out var element))
                                {
                                    if (element.Node.Name == "Order A" && element.Node.Role == UiAccessibilityRole.Edit)
                                        found = element.Node.Id;
                                    Visit(element.FirstChild);
                                    id = element.Next;
                                }
                            }
                            Visit(tree.Root.First);
                            return found ?? throw new InvalidOperationException("Missing original input.");
                        }
                        ime.SetCompositionOwner(null);
                        ime.ConsumeRecentCommittedText();
                        platform.PollEvents();
                        Frame();
                        var idA = FindA();
                        context.State.ActiveId = context.State.FocusedId = idA;
                        context.State.SetCursor(idA, 1);
                        context.State.SetSelection(idA, new(1, 1));
                        Frame();
                        platform.PollEvents();
                        var pending = idle;
                        void Click() => pending = pending with
                        {
                            MousePosition = new(200, 160),
                            LeftMouseDown = true,
                            LeftMousePressed = true
                        };
                        void Character() => SendMessageW(platform.WindowHandle, 0x0102, 'X', 0);
                        // Both histories deliberately run before one render. The current snapshot has no cross-channel order.
                        if (charFirst)
                        {
                            Character();
                            Click();
                        }
                        else
                        {
                            Click();
                            Character();
                        }
                        var raw = platform.Input.Snapshot.CharEvents.ToArray();
                        var prefix = $"Input order : {kind}, reverse={reverse}, native={native}, charFirst={charFirst}";
                        check(raw.Length == 1 && raw[0].CodePoint == 'X', prefix + " / real WM_CHAR produces one raw scalar");
                        Frame(pending with
                        {
                            CharEvents = raw
                        });
                        Frame(idle with
                        {
                            LeftMouseReleased = true
                        });
                        var expectedA = charFirst ? "aX" : "a";
                        var expectedB = charFirst ? "b" : "bX";
                        check(first == expectedA && second == expectedB,
                            prefix + $" / expected={expectedA}|{expectedB}; actual={first}|{second}");
                        ime.SetCompositionOwner(null);
                        ime.ConsumeRecentCommittedText();
                        platform.PollEvents();
                    }
        check(!platform.ValidationWindowShown, "input order probe never shows its window or changes user focus");
    }
}
