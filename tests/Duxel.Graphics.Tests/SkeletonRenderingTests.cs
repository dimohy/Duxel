using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Duxel.Core;

internal static class SkeletonRenderingTests
{
    internal static void Run(string output, int slots)
    {
        if (Directory.Exists(output))
            throw new IOException("Skeleton GPU validation requires a fresh output directory.");
        Directory.CreateDirectory(output);
        var checks = new List<string>();
        var pairs = new List<SkeletonPixelPair>();
        string? failure = null;
        void Check(bool condition, string name)
        {
            if (!condition)
                throw new InvalidOperationException(name);
            checks.Add(name);
        }
        try
        {
            using var reference = new PresentationReuseTests.Fixture(slots, false);
            using var candidate = new PresentationReuseTests.Fixture(slots, true);
            var background = new UiColor(11, 33, 71);
            void Draw(UiImmediateContext ui, UiWidget item)
            {
                ui.EnableRootViewportContentLayout(contentPadding: 0);
                ui.GetWindowDrawList().AddRectFilled(new(0, 0, 320, 240), background, new UiTextureId(2));
                ui.SetCursorScreenPos(new(20, 20));
                item.Render(ui);
            }
            foreach (var dark in new[] { false, true })
                foreach (var msaa in new[] { 1, 4 })
                {
                    reference.Context.SetDesign(dark ? UiCompiledDesign.Windows11Dark : UiCompiledDesign.Windows11);
                    candidate.Context.SetDesign(dark ? UiCompiledDesign.Windows11Dark : UiCompiledDesign.Windows11);
                    reference.Renderer.SetMsaaSamples(msaa);
                    candidate.Renderer.SetMsaaSamples(msaa);
                    foreach (var appearance in Enum.GetValues<UiSkeletonAppearance>())
                        foreach (var animation in Enum.GetValues<UiSkeletonAnimation>())
                            foreach (var shape in Enum.GetValues<UiSkeletonShape>())
                            {
                                var options = new UiSkeletonOptions(Animation: animation, Appearance: appearance, Size: 20, Shape: shape, Width: 80);
                                using var control = Fluent.SkeletonItem(options);
                                using var subject = Fluent.SkeletonItem(options);
                                for (var step = 0; step < 8; step++)
                                {
                                    var expected = reference.Capture(ui => Draw(ui, control));
                                    var actual = candidate.Capture(ui => Draw(ui, subject));
                                    var name = $"{dark}-{msaa}-{appearance}-{animation}-{shape}-{step}";
                                    var equal = expected.AsSpan().SequenceEqual(actual);
                                    pairs.Add(new(name, Convert.ToHexString(SHA256.HashData(expected)), Convert.ToHexString(SHA256.HashData(actual))));
                                    if (!equal)
                                    {
                                        File.WriteAllBytes(Path.Combine(output, name + ".reference.rgba"), expected);
                                        File.WriteAllBytes(Path.Combine(output, name + ".candidate.rgba"), actual);
                                    }
                                    Check(equal, name + " matches independent full repaint raw pixels");
                                }
                            }
                    using var ellipse = Fluent.SkeletonItem(new(Size: 20, Shape: UiSkeletonShape.Circle, Width: 80, Paused: true));
                    using var referenceEllipse = Fluent.SkeletonItem(ellipse.Options);
                    var expectedEllipse = reference.Capture(ui => Draw(ui, referenceEllipse));
                    var pixels = candidate.Capture(ui => Draw(ui, ellipse));
                    Check(expectedEllipse.AsSpan().SequenceEqual(pixels), $"{dark}-{msaa} frozen ellipse matches full repaint");
                    int At(int x, int y) => (y * 320 + x) * 4;
                    var edge = At(22, 22);
                    var center = At(60, 30);
                    Check(pixels[edge] == 11 && pixels[edge + 1] == 33 && pixels[edge + 2] == 71,
                        $"{dark}-{msaa} nonsquare circle is an ellipse rather than a capsule");
                    Check(pixels[center] == (dark ? 87 : 230) && pixels[center + 1] == pixels[center]
                        && pixels[center + 2] == pixels[center], $"{dark}-{msaa} opaque stencil uses pinned source tokens");
                }
        }
        catch (Exception error) { failure = error.ToString(); }
        finally
        {
            File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(
                new SkeletonRenderResult(failure is null, failure, slots, checks.Count, checks.ToArray(), pairs.ToArray()),
                SkeletonRenderJson.Default.SkeletonRenderResult));
        }
        if (failure is not null)
            throw new InvalidOperationException(failure);
    }
}

internal sealed record SkeletonPixelPair(string Name, string ReferenceSha256, string CandidateSha256);
internal sealed record SkeletonRenderResult(bool Completed, string? Failure, int FrameSlots, int Passed,
    string[] Checks, SkeletonPixelPair[] Pairs);
[JsonSerializable(typeof(SkeletonRenderResult))]
internal partial class SkeletonRenderJson : JsonSerializerContext;
