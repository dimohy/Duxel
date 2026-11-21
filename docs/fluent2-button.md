# Fluent 2 Button typography and secondary appearance

Last synced: 2026-11-20

`Fluent.CompoundButton(label, description, activated)` displays a title and a single-line description in one button. Both lines invoke the same action. Assistive technology can read the title as the button name and the description as help text. In the Gallery, open **동작과 탐색 → Button → 문서 만들기**.

`Fluent.SplitButton(label, menuLabel, activate, items, invoked, options)` puts a primary action and a menu chevron side by side. The primary button calls `activate`. The chevron opens a menu, and selecting an item calls `invoked`. Both segments are keyboard tab stops. In the Gallery, open **동작과 탐색 → Button → PNG로 저장**.

The scope authority is the [official Button guidance](https://fluent2.microsoft.design/components/web/react/core/button/usage). The pinned reference implementation and independently resolved color fixture are recorded in `artifacts/fluent-button-typography-2026-11-05/source-manifest.json`.

The generated default `UiFluentButton` selects `UiFluentButtonPresentation`: Segoe UI Semibold 14px with a 20px line box, minimum 96×32. `UiFluentSmallButtonPresentation` uses Regular 12/16 and minimum 64×24; `UiFluentLargeButtonPresentation` uses Semibold 16/22 and minimum 96×40. Use `UiButtonStyle` to bind either presentation to an existing behavior at compile time, as demonstrated by the gallery's `--buttons` page. These are size implementations of Button, not additional catalog components.

The shared Button code measures, handles input, paints and publishes item state for generated widgets, inherited buttons and immediate calls. The non-generic `ui.Button()` and `UiButton` use the configured input policy and compatibility presentation. The common code opens a value-type typography scope before measurement and restores the caller's font even if measurement or painting throws. Text requires configured typography faces and a DirectText font-metrics backend. Empty captions can be drawn without a text backend.

Secondary light/dark rest, hover, pressed and disabled paint comes from the pinned neutral tokens. Keyboard focus adds an inset ring. Text uses actual primary-face ascent/descent and the selected line box, clipped to the Button and current item/table cell. Rounded-corner text masking is still pending.

Font metrics occupy four inline context slots, keyed by face, physical size and backend generation. Each retained path is limited to 1024 characters. Font configuration changes invalidate static layers; backend revision changes invalidate metrics. Context disposal clears the slots. There is no new per-Button metrics object. Tests cover 10,000 same-size and interleaved-size controls, font restoration on exceptions, backend revision, face changes, lifetime and actual glyph colors.

Validation: .NET10 focused 20/full 1228 checks, generator 83, net8/9/10 build with zero warnings/errors; fixed .NET11 RC3 NativeAOT EXT/core 375 checks each. All 14 new Button images match managed rendering in both Native paths, and all 209 previous Native images remain byte-identical in each corresponding configuration. The broad managed/Native file intersection also contains eight unequal Progress/Window images. Those separate runners do not establish a matched scene history; their differences remain unclassified and are excluded from the equality claim. Root MSAA alone does not explain them because they are captured before its switch. See `verification.json` and the image comparison files in the artifact directory.

The GPU state matrix uses explicit test behavior policies; it proves paint and generated/direct/retained consistency, not real pointer transitions or accessibility. The gallery startup and three-second auto-exit passed; foreground, manual input and official browser pixel equivalence remain unverified.

The subsequent appearance implementation adds primary/outline/subtle/transparent policies below. Still required: shapes, icons, compound/menu/split variants, motion, disabled-focusable behavior, Windows UIA and matched official browser comparisons. Fractional-DPI raster rounding remains a backend limitation. This is not full Button or Fluent 2 completion. No new app FPS, Private-memory or Raspberry Pi performance claim is made. Earlier CPU measurements use their recorded font policy and cannot establish performance of this changed policy without a matched new comparison.

## Compile-time appearance implementations

`IUiButtonAppearance.Paint` owns frame drawing and returns the text color. A custom implementation can change the drawing algorithm; replacing the complete `IUiButtonPresentation` also changes typography, measurement and layout. The appearance interface does not replace that wider extension point.

The five policies are `UiFluentSecondaryButtonAppearance`, `UiFluentPrimaryButtonAppearance`, `UiFluentOutlineButtonAppearance`, `UiFluentSubtleButtonAppearance` and `UiFluentTransparentButtonAppearance`. Each can be bound to medium `UiFluentButtonPresentation<TAppearance>`, small `UiFluentSmallButtonPresentation<TAppearance>` or large `UiFluentLargeButtonPresentation<TAppearance>`. Selection is a closed generic call, without an appearance enum, runtime implementation registry or per-widget policy object. Primary has its own extra inner focus ring; the shared frame routine preserves the existing secondary pixels. Primary focus shadow2 remains unimplemented and must not be called visually identical to the browser reference.

```csharp
[UiButtonStyle(typeof(UiReleaseButtonBehavior),
    typeof(UiFluentSmallButtonPresentation<UiFluentPrimaryButtonAppearance>))]
public partial class SaveButton;
```

A Build method can retain this generated button with `context.Child("save", () => new SaveButton("Save", Save))`. Typed immediate code uses `ui.Button<UiReleaseButtonBehavior, UiFluentSmallButtonPresentation<UiFluentPrimaryButtonAppearance>>("Save")`. The generated widget and the typed call share behavior, font scope and rendering. You can supply your own `IUiButtonPresentation` to control measurement and painting in either form. Size and appearance variants do not add top-level component catalog names.

The .NET 11 regression suite checks pointer, keyboard and disabled behavior on four Fluent appearances, a subclass of `UiButton` and `ui.Button()`. It also compares the drawing primitive produced by a custom circular presentation through a subclass and a typed immediate call. See [verification results](../artifacts/button-shared-contract-2026-11-20/verification.json).

The independent `ButtonAppearances.json` fixture resolves actual brandWeb and grey tokens, including disabled overrides and transparent surfaces; token comments contain older brand colors and are not color authority. Source hashes, the resolver and qualification results are in `artifacts/fluent-button-appearance-2026-11-05`. GPU comparisons span five appearances, three sizes, four paint states, light/dark and MSAA1/4; separate real-input tests cover held/released/cancelled pointer, Tab, Enter, Space repeat and disabled activation. Paint-state injection is explicitly test-only. These checks do not prove UIA, forced colors, browser raster equivalence, motion or the remaining Button variants.

Appearance qualification: .NET10 input32/full1320; fixed RC3 Native EXT/core467 each; net8/9/10 build zero warnings/errors. New100 images match each Native path (200 comparisons). Previous223 Native images in each path and500 managed images are unchanged. The latest gallery executable is recorded by `artifacts/fluent-button-appearance-2026-11-05/gallery-smoke.json`; startup/first-submit/auto-exit passed.

## Shape drawing policies

`IUiButtonShape.Paint` draws a complete frame, including the requested focus inset. The built-in implementations are `UiFluentRoundedButtonShape`, `UiFluentSquareButtonShape` and `UiFluentCircularButtonShape`. Rounded remains the default. A circular rectangular button is a capsule; it becomes a circle for square bounds. The official10000px radius is normalized against the shortest side and retained as an upper limit even for oversized clipped controls.

Choose the shape inside the appearance type, for example:

```csharp
[UiButtonStyle(typeof(UiReleaseButtonBehavior),
    typeof(UiFluentButtonPresentation<UiFluentPrimaryButtonAppearance<UiFluentCircularButtonShape>>))]
public partial class ConfirmButton;
```

The same composition works with the small/large presentation and each of the five appearances. The generator resolves this closed type; there is no runtime shape registry or per-widget shape object. Existing non-generic appearance types forward to rounded and preserve earlier pixels. A user shape can draw two disjoint surfaces or a different geometry algorithm through the same extension point; the GPU test verifies this rather than only radius changes. Such user extensions do not become new default catalog components.

Qualification: .NET10 full1450, fixed RC3 Native EXT/core597 each, net8/9/10 zero warnings/errors. New210 images match each Native path (420 comparisons); previous323 Native images per path and600 managed images are unchanged. Tests span shape/appearance/size/state/light/dark/MSAA1/4, focus, independent corner samples and the oversized radius limit. Paint-state matrices remain distinct from real-input/UIA/browser-equivalence proof. Primary shadow2, exact size-specific rounded focus treatment, rounded glyph masking, icons, compound/menu/split, motion, forced colors and legacy immediate migration still require work. No new FPS/Private/Pi claim is made. See `artifacts/fluent-button-shapes-2026-11-05/verification.json`.

## Compiled icon content

`UiFluentIconButtonPresentation<TSize,TAppearance,TIcon,TPosition>` composes an actual icon drawing implementation with text, frame and input behavior. Choose `UiFluentSmallButtonIconSize`, `UiFluentMediumButtonIconSize` or `UiFluentLargeButtonIconSize`; icon slots are20/20/24px with4/6/6px text gaps. Empty display captions use24/32/40px square icon-only controls. Small icons are20px in the pinned Button reference, not16px. Explicit Duxel Size overrides still take precedence.

Implement `IUiButtonIcon.Paint` to draw the supplied bounds/color/input state. `UiButtonIconBefore` and `UiButtonIconAfter` choose physical placement at compile time; automatic logical RTL mapping is not implemented. The appearance separately resolves icon color, including subtle hover/pressed brand colors while its text remains neutral. Custom icons can use current interaction to select regular/filled geometry.

```csharp
[UiButtonStyle(typeof(UiReleaseButtonBehavior),
    typeof(UiFluentIconButtonPresentation<UiFluentMediumButtonIconSize,
        UiFluentPrimaryButtonAppearance,MyIcon,UiButtonIconBefore>))]
public partial class AddButton;
```

A nonempty display caption and icon-only content are distinguished by the Button kernel's immutable `UiControlContext.ButtonHasText`, including hidden `##` suffixes; this is not inferred from measured width. The original presentation interface remains compatible. This adds a bool to the context value passed during drawing, with no new per-widget field or heap policy object; its whole-app performance effect has not been measured.

`UiControlContext.Clip` returns a value-type scope intersecting the Button, item/table cell and current clip. It is restored on icon exceptions, together with the surrounding font. Icon-only vector content does not require a text backend. Glyph masking still uses a rectangular clip; exact rounded masking remains pending.

Validation: .NET10 icon functional39/full1809, fixed RC3 Native EXT/core956 each. New480 images match each Native path (960 comparisons); previous533 Native images per path and810 managed images remain unchanged. The tests use a small solid probe icon to verify layout, pixel tint and generated/direct/retained consistency. The gallery uses a user-drawn check icon and shares its callback/state pipeline. Neither is claimed as an official Fluent system icon asset. A subsequent implementation supplies official fonts and generated Add types. Full symbol APIs, icon-only accessible names/UIA and exact SVG/browser parity remain required. Other Button limitations above remain open. No app FPS/Private/Pi improvement is claimed. Evidence: `artifacts/fluent-button-icons-2026-11-06/verification.json`.

See [official icons](fluent2-icons.md) for the bundled fonts and `[UiFluentIcon]` generation path. Add regular/filled/interactive is provided; full symbol API, SVG/browser parity and UIA are not complete.
