using System;
using System.Linq;
using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Duxel.Core.Dsl.Generator;

internal static class CompiledWidgetContract
{
    internal static bool TryResolve(INamedTypeSymbol target, Compilation compilation, out INamedTypeSymbol? implementation, out string error)
    {
        implementation = null;
        error = "Select one accessible concrete widget implementation and matching closed type arguments.";
        var styles = target.GetAttributes().Where(attribute => attribute.AttributeClass?.ToDisplayString()
            is "Duxel.Core.UiButtonStyleAttribute" or "Duxel.Core.UiWidgetStyleAttribute").ToArray();
        if (styles.Length != 1) return false;
        var style = styles[0];
        var arguments = style.ConstructorArguments;
        if (style.AttributeClass!.Name == "UiButtonStyleAttribute")
        {
            if (arguments.Length != 2 || arguments[0].Value is not ITypeSymbol behavior || arguments[1].Value is not ITypeSymbol presentation) return false;
            var button = compilation.GetTypeByMetadataName("Duxel.Core.UiButton`2");
            if (button is null) return false;
            implementation = button.Construct(behavior, presentation);
        }
        else
        {
            if (arguments.Length != 2 || arguments[0].Value is not INamedTypeSymbol widget || arguments[1].Kind != TypedConstantKind.Array) return false;
            var supplied = arguments[1].Values;
            if (supplied.Any(value => value.Value is not ITypeSymbol)) return false;
            if (supplied.Length == 0 && !widget.IsUnboundGenericType) implementation = widget;
            else
            {
                if (!widget.IsUnboundGenericType || widget.Arity != supplied.Length) return false;
                implementation = widget.OriginalDefinition.Construct(supplied.Select(value => (ITypeSymbol)value.Value!).ToArray());
            }
        }
        var leaf = compilation.GetTypeByMetadataName("Duxel.Core.UiWidget");
        if (leaf is null || implementation.IsAbstract || implementation.IsSealed || implementation.ContainsTypeParameters()
            || !compilation.IsSymbolAccessibleWithin(implementation, target)) return false;
        var current = implementation;
        while (current is not null && !SymbolEqualityComparer.Default.Equals(current, leaf)) current = current.BaseType;
        if (current is null) return false;
        var definition = implementation.OriginalDefinition;
        for (var i = 0; i < definition.TypeParameters.Length; i++)
        {
            var parameter = definition.TypeParameters[i];
            var argument = implementation.TypeArguments[i];
            if (argument is INamedTypeSymbol policy && (policy.IsAbstract || policy.TypeKind == TypeKind.Interface)
                && parameter.ConstraintTypes.OfType<INamedTypeSymbol>().Any(contract =>
                    contract.GetMembers().OfType<IMethodSymbol>().Any(method => method.IsStatic && method.IsAbstract))) return false;
            if (argument.TypeKind is TypeKind.Error or TypeKind.TypeParameter || argument is INamedTypeSymbol named && named.IsUnboundGenericType
                || parameter.HasReferenceTypeConstraint && !argument.IsReferenceType
                || parameter.HasValueTypeConstraint && (!argument.IsValueType || argument.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
                || parameter.HasNotNullConstraint && (argument.NullableAnnotation == NullableAnnotation.Annotated || argument.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
                || parameter.HasUnmanagedTypeConstraint && !argument.IsUnmanagedType) return false;
            if (parameter.HasConstructorConstraint && !argument.IsValueType
                && (argument is not INamedTypeSymbol constructed || constructed.IsAbstract
                    || !constructed.InstanceConstructors.Any(ctor => ctor.DeclaredAccessibility == Accessibility.Public && ctor.Parameters.Length == 0))) return false;
            foreach (var constraint in parameter.ConstraintTypes)
            {
                var closed = Substitute(constraint, definition, implementation.TypeArguments.ToArray(), compilation);
                var conversion=((CSharpCompilation)compilation).ClassifyConversion(argument,closed);
                if (!conversion.IsImplicit || !(conversion.IsIdentity || conversion.IsReference || conversion.IsBoxing))
                {
                    error = argument.ToDisplayString() + " does not satisfy " + closed.ToDisplayString() + ".";
                    return false;
                }
            }
        }
        if (!implementation.InstanceConstructors.Any(ctor => ctor.DeclaredAccessibility == Accessibility.Public)) return false;
        return true;
    }

    private static ITypeSymbol Substitute(ITypeSymbol type, INamedTypeSymbol definition, ITypeSymbol[] arguments, Compilation compilation)
    {
        if (type is ITypeParameterSymbol parameter && SymbolEqualityComparer.Default.Equals(parameter.ContainingSymbol, definition))
            return arguments[parameter.Ordinal];
        if (type is INamedTypeSymbol named && named.IsGenericType)
            return named.OriginalDefinition.Construct(named.TypeArguments.Select(argument => Substitute(argument, definition, arguments, compilation)).ToArray());
        if (type is IArrayTypeSymbol array) return compilation.CreateArrayTypeSymbol(Substitute(array.ElementType, definition, arguments, compilation), array.Rank);
        return type;
    }

    internal static string Parameters(IMethodSymbol constructor) => string.Join(", ", constructor.Parameters.Select(parameter =>
        (parameter.ScopedKind != ScopedKind.None ? "scoped " : "")
        + (parameter.IsParams ? "params " : RefModifier(parameter.RefKind))
        + parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(
            SymbolDisplayFormat.FullyQualifiedFormat.MiscellaneousOptions | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier)) + " @" + parameter.Name
        + (parameter.HasExplicitDefaultValue ? " = " + DefaultValue(parameter) : "")));
    internal static string Arguments(IMethodSymbol constructor) => string.Join(", ", constructor.Parameters.Select(parameter =>
        (parameter.RefKind == RefKind.RefReadOnlyParameter ? "in " : RefModifier(parameter.RefKind)) + "@" + parameter.Name));
    internal static string Signature(IMethodSymbol constructor) => string.Join(",", constructor.Parameters.Select(parameter =>
        (parameter.RefKind == RefKind.None ? "value" : "byref") + ":" + parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
    private static string RefModifier(RefKind kind) => kind switch { RefKind.Ref => "ref ", RefKind.Out => "out ", RefKind.In => "in ", RefKind.RefReadOnlyParameter => "ref readonly ", _ => "" };
    private static string DefaultValue(IParameterSymbol parameter)
    {
        if (parameter.ExplicitDefaultValue is null) return "default";
        if (parameter.Type.TypeKind == TypeKind.Enum)
            return "(" + parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + ")" + Convert.ToString(parameter.ExplicitDefaultValue, CultureInfo.InvariantCulture);
        return SymbolDisplay.FormatPrimitive(parameter.ExplicitDefaultValue, true, false);
    }
}
