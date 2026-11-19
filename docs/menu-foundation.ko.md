# 공통 메뉴 동작

최신 표현 기준: [Fluent Menu](fluent2-menu.ko.md)가 생성 팩터리·체크 항목·스크롤·확장 이벤트를 공유 팝업 경로에 연결합니다. [TabList 넘침](fluent2-tab-overflow.ko.md)도 같은 경로를 사용합니다. 아래 초기 검증 수치는 당시 기록이며 Fluent 전체 일치는 남아 있습니다.

갤러리의 **기존 API**에서 쓰는 즉시 모드 `MenuItem(label, selected)`은 체크 표시 자리 16 DIP와 간격 4 DIP를 확보합니다. 선택하면 공통 Fluent 벡터 체크 표시가 나타납니다. 글꼴에 U+2713 문자가 없어도 체크 표시가 그려지며 메뉴 이름에는 지정한 글꼴을 사용합니다.

Windows .NET 11 Vulkan 검사에서는 먼저 DirectWrite의 글리프 누락을 재현했습니다. 밝은·어두운 테마와 MSAA 1×·4×에서 미선택·선택·선택 해제 프레임을 읽었습니다. 관리형과 x64 NativeAOT에서 각각 13건이 통과했고 이미지 8장이 일치했습니다. 근거는 `W:/Cache/Duxel/gallery-source-20261117/legacy-menu-gpu-1961/verification.json`입니다.

Last synced: 2026-11-11

`Dux.Menu`는 트리거·고유 문자열 값의 항목·동작 콜백 하나를 가진 메뉴를 소유합니다. `UiMenu<TPresentation>`는 정적 측정·그리기와 공통 팝업·닫기·입력·접근성 경로를 분리합니다. `UiThemeMenuPresentation`은 현재 테마의 표면과 기존 Fluent 버튼 트리거를 사용하므로 DirectText와 글꼴 메트릭 백엔드가 필요합니다. Fluent 외관·체크/라디오 항목·아이콘/보조 내용·첫 글자 탐색·긴 메뉴 스크롤·TabList 넘침이 이 기반을 공유합니다. 하위 메뉴·여러 줄 하위 문구는 남아 있습니다. 고정 Fluent MenuList 소스는 첫 글자 순환을 사용합니다.

```csharp
using var menu = Dux.Menu("Actions",
    [new("open", "Open"), new("disabled", "Unavailable", false), new("save", "Save")],
    value => Console.WriteLine(value));
// Render menu inside the caller's ordinary widget flow.
```

포커스가 있는 트리거에서 아래·위 방향키를 누르면 처음·마지막 활성 항목으로 엽니다. Enter·Space로도 엽니다. 위·아래 방향키는 비활성 항목을 건너뛰며 순환하고 Home·End는 양 끝으로 이동합니다. Enter는 즉시 실행하며 Space는 놓을 때 실행합니다. Escape는 누른 Space 동작을 취소하고 닫은 뒤 트리거로 포커스를 돌립니다. Tab은 닫은 뒤 열기 전의 탭 순서로 진행합니다. 포인터는 놓을 때 실행하며 바깥을 누르면 닫습니다. 다른 공통 메뉴를 열면 이전 메뉴가 닫히고 소유자를 제거하면 프레임 종료 때 팝업이 닫힙니다. 메뉴 애니메이션이나 주기적 프레임 타이머는 추가하지 않았습니다.

`SetItems(ReadOnlySpan<UiMenuOption>)`는 항목을 복사·검증한 뒤 상태를 교체하고, 가능하면 고유 값의 포커스를 유지하며 누르던 동작은 취소합니다. 빈 목록은 팝업을 열지 않습니다. 중복·빈 값·빈 라벨과 잘못된 크기는 명시적으로 실패합니다. `UiMenuOptions`는 최소 폭·행 높이·여백·논리적 RTL 위치를 제어합니다. 팝업은 공통 위치 계산과 팝업 그리기 계층을 사용하고 부모 클립 밖에도 표시됩니다. 화면보다 큰 표면은 클립 영역과 공통 스크롤바를 사용합니다. 체크 바인딩과 열린 상태를 유지하는 계약은 최신 Fluent 메뉴 지침에서 설명합니다.

Menu/MenuItem 접근성은 UIA 컨트롤 형식 50009/50011과 말단 항목의 Invoke를 사용합니다. 비활성 항목은 동작을 거부합니다. 닫으면 항목 제공자가 무효화되고 다시 열면 새 런타임 식별자를 받으며 이전 클라이언트는 계속 사용할 수 없습니다. 최신 지침에서 Scroll/ScrollItem·트리거 확장·메뉴 열기/닫기 이벤트·체크 Toggle의 검증 범위를 설명합니다. 전체 보조기술 검증은 남아 있습니다. 컨트롤 형식은 [Microsoft 식별자 명세](https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-controltype-ids)를 따릅니다.

`./run-gallery.ps1` 실행 → **컨테이너** → **Menu 공통 동작**까지 스크롤 → **공통 메뉴 열기**를 누르세요. 활성 동작을 고르면 선택 값 문구가 바뀝니다. 옆의 넘침 예제는 별개이며 그 배지를 탭 메뉴로 바꾼 상태는 아닙니다.

검증은 `artifacts/menu-2026-11-11/verification.json`에 기록합니다. 관리 SDK 11.0.100/런타임 11.0.0 고정: CPU 21·GPU 20(MSAA 1/4, 팝업 그리기·실제 포인터 호출·제거·획득 이미지 재사용)·외부 Windows UIA 클라이언트 4/호스트 2·관찰 샘플 51검사입니다. 접근성 49·탭 목록 197·예약 35·넘침 26 회귀 검사도 포함합니다. 초기 컴파일·타이포그래피 범위·GPU 글꼴 설정 실패는 수정 결과와 함께 보존합니다. 포커스 회귀에서 이전 팝업 차단 정보의 잔류와 트리거 누름 소유권 결함을 찾았으며 공통 등록은 닫는 메뉴 자신의 현재 사각형만 제거합니다. NativeAOT 검증이나 일반적인 성능 개선을 주장하지 않습니다.

시각 검사에서 이전의 위치 중심 캡처에 메뉴 항목 글자가 보이지 않는 결함을 확인했습니다. 항목 측정과 그리기는 같은 Regular Type300 타이포그래피 범위를 사용하고 비활성 글자는 테마의 비활성 색을 사용합니다. 추가한 글자 픽셀 검사는 이전 자리 표시자 결과를 거부합니다. 최종 읽기 가능한 이미지와 비교는 L:/Cache/Duxel/menu-2026-11-11/gpu-readable에 보존합니다.
