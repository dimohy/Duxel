using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Duxel.Svg;

internal static class LinuxProbeResultContracts
{
    internal static void Run(string output)
    {
        if (Directory.Exists(output))
            throw new IOException("Use a fresh result-contract directory.");
        Directory.CreateDirectory(output);
        var checks = new List<string>();
        if (JsonSerializer.IsReflectionEnabledByDefault)
            throw new InvalidOperationException("The Linux probe must not enable reflection JSON.");
        checks.Add("Reflection JSON is disabled in the actual executable");
        void Expect<T>(string name, T value, JsonTypeInfo<T> metadata, string schema)
        {
            using var expected = JsonDocument.Parse(schema);
            var canonical = JsonSerializer.Serialize(expected.RootElement, LinuxProbeResultsJson.Default.JsonElement);
            var actual = JsonSerializer.Serialize(value, metadata);
            File.WriteAllText(Path.Combine(output, name + ".expected.json"), canonical);
            File.WriteAllText(Path.Combine(output, name + ".json"), actual);
            if (actual != canonical)
                throw new InvalidOperationException("Generated result changed names, casing, order, values or formatting : " + name);
            checks.Add("Original result shape and values preserved : " + name);
        }
        string[] messages = ["a", "정"];
        Expect("checks", new LinuxChecksResult("fixture", "scope", 2, messages), LinuxProbeResultsJson.Default.LinuxChecksResult,
            """{"framework":"fixture","scope":"scope","passed":2,"checks":["a","정"]}""");
        Expect("boundary", new LinuxBoundaryResult("pango", 2, messages), LinuxProbeResultsJson.Default.LinuxBoundaryResult,
            """{"pangoVersion":"pango","passed":2,"checks":["a","정"]}""");
        Expect("font", new LinuxFontResult("harfbuzz", 2, messages), LinuxProbeResultsJson.Default.LinuxFontResult,
            """{"harfbuzzVersion":"harfbuzz","passed":2,"checks":["a","정"]}""");
        var observation = new LinuxApplicationObservation(true, 3, 0, 4, .5, 2, "정", false, 9,
            true, true, true, true, new SvgStoreStatistics(1, 2, 3, 4, 5, 6, 7, 8), true, true, true, false, false, 13);
        const string observationSchema = """{"registered":true,"RenderCount":3,"idleFrames":0,"workerFrames":4,"idleCpuMilliseconds":0.5,"Clicks":2,"Value":"정","nativeCloseSent":false,"capturedColors":9,"imagePixelsMatch":true,"transformedSvgReused":true,"nativeAccessibility":true,"nativeAccessibilityWindowFocus":true,"imageStatistics":{"SourceReads":1,"XmlParses":2,"SceneCompiles":3,"Rasterizations":4,"RasterCacheHits":5,"CachedRasterBytes":6,"Documents":7,"GpuSceneCompiles":8},"copied":true,"pasted":true,"unicodeTextState":true,"userClipboardRead":false,"userClipboardWritten":false,"elapsedMilliseconds":13}""";
        Expect("observation", observation, LinuxProbeResultsJson.Default.LinuxApplicationObservation, observationSchema);
        Expect("application", new LinuxApplicationResult("fixture", "os", "arch", 2, messages, [observation], false),
            LinuxProbeResultsJson.Default.LinuxApplicationResult,
            """{"framework":"fixture","os":"os","architecture":"arch","passed":2,"checks":["a","정"],"observations":["""
            + observationSchema + """],"validationHooks":false}""");
        Expect("transport", new LinuxTransportResult("fixture", "unreachable-bus", 8, 5, 10, 10, 0, [.25, .5]),
            LinuxProbeResultsJson.Default.LinuxTransportResult,
            """{"framework":"fixture","failureKind":"unreachable-bus","expectedFailures":8,"measuredFailures":5,"beforeDescriptors":10,"afterDescriptors":10,"ownedThreadsRemaining":0,"elapsedMilliseconds":[0.25,0.5]}""");
        Expect("ancestor", new LinuxAncestorResult("fixture", true, false, true, null, 2, messages, "qualification"),
            LinuxProbeResultsJson.Default.LinuxAncestorResult,
            """{"framework":"fixture","rightToLeft":true,"virtualized":false,"variableExtents":true,"popupMode":null,"passed":2,"checks":["a","정"],"qualification":"qualification"}""");
        Expect("cleanup", new LinuxCleanupResult("fixture", 42, true, 2, messages), LinuxProbeResultsJson.Default.LinuxCleanupResult,
            """{"framework":"fixture","parentPid":42,"nativeCleanupCompleted":true,"passed":2,"checks":["a","정"]}""");
        Expect("registry", new LinuxRegistryResult("fixture", 42, false), LinuxProbeResultsJson.Default.LinuxRegistryResult,
            """{"framework":"fixture","parentPid":42,"registered":false}""");
        Expect("qualified", new LinuxQualifiedResult("fixture", 2, messages, "qualification"), LinuxProbeResultsJson.Default.LinuxQualifiedResult,
            """{"framework":"fixture","passed":2,"checks":["a","정"],"qualification":"qualification"}""");
        Expect("markdown", new LinuxMarkdownResult("fixture", 2, messages, false, false, "qualification"),
            LinuxProbeResultsJson.Default.LinuxMarkdownResult,
            """{"framework":"fixture","passed":2,"checks":["a","정"],"userClipboardRead":false,"userClipboardWritten":false,"qualification":"qualification"}""");
        Expect("policy", new LinuxPolicyResult(2, messages), LinuxProbeResultsJson.Default.LinuxPolicyResult,
            """{"passed":2,"checks":["a","정"]}""");
        if (checks.Count != 13)
            throw new InvalidOperationException("Every retained Linux result shape must be checked.");
        File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(
            new LinuxChecksResult(RuntimeInformation.FrameworkDescription, "result-contract", checks.Count, checks),
            LinuxProbeResultsJson.Default.LinuxChecksResult));
    }
}
