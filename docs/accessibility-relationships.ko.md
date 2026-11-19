# 접근성 관계 변경

Last synced: 2026-11-12

[독립 Label](label-widgets.ko.md)은 따로 렌더링한 두 위젯이 현재 프레임에 있을 때 실제 Text 공급자와 `LabeledBy` 관계를 제공합니다. 캡처 종료 때 주 대상만 연결하므로 렌더 순서에 의존하지 않으며 라벨 생략·해제로 없는 요소를 참조하지 않습니다. 기존 게시 이후 관계 이벤트가 연결 해제도 전달합니다. 근거: `artifacts/label-relationships-2026-11-12/verification.json`.

`UiAccessibilityTree.RelationshipChanged`는 유지되는 컨트롤의 `LabeledBy`나 `ControlledElementId`가 바뀌었음을 알린다. `RenderPanel`이 선택한 탭과 본문에 이 관계를 자동으로 연결한다. 기존 관찰 샘플의 열 페이지가 사용하므로, 이전 관찰 창을 닫고 `./run-gallery.ps1`로 실행한다.

```csharp
tree.RelationshipChanged += change =>
{
    Console.WriteLine($"{change.Property} : {change.Previous?.Element.Node.Id} -> {change.Current?.Element.Node.Id}");
};
```

`UiAccessibilityRelationshipChange`는 게시된 원본 컨트롤, 속성, 이전 대상과 새 대상을 담는다. 대상이 null이면 관계가 없다는 뜻이다. `UiAccessibilityRelationshipTarget.Ancestry`는 대상부터 부모 순서로 보관한 불변 스냅샷이다. 이전 대상과 조상은 실제 트리에서 제거된 뒤에도 읽을 수 있다. 새 대상 정보는 원본과 같은 게시 상태에서 가져온다. 콜백은 트리 게시 후 읽기 잠금 밖에서 실행한다. 읽기·작업 큐 입력은 허용하지만 게시·폐기에 재진입할 수 없다.

두 관계 ID는 해당 프레임에 캡처한 컨트롤을 가리켜야 한다. 없는 대상을 조용히 무시하지 않고 게시 전에 실패한다. 변경 알림을 준비하며 발견한 조상 순환도 반복 탐색으로 게시 전에 거부한다. 최초 탐색·값 유지·원본 제거에서는 속성 변경 이벤트를 만들지 않는다. 프레임 취소는 준비된 알림을 비운다.

| 필드 | Windows 속성 | 공급자 값 | 클라이언트 값 |
| --- | --- | --- | --- |
| `LabeledBy` | 30018 | `VT_UNKNOWN`, 해제하면 null | 실제 `IUIAutomationElement` 또는 null |
| `ControlledElementId` | 30104 | `VT_ARRAY \| VT_UNKNOWN`, 해제하면 빈 배열 | `IUIAutomationElementArray` |

현재 모델은 제어 대상 하나를 지정한다. 관계가 없으면 Windows에 정의된 null·빈 배열을 반환한다. 속성 이벤트는 실제 원본과 살아 있는 새 대상을 전달한다. 새 대상의 실행 패턴을 유지하며 읽기 전용 스냅샷으로 바꾸지 않는다. 이전 값 공급자는 제거된 대상의 식별자와 조상을 보존해 UIA가 처리할 수 있게 한다. 임시 포인터와 VARIANT는 네이티브 알림 호출 뒤 해제한다. 제품 경로에 대기·타이머·프레임 요청을 추가하지 않는다.

소유한 네이티브 검사 창에서 두 관계를 이전 대상 → 새 대상 → 해제 → 같은 키 복원 순서로 바꾸고 원본을 제거한다. 속성 이벤트 여섯 건을 받고, 이벤트로 받은 새 대상을 두 번 실제 호출하며 이전 대상은 계속 사용할 수 없음을 확인한다. 네이티브 Invoke는 비동기 큐 요청이므로 실제 원본 제거를 관찰한 뒤 종료 상태를 검사한다. 처음의 성급한 검사 실패와 호스트 기한 실패 로그도 보존한다.

근거는 `artifacts/relationship-events-2026-11-11/verification.json`이다. 해당 폴더에서 roll-forward를 끄고 SDK11.0.100을 고정하며 런타임은11.0.0이다. 관리 실행의 네이티브 연동 검증으로, 미실행 RC3 NativeAOT·런타임 이미지 비교와 구분한다. 이 검사는 실제 HWND에 접근성 트리를 직접 게시하며 GPU 화면을 그리지 않는다. 생성된 탭·본문은 별도 외부 통합 검사로 확인한다. 기존 GPU 근거는 TabPanel 검증에 남긴다.

현재 검사: 코어 관계17개(TabList 전체94개), 네이티브 관계 클라이언트12/호스트4개, 실제 생성 탭 클라이언트25/호스트3개, 관찰39·접근성49·스케줄링35·기존 API10·다중 행/Tooltip/편집4114개 통과. 실제 탭 검사에서는 본문 전환·해제·복원 때 ControllerFor 네 건을 받고 탭 제거 뒤 죽은 원본의 이벤트가 추가되지 않음을 확인한다. 사용자는 이전 Immediate 동작이 개선된 것 같다고 확인했으며, 이 수동 피드백은 전체 데스크톱 입력 검증과 구분한다.

범위는 위 두 필드다. [네이티브 포커스 이벤트](accessibility-focus.ko.md)는 별도로 검증했다. 다른 관계 종류·화면 읽기 음성·전체 Fluent 상태·NativeAOT는 남아 있다. 관계가 바뀔 때 대상 스냅샷을 할당한다. 변화 없는 작은 트리의 검사는 관찰자를 연결한 워밍업 뒤 관리 할당0만 확인하며 전체 UI·GPU 비용을 대신하지 않는다.

공식 기준: [속성 식별자](https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-automation-element-propids), [속성 변경 알림](https://learn.microsoft.com/en-us/windows/win32/api/uiautomationcoreapi/nf-uiautomationcoreapi-uiaraiseautomationpropertychangedevent).
