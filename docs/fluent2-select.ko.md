# Fluent Select

Select는 폼에서 텍스트 항목 하나를 고를 때 사용합니다. 아이콘·설명·다중 선택은 Dropdown, 검색과 직접 입력은 Combobox를 사용하세요. [공식 사용 안내](https://fluent2.microsoft.design/components/web/react/core/select/usage)와 [공식 구현](https://github.com/microsoft/fluentui/blob/master/packages/react-components/react-select/library/src/components/Select/useSelect.tsx)도 단순 선택과 풍부한 목록을 구분합니다.

```csharp
var value = new UiState<string?>(null);
using var select = Fluent.Select("지역", value.Binding,
    [new("kr", "대한민국"), new("us", "United States"), new("jp", "日本"), new("de", "Deutschland", false)],
    new(Width : 250, ExpandLabel : "목록 열기"));
select.Render(ui);
```

생성할 때 값이 null이면 첫 번째 활성 항목을 한 번 제안합니다. 지정한 초기 값은 유지합니다. setter가 제안을 거부해도 렌더링할 때 계속 다시 쓰지 않습니다. 생성 후 외부에서 바꾼 값은 null을 포함해 그대로 읽습니다. 알 수 없는 값이나 중복 키는 오류로 처리합니다. 항목은 복사해서 보관하며 빈 목록도 사용할 수 있습니다. 아이콘·설명이 있는 항목은 거부하고 다중 선택 생성자는 제공하지 않습니다.

닫힌 상태에서 위·아래 방향키, Home/End, PageUp/PageDown을 누르면 비활성 항목을 건너뛰어 값을 바꾸고 필드 초점을 유지합니다. 이름을 입력하면 공통 선택 모델로 검색해 바로 선택합니다. 공백이 있는 이름과 같은 글자를 반복해 다음 항목을 찾는 동작도 지원합니다. Alt+Down·Space·필드나 화살표 클릭으로 목록을 펼칩니다. 펼친 목록의 키보드·Enter·Escape·바깥 클릭·스크롤·포인터 선택·접근성 요청은 기존 메뉴 엔진을 공유합니다. 접근성 Deselect로 마지막 선택 항목을 해제할 수 없습니다. 이 선택 유지 규칙은 폼의 필수 입력 검증과는 별개입니다.

UiSelectOptions에서 너비, 공통 UiDropdownSize, 네 UiInputAppearance, RTL, 목록 최대 높이, 오류 상태와 화살표의 접근성 이름을 지정합니다. 필드는 Fluent 입력 크기(논리 높이24/32/40)를 공유합니다. 목록은 체크 표시나 복잡한 콘텐츠 슬롯 없이 텍스트를 표시하고 선택한 항목의 배경을 강조합니다. 시스템 대비 색상을 따르며 비활성 컨트롤은 값을 바꾸지 않습니다.

목록은 Duxel이 앱 화면 안에 그립니다. 브라우저의 HTML select나 운영체제의 선택 대화상자를 호출하는 구현은 아니므로 그 화면과 픽셀이 같다고 주장하지 않습니다. 팩터리 연결50/55는 전체 외관·플랫폼·보조공학 검증 완료율이 아닙니다.

`.\run-gallery.ps1` → **Fluent 컨트롤 → 입력과 선택 → Select**에서 확인하세요. 세 예제마다 크기·모양·방향·검증 상태를 바꾸고 바로 아래 현재 소스를 확인할 수 있습니다. NEW는 Select에만 표시합니다. [검증 기록](../artifacts/fluent-select-2026-11-18/result.json). 실제 Linux 실행·고정 NativeAOT·외부 화면 읽기 프로그램 검증은 남아 있습니다.
