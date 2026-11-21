using Duxel.Core;

internal static class BreadcrumbTypographyRenderingTests
{
    internal static void Run(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool, string> check,
        UiFluentColorScheme scheme)
    {
        foreach (var (size, height, padding) in new[] { (UiBreadcrumbSize.Small, 24, 6), (UiBreadcrumbSize.Medium, 32, 6), (UiBreadcrumbSize.Large, 40, 8) })
        foreach (var text in new[] { "Team En", "정세일 한글" })
        foreach (var current in new[] { false, true })
        foreach (var rtl in new[] { false, true })
        foreach (var hovered in new[] { false, true })
        {
            var dark = scheme == UiFluentColorScheme.Dark;
            var color = hovered && !current ? dark ? new UiColor(255, 255, 255) : new(36, 36, 36)
                : dark ? new UiColor(214, 214, 214) : new(66, 66, 66);
            using UiWidget reference = (size, current) switch
            {
                (UiBreadcrumbSize.Small, false) => Fluent.Caption1(text, color),
                (UiBreadcrumbSize.Small, true) => Fluent.Caption1Strong(text, color),
                (UiBreadcrumbSize.Medium, false) => Fluent.Body1(text, color),
                (UiBreadcrumbSize.Medium, true) => Fluent.Body1Strong(text, color),
                (UiBreadcrumbSize.Large, false) => Fluent.Body2(text, color),
                _ => Fluent.Subtitle2(text, color)
            };
            var options = new UiBreadcrumbOptions(Size: size, RightToLeft: rtl);
            var item = new UiBreadcrumbItemContent(text, default, current);
            var bounds = new UiRect(20, 80, 280, height);
            void Draw(UiImmediateContext ui, int mode)
            {
                ui.EnableRootViewportContentLayout(contentPadding: 0);
                var context = new UiControlContext(ui);
                context.Frame(new(0, 0, 320, 240), dark ? new(32, 32, 32) : new(255, 255, 255), default, 0, 0);
                using var clip = context.Clip(bounds);
                if (mode == 0)
                {
                    UiFluentBreadcrumbPresentation.PaintItem(context, item, bounds, new(false, hovered, false), options);
                    return;
                }
                if (hovered && !current)
                    context.Frame(bounds, dark ? new(56, 56, 56) : new(245, 245, 245), default, 4, 0);
                var measured = reference.Measure(ui, new(280, height));
                var x = rtl ? bounds.X + bounds.Width - padding - measured.X : bounds.X + padding;
                var y = bounds.Y + (height - measured.Y) / 2;
                if (mode == 1)
                {
                    // Honor the same outer clip without clipping the glyph ink to its advance rectangle.
                    ui.SetCursorScreenPos(new(x, y));
                    reference.Render(ui);
                    return;
                }
                // Reproduce the retired legacy text origin only as a discriminating regression fixture.
                using var typography = context.BeginTypography<UiRegularTypeface, UiFluentType300>();
                var legacy = context.MeasureText(text);
                context.Text(text, new(x, bounds.Y + (height - legacy.Y) / 2), color);
            }
            var key = $"text-center-{size}-{text}-{current}-{rtl}-{hovered}";
            var actual = capture(key, ui => Draw(ui, 0));
            var expected = capture(key + "-reference", ui => Draw(ui, 1));
            check(actual.AsSpan().SequenceEqual(expected),
                "Breadcrumb text matches an independently centered Fluent typography widget : " + key);
            if (size == UiBreadcrumbSize.Medium && text == "정세일 한글" && !current && !rtl && !hovered)
                check(!actual.AsSpan().SequenceEqual(capture(key + "-legacy-reproduction", ui => Draw(ui, 2))),
                    "Breadcrumb Korean centering regression distinguishes the previous legacy text origin");
        }
    }
}
