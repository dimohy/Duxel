using Duxel.Core;
using Duxel.Gallery;

internal static class ObserverNarrowNavigationTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        using var tree = new UiAccessibilityTree();
        using var screen = new ObserverScreen(ObserverServices.CreateWindows());
        ControlFontFixture.Configure(context);
        context.SetAccessibilityTree(tree);
        UiInputState Input(UiVector2? point = null, bool down = false, bool pressed = false, bool released = false) =>
            new(point ?? new(-100, -100), down, false, pressed, released, false, false, 0, 0, [], [], default, default);
        void Frame(UiInputState? input = null)
        {
            context.NewFrame(new(1f / 60, new(320, 900), new(1, 1)), input ?? Input(), new(0, 0, 320, 900), UiTextSettings.Default);
            context.Render(screen);
            using var draw = context.GetDrawData();
        }
        UiAccessibilityElement? Find(string name, UiAccessibilityRole role)
        {
            UiAccessibilityElement? result = null;
            void Visit(string? id)
            {
                while (id is not null && tree.TryGet(id, out var entry))
                {
                    if (entry.Node.Name == name && entry.Node.Role == role)
                        result = entry;
                    Visit(entry.FirstChild);
                    id = entry.Next;
                }
            }
            Visit(tree.Root.First);
            return result;
        }
        void Click(UiAccessibilityElement entry)
        {
            var rect = entry.Node.Bounds;
            var point = new UiVector2(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);
            Frame(Input(point, down: true, pressed: true));
            Frame(Input(point, released: true));
        }
        void OpenNavigation()
        {
            if (screen.CompactNavigationOpen) return;
            var button = Find("탐색 메뉴 열기", UiAccessibilityRole.Button) ?? throw new InvalidOperationException("Compact navigation trigger is missing.");
            Click(button);
            context.State.AdvanceTime(.3f);
            Frame();
            check(screen.CompactNavigationOpen, "gallery compact navigation opens through its actual visible trigger");
        }
        Frame();
        check(!screen.CompactNavigationOpen && Find(screen.SelectedNavigationLabel, UiAccessibilityRole.Pane) is { } initial
            && initial.Node.LabeledBy is null && initial.Node.Bounds.Width >= 300,
            "gallery compact main content is named and uses the full available width while navigation is closed");
        OpenNavigation();
        var required = Fluent.RequiredComponentNames.ToArray().ToDictionary(static name => name, StringComparer.Ordinal);
        var missing = Fluent.MissingComponentNames.ToArray().ToHashSet(StringComparer.Ordinal);
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(9));
        var navigation = tree.CaptureSnapshot().Elements;
        var componentTabs = navigation.Where(element => element.Node.Role == UiAccessibilityRole.Link && required.ContainsKey(element.Node.Name)).ToArray();
        check(componentTabs.Length == required.Count && componentTabs.Select(static element => element.Node.Name).Distinct(StringComparer.Ordinal).Count() == required.Count,
            "gallery lists every Fluent component exactly once using its canonical name");
        check(componentTabs.All(element =>
        {
            var component = required[element.Node.Name];
            var badge = missing.Contains(component) ? GalleryText.Get("Planned")
                : GalleryNavigation.IsNewMenu(component, today) ? GalleryText.Get("NEW") : null;
            return element.Node.Hint == badge;
        }), "gallery pending and NEW badges expose distinct status without changing component names or disabling navigation");
        foreach (var group in componentTabs.GroupBy(static element => element.Node.ParentId))
        {
            var names = group.Select(element => required[element.Node.Name]).ToArray();
            check(names.SequenceEqual(names.Order(StringComparer.Ordinal)), "gallery canonical component names are alphabetic inside their group : " + group.Key);
        }
        UiAccessibilityNode NavigationBrand() => tree.CaptureSnapshot().Elements.Single(element =>
            element.Node.Name == "Duxel 갤러리" && element.Node.Role == UiAccessibilityRole.Label).Node;
        var brandBefore = NavigationBrand().Bounds;
        check(Find("갤러리 탐색", UiAccessibilityRole.Navigation) is { } start && !start.Node.Offscreen
            && start.Node.Bounds.X < 120,
            "gallery narrow viewport exposes its grouped vertical navigation on the left");
        void Navigate(string page)
        {
            OpenNavigation();
            var label = screen.NavigationLabelForPage(page);
            var header = Find(label, UiAccessibilityRole.Link) ?? throw new InvalidOperationException("Missing page tab : " + page);
            if (!header.Node.Offscreen)
            {
                Click(header);
                check(screen.SelectedPage == page, "gallery compact visible destination selects its real page : " + page);
                OpenNavigation();
                return;
            }
            if (tree.RequestAction(header.Node.Id, header.RuntimeId, UiAccessibilityAction.ScrollIntoView, wait: false) != UiAccessibilityResult.Success)
                throw new InvalidOperationException("Gallery page cannot scroll into view : " + page);
            Frame();
            Frame();
            header = Find(label, UiAccessibilityRole.Link)!.Value;
            if (header.Node.Offscreen)
                throw new InvalidOperationException("Gallery page remained outside the navigation viewport : " + page);
            Click(header);
            check(screen.SelectedPage == page && !screen.CompactNavigationOpen,
                "gallery compact destination selects its page and closes the modal navigation : " + page);
            OpenNavigation();
        }
        foreach (var page in new[] { "컨테이너", "기존 API", "Fluent", "타이포그래피", "입력·IME", "Tooltip", "가상 목록", "렌더링", "SVG", "관찰 범위" })
        {
            Navigate(page);
            check(screen.SelectedPage == page && Find(screen.SelectedNavigationLabel, UiAccessibilityRole.Link)!.Value.Node.IsCurrent == true
                && Find(screen.SelectedNavigationLabel, UiAccessibilityRole.Pane)!.Value.Node.Bounds.Height > 0,
                "gallery actual narrow sidebar navigation renders the persistent page in the same frame : " + page);
            Frame();
        }
        Navigate("개요");
        check(screen.SelectedPage == "개요" && screen.UpdateOnlyOnChange,
            "gallery sidebar navigation returns to its retained overview while preserving event-wait mode");
        foreach (var component in Fluent.MissingComponentNames.ToArray())
        {
            Navigate(component);
            check(screen.SelectedPage == component && tree.CaptureSnapshot().Elements.Any(static element => element.Node.Name == "아직 미구현됨"),
                "gallery planned component is navigable and explicitly reports no implementation : " + component);
        }
        var brandAfter = NavigationBrand();
        check(!brandAfter.Offscreen && brandAfter.Bounds == brandBefore,
            "gallery brand and summary header stay fixed while the navigation scrolls through every planned component");
        var visibleDestination = tree.CaptureSnapshot().Elements.First(element => element.Node.Role == UiAccessibilityRole.Link
            && !element.Node.Offscreen && element.Node.Bounds.X < 120);
        check(tree.RequestAction(visibleDestination.Node.Id, visibleDestination.RuntimeId, UiAccessibilityAction.Focus, wait: false) == UiAccessibilityResult.Success,
            "gallery narrow navigation accepts focus on a visible destination");
        Frame();
        var selectedBeforeArrows = screen.SelectedPage;
        Frame(Input() with { KeyEvents = [new(UiKey.End, true, default)] });
        Frame();
        Frame();
        check(Find("기존 API", UiAccessibilityRole.Link) is { } final && !final.Node.Offscreen
            && context.State.FocusedId == final.Node.Id && screen.SelectedPage == selectedBeforeArrows,
            "gallery End reveals and focuses the final offscreen destination without changing its page");
        Frame(Input() with { KeyEvents = [new(UiKey.Home, true, default)] });
        Frame();
        Frame();
        check(Find("시작", UiAccessibilityRole.Button) is { } first && !first.Node.Offscreen
            && context.State.FocusedId == first.Node.Id && screen.SelectedPage == selectedBeforeArrows,
            "gallery Home reveals and focuses the first offscreen category without moving the fixed header");
    }
}
