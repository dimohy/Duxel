using System.Reflection;
using System.Runtime.Loader;
using System.Runtime.InteropServices;
using System.Text.Json;
using Duxel.Core;
using Duxel.Core.Dsl.Generator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

internal static class GeneratorTestRunner
{
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    internal static void Run(string[] args)
    {
        var output = Path.GetFullPath(args.Length > 0 ? args[0] : "artifacts/widget-generators");
        Directory.CreateDirectory(output);
        var checks = new List<string>();
        void Check(bool value, string name)
        {
            if (!value)
                throw new InvalidOperationException(name);
            checks.Add(name);
            Console.WriteLine("PASS " + name);
        }
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Append(typeof(UiWidgetThemeAttribute).Assembly.Location).Distinct().Select(p => MetadataReference.CreateFromFile(p)).ToArray();
        var parseOptions = new CSharpParseOptions(LanguageVersion.Preview);
        (Compilation Compilation, GeneratorDriverRunResult Result) Generate(string source)
        {
            var compilation = CSharpCompilation.Create("WidgetThemeConsumer" + Guid.NewGuid().ToString("N"),
                [CSharpSyntaxTree.ParseText(source, parseOptions)], references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            GeneratorDriver driver = CSharpGeneratorDriver.Create([new UiWidgetThemeSourceGenerator().AsSourceGenerator()], parseOptions: parseOptions);
            driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var updated, out _);
            return (updated, driver.GetRunResult());
        }
        var positive = """
        using Duxel.Core;
        namespace Consumer;
        [UiWidgetTheme(UiWidgetPreset.Material3Expressive, ButtonActivation=UiButtonActivation.Press,
            ButtonMinimumHeight=44, ButtonCornerRadius=18, DarkColors=true)]
        public readonly partial struct ProductDesign { }
        public static class Probe { public static UiCompiledDesign Get() => ProductDesign.Create(); }
        """;
        var generated = Generate(positive);
        Check(!generated.Result.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error), "valid widget theme has no generator errors");
        var errors = generated.Compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
        if (errors.Length != 0)
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors.Select(e => e.ToString())));
        Check(true, "generated design compiles as an IUiDesign consumer");
        var text = generated.Result.GeneratedTrees.Single().GetText().ToString();
        File.WriteAllText(Path.Combine(output, "ProductDesign.WidgetTheme.g.cs"), text);
        Check(text.Contains("static readonly") && !text.Contains("switch") && !text.Contains("Parse(") && !text.Contains("Reflection"),
            "generated theme uses direct static configuration without runtime selection,parsing or reflection");
        using (var stream = new MemoryStream())
        {
            var emitted = generated.Compilation.Emit(stream);
            if (!emitted.Success)
                throw new InvalidOperationException(string.Join(Environment.NewLine, emitted.Diagnostics));
            stream.Position = 0;
            var assembly = AssemblyLoadContext.Default.LoadFromStream(stream);
            var design = (UiCompiledDesign)assembly.GetType("Consumer.Probe")!.GetMethod("Get", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, null)!;
            Check(design.ControlSkins.Button.Activation == UiButtonActivation.Press
                && design.ControlSkins.Button.MinimumHeight == 44 && design.ControlSkins.Button.CornerRadius == 18,
                "generated constants execute as the selected widget behavior and geometry");
        }
        var again = Generate(positive);
        Check(text == again.Result.GeneratedTrees.Single().GetText().ToString(), "same source produces deterministic generated design text");
        var unrelated = Generate(positive + "\npublic class Unrelated { public int Value => 7; }");
        Check(text == unrelated.Result.GeneratedTrees.Single().GetText().ToString(), "unrelated source changes preserve generated theme text");
        foreach (var item in new[]{
            ("nonpartial","[UiWidgetTheme(UiWidgetPreset.Windows11)] public struct Bad {}","DUXWGT001"),
            ("generic","[UiWidgetTheme(UiWidgetPreset.Windows11)] public partial struct Bad<T> {}","DUXWGT001"),
            ("record","[UiWidgetTheme(UiWidgetPreset.Windows11)] public partial record struct Bad;","DUXWGT001"),
            ("nested","public class Outer { [UiWidgetTheme(UiWidgetPreset.Windows11)] public partial struct Bad {} }","DUXWGT001"),
            ("existing-create","[UiWidgetTheme(UiWidgetPreset.Windows11)] public partial struct Bad { public static int Create()=>1; }","DUXWGT001"),
            ("negative","[UiWidgetTheme(UiWidgetPreset.Windows11,ButtonMinimumHeight=-1)] public partial struct Bad {}","DUXWGT002"),
            ("nan","[UiWidgetTheme(UiWidgetPreset.Windows11,ButtonCornerRadius=float.NaN)] public partial struct Bad {}","DUXWGT002"),
            ("infinite","[UiWidgetTheme(UiWidgetPreset.Windows11,ButtonPaddingX=float.PositiveInfinity)] public partial struct Bad {}","DUXWGT002"),
            ("unknown-preset","[UiWidgetTheme((UiWidgetPreset)99)] public partial struct Bad {}","DUXWGT002"),
            ("unknown-activation","[UiWidgetTheme(UiWidgetPreset.Windows11,ButtonActivation=(UiButtonActivation)99)] public partial struct Bad {}","DUXWGT002")})
        {
            var invalid = Generate("using Duxel.Core;\n" + item.Item2);
            Check(invalid.Result.Diagnostics.Any(d => d.Id == item.Item3 && d.Severity == DiagnosticSeverity.Error)
                && invalid.Result.GeneratedTrees.Length == 0, "invalid " + item.Item1 + " theme fails before consumer execution");
        }
        (Compilation Compilation, GeneratorDriverRunResult Result) GenerateButton(string source)
        {
            var compilation = CSharpCompilation.Create("ButtonConsumer" + Guid.NewGuid().ToString("N"),
                [CSharpSyntaxTree.ParseText(source, parseOptions)], references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            GeneratorDriver driver = CSharpGeneratorDriver.Create([new UiButtonStyleSourceGenerator().AsSourceGenerator()], parseOptions: parseOptions);
            driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var updated, out _);
            return (updated, driver.GetRunResult());
        }
        const string buttonSource = """
        using Duxel.Core;
        namespace Consumer;
        [UiButtonStyle(typeof(UiReleaseButtonBehavior),typeof(UiFluentButtonPresentation))]
        public partial class FluentButton { }
        public sealed class SaveButton:FluentButton
        {
            public SaveButton():base("Save"){}
            protected override void OnActivated(){}
        }
        public static class ButtonProbe
        {
            public static bool IsCompiled()=>new SaveButton() is UiButton<UiReleaseButtonBehavior,UiFluentButtonPresentation>;
        }
        """;
        var compiledButton = GenerateButton(buttonSource);
        var buttonErrors = compiledButton.Compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
        Check(buttonErrors.Length == 0, "generated button binds concrete behavior and presentation and remains inheritable: " + string.Join(";", buttonErrors.Select(d => d.ToString())));
        var buttonText = compiledButton.Result.GeneratedTrees.Single().GetText().ToString();
        File.WriteAllText(Path.Combine(output, "FluentButton.ButtonStyle.g.cs"), buttonText);
        Check(buttonText.Contains("UiButton<global::Duxel.Core.UiReleaseButtonBehavior, global::Duxel.Core.UiFluentButtonPresentation>")
            && !buttonText.Contains("typeof(") && !buttonText.Contains("switch") && !buttonText.Contains("Activator"),
            "generated implementation binding is direct generic code without runtime type lookup");
        using (var stream = new MemoryStream())
        {
            var emitted = compiledButton.Compilation.Emit(stream);
            Check(emitted.Success, "compiled button consumer emits successfully");
            stream.Position = 0;
            var assembly = AssemblyLoadContext.Default.LoadFromStream(stream);
            Check((bool)assembly.GetType("Consumer.ButtonProbe")!.GetMethod("IsCompiled")!.Invoke(null, null)!,
                "generated concrete binding executes through a user subclass");
        }
        Check(buttonText == GenerateButton(buttonSource).Result.GeneratedTrees.Single().GetText().ToString(),
            "compiled implementation output is deterministic");
        foreach (var invalid in new[]{
            ("target","[UiButtonStyle(typeof(UiReleaseButtonBehavior),typeof(UiFluentButtonPresentation))] public class Bad{}","DUXWGT003"),
            ("generic","[UiButtonStyle(typeof(UiReleaseButtonBehavior),typeof(UiFluentButtonPresentation))] public partial class Bad<T>{}","DUXWGT003"),
            ("base","[UiButtonStyle(typeof(UiReleaseButtonBehavior),typeof(UiFluentButtonPresentation))] public partial class Bad:UiButton{public Bad():base(\"x\") {}}","DUXWGT003"),
            ("implementation","[UiButtonStyle(typeof(string),typeof(UiFluentButtonPresentation))] public partial class Bad{}","DUXWGT004"),
            ("swapped","[UiButtonStyle(typeof(UiFluentButtonPresentation),typeof(UiReleaseButtonBehavior))] public partial class Bad{}","DUXWGT004"),
            ("abstract","public abstract class Policy:IUiButtonBehavior{public static UiButtonInteraction Process(UiControlContext c,string i,UiRect b)=>default;} [UiButtonStyle(typeof(Policy),typeof(UiFluentButtonPresentation))] public partial class Bad{}","DUXWGT004")})
        {
            var result = GenerateButton("using Duxel.Core;" + invalid.Item2);
            Check(result.Result.Diagnostics.Any(d => d.Id == invalid.Item3) && result.Result.GeneratedTrees.Length == 0,
                "invalid compiled button " + invalid.Item1 + " is rejected before execution");
        }
        const string catalogContracts = """
        using System;
        namespace Duxel.Core
        {
            public enum UiComponentExposure { Fluent, Infrastructure, Legacy }
            public enum UiWidgetIdentity { Instance, Parent }
            [AttributeUsage(AttributeTargets.Class)]
            public sealed class UiComponentAttribute(string name,UiComponentExposure exposure):Attribute;
            [AttributeUsage(AttributeTargets.Class)]
            public sealed class UiButtonStyleAttribute:Attribute;
            [AttributeUsage(AttributeTargets.Class)]
            public sealed class UiWidgetStyleAttribute(Type widget,params Type[] arguments):Attribute;
            public abstract class UiWidget;
            public abstract class UiControlWidget:UiWidget;
            public class ButtonBase:UiControlWidget
            {
                public ButtonBase(string label,Action? activated,UiWidgetIdentity identity){}
            }
        }
        """;
        const string catalogManifest = "# Scope: complete\nButton\tcomponent\tReact\tstable\thttps://fluent2.microsoft.design/components/web/react/core/button/usage\nCheckbox\tcomponent\tReact\tstable\thttps://fluent2.microsoft.design/components/web/react/core/checkbox/usage\n";
        (Compilation Compilation, GeneratorDriverRunResult Result) GenerateCatalog(string components, string? manifest = catalogManifest, bool strict = false)
        {
            var catalogReferences = references.Where(reference => !string.Equals(reference.Display, typeof(UiWidgetThemeAttribute).Assembly.Location, StringComparison.OrdinalIgnoreCase)).ToArray();
            var compilation = CSharpCompilation.Create("CatalogConsumer" + Guid.NewGuid().ToString("N"),
                [CSharpSyntaxTree.ParseText(catalogContracts + components, parseOptions)], catalogReferences, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            GeneratorDriver driver = CSharpGeneratorDriver.Create([new UiComponentCatalogSourceGenerator().AsSourceGenerator()],
                additionalTexts: manifest is null ? [] : [new CatalogFile(manifest)], parseOptions: parseOptions,
                optionsProvider: new CatalogOptionsProvider(strict));
            driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var updated, out _);
            return (updated, driver.GetRunResult());
        }
        const string visibleComponent = """
        [Duxel.Core.UiComponent("Button",Duxel.Core.UiComponentExposure.Fluent)]
        [Duxel.Core.UiWidgetStyle(typeof(Duxel.Core.ButtonBase))]
        public class VisibleButton
        {
            public VisibleButton(string label,Action? activated,Duxel.Core.UiWidgetIdentity identity){}
        }
        """;
        const string preservedComponents = """
        [Duxel.Core.UiComponent("Preserved",Duxel.Core.UiComponentExposure.Legacy)] public class Preserved{}
        [Duxel.Core.UiComponent("Internal",Duxel.Core.UiComponentExposure.Infrastructure)] public class Internal{}
        """;
        var catalog = GenerateCatalog(visibleComponent + preservedComponents);
        Check(!catalog.Compilation.GetDiagnostics().Any(d => d.Severity == DiagnosticSeverity.Error), "generated component discovery and typed factory compile");
        var catalogText = catalog.Result.GeneratedTrees.Single().GetText().ToString();
        File.WriteAllText(Path.Combine(output, "Fluent.ComponentCatalog.g.cs"), catalogText);
        Check(catalogText.Contains("new global::VisibleButton(") && !catalogText.Contains("Preserved") && !catalogText.Contains("Internal"),
            "exposure attributes exclude legacy and infrastructure from both generated names and factories without deleting their types");
        Check(catalogText == GenerateCatalog(preservedComponents + visibleComponent).Result.GeneratedTrees.Single().GetText().ToString(),
            "catalog output is independent of source declaration order");
        foreach (var invalid in new[]{
            "[Duxel.Core.UiComponent(\"Bad\",(Duxel.Core.UiComponentExposure)99)] public class Bad{}",
            "[Duxel.Core.UiComponent(\"\",Duxel.Core.UiComponentExposure.Legacy)] public class Bad{}",
            "[Duxel.Core.UiComponent(\"Button\",Duxel.Core.UiComponentExposure.Legacy)] public class Bad{}",
            "[Duxel.Core.UiComponent(\"Unsupported\",Duxel.Core.UiComponentExposure.Fluent)] public class Bad{}",
            "[Duxel.Core.UiComponent(\"Abstract\",Duxel.Core.UiComponentExposure.Fluent)][Duxel.Core.UiButtonStyle] public abstract class Bad{}"})
        {
            var result = GenerateCatalog(visibleComponent + invalid);
            Check(result.Result.Diagnostics.Any(d => d.Id == "DUXWGT005") && result.Result.GeneratedTrees.Length == 0,
                "invalid or unsupported component discovery fails before exposing a catalog: " + invalid);
        }
        const string genericWidget = """
        using Duxel.Core;
        namespace Consumer;
        [UiWidgetStyle(typeof(UiToggle<,,,>),typeof(bool?),typeof(UiNullableToggleValue),typeof(UiReleaseButtonBehavior),typeof(UiFluentCheckboxPresentation))]
        public partial class MixedCheckbox;
        public sealed class CustomCheckbox:MixedCheckbox
        {
            public CustomCheckbox(UiBinding<bool?> value):base("Mixed",value){}
            protected override void OnChanged(bool? value)=>base.OnChanged(value);
        }
        public static class GenericProbe
        {
            public static bool Bound()=>new CustomCheckbox(new UiState<bool?>(null).Binding) is UiToggle<bool?,UiNullableToggleValue,UiReleaseButtonBehavior,UiFluentCheckboxPresentation>;
        }
        """;
        var generic = GenerateButton(genericWidget);
        Check(!generic.Compilation.GetDiagnostics().Any(d => d.Severity == DiagnosticSeverity.Error),
            "generic widget implementation and typed nullable binding constructor compile");
        var genericText = generic.Result.GeneratedTrees.Single().GetText().ToString();
        var radioConsumer = GenerateButton("""
        #nullable enable
        using Duxel.Core;
        [UiWidgetStyle(typeof(UiRadioGroup<,>),typeof(UiFluentRadioGroupBehavior),typeof(UiFluentRadioGroupPresentation<UiRadioHorizontalStackedLayout>))]
        public partial class ProductRadioGroup;
        public sealed class DerivedRadio:ProductRadioGroup
        {
            public DerivedRadio(UiBinding<string?> binding):base("Choice",binding,new UiRadioOption[]{new("first","First")}){}
            protected override void OnChanged(string value)=>base.OnChanged(value);
        }
        public static class RadioProbe
        {
            public static string Create(){var state=new UiState<string?>("first");using var widget=new DerivedRadio(state.Binding);return widget.Items[0].Value;}
        }
        """);
        Check(!radioConsumer.Compilation.GetDiagnostics().Any(d => d.Severity == DiagnosticSeverity.Error),
            "RadioGroup generated style forwards nullable binding and owned option memory into an inheritable closed implementation");
        var radioText = radioConsumer.Result.GeneratedTrees.Single().GetText().ToString();
        Check(radioText.Contains("ReadOnlyMemory<global::Duxel.Core.UiRadioOption>") && !radioText.Contains("switch") && !radioText.Contains("Dictionary"),
            "RadioGroup generated constructor directly binds its behavior/presentation without a runtime implementation registry");
        using (var stream = new MemoryStream())
        {
            Check(radioConsumer.Compilation.Emit(stream).Success, "RadioGroup source-generated consumer emits successfully");
            stream.Position = 0;
            var assembly = AssemblyLoadContext.Default.LoadFromStream(stream);
            Check((string)assembly.GetType("RadioProbe")!.GetMethod("Create")!.Invoke(null, null)! == "first",
                "actual generated RadioGroup consumer constructs its owned options and can be inherited");
        }
        File.WriteAllText(Path.Combine(output, "ProductRadioGroup.WidgetStyle.g.cs"), radioText);
        File.WriteAllText(Path.Combine(output, "MixedCheckbox.WidgetStyle.g.cs"), genericText);
        Check(genericText.Contains("UiNullableToggleValue") && genericText.Contains("UiBinding<bool?>") && !genericText.Contains("typeof(") && !genericText.Contains("switch"),
            "generic source binding forwards concrete value-policy and constructor types without runtime selection");
        using (var stream = new MemoryStream())
        {
            var emitted = generic.Compilation.Emit(stream);
            Check(emitted.Success, "generic implementation consumer emits successfully");
            stream.Position = 0;
            var assembly = AssemblyLoadContext.Default.LoadFromStream(stream);
            Check((bool)assembly.GetType("Consumer.GenericProbe")!.GetMethod("Bound")!.Invoke(null, null)!,
                "generic implementation executes through a user checkbox subclass");
        }
        foreach (var invalid in new[]{
            "[UiWidgetStyle(typeof(UiText<,,>),typeof(string),typeof(UiGetterTextSource),typeof(UiFluentTextPresentation))] public partial class Bad{}",
            "[UiWidgetStyle(typeof(UiText<,,>),typeof(System.Func<string>),typeof(UiStaticTextSource),typeof(UiFluentTextPresentation))] public partial class Bad{}",
            "[UiWidgetStyle(typeof(UiToggle<,,,>),typeof(bool),typeof(UiNullableToggleValue),typeof(UiReleaseButtonBehavior),typeof(UiFluentCheckboxPresentation))] public partial class Bad{}",
            "[UiWidgetStyle(typeof(UiToggle<,,,>),typeof(bool))] public partial class Bad{}",
            "[UiWidgetStyle(typeof(string))] public partial class Bad{}",
            "[UiWidgetStyle(typeof(UiToggle<,,,>),typeof(bool),typeof(UiBooleanToggleValue),typeof(UiFluentCheckboxPresentation),typeof(UiReleaseButtonBehavior))] public partial class Bad{}",
            "[UiWidgetStyle(typeof(UiToggle<,,,>),typeof(bool),typeof(UiBooleanToggleValue),typeof(UiReleaseButtonBehavior),typeof(System.Collections.Generic.List<>))] public partial class Bad{}"})
        {
            var result = GenerateButton("using Duxel.Core;" + invalid);
            Check(result.Result.Diagnostics.Any(d => d.Id == "DUXWGT004") && result.Result.GeneratedTrees.Length == 0,
                "invalid generic widget arguments or constraints are rejected: " + invalid);
        }
        Check(genericText == GenerateButton(genericWidget + "\npublic class Other{}").Result.GeneratedTrees.Single().GetText().ToString(),
            "unrelated changes preserve generic widget output");
        var referenceConstructor = GenerateButton("""
        using Duxel.Core;
        public class VectorWidget:UiControlWidget
        {
            public VectorWidget(ref readonly UiVector2 size,string caption="Vector"){Size=size;}
            public UiVector2 Size{get;}
            protected override void RenderControl(UiImmediateContext ui){}
        }
        [UiWidgetStyle(typeof(VectorWidget))] public partial class GeneratedVectorWidget;
        public static class VectorProbe
        {
            public static bool Preserved(){var size=new UiVector2(3,5);using var widget=new GeneratedVectorWidget(in size);return widget.Size==size;}
        }
        """);
        Check(!referenceConstructor.Compilation.GetDiagnostics().Any(d => d.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning),
            "constructor forwarding preserves readonly reference arguments and optional string defaults without warnings");
        const string overloads = """
        namespace Duxel.Core
        {
            public class ValueBase<T>:UiWidget { public ValueBase(T value){} }
        }
        [Duxel.Core.UiComponent("Checkbox",Duxel.Core.UiComponentExposure.Fluent)]
        [Duxel.Core.UiWidgetStyle(typeof(Duxel.Core.ValueBase<>),typeof(bool))]
        public class BoolValue { public BoolValue(bool value){} }
        [Duxel.Core.UiComponent("Checkbox",Duxel.Core.UiComponentExposure.Fluent)]
        [Duxel.Core.UiWidgetStyle(typeof(Duxel.Core.ValueBase<>),typeof(bool?))]
        public class MixedValue { public MixedValue(bool? value){} }
        """;
        var overloaded = GenerateCatalog(overloads);
        Check(!overloaded.Compilation.GetDiagnostics().Any(d => d.Severity == DiagnosticSeverity.Error),
            "distinct typed value variants generate valid overloaded component factories");
        var overloadedText = overloaded.Result.GeneratedTrees.Single().GetText().ToString();
        Check(overloadedText.Contains("@Checkbox(bool @value)") && overloadedText.Contains("@Checkbox(bool? @value)")
            && overloadedText.Contains("__componentNames = new string[] { \"Checkbox\", };"),
            "component discovery names deduplicate variants while preserving both typed factory overloads");
        var duplicate = GenerateCatalog(visibleComponent + visibleComponent.Replace("VisibleButton", "DuplicateButton"));
        Check(duplicate.Result.Diagnostics.Any(d => d.Id == "DUXWGT005") && duplicate.Result.GeneratedTrees.Length == 0,
            "identical factory signatures cannot silently shadow another standard component");
        var reserved = GenerateCatalog("[Duxel.Core.UiComponent(\"ComponentNames\",Duxel.Core.UiComponentExposure.Fluent)] public class Bad{}");
        Check(reserved.Result.Diagnostics.Any(d => d.Id == "DUXWGT005") && reserved.Result.GeneratedTrees.Length == 0,
            "reserved discovery member names fail with a component diagnostic");
        foreach (var invalidManifest in new string?[]{null,"",catalogManifest.Replace("# Scope: complete\n",""),
            catalogManifest.Replace("\tstable\t","\t"),catalogManifest.Replace("https://fluent2.microsoft.design","https://example.com"),
            catalogManifest+catalogManifest.Split('\n')[1]+"\n",catalogManifest+"# Scope: draft\n"})
        {
            var invalid = GenerateCatalog(visibleComponent, invalidManifest);
            Check(invalid.Result.Diagnostics.Any(d => d.Id == "DUXWGT006") && invalid.Result.GeneratedTrees.Length == 0,
                "missing, malformed, duplicate or unofficial reference data cannot silently generate a catalog");
        }
        var unknownStandard = GenerateCatalog(visibleComponent.Replace("\"Button\"", "\"UnknownStandard\""));
        var repositoryRequirement = catalogManifest + "AcrylicPane\tcomponent\tandroid-compose\tpinned-v2-source\thttps://raw.githubusercontent.com/microsoft/fluentui-android/40095bd360026497c0e92159bc307ac82103e655/AcrylicPane.kt\n";
        Check(GenerateCatalog(visibleComponent, repositoryRequirement).Result.Diagnostics.Any(d => d.Id == "DUXWGT006"),
            "repository-only APIs cannot expand the official-site component scope");
        var variantScope = catalogManifest + "CounterButton\tvariant\tReact\tparent-guidance\thttps://fluent2.microsoft.design/components/web/react/core/button/usage\n";
        var variantCatalog = GenerateCatalog(visibleComponent, variantScope);
        Check(!variantCatalog.Result.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error)
            && !variantCatalog.Result.GeneratedTrees.Single().GetText().ToString().Contains("\"CounterButton\""),
            "parented variants do not inflate top-level requirements or missing-factory counts");
        var variantFactory = GenerateCatalog(visibleComponent.Replace("\"Button\"", "\"CounterButton\""), variantScope);
        Check(!variantFactory.Result.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error)
            && !variantFactory.Compilation.GetDiagnostics().Any(d => d.Severity == DiagnosticSeverity.Error),
            "explicitly parented variants retain their statically generated factory path");
        var orphanVariant = catalogManifest + "Orphan\tvariant\tReact\tparent-guidance\thttps://fluent2.microsoft.design/components/web/react/core/unknown/usage\n";
        Check(GenerateCatalog(visibleComponent, orphanVariant).Result.Diagnostics.Any(d => d.Id == "DUXWGT006"),
            "a variant without matching parent guidance fails instead of silently entering discovery");
        using (var descriptor = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "FluentSiteComponents.json"))))
        {
            var root = descriptor.RootElement;
            var expected = root.GetProperty("components").EnumerateArray().Concat(root.GetProperty("variants").EnumerateArray())
                .Select(row => string.Join('\t', row.EnumerateArray().Select(field => field.GetString()))).ToHashSet(StringComparer.Ordinal);
            var actual = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "Fixtures", "FluentComponents.tsv"))
                .Where(row => row.Length > 0 && !row.StartsWith('#')).ToHashSet(StringComparer.Ordinal);
            Check(actual.SetEquals(expected), "production requirements exactly match reviewed official-site components and parented variants");
            Check(root.GetProperty("components").GetArrayLength() == 64
                && root.GetProperty("components").EnumerateArray().Select(row => row[0].GetString()).Distinct().Count() == 55,
                "reviewed web47/iOS12/Android5 catalog entries have55 distinct top-level names");
            Check(!actual.Any(row => row.Contains("raw.githubusercontent.com", StringComparison.Ordinal)
                || row.StartsWith("AcrylicPane\t", StringComparison.Ordinal) || row.StartsWith("NotificationCommon\t", StringComparison.Ordinal)),
                "repository controls and internal helpers remain outside production requirements");
            var siteManifest = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "FluentComponents.tsv"));
            var expanded = siteManifest + "RepositoryOnly\tcomponent\tandroid\tsource\thttps://raw.githubusercontent.com/microsoft/fluentui-android/main/Widget.kt\n";
            Check(GenerateCatalog(visibleComponent, expanded).Result.Diagnostics.Any(d => d.Id == "DUXWGT006"),
                "appending a repository-only requirement to real production data is rejected");
        }
        foreach (var repository in new[] { "fluentui", "fluentui-android", "fluentui-apple" })
        {
            var reference = "https://raw.githubusercontent.com/microsoft/" + repository + "/0123456789012345678901234567890123456789/Controls/Button.source";
            var official = GenerateCatalog(visibleComponent, catalogManifest.Replace("https://fluent2.microsoft.design/components/web/react/core/button/usage", reference));
            Check(official.Result.Diagnostics.Any(d => d.Id == "DUXWGT006") && official.Result.GeneratedTrees.Length == 0,
                "official repository provenance alone does not authorize a component requirement: " + repository);
        }
        foreach (var reference in new[]{
            "https://raw.githubusercontent.com/microsoft/fluentui-android-fork/main/Button.kt",
            "https://raw.githubusercontent.com/other/fluentui-android/main/Button.kt",
            "https://raw.githubusercontent.com.evil.example/microsoft/fluentui-apple/main/Button.swift",
            "https://token@raw.githubusercontent.com/microsoft/fluentui-apple/main/Button.swift",
            "http://raw.githubusercontent.com/microsoft/fluentui-apple/main/Button.swift"})
        {
            var invalid = GenerateCatalog(visibleComponent, catalogManifest.Replace("https://fluent2.microsoft.design/components/web/react/core/button/usage", reference));
            Check(invalid.Result.Diagnostics.Any(d => d.Id == "DUXWGT006") && invalid.Result.GeneratedTrees.Length == 0,
                "lookalike,foreign,insecure or credential-bearing references cannot widen official component scope");
        }
        Check(unknownStandard.Result.Diagnostics.Any(d => d.Id == "DUXWGT005") && unknownStandard.Result.GeneratedTrees.Length == 0,
            "a valid widget adapter outside the reference catalog cannot be exposed as a standard component");
        var allComponents = visibleComponent + overloads;
        Check(GenerateCatalog(allComponents, variantScope, true).Result.Diagnostics.Any(d => d.Id == "DUXWGT007"),
            "strict completeness still rejects missing variants even when all top-level factories exist");
        var strictComplete = GenerateCatalog(allComponents, strict: true);
        Check(!strictComplete.Result.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error)
            && !strictComplete.Compilation.GetDiagnostics().Any(d => d.Severity == DiagnosticSeverity.Error),
            "strict catalog accepts a complete test scope with every declared factory");
        var strictDraft = GenerateCatalog(allComponents, catalogManifest.Replace("# Scope: complete", "# Scope: draft"), true);
        Check(strictDraft.Result.Diagnostics.Any(d => d.Id == "DUXWGT007") && strictDraft.Result.GeneratedTrees.Length == 1,
            "strict catalog rejects an unreviewed draft even when every currently known factory exists");
        var strictMissing = GenerateCatalog(visibleComponent, strict: true);
        Check(strictMissing.Result.Diagnostics.Any(d => d.Id == "DUXWGT007"),
            "strict catalog rejects a missing required factory without silently shrinking the reference scope");
        Check(catalogText.Contains("private static class __CatalogAudit")
            && catalogText.Contains("Missing = new string[] { \"Checkbox\", };")
            && catalogText.Contains("IsCatalogScopeComplete => true;")
            && !catalogText.Contains("Parse(") && !catalogText.Contains("Activator"),
            "missing factory metadata is generated at compilation and isolated from normal widget creation");
        using (var stream = new MemoryStream())
        {
            var emitted = catalog.Compilation.Emit(stream);
            Check(emitted.Success, "catalog consumer with deferred audit metadata emits successfully");
            stream.Position = 0;
            var assembly = AssemblyLoadContext.Default.LoadFromStream(stream);
            var audit = assembly.GetType("Duxel.Core.Fluent+__CatalogAudit")!;
            Check((audit.Attributes & TypeAttributes.BeforeFieldInit) == 0,
                "runtime cannot eagerly initialize audit arrays before an explicit audit query");
        }
        FluentIconGeneratorTests.Run(Check, references, parseOptions, output);
        FluentIconIncrementalTests.Run(Check, references, parseOptions, output);
        File.WriteAllText(Path.Combine(output, "core-catalog.json"), JsonSerializer.Serialize(new
        {
            available = Fluent.ComponentNames.ToArray(),
            required = Fluent.RequiredComponentNames.ToArray(),
            missing = Fluent.MissingComponentNames.ToArray(),
            scopeComplete = Fluent.IsCatalogScopeComplete
        }, new JsonSerializerOptions { WriteIndented = true }));
        File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new
        {
            framework = RuntimeInformation.FrameworkDescription,
            os = RuntimeInformation.OSDescription,
            architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            roslyn = new
            {
                runtimeMode = Assembly.GetExecutingAssembly().GetCustomAttributes<AssemblyMetadataAttribute>().Single(a => a.Key == "DuxelRoslynRuntimeMode").Value,
                apiReferenceVersion = Assembly.GetExecutingAssembly().GetReferencedAssemblies().Single(a => a.Name == "Microsoft.CodeAnalysis").Version!.ToString(),
                compilerContext = AssemblyLoadContext.GetLoadContext(typeof(Compilation).Assembly)!.Name,
                commonVersion = typeof(Compilation).Assembly.GetName().Version!.ToString(),
                commonInformationalVersion = typeof(Compilation).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
                csharpVersion = typeof(CSharpCompilation).Assembly.GetName().Version!.ToString(),
                csharpInformationalVersion = typeof(CSharpCompilation).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            },
            passed = checks.Count,
            checks
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
}

sealed class CatalogFile(string text) : AdditionalText
{
    public override string Path => "FluentComponents.tsv";
    public override Microsoft.CodeAnalysis.Text.SourceText GetText(System.Threading.CancellationToken cancellationToken = default)
        => Microsoft.CodeAnalysis.Text.SourceText.From(text);
}

sealed class CatalogOptionsProvider(bool strict) : Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptionsProvider
{
    private readonly Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptions options = new CatalogOptions(strict);
    public override Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptions GlobalOptions => options;
    public override Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptions GetOptions(SyntaxTree tree) => options;
    public override Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptions GetOptions(AdditionalText textFile) => options;
    private sealed class CatalogOptions(bool strict) : Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptions
    {
        public override bool TryGetValue(string key, out string value)
        {
            value = strict ? "true" : "false";
            return key == "build_property.DuxelRequireCompleteFluentCatalog";
        }
    }
}
