using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Duxel.Core;

internal static class PresenceGroupRenderingTests
{
    internal static void Run(string output)
    {
        if (Directory.Exists(output))
            throw new IOException("Use a fresh presence GPU directory.");
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
                using var first = new Block(new(200, 100, 50));
                using var second = new Block(new(50, 180, 100));
                using var group = new UiPresenceGroupWidget([first, second], new(Gap: 10));
                void Begin(UiImmediateContext ui)
                {
                    ui.EnableRootViewportContentLayout(contentPadding: 0);
                    new UiControlContext(ui).Frame(new(0, 0, 320, 240), background, default, 0, 0);
                    ui.SetCursorScreenPos(new(20, 20));
                }
                byte[] Actual() => candidate.Capture(ui => { Begin(ui); group.Render(ui); });
                void Pair(byte[] actual, string name, float alpha, float secondY)
                {
                    var expected = reference.Capture(ui =>
                    {
                        Begin(ui);
                        var paint = new UiControlContext(ui);
                        if (alpha > 0)
                            paint.Frame(new(20, 20, 80, 20), new(200, 100, 50, (byte)MathF.Round(255 * alpha)), default, 0, 0);
                        paint.Frame(new(20, secondY, 80, 20), new(50, 180, 100), default, 0, 0);
                    });
                    var maximumDifference = 0;
                    for (var i = 0; i < actual.Length; i++)
                        maximumDifference = Math.Max(maximumDifference, Math.Abs(actual[i] - expected[i]));
                    // The reference quantizes alpha to8bits; the retained composite keeps a float opacity.
                    var equal = maximumDifference <= 2;
                    pairs.Add(new(name, Hash(expected), Hash(actual), maximumDifference, equal));
                    if (!equal)
                    {
                        File.WriteAllBytes(Path.Combine(output, name + ".reference.rgba"), expected);
                        File.WriteAllBytes(Path.Combine(output, name + ".actual.rgba"), actual);
                    }
                }
                Pair(Actual(), $"{dark}-{samples}-initial", 1, 50);
                group.SetVisible(0, false);
                double? started = null;
                double? reflowStarted = null;
                for (var frame = 0; frame < 34; frame++)
                {
                    var actual = Actual();
                    var now = candidate.Context.State.TimeSeconds;
                    started ??= now;
                    var elapsed = now - started.Value;
                    var alpha = (float)Math.Clamp(1 - elapsed / .25, 0, 1);
                    if (elapsed >= .25)
                        reflowStarted ??= now;
                    var phase = reflowStarted is { } reflow ? Math.Clamp((now - reflow) / .25, 0, 1) : 0;
                    var y = (float)(20 + 30 * Math.Pow(1 - phase, 3));
                    Pair(actual, $"{dark}-{samples}-exit-{frame}", alpha, y);
                }
                if (first.Paints != 1)
                    throw new InvalidOperationException("Exit replay executed the retired child instead of replaying its frozen paint.");
            }
        var result = new PresencePixelResult(pairs.All(x => x.Equal), pairs.Count, pairs.ToArray(),
            "Independent flat rectangles,linear250ms alpha,cubic250ms reflow. Maximum2RGBA levels permits only8bit reference alpha quantization;not browser identity.");
        File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(result, PresencePixelJson.Default.PresencePixelResult));
        if (!result.Completed)
            throw new InvalidOperationException("Presence pixels differ from the independent alpha and position oracle.");
    }
    private static string Hash(byte[] pixels) => Convert.ToHexString(SHA256.HashData(pixels));
    private sealed class Block(UiColor color) : UiControlWidget
    {
        internal int Paints;
        protected override bool CanRetainMeasurement => true;
        protected override UiVector2 MeasureContent(UiControlContext context) => context.LayoutConstraints.Constrain(new(80, 20));
        protected override void RenderControl(UiImmediateContext ui)
        {
            Paints++;
            var origin = ui.AllocateLayoutBox(new(80, 20));
            new UiControlContext(ui).Frame(new(origin.X, origin.Y, 80, 20), color, default, 0, 0);
            ui.FinishLayoutBox(origin, new(80, 20));
        }
    }
}
internal sealed record PresencePixelPair(string Name, string Reference, string Actual, int MaximumDifference, bool Equal);
internal sealed record PresencePixelResult(bool Completed, int ComparedPairs, PresencePixelPair[] Pairs, string Scope);
[JsonSerializable(typeof(PresencePixelResult))]
internal partial class PresencePixelJson : JsonSerializerContext;
