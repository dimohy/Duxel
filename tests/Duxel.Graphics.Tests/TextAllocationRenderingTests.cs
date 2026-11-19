using System.Numerics;
using Duxel.Core;

internal static class TextAllocationRenderingTests
{
    internal static void Run(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool, string> check, bool vector)
    {
        Compare<UiFluentTextPresentation>("fluent-wrapped", capture, check, vector);
        Compare<UiFluentTypographyPresentation<UiSemiboldTypeface, UiFluentType400>>("subtitle", capture, check, vector);
        Compare<UiContextTextPresentation>("context", capture, check, vector);
        Compare<UiContextWrappedTextPresentation>("context-wrapped", capture, check, vector);

        using var custom = Dux.SizedBox(new GeneratedAllocatedText("User"), 80, 64);
        var actual = capture("text-user-" + vector, ui => Scene(ui, vector, "user", () => custom.Render(ui)));
        var expected = capture("text-user-reference-" + vector, ui => Scene(ui, vector, "user-reference", () =>
        {
            var context = new UiControlContext(ui);
            context.Frame(new(20, 20, 80, 64), context.Theme.Button, context.Theme.Border, 0, 1);
        }));
        check(actual.AsSpan().SequenceEqual(expected), "generated user text paints the assigned root with exact pixels, vector = " + vector);
    }

    private static void Compare<TPresentation>(string name, Func<string, Action<UiImmediateContext>, byte[]> capture,
        Action<bool, string> check, bool vector) where TPresentation : IUiTextPresentation
    {
        var index = 0;
        foreach (var content in new[] { "Short ## literal\nSecond line", "Several words wrap at the actual parent width without changing the glyph size.", string.Empty })
        foreach (var size in new[] { new UiVector2(80, 64), new UiVector2(160, 16) })
        {
            using var text = new UiText<TPresentation>(content, identity : UiWidgetIdentity.Instance);
            using var box = Dux.SizedBox(text, size.X, size.Y);
            var key = $"text-allocated-{name}-{vector}-{index++}";
            var actual = capture(key, ui => Scene(ui, vector, key, () =>
            {
                ui.PushTextWrapPos(50);
                box.Render(ui);
                ui.PopTextWrapPos();
            }));
            var expected = capture(key + "-reference", ui => Scene(ui, vector, key + "-reference", () =>
                Reference<TPresentation>(ui, content, new(20, 20, size.X, size.Y))));
            check(actual.AsSpan().SequenceEqual(expected),
                key + " preserves glyph size, baseline, source content and assigned-width wrapping with independent clipping");
        }

        using var zero = Dux.SizedBox(new UiText<TPresentation>("Clipped"), 0, 0);
        var zeroPixels = capture($"text-zero-{name}-{vector}", ui => Scene(ui, vector, name + "-zero", () => zero.Render(ui)));
        var emptyPixels = capture($"text-zero-{name}-{vector}-reference", ui => Scene(ui, vector, name + "-empty", () => { }));
        check(zeroPixels.AsSpan().SequenceEqual(emptyPixels), name + " has no painted pixels at zero allocation, vector = " + vector);
    }

    internal static void VerifyRetained(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool, string> check, bool vector)
    {
        using var first = Fluent.Text("FPS : 0");
        using var second = Fluent.Text("NF : 0");
        using var firstBox = Dux.SizedBox(first, 80, 64);
        using var secondBox = Dux.SizedBox(second, 80, 64);
        long version = 0;
        var builds = 0;
        var expectedBuilds = 0;
        for (var frame = 0; frame < 20; frame++)
        {
            var changed = false;
            if (frame % 16 == 0)
            {
                first.Text = "FPS : " + frame / 16;
                changed = true;
            }
            if (frame % 3 == 0)
            {
                second.Text = "NF : " + frame / 3;
                changed = true;
            }
            if (frame == 8)
            {
                firstBox.Options = firstBox.Options with { Width = 24 };
                changed = true;
            }
            if (frame == 12)
            {
                firstBox.Options = firstBox.Options with { Height = 16 };
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
            var actual = capture($"text-retained-{vector}-{frame}", ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding : 0);
                if (ui.BeginLayer("allocated-text-retained-" + vector, options))
                {
                    builds++;
                    ui.SetCursorScreenPos(new(20, 20));
                    firstBox.Render(ui);
                    ui.SetCursorScreenPos(new(120, 20));
                    secondBox.Render(ui);
                }
                ui.EndLayer();
            });
            var expected = capture($"text-retained-{vector}-{frame}-reference", ui => Scene(ui, vector, "retained-reference-" + frame, () =>
            {
                Reference<UiFluentTextPresentation>(ui, first.Text, new(20, 20, firstBox.Options.Width!.Value, firstBox.Options.Height!.Value));
                Reference<UiFluentTextPresentation>(ui, second.Text, new(120, 20, 80, 64));
            }));
            check(actual.AsSpan().SequenceEqual(expected),
                $"independently paced text and changed root clips stay exact across retained frame images, vector = {vector}, frame = {frame}");
        }
        check(builds == expectedBuilds, "unchanged allocated text layers reuse content without rebuilding, vector = " + vector);
    }

    private static void Reference<TPresentation>(UiImmediateContext ui, string content, UiRect bounds) where TPresentation : IUiTextPresentation
    {
        using var clip = new UiControlContext(ui).Clip(bounds);
        ui.SetCursorScreenPos(new(bounds.X, bounds.Y));
        ui.PushTextWrapPos(bounds.X + bounds.Width);
        ui.Text<TPresentation>(content);
        ui.PopTextWrapPos();
    }

    private static void Scene(UiImmediateContext ui, bool vector, string id, Action draw)
    {
        ui.EnableRootViewportContentLayout(contentPadding : 0);
        if (vector)
            ui.BeginLayer("allocated-text-vector-" + id, new(false, 1, default) { Scene3D = new(Matrix4x4.Identity, false) });
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
