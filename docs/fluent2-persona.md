# Persona — person information

```csharp
var person = Fluent.Persona(new UiPersonaContent("정세일",
    SecondaryText: "Developer", TertiaryText: "Duxel", QuaternaryText: "Seoul"));
person.Avatar.Options = new(Color: UiAvatarColor.Brand, Presence: new());
person.Avatar.PresenceText = "Available";
person.Options = new(UiPersonaSize.Huge, TextPosition: UiPersonaTextPosition.Below);
```

Persona combines up to four single-line text slots with an owned Avatar or a presence-only badge. `PrimaryText` defaults to `Name`; an empty slot hides that row. Updating `Content.Name` also updates the avatar name. Use `Content.PrimaryText` for a displayed override. Control characters/newlines are rejected. Slots clip within finite parent allocations; automatic ellipsis/wrapping and per-slot tooltips remain open.

| Size | Avatar body | Presence-only badge | Avatar spacing |
| --- | ---: | --- | ---: |
| ExtraSmall | 20 | Tiny | 6 |
| Small | 28 | ExtraSmall | 8 (presence-only6) |
| Medium | 32 | Small | 8 |
| Large | 36 | Medium | 10 |
| ExtraLarge | 40 | Large | 10 |
| Huge | 56 | Large | 12 |

Text can follow, precede or sit below media. Start/center alignment and logical RTL positioning are supported. `PresenceOnly` uses `Avatar.Options.Presence`; missing presence adds no empty media gap. `ShowMedia: false` displays text alone. Large text tiers, optional-row typography, second-row spacing and presence-to-primary alignment follow the pinned Fluent source.

`Avatar` is an owned keyed child. Source/icon/color/presence/activity/inactive changes use the existing avatar pipeline and invalidate the Persona ancestor chain. Persona size/direction govern its displayed avatar size/direction while preserving the configured source options. Persona owns one visual binding with independent inactive/shadow resources. Dispose Persona through its owning tree; its Avatar is disposed with it. No new renderer, native import, parser or timer is introduced.

One passive native Group exposes all displayed text and the localized shown presence. It adds no fake Invoke/Toggle/editable value or keyboard stop. Changing appearance/content preserves provider identity; removing Persona retires it. Set `Avatar.PresenceText` for localized state. Custom styles use `UiPersona<TPresentation, TAvatarPresentation>` and the explicit `IUiPersonaPresentation` measure/paint contract.

Run `./run-gallery.ps1` → **인물 정보** (use the header overflow if hidden). Change name, six sizes, text position/alignment/direction, row count, presence-only/text-only, SVG image/initials and inactive mode. State persists across tabs. SVG is decoded through the existing image path; initials use fonts.

Source: [pinned hook](https://raw.githubusercontent.com/microsoft/fluentui/8830c10bfb2f4c1bdd540287b09713b9679cd1ad/packages/react-components/react-persona/library/src/components/Persona/usePersona.ts), [pinned styles](https://raw.githubusercontent.com/microsoft/fluentui/8830c10bfb2f4c1bdd540287b09713b9679cd1ad/packages/react-components/react-persona/library/src/components/Persona/usePersonaStyles.styles.ts). Snapshots/hashes and qualification: `artifacts/persona-2026-11-13/`; large GPU evidence is explicitly stored under `L:/Cache/Duxel/persona-2026-11-13-evidence/` after volume-capacity probes. Catalog connectivity is not full conformance. Physical screen reader/forced colors, exact CSS/ellipsis behavior and fixed RC3 NativeAOT/runtime-image/performance/device gates remain open.

Explicit `Avatar.Options.Presence.Size` overrides the presence-only default and expands the primary row when needed to keep the badge inside its bounds.

Visible avatar activity is included using `Avatar.ActivityText`; inactive, presence-only and hidden-media modes omit activity that is not shown. Cached avatar presence text is reused.
