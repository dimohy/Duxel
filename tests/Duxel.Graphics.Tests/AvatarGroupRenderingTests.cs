using Duxel.Core;

internal static class AvatarGroupRenderingTests
{
    internal static void Run(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool, string> check,
        UiContext context, int stride)
    {
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        UiImageSource Source(byte r, byte g, byte b) => new(new(1, 1, [r, g, b, 255]));
        UiAvatarGroupItem[] members = [new("red", "Red") { Source = Source(255, 0, 0) },
            new("green", "Green") { Source = Source(0, 255, 0) }, new("blue", "Blue") { Source = Source(0, 0, 255) }];
        using var group = Fluent.AvatarGroup("GPU people", members,
            new(UiAvatarSize.Size64, UiAvatarGroupLayout.Pie));
        void Draw(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding: 0);
            var paint = new UiControlContext(ui);
            paint.Frame(paint.ClipRect, new(30, 70, 90), default, 0, 0);
            ui.SetCursorScreenPos(new(8, 16));
            group.Render(ui);
        }
        bool Pixel(byte[] data, int x, int y, byte r, byte g, byte b)
        {
            var i = (y * stride + x) * 4;
            return data[i] == r && data[i + 1] == g && data[i + 2] == b;
        }
        UiAccessibilityElement Find(string name)
        {
            UiAccessibilityElement? result = null;
            void Visit(string? id)
            {
                while (id is not null && tree.TryGet(id, out var item))
                {
                    if (item.Node.Name == name)
                        result = item;
                    Visit(item.FirstChild);
                    id = item.Next;
                }
            }
            Visit(tree.Root.First);
            return result ?? throw new InvalidOperationException(name);
        }
        var pie = capture("pie-three", Draw);
        check(Pixel(pie, 24, 48, 255, 0, 0) && Pixel(pie, 56, 32, 0, 255, 0) && Pixel(pie, 56, 64, 0, 0, 255),
            "GPU AvatarGroup pie paints actual image halves and quarters");
        check(Pixel(pie, 9, 17, 30, 70, 90) && Pixel(pie, 40, 48, 30, 70, 90),
            "GPU AvatarGroup pie uses the common analytic circular clip and transparent dividers");
        check(pie.AsSpan().SequenceEqual(capture("pie-three-replay", Draw)), "GPU AvatarGroup unchanged pie replays exactly");
        group.Options = group.Options with
        {
            RightToLeft = true
        };
        var rtl = capture("pie-rtl", Draw);
        check(Pixel(rtl, 56, 48, 255, 0, 0) && Pixel(rtl, 24, 32, 0, 255, 0) && Pixel(rtl, 24, 64, 0, 0, 255),
            "GPU AvatarGroup RTL mirrors pie geometry and preserves source member identity");
        check(rtl.AsSpan().SequenceEqual(capture("pie-rtl-replay", Draw)), "GPU AvatarGroup unchanged RTL pie replays exactly");
        var button = Find("More people (3)");
        tree.RequestAction(button.Node.Id, button.RuntimeId, UiAccessibilityAction.Expand, wait: false);
        capture("pie-menu-plan", Draw);
        var menu = capture("pie-menu", Draw);
        check(group.IsOverflowOpen && Find("Green").Node.Role == UiAccessibilityRole.MenuItem
            && !menu.AsSpan().SequenceEqual(rtl), "GPU AvatarGroup pie opens a real named avatar menu outside the circle");
        check(menu.AsSpan().SequenceEqual(capture("pie-menu-replay", Draw)), "GPU AvatarGroup unchanged overflow menu replays exactly");
        var green = Find("Green");
        var greenArea = 0;
        for (var y = (int)green.Node.Bounds.Y; y < green.Node.Bounds.Y + green.Node.Bounds.Height; y++)
            for (var x = (int)green.Node.Bounds.X; x < green.Node.Bounds.X + green.Node.Bounds.Width; x++)
                if (Pixel(menu, x, y, 0, 255, 0))
                    greenArea++;
        check(green.Node.Bounds.Height == 72 && greenArea > 2500,
            "GPU AvatarGroup overflow preserves the group's64px avatar size and full image content");
        tree.RequestAction(green.Node.Id, green.RuntimeId, UiAccessibilityAction.Invoke, wait: false);
        capture("pie-menu-dismiss", Draw);
        capture("pie-menu-settled", Draw);
        group.Options = group.Options with
        {
            Layout = UiAvatarGroupLayout.Stack,
            RightToLeft = false
        };
        var stack = capture("stack", Draw);
        check(Pixel(stack, 24, 50, 255, 0, 0) && Pixel(stack, 88, 50, 0, 255, 0) && Pixel(stack, 150, 50, 0, 0, 255),
            "GPU AvatarGroup stack paints original image sources in the intended overlap order");
        check(stack.AsSpan().SequenceEqual(capture("stack-replay", Draw)), "GPU AvatarGroup unchanged stack replays exactly");
        group.Options = group.Options with
        {
            Layout = UiAvatarGroupLayout.Spread
        };
        var spread = capture("spread", Draw);
        check(!spread.AsSpan().SequenceEqual(stack) && Pixel(spread, 108, 48, 0, 255, 0),
            "GPU AvatarGroup spread restores the pinned positive spacing");
        check(spread.AsSpan().SequenceEqual(capture("spread-replay", Draw)), "GPU AvatarGroup unchanged spread replays exactly");
        using var pair = Fluent.AvatarGroup("Pair", members[..2], new(UiAvatarSize.Size64, UiAvatarGroupLayout.Pie));
        var two = capture("pie-two", ui =>
        {
            ui.EnableRootViewportContentLayout(contentPadding: 0);
            ui.SetCursorScreenPos(new(8, 16));
            pair.Render(ui);
        });
        check(Pixel(two, 24, 48, 255, 0, 0) && Pixel(two, 56, 48, 0, 255, 0),
            "GPU AvatarGroup two-member pie paints both full-height image halves");
        check(context.State.NextScheduledFrameTimeSeconds is null, "GPU AvatarGroup schedules no idle animation");
    }
}
