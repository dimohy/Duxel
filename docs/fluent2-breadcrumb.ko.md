# Fluent Breadcrumb

[English](fluent2-breadcrumb.md)

Last synced: 2026-11-12

`Fluent.Breadcrumb`는 고유 값과 이름을 가진 경로를 소유합니다. 마지막 항목은 현재 위치이며 이동 동작이 없습니다. 앞선 항목의 선택은 콜백에 해당 값을 전달합니다. 폭이 부족하면 두 번째 항목부터 기존 Menu 팝업에 모으며 첫 항목과 현재 위치를 유지합니다. 둘도 들어가지 않는 극단적인 폭에서는 현재 위치만 클립해 표시합니다.

`UiBreadcrumb<TBehavior, TPresentation>`을 상속하면 항목의 포인터 활성화 시점과 Arrow 모드의 포커스 키를 측정·그리기와 따로 지정할 수 있습니다. 넘친 항목의 메뉴, 네이티브 Invoke, 현재 위치의 접근성 정보는 공통 코드를 사용합니다.

```csharp
using var path = Fluent.Breadcrumb("문서 경로",
    [new("home", "홈"), new("folder", "문서"), new("current", "현재 문서")],
    value => OpenLocation(value),
    new(MaximumCharacters: 30, OverflowLabel: "숨긴 경로", CurrentLabel: "현재 위치"));
path.Render(ui);
```

`UiBreadcrumbOptions`는 Small/Medium/Large(높이24/32/40), Tab/Arrow 포커스 방식, RightToLeft, 이름·Tooltip 글자 수 상한을 제공합니다. 글자 수는 결합 문자와 한글을 쪼개지 않는 텍스트 요소로 셉니다. 긴 이름은 말줄임하고 포커스·호버 Tooltip을 제공합니다. 옵션 변경은 같은 인스턴스의 입력 상태를 유지하며 재측정합니다. 항목 배열은 생성 시 복사하며 현재 공개 API로 교체하지 않습니다. 이름과 값은 비어 있으면 안 되며 값은 고유해야 합니다.

`UiBreadcrumbOption.Enabled = false`는 이동을 차단합니다. `DisabledFocusable = true`가 기본값이므로 표시 중인 비활성 경로도 키보드나 네이티브 Focus로 설명을 확인할 수 있습니다. 부모 전체가 비활성이면 이 예외를 허용하지 않습니다. Arrow 모드는 좌우·Home·End를 사용하며 RTL에서 방향을 뒤집습니다. 접힘 팝업은 공통 Menu의 포커스·선택·Escape 처리를 재사용합니다. 팝업 안의 비활성 항목은 공통 Menu 규칙에 따라 키보드에서 건너뜁니다.

접근성은 Navigation landmark → OrderedList → ListItem → 실제 버튼 구조입니다. 현재 위치는 수동 ListItem이며 `ItemStatus`, `current=page`, 원래 순서·전체 항목 수를 공개합니다. 구분자는 접근성 요소가 아닙니다. 숨겼다 다시 표시한 항목은 논리 ID를 유지하되 폐기된 네이티브 런타임 ID를 재사용하지 않습니다.

기본 화살표와 더 보기 아이콘은 **Fluent 아이콘 폰트**입니다. 생성기가 글리프를 고르고 공통 DirectText 캐시로 그립니다. SVG는 별도 `UiSvgIcon.MenuIcon`을 `UiBreadcrumbOption.Icon`에 전달할 수 있으며 호출자가 원본 아이콘의 수명을 소유합니다. 매 프레임 SVG XML을 읽는 경로를 추가하지 않습니다.

`./run-gallery.ps1`을 실행한 뒤 **Fluent 컨트롤 → 동작과 탐색 → Breadcrumb**에서 너비, 포커스 방식, 배치 방향과 이동 기록을 확인하세요. 다른 페이지를 다녀와도 상태가 유지됩니다. Breadcrumb는 주기 프레임을 예약하지 않습니다. 기본 표현은 측정과 문자열 가공 결과를 재사용합니다.

검증: `artifacts/breadcrumb-2026-11-12/verification.json`. managed CPU56개 모음/8803회 검사, GPU14개 프로필/2944회 검사, 별도 외부 Windows UIA7개 프로필/138회 검사를 통과했습니다. 검사 횟수는 반복을 포함합니다. 공식 전체 적합성, 동적 경로 교체, 극단적인 폭의 픽셀 기반 말줄임, 강제 색상과 모든 기기 검증 및 고정 RC3 NativeAOT·원래 성능 검증은 완료되지 않았습니다.

근거: [공식 사용 가이드](https://fluent2.microsoft.design/components/web/react/core/breadcrumb/usage), [고정 원본](https://github.com/microsoft/fluentui/tree/8830c10bfb2f4c1bdd540287b09713b9679cd1ad/packages/react-components/react-breadcrumb/library/src/components). 원본과 출처는 검증 폴더의 `sources.json`에 보존합니다.


텍스트 수직 정렬: 경로 글자는 공통 타이포그래피의 줄 높이와 기준선을 사용해 버튼 가운데에 배치합니다. 한글·영문, 크기3종, 현재 위치의 굵기, 호버와 RTL을 독립 텍스트 위젯의 픽셀과 비교합니다. 일반 텍스트 원점으로 되돌아가는 회귀도 함께 검사합니다.
