using Duxel.Core;

internal static class RatingTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        using var tree = new UiAccessibilityTree();
        ControlFontFixture.Configure(context);
        context.SetAccessibilityTree(tree);
        var value = 0f;
        var reads = 0;
        var writes = new List<float>();
        using var rating = Fluent.Rating("Experience", new UiBinding<float>(() => { reads++; return value; }, next => { value = next; writes.Add(next); }), new UiRatingOptions { Step = .5f });
        UiWidget current = rating;
        var disabled = false;
        var onlyMeasure = false;
        UiVector2 measured = default;
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        var frame = new UiFrameInfo(.016f, new(320, 240), new(1, 1));
        var screen = new Screen(ui =>
        {
            ui.EnableRootViewportContentLayout(contentPadding: 0);
            ui.SetCursorScreenPos(new(10, 10));
            ui.BeginDisabled(disabled);
            if (onlyMeasure)
                measured = rating.MeasureConstrained(ui, new(0, 56, 0, float.PositiveInfinity));
            else
                current.Render(ui);
            ui.EndDisabled();
        });
        void Frame(UiInputState? input = null)
        {
            context.NewFrame(frame, input ?? idle, new(0, 0, 320, 240), UiTextSettings.Default);
            context.Render(screen);
            using var drawing = context.GetDrawData();
        }
        UiInputState Pointer(float x, float y, bool press) => idle with
        {
            MousePosition = new(x, y),
            LeftMouseDown = press,
            LeftMousePressed = press,
            LeftMouseReleased = !press
        };
        void Click(float x, float y)
        {
            Frame(Pointer(x, y, true));
            Frame(Pointer(x, y, false));
        }
        void Key(UiKey key) => Frame(idle with
        {
            KeyEvents = [new(key, true, default)]
        });
        onlyMeasure = true;
        Frame();
        check(reads == 0 && measured == new UiVector2(56, 84), "Rating / constrained measurement wraps fixed icons without reading the value binding");
        onlyMeasure = false;
        Frame();
        var nodes = tree.CaptureSnapshot().Elements;
        check(nodes.Count(e => e.Node.Role == UiAccessibilityRole.RadioButton) == 10
            && nodes.Single(e => e.Node.Name == "Experience").Node is { Role: UiAccessibilityRole.RadioGroup, SelectionRequired: false },
            "Rating / half steps expose10 radio choices and an initially empty selection owner");
        var revision = tree.Revision;
        Frame();
        check(writes.Count == 0 && tree.Revision == revision, "Rating / idle frame neither writes the binding nor republishes accessibility changes");
        Frame(idle with
        {
            MousePosition = new(73, 20)
        });
        check(value == 0 && writes.Count == 0, "Rating / hover preview does not commit a value");
        Click(73, 20);
        check(value == 2.5f && writes.SequenceEqual(new[] { 2.5f }), "Rating / left half of third star selects2.5 once on release");
        Key(UiKey.RightArrow);
        check(value == 3, "Rating / shared radio keyboard increments by the selected half step");
        rating.Options = rating.Options with
        {
            RightToLeft = true
        };
        Frame();
        Key(UiKey.LeftArrow);
        check(value == 3.5f, "Rating / left arrow increases the rating in RTL visual order");
        Click(17, 20);
        check(value == 5, "Rating / leftmost RTL half segment selects the maximum");
        disabled = true;
        Click(143, 20);
        check(value == 5, "Rating / disabled pointer cannot change the controlled value");
        disabled = false;
        rating.Options = rating.Options with
        {
            RightToLeft = false
        };
        Frame();
        var target = tree.CaptureSnapshot().Elements.Single(e => e.Node.Role == UiAccessibilityRole.RadioButton && e.Node.Name == "4.5");
        tree.RequestAction(target.Node.Id, target.RuntimeId, UiAccessibilityAction.Select, wait: false);
        Frame();
        check(value == 4.5f && tree.CaptureSnapshot().Elements.Single(e => e.Node.Id == target.Node.Id).Node.Selected == true,
            "Rating / shared accessibility selection commits the typed value and selected node");
        var previous = rating.Options;
        var rejected = 0;
        foreach (var invalid in new[] { previous with { Step = .2f }, previous with { Maximum = 0 }, previous with { Size = (UiRatingSize)99 } })
            try
            {
                rating.Options = invalid;
            }
            catch (ArgumentOutOfRangeException) { rejected++; }
        check(rejected == 3 && rating.Options == previous, "Rating / invalid options fail before replacing current state");
        using var parent = Dux.Container(rating, new(Width: 56, Height: 84));
        current = parent;
        Frame();
        Click(17, 80);
        check(value == 4.5f, "Rating / wrapped third row retains its half-step pointer mapping");
        context.NewInputBatch(frame, [Pointer(45, 20, true), Pointer(45, 20, false), idle], new(0, 0, 320, 240), UiTextSettings.Default);
        context.Render(screen);
        using (context.GetDrawData())
            check(value == 1.5f && context.InputProcessingMetrics is { CompletedSteps: 3, BuiltDrawings: 1 },
                "Rating / ordered input preserves actual selection while publishing once");
        check(Fluent.ComponentNames.Contains("Rating") && !Fluent.MissingComponentNames.Contains("Rating"),
            "Rating / generated compiled factory participates in the official catalog");
        RunGpu(check);
    }

    private static void RunGpu(Action<bool, string> check)
    {
        foreach (var slots in new[] { 1, 3 })
        {
            using var reference = new PresentationReuseTests.Fixture(slots, false);
            using var candidate = new PresentationReuseTests.Fixture(slots, true);
            var value = 0f;
            using var rating = Fluent.Rating("GPU rating", new UiBinding<float>(() => value, _ => throw new InvalidOperationException("Idle rating must not write.")), new UiRatingOptions { Step = .5f });
            using var parent = Dux.Container(rating, new(Width: 140));
            foreach (var rtl in new[] { false, true })
                foreach (var width in new[] { 140, 56 })
                    foreach (var score in new[] { 0f, .5f, 2.5f, 5f })
                    {
                        value = score;
                        rating.Options = rating.Options with
                        {
                            RightToLeft = rtl
                        };
                        parent.Options = parent.Options with
                        {
                            Width = width
                        };
                        var columns = width == 56 ? 2 : 5;
                        var height = columns == 2 ? 84 : 28;
                        using var expectedData = reference.Build(ui =>
                        {
                            ui.EnableRootViewportContentLayout(contentPadding: 0);
                            var paint = new UiControlContext(ui);
                            using var clip = paint.Clip(new(25, 30, width, height));
                            for (var i = 0; i < 5; i++)
                            {
                                var x = 25 + (rtl ? width - (i % columns + 1) * 28 : i % columns * 28);
                                var icon = new UiRect(x, 30 + i / columns * 28, 28, 28);
                                var fill = Math.Clamp(score - i, 0, 1);
                                if (fill < 1)
                                {
                                    using var empty = paint.Clip(new(rtl ? icon.X : icon.X + fill * 28, icon.Y, (1 - fill) * 28, 28));
                                    UiRatingStarOutline.Paint(paint, icon, paint.Theme.Text, default);
                                }
                                if (fill > 0)
                                {
                                    using var selected = paint.Clip(new(rtl ? icon.X + (1 - fill) * 28 : icon.X, icon.Y, fill * 28, 28));
                                    UiRatingStarFilled.Paint(paint, icon, paint.Theme.Text, default);
                                }
                            }
                        });
                        var expected = reference.Render(expectedData);
                        using var actualData = candidate.Build(ui => { ui.EnableRootViewportContentLayout(contentPadding: 0); ui.SetCursorScreenPos(new(25, 30)); parent.Render(ui); });
                        var actual = candidate.Render(actualData);
                        check(actual.AsSpan().SequenceEqual(expected),
                            $"Rating GPU / slots={slots}, RTL={rtl}, width={width}, score={score} / clipped whole and half stars equal independent glyph placement");
                    }
        }
    }
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
