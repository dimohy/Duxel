using Duxel.Core;

internal static class StyledSelectionColorTests
{
    internal static void Run(UiForcedColorPalette palette, Action<bool, string> check)
    {
        foreach (var mode in new[] { "Native", "Segmented", "Wrapped", "Table" })
        {
            using var fixture = new PresentationReuseTests.Fixture(1, true);
            if (mode == "Segmented")
                fixture.Context.SetPlatformTextBackend(new RunOnlyTextBackend());
            const string rich = "**Bold** ` Code ` [Link](https://e.test)\n\nTail";
            var initial = mode == "Table" ? "**Bold** ` Code `\n\n| Name |\n| --- |\n| **Cell** |\n\nTail" : rich;
            var text = new UiState<string>(initial);
            var width = mode == "Wrapped" ? 140 : 300;
            using var editor = Dux.MarkdownEditor("Styled selection", text.Binding, new(Width: width, Height: 220));
            using var tree = new UiAccessibilityTree();
            fixture.Context.SetAccessibilityTree(tree);
            var mirror = new UiVisualTextLines();
            var source = new UiMarkdownTextSource(new(initial));
            var probes = new List<UiRect>();
            UiVector2? codeSpace = null;
            UiVector2? headerSpace = null;
            var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
            void Draw(UiImmediateContext ui)
            {
                ui.EnableRootViewportContentLayout(contentPadding: 0);
                var context = new UiControlContext(ui);
                context.Frame(new(0, 0, 8, 8), new(48, 48, 48), default, 0, 0);
                context.Frame(new(0, 0, 8, 8), context.Theme.InputSelectionBg, default, 0, 0);
                context.Frame(new(8, 0, 8, 8), new(45, 45, 45), default, 0, 0);
                context.Frame(new(8, 0, 8, 8), context.Theme.InputSelectionBg, default, 0, 0);
                ui.SetCursorScreenPos(new(10, 10));
                new UiControlContext(ui).RestartInputCaretBlink();
                editor.Render(ui);
                var selection = fixture.Context.State.ActiveId is { } active ? fixture.Context.State.GetSelection(active) : default;
                using var typography = new UiControlContext(ui).BeginTypography<UiRegularTypeface, UiFluentType300>();
                mirror.Prepare(ui, initial, width - 24, 200, typography.LineHeight, 0, styled: source,
                    activeSourceIndex: selection.End);
                var codeStart = initial.IndexOf('`') + 1;
                var codeLine = mirror.FindLine(codeStart);
                var codeFragments = mirror.SelectionFragments(codeStart, codeStart + 1, codeLine);
                codeSpace = codeFragments.Length == 1 && codeFragments[0].Width > 0
                    ? new(22 + codeFragments[0].Left + codeFragments[0].Width / 2, 20 + mirror.Y(codeLine) + mirror.LineHeight(codeLine) / 2)
                    : null;
                if (mode == "Table")
                {
                    var header = mirror.StyledTableRows.Single(entry => entry.Value.Source.Header).Key;
                    var fragments = mirror.SelectionFragments(mirror[header].Start, mirror[header].End, header);
                    headerSpace = fragments.Length == 1 && fragments[0].Width > 0
                        ? new(22 + fragments[0].Left + fragments[0].Width / 2, 20 + mirror.Y(header) + 3)
                        : null;
                }
                probes.Clear();
                for (var line = 0; line < mirror.Count; line++)
                {
                    var start = Math.Max(Math.Min(selection.Start, selection.End), mirror[line].Start);
                    var end = Math.Min(Math.Max(selection.Start, selection.End), mirror[line].End);
                    if (end <= start)
                        continue;
                    foreach (var fragment in mirror.SelectionFragments(start, end, line))
                    {
                        var left = MathF.Ceiling(22 + fragment.Left) + 1;
                        var right = MathF.Floor(22 + fragment.Left + fragment.Width) - 1;
                        var top = MathF.Ceiling(20 + mirror.Y(line)) + 3;
                        var bottom = MathF.Floor(20 + mirror.Y(line) + mirror.LineHeight(line)) - 3;
                        if (right > left && bottom > top)
                            probes.Add(new(left, top, right - left, bottom - top));
                    }
                }
            }
            byte[] Frame(UiInputState? input = null)
            {
                using var drawing = fixture.Build(Draw, input ?? idle);
                return fixture.Render(drawing);
            }
            byte[] Key(UiKey key, KeyModifiers modifiers = default) => Frame(idle with
            {
                KeyEvents = [new(key, true, modifiers), new(key, false, modifiers)], Modifiers = modifiers
            });
            bool EveryProbe(Func<uint, int, bool> predicate, byte[] pixels)
            {
                var count = 0;
                foreach (var rect in probes)
                    for (var y = (int)rect.Y; y < rect.Y + rect.Height; y++)
                        for (var x = (int)rect.X; x < rect.X + rect.Width; x++)
                        {
                            var offset = (y * (int)fixture.FrameInfo.DisplaySize.X + x) * 4;
                            if (!predicate(BitConverter.ToUInt32(pixels, offset), offset))
                                return false;
                            count++;
                        }
                return count > 0;
            }
            fixture.Context.SetTheme(palette.Apply(UiCompiledDesign.Windows11Dark.Theme));
            Frame();
            var node = tree.CaptureSnapshot().Elements.Single(e => e.Node.Name == "Styled selection");
            tree.RequestAction(node.Node.Id, node.RuntimeId, UiAccessibilityAction.Focus, wait: false);
            Frame();
            var parsed = editor.Document.ParseCount;
            var selected = Key(UiKey.A, KeyModifiers.Ctrl);
            check(fixture.Context.State.GetSelection(node.Node.Id) == new UiTextSelection(0, initial.Length)
                && text.Value == initial && editor.Document.ParseCount == parsed,
                $"Styled selection / {mode} / actual Ctrl+A selects the committed source without reparsing");
            var foreground = false;
            EveryProbe((pixel, _) => { foreground |= pixel == palette.HighlightText.Rgba; return true; }, selected);
            check(foreground, $"Styled selection / {mode} / selected styled glyphs use system HighlightText");
            fixture.Context.SetTheme((palette with { CanvasText = new(190, 30, 80), LinkText = new(170, 20, 210) }).Apply(UiCompiledDesign.Windows11Dark.Theme));
            var changed = Frame();
            check(EveryProbe((pixel, offset) => pixel == BitConverter.ToUInt32(changed, offset), selected),
                $"Styled selection / {mode} / selected antialiasing is independent of ordinary text and link colors");
            fixture.Context.SetTheme((palette with { HighlightText = palette.Highlight }).Apply(UiCompiledDesign.Windows11Dark.Theme));
            var flat = Frame();
            check(EveryProbe((pixel, _) => pixel == palette.Highlight.Rgba, flat),
                $"Styled selection / {mode} / equal highlight foreground and background erase every selected glyph and code background");
            Key(UiKey.Home, KeyModifiers.Ctrl);
            Key(UiKey.RightArrow, KeyModifiers.Shift);
            var partial = Key(UiKey.RightArrow, KeyModifiers.Shift);
            check(fixture.Context.State.GetSelection(node.Node.Id) == new UiTextSelection(0, 2)
                && EveryProbe((pixel, _) => pixel == palette.Highlight.Rgba, partial),
                $"Styled selection / {mode} / partial raw-marker selection uses its mapped physical range");
            var ordinary = UiCompiledDesign.Windows11Dark.Theme;
            fixture.Context.SetTheme(ordinary);
            var ordinarySelection = Key(UiKey.A, KeyModifiers.Ctrl);
            var ordinaryExpected = BitConverter.ToUInt32(ordinarySelection, (4 * (int)fixture.FrameInfo.DisplaySize.X + 4) * 4);
            var ordinaryActual = codeSpace is { } point ? BitConverter.ToUInt32(ordinarySelection,
                ((int)point.Y * (int)fixture.FrameInfo.DisplaySize.X + (int)point.X) * 4) : 0;
            check(codeSpace.HasValue && ordinaryActual == ordinaryExpected,
                $"Styled selection / {mode} / ordinary code background no longer covers the selection highlight"
                + $" / actual={ordinaryActual:X8}, independently blended reference={ordinaryExpected:X8}, source={ordinary.InputSelectionBg.Rgba:X8}");
            if (mode == "Table")
            {
                var headerExpected = BitConverter.ToUInt32(ordinarySelection, (4 * (int)fixture.FrameInfo.DisplaySize.X + 12) * 4);
                var headerActual = headerSpace is { } header ? BitConverter.ToUInt32(ordinarySelection,
                    ((int)header.Y * (int)fixture.FrameInfo.DisplaySize.X + (int)header.X) * 4) : 0;
                check(headerSpace.HasValue && headerActual == headerExpected,
                    $"Styled selection / table header / ordinary selection blends over the retained header background"
                    + $" / actual={headerActual:X8}, independently blended reference={headerExpected:X8}");
            }
        }
    }

}
