using Duxel.Core;

internal static class BreadcrumbTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        var calls = new List<string>();
        using var breadcrumb = Fluent.Breadcrumb("Project path", [new("home", "Home"), new("category", "Category", false),
            new("folder", "Folder"), new("current", "Current")], calls.Add);
        using var root = Dux.ConstrainedBox(breadcrumb, new(0, 600, 0, 400));
        UiWidget current = root;
        var parentDisabled = false;
        void Frame(UiInputState? input = null)
        {
            context.NewFrame(new(1f / 60, new(640, 480), new(1, 1)), input ?? idle, new(0, 0, 640, 480), UiTextSettings.Default);
            context.Render(new Screen(ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding: 0);
                ui.BeginDisabled(parentDisabled);
                try
                {
                    current.Render(ui);
                }
                finally { ui.EndDisabled(); }
            }));
            using var data = context.GetDrawData();
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
            return found ?? throw new InvalidOperationException(name + ":" + role);
        }
        void Key(UiKey key) => Frame(idle with
        {
            KeyEvents = [new(key, true, default)]
        });
        void Click(UiAccessibilityElement element)
        {
            var rect = element.Node.Bounds;
            var point = new UiVector2(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);
            Frame(idle with
            {
                MousePosition = point,
                LeftMouseDown = true,
                LeftMousePressed = true
            });
            Frame(idle with
            {
                MousePosition = point,
                LeftMouseReleased = true
            });
        }
        Frame();
        var nav = Find("Project path", UiAccessibilityRole.Navigation);
        var list = Find(string.Empty, UiAccessibilityRole.OrderedList);
        check(list.Node.ParentId == nav.Node.Id && !nav.Node.Focusable && !list.Node.Focusable,
            "Breadcrumb exposes a named navigation landmark with an ordered child list");
        var home = Find("Home", UiAccessibilityRole.Button);
        var category = Find("Category", UiAccessibilityRole.Button);
        var last = Find("Current", UiAccessibilityRole.Label);
        var lastItem = Find("Current", UiAccessibilityRole.ListItem);
        check(last.Node.ParentId == lastItem.Node.Id && lastItem.Node.PositionInSet == 4 && lastItem.Node.SizeOfSet == 4
            && lastItem.Node.IsCurrent && !last.Node.Focusable && lastItem.Node.ItemStatus == "Current location",
            "Breadcrumb current location retains its ordered position and passive current-page semantics");
        check(!category.Node.Enabled && category.Node.DisabledFocusable && category.Node.CanReceiveFocus,
            "Breadcrumb disabledFocusable remains keyboard focusable while disabled");
        check(tree.RequestAction(category.Node.Id, category.RuntimeId, UiAccessibilityAction.Focus, wait: false) == UiAccessibilityResult.Success,
            "Breadcrumb disabledFocusable accepts native focus requests");
        Frame();
        check(context.State.FocusedId == category.Node.Id && tree.Root.Focus == category.Node.Id,
            "Breadcrumb disabledFocusable publishes actual disabled keyboard focus");
        Key(UiKey.Enter);
        Key(UiKey.Space);
        Frame(idle with
        {
            KeyEvents = [new(UiKey.Space, false, default)]
        });
        Click(category);
        check(calls.Count == 0 && tree.RequestAction(category.Node.Id, category.RuntimeId, UiAccessibilityAction.Invoke, wait: false) == UiAccessibilityResult.Disabled,
            "Breadcrumb disabledFocusable blocks pointer, Enter, Space and native Invoke");
        tree.RequestAction(home.Node.Id, home.RuntimeId, UiAccessibilityAction.Focus, wait: false);
        Frame();
        Key(UiKey.Tab);
        check(context.State.FocusedId == category.Node.Id, "Breadcrumb default Tab visits the disabled focusable level in source order");
        Key(UiKey.Tab);
        check(context.State.FocusedId == Find("Folder", UiAccessibilityRole.Button).Node.Id, "Breadcrumb Tab moves to the next actual interactive level");
        Click(home);
        check(calls.SequenceEqual(new[] { "home" }), "Breadcrumb pointer activation invokes the actual path value once");
        breadcrumb.Options = breadcrumb.Options with
        {
            FocusMode = UiBreadcrumbFocusMode.Arrow
        };
        Frame();
        tree.RequestAction(home.Node.Id, home.RuntimeId, UiAccessibilityAction.Focus, wait: false);
        Frame();
        Key(UiKey.RightArrow);
        check(context.State.FocusedId == category.Node.Id, "Breadcrumb arrow mode includes disabledFocusable without activating it");
        Key(UiKey.End);
        check(context.State.FocusedId == Find("Folder", UiAccessibilityRole.Button).Node.Id && calls.Count == 1,
            "Breadcrumb End reaches the last interactive level without changing the current location");
        breadcrumb.Options = breadcrumb.Options with
        {
            RightToLeft = true
        };
        Frame();
        tree.RequestAction(home.Node.Id, home.RuntimeId, UiAccessibilityAction.Focus, wait: false);
        Frame();
        Key(UiKey.LeftArrow);
        check(context.State.FocusedId == category.Node.Id && Find("Home", UiAccessibilityRole.Button).Node.Bounds.X > last.Node.Bounds.X,
            "Breadcrumb RTL reverses physical arrows and layout while retaining logical hierarchy");
        breadcrumb.Options = breadcrumb.Options with
        {
            RightToLeft = false,
            FocusMode = UiBreadcrumbFocusMode.Tab
        };
        Frame();
        root.Options = root.Options with
        {
            MaxWidth = home.Node.Bounds.Width + last.Node.Bounds.Width + 65
        };
        Frame();
        Frame();
        var overflow = Find("More levels", UiAccessibilityRole.Button);
        check(!overflow.Node.Offscreen && Find("Home", UiAccessibilityRole.Button).Node.Bounds.X == 0,
            "Breadcrumb width pressure inserts an overflow trigger while preserving the root");
        Click(overflow);
        Frame();
        check(breadcrumb.IsOverflowOpen, "Breadcrumb overflow uses the actual shared popup menu");
        var hidden = Find("Folder", UiAccessibilityRole.MenuItem);
        Click(hidden);
        Frame();
        check(!breadcrumb.IsOverflowOpen && calls.Last() == "folder", "Breadcrumb overflow navigation invokes a real hidden level and dismisses its menu");
        root.Options = root.Options with
        {
            MaxWidth = 20
        };
        Frame();
        Frame();
        check(Find("Current", UiAccessibilityRole.Label).Node.Bounds.Width <= 20,
            "Breadcrumb extreme reflow retains only its current location within the finite viewport");
        root.Options = root.Options with
        {
            MaxWidth = 600
        };
        Frame();
        Frame();
        var restoredHome = Find("Home", UiAccessibilityRole.Button);
        check(restoredHome.Node.Id == home.Node.Id && restoredHome.RuntimeId != home.RuntimeId
            && tree.RequestAction(home.Node.Id, home.RuntimeId, UiAccessibilityAction.Invoke, wait: false) == UiAccessibilityResult.Unavailable,
            "Breadcrumb reflow restores stable logical identities and rejects retired native runtime identities");
        check(context.State.NextScheduledFrameTimeSeconds is null, "Breadcrumb idle layout schedules no periodic frames");
        parentDisabled = true;
        Frame();
        var disabledParentChild = Find("Category", UiAccessibilityRole.Button);
        check(!disabledParentChild.Node.DisabledFocusable && !disabledParentChild.Node.CanReceiveFocus
            && tree.RequestAction(disabledParentChild.Node.Id, disabledParentChild.RuntimeId, UiAccessibilityAction.Focus, wait: false) == UiAccessibilityResult.Disabled,
            "Breadcrumb disabledFocusable cannot enable focus through a disabled parent scope");
        var previousCalls = calls.Count;
        Key(UiKey.Enter);
        check(calls.Count == previousCalls, "Breadcrumb disabled parent prevents keyboard actions for every level");
        parentDisabled = false;
        ControlFontFixture.ConfigureEmoji(context, korean : true);
        using var unicode = Fluent.Breadcrumb("Unicode path", [new("root", "정👨‍👩‍👧‍👦세일문서파일"), new("last", "현재")],
            options: new(MaximumCharacters: 3, TooltipMaximumCharacters: 5));
        current = unicode;
        Frame();
        var longItem = Find("정👨‍👩‍👧‍👦세일문서파일", UiAccessibilityRole.Button);
        check(longItem.Node.Tooltip is { Text: "정👨‍👩‍👧‍👦세일…" },
            "Breadcrumb truncated labels preserve full accessible names and grapheme-safe tooltip limits");
        tree.RequestAction(longItem.Node.Id, longItem.RuntimeId, UiAccessibilityAction.Focus, wait: false);
        Frame();
        for (var i = 0; i < 20; i++)
            Frame();
        check(Find("정👨‍👩‍👧‍👦세일…", UiAccessibilityRole.Tooltip).Node.ParentId == longItem.Node.Id,
            "Breadcrumb long-name tooltip opens through the shared focus lifecycle and references its actual target");
        unicode.Dispose();
        ControlFontFixture.Configure(context);
        current = root;
        Frame();
        check(Fluent.ComponentNames.Contains("Breadcrumb") && Fluent.RequiredComponentNames.Length == 55,
            "Breadcrumb connects the existing official top-level requirement");

        var customCalls = new List<string>();
        using var custom = new CustomBreadcrumb(customCalls.Add);
        current = custom;
        Frame();
        var customRoot = Find("Root", UiAccessibilityRole.Button);
        var customBounds = customRoot.Node.Bounds;
        check(customBounds.Height == 44, "Custom Breadcrumb presentation supplies the measured height");
        var point = new UiVector2(customBounds.X + customBounds.Width / 2, customBounds.Y + customBounds.Height / 2);
        Frame(idle with { MousePosition = point, LeftMouseDown = true, LeftMousePressed = true });
        check(customCalls.SequenceEqual(["root"]) && CustomBreadcrumbPresentation.SawActivation,
            "Custom Breadcrumb navigates on pointer press and passes activation to its painter");
        Frame(idle with { MousePosition = point, LeftMouseReleased = true });
        check(customCalls.SequenceEqual(["root"]), "Custom Breadcrumb pointer release does not navigate twice");
        Key(UiKey.DownArrow);
        var customParent = Find("Parent", UiAccessibilityRole.Button);
        check(context.State.FocusedId == customParent.Node.Id && customCalls.SequenceEqual(["root"]),
            "Custom Breadcrumb can use a different focus key without invoking a destination");
        tree.RequestAction(customParent.Node.Id, customParent.RuntimeId, UiAccessibilityAction.Invoke, wait: false);
        Frame();
        Click(Find("Here", UiAccessibilityRole.Label));
        check(customCalls.SequenceEqual(["root", "parent"]),
            "Custom Breadcrumb still accepts native Invoke and keeps the current location passive");
    }

    private sealed class CustomBreadcrumb(Action<string> navigated)
        : UiBreadcrumb<PressBehavior, CustomBreadcrumbPresentation>("Custom path",
            [new("root", "Root"), new("parent", "Parent"), new("here", "Here")], navigated,
            new(FocusMode : UiBreadcrumbFocusMode.Arrow));

    private readonly struct PressBehavior : IUiBreadcrumbBehavior
    {
        public static UiButtonActivation ItemActivation => UiButtonActivation.Press;
        public static UiBreadcrumbFocusCommand ResolveFocusCommand(UiKey key, bool rightToLeft)
            => key == UiKey.DownArrow ? UiBreadcrumbFocusCommand.Next
                : UiDefaultBreadcrumbBehavior.ResolveFocusCommand(key, rightToLeft);
    }

    private readonly struct CustomBreadcrumbPresentation : IUiBreadcrumbPresentation
    {
        internal static bool SawActivation { get; private set; }
        public static float Height(UiBreadcrumbOptions options) => 44;
        public static float DividerWidth(UiBreadcrumbOptions options) => UiFluentBreadcrumbPresentation.DividerWidth(options);
        public static UiVector2 MeasureItem(UiControlContext context, UiBreadcrumbItemContent item, UiBreadcrumbOptions options)
            => UiFluentBreadcrumbPresentation.MeasureItem(context, item, options);
        public static void PaintItem(UiControlContext context, UiBreadcrumbItemContent item, UiRect bounds,
            UiButtonInteraction interaction, UiBreadcrumbOptions options)
        {
            if (item.Text == "Root")
                SawActivation = interaction.Activated;
            UiFluentBreadcrumbPresentation.PaintItem(context, item, bounds, interaction, options);
        }
        public static void PaintDivider(UiControlContext context, UiRect bounds, UiBreadcrumbOptions options)
            => UiFluentBreadcrumbPresentation.PaintDivider(context, bounds, options);
    }

    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
