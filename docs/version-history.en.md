# Version History (English Summary)

> Synced: 2026-02-26  
> Korean full history: [version-history.md](version-history.md)

This page is an English summary of key releases. The Korean document is the full, detailed changelog.

## Documentation Update (2026-02-26)

- Reworked [../README.md](../README.md) as English-first and added [../README.ko.md](../README.ko.md).
- Rewrote DSL docs around current parser/runtime behavior.
- Updated FBA guides to match current sample directive (`Duxel.$(platform).App`).
- Added sync timestamps across major docs.

## 0.1.13-preview (2026-02-20)

- Fixed compatibility by changing `Duxel.Windows.App` / `Duxel.Platform.Windows` target from `net10.0-windows` to `net10.0`.
- This removes NU1202 mismatch in `net10.0` FBA runs and keeps cross-platform test paths open.

## 0.1.12-preview (2026-02-20)

- Added DirectWrite text rendering path and runtime toggle.
- Completed Windows platform backend split (GLFW path removed).
- Added animation helper APIs and multiple sample/API uplift improvements.

## 0.1.11-preview (2026-02-17)

- Expanded benchmark automation and performance logging policy.
- Improved repeatability for cache/clip strategy comparisons.
