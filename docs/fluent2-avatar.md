# Fluent Avatar

[한국어](fluent2-avatar.ko.md)

## Korean initials and status-circle edge correction

`Fluent.Avatar("정세일")` displays **정** when `Initials = null`. The first Hangul text element is composed to NFC, including decomposed Hangul. This intentionally extends the original Fluent rule that excluded automatic Hangul, following the Korean-use requirement. Derivation runs on property changes, not every frame. Explicit `Initials` still overrides derivation; an empty string selects icon mode. An assigned image takes precedence.

The Observer **Try Korean initials** preset now derives from the name rather than fixing a literal initial. Run `./run-gallery.ps1` → **Avatar** → **Show avatar initials**; enter `정세일`. Compare presence, size and left/right placement controls.

The clipped right side came from discarding the font bitmap's negative horizontal origin. `PlatformTextLayoutResult.OriginX` / `PlatformTextRasterizeResult.OriginX` now survive caching and adjust font-icon placement. Layout/raster origins must agree. Avatar/parent clips stay authoritative and badge dimensions are not artificially reduced. Vector rendering and ordinary text placement preserve their existing contracts.

Validation: CPU55/8402; twelve GPU profiles2289 (Avatar688,Presence552); external native Avatar23/Card14. Includes48 curved-edge checks and8 automatic/explicit Korean rendering comparisons. Evidence: `artifacts/avatar-korean-presence-2026-11-12/verification.json`. Whole-goal and fixed RC3 deployment gates remain open.

## Inactive appearance

Set `avatar.Options = avatar.Options with { IsInactive = true };`. The complete body and presence badge are captured at0.875 final raster resolution, centered at0.875 visual scale, and composited once at opacity0.8. Activity ring/shadow options are preserved but hidden; their extents are omitted from natural measurement and their activity wording is omitted from the default accessible name. Restoring `IsInactive=false` restores those options. The actual parent allocation remains the layout and single passive UIA Image rectangle. Inactive appearance does not disable the widget or add an input action.

The widget owns its `UiPaintGroup`. Unchanged image/initial/default-person content reuses the capture, including on unrelated requested frames. Cache inputs include actual image source identity, content/options, theme colors and configured typography. SVG icons supply their regular document revision; document changes still request a frame. Other icon painters are evaluated on each requested frame unless explicitly versioned with `icon.WithContentVersion(version)`. Change that version and assign the new icon when external paint inputs change. Custom Avatar presentations default to `CanRetainPaint=false`; opt in only for pure painting from supplied content/options and configured theme/typography. There is no periodic timer. Group ownership follows normal widget/context disposal and queued-frame leases.

Run `./run-gallery.ps1` → **Avatar** → **Toggle inactive avatar**. Image, SVG, Korean initials, presence, ring and shadow controls remain available. Managed qualification: CPU54/8283, GPU1374 assertion executions, external native Avatar23;635 prior deterministic images exact. Across each Light/Dark×MSAA1/4 matrix,28 new inactive definitions perform56 initial offscreen passes and zero capture passes for unchanged replay. This is capture reuse evidence, not a latency/memory reduction or original RC3 NativeAOT performance result. Evidence: `artifacts/inactive-avatar-2026-11-12/verification.json`. Motion, forced colors, full RTL/Fluent conformance and original distribution/performance gates remain open.

`Fluent.Avatar` is a passive person/group image. It supports fourteen Fluent sizes, circular and square shapes, immutable decoded image sources, automatic or explicit initials, and a borrowed icon. It does not register an input action, keyboard stop, hover state or animation timer.

```csharp
using var avatar = Fluent.Avatar("Ada Lovelace",
    new(UiAvatarSize.Size64, UiAvatarShape.Circular, UiAvatarColor.Colorful));
avatar.Source = imageSource; // Already decoded PNG/SVG pixels; centered Cover.
avatar.Render(ui);
```

Image content takes precedence over initials; an icon is selected when neither is present. The default person icon uses the existing bundled Fluent icon font, through a compiled catalog painter. `Icon = svgIcon.MenuIcon` borrows the existing SVG painter: keep its owner alive until all borrowers are disposed. No image decode error is hidden or converted to another asset.

`Initials = null` derives from Name; an explicit empty string selects the icon. Size16 derives one initial; other sizes derive up to two. Hangul names derive their first complete syllable automatically; Arabic, BMP CJK/Japanese and phone numbers remain excluded. To display Korean explicitly, set `Name = "김민수"; Initials = "김";`. Full Name is still exposed to accessibility tools. Explicit initials preserve their text; the viewport clips unusually long values. Name cleanup and deterministic UTF-16 color hashing run on property changes, not every frame. RightToLeft reverses automatically derived initials and anchors an optional presence badge on the left.

`UiAvatarColor` supports Neutral, Brand, Colorful and thirty named colors. Colorful derives from `IdForColor ?? Name`; it uses the pinned web hash and palette. Text uses semibold Fluent metrics. Square corner radii are2/4/6/8 for size bands<=24/<=48/<=72/larger. The assigned parent rectangle remains authoritative; the built-in presentation centers a square inside it. Its image uses the common analytic rounded viewport and Cover UVs without CPU resampling, source redecoding or another texture upload for size/shape changes.

The widget owns its internal image lease and borrows the immutable Source and optional icon painter. Setting Source to null releases that lease. Avatar and Image share one texture for the same source object within a context. Explicit retained layers keep their normal invalidation and resource-lifetime contracts. `UiAvatar<TPresentation>` accepts custom static Measure/Paint implementations; measurement retention defaults false, invalid extents fail, and painting receives the actual parent allocation and clip. Content.Image is borrowed for painting and must not be mutated or disposed by a presentation.

A named Avatar publishes one native UIA Image(ControlType50006), with no Invoke/Value or keyboard focus. The common collector delivers committed Name changes. Omission invalidates old native providers; disable/re-enable preserves identity. Set the inherited AccessibleName when a separate description is required.

Run `./run-gallery.ps1` from the repository root and choose **Avatar** (use the overflow menu in a narrow window). The page exposes name, shape, size, color, image, SVG icon and Korean initials; a gallery shows all fourteen sizes. State survives page changes and idle rendering requests no periodic frames.

Current base qualification is under `artifacts/fluent-avatar-2026-11-12/`. Activity rings and presence overlays are described below. Transitions, forced colors, complete RTL/Fluent conformance and original NativeAOT/performance gates remain open. This exposed factory is not a claim of complete web Avatar feature parity.

References: [official usage](https://fluent2.microsoft.design/components/web/react/core/avatar/usage), [pinned props](https://github.com/microsoft/fluentui/blob/8830c10bfb2f4c1bdd540287b09713b9679cd1ad/packages/react-components/react-avatar/library/src/components/Avatar/Avatar.types.ts), [pinned styling](https://github.com/microsoft/fluentui/blob/8830c10bfb2f4c1bdd540287b09713b9679cd1ad/packages/react-components/react-avatar/library/src/components/Avatar/useAvatarStyles.styles.ts). Sources and MIT license are retained with the qualification artifacts.
Qualified results:49contracts/8021assertions(Avatar110,Observer175),GPU100 acrossLight/Dark×MSAA1/4,Observer3,existingroundedImage40,nativeAvatar10 plus existing56. Visual review caught an inadequate test fixture: missing Malgun secondary typography rendered a box while the old pixel-difference check passed. Shared fixtures now match the actual application defaults. A controlled missing-font capture and restored Hangul equality are permanent GPU regressions; before/after screenshots remain in the evidence directory. No product font substitution or automatic installation was introduced.

## Standalone presence badge
`Fluent.PresenceBadge` supports eight statuses, OutOfOffice and six sizes. Run `./run-gallery.ps1` → **Avatar** → **Presence badge**; change status, OutOfOffice and size. The scrollable page preserves state and requests no periodic frames. Default icons use compiled Fluent font glyphs; supplied SVG painters reuse their cache. [API, resource lifetime and validation](fluent2-presence-badge.md). Avatar overlay and activity ring are described below; inactive appearance is described above.

## Activity ring and gallery scrolling

Set `avatar.Options = avatar.Options with { ShowActivityRing = true };` and optionally `avatar.ActivityText = "active";`. The default name gains the activity wording; `AccessibleName` still overrides it. The label is cached on mutation. A ring remains one passive Image with no action, focus, hover or animation timer.

The body keeps the selected14-size contract at natural size. Native layout measures the complete visual extent: stroke2 for body sizes<=48,3 for<=64,4 above64; the ring extends two stroke widths on each side (32px body→40px total). This reserves the decoration instead of painting over neighboring allocations. The body and gap scale uniformly under tighter parent allocations. Circular/square geometry and Light/Dark ring colors reuse the existing analytic border-only primitive and pinned persona palette. The gap paints nothing; it preserves the underlying pixels. Image sizes and ring toggles reuse the existing texture lease. `ShowActivityRing=false` restores the ordinary avatar; it does not claim Fluent inactive appearance.

Run `./run-gallery.ps1` from the repository root→**Avatar**→**Toggle activity ring**. The sample sets Korean activity wording. The page now uses the common `UiScrollView`, allowing its growing gallery to exceed the viewport without flex overflow. Actual scroll-to-bottom and offset/model preservation across tabs are checked at800×400.

Current evidence: `artifacts/fluent-avatar-ring-2026-11-12/verification.json`, managedSDK11.0.100/runtime11.0.0. Full Fluent active/inactive transitions, forced colors and original exactRC3 NativeAOT/full-workload qualification remain open. [PresenceBadge](fluent2-presence-badge.md) is available standalone and as the Avatar overlay described below, with a shared transparent cutout across image/initials/icon/ring paths.

## Avatar presence overlay

Set `Presence` on `UiAvatarOptions` to `new UiAvatarPresenceOptions(UiPresenceStatus.Available)`; null removes it. `Size = null` selects the pinned automatic badge size: avatar <28 →6px, >=28 →10px, >=40 →12px, >=56 →16px, >=64 →20px, >=96 →28px. An explicit `UiBadgeSize` overrides this mapping. The badge anchors to the body's bottom/right, or bottom/left with `RightToLeft`. The transparent gap is1px through Medium and2px for Large/ExtraLarge. Image, initials, font/SVG icon and activity ring share the common analytic circle subtraction; the badge paints after that scope is disposed. No background-colored patch is used.

Natural measurement reserves the union of body, activity ring and badge, including an explicit badge larger than the body. The union fits uniformly inside the actual parent rectangle. `PresenceText = null` uses the status wording; set a nonempty complete localized status (including OutOfOffice when applicable) to override it. The cached accessible name combines person, status and activity; `AccessibleName` overrides it. There is still one passive Image provider, no child badge provider, focus stop, Invoke or animation timer. Existing source leases and borrowed icon ownership apply.

Run `./run-gallery.ps1` → **Avatar** → **Toggle avatar presence badge**. Controls change status, OutOfOffice, automatic/explicit size and RTL; image, Korean initials, SVG icon and ring controls remain available. State persists across tabs. Managed validation and limits are recorded under `artifacts/avatar-presence-2026-11-12/`. Motion/forced colors, full Fluent conformance and original exactRC3 NativeAOT/performance gates remain open.

Validation:51/51 CPU contracts,8193 assertion executions(Avatar185,Observer191);Avatar GPU380 across Light/Dark and MSAA1/4,Observer11,native Avatar17. Existing233 Avatar images are byte-identical. This is managedSDK11.0.100/runtime11.0.0 qualification. External draw callbacks and distinct nested circle scopes remain unsupported by the common mask contract.

## Avatar activity shadow

Set `ShowActivityShadow = true` independently of `ShowActivityRing`. Both add the cached ActivityText once to the same passive Image name. The pinned body-size mapping is <=28→Shadow4,<=48→Shadow8,<=64→Shadow16,larger→Shadow28. Shadow-only uses the body contour;with a ring it uses the complete ring contour. The measured union reserves shadow support,ring and an optional oversized badge,then fits uniformly to the actual parent rectangle. The shadow paints before body/ring subtraction and the badge,matching the pinned shadow pseudo-element. Image/initials/font/SVG all use this shared path.

Run `./run-gallery.ps1` → **Avatar** → **Toggle activity shadow**. State persists across tabs and no periodic frames are requested. [Common shadow API](box-shadow.md). Inactive0.875scale/group0.8opacity is described in the current inactive contract; transitions/forced colors remain open;existing per-command layer opacity cannot substitute for group composition. Current managed evidence: `artifacts/box-shadow-2026-11-12/`.

Validation:CPU52/52 contracts,8247 assertion executions(Shadow37,Avatar201,Observer192);four explicitly verified compiled profiles x256shadowGPU checks,Avatar392,cutout88,primitive74,rounded40,Observer12;nativeAvatar19. Assertions repeat across settings. Existing498 image files remain byte-identical. Sample description uses the common wrapped text presentation,including the800x400 scroll regression.
