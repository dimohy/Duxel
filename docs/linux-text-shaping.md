# Linux text shaping and line geometry

Last synced: 2026-11-16

The [styled paragraph capability](styled-paragraphs.md) now feeds literal `UiInlineText`. Pinned .NET11 RC3 NativeAOT passes351 styled,334 original geometry and114 text-baseline checks. One native snapshot supplies wrapping, explicit base direction, source carets, raster/vector positions and retained Core layout. Markdown and atomic children remain open. Inkless glyphs stay in layout snapshots but are excluded from cropped Cairo paint to preserve native fringe coverage. Pango1.50 or later is required and checked at backend construction.

The Linux text backend now uses Pango for complete-line script itemization, bidi ordering and HarfBuzz shaping. Raster, layout and vector output consume the resulting visual glyph runs through the existing Cairo/FreeType and HarfBuzz outline modules. Windows continues to use DirectWrite without these Linux dependencies.

Each backend retains at most16 private font-map sets. Each empty Fontconfig configuration registers only the explicitly supplied font files; it never replaces the process-global configuration or searches for a substitute. The existing primary-first coverage rule assigns contiguous font attributes before shaping. Every returned run must match the declared font file and face index. Missing glyphs fail explicitly. Adjacent identical font assignments stay together so kerning, ligatures and Hangul composition are preserved.

`IPlatformTextGeometryBackend.CreateLineGeometry` returns an immutable original-UTF16 snapshot. Uniform-style Core rows reuse its caret positions, disjoint visual selection fragments and native pointer transitions. A native pointer transition can differ from the visual midpoint for combining marks and Arabic; `PlatformTextCharacterGeometry.HitBoundaryX` records that transition during construction. Later hit tests need no native layout. Native layouts and iterators are released before the snapshot returns. Tabs remain one source character and expand to four spaces for drawing.

Cairo grayscale coverage can extend one device pixel beyond nominal ink metrics. The raster allocation includes that coverage guard without changing logical advance; empty output remains empty. The independent oracle draws the whole native Pango layout directly and requires exact coverage bytes, rather than comparing the product renderer with itself.

## Dependencies

Linux needs FreeType, Cairo, HarfBuzz, Pango>=1.50, PangoCairo, PangoFc, Fontconfig and GLib/GObject. Qualification used Ubuntu Pango1.52.1 and HarfBuzz8.3.0. These are native Linux libraries, not Skia or managed GUI framework dependencies. Typical Ubuntu runtime packages include `libpango-1.0-0`, `libpangocairo-1.0-0`, `libpangoft2-1.0-0` and `libfontconfig1`, in addition to the existing FreeType/Cairo/HarfBuzz packages. No system package installation or global font configuration change was performed for this qualification.

## Run and inspect

From the Windows repository root:

```powershell
& 'W:/MyWorks/Duxel/artifacts/linux-present-path-2026-11-15/run-observer.ps1'
```

The verified manifest selects NativeAOT executable `7746d0b7` and checks its full hash. The launcher supplies the five explicit fonts and limits llvmpipe workers to4 in the launched process. Open **Input and IME · combined** and select **Kerning and mixed-script example** to inspect kerning and mixed-direction text. Older delivery `ce552ef3` is preserved separately.

The focused `paragraph-geometry` profile in `tests/Duxel.Vulkan.Linux.Tests` takes a fresh output directory, sample count1, the five explicit regular/bold/Korean/monospace font paths, then the profile name. It needs no GUI. The native suite passed334 checks:11 text cases at five sizes, each with advance, exact native pixels, original-grapheme carets, retained Core consumption, native hit/affinity and native selection coverage, plus four resource/font-isolation checks. Text baseline114, actual candidate Observer41 and delivered-launcher38 scenarios also passed. Windows related geometry/shaping/input/Markdown regressions passed. [Evidence](../artifacts/fluent-messagebar-2026-11-15/linux-paragraph-verification.json).

This qualifies the tested uniform-style rows with an LTR paragraph base and explicit font selection. Mixed-style paragraph composition, complete wrapping/base-direction integration, visual keyboard navigation, color fonts, variation sequences, full accessibility and physical input/display latency remain open. No new speedup or performance non-regression is claimed. MessageBar remains12/13; this is not whole-project completion.

Native references: [private font-map configuration](https://gnome.pages.gitlab.gnome.org/gtk/PangoFc/method.FontMap.set_config.html), [glyph runs](https://docs.gtk.org/Pango/struct.GlyphItem.html), [character geometry](https://docs.gtk.org/Pango/method.Layout.index_to_pos.html), [pointer positions](https://docs.gtk.org/Pango/method.Layout.xy_to_index.html), [visual selection ranges](https://docs.gtk.org/Pango/method.LayoutLine.get_x_ranges.html).
