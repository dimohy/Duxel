# Fluent 2 named typography

Last synced: 2026-11-05

Seventeen named text presets now use generated inheritable widgets and factories. They select real Regular400, Semibold600 and Bold700 font files and position baselines inside the specified line boxes using DirectWrite ascent/descent. Built-in and user widgets share the same UiText, presentation and generation path. This does not complete the Text API or overall Fluent visual fidelity.

## Official reference and binding

The [Text usage guide](https://fluent2.microsoft.design/components/web/react/core/text/usage), TextPresetProps, tokens/global/typographyStyles.ts and fonts.ts at microsoft/fluentui commit `8830c10bfb2f4c1bdd540287b09713b9679cd1ad` were compared on2026-11-05. Sources and17 mappings are preserved under `artifacts/fluent-typography-2026-11-05/official-presets.json`. These presets are declared in the stable React Text package API; a documentation Preview heading is not a package release channel. The full Windows/mobile union still requires review.

| Factory | Size / line height | Weight |
| --- | ---: | --- |
| Body1 / Body1Strong / Body1Stronger | 14 / 20 | 400 / 600 / 700 |
| Body2 | 16 / 22 | 400 |
| Caption1 / Caption1Strong / Caption1Stronger | 12 / 16 | 400 / 600 / 700 |
| Caption2 / Caption2Strong | 10 / 14 | 400 / 600 |
| Subtitle1 | 20 / 28 | 600 |
| Subtitle2 / Subtitle2Stronger | 16 / 22 | 600 / 700 |
| Title1 / Title2 / Title3 | 32 / 40, 28 / 36, 24 / 32 | 600 |
| LargeTitle / Display | 40 / 52, 68 / 92 | 600 |

Each `Fluent.<name>(string)` and `Fluent.<name>(Func<string>)` creates a concrete widget with closed source policy, typeface role and size/line-height presentation. There is no runtime theme registry or preset-name lookup. Getters run once per render; classes such as UiFluentTitle1 remain inheritable. User presentations can compose `UiControlContext.Typography<TTypeface, TMetrics>`. Generated bindings consume actual font selection, measurement, line placement and drawing rather than only numeric tokens.

Configure files through `DuxelFontOptions.Typography`, or `UiContext.SetTypographyFonts` for custom hosts. Windows defaults select Segoe UI segoeui.ttf, seguisb.ttf and segoeuib.ttf. Secondary Korean files use malgun.ttf and malgunbd.ttf; the Semibold role also uses Malgun Bold, so Korean weight600 visual equivalence is not claimed. Public `WindowsPlatformTextBackend.Instance` and `IPlatformFontMetricsBackend.TryGetFontMetrics` expose real metrics without creating a window, following [DWRITE_FONT_METRICS](https://learn.microsoft.com/en-us/windows/win32/api/dwrite/ns-dwrite-dwrite_font_metrics) design units.

## Running and validation

The existing FBA accepts `--typography` and retains all17 presets as keyed children through the common renderer.

```powershell
./run-fba.ps1 samples/fba/widget_tree_theme_fba.cs -NoCache -NoLaunch
# Pass --typography to the executable path printed above.
& './artifacts/fluent-typography-2026-11-05/native-gallery-rc3/net11.0-windows7.0/widget_tree_theme_fba.exe' --typography
```

The second command opens the preserved fixed-RC3 publication. The first publishes local sources with the currently selected SDK; these are distinct artifacts and configurations.

.NET10 passed847 full checks and66 generator checks; the net8/9/10 solution built with zero warnings/errors. Checks cover actual OS/2 weights400/600/700, distinct raster pixels/textures, all17 line heights, baseline placement, DPI, multiple lines, getters, surrounding font restoration, real3D vector geometry and explicit unsupported failures. All323 preceding regression images are identical. These are not official browser screenshot comparisons.

Fixed SDK11.0.100-rc.3.26522.148/runtime11.0.0-rc.3.26522.148 win-x64 NativeAOT Speed publication performed actual native code generation. Default and typography modes both reached first GPU present and exit0. Executable SHA256: `5E473E7E844F1B7F6BD28612DBA34D65AD06C56545B29148818A388ADE3FEAF9`. This proves startup, not visual/interaction conformance or performance.

## Limits and remaining work

- The [default Text follow-up](fluent2-text.md) connects Fluent.Text and Dux.Text to Body1. Nongeneric immediate migration and complete Text variants remain open.
- Alignment, wrapping, truncation, italic, underline, strikethrough, semantics/UIA and all official variants/states/visual comparisons remain open. The22 standard names,289 known requirements and267 missing names are not overall support percentages.
- DirectText and a font-metrics backend are required, with no silent atlas substitution. Raster requests above512 physical pixels fail explicitly; existing3D vector rendering and quality remain intact.
- Metrics admission retains at most64 paths of up to2,048 characters; other requests still calculate actual metrics without admission. Existing process-lifetime font-face resources are not fully resolved.
- Multiline substring allocations remain. No FPS, CPU, Private-memory or Pi improvement was measured in this phase. The previous retained-ID residency cost and full-RC3 GPU comparison failure remain unresolved.

Evidence: `artifacts/fluent-typography-2026-11-05/verification.json`, image-comparison.json, native-gallery-run.json and source-hashes.json. The full goal remains active.

2026-11-12 follow-up — [Content measurement contract](widget-cpu-retention.md): deterministic sources/presentations participate in parent retention; dynamic string/badge getters and custom traits default to fresh measurement. Declared getter Progress geometry and stable Spinner visibility are separate promises; rendering/getters/animation/navigation continue. Focused26 and exact GPU comparisons qualify this scope, not full NativeAOT/performance. Evidence: artifacts/content-layout-retention-2026-11-12/verification.json.
