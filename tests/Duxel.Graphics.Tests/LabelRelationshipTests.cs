using Duxel.Core;
using Duxel.Platform.Windows;

internal static class LabelRelationshipTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var native = new WindowsAccessibility(0);
        context.SetAccessibilityTree(native.Tree);
        using var input = Fluent.Input("Original source", new UiState<string>("Value").Binding, 100, 220);
        using var label = Fluent.Label("First label", input, new(Required : true));
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        var before = true;
        var showLabel = true;
        var showInput = true;
        var clipLabel = false;
        var abort = false;
        var changes = new List<UiAccessibilityRelationshipChange>();
        var committed = true;
        native.Tree.RelationshipChanged += change =>
        {
            committed &= native.Tree.TryGet(change.Element.Node.Id, out var current) && current == change.Element;
            changes.Add(change);
        };
        void Frame()
        {
            context.NewFrame(new(1f / 60, new(480, 320), new(1, 1)), idle, new(0, 0, 480, 320), UiTextSettings.Default);
            context.Render(new Screen(ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding : 0);
                void Label()
                {
                    if (!showLabel) return;
                    ui.SetCursorScreenPos(new(clipLabel ? 700 : 10, 10));
                    label.Render(ui);
                }
                if (before) Label();
                if (abort) throw new CaptureAborted();
                if (showInput) { ui.SetCursorScreenPos(new(10, 70)); input.Render(ui); }
                if (!before) Label();
            }));
            using var draw = context.GetDrawData();
        }
        UiAccessibilityElement Find(UiAccessibilityRole role)
        {
            for (var id = native.Tree.Root.First; id is not null;)
            {
                if (!native.Tree.TryGet(id, out var node)) break;
                if (node.Node.Role == role) return node;
                id = node.Next;
            }
            throw new InvalidOperationException("Missing label test role : " + role);
        }
        Frame();
        var text = Find(UiAccessibilityRole.Label);
        var edit = Find(UiAccessibilityRole.Edit);
        check(native.Tree.Count == 2 && edit.Node.LabeledBy == text.Node.Id,
            "standalone Label publishes a real label node and associated Edit relationship");
        check(text.Node is { Name : "First label", Focusable : false, ValuePattern : false, Focused : false }
            && !text.Node.SupportsInvoke && text.Node.LabeledBy is null && text.Node.Bounds.Width > 0,
            "Label node exposes source text without required adornment or interactive patterns : " + text.Node);
        var provider = native.Find(text.Node.Id)!;
        provider.GetPropertyValue(30003, out var type);
        provider.GetPropertyValue(30017, out var content);
        provider.GetPropertyValue(30016, out var control);
        check(type.Type == 3 && type.Data.Integer == 50020 && content.Data.Boolean == 0 && control.Data.Boolean != 0,
            "native Label uses Text control type in control view without duplicate content view narration");
        var revision = native.Tree.Revision;
        Frame();
        check(native.Tree.Revision == revision && changes.Count == 0,
            "unchanged Label relationship preserves publication and emits no duplicate events");
        before = false;
        Frame();
        check(Find(UiAccessibilityRole.Edit).Node.LabeledBy == text.Node.Id
            && Find(UiAccessibilityRole.Label).RuntimeId == text.RuntimeId && changes.Count == 0,
            "target-before-label order resolves the same live provider without relationship churn");
        label.Text = "Changed label";
        Frame();
        check(Find(UiAccessibilityRole.Edit).Node is { Name : "Changed label", Required : true }
            && Find(UiAccessibilityRole.Label).Node.Name == "Changed label" && changes.Count == 0,
            "text changes refresh both names while retaining the established LabeledBy target");
        label.Options = label.Options with { Disabled = true };
        Frame();
        check(!Find(UiAccessibilityRole.Label).Node.Enabled && Find(UiAccessibilityRole.Edit).Node.Enabled
            && Find(UiAccessibilityRole.Edit).Node.LabeledBy == text.Node.Id,
            "disabled Label retains its semantic relation while target stays enabled");
        showLabel = false;
        Frame();
        check(Find(UiAccessibilityRole.Edit).Node.LabeledBy is null && changes.Count == 1 && committed
            && changes[0].Previous?.Element.RuntimeId == text.RuntimeId && changes[0].Current is null,
            "omitted Label clears the live relation with one committed old-target event");
        check(provider.GetPropertyValue(30005, out _) == UiaNative.Unavailable,
            "removed Label invalidates retained native providers");
        showLabel = true;
        Frame();
        check(Find(UiAccessibilityRole.Label).RuntimeId != text.RuntimeId && changes.Count == 2
            && changes[^1].Current?.Element.Node.Role == UiAccessibilityRole.Label,
            "reappearing Label links a newly live provider and emits one relationship event");
        clipLabel = true;
        Frame();
        check(Find(UiAccessibilityRole.Label).Node.Offscreen && Find(UiAccessibilityRole.Edit).Node.LabeledBy is not null,
            "clipped Label remains semantically linked with its own offscreen state");
        showInput = false;
        Frame();
        check(native.Tree.Count == 1 && Find(UiAccessibilityRole.Label).Node.LabeledBy is null,
            "omitted targets do not leave a dangling reverse relation on the Label");
        showInput = true;
        clipLabel = false;
        label.Target = null;
        Frame();
        check(Find(UiAccessibilityRole.Edit).Node is { Name : "Original source", LabeledBy : null, Required : false },
            "detaching Label clears naming metadata and LabeledBy together");
        label.Target = input;
        before = true;
        abort = true;
        var caught = false;
        try { Frame(); } catch (CaptureAborted) { caught = true; }
        abort = false;
        showLabel = false;
        Frame();
        check(caught && Find(UiAccessibilityRole.Edit).Node.LabeledBy is null,
            "aborted capture cannot leak an old Label provider into the next frame");
        showLabel = true;
        input.AccessibleName = "Explicit source name";
        Frame();
        check(Find(UiAccessibilityRole.Edit).Node.Name == "Explicit source name"
            && Find(UiAccessibilityRole.Edit).Node.LabeledBy == Find(UiAccessibilityRole.Label).Node.Id,
            "explicit accessible name precedence coexists with the visual Label relation");
        label.Dispose();
        showLabel = false;
        Frame();
        check(Find(UiAccessibilityRole.Edit).Node.LabeledBy is null,
            "disposing Label leaves its borrowed target live without a stale relation");
    }
    private sealed class CaptureAborted : Exception;
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
