using Duxel.Core;
using Duxel.Platform.Linux;

internal static class CompositionLayoutTests
{
    internal static void Run(string primary, string secondary, Action<bool, string> check)
    {
        foreach (var mode in new[] { "single", "multiline", "markdown", "legacy", "legacy-ownerless" })
        {
            using var tree = new UiAccessibilityTree();
            using var backend = new LinuxPlatformTextBackend();
            using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
            context.SetPlatformTextBackend(backend);
            context.SetDirectTextEnabled(true);
            context.SetDirectTextPrimaryFontPath(primary);
            context.SetDirectTextSecondaryFontPath(secondary);
            context.SetTypographyFonts(new(primary, primary, primary, secondary, secondary, secondary)
            {
                Monospace = primary
            });
            context.SetAccessibilityTree(tree);
            var ime = new Ime();
            context.SetImeHandler(ime);
            var value = "A정😀B";
            using var owner = new UiMultilineInputOwner();
            using var markdown = new UiMarkdownEditor("Document", new(() => value, text => value = text), new(Width: 250, Height: 180));
            var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
            var screen = new Screen(ui =>
            {
                if (mode == "single")
                    ui.InputText("Document", ref value, 512);
                else if (mode == "multiline")
                    ui.InputTextMultiline<UiFluentMultilineInputBehavior, UiFluentTextareaPresentation<UiFluentSmallTextareaSize, UiFluentOutlineInput>>(
                        "Document", ref value, 512, 250, 180, owner);
                else if (mode.StartsWith("legacy", StringComparison.Ordinal))
                    ui.InputTextMultiline<UiStandardMultilineInputBehavior, UiFluentTextareaPresentation<UiFluentSmallTextareaSize, UiFluentOutlineInput>>(
                        "Document", ref value, 512, new()
                        {
                            Wrap = false
                        }, 250, 180, mode == "legacy" ? owner : null);
                else
                    markdown.Render(ui);
            });
            UiAccessibilityElement Frame(UiInputState? input = null)
            {
                context.NewFrame(new(.1f, new(400, 300), new(1, 1)), input ?? idle, new(0, 0, 400, 300), UiTextSettings.Default);
                context.Render(screen);
                context.GetDrawData().ReleasePooled();
                return tree.CaptureSnapshot().Elements.Single(e => e.Node.Name == "Document");
            }
            var node = Frame();
            if (mode.StartsWith("legacy", StringComparison.Ordinal))
            {
                var cell = node.Node.TextLayout!.GetCharacterExtents(0);
                var pointer = new UiVector2(cell.X + cell.Width / 2, cell.Y + cell.Height / 2);
                Frame(idle with
                {
                    MousePosition = pointer,
                    LeftMouseDown = true,
                    LeftMousePressed = true
                });
                Frame(idle with
                {
                    MousePosition = pointer,
                    LeftMouseReleased = true
                });
            }
            else
                check(tree.RequestAction(node.Node.Id, node.RuntimeId, UiAccessibilityAction.Focus, wait: false) == UiAccessibilityResult.Success,
                    "The navigable editor accepts explicit accessibility focus, " + mode);
            node = Frame();
            tree.RequestTextSelection(node.Node.Id, node.RuntimeId, new(node.Node.TextInfo!.Content, 1, 1), wait: false);
            node = Frame();
            var original = node.Node.TextLayout!;
            check(ime.HasOwner && node.Node.TextInfo!.CaretUtf16 == 1,
                $"The editor owns IME composition at the requested committed source caret, {mode}, owner={ime.HasOwner}, caret={node.Node.TextInfo!.CaretUtf16}, bounds={node.Node.Bounds}, cell={original.GetCharacterExtents(0)}");
            ime.Preedit = "한";
            node = Frame();
            var composing = node.Node.TextLayout ?? throw new InvalidOperationException("Composition source geometry missing, " + mode);
            check(composing.HasComposition && node.Node.TextInfo is { CaretUtf16: 1, Content.Text: "A정😀B", Content.CharacterCount: 4 }
                && ReferenceEquals(original.Content, composing.Content), "Preedit keeps exact committed source identity and scalar caret, " + mode);
            var prefix = composing.GetCharacterExtents(0);
            var suffix = composing.GetCharacterExtents(1);
            check(prefix == original.GetCharacterExtents(0) && suffix.X > original.GetCharacterExtents(1).X,
                "Actual preedit leaves prefix geometry intact and moves the committed suffix, " + mode);
            var gapX = (prefix.X + prefix.Width + suffix.X) / 2;
            check(composing.GetOffsetAtPoint(gapX, suffix.Y + suffix.Height / 2) == -1,
                "An uncommitted preedit-only hit cannot invent a committed source offset, " + mode);
            var caret = composing.GetRangeExtents(1, 1);
            if (mode != "single")
                check(ime.CaretUpdates > 0 && MathF.Abs(ime.LastCaret.X - caret.X) <= 1 && MathF.Abs(ime.LastCaret.Y - caret.Y) <= 1
                    && ime.LastCaret.Width > 0 && ime.LastCaret.Height > 0,
                    "The input-method caret agrees with the displayed source insertion after preedit, " + mode);
            check(MathF.Abs(caret.X - suffix.X) <= 1 && caret.Width == 0,
                "The committed insertion caret follows the visible preedit rather than its old source position, " + mode);
            check(node.Node.TextAppearance is null, "Preedit does not publish stale uniform or styled source paint, " + mode);
            check(tree.RequestTextScroll(node.Node.Id, node.RuntimeId, new(node.Node.TextInfo!.Content, 0, 1), wait: false)
                == UiAccessibilityResult.Busy, "Source-range scrolling refuses to move a viewport during active preedit, " + mode);
            node = Frame();
            check(ReferenceEquals(composing, node.Node.TextLayout), "An unchanged preedit retains its immutable source geometry, " + mode);
            ime.Preedit = "한😀";
            var changed = Frame().Node.TextLayout!;
            check(changed.GetCharacterExtents(1).X > suffix.X && !ReferenceEquals(changed, composing),
                "Changed preedit text invalidates geometry while committed source remains unchanged, " + mode);
            if (mode != "single")
            {
                ime.Preedit = "조합\n새줄 ";
                var wrapped = Frame().Node.TextLayout!;
                var displayedCaret = wrapped.GetRangeExtents(1, 1);
                check(MathF.Abs(ime.LastCaret.X - displayedCaret.X) <= 1 && MathF.Abs(ime.LastCaret.Y - displayedCaret.Y) <= 1,
                    "Multiline preedit updates input-method caret row and column from the same display map, " + mode);
                check(wrapped.LineCount > original.LineCount && wrapped.GetCharacterExtents(1).Y > original.GetCharacterExtents(1).Y,
                    "Actual multiline preedit moves committed suffix geometry to its displayed row, " + mode);
                check(Enumerable.Range(0, wrapped.LineCount).Select(wrapped.GetLine).All(line => line.Start >= 0
                    && line.End >= line.Start && line.NextStart >= line.End && line.NextStart <= 4)
                    && wrapped.GetLineRangeAt(1) == (1, 4) && new LinuxTextBoundarySequence(wrapped, false).GetRange(1, 0) == (1, 4),
                    "Preedit-only rows expose ordered committed source bounds without composition characters, " + mode);
                ime.Preedit = "\u0301";
                var combining = Frame().Node.TextLayout!;
                check(combining.GetCharacterExtents(0).Width > 0 && combining.Content.Text == "A정😀B",
                    "Preedit combining with the preceding grapheme preserves its actual displayed source cell, " + mode);
                ime.Preedit = new string('W', 40);
                var softNode = Frame().Node;
                var soft = softNode.TextLayout!;
                if (mode.StartsWith("legacy", StringComparison.Ordinal))
                    check(soft.LineCount == original.LineCount && soft.Content.CharacterCount == 4
                        && softNode.Scroll is { Offset.X: > 0 }
                        && MathF.Abs(ime.LastCaret.X - soft.GetRangeExtents(1, 1).X) <= 1
                        && ime.LastCaret.X <= soft.ClipBounds.X + soft.ClipBounds.Width,
                        "Unwrapped preedit scrolls horizontally to the displayed caret without adding source rows, " + mode);
                else
                    check(soft.LineCount > original.LineCount && soft.GetCharacterExtents(1).Y > original.GetCharacterExtents(1).Y
                        && soft.Content.CharacterCount == 4,
                        "Soft-wrapped preedit moves committed source rows without exposing the wrapping text, " + mode);
            }
            ime.Preedit = null;
            node = Frame();
            check(node.Node.TextLayout is { HasComposition: false } restored && restored.GetCharacterExtents(1) == original.GetCharacterExtents(1)
                && composing.GetCharacterExtents(1) == suffix && node.Node.TextAppearance is not null,
                "Cancelling preedit restores geometry and paint without mutating retained composing snapshots, " + mode);
            ime.Commit = "한";
            node = Frame();
            check(value == "A한정😀B" && node.Node.TextInfo!.Content.CharacterCount == 5
                && node.Node.TextLayout is { HasComposition: false } committed && ReferenceEquals(committed.Content, node.Node.TextInfo.Content),
                "IME commit changes source exactly once and publishes coherent final text and geometry, " + mode);
            foreach (var position in new[] { 0, value.Length })
            {
                ime.Preedit = null;
                node = Frame();
                tree.RequestTextSelection(node.Node.Id, node.RuntimeId, new(node.Node.TextInfo!.Content, position, position), wait: false);
                var baseline = Frame().Node.TextLayout!;
                ime.Preedit = "조합";
                var edge = Frame().Node.TextLayout!;
                check(position == 0 ? edge.GetCharacterExtents(0).X > baseline.GetCharacterExtents(0).X
                    : edge.GetCharacterExtents(0) == baseline.GetCharacterExtents(0)
                        && edge.GetRangeExtents(5, 5).X > baseline.GetRangeExtents(5, 5).X,
                    "Preedit at the start or end preserves correct source cells and terminal caret, " + mode);
            }
            ime.Preedit = null;
            value = "";
            var empty = Frame().Node.TextLayout!;
            ime.Preedit = "정😀";
            var emptyComposition = Frame().Node.TextLayout!;
            var emptyCaret = emptyComposition.GetRangeExtents(0, 0);
            check(emptyComposition.HasComposition && emptyComposition.Content.CharacterCount == 0
                && emptyCaret.X > empty.GetRangeExtents(0, 0).X && emptyComposition.GetLineRangeAt(0) == (0, 0),
                "An empty committed document retains only a caret after visible preedit, " + mode);
            check(emptyComposition.GetOffsetAtPoint(emptyCaret.X - 1, emptyCaret.Y + emptyCaret.Height / 2) == -1,
                "Preedit in an empty document cannot appear as committed accessible text, " + mode);
            if (mode.StartsWith("legacy", StringComparison.Ordinal))
            {
                Frame(idle with
                {
                    LeftMousePressed = true,
                    LeftMouseDown = true
                });
                var inactive = Frame().Node;
                check(!ime.HasOwner && inactive.TextLayout is { HasComposition: false } && value.Length == 0,
                    "An outside click releases legacy composition ownership without committing preedit, " + mode);
            }
        }
    }

    private sealed class Ime : IUiImeHandler
    {
        internal string? Preedit, Commit;
        internal UiRect LastCaret;
        internal int CaretUpdates;
        private string? _owner;
        internal bool HasOwner => _owner is not null;
        public string? GetCompositionText() => Preedit;
        public void SetCompositionOwner(string? inputId) => _owner = inputId;
        public void SetCaretRect(UiRect caretRect, UiRect inputRect, float fontPixelHeight, float fontPixelWidth)
        {
            LastCaret = caretRect;
            CaretUpdates++;
        }
        public string? ConsumeCommittedText(string inputId)
        {
            if (inputId != _owner)
                return null;
            var text = Commit;
            Commit = null;
            return text;
        }
        public void ReleaseInput(string inputId) => _ = ConsumeCommittedText(inputId);
        public string? ConsumeRecentCommittedText() => null;
    }

    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
