# 독립 라벨 위젯

네이티브 검증 근거는 `artifacts/label-relationships-2026-11-12/verification.json`입니다. 소유 Win32 창과 별도 UIA 클라이언트로 클릭 포커스·이름 변경·필수 상태·값 편집·연결 해제·비활성화/재활성화와 실제 `LabeledBy` 공급자 정체성·관계 제거 이벤트를 확인했습니다(16개 검사). CPU Label40개·전체46개 계약/7844회 검사, 별도 GPU28개 검사와 기존 이미지33개 동일성도 확인했습니다. 이전 텍스트 변경 이벤트 근거는 `artifacts/label-native-2026-11-12/`에 보존합니다.

[English](label-widgets.md)

`Fluent.Label(text, target, options)`는 독립적으로 배치하는 라벨입니다. 대상 컨트롤은 따로 렌더링합니다. 라벨은 대상을 빌려 쓰며 렌더링하거나 해제하지 않습니다.

```csharp
var editor = Fluent.Input("Original name", value.Binding, 256, 320);
var label = Fluent.Label("User name", editor, new(Required : true));
// In the existing render callback, or as separate children in a Column:
label.Render(ui);
editor.Render(ui);
// Dispose label before editor.
```

`UiLabelOptions`에서 필수 표시 `Required`, 비활성 표현 `Disabled`, 크기 `Size`(Small/Medium/Large), 굵기 `Semibold`를 고릅니다. 너비가 부족하면 원문을 자르지 않고 줄바꿈합니다. 필수 표시는 마지막 줄 뒤에 논리 단위4의 간격을 둡니다. Large는16/22의 semibold를, Small12/16과Medium14/20은 regular 또는semibold를 사용합니다. 비활성 텍스트와 필수 표시는 같은 비활성 전경색입니다. [공식 사용 지침](https://fluent2.microsoft.design/components/web/react/core/label/usage)과 [고정8830c10b 스타일](https://raw.githubusercontent.com/microsoft/fluentui/8830c10b/packages/react-components/react-label/library/src/components/Label/useLabelStyles.styles.ts)을 기준으로 삼았습니다.

대상은 `IUiLabelTarget`을 구현하고 인스턴스 식별자를 가져야 합니다. Input·Textarea·Button·Link·Slider·Toggle 기반 Checkbox/Switch·RadioGroup이 지원합니다. 대상 하나에 독립 라벨 하나를 연결하며, 지원하지 않는 대상은 명시적으로 거부합니다. 사용자 컨트롤은 표준 primary-control 식별자와 키보드 포커스 등록 계약을 유지하며 참여할 수 있습니다. 임의의 복합 컨테이너를 대상으로 하는 API는 아닙니다. 대상을 생략하면 분류용 일반 라벨이 됩니다.

라벨을 누르면 대상으로 포커스를 옮깁니다. 라벨이 먼저 렌더링되면 같은 프레임에, 대상이 먼저 렌더링됐다면 요청한 다음 프레임에 적용합니다. 비활성·가려진·차단된 대상은 나중에 포커스를 가져가지 않습니다. 연결 해제·대상 변경·라벨 비활성화·해제는 대기 요청을 취소합니다. 이 API는 포커스를 옮기며 버튼 실행이나 토글 값 변경을 대신하지 않습니다. Label.Disabled는 라벨만 변경하므로 대상도 비활성화하려면 따로 설정하세요. 라벨은 Tab 정지점·타이머·호버 애니메이션을 추가하지 않습니다.

연결된 대상의 접근성 이름과 필수 메타데이터를 제공합니다. 명시적 AccessibleName과 둘러싼 Field의 라벨이 우선하며 그룹 내부 항목 이름은 유지합니다. 렌더링한 라벨은 포커스·Invoke·Value가 없는 `UiAccessibilityRole.Label` 요소(Windows UIA Text50020)를 게시하며 콘텐츠 뷰 요소로 중복 노출하지 않습니다. 같은 프레임에 두 위젯이 있으면 렌더 순서에 관계없이 대상의 `LabeledBy`가 실제 라벨 요소를 가리킵니다. 라벨 생략·연결 해제·해제 시 관계를 지우고, 클리핑·비활성화 시 의미는 유지합니다. 기존 게시 이후 관계 이벤트 계약을 사용합니다. 라벨을 생략해도 활성 연결의 이름·필수 메타데이터는 유지할 수 있지만 없는 공급자를 참조하지는 않습니다. 강제 색상 모드·전체 Fluent 적합성은 미검증입니다. 대상이 라벨보다 오래 유지되도록 라벨을 먼저 해제하거나 연결을 끊으세요. [Microsoft Text 지침](https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-supporttextcontroltype)을 참고하세요.

기존 자원·제약 키 아래 텍스트 레이아웃 하나만 보관합니다. 보관 조건은 원문1024자 이하·파생 문자열4096자 이하·256줄 이하이며 글리프 관찰 중에는 유지하지 않습니다. 부모 측정 뒤 같은 프레임에서 텍스트나 옵션이 바뀌면 글리프 내용을 다시 계산합니다. 부모가 준비한 기하로 이전 라벨 텍스트를 대신하지 않습니다.

저장소 루트에서 `./run-gallery.ps1` 실행 → **입력·IME** 탭으로 이동하세요. **메모**를 누르면 아래 편집기에 포커스가 갑니다. 아래 버튼으로 필수 표시·크기·굵기·비활성 상태·긴 라벨 줄바꿈을 전환할 수 있습니다. 라벨만 비활성화해도 별도로 렌더링한 편집기는 계속 사용할 수 있습니다. 근거는 `artifacts/fluent-label-2026-11-12/`, 반복 실행은 [검증 안내](widget-contract-validation.ko.md)를 참고하세요.
