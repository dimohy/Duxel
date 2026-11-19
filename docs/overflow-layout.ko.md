# 우선순위 넘침 배치

Last synced: 2026-11-11

`Dux.Overflow`와 `Dux.VerticalOverflow`는 항목 위젯과 호출자가 제공한 표시기를 소유합니다. 제한된 주축 공간에 맞는 항목만 렌더링합니다. 숨긴 위젯은 인스턴스와 모델 값을 유지하지만 렌더링·입력·현재 접근성 노드 게시에는 참여하지 않습니다. 표시기는 일반 위젯이며 이 배치 자체가 메뉴를 만들지는 않습니다.

```csharp
var first = Fluent.Button("First");
first.Size = new(100, 32);
var second = Fluent.Button("Second");
second.Size = new(100, 32);
var overflow = Dux.Overflow([first, second], Fluent.Badge("More"),
    new(Spacing : 8), priorities : [2, 0]);
using var root = Dux.SizedBox(overflow, width : 150, height : 48);
// Render root inside the caller's ordinary widget/layout flow.
```

우선순위가 높은 항목을 먼저 남깁니다. 우선순위가 같으면 기본적으로 논리적 끝부터 숨기며 `CollapseFrom = UiOverflowCollapseDirection.Start`로 동률 처리 방향을 바꿉니다. 남은 항목의 원래 순서는 유지합니다. `RightToLeft`는 수평 배치 방향만 바꾸며 우선순위나 논리적 순서를 바꾸지 않습니다. 수직 배치는 아래로 진행합니다.

`UiOverflowLayout.Resolve(items, available, indicatorExtent, visible, options)`는 렌더러와 독립적인 알고리즘입니다. 입력을 모두 검증한 뒤 처음 `items.Length`개의 표시 여부만 씁니다. `Spacing`·사용 가능 공간·항목 크기·표시기 크기는 유한한 음이 아닌 값이어야 합니다. 위젯의 주축 제약은 유한해야 하며 항목은 주축의 고유 크기로 측정합니다.

항목을 숨길 때만 표시기 공간을 예약하므로 표시기 때문에 추가 항목이 숨겨질 수 있습니다. `MinimumVisible`은 지정 개수까지 항목을 남깁니다. 그 항목들이 들어가지 않으면 `Result.Fits`가 false이며 부모 클립이 실제 표시 영역을 제한합니다. 표시기가 공간보다 크면 잘라내고 `IndicatorClipped`로 알립니다. 숨긴 항목을 자동으로 메뉴 항목으로 바꾸지 않습니다.

`Items`에는 표시기가 포함되지 않습니다. `SetPriorities`는 항목마다 우선순위 하나를 요구하며 배치 캐시를 무효화합니다. `Options` 변경도 배치를 무효화합니다. `Visibility`는 빌려 쓰는 span이며 첫 배치 전에는 비어 있고 `Result`는 null입니다. 두 값은 마지막 배치 결과로, 아직 적용하지 않은 옵션 변경의 결과를 보장하지 않습니다. 소유자가 항목과 표시기를 해제하며 숨김만으로 해제하거나 교체하지 않습니다. 복원된 접근성 노드는 새 런타임 식별자를 받으며 이전 클라이언트는 계속 사용할 수 없는 상태입니다.

`./run-gallery.ps1` 실행 → **컨테이너** → **Overflow 공통 배치**로 스크롤하세요. 카운터를 누르고 폭을 펼쳤다가 줄이고, 4번 항목을 우선 표시하고 RTL로 바꿉니다. 다른 페이지에 갔다 돌아와도 카운터는 유지됩니다. 배지는 표시 여부를 알리는 표시기이며 공통 Menu와 TabList 넘침 연결은 별도 안내에서 확인할 수 있습니다.

검증 기록은 `artifacts/overflow-layout-2026-11-11/verification.json`입니다. 관리 SDK 11.0.100/런타임 11.0.0을 고정했습니다. CPU 26검사에는 독립적인 결정론적 사례 1,200개와 16/512항목 resolver의 준비 후 할당 0바이트 검사가 포함됩니다. GPU 66검사는 MSAA 1/4, 위치·우선순위·RTL·수직 배치·클리핑·폭 0의 잔상 제거·획득 이미지 재사용을 확인합니다. 외부 Windows UIA 클라이언트 4검사와 소유 네이티브 호스트 3검사는 숨긴 제공자의 무효화와 복원 후 위젯 인스턴스·모델 유지를 확인합니다. 관찰 샘플은 새 실제 조작 다섯 개를 포함한 49검사를 통과했습니다. 전체 데스크톱 입력·음성 안내·Fluent 일치·NativeAOT 검증이나 일반적인 성능 개선을 뜻하지 않습니다.

고정 Fluent 근거는 Overflow·Menu·TabList를 별도로 조합합니다. [공식 넘침 탭 예제](https://github.com/microsoft/fluentui/blob/8830c10bfb2f4c1bdd540287b09713b9679cd1ad/packages/react-components/react-tabs/stories/src/Tabs/TabListWithOverflow.stories.tsx)의 조합을 적용한 현재 구현은 [탭 넘침](fluent2-tab-overflow.ko.md)에서 확인할 수 있습니다.
