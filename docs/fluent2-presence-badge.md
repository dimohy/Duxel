# PresenceBadge

`Fluent.PresenceBadge(new UiPresenceBadgeOptions(Status: UiPresenceStatus.Available, Size: UiBadgeSize.Medium))` creates one passive status image. The default is Available, Medium (16 logical pixels), without OutOfOffice. `Options` changes request a frame; equal assignments and unchanged painting request no periodic frames. Status text is cached on mutation. The standard presentation retains measurement; a custom `IUiPresenceBadgePresentation` opts in explicitly.

Eight statuses are supported: Available, Away, Busy, DoNotDisturb, Offline, OutOfOffice, Unknown and Blocked. Tiny/ExtraSmall/Small/Medium/Large/ExtraLarge edges are6/10/12/16/20/28. `OutOfOffice` combines with the status using the official glyph/color map. Light/Dark palettes and background circles follow the pinned Fluent implementation. Actual parent bounds and clipping apply; rectangular allocations center a square. Invalid enum values and invalid presentation extents fail explicitly.

The default icon uses exact compiled Fluent font glyphs at10/12/16/20, scaled for6/28. No XML, runtime icon catalog lookup or reflection is needed. Optional `Icon` borrows a `UiMenuIcon`; for SVG use an existing `UiSvgIcon.MenuIcon` and dispose the widget before its painter/document/store. It reuses the existing SVG cache. The widget never disposes borrowed resources.

Accessibility exposes one nonfocusable Image (Windows50006), with status text as its default name and no Invoke/Value. Set `AccessibleName` for localized wording. There is no hover, click, progress animation or timer. Avatar overlay, transparent cutout, activity rings, motion, forced colors and full reference conformance remain open; this is a standalone badge.

Run `./run-gallery.ps1` from the repository root, select **Avatar**, then **Presence badge**. Buttons change status, OutOfOffice and size. The page scrolls in small windows and keeps state across tab switches.

Reference: pinned microsoft/fluentui commit8830c10bfb2f4c1bdd540287b09713b9679cd1ad, react-badge PresenceBadge source and existing Fluent font assets. Copies/license are in `artifacts/fluent-presence-2026-11-12`. [Official Badge usage](https://fluent2.microsoft.design/components/web/react/core/badge/usage).

Validation: CPU50/50 suites,8086 assertion executions (Presence61,Observer179); generator117; GPU Presence552,Avatar100,Observer3,rounded-image40 across the applicable Light/Dark andMSAA1/4 cases. External owned Windows UIA Presence11 plus five existing profiles66 pass. Tiny red status glyphs were visually reviewed. GPU replay warms each actual swapchain image before checking zero draw/streamed upload; no whole-frame CPU-zero claim. ManagedSDK11.0.100/runtime11.0.0 only; original exactRC3 NativeAOT/full workload gates remain open.

Windows rasterization below10physical pixels now uses existing2x supersampling; grayscale RGB carries coverage like the font shader expects. Both single/mixed-font paths are checked. This corrects lost tiny DND strokes at an increased first-raster cost, documented in [measurement notes](archive/performance/optimization-session-2026-11-12.md). Glyph caching is unchanged; no shader binary change.

Avatar now consumes this presentation directly through `UiAvatarOptions.Presence`, with a real shared transparent cutout and one combined accessible Image. See [Avatar overlay](fluent2-avatar.md#avatar-presence-overlay).
