# Tooltip 오프셋·덮기·이동 제한

Last synced: 2026-11-11

현재 [자동 크기/넘침](tooltip-auto-size.ko.md)은 상태를 유지한 재측정과 본문/전체 콘텐츠 영역을 제공한다.

현재 [측정 기반 배치](tooltip-measured-layout.ko.md)는 후보별 컴파일 오프셋과 실제 대상 폭 콘텐츠 재측정을 추가한다.

`UiPopupPositioning.Gap`은 유한한 음수를 허용하며 화살표 높이를 더하기 전의 부호 있는 주축 오프셋이다. `CrossAxisOffset`은 위·아래 배치에서 수평으로 이동하고 RTL에서는 방향을 반전하며, 앞·뒤 배치에서는 수직으로 이동한다. `CoverTarget`은 후보 선택 전에 대상의 주축 크기만큼 본문을 대상 방향으로 이동한다. 기본값은 기존 좌표를 유지한다.

`OverflowBoundaryPadding`은 `UiPopupBoundaryPadding(Start, Top, End, Bottom)` 또는 균일 생성자 `new(value)`를 사용한다. 시작/끝은 RTL에서 반전하고 위/아래는 물리 방향을 유지한다. 유한한 음수 패딩은 유효 이동 경계를 넓힌다. 패딩은 이동에 적용하며 분리된 후보 선택 경계는 유지한다.

`ShiftToCoverTarget`은 공간이 부족할 때 표면이 향한 축에서도 이동하도록 하며 원문의 제한기로 대상 부착 범위를 유지한다. `TetherMode`는 Fluent 소스 옵션을 따른다.

| 모드 | Fluent 플래그 | 향한 축 이동 | 향한 축 제한 |
| --- | --- | --- | --- |
| Default | 미지정 | ShiftToCoverTarget 따름 | ShiftToCoverTarget 따름 |
| Disabled | true | 비활성 | 활성 |
| DisabledAllAxes | all | 활성 | 비활성 |

표면 변과 평행한 축은 항상 이동한다. 큰 음수 오프셋으로 제한 범위가 역전돼도 원문의 순서 있는 비교를 유지한다. 화살표 정렬 재배치는 좌표를 유지한 채 offset/flip을 건너뛰고 cover/shift를 다시 실행한다. 덮기와 화살표 재배치가 함께 있으면 두 번째 덮기 이동까지 원문 순서를 따른다.

참고: 고정 [Fluent coverTarget](https://github.com/microsoft/fluentui/blob/8830c10bfb2f4c1bdd540287b09713b9679cd1ad/packages/react-components/react-positioning/library/src/middleware/coverTarget.ts), [shift 조합](https://github.com/microsoft/fluentui/blob/8830c10bfb2f4c1bdd540287b09713b9679cd1ad/packages/react-components/react-positioning/library/src/middleware/shift.ts), 잠금 버전 Floating UI core1.6.8. 참조 실행기는 원본 core와 import/타입을 제거하고 물리 방향 추출을 인라인한 원문 cover 함수를 실행한다. 명시적인 단위 배율 사각형과 원문 이동 옵션을 제공하며 DOM 조상/배율 탐색 전체를 검증하지 않는다.

추가 참조1536사례는 음수 오프셋·LTR/RTL의 12방향·이동 제한3모드·논리 패딩·덮기/화살표 재배치 조합을 포함한다. 위치/방향/덮기/이동/제한의 288조합 중286개와 덮기+화살표 재배치64개를 포함한다. 기존768사례와 함께 허용오차0.001로 모두 통과했다. CPU2863·Tooltip GPU464가 통과했으며 GPU에는 밝은/어두운 테마·MSAA1/4별 실제 소유자 참조 장면12개를 추가했다. 사용자 샘플은 교차축8·균일 경계 패딩8·ShiftToCoverTarget을 사용한다. 근거는 `artifacts/tooltip-offset-cover-2026-11-11`이다.

공식 둥근 화살표/그림자·제어된 표시·전체 접근성 속성/종료 이벤트, Fluent 전체 검증·현재 NativeAOT/런타임 비교와 원래 프로젝트 전체 목표는 남아 있다. 성능 개선은 주장하지 않는다. Core는 Floating UI와 Microsoft의 MIT 고지를 함께 배포한다.
