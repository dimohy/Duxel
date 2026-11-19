# HeadsUpDisplay

`./run-gallery.ps1` → **알림과 진행 상태 → HeadsUpDisplay**에서 실행합니다. 중앙 표시·입력 차단·자동 닫기와 네 상태의 본체를 확인하며, 예제마다 실제 설정에 맞는 코드가 바로 아래에 붙습니다. 최신 메뉴의 NEW 배지를 이 메뉴로 옮겼습니다. 상위 카탈로그는 47/55 연결·8개 미연결이며 전체 Fluent 적합성을 완료했다는 뜻은 아닙니다.

```csharp
var hud = Fluent.HeadsUpDisplay(new(Label : "저장 중"));
hud.Content.Tapped = () => hud.Hide();
var show = Fluent.Button("표시", hud.Show);
var example = Dux.Column(new(Spacing : 8, MainAxisSize : UiMainAxisSize.Min), show, hud);
```

`UiHeadsUpDisplayWidget`은 화면 중앙 포털을 소유하고 `UiHeadsUpDisplayContent`는 표시 본체를 소유합니다. 기본값은 입력 차단·명시적으로 닫을 때까지 유지입니다. `Show`/`Hide`와 `Open` 바인딩·`OpenChanged`를 제공합니다. `ShowSuccess`/`ShowFailure`는 상태를 바꾸고 1초 뒤 닫힙니다. `IsPersistent`가 false인 표시도 같은 수명을 사용합니다. 바인딩이 자동 닫기를 거부하면 같은 요청을 매 프레임 반복하지 않습니다. Escape나 배경 클릭으로 작업을 임의 취소하지 않습니다.

`IsBlocking`이 false이면 뒤쪽 컨트롤을 사용할 수 있고 포커스를 강제로 가져오지 않습니다. 차단 모드는 공통 오버레이의 키보드 포커스와 복원 규칙을 사용합니다. 시작은 1.3배→1배·투명→불투명, 닫기는 1배→.8배·불투명→투명으로 각각 200ms 선형 전환합니다. 닫기 시작과 함께 입력·접근성을 제거하고 마지막 그림만 재생하므로 사라지는 버튼을 실행할 수 없습니다. 새 스레드·타이머를 추가하지 않고 공통 프레임 예약을 사용합니다.

`KeyboardHeight`는 호스트가 제공하는 화면 키보드 높이이며, 값이 있으면 그 위에 50의 여유를 두고 남은 영역 중앙에 배치합니다. 갤러리의 영역 모의는 실제 키보드를 열지 않습니다. OS 화면 키보드 높이를 자동 감지하는 기능은 아닙니다. 전체 영역을 가렸을 때는 빈 GPU 캡처와 전환 프레임을 만들지 않습니다. 포털의 `ReducedMotion`은 본체의 회전에도 적용합니다.

본체의 자연 크기는 한 변이 100~192인 정사각형입니다. 아이콘은 36, 좌우 여백은 12, 문구와 아이콘 사이는 20입니다. 문구가 있으면 위·아래 여백은 20·16, 없으면 20·20입니다. 모서리는 반지름 4입니다. 부모가 더 작은 공간을 할당하면 자연 크기를 보존한 채 그 안에서 클리핑합니다. 할당 크기가 0이면 그리지 않고 애니메이션 프레임도 요청하지 않습니다.

문구는 가운데 정렬한 두 줄로 표시합니다. 공간이 부족하면 글꼴과 줄 높이를 함께 최대 60%까지 줄이고, 그래도 넘치면 말줄임표를 붙입니다. 결합 문자 경계에서 자르며 접근성 이름에는 원문을 유지합니다. 배치·줄바꿈 결과는 공통 측정 키로 보관하므로 같은 입력에서 프레임마다 다시 준비하지 않습니다. `UiControlContext.BeginTypography<TTypeface, TMetrics>(float scale, bool hasText = true)`는 기존 bool 오버로드를 보존하는 추가 API이며, Dispose 시 바깥의 글꼴과 줄 높이를 복원합니다.

Activity는 기존 ActivityIndicator의 원호와 시간축을 재사용합니다. 성공·실패에는 기존 Fluent 벡터 아이콘을 사용합니다. Custom은 `UiMenuIcon`을 받아 같은 36 크기와 회색 틴트를 전달합니다. 호출자가 제공한 아이콘도 전달된 색상·영역 계약을 따라야 합니다. 시스템 대비 색상이 일반 색상보다 우선합니다. `ReducedMotion`을 켜면 활동 중이라는 의미를 유지하면서 회전과 추가 프레임 요청을 멈춥니다.

`Tapped`가 없으면 읽기 전용 Image이며, 있으면 키보드·접근성으로 실행할 수 있는 Button입니다. 탭은 콜백만 실행하고 작업을 임의로 취소하지 않습니다. Activity의 Busy는 true, 나머지는 false입니다. `AccessibleName`으로 공통 이름을 덮어쓸 수 있습니다. 공통 포털의 차단·포커스 수명은 실행 검사로 확인했습니다. 실제 보조공학 도구의 안내 내용과 모든 OS 조합 검증은 남아 있습니다.

공식 iOS 토큰에서 밝은/어두운 배경은 grey14/grey24, 아이콘은 grey56/grey72, 문구는 흰색입니다. 현재 데스크톱 팔레트는 이 두 경우를 사용하며 iOS의 darkElevated 환경을 구현했다고 주장하지 않습니다. 실제 SwiftUI 화면과의 픽셀 일치, 전체 플랫폼 접근성, 화면 키보드를 피하는 배치는 남아 있습니다.

검증: 포털 CPU 20건·Vulkan 22건(MSAA 1·4), 실제 갤러리 경로 5건, 본체·소스 17건을 확인했습니다. 공통 퇴장 코드 변경은 Dialog 107건, 팩터리 연결은 카탈로그 4건으로 확인했습니다. 갤러리 소스 검사 하나는 Content와 ContentOptions 생성자를 혼동한 검사식이 원인이어서 수정 후 해당 두 항목만 다시 실행했습니다. Core 지문이 같아 이미 통과한 포털 검사는 반복하지 않았습니다. 범용 검사·종료 A/B·이전 ActivityIndicator 검사는 재실행하지 않았습니다. 최신 빌드·복구·실행 경로는 `artifacts/fluent-heads-up-display-portal-2026-11-17/result.json`을 따릅니다. 실제 Linux와 고정 RC3 NativeAOT는 미실행입니다. 이전 본체의 이모지 입력 중단은 선택된 검사 글꼴의 글리프 미지원이며 이모지 지원을 검증한 것이 아닙니다.

```text
Duxel.Graphics.Tests.exe <새 결과 폴더> hud-portal-only
Duxel.Graphics.Tests.exe <새 결과 폴더> hud-portal-gpu
Duxel.Graphics.Tests.exe <새 결과 폴더> hud-gallery-only
Duxel.Graphics.Tests.exe <새 결과 폴더> hud-gallery-source-only
```

공식 근거: [본체](https://github.com/microsoft/fluentui-apple/blob/main/Sources/FluentUI_iOS/Components/HUD/HeadsUpDisplay.swift), [토큰](https://github.com/microsoft/fluentui-apple/blob/main/Sources/FluentUI_iOS/Components/HUD/HeadsUpDisplayTokenSet.swift), [표시 수명](https://github.com/microsoft/fluentui-apple/blob/main/Sources/FluentUI_iOS/Components/HUD/HUD.swift), [색상](https://github.com/microsoft/fluentui-apple/blob/main/Sources/FluentUI_common/Core/Theme/Extensions/iOS/FluentTheme+iOS.swift).
