# Accessibility relationship changes

Last synced: 2026-11-12

[Standalone Label](label-widgets.md) now supplies a real Text provider and `LabeledBy` relation when both separately rendered widgets exist in the current capture. The end-of-capture connection supports either render order and only the primary target; missing/disposed labels cannot leave dangling references. The existing committed relationship event path also covers detach. Evidence: `artifacts/label-relationships-2026-11-12/verification.json`.

`UiAccessibilityTree.RelationshipChanged` reports retained controls whose `LabeledBy` or `ControlledElementId` changes. `RenderPanel` uses these relationships automatically for the selected tab and its body. The existing observer's ten pages consume that API; run `./run-gallery.ps1` after closing an earlier observer window.

```csharp
tree.RelationshipChanged += change =>
{
    Console.WriteLine($"{change.Property} : {change.Previous?.Element.Node.Id} -> {change.Current?.Element.Node.Id}");
};
```

`UiAccessibilityRelationshipChange` contains the committed source, property, previous target and current target. A null target means no relationship. `UiAccessibilityRelationshipTarget.Ancestry` is an immutable, target-first snapshot through its parents. The previous target and its ancestors remain readable after their live entries have been removed. Current target metadata comes from the same prepared publication as the source. Callbacks run after publication and outside reader locks; they may read or enqueue actions but cannot reenter publication or disposal.

Both relationship IDs must refer to controls captured in that frame. Dangling IDs fail before publication instead of becoming a silently missing target. An ancestry cycle encountered while preparing a change also fails before publication, using iterative traversal. Initial discovery, unchanged values, and removal of the source produce no property-change event. Abort clears staged notifications.

| Field | Windows property | Provider value | Client value |
| --- | --- | --- | --- |
| `LabeledBy` | 30018 | `VT_UNKNOWN`, null when cleared | Actual `IUIAutomationElement`, or null |
| `ControlledElementId` | 30104 | `VT_ARRAY \| VT_UNKNOWN`, empty when cleared | `IUIAutomationElementArray` |

The current model names one controlled element. Windows returns the defined empty/null property value when no relationship is present. Property events use the real live source and new target. Their new target retains its callable patterns; it is not replaced with a read-only snapshot. Previous-value providers preserve removed target identity and ancestry for UIA to process. Every temporary pointer and VARIANT is released after the native raise call. The product adds no waits, timers, or frame requests for these notifications.

The owned native fixture changes both fields from an old target to a new target, clears them, restores the new key, then removes the source. It receives six property events, invokes the new target twice through event payload references, and confirms that the old target stays unavailable. Native Invoke queues asynchronously: the fixture waits for actual source removal before asserting its terminal state. The first premature assertion and failed host deadline remain in the artifacts.

Evidence: `artifacts/relationship-events-2026-11-11/verification.json`. SDK11.0.100 is pinned in that folder with roll-forward disabled; runtime is11.0.0. This is managed native-interop qualification, separate from the unrun RC3 NativeAOT/runtime-image comparison. The fixture is a native HWND with direct accessibility publication and no GPU rendering; actual generated tabs/panels are covered by a separate external integration fixture. Existing GPU evidence remains in the TabPanel qualification.

Current checks: core relationships17 (TabList suite94), native relationship client12/host4, actual generated-tab client25/host3, observer39, accessibility49, scheduling35, legacy10 and multiline/Tooltip/editing4114. The actual tab fixture receives four ControllerFor events for switching, clearing and restoring a body, and no dead-source event after tab removal. The user also reported that the earlier Immediate behavior appears improved; this is manual feedback, separate from complete desktop-input qualification.

This covers the two fields above. [Native focus events](accessibility-focus.md) have separate qualification. Other relationship families, screen-reader narration, complete Fluent states and NativeAOT remain open. Changing relations allocate target snapshots; the unchanged small-tree check only qualifies zero managed allocations after warmup with an attached observer, not overall UI or GPU cost.

Primary references: [property identifiers](https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-automation-element-propids) and [raising property changes](https://learn.microsoft.com/en-us/windows/win32/api/uiautomationcoreapi/nf-uiautomationcoreapi-uiaraiseautomationpropertychangedevent).
