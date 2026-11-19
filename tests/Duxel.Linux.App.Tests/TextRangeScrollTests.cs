using Duxel.Core;
using Duxel.Platform.Linux;

internal static class TextRangeScrollTests
{
    internal static void Run(string primary, string secondary, Action<bool, string> check)
    {
        using var tree = new UiAccessibilityTree();
        using var backend = new LinuxPlatformTextBackend();
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        context.SetPlatformTextBackend(backend);
        context.SetDirectTextEnabled(true);
        context.SetDirectTextPrimaryFontPath(primary);
        context.SetDirectTextSecondaryFontPath(secondary);
        context.SetTypographyFonts(new(primary, primary, primary, secondary, secondary, secondary) { Monospace = primary });
        context.SetAccessibilityTree(tree);
        using var owner = new UiMultilineInputOwner();
        var value = string.Join('\n', Enumerable.Repeat(new string('W', 70), 20));
        var disabled = false;
        var readOnly = false;
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        var screen = new Screen(ui =>
        {
            ui.BeginDisabled(disabled);
            ui.InputTextMultiline<UiStandardMultilineInputBehavior, UiFluentTextareaPresentation<UiFluentSmallTextareaSize, UiFluentOutlineInput>>(
                "Document", ref value, 4096, new() { Wrap = false, Flags = readOnly ? UiInputTextFlags.ReadOnly : 0 }, 170, 90, owner);
            ui.EndDisabled();
        });
        UiAccessibilityElement Frame()
        {
            context.NewFrame(new(.1f, new(400, 300), new(1, 1)), idle, new(0, 0, 400, 300), UiTextSettings.Default);
            context.Render(screen);
            context.GetDrawData().ReleasePooled();
            return tree.CaptureSnapshot().Elements.Single(e => e.Node.Name == "Document");
        }
        UiAccessibilityElement Reset()
        {
            var current = Frame();
            tree.RequestScroll(current.Node.Id, current.RuntimeId, UiAccessibilityScrollCommand.ByPercent(0, 0), wait: false);
            return Frame();
        }
        const int target = 8 * 71 + 20;
        var node = Reset();
        var original = node.Node.TextInfo!;
        var first = node.Node.TextLayout!.GetCharacterExtents(0);
        var size = node.Node.Scroll!.Value.Viewport;
        foreach (var (placement, left, top) in new[]
        {
            (UiAccessibilityTextScrollPlacement.TopLeft, true, true),
            (UiAccessibilityTextScrollPlacement.BottomRight, false, false),
            (UiAccessibilityTextScrollPlacement.TopEdge, false, true),
            (UiAccessibilityTextScrollPlacement.BottomEdge, false, false),
            (UiAccessibilityTextScrollPlacement.LeftEdge, true, false),
            (UiAccessibilityTextScrollPlacement.RightEdge, false, false),
            (UiAccessibilityTextScrollPlacement.Nearest, false, false)
        })
        {
            node = Reset();
            check(tree.RequestTextScroll(node.Node.Id, node.RuntimeId,
                new(node.Node.TextInfo!.Content, target, target + 1, placement), wait: false) == UiAccessibilityResult.Success,
                "Committed source range scroll accepts the requested placement, " + placement);
            node = Frame();
            var box = node.Node.TextLayout!.GetCharacterExtents(target);
            var x = left ? box.X : box.X + box.Width;
            var y = top ? box.Y : box.Y + box.Height;
            check(MathF.Abs(x - (first.X + (left ? 0 : size.X))) < .05f
                && MathF.Abs(y - (first.Y + (top ? 0 : size.Y))) < .05f
                && node.Node.TextInfo!.CaretUtf16 == original.CaretUtf16 && node.Node.TextInfo.AnchorUtf16 == original.AnchorUtf16,
                "Applied range placement aligns actual text geometry without moving source selection, " + placement);
        }
        node = Reset();
        var point = new UiVector2(first.X + 50, first.Y + 20);
        check(tree.RequestTextScroll(node.Node.Id, node.RuntimeId, new(node.Node.TextInfo!.Content, target, target + 1, point), wait: false)
            == UiAccessibilityResult.Success, "Committed range scrolling accepts a finite visible client point");
        node = Frame();
        var positioned = node.Node.TextLayout!.GetCharacterExtents(target);
        check(MathF.Abs(positioned.X - point.X) < .05f && MathF.Abs(positioned.Y - point.Y) < .05f,
            "Client-point range scrolling aligns the actual source cell in both axes");
        var offset = node.Node.Scroll!.Value.Offset;
        check(Frame().Node.Scroll!.Value.Offset == offset, "An unchanged subsequent input frame preserves the requested range scroll position");
        disabled = true;
        node = Frame();
        check(tree.RequestTextScroll(node.Node.Id, node.RuntimeId, new(node.Node.TextInfo!.Content, 0, 1), wait: false)
            == UiAccessibilityResult.Disabled, "Disabled text refuses a range scroll before mutation");
        disabled = false;
        readOnly = true;
        node = Frame();
        check(tree.RequestTextScroll(node.Node.Id, node.RuntimeId, new(node.Node.TextInfo!.Content, target, target + 1), wait: false)
            == UiAccessibilityResult.Success, "Read-only text permits scrolling without permitting source edits");
        node = Frame();
        var previousContent = node.Node.TextInfo!.Content;
        value = string.Join('\n', Enumerable.Repeat(new string('M', 70), 20));
        node = Frame();
        check(tree.RequestTextScroll(node.Node.Id, node.RuntimeId, new(previousContent, target, target + 1), wait: false)
            == UiAccessibilityResult.InvalidArgument, "Stale content identity cannot scroll a replacement document");
        check(value.StartsWith("MMM", StringComparison.Ordinal) && node.Node.TextLayout!.Content.Text == value,
            "Source range scrolling and refused requests preserve committed text");
        value = "short";
        node = Frame();
        var stationary = node.Node.TextLayout!.GetCharacterExtents(0);
        check(node.Node.Scroll is null && tree.RequestTextScroll(node.Node.Id, node.RuntimeId,
            new(node.Node.TextInfo!.Content, 0, 1), wait: false) == UiAccessibilityResult.Success,
            "An already visible multiline range accepts a successful no-movement request without a scrollbar");
        check(Frame().Node.TextLayout!.GetCharacterExtents(0) == stationary && value == "short",
            "No-movement range scrolling preserves actual geometry and source");
    }

    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
