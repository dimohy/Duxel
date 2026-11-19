# Internal window placement and movement

Last synced: 2026-11-05

The user directly reviewed the corrected Slider window and confirmed improved clipping/spacing and natural movement. This closes the reported layout/movement defect. Profiling limits below and whole-engine performance/memory goals remain separate.

Gallery titles overlapped app chrome and fixed height left excessive space. Reapplying declarative Position/Size every frame also reset position on drag release. First presentation did not validate layout/interactions.

UiWindowOptions.Placement defaults to Initial, applying size/position only at first creation and preserving user movement/resizing. Choose UiWindowPlacementCondition.Always for controlled placement. Existing SetNextWindowPos/Size still apply each time. PositionSpace=UiWindowPositionSpace.WorkArea adds the work-area origin. The gallery reserves24px below48px app chrome; default Screen coordinates remain available. Dragging prevents title Y from moving above the work area.

AutoHeight fits stored height to actual content and requests a subsequent frame, preserving minimum height, work-area bottom and necessary scroll. It measures during ordinary layout and applies on the next frame without duplicate callbacks. Automatic height bypasses height interpolation; nested windows restore parent policy.

FullNET10 1,004, shared managed/fixedRC3 Native108 and428 unchanged images pass. The position-reset test fails before the fix; pointer following, release position, title inset, fitting and whole-image GPU checks pass afterward. The common GPU window image was inspected directly, not treated as desktop verification. Evidence: artifacts/gallery-window-fix-2026-11-05.

Movement still uses6 draws and streamed geometry:1,376B at320x240 and1,584B after the full test's404x281 resize. Different-size counters are not performance comparisons. Subjective latency is under investigation using actual gallery profiles; FPS/Private gains and complete latency resolution are unproven.
