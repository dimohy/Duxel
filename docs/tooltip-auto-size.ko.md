# Tooltip 자동 크기와 넘침

Last synced: 2026-11-11

`UiPopupAutoSize.None/Width/Height/Both`로 `ArrangeMeasured`의 화면 크기 조정을 선택한다. 고정 크기 `Arrange`는 자동 크기를 거부한다. `UiPopupMeasureRequest`는 nullable Width/Height/MaxWidth/MaxHeight를 제공한다. 시작할 때 활성 축의 이전 인라인 크기/제한을 지운 뒤 대상 폭 맞춤·오프셋·후보 선택을 실행한다. 이동 뒤 가용 공간으로 제한과 필요한 명시 크기를 갱신한다. 실제 측정 크기가 바뀌면 좌표를 초기화하되 후보 이력·오프셋·화살표 상태를 유지한다. 화살표만의 재시작은 좌표를 유지하고 덮기/이동을 재실행한다. 음수 가용 CSS 크기는 무효 스타일처럼 무시한다.

마이그레이션: `IUiPopupMeasurement<T>.Measure`는 이제 `UiPopupContentMeasure(Size, ContentSize)`를 반환한다. Size는 유한한 양수 본문, ContentSize는 확정 폭으로 줄바꿈한 뒤의 유한한 비음수 스크롤 영역이다. 요청 크기/제한과 테두리/패딩의 고유 최소 크기를 반영한다. 인수 하나인 결과 생성자는 두 영역을 같게 설정한다. `UiPopupLayout.Measurement`는 최종 제약, ContentSize는 넘침을 제공한다. 샘플은 매 폭에서 실제 Type200 텍스트를 재측정하고 본문에 그리기를 클립한다.

표시 중인 Tooltip마다 `ScrollOffset`을 유지하고 콘텐츠 변경 뒤 제한하며 해제할 때 초기화한다. 포인터 호버와 두 휠 축은 이전 그리기에서 루트에 클립된 가장 앞쪽 Tooltip 본문을 사용한다. 같은 계층에서는 그리기 순서를 따른다. 더 움직이지 않는 축은 가려진 Tooltip을 스크롤하지 않고 바깥 영역에 남긴다. Escape 요청은 Tooltip 휠 처리보다 우선한다. 표현은 `ScrollOffset`만큼 콘텐츠를 이동하고 표시 영역에 클립해야 한다. `UiTooltipOptions.ScrollStep` 기본값은 논리 48단위다.

`UiTooltipOptions.ShowScrollbars = true`를 설정하면 콘텐츠가 넘치는 축에 스크롤바를 겹쳐 그린다. Tooltip의 표시 영역과 테마를 사용하며 측정 크기와 줄바꿈은 유지한다. 트랙을 눌러 스크롤하거나 손잡이를 표시 영역 밖까지 끌 수 있다. 놓기, 닫기, Escape 또는 해당 스크롤바가 사라질 때까지 Tooltip이 포인터를 소유한다. 사용자 정의 레이아웃 FBA가 이 옵션을 사용한다. 브라우저 스크롤바 기하는 남아 있다.

접근 가능한 Tooltip이 표시되고 내용이 넘치면 겹쳐 그리는 스크롤바의 표시 여부와 관계없이 Windows UIA Scroll 패턴을 제공한다. `Scroll`의 작은 이동은 `ScrollStep`, 큰 이동은 측정된 표시 영역을 사용한다. `SetScrollPercent`는 스크롤 가능한 축에서 0~100을 사용하며 `-1`은 해당 축을 유지한다. 그리기 전에 명령을 적용하고 같은 프레임에 새 위치를 게시한다. 내용이 표시 영역에 들어오거나 Tooltip을 닫으면 Scroll 패턴을 해제한다. [Microsoft Scroll 패턴](https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-implementingscroll).

Tooltip이 계속 표시되면 UIA는 새 스크롤 위치·뷰포트 비율·스크롤 가능 여부와 Scroll 패턴 제공 여부를 속성 변경 알림으로 받는다. 내용이 줄어 표시 영역에 들어오면 같은 Tooltip의 양축에서 위치 `-1`, 뷰포트 비율 `100`, 스크롤 가능 여부 `false`를 보고한다.

`Positioning.RightToLeft`를 사용하면 가로로 넘치는 내용이 오른쪽 끝에서 시작한다. UIA 위치 `0`은 오른쪽, `100`은 왼쪽이며 세로 오버레이 스크롤바는 왼쪽에 표시된다. 커스텀 프리젠터는 물리 좌표의 `UiPopupLayout.ScrollOffset`을 받아 콘텐츠 원점에서 빼서 그린다.

참고: 고정 [Fluent maxSize/resetMaxSize](https://github.com/microsoft/fluentui/blob/8830c10bfb2f4c1bdd540287b09713b9679cd1ad/packages/react-components/react-positioning/library/src/middleware/maxSize.ts)와 공개 core1.6.8. 원본 미들웨어를 단위 배율 사각형·CSS 유효성·고유 최소 크기·줄 재측정 모델에서 실행한다. 추가768사례는 크기4모드를 동일하게 포함하며 본문 크기 변경532개·넘침133개다. 최대 재시작14회였고 본문/화살표·콘텐츠·최종 제약이 허용오차0.001로 모두 일치했다. 기존2688사례도 유지한다. 실제 텍스트 휠/클립·끝 이벤트 전달·재열기 초기화를 포함해 CPU4022/Tooltip GPU620이 통과했다. 근거는 `artifacts/tooltip-auto-size-2026-11-11`이다.

51회를 넘어 재시작이 필요하면 명시적으로 실패한다. 예산 소진 뒤 마지막 상태를 반환하는 원문과 의도적으로 다르며, 기록한 참조는 한도에 도달하지 않았고 진동하는 생산자 검사로 거부를 확인했다. DOM 재조정/배율 탐색·전체 접근성 알림·Fluent 전체 준수·현재 NativeAOT/런타임 비교와 원래 전체 목표는 남아 있다. 성능 개선은 주장하지 않는다.
