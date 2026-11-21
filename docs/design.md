# Duxel architecture

[한국어](design.ko.md) · [Documentation](README.md)

Duxel runs native desktop applications with a platform host and a Vulkan renderer. Application code can compose widgets, render through an immediate-mode screen, or combine the two.

## Application UI

Use the `Dux` factories for layout and `Fluent` for themed controls. Keep the widget root for the lifetime of the screen, and update application values through `UiState<T>`, bindings, or the widget's supported properties. A widget tree can contain immediate content through a `UiView` hosted by a `UiViewHost`. An immediate-mode screen can also render an owned widget. [Custom widgets](custom-widgets.md) · [Layout containers](widget-containers.md)

Override `UiScreen.Render(UiImmediateContext ui)` when the application needs direct, frame-oriented input and drawing. For declarative layouts, Duxel can parse `.ui` at runtime or generate C# at build time. [Declarative UI](ui-dsl.md)

## Designs and themes

`UiCompiledDesign.Windows11` and `UiCompiledDesign.Windows11Dark` provide built-in light and dark themes for Fluent 2 controls. Set a compiled design in the app options. Use `UiTheme` for palette changes, `UiDesignTokens` for control geometry, and an `IUiDesign` implementation for a reusable design. `Fluent.FluentProvider` can scope theme, fonts and supported layout direction to a subtree. [Fluent 2 coverage](fluent2-coverage.md) · [Theme provider](fluent2-provider.md)

## Platform and rendering

Platform hosts handle the window, input, clipboard, native text, and accessibility integration. The Vulkan backend renders the draw data produced by the UI. Consult the platform documentation for supported environments and the rendering documentation for cache and presentation behavior. [Linux](linux-platform.md) · [Accessibility](widget-accessibility.md) · [Retained rendering](retained-rendering.md)
