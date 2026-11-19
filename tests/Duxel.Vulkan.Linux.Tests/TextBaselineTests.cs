using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Duxel.Core;
using Duxel.Platform.Linux;

internal static class TextBaselineTests
{
    internal static void Run(string output, int slots)
    {
        Directory.CreateDirectory(output);
        var checks = new List<string>();
        var metrics = new List<BaselineMetric>();
        var pixels = new List<BaselinePixels>();
        void Check(bool condition, string name)
        {
            if (!condition)
                throw new InvalidOperationException(name);
            checks.Add(name);
            Console.WriteLine("PASS " + name);
        }
        using var native = new LinuxPlatformTextBackend();
        using var correct = new NativeMetricOracle(native, priorMapping: false);
        using var prior = new NativeMetricOracle(native, priorMapping: true);
        foreach (var path in PresentationTestPlatform.Fonts)
            foreach (var size in new[] { 12f, 14f, 16f, 20f, 28f })
            {
                Check(native.TryGetFontMetrics(path, size, out var actual) && correct.TryGetFontMetrics(path, size, out var expected),
                    "Actual Linux metrics and independent raw Cairo oracle are available : " + Path.GetFileName(path) + "/" + size);
                correct.TryGetFontMetrics(path, size, out expected);
                Check(actual == expected && actual.Ascent > 0 && actual.Descent > 0,
                    "Shared ascent/descent preserve native positive distances : " + Path.GetFileName(path) + "/" + size);
                var expectedBaseline = (20 - expected.Ascent - expected.Descent) / 2 + expected.Ascent;
                var priorBaseline = (20 - expected.Ascent + expected.Descent) / 2 + expected.Ascent;
                metrics.Add(new(path, size, actual, expected, expectedBaseline, priorBaseline));
            }
        using var reference = new PresentationReuseTests.Fixture(slots, false);
        using var candidate = new PresentationReuseTests.Fixture(slots, true);
        using var referenceTree = new UiAccessibilityTree();
        using var candidateTree = new UiAccessibilityTree();
        reference.Context.SetAccessibilityTree(referenceTree);
        candidate.Context.SetAccessibilityTree(candidateTree);
        for (var kind = 0; kind < 8; kind++)
            foreach (var dark in new[] { false, true })
                foreach (var samples in new[] { 1, 4 })
                {
                    var design = dark ? UiCompiledDesign.Windows11Dark : UiCompiledDesign.Windows11;
                    reference.Context.SetDesign(design);
                    candidate.Context.SetDesign(design);
                    reference.Renderer.SetMsaaSamples(samples);
                    candidate.Renderer.SetMsaaSamples(samples);
                    using var left = Widget(kind);
                    using var right = Widget(kind);
                    void Draw(UiImmediateContext ui, UiWidget widget)
                    {
                        ui.EnableRootViewportContentLayout(contentPadding: 0);
                        new UiControlContext(ui).Frame(new(0, 0, 320, 240), new(11, 33, 71), default, 0, 0);
                        ui.SetCursorScreenPos(new(20, 20));
                        widget.Render(ui);
                    }
                    void Focus(UiAccessibilityTree tree)
                    {
                        if (kind is not (6 or 7))
                            return;
                        var field = tree.CaptureSnapshot().Elements.First(x => x.Node.Role == UiAccessibilityRole.Edit || x.Node.Role == UiAccessibilityRole.SpinButton);
                        if (tree.RequestAction(field.Node.Id, field.RuntimeId, UiAccessibilityAction.Focus, wait: false) != UiAccessibilityResult.Success)
                            throw new InvalidOperationException("Owned baseline input did not accept virtual focus.");
                    }
                    reference.Context.SetPlatformTextBackend(prior);
                    reference.Capture(ui => Draw(ui, left));
                    Focus(referenceTree);
                    var oldPixels = reference.Capture(ui => Draw(ui, left));
                    reference.Context.SetPlatformTextBackend(correct);
                    var expectedPixels = reference.Capture(ui => Draw(ui, left));
                    candidate.Capture(ui => Draw(ui, right));
                    Focus(candidateTree);
                    var actualPixels = candidate.Capture(ui => Draw(ui, right));
                    var name = $"{kind}-{dark}-{samples}";
                    var equal = actualPixels.AsSpan().SequenceEqual(expectedPixels);
                    var reproduced = !oldPixels.AsSpan().SequenceEqual(expectedPixels);
                    pixels.Add(new(name, Hash(oldPixels), Hash(expectedPixels), Hash(actualPixels), equal, reproduced));
                    if (!equal || dark && samples == 1)
                    {
                        File.WriteAllBytes(Path.Combine(output, name + ".prior-mapping.rgba"), oldPixels);
                        File.WriteAllBytes(Path.Combine(output, name + ".native-reference.rgba"), expectedPixels);
                        File.WriteAllBytes(Path.Combine(output, name + ".actual.rgba"), actualPixels);
                    }
                    Check(reproduced, "Historical negative-descent mapping reproduces a visible widget displacement : " + name);
                    Check(equal, "Actual widget and caret pixels match independent raw native metrics : " + name);
                }
        File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(
            new BaselineResult(true, checks.Count, checks.ToArray(), metrics.ToArray(), pixels.ToArray(),
                "Prior pixels explicitly simulate the historical adapter sign;they are not a measurement of an old executable."), BaselineJson.Default.BaselineResult));
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static UiWidget Widget(int kind) => kind switch
    {
        0 => Fluent.Button("확인 Abgj"),
        1 => Fluent.Badge("Ready"),
        2 => Fluent.Checkbox("선택 Abgj", new UiState<bool?>(null).Binding),
        3 => Fluent.RadioGroup("처리 방식", new UiState<string?>("a").Binding,
            new UiRadioOption[] { new("a", "한글 Abgj"), new("b", "선택") }),
        4 => Fluent.Spinner("일시 정지 Abgj", new(ReducedMotion: true) { Paused = true }),
        5 => Fluent.Body1("한글 본문 Abgj"),
        6 => Fluent.Input("Baseline input", new UiState<string>("한글 Abgj").Binding, 240, 120),
        7 => Fluent.SpinButton("Baseline spin", new UiState<double?>(3.5).Binding),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    private sealed class NativeMetricOracle(LinuxPlatformTextBackend source, bool priorMapping) : IPlatformTextBackend,
        IPlatformTextLayoutBackend, IPlatformFontMetricsBackend, IPlatformVectorTextBackend, IPlatformFontVariantBackend,
        IPlatformTextFontRunBackend, IDisposable
    {
        private readonly Dictionary<(string, float), LinuxFontResource> _resources = [];
        public ulong TextLayoutVersion => source.TextLayoutVersion;
        public bool TryGetFontMetrics(string path, float size, out PlatformFontMetrics result)
        {
            if (!_resources.TryGetValue((path, size), out var font))
                _resources.Add((path, size), font = new(path, size, false));
            var raw = font.Metrics;
            result = new((float)raw.Ascent, (float)(priorMapping ? -raw.Descent : raw.Descent), (float)(raw.Height - raw.Ascent - raw.Descent));
            return true;
        }
        public bool TryMeasure(in PlatformTextMeasureRequest request, out PlatformTextMeasureResult result) => source.TryMeasure(request, out result);
        public bool TryLayout(in PlatformTextMeasureRequest request, out PlatformTextLayoutResult result) => source.TryLayout(request, out result);
        public bool TryRasterize(in PlatformTextRasterizeRequest request, out PlatformTextRasterizeResult result) => source.TryRasterize(request, out result);
        public PlatformTextMeshResult CreateTextMesh(in PlatformTextMeshRequest request) => source.CreateTextMesh(request);
        public string GetFontVariantResource(string font, PlatformFontSimulation simulation) => source.GetFontVariantResource(font, simulation);
        public IReadOnlyList<PlatformTextFontRun> GetFontRuns(in PlatformTextMeasureRequest request, string language) => source.GetFontRuns(request, language);
        public void Dispose()
        {
            foreach (var font in _resources.Values)
                font.Dispose();
            _resources.Clear();
        }
    }
}

internal sealed record BaselineMetric(string Font, float Size, PlatformFontMetrics Actual, PlatformFontMetrics Native, float CorrectBaseline, float HistoricalBaseline);
internal sealed record BaselinePixels(string Name, string Prior, string Reference, string Actual, bool Equal, bool HistoricalDisplacementReproduced);
internal sealed record BaselineResult(bool Completed, int Passed, string[] Checks, BaselineMetric[] Metrics, BaselinePixels[] Pixels, string Scope);
[JsonSerializable(typeof(BaselineResult))]
internal partial class BaselineJson : JsonSerializerContext;
