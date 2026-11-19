# Toolbar 도구 모음

```csharp
var bold = new UiState<bool>(false);
var toolbar = Fluent.Toolbar("Document tools", [
    Fluent.Button("Save", Save),
    Fluent.ToggleButton("Bold", bold.Binding),
    Fluent.Button("Print", Print)
]);
toolbar.Options = new(UiToolbarSize.Large, Vertical: true, RightToLeft: false);
```

일반 위젯을 소유하고 줄바꿈 없는 한 행 또는 한 열로 배치합니다. 기존 자식 배치·컨트롤 입력·포커스·접근성 경로를 사용합니다. Size는 루트 여백을 바꾸며 자식의 타이포그래피·크기·외관·바인딩은 명시적으로 유지합니다. 관찰 샘플처럼 은은한 명령 버튼을 쓰려면 `UiButton<UiReleaseButtonBehavior, UiFluentButtonPresentation<UiFluentSubtleButtonAppearance>>`를 사용할 수 있습니다. Button·ToggleButton·Input·Slider와 레이아웃 하위 컨트롤은 실제 의미를 유지하며 루트에는 Invoke·Value·포커스 패턴을 추가하지 않습니다. 자식 인스턴스는 한 소유자에만 속하고 도구 모음 해제 시 함께 해제됩니다.

Tab은 사용했던 유효 자식 하나로 들어가고 다음 Tab은 밖으로 나갑니다. 방향키는 두 축 모두에서 순환 이동하며 Home·End는 처음·마지막을 선택합니다. 고정 공식 훅처럼 가로 도구 모음에서도 위·아래를 지원합니다. RTL은 좌우 방향키와 배치를 반전합니다. 포인터·네이티브 포커스 이동도 다음 진입 위치에 반영합니다. 비활성·보이지 않는 컨트롤은 정지점을 등록하지 않으며 포커스된 자식이 제거·비활성화되면 유효한 자식으로 복귀합니다. 전체 비활성화·빈 도구 모음에는 자식 진입점이 없습니다. 공통 범위·편집 키 정책으로 등록된 컨트롤의 커서·값 조정 방향키는 유지합니다. 편집기 경계 판단과 중첩 복합 그룹을 포함한 Tabster 전체 이동은 검증되지 않았으므로 그런 컨트롤의 배치를 명시적으로 선택하세요. Toolbar 중첩은 명시적으로 예외를 발생시킵니다. 일반 자식 레이아웃 그룹은 사용할 수 있습니다.

가로 Small·Medium·Large의 세로×가로 여백은0×4·4×8·4×20입니다. 세로 도구 모음은 크기와 무관하게4×8이며 고정 스타일과 일치합니다. 자식은 교차 축 중앙에 배치하고 RTL의 논리 순서를 유지하며 부모 경계에서 자릅니다. 자동 줄바꿈·더 보기 메뉴·임의 Flex 부모 데이터는 제공하지 않습니다. 사용자 정의 표현은 `IUiToolbarPresentation.Padding`의 유한한 음수 아닌 여백을 사용합니다. 옵션 변경은 상위 측정을 무효화하고 바뀌지 않은 배치는 기존 캐시에서 재사용합니다. 구성원·키 버퍼도 재사용하며 새 렌더러·셰이더·네이티브 import·파서·타이머·정량 성능 주장을 추가하지 않습니다.

UIA에는 Toolbar50021·`Duxel.Toolbar`·`toolbar` ARIA 역할·현재 방향1/2와 실제 하위 Invoke·Toggle·Value를 게시합니다. 방향 변경 이벤트·루트 선택 값 사전·전용 Toolbar 자식 변형·실제 화면 읽기 프로그램·강제 색상·CSS/Tabster 전체 적합성은 초기 API 범위에 없습니다. 선택 상태는 기존 자식 바인딩이나 호출자 상태의 Map으로 구성합니다.

`./run-gallery.ps1` → **도구 모음**에서 방향·여백 크기·RTL·명령·굵게 토글을 확인하세요. 탭이 숨겨졌으면 상단 더 보기에서 선택합니다. Tab·Shift+Tab·방향키·Home·End·Enter·Space를 사용하고 다른 페이지를 왕복해도 상태가 유지되는지 확인할 수 있습니다. 근거는 `artifacts/toolbar-2026-11-13/`, 큰 GPU 결과는 `L:/Cache/Duxel/toolbar-2026-11-13-evidence/`입니다. 관리형 검증과 고정 RC3 NativeAOT·런타임 이미지·실기기·성능 검증을 구분합니다.

고정 소스: [동작](https://raw.githubusercontent.com/microsoft/fluentui/8830c10bfb2f4c1bdd540287b09713b9679cd1ad/packages/react-components/react-toolbar/library/src/components/Toolbar/useToolbar.ts), [스타일](https://raw.githubusercontent.com/microsoft/fluentui/8830c10bfb2f4c1bdd540287b09713b9679cd1ad/packages/react-components/react-toolbar/library/src/components/Toolbar/useToolbarStyles.styles.ts), [버튼 기본값](https://raw.githubusercontent.com/microsoft/fluentui/8830c10bfb2f4c1bdd540287b09713b9679cd1ad/packages/react-components/react-toolbar/library/src/components/ToolbarButton/useToolbarButton.ts).
