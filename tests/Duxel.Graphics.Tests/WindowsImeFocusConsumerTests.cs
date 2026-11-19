using Duxel.Core;
using Duxel.Platform.Windows;

internal static partial class WindowsImeOwnershipTests
{
    private static void RunFocusConsumers(WindowsPlatformBackend platform, WindowsImeHandler ime, Action<bool, string> check)
    {
        foreach (var kind in new[] { "Input", "Textarea", "Markdown" })
            foreach (var reverse in new[] { false, true })
            {
                using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
                using var tree = new UiAccessibilityTree();
                ControlFontFixture.Configure(context);
                context.SetAccessibilityTree(tree);
                context.SetImeHandler(ime);
                var first = "a";
                var second = "b";
                var binding = new UiBinding<string>(() => first, value => first = value);
                using UiWidget inputA = kind switch
                {
                    "Input" => Fluent.Input("Focus A", binding, 256, 240),
                    "Textarea" => Fluent.Textarea("Focus A", binding, 256, 240, 90),
                    "Markdown" => new UiMarkdownEditor("Focus A", binding, new() { Width = 240, Height = 90 }),
                    _ => throw new InvalidOperationException("Unknown focus consumer.")
                };
                using var inputB = Fluent.Input("Focus B", new UiBinding<string>(() => second, value => second = value), 256, 240);
                var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
                void Frame(UiInputState? state = null)
                {
                    context.NewFrame(new(1f / 60, new(400, 300), new(1, 1)), state ?? idle, new(0, 0, 400, 300), UiTextSettings.Default);
                    context.Render(new ConsumerScreen(ui =>
                    {
                        ui.EnableRootViewportContentLayout(contentPadding: 0);
                        void First()
                        {
                            ui.SetCursorScreenPos(new(0, 0));
                            inputA.Render(ui);
                        }
                        void Second()
                        {
                            ui.SetCursorScreenPos(new(0, 150));
                            inputB.Render(ui);
                        }
                        if (reverse)
                        {
                            Second();
                            First();
                        }
                        else
                        {
                            First();
                            Second();
                        }
                    }));
                    using var data = context.GetDrawData();
                }
                UiAccessibilityNode Find(string name)
                {
                    UiAccessibilityNode? found = null;
                    void Visit(string? id)
                    {
                        while (id is not null && tree.TryGet(id, out var element))
                        {
                            if (element.Node.Name == name && element.Node.Role == UiAccessibilityRole.Edit)
                                found = element.Node;
                            Visit(element.FirstChild);
                            id = element.Next;
                        }
                    }
                    Visit(tree.Root.First);
                    return found ?? throw new InvalidOperationException("Missing focus consumer : " + name);
                }
                void Send(uint message, nint flags = 0) => SendMessageW(platform.WindowHandle, message, 0, flags);
                void Verify(bool condition, string name) => check(condition, $"Focus consumers : {kind}, reverse={reverse} / {name}");
                Frame();
                var idA = Find("Focus A").Id;
                context.State.ActiveId = context.State.FocusedId = idA;
                context.State.SetCursor(idA, 1);
                context.State.SetSelection(idA, new(1, 1));
                Frame();
                Send(0x010D);
                ime.ValidationSetCompositionStrings("한", "정");
                Send(0x010F, 0x8);
                Frame();
                platform.PollEvents();
                ime.ValidationCompletions.Clear();
                Verify(first == "a" && second == "b", "preedit leaves both bound source values unchanged");
                ime.ValidationCompositionNotification = _ => { Send(0x010F, 0x800); Send(0x010E); return true; };
                Frame(idle with
                {
                    MousePosition = new(10, 160),
                    LeftMouseDown = true,
                    LeftMousePressed = true
                });
                platform.PollEvents();
                Frame(idle with
                {
                    LeftMouseReleased = true
                });
                Verify(ime.ValidationCompletions.Count == 1 && ime.ValidationCompletions[0].Action == 1,
                    "actual pointer focus invokes the queued completion exactly once");
                Verify(first == "a정" && second == "b" && context.State.ActiveId == Find("Focus B").Id,
                    "automatic native completion updates the original binding while preserving new focus");
                Verify(context.State.GetCursor(idA, first.Length) == 2, "inactive original input persists the final committed cursor");
                context.State.ActiveId = context.State.FocusedId = idA;
                Frame();
                Verify(context.State.GetCursor(idA, first.Length) == 2, "returning focus preserves the cursor after native completion");
                inputA.Dispose();
                inputB.Dispose();
                Verify(context.State.TextInputStorage == (0, 0), "focus consumers release all edit buffers");
                ime.ValidationCompositionNotification = null;
                ime.SetCompositionOwner(null);
                ime.ConsumeRecentCommittedText();
                Send(0x010E);
                platform.PollEvents();
            }
    }
}
