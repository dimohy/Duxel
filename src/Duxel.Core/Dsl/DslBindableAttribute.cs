using System;

namespace Duxel.Core.Dsl;

/// <summary>
/// Marks a type for DSL property binding source generation.
/// A source generator produces property accessor methods so that DSL templates
/// can reference object properties (e.g., <c>{user.Name}</c>) without reflection.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
public sealed class DslBindableAttribute : Attribute;
