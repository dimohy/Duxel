# SVG platform rendering

2026-11-15: SVG widgets/icons now use a retained Vulkan GPU scene and visible-region camera. The CPU raster/LRU descriptions below apply to explicit bitmap export or the earlier checkpoint. See [current architecture and remaining pixel qualification](svg-gpu-camera.md).

SVG XML is read once per source revision and compiled into immutable geometry, paint and glyph outlines. Requested raster sizes use the existing bounded LRU; returning to a cached size reuses the same pixels. `SvgStore`, `SvgImageDecoder`, `UiSvg` and `UiSvgIcon` retain their public contracts.

| Platform | SVG geometry and paint | Text | Embedded bitmaps | Additional native SVG deployment |
| --- | --- | --- | --- | --- |
| Windows | Managed C# | System DirectWrite | System WIC | No Skia, HarfBuzz or third-party image DLL |
| Linux | Managed C# | HarfBuzz14.2.0 native assets | Installed GdkPixbuf | HarfBuzz; existing GdkPixbuf/GLib/GObject requirements remain |

Windows snapshots explicit font bytes through the DirectWrite in-memory loader. It validates the collection index and actual OpenType style, shapes script runs and records glyph outlines. It does not discover or synthesize an unrelated font. Linux uses the same font catalog and ownership rules with HarfBuzz. `SvgFontStatistics` counts registration, font requests and cached coverage work. Raster replay and enlargement do not repeat that work.

The Windows font snapshot path requires Windows10 build14393 or later. This is a system API requirement and does not add a redistributable font library. macOS and ARM64 are not qualified by this checkpoint; embedded macOS bitmaps are not implemented.

Supported, tested routes include paths with smooth/elliptical curves; basic shapes; winding; transforms; fill/stroke/caps/joins; CSS; linear/radial gradients; local use/symbols; patterns; basic clipping/luminance masks; endpoint markers; embedded bitmaps; explicit fonts, spans, RTL, text paths and local text references. Subsequent work adds dashes/offsets/positive pathLength, gradient/pattern templates and a single Gaussian blur. Other filters, middle markers, some units and advanced paint/effect/text attributes remain unfinished. This is not complete SVG specification coverage; a passing sample does not qualify every SVG document.

Font outlines now remain vector geometry through compilation. The old backend's hinted glyph bitmap is retained as historical evidence, not used as a font rasterizer. The GPU reference instead uses an independently captured native vector outline, bound to the exact reference font SHA-256. The full-pixel tolerance remains one channel value. This separates font selection/geometry from the rendering engine's antialiasing policy.

Validation evidence: `artifacts/svg-managed-2026-11-14/verification.json`, `legacy-oracle.json`, and `tests/Duxel.Graphics.Tests/Fixtures/SvgFontOutlineReference.svg`. Current checks cover114 Windows SVG contracts,76 real Vulkan GPU checks at MSAA1/4,288 Observer CPU checks,7 owned Observer-window checks,18 Windows native-platform checks and17 Linux native-platform checks. The Windows fixture verifies that no Skia/HarfBuzz native module is loaded; deployment is also inspected independently.

The managed Windows Observer is built at:

```powershell
Start-Process -FilePath 'L:\Cache\Duxel\linux-accessibility-2026-11-14\observer-managed-build\bin\Duxel.Gallery\release\Duxel.Gallery.exe' -WorkingDirectory 'W:\MyWorks\Duxel' -WindowStyle Normal
```

Open the SVG tab, including text and document features. This is a .NET11.0.100 managed validation build, not the fixed RC3 NativeAOT comparison. No normal Observer window was automatically launched.

References: [DirectWrite in-memory snapshots](https://learn.microsoft.com/en-us/windows/win32/api/dwrite_3/nf-dwrite_3-idwriteinmemoryfontfileloader-createinmemoryfontfilereference), [DirectWrite shaping](https://learn.microsoft.com/en-us/windows/win32/api/dwrite/nf-dwrite-idwritetextanalyzer-getglyphs), [HarfBuzz](https://harfbuzz.github.io/what-is-harfbuzz.html), [SVG paths](https://www.w3.org/TR/SVG2/paths.html), [SVG paint servers](https://www.w3.org/TR/SVG2/pservers.html).

License notices are included as text on both platforms because referenced libraries are built without the caller's runtime identifier during content collection. This adds no native DLL or package dependency on Windows. Native HarfBuzz deployment remains Linux/macOS-specific.

The counts above describe the dependency-replacement checkpoint. Subsequent browser-equality work, shared Core RGBA8 conversions, Vulkan image filtering and modular SVG paint/stroke/filter handling are covered by the [browser comparison guide](svg-browser-validation.md). Transparency is stored as premultiplied RGBA8 before straight-alpha export; equivalent pixels can have different straight-alpha RGB representations. Full-document equality remains unfinished.

