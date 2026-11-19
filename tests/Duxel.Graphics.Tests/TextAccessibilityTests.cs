using Duxel.Core;

internal static class TextAccessibilityTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame(Action<UiImmediateContext> render)
        {
            context.NewFrame(new(0, new(400, 240), new(1, 1)), idle, new(0, 0, 400, 240), UiTextSettings.Default);
            context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding: 0); render(ui); }));
            using var data = context.GetDrawData();
        }
        UiAccessibilityNode[] Nodes() => tree.CaptureSnapshot().Elements.Select(x => x.Node).ToArray();
        using var first = Fluent.Body1("정세일 ## literal");
        using var second = Fluent.Body1("정세일 ## literal");
        Frame(ui => { first.Render(ui); second.Render(ui); });
        var initial = Nodes();
        check(initial.Length == 2 && initial[0].Id != initial[1].Id,
            "Equal sibling text has distinct passive identity with the existing Parent input policy");
        check(initial.All(x => x.Role == UiAccessibilityRole.Label && x.Name == "정세일 ## literal"),
            "Standalone Fluent text publishes literal Korean text rather than stripping identifier markers");
        check(initial.All(x => !x.Focusable && !x.Focused && !x.ValuePattern && x.ReadOnly && x.Value is null && x.Range is null),
            "Static text publishes no focus, editable value or numeric pattern");
        Frame(ui => { first.Render(ui); second.Render(ui); });
        check(Nodes().Select(x => x.Id).SequenceEqual(initial.Select(x => x.Id)),
            "Repeated text frames retain native identities");
        first.Source = "Changed 정세일";
        Frame(ui => { first.Render(ui); second.Render(ui); });
        check(Nodes()[0].Id == initial[0].Id && Nodes()[0].Name == "Changed 정세일",
            "Changing text updates the name without replacing identity");
        first.AccessibleName = "Profile summary";
        Frame(first.Render);
        check(Nodes().Single().Name == "Profile summary" && Nodes().Single().Id == initial[0].Id,
            "Explicit accessible names preserve passive identity");
        first.AccessibleName = null;
        using var allocated = Dux.SizedBox(first, 80, 64);
        Frame(allocated.Render);
        check(Nodes().Single().Bounds == new UiRect(0, 0, 80, 64),
            "Text accessibility uses its actual assigned root rather than glyph extents");
        Frame(ui => { using var clip = new UiControlContext(ui).Clip(new(10, 12, 30, 24)); allocated.Render(ui); });
        check(Nodes().Single().Bounds == new UiRect(10, 12, 30, 24),
            "Text accessibility follows the active clip without another measurement");
        allocated.Margin = new(7, 11, 13, 17);
        Frame(allocated.Render);
        check(Nodes().Single().Bounds == new UiRect(7, 11, 80, 64),
            "Text accessibility inherits the common widget margin");
        allocated.Margin = default;
        allocated.Transform = new(RotationDegrees: 90);
        Frame(allocated.Render);
        var rotated = Nodes().Single().Bounds;
        check(MathF.Abs(rotated.Width - 64) < .01f && MathF.Abs(rotated.Height - 80) < .01f,
            "Text accessibility follows the common widget transform");
        allocated.Transform = new(RotationDegrees: 0);
        Frame(ui =>
        {
            ui.BeginDisabled();
            try
            {
                allocated.Render(ui);
            }
            finally { ui.EndDisabled(); }
        });
        check(!Nodes().Single().Enabled && !Nodes().Single().Focusable,
            "Standalone text inherits disabled state without acquiring keyboard focus");
        Frame(ui => { ui.SetCursorScreenPos(new(500, 0)); allocated.Render(ui); });
        check(Nodes().Single().Offscreen && Nodes().Single().Bounds.Width == 0,
            "Offscreen text keeps its node and exposes clipped zero-width geometry");
        using var childText = Fluent.Body1("Child static text");
        using var parent = Fluent.Skeleton(childText, new(Paused: true));
        parent.AccessibleName = "Loading text group";
        Frame(parent.Render);
        var parentNodes = Nodes();
        check(parentNodes.Single(x => x.Name == "Child static text").ParentId ==
            parentNodes.Single(x => x.Name == "Loading text group").Id,
            "Static text preserves the actual accessibility parent scope");
        Frame(_ => { });
        check(Nodes().Length == 0, "Unmounted standalone text retires from the accessibility tree");
        var reads = 0;
        using var dynamic = Fluent.Body1(() => { reads++; return "Dynamic 한글"; });
        Frame(dynamic.Render);
        check(reads == 1 && Nodes().Single().Name == "Dynamic 한글",
            "Accessible dynamic text reads its source only once per render");
        context.SetAccessibilityTree(null);
        Frame(dynamic.Render);
        check(reads == 2, "The no-accessibility path preserves the existing single source read");
        context.SetAccessibilityTree(tree);
        using var unsupported = new UiText<MissingRootPresentation>("Missing");
        var rejected = false;
        Frame(ui =>
        {
            try
            {
                unsupported.Render(ui);
            }
            catch (NotSupportedException) { rejected = true; }
            second.Render(ui);
        });
        check(rejected && Nodes().Single().Name == "정세일 ## literal",
            "A missing custom text root fails explicitly and restores the enclosing capture");
        TextAllocationTests.Run(check);
    }

    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
    private readonly struct MissingRootPresentation : IUiTextPresentation
    {
        public static UiVector2 Measure(UiControlContext context, string text) => default;
        public static void Render(UiControlContext context, string text, UiColor? color)
        {
        }
    }
}
