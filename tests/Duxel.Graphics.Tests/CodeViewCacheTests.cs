using Duxel.Core;

internal static class CodeViewCacheTests
{
    internal static void Run(Action<bool, string> check)
    {
        CodeHighlightTests.Run(check);
        ParagraphRasterKeyTests.Run(check);
        VerifyVisibleWorkingSet(check);
        VerifyClippedAccessibilityGeometry(check);
        VerifyScrollbarDragging(check);
        VerifySharedLineLayouts(check);
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var code = new UiCodeView("using Duxel.Core;\n" + string.Join("\n", Enumerable.Repeat("var message = \"한글 and English\"; // comment", 30)));
        var input = new UiInputState(default, false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame(Action<UiImmediateContext> draw, float scale = 1)
        {
            context.NewFrame(new(.016f, new(640, 480), new(1, 1)), input, new(0, 0, 640, 480), UiTextSettings.Default with { Scale = scale });
            context.Render(new Screen(draw));
            using var drawing = context.GetDrawData();
        }
        void Measure(UiImmediateContext ui, float width) => code.MeasureConstrained(ui, new(0, width, 0, float.PositiveInfinity));
        Frame(ui => { Measure(ui, 420); Measure(ui, 404); });
        var initial = code.Metrics;
        check(initial.MeasurementBuilds == 2 && initial.SyntaxParses == 1,
            "code measurement prepares exactly two width variants with one syntax parse");
        for (var frame = 0; frame < 5; frame++) Frame(ui => { Measure(ui, 420); Measure(ui, 404); });
        check(code.Metrics == initial, "alternating scrollbar probe and committed widths reuse both source layouts across frames");
        Frame(ui => { Measure(ui, 500); Measure(ui, 404); });
        check(code.Metrics.MeasurementBuilds == initial.MeasurementBuilds + 1,
            "a third width replaces one bounded layout slot while keeping the other hot width");
        code.Code += "\n// revised";
        var beforeSource = code.Metrics;
        Frame(ui => { Measure(ui, 500); Measure(ui, 404); });
        check(code.Metrics.MeasurementBuilds == beforeSource.MeasurementBuilds + 2 && code.Metrics.SyntaxParses == 2,
            "source edits invalidate both layout variants and parse changed syntax once");
        var prepared = new UiVisualTextLines();
        Frame(ui =>
        {
            prepared.Prepare(ui, code.Code, 400, float.MaxValue, 20, 0, true, styled: code.Document);
            var builds = prepared.Builds;
            var contentHeight = prepared.Height;
            prepared.Prepare(ui, code.Code, 400, 80, 20, 0, true, styled: code.Document);
            check(prepared.Builds == builds && prepared.Height == contentHeight && prepared.EffectiveHeight == 80,
                "code geometry survives a measurement-to-paint height change with the same wrapping width");
            prepared.Prepare(ui, code.Code, 300, 80, 20, 0, true, styled: code.Document);
            check(prepared.Builds == builds + 1, "changed code wrapping width still rebuilds native geometry");
        });
        Frame(ui =>
        {
            var cache = new UiCodeParagraphLayouts();
            var source = "short\n" + new string('W', 120) + "\nlast";
            var fonts = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
            PlatformTextParagraphStyle[] styles = [new(0, source.Length, Path.Combine(fonts, "consola.ttf"), null, 14)];
            var key = ui.LayoutMeasurementKey;
            var first = cache.Prepare(Duxel.Platform.Windows.WindowsPlatformTextBackend.Instance, source, styles, 300, false, key);
            var builds = cache.NativeBuilds;
            check(builds == 4 && first.Lines.Span[^1].Start + first.Lines.Span[^1].Length == source.Length,
                "code hard-line layout covers the original source with three natural shapes and one wrapped shape");
            cache.Prepare(Duxel.Platform.Windows.WindowsPlatformTextBackend.Instance, source, styles, 280, false, key);
            check(cache.NativeBuilds == builds + 1, "resizing code reshapes only the long line that needs wrapping");
            cache.Prepare(Duxel.Platform.Windows.WindowsPlatformTextBackend.Instance, source, styles, 280, false, key);
            check(cache.NativeBuilds == builds + 1, "repeated code width reuses every native hard-line shape");

            var backend = Duxel.Platform.Windows.WindowsPlatformTextBackend.Instance;
            const int insertion = 6;
            const string inserted = "var value = 10;\n";
            var revised = source.Insert(insertion, inserted);
            var suffix = insertion + inserted.Length;
            PlatformTextParagraphStyle[] revisedStyles =
            [
                styles[0] with { Length = insertion },
                styles[0] with { Start = insertion, Length = inserted.Length },
                styles[0] with { Start = suffix, Length = source.Length - insertion }
            ];
            var reused = cache.Prepare(backend, revised, revisedStyles, 280, false, key);
            check(cache.NativeBuilds == builds + 2,
                "inserting a source line reshapes only that line and retains the shifted wrapped suffix");
            var fresh = new UiCodeParagraphLayouts().Prepare(backend, revised, revisedStyles, 280, false, key);
            check(reused.Width == fresh.Width && reused.Height == fresh.Height
                && reused.Carets.Span.SequenceEqual(fresh.Carets.Span)
                && reused.Runs.Length == fresh.Runs.Length
                && reused.Runs.ToArray().Zip(fresh.Runs.ToArray()).All(pair =>
                    pair.First.StyleIndex == pair.Second.StyleIndex && pair.First.Origin == pair.Second.Origin
                    && pair.First.Glyphs.Span.SequenceEqual(pair.Second.Glyphs.Span)),
                "reused code layouts retain fresh source positions, caret geometry and remapped paint styles");

            cache.Prepare(Duxel.Platform.Windows.WindowsPlatformTextBackend.Instance, source, styles, 280, false, key with { BaseFontSize = 18 });
            check(cache.NativeBuilds == builds + 6, "font resource changes invalidate the native hard-line snapshots");
        });
        var beforeScale = code.Metrics;
        Frame(ui => { Measure(ui, 500); Measure(ui, 404); }, 1.25f);
        check(code.Metrics.MeasurementBuilds == beforeScale.MeasurementBuilds + 2 && code.Metrics.SyntaxParses == 2,
            "font scaling invalidates both width variants without reparsing source syntax");

        var original = new UiAccessibilityTextLayout(new("a"), default, new(20, 30), new(20, 30, 100, 80),
            14, 1, [new(20, 30, 8, 14)], [new(20, 30, 0, 14), new(28, 30, 0, 14)], [new(0, 1, 1)]);
        var scrolled = original.WithViewport(new(20, 12), new(20, 12, 100, 80))
            ?? throw new InvalidOperationException("An untransformed layout must support viewport movement.");
        var originalCharacter = original.GetCharacterExtents(0);
        var scrolledCharacter = scrolled.GetCharacterExtents(0);
        var originalCaret = original.GetRangeExtents(0, 0);
        check(scrolledCharacter == originalCharacter with { Y = originalCharacter.Y - 18 }
            && scrolled.GetRangeExtents(0, 0) == originalCaret with { Y = originalCaret.Y - 18 }
            && scrolled.GetOffsetAtPoint(scrolledCharacter.X + scrolledCharacter.Width / 2,
                scrolledCharacter.Y + scrolledCharacter.Height / 2) == 0
            && scrolled.GetBoundedRanges(scrolledCharacter).Any(range => range.Start == 0)
            && original.GetCharacterExtents(0) == originalCharacter,
            "moving a code view updates accessibility ranges, carets and hit testing while preserving the prior snapshot");
    }
    internal static void VerifySharedLineLayouts(Action<bool, string> check)
    {
        var fonts = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        var backend = Duxel.Platform.Windows.WindowsPlatformTextBackend.Instance;
        var line = "var mix = \"한글 + English #42\"; // " + new string('x', 70);
        var source = string.Join("\n", line, line);
        var style = new PlatformTextParagraphStyle(0, line.Length, Path.Combine(fonts, "consola.ttf"),
            Path.Combine(fonts, "malgun.ttf"), 14);
        PlatformTextParagraphStyle[] styles = [style with { Length = line.Length + 1 }, style with { Start = line.Length + 1 }];
        var cache = new UiCodeLineLayoutCache();
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        var input = new UiInputState(default, false, false, false, false, false, false, 0, 0, [], [], default, default);
        context.NewFrame(new(.016f, new(640, 480), new(1, 1)), input, new(0, 0, 640, 480), UiTextSettings.Default);
        context.Render(new Screen(ui =>
        {
            var key = ui.LayoutMeasurementKey;
            var first = new UiCodeParagraphLayouts();
            first.Prepare(backend, line, [style], 220, false, key, cache);
            var second = new UiCodeParagraphLayouts();
            var reused = second.Prepare(backend, source, styles, 220, false, key, cache);
            var fresh = new UiCodeParagraphLayouts().Prepare(backend, source, styles, 220, false, key);
            check(first.NativeBuilds == 2 && second.NativeBuilds == 0,
                "both wrapped and natural mixed-language lines are shared across documents");
            check(reused.Width == fresh.Width && reused.Height == fresh.Height
                && reused.Carets.Span.SequenceEqual(fresh.Carets.Span) && reused.Runs.Length == fresh.Runs.Length
                && reused.Runs.ToArray().Zip(fresh.Runs.ToArray()).All(pair =>
                    pair.First.StyleIndex == pair.Second.StyleIndex && pair.First.Origin == pair.Second.Origin
                    && pair.First.Glyphs.Span.SequenceEqual(pair.Second.Glyphs.Span)),
                "shared native lines preserve source offsets, paint slots, glyph placement and caret geometry");
            second.Prepare(backend, source, styles, 200, false, key, cache);
            check(second.NativeBuilds == 1, "a new width shapes the shared wrapped line once");
            second.Prepare(backend, source, styles, 200, false, key with { BaseFontSize = 18 }, cache);
            check(second.NativeBuilds == 3, "changing layout resources invalidates shared natural and wrapped lines");
        }));
        using var drawing = context.GetDrawData();
    }

    internal static void VerifyScrollbarDragging(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        using var tree = new UiAccessibilityTree();
        ControlFontFixture.Configure(context);
        context.SetAccessibilityTree(tree);
        var source = string.Join("\n", Enumerable.Repeat("var value = " + new string('W', 100) + ";", 35));
        using var sized = Dux.ConstrainedBox(new UiCodeView(source, new(Wrap: false, ShowToolbar: false)),
            new(MinWidth: 410, MaxWidth: 410, MinHeight: 210, MaxHeight: 210));
        var idle = new UiInputState(new(-10, -10), false, false, false, false, false, false, 0, 0, [], [], default, default);
        var thickness = context.GetStyleData().ScrollbarSize;

        UiAccessibilityNode Frame(UiInputState input)
        {
            context.NewFrame(new(.016f, new(640, 480), new(1, 1)), input, new(0, 0, 640, 480), UiTextSettings.Default);
            context.Render(new Screen(ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding: 0);
                ui.RenderNaturalHeight(sized);
            }));
            using var drawing = context.GetDrawData();
            return tree.CaptureSnapshot().Elements.Single(element => element.Node.Role == UiAccessibilityRole.Edit).Node;
        }

        foreach (var horizontal in new[] { true, false })
        {
            var bounds = Frame(idle).Bounds;
            var text = new UiVector2(bounds.X + 80, bounds.Y + 32);
            Frame(idle with { MousePosition = text, LeftMouseDown = true, LeftMousePressed = true });
            var before = Frame(idle with { MousePosition = text, LeftMouseReleased = true });
            var point = ThumbCenter(before);
            var held = idle with { MousePosition = point, LeftMouseDown = true };
            Frame(held with { LeftMousePressed = true });
            var scrollbar = context.State.PreciseScrollbarDrag?.Id;
            var moved = horizontal ? point with { X = point.X + 48 } : point with { Y = point.Y + 36 };
            Frame(held with { MousePosition = moved });
            Frame(idle with { MousePosition = moved, LeftMouseReleased = true });
            var after = Frame(idle);
            var offset = after.Scroll!.Value.Offset;
            check(scrollbar is not null && (horizontal ? offset.X : offset.Y) > 0,
                $"code view {(horizontal ? "horizontal" : "vertical")} thumb captures after text focus and scrolls");
            var regrab = ThumbCenter(after);
            Frame(held with { MousePosition = regrab, LeftMousePressed = true });
            check(scrollbar is not null && context.State.PreciseScrollbarDrag?.Id == scrollbar,
                $"code view {(horizontal ? "horizontal" : "vertical")} thumb can be grabbed again at its new position");
            Frame(idle with { MousePosition = regrab, LeftMouseReleased = true });

            UiVector2 ThumbCenter(UiAccessibilityNode node)
            {
                var range = node.Scroll ?? throw new InvalidOperationException("The bounded code view must publish both scroll ranges.");
                var track = (horizontal ? node.Bounds.Width : node.Bounds.Height) - thickness;
                var content = horizontal ? range.Content.X : range.Content.Y;
                var offset = horizontal ? range.Offset.X : range.Offset.Y;
                var maximum = horizontal ? range.HorizontalMaximum : range.VerticalMaximum;
                var thumb = MathF.Min(track, MathF.Max(horizontal ? 24 : 16, track * (track / content)));
                var center = (float)(offset / maximum) * (track - thumb) + thumb / 2;
                return horizontal
                    ? new(node.Bounds.X + center, node.Bounds.Y + node.Bounds.Height - thickness / 2)
                    : new(node.Bounds.X + node.Bounds.Width - thickness / 2, node.Bounds.Y + center);
            }
        }
    }

    private static void VerifyClippedAccessibilityGeometry(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        using var tree = new UiAccessibilityTree();
        using var code = new UiCodeView("var label = \"한글 abc\";\nvar amount = 42;");
        ControlFontFixture.Configure(context);
        context.SetAccessibilityTree(tree);
        var input = new UiInputState(default, false, false, false, false, false, false, 0, 0, [], [], default, default);

        UiAccessibilityNode? Frame(bool clipped = false, bool shown = true)
        {
            context.NewFrame(new(.016f, new(640, 480), new(1, 1)), input, new(0, 0, 640, 480), UiTextSettings.Default);
            context.Render(new Screen(ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding: 0);
                if (!shown)
                    return;
                if (clipped)
                    ui.PushClipRect(new(900, 900, 1, 1), true);
                code.Render(ui);
                if (clipped)
                    ui.PopClipRect();
            }));
            using var drawing = context.GetDrawData();
            return tree.CaptureSnapshot().Elements
                .Where(element => element.Node.Name == "C# 예제 소스")
                .Select(element => (UiAccessibilityNode?)element.Node).SingleOrDefault();
        }

        var original = Frame()?.TextLayout ?? throw new InvalidOperationException("Visible code must publish text geometry.");
        var hidden = Frame(clipped: true);
        Frame(clipped: true);
        check(hidden is { Offscreen: true, TextLayout: null }, "fully clipped code publishes no accessible text geometry");
        check(ReferenceEquals(Frame()?.TextLayout, original), "code returning to the same viewport shares its pre-clip geometry");
        Frame(clipped: true);
        Frame(shown: false);
        check(Frame()?.TextLayout is { } restored && !ReferenceEquals(restored, original),
            "removing the code view retires its clipped geometry");
        Frame(clipped: true);
        code.Code += "\n// revised";
        check(Frame()?.TextLayout?.Content.Text == code.Code, "changed code discards clipped geometry from the previous source");
    }

    internal static void VerifyVisibleWorkingSet(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        var labels = Enumerable.Range(0, 600).Select(i => "Visible text " + i).ToArray();
        var input = new UiInputState(default, false, false, false, false, false, false, 0, 0, [], [], default, default);
        UiDrawData Frame(bool visible)
        {
            context.NewFrame(new(.016f, new(640, 480), new(1, 1)), input, new(0, 0, 640, 480), UiTextSettings.Default);
            context.Render(new Screen(ui =>
            {
                if (!visible) return;
                foreach (var label in labels)
                {
                    ui.SetCursorScreenPos(new(20, 20));
                    ui.Text(label);
                }
            }));
            return context.GetDrawData();
        }
        using (var warm = Frame(true)) { }
        for (var i = 0; i < 4; i++)
        {
            using var next = Frame(true);
            check(!next.TextureUpdates.Any(update => update.Kind is UiTextureUpdateKind.Create or UiTextureUpdateKind.Destroy),
                "600 ordinary visible text entries survive the cold-cache limit without GPU texture churn, frame " + i);
        }
        using (var hidden = Frame(false)) { }
        using (var trim = Frame(false))
            check(trim.TextureUpdates.Any(update => update.Kind == UiTextureUpdateKind.Destroy),
                "hidden ordinary text becomes eligible for the bounded cold cache after one frame");
    }
    private sealed class Screen(Action<UiImmediateContext> draw) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => draw(ui);
    }
}
