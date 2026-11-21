# Windows text shaping

Native Windows composition ownership stays with its starting input across focus changes. Preedit is hidden from other inputs, and late accepted single-line commits retain their cursor when focus returns. [Routing contract, native message checks and remaining protocol work](windows-ime-routing.md).

Ordinary characters, clipboard paste, and IME commit share single-line insertion completion. The final sentence preserves native trailing caret affinity after selection replacement and length limits. Character batches process grapheme boundaries in order before preparing the final native line. [Ordinary insertion verification](../artifacts/fluent-messagebar-2026-11-15/single-line-insertion-verification.json).

Single-line IME commit retains the native trailing edge of the committed text in its new sentence. This fixes jumps in the caret, candidate rectangle, and accessibility coordinates at bidi boundaries. Composed dragging also computes its scroll range from the complete display sentence. The [commit/pointer verification record](../artifacts/fluent-messagebar-2026-11-15/ime-commit-verification.json) distinguishes actual input-widget checks with a scripted IME handler from native OS IME protocol acceptance.

OS text scaling is independent of display DPI. Plain strings, Fluent typography, and styled paragraph raster origins honor physical `PixelSnap`, reducing fractional-scroll edge changes while preserving relative native glyph positions. See [system typography](system-typography.md).

IME preedit joins the prefix and suffix in one display string without changing committed source. Native trailing caret edges, underline fragments, full width, host candidate-window caret and accessibility coordinates share this line. Unchanged preedit reuses its display string and geometry; commit writes once and retires composition state. Actual input widgets with a synthetic IME handler pass 36 Latin/Hebrew/Arabic/Korean checks and 32 independent pixel pairs. Automated interaction with OS IMEs and fresh Linux native execution remain separate. [IME evidence](../artifacts/fluent-messagebar-2026-11-15/single-line-ime-verification.json).

Windows rasters return tight vertical ink bounds and their relative baseline. The common path also fixes switch labels displaced by transparent top padding. [Switch evidence](../artifacts/fluent-messagebar-2026-11-15/windows-switch-vertical-verification.json).

Windows qualification includes 19 focused single-line checks, 4 selector checks and 16 independent input pixel pairs. The related 680-pair GPU suite, 4,642 widget checks excluding resizing, and 44 actual Observer scenarios passed. These scopes overlap and are separate from product completion criteria. [Evidence](../artifacts/fluent-messagebar-2026-11-15/single-line-native-verification.json).

The validation tool's `text-accessibility-only` profile checks actual Windows layout/raster metric consistency before accessibility checks. It exercises 594 font/size/Unicode/multiline combinations before the broader GPU widget suite is needed.

Raster placement includes the horizontal origin, while glyph analysis preserves the original native baseline. The duplicate icon-only origin adjustment was removed. When numeric input reformats its committed text, painting and accessibility prepare geometry for that final text. Variation selectors retain source spelling in the preceding font span using the ranges in [Unicode17 PropList](https://www.unicode.org/Public/17.0.0/ucd/PropList.txt). Emoji/Korean test cases explicitly declare fonts with the required glyphs; application defaults and missing-glyph failures remain unchanged.

Single-font literal strings now use the same native paragraph shaping as multi-font strings. The separate short single-font raster/vector branches were removed so kerning, combining text and fractional original glyph-run origins follow one contract. Acceptance uses independent system shaping versus actual input pixels; focused qualification remains distinct from whole-product completion.

Single-line inputs retain unchanged native line geometry for the edit buffer's lifetime. Ordinary pointer hits, carets, bidi selection fragments, horizontal scrolling and accessibility extents use the same coordinates; IME display strings also retain whole-sentence context. Left/Right and key repeat follow physical screen direction; Shift preserves the source anchor. Delete, Backspace and Ctrl+word navigation retain logical source order. Inspect **Input and IME · combined → Single-line caret example** for Korean, combining marks and mixed Hebrew. Automated OS IME interaction and fresh Linux native qualification remain separate. No new performance measurements are claimed.

Last synced: 2026-11-16

The [styled paragraph API and literal UI consumer](styled-paragraphs.md) share one native layout for wrapping, font/weight ranges, carets and original raster/vector glyph painting on Windows and Linux. Inspect the Typography Observer samples. Full Markdown/atomic-flow/UI-direction integration remains open.

Latest cross-font qualification: font coverage intervals now stay in one explicit-font TextLayout, preserving bidi context across Korean/Latin/Hebrew font changes. The shared paragraph engine owns single-file font collections and captures visual absolute glyph positions plus the original raster run's order, direction and fractional baseline origin. Raster analysis needs that original origin: moving it into offsets changes hinted pixels. Long rows retain native absolute positions and convert to the legacy offset format only once, avoiding an observed float-precision regression. Single-face long shaping uses the same collection owner; the obsolete separate geometry adapter was removed.

Passed16 focused checks, including exact RGB bytes against analysis of the unmodified native glyph runs, vector origins, original carets, actual Core consumption and empty geometry. Existing shaping52, geometry29, related CPU155, styled199, composite416, multiline2560, Markdown312, GPU152 image pairs and SVG14 also pass. The shared Input/IME example now includes an RTL embedding across the Korean font interval. This is a font-context foundation, not completion of mixed-style Core paragraphs, explicit paragraph direction, wrapping or visual keyboard navigation. [Evidence](../artifacts/fluent-messagebar-2026-11-15/paragraph-font-context-verification.json).

The range contract follows [SetFontCollection](https://learn.microsoft.com/en-us/windows/win32/api/dwrite/nf-dwrite-idwritetextlayout-setfontcollection), [SetFontSize](https://learn.microsoft.com/en-us/windows/win32/api/dwrite/nf-dwrite-idwritetextlayout-setfontsize) and Microsoft's [public dwrite.h](https://github.com/microsoft/win32metadata/blob/main/generation/WinSDK/RecompiledIdlHeaders/um/dwrite.h). Returned managed arrays retain no COM objects. No Windows Skia/HarfBuzz dependency was added; new performance gains are unmeasured.

Latest line geometry qualification (2026-11-16): all4 recorded Windows caret defects are fixed. Uniform-style rows retain native complete-row geometry for actual carets, click affinity, selection fragments, composition underlines and span bounds. Windows focused29, shaping52, multiline2560 and Markdown312 pass. Linux now implements the same geometry capability with private Pango font maps and passes native334, baseline114 and Observer41/delivered38. See [Linux scope and dependencies](linux-text-shaping.md). Full mixed-style/cross-font paragraph composition, explicit base direction and wrapping integration remain open. The failure records below are historical.

Known paragraph geometry defects (2026-11-16):4 of12 complete-row native caret comparisons/retention checks fail:AV kerning and mixed Hebrew in both plain and inline consumers. All6 retention checks and52 existing shaping regressions pass. Paragraph caret/selection correctness is not yet qualified. [Failure evidence and next scope](../artifacts/fluent-messagebar-2026-11-15/paragraph-geometry-verification.json).

Latest qualification (2026-11-16): empty shaped output bypasses GlyphRunAnalysis. Core preserves valid zero advances,rejects invalid metrics before caching,and disposes exclusive pixels on failure. Publish53 passed52 expanded checks. [Evidence](../artifacts/fluent-messagebar-2026-11-15/zero-advance-verification.json). The37-check record below is historical.

Windows bitmap/vector text and SVG text share DirectWrite modules through common source, without a Core-to-SVG dependency. No Windows Skia or HarfBuzz runtime dependency is added. Linux retains its existing HarfBuzz path.

Prepared input above 16384 UTF16 characters selects native TextLayout before any shaping attempt. This is an internal dispatch ceiling, not an asserted native length limit. Bounded inputs retain script/bidi analysis and GetGlyphs/GetGlyphPlacements. Failures do not trigger another font or nominal-glyph substitution.

Long input borrows the caller's font factory and face to create a private collection. In-memory fonts retain their original factory. Every emitted face is checked against the selected file-loader identity, file key, collection index and simulation flags, rejecting system-font substitution. NoWrap DrawGlyphRun output supplies positions, advances and original clusters without arbitrary contextual splitting. Internal TextLayout dimensions neither allocate widget space nor clip emitted glyphs.

Owned layouts, formats and collections are released before unregistering the owned collection loader. The caller's factory and face remain borrowed. Loader/renderer owner references must reach zero; enumerators release owned file references. Independent long-duration native leak measurement remains separate.

Modern Hangul uses Unicode17 section3.12 composition only for glyph input, mapping prepared UTF16 positions back to the unchanged source. LV+T is supported; intervening marks block composition. Zero-width characters do not gain fabricated half-em advances; absent selected-font characters fail explicitly. Preserved clusters do not qualify complete editor caret/selection/native character geometry.

## Qualification and reproduction

[Current evidence](../artifacts/fluent-messagebar-2026-11-15/long-shaping-verification.json): 37 expanded checks pass, retaining the original six expectations and40000-character expectation. Cases include 16383/16384/16385/32767/32768/32769 boundaries,39,000-character Latin,44,000-character Arabic and105,000-UTF16 decomposed Hangul. Checks cover final original Hangul cluster 104997, long oblique simulation, actual SVG memory fonts and compiled-asset reuse. Eleven independent system TextLayout position references, including three long cases, match within unchanged 0.002 DIP tolerance. Only Hangul has explicit composed references; all other oracles receive the exact original input.

Passed 155 related CPU checks,416 composite contracts,152 GPU pairs,14 SVG font checks and38 actual Windows Observer scenarios. SVG GPU checks retain independent ABC outline pixels at MSAA 1/4. Windows/SVG .NET 8/9/10 builds have zero warnings/errors. This does not establish whole-browser SVG pixel equality or a speed improvement.

Reproduce with the pinned local executable on this PC, choosing a fresh result directory:

```powershell
& 'L:/Cache/Duxel/fluent-messagebar-20261115/publish-48/Duxel.Graphics.Tests.exe' 'L:/Cache/Duxel/long-shaping-manual-1' 'windows-shaping-only'
& 'W:/MyWorks/Duxel/run-observer.ps1'
```

An additional visibility check exposed a cluster-read error for native zero-glyph control runs. Respecting the native zero count fixes it. A visible A after 40,000 zero-width characters matches the bounded A across every 32×32 pixel, with nonempty alpha.

Initial 26/27 and 30/33 failures, the visibility failure and the original 40000 failure remain preserved. The long-Latin oracle input mismatch was corrected to the original source without relaxing tolerance. Memory-font factory ownership and dispatch boundaries were corrected and qualified.

Whole-paragraph bidi, wrapping, editing and native character geometry across multiple font/style spans remain incomplete. MessageBar stays 12/13 with RTL contract 007 pending. Windows NativeAOT retains its separate toolchain gate. Linux delivery c8246d remains protected; no new Linux runtime or performance claim is made.

Official references: [glyph placement](https://learn.microsoft.com/en-us/windows/win32/api/dwrite/nf-dwrite-idwritetextanalyzer-getglyphplacements), [custom collections](https://learn.microsoft.com/en-us/windows/win32/directwrite/custom-font-collections), [source clusters](https://learn.microsoft.com/en-us/windows/win32/api/dwrite/ns-dwrite-dwrite_glyph_run_description), [Unicode17 Hangul composition](https://www.unicode.org/versions/Unicode17.0.0/core-spec/chapter-3/).
