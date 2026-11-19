using System.Runtime.InteropServices;
using Duxel.Core;
using Duxel.Platform.Windows;

internal static class TooltipAccessibilityTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var tooltip = new UiTooltip<UiHoverFocusTooltipBehavior, Surface>(Fluent.Button("Save"), "Save a local copy");
        using var scene = new Scene(tooltip);
        scene.Frame();
        check(!tooltip.IsOpen && scene.NativeString(scene.First.Node.Id, 30013) == "Save a local copy",
            "tooltip accessibility : hidden description reaches native HelpText");
        void Check(bool value, string name) => check(value, "tooltip accessibility : " + name);
        var initial = scene.First;
        Check(initial.Node.Name == "Save" && initial.Node.Tooltip is { Relationship : UiTooltipRelationship.Description,
            Text : "Save a local copy" }, "description preserves target name and has explicit semantics");
        Check(scene.Host.Tree.Count == 1, "hidden help does not add a synthetic focusable node");
        var revision = scene.Host.Tree.Revision;
        scene.Frame();
        Check(scene.Host.Tree.Revision == revision && scene.First.RuntimeId == initial.RuntimeId,
            "unchanged hidden relationship preserves target publication and identity");
        scene.Frame(0.01f, new(30, 70));
        scene.Frame(0.26f, new(30, 70));
        Check(tooltip.IsOpen && scene.First.Node.Tooltip == initial.Node.Tooltip
            && scene.NativeString(initial.Node.Id, 30013) == "Save a local copy",
            "showing the surface does not remove or duplicate target help");
        scene.Frame(0.01f, new(30, 70), [new(UiKey.Escape, true, KeyModifiers.None)]);
        Check(!tooltip.IsOpen && scene.First.Node.Tooltip == initial.Node.Tooltip,
            "Escape dismissal preserves the hidden description");
        tooltip.Text = "Save the updated copy";
        scene.Frame();
        Check(scene.First.Node.Tooltip?.SourceId == initial.Node.Tooltip?.SourceId && scene.First.RuntimeId == initial.RuntimeId
            && scene.NativeString(initial.Node.Id, 30013) == "Save the updated copy",
            "text changes update native help without replacing target or source identity");
        scene.Disabled = true;
        scene.Frame();
        Check(!scene.First.Node.Enabled && scene.NativeString(initial.Node.Id, 30013) == "Save the updated copy",
            "disabled controls retain explanations");
        scene.Disabled = false;
        scene.Clip = new(500, 400, 10, 10);
        scene.Frame();
        Check(scene.First.Node.Offscreen && scene.First.Node.Tooltip?.Text == "Save the updated copy",
            "visual clipping does not discard the semantic relationship");
        var retained = scene.Host.Find(initial.Node.Id)!;
        scene.Hidden = true;
        scene.Frame();
        Check(scene.Host.Tree.Count == 0 && retained.GetPropertyValue(30013, out _) == UiaNative.Unavailable,
            "omitted targets remove help and invalidate old providers");
        scene.Hidden = false;
        scene.Clip = null;
        scene.Frame();
        Check(scene.First.RuntimeId != initial.RuntimeId && scene.First.Node.Tooltip?.SourceId == initial.Node.Tooltip?.SourceId,
            "returning targets get a fresh provider identity with the same logical relationship");

        using var label = Wrap(Fluent.Button("##icon"), "More options", UiTooltipRelationship.Label);
        using var labelScene = new Scene(label);
        labelScene.Frame();
        Check(labelScene.First.Node.Name == "More options" && labelScene.NativeString(labelScene.First.Node.Id, 30005) == "More options"
            && labelScene.NativeString(labelScene.First.Node.Id, 30013) == string.Empty,
            "label relationship provides native Name without becoming HelpText");
        Check(label.Target.AccessibleName is null, "relationship does not mutate the target AccessibleName property");
        label.Target.AccessibleName = "Explicit name";
        labelScene.Frame();
        Check(labelScene.First.Node.Name == "Explicit name", "explicit child name wins over tooltip label");
        label.Target.AccessibleName = string.Empty;
        labelScene.Frame();
        Check(labelScene.First.Node.Name == string.Empty, "an explicit empty child name also wins");
        label.Target.AccessibleName = null;
        label.Options = label.Options with { Relationship = UiTooltipRelationship.Inaccessible };
        labelScene.Frame();
        Check(labelScene.First.Node.Tooltip is null && labelScene.First.Node.Name == string.Empty
            && labelScene.NativeString(labelScene.First.Node.Id, 30013) == string.Empty,
            "inaccessible relationship removes naming and description semantics");

        var value = "Account value";
        var input = Fluent.Input("Editor", new(() => value, next => value = next), 128, 220);
        var before = Fluent.Button("Before");
        var after = Fluent.Button("After");
        input.BeforeSlot = new("before", before, new(24, 24));
        input.AfterSlot = new("after", after, new(24, 24));
        var inputTooltip = Wrap(input, "Account details");
        using var field = Fluent.Field(inputTooltip);
        field.Width = 300;
        field.Options = new() { Label = "Account", Hint = "Enter account", ValidationMessage = "Missing", Required = true };
        using var fieldScene = new Scene(field);
        fieldScene.Frame();
        var edit = fieldScene.First;
        Check(edit.Node.Name == "Account" && edit.Node.Hint == "Enter account" && edit.Node.ValidationMessage == "Missing"
            && edit.Node.Required && edit.Node.Invalid && edit.Node.Tooltip?.Text == "Account details",
            "Field name, hint, validation and requirement stay distinct from tooltip metadata");
        Check(fieldScene.NativeString(edit.Node.Id, 30013) == "Enter account Missing Account details",
            "native HelpText preserves Field hint and validation before tooltip description");
        fieldScene.Host.Tree.TryGet(edit.FirstChild!, out var beforeNode);
        fieldScene.Host.Tree.TryGet(edit.LastChild!, out var afterNode);
        Check(beforeNode.Node.Name == "Before" && afterNode.Node.Name == "After"
            && beforeNode.Node.Tooltip is null && afterNode.Node.Tooltip is null,
            "primary edit claims the relationship before both adornment buttons");
        inputTooltip.Options = inputTooltip.Options with { Relationship = UiTooltipRelationship.Label };
        fieldScene.Frame();
        Check(fieldScene.First.Node.Name == "Account" && fieldScene.NativeString(edit.Node.Id, 30013) == "Enter account Missing",
            "explicit Field label wins and label text is not appended as help");

        var innerField = Fluent.Field(Fluent.Button("Action"));
        innerField.Width = 200;
        innerField.Options = new() { Label = "Field action", Hint = "Field hint" };
        using var outerTooltip = Wrap(innerField, "Outer description");
        using var reverseScene = new Scene(outerTooltip);
        reverseScene.Frame();
        Check(reverseScene.First.Node.Name == "Field action" && reverseScene.First.Node.Tooltip?.Text == "Outer description"
            && reverseScene.NativeString(reverseScene.First.Node.Id, 30013) == "Field hint Outer description",
            "Tooltip around Field and Field around Tooltip preserve equivalent primary semantics");

        var inner = Wrap(Fluent.Button("Nested"), "Inner description");
        inner.Options = inner.Options with { ShowDelaySeconds = 0 };
        using var outer = Wrap(inner, "Outer description");
        outer.Options = outer.Options with { ShowDelaySeconds = 0 };
        using var nestedScene = new Scene(outer);
        nestedScene.Frame(0.01f, new(30, 70));
        Check(nestedScene.First.Node.Tooltip?.Text == "Inner description"
            && nestedScene.NativeString(nestedScene.First.Node.Id, 30013) == "Inner description",
            "nearest nested relationship owns the actual target");
        Check(inner.IsOpen && !outer.IsOpen && Surface.LastText == "Inner description",
            "nearest nested surface agrees with its semantic relationship");

        var invalid = false;
        try
        {
            outer.Options = outer.Options with { Relationship = (UiTooltipRelationship)99 };
        }
        catch (ArgumentOutOfRangeException)
        {
            invalid = true;
        }
        Check(invalid && outer.Options.Relationship == UiTooltipRelationship.Description,
            "invalid relationship fails before changing the previous options");

        var described = Wrap(Fluent.Button("Described"), "Only the first target");
        var unrelated = Fluent.Button("Unrelated");
        using var column = new UiColumn([described, unrelated], new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min));
        using var siblingScene = new Scene(column);
        siblingScene.Frame();
        var describedNode = siblingScene.First;
        siblingScene.Host.Tree.TryGet(describedNode.Next!, out var unrelatedNode);
        Check(describedNode.Node.Tooltip?.Text == "Only the first target" && unrelatedNode.Node.Tooltip is null
            && siblingScene.NativeString(unrelatedNode.Node.Id, 30013) == string.Empty,
            "completed tooltip scope does not leak into the next sibling");

        using var throwing = Wrap(new UiButton<UiReleaseButtonBehavior, ThrowingPaint>("Failed") { Size = new(80, 24) }, "Failed scope");
        using var healthy = Fluent.Button("Healthy");
        using var failureScene = new Scene(throwing);
        var caught = false;
        failureScene.Draw = ui =>
        {
            try
            {
                throwing.Render(ui);
            }
            catch (InvalidOperationException error) when (error.Message == "Expected target paint failure.")
            {
                caught = true;
            }
            ui.SetCursorScreenPos(new(20, 120));
            healthy.Render(ui);
        };
        failureScene.Frame();
        Check(caught && failureScene.Host.Tree.Count == 1 && failureScene.First.Node.Name == "Healthy"
            && failureScene.First.Node.Tooltip is null, "throwing target restores semantics before a later healthy sibling");

        using var invalidTree = new UiAccessibilityTree();
        invalidTree.BeginFrame();
        var rejected = false;
        try
        {
            invalidTree.Add(new("invalid-tooltip", string.Empty, UiAccessibilityRole.Button, "Invalid", new(0, 0, 80, 24), true, false, false)
            {
                Tooltip = new("source", "Hidden from accessibility", UiTooltipRelationship.Inaccessible)
            });
        }
        catch (InvalidOperationException)
        {
            rejected = true;
        }
        invalidTree.AbortFrame();
        Check(rejected && invalidTree.Count == 0, "semantic tree refuses publishing an inaccessible relationship as accessible metadata");

        using var snapshot = Wrap(Fluent.Button("Snapshot"), "Frame description");
        snapshot.Options = snapshot.Options with { ShowDelaySeconds = 0 };
        using var snapshotScene = new Scene(snapshot);
        snapshotScene.Draw = ui =>
        {
            snapshot.Render(ui);
            snapshot.Text = "Next description";
        };
        snapshotScene.Frame(0.01f, new(30, 70));
        Check(snapshot.IsOpen && Surface.LastText == snapshotScene.First.Node.Tooltip?.Text,
            "deferred surface and published semantics use the same target-frame text snapshot");
        snapshotScene.Frame(0.01f, new(30, 70));
        Check(snapshotScene.First.Node.Tooltip?.Text == "Next description" && Surface.LastText == "Next description",
            "post-target text changes are visible on the next frame");

        using var firstTip = Wrap(Fluent.Button("First"), "First description");
        using var secondTip = Wrap(Fluent.Button("Second"), "Second description");
        firstTip.Visible = secondTip.Visible = true;
        using var controlled = new Scene(firstTip);
        controlled.Draw = ui =>
        {
            firstTip.Render(ui);
            ui.SetCursorScreenPos(new(220, 60));
            secondTip.Render(ui);
        };
        controlled.Frame();
        var tree = controlled.Host.Tree;
        tree.TryGet(tree.Root.Last!, out var secondTarget);
        var firstSource = controlled.First.Node.Tooltip!.Value.SourceId;
        var secondSource = secondTarget.Node.Tooltip!.Value.SourceId;
        Check(firstSource != secondSource && tree.TryGet(firstSource, out var firstSurface)
            && firstSurface.Node.Role == UiAccessibilityRole.Tooltip && firstSurface.Node.ParentId == controlled.First.Node.Id
            && tree.TryGet(secondSource, out var secondSurface) && secondSurface.Node.Role == UiAccessibilityRole.Tooltip
            && secondSurface.Node.ParentId == secondTarget.Node.Id, "both controlled surfaces have separate target relationships");
        firstTip.Visible = false;
        controlled.Frame();
        Check(!tree.TryGet(firstSource, out _) && tree.TryGet(secondSource, out _)
            && controlled.NativeString(controlled.First.Node.Id, 30013) == "First description",
            "dismissing one controlled surface preserves the other surface and hidden help");
    }

    internal static UiTooltip<UiHoverFocusTooltipBehavior, Surface> Wrap(UiWidget target, string text,
        UiTooltipRelationship relationship = UiTooltipRelationship.Description) =>
        new(target, text, new() { Relationship = relationship });

    internal sealed class Scene : IDisposable
    {
        internal readonly WindowsAccessibility Host = new(0);
        private readonly UiContext _context;
        private readonly UiWidget _root;
        internal bool Hidden;
        internal bool Disabled;
        internal UiRect? Clip;
        internal UiHostVisibility HostVisibility;
        internal Action<UiImmediateContext>? Draw;
        internal UiAccessibilityElement First
        {
            get
            {
                if (!Host.Tree.TryGet(Host.Tree.Root.First!, out var node))
                {
                    throw new InvalidOperationException("The target accessibility node is missing.");
                }
                return node;
            }
        }
        internal Scene(UiWidget root)
        {
            _root = root;
            _context = new(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
            ControlFontFixture.Configure(_context);
            _context.SetAccessibilityTree(Host.Tree);
        }
        internal void Frame(float delta = 1f / 60, UiVector2? mouse = null, UiKeyEvent[]? keys = null)
        {
            _context.NewFrame(new UiFrameInfo(delta, new(640, 480), new(1, 1)) { HostVisibility = HostVisibility },
                new(mouse ?? new(-100, -100), false, false, false, false, false, false, 0, 0, keys ?? [], [], default, default),
                new(0, 0, 640, 480), UiTextSettings.Default);
            _context.Render(new Screen(ui =>
            {
                if (Hidden)
                {
                    return;
                }
                ui.EnableRootViewportContentLayout(contentPadding : 0);
                ui.SetCursorScreenPos(new(20, 60));
                using var clip = new UiControlContext(ui).Clip(Clip ?? new(0, 0, 640, 480));
                ui.BeginDisabled(Disabled);
                try
                {
                    if (Draw is { } custom)
                    {
                        custom(ui);
                    }
                    else
                    {
                        _root.Render(ui);
                    }
                }
                finally
                {
                    ui.EndDisabled();
                }
            }));
            using var draw = _context.GetDrawData();
        }
        internal string NativeString(string id, int property)
        {
            var provider = Host.Find(id) ?? throw new InvalidOperationException("The target provider is missing.");
            Marshal.ThrowExceptionForHR(provider.GetPropertyValue(property, out var value));
            try
            {
                if (value.Type != 8)
                {
                    throw new InvalidOperationException("Expected a native BSTR property.");
                }
                return Marshal.PtrToStringBSTR(value.Data.Pointer);
            }
            finally
            {
                Marshal.ThrowExceptionForHR(UiaNative.VariantClear(ref value));
            }
        }
        public void Dispose()
        {
            _context.Dispose();
            Host.Dispose();
        }
    }

    internal readonly struct Surface : IUiTooltipPresentation
    {
        internal static string LastText = string.Empty;
        internal static UiVector2 LastScrollOffset;
        public static UiPopupLayout Arrange(UiControlContext context, string text, UiRect target, UiRect viewport,
            UiTooltipOptions options) => new(new(target.X, target.Y - 32, 120, 28), UiPopupPosition.Above)
            {
                ContentSize = options.ShowScrollbars && text != "Fit" ? new(text == "Vertical" ? 120 : 240, 112) : default
            };
        public static void Paint(UiControlContext context, string text, UiPopupLayout layout)
        {
            var bounds = layout.Bounds;
            LastText = text;
            LastScrollOffset = layout.ScrollOffset;
            context.Frame(bounds, new(220, 240, 255), new(0, 0, 0, 0), 4, 0);
        }
    }
    private readonly struct ThrowingPaint : IUiButtonPresentation
    {
        public static UiVector2 Measure(UiControlContext context, UiVector2 textSize, UiVector2 requestedSize) => requestedSize;
        public static void Paint(UiControlContext context, string text, UiVector2 textSize, UiRect bounds,
            UiButtonInteraction interaction) => throw new InvalidOperationException("Expected target paint failure.");
    }
    private sealed class Screen(Action<UiImmediateContext> draw) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => draw(ui);
    }
}
