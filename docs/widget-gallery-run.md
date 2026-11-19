# Running the in-progress widget gallery

2026-11-10 [Keyed virtual list](virtual-list.md): fixed-height vertical rows use double offsets and viewport-local placement for billion-item sources. CPU43 and expanded routing2317 checks pass; rows outside overscan dispose through existing ownership. Variable-height/other axes, cross-key recycling policy, external UIA and current Native remain open; container stages stay3/6. Sample: `layout_virtual_list_fba.cs`.

2026-11-10 [Owned ScrollView](scroll-view.md): Dux.ScrollView owns finite viewport measurement, real child placement, offset, scrollbar lifetime and invalidation. CPU47 and separate GPU/routing1933 checks pass. World-coordinate precision loss fails explicitly; rebased large content/virtualization and current Native remain pending. Container stages stay3/6. Sample: `layout_scroll_view_fba.cs`.

2026-11-10 [Nested scroll routing](scroll-routing.md): boundary handoff on independent wheel axes, caller clip and popup/window-order hit testing, shared scrollbar capture/release, and measured DirectText window steps. Expanded managed regression passes 1,645 checks and 1,025 readbacks; container stages remain 3/6. Owned scroll widgets, virtualization and current Native remain pending. Sample: `layout_scroll_routing_fba.cs`.

## Allocated Field sample — 2026-11-10

```powershell
& W:/MyWorks/Duxel/artifacts/field-allocation-2026-11-10/sample-build/bin/layout_field_allocation_fba.cs/release/layout_field_allocation_fba.exe
```

Change Width : 180 / Height : 180 and Validation : On around two real editors. The horizontal Field uses the UiWidgetStyle generation path. This API/sample is local and unpublished; the helper requires PowerShell7. This PS5 host qualified an identical-body local-reference FBA with explicit generator Analyzer metadata in sample-validation.targets. First presentation and 15-second exit0 are separate from manual input, official visuals, external Windows UIA and current Native acceptance. [Contract and evidence](fluent2-field.md#parent-allocation--2026-11-10).

## Managed sample after child-routing fix — 2026-11-10

```powershell
& W:/MyWorks/Duxel/artifacts/allocation-routing-2026-11-10/sample-build/bin/layout_badge_allocation_fba.cs/release/layout_badge_allocation_fba.exe
```

The existing Badge sample was rebuilt against the current shared allocation code. First presentation 542.23ms and 15-second exit0 pass. Manual input, official visual acceptance and current Native remain unverified;Field own allocation remains a separate confirmed gap. [Scope and evidence](widget-containers.md#allocated-child-routing--2026-11-10).

## Allocated Badge sample — 2026-11-10

```powershell
& W:/MyWorks/Duxel/artifacts/badge-allocation-2026-11-10/sample-build/bin/layout_badge_allocation_fba.cs/release/layout_badge_allocation_fba.exe
```

Use Width : 100 / Height : 64, Dot and Increment/Zero/Overflow to inspect frames, clips and hidden-counter reservation. This API/sample is local and unpublished. The helper requires PowerShell7; this PS5 host built an identical-body local-project FBA. First presentation 549.78ms and 15-second exit0 pass. Manual input, official visual acceptance and current Native remain unverified. [Contract and evidence](fluent2-badge.md#measurement-and-parent-allocation--2026-11-10).

## Allocated Link sample — 2026-11-10

```powershell
& W:/MyWorks/Duxel/artifacts/link-allocation-2026-11-10/sample-build/bin/layout_link_allocation_fba.cs/release/layout_link_allocation_fba.exe
```

Use Width : 100 / Height : 120 and Short/Long to change real wrapping and clips. Link click/Enter updates application status. This API/sample is local and unpublished; the helper requires PowerShell7. The current PS5 host used an identical-body local-project FBA, with first presentation 560.27ms and 15-second exit0. Manual input, official visual acceptance and current Native remain unverified. [Contract and evidence](fluent2-link.md#measurement-and-parent-allocation--2026-11-10).

## Allocated Divider sample — 2026-11-10

```powershell
& W:/MyWorks/Duxel/artifacts/divider-allocation-2026-11-10/sample-build/bin/layout_divider_allocation_fba.cs/release/layout_divider_allocation_fba.exe
```

Use Width : 100 / Height : 120, Orientation and Short/Empty/Reset to inspect actual lines, captions and clips. This API/sample is local and unpublished. The helper requires PowerShell7; this PowerShell5 host used an identical-body local-project FBA. Managed first presentation and 15-second exit0 pass; manual interaction, official visual acceptance and Native remain unverified. [Contract and evidence](fluent2-divider.md#measurement-and-parent-allocation--2026-11-10).

## Allocated Text sample — 2026-11-10

```powershell
& W:/MyWorks/Duxel/artifacts/text-allocation-2026-11-10/sample-build/bin/layout_text_allocation_fba.cs/release/layout_text_allocation_fba.exe
```

Duxel / Allocated Text: Width : 100 / Height : 120 changes both roots; Short/Empty/Reset changes only the getter content. Text keeps its font and line height while the parent controls wrapping and clipping. Managed local-reference FBA build and first presentation/15-second exit0 are verified. Manual interaction/official visual approval and Native remain unverified. The known PowerShell5 helper incompatibility is preserved;the sample was built with explicit local project directives. Evidence:artifacts/text-allocation-2026-11-10/verification.json. [Text contracts](fluent2-text.md).

## Allocated Progress sample — 2026-11-10

```powershell
& W:/MyWorks/Duxel/artifacts/progress-allocation-2026-11-10/sample-build/bin/layout_progress_allocation_fba.cs/release/layout_progress_allocation_fba.exe
```

Duxel / Allocated Progress: use the Slider to change the live bar; Width : 80 / Height : 64 change both roots while the square bar stays at 50%. The callback is used only for rendering; pure measurement uses the tight parent. Managed local-reference FBA builds without diagnostics and first presentation/15-second exit0 are verified. Manual input/official appearance and Native remain unverified. PowerShell7 is unavailable here, so the previously incompatible helper was not retried; the FBA package directive was replaced by explicit local projects for this build. [Contracts/evidence](fluent2-progress.md).

2026-11-10 spacing correction: the sample now uses readable C# spacing/line breaks and Width : 160 / Height : 180 labels. The local-reference FBA build is also verified; its executable is artifacts/radio-resume-2026-11-10/sample-fba-build/bin/layout_radio_allocation_fba.cs/release/layout_radio_allocation_fba.exe. Current correction evidence:artifacts/radio-resume-2026-11-10/formatting-correction.json.

## Managed RadioGroup allocation — 2026-11-10

```powershell
& W:/MyWorks/Duxel/artifacts/radio-resume-2026-11-10/sample-build/bin/RadioAllocation.Sample/release/RadioAllocation.Sample.exe
```

Duxel / Allocated Radio Groups:three layouts share selection. Width/Height change wrapping/clipping. Tab/arrows/Space select. Managed first presentation and15-second exit verified;manual input/appearance and Native unverified. Windows PowerShell5 could not run the existing FBA helper;the exact sample body was built in an isolated .NET11 project with local references. Evidence:artifacts/radio-resume-2026-11-10/verification.json.

## Latest editable Input allocation sample — 2026-11-06

```powershell
& 'W:\MyWorks\Duxel\artifacts\input-allocation-2026-11-06\native-gallery\net11.0-windows7.0\layout_input_allocation_fba.exe'
```

**Duxel / Allocated Editable Input**:click the lower part of the field,type text,change **Width/Height**,then try **Ctrl+Z**. The owned **?** and **Clear** controls retain their fixed24/48px sizes and keyboard order. SHA256 `76CDEC9AF6D3F3C2476B127C4917C33DCDDCF2047D2560BEC1F7791DFA1E61E3`. First actual presentation and15-second exit verified;manual/official-appearance/resize approval remains distinct.

Rebuild local source with fixed.NET11:

```powershell
Set-Location -LiteralPath 'W:/MyWorks/Duxel/artifacts/optimization-evaluation-2026-11-04/toolchain'
& 'W:/MyWorks/Duxel/run-fba.ps1' 'W:/MyWorks/Duxel/samples/fba/layout_input_allocation_fba.cs' -NoCache -TargetFramework net11.0-windows7.0 -DotnetPath 'C:/Program Files/dotnet/dotnet.exe'
```

[Input contracts and verification](widget-containers.md#editable-input-parent-allocation--2026-11-06). Other leaf/full Fluent goals remain open;resize stays deferred.

## Latest constrained-control sample — 2026-11-06

```powershell
& 'W:\MyWorks\Duxel\artifacts\leaf-constraints-2026-11-06\native-gallery\net11.0-windows7.0\layout_constrained_controls_fba.exe'
```

**Duxel / Constrained Fluent Controls**:drag the80x64 Slider even near the bottom,select **Width:80** to alternate80/240,and use **Reset**. Tab,arrows,Home/End retain Slider behavior. Switch/Checkbox accept input across their240x64 allocations. SHA256 `29D16F03794CD2F448D68F69C350343BF069A46316F2026398CED0EC4D82C41D`. First presentation/15-second exit verified;manual/official-Fluent visual/resize approval remains distinct.

Local source:

```powershell
Set-Location -LiteralPath 'W:/MyWorks/Duxel/artifacts/optimization-evaluation-2026-11-04/toolchain'
& 'W:/MyWorks/Duxel/run-fba.ps1' 'W:/MyWorks/Duxel/samples/fba/layout_constrained_controls_fba.cs' -NoCache -TargetFramework net11.0-windows7.0 -DotnetPath 'C:/Program Files/dotnet/dotnet.exe'
```

[Contracts and verification](widget-containers.md#parent-allocation-for-slider-and-toggle--2026-11-06). Other leaf families and full Fluent coverage remain open;resize is deferred.

## Latest single-child layout sample — 2026-11-06

Run the qualified Native sample:

```powershell
& 'W:\MyWorks\Duxel\artifacts\single-child-2026-11-06\native-gallery\net11.0-windows7.0\layout_single_child_fba.exe'
```

Title: **Duxel / Single Child Layouts**. Click **Count**, then **Inset** to change the same owned tree's padding between8/16,and **Reset**. Tab/Space use actual Fluent button behavior. The page demonstrates SizedBox/Padding/Align and ConstrainedBox/Center without an internal Window. SHA256 `21FC77820D5A31AD93DE421399B08EBBF578ABF6A9C1E688FB3B9204B1CBE825`. First actual presentation and15-second normal exit are verified;manual/official-appearance/resize approval is not implied.

To rebuild from local source with fixed.NET11:

```powershell
Set-Location -LiteralPath 'W:/MyWorks/Duxel/artifacts/optimization-evaluation-2026-11-04/toolchain'
& 'W:/MyWorks/Duxel/run-fba.ps1' 'W:/MyWorks/Duxel/samples/fba/layout_single_child_fba.cs' -NoCache -TargetFramework net11.0-windows7.0 -DotnetPath 'C:/Program Files/dotnet/dotnet.exe'
```

[Contracts and storage limits](widget-containers.md#generated-single-child-layouts--2026-11-06). Whole-goal work continues;resize remains separately deferred.

The latest **Stack and Flex** snapshot includes shared Row/Column/Container/Stack retention. Run `layout_containers_fba.cs` with the source command below,or use the exact qualified Native path in `artifacts/container-retention-2026-11-06/verification.json`. Count/Reset and Move overlay exercise the compiled widget tree. First presentation/normal exit are checked separately from manual interaction and resizing;the latter remains deferred. The measured Container creation cost is recorded rather than presented as a memory improvement.

The latest retained-layout build uses the same **layout_flow_containers_fba.cs** sample. Wrap/Grid now retain eligible measurements while buttons still paint and handle input. The new Native snapshot/first-presentation scope is recorded in `artifacts/layout-retention-2026-11-06/verification.json`;the earlier flow snapshot is historical. Pure-measure benchmark results are not this gallery's FPS or resize acceptance.

For the new flow layouts,run **layout_flow_containers_fba.cs** using the same fixed toolchain command below with that filename. **Columns** switches2/3 columns while the six cell counters retain their state;the controls themselves form Wrap runs. Title:**Duxel / Layout Containers / Wrap and Grid**. Native minimum480x560 is a temporary sample constraint until overflow/scrolling integration is complete,not a performance result. Exact source/executable hashes and first-presentation scope:`artifacts/flow-containers-2026-11-06/verification.json`.

```powershell
Set-Location W:/MyWorks/Duxel/artifacts/optimization-evaluation-2026-11-04/toolchain
& W:/MyWorks/Duxel/run-fba.ps1 W:/MyWorks/Duxel/samples/fba/layout_flow_containers_fba.cs -NoCache -TargetFramework net11.0-windows7.0 -DotnetPath 'C:/Program Files/dotnet/dotnet.exe'
```

The same container sample now includes **Move overlay**. Click it to move a Positioned button while preserving its instance;Stack uses the common constrained layout and clips to its box. This preview does not yet include Wrap/Grid. The fixedRC3 Native snapshot and precise first-presentation scope are in `artifacts/stack-containers-2026-11-06/verification.json`;resizing remains deferred.

For weighted containers,run layout_containers_fba.cs:the **Duxel / Layout Containers / Stack and Flex** screen divides remaining width1:1:2 among Count,empty space and Reset. The entire allocated button is clickable. First `Set-Location W:/MyWorks/Duxel/artifacts/optimization-evaluation-2026-11-04/toolchain`,then `& W:/MyWorks/Duxel/run-fba.ps1 W:/MyWorks/Duxel/samples/fba/layout_containers_fba.cs -NoCache -TargetFramework net11.0-windows7.0 -DotnetPath 'C:/Program Files/dotnet/dotnet.exe'`. The sample has fixed weights;live changes are covered separately. This does not complete Fluent/container coverage or resolve resizing.

Last synced: 2026-11-06

Run one desired screen in PowerShell. Close and rerun to pick up new source changes. This gallery previews implemented paths; it does not establish complete Fluent component support.

The RadioGroup snapshot exposes three layouts, grouped keyboard input and descriptions. SHA256: `200C1E191D31EA9F7F277C4B3E3F9385BBF9F130333BB09DD1DE1512958FF6D0`. First-submit/auto-exit were checked separately from manual input acceptance.

```powershell
& 'W:\MyWorks\Duxel\artifacts\fluent-radio-2026-11-06\native-gallery\net11.0-windows7.0\widget_tree_theme_fba.exe' --radio
```

The qualified Checkbox Native snapshot has SHA256 `17353B3F448A390DC9B064303BADE2199D0FD4C633CBF55888E0B76EA595E882`. Startup/first-submit/auto-exit were checked; manual input and complete official visual fidelity remain separate.

```powershell
& 'W:\MyWorks\Duxel\artifacts\fluent-checkbox-2026-11-06\native-gallery-qualified\net11.0-windows7.0\widget_tree_theme_fba.exe' --checkbox
```

## Current local source (.NET10)

```powershell
Set-Location -LiteralPath 'W:\MyWorks\Duxel'
$run = @{
    Path = 'samples/fba/widget_tree_theme_fba.cs'
    Managed = $true
    NoCache = $true
    DotnetPath = 'C:\Users\dimohy\.cache\duxel-dotnet-10.0.301\dotnet.exe'
}
.\run-fba.ps1 @run
.\run-fba.ps1 @run -ExtraArgs @('--buttons')
.\run-fba.ps1 @run -ExtraArgs @('--checkbox')          # Eight Checkbox compositions and selection/mixed state
.\run-fba.ps1 @run -ExtraArgs @('--radio')             # Group keyboard input and three layouts
.\run-fba.ps1 @run -ExtraArgs @('--typography')
.\run-fba.ps1 @run -ExtraArgs @('--progress')
.\run-fba.ps1 @run -ExtraArgs @('--slider')
.\run-fba.ps1 @run -ExtraArgs @('--divider')
.\run-fba.ps1 @run -ExtraArgs @('--badge')
.\run-fba.ps1 @run -ExtraArgs @('--panel')
```

Choose one command. Do not add `--` inside ExtraArgs; the script adds the separator. Direct `dotnet run samples/fba/...cs` may consume NuGet packages, so use the script for local changes. If an in-progress edit fails compilation, use the validated publication below.

## Published snapshot (fixed RC3 NativeAOT)

```powershell
$demo = 'W:\MyWorks\Duxel\artifacts\fluent-icon-assets-2026-11-06\native-gallery-qualified\net11.0-windows7.0\widget_tree_theme_fba.exe'
& $demo
& $demo --buttons
& $demo --typography
& $demo --progress
& $demo --slider
& $demo --divider
& $demo --badge
& $demo --panel
```

This executable contains the source at publication time, not subsequent edits, and includes its runtime. Paths/hashes are in artifacts/fluent-icon-assets-2026-11-06/gallery-smoke.json. Keep .NET10 functional checks separate from RC3 NativeAOT measurements; gallery FPS alone does not prove improvement.

Check buttons/toggles/checks, keyed child removal and dynamic text on the main screen;17 font presets in typography; determinate/indeterminate/reduced-motion progress; and shared-value medium/small, stepped, reversed, vertical and disabled sliders. Slider supports Tab/Shift+Tab, arrows, Home/End, PageUp/PageDown and dragging beyond bounds. Some existing inputs are still being migrated.

When reporting an issue, include the command, mode and input action. The existing ERD window is independent and need not be closed.


Default pages render directly in the app. `--panel` demonstrates collapse/expand; All Features `--panels` explicitly opts into panel layout. See [Panels and direct content](panels-root-content.md).


`--badge` shows eight colors,four appearances,six sizes,custom vector icons before/after/alone and dynamic CounterBadge. Add one crosses99; Reset to zero demonstrates hidden/zero/dot behavior. This does not establish full UIA or official visual parity.

`--divider` shows horizontal/vertical orientation,3 alignments,4 appearances,inset and retained content updates. Latest publication startup/first-frame/clean auto-exit passed; manual visual/input acceptance remains separate. [Implementation and limits](fluent2-divider.md).

`--buttons` shows small/medium/large and disabled buttons. Click to update the counter, or use Tab/Space/Enter. The new publication passed startup/auto-exit only; manual input remains unverified. [Implementation and limits](fluent2-button.md).

The current-source `--buttons` page also includes primary/outline/subtle/transparent. Bind them statically to shared behavior using `UiButtonStyle` and the size-specific `UiFluentButtonPresentation<TAppearance>` family. [Exact API and remaining scope](fluent2-button.md).

The `--buttons` square/circular size rows also use generated shared widgets. Compose a shape statically inside an appearance, such as `UiFluentPrimaryButtonAppearance<UiFluentCircularButtonShape>`. User frame algorithms share this extension point. See [scope and limits](fluent2-button.md).

Bind `UiFluentIconButtonPresentation<TSize,TAppearance,TIcon,TPosition>` through a generated style. The `--buttons` Add icon uses the pinned official font, including a user-declared generated icon type; icon-only UIA remains incomplete. [Composition, validation and limits](fluent2-button.md).

See [official icons](fluent2-icons.md) for the bundled fonts and `[UiFluentIcon]` generation path. Add regular/filled/interactive is provided; full symbol API, SVG/browser parity and UIA are not complete.

Switch snapshot with six size/composition combinations: startup, first submit and auto-exit were checked; manual acceptance is separate.

```powershell
& 'W:\MyWorks\Duxel\artifacts\fluent-switch-2026-11-06\native-gallery\net11.0-windows7.0\widget_tree_theme_fba.exe' --switch
```

SHA256: `FA30899041EAD1D9E9DBC394EF01D7B88AB6EA71EA05BD8E6173F7350892A354`.

Link snapshot with eight presentations and internal-page navigation/back. Startup, first submit and auto-exit0 passed; manual/foreground acceptance is separate.

```powershell
& 'W:\MyWorks\Duxel\artifacts\fluent-link-2026-11-06\native-gallery\net11.0-windows7.0\widget_tree_theme_fba.exe' --link
```

SHA256: `E3B0182BB9AD79B3E9DEDB89F5F6FC4BB4E790039D268BFDF745BD220F20D502`.

Input: run current sources with `./run-fba.ps1 samples/fba/widget_tree_theme_fba.cs -NoCache -Managed -DotnetPath C:/Users/dimohy/.cache/duxel-dotnet-10.0.301/dotnet.exe -ExtraArgs @('--input')`. Provides12 actual inputs plus readonly/disabled. Qualified Native snapshot:

```powershell
& 'W:/MyWorks/Duxel/artifacts/fluent-input-2026-11-06/native-gallery/net11.0-windows7.0/widget_tree_theme_fba.exe' --input
```

SHA256 CD3044409186C985E333DC9351C71F1C4B4345D5C7C18001C6BECBEF2ABD8F3A. Verified first submission/3-second auto-exit0 only; no manual interaction or visible-window acceptance.

The Input-adornment snapshot below adds text/search-icon prefixes and suffixes. Only first submission/auto-exit are qualified; no manual acceptance.

```powershell
& 'W:/MyWorks/Duxel/artifacts/fluent-input-slots-2026-11-06/native-gallery-final/net11.0-windows7.0/widget_tree_theme_fba.exe' --input
```

SHA256 898085D0451227CAADB5167540774875CD4EE34BD139E219D2B1C115D5728E55

Input snapshot with a real owned Clear button:

```powershell
& 'W:/MyWorks/Duxel/artifacts/fluent-input-children-2026-11-06/native-gallery-final/net11.0-windows7.0/widget_tree_theme_fba.exe' --input
```

SHA256 B66A4252597E21E0C4269077504B67BA01F0B83C7E7265AD46F2428242FF0451. Only first submission/3-second auto-exit0 is verified; no manual/visible-window acceptance.
## Field gallery

```powershell
.\run-fba.ps1 samples/fba/widget_tree_theme_fba.cs -NoCache -Managed -DotnetPath 'C:/Users/dimohy/.cache/duxel-dotnet-10.0.301/dotnet.exe' -ExtraArgs @('--field')
```

Inspect small/medium/large labels,vertical/horizontal layout,required/error/success/hint and disabled states. Clicking a label focuses the real input; entering at least three characters selects Success. Input heights are naturally measured; the last Field uses available window width. UIA and complete official parity remain open. See [Field details](fluent2-field.md) and the [measurement contract](widget-measurement.md).

The new Field Native snapshot can also run directly.

```powershell
& 'W:/MyWorks/Duxel/artifacts/fluent-field-2026-11-06/native-gallery/net11.0-windows7.0/widget_tree_theme_fba.exe' --field
```

SHA256 BB118CDCAADEE7C6144AE5A68B858565612E1C0DBC85845A5AFBC6F9DD4F90A1. First submission and3-second exit0 were checked; this is not visible/manual acceptance.

## Latest naturally measured Field snapshot

```powershell
& 'W:/MyWorks/Duxel/artifacts/widget-measurement-2026-11-06/native-gallery-final/net11.0-windows7.0/widget_tree_theme_fba.exe' --field
```

SHA256 728DD82D3965374B144C30C369C31B7705B3D2F67DDDE8B71E3A633129E05CF2. Actual widgets supply Input24/32/40 heights; the last Field uses available window width. Hidden first submission/3-second exit0 was checked; this is not visible/manual acceptance. See the [measurement contract](widget-measurement.md).

# UIA foundation checkpoint (2026-11-06)

The latest qualified Input gallery is `artifacts/uia-foundation-2026-11-06/native-gallery-names-final/net11.0-windows7.0/widget_tree_theme_fba.exe --input` (SHA256 `CC4DBDF979E1B72B94FE8F58CB8DD1107BA5E2F6D0E0DFC7CB15287CD7AFE6D4`). The external NativeAOT UIA client passed4 interaction checks and the gallery exited normally after15 seconds. This is not manual visual acceptance. See [accessibility scope](widget-accessibility.md) and the artifact verification record. Existing resize/FPS user acceptance remains preserved.

# UIA lifetime checkpoint (2026-11-06)

Latest gallery: `artifacts/uia-lifetime-2026-11-06/native-gallery/net11.0-windows7.0/widget_tree_theme_fba.exe --field`, SHA256 `032DF8EDB028D142630D45ACB434E773FA3D5F555BE239357A7BADA32AC97864`. External NativeAOT Field14 passes and the target exits normally after12 seconds. `--input` still opens the input/adornment gallery. Rebuild from local sources with `./run-fba.ps1 samples/fba/widget_tree_theme_fba.cs -NoCache -ExtraArgs @('--field')`. UIA verification is not manual visual acceptance.

# Spinner checkpoint (2026-11-06)

Latest gallery: `artifacts/fluent-spinner-2026-11-06/native-gallery-final/net11.0-windows7.0/widget_tree_theme_fba.exe --spinner`, SHA256 `8A7384E3CCFB0BBA4072277407956B271E55D70826A826342E01C30135FB3413`. It includes16 size/label/motion/appearance examples. External NativeAOT UIA33 passes and the target exits normally after15 seconds. Rebuild with `./run-fba.ps1 samples/fba/widget_tree_theme_fba.cs -NoCache -ExtraArgs @('--spinner')`. First-frame/UIA checks are not manual visual approval or official browser parity.


Latest Build-storage sample (2026-11-06): `& 'W:\MyWorks\Duxel\artifacts\build-store-2026-11-06\native-gallery\net11.0-windows7.0\layout_containers_fba.exe'`. SHA2566E8C8A939846673FE35670FE6BDFCCA0FAA2D8858252EF418E47AD31C0FB7311. First actual presentation and15-second normal exit verified;manual interactions and resize are not newly approved. Count/Reset and Move overlay use the current common widget path. [Memory results and limitations](widget-cpu-retention.md#shared-widget-build-storage--2026-11-06).
