using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;

namespace Duxel.Core.Dsl.Generator;

internal sealed class FluentCatalogRequirements
{
    internal readonly string[] Names;
    internal readonly string[] AllowedNames;
    internal readonly bool Complete;
    private FluentCatalogRequirements(string[] names, string[] allowedNames, bool complete)
    { Names = names; AllowedNames = allowedNames; Complete = complete; }

    internal static bool TryRead(string? text, out FluentCatalogRequirements? catalog, out string error)
    {
        catalog = null;
        error = "FluentComponents.tsv must exist and contain an explicit draft/complete scope and referenced component names.";
        if (text is null) return false;
        bool? complete = null;
        var names = new HashSet<string>(StringComparer.Ordinal);
        var rows = new HashSet<string>(StringComparer.Ordinal);
        var componentReferences = new HashSet<string>(StringComparer.Ordinal);
        var variants = new List<(string Name, string Reference)>();
        var lines = text.Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index].TrimEnd('\r');
            if (line.Length == 0) continue;
            if (line.StartsWith("# Scope:", StringComparison.Ordinal))
            {
                if (complete.HasValue || line != "# Scope: draft" && line != "# Scope: complete") return false;
                complete = line == "# Scope: complete";
                continue;
            }
            if (line.StartsWith("#", StringComparison.Ordinal)) continue;
            var fields = line.Split('\t');
            if (fields.Length != 5 || fields.Any(string.IsNullOrWhiteSpace)
                || !SyntaxFacts.IsValidIdentifier(fields[0])
                || fields[1] is not ("component" or "pattern" or "variant" or "infrastructure" or "legacy")
                || !OfficialReference(fields[4]) || !rows.Add(line))
            {
                error = "FluentComponents.tsv line " + (index + 1) + " requires five valid fields, an official HTTPS reference and no duplicate row.";
                return false;
            }
            if (fields[1] is "component" or "pattern" or "variant")
            {
                var reference = new Uri(fields[4]);
                if (reference.Host != "fluent2.microsoft.design" || !reference.AbsolutePath.StartsWith("/components/", StringComparison.Ordinal))
                {
                    error = "Implementation requirements must reference the Fluent 2 site's component guidance; repository APIs are supporting evidence only.";
                    return false;
                }
                if (fields[1] == "variant") variants.Add((fields[0], fields[4]));
                else { names.Add(fields[0]); componentReferences.Add(fields[4]); }
            }
        }
        if (!complete.HasValue || names.Count == 0) return false;
        if (variants.Any(variant => !componentReferences.Contains(variant.Reference)))
        {
            error = "A variant must reference its parent component's guidance in the same catalog.";
            return false;
        }
        catalog = new(names.OrderBy(name => name, StringComparer.Ordinal).ToArray(),
            names.Concat(variants.Select(variant => variant.Name)).Distinct(StringComparer.Ordinal).OrderBy(name => name, StringComparer.Ordinal).ToArray(), complete.Value);
        return true;
    }

    private static bool OfficialReference(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.UserInfo.Length != 0) return false;
        return uri.Host == "fluent2.microsoft.design" || uri.Host == "learn.microsoft.com"
            || uri.Host == "raw.githubusercontent.com"
                && (uri.AbsolutePath.StartsWith("/microsoft/fluentui/", StringComparison.Ordinal)
                    || uri.AbsolutePath.StartsWith("/microsoft/fluentui-android/", StringComparison.Ordinal)
                    || uri.AbsolutePath.StartsWith("/microsoft/fluentui-apple/", StringComparison.Ordinal));
    }
}
