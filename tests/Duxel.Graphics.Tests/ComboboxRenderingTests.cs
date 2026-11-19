using Duxel.Core;
using Duxel.Svg;

internal static class ComboboxRenderingTests
{
    internal static void Run(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool, string> check,
        UiContext context, Action<UiInputState> setInput, int width)
    {
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        using var store = new SvgStore();
        var assets = Path.Combine(AppContext.BaseDirectory, "Assets", "Svg");
        using var icon = new UiSvgIcon(store.Load(Path.Combine(assets, "regular.svg")), store.Load(Path.Combine(assets, "filled.svg")), UiSvgIconColorMode.MultiplyForeground);
        string text = "";
        string? selected = "a";
        using var combo = Fluent.Combobox("GPU editable", new UiBinding<string>(() => text, value => text = value),
            new UiBinding<string?>(() => selected, value => selected = value),
            [new("a", "Alpha"), new("b", "Beta") { Icon = icon.MenuIcon }, new("n", "New York"), new("k", "부산")],
            new(Field: new(Width: 220, Placeholder: "Search"), Freeform: true));
        void Draw(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding: 0);
            var paint = new UiControlContext(ui);
            paint.Frame(paint.ClipRect, new(30, 70, 90), default, 0, 0);
            ui.SetCursorScreenPos(new(8, 16));
            paint.RestartInputCaretBlink();
            combo.Render(ui);
        }
        UiAccessibilityElement Find(string name, UiAccessibilityRole role)
        {
            UiAccessibilityElement? result = null;
            void Visit(string? id)
            {
                while (id is not null && tree.TryGet(id, out var e))
                {
                    if (e.Node.Name == name && e.Node.Role == role)
                        result = e;
                    Visit(e.FirstChild);
                    id = e.Next;
                }
            }
            Visit(tree.Root.First);
            return result ?? throw new InvalidOperationException(name);
        }
        void Send(string name, UiAccessibilityRole role, UiAccessibilityAction action, string? value = null)
        {
            var item = Find(name, role);
            check(tree.RequestAction(item.Node.Id, item.RuntimeId, action, value, wait: false) == UiAccessibilityResult.Success,
                "GPU Combobox accepts actual native action : " + action);
        }
        var closed = capture("closed", Draw);
        check(closed.AsSpan().SequenceEqual(capture("closed-replay", Draw)), "GPU Combobox closed input replays exactly");
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        byte[] ClickArrow(string name)
        {
            var bounds = Find("Show options", UiAccessibilityRole.Button).Node.Bounds;
            var point = new UiVector2(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
            setInput(idle with
            {
                MousePosition = point,
                LeftMouseDown = true,
                LeftMousePressed = true
            });
            capture(name + "-press", Draw);
            setInput(idle with
            {
                MousePosition = point,
                LeftMouseDown = true
            });
            capture(name + "-hold", Draw);
            capture(name + "-hold-again", Draw);
            setInput(idle with
            {
                MousePosition = point,
                LeftMouseReleased = true
            });
            var pixels = capture(name + "-release", Draw);
            setInput(idle);
            return pixels;
        }
        ClickArrow("arrow-open");
        check(combo.IsOpen && !Find("Alpha", UiAccessibilityRole.Option).Node.Offscreen
            && !Find("Beta", UiAccessibilityRole.Option).Node.Offscreen,
            "GPU Combobox held arrow click opens the actual complete option popup");
        context.State.AdvanceTime(.2f);
        var opened = capture("arrow-open-idle", Draw);
        var popup = Find("GPU editable", UiAccessibilityRole.ListBox).Node.Bounds;
        var shadowPixel = ((int)(popup.Y + 24) * width + (int)(popup.X + popup.Width + 3)) * 4 + 1;
        check(opened[shadowPixel] < closed[shadowPixel], "GPU Combobox casts a visible shadow beyond the listbox");
        check(opened.AsSpan().SequenceEqual(capture("arrow-open-replay", Draw)) && !closed.AsSpan().SequenceEqual(opened),
            "GPU Combobox arrow-open popup changes pixels and replays exactly");
        ClickArrow("arrow-close");
        check(!combo.IsOpen, "GPU Combobox repeated held arrow click closes the actual popup");
        Send("GPU editable", UiAccessibilityRole.ComboBox, UiAccessibilityAction.Focus);
        capture("focus", Draw);
        Send("GPU editable", UiAccessibilityRole.ComboBox, UiAccessibilityAction.SetValue, "Be");
        capture("filtered-while-opening", Draw);
        context.State.AdvanceTime(.2f);
        var filtered = capture("filtered", Draw);
        check(combo.IsOpen && !Find("Beta", UiAccessibilityRole.Option).Node.Offscreen
            && Find("Alpha", UiAccessibilityRole.Option).Node.Offscreen && Find("Alpha", UiAccessibilityRole.Option).Node.Selected == true,
            "GPU Combobox paints the actual SVG option while retaining the hidden canonical selection");
        check(!closed.AsSpan().SequenceEqual(filtered) && filtered.AsSpan().SequenceEqual(capture("filtered-replay", Draw)),
            "GPU Combobox edited input and filtered SVG popup replay exactly");
        Send("Beta", UiAccessibilityRole.Option, UiAccessibilityAction.Select);
        var chosen = capture("chosen", Draw);
        check(text == "Beta" && selected == "b" && !combo.IsOpen && !chosen.AsSpan().SequenceEqual(filtered),
            "GPU Combobox commits selected text to the actual edit field and closes the popup");
        context.State.AdvanceTime(.2f);
        var settled = capture("chosen-settled", Draw);
        check(settled.AsSpan().SequenceEqual(capture("chosen-replay", Draw)), "GPU Combobox selected editor replays exactly after motion");
        foreach (var size in Enum.GetValues<UiDropdownSize>())
            foreach (var appearance in Enum.GetValues<UiInputAppearance>())
            {
                combo.Options = combo.Options with
                {
                    Field = new(Width: 180, Size: size, Appearance: appearance, RightToLeft: true)
                };
                context.State.AdvanceTime(.2f);
                var pixels = capture($"rtl-{size}-{appearance}", Draw);
                check(pixels.AsSpan().SequenceEqual(capture($"rtl-{size}-{appearance}-replay", Draw)),
                    "GPU Combobox RTL size/appearance exact replay : " + size + " / " + appearance);
                ClickArrow($"rtl-{size}-{appearance}-arrow-open");
                var arrowOpened = combo.IsOpen;
                ClickArrow($"rtl-{size}-{appearance}-arrow-close");
                check(arrowOpened && !combo.IsOpen,
                    "GPU Combobox RTL arrow opens and closes across sizes and appearances : " + size + " / " + appearance);
                var input = Find("GPU editable", UiAccessibilityRole.Edit).Node.Bounds;
                var arrow = Find("Show options", UiAccessibilityRole.Button).Node.Bounds;
                check(input.X >= arrow.X + arrow.Width && input.X + input.Width <= 188,
                    "GPU Combobox RTL editor and arrow preserve their separate assigned bounds");
            }
        var desiredOpen = false;
        var transitions = new List<bool>();
        combo.Open = new(() => desiredOpen, _ => { });
        combo.OpenChanged = transitions.Add;
        context.State.AdvanceTime(.2f);
        var controlledClosed = capture("controlled-closed", Draw);
        Send("GPU editable", UiAccessibilityRole.ComboBox, UiAccessibilityAction.Expand);
        var deniedOpen = capture("controlled-denied-open", Draw);
        check(!combo.IsOpen && controlledClosed.AsSpan().SequenceEqual(deniedOpen) && transitions.Count == 0,
            "GPU Combobox denied opening retains the exact closed image and emits no transition");
        desiredOpen = true;
        capture("controlled-opening", Draw);
        context.State.AdvanceTime(.2f);
        var controlledOpen = capture("controlled-open", Draw);
        check(combo.IsOpen && !Find("Alpha", UiAccessibilityRole.Option).Node.Offscreen,
            "GPU Combobox external opening paints the explicit expansion's full canonical option set");
        Send("GPU editable", UiAccessibilityRole.ComboBox, UiAccessibilityAction.Collapse);
        var deniedClose = capture("controlled-denied-close", Draw);
        check(combo.IsOpen && controlledOpen.AsSpan().SequenceEqual(deniedClose) && transitions.SequenceEqual(new[] { true }),
            "GPU Combobox denied collapse preserves its actual expanded pixels without a fake close event");
        text = "no matches";
        var empty = capture("controlled-empty", Draw);
        check(combo.IsOpen && Find("Alpha", UiAccessibilityRole.Option).Node.Offscreen
            && Find("GPU editable", UiAccessibilityRole.ListBox).Node.Bounds.Height >= 32
            && !Find("No matches", UiAccessibilityRole.Label).Node.Offscreen
            && !empty.AsSpan().SequenceEqual(controlledOpen),
            "GPU Combobox a controlled empty result keeps a real listbox surface and the authoritative input query");
        check(empty.AsSpan().SequenceEqual(capture("controlled-empty-replay", Draw)),
            "GPU Combobox empty filtered surface and input replay exactly");
        desiredOpen = false;
        capture("controlled-external-close", Draw);
        check(!combo.IsOpen && transitions.SequenceEqual(new[] { true, false }),
            "GPU Combobox external close publishes exactly the accepted open and close transitions");
    }
}
