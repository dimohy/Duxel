# 측정 기반 Tooltip 배치

Last synced: 2026-11-11

현재 [자동 크기/넘침](tooltip-auto-size.ko.md)은 상태를 유지한 재측정과 본문/전체 콘텐츠 영역을 제공한다.

`UiPopupPlacement.ArrangeMeasured<TMeasure, TContent>`는 실제 콘텐츠를 먼저 측정한 뒤 배치한다. 순수한 `IUiPopupMeasurement<TContent>`가 유한한 양수 본문 크기를 반환한다. `UiPopupMeasureRequest.Width`를 줄바꿈·높이 측정에 먼저 적용한다. 부모 커서/레이아웃/제약/입력 영역과 다음 항목 정보를 복원하고 측정 중 입력 처리를 거부한다. 정책은 그리기나 애플리케이션 상태 변경을 수행하면 안 된다.

`MatchTargetWidth`는 후보 선택 전에 자연 폭의 콘텐츠를 대상 폭으로 재측정하며 명시한 요청 폭은 유지한다. 고정 크기 `Arrange`는 폭 맞춤을 거부하므로 콘텐츠를 측정하는 경로를 사용해야 한다. 고정 [matchTargetSize](https://github.com/microsoft/fluentui/blob/8830c10bfb2f4c1bdd540287b09713b9679cd1ad/packages/react-components/react-positioning/library/src/middleware/matchTargetSize.ts)는 배치 수명주기에서 한 번 맞춤을 시도한다.

`Arrange<TOffset>`와 `ArrangeMeasured<TMeasure, TContent, TOffset>`은 순수한 `IUiPopupOffset`을 정적으로 선택한다. 대상 사각형·측정 크기·논리 후보·옵션·물리적 `UiPopupSide`를 받아 화살표 높이를 더하기 전의 주축/교차축 오프셋을 반환한다. `UiConfiguredPopupOffset`은 Gap/CrossAxisOffset을 읽는다. 정책은 여러 번 평가될 수 있으므로 호출 횟수에 의존하면 안 된다. 물리 Side로 RTL에서도 원문 콜백의 방향 규칙을 사용할 수 있다.

생성된 사용자 샘플은 실제 Type200 텍스트를 측정하고 확정된 본문 폭으로 그리기도 줄바꿈한다. 대상 폭 맞춤과 사용자 컴파일 오프셋 정책을 적용했다. 기존 텍스트/옵션의 프레임 확보 규칙은 유지한다.

보관한 core1.6.8과 고정 matchTargetSize/coverTarget 원문을 단위 배율 사각형·줄 측정 플랫폼에서 실행한다. 추가384사례는 LTR/RTL·12방향·기존 명시 폭·측정 크기/물리 방향을 사용하는 콜백을 포함한다. 맞춤192개·요청 폭196개·동적 정책192개·여러 줄188개이며 기존2304참조를 유지한다. CPU는 실제 DirectText 줄바꿈과 커서/입력 범위 복구도 검사한다. GPU는 실제 Tooltip 소유자·수치 참조·독립 텍스트 장면을 사용한다. 근거는 `artifacts/tooltip-measured-layout-2026-11-11`이다.

현재 [자동 크기/넘침](tooltip-auto-size.ko.md)은 상태 수명주기와 실제 콘텐츠 재측정을 구현한다. 측정은 UiPopupContentMeasure(Size, ContentSize)를 반환하며 요청은 Width/Height/MaxWidth/MaxHeight를 제공한다. 자동 크기 활성 축은 맞춤 전에 이전 인라인 크기를 지운다. 공식 표면/제어/접근성 동작·NativeAOT/런타임 비교와 전체 목표는 남아 있다.
