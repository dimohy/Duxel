# Popover pointer triggers and event-wait deadlines — 2026-11-13

가설/선택: 기존 프레임 시계·RequestFrameAt·팝업 소유권과 실제 Windows 입력을 재사용하면 별도 타이머/스레드나 주기 렌더 없이 호버·우클릭을 지원할 수 있습니다. public TriggerMode enum으로 플래그 충돌을 피하고 지연 단위는 초로 명시합니다.

변경: Hover 즉시 열기, 기본500ms 이탈 닫기, 표면/하위 표면 재진입 취소, 요청 거부 후 재예약 방지와 수명 정리. 종료 시점에 살아 있는 소유자의 예약만 모아 같은 프레임 후속 닫기도 취소합니다. Context는 실제 우클릭 점·Menu/Shift+F10·외부 스크롤 닫기이며 공유 배치에0크기 점 대상을 추가했습니다. 기존 UiKey 값을 유지해 ContextMenu를 덧붙이고 VK_APPS를 기존 Windows 변환에 연결했습니다. 공식 소스를 대조하여 화살표 여백8 및 CoverTarget 화살표 생략도 반영했습니다. 원래 캐시·자식 편집·포커스 수명 경로를 재사용합니다.

검증 근거: artifacts/popover-triggers-2026-11-13/verification.json. CPU는 절대 시각이 이탈 중 연장되지 않음, 재진입·모드 변경·숨김/해제 취소, 거부 후 대기 복귀, 중첩 Escape와 정확한 점 배치를 확인합니다. GPU는 실제 옵션/포인터/위치/화살표 픽셀을 확인합니다. 네이티브는 실제 입력 스냅샷과 실시간 delta를 사용해 마우스 진입·이탈과 Menu 키를 확인합니다. 입력 검사용 네이티브 수동 루프를 앱 유휴 CPU 측정으로 승격하지 않습니다.

Before/After 시간·할당·전체 메모리·개선율은 미측정입니다. 예약 없음·픽셀 일치·요청 슬롯2/3은 전체 앱 성능·전력·실제 동시성 증거가 아닙니다. 포커스된 편집기의 기존 커서 예약은 유지합니다. 모션·전체 Tabster/강제 색상·물리 리더·원래 고정 RC3/실기기 관문은 남아 있습니다.

# Popover combined arrow raster and cache — 2026-11-13

가설/선택: Tooltip에 검증된 본문·회전 화살표·순차 Gaussian 합성 코드를 재사용하고, 팝업 소유자별로 공통 래스터 캐시를 빌리는 것이 중복 파서·도형 엔진보다 단순합니다. 모양·색·배율·픽셀 위상만 키로 유지합니다.

변경: WithArrow 기본false, Small6/Medium·Large8, 공식1.414배 사각형·둥근 끝·투명 테두리 형상을 공유합니다. Popover는 전체 소스에 sigma2/16·offset8의 순차 drop-shadow를 적용하며 기존 Tooltip의 sigma2/8·offset4와 회색 팔레트는 유지합니다. 새 UiPopupSurface는 일반 RGBA 캐시의 소유자별 lease를 유지하고 크기·색·위상 변경 때만 다시 준비합니다. 동일 모양·정수 픽셀 이동은 업로드0회, 부분 픽셀 위상과 배율 변경은 각각 새 준비1회 후 재사용을 회귀로 확인합니다. 화살표 자체 닫기 판정과 별도 포인터 가림도 연결합니다.

검증 근거: artifacts/popover-arrow-2026-11-13/verification.json. 첫 GPU 반복은 프레임 이름에만 True를 넣고 WithArrow를 적용하지 않아 실제 화면 검토에서 누락을 발견했습니다. 해당 출력은 prebinding-gpu-note.json에 비화살표 이력으로 분류하고, 수정한 실제 옵션·본문 밖 화살표 픽셀 검사만 최종 근거로 사용합니다. 그림자·AA·색·6/8px 네 방향·정수/부분 픽셀/배율·다중 소유자·초과 크기 사전 실패·해제, 실제 관찰 탭·Tooltip CSS 기준과 네이티브 입력을 구분해 원장에 기록합니다.

Before/After 시간·할당·전체 메모리·개선율은 미측정입니다. 캐시 업로드 횟수와 픽셀 일치는 처리량·동시 프레임 수·앱 전체 속도 개선 증거가 아닙니다. 캐시의 예산 내 미사용 상주는 소유자 해제 후 즉시 GPU 파괴를 뜻하지 않습니다. 호버/컨텍스트·모션·전체 Tabster·강제 색상·실기기·고정 RC3 관문은 남아 있습니다.

# Popover owned surface — 2026-11-13

가설: 기존 팝업 입력 층·측정·ScrollView·일반 편집/IME·Shadow16을 재사용해 임의 콘텐츠와 소유권·포커스 수명을 연결할 수 있습니다.

변경: 독립 Popover 위젯과 Fluent 표현, 제어된 열기/거부, 중첩 소유권·닫기·숨김·컨텍스트 이전/해제와 포커스 복귀를 연결했습니다. 표면은 자식 자연 높이로 측정한 뒤 제한 높이만 스크롤합니다. 화면 전체를 채우던 초기 뷰포트 측정은 자연 높이 회귀로 교정했습니다. 팝업당 재사용 입력 멤버 버퍼·식별자·그림자 캐시를 사용하며 XML·셰이더·네이티브 import·주기 타이머를 추가하지 않습니다.

검증 원장: artifacts/popover-2026-11-13/verification.json. 관리형 CPU·GPU·외부 UIA 범위를 각각 기록합니다. 시간/할당/메모리 Before/After·개선율은 미측정이며 구조적 재사용을 수치 성능 향상으로 주장하지 않습니다. 요청 슬롯2/3은 실제 동시성·처리량 측정이 아닙니다. 화살표·호버/컨텍스트·모션·전체 Tabster·강제 색상·물리 스크린리더와 원래 고정 RC3/실기기 관문은 남아 있습니다.

# AvatarGroup and compound identity qualification — 2026-11-13

가설: 불변 구성원 분할·크기·레이블은 측정 결과로 유지하고, 실제 입력·그리기에는 기존 Avatar·Menu·analytic 클립을 재사용할 수 있습니다. 새 그룹 전용 셰이더나 SVG XML 처리 경로를 만들지 않습니다.

변경: 펼침·겹침·원 분할, 크기별 간격/구분선, 초과 수·구성원 목록·스크롤·RTL을 추가했습니다. 팝업 아바타는 그룹 크기를 유지합니다. 복합 위젯의 AccessibleName 범위를 루트로 제한하고 공통 UiOwnedMenuState를 Breadcrumb과 함께 사용합니다. 유지되는 레이아웃에 분할·표시 이름·ID 문자열을 보관합니다. 내부 아이콘 설명자에 크기 힌트가 추가되었으며 메모리 변화는 미측정입니다.

추가 재현: Parent 식별자 모드 그룹 두 개를 직접 그리면 중복 ##avatar-overflow 예외가 발생했습니다. 루트에 묶인 캐시 ID로 수정하고 Breadcrumb의 반복 Home 값·초과 메뉴에도 적용했습니다. identity-repro.stderr.log에 실패를 보존했고 현재 두 격리 검사는 통과합니다.

검증: 첫 전체 CPU57개 모음/8820회, GPU16개 프로필/3059회, 외부 UIA8개 프로필/149회 통과. 식별자 수정 후 최신 CPU57개/8822회와 영향을 받는 GPU6개/431회·UIA2개/26회를 재검증했습니다. 영향을 받지 않는10개 GPU·6개 UIA 프로필은 첫 전체 근거를 재사용하며 최신 수치와 합산하지 않습니다. 실제 앱의 렌더링 탭 유지·재진입 세션은 각각614프레임/4회 검사·종료 코드0입니다. 실제 Khronos 인스턴스·장치 계층에서 검증 오류0개입니다. 요청 슬롯2/3은 실제 동시 프레임 수를 측정했다는 뜻이 아닙니다.

픽셀: 이전 Breadcrumb 단계2630장 중2608장 RGB 일치,22장은 새 탐색 탭/HUD가 있는 상단128px 안의 차이입니다. 식별자 수정 전후528장은398장 RGB 일치,130장은 상단32px HUD만 다릅니다. 변경한 갤러리는 첫 비교에서 제외했고 자체 리플레이·최신 비교로 확인했습니다.835개 구현 입력의 스냅샷 이후 내용 변경은0개입니다.

Before/After 시간·할당·메모리와 개선율은 미측정입니다. 구조적 재사용을 정량 성능 개선으로 합산하지 않습니다. 모든 SVG·Fluent 상태, 강제 색상·동적 멤버 교체, 고정 RC3 NativeAOT·런타임 이미지·원래 같은 부하3회 A/B 및 기기별 관문은 남아 있습니다. MSVC/Windows SDK 경로 탐색·설치·재시도는 하지 않았고 리사이즈는 보류했습니다.

근거: `artifacts/avatar-group-implementation-2026-11-12/verification.json`, `implementation-source-manifest.json`. 확인 명령: `./run-gallery.ps1` → **아바타 그룹**. 사용자 실행 캐시와 별도 샘플 빌드를 갱신했고 일반 관찰 창은 실행하지 않았습니다.


## Tag media derived initials and ownership (managed qualification)

- Hypothesis: deriving media initials for unchanged names during every requested paint repeats regex/string work. Sharing one inactive paint definition between different media sizes also violates the existing per-owner/per-frame contract.
- Change: reuse the Avatar's existing derived initials where its rules match; retain at most four first-only/direction variants and invalidate them on the same derived-content updates. Each Tag binds its own inactive composite and shadow resources while borrowing the source avatar/image. Image/SVG decoding remains outside paint.
- Choice: reuse existing Avatar presentation, UiAvatarInitials, PaintGroup, BoxShadow and image pipeline. No new renderer, shader, parser, timer, pooling or fallback. Per-tag bindings are necessary to keep independently sized simultaneous paint definitions and disposal under separate owners.
- Validation: managed Graphics Release `-warnaserror`; `./scripts/Test-WidgetContracts.ps1` Tag/Avatar/Observer contracts; interaction-tag GPU profiles with actual validation layers, MSAA1/4 and Light/Dark. The focused string identity check proves derived-string reuse; GPU checks cover three simultaneously sized inactive/shadow media, owner content changes and releasing media resources. Evidence: `artifacts/tag-media-2026-11-13/`.
- Before/After elapsed time and allocation bytes: unmeasured. Improvement percentage: null, no numerical performance claim. Original fixedRC3 NativeAOT/runtime-image/performance gates remain open.
- SDK/runtime: managed SDK11.0.100/runtime11.0.0, separate from the original fixed RC3 qualification. Performance warm-up, three independent timing runs, average FPS, median/p95/p99 frame time and 1% low FPS: not run/not measured for this structural addition.
- Risks: four cached variants live with the source avatar and must invalidate on name/initials/direction changes. Borrowed source owners must outlive their consuming tags. Per-tag composite/shadow bindings are released on Tag disposal. Full official Tag visual/forced-color/touch conformance and physical device checks remain open.


## Persona shared layout and avatar visuals

- Hypothesis/choice: person information should reuse existing avatar/presence/text and measured-layout contracts instead of introducing a renderer or a second inactive/shadow owner model. A keyed owned Avatar must propagate external layout changes through retained ancestors.
- Change: extract consumer-owned `UiAvatarPaintBinding<T>` from the existing Tag adapter; Persona owns its Avatar through `OwnChild`. `UiRetainedLayout` keys include content/options/resolved avatar options. Prepared layouts are reused only while their content key still matches, so a preceding control can update painted text in the same frame without changing the parent's already assigned box. Existing cached presence/initial strings are reused.
- Verification: Persona CPU checks cover retained measurement, owned-avatar ancestor invalidation, real queued button updates in the same frame, explicit presence sizes and passive semantics. Actual GPU checks cover six sizes/positions/presence modes in Light/Dark and MSAA1/4, source changes, inactive/shadow lifetimes and two/three frame resources; native Group events/lifetime are checked externally. Evidence: `artifacts/persona-2026-11-13/` with large captures explicitly on `L:/Cache/Duxel/persona-2026-11-13-evidence/`.
- Before/After elapsed time, allocation bytes, improvement percentage, average FPS and median/p95/p99/1%low: unmeasured; no numerical performance claim. Performance warm-up/three independent timing runs were not performed. Managed SDK11.0.100/runtime11.0.0 qualification is separate from original fixedRC3 NativeAOT/runtime-image/performance/device gates.
- Risk/limits: custom presentations must honor slot/measurement contracts; automatic ellipsis/wrapping, forced colors, physical screen-reader fidelity and full official conformance remain open. Persona owns its Avatar; Tag media only borrows its sources. Completed proof is retained and storage capacity is checked before large runs; previously rejected deletion is not retried.

## SearchBox shared editor and paint frame

Hypothesis: another independent text editor would duplicate IME/history/selection behavior and add maintenance cost. Change: SearchBox reuses EditNavigableInput, the existing font icons/palette and a shared input-frame dispatcher; Combobox and SearchBox share edit reconciliation. Verify through focused/full CPU, native Value events and actual GPU pixels recorded in artifacts/searchbox-2026-11-13. No XML parse, shader, native import or periodic animation is added. Before/After timings, improvement percentage, warmup/independent performance runs and median/p95/p99/1%low remain unmeasured; no numerical speed claim. Managed SDK11.0.100/runtime11.0.0; fixed RC3 NativeAOT comparison remains open. Risk: arbitrary slots/submission callback, full Unicode bidi/forced colors/physical screen reader conformance remain outside this initial surface.

## Toolbar focus and arrangement reuse

Hypothesis: Toolbar can reuse owned child arrangement and existing control activation without introducing another button or popup implementation. Change: one bounded reusable membership/key buffer, remembered tab entry, shared FocusGroupControl and an arrow-ownership bit on existing tab stops; standard range/edit controls retain their keys. Existing retained arrangement handles options/ancestor invalidation. Verify: full CPU63/9658, actual GPU Toolbar2/3 and Observer, external native Toolbar/Radio/Combobox in artifacts/toolbar-2026-11-13. No timer/renderer/shader/parser/native import added. Before/After timings, improvement percentage, warmup/independent performance runs and median/p95/p99/1%low remain unmeasured; no speedup claim. Managed SDK11.0.100/runtime11.0.0; fixedRC3 NativeAOT remains open. Risks: mixed-editor boundary navigation, nested Toolbar, orientation property events and full CSS/Tabster conformance are not qualified.

## Nonmodal popup navigation foundation

Hypothesis: reusing popup paint tiers as a global keyboard trap prevents ordinary Popover behavior. Change: retain existing rectangle API and add parallel keyboard-isolation flags; filter final stops by current exclusive minimum layer, preserve higher nonmodal stops, and constrain Card focus-group Enter/Escape/Tab logic to its own layer. Reuse all existing painters/control/input paths. Verify through64 CPU suites, real GPU z-order/replay and native Windows Tab/background actions recorded in artifacts/popup-input-2026-11-13. Two reusable owner/isolation metadata lists track the same registrations as existing rectangle lists. Closing a scope removes its current registration, releases only its prior keyboard isolation and requests a follow-up frame; previous pointer geometry remains valid for the current input. Before/After timings, improvement percentage, warmup/independent performance runs and median/p95/p99/1%low remain unmeasured; no numerical performance claim. Managed SDK11.0.100/runtime11.0.0 only. Risks: previous-frame input model and default exclusive legacy behavior are retained; full owned Fluent Popover, hidden-owner lifecycle and fixedRC3 NativeAOT/performance gates remain open.
