using Duxel.Core;

internal static class PopupInputRenderingTests
{
    internal static void Run(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool, string> check, UiContext context)
    {
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        using var before = Fluent.Button("GPU before");
        using var inside = Fluent.Button("GPU inside");
        using var after = Fluent.Button("GPU after");
        var mode = UiPopupInputMode.NonModal;
        var requestOpen = true;
        var surface = default(UiRect);
        var expected = default(UiColor);
        void Draw(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding: 0);
            before.Render(ui);
            if (requestOpen)
            {
                ui.OpenPopup("gpu input");
                requestOpen = false;
            }
            ui.SetNextPopupPosition(new(120, 80));
            if (ui.BeginPopup("gpu input", mode))
            {
                try
                {
                    var paint = new UiControlContext(ui);
                    surface = paint.ClipRect;
                    expected = paint.Theme.PopupBg;
                    inside.Render(ui);
                }
                finally { ui.EndPopup(); }
            }
            new UiControlContext(ui).Frame(surface, new(255, 0, 0), default, 0, 0);
            ui.SetCursorScreenPos(new(0, 40));
            after.Render(ui);
        }
        UiAccessibilityElement Find(string name)
        {
            var id = tree.Root.First;
            while (id is not null && tree.TryGet(id, out var e))
            {
                if (e.Node.Name == name)
                    return e;
                id = e.Next;
            }
            throw new InvalidOperationException("Missing GPU popup provider : " + name);
        }
        foreach (var inputMode in Enum.GetValues<UiPopupInputMode>())
        {
            mode = inputMode;
            context.State.FocusedId = null;
            capture(mode + "-warmup1", Draw);
            capture(mode + "-warmup2", Draw);
            var pixels = capture(mode + "-reference", Draw);
            check(pixels.AsSpan().SequenceEqual(capture(mode + "-replay", Draw)), "GPU popup unchanged layered pixels replay : " + mode);
            var x = (int)(surface.X + surface.Width - 10);
            var y = (int)(surface.Y + surface.Height - 10);
            var offset = (y * (int)context.GetIO().DisplaySize.X + x) * 4;
            check(pixels[offset] == (byte)expected.Rgba && pixels[offset + 1] == (byte)(expected.Rgba >> 8) && pixels[offset + 2] == (byte)(expected.Rgba >> 16),
                "GPU popup paints above later base geometry : " + mode);
            check(Find("GPU after").Node.Enabled == (mode == UiPopupInputMode.NonModal), "GPU popup native background eligibility follows keyboard mode : " + mode);
        }
        var outside = Find("GPU after");
        check(tree.RequestAction(outside.Node.Id, outside.RuntimeId, UiAccessibilityAction.Focus, wait: false) == UiAccessibilityResult.Success,
            "GPU nonmodal popup permits real native outside focus");
        capture("outside-focus", Draw);
        check(Find("GPU after").Node.Focused, "GPU nonmodal outside focus survives layered rendering");
        check(context.State.NextScheduledFrameTimeSeconds is null, "GPU popup input mode adds no periodic frames");
    }
}
