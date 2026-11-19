using Duxel.Core;
using Duxel.Platform.Linux;

internal static class UnwrappedTextLayoutTests
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
        var label = "Document";
        var identityProbe = false;
        var value = "정😀\n\nend\n";
        var wrap = false;
        var disabled = false;
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        var screen = new Screen(ui =>
        {
            ui.BeginDisabled(disabled);
            if (identityProbe)
                ui.InputTextMultiline<UiStandardMultilineInputBehavior, BarePresentation>(label, ref value, 4096,
                    new() { Wrap = wrap }, 170, 210);
            else
                ui.InputTextMultiline<UiStandardMultilineInputBehavior, UiFluentTextareaPresentation<UiFluentSmallTextareaSize, UiFluentOutlineInput>>(
                    label, ref value, 4096, new() { Wrap = wrap }, 170, 75, owner);
            ui.EndDisabled();
        });
        UiAccessibilityElement Frame()
        {
            context.NewFrame(new(.1f, new(400, 300), new(1, 1)), idle, new(0, 0, 400, 300), UiTextSettings.Default);
            context.Render(screen);
            context.GetDrawData().ReleasePooled();
            return tree.CaptureSnapshot().Elements.Single(e => e.Node.Name == label);
        }
        var node = Frame();
        var layout = node.Node.TextLayout ?? throw new InvalidOperationException("Actual unwrapped source geometry missing.");
        check(layout.LineCount == 4 && layout.GetLine(0) == new UiAccessibilityTextLine(0, 2, 3)
            && layout.GetLine(1) == new UiAccessibilityTextLine(3, 3, 4) && layout.GetLine(3) == new UiAccessibilityTextLine(8, 8, 8),
            "Actual unwrapped source geometry preserves Korean/emoji, empty and trailing hard lines");
        var emoji = layout.GetCharacterExtents(1);
        check(emoji.Width > 0 && layout.GetOffsetAtPoint(emoji.X + emoji.Width / 2, emoji.Y + emoji.Height / 2) == 1,
            "Unwrapped painted emoji hits retain their original scalar source offset");
        check(ReferenceEquals(layout, Frame().Node.TextLayout), "Unchanged legacy unwrapped geometry retains its cached hard-line map and snapshot");
        check(new LinuxTextBoundarySequence(layout, false).GetRange(4, 0) == (4, 8),
            "The native line adapter reads the exact unwrapped painted source intervals");
        value = new string('W', 70) + "\n정😀\n\nend\nlast";
        node = Frame();
        var before = node.Node.TextLayout!;
        var beforeFirst = before.GetCharacterExtents(0);
        var scroll = node.Node.Scroll!.Value;
        check(before.LineCount == 5 && scroll.HorizontallyScrollable && scroll.VerticallyScrollable,
            "Editable unwrapped multiline publishes both actual horizontal and vertical scroll ranges");
        var distant = before.GetCharacterExtents(60);
        check(before.GetOffsetAtPoint(distant.X + distant.Width / 2, distant.Y + distant.Height / 2) == -1,
            "Offscreen unwrapped source boxes cannot claim a visible text hit");
        check(tree.RequestScroll(node.Node.Id, node.RuntimeId, UiAccessibilityScrollCommand.ByPercent(100, 100), wait: false) == UiAccessibilityResult.Success,
            "Unwrapped multiline accepts a guarded percent scroll request");
        node = Frame();
        var moved = node.Node.TextLayout!;
        var offset = node.Node.Scroll!.Value.Offset;
        check(offset.X > 0 && offset.Y > 0 && moved.GetCharacterExtents(0).X < before.GetCharacterExtents(0).X
            && moved.GetCharacterExtents(0).Y < before.GetCharacterExtents(0).Y,
            "Applied two-axis scrolling changes actual source boxes before the frame is painted and published");
        check(before.GetCharacterExtents(0) == beforeFirst && layout.GetCharacterExtents(1) == emoji
            && ReferenceEquals(moved, Frame().Node.TextLayout),
            "Scroll snapshots remain immutable and unchanged scrolled frames reuse their geometry");
        disabled = true;
        node = Frame();
        check(tree.RequestScroll(node.Node.Id, node.RuntimeId, UiAccessibilityScrollCommand.ByPercent(0, 0), wait: false) == UiAccessibilityResult.Disabled
            && node.Node.Scroll!.Value.Offset == offset,
            "Disabled multiline rejects native scrolling without changing the existing offset");
        disabled = false;
        wrap = true;
        node = Frame();
        check(node.Node.TextLayout!.LineCount > before.LineCount && node.Node.Scroll is { HorizontallyScrollable: false, VerticallyScrollable: true },
            "Switching the same editor to wrapped mode replaces hard-line geometry and removes horizontal scrolling");
        check(tree.RequestScroll(node.Node.Id, node.RuntimeId, UiAccessibilityScrollCommand.ByPercent(-1, 100), wait: false) == UiAccessibilityResult.Success,
            "Wrapped multiline uses the same shared accessible vertical scroll request path");
        node = Frame();
        check(node.Node.Scroll!.Value.Offset.Y > 0 && node.Node.TextInfo!.Content.Text == value,
            "Wrapped accessible scrolling preserves exact committed source content");
        identityProbe = true;
        label = "Identity";
        value = new string('W', 70);
        wrap = false;
        var hard = Frame().Node.TextLayout!;
        wrap = true;
        var soft = Frame().Node.TextLayout!;
        check(hard.ClipBounds == soft.ClipBounds && hard.LineCount == 1 && soft.LineCount > 1
            && !ReferenceEquals(hard, soft),
            "Different line-map instances with matching local versions and identical clips cannot reuse stale geometry");
        check(ReferenceEquals(soft, Frame().Node.TextLayout),
            "Fresh ownerless line maps with identical layout still reuse immutable accessible geometry");
    }

    private readonly struct BarePresentation : IUiMultilineInputPresentation
    {
        public static UiControlTypographyScope BeginText(UiControlContext context) => UiFluentSmallTextareaSize.BeginText(context);
        public static UiMultilineInputLayout Measure(UiControlContext context, float width, float height)
            => new(new(0, 0, width, height), context.TypographyLineHeight) { Content = new(0, 0, width, height) };
        public static void Paint(UiControlContext context, in UiMultilineInputLayout layout, in UiMultilineEditResult edit)
            => context.PaintEditableMultiline(layout, edit, new(new(31, 31, 31), context.Theme.InputSelectionBg, new(31, 31, 31)) { Typography = true });
    }

    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
