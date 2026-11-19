# Fluent 2 NavigationBar

`Fluent.NavigationBar(label, title, leading, trailing, secondary, options)` creates a header for the current screen. The label names its accessibility landmark. The title can change while the leading and trailing action widgets retain their identities. Pass an empty title for an action-only bar. All supplied slot widgets are owned by the bar.

The header centers its title independently of the action widths. When more room is needed, the title moves to a separate row. If the actions also need more space, they stack. An optional secondary row hosts a widget such as `Fluent.SegmentedControl`. Use `Options.RightToLeft` to mirror the action placement. Set the hosted control's own direction when it also needs RTL interaction.

`UiNavigationBarAppearance` offers the inherited neutral surface, a transparent surface, and a blue brand gradient with light foreground. System contrast colors are applied when forced colors are active. `Options.Width` requests a width within the parent constraints.

Run `./run-gallery.ps1` on Windows and open **동작과 탐색 → NavigationBar**. The first example changes title and width. The second embeds a period selector in the branded header. Each example shows its source directly below it. [Fluent 2 Navigation bar guidance](https://fluent2.microsoft.design/components/ios/core/navigationbar/usage)

The Windows run presented 100 Gallery scenarios, including NavigationBar. Thirteen focused checks cover action input, title placement, RTL, narrow layout, live source and Vulkan pixels under the brand and system contrast palettes. Both sample sources compile for Windows and platform-neutral .NET 11.

The external `atspi-gallery-navigation-bar` probe passed four checks on each WSLg Linux x64 build (.NET 11.0.0 managed and the previously published NativeAOT build). It found the named landmark, title and both buttons, invoked Back and Save, and selected a period in the secondary row before resetting it with Today. Physical screen-reader speech is still unverified. Evidence is in `W:/Cache/Duxel/gallery-source-20261117/navigation-bar-atspi-2035/verification.json`.

The branded example sizes its period buttons to fit their full labels. The date range stays selectable by horizontal scrolling when the window is narrow.

NavigationBar reuses the period selector's measured layout when the parent width constraint changes without changing the selector's actual width. A paint group update damages only commands that consume the changed matte textures. The Linux Gallery repaint probe tests continuous mode, title actions and period selection, and records the real Vulkan repair bounds.
