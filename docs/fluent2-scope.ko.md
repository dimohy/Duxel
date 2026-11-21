# Fluent 2 구현 범위 — 사용자 교정, 2026-11-05

Dialog의 모달·비모달·경고 유형은 `UiDialogType`으로 지정합니다. 디자인 가이드는 경고 대화 상자를 하단 버튼으로 닫도록 안내합니다. 해당 React 참조 구현은 경고 유형에도 Escape로 닫기 요청을 보냅니다. Duxel API는 기존 기본 동작을 유지하며, Gallery 경고 예제는 `SuppressEscapeDismissal`을 사용해 가이드대로 하단 버튼으로 닫습니다. [Dialog 디자인 가이드](https://fluent2.microsoft.design/components/web/react/core/dialog/usage) · [React 소스 c78d223](https://github.com/microsoft/fluentui/blob/c78d22353ba03b9eb744f577332aa400daf10934/packages/react-components/react-dialog/library/src/components/DialogSurface/useDialogSurface.ts)

ProgressIndicator 아래에 Android 공식 원형 유형인 `CircularProgressIndicator`를 연결했습니다. 선형과 원형의 갤러리 예제를 함께 볼 수 있습니다. [원형 동작과 검증](fluent2-progress-indicator.ko.md#원형-진행-표시)

2026-11-16: 현재 컴파일된 요구55개 중 Rating을 포함한39개가 연결되고16개가 미연결입니다. 노출 팩터리는 변형을 포함해68개이며 전체 상태/외관/플랫폼 적합성 분모와 다릅니다. [Rating 범위](fluent2-rating.ko.md)를 따르며 원래 전체 목표와 소스 생성기 첫 선언 삭제 캐시 기준0/2는 유지합니다.

2026-11-13 [목록](fluent2-list.ko.md): 생성형 Fluent.List를 일반·단일·다중 선택과 관찰 샘플에 연결했습니다. 팩터리59개·공식 상위35/55 연결·20개 미연결이며 전체 적합성은 미완료입니다.

2026-11-20 공식 목록을 다시 대조했습니다. React 47개, iOS 12개, Android 5개 링크가 저장소와 일치했습니다. [React 목록](https://fluent2.microsoft.design/components/web/react) · [iOS 목록](https://fluent2.microsoft.design/components/ios) · [Android 목록](https://fluent2.microsoft.design/components/android)

Button 가이드에 따라 `CompoundButton`(제목과 보조 설명), `MenuButton`(메뉴 열기), `SplitButton`(기본 동작과 별도 메뉴)을 Button의 하위 변형으로 등록했습니다. 세 변형 모두 갤러리 예제와 입력·접근성 검사에 연결했습니다. 다른 구성요소의 변형과 상태는 원문 검토를 이어갑니다. [공식 Button 가이드](https://fluent2.microsoft.design/components/web/react/core/button/usage)

Drawer 가이드에 나오는 인라인과 오버레이를 기존 Drawer의 하위 유형 `InlineDrawer`와 `OverlayDrawer`로 등록했습니다. 갤러리의 두 예제에서 각 팩터리를 사용합니다. 패널의 세부 구성요소와 반응형 동작은 원문 검토 범위에 남아 있습니다. [공식 Drawer 가이드](https://fluent2.microsoft.design/components/web/react/core/drawer/usage)


2026-11-13 [Popover 열기 방식](fluent2-popover.ko.md): 클릭·호버·우클릭 단일 모드, 500ms 닫기 예약/취소·거부 시 대기, 가상 점 위치와 Menu/Shift+F10을 연결했습니다. 관찰 샘플 → Popover → 팝업 열기 방식.


2026-11-13 [Popover 화살표와 표면 캐시](fluent2-popover.ko.md): WithArrow·6/8px 형상·합성 그림자·정수 이동/배율 캐시·포인터 가림을 연결했습니다. Tooltip의 기존 래스터를 공유하며 전체 적합성은 미완료입니다.


2026-11-13 [Popover](fluent2-popover.ko.md): 임의 콘텐츠 소유·제어된 열기/거부·비모달/포커스 제한·Escape 복귀·자연 높이/스크롤·중첩·숨김/해제를 연결했습니다. 관찰 샘플 → Popover. 팩터리 56개·상위 32/55 연결·23개 미연결이며 전체 적합성은 미완료입니다.


2026-11-13 비모달 팝업 입력 기반을 추가했습니다. 당시에는 팩터리 55개·상위 31/55 연결·24개 미연결이었으며, 위의 Popover 연결로 갱신되었습니다. [기반](popup-input-modes.ko.md).

2026-11-13 Toolbar 연결 포함: 55개 팩터리·상위31/55 연결·24개 미연결입니다. 이는 전체 적합성 완료가 아닙니다. [API](fluent2-toolbar.ko.md).

2026-11-13 InteractionTag 연결: 노출52개·상위28/55 연결·27개 미연결입니다. Tag의 하위 변형이며 상위 분모는 유지합니다. 전체 적합성은 미완료입니다. [API](fluent2-interaction-tag.ko.md).

2026-11-13 Tag 연결: 노출51개·상위28/55 연결·27개 미연결입니다. TagGroup은 Tag 하위 구성으로 추적하며 독립 상위 분모에 더하지 않습니다. 전체 적합성은 미완료입니다. [API](fluent2-tag.ko.md).

2026-11-13 Combobox 동작 검증: 닫기·포커스 이탈 복원, Open 바인딩 수락/거부·외부 모델 동기화, 실제 OpenChanged 알림과 지역화한 결과 없음 정적 텍스트를 추가했습니다. 기준 CPU59개/8940회·GPU6개/804회·외부 UIA3개/47회와 최신 영향 범위 Combobox67회·Observer246회·실제 앱614프레임/13검사를 확인했습니다. [API](fluent2-combobox.ko.md), `artifacts/combobox-behavior-2026-11-13/verification.json`. 원래 전체 목표와 선택 태그·실제 IME·전체 적합성은 남아 있습니다.

2026-11-13 Combobox: 편집·부분 일치 검색·단일/다중 선택·자유 입력을 실제 입력/IME·팝업 경로로 연결했습니다. 카탈로그50개 노출·상위27/55 연결·28개 미연결입니다. CPU59개/8908회, GPU5개/692회, 외부 UIA3개/40회, 실제 앱3세션(각614프레임)과 RGB579장 일치가 통과했습니다. 연결 수는 전체 적합성 완료가 아닙니다. [API와 한계](fluent2-combobox.ko.md), `artifacts/combobox-2026-11-13/verification.json`.

추가 검증: 컨텍스트 전환 시 팝업 게시 기록이 남아 발생하던 종료 예외를 수정했습니다. 최신 CPU58개/8869회, 영향 범위 GPU3개/296회·네이티브3개/39회, 실제 앱 두 세션이 통과했습니다. 이전 광범위 GPU9개/1646회·네이티브7개/109회는 별도 원장에 보존하며 이번 바이너리의 전체 재실행으로 주장하지 않습니다.

2026-11-13 Dropdown: 단일·다중 선택, 닫힌 논리 항목과 실제 Windows Selection·Value, 공통 팝업·스크롤·이름 검색을 검증했습니다. 팩터리49개·상위26/55 연결·29개 미연결입니다. CPU58개/8844회, GPU9개/1646회, 네이티브7개/109회, 실제 렌더링 세션2개(각614프레임·4검사)가 통과했습니다. [API와 남은 범위](fluent2-dropdown.ko.md), `artifacts/dropdown-2026-11-13/verification.json`. 편집 가능한 Combobox·전체 적합성·고정 RC3 NativeAOT·성능은 미완료입니다.

2026-11-13 AvatarGroup: 펼침·겹침·원 분할, 정확한 초과 수·구성원 메뉴, RTL과 이미지·한글·SVG를 기존 아바타·클립·메뉴로 제공합니다. `./run-gallery.ps1` → **아바타 그룹**. [API와 한계](fluent2-avatar-group.ko.md). 팩터리48개·상위25/55 연결·30개 미연결이며 전체 적합성은 미완료입니다.

2026-11-12 경로 위젯 검증: Breadcrumb를 추가해 팩터리47개, 공식 상위 그룹24/55개 연결·31개 미연결입니다. 연결 여부는 공식 전체 동작·외관·접근성 적합성과 별개입니다. [경로 API와 남은 범위](fluent2-breadcrumb.ko.md), `artifacts/breadcrumb-2026-11-12/verification.json`. 기본 아이콘은 Fluent 폰트이며 선택적인 SVG는 기존 캐시 경로를 재사용합니다.

2026-11-11 Menu 최신 연결: 노출36=상위17+변형19, 미연결38/55. 근거 artifacts/menu-composition-2026-11-11/catalog-counts.json. 아래 이전 기준과 URL64 일치 기록은 역사적 근거이며 현재 전체 플랫폼 재검증을 뜻하지 않습니다. [Fluent Menu](fluent2-menu.ko.md)의 그림자·전체 상태/접근성·탭 연결은 남아 있습니다.

2026-11-11 이전 기준: 노출 이름은35개(상위16·부모 연결 변형19)이며 상위55개 중 미연결은39개다. 공식 플랫폼 URL64개(Web React47·iOS12·Android5)와 차이 없이 일치한다. 근거는 `artifacts/fluent-tooltip-2026-11-11/catalog-drift.json`이다. [Tooltip](fluent2-tooltip.ko.md)은 기존 대상 래퍼에 연결한 생성 팩터리를 제공한다. 전체 상태·외형·접근성 준수의 분모는 미확정이다.

구현 범위의 권위는 https://fluent2.microsoft.design/ 의 공개 컴포넌트 카탈로그와 각 컴포넌트의 디자인·상태·동작 지침이다. 사이트에 게시된 플랫폼별 차이는 대응표로 관리한다.

공식 저장소와 Storybook은 해당 컴포넌트의 구현·검증 참고 자료다. 모든 공개 타입, 보조 타입, 과거 디자인 세대, 플랫폼 전용 API를 독립 구현 요구로 승격하지 않는다. 하위 구성은 공식 컴포넌트 계약에 필요한 범위로 추적한다.

이전 325개 이름은 저장소 조사 결과이며 목표의 컴포넌트 분모가 아니다. 299개 미연결 이름도 구현해야 할 위젯 수가 아니다. 기존 자료와 구현은 보존한다. 생성 요구 목록과 관련 검사는 아래와 같이 공식 사이트 기준으로 재분류했다.

공통 상속, 동작/표현 분리, 소스 생성기의 정적 연결, 비표준 기본 컴포넌트 비노출과 공통 엔진 최적화 목표는 유지한다.

## 실제 생성 경로와 검증

공식 사이트의 Web React47·iOS12·Android5개 항목을 기존 독립 링크 스냅샷과 대조했다. 플랫폼 중복 제거 후 상위 이름55개이며, 현재 지원하는19개 변형은 부모 컴포넌트 문서에 연결한다. Text 프리셋과 CounterBadge·ToggleButton을 독립 상위 컴포넌트 수에 더하지 않는다. Windows 페이지의 WinUI 안내는 Windows 표현 참고 기준으로 유지한다. 저장소의 전체 API 목록으로 분모를 늘리지 않는다.

생산 FluentComponents.tsv를 이 기준으로 교체했다. 원래 전체 조사는 artifacts/fluent-site-scope-2026-11-05/FluentComponents.before.tsv에 보존한다. 생성기는 component/pattern/variant의 근거가 공식 사이트 컴포넌트 문서가 아니면 DUXWGT006을 낸다. variant는 같은 목록에 있는 부모 문서와 연결되어야 한다. 상위 필수 목록과 팩터리 허용 이름을 구분하며, 엄격 완료 검사에서는 미구현 변형도 DUXWGT007로 거부한다.

.NET10 생성기83개·위젯 트리72개 통과. 관리 .NET10.0.9와 고정 RC3 Native .NET11.0.0-rc.3.26522.148의 목록이 정확히 일치한다. 팩터리26개 중 상위 컴포넌트 이름7개와 변형19개이며, 상위 이름48개는 미연결이다. 이는 기존 레거시 구현 부재 또는 연결된7개의 공식 룩앤필 완료를 뜻하지 않는다. 전체 변형·상태·시각·입력·접근성 검증은 미완료로 유지한다. 근거: artifacts/fluent-site-scope-2026-11-05/verification.json.

2026-11-06 RadioGroup 연결 후 생성 팩터리 이름28개=상위9개+기존 변형19개, 상위 미연결46개다. 공식 상위 분모55개는 유지한다. 이는 연결 수이며 공식 외형·입력·접근성 지원 완료 수가 아니다. 최신 근거는 artifacts/fluent-radio-2026-11-06/verification.json이다.

2026-11-06 Link 연결 후 팩터리29개=상위10개+변형19개이며 상위55개 중45개가 미연결이다. 전체 변형·상태·외형·접근성 완료 분모와 구분한다. 근거 artifacts/fluent-link-2026-11-06/verification.json.

2026-11-06 Input 연결 후 카탈로그30개=상위11개+변형19개, 공식 상위55개 중44개 미연결이다. 공식 상태·외형·접근성 전체 완료 수가 아니다. 근거 artifacts/fluent-input-2026-11-06/catalog.json 및 verification.json.
2026-11-06 Field 연결 후 생성 카탈로그는31개 이름=상위12개+변형19개이며 상위43/55개가 미연결이다. 기본 Field가 실제 입력을 소유하며 생성된 크기/방향 표현을 소비한다. UIA·전체 변형/공식 화면 동치는 미완료다. 근거: artifacts/fluent-field-2026-11-06/catalog/result.json·component-mapping.json·verification.json.

# 최신 Spinner 연결 상태 (2026-11-06)

[Spinner Preview](fluent2-spinner.ko.md)를13번째 상위 이름으로 연결했다. 생성 카탈로그는32개=공식 상위13/55+변형19이며 미연결 상위 이름은42개다. 카탈로그 연결 수이며 컴포넌트·상태·시각적 동치 전체 완료 수는 아니다. `artifacts/fluent-spinner-2026-11-06/component-mapping.json`에 대응을 기록했고 전체 변형·상태·룩앤필 분모는 미확정이다.


이전 Tooltip 팩터리 단계 근거: `artifacts/fluent-tooltip-factory-2026-11-11/final-catalog-only/result.json`; 팩터리 검증: `artifacts/fluent-tooltip-factory-2026-11-11/verification.json`.

현재 TabList 카탈로그: `artifacts/fluent-tablist-2026-11-11/final-catalog-only/result.json`. [TabList 기본 구현](fluent2-tablist.ko.md)을 참조한다.

2026-11-13 [Popover 진입 모션](fluent2-popover.ko.md): 기본400ms 페이드·방향별10px 이동·즉시 닫기·같은 시점 캐시·실제 입력/IME/접근성 좌표·유한 예약을 연결했습니다. 관찰 샘플 → Popover → 팝업 모션. 전체 적합성은 미완료입니다.

2026-11-13 [Popover 그리기 범위](fluent2-popover.ko.md): 사용자 정의 PaintBounds·뷰포트 기본값·범위/예산 검사·넓은 장식 샘플을 연결했습니다. 기본 Fluent 픽셀은 유지합니다. 네이티브 Popover 검사 실패는 별도 진단 중이며 전체 적합성 완료가 아닙니다.
