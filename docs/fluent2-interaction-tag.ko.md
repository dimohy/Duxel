# InteractionTag — 본문 동작과 해제 동작

```csharp
var tag = Fluent.InteractionTag("SVG", () => Inspect(), () => Remove());
var tags = new UiFluentTagGroup("기능", selected.Binding, options, activated: InspectValue);
```

`activated`는 본문 동작이며 필수입니다. 독립 위젯의 선택적 `dismissed` 콜백이 × 동작입니다. 콜백이 실제 데이터 변경을 담당하며 위젯을 자동으로 Dispose하지 않습니다. 그룹의 `activated`는 해당 `UiSelectOption.Value`를 받고 선택 바인딩을 자동으로 바꾸지 않습니다. 그룹의 ×는 기존 선택 모델을 통해 해당 값만 해제합니다. `dismissible: false`인 그룹도 본문 동작은 유지합니다.

본문과 ×는 별도 버튼·포인터 영역·Enter/Space 동작을 가집니다. Delete/Backspace 해제는 × 버튼에 포커스가 있을 때만 적용합니다. 본문에서 Delete를 눌러도 해제하지 않습니다. 그룹의 방향키 탐색은 본문 → × → 다음 태그 순서로 이동하며 비활성 항목의 두 동작을 모두 건너뜁니다. RTL에서는 가로 배치·탐색 방향을 반대로 적용합니다. Tab으로 나갔다 Shift+Tab으로 돌아오면 마지막 하위 동작을 기억합니다.

기존 Tag의 세 크기·세 외관·두 모양, 보조 텍스트와 빌린 SVG 아이콘을 공유합니다. × 영역 폭은 Medium 32, Small/ExtraSmall 24입니다. 배경·텍스트·구분선은 고정 Fluent 토큰과 web brand 램프로 계산하며 기본 Tag의 Brand 전경도 같은 권위로 교정했습니다. 마우스를 올린 동작의 영역만 바뀝니다. 기존 둥근 클립·텍스트·아이콘 파이프라인을 재사용하며 SVG XML을 매 프레임 해석하지 않습니다. ×는 Fluent 아이콘 폰트입니다.

접근성은 실행 동작이 없는 Group 아래 두 개의 실제 Button입니다. ×는 본문과 LabeledBy 관계를 가지며 전체 문맥을 담은 이름을 제공합니다. 그룹은 Toolbar이고 두 하위 동작을 하나의 Tab 경유점으로 관리합니다. 관찰 샘플의 동작 결과는 일반 그림 텍스트 대신 실제 Label로 노출합니다.

`IUiTagPresentation` 사용자 표현은 기존 기본 Tag 구현을 그대로 유지할 수 있습니다. InteractionTag를 지원하려면 `SupportsInteraction`, `MeasureInteraction`, `SecondaryWidth`, `PaintInteraction`을 구현합니다. 지원하지 않는 표현은 생성 시 명시적으로 실패하며 다른 표현으로 바꾸지 않습니다. 아이콘·SVG 자산 소유자는 위젯보다 오래 유지해야 합니다.

실행: `./run-gallery.ps1` → **선택 목록** → **확인할 기능**에서 이미지·SVG 등을 선택하세요. 위쪽 **확인형 태그** 본문을 누르면 **태그 확인**이 바뀌고 선택은 유지됩니다. ×를 누르면 선택이 해제됩니다. 아래 **기본 태그**는 전체가 해제 버튼입니다. **태그 크기 전환**, 방향 변경과 탭 왕복으로 두 표현을 비교할 수 있습니다.

근거: `artifacts/interaction-tag-2026-11-13/`. [공식 Tag 지침](https://fluent2.microsoft.design/components/web/react/core/tag/usage), 고정 구현 커밋 `8830c10b`. InteractionTag는 Tag의 변형이며 상위 컴포넌트 분모를 늘리지 않습니다. 초과 항목·강제 색상·작은 크기 확장 터치 영역·실제 스크린 리더와 고정 RC3 NativeAOT/성능/실기기 검증은 남아 있습니다.


## 선택 강조 바인딩

```csharp
var selected = new UiState<bool>(false);
var tag = Fluent.InteractionTag("강조 선택", () => Inspect());
tag.Selected = selected.Binding;
```

독립 InteractionTag의 `Selected`에 선택적인 `UiBinding<bool>`을 연결합니다. 본문 포인터 해제·Enter/Space·네이티브 Toggle은 반대 값을 한 번 요청합니다. 설정자가 변경을 거부할 수 있으므로, 같은 프레임의 그리기·접근성 노출 전에 getter를 다시 읽습니다. 본문 콜백은 활성화마다 실행하고 ×는 이 바인딩을 변경하지 않습니다. 외부 값은 다음 요청 프레임에 읽으며 주기적 렌더링을 예약하지 않습니다.

바인딩된 본문은 기존 Button의 Toggle 패턴을 노출하고 ×는 Invoke만 유지합니다. 바인딩을 제거하면 Toggle도 제거됩니다. `UiTagOptions.Selected`는 바인딩이 없을 때의 시각적 강조이며 선택·Toggle 동작을 자동 생성하지 않습니다. 비활성 표현이 강조보다 우선합니다. 선택의 기본·호버·눌림, 흰색 전경·구분선과 외곽선은 고정 brand 토큰을 사용합니다. 기존 TagGroup 선택 바인딩은 보이는 값들을 관리하며 독립 위젯의 강조 바인딩과 구분합니다.

실행: `./run-gallery.ps1` → **선택 목록**에서 **강조 선택**까지 스크롤합니다. **태그 변경 허용 전환**으로 변경 수락·거부를 비교하세요. 표시 상태·색상·네이티브 Toggle 상태는 수락된 모델 값을 따릅니다. 근거: `artifacts/tag-selection-2026-11-13/`.


## 그룹의 독립 강조 값

```csharp
var visible = new UiState<string[]>(["image", "svg"]);
var emphasized = new UiState<string[]>([]);
var group = new UiFluentTagGroup("기능", visible.Binding, items,
    emphasized: emphasized.Binding);
```

선택적인 `emphasized` 바인딩은 기존 `selection`이 관리하는 보이는 값들과 별도로 선택 색상·본문 Toggle 상태를 관리합니다. `activated` 콜백 없이도 본문 Toggle을 사용할 수 있습니다. 본문은 강조만 전환하고 선택적인 `activated`에는 실제 항목 값을 전달합니다. ×는 보이는 값만 제거합니다. 숨겨진 항목의 강조 값은 유지하므로 같은 항목을 다시 추가하면 강조도 복원합니다. 두 배열 모두 알려진 항목의 중복 없는 값이어야 합니다. 거부·정규화된 변경은 기존 선택 모델로 다시 읽고, 외부에서 배열 원소만 바꾼 경우도 배치 전에 감지합니다. 강조 스냅샷은 자식들이 공유하며 각 자식을 그릴 때 전체 바인딩을 반복 검색하지 않습니다. 주기적 프레임은 요청하지 않습니다.

`dismissible: false`에서도 본문 Toggle·방향키 탐색을 유지합니다. 비활성 항목은 Toggle을 거부합니다. 크기·RTL 변경으로 선택 상태의 공급자를 교체하지 않습니다. 선택 표현은 독립 태그와 같고 `TagOptions.Selected`는 강조 바인딩이 없을 때의 시각적 기본값입니다.

실행: `./run-gallery.ps1` → **선택 목록**에서 기능을 선택합니다. 위쪽 태그 본문으로 강조를 전환하고 ×로 제거한 뒤 같은 기능을 다시 추가해 강조 복원을 확인하세요. 아래 기본 태그 그룹도 같은 보이는 값을 따릅니다. 근거: `artifacts/tag-group-emphasis-2026-11-13/`.

미디어는 [Tag 아바타·이미지 슬롯](fluent2-tag.ko.md#빌린-아바타이미지-미디어)을 공유하며 태그별 자원 소유권을 분리합니다.
