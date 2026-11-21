# Duxel 갤러리 탐색

HeadsUpDisplay는 중앙 포털·다섯 개 예제와 설정에 맞는 소스를 제공하며 현재 NEW 배지를 표시합니다. 연결은47/55·미연결8개입니다. 새 달력의 단일·기간 선택은 Duxel 확장으로 구현 중이며 UI가 연결되기 전에는 완성된 메뉴로 집계하지 않습니다.

Windows에서는 `./run-gallery.ps1`, Linux에서는 기존 글꼴·런타임 인수를 받는 `./run-gallery-linux.ps1`을 사용합니다. 이전 observer 실행 명령과 내부 프로젝트·캐시 경로도 유지합니다.

왼쪽 고정 헤더에는 갤러리 제목과 구성요소·미구현 수를 표시합니다. **밝은 테마** 토글은 오른쪽 고정 도구 막대에 있으며, 끄면 어두운 테마, 켜면 밝은 테마입니다. 같은 막대에 상태 정보, V-Sync, 이벤트 대기와 갱신 강조를 표시합니다. 메뉴와 본문은 각각 독립적으로 세로 스크롤하며, 아주 좁은 화면에서는 도구 막대를 가로로 스크롤합니다.

소스 카탈로그의 공식 상위 구성요소55개를 일곱 기능 그룹 안에서 알파벳순으로 표시합니다. 시작·추가 샘플·개발 도구는 별도로 둡니다. 그룹 제목 앞에 Fluent 벡터 아이콘을 배치하고 하위 메뉴에는 반복하지 않습니다. 미구현13개 항목에는 **미구현** 배지를 붙입니다. 선택하면 **아직 미구현됨**을 표시합니다. 배지 상태는 접근성 도움말에도 전달하며 메뉴 선택을 막지 않습니다.

테마와 상태 옵션은 본문을 스크롤해도 오른쪽 상단에 고정됩니다.

구성요소 메뉴는 각각 독립된 예제로 이동합니다. 기존에 종합 화면을 공유하던 21개 구성요소를 분리하고, 종합 화면은 별도 그룹에 모았습니다. 현재 구성요소는 55개이며, 기존·예정 페이지와 메뉴 경로는 각각 71개입니다. 아직 없는 팩터리나 전체 외관·접근성 검증을 완료한 것으로 표시하지 않습니다.

테마를 바꿔도 선택한 페이지, 실제 카운터 값과 시스템 강제 색상은 유지합니다. `GetStyle(includeRequested: true)`는 아직 적용 전인 마지막 테마 요청을 읽으므로 한 입력 묶음에서 두 번 눌러도 두 토글을 모두 처리합니다. 기본 `GetStyle()`은 현재 적용된 범위의 팔레트를 반환합니다. 앱의 렌더링 작업도 시작 시점의 배경 대신 현재 적용된 배경을 사용합니다.

2026-11-17 초기 검증 기록: 갤러리556개(공통 Nav66개 포함), 일반 위젯4,642개, 메뉴378개와 기존 탭197개 계약을 통과했습니다. 최종 Windows 실행은 실제 보조 작업·테마 전환을 포함한68개 표시 시나리오를 확인했고 Linux 호스트 빌드는 경고·오류가 없습니다. 표시 확인은 픽셀 동등성이나 실제 OS 키보드 검증이 아닙니다. Linux 실제 실행, 원래 고정 NativeAOT와 전체 Fluent 적합성은 별도로 남아 있습니다. [검증 기록](../artifacts/drawer-2026-11-17/verification.json).

[공식 Fluent React 갤러리](https://storybooks.fluentui.dev/react/?path=/docs/components-nav--docs)의 구성요소 이름 중심 탐색을 참고했습니다. 기능 그룹은 이 갤러리의 구성입니다. 갤러리는 공통 `Fluent.Nav`와 `ScrollView`를 사용합니다. 그룹 제목을 누르면 접고 펼칠 수 있으며 그룹별 설명도 표시합니다. `동작과 탐색 → Nav`에서 독립 예제를 확인하세요.
## 기존 예제 이름과 실제 메뉴

| 기존 본문 이름 | 왼쪽 메뉴 |
| --- | --- |
| Fluent | 통합 예제 → Fluent · 종합. 개별 컨트롤은 Fluent 컨트롤의 각 메뉴 |
| 타이포그래피 | Text |
| 입력·IME | Field, Input, Label, Textarea |
| 평가 | Rating |
| 아바타 / 아바타 그룹 / 인물 정보 | Avatar / AvatarGroup / Persona |
| 선택 목록 | Combobox, Dropdown, Tag |
| 접기·펼치기 / 목록 / 카드 / 경로 | Accordion / List / Card / Breadcrumb |
| 검색 입력 / 숫자 입력 / 도구 모음 | SearchBox / SpinButton / Toolbar |
| 추가 정보 / 메시지 표시 / 스켈레톤 | InfoLabel / MessageBar / Skeleton |
| SVG / Markdown / 위젯 배치 / 가상 목록 / 팝업 입력 | 추가 샘플 |
| 렌더링 / 측정 재사용 / 갱신 관찰 / 관찰 범위 / 기존 API | 개발 도구 |

## 공통 Nav API

```csharp
var selected = new UiState<string?>("home");
var nav = Fluent.Nav("앱 탐색", selected, new UiNavGroup[]
{
    new("start", "시작", new UiNavItem[]
    {
        new("home", "홈"),
        new("planned", "예정 기능", Badge : new("미구현"))
    }, Description : "소개와 예제")
});
```

`UiNavGroup`은 한 단계의 그룹이며 펼침 상태는 Nav가 유지합니다. `SetExpanded`와 `IsExpanded`로 제어할 수 있습니다. 항목 데이터는 복사하고 아이콘과 페이지는 빌려 사용합니다. 위·아래 방향키와 Home·End는 포커스만 이동하며 Enter나 클릭이 페이지를 선택합니다. 좌·우 방향키는 그룹 접기·펼치기와 하위 항목 이동에 사용하고 RTL에서는 방향을 바꿉니다. 화면 밖 항목은 공통 ScrollView가 드러냅니다. 그룹을 접어도 현재 페이지와 모델을 유지합니다.

같은 프레임에서 `nav.Render(ui)` 다음 `nav.RenderPage(ui, "home", render)`를 호출하면 선택한 페이지를 한 번 렌더링하고 접근성 관계를 연결합니다. Nav는 Navigation, 목적지는 Link, 그룹은 펼침 상태를 가진 Button, 본문은 Pane으로 노출합니다. 다른 위젯 안에서 본문을 직접 관리하는 경우 `RenderPage`는 선택 사항입니다. 탭 목록의 Tab/TabPanel 계약은 바꾸지 않습니다.

`UiNav<TBehavior, TPresentation>`에서 `IUiNavBehavior`와 `IUiNavPresentation`을 각각 지정할 수 있습니다. 동작 타입은 그룹과 목적지 행의 입력을 처리합니다. `UiNavInput.Process`를 사용하면 공통 포인터·키보드 처리에 연결하고 누를 때 실행할지, 놓을 때 실행할지 선택할 수 있습니다. 표현 타입은 행의 크기를 재고 그립니다. Nav는 그리기와 접근성 정보를 갱신하기 전에 바인딩에 선택값을 전달합니다. 바인딩이 거절하면 화면과 접근성 정보에 기존 선택이 유지됩니다. `Fluent.Nav`의 기본 동작은 마우스를 놓을 때 실행합니다.

좁은 메뉴에서는 긴 이름과 그룹 설명을 줄바꿈합니다. 측정 결과는 폭·글꼴·배율·펼침 상태에 따라 보관하며, 선택만 바뀌면 다시 측정하지 않습니다. 팩터리 제공은 공식 전체 적합성 완료를 뜻하지 않습니다. 갤러리 너비가 640 UI 단위 이하이면 **탐색 메뉴 열기**로 여는 Drawer를 사용합니다. 다른 메뉴를 선택하면 닫힙니다. 더 넓은 화면에서는 고정 사이드바를 유지합니다. [공식 Nav 사용 지침](https://fluent2.microsoft.design/components/web/react/core/nav/usage).

## 목적지의 보조 작업

`UiNavItem.SecondaryAction`에 `UiNavSecondaryAction`을 지정하면 페이지 이동과 별개인 작업 버튼 하나를 제공합니다. 아이콘과 접근성 이름, 콜백을 명시합니다. 그룹 제목에는 보조 작업을 붙이지 않습니다.

```csharp
var pin = new UiNavSecondaryAction("홈 고정", () => Console.WriteLine("Pinned"),
    UiMenuIcon.Create<PinIcon, PinFilledIcon>());
var home = new UiNavItem("home", "홈", SecondaryAction : pin);

[UiFluentIcon("pin", UiFluentIconStyle.Regular, 20)]
readonly partial struct PinIcon;
[UiFluentIcon("pin", UiFluentIconStyle.Filled, 20)]
readonly partial struct PinFilledIcon;
```

버튼은 행에 마우스를 올리거나 행·버튼에 포커스를 둘 때 보입니다. 보이지 않을 때도 접근성 트리에 Button으로 남고 이름·Invoke·Expand/Collapse를 제공합니다. 목적지와 버튼의 접근성 경계는 실제 클릭 영역에 맞춰 나눕니다. 버튼·콜백·컨텍스트 메뉴를 실행해도 Nav의 선택 값은 바꾸지 않습니다.

Tab은 목적지 → 보조 버튼 → Nav 밖으로 이동합니다. 보조 버튼에서 Shift+Tab을 누르면 목적지로 돌아갑니다. LTR에서는 오른쪽 방향키로 버튼에 들어가고 왼쪽으로 돌아오며 RTL에서는 방향을 바꿉니다. 위·아래·Home·End는 버튼에서도 목적지 목록을 탐색합니다. Enter·Space와 접근성 Invoke는 같은 콜백을 실행합니다.

행에서 우클릭하거나 포커스를 두고 Shift+F10·메뉴 키를 누르면 같은 작업이 있는 공통 Fluent 메뉴가 열립니다. 보조 버튼의 접근성 Expand도 이 메뉴를 엽니다. Escape나 작업 실행 후에는 호출한 항목·버튼으로 포커스를 돌립니다. 그룹을 접으면 메뉴를 닫고 그룹으로 포커스를 옮깁니다. `IsContextMenuOpen`은 마지막으로 그린 메뉴 노드의 잔존 여부가 아닌 실제 열림 상태입니다. 닫힌 메뉴의 접근성 하위 트리는 다음 프레임에서 제거됩니다.

예제: `./run-gallery.ps1` → **동작과 탐색 → Nav** → **개요 고정**. 마우스·Tab·방향키·우클릭·Shift+F10을 비교하고 아래 실행 횟수가 한 번씩 증가하는지 확인하세요. 테마나 메뉴를 바꿔도 횟수를 유지합니다. 사용자 아이콘 painter와 콜백은 호출자가 소유하며 Nav는 메뉴·Tooltip 상태를 해제합니다. 사용자 정의 `IUiNavPresentation`은 보조 작업의 배치·그리기 메서드도 구현해야 합니다.


## Drawer와 좁은 화면의 탐색 메뉴

`Fluent.Drawer(title, content, options : new(...))`는 본문과 선택적인 하단 작업 버튼을 소유합니다. `Open`, `OpenChanged`, `Show()`, `Close()`로 열고 닫습니다. `UiDrawerOptions`는 화면 위에 겹치는 패널과 배치 공간을 차지하는 인라인 패널, 논리 방향 Start/End/Bottom, RTL, 기본 크기 320/592/940/전체, 크기 직접 지정, 모달·바깥 클릭 닫기, 고정 헤더·하단 작업과 본문 스크롤을 지원합니다. 인라인 패널에는 유한한 교차축 공간 또는 `InlineCrossExtent`가 필요합니다.

겹치는 패널은 보관한 내용을 확대하지 않고 이동시켜 열고 닫습니다. 애니메이션이 끝나면 추가 프레임을 요청하지 않습니다. 인라인 패널의 공간은 현재 즉시 바뀌며, 인라인 배치 애니메이션과 전체 플랫폼 적합성 검증은 남아 있습니다. `ScrollBody : false`는 갤러리 메뉴처럼 자체 스크롤이 있는 유한 높이 본문을 담을 때 사용합니다. 메뉴를 숨긴 호스트는 `RenderPage(..., navigationVisible : false)`로 호출하여 본문 Pane이 없는 Link를 참조하지 않게 합니다.

`./run-gallery.ps1` → **알림과 진행 상태 → Drawer**에서 두 패널을 확인하세요. 좁은 화면의 갤러리는 같은 그룹과 테마 토글을 탐색 패널에 표시합니다. 패널을 닫거나 테마를 바꿔도 입력 모델은 유지합니다. [공식 Fluent Drawer 지침](https://fluent2.microsoft.design/components/web/react/core/drawer/usage), [검증 기록](../artifacts/drawer-2026-11-17/verification.json).


2026-11-17 Drawer 호스팅 검증: 갤러리 564개 검사에 새 GPU·상호작용 검사 8개가 포함됩니다. 좁은 화면의 닫힌 메뉴, 다크·라이트 메뉴, 페이지 선택과 명시적 닫기, Drawer 예제의 두 표시 방식을 캡처했습니다. Windows 실행 검사는 열기·닫기·다시 열기·저장을 포함해 72개 표시 시나리오를 통과했습니다. 실제 접근성 명령을 실행하고, 패널 안의 보이는 컨트롤에 포커스가 있는지와 닫은 뒤 호출 버튼으로 복귀하는지 확인합니다. Linux 호스트 빌드는 통과했으며 실제 Linux 실행은 반복하지 않았습니다. 제품 모듈이 이전 Drawer 빌드와 바이트 단위로 같아 공통 회귀 검증 결과는 재사용합니다. 인라인 배치 애니메이션은 남아 있습니다. [검증 기록](../artifacts/drawer-hosting-2026-11-17/verification.json).


## 독립 예제와 동등한 소스

각 메뉴는 서로 다른 페이지를 유지합니다. 같은 페이지를 가리키는 메뉴가 생기면 시작 검사에서 실패합니다. 종합 화면을 공유하던 구성요소 21개를 독립 예제로 나누고, 기존 종합 화면은 **종합·회귀 예제** 그룹으로 옮겼습니다. 각 페이지 하단에는 선택·복사할 수 있는 읽기 전용 C# 소스를 표시합니다. 소스 영역은 본문과 별도로 가로·세로 스크롤합니다.

독립 예제의 소스는 실제 실행하는 팩터리 식을 `CallerArgumentExpression`으로 가져옵니다. 기존 복합 예제는 빌드할 때 포함한 실제 `.cs` 파일을 표시합니다. 실행할 때 저장소 파일을 읽지 않습니다. 복합 예제의 원본 파일은 갤러리 프로젝트의 보조 타입과 참조를 함께 사용합니다. 미구현 메뉴에는 실행 가능한 소스가 없음을 명시합니다.

`UiViewHost`는 호출자가 소유하는 `IUiView`를 유한한 배치 영역에 담습니다. 순서대로 그리는 화면의 각 위젯이 전체 영역을 상속하지 않게 하며 자식 클리핑·팝업 계층·중첩 창의 좌표를 구분합니다. 인라인 Drawer는 공통 `UiFloatTransition`으로 차지하는 공간과 보이는 영역을 함께 바꿉니다. 내용 크기는 유지하고 반대 방향으로 전환하면 현재 크기부터 이어갑니다. 닫히는 즉시 입력과 접근성 노드는 제거합니다. 처음부터 열린 인라인 패널은 바로 배치하고 이후 변경을 애니메이션으로 표시합니다. `MotionEnabled`가 false이면 즉시 반영합니다. [검증 기록](../artifacts/gallery-source-2026-11-17/verification.json).

## 공통 페이지 머리글과 실행 이름

모든 페이지의 제목은 왼쪽 메뉴 이름과 같습니다. 제목에서 6 UI 단위 아래에 설명을 두고, 설명과 본문 사이에는 28 단위의 여백을 둡니다. 같은 예제의 설정과 상태, 미리보기를 한곳에 모으고 바로 아래에 해당 소스를 표시합니다. 개요와 관찰 범위, 미구현 페이지에는 소스가 없습니다.

갤러리 이름과 구성요소 수는 왼쪽 탐색 메뉴의 고정 헤더입니다. 오른쪽 본문에는 별도의 헤더를 두고 테마 전환, 상태와 옵션을 그 영역의 맨 위에 표시합니다. 왼쪽 탐색 메뉴는 독립적으로 스크롤됩니다. 개요는 기능별 시작 카드와 예제 이동 버튼을 제공합니다. Windows 프로젝트·실행 파일은 `Duxel.Gallery`, Linux는 `Duxel.Gallery.Linux`입니다. 실행은 `./run-gallery.ps1` 또는 `./run-gallery-linux.ps1`을 사용하며 기존 실행 스크립트도 유지합니다.

## 섹션과 그룹 구분

`UiNavGroup.SectionHeader(value, label)`은 목적지가 없는 분류 제목입니다. 섹션에는 항목·아이콘·설명·펼침 동작을 넣을 수 없으며, 클릭이나 키보드 탐색의 대상이 되지 않습니다. 접근성에는 일반 레이블로 남습니다. 실제 그룹은 기존 아이콘과 펼침 화살표를 유지합니다. 하위 항목의 시작 여백은 46 UI 단위이며 RTL에서는 반전됩니다. Fluent 표현은 섹션에 Caption1Strong과 상하 8 단위 여백을 적용하고 다크·라이트·강제 색상을 구분합니다. 섹션은 시각적 분류이므로 목적지의 중첩 단계가 추가되지는 않습니다.

근거: [공식 Nav 사용 지침](https://fluent2.microsoft.design/components/web/react/core/nav/usage/), [섹션 제목 스타일](https://raw.githubusercontent.com/microsoft/fluentui/master/packages/react-components/react-nav/library/src/components/NavSectionHeader/useNavSectionHeaderStyles.styles.ts), [하위 항목 스타일](https://raw.githubusercontent.com/microsoft/fluentui/master/packages/react-components/react-nav/library/src/components/NavSubItem/useNavSubItemStyles.styles.ts).

## 현재 값과 예제 코드

독립 예제 22개도 공통 GallerySample·UiCodeView를 사용합니다. 상태가 바뀌는 16개는 현재 값을 초기화 코드에 반영하고, 고정 예제 6개는 같은 코드를 유지합니다. 팩터리 본문은 실제 컴파일하는 람다에서 가져옵니다. 값이 그대로면 문자열 생성과 구문 분석을 반복하지 않습니다. TagPicker는 검색어와 선택 배열, Combobox는 검색어와 선택값을 함께 반영합니다.

집중 검사 모드 gallery-component-source-only는 세 번째 인수에 TagPicker,Combobox처럼 예제 이름을 쉼표로 구분해 받습니다. 생략하면 22개를 실행합니다. 예제를 마칠 때마다 completed-pages.txt와 소스 스냅샷을 저장합니다. 실행 선택은 명시적이며, 입력과 환경을 판단해 자동으로 검사를 생략하는 전역 캐시는 아닙니다.

이번에는 제품 소스가 바뀌지 않은 성공 13개를 재사용하고 미확인 8개, 요청 방식을 고친 TagPicker 1개를 나누어 확인했습니다. 기존 태그를 유지하며 추가하려면 접근성 AddToSelection을 사용하고, Select는 선택을 교체합니다. 목록을 닫은 뒤 바깥의 태그 제거 버튼을 조작합니다. Label에 연결된 입력창의 접근성 이름은 표시 라벨을 따릅니다. GPU 화면 검사 268건도 통과했습니다. 근거: artifacts/gallery-factory-state-2026-11-17/coverage-and-reuse.json.

2026-11-17 최신 추가 메뉴는 FluentProvider입니다. NEW는 해당 메뉴로 옮기고 이전 TagPicker의 NEW는 제거했습니다. 테마와 설정 그룹에서 실행하며 세 예제의 현재 소스를 바로 확인할 수 있습니다. [API](fluent2-provider.ko.md).


최신 메뉴 추가 묶음은 Shimmer입니다. NEW는 Shimmer 오른쪽에만 표시하고 이전 FluentProvider의 NEW는 제거합니다. 알림과 진행 상태 그룹에서 로딩·완료 전환과 바로 아래 소스를 확인합니다.


왼쪽 고정 메뉴의 최대 너비를 224에서 280 논리 픽셀로 넓혔습니다. 기본 갤러리 창에서는 280이며 좁은 창에서는 본문 너비에 맞춰 줄어듭니다. 640 이하의 기존 서랍 메뉴 전환 기준은 유지합니다.


최신 추가 묶음은 ProgressIndicator입니다. NEW는 ProgressIndicator에 표시하며 Shimmer의 NEW는 제거했습니다. 알림과 진행 상태 그룹에서 확정·미확정 예제와 각각의 소스를 확인합니다.


최신 추가 묶음은 ActivityIndicator입니다. NEW는 ActivityIndicator에만 표시하며 ProgressIndicator의 NEW는 제거했습니다. 다섯 크기의 예제마다 바로 아래 소스를 제공합니다.


최신 추가 묶음은 DatePicker·TimePicker·DateTimePicker·공휴일 제공자입니다. NEW를 이 네 메뉴로 옮겼습니다. Duxel 위젯의 날짜와 시간 그룹에 Calendar·RangeCalendar와 함께 두고, 각 예제 바로 아래에 현재 설정의 소스를 표시합니다.

렌더링 메뉴 맨 아래에 양끝 투명 마스크·캐시 카메라 예제를 추가했습니다. 배경·마스크·카메라를 바꾸면서 내용 준비 횟수와 바로 아래의 실제 소스를 확인할 수 있습니다. 렌더링 예제별 소스는6개입니다.
