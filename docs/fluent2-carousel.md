# Fluent 2 Carousel

`Fluent.Carousel(label, selected, slides, options)` takes a `UiBinding<string>` whose value identifies one slide by key. Each `UiCarouselSlide` has a unique key, a nonempty accessible label and its own content widget. The Carousel owns the content and keeps inactive widgets alive so their state is available on return. Only the selected content takes layout space and participates in input.

Previous and next buttons step through the slides. `Wrap` connects the first and last slides. The Fluent step list accepts keyboard and assistive selection. Providing a `UiImageSource` for every slide shows image previews with the same accessible labels. The preview strip scrolls when it runs out of space. `RightToLeft` mirrors its navigation direction. `ReducedMotion` removes the step selection animation.

Set `AutoPlayInterval` to at least one second to add an autoplay switch. Autoplay starts off and can also be controlled through `IsPlaying`. While playing, pointer hover or keyboard focus inside the Carousel suspends the timer. Rotation stops at the last slide when wrapping is disabled. The switch stops scheduling rotation when turned off.

Run `./run-gallery.ps1` on Windows or `./run-gallery-linux.ps1` from Windows to launch the WSL Gallery. Under **Content and lists → Carousel**, each example has C# source directly below it. Change slides or use a slide action to see the source state update. [Fluent 2 Carousel guidance](https://fluent2.microsoft.design/components/web/react/core/carousel/usage)

Verification covers 17 focused checks, Vulkan captures of the Gallery and its image previews, and 1,019 checks across the focused and related suites. The Windows Gallery presented all 100 scenarios, including Carousel. Both example sources compile against the Windows and platform-neutral .NET 11 builds.

On WSLg, the Linux x64 .NET 11.0.0 NativeAOT Gallery submitted the Carousel page through Vulkan. An external AT-SPI client passed nine checks each against the managed and NativeAOT Gallery. In the first example, it verified the disabled boundary buttons, Previous/Next, direct tab selection, retirement of inactive slide content and the shared counter. In the second, it verified selection by preview name, wrapping from the first slide to the last, automatic advance and retention of the selected slide for 4.4 seconds after pausing. Screen-reader speech remains unverified.

With an owned Linux Gallery process running on the same accessibility bus, run `dotnet <Duxel.Linux.App.Tests.dll> atspi-gallery-carousel <gallery-pid> <new-result.json>`.
