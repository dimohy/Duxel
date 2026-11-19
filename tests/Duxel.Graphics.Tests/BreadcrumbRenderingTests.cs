using Duxel.Core;

internal static class BreadcrumbRenderingTests
{
    internal static void Run(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool, string> check,
        UiContext context, int stride)
    {
        foreach (var size in Enum.GetValues<UiBreadcrumbSize>())
        {
            using var tree = new UiAccessibilityTree();
            context.SetAccessibilityTree(tree);
            var calls = new List<string>();
            using var path = Fluent.Breadcrumb("GPU path", [new("root", "Root"), new("a", "A"), new("b", "B"), new("current", "Here")],
                calls.Add, new(Size: size));
            using var root = Dux.ConstrainedBox(path, new(0, 300, 0, 200));
            void Draw(UiImmediateContext ui)
            {
                ui.EnableRootViewportContentLayout(contentPadding: 0);
                var paint = new UiControlContext(ui);
                paint.Frame(paint.ClipRect, new(30, 70, 90), default, 0, 0);
                ui.SetCursorScreenPos(new(8, 16));
                root.Render(ui);
            }
            UiAccessibilityElement Find(string name, UiAccessibilityRole role)
            {
                UiAccessibilityElement? found = null;
                void Visit(string? id)
                {
                    while (id is not null && tree.TryGet(id, out var item))
                    {
                        if (item.Node.Name == name && item.Node.Role == role)
                            found = item;
                        Visit(item.FirstChild);
                        id = item.Next;
                    }
                }
                Visit(tree.Root.First);
                return found ?? throw new InvalidOperationException(name);
            }
            capture(size + "-wide", Draw);
            var home = Find("Root", UiAccessibilityRole.Button);
            var current = Find("Here", UiAccessibilityRole.Label);
            var height = UiFluentBreadcrumbPresentation.Height(path.Options);
            var divider = UiFluentBreadcrumbPresentation.DividerWidth(path.Options);
            root.Options = root.Options with
            {
                MaxWidth = home.Node.Bounds.Width + current.Node.Bounds.Width + height + divider * 2 + 1
            };
            capture(size + "-narrow-plan", Draw);
            var narrow = capture(size + "-narrow", Draw);
            var more = Find("More levels", UiAccessibilityRole.Button);
            home = Find("Root", UiAccessibilityRole.Button);
            check(more.Node.Bounds.Height == height && !more.Node.Offscreen && Find("Here", UiAccessibilityRole.Label).Node.Bounds.Height == height,
                "GPU Breadcrumb source size and responsive overflow stay on one row : " + size);
            var ink = 0;
            for (var y = (int)home.Node.Bounds.Y; y < home.Node.Bounds.Y + height; y++)
                for (var x = (int)(home.Node.Bounds.X + home.Node.Bounds.Width); x < more.Node.Bounds.X; x++)
                    if (!narrow.AsSpan((y * stride + x) * 4, 3).SequenceEqual(new byte[] { 30, 70, 90 }))
                        ink++;
            check(ink > 0, "GPU Breadcrumb divider paints its generated Fluent font icon : " + size);
            check(narrow.AsSpan().SequenceEqual(capture(size + "-narrow-replay", Draw)), "GPU Breadcrumb unchanged row replays exactly : " + size);
            tree.RequestAction(more.Node.Id, more.RuntimeId, UiAccessibilityAction.Invoke, wait: false);
            capture(size + "-menu-plan", Draw);
            var popup = capture(size + "-menu", Draw);
            check(path.IsOverflowOpen && !Find("B", UiAccessibilityRole.MenuItem).Node.Offscreen,
                "GPU Breadcrumb opens its real hidden-level menu outside the row clip : " + size);
            check(popup.AsSpan().SequenceEqual(capture(size + "-menu-replay", Draw)), "GPU Breadcrumb popup replay is exact : " + size);
            var item = Find("B", UiAccessibilityRole.MenuItem);
            tree.RequestAction(item.Node.Id, item.RuntimeId, UiAccessibilityAction.Invoke, wait: false);
            capture(size + "-menu-navigation", Draw);
            check(!path.IsOverflowOpen && calls.SequenceEqual(new[] { "b" }), "GPU Breadcrumb menu invokes the actual path callback once : " + size);
            home = Find("Root", UiAccessibilityRole.Button);
            tree.RequestAction(home.Node.Id, home.RuntimeId, UiAccessibilityAction.Focus, wait: false);
            path.Options = path.Options with
            {
                RightToLeft = true
            };
            capture(size + "-rtl-plan", Draw);
            var rtl = capture(size + "-rtl", Draw);
            check(Find("Root", UiAccessibilityRole.Button).Node.Bounds.X > Find("Here", UiAccessibilityRole.Label).Node.Bounds.X
                && !rtl.AsSpan().SequenceEqual(narrow), "GPU Breadcrumb RTL changes item positions and chevron direction : " + size);
            check(rtl.AsSpan().SequenceEqual(capture(size + "-rtl-replay", Draw)), "GPU Breadcrumb RTL replay is exact : " + size);
            check(context.State.NextScheduledFrameTimeSeconds is null, "GPU Breadcrumb static row requests no periodic frames : " + size);
            context.SetAccessibilityTree(null);
        }
    }
}
