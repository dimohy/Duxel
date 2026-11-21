using System.Collections.Immutable;
using Duxel.Core;
using Duxel.Platform.Windows;

internal static class AccordionWidgetTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var scene = new Scene();
        scene.Frame();
        check(scene.Value.IsEmpty && scene.Count(UiAccessibilityRole.AccordionPanel) == 0 && scene.Writes == 0,
            "Accordion starts closed without rendering or mutating any panel");
        check(scene.Count(UiAccessibilityRole.Button) == 3 && scene.Find("First").Expansion == UiAccessibilityExpansionState.Collapsed,
            "Accordion publishes separate keyboard buttons with collapsed expansion state");
        var hoverBounds = scene.Find("First").Bounds;
        var firstPointer = new UiVector2(hoverBounds.X + hoverBounds.Width / 2, hoverBounds.Y + hoverBounds.Height / 2);
        scene.Pointer(firstPointer, false, false, false);
        check(scene.Cursor == UiMouseCursor.Hand && scene.Value.IsEmpty && scene.Writes == 0,
            "Fluent Accordion hover exposes the official pointer cursor without expanding content");
        var disabledBounds = scene.Find("Disabled").Bounds;
        var disabledPointer = new UiVector2(disabledBounds.X + 20, disabledBounds.Y + disabledBounds.Height / 2);
        scene.Pointer(disabledPointer, false, false, false);
        check(scene.Cursor == UiMouseCursor.NotAllowed, "Fluent disabled Accordion header exposes the official not-allowed cursor");
        scene.Disabled = true;
        scene.Pointer(firstPointer, false, false, false);
        check(scene.Cursor == UiMouseCursor.NotAllowed, "Fluent Accordion inherits its parent disabled cursor policy");
        scene.Disabled = false;
        scene.Clip = new(0, 300, 200, 100);
        scene.Pointer(firstPointer, false, false, false);
        check(scene.Cursor == UiMouseCursor.Arrow, "Clipped Accordion headers do not claim the pointer cursor");
        scene.Clip = null;
        scene.Frame();
        check(scene.Cursor == UiMouseCursor.Arrow, "Leaving an Accordion header restores the ordinary cursor");
        var requests = scene.Requests;
        scene.Frame();
        check(scene.Requests == requests && scene.Writes == 0, "unchanged Accordion neither writes state nor schedules animation");
        scene.Request("First", UiAccessibilityAction.Focus);
        scene.Key(UiKey.Enter, true);
        check(scene.Value.SequenceEqual(["a"]) && scene.Count(UiAccessibilityRole.AccordionPanel) == 1 && scene.Writes == 1,
            "Enter opens one panel with exactly one controlled binding write");
        check(scene.RepaintDeadline.HasValue, "Accordion schedules its shared disclosure transition after opening");
        for (var frame = 0; frame < 12; frame++)
            scene.Frame();
        check(scene.RepaintDeadline is null, "Accordion stops scheduling frames after its disclosure settles");
        scene.Key(UiKey.Enter, false);
        var first = scene.Find("First");
        var panel = scene.Find("First", UiAccessibilityRole.AccordionPanel);
        check(first.ControlledElementId == panel.Id && panel.LabeledBy == first.Id && scene.Find("Editor").ParentId == panel.Id,
            "Accordion exposes reciprocal button-panel relations and parents actual content under its panel");
        scene.Request("First", UiAccessibilityAction.Collapse);
        check(scene.Value.SequenceEqual(["a"]) && scene.Writes == 1,
            "default noncollapsible mode refuses to close the last open item without a spurious write");
        scene.Accordion.Options = new(Collapsible: true);
        scene.Request("Editor", UiAccessibilityAction.SetValue, "Saved panel text");
        check(scene.Text.Value == "Saved panel text", "expanded content accepts its actual native editing action");
        scene.Request("First", UiAccessibilityAction.Focus);
        scene.Key(UiKey.Tab, true);
        check(scene.FocusedName() == "Editor", "Tab visits panel content after its header rather than jumping through all headers");
        scene.Key(UiKey.Tab, false);
        scene.Key(UiKey.Tab, true);
        check(scene.FocusedName() == "Second", "Tab resumes with the following Accordion header after panel content");
        scene.Key(UiKey.Tab, false);
        scene.Key(UiKey.DownArrow, true);
        check(scene.FocusedName() == "Second" && scene.Value.SequenceEqual(["a"]),
            "default Accordion does not add the deprecated arrow-navigation behavior");
        scene.Key(UiKey.DownArrow, false);
        scene.Key(UiKey.Space, true);
        check(scene.Value.SequenceEqual(["a"]), "Space press holds without prematurely toggling Accordion");
        scene.Key(UiKey.Space, false);
        check(scene.Value.SequenceEqual(["b"]) && scene.Count(UiAccessibilityRole.AccordionPanel) == 1,
            "Space release opens the next item and closes the previous item in single mode");
        scene.Request("First", UiAccessibilityAction.Expand);
        check(scene.Text.Value == "Saved panel text" && scene.Find("Editor").Value == "Saved panel text",
            "closing and reopening retain the owned panel widget and its caller state");
        scene.Request("Editor", UiAccessibilityAction.Focus);
        scene.Request("First", UiAccessibilityAction.Collapse);
        check(scene.Value.IsEmpty && scene.FocusedName() == "First" && scene.Count(UiAccessibilityRole.Edit) == 0,
            "collapsing the focused panel restores header focus and removes hidden interactive nodes");
        scene.Request("Disabled", UiAccessibilityAction.Expand, expected: UiAccessibilityResult.Disabled);
        check(scene.Value.IsEmpty && !scene.Find("Disabled").Enabled,
            "disabled Accordion header rejects expansion requests");
        scene.Accordion.Options = new(Multiple: true, Collapsible: true);
        scene.Request("First", UiAccessibilityAction.Expand);
        scene.Request("Second", UiAccessibilityAction.Expand);
        check(scene.Value.SequenceEqual(["a", "b"]) && scene.Count(UiAccessibilityRole.AccordionPanel) == 2,
            "multiple mode opens independent panels through the same controlled state");
        scene.Request("First", UiAccessibilityAction.Collapse);
        check(scene.Value.SequenceEqual(["b"]), "multiple mode closes only the requested panel");
        scene.Accordion.Options = new(Multiple: true);
        scene.Request("Second", UiAccessibilityAction.Collapse);
        check(scene.Value.SequenceEqual(["b"]), "noncollapsible multiple mode protects its last remaining item");
        scene.Accordion.Options = new(Multiple: true, Collapsible: true, Size: UiAccordionSize.Small,
            ChevronPosition: UiAccordionChevronPosition.End);
        scene.Request("Second", UiAccessibilityAction.Collapse);
        check(scene.Find("First").Bounds.Height == 32 && scene.Value.IsEmpty,
            "small headers use pinned32px metrics and end-chevron layout preserves interaction");
        var firstBounds = scene.Find("First").Bounds;
        scene.Pointer(new(firstBounds.X + 20, firstBounds.Y + 16), true, true, false);
        scene.Pointer(new(firstBounds.X + 20, firstBounds.Y + 16), false, false, true);
        check(scene.Value.SequenceEqual(["a"]), "pointer release opens Accordion using the actual allocated header bounds");
        scene.Disabled = true;
        scene.Request("First", UiAccessibilityAction.Collapse);
        check(scene.Value.SequenceEqual(["a"]) && !scene.Find("First").Enabled,
            "inherited disabled scope blocks Accordion expansion changes");
        scene.Disabled = false;
        scene.Frame();
        var writes = scene.Writes;
        using (var measurement = new MeasureScene(scene.Accordion))
            measurement.Measure(new(120, 120, 0, 600));
        check(scene.Writes == writes && scene.Value.SequenceEqual(["a"]),
            "pure constrained measurement never consumes input or writes Accordion state");
        scene.Value = ImmutableArray.Create("unknown");
        var rejected = false;
        try
        {
            scene.Frame();
        }
        catch (ArgumentException) { rejected = true; }
        scene.Value = ImmutableArray<string>.Empty;
        scene.Frame();
        check(rejected && scene.Count(UiAccessibilityRole.AccordionPanel) == 0,
            "unknown controlled values fail explicitly and the next valid capture recovers");
        check(Fluent.ComponentNames.Contains("Accordion"), "Accordion factory is exposed through the compiled Fluent catalog");
        var secondId = scene.Find("Second").Id;
        scene.Accordion.SetHeader("b", "Updated second", enabled: false);
        scene.Frame();
        check(scene.Find("Updated second").Id == secondId && !scene.Find("Updated second").Enabled,
            "live header label and enabled updates preserve stable identity and owned panel widgets");
        using var constrained = new Scene();
        constrained.Value = ImmutableArray.Create("a");
        constrained.Root = Dux.SizedBox(constrained.Accordion, 160, 120);
        constrained.Frame();
        check(constrained.VisibleWithin(new(10, 10, 160, 120)) && constrained.Find("Editor").Bounds.Width <= 136,
            "Accordion parent allocation constrains header and body clips without replacing persistent content");
        constrained.Request("Editor", UiAccessibilityAction.Focus);
        constrained.SetAccessibility(false);
        constrained.Value = ImmutableArray<string>.Empty;
        constrained.Frame();
        check(constrained.FocusedId == constrained.Accordion.HeaderIds[0],
            "externally closing a focused panel restores header focus even when accessibility capture is disabled");

        CheckCustomHeader(check);
    }

    private static void CheckCustomHeader(Action<bool, string> check)
    {
        using var scene = new Scene();
        var accepts = true;
        scene.Root = new CustomAccordion(new(() => scene.Value, value =>
        {
            scene.Writes++;
            if (accepts)
                scene.Value = value;
        }));
        scene.Frame();
        var header = scene.Find("Details");
        var pointer = new UiVector2(header.Bounds.X + 20, header.Bounds.Y + header.Bounds.Height / 2);
        check(header.Bounds.Height == 48, "Custom Accordion presentation measures a taller header using the shared layout");
        scene.Pointer(pointer, true, true, false);
        check(scene.Writes == 1 && scene.Value.SequenceEqual(["details"])
            && scene.Find("Details").Expansion == UiAccessibilityExpansionState.Expanded
            && CustomAccordionPresentation.LastExpanded,
            "Custom Accordion press opens its panel and publishes the accepted value to paint and accessibility in the same frame");
        scene.Pointer(pointer, false, false, true);
        check(scene.Writes == 1, "Custom Accordion releases the header without a second write");
        accepts = false;
        scene.Pointer(pointer, true, true, false);
        check(scene.Writes == 2 && scene.Count(UiAccessibilityRole.AccordionPanel) == 1 && CustomAccordionPresentation.LastExpanded,
            "Custom Accordion preserves the expansion painted and exposed by a binding that rejects collapse");
        scene.Pointer(pointer, false, false, true);
        accepts = true;
        scene.Request("Details", UiAccessibilityAction.Collapse);
        check(scene.Writes == 3 && scene.Count(UiAccessibilityRole.AccordionPanel) == 0 && !CustomAccordionPresentation.LastExpanded,
            "Custom Accordion preserves the independent native collapse path");
    }

    private sealed class CustomAccordion(UiBinding<ImmutableArray<string>> value)
        : UiAccordion<UiPressButtonBehavior, CustomAccordionPresentation>("Custom Accordion", value,
            new UiAccordionItem[] { new("details", "Details", Fluent.Body1("Retained content")) },
            options : new(Collapsible : true));

    private readonly struct CustomAccordionPresentation : IUiAccordionPresentation
    {
        internal static bool LastExpanded { get; private set; }
        public static float PanelMargin => UiFluentAccordionPresentation.PanelMargin;
        public static UiAccordionHeaderLayout MeasureHeader(UiControlContext context, UiAccordionItem item,
            float width, UiAccordionOptions options)
        {
            var layout = UiFluentAccordionPresentation.MeasureHeader(context, item, width, options);
            return layout with { Size = new(layout.Size.X, 48) };
        }
        public static void PaintHeader(UiControlContext context, UiAccordionItem item, UiAccordionHeaderLayout layout,
            UiRect bounds, bool expanded, UiButtonInteraction interaction, UiAccordionOptions options)
        {
            LastExpanded = expanded;
            UiFluentAccordionPresentation.PaintHeader(context, item, layout, bounds, expanded, interaction, options);
        }
    }

    private sealed class Scene : IDisposable
    {
        private readonly UiContext _context = new(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        internal double? RepaintDeadline => _context.State.NextScheduledFrameTimeSeconds;
        private readonly WindowsAccessibility _native = new(0);
        internal readonly UiState<string> Text = new("Initial");
        internal readonly UiFluentAccordion Accordion;
        internal ImmutableArray<string> Value = ImmutableArray<string>.Empty;
        internal int Writes, Requests;
        internal bool Disabled;
        internal UiRect? Clip;
        internal UiMouseCursor Cursor => _context.State.MouseCursor;
        internal UiWidget? Root;
        internal string? FocusedId => _context.State.FocusedId;
        internal void SetAccessibility(bool enabled) => _context.SetAccessibilityTree(enabled ? _native.Tree : null);
        internal Scene()
        {
            ControlFontFixture.Configure(_context);
            _context.SetAccessibilityTree(_native.Tree);
            _context.SetRequestFrameCallback(() => Requests++);
            Accordion = Fluent.Accordion("Test Accordion", new(() => Value, next => { Value = next; Writes++; }),
                new UiAccordionItem[] { new("a", "First", Fluent.Input("Editor", Text.Binding, 100, 220)),
                    new("b", "Second", Fluent.Button("Body action")), new("c", "Disabled", Fluent.Body1("Disabled panel"), false) });
        }
        internal void Frame(UiInputState? input = null)
        {
            _context.NewFrame(new(1f / 60, new(640, 480), new(1, 1)), input ?? Idle, new(0, 0, 640, 480), UiTextSettings.Default);
            _context.Render(new Screen(ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding: 0);
                ui.SetCursorScreenPos(new(10, 10));
                using var clip = new UiControlContext(ui).Clip(Clip ?? new UiRect(0, 0, 640, 480));
                ui.BeginDisabled(Disabled);
                (Root ?? Accordion).Render(ui);
                ui.EndDisabled();
            }));
            using var draw = _context.GetDrawData();
        }
        private static UiInputState Idle => new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        internal void Key(UiKey key, bool down) => Frame(Idle with { KeyEvents = [new(key, down, KeyModifiers.None)] });
        internal void Pointer(UiVector2 position, bool down, bool pressed, bool released) =>
            Frame(Idle with
            {
                MousePosition = position,
                LeftMouseDown = down,
                LeftMousePressed = pressed,
                LeftMouseReleased = released
            });
        internal UiAccessibilityNode Find(string name, UiAccessibilityRole? role = null) => Nodes().Single(node =>
            node.Name == name && (role.HasValue ? node.Role == role : node.Role != UiAccessibilityRole.AccordionPanel));
        private IEnumerable<UiAccessibilityNode> Nodes()
        {
            var result = new List<UiAccessibilityNode>();
            void Walk(string? id)
            {
                while (id is not null && _native.Tree.TryGet(id, out var node))
                {
                    result.Add(node.Node);
                    Walk(node.FirstChild);
                    id = node.Next;
                }
            }
            Walk(_native.Tree.Root.First);
            return result;
        }
        internal int Count(UiAccessibilityRole role) => Nodes().Count(node => node.Role == role);
        internal bool VisibleWithin(UiRect bounds) => Nodes().Where(node => !node.Offscreen).All(node =>
            node.Bounds.X >= bounds.X && node.Bounds.Y >= bounds.Y
            && node.Bounds.X + node.Bounds.Width <= bounds.X + bounds.Width
            && node.Bounds.Y + node.Bounds.Height <= bounds.Y + bounds.Height);
        internal string? FocusedName() => Nodes().SingleOrDefault(node => node.Focused).Name;
        internal void Request(string name, UiAccessibilityAction action, string? value = null,
            UiAccessibilityResult expected = UiAccessibilityResult.Success)
        {
            var node = Find(name);
            _native.Tree.TryGet(node.Id, out var current);
            var result = _native.Tree.RequestAction(node.Id, current.RuntimeId, action, value, wait: false);
            if (result != expected)
                throw new InvalidOperationException($"Accordion action result : {name}/{action} expected {expected}, actual {result}.");
            Frame();
        }
        public void Dispose()
        {
            Root?.Dispose();
            Accordion.Dispose();
            _context.Dispose();
            _native.Dispose();
        }
    }
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
    private sealed class MeasureScene(UiWidget widget) : IDisposable
    {
        private readonly UiContext _context = new(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        internal void Measure(UiBoxConstraints constraints)
        {
            ControlFontFixture.Configure(_context);
            _context.NewFrame(new(1f / 60, new(640, 480), new(1, 1)),
                new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default),
                new(0, 0, 640, 480), UiTextSettings.Default);
            _context.Render(new Screen(ui => new UiControlContext(ui).MeasureConstrained(widget, constraints)));
            using var draw = _context.GetDrawData();
        }
        public void Dispose() => _context.Dispose();
    }
}
