using System.Text.Json;
using Duxel.Core;
using Duxel.Gallery;

internal static class FluentMenuTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "MenuPalette.json")));
        foreach (var entry in fixture.RootElement.EnumerateArray())
        {
            var scheme = Enum.Parse<UiFluentColorScheme>(entry.GetProperty("scheme").GetString()!);
            var state = Enum.Parse<UiButtonVisualState>(entry.GetProperty("state").GetString()!);
            UiColor Color(string name)
            {
                var values = entry.GetProperty(name).EnumerateArray().Select(value => value.GetByte()).ToArray();
                return new(values[0], values[1], values[2]);
            }
            var actual = UiFluentMenuPalette.Resolve(scheme, state);
            check(actual.Background == Color("background") && actual.Foreground == Color("foreground"),
                "Fluent menu colors match the independent pinned official token fixture : " + scheme + " / " + state);
        }
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        using var tree = new UiAccessibilityTree();
        ControlFontFixture.Configure(context);
        context.SetAccessibilityTree(tree);
        using var menu = Fluent.Menu("Menu", [new("one", "One"), new("two", "Two"), new("three", "Three")], static _ => { });
        UiWidget current = menu;
        Action<UiControlContext>? inspectText = null;
        var frameSize = new UiVector2(640, 400);
        UiVector2? triggerPosition = null;
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame(UiInputState? input = null)
        {
            context.NewFrame(new(1f / 60, frameSize, new(1, 1)), input ?? idle,
                new(0, 0, frameSize.X, frameSize.Y), UiTextSettings.Default);
            context.Render(new Screen(ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding : 0);
                if (triggerPosition is { } position)
                    ui.SetCursorScreenPos(position);
                inspectText?.Invoke(new(ui));
                current.Render(ui);
            }));
            using var draw = context.GetDrawData();
        }
        Frame();
        tree.TryGet(tree.Root.First!, out var trigger);
        tree.RequestAction(trigger.Node.Id, trigger.RuntimeId, UiAccessibilityAction.Invoke, wait : false);
        Frame();
        tree.TryGet(trigger.Node.Id, out trigger);
        tree.TryGet(trigger.Next!, out var surface);
        check(menu is UiFluentMenu && Fluent.ComponentNames.Contains("Menu") && !Fluent.MissingComponentNames.Contains("Menu"),
            "the generated Fluent Menu factory and exposed catalog identify the actual compiled presentation");
        check(surface.Node.Bounds.Width == 138 && surface.Node.Bounds.Height == 110,
            "actual Fluent menu geometry follows 138 minimum width, 5 inset, 32 rows and 2 gaps");
        tree.TryGet(surface.FirstChild!, out var first);
        tree.TryGet(first.Next!, out var second);
        check(first.Node.Bounds.X == surface.Node.Bounds.X + 5 && first.Node.Bounds.Y == surface.Node.Bounds.Y + 5
            && second.Node.Bounds.Y - first.Node.Bounds.Y == 34, "actual Fluent menu child allocations include independent official insets and list gap");
        menu.SetItems([new("wide", new string('W', 200))]);
        Frame();
        tree.TryGet(trigger.Next!, out surface);
        check(surface.Node.Bounds.Width == 300, "actual Fluent long-label menu respects the official maximum surface width");
        const string longLabel = "Export the selected documents and include all comments and revision history";
        menu.SetItems([new("wide", longLabel) { SecondaryContent = "Ctrl+E" }, new("next", "Next")]);
        Frame();
        tree.TryGet(trigger.Next!, out surface);
        tree.TryGet(surface.FirstChild!, out first);
        tree.TryGet(first.Next!, out second);
        check(first.Node.Name == longLabel && first.Node.Hint == "Ctrl+E" && first.Node.Bounds.Height >= 52
            && second.Node.Bounds.Y == first.Node.Bounds.Y + first.Node.Bounds.Height + 2,
            "Fluent labels wrap beside trailing content and move the next accessible row");
        menu.Options = menu.Options with { RightToLeft = true, MaximumHeight = 76 };
        Frame();
        tree.TryGet(trigger.Next!, out surface);
        check(surface.Node.Scroll is { } scroll && scroll.Content.Y > scroll.Viewport.Y,
            "wrapped RTL rows contribute their complete height to the menu scroll range");
        Frame(idle with { KeyEvents = [new(UiKey.End, true, KeyModifiers.None)] });
        tree.TryGet(second.Node.Id, out second);
        check(second.Node.Focused && !second.Node.Offscreen && second.Node.Bounds.Height == 32,
            "End reveals the complete short row following a wrapped row");
        const string korean = "선택한문서를변경이력과함께내보내고공유용파일에저장하기";
        const string chinese = "选择文档并将修订记录和所有批注一起导出到存档文件";
        const string japanese = "選択した文書（変更履歴を含む）を別のファイルに書き出す";
        var cjkFonts = GalleryText.ConfigureFonts(new());
        context.SetDirectTextSecondaryFontPath(cjkFonts.DirectTextSecondaryFontPath);
        context.SetTypographyFonts(cjkFonts.Typography);
        menu.Options = menu.Options with { RightToLeft = false, MaximumHeight = null, MinimumWidth = 150, MaximumWidth = 150 };
        menu.SetItems([new("ko", korean), new("zh", chinese), new("ja", japanese)]);
        var verifiedText = false;
        inspectText = text =>
        {
            if (verifiedText)
                return;
            verifiedText = true;
            using var typography = text.BeginTypography<UiRegularTypeface, UiFluentType300>();
            foreach (var value in new[] { korean, chinese, japanese })
            {
                var block = UiMenuContentLayout.MeasureLabel(text, value, 100, wrap : true);
                var lines = Enumerable.Range(0, block.LineCount).Select(index => block.GetLine(index).Text).ToArray();
                check(block.LineCount > 1 && block.Size.X <= 100 && string.Concat(lines) == value
                    && lines.All(line => !line.StartsWith('）') && !line.EndsWith('（')),
                    "CJK menu wraps without spaces, preserving label content and bracket boundaries");
            }
            string WrapAt(string label, string first) => UiMenuLabelWrapping.Wrap(text, label, text.MeasureText(first).X + .01f);
            check(WrapAt("漢字、漢字", "漢字") == "漢\n字、\n漢字"
                && WrapAt("漢字（漢字）", "漢字（") == "漢字\n（漢\n字）",
                "CJK menu retains Japanese punctuation with adjacent graphemes at forced boundaries");
            check(UiMenuLabelWrapping.Wrap(text, "\r\n漢字\r\n\r\n次\r\n", 100) == "\n漢字\n\n次\n",
                "CJK menu keeps leading, empty and trailing lines while normalizing CRLF");
            const string mixed = "漢字PDF文書";
            var mixedLines = UiMenuLabelWrapping.Wrap(text, mixed, text.MeasureText("PDF").X + .01f);
            check(mixedLines.Split('\n').Any(line => line.Contains("PDF", StringComparison.Ordinal))
                && mixedLines.Replace("\n", string.Empty, StringComparison.Ordinal) == mixed,
                "CJK menu wrapping preserves Latin words within mixed labels");
        };
        Frame();
        inspectText = null;
        var cjkRows = tree.CaptureSnapshot().Elements.Where(entry => entry.Node.Role == UiAccessibilityRole.MenuItem)
            .OrderBy(entry => entry.Node.Bounds.Y).ToArray();
        check(cjkRows.Length == 3 && cjkRows.Select(entry => entry.Node.Name).SequenceEqual([korean, chinese, japanese])
            && cjkRows.All(entry => entry.Node.Bounds.Height >= 52),
            "Fluent menu wraps Korean, Chinese and Japanese rows while retaining full accessible names : "
            + string.Join(", ", cjkRows.Select(entry => $"{entry.Node.Name} / {entry.Node.Bounds.Height}")));
        ControlFontFixture.Configure(context);
        var invalid = false;
        try { menu.Options = menu.Options with { ItemSpacing = -1 }; }
        catch (ArgumentOutOfRangeException) { invalid = true; }
        check(invalid && menu.Options.ItemSpacing == 2, "invalid item spacing fails before replacing valid menu options");
        using var menuButton = Fluent.MenuButton("Export", [new("pdf", "PDF")], static _ => { });
        check(menuButton is UiFluentMenuButton && Fluent.ComponentNames.Contains("MenuButton")
            && !Fluent.RequiredComponentNames.Contains("MenuButton"),
            "the generated MenuButton factory exposes the Fluent trigger as a Button variant");

        var invocations = new List<string>();
        using var custom = new UiMenu<PressPopupBehavior, UiFluentMenuPresentation>("Custom menu", [new("run", "Run")], invocations.Add);
        current = custom;
        Frame();
        UiAccessibilityElement Find(string name, UiAccessibilityRole role)
            => tree.CaptureSnapshot().Elements.Single(element => element.Node.Name == name && element.Node.Role == role);
        UiVector2 Center(UiAccessibilityElement element)
        {
            var bounds = element.Node.Bounds;
            return new(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
        }
        var point = Center(Find("Custom menu", UiAccessibilityRole.Button));
        Frame(idle with { MousePosition = point, LeftMousePressed = true, LeftMouseReleased = true });
        check(custom.IsOpen && invocations.Count == 0, "Custom Menu opens on a single-frame press and release");
        var command = Center(Find("Run", UiAccessibilityRole.MenuItem));
        Frame(idle with { MousePosition = command, LeftMousePressed = true, LeftMouseDown = true });
        check(!custom.IsOpen && invocations.SequenceEqual(["run"]), "Custom Menu invokes and closes on row press");
        Frame(idle with { MousePosition = command, LeftMouseReleased = true });
        check(!custom.IsOpen && invocations.SequenceEqual(["run"]), "Custom Menu does not invoke again on row release");

        invocations.Clear();
        using var nested = Fluent.Menu("Actions", [UiMenuOption.Submenu("export", "Export",
            [new("pdf", "PDF"), UiMenuOption.Submenu("advanced", "Advanced", [new("archive", "Archive")])]),
            new("other", "Other")], invocations.Add);
        current = nested;
        foreach (var rtl in new[] { false, true })
        {
            nested.Options = nested.Options with { RightToLeft = rtl };
            Frame();
            var nestedTrigger = Find("Actions", UiAccessibilityRole.Button);
            void ActionFor(UiAccessibilityElement element, UiAccessibilityAction action)
            {
                check(tree.RequestAction(element.Node.Id, element.RuntimeId, action, wait : false) == UiAccessibilityResult.Success,
                    $"nested menu accepts {action} on {element.Node.Name}");
                Frame();
            }
            void Key(UiKey key) => Frame(idle with { KeyEvents = [new(key, true, KeyModifiers.None)] });
            ActionFor(nestedTrigger, UiAccessibilityAction.Expand);
            var branch = Find("Export", UiAccessibilityRole.MenuItem);
            check(branch.Node.Focused && branch.Node.Expansion == UiAccessibilityExpansionState.Collapsed,
                "nested menu trigger is a focused collapsed MenuItem");
            Key(rtl ? UiKey.LeftArrow : UiKey.RightArrow);
            branch = Find("Export", UiAccessibilityRole.MenuItem);
            var child = Find("Export", UiAccessibilityRole.Menu);
            check(nested.IsOpen && branch.Node.Expansion == UiAccessibilityExpansionState.Expanded
                && child.Node.ParentId == branch.Node.Id && Find("PDF", UiAccessibilityRole.MenuItem).Node.Focused,
                "logical arrow opens the child popup and focuses its first item");
            Key(rtl ? UiKey.RightArrow : UiKey.LeftArrow);
            Frame();
            branch = Find("Export", UiAccessibilityRole.MenuItem);
            check(nested.IsOpen && branch.Node.Expansion == UiAccessibilityExpansionState.Collapsed
                && branch.Node.Focused && !tree.TryGet(child.Node.Id, out _),
                "reverse arrow closes only the submenu and restores parent row focus");
            ActionFor(branch, UiAccessibilityAction.Expand);
            ActionFor(Find("Advanced", UiAccessibilityRole.MenuItem), UiAccessibilityAction.Expand);
            check(Find("Archive", UiAccessibilityRole.MenuItem).Node.Focused && invocations.Count == (rtl ? 1 : 0),
                "nested expansion focuses the deepest leaf without invoking a command");
            ActionFor(Find("Archive", UiAccessibilityRole.MenuItem), UiAccessibilityAction.Invoke);
            check(!nested.IsOpen && invocations.Count == (rtl ? 2 : 1) && invocations[^1] == "archive",
                "nested leaf invokes exactly once and dismisses the whole menu");
            Frame();
        }
        invocations.Clear();
        nested.Options = nested.Options with { RightToLeft = false };
        var pointerTrigger = Find("Actions", UiAccessibilityRole.Button);
        tree.RequestAction(pointerTrigger.Node.Id, pointerTrigger.RuntimeId, UiAccessibilityAction.Expand, wait : false);
        Frame();
        var hoverBranch = Find("Export", UiAccessibilityRole.MenuItem);
        var hover = idle with { MousePosition = Center(hoverBranch) };
        Frame(hover);
        for (var i = 0; i < 29; i++)
            Frame(hover);
        check(Find("Export", UiAccessibilityRole.MenuItem).Node.Expansion == UiAccessibilityExpansionState.Collapsed
            && context.State.NextScheduledFrameTimeSeconds is not null,
            "pointer movement schedules the Fluent submenu without opening it before the hover delay");
        for (var i = 0; i < 3; i++)
            Frame(hover);
        var pointerLeaf = Find("PDF", UiAccessibilityRole.MenuItem);
        check(pointerLeaf.Node.Focused && !context.State.NavCursorVisible, "hover opens and focuses the submenu without a keyboard focus ring");
        var overLeaf = idle with { MousePosition = Center(pointerLeaf) };
        for (var i = 0; i < 33; i++)
            Frame(overLeaf);
        check(Find("Export", UiAccessibilityRole.MenuItem).Node.Expansion == UiAccessibilityExpansionState.Expanded,
            "moving into the child surface keeps it open beyond the hover deadline");
        for (var i = 0; i < 33; i++)
            Frame();
        check(nested.IsOpen && !tree.TryGet(pointerLeaf.Node.Id, out _)
            && Find("Export", UiAccessibilityRole.MenuItem).Node.Expansion == UiAccessibilityExpansionState.Collapsed,
            "leaving the branch and child closes only the submenu after the hover delay");
        Frame(hover with { LeftMousePressed = true, LeftMouseReleased = true });
        pointerLeaf = Find("PDF", UiAccessibilityRole.MenuItem);
        Frame(idle with { MousePosition = Center(pointerLeaf), LeftMousePressed = true, LeftMouseReleased = true });
        check(!nested.IsOpen && invocations.SequenceEqual(["pdf"]), "pointer click reaches the child command and closes the entire branch");
        foreach (var (width, rtl) in new[] { (350f, false), (220f, false), (130f, false), (350f, true), (220f, true) })
        {
            frameSize = new(width, 240);
            triggerPosition = new(rtl ? 8 : MathF.Max(0, width - 145), 12);
            nested.Options = nested.Options with { RightToLeft = rtl };
            Frame();
            var menuTrigger = Find("Actions", UiAccessibilityRole.Button);
            tree.RequestAction(menuTrigger.Node.Id, menuTrigger.RuntimeId, UiAccessibilityAction.Expand, wait : false);
            Frame();
            var narrowBranch = Find("Export", UiAccessibilityRole.MenuItem);
            tree.RequestAction(narrowBranch.Node.Id, narrowBranch.RuntimeId, UiAccessibilityAction.Expand, wait : false);
            Frame();
            var narrowChild = Find("Export", UiAccessibilityRole.Menu);
            var rect = narrowChild.Node.Bounds;
            var correctSide = rtl ? rect.X >= narrowBranch.Node.Bounds.X : rect.X < narrowBranch.Node.Bounds.X;
            check(rect.X >= 0 && rect.X + rect.Width <= width && rect.Width == MathF.Min(width, 138) && correctSide,
                $"narrow viewport keeps the complete side-flipped submenu reachable : {width}px / RTL={rtl} / submenu {rect} / branch {narrowBranch.Node.Bounds}");
            if (width == 220)
            {
                var leaf = Find("PDF", UiAccessibilityRole.MenuItem).Node.Bounds;
                var parent = narrowBranch.Node.Bounds;
                var overlapLeft = MathF.Max(leaf.X, parent.X);
                var overlapRight = MathF.Min(leaf.X + leaf.Width, parent.X + parent.Width);
                var overlapTop = MathF.Max(leaf.Y, parent.Y);
                var overlapBottom = MathF.Min(leaf.Y + leaf.Height, parent.Y + parent.Height);
                check(overlapRight > overlapLeft && overlapBottom > overlapTop, "narrow pointer test intersects the parent and child rows");
                var overlapCenter = new UiVector2((overlapLeft + overlapRight) * .5f, (overlapTop + overlapBottom) * .5f);
                var beforeClick = invocations.Count;
                Frame(idle with { MousePosition = overlapCenter, LeftMousePressed = true, LeftMouseReleased = true });
                check(!nested.IsOpen && invocations.Count == beforeClick + 1 && invocations[^1] == "pdf",
                    "the child receives the overlapping pointer click exactly once");
                continue;
            }
            tree.RequestAction(menuTrigger.Node.Id, menuTrigger.RuntimeId, UiAccessibilityAction.Collapse, wait : false);
            Frame();
        }
        frameSize = new(130, 260);
        triggerPosition = new(0, 12);
        nested.Options = nested.Options with { RightToLeft = false };
        nested.SetItems([new("one", "One"), new("two", "Two"), new("three", "Three"),
            UiMenuOption.Submenu("export", "Export", [new("pdf", "PDF"), new("png", "PNG")])]);
        Frame();
        var fullWidthTrigger = Find("Actions", UiAccessibilityRole.Button);
        tree.RequestAction(fullWidthTrigger.Node.Id, fullWidthTrigger.RuntimeId, UiAccessibilityAction.Expand, wait : false);
        Frame();
        var fullWidthBranch = Find("Export", UiAccessibilityRole.MenuItem);
        tree.RequestAction(fullWidthBranch.Node.Id, fullWidthBranch.RuntimeId, UiAccessibilityAction.Expand, wait : false);
        Frame();
        var above = Find("Export", UiAccessibilityRole.Menu).Node.Bounds;
        check(above.Width == frameSize.X && above.X == 0 && above.Y >= 0
            && above.Y + above.Height <= fullWidthBranch.Node.Bounds.Y,
            $"full-width submenu opens above its trigger when vertical room is available : submenu {above} / branch {fullWidthBranch.Node.Bounds}");
        tree.RequestAction(fullWidthTrigger.Node.Id, fullWidthTrigger.RuntimeId, UiAccessibilityAction.Collapse, wait : false);
        Frame();
        frameSize = new(640, 400);
        triggerPosition = new(245, 12);
        nested.SetItems([UiMenuOption.Submenu("export", "Export",
            [new("pdf", "PDF"), new("png", "PNG"), new("svg", "SVG"), new("text", "Text")]),
            UiMenuOption.Submenu("recent", "Recent", [new("local", "Local file")])]);
        foreach (var (rtl, protection) in new (bool, double?)[] { (false, null), (false, .3), (true, .3) })
        {
            nested.Options = nested.Options with { RightToLeft = rtl, SubmenuPointerProtectionSeconds = protection };
            Frame();
            var root = Find("Actions", UiAccessibilityRole.Button);
            tree.RequestAction(root.Node.Id, root.RuntimeId, UiAccessibilityAction.Expand, wait : false);
            Frame();
            var from = Find("Export", UiAccessibilityRole.MenuItem);
            tree.RequestAction(from.Node.Id, from.RuntimeId, UiAccessibilityAction.Expand, wait : false);
            Frame();
            var childMenu = Find("Export", UiAccessibilityRole.Menu);
            var sibling = Find("Recent", UiAccessibilityRole.MenuItem);
            var startX = rtl ? from.Node.Bounds.X + from.Node.Bounds.Width - 15 : from.Node.Bounds.X + 15;
            var edgeX = rtl ? sibling.Node.Bounds.X + 8 : sibling.Node.Bounds.X + sibling.Node.Bounds.Width - 8;
            var siblingY = Center(sibling).Y;
            Frame(idle with { MousePosition = new(startX, Center(from).Y) });
            for (var i = 0; i < 38; i++)
                Frame(idle with { MousePosition = new(edgeX + i % 2, siblingY) });
            var protectedBranch = Find("Export", UiAccessibilityRole.MenuItem);
            check((protectedBranch.Node.Expansion == UiAccessibilityExpansionState.Expanded) == protection.HasValue,
                $"diagonal movement past a sibling follows the submenu pointer protection option : RTL={rtl}, timeout={protection}");
            if (protection.HasValue && !rtl)
            {
                var inside = Find("PNG", UiAccessibilityRole.MenuItem);
                for (var i = 0; i < 38; i++)
                    Frame(idle with { MousePosition = Center(inside) });
                check(tree.TryGet(childMenu.Node.Id, out var arrived) && arrived.RuntimeId == childMenu.RuntimeId,
                    "entering the child cancels the pointer-protection timeout and retains its runtime identity");
            }
            if (protection.HasValue && rtl)
            {
                var stopped = idle with { MousePosition = new(edgeX + 1, siblingY) };
                for (var i = 0; i < 21; i++)
                    Frame(stopped);
                check(!tree.TryGet(childMenu.Node.Id, out _) && Find("Recent", UiAccessibilityRole.MenuItem).Node.Expansion == UiAccessibilityExpansionState.Expanded,
                    "stationary pointer-protection timeout opens the sibling under the pointer");
            }
            tree.RequestAction(root.Node.Id, root.RuntimeId, UiAccessibilityAction.Collapse, wait : false);
            Frame();
        }
        invocations.Clear();
        using var described = Fluent.Menu("Clipboard",
        [
            new("cut", "Cut") { SubText = "Cut to clipboard", SecondaryContent = "Ctrl+X" },
            new("paste", "Paste") { SubText = "Paste from clipboard\nKeep the original formatting" },
            new("edit", "Edit", false) { SubText = "Edit file" }
        ], invocations.Add, new UiMenuOptions(150, 32, 5, ItemSpacing : 2, MaximumWidth : 150));
        current = described;
        Frame();
        var describedTrigger = Find("Clipboard", UiAccessibilityRole.Button);
        tree.RequestAction(describedTrigger.Node.Id, describedTrigger.RuntimeId, UiAccessibilityAction.Expand, wait : false);
        Frame();
        var cut = Find("Cut Cut to clipboard", UiAccessibilityRole.MenuItem);
        var paste = Find("Paste Paste from clipboard Keep the original formatting", UiAccessibilityRole.MenuItem);
        var edit = Find("Edit Edit file", UiAccessibilityRole.MenuItem);
        check(cut.Node.Bounds.Height == 46 && cut.Node.Hint == "Ctrl+X"
            && paste.Node.Bounds.Height >= 60 && paste.Node.Bounds.Y == cut.Node.Bounds.Y + 48
            && !edit.Node.Enabled && edit.Node.Bounds.Height == 46,
            "Fluent subtext sets row height, contributes to Name and keeps the independent shortcut hint");
        Frame(idle with { CharEvents = [new('p')] });
        check(Find(paste.Node.Name, UiAccessibilityRole.MenuItem).Node.Focused,
            "subtext rows retain label-based typeahead");
        Frame(idle with { KeyEvents = [new(UiKey.Enter, true, KeyModifiers.None)] });
        check(!described.IsOpen && invocations.SequenceEqual(["paste"]), "keyboard activates the described row's original command");
    }
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}

internal readonly struct PressPopupBehavior : IUiMenuBehavior
{
    public static UiButtonActivation TriggerActivation => UiButtonActivation.Press;
    public static UiButtonActivation OptionActivation => UiButtonActivation.Press;
}
