# Fluent Accordion 접기·펼치기

[English](fluent2-accordion.md)

`./run-gallery.ps1` 실행 → **접기·펼치기** 탭으로 이동하세요. 탭이 숨겨지면 더 보기 메뉴를 사용합니다. 첫 항목을 열어 메모를 입력하고 접거나 다른 탭에 다녀와도 같은 본문 위젯과 값이 유지됩니다. 위 버튼으로 다중 열기·마지막 항목 접기 허용·화살표 위치·네 가지 헤더 크기를 바꿀 수 있습니다. 세 번째 헤더는 비활성 동작을 보여 줍니다.

```csharp
using System.Collections.Immutable;

var open = new UiState<ImmutableArray<string>>(ImmutableArray<string>.Empty);
var note = new UiState<string>("저장한 내용");
var accordion = Fluent.Accordion("설정", open.Binding,
    new UiAccordionItem[]
    {
        new("note", "메모", Fluent.Input("메모 내용", note.Binding, 256, 300)),
        new("action", "실행", Fluent.Button("적용"))
    }, width : 360, options : new(Collapsible : true));
// In the existing screen callback:
accordion.Render(ui);
// The accordion owns both content widgets; dispose it with the screen.
```

`UiAccordion<TPresentation>`과 `IUiAccordionPresentation`으로 순수 헤더 측정·표현을 분리하며 생성 팩터리는 `UiFluentAccordionPresentation`을 선택합니다. 항목은 고유 Value·Label·독점 소유 Content·Enabled·빌려 쓰는 UiMenuIcon으로 구성합니다. 항목 설명을 복사하고 숨긴 본문도 살아 있게 유지하며 Accordion 해제 때 함께 해제합니다. `SetHeader(value, label, enabled, icon)`은 키·본문을 교체하지 않고 헤더를 바꿉니다. 아이콘 소유자는 Accordion보다 오래 유지해야 합니다.

호출자 제어 값은 `ImmutableArray<string>`입니다. 초기 default/빈 값은 모두 닫힌 상태입니다. Multiple=false는 한 항목만 허용하고 Collapsible=false는 사용자·UIA가 마지막 열린 항목을 접는 것을 막습니다. 호출자는 제어 값을 명시적으로 비울 수 있습니다. 알 수 없는 키·중복 키·단일 모드의 복수 값은 명시적으로 거부합니다. 다중 모드에서 단일 모드로 바꾸기 전에 선택 값을 줄이세요. 바인딩 setter가 변경을 거부하면 Expand/Collapse 성공으로 잘못 보고하지 않습니다.

Enter·Space 해제로 포커스한 헤더를 전환하고 Tab/Shift+Tab은 헤더 → 보이는 본문 → 다음 헤더를 따릅니다. 공식 참조에서 폐기 예정인 화살표 탐색은 기본으로 넣지 않았습니다. 포커스한 본문을 접으면 접근성 캡처가 없어도 헤더로 돌아옵니다. 헤더 입력을 한 번 처리한 뒤 최종 배치를 계산하며 실제 Tab 순서에는 본문이 포함됩니다. 부모 할당 안에서만 그리기·입력을 처리합니다. 입력보다 먼저 측정한 부모의 새 자연 높이는 요청된 다음 프레임에 반영될 수 있습니다. 주기적 애니메이션을 요청하지 않습니다. 임의 바인딩 getter의 독립 변경을 보존하기 위해 측정 유지 최적화는 아직 적용하지 않았으며 성능 개선을 주장하지 않습니다.

UIA는 Accordion Group·Invoke/ExpandCollapse를 지원하는 Button 헤더·실제 비포커스 패널을 제공합니다. 열린 헤더는 실제 패널을 제어하고 패널의 LabeledBy는 헤더를 가리킵니다. 접으면 본문 노드·공급자는 사라지지만 위젯 모델은 유지됩니다. 비활성 요청은 Disabled, 마지막 항목 접기 금지는 InvalidOperation을 반환합니다. 기존 게시 이후 확장·관계 이벤트를 재사용합니다.

FluentUI 참조 커밋은 `8830c10bfb2f4c1bdd540287b09713b9679cd1ad`입니다. [공식 사용 지침](https://fluent2.microsoft.design/components/web/react/core/accordion/usage)과 [상태 동작](https://raw.githubusercontent.com/microsoft/fluentui/8830c10bfb2f4c1bdd540287b09713b9679cd1ad/packages/react-components/react-accordion/library/src/components/Accordion/useAccordion.ts)을 따릅니다. 투명 헤더 배경·최소 높이44px(Small32)·보통 굵기12/16,14/20,16/22,20/28 타이포그래피·앞/뒤 화살표·본문 좌우12px 여백을 제공합니다. 강제 색상·RTL 전용 동작·접기 모션·공식 브라우저 픽셀 동등성·전체 적합성은 남아 있습니다.

근거는 `artifacts/fluent-accordion-2026-11-12/verification.json`입니다. CPU47개 계약·7876회 검사(Accordion27·Observer164), 네이티브 Accordion13개와 기존 Label/Radio/Toggle43개, Light/Dark×MSAA1/4 GPU32개 검사가 통과했습니다. 네이티브 실행은 [계약 검증 안내](widget-contract-validation.ko.md)의 명시 바이너리·새 출력 명령에서 Profile=Accordion을 선택합니다. 고정 RC3 NativeAOT·전체 목표 검증은 별도입니다.


공식 React 데모를 직접 비교한 기본 호버 반응은 투명 배경을 유지하는 손 모양 커서입니다. 비활성 헤더는 금지 커서와 비활성 글자색을 사용합니다. Duxel도 헤더의 실제 클립·팝업 가림을 확인한 뒤 해당 커서를 게시합니다. 키보드 Tab은 포커스 테두리를 표시하고 Enter·Space는 기존 헤더 버튼 계약으로 동작합니다. 별도 호버 배경색은 공식 기본 스타일에 추가하지 않습니다.

출처: [디자인 가이드](https://fluent2.microsoft.design/components/web/react/core/accordion/usage), [실제 React 데모](https://react.fluentui.dev/?path=/docs/components-accordion--docs). 브라우저·고정 소스 근거는 `artifacts/accordion-hover-2026-11-13/official-evidence.json`에 보존합니다. 이는 해당 상호작용의 검증이며 전체 Fluent 시각·모션 적합성 완료를 뜻하지 않습니다.
