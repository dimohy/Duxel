# 목록

Linux 네이티브 Selection은 공통 자식 선택 동작을 사용하며 전체 자식 인덱스와 선택된 항목의 인덱스를 구분합니다. 자식 상태와 부모 선택 이벤트가 외부 캐시를 갱신합니다. 공통 동작 접수 단계에서 필수 소유자의 마지막 선택 항목을 보호하며 여러 필수 선택 항목 중 하나는 해제할 수 있습니다. Linux 샘플 하단 도시 목록으로 단일·다중 선택을 확인하세요. 현재 앱35건·실행 경로별 네이티브48건·정책7건·컨트롤99건의 근거는 `artifacts/linux-accessibility-2026-11-14/selection-verification.json`입니다. 전체 위젯·실제 환경 검증은 남아 있습니다. 일괄 요청은 알려진 거부 조건을 먼저 확인한 후 현재 바인딩에 순서대로 반영하므로 이후 거부나 동시 계층 변경이 발생하면 앞서 반영한 변경은 유지됩니다.

`Fluent.List`는 `UiList<UiFluentListPresentation>`을 상속하는 생성 위젯과 직접 소유하는 `UiListItem` 행을 제공합니다. 일반·단일·다중 선택의 세 생성 오버로드가 있습니다. 카탈로그는 팩터리59개·공식 상위35/55 연결·20개 미연결이며 전체 Fluent 적합성 완료를 뜻하지 않습니다.

```csharp
var selected = new UiState<string?>("a");
var list = Fluent.List("문서", selected.Binding,
[
    new("a", "첫 문서", Fluent.Body1("첫 문서"), selectionLabel: "첫 문서 선택"),
    new("b", "둘째 문서", Fluent.Body1("둘째 문서"), selectionLabel: "둘째 문서 선택")
], new(Width: 320));
```

바인딩을 생략하면 일반 목록이며 `UiBinding<string[]>`을 사용하면 다중 선택입니다. 행 값은 중복되지 않는 안정적인 식별자여야 합니다. 선택은 기존 `UiOptionSelection`을 재사용하며 거부한 쓰기는 다시 읽습니다. 단일 선택도 해제할 수 있습니다. 행 콘텐츠는 목록과 함께 소유하고 해제합니다.

탐색 방식은 Off·Items·Composite입니다. 선택하면서 Off를 지정하면 Items로 동작합니다. Off는 자식 컨트롤의 독립 탐색을 유지합니다. Items는 비대화형 콘텐츠를 사용하며 입력·버튼은 Composite에서 사용합니다. 세로 방향키·Home·End로 행을 이동하고 Tab 진입점 하나와 마지막 포커스를 유지합니다. Composite의 가로 방향키는 자식으로 진입하거나 자식 사이를 이동하고 Escape는 행으로 돌아옵니다. 방향키를 사용하는 편집기는 편집 키를 유지합니다. Tab은 목록 밖으로 이동합니다.

Space는 행 실행 없이 선택을 전환합니다. Enter·행 클릭은 `Action`을 먼저 실행하고 `UiListItemAction.PreventSelection`으로 기본 선택을 막을 수 있습니다. 체크박스와 자식은 행 실행을 유발하지 않습니다. `DisabledSelection`은 선택을 막으며 명시한 행 실행은 계속 제공할 수 있습니다. 네이티브 선택 요청도 같은 바인딩을 사용합니다.

실행: `./run-gallery.ps1` → **목록**. 일반·단일·다중 선택, 실제 입력, 행 실행과 독립 자식 명령을 확인하세요. 세 예제 모두 생성 팩터리를 사용합니다.

현재 네이티브 구조는 List·ListItem, ListBox·Option, Grid·Row·GridCell 역할과 선택 의미 관계를 연결합니다. 복합 목록은 Grid·GridItem 패턴과 행·열 수, 논리 좌표, 차지하는 행·열 수, 소속 그리드를 제공합니다. 선택 셀이 있으면 논리0열이며 텍스트 방향에 따른 시작 쪽에 배치합니다. 본문은 다음 열을 사용합니다. 행은 전체 너비를 나타내고 GetItem은 실제 자식 셀을 반환합니다. 본문이 비어 있어도 셀 공급자는 유지합니다. 숨김·비활성·해제·컨텍스트 이전과 편집 키 전달은 집중 CPU 검사로 확인했고 실제 외부 UIA 클라이언트에서도 셀·선택·편집·비활성·숨김을 검증했습니다. 여러 DPI와 물리 스크린리더의 전체 검증은 남아 있습니다.

기준: 고정 버전의 [List 명세](https://github.com/microsoft/fluentui/blob/8830c10bfb2f4c1bdd540287b09713b9679cd1ad/packages/react-components/react-list/library/docs/Spec.md), [ListItem 동작](https://github.com/microsoft/fluentui/blob/8830c10bfb2f4c1bdd540287b09713b9679cd1ad/packages/react-components/react-list/library/src/components/ListItem/useListItem.tsx).

행은 자연 크기로 측정한 콘텐츠를32px 체크박스 클릭 영역 옆의 세로 중앙에 배치합니다. 복합 Row 안에도 CrossAxisAlignment = UiCrossAxisAlignment.Center를 지정하며 관찰 샘플은 이름·입력·버튼에 이를 적용합니다. 모든 행의 선택 변경을 처리한 다음 각 행의 클립과 활성 상태를 유지해 체크 표시를 한 번 그립니다. 뒤쪽 행을 선택해도 앞쪽 표시가 같은 프레임에 갱신됩니다.

사용자 정의 IUiListPresentation의 CanRetainMeasurement 기본값은 false이며 Fluent는 명시적으로 재사용을 허용합니다. 부모가 정한 체크박스 크기가 바뀌면 행과 소유 목록의 배치를 무효화하므로 가변 크기를 이전 기하로 재사용하지 않습니다.

목록은 모든 행의 포커스 요청과 Tab 진입점 복구가 끝난 다음 행 테두리를 그립니다. 뒤쪽 행으로 이동해도 같은 프레임에 이전 테두리를 지웁니다. 각 행의 클립을 유지하며 자식 편집기의 포커스 표시는 해당 편집기가 담당합니다.

`UiAccessibilityNode.Grid`와 `.GridItem`은 현재 제공하는 전체 그리드를 기술합니다. 음수 크기, 범위를 벗어난 영역, 겹치거나 빠진 셀, 조상 관계가 아닌 소속 그리드는 게시 전에 거부합니다. 셀 인덱스와 메타데이터를 함께 반영하며 중단된 프레임은 이전 상태를 유지합니다. 일반 셀은 좌표 사전으로 찾고 병합 셀은 사각형만 유지하므로 차지하는 좌표마다 메모리를 할당하지 않습니다. 행·열 수, 좌표, 차지하는 영역과 소속 그리드 변경 알림은 반영 후 전달합니다.

숨긴 공급자의 생존 여부는 요소와 `GetItem` 연산의 `UIA_E_ELEMENTNOTAVAILABLE`로 검증합니다. 현재 Windows 클라이언트에서는 해제된 Grid의 숫자 속성이 성공·0을 반환하는 현상을 관측했습니다. 공급자 자체의 행 수·속성 조회는 요소 없음 오류를 반환하는 별도 검사로 확인합니다. [UIA는 속성 기본값을 제공할 수 있으므로](https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-propertiesforclients), 숫자0만으로 살아 있는 빈 그리드라고 판단하지 않습니다. 해당 Windows 내부 구현 원인까지 확인했다는 뜻은 아닙니다.

