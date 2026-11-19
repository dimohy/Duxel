# 숫자 입력 (SpinButton)

증감 버튼의 색상 계산을 배치·포인터 처리와 분리했습니다. 시스템 대비 색상이 활성화되면 일반 화살표는 ButtonText, 비활성·최솟값/최댓값 도달·읽기 전용 화살표는 GrayText를 사용합니다. 호버·누름 배경에는 ButtonFace를 적용하고 기본·비활성 배경은 투명하게 유지합니다. [공식 Fluent의 상태·외관 구조](https://raw.githubusercontent.com/microsoft/fluentui/master/packages/react-components/react-spinbutton/library/src/components/SpinButton/useSpinButtonStyles.styles.ts)와 [시스템 색상 역할](https://www.w3.org/TR/css-color-adjust-1/#forced-colors-properties)을 네이티브 표현에 적용했습니다. GPU 검사 48개로 네 외관·두 크기·양방향 배치·두 화살표·여섯 상태의 192개 조합을 확인하며, 색상 역할을 직접 지정한 별도 기준 표현과 픽셀을 정확히 비교합니다. 일반 밝음·어두움/MSAA 화면 293장은 이전 구현과 모두 일치합니다. 검증과 OS·플랫폼 한계: [시스템 대비 색상](system-forced-colors.ko.md).

`Fluent.SpinButton`은 비어 있을 수 있는 `double` 바인딩에 숫자 편집과 두 증감 버튼을 연결합니다. 생성 타입은 `UiFluentSpinButton`이며 사용자 정의 표현은 `UiSpinButton<TPresentation>`을 사용합니다.

```csharp
var quantity = new UiState<double?>(3);
var input = Fluent.SpinButton("수량", quantity.Binding,
    new(Minimum: 0, Maximum: 100, Step: 1, PageStep: 10,
        IncrementLabel: "수량 늘리기", DecrementLabel: "수량 줄이기"));
var form = Dux.Column(new(Spacing: 8, MainAxisSize: UiMainAxisSize.Min),
    Fluent.Label("수량", input), input);
```

입력 중에는 편집 문자열을 유지하고 Enter 또는 포커스 이탈 시 값을 반영합니다. Escape는 반영 전 값으로 되돌립니다. ↑·↓는 `Step`, Page Up·Down은 `PageStep`, Shift를 누르지 않은 Home·End는 명시한 최솟값·최댓값을 적용합니다. 편집 영역 하나만 Tab 대상이며 증감 버튼은 추가 Tab 대상 없이 네이티브 Invoke 동작을 제공합니다.

증감 버튼을 누르면 즉시 반영합니다. 길게 누르면300ms 후 반복을 시작하고 간격을80ms까지 줄입니다. 버튼 놓기·이탈·비활성·읽기 전용·경계 도달·변경 거부 시 반복을 멈춥니다. 같은 누름 상태로 다시 진입해도 취소된 반복을 재시작하지 않습니다. 멈춘 컨트롤은 주기 반복을 예약하지 않습니다.

기본 파서는 지수 표기를 포함한 유한한 고정 문화권 숫자를 받습니다. 빈 문자열은 null입니다. 반영 시 범위를 제한하고 `Precision`(0–15)으로 반올림합니다. 생략하면 `Step`에서 자릿수를 구하며 중간값은0에서 멀어지는 방향으로 처리합니다. 외부 모델은 이미 유한하고 범위 안이어야 합니다. 표시는 증감 정밀도와 관계없이 외부 모델의 소수 부분을 유지합니다. `Formatter`·`Parser`로 단위나 문화권별 입력을 구현할 수 있습니다. 출력은 유효한 한 줄 문자열, 숫자는 유한한 값이어야 하며 사용자 파서도 범위·정밀도 규칙을 따릅니다. `Changed`는 요청값·실제값·반영 여부를 전달하고 바인딩이 거부한 값은 다시 읽습니다.

Small·Medium의 최소 높이는24·32이며 네 가지 `UiInputAppearance`를 제공합니다. `RightToLeft`는 증감 버튼 위치를 반전합니다. `ReadOnly`는 편집과 증감을 막습니다. Field·독립 Label 연결은 공통 입력 경로를 사용합니다. 네이티브 의미는 Spinner 타입50016, Value와 선택적 RangeValue이며 빈 값은 RangeValue를 제공하지 않습니다.

실행: `./run-gallery.ps1` → **숫자 입력**. 비우기·단위 입력·크기·모양·읽기 전용 전환·문자 입력·포커스 이동·길게 누르기를 확인하세요.

기준: 고정 버전의 [동작](https://github.com/microsoft/fluentui/blob/8830c10bfb2f4c1bdd540287b09713b9679cd1ad/packages/react-components/react-spinbutton/library/src/components/SpinButton/useSpinButton.tsx), [스타일](https://github.com/microsoft/fluentui/blob/8830c10bfb2f4c1bdd540287b09713b9679cd1ad/packages/react-components/react-spinbutton/library/src/components/SpinButton/useSpinButtonStyles.styles.ts).

범위 제한과 중간값 반올림은 위에 명시한 Duxel 계약입니다. React 전체 동작 일치, 강제 색상 테마, 물리 스크린리더, 전체 양방향 텍스트와 NativeAOT는 아직 검증되지 않았습니다. 전체 Fluent 적합성 완료를 뜻하지 않습니다.

검증: artifacts/spinbutton-2026-11-13/verification.json. 관리 실행65개 모음·10,149회 검사, 관찰 샘플25페이지를 포함한 GPU899회 검사를 통과했습니다. SpinButton의 프레임 슬롯2·3 사이 PPM265장이 완전히 같습니다. 빌드는 경고·오류0건입니다. 검사 횟수에는 반복 사례가 포함됩니다. 전용 관찰 샘플 세션4개 검사도 통과했으며 대기 전력 측정을 뜻하지는 않습니다.


포인터 포커스 교정: 같은 편집 영역을 클릭하면 커서만 이동하고 미완성 숫자를 포함한 편집 문자열을 유지합니다. 증감 버튼은 중간 반영 없이 증감한 결과만 반영하며 다른 컨트롤이나 빈 영역으로 이동하면 한 번 반영합니다. 공통 포커스 처리는 임시 초기화 대신 실제 클릭 대상을 전달하고 중단된 프레임의 임시 포커스와 소유 상태를 복구합니다.


포인터 교정 근거: artifacts/spinbutton-pointer-focus-2026-11-13/verification.json. 수정 전 같은 편집 영역 클릭 실패를 재현한 뒤 관리 실행65개 모음·10,171회, GPU495회와 전용 세션4개 검사를 통과했습니다. 프레임 슬롯2·3 사이 PPM293장이 일치하며 기존265장도 모두 그대로입니다. 속도 개선이나 전체 목표 완료를 뜻하지 않습니다.

