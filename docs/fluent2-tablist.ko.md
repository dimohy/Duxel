# Fluent 2 TabList

최신 넘침 연결: [선택 우선 넘침과 공유 메뉴](fluent2-tab-overflow.ko.md). 기존 배치가 기본값이며 TabList 전체 일치 검증은 남아 있습니다.

Last synced: 2026-11-11

SDK 근거: 저장소는 latestPatch 설정으로 RC3를 요청하지만 현재 SDK11.0.100을 선택한다. 이번 본문 검증은 `artifacts/tab-panel-2026-11-11/global.json`에서 roll-forward를 끄고11.0.100을 고정했으며 런타임은11.0.0이다. 미실행 RC3 NativeAOT·런타임 이미지 비교를 대체하지 않는다. 이전 선택 이벤트 기록은 빌드 전에 SDK를 직접 확인하지 않아 RC3 SDK 표기를 미검증으로 정정했다.

`Fluent.TabList`는 선택 값과 키보드 포커스를 분리한 컴파일된 탭 헤더다. 상속 가능한 `UiFluentTabList`는 `UiTabList<UiFluentTabListBehavior, UiFluentTabListPresentation>`에 정적으로 연결된다. 탭 내용은 호출자가 선택 값으로 표시하며, 헤더 위젯이 별도 창이나 본문 인스턴스를 만들지 않는다.

```csharp
var selection = new UiState<string?>("overview");
UiTabOption[] pages = [new("overview", "개요"), new("details", "상세"), new("unavailable", "사용 불가", false)];
var tabs = Fluent.TabList("페이지", selection.Binding, pages);
```

같은 프레임에서 헤더와 호출자가 소유한 본문을 렌더링한다.

```csharp
tabs.Render(ui);
tabs.RenderPanel(ui, "overview", overviewWidget);
tabs.RenderPanel(ui, "details", detailsWidget);
```

기존 화면 코드는 캐시한 `Action<UiImmediateContext>`도 전달할 수 있다. `RenderPanel`은 선택된 본문에서만 true를 반환하고 비선택 본문을 호출하지 않는다. 위젯·콜백을 빌려 사용하므로 수명과 상태는 호출자가 유지한다. 호출자의 배치·ID 범위를 사용하고 별도 창을 만들지 않는다. 기본 Tab 순서는 헤더 → 본문 → 본문 자식이며, `focusable : false`이면 본문 자체의 정지점을 뺀다. 헤더보다 먼저, 다른 컨텍스트·프레임에서, 렌더링한 헤더에 없는 키로, 선택 본문을 두 번 호출하면 명시적으로 실패한다. 헤더 뒤에 선택 값이나 항목이 바뀌어도 이번 프레임의 선택·키·라벨은 이미 그린 헤더와 맞추고 다음 프레임에 변경을 적용한다.

선택한 TabPanel은 실제 소비한 본문 영역을 클립해 게시하고 본문 접근성 노드의 부모가 된다. 범위를 나가면 바깥 부모를 복원한다. Windows는 Pane(50033)과 AriaRole `tabpanel`을 제공한다. LabeledBy(30018)는 실제 탭을 가리키고, 탭의 ControllerFor(30104)는 살아 있는 본문을 담은 공급자 배열을 반환한다. 비선택 탭은 빈 배열을 제공한다. 떠난 본문 공급자는 사용할 수 없으며, 돌아오면 같은 의미 ID와 새 런타임 식별자를 사용한다. Tab·Shift+Tab과 큐에 넣은 접근성 포커스는 공통 탐색 경로를 사용한다. LabeledBy·ControllerFor 변경 알림은 [관계 변경](accessibility-relationships.ko.md) 범위로 검증하며, 네이티브 포커스 이벤트는 [포커스 게시](accessibility-focus.ko.md) 범위로 검증했고 화면 읽기 음성은 남아 있다.

현재 관리 검증: TabListCPU77 = 기본27 + COM12 + 선택 게시13 + 본문25, 관찰CPU39, 외부 네이티브 클라이언트21/호스트3, 관찰GPU37 통과. 이전 관찰 화면58개의 본문은 변동하는 상단128픽셀을 제외하고 일치한다. Graphics·Rendering 검사 프로젝트와 관찰 샘플은 경고·오류0으로 빌드한다. 근거: `artifacts/tab-panel-2026-11-11/verification.json`. 공식 전체 외관과 NativeAOT는 별도 미완료 게이트다.

실행은 저장소 루트에서 `./run-gallery.ps1`이다. 관찰 창의 모든 페이지 상단에서 실제 팩터리를 사용한다. Tab으로 진입하고 좌우 방향키로 포커스를 옮긴 뒤 Enter 또는 Space로 선택한다. Shift+Tab으로 나갔다가 다시 들어오면 현재 선택한 항목에 포커스를 둔다. 세로 배치는 위아래 방향키를 사용하고 RTL 가로 배치는 좌우를 반전한다. 비활성 항목은 건너뛰며 Home·End와 순환 이동을 지원한다. Escape는 대기 중인 포인터·Space 선택을 취소한다.

`UiTabListOptions`는 Small·Medium·Large, 가로·세로, RTL, Transparent·Subtle·SubtleCircular·FilledCircular, 선택 글자 너비 예약을 제공한다. `SelectOnFocus` 기본값은 false이며 true이면 포커스 이동과 함께 선택한다. Small과 Medium은 Body1 14/20, Large는 Body2 16/22를 사용하며 선택 글자는 Semibold다. 기본 너비 예약은 선택에 따른 헤더 흔들림을 막는다. 옵션 변경과 `SetItems`는 프레임을 요청한다. 항목은 복사하며 중복·null 키/라벨은 기존 상태를 바꾸기 전에 거부한다. 키는 표시 이름과 독립적이고 식별자 구문을 포함해도 충돌하지 않도록 UTF-16 코드 단위로 인코딩한다.

바인딩은 호출자의 선택 값을 소유한다. null은 명시적인 미선택 상태이며 임의의 대체 선택을 만들지 않는다. 포커스는 재정렬 후에도 같은 키를 따른다. `IUiTabListBehavior`와 `IUiTabListPresentation`으로 동작·측정·그리기를 교체할 수 있으며 입력 상태는 위젯이 제공한다. 기본 예약 측정은 선택 바인딩을 읽지 않고, 렌더링에서는 선택 값을 한 번 읽어 처리한다.

접근성 트리는 TabList 부모와 Tab 자식을 게시하고 선택·포커스·활성·클립 상태를 나눈다. Windows는 실제 Tab/TabItem 제어 형식과 Selection/SelectionItem 패턴을 제공한다. Select·Focus·Deselect는 기존 렌더 스레드 작업 큐를 사용하며 공급자 호출은 비동기로 요청을 전달한다. Selection은 단일 선택이고 미선택도 허용한다. 다른 항목이 선택된 상태의 AddToSelection은 오류를 반환하며, 제거된 공급자는 사용할 수 없다. 외부 클라이언트 전체 검증은 남아 있으며 선택 본문 관계는 위 범위로 검증한다.

`UiAccessibilityTree.SelectionChanged`는 새 트리를 게시한 뒤 읽기 잠금 밖에서 호출한다. 콜백은 게시된 상태를 읽거나 작업을 큐에 넣을 수 있지만 게시·폐기에 재진입할 수 없다. 선택 교체는 두 탭의 IsSelected 속성 변경과 새 선택의 ElementSelected를 알린다. 명시적 미선택은 남아 있는 탭에서 ElementRemovedFromSelection을 알린다. 선택 항목을 대체 없이 제거하면 살아 있는 부모의 SelectionInvalidated를 알리며, 부모까지 제거하면 알리지 않는다. 초기 탐색·변화 없는 프레임·같은 키의 재정렬은 선택 이벤트를 만들지 않는다. 기존 부모에 선택된 항목을 추가할 때 과거 속성 값을 임의로 만들지 않는다.

이전 선택 이벤트 검증의 런타임은 관리 .NET 11.0.0이다. CPU52 = 기본27 + COM12 + 게시13이며, 별도 Windows UIA 클라이언트8개와 소유 호스트3개 검사가 통과하고 둘 다 exit0으로 종료한다. 클라이언트는 캐시된 선택·제거 이벤트, IsSelected 속성 변경4건, 선택 항목 제거 후 부모 무효화를 수신한다. 근거: `artifacts/tab-selection-events-2026-11-11/verification.json`. 이 소유 HWND 검사는 GPU 화면이나 화면 읽기 프로그램의 음성 출력을 검증하지 않으며, 아래 기존 GPU192 근거와 구분한다.

이전 관리 .NET11 검증: 기본 입력·수명 경계27 + 실제 COM ABI12 = CPU39, GPU192(밝기·세 크기·두 방향·두 표현·MSAA1/4·획득 이미지 재사용과 독립 지정 지시선 색 표본), 관찰 CPU29/GPU30, 기존 접근성49·컨테이너179·스케줄링35 통과. GPU는 Duxel 소비 경로 검증이며 공식 브라우저 전체 픽셀 일치나 성능 증거가 아니다. 근거는 `artifacts/fluent-tablist-2026-11-11/verification.json`이다.

기준은 [공식 사용 지침](https://fluent2.microsoft.design/components/web/react/core/tablist/usage), Fluent UI 커밋 `8830c10bfb2f4c1bdd540287b09713b9679cd1ad`의 react-tabs와 [Selection](https://learn.microsoft.com/en-us/windows/win32/api/uiautomationcore/nn-uiautomationcore-iselectionprovider)·[SelectionItem](https://learn.microsoft.com/en-us/windows/win32/api/uiautomationcore/nn-uiautomationcore-iselectionitemprovider) 명세다. 참고 원문과 해시는 같은 산출물 폴더에 보존했다.

기본 탭에 [아이콘 콘텐츠와 원형 표현](fluent2-tab-content.ko.md)을 추가했다. 넘침 메뉴·지시선 전환 애니메이션·강제 색상·공식 전체 외관과 현재 NativeAOT/런타임 비교는 미완료다. 노출 이름은35개(상위16·변형19), 상위55개 중 미연결39개이며 전체 Fluent 준수의 분모는 미확정이다.


## 본문 포커스 경계 — 2026-11-11

선택 본문의 포커스 테두리는 호출자 콘텐츠의 가시 영역 안에 그립니다. 루트 여백과 자식 클립을 반영해 우측·하단이 화면 밖으로 잘리지 않습니다. 긴 본문은 표시 가능한 패널 경계를 사용합니다. 짧은 본문의 실제 높이와 탭 → 본문 → 컨트롤 탐색 순서는 유지합니다. 근거: artifacts/observer-idle-panel-2026-11-11/verification.json, MSAA1/4의 네 변 GPU 검사16개와 실제 관찰 창 GPU43개.


## 선택 폭 예약 — 2026-11-12

`ReserveSelectedSpace`가 켜져 있으면 선택 여부와 관계없이 보통 굵기와 중간 굵기 텍스트 측정값의 최댓값을 예약합니다. 앱이 서로 다른 글꼴을 설정하면 보통 굵기의 폭이 더 클 수 있으므로 선택 시 예약된 탭 폭이 줄어들면 안 됩니다. 예약을 끄면 선택 상태에 따른 측정을 유지합니다. 모든 크기·방향·외관에 적용하며, 이번 수정에서 자연 크기 캐시를 새로 추가하지는 않습니다.

## 제한된 자연 크기 측정 — 2026-11-12

`IUiTabListPresentation.CanRetainMeasurement`의 기본값은 false입니다. 참여하는 표현은 부모 제약과 독립적인 자연 크기를 불변 항목·선택·옵션·텍스트 환경으로 결정하며, `ReserveSelectedSpace`가 켜지면 선택과 독립적인 크기를 반환해야 합니다. 내장 Fluent는 이 계약을 선언합니다. 위젯은 최대256개 UiVector2(순수 값2048바이트) 버퍼 하나를 선택적으로 유지합니다. 더 큰 목록·글리프 관찰·상한을 넘는 키 문자열에서는 버퍼를 유지하지 않고 원래대로 측정합니다. 이 상한은 추가 측정 캐시에 해당하며 기존 항목·입력·배치 저장소 전체의 상한은 아닙니다.

키는 리소스 식별자·타이포그래피와 텍스트 설정·옵션, 예약을 끈 경우의 선택을 포함합니다. 항목·속성 무효화는 키 참조를 해제하고 Dispose는 버퍼를 해제합니다. 실패하거나 측정 중 무효화된 시도는 캐시에 등록하지 않습니다. 헤더 사각형과 오버플로 가시성은 매번 자연 크기에서 다시 계산하므로 숨김·RTL·선택된 오버플로 위치가 캐시를 오염시키지 않습니다. 오버플로 표시 측정과 실제 입력·그리기·팝업 동작·패널은 계속 처리합니다. 폭 예약이 켜진 일반 탭은 기존 조상 측정 캐시에도 참여하며 비예약·오버플로 탭은 조상에 대해 동적으로 유지합니다.

./run-gallery.ps1 → 측정 재사용에서 실제 탭·프로세스 단위 진단 측정 수·선택·크기 변경 버튼을 확인합니다. 선택에서는 측정 수를 유지하고 크기 변경에서는 무효화합니다. 진단 수치는 주기 프레임을 요청하지 않습니다. 전체 앱 시간·메모리 및 고정 RC3 NativeAOT 검증은 별도로 남아 있습니다. 근거: artifacts/tab-measurement-retention-2026-11-12/verification.json.


### 드래그로 이동하는 탭

`UiTabListOptions.Scroll = new UiTabListScrollOptions()`로 가로 헤더 스크롤을 켭니다. 기존 `Overflow` 메뉴 할당 방식과는 별도 선택이며 두 옵션을 동시에 켜지 않습니다. 상단 관찰 페이지는 이 스크롤 방식을 사용합니다.

탭 영역을 드래그하거나 휠로 움직입니다. 가려진 탭이 있는 쪽에만 `…`이 나타나므로 왼쪽·오른쪽·양쪽 표시가 가능합니다. 점 메뉴를 누르면 해당 방향의 탭 목록이 열리며 위치는 그대로 유지합니다. 항목을 고르면 원래 탭·본문을 선택하고 표시합니다. 가장자리 탭을 누르거나 키보드로 이동하면 선택한 탭뿐 아니라 그 방향의 이웃 탭 하나도 노출해 탐색을 이어갈 수 있습니다. 드래그를 놓는 동작은 탭 클릭으로 처리하지 않습니다.

하단 스크롤바는 별도 영역을 차지하지 않는 오버레이입니다. 기본120ms 나타남·400ms 정지 대기·160ms 사라짐을 적용하며 드래그 중에는 표시를 유지합니다. 공통 `UiScrollbarMotionOptions`로 시간·곡선·감소 모션을 설정하고, 탭별 재정의는 `UiTabListScrollOptions.Motion`으로 지정합니다. 애니메이션이 끝나면 다음 프레임 예약을 멈춥니다. [공통 전환 설계와 API](finite-transitions.ko.md)를 참조하세요.


전체 스크롤바는 동일한 [공통 모션 모듈](scrollbar-motion.ko.md)을 사용합니다.

UiTabOption.Badge에 UiTabBadge(Text, Options)를 지정할 수 있습니다. Fluent는 공통 Badge 경로로 뒤쪽 배지를 측정·그리며 배지 문구를 접근성 도움말로 제공합니다. 세로 항목은 실제 할당 너비를 사용하므로 라벨이 잘려도 상태 배지를 표시할 수 있습니다. 기존 텍스트 전용 사용자 표현은 유지하며 아이콘·배지는 항목 오버로드가 필요합니다. 갤러리는 미구현 항목에 Small/Informative/Tint 배지를 사용합니다. 기존 TabList 전체 적합성의 미완료 범위는 유지합니다.
