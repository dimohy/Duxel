# Fluent 2 RadioGroup

Built-in RadioGroup layouts follow the enclosing `FluentProvider.Direction`. In right-to-left layout, horizontal options start on the right. Side labels and radio indicators swap sides, and stacked labels stay centered. Left and Right follow the displayed order. Vertical arrow behavior and the selected value survive a direction change.

On stable .NET 11.0.0, the Windows parent-allocation suite passed 264 checks each in managed and NativeAOT mode. All 245 RGB GPU readback pairs matched at MSAA 1 and 4 with three frame slots. Evidence: `W:/Cache/Duxel/gallery-source-20261117/radio-ga-1910/comparison.json`.

On November 19, the current Windows managed build and the archived Windows x64 NativeAOT executable each passed 264 checks at 320 × 240. All 245 image hashes matched. Fresh Linux x64 managed and NativeAOT builds each passed 212 checks at 400 × 300, with all 244 images matching. With Windows and Linux both at 400 × 300, four font-free images matched. Each of the 96 raster label comparisons differed by 2,245–2,427 pixels and each of the 96 vector label comparisons differed by 21–755 pixels. Results and the archived Windows executable hash are in `W:/Cache/Duxel/gallery-source-20261117/gallery-uia-2082`.

The Linux platform test now reuses the parent-allocation and generated/immediate GPU fixtures. Run the Linux test executable with a fresh output directory, `radio-allocation`, and the regular, semibold and bold font paths in that order. WSLg Ubuntu x64 with Mesa lavapipe passed 212 checks each on .NET 11 managed and NativeAOT, with all 244 fixture images matching between those Linux builds. The Windows and WSLg framebuffers were 400 × 300 and 320 × 240 respectively. In their shared 320 × 240 region, all four font-free custom allocation images matched. The 240 images containing labels differed. This comparison does not isolate text rendering from other host effects. See `W:/Cache/Duxel/gallery-source-20261117/radio-cross-host-1964/verification.json`.

The `radio-allocation-matched` test mode uses 400 × 300 framebuffers, the same Segoe UI font files, a DirectText base size of 16 and disabled DirectText fallback. Four complete font-free images matched between Windows and WSLg. In the labelled images, the 96 raster cases differed at 2,245–2,427 pixels per frame and the 96 vector cases at 21–755. These counts describe the observed output differences. Glyph placement, coverage and driver effects still need separate checks. See `W:/Cache/Duxel/gallery-source-20261117/radio-matched-1965/verification.json`.

A separate .NET 11 managed probe used the same Segoe UI Regular bytes with seven sample labels at 14 and 16 px. It called DirectWrite and the Linux text backend before UI layout or Vulkan. All 14 logical advances, vector widths and vector baselines matched. The maximum difference between the vector ink bounds was under 0.008 px. Raster baselines relative to their own pixel canvases differed by 0.73–1.89 px. The 14 raster alpha masks differed even after trimming transparent borders. This establishes a difference before submission to Vulkan. It does not measure baseline alignment in the final widget. See `W:/Cache/Duxel/gallery-source-20261117/radio-text-backends-1966/verification.json`.

A second probe placed those labels through `UiContext` and read the submitted text-quad coordinates at content scale 1. With pixel snapping disabled, backend-relative offsets compensated fully and all 28 effective baselines matched. With snapping enabled, the maximum inter-host difference was 0.266 px at Y = 40 and 0.734 px at Y = 40.25. Each host stayed within 0.5 px of the intended baseline. These values describe the command geometry before Vulkan sampling. See `W:/Cache/Duxel/gallery-source-20261117/radio-placement-1967/verification.json`.

A Vulkan probe read back the label “More context.” inside a `ScrollView` on each host. It applied six offsets at each content scale of 1, 1.25 and 1.5 with snapping enabled and disabled. With snapping enabled, the cropped RGB ink was byte-identical across the six offsets within each host and scale. The Windows unsnapped cases produced two distinct ink images per scale. The WSLg unsnapped cases produced one for this label and these offsets. This test uses programmatic scroll offsets and content scale. See `W:/Cache/Duxel/gallery-source-20261117/text-scroll-pixels-1968/verification.json`.

Run `./run-fba.ps1 samples/fba/layout_radio_allocation_fba.cs -TargetFramework net11.0-windows7.0 -NoCache` from the repository root to try the three layouts. Change Width and Height and click the options to see how the parent allocation affects wrapping.

In the Gallery, open **Input and selection → RadioGroup** and scroll down to **Wrapping at different container widths**. **Change container width** changes the parent's maximum width between 180 and 360 logical pixels. The selected value is retained, and the C# example below the group shows the current width and selected value.

The Windows Gallery source fixture checks that the long option gets shorter and wider, that the selected value survives, and that both code views show their actual factories. The external Linux client takes the Gallery process ID, output JSON path, and X11 window ID after `x11-gallery-radio-allocation-pixels`. Managed and NativeAOT .NET 11.0.0 each passed seven checks on WSLg. Four cross-runtime screenshot pairs match outside the diagnostics header and fading scrollbar. The Windows parent-allocation fixture's 245 current managed GPU readbacks also match the archived Windows NativeAOT hashes. These checks cover the Gallery on WSLg and the dedicated Windows GPU fixture. Evidence is in `W:/Cache/Duxel/gallery-source-20261117/radio-gallery-allocation-2116`.

## Parent allocation across runtimes — 2026-11-18

The same source was published as managed and NativeAOT Windows x64 builds with SDK 11.0.100-rc.3.26522.148 and runtime 11.0.0-rc.3.26522.148. Both passed 264 checks with MSAA 1 and 4 and three frames in flight. All 245 pairs of saved RGB GPU readbacks were byte-identical. Evidence is in `W:/Cache/Duxel/gallery-source-20261117/radio-rc3-1901/comparison.json`. This comparison covers RadioGroup parent allocation.

## Selection accessibility — 2026-11-11

The existing generated, generic and direct RadioGroup paths publish a named `RadioGroup` with `RadioButton` children. Windows uses Group50026/Selection10001 and RadioButton50013/SelectionItem10010. Radio options expose no Toggle or Invoke, following [Microsoft's RadioButton contract](https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-supportradiobuttoncontroltype). SelectionContainer identifies the actual owner; the owner is single-select and selection-required. Caller-supplied null/absent values still display no selection without silently changing the binding. Once selected, a radio cannot be cleared by RemoveFromSelection.

Typed Select and Focus reuse the existing queue, live enabled/clip/popup checks and real caller callback. Focus can move to an unselected option without selecting it, survives idle frames and continues the original arrow-key navigation. Option descriptions supply HelpText. Explicit group names and Field/Tooltip naming use the common rules. IDs follow immutable option values across reorder; replacement/removal retires missing values. IDs are retained only while their content is owned and are cleared at disposal. Parent allocation, option geometry and ordinary pointer/key processing remain unchanged.

Focused semantic checks18, existing input/allocation checks91 and actual Observer137 pass. External UIA client9/host3 qualify real selected/property events, ownership, disabled state and exact0x80040201 after removal. ObserverGPU47 and73 unchanged prior body captures preserve the existing visual path. External UIA and precise source evidence are recorded in `artifacts/radio-accessibility-2026-11-11/verification.json`; this focused contract does not complete narration, complete Fluent states, memory/performance or exactRC3 NativeAOT validation.

## RadioGroup parent allocation — 2026-11-10

The actual parent width controls wrapped text. Static `IUiRadioGroupPresentation.Arrange` returns the allocated root without changing canonical option geometry. Fluent options keep natural,start-aligned hit regions;blank root space does not select. Root clipping applies to painting and input. Height-only changes reuse natural measurement when `ReuseLayout=true`;height-dependent custom presentations must leave it false. Unsupported/incorrect Arrange implementations fail explicitly.

Current-source .NET11 RC3 managed validation passes264 checks with MSAA1/4 and three frame slots,producing245 readback images. Tests and the new `layout_radio_allocation_fba.cs` sample build with zero warnings/errors. The sample has three layouts,shared selection and Width/Height controls. First presentation and15-second normal exit are verified;manual input/appearance approval is separate.

Native publish failed because Visual Studio C++ Build Tools/vswhere.exe are absent. Native execution and managed/native image comparison remain unverified. Historical L:/Cache evidence is inaccessible. Evidence:`artifacts/radio-resume-2026-11-10/verification.json`. Whole-goal counts remain engine15/27,widgets9/21,containers3/6,integration8/15,portability5/8;Fluent/overall denominator unresolved. Resize stays deferred. No performance improvement is claimed.


2026-11-06. The [official Radio group guidance](https://fluent2.microsoft.design/components/web/react/core/radiogroup/usage) currently labels the component Preview. Radio/RadioGroup styles and behavior reuse Microsoft Fluent UI commit `8830c10bfb2f4c1bdd540287b09713b9679cd1ad`; originals and hashes are retained in `artifacts/fluent-radio-2026-11-06/plan.json`. The scope is not expanded to all exports from platform repositories.

`Fluent.RadioGroup` is a generated widget with actual exclusive selection, grouped input, layout and painting. It is not a renamed legacy RadioButton. Legacy RadioButton remains available through its compatibility path. `Dux.RadioGroup`, generated widgets and direct generic calls share the implementation.

```csharp
private readonly UiState<string?> _choice = new("balanced");
private static readonly UiRadioOption[] Choices =
[
    new("fast", "Efficient", Description: "Prefer shorter processing time."),
    new("balanced", "Balanced"),
    new("disabled", "Unavailable", false)
];

protected override IUiView Build(UiBuildContext context)
{
    return context.Child("choice", () =>
        Fluent.RadioGroup("Processing mode", _choice.Binding, Choices));
}
```

Keys are strings. Duplicate/null keys and null labels are rejected before replacement. Explicit empty-string values remain valid; null means no selection. An external value absent from the current options displays no selected option without silently rewriting the binding. The caller supplies a recommended default explicitly.

## Behavior and lifetime

Tab visits the group once, showing focus on the enabled selection or first enabled option. Arrows wrap and skip disabled options. Space selects on release; Enter does not change selection. Selecting the current value never clears it or repeats a no-op callback. Batched arrow events preserve intermediate callback values and order.

Labels/indicators are clickable. Release over another option, Escape, removal and disabling cancel the relevant press. `SetItems` validates before copying caller data. Reordering preserves a pressed key at its new location; removing it cancels activation. Changing the displayed heading does not change generated-widget input identity.

Generated classes support inheritance and `OnChanged` overrides. Disposal releases binding references, options, layout and scratch event storage; the binding itself belongs to the caller. A direct-call `UiRadioGroupContent<TPresentation>` owns one logical control's input/scratch state and should be separate per control. Selection bindings may be shared across groups.

## Compile-time presentation

Vertical is the default. Other layouts are selected by implementation type:

```csharp
[UiWidgetStyle(typeof(UiRadioGroup<,>), typeof(UiFluentRadioGroupBehavior),
    typeof(UiFluentRadioGroupPresentation<UiRadioHorizontalStackedLayout>))]
public partial class ProductRadioGroup;
```

`UiRadioVerticalLayout`, `UiRadioHorizontalLayout` and `UiRadioHorizontalStackedLayout` implement distinct arrangement. Behavior and presentation remain independently replaceable. Labels use Regular14/20 and descriptions Regular12/16. The16px indicator has a1px border and10px selected dot; outlined brand tokens are shared with mixed Checkbox. No runtime implementation registry or reflection selects styles.

Width is available wrapping space. Ordinary sentences wrap without ellipsis. Extremely long unbreakable words may retain the common text system's minimum-content width. Complete browser parity for every font, character and narrow width is not claimed.

## Retention and validation

Unchanged width/font/scale/backend generation/context reuse measurements and line strings. Context references are weak. Additional wrapped/split strings are retained up to4,096 characters per group; larger results render for the frame and are discarded. Cached font paths are limited to1,024 characters each and the heading key to1,024. Custom presentations default to no layout reuse and opt in only under a deterministic input contract. A failed measurement clears partial rows and propagates the error rather than reusing a poisoned cache key.

`UiControlTextBlock` retains measured line strings and widths, then paints explicit line heights. Bounds place lines; the caller's clip controls actual glyph ink. Glyphs are not clipped to advance width. Existing single-line painting remains unchanged. Actual readback exposed missing final lines, and an MSAA4 centered comparison exposed advance clipping; both are retained and corrected using independent20px line positions and actual first/final-line pixels.

Run `--radio-only` input/cache checks before GPU work. Managed .NET10 and fixed .NET11 RC3 NativeAOT validation remain separate. The gallery has a `--radio` page for all three layouts. Current counts, hashes and raw pixel comparisons are in `artifacts/fluent-radio-2026-11-06/verification.json`. No new FPS/Private/Pi gain is claimed.

Official browser pixels, complete UIA narration/state coverage, logical RTL, forced colors, complete required/Field integration, toolbar-specific keyboard semantics and complete motion validation remain open. The focused Selection contract above does not complete RadioGroup or full Fluent support.

2026-11-06 common text cache follow-up: additional line arrays are capped at256 total slots. Larger input still paints for the frame, then releases its cached layout. Empty lines cannot retain an oversized array merely because their character count is small. Evidence: `artifacts/fluent-link-2026-11-06/verification.json`.

2026-11-12 — Known deterministic leaf presentations now participate in the existing parent's measurement cache while rendering/input continue. Custom traits remain opt-in; label/options/size/items and environment changes invalidate layout. See [common leaf measurement contract](widget-cpu-retention.md) and artifacts/leaf-layout-retention-2026-11-11/verification.json. Full NativeAOT/performance gates remain open.

The stable .NET 11 parent-allocation fixture uses the same 400×300 framebuffer on Windows and Linux. On Windows the managed and NativeAOT runs each passed 264 checks and matched all 245 GPU captures. The Linux managed and NativeAOT runs each passed 212 checks and matched all 244 captures. Across hosts, all four font-free captures matched. In all 48 constrained label cases, the pixel differences stayed inside the assigned area.

Open the Gallery's **RadioGroup** example under **Input and selection** to try the vertical choices. With the Gallery running on Linux, use `x11-gallery-radio-group <gallery-pid> <fresh-result.json> <decimal-window-id>` in the external `Duxel.Linux.App.Tests` client. It checks default selection, focus and selection with Up and Down, and selection through AT-SPI.

The Linux Vulkan test runner accepts `radio-parent-pixels` after its output, frame-slot and five font arguments. It runs the same parent allocation fixture used in Windows for vertical, horizontal and stacked groups, including the generated custom presentation. The current WSLg .NET 11.0.0 managed and NativeAOT builds each pass 20 checks at one and three frame slots with 1× and 4× MSAA. All 104 same-slot pairs and all 208 pairs with the previous builds match. The current Windows managed build passes 264 checks. In the shared 320×240 crop, all four font-free images match Linux, and the 48 labelled images differ only inside each parent allocation. [Linux capture hashes](L:/Cache/Duxel/radio-parent-2148/verification.json) · [Windows and Linux crop comparison](L:/Cache/Duxel/radio-parent-2148/cross-host.json).

The current Linux Gallery builds each pass seven external AT-SPI and X11 checks, including switching the parent maximum from 180 to 360 logical pixels and keeping the chosen radio option. All four cross-runtime body captures and four repeated body captures match. [Live Gallery comparison](L:/Cache/Duxel/radio-parent-2148/gallery-comparison.json).

The FluentProvider Gallery direction example now has Daily, Weekly and Monthly choices. The independent Linux `x11-gallery-provider-radio-direction <Gallery PID> <new result JSON> <decimal X11 window ID>` probe verifies Left and Right follow the visible neighbor in RTL and LTR, and a direction change keeps selection. Fixed .NET 11.0.0 managed and NativeAOT WSLg runs passed 10 checks each. [Capture evidence](W:/Cache/Duxel/gallery-source-20261117/radio-direction-2126/verification.json).
