using Duxel.Core.Dsl.Generator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

internal static class FluentIconGeneratorTests
{
    internal static void Run(Action<bool,string> check,IEnumerable<MetadataReference> references,CSharpParseOptions parseOptions,string output)
    {
        const string regular="{\n\"ic_fluent_add_20_regular\": 61705,\n\"ic_fluent_add_24_regular\": 61706\n}";
        const string filled="{\n\"ic_fluent_add_20_filled\": 61705,\n\"ic_fluent_add_24_filled\": 61706\n}";
        const string source="using Duxel.Core; namespace Consumer; [UiFluentIcon(\"add\",UiFluentIconStyle.Interactive)] public readonly partial struct ProductIcon;";
        (Compilation Compilation,GeneratorDriverRunResult Result) Generate(string code,string? r=regular,string? f=filled,bool duplicate=false)
        {
            var files=new List<AdditionalText>();
            if(r is not null){files.Add(new GlyphFile("FluentSystemIcons-Regular.json",r));if(duplicate)files.Add(new GlyphFile("other/FluentSystemIcons-Regular.json",r));}
            if(f is not null)files.Add(new GlyphFile("FluentSystemIcons-Filled.json",f));
            var compilation=CSharpCompilation.Create("IconConsumer",[CSharpSyntaxTree.ParseText(code,parseOptions)],references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            GeneratorDriver driver=CSharpGeneratorDriver.Create([new UiFluentIconSourceGenerator().AsSourceGenerator()],files,parseOptions);
            driver=driver.RunGeneratorsAndUpdateCompilation(compilation,out var updated,out _);return(updated,driver.GetRunResult());
        }
        var positive=Generate(source);var text=positive.Result.GeneratedTrees.Single().GetText().ToString();
        check(!positive.Compilation.GetDiagnostics().Any(d=>d.Severity==DiagnosticSeverity.Error)&&positive.Result.Diagnostics.Length==0,
            "compiled official icon binds both font faces and glyph sizes as an actual IUiButtonIcon consumer");
        check(text.Contains("FontIcon<global::Duxel.Core.UiFluentRegularIconFont>")&&text.Contains("FontIcon<global::Duxel.Core.UiFluentFilledIconFont>")
            &&text.Contains("\\U0000F109")&&text.Contains("\\U0000F10A")&&!text.Contains("Dictionary")&&!text.Contains("Json")&&!text.Contains("Reflection"),
            "generated icon has direct typed font calls and scalar constants without a runtime catalog");
        check(Generate(source).Result.GeneratedTrees.Single().GetText().ToString()==text,"identical icon inputs emit deterministic source");
        File.WriteAllText(Path.Combine(output,"ProductIcon.FluentIcon.g.cs"),text);
        bool Rejected(string code,string? r=regular,string? f=filled,bool duplicate=false)=>
            Generate(code,r,f,duplicate).Result.Diagnostics.Any(d=>d.Id=="DUXWGT008"&&d.Severity==DiagnosticSeverity.Error);
        check(Rejected(source,r:null),"missing required icon catalog fails explicitly before consumer compilation");
        check(Rejected(source,r:regular.Replace("ic_fluent_add_24_regular","ic_fluent_other_24_regular")),"missing required size never substitutes another glyph");
        check(Rejected(source.Replace("UiFluentIconStyle.Interactive","(UiFluentIconStyle)999")),"unknown compiled icon style is rejected");
        check(Rejected(source.Replace("struct ProductIcon","record struct ProductIcon")),"record-struct icon declarations produce an explicit generator diagnostic");
        check(Rejected(source.Replace("ProductIcon;","ProductIcon<T>;")),"open generic icon targets are rejected");
        check(Rejected(source,r:regular.Replace("61705","65")),"non-private-use glyph code points are rejected");
        check(Rejected(source,r:regular.Replace("ic_fluent_add_24_regular","ic_fluent_add_20_regular")),"duplicate glyph keys cannot silently replace mappings");
        check(Rejected(source,duplicate:true),"duplicate icon catalogs cannot select an arbitrary source");
        check(Rejected(source,r:regular.Replace("61706\n}","61706,\n}")),"malformed catalog delimiters are rejected");
        var digits=Generate(source.Replace("\"add\"","\"3d_add\""),regular.Replace("ic_fluent_add_","ic_fluent_3d_add_"),filled.Replace("ic_fluent_add_","ic_fluent_3d_add_"));
        check(!digits.Result.Diagnostics.Any(d=>d.Severity==DiagnosticSeverity.Error),"official icon names may begin with a digit");
        var fixedSource=source.Replace("UiFluentIconStyle.Interactive","UiFluentIconStyle.Filled,12");
        var fixedIcon=Generate(fixedSource,f:"{\n\"ic_fluent_add_12_filled\": 61707\n}");
        var fixedText=fixedIcon.Result.GeneratedTrees.Single().GetText().ToString();
        check(!fixedIcon.Compilation.GetDiagnostics().Any(d=>d.Severity==DiagnosticSeverity.Error)
            &&fixedText.Contains("\\U0000F10B")&&!fixedText.Contains("bounds.Width <="),
            "fixed icon size emits its exact glyph without adaptive size selection or20/24 requirements");
        check(Rejected(fixedSource),"missing exact fixed-size glyph never substitutes adaptive glyphs");
        var tenSource = fixedSource.Replace("Filled,12", "Filled,10");
        var tenIcon = Generate(tenSource, f : "{\n\"ic_fluent_add_10_filled\": 61708\n}");
        check(!tenIcon.Compilation.GetDiagnostics().Any(d => d.Severity == DiagnosticSeverity.Error)
            && tenIcon.Result.GeneratedTrees.Single().GetText().ToString().Contains("\\U0000F10C"),
            "fixed10px presence icons bind the exact catalog glyph");
        check(Rejected(tenSource), "missing10px presence icon never substitutes12px or adaptive glyphs");
        check(Rejected(source.Replace("UiFluentIconStyle.Interactive","UiFluentIconStyle.Filled,13")),"unsupported fixed icon size is a compile diagnostic");
        check(typeof(Duxel.Core.UiFluentIconAttribute).GetConstructor([typeof(string),typeof(Duxel.Core.UiFluentIconStyle)]) is not null,
            "existing two-argument icon attribute constructor remains binary-compatible");
    }
    private sealed class GlyphFile(string path,string content):AdditionalText
    {
        public override string Path=>path;
        public override SourceText GetText(CancellationToken cancellationToken=default)=>SourceText.From(content);
    }
}
