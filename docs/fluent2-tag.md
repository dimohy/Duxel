# Fluent Tag and selected tags

`Fluent.Tag("정세일")` is a passive value. A dismissal callback enables click, Enter, Space, Delete and Backspace on the entire tag. The callback owns the actual data change.

```csharp
var tag = Fluent.Tag("SVG", () => selected.Value = []);
var tags = new UiFluentTagGroup("Selected features", selected.Binding, options);
```

Here `selected` is `UiState<string[]>` and `options` is `UiSelectOption[]`. Give Dropdown or a multiple Combobox the same binding/options to share one selected collection. The group reads the getter/setter's actual result and notices in-place external array changes. Option values must be unique; selected values must be a distinct subset of those options. Construction copies the option array. Disabled options remain visible but cannot be dismissed.

`UiTagOptions` offers Medium/Small/ExtraSmall, Filled/Outline/Brand, Rounded/Circular, RTL and a dismiss action label. Base heights are 32/24/20 and group spacing is 8/6/4. `UiTagContent` supports primary text, secondary text and a borrowed `UiMenuIcon`. Measurement uses complete text, including Korean, without inserting ellipses. Paint remains inside the parent's actual allocated boundary.

TagGroup reuses Wrap layout with only selected children participating, leaving no placeholder gaps. Surviving tags preserve identity. Arrow keys and Home/End navigate, with horizontal direction reversed for RTL. The group has one roving Tab stop. Dismissal focuses the next enabled tag or the previous one; an empty group uses the existing Tab navigation rules to move outside, or releases focus if no target exists. Use `dismissible: false` for a passive group. Dismissed tags retire in the requested follow-up frame.

The dismiss × uses the **Fluent icon font**. Custom icons can borrow the existing SVG `MenuIcon` path. SVG XML is processed when loading the asset, rather than on each tag frame. Asset owners must outlive tags; disposing a group does not dispose borrowed SVG assets.

Run `./run-gallery.ps1` → **Selection lists · combined**. Select image/SVG features in **Features to try**, then click a lower basic tag to clear the same list selection. Upper interactive tags separate primary inspection and × dismissal. Inspect **Change tag size**, direction changes and page persistence.

This scope covers basic Tag and selection dismissal groups. selected emphasis, avatar media, overflow patterns, forced colors, extended extra-small touch targets, physical screen-reader acceptance remain open. The group publishes a native Toolbar containing actual Buttons/Labels. This is not fixed RC3 NativeAOT/performance/device acceptance.

Evidence: `artifacts/tags-2026-11-13/`. [Official Tag guidance](https://fluent2.microsoft.design/components/web/react/core/tag/usage), [pinned Tag styles](https://raw.githubusercontent.com/microsoft/fluentui/8830c10bfb2f4c1bdd540287b09713b9679cd1ad/packages/react-components/react-tags/library/src/components/Tag/useTagStyles.styles.ts), [pinned group behavior](https://raw.githubusercontent.com/microsoft/fluentui/8830c10bfb2f4c1bdd540287b09713b9679cd1ad/packages/react-components/react-tags/library/src/components/TagGroup/useTagGroup.ts).


## Focus reentry and accessible names

Leaving with Tab and returning with Shift+Tab restores the last navigated enabled tag. Removing the final selected tag transfers focus outside the group, preserving a valid target even when it was rendered earlier in the same frame. The group root is a Windows Toolbar without a fake root Invoke action; children own actual dismissal actions.

Literal `##` in tag content remains intact in its accessible name. Explicit `AccessibleName` overrides retain precedence. External UIA verifies Toolbar type, ARIA role, control/content views, child Invoke and complete Korean names. Evidence: `artifacts/tag-accessibility-2026-11-13/`. [Microsoft Toolbar contract](https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-supporttoolbarcontroltype).

For independent primary and × actions, use [InteractionTag](fluent2-interaction-tag.md). Selected emphasis, avatar media and full conformance remain separate open work.

`UiTagOptions.Selected` also provides visual emphasis for a basic Tag. It does not add Toggle/selection capability; use the standalone InteractionTag.Selected binding for controlled selection. See [controlled selection](fluent2-interaction-tag.md#controlled-selection-emphasis).


## Borrowed avatar and image media

```csharp
using var avatar = Fluent.Avatar("정세일", new(Color: UiAvatarColor.Brand));
using var tag = Fluent.Tag(new UiTagContent("정세일", Media: UiTagMedia.Avatar(avatar)));
// Raster or SVG-decoded image owners use UiTagMedia.Image(image).
```

`UiTagContent.Media` is a decorative leading slot for both basic Tag and InteractionTag. Use either Icon or Media; supplying both fails at construction. Pinned avatar defaults are 28/20/16 for Medium/Small/ExtraSmall, square/circular according to tag shape, and the tag's direction. Automatic initials resolve at the adapted size using the existing Korean/grapheme rules, reusing derived strings until the source changes. The borrowed avatar's configured size, shape and direction remain unchanged.

Each tag binds and disposes its own inactive composite and shadow resources. Multiple tags can borrow the same avatar at different sizes in one frame without sharing conflicting paint definitions. The avatar/image owner must outlive all consuming tags; disposing a tag does not dispose the borrowed owner. Owner content changes appear on the next requested frame. No new timers, renderer, shader or XML parser are introduced. A disposed avatar owner fails explicitly when painted.

`UiTagMedia.Image` reuses cover-fit image painting with the tag's shape. SVG raster input uses the existing SVG image decoder once; an avatar's borrowed SVG icon still uses the existing vector icon path. Initials and the dismiss glyph use fonts. Media publishes no additional native Image or focus item: tag text must carry the relevant accessible name.

TagGroup accepts an optional `media: item => UiTagMedia.Avatar(owner)` selector. It runs once per source option at group construction, including currently hidden options. Keep icon-bearing options separate from media options. Custom presentations receive bound media in render content and call its `Paint`; raw factory values are bound by the tag and are not standalone painters.

Run `./run-gallery.ps1` → **Selection lists · combined**, scroll to **Assignee media**. Compare Korean initials, SVG-decoded image, presence, size/direction/shape, inactive display, selection/dismissal and **Restore assignee tags**. Evidence: `artifacts/tag-media-2026-11-13/`. Overflow, forced colors, extended touch targets, detailed focus styling and full official conformance remain open.
