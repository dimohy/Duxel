using System;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Duxel.Core.Dsl.Generator;

[Generator]
public sealed class UiComponentCatalogSourceGenerator : IIncrementalGenerator
{
    private static readonly DiagnosticDescriptor Invalid = new("DUXWGT005", "Invalid component discovery declaration",
        "{0}", "UI.WidgetTheme", DiagnosticSeverity.Error, true);
    private static readonly DiagnosticDescriptor InvalidCatalog = new("DUXWGT006", "Invalid official component catalog",
        "{0}", "UI.WidgetTheme", DiagnosticSeverity.Error, true);
    private static readonly DiagnosticDescriptor IncompleteCatalog = new("DUXWGT007", "Incomplete official component factories",
        "{0}", "UI.WidgetTheme", DiagnosticSeverity.Error, true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var components = context.SyntaxProvider.ForAttributeWithMetadataName("Duxel.Core.UiComponentAttribute",
            static (node, _) => node is ClassDeclarationSyntax,
            static (attribute, _) => attribute);
        var files = context.AdditionalTextsProvider.Where(static file =>
            string.Equals(System.IO.Path.GetFileName(file.Path), "FluentComponents.tsv", StringComparison.OrdinalIgnoreCase))
            .Select(static (file, token) => file.GetText(token)?.ToString()).Collect();
        var strict = context.AnalyzerConfigOptionsProvider.Select(static (options, _) =>
            options.GlobalOptions.TryGetValue("build_property.DuxelRequireCompleteFluentCatalog", out var value)
            && string.Equals(value, "true", StringComparison.OrdinalIgnoreCase));
        var owner = context.CompilationProvider.Select(static (compilation, _) =>
            compilation.Assembly.GetTypeByMetadataName("Duxel.Core.UiComponentAttribute") is not null);
        context.RegisterSourceOutput(components.Collect().Combine(files).Combine(strict).Combine(owner), static (production, input) =>
        {
            if (input.Right) Emit(production, input.Left.Left.Left, input.Left.Left.Right, input.Left.Right);
        });
    }

    private static void Emit(SourceProductionContext context, ImmutableArray<GeneratorAttributeSyntaxContext> declarations,
        ImmutableArray<string?> files, bool strict)
    {
        if (!FluentCatalogRequirements.TryRead(files.Length == 1 ? files[0] : null, out var requirements, out var error))
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidCatalog, Location.None, error));
            return;
        }
        var requiredNames = new System.Collections.Generic.HashSet<string>(requirements!.AllowedNames, StringComparer.Ordinal);
        var valid = true;
        var names = new System.Collections.Generic.Dictionary<string,int>(StringComparer.Ordinal);
        var signatures = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
        var exposed = new System.Collections.Generic.List<(string Name, INamedTypeSymbol Type,INamedTypeSymbol Implementation)>();
        foreach (var declaration in declarations.OrderBy(item => item.TargetSymbol.ToDisplayString(), StringComparer.Ordinal))
        {
            var arguments = declaration.Attributes[0].ConstructorArguments;
            var type = (INamedTypeSymbol)declaration.TargetSymbol;
            var location = declaration.TargetNode.GetLocation();
            if (arguments.Length != 2 || arguments[0].Value is not string name
                || !SyntaxFacts.IsValidIdentifier(name) || name is "ComponentNames" or "__componentNames" or "Fluent"
                    or "RequiredComponentNames" or "MissingComponentNames" or "IsCatalogScopeComplete" or "__CatalogAudit"
                || arguments[1].Value is not int exposure || exposure < 0 || exposure > 2
                || names.TryGetValue(name,out var existingExposure) && (exposure != 0 || existingExposure != 0))
            {
                context.ReportDiagnostic(Diagnostic.Create(Invalid, location, "Use a unique component identifier and a known exposure value."));
                valid = false;
                continue;
            }
            names[name]=exposure;
            if (exposure != 0) continue;
            if (!requiredNames.Contains(name))
            {
                context.ReportDiagnostic(Diagnostic.Create(Invalid, location, "A standard component must have an official component reference in FluentComponents.tsv."));
                valid = false;
                continue;
            }
            if (type.Arity != 0 || type.ContainingType is not null || type.IsAbstract || type.IsStatic || type.DeclaredAccessibility != Accessibility.Public
                || !CompiledWidgetContract.TryResolve(type,declaration.SemanticModel.Compilation,out var implementation,out _))
            {
                context.ReportDiagnostic(Diagnostic.Create(Invalid, location, "A visible component requires a supported, public, compiled widget adapter."));
                valid = false;
                continue;
            }
            foreach(var constructor in implementation!.InstanceConstructors.Where(ctor=>ctor.DeclaredAccessibility==Accessibility.Public))
                if(!signatures.Add(name+"("+CompiledWidgetContract.Signature(constructor)+")"))
                {
                    context.ReportDiagnostic(Diagnostic.Create(Invalid,location,"Component factory overloads must have distinct parameter types."));
                    valid=false;
                }
            exposed.Add((name, type,implementation));
        }
        if (!valid) return;
        var source = new StringBuilder("// <auto-generated/>\n#nullable enable\nnamespace Duxel.Core;\npublic static partial class Fluent\n{\n");
        source.Append("    private static readonly string[] __componentNames = new string[] { ");
        foreach (var name in exposed.Select(item=>item.Name).Distinct(StringComparer.Ordinal).OrderBy(name=>name,StringComparer.Ordinal))
            source.Append(SymbolDisplay.FormatLiteral(name, true)).Append(", ");
        source.Append("};\n    public static global::System.ReadOnlySpan<string> ComponentNames => __componentNames;\n");
        var available = new System.Collections.Generic.HashSet<string>(exposed.Select(item => item.Name), StringComparer.Ordinal);
        var missing = requirements.Names.Where(name => !available.Contains(name)).ToArray();
        // Diagnostic metadata initializes only if explicitly queried, not during normal widget creation.
        source.Append("    private static class __CatalogAudit\n    {\n        static __CatalogAudit() { }\n        internal static readonly string[] Required = new string[] { ");
        foreach (var name in requirements.Names) source.Append(SymbolDisplay.FormatLiteral(name, true)).Append(", ");
        source.Append("};\n        internal static readonly string[] Missing = new string[] { ");
        foreach (var name in missing) source.Append(SymbolDisplay.FormatLiteral(name, true)).Append(", ");
        source.Append("};\n    }\n    public static global::System.ReadOnlySpan<string> RequiredComponentNames => __CatalogAudit.Required;\n")
            .Append("    public static global::System.ReadOnlySpan<string> MissingComponentNames => __CatalogAudit.Missing;\n")
            .Append("    public static bool IsCatalogScopeComplete => ").Append(requirements.Complete ? "true" : "false").Append(";\n");
        foreach (var entry in exposed.OrderBy(item => item.Name, StringComparer.Ordinal))
        {
            var typeName = entry.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            foreach(var constructor in entry.Implementation.InstanceConstructors.Where(ctor=>ctor.DeclaredAccessibility==Accessibility.Public))
                source.Append("    public static ").Append(typeName).Append(" @").Append(entry.Name)
                    .Append('(').Append(CompiledWidgetContract.Parameters(constructor)).Append(") => new ")
                    .Append(typeName).Append('(').Append(CompiledWidgetContract.Arguments(constructor)).Append(");\n");
        }
        source.Append("}\n");
        context.AddSource("Fluent.ComponentCatalog.g.cs", SourceText.From(source.ToString(), Encoding.UTF8));
        var missingVariants = requirements.AllowedNames.Except(requirements.Names, StringComparer.Ordinal).Count(name => !available.Contains(name));
        if (strict && (!requirements.Complete || missing.Length != 0 || missingVariants != 0))
            context.ReportDiagnostic(Diagnostic.Create(IncompleteCatalog, Location.None,
                "Missing referenced factories: " + missing.Length + "; missing variants: " + missingVariants + "; source scope: " + (requirements.Complete ? "complete" : "draft")
                + ". This checks factory declarations only; visual and behavioral conformance require separate verification."));
    }
}
