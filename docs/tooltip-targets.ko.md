# 컴파일된 Tooltip 대상

Last synced: 2026-11-11

현재 [공유 배치](tooltip-positioning.ko.md)는 프레임 배치/옵션, 12개 논리 위치·RTL·화면 경계 충돌·사용자 화살표 기준점을 추가한다. 표현 시그니처는 문서의 배치 계약을 입력/출력한다.

현재 [표면 노드·이벤트](tooltip-events.ko.md)는 실제 대상 아래 ToolTip 역할, 열림/닫힘과 제거 중 비동기 이벤트 수명을 지원한다.

현재 [대상 접근성](tooltip-accessibility.ko.md)은 설명·레이블·비노출 관계, Field 보존과 별도 Windows UIA 클라이언트 검증을 추가했다. 중첩 대상은 가장 안쪽 표면과 의미를 일치시키며 텍스트는 대상 프레임 단위로 확보한다.

`UiTooltip<TBehavior, TPresentation>`은 실제 대상을 소유하고 자연 측정과 부모 할당을 유지한다. 동작은 요청을 선택하고 표현은 표면 배치와 그리기를 제공한다. `UiHoverFocusTooltipBehavior`는 대상 호버, 탐색 가능한 자손 포커스와 열린 표면으로의 포인터 이동을 지원한다. 비활성 대상도 호버로 설명할 수 있다.

`Visible = null`이면 창은 대상 렌더링 후 자동 표시 소유자 하나를 확정한다. 기본 표시·해제 지연은 각각 0.25초이며 프레임 예약 시계를 사용한다. 표시 중인 대상에서의 전환은 즉시 처리한다. 대상이나 표면으로 돌아오면 해제를 취소한다. Escape는 자식 처리 전에 이벤트를 소비하고 포커스를 유지하며 트리거 이탈 전 자동 재표시를 막는다. 열린 Tooltip이 없으면 이후 Escape는 자식이 다시 사용한다.

첫 렌더링 전에 `Visible = false` 또는 `Visible = true`로 설정하면 호출자가 표시 여부를 제어한다. 첫 렌더링에서 모드가 고정된다. `VisibleChangeRequested`는 요청 값과 `Interaction`, `Escape`, `Handover`, `HostHidden` 사유를 전달한다. `Visible = change.Visible`로 요청을 수락하면 다음 프레임에서 반영된다. 외부 값에는 상호작용 지연을 적용하지 않는다. 요청을 거절하면 `Visible`을 유지하며 전환 요청을 거절하면 제어 표면 여러 개가 열릴 수 있다. Escape는 아직 표시 중인 Tooltip 중 가장 먼저 등록된 곳에 전달한다. `IsOpen`은 실제 표시 상태다. 누락, 완전 클립, 상위 팝업, 컨텍스트 이동과 폐기는 표시 상태를 해제하며 살아 있는 위젯의 외부 값은 유지한다.

Windows에서 호스트 창을 최소화하거나 숨기면 Tooltip 표면이 그리기와 접근성 트리에서 빠지고 `HostHidden` 해제 요청을 보낸다. 최소화로 그릴 영역이 사라지면 직전 배치 크기로 가시성 변화를 한 번 처리하고 GPU 프레임을 제출하지 않는다. 복원할 때는 호출자의 최신 `Visible` 값을 따른다. 자동 소유자는 트리거를 떠날 때까지 다시 열리지 않는다. 두 렌더링 사이에 창을 숨겼다가 복원한 경우에도 `UiHostVisibility.HideSequence`로 숨김을 감지한다. 다른 창이 덮은 경우에는 Windows 창 표시 상태를 유지한다. 플랫폼은 `IPlatformBackend.HostVisibility`를 제공하며 직접 프레임을 만드는 호스트는 `UiFrameInfo.HostVisibility`에 값을 설정할 수 있다.

표면은 팝업 계층과 프레임 루트 클립을 사용하므로 대상이 보이는 동안 조상 클립을 벗어난다. 레이아웃 커서는 이동하지 않는다. 대상 입력·그리기는 공유 자식 할당 제약을 사용한다. 잘못된 표면 기하는 명시적으로 실패한다. 측정은 팝업 등록이나 입력 소비를 하지 않는다. 프레임 사이에 `Text`·`Options`를 변경할 수 있으며 nullable `ShowDelaySeconds`·`HideDelaySeconds`는 유한한 비음수이고 null이면 기본값이다. 래퍼는 자식을 소유·폐기한다. 창 소유권에는 유휴 대상 사전을 사용하지 않으며 새 경로의 할당량·속도는 아직 측정하지 않았다.

`layout_custom_contract_fba.cs`의 Footer 버튼은 Fluent 표면과 사용자 오프셋을 적용한 `ConsumerTooltip`을 사용한다. `UiWidgetStyle(typeof(UiTooltip<,>), ...)`는 닫힌 타입 쌍으로 생성자를 전달한다. 샘플은 받은 값을 대입해 변경 요청을 수락하며 프로그램에서 지정한 표시 전환도 실행한다. Footer를 호버하거나 Tab으로 포커스하고 Escape로 닫는다.

```powershell
./run-fba.ps1 samples/fba/layout_custom_contract_fba.cs -NoCache
```

이전 소유권 단계에서는 고정 .NET11 RC3에서 빌드 경고·오류0, CPU473에 신규29검사를 포함한다. Tooltip GPU224개는 밝은·어두운 테마, 래스터·벡터 대상, MSAA1/4와 반복 프레임에서 통과했다. 독립 직접 그리기 이미지로 대상 전환·Escape·클립·비활성·누락·대기를 확인했다. 기존 입력400·FluentInput1362·다중 행2560·컨테이너237·할당 경로2317·접근성40·수명101도 통과했다. 이전 입력693·다중 행3857이미지는 정확히 일치한다. 샘플은 빌드 후 15초 자동 연습에서 정상 종료했다. 근거는 `artifacts/tooltip-owner-2026-11-11`이다.

`Fluent.Tooltip`은 아직 없다. 프로그램 포커스 출처·브라우저 문서 가시성, 전체 포인터 이벤트 동등성, 전체 속성 변경·종료 알림, 다중 주 대상 관계와 현재 Native·성능 검증은 남아 있다. 탐색 등록이 없는 기존 활성 편집기는 검증한 호버·Escape 경로를 사용한다. [공식 계약](fluent2-tooltip.ko.md)을 따른다.
