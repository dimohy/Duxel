# Fluent AvatarGroup

[한국어](fluent2-avatar-group.ko.md)

Last synced: 2026-11-13

`Fluent.AvatarGroup` owns member image leases, input and popup state. It supports Spread, Stack and Pie layouts. Defaults are32px Spread and five inline slots. Overflow reserves the final slot for an exact count button and moves the earlier members into the popup. A one-slot limit displays only the count button. Pie displays the first three members in one circle; activating it lists every member.

```csharp
using var group = Fluent.AvatarGroup("Collaborators",
    [new("jeong", "정세일"), new("ada", "Ada Lovelace"), new("grace", "Grace Hopper")],
    new(UiAvatarSize.Size48, UiAvatarGroupLayout.Stack),
    selected: value => OpenPerson(value));
group.Render(ui);
```

`UiAvatarGroupItem.Value` is a unique navigation value; Name remains the complete accessible name. Invalid empty names/values and duplicate values fail before resource creation. Distinct named groups using `UiWidgetIdentity.Parent` isolate internal trigger IDs. Breadcrumb also isolates repeated Home values by its path root.

Items are copied and cannot currently be replaced through public API. Each item can supply Initials, immutable Source, borrowed Icon, name color, activity ring and presence. Existing Avatar Korean initials, image/SVG caching and resource lifetime remain shared. Keep borrowed source/icon owners alive until the group is disposed.

Options select all fourteen avatar sizes, layout, maximum inline slots, RTL and localized overflow wording. Spread permits rings and presence, Stack permits rings, and Pie suppresses both. The complete visual union of rings and oversized badges is reserved. Smaller parent maxima clip through the common layout machinery. Inline slots do not automatically adapt to available width; callers choose the maximum.

Stack uses size-dependent negative spacing and neutral-background2 dividers. Pie composes existing analytic rounded and rectangular clips without adding shaders or polygon substitutes. Two members occupy half circles; three use one half and two quarters. RTL mirrors geometry while retaining semantic order. Half-sized initials use the original typeface rasterized at half size.

Accessibility exposes a passive named group and individually named Images. Only the overflow button receives a keyboard stop, Invoke and ExpandCollapse. Circular pointer bounds exclude clipped corners. The actual shared Menu handles member selection, scrolling, Escape and focus restoration. Popup avatars preserve the group size; the surface is limited to220px height. Current overflow entries are actionable MenuItems; this is not a claim of complete parity with the web passive-list structure. A group AccessibleName override does not replace the overflow button name.

Measurement, partition results, labels and icon descriptors are retained; requested frames still process input and painting. No periodic frames or XML reparsing are added. A shared owner releases popup context and scroll references; Breadcrumb now uses that owner too. No time or memory improvement percentage was measured.

Run `./run-gallery.ps1` → **아바타 그룹**; use the header overflow menu if the tab is hidden. Change layout, size, inline count and direction, then activate the count button or pie. The sample combines images, Korean initials and SVG icons, retaining settings and the selected name across tabs.

Evidence: `artifacts/avatar-group-implementation-2026-11-12/verification.json`. Complete official states/colors, forced colors, Tooltips, dynamic item replacement, fixed RC3 NativeAOT, original performance and device gates remain open. Factory connectivity does not establish complete Fluent conformance.

References: [official usage](https://fluent2.microsoft.design/components/web/react/core/avatargroup/usage), [pinned implementation](https://github.com/microsoft/fluentui/tree/8830c10bfb2f4c1bdd540287b09713b9679cd1ad/packages/react-components/react-avatar/library/src/components/AvatarGroup). Source snapshots are recorded in `artifacts/avatar-group-2026-11-12/sources.json`.
