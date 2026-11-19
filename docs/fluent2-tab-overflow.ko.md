# 탭 넘침 메뉴

Last synced: 2026-11-11

`UiTabListOptions.Overflow`를 켜면 선택 헤더를 우선 유지하고 숨긴 탭을 공통 Fluent 메뉴에서 선택합니다. 기존 항목 배열·바인딩·`RenderPanel` 키는 그대로 사용합니다.

```csharp
var selection = new UiState<string?>("one");
var tabs = Fluent.TabList("Pages", selection.Binding,
    new UiTabOption[] { new("one", "One"), new("two", "Two"), new("three", "Three"), new("four", "Four") },
    new() { Overflow = new() });
using var header = Dux.SizedBox(tabs, width : 140, height : 40);
// Render header, then tabs.RenderPanel(ui, key, body) for the original keys.
```

기본적으로 헤더 두 개를 남깁니다. [고정 공식 조합 예제](https://github.com/microsoft/fluentui/blob/8830c10bfb2f4c1bdd540287b09713b9679cd1ad/packages/react-components/react-tabs/stories/src/Tabs/TabListWithOverflow.stories.tsx)처럼 선택 값의 우선순위는 2, 나머지는 1입니다. 남은 헤더는 원래 순서를 유지하고 숨긴 헤더는 입력·접근성 컨트롤 게시에서 빠집니다. 메뉴에는 숨긴 값만 들어가며 비활성 상태를 유지합니다. 트리거는 컴파일된 MoreHorizontal 아이콘·선택되지 않은 Tab 역할·명시적 Invoke 기능을 사용하고 일반 탭에 Invoke를 추가하지 않습니다.

메뉴 선택은 원래 바인딩을 바꾸고 해당 헤더를 표시합니다. 입력과 메뉴 선택이 끝난 뒤 헤더 접근성을 게시하므로 새 선택 헤더와 실제 본문이 같은 프레임에 일치합니다. `RenderPanel`의 같은 컨텍스트/프레임 스냅샷 계약과 실제 LabeledBy/ControllerFor 관계도 유지합니다. 페이지를 오가도 호출자가 소유한 본문과 카운터를 재사용합니다.

방향키는 숨긴·비활성 헤더를 건너뛰며 더 보기 동작까지 포커스를 이동합니다. 그룹 전체는 하나의 Tab 정지점입니다. Enter·Space로 더 보기를 열며 수평 목록의 아래·위 방향키도 메뉴 트리거 탐색을 사용합니다. Escape는 더 보기로 복귀하고 Tab은 열기 전 그룹 순서를 따라 나갑니다. 폭이 넓어져 더 보기가 사라지면 선택 헤더로 포커스를 옮깁니다. 수평 RTL과 수직 배치도 같은 우선순위 계산기를 사용합니다. 현재 메뉴 항목은 글자로 표시하며 헤더 아이콘은 유지하지만 메뉴 아이콘 슬롯은 남아 있습니다.

`UiTabListOverflowOptions.MinimumVisible`은 양수, `Label`은 비어 있지 않아야 합니다. 동작의 접근성 이름 앞에는 현재 숨김 개수가 붙습니다. `Overflow = null`로 원래 탭 배치를 복원합니다. `OverflowResult`는 기능이 꺼져 있으면 null이며 켜져 있으면 마지막 배치 결과입니다. 최소 개수가 들어가지 않으면 `Fits`가 false이고 더 보기에는 사용 가능한 공간을 남기며 `IndicatorClipped`로 알립니다. 헤더 입력·접근성은 남은 영역으로 제한하며 최소 개수를 조용히 줄이지 않습니다. 샘플의 부모 할당 변경은 네이티브 창 리사이즈 검사가 아닙니다.

`./run-gallery.ps1` 실행 → **컨테이너** → **탭 넘침과 본문**으로 이동하세요. 홈 본문 카운터를 누르고 점 세 개 메뉴에서 도움말을 선택한 뒤 홈으로 돌아와 값을 확인합니다. 폭 160/520과 LTR/RTL도 전환할 수 있습니다. 설정은 비활성입니다. 둘러싼 ScrollView는 UIA ScrollIntoView로 실제 그룹이나 본문을 표시할 수 있습니다. 헤더만 표시하도록 요청했다고 본문까지 보이는 것을 보장하지는 않습니다.

검증은 `artifacts/tab-overflow-2026-11-11/verification.json`입니다. 관리 SDK 11.0.100/런타임 11.0.0에서 CPU 20·GPU 48(밝음/어두움, MSAA 1/4, 실제 포인터 선택·같은 프레임 본문·획득 이미지 재사용·RTL·수직 배치)·외부 Windows UIA 클라이언트 7/호스트 2·관찰 샘플 70검사를 수행합니다. 외부 클라이언트는 Tab 동작 호출 → 실제 숨긴 MenuItem 선택 → 표시된 Tab과 실제 본문 관계 확인 → 이전 메뉴 제공자 무효화 → 선택 본문 자식 호출을 확인합니다. 탭 목록 197·메뉴 57·접근성 49·예약 35·넘침 26의 집중 회귀도 유지합니다.

전체 표시기 전환·모든 크기/외관/아이콘 조합·메뉴 아이콘 슬롯·정확한 shadow16/강제 색상·전체 네이티브 선택/포커스 알림·음성 안내 검증은 남아 있습니다. NativeAOT와 런타임 이미지 비교도 별도 미실행 단계입니다. 이 집중 검사가 원래 전체 목표의 완료를 뜻하지 않습니다. 사용자 탭 표현은 `MeasureOverflow`·`PaintOverflow`를 명시적으로 구현해야 하며 지원하지 않는 표현을 다른 외관으로 대신하지 않습니다.

관찰 창 상단 탐색에도 넘침을 켜고 자유 흐름 헤더를 사용 가능한 뷰포트 폭으로 제한합니다. 고정 320×900 검사에서 실제 헤더/메뉴 포인터 입력으로 열 페이지에 도달하며 네이티브 창을 리사이즈하지 않습니다. 버튼 도구 모음은 Wrap을 사용해 좁은 페이지에서 엄격한 Row 제약을 위반하지 않습니다. 컨테이너 예제는 폭·RTL 외에 가로/세로 배치 전환도 제공합니다.


### 드래그로 이동하는 탭

`UiTabListOptions.Scroll = new UiTabListScrollOptions()`로 가로 헤더 스크롤을 켭니다. 기존 `Overflow` 메뉴 할당 방식과는 별도 선택이며 두 옵션을 동시에 켜지 않습니다. 상단 관찰 페이지는 이 스크롤 방식을 사용합니다.

탭 영역을 드래그하거나 휠로 움직입니다. 가려진 탭이 있는 쪽에만 `…`이 나타나므로 왼쪽·오른쪽·양쪽 표시가 가능합니다. 점 메뉴를 누르면 해당 방향의 탭 목록이 열리며 위치는 그대로 유지합니다. 항목을 고르면 원래 탭·본문을 선택하고 표시합니다. 가장자리 탭을 누르거나 키보드로 이동하면 선택한 탭뿐 아니라 그 방향의 이웃 탭 하나도 노출해 탐색을 이어갈 수 있습니다. 드래그를 놓는 동작은 탭 클릭으로 처리하지 않습니다.

하단 스크롤바는 별도 영역을 차지하지 않는 오버레이입니다. 기본120ms 나타남·400ms 정지 대기·160ms 사라짐을 적용하며 드래그 중에는 표시를 유지합니다. 공통 `UiScrollbarMotionOptions`로 시간·곡선·감소 모션을 설정하고, 탭별 재정의는 `UiTabListScrollOptions.Motion`으로 지정합니다. 애니메이션이 끝나면 다음 프레임 예약을 멈춥니다. [공통 전환 설계와 API](finite-transitions.ko.md)를 참조하세요.


전체 스크롤바는 동일한 [공통 모션 모듈](scrollbar-motion.ko.md)을 사용합니다.
