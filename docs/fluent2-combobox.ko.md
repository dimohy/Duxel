# Fluent Combobox

[English](fluent2-combobox.md)

팝업 항목은 Dropdown과 같은 시스템 색상 표현을 사용합니다. Canvas·CanvasText, 비활성 GrayText, 단일·다중 선택 표시와 항목을 그린 뒤 표시하는 바깥 포커스 선이 함께 적용됩니다. 편집기와 입력·선택 바인딩은 바꾸지 않았습니다. [Dropdown 색상 역할과 검증 범위](fluent2-dropdown.ko.md)를 참고하세요. 근거: `artifacts/option-contrast-2026-11-16/verification.json`.

`Fluent.Combobox`는 기존 한 줄 입력·IME·선택·되돌리기 커널과 공통 목록 팝업을 연결합니다. 입력 문자열과 선택한 옵션 값은 별도 바인딩입니다. 입력 중에는 원래 선택을 유지하며, 이름 검색은 대소문자를 구분하지 않는 부분 일치입니다. 필터에 가려진 선택도 실제 GetSelection과 동일한 옵션 ID를 유지합니다.

```csharp
string query = "";
string? city = null;
using var combo = Fluent.Combobox("도시",
    new UiBinding<string>(() => query, value => query = value),
    new UiBinding<string?>(() => city, value => city = value),
    [new("seoul", "서울"), new("new-york", "New York")],
    new(Field: new(Width: 260, Placeholder: "도시 검색"), Freeform: true));
combo.Render(ui);
```

`UiBinding<string[]>`를 선택 바인딩으로 전달하면 다중 선택입니다. 가려진 선택을 보존하며 팝업에서 항목을 토글합니다. 다중 선택의 정확한 이름을 Enter로 확정하면 검색 문자열을 비우고 목록을 유지합니다. `SelectedText`는 선택된 옵션 이름을 제공합니다. 선택 태그 위젯은 아직 제공하지 않습니다.

방향키·Home·End·PageUp·PageDown으로 목록을 탐색하고 Enter·Space로 확정합니다. 탐색 전 Space는 검색 문자열의 일부입니다. Escape는 목록을 닫고 입력기를 유지하며 Tab은 다음 실제 컨트롤로 이동합니다. 입력 영역 클릭이나 화살표로 목록을 열 수 있습니다. 화살표는 전체 옵션을 다시 표시합니다. 팝업 포인터 해제를 입력기가 선점하지 않으며 닫힌 프레임에서 뒤쪽 컨트롤을 바로 활성화합니다.

기본 `Freeform=false`에서는 목록에 없는 입력을 Enter로 확정하면 선택된 이름으로 돌아갑니다. `Freeform=true`의 단일 선택은 입력을 유지하고 이전 옵션 선택을 비웁니다. 자유 입력은 문자열 바인딩에 남으며 가짜 옵션 값을 만들지 않습니다. 다중 선택에서 목록 밖 입력을 새 선택 항목으로 자동 추가하지 않습니다.

`UiComboboxOptions.Field`는 Dropdown과 같은 3개 크기·4개 외관·폭·RTL·팝업 높이·검증 상태를 제공합니다. `MaxLength` 기본값은4096입니다. 옵션 값은 고유해야 하고 목록·설명은 한 줄이어야 합니다. SVG `MenuIcon`을 빌려 쓸 수 있으며 소유자는 위젯보다 오래 유지해야 합니다. 화살표는 Fluent 아이콘 폰트입니다. XML 재파싱이나 별도 입력기·셰이더를 추가하지 않습니다. 포커스를 가진 입력의 캐럿 갱신은 기존 입력 계약을 따릅니다.

Windows는 편집 가능한 ComboBox·자식 Edit·ListBox·Option과 Value·Selection·ExpandCollapse를 노출합니다. 숨겨진 옵션도 선택·추가·해제할 수 있으며 필터 변경으로 공급자를 교체하지 않습니다. IME 조합 중 Enter는 옵션을 확정하지 않고 확정 문자열을 한 번만 처리합니다.

실행: `./run-gallery.ps1` → **입력과 선택 → Combobox** → **도시 검색**. `New `를 입력한 뒤 `New York`을 선택하거나 한글을 입력하세요. 탭을 왕복해도 값을 유지합니다. 위쪽 크기·외관·방향·폭 버튼은 도시 검색에도 적용합니다. 선택 지우기는 아래 Dropdown 두 개를 초기화합니다.

근거: `artifacts/combobox-2026-11-13/`. 관리형 .NET11 검증입니다. 실제 사용자의 Windows IME 조작, 선택 태그·동적 목록·대규모 가상화·전체 Fluent 상태/강제 색상 적합성, 고정 RC3 NativeAOT·성능·실기기 검증은 남아 있습니다.

참고: [Fluent Combobox 사용 가이드](https://fluent2.microsoft.design/components/web/react/core/combobox/usage), [Windows ComboBox 계약](https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-supportcomboboxcontroltype). 고정 구현 소스는 이전 선택 목록 단계의 `sources.json`과 `ComboboxBase.types.ts`를 재사용합니다.

## 닫기와 외부 열림 상태

`Freeform=false`에서는 Escape·Tab·바깥 클릭으로 목록을 닫거나 입력 포커스를 떠날 때 미확정 검색어를 선택된 이름으로 복원합니다. 다중 선택은 검색어만 비우며 선택 값을 보존합니다. 닫기만으로 검색 중인 항목을 새로 선택하지 않습니다. `Freeform=true`의 초안은 그대로 유지합니다.

```csharp
var popup = new UiState<bool>(false);
combo.Open = popup.Binding;
combo.OpenChanged = Console.WriteLine;
```

`Open`이 null이면 내부 상태를 사용합니다. 바인딩을 제공하면 setter가 열기·닫기 요청을 받고, getter를 다시 읽은 결과가 실제 상태를 결정합니다. setter가 이전 값을 유지하면 요청을 거부합니다. 거부된 UIA Expand/Collapse는 InvalidOperation을 반환하고 실제 팝업과 메타데이터를 유지합니다. `OpenChanged`는 실제 관측한 전환만 한 번 알리며 거부되거나 같은 상태인 프레임에서는 호출하지 않습니다. 외부 모델 값 변경은 setter로 다시 전달하지 않습니다. 외부 모델만 바꿀 때는 기존 Watch/프레임 요청 계약을 따릅니다.

외부 검색 문자열 변경은 필터를 갱신하되 사용자 열기 요청을 만들지 않습니다. 명시적 화살표·UIA 확장은 전체 목록을 다시 표시하며 이미 열린 목록의 필터는 유지합니다. 선택으로 열림 바인딩이 바뀐 뒤에는 최신 값을 읽어 오래된 상태로 다시 열지 않습니다.

`EmptyText` 기본값은 `No matches`입니다. 검색 결과가 없으면 충분한 높이의 팝업과 선택 불가능한 정적 텍스트를 표시합니다. 가짜 비활성 Option을 만들지 않습니다. 샘플은 `검색 결과가 없습니다`를 표시합니다. **도시 목록 열림 전환** 버튼과 전환 횟수로 외부 제어·콜백 중복 여부를 확인할 수 있습니다.

추가 근거: `artifacts/combobox-behavior-2026-11-13/`. [고정 Fluent 닫기 처리 원문](https://raw.githubusercontent.com/microsoft/fluentui/8830c10bfb2f4c1bdd540287b09713b9679cd1ad/packages/react-components/react-combobox/library/src/utils/useComboboxBaseState.ts). 이 API의 `OpenChanged`는 실제 전환 알림이며 요청 제어는 `Open` 바인딩이 담당합니다.

## 수명·배치와 검증 상태

검색형 입력에서도 화살표 클릭은 전체 선택 목록을 열고 다시 누르면 닫습니다. 누른 채 여러 프레임이 지나도 공통 편집기의 포인터 소유권 유지 기능으로 화살표의 클릭을 보존합니다. 검색 문자열 입력과 키보드 탐색은 기존대로 동작합니다.

다른 UI 컨텍스트로 옮길 때 이전 편집기·팝업 공급자를 해제하고 같은 프레임 번호에서도 새 논리 목록을 게시합니다. 열린 상태에서 Dispose하면 팝업·편집기 소유권을 해제하고 오래된 네이티브 요청은 거부합니다. 80×64와 변경 후 120×24 부모 할당에서 실제 크기·팝업 위치·공급자 정체성을 확인했습니다.

독립 `Field.Invalid`도 ComboBox와 실제 자식 Edit의 접근성 검증 상태에 전달됩니다. false로 바꾸면 둘 다 해제됩니다. `./run-gallery.ps1` → **입력과 선택 → Dropdown / 검증 상태 전환**에서 도시 검색과 두 Dropdown의 테두리·네이티브 상태를 함께 확인하세요. 탭을 왕복해도 유지합니다. 근거: `artifacts/combobox-lifetime-2026-11-13/`.
