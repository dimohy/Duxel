using Duxel.Core;

internal static class ListWidgetTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        var selected = new UiState<string?>("a");
        var multiple = new UiState<string[]>([]);
        var text = new UiState<string>("edit");
        var accept = true;
        var prevent = false;
        var actions = 0;
        var childActions = 0;
        void Action(UiListItemAction action)
        {
            actions++;
            action.PreventSelection = prevent;
        }
        var singleRows = new[]
        {
            new UiListItem("a", "A", Fluent.Body1("A"), Action, selectionLabel: "Select A"),
            new UiListItem("b", "B", Fluent.Body1("B"), Action, selectionLabel: "Select B"),
            new UiListItem("c", "C", Fluent.Body1("C"), Action, selectionLabel: "Select C")
        };
        using var single = new UiList<UiFluentListPresentation>("Single", new UiBinding<string?>(() => selected.Value,
            value => { if (accept) selected.Value = value; }), singleRows, new(Width: 180));
        using var composite = new UiList<UiFluentListPresentation>("Composite", multiple.Binding,
        [
            new("x", "Row X", Fluent.Input("Edit X", text.Binding, 100, 180), Action, selectionLabel: "Select X"),
            new("y", "Row Y", Fluent.Button("Child action", () => childActions++), Action, selectionLabel: "Select Y")
        ], new(Navigation: UiListNavigation.Composite, Width: 230));
        var pressSelection = new UiState<string?>(null);
        var pressActions = new List<UiListActionKind>();
        var pressChildActions = 0;
        using var press = new UiList<UiPressListRowBehavior, UiFluentListPresentation>("Press list", pressSelection.Binding,
        [
            new("press", "Press row", Fluent.Button("Press child", () => pressChildActions++),
                action => pressActions.Add(action.Kind), selectionLabel: "Press checkbox")
        ], new(Navigation: UiListNavigation.Composite, Width: 140));
        using var before = Fluent.Button("Before list");
        using var after = Fluent.Button("After list");
        var showPressList = false;
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame(UiInputState? input = null)
        {
            context.NewFrame(new(1f / 60, new(640, 400), new(1, 1)), input ?? idle, new(0, 0, 640, 400), UiTextSettings.Default);
            context.Render(new Screen(ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding: 0);
                ui.SetCursorScreenPos(new(10, 10));
                before.Render(ui);
                ui.SetCursorScreenPos(new(10, 60));
                single.Render(ui);
                ui.SetCursorScreenPos(new(230, 60));
                composite.Render(ui);
                ui.SetCursorScreenPos(new(10, 230));
                after.Render(ui);
                if (showPressList)
                {
                    ui.SetCursorScreenPos(new(490, 60));
                    press.Render(ui);
                }
            }));
            using var draw = context.GetDrawData();
        }
        List<UiAccessibilityElement> Nodes()
        {
            var nodes = new List<UiAccessibilityElement>();
            void Visit(string? id)
            {
                while (id is not null && tree.TryGet(id, out var element))
                {
                    nodes.Add(element);
                    Visit(element.FirstChild);
                    id = element.Next;
                }
            }
            Visit(tree.Root.First);
            return nodes;
        }
        UiAccessibilityElement Find(string name, UiAccessibilityRole? role = null)
            => Nodes().Single(element => element.Node.Name == name && (role is null || element.Node.Role == role));
        void Request(string name, UiAccessibilityAction action, UiAccessibilityRole? role = null)
        {
            var element = Find(name, role);
            check(tree.RequestAction(element.Node.Id, element.RuntimeId, action, wait: false) == UiAccessibilityResult.Success,
                "List queues actual accessibility action: " + name + " / " + action);
            Frame();
        }
        void Key(UiKey key)
        {
            Frame(idle with
            {
                KeyEvents = [new(key, true, default)]
            });
            Frame(idle with
            {
                KeyEvents = [new(key, false, default)]
            });
        }
        UiVector2 Point(string name, UiAccessibilityRole? role = null, bool trailing = false)
        {
            var bounds = Find(name, role).Node.Bounds;
            return new(bounds.X + (trailing ? bounds.Width - 2 : bounds.Width / 2), bounds.Y + bounds.Height / 2);
        }
        void Pointer(UiVector2 point, bool pressed, bool released) => Frame(idle with
        {
            MousePosition = point,
            LeftMouseDown = pressed && !released,
            LeftMousePressed = pressed,
            LeftMouseReleased = released
        });
        void Click(string name, UiAccessibilityRole? role = null)
        {
            var point = Point(name, role);
            Pointer(point, pressed: true, released: false);
            Pointer(point, pressed: false, released: true);
        }
        Frame();
        check(Find("Single").Node is { Role: UiAccessibilityRole.ListBox, SupportsSelection: true, CanSelectMultiple: false }
            && Find("A", UiAccessibilityRole.Option).Node.Selected == true, "List publishes its real single selection container and option state");
        check(Find("Composite").Node is { Role: UiAccessibilityRole.Grid, SupportsSelection: true, CanSelectMultiple: true }
            && Nodes().Count(element => element.Node.Role == UiAccessibilityRole.GridCell) == 4,
            "List composite semantics contain real row cells for content and checkmarks");
        var grid = Find("Composite");
        check(grid.Node.Grid == new UiAccessibilityGridInfo(2, 2)
            && Find("Row X", UiAccessibilityRole.Row).Node.GridItem == new UiAccessibilityGridItemInfo(grid.Node.Id, 0, 0, ColumnSpan: 2),
            "List composite publishes its loaded dimensions and aggregate row coordinates");
        check(tree.GetGridItem(grid.Node.Id, grid.RuntimeId, 1, 0, out var checkCell) == UiAccessibilityResult.Success
            && tree.TryGet(checkCell!.Value.Id, out var checkNode) && checkNode.Node.Name == "Select Y"
            && tree.GetGridItem(grid.Node.Id, grid.RuntimeId, 1, 1, out var contentCell) == UiAccessibilityResult.Success
            && tree.TryGet(contentCell!.Value.Id, out var contentNode) && contentNode.Node.Name == "Row Y",
            "List grid indexes the selection cell before the real content cell");
        check(Find("Child action").Node.Bounds.Width < 198,
            "List row content keeps its natural button width inside the available content cell");
        Request("A", UiAccessibilityAction.Focus, UiAccessibilityRole.Option);
        Key(UiKey.DownArrow);
        check(Find("B", UiAccessibilityRole.Option).Node.Focused && selected.Value == "a", "List arrow navigation moves focus independently of selection");
        Key(UiKey.Space);
        check(selected.Value == "b" && actions == 0, "List Space selects without invoking a row action");
        Key(UiKey.Space);
        check(selected.Value is null, "List single selection permits toggling its selected row off");
        Key(UiKey.Home);
        check(Find("A", UiAccessibilityRole.Option).Node.Focused, "List Home reaches its first row");
        Key(UiKey.End);
        check(Find("C", UiAccessibilityRole.Option).Node.Focused, "List End reaches its last row");
        Key(UiKey.Tab);
        check(Find("Row X", UiAccessibilityRole.Row).Node.Focused, "List has one Tab entry before the next composite list");
        Key(UiKey.Tab);
        check(Find("After list").Node.Focused, "List Tab exits its composite rather than visiting every child");
        Click("Select C");
        check(selected.Value == "c" && actions == 0, "List checkbox click selects without a row action");
        Request("A", UiAccessibilityAction.Focus, UiAccessibilityRole.Option);
        Key(UiKey.Enter);
        check(selected.Value == "a" && actions == 1, "List Enter invokes its row action before default selection");
        prevent = true;
        Key(UiKey.Enter);
        check(selected.Value == "a" && actions == 2, "List row action can prevent its default selection toggle");
        Click("Select A");
        check(selected.Value is null && actions == 2, "List checkbox selection remains independent of row prevent-default");
        prevent = false;
        Request("B", UiAccessibilityAction.Select, UiAccessibilityRole.Option);
        check(selected.Value == "b" && Find("B", UiAccessibilityRole.Option).Node.Selected == true,
            "List native Select reads back the accepted binding into its option");
        check(Find("Select A").Node.ToggleState == UiAccessibilityToggleState.Off
            && Find("Select B").Node.ToggleState == UiAccessibilityToggleState.On,
            "List native row and checkbox states share the same completed selection snapshot");
        accept = false;
        Request("A", UiAccessibilityAction.Select, UiAccessibilityRole.Option);
        check(selected.Value == "b" && Find("B", UiAccessibilityRole.Option).Node.Selected == true
            && Find("A", UiAccessibilityRole.Option).Node.Selected == false, "List rejected selection preserves model and native state");
        accept = true;
        Request("Row X", UiAccessibilityAction.Focus, UiAccessibilityRole.Row);
        Key(UiKey.RightArrow);
        check(Find("Edit X").Node.Focused, "List composite Right enters its real input child");
        Key(UiKey.Escape);
        check(Find("Row X", UiAccessibilityRole.Row).Node.Focused, "List composite Escape restores its owning row");
        Key(UiKey.DownArrow);
        check(Find("Row Y", UiAccessibilityRole.Row).Node.Focused, "List composite Down navigates to the next row");
        Key(UiKey.RightArrow);
        Key(UiKey.Enter);
        check(childActions == 1 && actions == 2 && multiple.Value.Length == 0, "List child command does not invoke or select its row");
        Click("Select X", UiAccessibilityRole.Checkbox);
        Click("Select Y", UiAccessibilityRole.Checkbox);
        check(multiple.Value.SequenceEqual(new[] { "x", "y" })
            && Find("Row X", UiAccessibilityRole.Row).Node.Selected == true && Find("Row Y", UiAccessibilityRole.Row).Node.Selected == true,
            "List multiple selection publishes both actual selected rows coherently");
        composite.Options = composite.Options with
        {
            RightToLeft = true
        };
        Frame();
        grid = Find("Composite");
        tree.GetGridItem(grid.Node.Id, grid.RuntimeId, 0, 0, out checkCell);
        tree.GetGridItem(grid.Node.Id, grid.RuntimeId, 0, 1, out contentCell);
        tree.TryGet(checkCell!.Value.Id, out var rtlCheck);
        tree.TryGet(contentCell!.Value.Id, out var rtlContent);
        check(rtlCheck.Node.Bounds.X > rtlContent.Node.Bounds.X && rtlCheck.Node.GridItem?.Column == 0
            && rtlContent.Node.GridItem?.Column == 1,
            "List RTL mirrors physical cells while retaining leading-column logical coordinates");
        Pointer(Point("Child action"), pressed: true, released: true);
        check(childActions == 2 && actions == 2,
            "List child alone receives both pointer edges when they arrive in one frame");
        var releasePoint = Point("A", UiAccessibilityRole.Option, trailing: true);
        Pointer(releasePoint, pressed: true, released: false);
        check(actions == 2, "Default List waits for pointer release");
        Pointer(releasePoint, pressed: false, released: true);
        check(actions == 3 && selected.Value == "a", "Default List invokes and selects on release");
        showPressList = true;
        Frame();
        var pressPoint = Point("Press row", UiAccessibilityRole.Row, trailing: true);
        Pointer(pressPoint, pressed: true, released: false);
        check(pressActions.SequenceEqual([UiListActionKind.Pointer]) && pressSelection.Value == "press",
            "Press List invokes and selects on the down edge");
        Pointer(pressPoint, pressed: false, released: true);
        check(pressActions.Count == 1 && pressSelection.Value == "press", "Press List release does not invoke again");
        var pressChild = Point("Press child");
        Pointer(pressChild, pressed: true, released: false);
        check(pressActions.Count == 1 && pressChildActions == 0 && Find("Press child").Node.Focused,
            "Press List child claims focus without invoking its row on down");
        Pointer(pressChild, pressed: false, released: true);
        Pointer(pressChild, pressed: true, released: true);
        check(pressActions.Count == 1 && pressChildActions == 2 && pressSelection.Value == "press",
            "Press List preserves child-only clicks across distinct and same-frame pointer edges");
        var pressCheckbox = Point("Press checkbox", UiAccessibilityRole.Checkbox);
        Pointer(pressCheckbox, pressed: true, released: false);
        check(pressActions.Count == 1 && pressSelection.Value == "press", "Press List checkbox waits for release");
        Pointer(pressCheckbox, pressed: false, released: true);
        check(pressActions.Count == 1 && pressSelection.Value is null, "Press List checkbox toggles independently");
        Request("Press row", UiAccessibilityAction.Focus, UiAccessibilityRole.Row);
        Key(UiKey.Space);
        Key(UiKey.Enter);
        Request("Press row", UiAccessibilityAction.Invoke, UiAccessibilityRole.Row);
        check(pressActions.SequenceEqual([UiListActionKind.Pointer, UiListActionKind.Enter, UiListActionKind.Accessibility])
            && pressSelection.Value == "press", "Press List preserves keyboard selection and keyboard and accessibility invocation");
        using var dynamicList = new UiList<MutableListPresentation>("Mutable size", new UiState<string?>(null).Binding,
            [new("one", "One", Fluent.Body1("Stable content"))]);
        var knownRow = new UiListItem("known", "Known", Fluent.Body1("Stable content"));
        using var knownList = new UiList<UiFluentListPresentation>("Known size", new UiState<string?>(null).Binding, [knownRow]);
        context.NewFrame(new(1f / 60, new(640, 400), new(1, 1)), idle, new(0, 0, 640, 400), UiTextSettings.Default);
        context.Render(new Screen(ui =>
        {
            var loose = new UiBoxConstraints(0, float.PositiveInfinity, 0, float.PositiveInfinity);
            MutableListPresentation.Extent = 24;
            var initial = dynamicList.MeasureConstrained(ui, loose);
            MutableListPresentation.Extent = 64;
            var changed = dynamicList.MeasureConstrained(ui, loose);
            check(changed.X == initial.X + 40 && changed.Y == 64 && !dynamicList.RetainsMeasurement,
                "List custom presentations remeasure mutable geometry without an implicit retention promise");
            var known = knownList.MeasureConstrained(ui, loose);
            knownList.MeasureConstrained(ui, loose);
            check(knownList.RetainsMeasurement && knownRow.RetainsMeasurement,
                "List known Fluent presentation keeps measured arrangement and row retention");
            var rowBefore = knownRow.MeasureConstrained(ui, loose);
            knownRow.SetCheckExtent(54);
            var rowAfter = knownRow.MeasureConstrained(ui, loose);
            check(rowAfter.X == rowBefore.X + 22 && rowAfter.Y == 54,
                "List parent-derived checkmark size invalidates retained row geometry");
            check(knownList.MeasureConstrained(ui, loose) == known,
                "List parent remeasurement restores its declared checkmark geometry after an alternate row measure");
        }));
        using var measuredDraw = context.GetDrawData();
    }
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
    private readonly struct MutableListPresentation : IUiListPresentation
    {
        internal static float Extent;
        public static float CheckmarkExtent(UiControlContext context) => Extent;
        public static void PaintCheckmark(UiControlContext context, UiRect bounds, UiButtonInteraction interaction, bool selected)
        {
        }
        public static void PaintFocus(UiControlContext context, UiRect bounds)
        {
        }
    }
}
