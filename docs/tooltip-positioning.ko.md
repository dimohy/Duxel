# 공유 Tooltip 배치

2026-11-13: 공유 배치는 너비·높이가0인 가상 점 대상을 허용합니다. 음수·비유한 대상, 양수가 아닌 본문·경계 크기는 계속 거부합니다. [Popover 우클릭](fluent2-popover.ko.md)에서 실제 마우스 점 위치를 사용합니다.

Last synced: 2026-11-11

현재 [자동 크기/넘침](tooltip-auto-size.ko.md)은 상태를 유지한 재측정과 본문/전체 콘텐츠 영역을 제공한다.

현재 [측정 기반 배치](tooltip-measured-layout.ko.md)는 후보별 컴파일 오프셋과 실제 대상 폭 콘텐츠 재측정을 추가한다.

현재 [오프셋/덮기 지원](tooltip-offset-cover.ko.md)은 부호 있는 두 축 오프셋·논리 이동 패딩·원문 덮기/이동 제한을 추가한다. 기본 옵션은 아래 좌표를 유지한다.

`UiPopupPlacement.Arrange(target, size, boundary, options)`는 위·아래의 중앙/시작/끝과 논리적 앞·뒤의 중앙/위/아래, 총12개 위치를 제공한다. RTL은 앞·뒤와 수평 시작·끝을 반전한다. 기본값은 Above, 간격4, 화살표 없음, 화살표 패딩8이다. 화살표 높이는 본문 간격에 더하며 선택적인 `ArrowWidth`의 기본값은 높이의 두 배다. `UiPopupLayout`은 본문 사각형·확정 위치·선택적인 삼각형 기준점·`ArrowCenterOffset`을 담는다. `UiControlContext.Triangle`은 현재 클립을 따른다. 샘플은 둥근 Fluent CSS 화살표가 아닌 사용자 삼각형을 그린다.

고정하지 않으면 요청 위치, 정렬 반전, 원래 정렬의 반대쪽, 반전 정렬의 반대쪽 순으로 검사한다. 중앙 정렬은 요청/반대쪽만 검사한다. 비어 있지 않은 불변 `FallbackPositions`가 있으면 요청 위치 다음의 기본 후보를 대체한다. `FlipBoundary`와 `OverflowBoundary`로 선택/이동 경계를 각각 지정한다. 고정 Floating UI core1.6.8에 따라 표면이 향한 변과 양쪽 교차 변에 들어가는 첫 후보를 고른다. 없으면 향한 변에 들어가는 후보 중 부호를 유지한 정렬 변 넘침으로 고르고, 그 변에 들어가는 후보도 없을 때 세 변의 양의 넘침 합을 최소화한다. 동률은 순서를 유지한다. 본문은 변과 평행하게 이동하고 큰 크기는 유지한 채 클립한다. 화살표 패딩은 원문의 1단위 여유를 따르며 작은 정렬 대상에서는 본문도 한 번 이동한 뒤 경계를 다시 적용한다. 변보다 큰 화살표를 포함해 폭·높이를 명시값으로 유지한다. `ArrowCenterOffset`은 대상 중앙과의 남은 차이를 제공한다. 대상 표시 가능 여부는 Tooltip 소유자가 처리한다.

`UiTooltipOptions.Positioning`은 대상 그리기 전에 텍스트와 함께 확보한다. `IUiTooltipPresentation.Arrange`가 이 값을 받아 `UiPopupLayout`을 반환하며 `Paint`는 같은 배치 결과를 받는다. 뒤늦은 옵션 변경은 다음 프레임에 반영한다. 사용자 구현은 두 시그니처를 갱신하고 자체 좌표를 배치 결과로 감싸거나 공유 계산기를 호출해야 한다. `layout_custom_contract_fba.cs`는 AboveStart와 높이6 화살표를 사용한다.

참고: 고정된 [Fluent React 배치 소스](https://github.com/microsoft/fluentui/tree/8830c10bfb2f4c1bdd540287b09713b9679cd1ad/packages/react-components/react-positioning/library/src)의 잠금 버전은 core1.6.8/dom1.6.12/utils0.2.8이다. [충돌 검증](tooltip-collision.ko.md)을 참고한다. 단위 배율의 사각형 계산은 기록한 사례에서 해당 core 경로와 일치한다. DOM/Floating UI/Fluent 전체 동등성을 인증하지 않으며 조상 경계 탐색·둥근 화살표/그림자·Fluent 팩터리는 남아 있다. Core 배포에 MIT 저작권 고지를 포함한다.

근거: `artifacts/tooltip-positioning-2026-11-11`. 독립 좌표 기준은 LTR/RTL의 12방향, 네 경계, 이동/화살표 패딩, 고정·큰 표면, 원점이 다른 경계와 잘못된 옵션을 포함한다. GPU 기준은 실제 Tooltip 소유자와 뒤늦은 옵션 변경을 독립 본문/삼각형 이미지와 비교한다. 기존 대상·접근성·이벤트 검사는 CPU 묶음에 유지한다. 현재 NativeAOT/런타임 비교는 알려진 도구 위치 문제로 미검증이다.
