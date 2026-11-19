# Fluent 메뉴 표현과 공유 팝업

Fluent 메뉴의 표면·행·선택 표시·아이콘·보조 텍스트에 시스템 대비 색상을 적용합니다. 기본 상태는 Canvas/CanvasText, 비활성 항목은 GrayText, 호버·누름 상태는 Highlight/HighlightText를 사용하며 보조 텍스트 위의 호버에도 같은 역할을 유지합니다. 표면의 1픽셀 테두리는 CanvasText를 사용합니다. 컨텍스트를 받는 `UiFluentMenuPalette.Resolve`·`Icon`·`Secondary` 오버로드를 추가했으며 기존 색상 모드 전용 API도 유지합니다.

`IUiMenuPresentation.PaintFocus`는 선택적으로 구현하는 포커스 덧그리기 콜백입니다. 공통 팝업 렌더러는 모든 항목을 그린 뒤 항목 뷰포트의 클립을 닫고 호출하며, 바깥 팝업의 클립은 유지합니다. Fluent 대비 모드에서는 공식 방식대로 행 바깥에 두께 2픽셀의 Highlight 선을 표시해, 포커스와 호버가 겹쳐도 선이 보이게 합니다. 기존 표현은 빈 기본 콜백을 상속하며 일반 Fluent 포커스는 기존 경로를 유지합니다. 근거: [메뉴 항목의 대비 색상](https://raw.githubusercontent.com/microsoft/fluentui/master/packages/react-components/react-menu/library/src/components/MenuItem/useMenuItemStyles.styles.ts), [바깥 포커스 선의 배치](https://raw.githubusercontent.com/microsoft/fluentui/master/packages/react-components/react-tabster/src/focus/createFocusOutlineStyle.ts), [표면 테두리](https://raw.githubusercontent.com/microsoft/fluentui/master/packages/react-components/react-menu/library/src/components/MenuPopover/useMenuPopoverStyles.styles.ts).

GPU 검사 132개로 양방향 배치·선택 여부·보조 텍스트 호버·포커스/호버/누름의 결합을 포함한 여덟 상태를 검사했습니다. 색상 역할을 직접 지정한 별도 기준과 픽셀을 정확히 비교하고 실제 팝업의 접근성 동작도 확인합니다. 기존 313개를 포함한 445개, 일반 Menu 216개·Dropdown 41개·Combobox 84개·스크롤 모션 44개 검사가 통과했으며 일반 RGB 화면 281장은 수정 전 기준과 일치합니다. 긴 메뉴의 기하 재사용 검사는 기존 `CaptureGeometry`로 비교할 두 프레임의 애니메이션 시각을 고정합니다. 전체 화면의 정확한 비교를 유지하며 다른 캡처의 애니메이션은 계속 진행됩니다. 근거: `artifacts/menu-contrast-2026-11-16/verification.json`. Dropdown·Combobox 항목에도 별도의 시스템 색상을 적용했으며 Menu의 Highlight 대신 Canvas 호버를 유지합니다. [선택 항목의 색상 역할](fluent2-dropdown.ko.md)을 참고하세요. 전체 브라우저·네이티브·OS 적합성과 shadow16도 남아 있습니다.

최신 탭 조합: [TabList 넘침](fluent2-tab-overflow.ko.md)이 공유 트리거/팝업 엔진을 사용합니다. 아래 전체 Menu 준수 잔여 범위는 계속 적용됩니다.

Last synced: 2026-11-16

`Fluent.Menu`는 `UiFluentMenu`를 만들며 `Dux.Menu`와 같은 입력·닫기·포커스·접근성 엔진을 사용합니다. 트리거 그리기 뒤 공유 팝업 메서드를 호출하므로 팝업 구현은 하나입니다. Menu를 처음 연결했을 때의 기록은 노출 이름 36개(공식 상위 이름 17개·변형 19개), 당시 상위 55개 중 미연결 38개입니다. 이후 연결 상태는 [현재 카탈로그 범위](fluent2-scope.ko.md)를 참고하세요. 팩터리 연결이 전체 상태·외관·접근성 준수 완료를 뜻하지 않습니다.

```csharp
using var menu = Fluent.Menu("Actions",
    [new("open", "Open"), new("disabled", "Unavailable", false), new("save", "Save")],
    value => Console.WriteLine(value));
```

고정 근거는 Fluent UI `8830c10bfb2f4c1bdd540287b09713b9679cd1ad`의 [MenuItem](https://github.com/microsoft/fluentui/blob/8830c10bfb2f4c1bdd540287b09713b9679cd1ad/packages/react-components/react-menu/library/src/components/MenuItem/useMenuItemStyles.styles.ts)·[MenuPopover](https://github.com/microsoft/fluentui/blob/8830c10bfb2f4c1bdd540287b09713b9679cd1ad/packages/react-components/react-menu/library/src/components/MenuPopover/useMenuPopoverStyles.styles.ts)·[MenuList](https://github.com/microsoft/fluentui/blob/8830c10bfb2f4c1bdd540287b09713b9679cd1ad/packages/react-components/react-menu/library/src/components/MenuList/useMenuListStyles.styles.ts)입니다. Regular Type300 글자·반경 4·표면 최소 폭 138/최대 300·행 안쪽 여백 5px(표면 여백 4px+투명 테두리 1px)·행 높이 32px·행 간격 2px·글자 여백 8px(항목 6px+내용 2px)을 적용합니다. 밝음/어두움의 기본·호버·누름·비활성 색은 독립적인 고정 토큰 자료와 대조합니다. RTL 옵션은 논리적 위치와 글자 정렬에 적용합니다.

`UiMenuOptions.ItemSpacing`과 `MaximumWidth`는 사용자 표현에도 적용됩니다. 잘못된 간격과 폭은 상태 교체 전에 실패합니다. `IUiMenuPresentation.DefaultOptions`로 표현별 기본값을 고르고 `PaintItem`에 옵션을 명시적으로 전달합니다. 열린 메뉴는 항목을 그리거나 게시하기 전에 대기 중인 모든 항목 포커스 요청을 처리하므로 뒤 항목의 요청 때문에 한 프레임에 두 행의 포커스를 그리지 않습니다. 소유자가 비활성화되면 비활성 트리거로 포커스를 복귀시키지 않고 닫습니다.

`./run-gallery.ps1` 실행 → **Menu** → **Menu 공통 동작**까지 스크롤 → **Fluent 메뉴 열기**를 누르세요. 바로 옆의 공통 테마 메뉴와 비교할 수 있고, 둘 다 실제 콜백과 선택 값 문구를 사용합니다. 관찰 범위는 새 팩터리를 이 페이지에 연결하며 시작할 때 누락을 검사합니다.

근거는 `artifacts/menu-composition-2026-11-11/verification.json`입니다. CPU 57검사는 두 표현·공식 색상 자료·실제 배치·최대 폭·생성 팩터리 노출을 확인합니다. GPU 96검사는 두 표현·두 색상 모드·MSAA 1/4·글자 표시·획득 이미지 재사용·제거와 실제 Fluent 호버/누름/비활성 배경의 독립 픽셀 비교를 포함합니다. 각 표현에서 외부 Windows UIA 클라이언트 4검사와 소유 호스트 2검사도 수행합니다. 관찰 샘플 53과 접근성·예약·탭·넘침·위젯 트리의 집중 회귀는 별도 근거입니다. 관리 SDK 11.0.100/런타임 11.0.0을 고정하며 NativeAOT 검증은 남아 있습니다.

정확한 shadow16·실제 OS 대비 모드 전환·전체 포커스 표현·중첩 항목·여러 줄 하위 문구는 남아 있습니다. 아래 절에서 체크/라디오 항목·아이콘/보조 내용·첫 글자 탐색·긴 메뉴 스크롤·트리거 확장·메뉴 자동화 이벤트의 범위를 설명합니다. 고정 MenuList 소스는 누적 검색 대신 첫 글자 순환을 사용합니다. TabList 넘침 연결은 별도 안내에서 확인할 수 있습니다. 빠진 그림자를 근사 표현으로 대신하지 않으며 Fluent 전체 완료나 속도 개선을 주장하지 않습니다. 키보드·수명 계약은 [공통 메뉴 동작](menu-foundation.ko.md)을 참고하세요.

## 아이콘과 오른쪽 안내 문구 — 2026-11-11

`UiMenuOption.Icon`은 `UiMenuIcon.Create<TRegular, TFilled>()`로 컴파일한 Fluent TTF 글리프를 재사용합니다. 활성 행의 hover는 Filled 글리프와 공식 브랜드 아이콘 색을 선택하며 비활성 행은 Regular를 유지합니다. `SecondaryContent`는 비어 있지 않은 한 줄 안내 문구이며 HelpText로 노출합니다. 동작 Name과 첫 글자 탐색은 계속 Label을 사용합니다. 단축키를 등록하지 않습니다. 체크/라디오 팩터리는 레코드를 반환하므로 `with { Icon = icon, SecondaryContent = "PNG" }`로 결과를 꾸밉니다.

공통 배치는 체크16px+간격4px, 아이콘20px+간격4px 열을 해당 내용이 있는 메뉴의 모든 행에 확보합니다. Fluent는 루트 여백6px·내용 여백2px·보조 글자12px/행 높이20px을 사용합니다. 보조 문구는 자체 영역의 hover 색을 가지며 라벨을 가리킬 때는 색을 바꾸지 않습니다. 보조 문구의 본래 폭을 확보한 뒤 남은 폭에 라벨을 배치하고 각각 클립합니다. RTL은 열을 반대로 배치합니다. 테마/Fluent 표현은 같은 배치를 사용합니다. 사용자 표현은 구조화된 UiMenuItemContent 오버로드를 구현해야 하며 미지원은 명시적으로 실패합니다. UiControlContext.IsHovered는 입력·포커스를 등록하거나 프레임을 요청하지 않는 클립 기반 포인터 조회입니다.

`./run-gallery.ps1` → **Menu** → **Menu 공통 동작** → **아이콘 메뉴 열기**에서 확인하세요. **메뉴 방향 : LTR/RTL**로 방향을 바꿉니다. 일반·체크·아이콘 없는 행·비활성 행을 포함합니다. 근거는 artifacts/menu-content-2026-11-11/verification.json입니다. GPU는 실제 고정 글리프와12px/20px 보조 글자를 별도로 그린 독립 기준과 로컬 hover 픽셀을 비교합니다. 기존 탭 아이콘도 컴파일된 쌍 캐시를 공유합니다. 이 구현은 아이콘 폰트를 사용하며 새로 요청한 SVG 이미지/위젯 목표는 [SVG 지원](svg-support.ko.md)에서 추적합니다.

## 긴 메뉴와 첫 글자 탐색 — 2026-11-11

메뉴는 화면 높이를 넘지 않으며 선택적으로 `MaximumHeight`를 지정할 수 있습니다. 넘친 행은 콘텐츠 영역으로 클립하고 기존 세로 스크롤바와 휠 처리를 재사용합니다. 방향키·Home·End·문자 탐색으로 옮긴 항목은 같은 프레임에 보이게 이동합니다. 항목 순서를 바꾸면 안정된 값으로 포커스를 유지하고 새 위치를 표시합니다.

```csharp
var menu = Fluent.Menu("동작", items, Select,
    new UiMenuOptions(138, 32, 5, ItemSpacing : 2, MaximumWidth : 300)
    { MaximumHeight = 210 });
```

표시 가능한 Unicode 문자는 다음 일치하는 첫 글자를 탐색합니다. 대소문자를 구분하지 않고 반복 입력하면 다음 일치 항목으로 순환합니다. 한글과 보조 평면 문자도 처리합니다. Ctrl·Alt·Super 입력은 제외하며 Space는 기존 실행 동작을 유지합니다. 여러 글자를 누적하는 검색 버퍼는 제공하지 않습니다. 탐색은 실행하지 않고 이전 Space 누름을 취소합니다. 정지한 메뉴는 주기적 프레임이나 검색 타이머를 예약하지 않습니다.

공통 동작이므로 기본 메뉴·Fluent 메뉴·탭 넘침 메뉴에 적용됩니다. 관찰 창의 컨테이너 → Menu 공통 동작 → 긴 메뉴·문자 탐색에서 휠·스크롤바·Z·Enter를 확인하세요. [WAI 메뉴 패턴](https://www.w3.org/WAI/ARIA/apg/patterns/menubar/)의 선택적 첫 글자 탐색을 참고했습니다. 전체 Fluent 룩앤필, 하위 메뉴·체크 항목·전체 확장/스크롤 UIA 계약은 여전히 미완료입니다.

## 메뉴 스크롤 접근성 — 2026-11-11

넘치는 메뉴는 공통 `UiAccessibilityScrollInfo`를 게시하고 Windows Scroll 패턴으로 백분율·상대 이동과 실제 뷰 크기를 제공합니다. 행은 ScrollItem을 통해 화면 밖에서도 `ScrollIntoView`를 요청할 수 있습니다. 같은 행의 런타임 정체성을 유지하며 필요한 후속 프레임에서 표시를 완료합니다. 표시 위치가 바뀌면 기존 스크롤 속성 알림을 사용하고 변화 없는 프레임에서는 알리지 않습니다.

팝업의 논리적 부모가 ScrollView여도 메뉴는 독립된 화면 좌표를 사용합니다. 공통 행 표시 경로에 독립 스크롤 경계를 지정해 메뉴 표시 요청이 바깥 페이지를 움직이지 않게 했습니다. 항목 수가 줄어 모두 들어가면 Scroll 패턴과 행 ScrollItem을 철회하고 네이티브 속성은 -1%·뷰 크기100을 게시합니다.

닫힌 메뉴의 직접 공급자 읽기와 외부 변경 요청은 element unavailable을 반환합니다. 현재 Windows UIA 클라이언트의 보유한 패턴 읽기에서는 S_OK/0이 관측됐습니다. 외부 읽기만으로 공급자 생존을 판정하지 않습니다. 첫 검사의 그 기대값 실패를 보존하고 직접 공급자 반환값·외부 변경 거부·새 팝업 표시를 각각 검사했습니다. 이는 실제 화면 읽기 프로그램의 발화나 전체 메뉴 준수 검증은 아닙니다.

근거: artifacts/menu-scroll-uia-2026-11-11/verification.json. 별도 클라이언트는 실제 MenuItem의 첫/마지막 표시, 백분율 변경 알림, 실행, 닫힌 변경 요청 거부와 재개방을 확인합니다. [Scroll 패턴](https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-implementingscroll)과 [ScrollItem](https://learn.microsoft.com/en-us/windows/win32/api/uiautomationclient/nf-uiautomationclient-iuiautomationscrollitempattern-scrollintoview)을 참고합니다. 메뉴 확장 상태·열림/닫힘 자동화 이벤트와 나머지 Fluent 계약은 계속 남아 있습니다.

## 확장 상태와 메뉴 알림 — 2026-11-11

실제 메뉴 버튼과 탭 넘침 트리거는 `UiAccessibilityNode.Expansion`에 Collapsed/Expanded를 게시합니다. 일반 탭은 이 기능을 받지 않습니다. `UiAccessibilityAction.Expand`와 Collapse는 멱등이며 반복 요청으로 메뉴를 토글하거나 동작을 실행하지 않습니다. disabled·사라진 트리거·leaf 상태의 요청은 해당 오류로 거부합니다.

`UiAccessibilityTree.ExpansionChanged`는 유지된 컨트롤의 최종 상태/기능 변경을 게시 후 잠금 밖에서 알립니다. 최초 발견이나 변화 없는 프레임을 변경 알림으로 만들지 않습니다. `wait : false` 요청의 성공은 접수 결과이며 다음 프레임의 상태/알림으로 적용을 확인합니다. Windows ExpandCollapse 호출은 기존 요청 큐를 통해 게시 완료를 기다립니다. 렌더 생산자 스레드에서 동기 요청하면 Busy입니다.

메뉴가 나타나면 MenuOpened, 사라지면 MenuClosed를 보냅니다. 닫힘은 게시 해제 전에 이전 소스가 조회 가능한 동안 전달하고, 열림은 새 트리 게시 후 전달합니다. 기존 Tooltip과 공통 수명 계산을 사용합니다. Windows는 10005 패턴·30028 가용성·30070 상태·20003/20007 메뉴 이벤트를 제공합니다. 닫힌 이벤트 캐시는 소스 이름·정체성과 offscreen 상태를 유지하며 창 종료도 처리합니다. 완전한 메뉴·화면 읽기 프로그램 발화 검증을 뜻하지 않습니다.

근거: artifacts/menu-expansion-2026-11-11/verification.json. 반복 요청, 잘못된 enum/leaf, disable·소유자 제거·트리 종료, 실제 관찰 샘플, 별도 Fluent 메뉴/탭 넘침 클라이언트와 Tooltip 종료 회귀를 검사합니다. [ExpandCollapse 계약](https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-implementingexpandcollapse), [Menu 이벤트](https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-supportmenucontroltype)를 참고합니다.

## 체크 메뉴 — 2026-11-11

`UiMenuOption.Checkbox(value, label, UiBinding<bool>, enabled : true)`는 호출자 소유 상태를 빌립니다. 포인터 놓기·Enter·Space 놓기·Invoke·Toggle은 콜백 전에 상태를 갱신합니다. 체크 항목은 팝업을 유지하고 일반 동작은 닫습니다. 같은 바인딩 인스턴스는 열린 프레임당 한 번 읽으며 모든 공유 행은 같은 프레임의 최종 상태를 게시합니다. 닫힌 메뉴는 바인딩을 읽지 않습니다. 변화 없는 재생은 setter·애니메이션 예약·중복 토글 알림을 만들지 않습니다.

기본 두 표현은 체크 항목이 있으면 모든 행에 공통 20px 체크 열을 확보합니다. Fluent는 공식 Checkmark16Filled 글리프를 논리 시작 위치에 그리며 RTL을 지원합니다. 사용자 `IUiMenuPresentation`은 체크 메뉴용 `UiMenuItemContent` 측정·그리기 오버로드를 구현해야 합니다. 미지원 내용은 명시적으로 실패합니다. 기존 문자열 전용 메뉴 계약은 유지합니다.

`UiAccessibilityNode.Checked`는 nullable이며 null이면 Toggle 기능이 없습니다. `ToggleChanged`는 유지된 행의 최종 상태·기능 변경을 게시 후 잠금 밖에서 알립니다. Windows는 10015 패턴·30041 가용성·30086 상태와 생성형 COM IToggleProvider의 Toggle/get_ToggleState를 연결합니다. 비활성·보이지 않는 행·사라진 행의 변경 요청은 기존 큐에서 거부합니다. 생산자 스레드의 동기 요청은 Busy입니다.

`./run-gallery.ps1` → **Menu** → **Menu 공통 동작** → **체크 메뉴 열기**에서 상세 정보·격자를 바꾸고 **설정 마침**으로 닫으세요. 다른 페이지로 이동했다 돌아와 상태 유지를 확인할 수 있습니다. 하위 메뉴·보조 문구·shadow16·강제 색상·전체 발화는 남은 범위입니다. [고정 체크 메뉴 소스](https://github.com/microsoft/fluentui/blob/8830c10bfb2f4c1bdd540287b09713b9679cd1ad/packages/react-components/react-menu/library/src/components/MenuItemCheckbox/useMenuItemCheckbox.tsx), artifacts/menu-checkbox-2026-11-11/verification.json을 참고하세요.

## 라디오 그룹 — 2026-11-11

```csharp
var selection = new UiState<string?>("name");
var sorting = new UiMenuRadioGroup("정렬 기준", selection);
using var menu = Fluent.Menu("정렬",
    [UiMenuOption.Radio("name", "이름순", sorting), UiMenuOption.Radio("date", "날짜순", sorting)],
    value => Console.WriteLine(value));
```

그룹은 문자열 바인딩 하나를 빌립니다. 같은 그룹의 연속 행은 상호 배타적으로 선택하며 다른 그룹은 독립 상태를 유지합니다. 한 메뉴 안의 그룹 이름은 고유해야 하며 독립 그룹은 같은 바인딩을 재사용할 수 없습니다. 잘못된 조합은 유효한 항목을 교체하기 전에 실패합니다. 바인딩 값이 현재 행에 없으면 기본값으로 대체하지 않고 빈 선택을 표시합니다. 열린 프레임당 그룹 바인딩을 한 번 읽습니다. 라디오 표시는 RTL을 포함한 공식 체크 열을 재사용하며 접근성에는 체크 Toggle 대신 Selected를 게시합니다.

포인터 놓기·Enter·Space 놓기·Invoke·Select는 콜백 전에 그룹 상태를 반영하고 팝업을 닫습니다. 고정 [라디오 메뉴 소스](https://github.com/microsoft/fluentui/blob/8830c10bfb2f4c1bdd540287b09713b9679cd1ad/packages/react-components/react-menu/library/src/components/MenuItemRadio/useMenuItemRadio.tsx)의 기본 동작을 따릅니다. 이미 선택한 항목도 닫고 콜백을 한 번 실행하지만 setter를 중복 호출하지 않습니다. 선택된 라디오의 Deselect와 다른 선택 옆의 AddToSelection은 실패하며 선택되지 않은 행의 Deselect는 아무것도 변경하지 않습니다. 체크 항목은 계속 열린 상태를 유지합니다.

접근성 트리는 이름 있는 MenuGroup(Windows Group50026)을 포함하며 Selection10001과 자식 MenuItem의 SelectionItem10010을 제공합니다. SelectionContainer는 실제 그룹을 가리킵니다. CanSelectMultiple은 false이며 IsSelectionRequired는 현재 행에 바인딩의 선택이 있는지를 반영합니다. 최종 선택·속성 알림은 기존 선택 게시 엔진을 공유합니다. 즉시 닫히는 메뉴 알림은 변경 불가능한 이벤트 소스와 읽기 전용 선택 상태를 사용하므로 비동기 UIA 캐시가 닫힌 뒤에도 읽기를 마칠 수 있습니다. 이벤트 스냅샷의 변경 동작은 거부하며 실제 제공자는 닫으면 무효화합니다.

`./run-gallery.ps1` → **Menu** → **Menu 공통 동작** → **라디오 메뉴 열기**에서 정렬·보기를 각각 선택하고 다시 열어 확인하세요. 다른 페이지를 다녀와도 값을 유지합니다. 근거와 남은 범위는 artifacts/menu-radio-2026-11-11/verification.json에 기록합니다. 고정 NativeAOT와 전체 발화는 별도 검증입니다.


## 부모 측정 재사용 — 2026-11-12

IUiMenuPresentation.CanRetainTriggerMeasurement는 트리거 기하의 결정성만 약속합니다. 내장 Theme·Fluent 표현은 참여하고 사용자 정의 표현의 기본값은 false입니다. 팝업 내용의 캐시나 체크·라디오 바인딩 읽기, 스크롤·선택·동작의 생략을 뜻하지 않습니다. 실제 Observer에서 부모 트리거 측정을 유지한 채 메뉴 체크 활성화를 검증했습니다.

근거: artifacts/form-layout-retention-2026-11-12/verification.json. 관찰: ./run-gallery.ps1 → 측정 재사용.
