using Duxel.Core;

internal static class PopoverMotionInputTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        var ime = new Composition();
        context.SetImeHandler(ime);
        var text = "정";
        using var popover = Fluent.Popover("Moving editor", Fluent.Input("Motion name",
            new UiBinding<string>(() => text, value => text = value), 64, 180), options: new(Width: 240, DisableAutoFocus: true));
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame(float elapsed = 0, UiInputState? input = null)
        {
            context.NewFrame(new(elapsed, new(640, 480), new(1, 1)), input ?? idle, new(0, 0, 640, 480), UiTextSettings.Default);
            context.Render(new Screen(ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding: 0);
                ui.SetCursorScreenPos(new(240, 240));
                popover.Render(ui);
            }));
            using var draw = context.GetDrawData();
        }
        UiAccessibilityElement Find()
        {
            UiAccessibilityElement? found = null;
            void Visit(string? id)
            {
                while (id is not null && tree.TryGet(id, out var e))
                {
                    if (e.Node.Role == UiAccessibilityRole.Edit && e.Node.Name == "Motion name")
                        found = e;
                    Visit(e.FirstChild);
                    id = e.Next;
                }
            }
            Visit(tree.Root.First);
            return found ?? throw new InvalidOperationException("Moving editor is missing.");
        }
        popover.Show();
        Frame();
        Frame(.05f);
        var bounds = Find().Node.Bounds;
        var pointer = idle with
        {
            MousePosition = new(bounds.X + bounds.Width - 10, bounds.Y + bounds.Height / 2)
        };
        Frame(input: pointer with
        {
            LeftMouseDown = true,
            LeftMousePressed = true
        });
        Frame(input: pointer with
        {
            LeftMouseReleased = true
        });
        check(ime.Owner == Find().Node.Id && context.State.FocusedId == Find().Node.Id,
            "A pointer can focus the real editor while its parent is entering");
        check(ime.InputBounds == Find().Node.Bounds,
            "IME placement and accessibility use the same animated editor rectangle");
        ime.Preview = "세";
        Frame(.025f, idle with
        {
            KeyEvents = [new(UiKey.Escape, true, default)]
        });
        check(popover.IsOpen && text == "정" && ime.InputBounds == Find().Node.Bounds,
            "Moving composition keeps Escape and caret geometry with its real input");
        ime.Preview = null;
        ime.Pending = "세일";
        Frame();
        check(text == "정세일", "Moving input commits Korean once through its existing IME owner");
        Frame(input: idle with
        {
            KeyEvents = [new(UiKey.Escape, false, default)]
        });
        Frame(input: idle with
        {
            KeyEvents = [new(UiKey.Escape, true, default)]
        });
        check(!popover.IsOpen && context.State.NextScheduledFrameTimeSeconds is null,
            "After composition ends Escape closes the moving editor and cancels its finite motion deadline");
    }

    private sealed class Composition : IUiImeHandler
    {
        internal string? Owner, Preview, Pending;
        internal UiRect InputBounds;
        public string? ConsumeCommittedText(string inputId)
        {
            if (inputId != Owner)
                return null;
            var result = Pending;
            Pending = null;
            return result;
        }
        public void ReleaseInput(string inputId) => _ = ConsumeCommittedText(inputId);
        public string? ConsumeRecentCommittedText() => null;
        public string? GetCompositionText() => Owner is null ? null : Preview;
        public void SetCompositionOwner(string? inputId) => Owner = inputId;
        public void SetCaretRect(UiRect caretRect, UiRect inputRect, float fontPixelHeight, float fontPixelWidth) => InputBounds = inputRect;
    }

    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
