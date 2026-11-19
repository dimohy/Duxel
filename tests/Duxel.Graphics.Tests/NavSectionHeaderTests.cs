using Duxel.Core;

internal static class NavSectionHeaderTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        var value = new UiState<string?>("first");
        using var nav = Fluent.Nav("Section navigation", value, Groups());
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame(UiInputState? input = null)
        {
            context.NewFrame(new(.016f, new(400, 400), new(1, 1)), input ?? idle, new(0, 0, 400, 400), UiTextSettings.Default);
            context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding : 0); nav.Render(ui); }));
            using var data = context.GetDrawData();
        }
        UiAccessibilityElement Find(string name, UiAccessibilityRole role) => tree.CaptureSnapshot().Elements.Single(element => element.Node.Name == name && element.Node.Role == role);
        Frame();
        var header = Find("Components", UiAccessibilityRole.Label);
        var category = Find("Category", UiAccessibilityRole.Button);
        check(!header.Node.Focusable && !header.Node.SupportsInvoke && header.Node.Expansion is null && !header.Node.IsCurrent,
            "Nav section titles are passive labels without destination, invocation, expansion or keyboard focus");
        check(header.Node.Bounds.Height < category.Node.Bounds.Height,
            "short Nav section labels use compact caption geometry instead of full category rows");
        check(tree.RequestAction(header.Node.Id, header.RuntimeId, UiAccessibilityAction.Focus, wait : false) == UiAccessibilityResult.InvalidArgument,
            "Nav rejects focusing a decorative section heading");
        tree.RequestAction(category.Node.Id, category.RuntimeId, UiAccessibilityAction.Focus, wait : false);
        Frame();
        Frame(idle with { KeyEvents = [new(UiKey.Home, true, default)] });
        check(context.State.FocusedId == category.Node.Id, "Nav Home skips the leading section header");
        Frame(idle with { KeyEvents = [new(UiKey.End, true, default)] });
        check(context.State.FocusedId == Find("Last", UiAccessibilityRole.Link).Node.Id,
            "Nav End and arrow traversal preserve destinations across visual sections");
        Frame(idle with { KeyEvents = [new(UiKey.UpArrow, true, default)] });
        Frame(idle with { KeyEvents = [new(UiKey.UpArrow, true, default)] });
        check(context.State.FocusedId == Find("First", UiAccessibilityRole.Link).Node.Id,
            "Nav arrow traversal skips the middle section heading");
        var point = new UiVector2(header.Node.Bounds.X + 20, header.Node.Bounds.Y + header.Node.Bounds.Height / 2);
        Frame(idle with { MousePosition = point, LeftMouseDown = true, LeftMousePressed = true });
        Frame(idle with { MousePosition = point, LeftMouseReleased = true });
        check(value.Value == "first" && nav.IsExpanded("components"), "clicking a section heading does not select or collapse anything");
        foreach (var invalid in new[]
        {
            UiNavGroup.SectionHeader("invalid", "Invalid") with { Collapsible = true },
            UiNavGroup.SectionHeader("invalid", "Invalid") with { Items = new UiNavItem[] { new("child", "Child") } },
            UiNavGroup.SectionHeader("invalid", "Invalid") with { Description = "Not a category" }
        })
        {
            var rejected = false;
            try { using var bad = Fluent.Nav("Invalid navigation", new UiState<string?>(null), new[] { invalid }); }
            catch (ArgumentException) { rejected = true; }
            check(rejected, "Nav rejects a section heading with category-only content");
        }
    }

    internal static void RunGpu(string output, Action<bool, string> check)
    {
        Directory.CreateDirectory(output);
        foreach (var light in new[] { false, true })
        foreach (var rtl in new[] { false, true })
        {
            using var fixture = new PresentationReuseTests.Fixture(1, true, PresentationTestPlatform.Create(400, 320));
            fixture.Context.SetTheme(light ? UiCompiledDesign.Windows11.Theme : UiCompiledDesign.Windows11Dark.Theme);
            using var tree = new UiAccessibilityTree();
            fixture.Context.SetAccessibilityTree(tree);
            using var nav = Fluent.Nav("Section navigation", new UiState<string?>(null), Groups(), new(260, rtl));
            void Draw(UiImmediateContext ui)
            {
                ui.EnableRootViewportContentLayout(contentPadding : 0);
                ui.GetWindowDrawList().AddRectFilled(new(0, 0, fixture.FrameInfo.DisplaySize.X, fixture.FrameInfo.DisplaySize.Y), new UiControlContext(ui).Theme.WindowBg, ui.WhiteTextureId);
                nav.Render(ui);
            }
            var idle = fixture.Capture(Draw);
            var pixelWidth = (int)fixture.FrameInfo.DisplaySize.X;
            var pixelHeight = (int)fixture.FrameInfo.DisplaySize.Y;
            check(idle.Length == pixelWidth * pixelHeight * 4,
                "Nav pixel oracle uses the actual DPI-scaled framebuffer extent");
            var nodes = tree.CaptureSnapshot().Elements.Select(element => element.Node).ToArray();
            var header = nodes.Single(node => node.Name == "Components");
            var category = nodes.Single(node => node.Name == "Category" && node.Role == UiAccessibilityRole.Button);
            var child = nodes.Single(node => node.Name == "First");
            int LeadingInk(UiRect bounds)
            {
                for (var distance = 0; distance < 120; distance++)
                {
                    var x = (int)(rtl ? bounds.X + bounds.Width - 1 - distance : bounds.X + distance);
                    for (var y = (int)bounds.Y; y < (int)(bounds.Y + bounds.Height); y++)
                    {
                        var pixel = (y * pixelWidth + x) * 4;
                        if (light ? idle[pixel] < 160 && idle[pixel + 1] < 160 && idle[pixel + 2] < 160
                            : idle[pixel] > 100 && idle[pixel + 1] > 100 && idle[pixel + 2] > 100) return distance;
                    }
                }
                return -1;
            }
            var headerInk = LeadingInk(header.Bounds);
            var categoryInk = LeadingInk(category.Bounds);
            var childInk = LeadingInk(child.Bounds);
            check(headerInk is >= 10 and < 30 && categoryInk is >= 12 and < 30 && childInk is >= 46 and < 65,
                $"actual Nav pixels distinguish caption, category and indented child in {(light ? "light" : "dark")} / RTL {rtl}");
            var point = new UiVector2(header.Bounds.X + 30, header.Bounds.Y + header.Bounds.Height / 2);
            var input = new UiInputState(point, false, false, false, false, false, false, 0, 0, [], [], default, default);
            var hover = fixture.Capture(Draw, input);
            var held = fixture.Capture(Draw, input with { LeftMousePressed = true, LeftMouseDown = true });
            check(idle.AsSpan().SequenceEqual(hover) && idle.AsSpan().SequenceEqual(held),
                $"passive Nav section has no hover or pressed surface in {(light ? "light" : "dark")} / RTL {rtl}");
            BrowserPng.Write(Path.Combine(output, $"{(light ? "light" : "dark")}-{(rtl ? "rtl" : "ltr")}.png"), pixelWidth, pixelHeight, idle);
        }
    }

    private static UiNavGroup[] Groups() =>
    [
        UiNavGroup.SectionHeader("components", "Components"),
        new("category", "Category", new UiNavItem[] { new("first", "First") }),
        UiNavGroup.SectionHeader("tools", "Tools"),
        new("diagnostics", "Diagnostics", new UiNavItem[] { new("last", "Last") })
    ];
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
