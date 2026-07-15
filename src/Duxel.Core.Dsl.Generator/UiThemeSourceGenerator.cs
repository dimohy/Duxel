using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Duxel.Core.Dsl.Generator;

[Generator(LanguageNames.CSharp)]
public sealed class UiThemeSourceGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var themeFiles = context.AdditionalTextsProvider
            .Where(file => file.Path.EndsWith(".duxel-theme", StringComparison.OrdinalIgnoreCase))
            .Select((file, ct) => new ThemeSourceFile(file.Path, file.GetText(ct)?.ToString() ?? string.Empty));

        var validThemes = themeFiles
            .Where(static file => !string.IsNullOrWhiteSpace(file.Content));

        // Generate a C# class for each theme
        context.RegisterSourceOutput(validThemes, (spc, item) =>
        {
            ThemeParser.ThemeDef def;
            try
            {
                def = ThemeParser.Parse(item.Content);
            }
            catch (Exception ex)
            {
                spc.ReportDiagnostic(Diagnostic.Create(
                    new DiagnosticDescriptor(
                        "DUXTHM001",
                        "Theme parse error",
                        "{0}",
                        "UI.Theme",
                        DiagnosticSeverity.Error,
                        true),
                    Location.None,
                    ex.Message));
                return;
            }

            var propertyName = SanitizeIdentifier(def.Name);
            var source = ThemeCompiler.GenerateCSharp("Duxel.Generated.Themes", propertyName, def);
            spc.AddSource($"Theme_{propertyName}.g.cs", SourceText.From(source, Encoding.UTF8));
        });

        // Generate theme registry and auto-registration
        var registryItems = validThemes
            .Select(static (file, _) =>
            {
                try
                {
                    var def = ThemeParser.Parse(file.Content);
                    return new ThemeRegistryItem(file.Path, SanitizeIdentifier(def.Name));
                }
                catch
                {
                    return default;
                }
            })
            .Where(static item => item.PropertyName is not null)
            .Collect();

        context.RegisterSourceOutput(registryItems, (spc, items) =>
        {
            if (items.IsDefaultOrEmpty)
            {
                return;
            }

            var source = ThemeAutoEmitter.Emit(items);
            spc.AddSource("UiThemeAuto.g.cs", SourceText.From(source, Encoding.UTF8));
        });
    }

    private static string SanitizeIdentifier(string name)
    {
        var sb = new StringBuilder(name.Length);
        foreach (var ch in name)
        {
            if (char.IsLetterOrDigit(ch) || ch is '_')
            {
                sb.Append(ch);
            }
        }

        if (sb.Length is 0 || char.IsDigit(sb[0]))
        {
            sb.Insert(0, '_');
        }

        return sb.ToString();
    }
}

internal readonly record struct ThemeSourceFile(string Path, string Content);
internal readonly record struct ThemeRegistryItem(string Path, string PropertyName);

/// <summary>
/// Minimal theme parser for the source generator (mirror of UiThemeParser in Duxel.Core).
/// </summary>
internal static class ThemeParser
{
    internal sealed class ThemeDef
    {
        public string Name { get; init; } = "Theme";
        public string? BasePreset { get; init; }
        public List<ThemeColorEntry> Overrides { get; init; } = [];
        public List<ThemeDesignTokenEntry> DesignTokenOverrides { get; init; } = [];
    }

    internal readonly record struct ThemeColorEntry(string ColorName, uint PackedRgba);
    internal readonly record struct ThemeDesignTokenEntry(string TokenName, float Value);

    private static readonly HashSet<string> DesignTokenNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "WindowCornerRadius",
        "ControlCornerRadius",
        "ControlBorderWidth",
        "ControlPressedOffsetY",
        "InputCornerRadius",
        "ToggleCornerRadius",
        "ProgressCornerRadius",
        "FocusRingThickness",
    };

    public static ThemeDef Parse(string text)
    {
        string? name = null;
        string? basePreset = null;
        var overrides = new List<ThemeColorEntry>();
        var designTokenOverrides = new List<ThemeDesignTokenEntry>();

        using var reader = new StringReader(text);
        var lineNumber = 0;
        while (reader.ReadLine() is { } rawLine)
        {
            lineNumber++;
            var line = rawLine.Trim();

            if (line.Length is 0 || line.StartsWith("//") || (line[0] is '#' && !line.StartsWith("#:")))
            {
                continue;
            }

            if (line.StartsWith("Theme", StringComparison.OrdinalIgnoreCase))
            {
                ParseHeader(line, lineNumber, out name, out basePreset);
                continue;
            }

            var eqIdx = line.IndexOf('=');
            if (eqIdx <= 0)
            {
                throw new FormatException($"Line {lineNumber}: expected 'ColorName = #HexValue'.");
            }

            var nameToken = line.Substring(0, eqIdx).Trim();
            var valueToken = line.Substring(eqIdx + 1).Trim();

            if (valueToken.StartsWith("#", StringComparison.Ordinal))
            {
                if (!TryParseHexColor(valueToken, out var packed))
                {
                    throw new FormatException($"Line {lineNumber}: invalid hex color '{valueToken}'.");
                }

                overrides.Add(new ThemeColorEntry(nameToken, packed));
                continue;
            }

            if (!TryParseDesignTokenName(nameToken, out var tokenName))
            {
                throw new FormatException($"Line {lineNumber}: unknown design token '{nameToken}'. Use Design.<TokenName> for shape tokens.");
            }

            if (!float.TryParse(valueToken, NumberStyles.Float, CultureInfo.InvariantCulture, out var tokenValue))
            {
                throw new FormatException($"Line {lineNumber}: invalid design token value '{valueToken}'.");
            }

            designTokenOverrides.Add(new ThemeDesignTokenEntry(tokenName, tokenValue));
        }

        if (name is null)
        {
            throw new FormatException("Missing 'Theme \"Name\"' header.");
        }

        return new ThemeDef { Name = name, BasePreset = basePreset, Overrides = overrides, DesignTokenOverrides = designTokenOverrides };
    }

    private static void ParseHeader(string line, int lineNumber, out string name, out string? basePreset)
    {
        var afterTheme = line.Substring(5).Trim();
        var firstQuote = afterTheme.IndexOf('"');
        if (firstQuote < 0)
        {
            throw new FormatException($"Line {lineNumber}: Theme name must be quoted.");
        }

        var afterOpen = afterTheme.Substring(firstQuote + 1);
        var closeQuote = afterOpen.IndexOf('"');
        if (closeQuote < 0)
        {
            throw new FormatException($"Line {lineNumber}: missing closing quote.");
        }

        name = afterOpen.Substring(0, closeQuote);
        var rest = afterOpen.Substring(closeQuote + 1).Trim();

        if (rest.Length is 0)
        {
            basePreset = null;
            return;
        }

        if (rest[0] is not ':')
        {
            throw new FormatException($"Line {lineNumber}: expected ':' for base preset.");
        }

        basePreset = rest.Substring(1).Trim();
        if (basePreset.Length is 0)
        {
            throw new FormatException($"Line {lineNumber}: missing preset name after ':'.");
        }
    }

    private static bool TryParseDesignTokenName(string value, out string tokenName)
    {
        const string designPrefix = "Design.";
        tokenName = value.StartsWith(designPrefix, StringComparison.OrdinalIgnoreCase)
            ? value.Substring(designPrefix.Length)
            : value;
        return DesignTokenNames.Contains(tokenName);
    }

    /// <summary>
    /// Parses #RRGGBB or #AARRGGBB to packed ABGR uint (matching UiColor(uint Rgba) layout).
    /// </summary>
    private static bool TryParseHexColor(string value, out uint packed)
    {
        packed = 0;
        if (value.Length is 0 || value[0] is not '#')
        {
            return false;
        }

        var hex = value.Substring(1);

        if (hex.Length is 6)
        {
            if (!uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb))
            {
                return false;
            }

            var r = (byte)((rgb >> 16) & 0xFF);
            var g = (byte)((rgb >> 8) & 0xFF);
            var b = (byte)(rgb & 0xFF);
            packed = unchecked((uint)(0xFF << 24)) | ((uint)b << 16) | ((uint)g << 8) | r;
            return true;
        }

        if (hex.Length is 8)
        {
            if (!uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var argb))
            {
                return false;
            }

            var a = (byte)((argb >> 24) & 0xFF);
            var r = (byte)((argb >> 16) & 0xFF);
            var g = (byte)((argb >> 8) & 0xFF);
            var b = (byte)(argb & 0xFF);
            packed = ((uint)a << 24) | ((uint)b << 16) | ((uint)g << 8) | r;
            return true;
        }

        return false;
    }
}

/// <summary>
/// Generates C# source code for a theme definition.
/// </summary>
internal static class ThemeCompiler
{
    public static string GenerateCSharp(string @namespace, string propertyName, ThemeParser.ThemeDef def)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("using Duxel.Core;");
        sb.AppendLine();
        sb.AppendLine($"namespace {@namespace};");
        sb.AppendLine();
        sb.AppendLine("public static partial class Themes");
        sb.AppendLine("{");
        sb.AppendLine($"    public static global::Duxel.Core.UiTheme {propertyName}");
        sb.AppendLine("    {");
        sb.AppendLine("        get");
        sb.AppendLine("        {");

        var baseLine = def.BasePreset?.ToLowerInvariant() switch
        {
            null or "" or "windows11" or "win11" => "var theme = global::Duxel.Core.UiWindows11Design.CreateTheme();",
            "windows11dark" or "win11dark" => "var theme = global::Duxel.Core.UiWindows11Design.CreateDarkTheme();",
            "dark" => "var theme = global::Duxel.Core.UiTheme.ImGuiDark;",
            "light" => "var theme = global::Duxel.Core.UiTheme.ImGuiLight;",
            "classic" => "var theme = global::Duxel.Core.UiTheme.ImGuiClassic;",
            var preset => throw new InvalidOperationException($"Unknown theme base preset: {preset}."),
        };
        sb.Append("            ").AppendLine(baseLine);

        foreach (var entry in def.Overrides)
        {
            sb.Append("            theme[global::Duxel.Core.UiStyleColor.")
              .Append(entry.ColorName)
              .Append("] = new global::Duxel.Core.UiColor(0x")
              .Append(entry.PackedRgba.ToString("X8"))
              .AppendLine(");");
        }

        sb.AppendLine("            return theme;");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine($"    public static global::Duxel.Core.UiCompiledDesign {propertyName}Design");
        sb.AppendLine("    {");
        sb.AppendLine("        get");
        sb.AppendLine("        {");
        sb.AppendLine("            var design = \"" + EscapeString(def.BasePreset ?? string.Empty) + "\".ToLowerInvariant() switch");
        sb.AppendLine("        {");
        sb.AppendLine($"            \"\" or \"windows11\" or \"win11\" => global::Duxel.Core.UiCompiledDesign.Windows11 with {{ Theme = {propertyName} }},");
        sb.AppendLine($"            \"windows11dark\" or \"win11dark\" => global::Duxel.Core.UiCompiledDesign.Windows11Dark with {{ Theme = {propertyName} }},");
        sb.AppendLine($"            \"dark\" or \"light\" or \"classic\" => new global::Duxel.Core.UiCompiledDesign({propertyName}, global::Duxel.Core.UiStyle.Default, global::Duxel.Core.UiDesignTokens.Default),");
        sb.AppendLine("            var preset => throw new global::System.InvalidOperationException($\"Unknown theme base preset: {preset}.\"),");
        sb.AppendLine("        };");
        sb.AppendLine("            var tokens = design.Tokens;");
        foreach (var entry in def.DesignTokenOverrides)
        {
            sb.Append("            tokens = tokens with { ")
              .Append(entry.TokenName)
              .Append(" = ")
              .Append(FormatFloat(entry.Value))
              .AppendLine("f };");
        }
        sb.AppendLine("            return design with { Tokens = tokens };");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string EscapeString(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static string FormatFloat(float value) => value.ToString("0.########", CultureInfo.InvariantCulture);
}

/// <summary>
/// Emits theme auto-registration source code.
/// </summary>
internal static class ThemeAutoEmitter
{
    public static string Emit(ImmutableArray<ThemeRegistryItem> items)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("using System;");
        sb.AppendLine("using System.IO;");
        sb.AppendLine("using System.Runtime.CompilerServices;");
        sb.AppendLine();
        sb.AppendLine("namespace Duxel.Generated.Themes;");
        sb.AppendLine();
        sb.AppendLine("internal static class UiThemeAutoRegistration");
        sb.AppendLine("{");
        sb.AppendLine("    [ModuleInitializer]");
        sb.AppendLine("    internal static void Init()");
        sb.AppendLine("    {");
        sb.AppendLine("#if DUX_NATIVEAOT");
        sb.AppendLine("        global::Duxel.Core.Dsl.UiThemeAuto.RegisterResolver(ResolveAot);");
        sb.AppendLine("#else");
        sb.AppendLine("        global::Duxel.Core.Dsl.UiThemeAuto.RegisterResolver(ResolveRuntime);");
        sb.AppendLine("#endif");
        sb.AppendLine("    }");
        sb.AppendLine();

        // NativeAOT resolver
        sb.AppendLine("    private static global::Duxel.Core.UiTheme ResolveAot(string themeFile)");
        sb.AppendLine("    {");
        sb.AppendLine("        var fileName = global::System.IO.Path.GetFileName(themeFile);");
        sb.AppendLine("        return fileName switch");
        sb.AppendLine("        {");
        for (var i = 0; i < items.Length; i++)
        {
            var fileName = Path.GetFileName(items[i].Path);
            var propertyName = items[i].PropertyName;
            sb.Append("            \"").Append(fileName).Append("\" => global::Duxel.Generated.Themes.Themes.")
              .Append(propertyName).AppendLine(",");
        }
        sb.AppendLine("            _ => throw new global::System.InvalidOperationException($\"Unknown theme file: {fileName}.\")");
        sb.AppendLine("        };");
        sb.AppendLine("    }");
        sb.AppendLine();

        // Runtime resolver (hot-reload handled by UiDslScreen, this provides initial load)
        sb.AppendLine("    private static global::Duxel.Core.UiTheme ResolveRuntime(string themeFile)");
        sb.AppendLine("    {");
        sb.AppendLine("        var fileName = global::System.IO.Path.GetFileName(themeFile);");
        sb.AppendLine("        var relative = global::System.IO.Path.Combine(\"Ui\", fileName);");
        sb.AppendLine("        var themePath = global::Duxel.Core.Dsl.UiDslSourcePathResolver.ResolveFromProjectRoot(relative);");
        sb.AppendLine("        var text = global::System.IO.File.ReadAllText(themePath);");
        sb.AppendLine("        var def = global::Duxel.Core.Dsl.UiThemeParser.Parse(text);");
        sb.AppendLine("        return global::Duxel.Core.Dsl.UiThemeCompiler.Apply(def);");
        sb.AppendLine("    }");

        sb.AppendLine("}");
        return sb.ToString();
    }
}
