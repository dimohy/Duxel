# Fluent 2 전체 컴포넌트 전환

2026-11-18 [FluentProvider 배치 방향](fluent2-provider.ko.md#영역별-배치-방향)을 추가했습니다. 자식의 측정과 탐색 컨트롤, 팝업이 영역의 방향을 따릅니다. Windows 갤러리와 관련 회귀 검사를 통과했습니다. 전체 컨트롤의 방향 상속과 브랜드·타이포그래피 검증은 이어서 진행합니다.

2026-11-18 [NavigationBar](fluent2-navigation-bar.ko.md)를 추가했습니다. 현재 화면의 제목과 양쪽 동작, 보조 컨트롤을 배치합니다. 현재 카탈로그에서 추적하는 상위 구성요소 55개를 모두 팩터리에 연결했습니다. 전체 디자인과 동작, 플랫폼별 검증은 남아 있습니다.

2026-11-18 [Carousel](fluent2-carousel.ko.md)을 추가했습니다. 단계나 이미지 미리보기로 슬라이드를 고르고 자동 재생을 켤 수 있습니다.

2026-11-18 [CardNudge](fluent2-card.ko.md#cardnudge)를 추가했습니다. 갤러리에서 안내 카드 닫기, 좌우 밀기, 동작 버튼을 시험할 수 있습니다.

2026-11-17 [HeadsUpDisplay](fluent2-heads-up-display.ko.md)의 중앙 포털·차단/비차단·자동 닫기·전환과 갤러리를 연결했습니다. 상위47/55 연결·8개 미연결이며 전체 적합성은 미완료입니다.

2026-11-16 Rating 대화형 경로를 연결했습니다. 생성 팩터리68개·공식 상위55개 중39개 연결·16개 미연결이며 전체 적합성은 미완료입니다. 입력14개·독립 기하 GPU32쌍·Observer356개와 정상45화면이 통과했습니다. 표시 전용/compact와 브라우저 시각 대조는 남았습니다. [Rating 안내](fluent2-rating.ko.md), `artifacts/fluent-rating-2026-11-16/verification.json`.

2026-11-15 [Skeleton](fluent2-skeleton.ko.md): 로딩 범위·Wave/Pulse·상속·정적 모션·실제 native busy와 옵저버 스켈레톤 탭을 추가했습니다. 팩터리62개, 상위37/55 연결·18개 미연결이며 연결 수는 전체 적합성 완료 수가 아닙니다.

2026-11-13 [목록](fluent2-list.ko.md): 생성 팩터리59개·상위35/55 연결·20개 미연결입니다. 수명·편집 키·외부 UIA Grid/선택/편집/비활성/숨김을 검증했습니다. 여러 DPI·물리 스크린리더와 전체 Fluent 적합성은 남아 있습니다.


2026-11-13 [Popover 열기 방식](fluent2-popover.ko.md): 클릭·호버·우클릭 단일 모드, 500ms 닫기 예약/취소·거부 시 대기, 가상 점 위치와 Menu/Shift+F10을 연결했습니다. 관찰 샘플 → Popover → 팝업 열기 방식.


2026-11-13 [Popover 화살표와 표면 캐시](fluent2-popover.ko.md): WithArrow·6/8px 형상·합성 그림자·정수 이동/배율 캐시·포인터 가림을 연결했습니다. Tooltip의 기존 래스터를 공유하며 전체 적합성은 미완료입니다.


2026-11-13 [Popover](fluent2-popover.ko.md): 임의 콘텐츠 소유·제어된 열기/거부·비모달/포커스 제한·Escape 복귀·자연 높이/스크롤·중첩·숨김/해제를 연결했습니다. 관찰 샘플 → Popover. 팩터리 56개·상위 32/55 연결·23개 미연결이며 전체 적합성은 미완료입니다.


2026-11-13 비모달 팝업 입력 기반을 추가했습니다. 당시에는 팩터리 55개·상위 31/55 연결·24개 미연결이었으며, 위의 Popover 연결로 갱신되었습니다. [기반](popup-input-modes.ko.md).

2026-11-13 Toolbar 연결 포함: 55개 팩터리·상위31/55 연결·24개 미연결입니다. 이는 전체 적합성 완료가 아닙니다. [API](fluent2-toolbar.ko.md).

2026-11-13 InteractionTag: 독립 본문·해제와 그룹 연동을 추가했습니다. 노출52개·상위28/55 연결이며 전체 상태·외형·접근성 적합성 완료 수는 아닙니다. [API](fluent2-interaction-tag.ko.md).

2026-11-13 Tag 연결: 팩터리51개·상위28/55 연결·27개 미연결입니다. 기본 Tag와 공유 선택 해제 그룹을 검증하며 InteractionTag·전체 적합성은 남아 있습니다. [API](fluent2-tag.ko.md).

2026-11-13 Combobox 동작 검증: 닫기·포커스 이탈 복원, Open 바인딩 수락/거부·외부 모델 동기화, 실제 OpenChanged 알림과 지역화한 결과 없음 정적 텍스트를 추가했습니다. 기준 CPU59개/8940회·GPU6개/804회·외부 UIA3개/47회와 최신 영향 범위 Combobox67회·Observer246회·실제 앱614프레임/13검사를 확인했습니다. [API](fluent2-combobox.ko.md), `artifacts/combobox-behavior-2026-11-13/verification.json`. 원래 전체 목표와 선택 태그·실제 IME·전체 적합성은 남아 있습니다.

2026-11-13 Combobox: 편집·부분 일치 검색·단일/다중 선택·자유 입력을 실제 입력/IME·팝업 경로로 연결했습니다. 카탈로그50개 노출·상위27/55 연결·28개 미연결입니다. CPU59개/8908회, GPU5개/692회, 외부 UIA3개/40회, 실제 앱3세션(각614프레임)과 RGB579장 일치가 통과했습니다. 연결 수는 전체 적합성 완료가 아닙니다. [API와 한계](fluent2-combobox.ko.md), `artifacts/combobox-2026-11-13/verification.json`.

추가 검증: 컨텍스트 전환 시 팝업 게시 기록이 남아 발생하던 종료 예외를 수정했습니다. 최신 CPU58개/8869회, 영향 범위 GPU3개/296회·네이티브3개/39회, 실제 앱 두 세션이 통과했습니다. 이전 광범위 GPU9개/1646회·네이티브7개/109회는 별도 원장에 보존하며 이번 바이너리의 전체 재실행으로 주장하지 않습니다.

2026-11-13 Dropdown: 단일·다중 선택, 닫힌 논리 항목과 실제 Windows Selection·Value, 공통 팝업·스크롤·이름 검색을 검증했습니다. 팩터리49개·상위26/55 연결·29개 미연결입니다. CPU58개/8844회, GPU9개/1646회, 네이티브7개/109회, 실제 렌더링 세션2개(각614프레임·4검사)가 통과했습니다. [API와 남은 범위](fluent2-dropdown.ko.md), `artifacts/dropdown-2026-11-13/verification.json`. 편집 가능한 Combobox·전체 적합성·고정 RC3 NativeAOT·성능은 미완료입니다.

2026-11-13 AvatarGroup: 펼침·겹침·원 분할, 정확한 초과 수·구성원 메뉴, RTL과 이미지·한글·SVG를 기존 아바타·클립·메뉴로 제공합니다. `./run-gallery.ps1` → **아바타 그룹**. [API와 한계](fluent2-avatar-group.ko.md). 팩터리48개·상위25/55 연결·30개 미연결이며 전체 적합성은 미완료입니다.

2026-11-12 경로 위젯 검증: Breadcrumb를 추가해 팩터리47개, 공식 상위 그룹24/55개 연결·31개 미연결입니다. 연결 여부는 공식 전체 동작·외관·접근성 적합성과 별개입니다. [경로 API와 남은 범위](fluent2-breadcrumb.ko.md), `artifacts/breadcrumb-2026-11-12/verification.json`. 기본 아이콘은 Fluent 폰트이며 선택적인 SVG는 기존 캐시 경로를 재사용합니다.

2026-11-12 현재 Card 단계: 팩터리43개 노출·상위55개 중23개 연결·32개 미연결이며 ScopeComplete=false입니다. 기본 정적 Card 컨테이너가 실제 콘텐츠를 소유하고 세 크기·네 외관을 관찰 샘플에서 제공합니다. 선택·포커스 모드·전용 슬롯은 구현할 범위로 유지합니다. 이는 팩터리 연결과 기본 검증이며 Card 전체 적합성 완료가 아닙니다. CPU55개/8309회·GPU1557회·네이티브 Card9+Avatar23회와 이전 이미지883장 일치입니다. [Card](fluent2-card.ko.md), `artifacts/fluent-card-2026-11-12/verification.json`; 아래 과거 관측은 당시 수치를 보존합니다.

2026-11-12 현재: Avatar 활동 링·그림자·상태 배지 투명 마스크·비활성 그룹 합성을 문서의 명시 범위에서 검증했습니다. CPU54개/8283회·GPU1374회·네이티브 Avatar23회가 통과하고 기존 장면635장이 일치합니다. 새 카탈로그 조회도 팩터리42개 노출·상위55개 중22개 연결·33개 미연결이며 연결 수가 전체 적합성 완료를 뜻하지 않습니다. ScopeComplete=false입니다. 모션·강제 색상·전체 공식 적합성과 원래 배포·성능 관문은 남아 있습니다. 근거: `artifacts/inactive-avatar-2026-11-12/verification.json`과 `catalog-current/result.json`; [Avatar](fluent2-avatar.ko.md).

2026-11-12 현재: 독립 PresenceBadge를 포함한 팩터리42개,필수 최상위55개,미연결33개입니다. 상태 배지는 Badge의 검토된 변형이며 최상위 분모를 늘리지 않습니다. 전체 외관·행동 적합성은 미완료입니다. [상태 배지](fluent2-presence-badge.ko.md). 아래 수치는 각 시점의 이력입니다.

2026-11-12 Avatar 기본 기능을 생성 팩터리와 관찰 샘플 아바타 탭에 노출했습니다. 카탈로그는41개 이름 노출·상위55개 요구·33개 미노출입니다. 노출은 전체 기능 완료가 아니며 활동·그림자·상태·동등성 검증은 남아 있습니다. [Avatar 안내](fluent2-avatar.ko.md)를 참고하세요. 앞의 과거 집계와 구분합니다.

Last synced: 2026-11-12

현재 컴파일된 카탈로그는 [Accordion 접기·펼치기](fluent2-accordion.ko.md)를 포함한40개 이름(상위21·부모 연결 변형19)을 노출합니다. [공식 사이트 범위](fluent2-scope.ko.md)의 상위55개 중 미연결은34개입니다. 근거는 `artifacts/fluent-accordion-2026-11-12/catalog-surface/result.json`이며 ScopeComplete=false입니다. 전체 변형·상태·외형·접근성 적합성의 분모는 여전히 미확정입니다. 이전39/35·35/39·325/299는 당시 기록이며 현재 완료 수치가 아닙니다.

기본 테마는 Microsoft Fluent 2다. 전체 공식 컴포넌트, 동일 룩앤필, 비표준 컴포넌트의 특성 기반 비노출이 추가된 현재 목표 원문은 `artifacts/fluent-components-2026-11-05/goal-objective.md`다. 기존 최적화 원장과 위젯 구조 원장을 유지하며 이 범위를 추가한다.

## 공식 기준의 범위

2026-11-05 공식 자료를 확인하고 원본·해시를 `source-manifest.json`에 보존했다.

| 기준 | 관측 | 해석 |
|---|---:|---|
| [Fluent React 카탈로그](https://fluent2.microsoft.design/components/web/react) | 카드 47개 | 전체 플랫폼·하위 컴포넌트의 분모가 아님 |
| [Fluent iOS](https://fluent2.microsoft.design/components/ios) | 카드 12개 | 동일 이름과 플랫폼별 추가 동작의 대응 필요 |
| [Fluent Android](https://fluent2.microsoft.design/components/android) | 카드 5개 | 별도 플랫폼 기준을 누락하지 않음 |
| [Windows 컨트롤](https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/) | 표의 연결 행 62개 | 컨트롤·그룹·패턴이 섞여 있어 컴포넌트 62개라는 뜻이 아님 |
| 공식 React 참조 패키지의 주 진입점 | export 패키지 57개 | 기반 패키지도 포함. 공식 웹 카드에 없는 표·색 선택기·교육용 팝오버 등도 검토 필요 |

참조 저장소 스냅샷은 `microsoft/fluentui` 커밋 `8830c10bfb2f4c1bdd540287b09713b9679cd1ad`다. master 스냅샷을 안정 릴리스로 간주하지 않는다. 카드의 이미지 설명에 있는 Preview도 릴리스 채널의 근거로 사용하지 않는다. Web Components의 공식 연결은 확인했으나 상세 참조 추출이 남아 있다. 현재 목록의 중복·하위 구성·변형·상태·지원 채널을 정규화 중이며 전체 컴포넌트 진행률과 총 진행률은 미확인이다.

## 실제 소비 경로

소스 생성기는 `src/Duxel.Core/FluentComponents.tsv`를 읽는다. 플랫폼 참고64행은 상위55개 이름으로 합쳐지며 변형19개는 부모에 연결된다. 2026-11-11 관리 .NET11 카탈로그 실행은 노출35개·상위 미연결39개·ScopeComplete=false를 확인했다. 현재 공식 URL64개와 차이가 없으며 `artifacts/fluent-tooltip-2026-11-11/catalog-drift.json`에 보존했다. 과거 Link 단계의 관리/Native 일치는 현재 소스의 Native 검증을 대신하지 않는다.

다음은 교정 전 조사 이력이다. 당시 초안은 출처419행·이름289개였으며 현재 생성 요구 목록이 아니다. React 주 진입점의 UI 컴포넌트 선언 217개와 기반 항목 7개, Windows·모바일의 대응을 포함한다. Web Components의 42개 Definition도 대조했다. 37개는 같은 이름이며 나머지 5개는 이름 대응 후보만 기록했다. 이름 대응을 동작·외형 일치로 간주하지 않는다. 폐기 패키지의 Alert, 이전 InfoButton/InfoLabel, Virtualizer 3종은 공식 README의 대체·이동 안내와 함께 별도로 보존한다. 현재 react-infolabel의 같은 이름 컴포넌트는 폐기된 패키지와 구분한다. 네이티브 하위 구성과 변형·상태의 검토는 남아 있다. 출처별 대응은 `artifacts/fluent-catalog-2026-11-05/source-mapping.json`, `scope-review.json`에 보존했다.

공식 출처가 없는 표준 이름은 DUXWGT005, 누락·손상된 목록은 DUXWGT006으로 빌드를 거부한다. `Fluent.RequiredComponentNames`와 `MissingComponentNames`는 각각 현재 목록과 팩터리 미구현 이름을 제공한다. `IsCatalogScopeComplete`는 현재 false다. 감사용 배열은 별도 중첩 타입에 두어 일반 위젯 생성에서 목록을 파싱하거나 조회하지 않는다.

`-p:DuxelRequireCompleteFluentCatalog=true`는 초안 또는 미구현 팩터리가 있으면 DUXWGT007로 실패한다. 교정 전 Core 빌드에서는286개 누락과 초안 상태를 검출했다. 현재 누락 수와 혼동하지 않는다. 이는 **팩터리 선언 검사**이며 외형·상태·동작의 완료 검사와 구분한다. 기본 개발 빌드는 미완성 구현을 계속 개발할 수 있다. 감사 타입의 명시적 정적 생성자로 런타임 조기 초기화를 막고 생성된 어셈블리에서 BeforeFieldInit가 없음을 확인했다. .NET10 생성기 64검사·위젯 집중 78검사와 net8/9/10 빌드 경고0·오류0을 확인했다. 이는 당시 검증 기록이다. 최신 RadioGroup의 관리 GPU1513·고정 RC3 Native 각1513 및 카탈로그 비교는 별도 검증 기록에 있다. 기존 교차 실행8개 이미지 차이와 공식 시각 검증 미완료는 유지한다.

`Dux.Button()`은 이제 생성된 `Fluent.Button()` 팩터리를 사용하고 실제 `UiFluentButton`을 반환한다. 기존 반환 계약 `IUiView`는 유지한다. `UiFluentButton`은 소스 생성기가 `UiButton<UiReleaseButtonBehavior, UiFluentButtonPresentation>`으로 직접 연결하며 사용자 상속을 허용한다.

`UiComponentAttribute`의 Fluent/Infrastructure/Legacy 구분을 Core의 표준 카탈로그 생성기가 소비한다. `Fluent.ComponentNames`와 타입 지정 팩터리는 Fluent 선언만 포함한다. 기존 `UiButton`은 삭제하지 않고 Legacy와 EditorBrowsable(Never)를 적용했다. 일반 사용자 확장은 `UiWidget`·구체 Fluent 위젯·`UiButtonStyle`을 이용한다. 사용자 어셈블리의 선언이 Core의 표준 카탈로그를 교체하지 않는다.

현재 생성기는 UiWidget 파생 구현의 구체 제네릭 타입과 공개 생성자를 분석한다. 잘못된 타입·제약·중복 팩터리를 컴파일 오류로 처리한다. 전체 기존 API·DSL·갤러리·문서의 비표준 분류와 비노출은 아직 완료하지 않았다. 생성 카탈로그에 이름이 있다는 사실은 모든 Fluent 변형·접근성·모션·픽셀 일치를 완료했다는 뜻이 아니다. 현재 색 팔레트는 기존 디자인과 연동하며 동일 룩앤필 비교가 남아 있다.

기존 `ui.Button()` 및 `UiButton`은 수치 스타일 호환 경로를 보존한다. 기본 `Dux.Button()`은 컴파일된 Fluent 구현을 선택하므로 기존 수치 ControlStyle을 새 Fluent 표현 교체 수단으로 사용하지 않는다. 표현 교체는 구체 구현 타입을 선택해 수행한다.

## 리프 수명과 입력 정체성

리프는 `UiControlWidget`을 통해 `UiWidget`의 입력 ID·재진입·소유·해제 계약을 공유하면서, 필요하지 않은 Build 컨텍스트·구독 컬렉션·렌더 델리게이트를 생성하지 않는다. 구성 위젯은 최초 Build 때 컨텍스트를 생성한다.

`UiWidgetIdentity.Instance`는 재사용 인스턴스를 구분한다. `Parent`는 매 프레임 다시 선언하는 기존 팩터리의 라벨 범위를 유지하며, keyed child로 소유하면 부모의 자식 키로 구분한다. 프레임마다 팩터리를 다시 호출해도 press/hold/release가 한 번의 활성화로 연결되는지, 동일 라벨의 서로 다른 키가 구분되는지 검사한다.

준비 후 .NET10 구성 전용 검사에서 1,024회 생성 할당은 이전 delegate 팩터리 131,072B, 새 팩터리 98,304B로 25% 줄었다(개당 128→96B). 이는 같은 프로세스에서 보존한 이전 팩터리 본문과 비교한 할당량이며 앱 Private·FPS·NativeAOT 반복 성능의 증거가 아니다. 강제 GC를 사용하지 않았다.

## 남은 완료 조건

전체 공식 목록·하위 구성·상태의 대응과 분모 확정, 모든 기본 컴포넌트 구현·상속·생성 연결, 비표준 전체 비노출, 공식 참조 화면·입력·접근성·모션 일치가 필요하다. 기존 공통 CPU/GPU·Private·부분 갱신·동일 조건 성능과 Linux/ARM64·Pi 검증도 계속 활성 상태다. 검증 로그·소스 원본·계획은 `artifacts/fluent-components-2026-11-05/`에 있다.


## Badge·CounterBadge 후속

기본 Badge 팩터리와 생성 카탈로그에 공통 상속 위젯을 연결했다. 색·표현64조합과6크기·3모양,카운트·숨김·overflow·dot을 구현했다. 공식 화면 동등성·UIA·RTL/강제 색상은 미완료다. [API·생성 코드·수명·검증](fluent2-badge.ko.md)을 참조한다.


## 공식 플랫폼 범위 후속 조사

과거 조사에서는 Android Compose-v2 소스46개/분류54행을 이전 생성기 입력에 연결했다. 당시289→325개 이름·표준 팩터리26개·미연결299개는 조사 이력이며 현재 요구 수가 아니다. 현재 범위와 수치는 위의 공식 사이트 기준을 따른다. 당시 생성기81·위젯 트리72와 관리/RC3 Native 카탈로그 동치를 확인했다. [출처·대응·회귀 검사·남은 범위](fluent2-platform-inventory.ko.md)를 따른다.

[Divider 공통 구현](fluent2-divider.ko.md)을 추가했다. 전체 공식 시각/입력/접근성 완료를 뜻하지 않는다.

Input의 최신 연결/검증은 artifacts/fluent-input-2026-11-06/verification.json을 따른다. 연결 수와 공식 전체 완료 수는 구분한다.

이전 Tooltip 팩터리 단계 근거: `artifacts/fluent-tooltip-factory-2026-11-11/final-catalog-only/result.json`; 팩터리 검증: `artifacts/fluent-tooltip-factory-2026-11-11/verification.json`.

현재 TabList 카탈로그: `artifacts/fluent-tablist-2026-11-11/final-catalog-only/result.json`. [TabList 기본 구현](fluent2-tablist.ko.md)을 참조한다.

2026-11-13 [Popover 진입 모션](fluent2-popover.ko.md): 기본400ms 페이드·방향별10px 이동·즉시 닫기·같은 시점 캐시·실제 입력/IME/접근성 좌표·유한 예약을 연결했습니다. 관찰 샘플 → Popover → 팝업 모션. 전체 적합성은 미완료입니다.

2026-11-13 [Popover 그리기 범위](fluent2-popover.ko.md): 사용자 정의 PaintBounds·뷰포트 기본값·범위/예산 검사·넓은 장식 샘플을 연결했습니다. 기본 Fluent 픽셀은 유지합니다. 네이티브 Popover 검사 실패는 별도 진단 중이며 전체 적합성 완료가 아닙니다.

2026-11-17 [FluentProvider](fluent2-provider.ko.md)의 네이티브 팔레트·기하 토큰 범위를 연결했습니다. 상위 구성요소 연결은 43/55, 미연결은 12개입니다. 연결 수는 전체 적합성 완료 수가 아닙니다. 전체 브랜드 토큰·Provider 단위 방향과 전체 타이포그래피 토큰 및 나머지 공식 범위는 남아 있습니다.


2026-11-17 [Shimmer](fluent2-shimmer.ko.md) 연결로 카탈로그는 44/55, 미연결은 11개입니다. 로딩→실제 내용 전환·중단·모션 줄이기를 기존 공통 엔진으로 처리합니다. 연결 수는 전체 적합성 완료 수가 아닙니다.


2026-11-17 ProgressIndicator 연결로 카탈로그는 45/55, 미연결은 10개입니다. [API·검증](fluent2-progress-indicator.ko.md). 연결 수는 전체 적합성 완료 수가 아닙니다.


2026-11-17 ActivityIndicator 연결로 카탈로그는 46/55, 미연결은 9개입니다. [API·검증](fluent2-activity-indicator.ko.md). 연결 수는 전체 적합성 완료 수가 아닙니다.

2026-11-18 현재 HeadsUpDisplay까지 연결해47/55·미연결8개입니다. [SegmentedControl 본체 미리보기](fluent2-segmented-control.ko.md)는 입력·화면 예제를 먼저 제공하며, 공식 스타일·스크롤이 남아 있어 연결 수에 포함하지 않습니다.
