using System.Security.Cryptography;
using System.Text.Json;
using Duxel.Core;

internal static class DynamicPresenceGroupRenderingTests
{
    private readonly record struct Box(float Y, UiColor Color);
    internal static void Run(string output)
    {
        if (Directory.Exists(output))
            throw new IOException("Use a fresh dynamic presence GPU directory.");
        Directory.CreateDirectory(output);
        var pairs = new List<PresencePixelPair>();
        using var reference = new PresentationReuseTests.Fixture(1, false);
        using var candidate = new PresentationReuseTests.Fixture(1, true);
        foreach (var dark in new[] { false, true })
            foreach (var samples in new[] { 1, 4 })
            {
                reference.Renderer.SetMsaaSamples(samples);
                candidate.Renderer.SetMsaaSamples(samples);
                var background = dark ? new UiColor(11, 33, 71) : new UiColor(240, 245, 250);
                var red = new UiColor(200, 100, 50);
                var green = new UiColor(50, 180, 100);
                var blue = new UiColor(50, 100, 210);
                var violet = new UiColor(120, 60, 210);
                using var a = new Block(red);
                using var b = new Block(green);
                using var c = new Block(blue);
                using var d = new Block(violet);
                using var group = new UiPresenceGroupWidget([a, b, c], new(Gap: 10, AnimateEnter: true), keys: ["a", "b", "c"]);
                void Begin(UiImmediateContext ui)
                {
                    ui.EnableRootViewportContentLayout(contentPadding: 0);
                    new UiControlContext(ui).Frame(new(0, 0, 320, 240), background, default, 0, 0);
                    ui.SetCursorScreenPos(new(20, 20));
                }
                byte[] Actual() => candidate.Capture(ui => { Begin(ui); group.Render(ui); });
                void Pair(byte[] actual, string phase, params Box[] boxes)
                {
                    var expected = reference.Capture(ui =>
                    {
                        Begin(ui);
                        var paint = new UiControlContext(ui);
                        using var clipped = paint.Clip(new(20, 20, 80, 200));
                        foreach (var box in boxes)
                            paint.Frame(new(20, box.Y, 80, 20), box.Color, default, 0, 0);
                    });
                    var difference = 0;
                    for (var i = 0; i < actual.Length; i++)
                        difference = Math.Max(difference, Math.Abs(actual[i] - expected[i]));
                    var name = $"{dark}-{samples}-{phase}";
                    var equal = difference <= 2;
                    pairs.Add(new(name, Hash(expected), Hash(actual), difference, equal));
                    if (!equal)
                    {
                        File.WriteAllBytes(Path.Combine(output, name + ".reference.rgba"), expected);
                        File.WriteAllBytes(Path.Combine(output, name + ".actual.rgba"), actual);
                    }
                }
                Pair(Actual(), "initial", new(20, red), new(50, green), new(80, blue));
                group.SetItems([new("c", c), new("a", a), new("b", b)]);
                double? start = null;
                for (var frame = 0; frame < 17; frame++)
                {
                    var actual = Actual();
                    var now = candidate.Context.State.TimeSeconds;
                    start ??= now;
                    var eased = Ease(now - start.Value);
                    Pair(actual, "reorder-" + frame, new(80 - 60 * eased, blue), new(20 + 30 * eased, red), new(50 + 30 * eased, green));
                }
                group.SetItems([new("c", c), new("b", b)]);
                start = null;
                double? reflowStart = null;
                var priorPaints = a.Paints;
                for (var frame = 0; frame < 34; frame++)
                {
                    var actual = Actual();
                    var now = candidate.Context.State.TimeSeconds;
                    start ??= now;
                    var elapsed = now - start.Value;
                    var progress = (float)Math.Clamp(elapsed / .25, 0, 1);
                    if (elapsed >= .25)
                        reflowStart ??= now;
                    var y = reflowStart is { } started ? 80 - 30 * Ease(now - started) : 80;
                    Pair(actual, "remove-" + frame, new(20, blue),
                        new(50, new UiColor(200, 100, 50, (byte)MathF.Round(255 * (1 - progress)))), new(y, green));
                }
                if (a.Disposals != 1 || a.Paints != priorPaints)
                    throw new InvalidOperationException("GPU collection removal did not retain paint-only ownership until one final disposal.");
                group.SetItems([new("d", d), new("c", c), new("b", b)]);
                start = null;
                for (var frame = 0; frame < 17; frame++)
                {
                    var actual = Actual();
                    var now = candidate.Context.State.TimeSeconds;
                    start ??= now;
                    var elapsed = now - start.Value;
                    var progress = (float)Math.Clamp(elapsed / .25, 0, 1);
                    var eased = Ease(elapsed);
                    Pair(actual, "insert-" + frame,
                        new(20 * progress, new UiColor(120, 60, 210, (byte)MathF.Round(255 * progress))),
                        new(20 + 30 * eased, blue), new(50 + 30 * eased, green));
                }
                group.Options = group.Options with
                {
                    ReducedMotion = true
                };
                group.SetItems([]);
                Pair(Actual(), "remove-all");
                Pair(Actual(), "empty-idle");
                if (b.Disposals != 1 || c.Disposals != 1 || d.Disposals != 1)
                    throw new InvalidOperationException("The empty GPU collection retained removed widget lifetimes.");
            }
        var result = new PresencePixelResult(pairs.All(x => x.Equal), pairs.Count, pairs.ToArray(),
            "Independent analytic keyed reorder/removal/reflow/insertion rectangles. Tolerance2RGBA fixed before execution for8bit reference alpha. Not browser identity.");
        File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(result, PresencePixelJson.Default.PresencePixelResult));
        if (!result.Completed)
            throw new InvalidOperationException("Dynamic presence pixels differ from the independent geometry and alpha oracle.");
    }
    private static float Ease(double elapsed) => 1 - MathF.Pow(1 - (float)Math.Clamp(elapsed / .25, 0, 1), 3);
    private static string Hash(byte[] pixels) => Convert.ToHexString(SHA256.HashData(pixels));
    private sealed class Block(UiColor color) : UiControlWidget
    {
        internal int Paints;
        internal int Disposals;
        protected override bool CanRetainMeasurement => true;
        protected override UiVector2 MeasureContent(UiControlContext context) => context.LayoutConstraints.Constrain(new(80, 20));
        protected override void RenderControl(UiImmediateContext ui)
        {
            Paints++;
            var origin = ui.AllocateLayoutBox(new(80, 20));
            new UiControlContext(ui).Frame(new(origin.X, origin.Y, 80, 20), color, default, 0, 0);
            ui.FinishLayoutBox(origin, new(80, 20));
        }
        protected override void OnDispose()
        {
            Disposals++;
            base.OnDispose();
        }
    }
}
