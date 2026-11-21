# Windows IME input routing

Windows repeat counts are retained before raw or ordered IME consumption. UTF-16 first-unit ownership also retains multiplicity; duplicate result comparison handles both a unit and its expanded repeated string. The low-level immediate-handler suite now passes 326 checks, including three repeat-specific raw/IME cases beyond the earlier 323. [Repeat metadata, boundary fixture and limits](input-event-batches.md#native-repeat-counts).

The Windows App facade and Observer now capture native messages in order. Flagged IMM strings are read once on the HWND owner thread and applied through immutable dispatch payloads on the UI thread before each logical input step. A payload requires its originating IME handler. Native capture sessions and logical UI sessions are separate: old logical work cannot complete or reposition an ended/replaced native composition. The first UTF-16 unit retains the target selected at its logical position, including a click in the same host batch. Native checks pass 363 cases, including removal/hidden-input lifetime and captured event time; native App/worker/Vulkan integration passes 37. [Ordered capture contract and commands](input-event-batches.md#windows-native-ordered-capture).

## Removed and hidden inputs

`IUiImeHandler.ReleaseInput(inputId)` is a required lifecycle contract for custom handlers. Owned Input, Textarea and Markdown disposal invokes it through their common text owner; transform wrappers forward it. Implementations must discard that input's pending commits and retained ownership without clearing another input's queue. A hidden live widget is not released and can consume its late commit when shown again. Custom handler implementations must implement this method when updating to this API. Hosts managing raw immediate input ids must call it explicitly when permanently removing an input.

Windows clears the removed target from its active/composition owners, pending character owners and caret placements. A pending high surrogate stays ownerless until its continuation is decoded, so the low unit cannot attach to new focus. No retired-id set is retained: a newly used identical id accepts new input. Removal of a live native composition requests `CPS_CANCEL` on its HWND owner thread; a queued prior completion resolves the now-ownerless session as cancellation. Ended/replaced sessions are still ignored, and native failures remain surfaced. [Native cancel/complete contract](https://learn.microsoft.com/en-us/windows/win32/api/imm/nf-imm-immnotifyime).

The controlled ordered test grew from 110 to 218 checks. Before the fix, 24 of 218 checks failed because disposal retained old owners or pending commits. All 218 now pass, including three actual editors, both render orders, queued text, split UTF-16, hidden restoration and cancellation. The earlier lifetime cohort passed 323 checks (313 previous plus 10 direct lifetime checks), covering both character lanes, id reuse, unrelated pending text and cancellation with/without replacement focus. These are controlled message/IMM tests, not full keyboard/TSF service acceptance. [Evidence](../artifacts/goal-audit-2026-11-16/ime-input-lifetime-verification.json).

`SetCompositionOwner` selects the currently active input. A native composition captures its input id at `WM_IME_STARTCOMPOSITION` and retains that target until `WM_IME_ENDCOMPOSITION`. Changing focus or clearing the active input does not retarget an existing composition. Preedit is exposed only while that original input is active. A composition that began without an input cannot later write into a newly focused input.

Both result strings and native character results use this captured target. Repeated result-only notifications without new preedit are consumed once; a subsequent preedit segment may commit identical text independently. Ordinary characters outside a composition follow the current active input. Ending without a result does not invent committed text. A late commit accepted by an inactive single-line widget persists its final cursor, so returning focus restores the position after the accepted text.

`WM_IME_COMPOSITION` retrieves only the strings indicated by its flags. Combined `GCS_RESULTSTR` and `GCS_COMPSTR` commit the previous segment and retain the next preedit. A result-only update clears prior preedit; attribute/clause-only updates preserve it. No GCS flags means cancellation. State changes are applied under one lock before requesting a frame, following [Microsoft's combined-flag and cancellation contract](https://learn.microsoft.com/en-us/windows/win32/intl/wm-ime-composition).

The earlier focus cohort passed all 313 cases: 249 existing cases, 28 focus/placement/failure cases, and 36 actual Input/Textarea/Markdown consumer checks. Before focus routing was fixed, 10 of 270 cases failed; integrated consumers subsequently exposed 8 additional cursor-restoration failures. The common multiline kernel now persists final cursors for inactive Textarea/Markdown commits. This does not establish actual keyboard-service or pixel-comparison acceptance.

## Logical focus and candidate placement

Leaving a composing input queues a request on the HWND owner thread: `NI_COMPOSITIONSTR/CPS_COMPLETE` for an owned composition, or `CPS_CANCEL` for an ownerless one. Requests coalesce per session. Returning to the original input before dispatch or starting another composition invalidates old work. The handler waits for actual result/end messages rather than inventing committed text or assuming completion. [ImmNotifyIME contract](https://learn.microsoft.com/en-us/windows/win32/api/imm/nf-imm-immnotifyime).

Placements carry the input, focus version, and composition session. A new input's position remains deferred until the previous composition ends; only a still-current latest position is applied afterward. All application occurs on the owner thread. Queued geometry coalesces, and requests reported as unapplied are not entered in the successful-placement cache.

IMM can deliver synchronous result/end callbacks, so notification runs outside the state lock. Rejection or a missing context is confirmed after an already-queued END has had a chance to retire the request. Ended/replaced sessions cannot fault current input. A failure that remains relevant is surfaced at a managed boundary; focus cleanup remains available and native retries do not loop.

Tests use the actual HWND queue, owner thread, and state transitions, while validation hooks control IMM completion responses and position application. Actual keyboard-service behavior and candidate-window pixels require separate acceptance. [Focus evidence](../artifacts/goal-audit-2026-11-16/ime-focus-verification.json).

## UTF-16 character messages

`WM_CHAR` and `WM_IME_CHAR` independently buffer at most one high surrogate. Only a completed pair becomes a Unicode scalar. The first code unit captures its input target: logical focus changes or intervening render frames cannot retarget its second half. A pair that began without an input cannot attach to later focus.

Raw input events and IME share the small decoder. Frame consumption retains pending code units; native composition start/end/cancellation, window focus loss, and disposal retire both lanes. An internal connection resets raw input even when the IME hook consumes the lifecycle message. The public constructor remains compatible.

Orphan low surrogates are rejected. A new ordinary character or high surrogate supersedes an incomplete pair without inserting replacement characters. This follows Microsoft's [UTF-16 code-unit contract](https://learn.microsoft.com/en-us/windows/win32/inputdev/wm-char) and [Unicode-window WM_IME_CHAR semantics](https://learn.microsoft.com/en-us/windows/win32/intl/wm-ime-char). [Current boundary evidence](../artifacts/goal-audit-2026-11-16/ime-scalar-verification.json).

Run the local-source graphics validation executable:

For the user sample, run `./run-gallery.ps1` from the repository root. In **Input and IME · combined**, switch between the name and memo during Korean composition and inspect commit/caret restoration. This manual path exercises the actual IME service; the automated message checks have a separate scope.

```powershell
./Duxel.Graphics.Tests.exe <fresh-output> ime-owner-only
```

The check creates an owned, unshown HWND and sends native start/update/result/end messages through the installed window hook. Only IMM string extraction is injected by a validation-only partial hook, omitted from ordinary builds. It covers owner-thread/worker-thread focus updates, character/result-string notifications, duplicate results, cancellation, ownerless sessions, and two real input widgets in both render orders. It does not change user focus, clipboard or keyboard settings.

This verifies message routing and widget consumption, not a complete keyboard/TSF service session. Checks include combined flags, cancellation, attribute-only changes, split UTF-16 messages, and consecutive commits in actual input widgets. Same-batch character/pointer order and owned-widget removal are now checked separately through the enabled Windows ordered path; the legacy aggregate negative comparison remains recorded. Actual service focus completion/candidate placement and CS_INSERTCHAR/CS_NOMOVECARET display remain separate work. [Original routing evidence](../artifacts/fluent-messagebar-2026-11-15/ime-owner-verification.json), [combined-message evidence](../artifacts/goal-audit-2026-11-16/ime-flags-verification.json).

Sources: [composition result characters](https://learn.microsoft.com/windows/win32/intl/wm-ime-char), [composition changes](https://learn.microsoft.com/windows/win32/intl/wm-ime-composition), [composition end](https://learn.microsoft.com/windows/win32/intl/wm-ime-endcomposition).
