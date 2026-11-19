using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Duxel.Core.Dsl.Generator;

[Generator]
public sealed class UiFluentIconSourceGenerator:IIncrementalGenerator
{
    private static readonly DiagnosticDescriptor Invalid=new("DUXWGT008","Invalid compiled Fluent icon","{0}","UI.WidgetTheme",DiagnosticSeverity.Error,true);
    private sealed class Catalog(string name,Dictionary<string,int> glyphs,string? error)
    {
        internal string Name {get;}=name;
        internal Dictionary<string,int> Glyphs {get;}=glyphs;
        internal string? Error {get;}=error;
    }
    private sealed class Binding(string name,string ns,string typeName,int style,int regular20,int regular24,int filled20,int filled24,
        string? error=null,Location? location=null,int glyphSize=0):IEquatable<Binding>
    {
        internal string Name {get;}=name;
        internal string Namespace {get;}=ns;
        internal string TypeName {get;}=typeName;
        internal int Style {get;}=style;
        internal int Regular20 {get;}=regular20;
        internal int Regular24 {get;}=regular24;
        internal int Filled20 {get;}=filled20;
        internal int Filled24 {get;}=filled24;
        internal int GlyphSize {get;}=glyphSize;
        internal string? Error {get;}=error;
        internal Location? Location {get;}=location;
        public bool Equals(Binding? other)=>other is not null&&Name==other.Name&&Namespace==other.Namespace&&TypeName==other.TypeName
            &&Style==other.Style&&Regular20==other.Regular20&&Regular24==other.Regular24&&Filled20==other.Filled20&&Filled24==other.Filled24
            &&GlyphSize==other.GlyphSize&&Error==other.Error&&Equals(Location,other.Location);
        public override bool Equals(object? other)=>other is Binding binding&&Equals(binding);
        public override int GetHashCode()=>StringComparer.Ordinal.GetHashCode(Name);
    }
    private sealed class Emission(Binding binding,string? source)
    {
        internal Binding Binding {get;}=binding;
        internal string? Source {get;}=source;
    }
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var catalogs=context.AdditionalTextsProvider.Where(static file=>
            file.Path.EndsWith("FluentSystemIcons-Regular.json",StringComparison.OrdinalIgnoreCase)
            ||file.Path.EndsWith("FluentSystemIcons-Filled.json",StringComparison.OrdinalIgnoreCase))
            .Select(static (file,ct)=>Parse(file.Path,file.GetText(ct)?.ToString()??string.Empty))
            .WithTrackingName("FluentIconCatalog").Collect();
        var declarations=context.SyntaxProvider.ForAttributeWithMetadataName("Duxel.Core.UiFluentIconAttribute",
            static (node,_)=>node is TypeDeclarationSyntax,static (declaration,_)=>declaration);
        var bindings=declarations.Combine(catalogs).Select(static (input,_)=>Resolve(input.Left,input.Right))
            .WithComparer(EqualityComparer<Binding>.Default).WithTrackingName("FluentIconBinding");
        var emissions=bindings.Select(static (binding,_)=>new Emission(binding,binding.Error is null?BuildSource(binding):null))
            .WithTrackingName("FluentIconEmission");
        context.RegisterSourceOutput(emissions,static (output,emission)=>
        {
            var binding=emission.Binding;
            if(binding.Error is not null)output.ReportDiagnostic(Diagnostic.Create(Invalid,binding.Location,binding.Error));
            else output.AddSource(binding.Name+".FluentIcon.g.cs",SourceText.From(emission.Source!,Encoding.UTF8));
        });
    }
    private static Binding Resolve(GeneratorAttributeSyntaxContext declaration,ImmutableArray<Catalog> catalogs)
    {
        var type=(INamedTypeSymbol)declaration.TargetSymbol;
        var syntax=declaration.TargetNode as StructDeclarationSyntax;
        var name=type.ToDisplayString();var ns=type.ContainingNamespace.IsGlobalNamespace?string.Empty:type.ContainingNamespace.ToDisplayString();
        Binding Reject(string message)=>new(name,ns,type.Name,0,0,0,0,0,message,declaration.TargetNode.GetLocation());
        if(syntax is null||!syntax.Modifiers.Any(SyntaxKind.PartialKeyword)||!syntax.Modifiers.Any(SyntaxKind.ReadOnlyKeyword)
            ||type.Arity!=0||type.ContainingType is not null||type.GetMembers("Paint").Length!=0)
            return Reject("Use a top-level readonly partial non-generic struct without a Paint member.");
        var attribute=declaration.Attributes[0];var iconName=attribute.ConstructorArguments[0].Value as string;
        var style=attribute.ConstructorArguments.Length>1?(int)attribute.ConstructorArguments[1].Value!:0;
        var size=attribute.ConstructorArguments.Length>2?(int)attribute.ConstructorArguments[2].Value!:0;
        if(iconName is null||!Regex.IsMatch(iconName,"^[a-z0-9][a-z0-9_]*$")||(uint)style>2
            ||size!=0&&size!=10&&size!=12&&size!=16&&size!=20&&size!=24&&size!=28&&size!=32&&size!=48)
            return Reject("The icon name or style is invalid.");
        var required=style==0?new[]{"Regular"}:style==1?new[]{"Filled"}:new[]{"Regular","Filled"};
        var regular20=0;var regular24=0;var filled20=0;var filled24=0;
        foreach(var face in required)
        {
            var matches=catalogs.Where(item=>item.Name==face).ToArray();
            if(matches.Length!=1||matches[0].Error is not null)
                return Reject("Supply exactly one valid FluentSystemIcons-"+face+".json AdditionalFile. "+matches.FirstOrDefault()?.Error);
            var key="ic_fluent_"+iconName+"_"+(size==0?20:size)+"_"+face.ToLowerInvariant();
            if(!matches[0].Glyphs.TryGetValue(key,out var code20))return Reject("The pinned icon catalog has no glyph: "+key);
            key="ic_fluent_"+iconName+"_"+(size==0?24:size)+"_"+face.ToLowerInvariant();
            if(!matches[0].Glyphs.TryGetValue(key,out var code24))return Reject("The pinned icon catalog has no glyph: "+key);
            if(face=="Regular"){regular20=code20;regular24=code24;}else{filled20=code20;filled24=code24;}
        }
        return new(name,ns,type.Name,style,regular20,regular24,filled20,filled24,glyphSize:size);
    }
    private static string BuildSource(Binding binding)
    {
        var source=new StringBuilder("// <auto-generated/>\n#nullable enable\n");
        if(binding.Namespace.Length!=0)source.Append("namespace ").Append(binding.Namespace).Append(";\n");
        source.Append("readonly partial struct @").Append(binding.TypeName).Append(" : global::Duxel.Core.IUiButtonIcon\n{\n")
            .Append("public static void Paint(global::Duxel.Core.UiControlContext context,global::Duxel.Core.UiRect bounds,global::Duxel.Core.UiColor color,global::Duxel.Core.UiButtonInteraction interaction)\n{\n");
        void Paint(string face,int code20,int code24)
        {
            if(binding.GlyphSize!=0)
            {
                source.Append("context.FontIcon<global::Duxel.Core.UiFluent").Append(face).Append("IconFont>(\"\\U")
                    .Append(code20.ToString("X8",CultureInfo.InvariantCulture)).Append("\",bounds,color);\n");
                return;
            }
            source.Append("context.FontIcon<global::Duxel.Core.UiFluent").Append(face).Append("IconFont>(bounds.Width <= 20 ? \"\\U")
                .Append(code20.ToString("X8",CultureInfo.InvariantCulture)).Append("\" : \"\\U")
                .Append(code24.ToString("X8",CultureInfo.InvariantCulture)).Append("\",bounds,color);\n");
        }
        if(binding.Style==2)
        {
            source.Append("if(interaction.Hovered||interaction.Held)\n{\n");Paint("Filled",binding.Filled20,binding.Filled24);
            source.Append("}\nelse\n{\n");Paint("Regular",binding.Regular20,binding.Regular24);source.Append("}\n");
        }
        else if(binding.Style==0)Paint("Regular",binding.Regular20,binding.Regular24);
        else Paint("Filled",binding.Filled20,binding.Filled24);
        return source.Append("}\n}\n").ToString();
    }
    private static Catalog Parse(string path,string content)
    {
        var name=path.EndsWith("Regular.json",StringComparison.OrdinalIgnoreCase)?"Regular":"Filled";
        var glyphs=new Dictionary<string,int>(StringComparer.Ordinal);var lines=content.Trim().Split('\n');
        if(lines.Length<3||lines[0].Trim()!="{"||lines[lines.Length-1].Trim()!="}")return new(name,glyphs,"Expected a single glyph object.");
        for(var i=1;i<lines.Length-1;i++)
        {
            var line=lines[i].Trim();var comma=i<lines.Length-2;
            var match=Regex.Match(line,comma?"^\"(ic_fluent_[a-z0-9_]+)\"\\s*:\\s*([0-9]+),$":"^\"(ic_fluent_[a-z0-9_]+)\"\\s*:\\s*([0-9]+)$");
            if(!match.Success||!int.TryParse(match.Groups[2].Value,NumberStyles.None,CultureInfo.InvariantCulture,out var code)
                ||code<0xE000||code>0x10FFFF||code>0xF8FF&&code<0xF0000||code>0xFFFFD&&code<0x100000||code>0x10FFFD)
                return new(name,glyphs,"Invalid private-use glyph mapping at line"+(i+1));
            var key=match.Groups[1].Value;
            if(glyphs.ContainsKey(key))return new(name,glyphs,"Duplicate glyph: "+key);
            glyphs.Add(key,code);
        }
        return new(name,glyphs,null);
    }
}
