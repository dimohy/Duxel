# Fluent 2 cross-platform reference scope

Last synced: 2026-11-05

> This is historical repository research. The [official-site scope](fluent2-scope.md) supersedes its implementation requirements. The325/299 figures below are not current requirement counts.

Five Android documentation cards were insufficient to define complete scope. The official Compose-v2 README and46 linked source bodies revealed36 names missing from the prior requirements. Generated input now preserves325 known names. This is not the final denominator:26 standard factory names exist and299 are unconnected. Missing factories do not prove absence of legacy functionality.

## Sources and versions

| Source | Pin | Reviewed scope |
| --- | --- | --- |
|[Fluent Android](https://github.com/microsoft/fluentui-android)|40095bd360026497c0e92159bc307ac82103e655|46 README Compose-v2 source links and public declarations|
|[Fluent Apple](https://github.com/microsoft/fluentui-apple)|03928222b0fde3621fd5b6ea5c7ff1aeefdc1f95|iOS/macOS READMEs,candidate directories and selected child paths|
|Existing React/Web Components|8830c10bfb2f4c1bdd540287b09713b9679cd1ad|Existing pinned source/catalog/API evidence reused|

Compose-v2 is an architectural label,not visual Fluent2 qualification. XML-v1 controls were neither promoted to Fluent2 nor deleted from review.41 iOS/10 macOS candidate directories are not component counts; public API,infrastructure and design-generation review remain open.

URLs,commits and SHA256 are retained in artifacts/fluent-platform-inventory-2026-11-05/source-manifest.json. Parallel HTTP timed out after18 successful downloads. Those files were retained and matched byte-for-byte against raw Git objects at the same commit; the remaining sources came from that pin. Checkout line-ending conversion was detected and avoided by reading actual Git object bytes. Source versions or implementations were not substituted.

## Classification and mappings

46 source families map to54 rows. NotificationCommon/ListContentBuilder are infrastructure contracts/builders,not standalone widget factories. Public Avatar ActivityRing/ActivityDot,Drawer BottomDrawer and ListItem child renderers remain requirements.

| Source name | Mapping | Meaning |
| --- | --- | --- |
|CheckBox|Checkbox|Spelling mapping,not implementation acceptance|
|ToolTipBox|Tooltip|Native API name retained in mapping data|
|Pill source file|PillButton/PillBar|A filename is not invented as a widget|
|ListItem.Header,etc.|ListItemHeader,etc.|Public child renderers remain separate requirements|
|NotificationCommon/ListContentBuilder|infrastructure|Not misclassified as widgets|

Full mappings are in android-component-mapping.json and the committed tests/Duxel.Generators.Tests/Fixtures/FluentAndroidComponents.json.54 rows were appended to Core FluentComponents.tsv without deleting prior289 names/references. The deduplicated known-name set is325. Previously missed AcrylicPane,FileCard,PeoplePicker,PillTabs,SideRail,etc.are now explicit. Existing controls are not relabeled as implementing them.

## Consumption and regression

The Core source generator consumes the TSV. Official raw references allow only Microsoft's fluentui/fluentui-android/fluentui-apple repositories. Tests reject lookalike repositories,foreign owners/hosts,credential-bearing URLs and HTTP. URL ownership validation does not certify behavior or release status.

81 generator checks audit the production TSV against46 committed source descriptors/54 rows. Removing a row,promoting infrastructure or adding a second helper-as-widget row is rejected. Strict completeness continues to reject missing official factories and draft scope.72.NET10 widget-tree checks passed.

Actual.NET10 and pinned RC3 NativeAOT catalogs agree on26/325/299 and ScopeComplete=false. The existing graphics host's catalog-only mode creates no window/GPU. Render/behavior source hashes match the prior Badge qualification,so expensive GPU/full runs were reused rather than repeated. Previous full1095/Native243 results are not presented as a new full rerun.

## Remaining scope

Web Components public API details,iOS/macOS APIs/design generations and complete platform alias/variant/state/accessibility mappings remain open.325 is not forced into a final denominator. Actual widgets,official visual parity,behavior,UIA and performance require their own evidence. Existing optimization,Pi,legacy exposure and accepted resize work remain intact.
