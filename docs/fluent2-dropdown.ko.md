# Fluent Dropdown

[English](fluent2-dropdown.md)

팝업 선택 항목은 공통 `UiFluentOptionPalette`에서 시스템 대비 색상을 받습니다. 활성 상태의 글자·아이콘·보조 글자·체크 표시·체크박스 테두리는 CanvasText, 비활성 글자와 테두리는 GrayText를 사용합니다. 다중 선택된 체크박스의 배경은 Canvas입니다. 호버·누름 배경에도 Canvas를 사용하며 일반 MenuItem의 Highlight 처리와 구분합니다. 기존 `PaintFocus` 단계에서 모든 항목을 그린 뒤 두께 2픽셀의 바깥 CanvasText 포커스 선을 표시합니다. 일반 테마의 색상과 입력·선택 동작은 유지합니다. 공식 [Option 스타일](https://raw.githubusercontent.com/microsoft/fluentui/master/packages/react-components/react-combobox/library/src/components/Option/useOptionStyles.styles.ts)과 해당 CSS 색상 규칙의 Chromium 진단을 근거로 했으며, 전체 브라우저 픽셀 일치나 실제 OS 모드 전환을 검증한 것은 아닙니다. 근거: `artifacts/option-contrast-2026-11-16/verification.json`.

시스템 색상 GPU 검사 192개, 일반 테마와 이전 그리기 코드의 정확한 GPU 비교 192개, 실제 팝업 동작 16개가 통과했습니다. 새 검사 400개와 기존 포함 845개, Dropdown 41개·Combobox 84개·복합 500개 검사가 통과했습니다. Windows에서 64개 표시 시나리오를 확인했고 Linux 호스트 빌드도 통과했으며 Linux 실제 실행은 제외했습니다.

`Fluent.Dropdown`은 읽기 전용 선택 필드와 실제 ListBox를 제공합니다. `UiBinding<string?>`는 단일 선택, `UiBinding<string[]>`는 다중 선택입니다. 단일 선택은 팝업을 닫고 다중 선택은 열어 둡니다. `UiSelectOption(Value, Text, Enabled)`의 값은 고유해야 하며 알 수 없는 선택 값이나 중복 값은 실패합니다. 항목 배열은 복사하며 동적 교체 API는 없습니다.

`UiDropdown<TBehavior, TPresentation>`을 상속하면 필드를 열 때와 항목을 선택할 때의 포인터 반응 시점을 각각 정하고, 측정·그리기 구현을 별도로 바꿀 수 있습니다. `IUiDropdownBehavior`는 Select에서도 사용합니다. 바인딩이 받아들인 값이 화면과 접근성 정보에 반영됩니다.

```csharp
string? selected = null;
using var dropdown = Fluent.Dropdown("위치",
    new UiBinding<string?>(() => selected, value => selected = value),
    [new("kr", "한국"), new("us", "United States")],
    new(Placeholder: "위치를 선택하세요"));
dropdown.Render(ui);
```

`Options`에서 3개 크기·4개 외관·폭·RTL·최대 팝업 높이·검증 상태를 바꿉니다. 긴 선택 이름은 `SelectedCountFormat`으로 표시합니다. 화살표는 Fluent 아이콘 폰트이며 옵션의 `Icon`에 빌려 쓰는 SVG `MenuIcon`을 전달할 수 있습니다. SVG 소유자는 위젯보다 오래 유지하세요.

공통 팝업·스크롤을 재사용합니다. 방향키·Home·End·PageUp·PageDown·Enter·Space·Escape와 점진적인 이름 검색을 지원합니다. 비활성 항목은 건너뜁니다. 주기 프레임이나 XML 재파싱을 추가하지 않습니다. Windows는 읽기 전용 ComboBox와 ListBox·Option, 닫힌 상태 선택, 실제 GetSelection·다중 추가/해제·Value 변경을 제공합니다.

`./run-gallery.ps1` → **입력과 선택 → Dropdown**에서 단일·다중 선택, SVG, 크기·외관·방향·폭과 초기화를 확인하세요. 다른 탭을 다녀와도 상태를 유지합니다.

근거: `artifacts/dropdown-2026-11-13/`. 동적 항목 교체·대규모 가상화·전체 Fluent 적합성과 고정 RC3 NativeAOT·성능 검증은 남아 있습니다.

참고: [Fluent 사용 가이드](https://fluent2.microsoft.design/components/web/react/core/dropdown/usage), [Windows ComboBox 계약](https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-supportcomboboxcontroltype).

부모 할당·상태 유지 추가 검증: 80×64와 실시간120×24 배치, 같은 옵션 값을 쓰는 Parent 정체성의 형제 격리, 외부 다중 선택 배열의 직접 변경, setter가 정규화한 값의 같은 프레임 반영, Space 누름/해제 토글과 빈 목록을 확인했습니다. 관찰 샘플의 실제 포인터로 한국·이미지·SVG를 선택하고 모든 외관 버튼·탭 왕복·초기화를 확인합니다. 같은 프레임 번호의 다른 UI 컨텍스트로 옮길 때는 공통 팝업 게시 기록을 초기화해 닫힌 논리 목록을 다시 노출합니다.

독립 `UiDropdownOptions.Invalid`를 Windows 접근성 검증 상태에도 전달하고 해제 시 정상 상태로 되돌립니다. `./run-gallery.ps1` → **입력과 선택 → Dropdown / 검증 상태 전환**에서 도시 검색과 두 Dropdown을 함께 확인하세요. 실제 외부 UIA의 DataValidForForm 전환과 탭 왕복을 검증했습니다. 근거: `artifacts/combobox-lifetime-2026-11-13/`.
