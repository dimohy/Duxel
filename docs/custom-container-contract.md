# Custom container contract

An external .NET 11 project referencing only the locally packed `Duxel.Windows.App` uses the analyzer carried by its `Duxel.App` dependency. Its generated Footer and overflow containers passed 15 checks for allocation, editor focus and undo, clipping and disposal. The packaged analyzer matches the build output when packing with a custom artifacts directory.

The unchanged `layout_custom_contract_fba.cs` also compiles and runs directly from an isolated local feed containing the two packages. To reproduce the managed build, run `dotnet build layout_custom_contract_fba.cs -c Release -p:RestoreSources=<local-feed> -p:PublishAot=false -p:SelfContained=false` with SDK 11.0.100. Running the output with `DUXEL_SAMPLE_EXERCISE=1` exercises the sample callbacks automatically. The validation observed the first Vulkan presentation, all four exercise steps, and exit code 0.

2026-11-11 the Footer tooltip explicitly declares Description. A separate Windows UIA client verifies the actual Footer help/name and separation from Inset. See the [target accessibility contract](tooltip-accessibility.md).

2026-11-11 sample extension: the Footer button now uses a generated custom Tooltip wrapper. Container contract definitions remain unchanged; the sample also demonstrates [tooltip target](tooltip-targets.md) timing/ownership/focus. This custom surface does not establish official Fluent Tooltip completion.

`layout_custom_contract_fba.cs` demonstrates an external generated container using `IUiSingleChildLayout<TOptions>`, `IUiContainerPresentation` and `UiWidgetStyle`. Its layout reserves an inset and footer, measures the existing editor with tight remaining constraints, and returns one root allocation. The source generator forwards constructors and options to the closed generic base; no friend assembly or runtime reflection is required.

Measurement validates metadata and finite parent geometry before measuring the child. It must not paint, move the cursor, read input bindings, or change ownership. `CanRetainMeasurement` is an explicit opt-in; option changes invalidate layout while preserving the child, focus and undo history. Invalid options fail before replacing the previous value. Unknown geometry fails explicitly.

Return a root `Size` inside both axes of the supplied constraints. A size outside those limits throws before the layout can be cached or painted. The contract still permits a custom child or surface rectangle to extend beyond the root, where the root clip bounds its effect.

The root allocation bounds both custom presentation paint and child input/rendering. `Surface` remains the presentation rectangle and child clip boundary; extending it beyond the root does not extend the allocation. Caller clips still intersect the root, and a throwing presentation restores the caller clip. Zero-area intersections may retain coordinates outside the root but cannot draw pixels or accept input.

## Validation — 2026-11-10

An independent public-API consumer reproduces the original paint/input overflow, then passes 15 checks after the common root clip is applied. It compiles the exact contract definitions extracted from the sample, including generated constructors, and exercises real editor input, undo, option changes, disposal and paint failure. The test's initial geometric assertion also rejected a harmless zero-height intersection; its corrected assertion considers positive-area clips. The failed output is retained separately.

Pinned .NET 11 RC3 managed validation passes: containers 237 checks (including eight new GPU comparisons over MSAA 1/4 and four acquired frames), routing 2,317, ScrollView 47, virtual list 43 and lifetime 101. The new GPU case compares oversized custom surface paint with independently clipped paint. These are correctness results; they establish no performance improvement, external Windows UIA or manual OS-input qualification.

The FBA package directive targets the published package convention. Local qualification substitutes in-tree project references and explicitly supplies the generator as an analyzer; it does not establish availability in older published packages. Original container gates remain 3/6.

## Linux runtime comparison — 2026-11-19

An external project compiled the same sample body and generated `ConsumerFooter` and `ConsumerTooltip`, with only the project directives and Windows host bootstrap adapted for Linux. The x64 managed and NativeAOT .NET 11.0.0 executables used SDK 11.0.100. Both exercised all four Footer, Inset and Tooltip states in the same order and exited normally. The initial 820×520 X11 client image matched byte for byte across eight captures and two launches per runtime on WSLg. Evidence is under `W:/Cache/Duxel/gallery-source-20261117/custom-container-linux-2098`.

A separate run held each of the four exercise states through presentation, then captured its owned X11 window twice. Both runtimes produced four distinct 820×520 images. All four runtime pairs matched exactly, and the final state matched the previous initial image. This verifies the exercise callbacks and their rendered output on WSLg. Physical input, current Windows NativeAOT and integrated container performance remain open. Evidence is under `W:/Cache/Duxel/gallery-source-20261117/custom-container-states-2099`.
