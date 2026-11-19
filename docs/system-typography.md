# System typography

The [verification record](../artifacts/fluent-messagebar-2026-11-15/system-typography-verification.json) separates host checks from native runtime acceptance. It includes 192 fractional-position GPU captures at 100%/114% text scale: 8/12 unsnapped cohorts changed coverage while 12/12 snapped cohorts stayed stable. Another 36 comparisons compose independent native RGB masks on the CPU over dark, light and colored backgrounds and match GPU output within one 8-bit channel step. This checks raw coverage blending; native GDI/Direct2D gamma-policy equivalence is a separate claim.

`IPlatformBackend.ContentScale` controls display DPI. `TextScale` independently controls accessibility/desktop font size and defaults to 1 for custom backends. The application passes `TextScale` into `UiTextSettings.Scale`; Fluent natural text measurements grow with the glyphs. A scale change invalidates retained layout measurements and layers. Explicit fixed-size constraints still belong to the application.

Windows reads `UISettings.TextScaleFactor` through native WinRT, without a new managed package. Its native change event wakes the owner thread; the render thread reads the cached value. Existing per-monitor DPI handling remains separate. Text scaling is uniform across the application typography hierarchy; it does not implement WinUI's nonlinear heading curve or replace application font families.

Linux X11 reads its own screen's XSETTINGS manager: `Gdk/WindowScalingFactor`, `Gdk/UnscaledDPI`, `Xft/DPI`, and `Gtk/FontName`. The font description is parsed by Pango, including point/absolute pixel sizes. Desktop body size is relative to Fluent's 14-DIP Body1 reference; other typography sizes retain their relative proportions. If XSETTINGS omits DPI, the X resource `Xft.dpi` applies; an unconfigured server uses 96 DPI. GTK's unset-DPI value -1 means unspecified. Linux does not read Windows host settings or execute settings commands every frame.

Manager discovery/subscription/property reads run under a short X server grab. Property and manager events refresh the complete settings snapshot, including manager replacement/removal; event-wait mode has no periodic settings polling. AT-SPI component/image/text coordinates convert between logical layout and physical desktop pixels.

Raster text honors `PixelSnap` by translating the finished bitmap onto the physical pixel grid. Native glyph advances and relative glyph positions remain intact. Plain text, Fluent typography, and native styled paragraph runs share the placement rule. This avoids fractional scrolling resampling/coverage changes. `PixelSnap = false` preserves fractional placement; vector text keeps its continuous transforms.

Validation entry points use the existing local-source graphics and Linux test builds:

```powershell
./Duxel.Graphics.Tests.exe <fresh-output> system-typography-only
./Duxel.Graphics.Tests.exe <fresh-output> raster-text-scroll 2
./Duxel.Graphics.Tests.exe <fresh-output> text-background-gpu
dotnet ./Duxel.Linux.App.Tests.dll desktop-typography <fresh-output>
```

The Linux parser/resolver check runs without native Linux libraries. It does not establish X11/Pango notification behavior on a real desktop. Real OS setting changes, Linux runtime acceptance, and NativeAOT qualification remain separate from managed host checks.

Sources: [Windows text scaling](https://learn.microsoft.com/windows/apps/develop/input/text-scaling), [XSETTINGS](https://specifications.freedesktop.org/xsettings/latest/), [GTK settings names](https://github.com/GNOME/gtk/blob/gtk-3-24/gdk/x11/gdksettings.c), [native WinRT ABI](https://github.com/microsoft/win32metadata/blob/main/generation/WinSDK/RecompiledIdlHeaders/winrt/windows.ui.viewmanagement.h).
