using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using YamlDotNet.Core;

namespace Duxel.FluentDsl.Generator;

[Generator(LanguageNames.CSharp)]
public sealed class FluentDslGenerator : IIncrementalGenerator
{
    private static readonly DiagnosticDescriptor InvalidView = new(
        "DUXFD001", "Invalid Fluent DSL view", "{0}", "Duxel.FluentDsl", DiagnosticSeverity.Error, true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var parsed = context.AdditionalTextsProvider
            .Where(file => file.Path.EndsWith(".duxel.yaml", StringComparison.OrdinalIgnoreCase))
            .Select(static (file, cancellationToken) => Parse(file, cancellationToken));
        var typography = context.CompilationProvider.Select(static (compilation, _) =>
            compilation.GetTypeByMetadataName("Duxel.Core.Text")?.GetMembers()
                .OfType<IFieldSymbol>().Where(field => field.HasConstantValue).Select(field => field.Name)
                .ToImmutableArray() ?? []);
        var screens = parsed.Combine(typography).Select(static (input, _) => Compile(input.Left, input.Right));
        context.RegisterSourceOutput(screens.Collect(), static (output, files) => Emit(output, files));
    }

    private static ParsedFile Parse(AdditionalText file, CancellationToken cancellationToken)
    {
        var text = file.GetText(cancellationToken);
        if (text is null)
            return new(file.Path, SourceText.From(""), null, new(Mark.Empty, "Could not read the view file."));
        try
        {
            return new(file.Path, text, DslYaml.Parse(text.ToString()), null);
        }
        catch (YamlException error)
        {
            return new(file.Path, text, null, new(error.Start, error.Message));
        }
        catch (DslError error)
        {
            return new(file.Path, text, null, error);
        }
    }

    private static CompiledFile Compile(ParsedFile file, ImmutableArray<string> typography)
    {
        if (file.Error is { } parseError)
            return new(file.Path, file.Text, null, parseError);
        try
        {
            return new(file.Path, file.Text, new DslCompiler(typography).Compile(file.Document!), null);
        }
        catch (DslError error)
        {
            return new(file.Path, file.Text, null, error);
        }
    }

    private static void Emit(SourceProductionContext context, ImmutableArray<CompiledFile> files)
    {
        var hasErrors = false;
        foreach (var file in files)
        {
            if (file.Error is { } error)
            {
                Report(context, file, error.Mark, error.Message);
                hasErrors = true;
            }
        }

        var hasScreen = false;
        foreach (var group in files.Where(file => file.Screen is not null)
                     .GroupBy(file => file.Screen!.Name, StringComparer.OrdinalIgnoreCase))
        {
            var entries = group.ToArray();
            if (entries.Length != 1)
            {
                foreach (var file in entries)
                    Report(context, file, file.Screen!.Location, $"Screen '{group.Key}' is defined more than once.");
                hasErrors = true;
                continue;
            }
            context.AddSource(group.Key + ".FluentView.g.cs",
                SourceText.From(DslEmitter.Screen(entries[0].Screen!), Encoding.UTF8));
            hasScreen = true;
        }
        if (hasScreen)
            context.AddSource("Duxel.FluentDsl.CompiledView.g.cs", SourceText.From(DslEmitter.Host, Encoding.UTF8));
        // Edit and Continue also needs a compiler error when invalid YAML removes generated types.
        if (hasErrors)
            context.AddSource("Duxel.FluentDsl.BuildError.g.cs", SourceText.From(
                "#error A Fluent DSL view is invalid. See the DUXFD001 diagnostic in the YAML file.\n", Encoding.UTF8));
    }

    private static void Report(SourceProductionContext context, CompiledFile file, Mark mark, string message)
    {
        var position = (int)Math.Min(file.Text.Length, Math.Max(0L, mark.Index));
        var span = new TextSpan(position, position < file.Text.Length ? 1 : 0);
        var location = Location.Create(file.Path, span, file.Text.Lines.GetLinePositionSpan(span));
        context.ReportDiagnostic(Diagnostic.Create(InvalidView, location, message));
    }

    private sealed record ParsedFile(string Path, SourceText Text, DslMap? Document, DslError? Error);
    private sealed record CompiledFile(string Path, SourceText Text, DslScreen? Screen, DslError? Error);
}
