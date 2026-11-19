using System.Text.Json;
using Duxel.Core.Dsl.Generator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

internal static class FluentIconIncrementalTests
{
    internal static void Run(Action<bool, string> check, IEnumerable<MetadataReference> references, CSharpParseOptions parseOptions, string output)
    {
        const string regularDeclaration = "[UiFluentIcon(\"add\")] public readonly partial struct RegularIcon;";
        const string source = "using Duxel.Core; namespace Consumer;\n" + regularDeclaration + "\n"
            + "[UiFluentIcon(\"add\",UiFluentIconStyle.Filled)] public readonly partial struct FilledIcon;\n"
            + "[UiFluentIcon(\"add\",UiFluentIconStyle.Interactive)] public readonly partial struct InteractiveIcon;";
        const string regular = "{\n\"ic_fluent_add_20_regular\": 61705,\n\"ic_fluent_add_24_regular\": 61706,\n\"ic_fluent_unused_20_regular\": 61707\n}";
        const string filled = "{\n\"ic_fluent_add_20_filled\": 61705,\n\"ic_fluent_add_24_filled\": 61706\n}";
        var regularFile = new GlyphFile("FluentSystemIcons-Regular.json", regular);
        var filledFile = new GlyphFile("FluentSystemIcons-Filled.json", filled);
        var compilation = CSharpCompilation.Create("IconIncrementalConsumer", [CSharpSyntaxTree.ParseText(source, parseOptions, path: "Consumer.cs")], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        GeneratorDriver driver = CSharpGeneratorDriver.Create([new UiFluentIconSourceGenerator().AsSourceGenerator()],
            [regularFile, filledFile], parseOptions, driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, true));
        var trace = new List<object>();
        GeneratorRunResult Step(string name)
        {
            driver = driver.RunGenerators(compilation);
            var result = driver.GetRunResult().Results.Single();
            trace.Add(new
            {
                step = name,
                emission = Reasons(result),
                sourceOutput = OutputReasons(result),
                generated = result.GeneratedSources.Select(s => s.HintName).ToArray(),
                errors = result.Diagnostics.Select(d => d.Id).ToArray()
            });
            File.WriteAllText(Path.Combine(output, "icon-incremental-trace.json"), JsonSerializer.Serialize(trace, new JsonSerializerOptions { WriteIndented = true }));
            return result;
        }
        static string[] Reasons(GeneratorRunResult result) => result.TrackedSteps["FluentIconEmission"].SelectMany(s => s.Outputs).Select(o => o.Reason.ToString()).ToArray();
        static string[] OutputReasons(GeneratorRunResult result) => result.TrackedOutputSteps.Values.SelectMany(steps => steps).SelectMany(s => s.Outputs).Select(o => o.Reason.ToString()).ToArray();
        static Dictionary<string, string> Sources(GeneratorRunResult result) => result.GeneratedSources.ToDictionary(s => s.HintName, s => s.SourceText.ToString());
        static bool Cached(GeneratorRunResult result, int count) => Reasons(result).Count(r => r == "Cached") == count && OutputReasons(result).Count(r => r == "Cached") == count;
        void Code(string text) => compilation = compilation.ReplaceSyntaxTree(compilation.SyntaxTrees.Single(), CSharpSyntaxTree.ParseText(text, parseOptions, path: "Consumer.cs"));
        void Regular(string text)
        {
            var next = new GlyphFile(regularFile.Path, text);
            driver = driver.ReplaceAdditionalText(regularFile, next);
            regularFile = next;
        }
        void Filled(string text)
        {
            var next = new GlyphFile(filledFile.Path, text);
            driver = driver.ReplaceAdditionalText(filledFile, next);
            filledFile = next;
        }
        var initial = Step("initial");
        var original = Sources(initial);
        var repeated = Step("unchanged");
        check(Cached(repeated, 3), "unchanged icon inputs reuse all three actual Roslyn source outputs");
        Regular(regular.Replace("61707", "61708"));
        var unused = Step("unused-glyph");
        check(Cached(unused, 3) && Sources(unused).All(p => original[p.Key] == p.Value),
            "an unused glyph mapping change skips both icon text construction and source output");
        var before = Sources(unused);
        Regular(regular.Replace("61707", "61708").Replace("61705", "61709"));
        var changedRegular = Step("selected-regular-glyph");
        var changed = Sources(changedRegular).Where(p => before[p.Key] != p.Value).Select(p => p.Key).ToArray();
        check(changed.Length == 2 && changed.All(n => !n.Contains("FilledIcon")) && Cached(changedRegular, 1),
            "a selected regular glyph regenerates only regular and interactive consumers");
        before = Sources(changedRegular);
        Filled(filled.Replace("61705", "61710"));
        var changedFilled = Step("selected-filled-glyph");
        changed = Sources(changedFilled).Where(p => before[p.Key] != p.Value).Select(p => p.Key).ToArray();
        check(changed.Length == 2 && changed.All(n => !n.Contains("RegularIcon")) && Cached(changedFilled, 1),
            "a selected filled glyph regenerates only filled and interactive consumers");
        var unrelatedSource = source + "\ninternal class Unrelated { public const int Value=42; }";
        Code(unrelatedSource);
        var unrelated = Step("unrelated-code");
        check(Cached(unrelated, 3), "unrelated CSharp edits do not reconstruct unchanged icon source text");
        var invalidSource = unrelatedSource.Replace(regularDeclaration, regularDeclaration.Replace("\"add\"", "\"missing\""));
        Code(invalidSource);
        var invalid = Step("missing-selected-glyph");
        check(invalid.Diagnostics.Count(d => d.Id == "DUXWGT008") == 1 && invalid.GeneratedSources.Length == 2,
            "a missing selected glyph removes its previous generated output and reports an explicit error");
        var oldLocation = invalid.Diagnostics.Single().Location;
        Code("\n" + invalidSource);
        var moved = Step("moved-error");
        var location = moved.Diagnostics.Single().Location;
        check(location.SourceTree == compilation.SyntaxTrees.Single() && location.SourceSpan.Start == oldLocation.SourceSpan.Start + 1 && Cached(moved, 2),
            "unchanged icon errors move to the current syntax tree while unrelated positive outputs stay cached");
        Code(unrelatedSource);
        var repaired = Step("repaired-name");
        check(repaired.Diagnostics.Length == 0 && repaired.GeneratedSources.Length == 3 && Cached(repaired, 2),
            "repairing an icon name restores only the removed output");
        Filled("invalid");
        var malformed = Step("malformed-filled-catalog");
        check(malformed.Diagnostics.Count(d => d.Id == "DUXWGT008") == 2 && malformed.GeneratedSources.Length == 1
            && malformed.GeneratedSources[0].HintName.Contains("RegularIcon") && Cached(malformed, 1),
            "malformed filled data fails both dependent styles without invalidating regular-only icons");
        Filled(filled.Replace("61705", "61710"));
        var beforeRemoval = Sources(Step("repaired-filled-catalog"));
        Code(unrelatedSource.Replace(regularDeclaration, string.Empty));
        var removed = Step("removed-declaration");
        check(removed.Diagnostics.Length == 0 && removed.GeneratedSources.Length == 2 && !removed.GeneratedSources.Any(s => s.HintName.Contains("RegularIcon"))
            && Sources(removed).All(p => beforeRemoval[p.Key] == p.Value),
            "removing an icon declaration drops its output and preserves the remaining source content exactly");
        File.WriteAllText(Path.Combine(output, "icon-incremental-limits.json"), JsonSerializer.Serialize(new
        {
            strictRemovalCacheComplete = Cached(removed, 2),
            expectedCachedOutputsOnRemoval = 2,
            observedCachedOutputsOnRemoval = Reasons(removed).Count(r => r == "Cached"),
            observedModifiedOutputsOnRemoval = Reasons(removed).Count(r => r == "Modified"),
            scope = "The no-reemission removal criterion remains incomplete when declaration positions shift. Functional source removal/content checks do not qualify this performance criterion."
        }, new JsonSerializerOptions { WriteIndented = true }));
        driver = driver.AddAdditionalTexts([new GlyphFile("Unrelated.txt", "not a glyph map")]);
        var ignored = Step("unrelated-additional-file");
        check(Cached(ignored, 2), "unrelated AdditionalFiles never trigger icon source construction");
        RunRemovalMatrix(check, references, parseOptions, output, regularFile, filledFile);
    }

    private static void RunRemovalMatrix(Action<bool, string> check, IEnumerable<MetadataReference> references,
        CSharpParseOptions parseOptions, string output, AdditionalText regular, AdditionalText filled)
    {
        string[] declarations =
        [
            "[UiFluentIcon(\"add\")] public readonly partial struct RegularIcon;",
            "[UiFluentIcon(\"add\", UiFluentIconStyle.Filled)] public readonly partial struct FilledIcon;",
            "[UiFluentIcon(\"add\", UiFluentIconStyle.Interactive)] public readonly partial struct InteractiveIcon;"
        ];
        string[] names = ["RegularIcon", "FilledIcon", "InteractiveIcon"];
        const string prefix = "using Duxel.Core; namespace Consumer;\n";
        var observations = new List<RemovalObservation>();
        foreach (var separateFiles in new[] { false, true })
            for (var removedIndex = 0; removedIndex < declarations.Length; removedIndex++)
            {
                var scope = (separateFiles ? "separate-files/" : "same-file/") + names[removedIndex];
                SyntaxTree[] trees = separateFiles
                    ? declarations.Select((declaration, index) => CSharpSyntaxTree.ParseText(prefix + declaration,
                        parseOptions, path: names[index] + ".cs")).ToArray()
                    : [CSharpSyntaxTree.ParseText(prefix + string.Join("\n", declarations), parseOptions, path: "Consumer.cs")];
                var compilation = CSharpCompilation.Create("IconRemovalMatrix", trees, references,
                    new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
                GeneratorDriver driver = CSharpGeneratorDriver.Create([new UiFluentIconSourceGenerator().AsSourceGenerator()],
                    [regular, filled], parseOptions, driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, true));
                driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var initialCompilation, out _);
                var initial = driver.GetRunResult().Results.Single();
                check(initial.GeneratedSources.Length == 3 && initial.Diagnostics.IsEmpty
                    && !initialCompilation.GetDiagnostics().Any(d => d.Severity == DiagnosticSeverity.Error),
                    "The removal matrix starts with three real compilable icon consumers, " + scope);
                var before = initial.GeneratedSources.ToDictionary(s => s.HintName, s => s.SourceText.ToString());
                var removedHint = "Consumer." + names[removedIndex] + ".FluentIcon.g.cs";
                if (!before.Remove(removedHint))
                    throw new InvalidOperationException("The removal fixture did not publish its target output: " + removedHint);
                var changed = separateFiles
                    ? compilation.RemoveSyntaxTrees(trees[removedIndex])
                    : compilation.ReplaceSyntaxTree(trees[0], CSharpSyntaxTree.ParseText(
                        prefix + string.Join("\n", declarations).Replace(declarations[removedIndex], string.Empty), parseOptions, path: "Consumer.cs"));
                driver = driver.RunGeneratorsAndUpdateCompilation(changed, out var updated, out _);
                var result = driver.GetRunResult().Results.Single();
                var sources = result.GeneratedSources.ToDictionary(s => s.HintName, s => s.SourceText.ToString());
                check(result.Diagnostics.IsEmpty && sources.Count == 2 && sources.All(p => before.TryGetValue(p.Key, out var previous) && previous == p.Value)
                    && updated.GetTypeByMetadataName("Consumer." + names[removedIndex]) is null
                    && !updated.GetDiagnostics().Any(d => d.Severity == DiagnosticSeverity.Error),
                    "Removal drops the actual consumer type and preserves both remaining compilable outputs exactly, " + scope);
                var emission = result.TrackedSteps["FluentIconEmission"].SelectMany(s => s.Outputs).Select(o => o.Reason.ToString()).ToArray();
                var sourceOutput = result.TrackedOutputSteps.Values.SelectMany(steps => steps).SelectMany(s => s.Outputs).Select(o => o.Reason.ToString()).ToArray();
                var cachedEmissions = emission.Count(r => r == "Cached");
                var cachedOutputs = sourceOutput.Count(r => r == "Cached");
                if (removedIndex == declarations.Length - 1)
                    check(cachedEmissions == 2 && cachedOutputs == 2,
                        "Unshifted declaration slots reuse both actual Roslyn source outputs, " + scope);
                observations.Add(new(scope, 2, cachedEmissions, cachedOutputs, emission, sourceOutput));
            }
        File.WriteAllText(Path.Combine(output, "icon-removal-matrix.json"), JsonSerializer.Serialize(observations,
            new JsonSerializerOptions { WriteIndented = true }));
    }

    private sealed record RemovalObservation(string Scope, int ExpectedCachedOutputs, int CachedEmissions,
        int CachedSourceOutputs, string[] EmissionReasons, string[] SourceOutputReasons);

    private sealed class GlyphFile(string path, string content) : AdditionalText
    {
        public override string Path => path;
        public override SourceText GetText(CancellationToken cancellationToken = default) => SourceText.From(content);
    }
}
