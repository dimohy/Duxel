# Compiled official Fluent icons

2026-11-15 actual package/SDK compiler matrix: each profile passes131 checks and the six generated files are unchanged. First/middle/last declaration removal caches0/1/2 remaining outputs even across separate files. The original2/2 first-removal criterion remains incomplete. [Selection and evidence](widget-contract-validation.md).

Last synced: 2026-11-06

2026-11-13 recheck: all117 generator checks pass on .NET11.0.0 on both Windows and Ubuntu x64; six captured generated sources are byte-identical. The declaration-removal criterion remains incomplete at0/2 cached outputs on both OSes. This is distinct from the historical checks below; see [execution and scope](widget-contract-validation.md).

Pin an exact size with `[UiFluentIcon("checkmark", UiFluentIconStyle.Filled, 12)]`. Supported selectors are0(existing20/24),12,16,20,24,28,32,48; a missing exact catalog glyph produces DUXWGT008. Fixed output has no size-selection branch. The previous two-argument constructor remains. Six12/16px [Checkbox marks](fluent2-checkbox.md) consume this path.

The bundled fonts and glyph mappings come from [Microsoft Fluent System Icons](https://github.com/microsoft/fluentui-system-icons/tree/9bfae3d782fda4c8764f2128cec4d0dda160b283/fonts), commit `9bfae3d782fda4c8764f2128cec4d0dda160b283`. The original MIT license accompanies the fonts in `Assets/FluentIcons`. Regular has10265 mappings and filled10389; every mapped code point was verified against the actual font cmap. Both fonts have unitsPerEm500/ascent500/descent0. These are icon-asset counts, not additional Fluent component requirements.

`UiFluentAddIcon`, `UiFluentAddFilledIcon` and `UiFluentAddInteractiveIcon` are generated `IUiButtonIcon` implementations. They bind actual20/24px glyphs and font-face types. Interactive uses regular at rest and filled while hovered/held; use it with appearances whose icon changes on interaction. The previous user/probe drawing examples remain separate from the official asset path.

```csharp
[UiFluentIcon("add", UiFluentIconStyle.Interactive)]
public readonly partial struct ProductAddIcon;

[UiButtonStyle(typeof(UiReleaseButtonBehavior),
    typeof(UiFluentIconButtonPresentation<UiFluentMediumButtonIconSize,
        UiFluentSubtleButtonAppearance,ProductAddIcon,UiButtonIconAfter>))]
public partial class AddButton;
```

The local project-reference/FBA build injects the two pinned JSON files as `AdditionalFiles`. The gallery consumes this exact user-defined attribute path. An external build must likewise provide exactly one `FluentSystemIcons-Regular.json` and/or `FluentSystemIcons-Filled.json` for the selected style. Names are the official lower-case family identifier. Both20/24 sizes are required. Missing/duplicate/malformed catalogs, invalid private-use codes, missing names/sizes and unsupported targets produce `DUXWGT008`; there is no glyph substitution.

The incremental generator parses build inputs and emits scalar UTF-16 literals plus direct `FontIcon<TFont>` calls for requested declarations. It does not generate a runtime name registry or thousands of unused icon types. Identical input determinism is tested; complete change-scope incremental qualification remains separate.

The common DirectText raster/vector backend performs the actual drawing and restores the surrounding font scope. Icon metrics use the pinned font profile; width measurement handles the fonts' differing advances. No text-metrics cache slots or per-widget metadata objects are added for icon metrics. Font files, totaling5466852 bytes, and the license copy to build/publish output. This file size is not a RAM-use measurement. Runtime JSON lookup and runtime font hashing are absent; tests verify the asset hashes before GPU initialization. A suitable DirectText backend is required, and 3D layers additionally require a vector text backend. Linux/ARM64 support is not implied.

Qualification and exact source/font/executable hashes are in `artifacts/fluent-icon-assets-2026-11-06`. The tests distinguish actual outline differences from pixel sampling: a thin antialiased raster stroke need not contain a pure-white pixel, and24px regular/filled vector outlines can produce identical MSAA1 pixels. State selection still must match the appropriate fixed-font rendering exactly in every condition, and the two styles must be distinguishable in qualified raster/vector conditions. Official SVG/browser raster equivalence, icon-only accessible names/UIA, logical RTL, full symbol API coverage and app-wide performance remain unverified/incomplete. No FPS, Private-memory or Raspberry Pi improvement is claimed.

Current validation: .NET10 full1862/generator96; fixed RC3 Native EXT/core1009 each; net8/9/10 zero warnings/errors. New192 managed/Native image comparisons and previous2026 Native/1290 managed comparisons match exactly. The latest gallery passed startup/first-submit/auto-exit only.

## Incremental emission and remaining removal cost

The icon generator now projects declarations/catalogs to immutable values containing only selected glyph codes, names and style. Positive bindings retain no syntax location or semantic model. Equal bindings skip both source-string construction and Roslyn source output. Diagnostics retain their current location so moving an error updates its source tree/span.

The107 generator functional checks include actual tracked-driver cases: unchanged input, unused glyph changes and unrelated code reuse all three outputs; a selected regular/filled glyph changes only its two dependent styles; missing/malformed data removes stale output and reports errors; unrelated AdditionalFiles remain cached. All57 Core-generated files have identical names and bytes before/after this implementation. Runtime sources, five icon assets and existing Native/gallery hashes were also checked. The prior .NET10 rendering1862 and fixed RC3 Native EXT/core1009 results are reused within that compatibility scope; they were not rerun as new GPU/performance measurements. The solution and current managed FBA consumer compile successfully.

One optimization criterion remains incomplete: removing the first declaration from a shared source file shifts Roslyn's positional entries and reconstructs the other two outputs. Their content is identical and the removed output disappears, but expected cached outputs2 vs observed0 is recorded separately as `strictRemovalCacheComplete=false`. Functional test success does not qualify that performance criterion. Actual trace, limits and compatibility evidence are in `artifacts/fluent-icon-incremental-2026-11-06`. This is not a full incremental-optimization or app FPS claim.
