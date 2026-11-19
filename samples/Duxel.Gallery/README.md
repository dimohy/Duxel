# Duxel Gallery

Nav destinations accept `UiNavSecondaryAction`. The action paints on hover/focus and remains accessible at rest; right-click, Shift+F10 and the menu key expose the same operation. Open `동작과 탐색 → Nav → 개요 고정` to verify selection preservation and execution count. [API and input rules](../../docs/gallery-navigation.md).

The sidebar uses shared `Fluent.Nav`. Group descriptions, vector icons and missing badges remain visible; click a category heading to collapse or expand it. Open `동작과 탐색 → Nav` for keyboard and RTL examples. The fixed **밝은 테마** switch selects light or dark mode.

Run `./run-gallery.ps1` on Windows. The light/dark switch is in the upper-right corner. The sidebar groups components by function and alphabetizes them within each group. Missing components have a **미구현** badge. Page headings match their sidebar labels. Each example places its source below the preview and settings. Find the menu names for Korean example labels in [the navigation guide](../../docs/gallery-navigation.md).

Ordered packets now build one final drawing while retaining all input traversals. In **Repaint observation → CPU stage measurement → Inspect measurements**, compare input steps with built drawings. FPS sampling counts presentation passes. [Common phase, custom painting and limits](../../docs/input-event-batches.md#input-and-presentation-phases).

The common drawing path now cleans unfinished layers after a failed callback while preserving previously claimed frames. Builder users should follow the consuming [Build/ownership contract and focused fixture](../../docs/input-event-batches.md#unpublished-drawing-ownership). Common input/presentation phase activation is described above.

Open **Repaint observation**, enable **CPU stage measurement**, then press **Inspect measurements** for the previous completed Render's input/drawing counts, preparation/composition/finalization time and current-thread allocation. Profiling is off by default and the snapshot does not update itself. [Scope and repeatable diagnostic](../../docs/input-event-batches.md#cpu-processing-measurements).

The Windows **Input·IME** page can also check focus loss: hold an arrow, switch apps, release it there, and return. The key should stay released until the next down. [Owned hidden-window fixture and limits](../../docs/input-event-batches.md#windows-focus-loss-and-held-keys) cover repeat deadlines and exact down/loss/down order without global input injection.

Windows uses delivered OS key repeats. In **입력·IME**, hold an arrow and check movement against actual repeats; idle renders no longer add another engine-owned movement. The [repeat-ownership fixture](../../docs/input-event-batches.md#repeat-ownership) compares platform and manual-engine hosts with deterministic idle frames and source transitions. Mouse-held spin buttons retain their own repetition.

In **입력·IME**, hold an ordinary character or an arrow key and inspect the text/caret after queued input is processed. The [native repetition fixture](../../docs/input-event-batches.md#native-repeat-counts) deterministically sends coalesced counts to an owned hidden window, including 65535 and split UTF-16. It verifies metadata, actual editors and nonrepeating queries without global keyboard input.

After `./run-gallery.ps1`, use **입력·IME** to compare separate clicks, double clicks and held arrow keys; **숫자 입력** also exercises held spin buttons. Input event time now remains separate from frame/animation time when events share a host batch. For deterministic queued-time reproduction, use the [timing fixture](../../docs/input-event-batches.md#event-time-and-frame-time). This does not establish input-only CPU optimization or measured FPS improvement.

Run `./run-gallery.ps1` to open a rebuilt Windows Observer using native ordered input. In **입력·IME**, type while switching fields and inspect source text and restored carets. Switch pages and return to inspect live hidden input preservation. The [native input/lifetime and App fixtures](../../docs/input-event-batches.md#windows-native-ordered-capture) pass 363 and 37 checks, including actual editor removal and worker/Vulkan consumption at scales 1 and 2. Owned input disposal retires IME queues through the required `ReleaseInput` contract; [custom handler migration](../../docs/windows-ime-routing.md#removed-and-hidden-inputs). Existing running generations keep their loaded binaries. Actual keyboard/TSF and candidate-window pixels require separate acceptance; Linux native capture remains aggregate.

The [ordered Core input fixture](../../docs/input-event-batches.md#ordered-core-input-consumption) demonstrates text/click/key order and one final GPU readback. The Windows Observer now consumes this path through App. Core still composes each logical step; this does not establish input-only CPU optimization.

For the unclaimed-frame texture lifetime regression and its actual Image GPU comparison, see [input event batches](../../docs/input-event-batches.md#unpublished-drawing-resource-lifetime).

In **입력·IME**, inspect composition and committed text while switching between input fields, then return to the original field to inspect its cursor. Native message routing is verified separately from complete keyboard/TSF service acceptance. [Scope](../../docs/windows-ime-routing.md).

Use **입력·IME → 한 줄 커서 예제** for ordinary typing and Ctrl+V as well. Selection replacement and input length limits retain the caret at the accepted insertion edge. [Validation scope](../../docs/windows-text-shaping.md).

Use **입력·IME → 한 줄 커서 예제** to compose Korean inside a sentence and inspect the caret/candidate position before and after commit, including clicks, Shift-clicks and drag selection. The shared input path now retains bidi commit affinity and uses composed display width for horizontal dragging. [Validation scope](../../docs/windows-text-shaping.md).

Windows accessibility text size and Linux X11 desktop DPI/font size apply automatically. Open a new window with `./run-gallery.ps1` and scroll **타이포그래피** vertically to inspect glyph edges. Raster text places completed bitmaps on the physical pixel grid; natural layout sizes update with text scaling. [OS settings and validation scope](../../docs/system-typography.md).

The Windows launcher separates its incremental build cache from per-run runtime copies. It can rebuild while an earlier window remains open without overwriting loaded DLLs. The console reports each `Observer runtime` path; inspect the new window for updated behavior.

Windows header **V-Sync / 이벤트 대기 모드** label alignment is corrected. In **입력·IME → 한 줄 커서 예제**, place the caret inside the sentence and compose Korean to inspect whole-context text, underlines, caret and horizontal scrolling. The source binding remains unchanged until commit.

Run `./run-gallery.ps1`, then choose **입력·IME → 한 줄 커서 예제**. The name field receives a mixed-script sentence. Click characters and try Left/Right, Shift and key repeat to inspect physical caret edges and selection. Delete/Backspace preserve logical source order. [Scope](../../docs/windows-text-shaping.md).

Run `./run-gallery.ps1` and open **Markdown → 문단 조판 예제** to inspect decoded `&amp;`, `&eacute;` and `&fjlig;`. Smart editing reveals the active block's literal source. Leaving that block restores rendered characters, with no caret stops inside hidden entity syntax. [Detailed scope](../../docs/styled-paragraphs.md).

**Markdown → 표 셀 예제** now includes bold Hebrew text before and after the table. Use preview and smart editing to inspect mixed native paragraphs, table alignment and source reveal together. Windows verification covers the unchanged real example button; fresh Linux runtime qualification remains pending.

Run `./run-gallery.ps1` on Windows and select **Markdown → 표 셀 예제**. Inspect mixed bold/RTL text and left/center/right alignment; switch to smart editing to reveal the original table and try arrows/Shift selection. Rendered cells retain natural widths and scroll horizontally instead of adding soft wrapping. The real example button is included in presentation verification. Fresh Linux qualification waits for the pending Ubuntu recovery approval; its preserved delivery is unchanged. [Details](../../docs/styled-paragraphs.md).

Run `./run-gallery.ps1` and select **Markdown → 구조적 문단 예제** to inspect mixed-script quotes, checked/unchecked tasks and a thematic break. Switch to smart editing to reveal a task list's original markers, then leave the block to restore its checkbox objects. The verification runner invokes the real example button and waits for source/viewer readiness and a presented frame. Tables and fresh Linux native qualification remain separate work. [Scope](../../docs/styled-paragraphs.md).

In **Markdown → 문단 조판 예제**, select smart editing and use Left/Right and Shift across Hebrew/Arabic text. Native geometry preserves the physical caret edge; held arrows use the same path. **이미지 예제** also exercises atomic image and wrap boundaries. Source editing and Ctrl+word commands keep logical order. Fresh Linux qualification and single-line navigation work remain open.

On Windows run `./run-gallery.ps1`, then select **Markdown → 이미지 예제**. Ordinary image paragraphs use native object allocation, proportional sizing and source-safe pointer geometry. Switch to smart editing and move across an image with Shift held to inspect selection. The verification runner invokes the real example button and checks source/viewer readiness before presentation (42 total scenarios). New Linux native qualification is pending; the preserved Linux delivery is unchanged. [Details](../../docs/styled-paragraphs.md).

**타이포그래피 → 자식을 소유한 문단** demonstrates actual title/body children sharing mixed-weight shaping and bidi context. The base-direction switch updates both literal and owned examples. A rotated title and working link retain their actual widget lifetimes. New Linux code is not runtime-qualified; the previous delivery remains selected.

On Windows, run `./run-gallery.ps1` and choose **Markdown → 문단 조판 예제**. Switch between preview and smart editing to inspect mixed weight, Korean, bidirectional text, code, links, carets and Shift selection. The new text-only paragraph path still requires Linux qualification after Ubuntu I/O recovery; the previously delivered Linux binary does not include this example button.

Open **타이포그래피** and scroll to **혼합 굵기 문단** for actual literal paragraph examples on Windows and Linux. Both share native wrapping, paint and geometry. Windows64 exact native GPU pairs/38 Observer scenarios and Linux NativeAOT351/334/114 checks plus41 candidate/38 delivered scenarios pass. Linux delivery `e529777d` runs through `./artifacts/linux-present-path-2026-11-15/run-observer.ps1`; earlier binaries remain preserved. [API and limits](../../docs/styled-paragraphs.md).

Open Input/IME and select the kerning/mixed-text example: it now includes a native RTL embedding across the Korean font interval. Windows font-context16, geometry29 and actual Observer38 pass; the rebuilt Linux shared sample passes41 candidate/38 delivered scenarios with delivery `7746d0b7`. Run `./run-gallery.ps1` on Windows or `./artifacts/linux-present-path-2026-11-15/run-observer.ps1` for Linux. Full mixed-style paragraph composition remains open. [Current native shaping scope](../../docs/windows-text-shaping.md).

Current delivery (2026-11-15), superseding older records below: a scroll view fixes constrained-height MessageBar page termination. Passed38 Windows,41 Linux NativeAOT and38 delivered scenarios. Windows: `& 'W:/MyWorks/Duxel/run-observer.ps1'`. Linux: `& 'W:/MyWorks/Duxel/artifacts/linux-present-path-2026-11-15/run-observer.ps1'`. The Linux manifest selects `c8246d`, preserving prior `43119a`. RTL updates actual body line alignment and action placement. Full bidi remains incomplete, the six existing Windows shaping failures are fixed through shared DirectWrite shaping (37 expanded checks passed). [Qualified scope](../../docs/fluent2-messagebar.md).

2026-11-15 Fixed MessageBar page termination on constrained height by owning a scroll view. Verified small height,125% text and actual add/reorder/remove commands. Use the **키를 가진 동적 메시지** section. Passed36Windows,39Linux NativeAOT and36delivered-launcher scenarios. The Linux manifest selectsfff949;restart an old window using the documented launcher.


The MessageBar page now owns a real Group. Use **닫기**,**모두 다시 열기**,and **동작 줄이기** to inspect exit fade,reflow and focus. Passed17models,140GPU pairs,32Windows pages,and35pinned RC3 NativeAOT Linux presentation cases plus32delivered-launcher pages. The Linux manifest now selects310c73;restart an old window with the documented launcher. Automatic announcements,title relations and full dynamic-collection conformance remain pending.


2026-11-15 Fixed Windows startup by mapping the new MessageBar factories to an actual **메시지 표시** demo page while retaining the startup catalog gate. Run `./run-gallery.ps1`; `./run-gallery.ps1 -Verify` presents all 32 pages and exits. Separate process logs are retained under `%LOCALAPPDATA%/Duxel/ImplementationObserver/logs/`.

Corrected the shared Linux font descent sign, fixing vertical alignment for buttons, badges, checkboxes, radio labels, spinners, body text, input and spin buttons. Restart the old window using `& ./artifacts/linux-present-path-2026-11-15/run-observer.ps1`. Validation passed 25 native metric cases, 32 widget/caret pixel comparisons and 35 NativeAOT presentation scenarios. This change does not establish a performance improvement.


2026-11-15 standalone Fluent text now publishes actual Korean content, updates and stable native identities in the current delivery. CPU64, Label63,24raw GPU pairs and independent native clients passed. Three new performance pairs have overlapping ranges, so their direction is inconclusive; prior measured binaries remain preserved.

2026-11-15 the current delivery includes the **스켈레톤** tab. Three independent runs per variant compared it with the protected Linux performance baseline using 4 workers. No disjoint regression appeared in the tracked CPU/input/composition metrics; this does not claim a new speed improvement. `artifacts/linux-observer-delivery.json` is the single current-delivery manifest. Historical measured executables and records remain preserved.


2026-11-15 latest GPU camera/linear-gradient builds and launch commands: [SVG guide](../../docs/svg-gpu-camera.md). Linux presentation32 passes; whole-browser pixel equality remains incomplete.

SVG GPU camera work: the SVG tab uses retained GPU vectors for zoom and the visible output window. Explicit image decoding remains a bitmap example. See [architecture and qualification limits](../../docs/svg-gpu-camera.md); Windows full Observer158 and Linux29 pages plus3 camera scenarios now pass. Complex pixel conformance remains incomplete.

The top-right **갱신 영역 강조** switch defaults to off, covers all pages/popups and persists across navigation. [Run the same Observer on Linux](../Duxel.ImplementationObserver.Linux/README.md).

**갱신 관찰**: inspect actual GPU repair with green outlines. Edit text, use **한 번 변경**, or toggle **텍스트 자동 변경 (60Hz)**. **측정값 확인** snapshots preceding metrics. Disable **갱신 영역 강조** for performance comparisons. [Guide](../../docs/repaint-observer.md).

Use **팝업 열기 방식** on Popover to cycle Click→Hover→Context. Hover leaves close after 0.5 seconds and reentry cancels. Context opens on right click or Menu/Shift+F10 with trigger focus.

On Popover, enable 팝업 화살표 and change size/appearance to inspect 6/8px arrows and combined shadows. Existing editing, commands, dismissal and focus behavior remain available.

Use the Popover tab for owned content, Korean editing, commands, closing, trapping, sizes, appearances and rejected closing. Run `./run-gallery.ps1`; use header overflow when hidden. [API and limits](../../docs/fluent2-popover.md).

Interactive tags: choose features in **선택 목록 / 확인할 기능**. Upper primary text updates **태그 확인**; × clears selection. Lower basic tags dismiss from their entire surface. Delete on an interactive tag requires focus on ×. [API](../../docs/fluent2-interaction-tag.md).

Tag focus: select two features in **선택 목록**, Tab into the tags and navigate to the second tag. Tab out and Shift+Tab back to verify remembered focus. Deleting the last tag while its × is focused transfers focus outside the group.

Selected tags: choose features in **선택 목록 / 확인할 기능**, then click a lower basic tag or the upper interactive tag’s × to clear the shared selection. Inspect **태그 크기 전환**, direction changes and page persistence. [API](../../docs/fluent2-tag.md).

City popup control: **도시 목록 열림 전환** changes the actual Open binding. Its displayed counter changes only on real visibility transitions. Empty search results display unselectable explanatory text.

City search: use **선택 목록 / 도시 검색** to type `New ` or Korean text, select options, retain freeform text and switch pages. Shared size/appearance/direction/width controls also affect the editor. [Combobox](../../docs/fluent2-combobox.md).

Dropdown: run `./run-gallery.ps1` → **선택 목록** for single/multiple selection, SVG options and size/appearance/direction/width controls. State persists across tabs. [API and limits](../../docs/fluent2-dropdown.md).

AvatarGroup: run `./run-gallery.ps1` → **아바타 그룹** to change Spread/Stack/Pie, size, inline count and RTL. Activate the count or pie to open the real member list. Image/Korean/SVG content and selected names persist across tabs. [API and limits](../../docs/fluent2-avatar-group.md).

Breadcrumb: run `./run-gallery.ps1` → **경로** to change width, direction, size and keyboard mode; inspect the overflow menu and retained navigation history. Default icons use the Fluent font; SVG icons can be supplied separately. [API and limits](../../docs/fluent2-breadcrumb.md).

Card: run `./run-gallery.ps1` → **카드**. Change appearance/size/width, edit **카드 메모** and press **카드 내부 실행**. Change pages and return to check retained state. The gallery covers twelve default size/appearance combinations. Use **카드 포커스 방식** for Off/NoTab/TabExit/TabOnly and **카드 루트 사용 전환** to disable only the root while children remain usable. Dedicated slots remain in progress. [API and limits](../../docs/fluent2-card.md).

Avatar inactive appearance: run `./run-gallery.ps1`, select **아바타**, then **아바타 비활성 전환**. Try the image, SVG icon, Korean initials and presence controls together. Inactive scales the complete group to0.875 and applies opacity0.8 once; ring/shadow selections return when restored. No periodic frame is scheduled. [API and current validation](../../docs/fluent2-avatar.md).

Run from PowerShell at the repository root. Windows and the .NET 11 SDK selected by `global.json` are required.

```powershell
./run-gallery.ps1
```

The command builds current local source, opens one window and waits until it closes. Run it again to inspect new source changes. Build artifacts go to `%LOCALAPPDATA%/Duxel/ImplementationObserver/build`. The sample's reference projects are validated with .NET 11 without changing their public supported frameworks.

| Tab | What to inspect |
| --- | --- |
| Overview | Current observer scope |
| Existing API | Actual Hello, Immediate and Widget Gallery controls and floating panels |
| Fluent | Button counter, toggles, checkbox, radio, slider, progress, badges and local link |
| Typography | Exposed Fluent text sizes and weights |
| Input / IME | Korean composition, label focus, validation, multiline editing, wrapping, read-only and undo |
| Tooltip | Delays, arrow, description/label relationships, caller-controlled visibility and Escape |
| Containers | Single-child layout, Flex, Stack, Wrap, Grid and live option changes |
| 접기·펼치기 | Accordion single/multiple expansion, saved panel input, keyboard order, disabled header and size/chevron options |
| Virtual list | Keyed billion-row editing, end navigation, reordering, variable heights and row disposal |
| Rendering | Retained primitive geometry, callback counts, independent 3D projection and explicit animation |
| SVG | Ordinary SVG image input, zoom/rotation/translation, regular/filled menu and tab icons, parse/raster/upload request counters |
| Coverage | Demo tabs for all40 exposed factory names and remaining scope |

Increment the counter or edit a note, visit another tab and return to check retained state. Long pages use the actual shared ScrollView.

The Hello → Immediate transition schedules its body frame in event-wait mode. Its embedded menu does not change the observer window's global top inset. GPU regression checks compare the first body selected through pointer press/release with four subsequent frames.

The header is an actual `Fluent.TabList`. Tab enters it, arrows move focus and Enter/Space selects a page. Arrow focus alone does not replace the selected page. An underline and stronger text identify the selected tab.

All eleven pages use `RenderPanel`: only the selected callback executes, each body keeps its page instance, and accessibility connects the tab to that body in both directions. Tab proceeds from the header to the named panel and then its controls; Shift+Tab reverses this order. Rebuild with `./run-gallery.ps1` after closing an earlier observer window to see current source.

Select SVG in the top tabs (or the overflow menu). Compare the ordinary bitmap on the left with the dedicated SVG widget on the right. Drag SVG 확대/회전/이동, reset the transform, switch icon tabs and open the icon menu. Rotation/translation and page round trips preserve XML/scene/raster counts; a new zoom resolution may rasterize/upload once. The counters create no periodic frame requests.

The curved caption in **이미지·텍스트** now uses tref inside textPath and a reusable text definition from text.svg. Changing that definition in the running output copy and explicitly reloading updates the caption; scale/rotation/replay keep the compiled resource.

The SVG page has real **이미지·텍스트** and **문서 기능** tabs. Select the second to inspect repeating stripes, radial gradient with a reused symbol, a cutout mask and an arrow marker. Five source assets share the same store. Both panels use the same zoom/rotation/translation controls and retain their cached scenes when switching.

Generated/generic Sliders in Fluent and SVG publish numeric Windows RangeValue data and accept typed accessibility changes. SVG movement keeps its1px step and shared cache. The strict external retained getter/removal gate now passes, with a passing Tooltip shutdown regression. Legacy Slider numeric access is also qualified; narration, complete legacy tab keyboard/flag coverage and NativeAOT remain open; see [Slider verification limits](../../docs/fluent2-slider.md).

SVG adjustment steps are1px for translation,1° for rotation and1 percentage point for zoom (25–400%). Click a slider and use Left/Right arrows for precise single steps. Labels and the current-value line show these units.

The original demo assets are under Assets/Svg. Runtime reads their output copies under `%LOCALAPPDATA%/Duxel/ImplementationObserver/build/bin/Duxel.ImplementationObserver/release/Assets/Svg`. Editing a running output copy and pressing SVG 파일 다시 읽기 reloads it explicitly; unchanged bytes keep scenes/cache. Rebuild after editing repository assets. See [SVG APIs and limits](../../docs/svg-support.md). The text.svg example uses explicit OS Segoe UI regular/bold and Malgun Gothic files, with regular/bold Latin, Korean and curved text. The same transform controls apply to text. Registered-face/provider/glyph counters remain unchanged on replay and scaling. Full feature/text-script/AOT qualification remains open.

The header also publishes Windows UIA selection changes. A separate owned client fixture verifies selection, explicit deselection, removed-item invalidation and unchanged-frame suppression; see [TabList qualification](../../docs/fluent2-tablist.md).

Page changes also raise ControllerFor relationship notifications for assistive clients. [Relationship qualification](../../docs/accessibility-relationships.md) records the actual generated-tab event payload checks and separate label/controller replacement fixture; the logs are available under `artifacts/relationship-events-2026-11-11`.

Native focus now follows the final control for each frame and the actual focused window. Returning from another window restores the current tab or body control without a metrics timer. See [focus qualification](../../docs/accessibility-focus.md) for scope and remaining desktop checks.

The Fluent Spinner starts paused. Use `Spinner 재생 전환` to play or pause it; pausing returns to event waiting. Fixed determinate progress and FPS text do not request continuous frames. The tab panel focus outline stays inside all four padded viewport edges.

The top contains one metrics line and one options line. V-Sync defaults off and can change at runtime. Event waiting defaults on: without changes, input or animation deadlines, the loop does not create frames. Turning it off enables continuous rendering. Both modes retain internal caches. Widgets may still schedule required deadlines such as tooltip delay or caret blinking.

UI FPS measures actual Render callback frequency, not monitor refresh or GPU completion. Metrics update only when a frame occurs and never request periodic rendering themselves. Idle values retain the last sample. Present, Draw, Upload and Cache describe the preceding renderer frame; zero Draw does not imply zero CPU work or uploads.

Change grid columns, alignment and overlay position in Containers. Edit the first virtual row, visit the end and return to inspect model retention and disposal counts. Reordering swaps the first and last keys. Nested scroll owners allow wheel handoff at their boundaries. In Rendering, changing static content rebuilds its drawing callback once; unchanged frames reuse it. This count does not represent total CPU/GPU cost.

The observer is expanding. Actual OS input, complete desktop interaction qualification and the original goal's remaining ledger scope are still open. Available demos do not establish full Fluent visual/accessibility conformance or current NativeAOT qualification.

On Fluent, scroll to **탭 표현과 아이콘**. Four lists share selection and demonstrate icon/text, icon-only, disabled, subtle-circular and filled-circular states. See [tab content](../../docs/fluent2-tab-content.md) for the API and qualified scope.

On Containers, scroll to **Overflow 공통 배치**. Click an item counter, switch width between 200/560, promote item 4, then toggle RTL. Hidden counters keep their values. The badge indicates hidden content; the actual tab overflow example is documented separately. See [overflow layout](../../docs/overflow-layout.md).

Below the overflow example, **Menu 공통 동작** provides **공통 메뉴 열기**. Try disabled rows, arrows, Enter/Space, Escape/Tab, outside click and the selected-action text. This common menu foundation is separate from the separate Fluent tab overflow integration. See [menu interaction](../../docs/menu-foundation.md).

**Fluent 메뉴 열기** in the same Containers section uses the generated Fluent.Menu factory. Compare it with the theme menu; both preserve disabled items and use the shared popup. See [Fluent menu scope](../../docs/fluent2-menu.md) for qualification and remaining shadows/full fidelity/tab integration.

On Containers, **탭 넘침과 본문** demonstrates the actual TabList overflow menu. Click the Home body counter, use the ellipsis to select Help, and return Home to check retained values. Width 160/520 and RTL controls change parent allocation; this does not resize the native window. See [tab overflow](../../docs/fluent2-tab-overflow.md).

The top page tabs also expose an ellipsis when their available width is insufficient. All ten pages remain reachable through that menu. The tab overflow/body example additionally has a horizontal/vertical control. These fixed-viewport checks do not qualify native window resize handling.

Containers → Menu common behavior → 긴 메뉴·문자 탐색 demonstrates bounded height, wheel, shared scrollbar and first-character navigation. Open it, press Z to reveal the last action, then Enter to invoke. Repeated A cycles Apple and Apricot.


Hidden long-menu items can be revealed through Windows UIA ScrollItem; the menu provides Scroll. Reveal does not move the outer container page. See [menu scrolling accessibility](../../docs/fluent2-menu.md) for scope and evidence.


Fluent → progress: the upper Slider keeps brand colors during hover and press. The lower ProgressBar is a read-only progress indicator and does not change with Slider hover. See [Slider guidance](../../docs/fluent2-slider.md) for official color sources and qualification.


Top-level tab overflow and container menu buttons support Windows UIA ExpandCollapse, actual menu open/close events and expansion-state changes. Run ./run-gallery.ps1 for current local source. Complete menu visuals and screen-reader narration remain separate qualification work.

Run `./run-gallery.ps1` → **컨테이너** → **Menu 공통 동작** → **체크 메뉴 열기**. Details/grid use borrowed bindings, stay open when toggled, and retain values across page navigation. **설정 마침** closes the popup. Keyboard, pointer and Windows Toggle share the same activation path. See [checkbox menu scope](../../docs/fluent2-menu.md).

The adjacent **라디오 메뉴 열기** offers independent sorting/view groups. A radio choice closes the popup; reopen to inspect the current selection, or leave/return to confirm retention. Windows Selection/SelectionItem exposes the actual named group containers. [Radio menu contract](../../docs/fluent2-menu.md).

**아이콘 메뉴 열기** combines compiled regular/filled icons, trailing information, a checkbox and disabled rows. Hover the label and trailing caption separately; **메뉴 방향 : LTR/RTL** mirrors the columns. Secondary information is exposed as HelpText. These icons use TTF glyphs. The separate SVG tab exposes SVG bitmap input, zoom/rotation widgets and SVG menu/tab icons; see [SVG support](../../docs/svg-support.md).



Legacy SliderFloat/SliderInt, scalar/vector/array wrappers, SliderAngle and VSliderFloat/VSliderInt share the typed numeric queue and live enabled/clip/range validation. Float values remain continuous: horizontal step is a keyboard increment, not a numeric snapping grid. Continuous controls report SmallChange0; integers report1 and retain ties-to-even float rounding. Angle publishes degrees and writes radians to its caller. Integer publication reflects final wrapper conversion. Existing float-based integer/double precision and keyboard/focus behavior are unchanged; no full-width precision or new Focus capability is claimed. Legacy tab selection/panel relationships are qualified separately; full keyboard/flag coverage remains open. Focused legacy16 (combined44), Observer127, external legacy client6/host3 and Observer GPU47 including actual pointer-selected Immediate controls pass. Run ./run-gallery.ps1 → Existing API → Hello / Immediate. Evidence: artifacts/slider-range-2026-11-11/legacy-verification.json.

### Legacy BeginTabBar/BeginTabItem semantics (2026-11-11)

The streaming legacy API now publishes a TabList, real Tab SelectionItems and only the active TabPanel, with reciprocal ControllerFor/LabeledBy links. Body controls belong to their panel; nested bars restore the original outer parent. TabItemButton remains an Invoke button, outside tab selection. Required selection rejects removing the selected header. Closing or removing a pending target cannot report a successful selection that was never published.

Pointer and accessibility selection preserve the existing next-frame transition, with one body per frame. A UiAccessibilityTree Select request with wait=true completes after that selected frame is published; closing/removing the target before publication returns InvalidOperation/Unavailable. Existing Windows SelectionItem methods still accept requests asynchronously: observe committed property/selection events before reading the new state. Selected headers/panels keep the legacy nonfocusable contract; full keyboard/flag coverage and narration remain open. A committed transition schedules no continuing wakeup or animation deadline.

Managed legacy25, common TabList197, actual Observer131, external legacy client15/host3 and generated-tab regression client25/host3 pass. ObserverGPU47 passes and all73 comparable body captures match the prior numeric-accessibility baseline (FPS header excluded). Tests distinguish equally named headers/panels by their actual TabList scope; generated panel checks still require focusability by default, while legacy checks explicitly require false. Run ./run-gallery.ps1 → Existing API → Hello / Immediate. Evidence: artifacts/legacy-tabs-2026-11-11/verification.json. NativeAOT/full-goal qualification is separate.
In Fluent, the mixed Checkbox, shared Switch and persistent ToggleButton now expose actual Windows Toggle states/actions. A control painted earlier observes a shared mutation on the requested next frame. See [common accessibility](../../docs/widget-accessibility.md) and artifacts/toggle-accessibility-2026-11-11/verification.json.

### RadioGroup selection accessibility (2026-11-11)

Run `./run-gallery.ps1`, open Fluent and use Processing mode (처리 방식). The actual options now expose native RadioButton SelectionItem under a named single-selection group, with descriptions and real keyboard focus. A UIA Select changes the original choice binding and clears the previous option; disabled/removed controls reject actions. Parent allocation and normal painting remain unchanged. Focused18, prior input/allocation91, Observer137 and external client9/host3 pass. ObserverGPU47 and73 unchanged prior bodies preserve the visual path. See [RadioGroup](../../docs/fluent2-radio-group.md) and artifacts/radio-accessibility-2026-11-11/verification.json. Narration, full Fluent state coverage and NativeAOT remain separate.

### Measurement reuse (2026-11-12)

Run `./run-gallery.ps1` and select **측정 재사용**. The Checkbox/Slider/RadioGroup values remain interactive while the child measurement counter stays unchanged. **프레임 확인** requests an input frame without changing geometry. **측정 라벨 변경** and **측정 너비 변경** invalidate geometry and increase the counter. Switch tabs and return to verify retained state. The counter is CPU child measurement, separate from FPS/draw/upload, and never requests periodic frames. Focused14/containers179/Observer142, GPU28/ObserverGPU50 and73 unchanged prior bodies pass. [Contract and limits](../../docs/widget-cpu-retention.md); artifacts/leaf-layout-retention-2026-11-11/verification.json.

2026-11-12 content follow-up: **측정 재사용** now shows8controls:body,Checkbox,Slider,RadioGroup,Link,Progress,CounterBadge,paused Spinner. Moving the slider updates both its value and the real Progress getter without increasing child measurements. Click the actual Link to update its navigation count; it stays inside this sample. **측정 본문 변경** updates body/badge/spinner sources and increases measurements. Dynamic text/badge getters and custom traits still remeasure; a delayed Spinner stays dynamic until shown. Focused26,Observer145,contentGPU28/previousleafGPU28/ObserverGPU50 pass. Earlier73bodies and121leafPPMs remain exact; this expanded page intentionally differs. See [contract](../../docs/widget-cpu-retention.md) and artifacts/content-layout-retention-2026-11-12/verification.json. No new timing/NativeAOT/full-goal completion claim.

## Standalone icons

`Fluent.Icon(painter, new(24, 24), foreground)` creates a decorative retained widget. Use `UiMenuIcon.Create<TRegular, TFilled>()` for compiled font/vector painters or `svgIcon.MenuIcon` for an existing `UiSvgIcon`. A standalone icon always paints the regular/rest variant; it has no pointer activation, focus stop or hover animation. Supply the foreground explicitly. SVG authored colors remain unchanged unless the borrowed painter uses `MultiplyForeground`.

The widget borrows its painter. Dispose widgets before their `UiSvgIcon`, and dispose SVG painters before the document/store. SVG source reload requests a frame through the existing SVG texture path. Repeated unchanged painting requests no periodic frame and reuses its raster/texture. `Size`, `Color` and `Icon` changes request a frame; invalid/empty inputs fail explicitly. `Size` is logical layout space; parent constraints can change the allocated viewport. This is not an accessible image role or an interactive button; use a labeled control for actions.

Run `./run-gallery.ps1` from the repository root, select **SVG**, then find **독립 SVG 아이콘 (Fluent.Icon)** below the icon menu. The same page also compares bitmap input with the transformable `UiSvg` widget. This demonstrates the new factory; full Fluent conformance remains incomplete.

The SVG page now uses Fluent.Image for the left bitmap snapshot and shows bitmap/SVG upload counters separately. [Image API and lifetime](../../docs/image-widgets.md).

## Standalone Label

Run `./run-gallery.ps1` from the repository root and choose **입력·IME**. Click **메모** to focus the separately rendered textarea. Buttons below it change the label's required marker,size,weight,disabled state and long wrapped text. Disabling the label alone leaves the editor enabled; Large uses semibold by design. [API and ownership](../../docs/label-widgets.md).

SVG page: the image/SVG corner slider changes by1px, with arrow-key support. Image fit buttons choose Stretch,Contain,Cover; reset restores radius0 andStretch. Radius/rotation/translation retain the raster; zoom may create a higher-quality resolution. Launch from the repository root using ./run-gallery.ps1. See ../../docs/image-widgets.md for contracts and validation limits.

Avatar: run ./run-gallery.ps1 from the repository root and select 아바타 (overflow menu if narrow). Change name,shape,size,color,image,SVG icon or explicit Korean initials; fourteen sizes appear together. Avatar is passive and does not drive periodic frames. Its default person icon is a bundled Fluent font icon; supplied SVG icons use the cached SVG painter. Full inactive appearance and shadow remain open. See ../../docs/fluent2-avatar.md.

## Standalone presence badge
`Fluent.PresenceBadge` supports eight statuses, OutOfOffice and six sizes. Run `./run-gallery.ps1` → **아바타** → **상태 배지**; change status, OutOfOffice and size. The scrollable page preserves state and requests no periodic frames. Default icons use compiled Fluent font glyphs; supplied SVG painters reuse their cache. [API, resource lifetime and validation](../../docs/fluent2-presence-badge.md). Avatar overlay and activity ring are described below; inactive appearance is described above.

## Activity ring and gallery scrolling

Set `avatar.Options = avatar.Options with { ShowActivityRing = true };` and optionally `avatar.ActivityText = "active";`. The default name gains the activity wording; `AccessibleName` still overrides it. The label is cached on mutation. A ring remains one passive Image with no action, focus, hover or animation timer.

The body keeps the selected14-size contract at natural size. Native layout measures the complete visual extent: stroke2 for body sizes<=48,3 for<=64,4 above64; the ring extends two stroke widths on each side (32px body→40px total). This reserves the decoration instead of painting over neighboring allocations. The body and gap scale uniformly under tighter parent allocations. Circular/square geometry and Light/Dark ring colors reuse the existing analytic border-only primitive and pinned persona palette. The gap paints nothing; it preserves the underlying pixels. Image sizes and ring toggles reuse the existing texture lease. `ShowActivityRing=false` restores the ordinary avatar; it does not claim Fluent inactive appearance.

Run `./run-gallery.ps1` from the repository root→**아바타**→**아바타 활동 링**. The sample sets Korean activity wording. The page now uses the common `UiScrollView`, allowing its growing gallery to exceed the viewport without flex overflow. Actual scroll-to-bottom and offset/model preservation across tabs are checked at800×400.

Current evidence: `artifacts/fluent-avatar-ring-2026-11-12/verification.json`, managedSDK11.0.100/runtime11.0.0. Full Fluent active/inactive transitions, forced colors and original exactRC3 NativeAOT/full-workload qualification remain open. [PresenceBadge](../../docs/fluent2-presence-badge.md) is available standalone and as the Avatar overlay described below, with a shared transparent cutout across image/initials/icon/ring paths.

## Circular subtraction
Use CircleCutout for a real transparent hole shared by primitive/image/text/SVG paint and retained/compiled layers. Run `./run-gallery.ps1` → **렌더링** → **원형 잘라내기**. [API and validation limits](../../docs/circle-cutout.md). Avatar presence consumes this mask; see the overlay contract below.

## Avatar presence overlay

Set `Presence` on `UiAvatarOptions` to `new UiAvatarPresenceOptions(UiPresenceStatus.Available)`; null removes it. `Size = null` selects the pinned automatic badge size: avatar <28 →6px, >=28 →10px, >=40 →12px, >=56 →16px, >=64 →20px, >=96 →28px. An explicit `UiBadgeSize` overrides this mapping. The badge anchors to the body's bottom/right, or bottom/left with `RightToLeft`. The transparent gap is1px through Medium and2px for Large/ExtraLarge. Image, initials, font/SVG icon and activity ring share the common analytic circle subtraction; the badge paints after that scope is disposed. No background-colored patch is used.

Natural measurement reserves the union of body, activity ring and badge, including an explicit badge larger than the body. The union fits uniformly inside the actual parent rectangle. `PresenceText = null` uses the status wording; set a nonempty complete localized status (including OutOfOffice when applicable) to override it. The cached accessible name combines person, status and activity; `AccessibleName` overrides it. There is still one passive Image provider, no child badge provider, focus stop, Invoke or animation timer. Existing source leases and borrowed icon ownership apply.

Run `./run-gallery.ps1` → **아바타** → **아바타 상태 배지**. Controls change status, OutOfOffice, automatic/explicit size and RTL; image, Korean initials, SVG icon and ring controls remain available. State persists across tabs. Managed validation and limits are recorded under `artifacts/avatar-presence-2026-11-12/`. Motion/forced colors, full Fluent conformance and original exactRC3 NativeAOT/performance gates remain open.

Validation:51/51 CPU contracts,8193 assertion executions(Avatar185,Observer191);Avatar GPU380 across Light/Dark and MSAA1/4,Observer11,native Avatar17. Existing233 Avatar images are byte-identical. This is managedSDK11.0.100/runtime11.0.0 qualification. External draw callbacks and distinct nested circle scopes remain unsupported by the common mask contract.

## Avatar activity shadow

Set `ShowActivityShadow = true` independently of `ShowActivityRing`. Both add the cached ActivityText once to the same passive Image name. The pinned body-size mapping is <=28→Shadow4,<=48→Shadow8,<=64→Shadow16,larger→Shadow28. Shadow-only uses the body contour;with a ring it uses the complete ring contour. The measured union reserves shadow support,ring and an optional oversized badge,then fits uniformly to the actual parent rectangle. The shadow paints before body/ring subtraction and the badge,matching the pinned shadow pseudo-element. Image/initials/font/SVG all use this shared path.

Run `./run-gallery.ps1` → **아바타** → **아바타 활동 그림자**. State persists across tabs and no periodic frames are requested. [Common shadow API](../../docs/box-shadow.md). Inactive0.875scale/group0.8opacity is described in the current inactive contract; transitions/forced colors remain open;existing per-command layer opacity cannot substitute for group composition. Current managed evidence: `artifacts/box-shadow-2026-11-12/`.

Validation:CPU52/52 contracts,8247 assertion executions(Shadow37,Avatar201,Observer192);four explicitly verified compiled profiles x256shadowGPU checks,Avatar392,cutout88,primitive74,rounded40,Observer12;nativeAvatar19. Assertions repeat across settings. Existing498 image files remain byte-identical. Sample description uses the common wrapped text presentation,including the800x400 scroll regression.

The Rendering tab also shows prepared black/white affine reference composition at opacity0/0.5/1, preserving separate RGB coverage even when source alpha is0. This is the compositor foundation; general interactive widget-subtree capture remains pending. Run `./run-gallery.ps1` from the repository root.

Runtime paint-only capture is available through `UiPaintGroup`. Rendering shows a fourth red/green overlap block at group opacity0.8; it is captured by real GPU passes and cached without a timer. General widget input/layout capture and arbitrary projective text quality remain pending.

Rendering also shows real Korean/Latin text captured at0.875 final resolution using the projected text mode. Existing physical LCD rendering remains unchanged.

`./run-gallery.ps1` → **카드** → **카드 루트 동작 전환**. Compare independent background/child execution counts and stable body placement on hover. Automatic focus is NoTab with a callback, Off without one; explicit modes override it.

Avatar check: run `./run-gallery.ps1` → **아바타** → **아바타 이니셜**, then enter **정세일**; the initial is **정**. The Korean preset follows subsequent name edits. Toggle presence, size and left/right placement to inspect the circular edge.

Card selection: `./run-gallery.ps1` → **카드** → **카드 선택 모드 전환**. Select via background; child buttons/inputs stay independent. Tab enters the semantic checkbox; Enter/Space toggle it. Selection excludes group focus modes. Model/change counters survive page changes.


The 카드 page uses `Fluent.CardFooter` for its real internal button and far-end execution count. Change width to verify alignment; click the button to verify independent child activation.


The 카드 page now uses real CardHeader image/title/description/action slots. Click 카드 정보 to update its description; change Card width and verify far-edge action alignment and independent root selection.


Rendering-tab crash fix: retained projected text now owns its cache source until content replacement or disposal. Both keeping the tab visible and returning after600 frames are covered by actual application-session regressions. Restart with `./run-gallery.ps1`; the canonical local build cache has also been rebuilt.


Run `./run-gallery.ps1` → **카드** → **떠 있는 선택 동작**. The real top-right checkbox and Card background share selection; the internal button increments only its own execution count. Selection changes are counted and survive page changes. This uses floatingAction, with shared rounded content clipping. Card Preview is documented below.


Run `./run-gallery.ps1` → **렌더링** → **둥근 클리핑 : 꺼짐**. Compare clipped retained primitives,3D projection and cached overlap capture; combine with 원형 잘라내기. Switching tabs preserves the choice. Animation remains explicitly controlled. [Paint scope](../../docs/rounded-paint-clip.md).


Run `./run-gallery.ps1` → **카드**, scroll to **둥근 경계와 입력**. The actual body-filling button obeys radius32 with padding0. Toggle 둥근 모서리, click the visible center and clipped corners, and watch the execution count. Settings persist across tabs. Popup and keyboard actions remain independent.


`Fluent.CardPreview(content, logo)` owns media and an absolute32px logo placed12px from the left/bottom. The logo does not contribute to intrinsic size. Put direct slots in `Dux.CardContent([...])` as the Card's primary child (composition-only wrappers are supported). Preview bleeds through the Card padding: vertical left/right and first top/last bottom; horizontal top/bottom and first left/last right. `UiCardOptions.Orientation` defaults to Vertical. Horizontal final Header/Footer grows into remaining bounded width. Other custom child trees keep their own layout. Main-axis overflow fails explicitly; CSS flex-shrink/min-content conformance remains open. Preview content and logo retain their actual input/accessibility providers; Card border paints above Preview. Replays keep measured allocations without scheduling periodic frames.

Run `./run-gallery.ps1` → **카드** → **미디어 미리보기**. Compare vertical/horizontal SVG cards, toggle preview width/size, and inspect the fixed logo and full-bleed edges. SVG is loaded once per content lifetime. Evidence: `artifacts/card-preview-2026-11-12/verification.json`. The46 exposed factories still connect23 of55 top-level names; this variant is not another top-level completion.


2026-11-12 minimum allocation correction: CardContent and direct vertical CardPreview preserve the Card body's minimum after shadow/padding deflation. Vertical Header/Footer first resolve their intrinsic width before the body stretches; a horizontal final Header/Footer receives remaining minimum width even when the maximum is loose. Preview keeps its natural main-axis extent; horizontal media centers within the minimum height. The prepared parent-input snapshot keeps replay stable. Run `./run-gallery.ps1` → **카드** → **부모의 최소 크기**; change parent minimum width and orientation. This uses an actual constrained parent without a fixed Card width. Full CSS min-content/flex-shrink conformance remains open.

Evidence: `artifacts/card-minimum-allocation-2026-11-12/verification.json`. Nine initial contract failures were reproduced; after both minimum propagation and intrinsic-slot correction, full CPU55/8772(Card419,Observer226), GPU12profiles2725(Card656), native80 and hidden/visible614-frame sessions pass. Of2332 existing RGB image baselines,2312 are exact and20 differ only in the dynamic HUD above y32;32 new captures and the changed Observer gallery have independent geometry/pixel/replay checks. Timing improvement was not measured. Factory/top-level counts remain46 exposed,23 connected of55. The original deployment/performance/full conformance gates remain open.

In **선택 목록**, scroll to **강조 선택**. The standalone tag supports a controlled selection binding; **태그 변경 허용 전환** demonstrates setter rejection without changing the displayed/native selected state.

Upper **확인형 태그** now bind independent emphasis values. Click a tag body to highlight it, remove it with × and select the feature again to verify retained emphasis. The visible feature collection and its lower basic tags remain independent of highlight state.

Scroll **선택 목록** to **담당자 미디어** for actual Korean-initial and SVG-image avatar tags. Compare size, shape/direction, inactive display and presence; dismiss and restore members. The avatar/image pipeline is reused and source owners outlive tags.

**인물 정보** demonstrates Persona: name editing, six sizes, text before/after/below, alignment/RTL, four information slots, presence-only/text-only, SVG image/initials and inactive avatar display. Header overflow reveals the tab when needed. All state persists across page visits.

**검색 입력** demonstrates SearchBox Korean editing, clear/focus return, size/appearance/RTL, readonly, rejected writes and state across tabs. Search/dismiss use Fluent font icons; the SVG tab covers the existing separate SVG paths.

**도구 모음** demonstrates horizontal/vertical Toolbar layout, padding sizes, RTL, actual commands and toggles. Tab enters its remembered child, arrows/Home/End navigate, and Tab exits. Options and state persist across pages.

**팝업 입력** compares nonmodal Tab traversal, exclusive input and actual inside/outside commands. It demonstrates the generic popup engine foundation, not a complete Fluent Popover. Close with its button or an outside pointer click before changing modes.

Popover → 팝업 내부 메뉴 exercises owned menu commands and Escape priority. Select Hover with 팝업 열기 방식 to retain the parent over its menu portal and dismiss once after leaving the whole chain.

Popover enters over400ms by default. Toggle 팝업 모션 and exercise input, owned menus and closing while moving. Entry finishes in event wait; a focused editor keeps its ordinary caret deadline.

On 검색 입력, hold X briefly and release to clear. Check Ctrl+Z/Ctrl+Y and immediate Korean retyping too.


`UiPopoverOptions.CloseOnFocusLeave` defaults to `false`. Enable it to dismiss when focus leaves the trigger, content, or an owned nested surface for another control, preserving that destination focus. It also observes departure from the trigger when autofocus is disabled. A rejected binding write keeps the surface open; reentering and leaving the region rearms dismissal. No Timer or periodic frame request is added. In Implementation Observer, choose Popover → **포커스 이탈 시 닫기**. This supplies shared behavior required by the official InfoButton; it does not complete InfoLabel itself.


### Drag-scroll tab headers

Set `UiTabListOptions.Scroll = new UiTabListScrollOptions()` to enable horizontal header scrolling. This is a separate choice from the existing `Overflow` allocation mode; do not enable both. Implementation Observer uses this mode for its top-level pages.

Drag the header or use the wheel. Each ellipsis appears only on an obscured edge, allowing left-only, right-only, or both. Clicking an ellipsis opens that side's hidden-tab context menu without moving the header. Choosing an item selects and reveals the original tab and panel. Selecting or focusing an edge tab also reveals one neighbouring tab in that direction to support continued navigation. Releasing a drag does not activate the tab under the pointer.

The bottom scrollbar overlays the header without reserving layout space. Defaults are120ms entry,400ms stationary hold and160ms exit; a held drag keeps it shown. Configure timing, curves and reduced motion through shared `UiScrollbarMotionOptions`, with per-tab overrides in `UiTabListScrollOptions.Motion`. Settled animation stops scheduling frames. See the [shared transition design and API](../../docs/finite-transitions.md).


All scrollbar families use the same [shared motion module](../../docs/scrollbar-motion.md).


Accordion headers show a pointer cursor; disabled headers show not-allowed. Check Tab focus outlines and Enter/Space activation. Hover backgrounds remain transparent like the official default demo.


InfoLabel: run `./run-gallery.ps1` and open **추가 정보** to exercise label-to-input focus, information icons, size/localized name changes, popover commands and Tab/Escape. Default icons use font glyphs; Image/Icon provide separate SVG support. [InfoLabel API](../../docs/fluent2-info-label.md).

Run ./run-gallery.ps1 → **숫자 입력** for SpinButton typing/commit/cancel, bounds, nullable values, unit parsing, keyboard stepping and held pointer repetition. Size, appearance and read-only toggles are provided. [API](../../docs/fluent2-spinbutton.md).


In **숫자 입력**, type a draft then click within its text: the numeric model below stays unchanged. Enter, another control or empty space commits it; Escape cancels. Stepping a draft emits one final numeric change.


Run ./run-gallery.ps1 → **목록** for plain rows, single/multiple selection, an input, row actions and child buttons. [Prototype API and remaining work](../../docs/fluent2-list.md); not yet a generated Fluent factory.


The **목록** tab centers checkmarks and row content, including names next to inputs/buttons. Selecting a later single-choice row also clears the earlier indicator in the same frame.


Markdown: switch between the smart editor and read-only preview in the Markdown page. Both retain the same source. [API and qualification](../../docs/markdown-widgets.md).

List: **목록** consumes generated Fluent.List plain/single/multiple overloads, including owned input and row/child actions. The catalog maps List to this page; see [List contracts](../../docs/fluent2-list.md).
Markdown images: click **이미지 예제** on **Markdown** to open the cached SVG, memory bitmap and unavailable-image alternative. Switching to smart editing reveals image syntax only in the current block.

Markdown preview links: use Tab/Shift+Tab and Enter, or click the text with its hand cursor. Windows accessibility exposes Hyperlink children with read-only URLs and Invoke/ScrollItem. Dragging selects text without following a link. See the Markdown API document for remaining qualification limits.

Click **중첩 링크 예제** to select **중첩 미리보기**, which places the same viewer inside two ScrollViews. Tab through the first and last links to see both ancestors reveal the target; Shift+Tab returns to the first link. Unchanged focus preserves manual scrolling. The regular preview and smart editor keep the same source.

Transformed popups: on **위젯 배치**, adjust rotation, scale and margins, then use **변환 팝업**, **변환 메뉴** and **변환 도구 설명**. Triggers transform while popup content remains upright in display coordinates. A shared scope separates owner transforms, clipping, pointer, accessibility and IME coordinates and restores them on exit. Deferred tooltips retain screen anchors and caller-owned visibility even with the pointer outside the window. Qualification covers declarative Popover/menu/Tooltip and compatibility BeginPopup/BeginTooltip, modal, Combo and menu paths. Arbitrary 3D-camera combinations and screen-reader conformance remain unqualified.

On 위젯 배치 → 기존 팝업 API, apply shared rotation/scale and exercise open, execute and close. The three legacy Combo overloads share screen-anchor placement. Popup width is the maximum of the original content width and transformed trigger width; transformed owner clipping is replaced by display clipping. Screen popups do not inherit the owner widget's forced vector-text mode.

MessageBar: use **표시할 때 자동 공지** and **차분한 공지로 고정** on the scrollable **메시지 표시** page and inspect the announcement request count. Actual title relationships and one mount/priority announcement are supported; reorder/removal do not repeat it. Requests do not prove spoken completion. MessageBar is 12/13, with full conformance pending. [API and scope](../../docs/fluent2-messagebar.md).

MessageBar → **오른쪽에서 왼쪽 배치** switches real slot/action placement. Common `UiWrapOptions.RightToLeft` mirrors child X positions while retaining navigation and row order. It does not guarantee arbitrary child paragraph bidi. Whole MessageBar remains 12/13.

MessageBar is 12/13. Actual title targets and automatic announcements passed Windows UIA 8 client/1 host and pinned Linux RC3 NativeAOT AT-SPI 9 client/3 host checks. Complete paragraph RTL remains; Windows NativeAOT is a separate gate. The qualified Linux Observer is retained.

MessageBar multiline/shape/direction/announcement options also apply to newly inserted dynamic messages. Passed 34 interaction/page checks and 38 Windows/41 Linux/38 delivered presentation scenarios. MessageBar is 12/13; complete RTL/reflow remains pending.
# Input batch regression

See [input event batches](../../docs/input-event-batches.md) for the executable shrinking-batch check and the separately failing mixed-input ordering probe. These checks preserve the current typography rendering.



Drawer: **알림과 진행 상태 → Drawer** demonstrates overlay/inline panels. At 640 UI units or less, **탐색 메뉴 열기** opens Gallery navigation, including its persistent theme switch and grouped icon menu. Inline reflow animation remains pending.


## Independent examples and equivalent source

Every Gallery route has a distinct persistent page; duplicate page destinations fail startup validation. Twenty-one formerly shared component destinations now have standalone examples. Combined regression pages remain under **종합·회귀 예제**. Each page displays a selectable, read-only C# source pane below its live example, with independent vertical/horizontal scrolling.

Standalone source comes from `CallerArgumentExpression` on the exact executed factory. Existing complex examples display their actual `.cs` file embedded at build time. This requires no repository files at runtime. Complex files use the Gallery project's supporting types and references. Planned components explicitly report that executable source is unavailable.

`UiViewHost` borrows an `IUiView` and hosts sequential rendering inside finite declarative bounds without assigning the entire allocation to each leaf. Child clips, popup tiers and nested windows retain their own geometry. The caller owns the borrowed view. Inline Drawer uses the shared `UiFloatTransition` for allocation and clipping, preserves full-size content, reverses from the sampled extent and retires interaction immediately on close. Initial open inline placement is immediate; later changes animate unless `MotionEnabled` is false. [Verification](../../artifacts/gallery-source-2026-11-17/verification.json).

Gallery snippets omit the common `using Duxel.Core;` directive. Actual project files and the general-purpose `UiCodeView` buffer remain unchanged. Other imports and matching text inside source string literals are preserved. Import `Duxel.Core` at project scope when moving examples into another application.

`GallerySample.BindSource` compares typed snapshots of current settings and updates that code block only after a change. Rating color, shape, direction, selected value and review count follow the actual controls. Per-example source placement currently covers Cards, typography, Rating and Avatar. Other compound pages and live setting connections remain in progress.

Navigation separates Fluent controls, Duxel widgets and integration/development tools. Classification does not depend on Korean or English titles. `Fluent.Nav` implements Fluent 2 Nav design and behavior on the Duxel engine and remains in the Fluent section; distinguish this style from its generic `UiNav<TPresentation>` base. SVG, Markdown, layout and virtualization belong to Duxel, while repaint, measurement and rendering inspection belong to development tools.
