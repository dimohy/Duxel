using System.Numerics;
using Duxel.Core;

internal static class LinkAllocationRenderingTests
{
    private static readonly Uri Destination = new("/guide", UriKind.Relative);
    private static readonly Action<Uri> Navigate = static _ => { };

    internal static void Run(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool, string> check, bool vector)
    {
        try
        {
            Compare<UiFluentLinkPresentation<UiFluentDefaultLinkAppearance, UiStandaloneLinkUnderline>>("default", capture, check, vector);
            Compare<UiFluentLinkPresentation<UiFluentSubtleLinkAppearance, UiInlineLinkUnderline>>("subtle-inline", capture, check, vector);
            Compare<UiFluentLinkPresentation<UiFluentInvertedLinkAppearance, UiStandaloneLinkUnderline>>("inverted", capture, check, vector);
            Compare<UiFluentLinkPresentation<UiFluentOnBrandLinkAppearance, UiInlineLinkUnderline>>("brand-inline", capture, check, vector);
            using var custom = Dux.SizedBox(new GeneratedAllocatedLink("User", Destination, Navigate), 80, 64);
            var actual = capture("link-custom-" + vector, ui => Scene(ui, vector, "custom", () => custom.Render(ui)));
            var expected = capture("link-custom-reference-" + vector, ui => Scene(ui, vector, "custom-reference", () =>
            {
                var context = new UiControlContext(ui);
                context.Frame(new(20, 20, 80, 64), context.Theme.Button, context.Theme.Border, 0, 1);
            }));
            check(actual.AsSpan().SequenceEqual(expected), "generated custom Link paints its actual parent root, vector = " + vector);
        }
        finally
        {
            AppearancePaintProbe.Interaction = default;
        }
    }

    private static void Compare<TPresentation>(string name, Func<string, Action<UiImmediateContext>, byte[]> capture,
        Action<bool, string> check, bool vector) where TPresentation : IUiLinkPresentation
    {
        var index = 0;
        using var reference = new UiLinkContent<TPresentation>();
        foreach (var content in new[] { "Read the guide##Hidden identity", "Several words wrap at the assigned width.\nSecond line", string.Empty })
        foreach (var size in new[] { new UiVector2(80, 64), new UiVector2(160, 8) })
        {
            using var link = new UiLink<AppearancePaintProbe, TPresentation>(content, Destination, Navigate, 220);
            using var box = Dux.SizedBox(link, size.X, size.Y);
            var state = index % 4;
            AppearancePaintProbe.Interaction = new(false, state == 1, false) { FocusVisible = state == 2, Focused = state == 2 };
            var key = $"link-allocated-{name}-{vector}-{index++}";
            void Draw(UiImmediateContext ui, bool actual)
            {
                ui.BeginDisabled(state == 3);
                try
                {
                    if (actual)
                        box.Render(ui);
                    else
                        Reference(ui, content, reference, new(20, 20, size.X, size.Y));
                }
                finally
                {
                    ui.EndDisabled();
                }
            }
            var actual = capture(key, ui => Scene(ui, vector, key, () => Draw(ui, true)));
            var expected = capture(key + "-reference", ui => Scene(ui, vector, key + "-reference", () => Draw(ui, false)));
            check(actual.AsSpan().SequenceEqual(expected), key + " matches legacy glyph baselines/underlines at assigned wrapping width with independent root clipping");
        }
        using var zero = Dux.SizedBox(new UiLink<AppearancePaintProbe, TPresentation>("Clipped", Destination, Navigate), 0, 0);
        var zeroPixels = capture($"link-zero-{name}-{vector}", ui => Scene(ui, vector, name + "-zero", () => zero.Render(ui)));
        var emptyPixels = capture($"link-zero-{name}-{vector}-reference", ui => Scene(ui, vector, name + "-empty", () => { }));
        check(zeroPixels.AsSpan().SequenceEqual(emptyPixels), name + " paints no Link text or underline at zero allocation, vector = " + vector);
    }

    internal static void VerifyRetained(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool, string> check, bool vector)
    {
        AppearancePaintProbe.Interaction = new(false, false, false) { FocusVisible = true, Focused = true };
        using var first = new UiLink<AppearancePaintProbe, UiFluentLinkPresentation<UiFluentDefaultLinkAppearance, UiStandaloneLinkUnderline>>("FPS : 0", Destination, Navigate);
        using var second = new UiLink<AppearancePaintProbe, UiFluentLinkPresentation<UiFluentDefaultLinkAppearance, UiStandaloneLinkUnderline>>("NF : 0", Destination, Navigate);
        using var firstBox = Dux.SizedBox(first, 100, 64);
        using var secondBox = Dux.SizedBox(second, 100, 64);
        using var firstReference = new UiLinkContent<UiFluentLinkPresentation<UiFluentDefaultLinkAppearance, UiStandaloneLinkUnderline>>();
        using var secondReference = new UiLinkContent<UiFluentLinkPresentation<UiFluentDefaultLinkAppearance, UiStandaloneLinkUnderline>>();
        long version = 0;
        var builds = 0;
        var expectedBuilds = 0;
        try
        {
            for (var frame = 0; frame < 20; frame++)
            {
                var changed = false;
                if (frame % 16 == 0)
                {
                    first.Label = "FPS : " + frame / 16;
                    changed = true;
                }
                if (frame % 3 == 0)
                {
                    second.Label = "NF : " + frame / 3;
                    changed = true;
                }
                if (frame == 8)
                {
                    firstBox.Options = firstBox.Options with { Width = 24 };
                    changed = true;
                }
                if (frame == 12)
                {
                    firstBox.Options = firstBox.Options with { Height = 8 };
                    changed = true;
                }
                if (changed)
                {
                    version++;
                    expectedBuilds++;
                }
                var options = new UiLayerOptions(true, 1, default) { ContentVersion = version };
                if (vector)
                    options = options with { Scene3D = new(Matrix4x4.Identity, false) };
                var actual = capture($"link-retained-{vector}-{frame}", ui =>
                {
                    ui.EnableRootViewportContentLayout(contentPadding : 0);
                    if (ui.BeginLayer("allocated-link-retained-" + vector, options))
                    {
                        builds++;
                        ui.SetCursorScreenPos(new(20, 20));
                        firstBox.Render(ui);
                        ui.SetCursorScreenPos(new(140, 20));
                        secondBox.Render(ui);
                    }
                    ui.EndLayer();
                });
                var expected = capture($"link-retained-{vector}-{frame}-reference", ui => Scene(ui, vector, "retained-reference-" + frame, () =>
                {
                    Reference(ui, first.Label, firstReference, new(20, 20, firstBox.Options.Width!.Value, firstBox.Options.Height!.Value));
                    Reference(ui, second.Label, secondReference, new(140, 20, 100, 64));
                }));
                check(actual.AsSpan().SequenceEqual(expected), $"paced Link content and changed clips remain exact across acquired images; vector = {vector}, frame = {frame}");
            }
            check(builds == expectedBuilds, "unchanged allocated Link layers retain content, vector = " + vector);
        }
        finally
        {
            AppearancePaintProbe.Interaction = default;
        }
    }

    private static void Reference<TPresentation>(UiImmediateContext ui, string label, UiLinkContent<TPresentation> content, UiRect root)
        where TPresentation : IUiLinkPresentation
    {
        using var clip = new UiControlContext(ui).Clip(root);
        ui.SetCursorScreenPos(new(root.X, root.Y));
        ui.Link<AppearancePaintProbe, TPresentation>(label, Destination, Navigate, content, root.Width);
    }

    private static void Scene(UiImmediateContext ui, bool vector, string id, Action draw)
    {
        ui.EnableRootViewportContentLayout(contentPadding : 0);
        if (vector)
            ui.BeginLayer("link-allocation-vector-" + id, new(false, 1, default) { Scene3D = new(Matrix4x4.Identity, false) });
        try
        {
            ui.SetCursorScreenPos(new(20, 20));
            draw();
        }
        finally
        {
            if (vector)
                ui.EndLayer();
        }
    }
}

internal readonly struct BoxLinkPresentation : IUiLinkPresentation
{
    public static UiControlTypographyScope BeginText(UiControlContext context) => default;
    public static UiLinkLayout Measure(UiControlContext context, string text, float width) => new(new(24, 12), default);
    public static UiLinkLayout Arrange(UiControlContext context, in UiLinkLayout natural, UiVector2 allocatedSize) => natural with { Size = allocatedSize };
    public static void Paint(UiControlContext context, in UiLinkLayout layout, UiRect bounds, UiButtonInteraction interaction) =>
        context.Frame(bounds, context.Theme.Button, context.Theme.Border, 0, 1);
}
[UiWidgetStyle(typeof(UiLink<,>), typeof(LinkAllocationBehavior), typeof(BoxLinkPresentation))]
internal partial class GeneratedAllocatedLink;
