using Duxel.Core;

internal static class StyledContrastTests
{
    internal static void Run(Action<bool, string> check)
        => Run(new(new(4, 8, 16), new(240, 230, 220), new(110, 120, 130),
            new(10, 90, 180), new(250, 240, 230), new(30, 200, 220), new(20, 30, 40), new(210, 220, 230)), check);

    internal static void Run(UiForcedColorPalette palette, Action<bool, string> check)
    {
        foreach (var mode in new[] { "Native", "Segmented", "Wrapped", "Table", "Syntax" })
        {
            using var fixture = new PresentationReuseTests.Fixture(1, true);
            if (mode == "Segmented")
                fixture.Context.SetPlatformTextBackend(new RunOnlyTextBackend());
            var initial = "# Heading\n\nBody [Link](https://e.test) ~~strike~~ ` code `";
            initial += mode == "Table" ? "\n\n| Head |\n| --- |\n| Cell |" : "\n\n---";
            var text = new UiState<string>(initial);
            var options = new UiMarkdownOptions(Width: mode == "Wrapped" ? 180 : 300, Height: 220, ShowBorder: false);
            using UiMarkdownWidget widget = mode == "Syntax"
                ? Dux.MarkdownEditor("Styled contrast", text.Binding, options)
                : Dux.MarkdownView("Styled contrast", text.Binding, options);
            using var tree = new UiAccessibilityTree();
            fixture.Context.SetAccessibilityTree(tree);
            var ime = new MarkdownLifetimeTests.Ime();
            var disabled = false;
            ime.AfterCaret = () => fixture.Context.State.AdvanceTime(.6f);
            fixture.Context.SetImeHandler(ime);
            void Draw(UiImmediateContext ui)
            {
                ui.EnableRootViewportContentLayout(contentPadding: 0);
                var context = new UiControlContext(ui);
                context.Frame(new(0, 0, fixture.FrameInfo.DisplaySize.X, fixture.FrameInfo.DisplaySize.Y),
                    context.Theme.WindowBg, default, 0, 0);
                context.RestartInputCaretBlink();
                ui.SetCursorScreenPos(new(10, 10));
                ui.BeginDisabled(disabled);
                widget.Render(ui);
                ui.EndDisabled();
            }
            byte[] Frame() => fixture.Capture(Draw);
            UiAccessibilityNode Node() => tree.CaptureSnapshot().Elements.Single(e => e.Node.Name == "Styled contrast").Node;
            bool Has(byte[] pixels, UiColor color)
            {
                for (var offset = 0; offset < pixels.Length; offset += 4)
                    if (BitConverter.ToUInt32(pixels, offset) == color.Rgba)
                        return true;
                return false;
            }
            fixture.Context.SetTheme(palette.Apply(UiCompiledDesign.Windows11Dark.Theme));
            Frame();
            if (mode == "Syntax")
            {
                var entry = tree.CaptureSnapshot().Elements.Single(e => e.Node.Name == "Styled contrast");
                if (tree.RequestAction(entry.Node.Id, entry.RuntimeId, UiAccessibilityAction.Focus, wait: false) != UiAccessibilityResult.Success)
                    throw new InvalidOperationException("Styled syntax focus rejected.");
                Frame();
            }
            var parsed = widget.Document.ParseCount;
            var actual = Frame();
            check(Has(actual, palette.CanvasText), $"Styled contrast / {mode} / body uses CanvasText");
            check(Has(actual, palette.LinkText), $"Styled contrast / {mode} / link uses LinkText");
            var linkOffset = initial.IndexOf("Link", StringComparison.Ordinal);
            check(Node().TextAppearance?.GetPaintRangeAt(linkOffset).Foreground == palette.LinkText,
                $"Styled contrast / {mode} / accessible link foreground matches painting");
            var changed = palette with { LinkText = new(210, 40, 170) };
            fixture.Context.SetTheme(changed.Apply(UiCompiledDesign.Windows11Dark.Theme));
            var recolored = Frame();
            check(!actual.AsSpan().SequenceEqual(recolored)
                && Node().TextAppearance?.GetPaintRangeAt(linkOffset).Foreground == changed.LinkText,
                $"Styled contrast / {mode} / link-only palette changes refresh pixels and accessible paint");
            fixture.Context.SetTheme(palette.Apply(UiCompiledDesign.Windows11.Theme));
            check(actual.AsSpan().SequenceEqual(Frame()), $"Styled contrast / {mode} / forced palette ignores base light or dark colors");
            var flat = new UiColor(23, 43, 67);
            // Isolate authored colors without assuming bit-exact cancellation in hardware alpha blending.
            var invisible = new UiColor(23, 43, 67, 0);
            fixture.Context.SetTheme(new UiForcedColorPalette(flat, invisible, invisible, flat, invisible, invisible, flat, invisible)
                .Apply(UiCompiledDesign.Windows11Dark.Theme));
            var flattened = Frame();
            var uniform = true;
            var firstDifference = -1;
            var differentPixels = 0;
            for (var offset = 0; offset < flattened.Length; offset += 4)
                if (BitConverter.ToUInt32(flattened, offset) != flat.Rgba)
                {
                    uniform = false;
                    if (firstDifference < 0)
                        firstDifference = offset;
                    differentPixels++;
                }
            var firstColor = firstDifference < 0 ? flat.Rgba : BitConverter.ToUInt32(flattened, firstDifference);
            check(uniform, $"Styled contrast / {mode} / transparent system foreground leaves only Canvas without authored colors"
                + $" / different={differentPixels}, first=({firstDifference / 4 % (int)fixture.FrameInfo.DisplaySize.X},{firstDifference / 4 / (int)fixture.FrameInfo.DisplaySize.X}), actual={firstColor:X8}, expected={flat.Rgba:X8}");
            disabled = true;
            fixture.Context.SetTheme(palette.Apply(UiCompiledDesign.Windows11Dark.Theme));
            var disabledPixels = Frame();
            check(Has(disabledPixels, palette.GrayText) && !Has(disabledPixels, palette.LinkText),
                $"Styled contrast / {mode} / disabled glyphs and links use GrayText");
            check(Node().TextAppearance?.GetPaintRangeAt(linkOffset).Foreground == palette.GrayText,
                $"Styled contrast / {mode} / disabled accessible link foreground uses GrayText");
            check(widget.Document.ParseCount == parsed && text.Value == initial,
                $"Styled contrast / {mode} / palette changes preserve parsed source");
        }
    }
}
