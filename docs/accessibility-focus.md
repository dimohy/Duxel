# Accessibility focus publication

Last synced: 2026-11-11

`UiAccessibilityTree.FocusChanged` reports a committed logical focus acquisition, transfer or loss. `UiAccessibilityFocusChange.Previous` and `.Current` are nullable immutable element values. Callbacks may read the committed tree or queue actions; publication and disposal cannot reenter the callback. Initial empty focus, unchanged focus, geometry-only changes and aborted frames emit no focus change. A focused target must be enabled, focusable and onscreen. Direct tree capture rejects two eligible targets before publication.

```csharp
tree.FocusChanged += change =>
{
    Console.WriteLine($"Focus : {change.Previous?.Node.Name} -> {change.Current?.Node.Name}");
};
```

Immediate controls can move focus after an earlier control was captured. Publication therefore resolves the final navigation owner once at the end of the frame, including the composite TabList owner-to-child mapping. Earlier controls lose their stale `Focused` flag. The nonfocusable TabList parent never claims its child's focus. Text-input accessibility focus requests retain their existing IME ownership handling.

Windows distinguishes this logical state from actual keyboard focus. `HasKeyboardFocus` (30008), fragment `GetFocus`, and `AutomationFocusChanged` (20005) use the foreground thread's real focused HWND. A background logical change cannot claim keyboard focus or emit a global focus acquisition. Deferred, coalesced WM_SETFOCUS/WM_KILLFOCUS processing restores the current virtual child when its window regains focus, even if logical focus did not change. A child-to-empty-root transition reports the surviving root; unavailable removed children receive no fabricated property event. Identity comparisons suppress unchanged notifications.

The product does not force foreground activation, inject OS input, add a timer, or request rendering to publish these events. UI Automation manages the hosting window's activation before provider `SetFocus`; the provider queues the internal focus request through the existing writer. Root `GetFocus` returns the root itself when its HWND has keyboard focus but no eligible virtual child.

Evidence: `artifacts/focus-events-2026-11-11/verification.json`. Managed SDK11.0.100 is pinned in that folder with roll-forward disabled; runtime11.0.0. Core TabList/focus suite111, observer39, accessibility49, scheduling35, legacy10 and multiline/Tooltip/editing4114 pass. The separate client uses actual generated Fluent tabs and panel content in two owned native windows: focus acquisition, arrow navigation without selection, unchanged focus, departure, background logical movement, restoration without logical change, body focus and removal. Client11/host3 pass; four virtual acquisitions and the root transition are observed. Fixture coordination uses atomic command files so invoking another focusable control does not alter the focus under test. Engine key events are supplied directly, without desktop keyboard injection. This fixture does not render GPU frames or qualify screen-reader speech.

Earlier failed fixture runs are preserved: an incorrect client COM slot caused an access violation; the native header pins corrected handler slots39/40. Subsequent control-driven coordination exposed earlier-captured focus remaining set when a later control gained focus. Final-frame resolution fixes that product defect, with an independent two-way core regression. No full Fluent, IME desktop, NativeAOT or RC3 runtime-image completion is claimed. Earlier GPU images remain separate evidence.

Run the observable sample from the repository root with `./run-gallery.ps1`. Close an older observer first so the current build is used. Tab enters the page header, arrows move focus and Enter/Space selects a page. Tab continues to the selected body and its controls. Switch windows and return to inspect restoration. Ordinary desktop operation and narration still require final manual qualification.

Primary references: [provider SetFocus](https://learn.microsoft.com/en-us/windows/win32/api/uiautomationcore/nf-uiautomationcore-irawelementproviderfragment-setfocus), [GetGUIThreadInfo](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getguithreadinfo), and [UIAutomationClient.h](https://github.com/microsoft/win32metadata/blob/main/generation/WinSDK/RecompiledIdlHeaders/um/UIAutomationClient.h).
