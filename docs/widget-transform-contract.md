# Common widget transform contract

Status : common `UiWidget.Margin` and `UiWidget.Transform` are implemented. Representative buttons, checkbox, slider, editor, nested clipping and accessibility point geometry are verified; SVG Zoom/RotationDegrees now forward to the common Transform. Full widget, complex text shaping, popup and screen-reader conformance remains incomplete.

## Common margin usage

```csharp
var button = Fluent.Button("Run", Run);
button.Margin = new UiEdgeInsets(12, 8, 20, 10);
```

Edges are left/top/right/bottom. Every UiWidget inherits the same property; the SVG-specific Margin implementation was removed. Values must be finite and nonnegative. Margins participate in measurement and flow, but remain outside content hit, hover and accessibility bounds. Mutation invalidates retained parent measurement. Default zero margins introduce no render wrapper.

The Implementation Observer `위젯 배치` tab adjusts each edge in 1px steps and exercises a button, checkbox and slider. `공통 여백 초기화` resets all edges to zero. Fifteen focused checks cover button/checkbox activation, editor input/caret movement, keyboard focus, empty-margin rejection, constraints, measurement invalidation, Build composition and empty-content lifetime. These checks do not qualify the entire transform contract.

Launch the current Windows managed build below, then select `위젯 배치`.

```powershell
& 'L:\Cache\Duxel\linux-accessibility-2026-11-14\observer-repaint-build\bin\Duxel.Gallery\release\Duxel.Gallery.exe'
```


## Common transform usage

```csharp
button.Transform = new UiWidgetTransform(Scale: 1.5f, RotationDegrees: 90);
button.Transform = new UiWidgetTransform(RotationDegrees: 30,
    Origin: new UiVector2(0, 0), Mode: UiWidgetTransformMode.Visual);
```

Default Layout reserves transformed bounds for following widgets. Visual preserves the original layout size and anchor while the origin changes painted position. It avoids self-allocation clipping but still respects parent clips. Tight Layout normalization may cancel the translation introduced by the origin.

UiWidgetTransform is immutable; assigning equal values introduces no invalidation. The Identity fast path adds no capture. Common affine geometry connects measurement, paint, inverse input, accessibility and IME. Source rounding stays separate from exact transformed parent masks. Natural flow without a window or allocation uses unbounded maxima; an explicitly zero-sized parent stays zero. Remaining scroll viewport height is not mistaken for child allocation.

SVG owns document and raster caches; nested transform scale contributes to requested raster quality. Rotation and margins never reparse XML. Host DPI and logical font size remain unchanged. Windows vector shaping for complex scripts and screen-space popup portals remain follow-up work.
## Required behavior

- Browser comparison applies the shared zoom, rotation and margin controls to its actual SVG widgets.
- Rotated/scaled bounds participate in measurement and flow, without self-clipping or overlap with subsequent items.
- Margin reserves layout space; Translate changes drawing coordinates.
- Content rounding happens before transformation and follows rotation/scale. Parent clipping remains separate.
- Relative rotation origins include top left, center and bottom right.
- These behaviors belong to the common UiWidget path, including genuinely interactive rotated buttons.

## Responsibilities

The shared layer owns margins, affine transforms, pure measurement, arrangement, paint coordinates, inverse input mapping and accessibility coordinates. SVG specializes document/raster resolution and texture lifetime. Its compatibility Zoom/RotationDegrees properties now forward to the shared Transform instead of maintaining duplicate geometry.

Default layout transforms reserve transformed bounds. A separate visual transform preserves the layout anchor and applies the selected origin. Tight layout normalization can cancel an origin's positional effect; never mix the two meanings or silently ignore an origin selection. See the [official transform overview](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/graphics-multimedia/transforms-overview).

## Acceptance evidence

| Area | Required runtime evidence |
| --- | --- |
| Shared consumer | Common UiWidget path and all declarative consumers; copying SVG properties is insufficient |
| Layout | Arbitrary/quarter/negative angles, scaling, per-axis size, four margins, parent constraints, row/column/nested scrolling, retained measurement invalidation |
| Origin | Actual top-left/center/bottom-right motion and the explicit difference between transform modes |
| Painting | Vulkan MSAA1/4, content corners, text/image/SVG/composed widgets, dynamic/static/retained replay, parent clipping |
| Interaction | Real transformed button clicks, rejection in empty bounding corners, dragging, keys, pointer capture and nested inverse mapping |
| Accessibility/editing | Screen bounds, clickable point, character/range boxes, point queries, caret, IME candidate position, screen readers and reveal |
| Overlays/lifetime | Tooltip/menu/Popover anchoring, screen-space popups, disposal, change invalidation and unchanged cache reuse |

The complete widget denominator remains unconfirmed. This design or a few button tests do not establish overall completion. Reuse existing UiLayer3D, allocated-child rendering, input slots, pure measurement and accessibility publication before introducing shared coordinate contracts. Do not expose nonfunctional properties without a verified consuming path.

Affine clip damage snapshots include both the shape and its following transform record. A matrix-only change repairs newly exposed and hidden pixels; unchanged acquired images return to zero draws. Native MSAA1/4 regressions cover rectangular, rounded and circular masks plus dynamic inverse parent/child rotations. Projected widget scenes still use conservative full-scene invalidation; this change does not claim partial redraw for arbitrary 3D scenes.

Transformed popups: on **위젯 배치**, adjust rotation, scale and margins, then use **변환 팝업**, **변환 메뉴** and **변환 도구 설명**. Triggers transform while popup content remains upright in display coordinates. A shared scope separates owner transforms, clipping, pointer, accessibility and IME coordinates and restores them on exit. Deferred tooltips retain screen anchors and caller-owned visibility even with the pointer outside the window. Qualification covers declarative Popover/menu/Tooltip and compatibility BeginPopup/BeginTooltip, modal, Combo and menu paths. Arbitrary 3D-camera combinations and screen-reader conformance remain unqualified.

On 위젯 배치 → 기존 팝업 API, apply shared rotation/scale and exercise open, execute and close. The three legacy Combo overloads share screen-anchor placement. Popup width is the maximum of the original content width and transformed trigger width; transformed owner clipping is replaced by display clipping. Screen popups do not inherit the owner widget's forced vector-text mode.

A screen popup separates text mode from camera layers already open at entry. Camera layers opened inside the popup remain active. Retained layers record their effective vector/screen text mode, so StaticCache and unchanged ContentVersion rebuild on a mode transition and reuse geometry afterward. MSAA1/4 comparisons cover translated/rotated parents, nested 2D layers, transformed widgets and exact mode-transition/restoration pixels. This does not complete the original fixed RC3 NativeAOT, general 3D input or screen-reader qualification.
