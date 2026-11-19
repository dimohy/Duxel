using Duxel.Core;
using Duxel.Platform.Linux;

internal static class SingleLineTextScrollTests
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
        var value = "정😀" + new string('W', 80);
        var original = value;
        var width = 180f;
        var disabled = false;
        var readOnly = false;
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        var screen = new Screen(ui =>
        {
            ui.BeginDisabled(disabled);
            ui.InputText<UiStandardTextInputBehavior, UiFluentInputPresentation<UiFluentSmallInputSize, UiFluentOutlineInput>>("Document", ref value, 4096,
                new UiInputOptions { Flags = readOnly ? UiInputTextFlags.ReadOnly : 0 }, width);
            ui.EndDisabled();
        });
        UiAccessibilityElement Frame()
        {
            context.NewFrame(new(.1f, new(400, 300), new(1, 1)), idle, new(0, 0, 400, 300), UiTextSettings.Default);
            context.Render(screen);
            context.GetDrawData().ReleasePooled();
            return tree.CaptureSnapshot().Elements.Single(e => e.Node.Name == "Document");
        }
        UiAccessibilityElement Scroll(UiAccessibilityElement node, int start, int end, UiAccessibilityTextScrollPlacement placement)
        {
            var content = node.Node.TextInfo!.Content;
            check(tree.RequestTextScroll(node.Node.Id, node.RuntimeId,
                new(content, content.GetUtf16Offset(start), content.GetUtf16Offset(end), placement), wait: false) == UiAccessibilityResult.Success,
                "Single-line source range accepts the requested placement, " + placement);
            return Frame();
        }
        var node = Frame();
        node = Scroll(node, 0, 1, UiAccessibilityTextScrollPlacement.TopLeft);
        var first = node.Node.TextLayout!.GetCharacterExtents(0);
        // Small outline input content has9px insets, independently specified by its presentation.
        var contentWidth = node.Node.Bounds.Width - 18;
        var caret = node.Node.TextInfo!.CaretUtf16;
        var anchor = node.Node.TextInfo.AnchorUtf16;
        const int target = 30;
        foreach (var placement in Enum.GetValues<UiAccessibilityTextScrollPlacement>())
        {
            node = Scroll(node, 0, 1, UiAccessibilityTextScrollPlacement.TopLeft);
            node = Scroll(node, target, target + 1, placement);
            var cell = node.Node.TextLayout!.GetCharacterExtents(target);
            var left = placement is UiAccessibilityTextScrollPlacement.TopLeft or UiAccessibilityTextScrollPlacement.LeftEdge;
            check(MathF.Abs((left ? cell.X : cell.X + cell.Width) - (first.X + (left ? 0 : contentWidth))) < .05f
                && cell.Y == first.Y && node.Node.TextInfo!.CaretUtf16 == caret && node.Node.TextInfo.AnchorUtf16 == anchor
                && !node.Node.Focused && value == original,
                $"Single-line range scrolling preserves focus/selection/vertical position, {placement}; cell={cell}; first={first}; viewportWidth={contentWidth}; caret={node.Node.TextInfo!.CaretUtf16}/{caret}; anchor={node.Node.TextInfo.AnchorUtf16}/{anchor}; focused={node.Node.Focused}");
        }
        node = Frame();
        var targetPoint = new UiVector2(first.X + 40, first.Y + 1);
        var current = node.Node.TextInfo!.Content;
        check(tree.RequestTextScroll(node.Node.Id, node.RuntimeId,
            new(current, current.GetUtf16Offset(target), current.GetUtf16Offset(target + 1), targetPoint), wait: false)
            == UiAccessibilityResult.Success, "Single-line text accepts a visible local target point");
        node = Frame();
        check(MathF.Abs(node.Node.TextLayout!.GetCharacterExtents(target).X - targetPoint.X) < .05f,
            "Single-line range-to-point aligns the actual source cell horizontally");
        tree.RequestAction(node.Node.Id, node.RuntimeId, UiAccessibilityAction.Focus, wait: false);
        node = Frame();
        check(node.Node.Focused, "The single-line fixture obtains real logical input focus before the retention check");
        caret = node.Node.TextInfo!.CaretUtf16;
        node = Scroll(node, target, target + 1, UiAccessibilityTextScrollPlacement.LeftEdge);
        var positioned = node.Node.TextLayout!.GetCharacterExtents(target);
        node = Frame();
        check(node.Node.TextLayout!.GetCharacterExtents(target) == positioned && node.Node.TextInfo!.CaretUtf16 == caret && node.Node.Focused,
            "An idle active input preserves the requested viewport instead of chasing the unchanged caret");
        current = node.Node.TextInfo!.Content;
        tree.RequestTextSelection(node.Node.Id, node.RuntimeId, new(current, caret, caret), wait: false);
        node = Frame();
        check(node.Node.TextLayout!.GetRangeExtents(current.CharacterCount, current.CharacterCount).X
            <= node.Node.Bounds.X + node.Node.Bounds.Width,
            "An explicit request for the unchanged caret still resumes normal caret visibility");
        node = Scroll(node, target, target + 1, UiAccessibilityTextScrollPlacement.LeftEdge);
        current = node.Node.TextInfo!.Content;
        tree.RequestTextSelection(node.Node.Id, node.RuntimeId, new(current, 0, 0), wait: false);
        node = Frame();
        check(node.Node.TextInfo!.CaretUtf16 == 0 && node.Node.TextLayout!.GetCharacterExtents(0).X >= first.X,
            "A subsequent accessibility caret move resumes normal caret visibility");
        node = Scroll(node, target, target + 1, UiAccessibilityTextScrollPlacement.LeftEdge);
        width = 120;
        node = Frame();
        check(node.Node.TextLayout!.GetCharacterExtents(0).X >= first.X,
            "A changed input width invalidates retained range alignment and resumes caret visibility");
        disabled = true;
        node = Frame();
        check(tree.RequestTextScroll(node.Node.Id, node.RuntimeId, new(node.Node.TextInfo!.Content, 0, 1), wait: false)
            == UiAccessibilityResult.Disabled, "Disabled single-line range scrolling is refused before mutation");
        disabled = false;
        readOnly = true;
        node = Frame();
        node = Scroll(node, target, target + 1, UiAccessibilityTextScrollPlacement.LeftEdge);
        check(node.Node.ReadOnly && value == original, "Read-only single-line text permits range movement without editing its source");
        var stale = node.Node.TextInfo!.Content;
        tree.RequestTextScroll(node.Node.Id, node.RuntimeId, new(stale, 0, 1), wait: false);
        value = "다른 " + new string('M', 80);
        node = Frame();
        check(!ReferenceEquals(node.Node.TextInfo!.Content, stale)
            && tree.RequestTextScroll(node.Node.Id, node.RuntimeId, new(stale, 0, 1), wait: false) == UiAccessibilityResult.InvalidArgument,
            "Single-line range requests cannot reuse source identity after an external content replacement");
        value = "정😀";
        node = Frame();
        var before = node.Node.TextLayout!.GetCharacterExtents(0);
        node = Scroll(node, 1, 2, UiAccessibilityTextScrollPlacement.RightEdge);
        check(node.Node.TextLayout!.GetCharacterExtents(0) == before && value == "정😀",
            "Short single-line text succeeds without inventing scroll range or splitting a supplementary scalar");
    }

    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
