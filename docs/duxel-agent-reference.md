# Duxel API and source map

For AI-assisted development, identify the target platform, choose a working Gallery example, and copy the API calls from it. The 0.4.0 FBA examples show complete files with package directives and app initialization. Check implementation signatures before proposing new API calls.

| Task | Start with |
| --- | --- |
| Runnable examples and package choice | [File-based app guide](getting-started-fba.md), [Fluent 2 sample commands](fba-run-samples.md) |
| Working controls and layout | [GalleryComponentExamples.cs](../samples/Duxel.Gallery/GalleryComponentExamples.cs), [Gallery navigation](../samples/Duxel.Gallery/GalleryNavigation.cs) |
| Fluent typography | [Fluent.Text.cs](../src/Duxel.Core/Fluent.Text.cs), e.g. `Fluent.Text(Text.Title2, "Title")` |
| State, widgets, and layout | [Duxel.Core](../src/Duxel.Core) |
| App options and frame loop | [Duxel.App](../src/Duxel.App) |
| Platform windows and input | [Windows host](../src/Duxel.Windows.App), [Linux host](../src/Duxel.Linux.App) |
| Vulkan, SVG | [Duxel.Vulkan](../src/Duxel.Vulkan), [Duxel.Svg](../src/Duxel.Svg) |

The shared widget layer uses `Dux` and `Fluent`. Keep platform-specific features in the host setup and reuse the widget tree. Run the example after editing it in each targeted platform.

For system colors on Windows, leave `Design` and `Theme` at their defaults so the host can apply the app color mode. With the 0.4.0 Linux host, the [representative file-based apps](fba-run-samples.md) choose a Fluent design from the desktop Settings portal at launch.

[Documentation index](README.md) · [한국어](duxel-agent-reference.ko.md)
