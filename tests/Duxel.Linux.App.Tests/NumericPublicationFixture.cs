using System.Runtime.CompilerServices;
using Duxel.Core;

// Stage semantic-only native transport fixtures without changing the product
// assembly or enabling rendering validation hooks.
internal static class NumericPublicationFixture
{
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "BeginFrame")]
    internal static extern void BeginFrame(UiAccessibilityTree tree);
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "Add")]
    internal static extern void Add(UiAccessibilityTree tree, UiAccessibilityNode node, UiRect? revealBounds = null);
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "EndFrame")]
    internal static extern void EndFrame(UiAccessibilityTree tree, string? focus, bool authoritativeFocus);
}
