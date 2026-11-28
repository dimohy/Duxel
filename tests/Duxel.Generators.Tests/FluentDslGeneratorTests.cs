using System.Reflection;
using System.Runtime.Loader;
using Duxel.FluentDsl.Generator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

internal static class FluentDslGeneratorTests
{
    internal static void Run(Action<bool, string> check, MetadataReference[] references, CSharpParseOptions options)
    {
        const string settings = """
            schema: 1
            screen: Settings
            using: [Fluent]
            state:
              Status: Ready
            view:
              Dux.Column:
                spacing: 12
                children:
                  - Input:
                      label: Email
                      value: { bind: Email }
                  - Switch:
                      label: Enable
                      value: { bind: Enabled }
                  - Text:
                      text: { read: Status }
                  - Button:
                      label: Save
                      onClick: Save
            """;
        const string about = """
            schema: 1
            screen: About
            view:
              Fluent.Text:
                text: About
            """;
        const string consumer = """
            using Duxel.Generated.Views;

            public static class Probe
            {
                public static bool Run()
                {
                    using var first = SettingsView.Create(state => state.Status.Value = state.Email.Value);
                    using var second = SettingsView.Create(_ => { });
                    using var about = AboutView.Create();
                    first.State.Email.Binding.Value = "changed";
                    first.State.Enabled.Value = true;
                    return first.State.Email.Value == "changed" && first.State.Enabled.Binding.Value
                        && second.State.Email.Value == "" && !second.State.Enabled.Value
                        && first.State.Status.Value == "Ready";
                }
            }
            """;

        (Compilation Compilation, GeneratorDriverRunResult Result) Generate(string source, params AdditionalText[] files)
        {
            var syntax = CSharpSyntaxTree.ParseText(source, options);
            var compilation = CSharpCompilation.Create("FluentDslConsumer" + Guid.NewGuid().ToString("N"),
                [syntax], references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            GeneratorDriver driver = CSharpGeneratorDriver.Create(
                [new FluentDslGenerator().AsSourceGenerator()], files, options);
            driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var updated, out _);
            return (updated, driver.GetRunResult());
        }

        var valid = Generate(consumer, new ViewFile("Settings.duxel.yaml", settings), new ViewFile("About.duxel.yaml", about));
        var errors = valid.Compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
        check(valid.Result.Diagnostics.Length == 0 && errors.Length == 0 && valid.Result.GeneratedTrees.Length == 3,
            "two Fluent DSL screens compile with typed state, existing widget factories and their shared host: " + string.Join("; ", errors.Select(d => d.ToString())));
        var names = valid.Result.GeneratedTrees.Select(tree => tree.GetText().ToString()).ToArray();
        var settingsSource = names.Single(text => text.Contains("class SettingsState"));
        check(settingsSource.Contains("Fluent.Input(") && settingsSource.Contains("state.Email.Binding")
            && settingsSource.Contains("state.Enabled.Binding") && settingsSource.Contains("() => state.Status.Value")
            && !names.Any(text => text.Contains("YamlDotNet")),
            "generated Fluent DSL uses direct C# API calls and no runtime YAML dependency");
        using (var stream = new MemoryStream())
        {
            var result = valid.Compilation.Emit(stream);
            check(result.Success, "compiled multi-file Fluent DSL emits a loadable assembly");
            stream.Position = 0;
            var loaded = AssemblyLoadContext.Default.LoadFromStream(stream);
            check(loaded.GetType("Probe")!.GetMethod("Run", BindingFlags.Public | BindingFlags.Static)!
                .Invoke(null, null) is true, "generated per-screen states share two-way bindings and isolate instances");
        }

        var qualified = settings.Replace("using: [Fluent]\n", "", StringComparison.Ordinal);
        foreach (var widget in new[] { "Input", "Switch", "Text", "Button" })
            qualified = qualified.Replace($"- {widget}:", $"- Fluent.{widget}:", StringComparison.Ordinal);
        var explicitNames = Generate("", new ViewFile("Settings.duxel.yaml", qualified));
        check(explicitNames.Result.GeneratedTrees.Any(tree => tree.GetText().ToString() == settingsSource),
            "file-local Fluent imports and qualified names generate identical code");

        const string padded = """
            schema: 1
            screen: Padded
            using: [Fluent, Dux]
            view:
              Padding:
                all: 8
                child:
                  Row:
                    spacing: 6
                    children:
                      - Input:
                          label: First
                          value: { bind: Shared }
                      - Input:
                          label: Second
                          value: { bind: Shared }
            """;
        var paddedResult = Generate("", new ViewFile("Padded.duxel.yaml", padded));
        var paddedSource = paddedResult.Result.GeneratedTrees.Single(tree => tree.GetText().ToString().Contains("class PaddedState"))
            .GetText().ToString();
        check(!paddedResult.Compilation.GetDiagnostics().Any(d => d.Severity == DiagnosticSeverity.Error)
            && paddedSource.Contains("Dux.Padding(") && paddedSource.Contains("Dux.Row(")
            && paddedSource.Split("state.Shared.Binding").Length == 3
            && paddedSource.Split("UiState<string> Shared").Length == 2,
            "Dux layout imports compose with Fluent controls and repeated binds reuse one generated state");

        const string showcase = """
            schema: 1
            screen: Showcase
            using: [Dux, Fluent]
            state:
              Level: { type: float, initial: 2.5 }
              Caption: Preview
            view:
              SizedBox:
                width: 240
                child:
                  UniformGrid:
                    columns: 2
                    spacing: 8
                    rowHeight: 96
                    children:
                      - Card:
                          title: Preview
                          child:
                            Wrap:
                              runSpacing: 4
                              children:
                                - Checkbox: { label: Enable, value: { bind: Enabled } }
                                - Badge: { text: { read: Caption } }
                                - Divider: { text: { read: Caption } }
                      - Textarea: { label: Notes, value: { bind: Notes }, width: 200, height: 80 }
                      - Slider: { label: Level, value: { bind: Level }, minimum: -5, maximum: 10, step: 0.5 }
                      - ProgressBar: { value: { read: Level }, maximum: 10, height: 4 }
                      - ProgressBar: { value: 25 }
            """;
        const string showcaseConsumer = """
            using Duxel.Generated.Views;

            public static class ShowcaseProbe
            {
                public static bool Run()
                {
                    using var view = ShowcaseView.Create();
                    float initial = view.State.Level.Value;
                    view.State.Level.Binding.Value = 6.5f;
                    view.State.Notes.Binding.Value = "a line";
                    return initial == 2.5f && view.State.Level.Value == 6.5f
                        && view.State.Notes.Value == "a line" && !view.State.Enabled.Value;
                }
            }
            """;
        var showcaseResult = Generate(showcaseConsumer, new ViewFile("Showcase.duxel.yaml", showcase));
        var showcaseSource = showcaseResult.Result.GeneratedTrees.Single(tree => tree.GetText().ToString().Contains("class ShowcaseState"))
            .GetText().ToString();
        var showcaseErrors = showcaseResult.Compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
        check(showcaseResult.Result.Diagnostics.Length == 0 && showcaseErrors.Length == 0,
            "new Fluent and Dux widget rules compile against the public factories: " + string.Join("; ", showcaseErrors.Select(d => d.ToString())));
        check(showcaseSource.Contains("UiState<float> Level { get; } = new(2.5f)")
            && showcaseSource.Contains("state.Level.Binding, -5f, 10f, 0.5f")
            && showcaseSource.Contains("new global::Duxel.Core.UiProgressValue(state.Level.Value, 10f)), height : 4f)")
            && showcaseSource.Contains("Fluent.ProgressBar(25f, 100f), height : 2f)"),
            "slider and progress use direct typed C# calls with finite invariant numbers");
        using (var stream = new MemoryStream())
        {
            var result = showcaseResult.Compilation.Emit(stream);
            check(result.Success, "new layouts and controls emit a loadable assembly");
            stream.Position = 0;
            var loaded = AssemblyLoadContext.Default.LoadFromStream(stream);
            check(loaded.GetType("ShowcaseProbe")!.GetMethod("Run", BindingFlags.Public | BindingFlags.Static)!
                .Invoke(null, null) is true, "new widget factories instantiate and their state uses the existing binding API");
        }
        var inferred = Generate("", new ViewFile("Showcase.duxel.yaml",
            showcase.Replace("  Level: { type: float, initial: 2.5 }\n", "", StringComparison.Ordinal)));
        check(inferred.Result.Diagnostics.Length == 0
            && inferred.Result.GeneratedTrees.Any(tree => tree.GetText().ToString().Contains("UiState<float> Level { get; } = new(0f)")),
            "a slider bind introduces a float state when no initial value is declared");

        foreach (var sample in new (string Label, string Document, string Error)[]
        {
            ("scope", about.Replace("Fluent.Text", "Text", StringComparison.Ordinal), "Add its group to 'using'"),
            ("type conflict", settings.Replace("bind: Enabled", "bind: Email", StringComparison.Ordinal), "column"),
            ("unknown state", settings.Replace("read: Status", "read: Typo", StringComparison.Ordinal), "Unknown state 'Typo'"),
            ("duplicate key", settings.Replace("spacing: 12", "spacing: 12\n    spacing: 2", StringComparison.Ordinal), "Duplicate key 'spacing'"),
            ("unknown style", about.Replace("text: About", "text: About\n    style: Unknown", StringComparison.Ordinal), "Unknown Fluent text style"),
            ("anchor", about.Replace("Fluent.Text:", "Fluent.Text: &forbidden", StringComparison.Ordinal), "anchors"),
            ("schema", about.Replace("schema: 1", "schema: 2", StringComparison.Ordinal), "schema version"),
            ("grid columns", showcase.Replace("columns: 2", "columns: 0", StringComparison.Ordinal), "'columns' must be a positive integer"),
            ("slider range", showcase.Replace("minimum: -5", "minimum: 10", StringComparison.Ordinal), "must be greater than 'minimum'"),
            ("slider step", showcase.Replace("step: 0.5", "step: 0", StringComparison.Ordinal), "'step' must be a positive number"),
            ("progress maximum", showcase.Replace("maximum: 10, height", "maximum: -1, height", StringComparison.Ordinal), "'maximum' must be a positive number"),
            ("progress height", showcase.Replace("height: 4", "height: 0", StringComparison.Ordinal), "'height' must be a positive number"),
            ("float state", showcase.Replace("initial: 2.5", "initial: 1e500", StringComparison.Ordinal), "Expected a finite number"),
            ("numeric state conflict", showcase.Replace("{ type: float, initial: 2.5 }", "2", StringComparison.Ordinal), "used as Float"),
        })
        {
            var negative = Generate("", new ViewFile("Broken.duxel.yaml", sample.Document));
            var diagnostic = negative.Result.Diagnostics.Single();
            var line = diagnostic.Location.GetLineSpan();
            check(diagnostic.Id == "DUXFD001" && diagnostic.GetMessage().Contains(sample.Error, StringComparison.Ordinal)
                && line.Path == "Broken.duxel.yaml" && line.StartLinePosition.Line >= 0
                && negative.Result.GeneratedTrees.Single().GetText().ToString().StartsWith("#error", StringComparison.Ordinal),
                "invalid Fluent DSL reports the YAML location for " + sample.Label);
        }
        var duplicate = Generate("", new ViewFile("First.duxel.yaml", about), new ViewFile("Second.duxel.yaml", about));
        check(duplicate.Result.Diagnostics.Length == 2
            && duplicate.Result.GeneratedTrees.Single().GetText().ToString().StartsWith("#error", StringComparison.Ordinal),
            "duplicate DSL screen names report both files without generating conflicting types");
    }

    private sealed class ViewFile(string path, string content) : AdditionalText
    {
        public override string Path { get; } = path;
        public override SourceText GetText(CancellationToken cancellationToken = default) => SourceText.From(content);
    }
}
