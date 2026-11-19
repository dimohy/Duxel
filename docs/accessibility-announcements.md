# Accessibility announcements

`UiImmediateContext.Announce(message, activityId, priority)` and `UiControlContext.Announce` provide an explicit announcement path without changing visual nodes or focus. Call once in the render frame that owns the notification.

```csharp
bool queued = ui.Announce("Connected 🙂", "connection", UiAnnouncementPriority.Polite);
```

The result means **queued in this frame**, not spoken by an assistive client. With no attached accessibility tree it returns false. The activity key is scoped by the existing caller/widget ID; use a stable, nonlocalized key.

Polite waits for normal delivery; Assertive requests important delivery. Two explicit equal messages/keys produce two announcements. The API does not silently deduplicate them. Widgets must own mount/state-transition timing. Calling on every Render requests a new announcement on every Render.

## Input and lifetime

Message and key must be nonempty, well-formed UTF-16 without NUL. Invalid priorities are rejected. Only the current accessibility render writer can enqueue. Measurement, another thread, observer reentry and disposed trees cannot enqueue. External work should update UI state and announce during the next render.

Announcements flush FIFO after publication, including frames with no node or revision change. Aborted captures discard their queue. Completed delivery clears it; observer failures propagate without later replay. The API does not schedule animation or continuing render frames.

## Native mapping

Windows uses the actual window provider with [UiaRaiseNotificationEvent](https://learn.microsoft.com/windows/win32/api/uiautomationcoreapi/nf-uiautomationcoreapi-uiaraisenotificationevent): NotificationKind.Other, Polite→All and Assertive→ImportantAll, with owned length-bearing BSTR message/activity arguments. The API requires Windows10version1709or later. The activity key is delivered as the Windows notification grouping identity.

Linux emits [AT-SPI Announcement](https://github.com/GNOME/at-spi2-core/blob/main/xml/Event.xml) from the window object: politeness1/2, unused integer0, string variant and an empty property dictionary. This protocol has no Windows activity-key field; the key is not inserted into unrelated fields. Assistive technology controls actual speech policy.

## Qualification scope

On2026-11-15,23common model checks,4independent Windows UIA client/1host checks and4pinned .NET11 RC3 NativeAOT Linux AT-SPI client/3host checks passed. They verify Korean/emoji, priority, explicit repeated messages and8unchanged following frames. Windows uses managed .NET11, not Windows NativeAOT. Human-audible speech is not claimed.

This is MessageBar infrastructure. Visual widgets, inline body, automatic reflow, motion and Observer exposure remain incomplete; the current qualified Linux executable is unchanged. Evidence: `artifacts/fluent-messagebar-2026-11-15/announcement-verification.json`.
