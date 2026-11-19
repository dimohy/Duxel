using Duxel.Core;

internal static class FluentTooltipRenderingTests
{
    internal static void Run(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool, string> check)
    {
        foreach (var arrow in new[] { false, true })
        {
            var options = new UiTooltipOptions { Positioning = new() { ArrowHeight = arrow ? 6 : 0 } };
            using var factory = Fluent.Tooltip(Fluent.Button("Target"), "Factory tooltip", options);
            using var reference = new UiTooltip<UiHoverFocusTooltipBehavior, UiFluentTooltipPresentation>(
                Fluent.Button("Target"), "Factory tooltip", options);
            factory.Visible = true;
            reference.Visible = true;
            void Render(UiImmediateContext ui, UiWidget widget)
            {
                ui.EnableRootViewportContentLayout(contentPadding : 0);
                ui.SetCursorScreenPos(new(80, 140));
                widget.Render(ui);
            }
            var expected = capture("fluent-tooltip-reference-" + arrow, ui => Render(ui, reference));
            for (var frame = 0; frame < 3; frame++)
            {
                var actual = capture($"fluent-tooltip-factory-{arrow}-{frame}", ui => Render(ui, factory));
                check(actual.AsSpan().SequenceEqual(expected),
                    $"Fluent Tooltip generated factory matches the established closed generic presentation : arrow = {arrow}, frame = {frame}");
            }
        }
    }
}
