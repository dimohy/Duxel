# Fluent SegmentedControl

`Fluent.SegmentedControl`은 여러 항목 중 하나를 고르는 컨트롤입니다. 네 가지 공식 표준 스타일, 단일 선택·키보드·RTL·아이콘·읽지 않음 표시와 가로 스크롤을 지원합니다. 갤러리 메뉴의 미구현 배지는 제거했습니다. 현재 NEW 배지는 Select에만 표시합니다. 팩터리 연결은50/55이며, 전체 Fluent 적합성이나 실제 UIKit 픽셀 일치를 뜻하지 않습니다.

```csharp
var value = new UiState<string?>("day");
UiSegmentOption[] items = [new("day", "일"), new("week", "주"), new("month", "월", Unread : true)];
var control = Fluent.SegmentedControl("기간", value.Binding, items,
    new(Width : 320, EqualWidth : true, RightToLeft : false, ReducedMotion : false));
```

## 선택과 배치

값으로 항목을 식별하므로 순서를 바꿔도 선택을 유지합니다. 중복 값은 상태를 바꾸기 전에 거부합니다. 항목 교체는 진행 중인 누르기 동작을 취소합니다. 승인된 선택만 읽지 않음 표시를 지웁니다. 제어형 바인딩이 변경을 거부하면 선택과 접근성 상태를 기존 값으로 유지합니다. 입력마다 바인딩을 한 번 호출합니다.

`EqualWidth`는 균등 너비이며 false이면 자연 너비 비율로 배치합니다. Width0은 자연 너비입니다. 고정 너비 안의 긴 글은 한 줄 말줄임으로 표시하되 접근성 이름은 전체 문장을 유지합니다. 텍스트 대신20px 아이콘도 사용할 수 있습니다. 비활성 항목을 건너뛰며 RTL에서는 물리 배치와 좌우 방향키가 반대로 동작합니다.

## 가로 스크롤과 GPU 캐시

`IsFixedWidth`는 기본true입니다. false로 지정하면 항목의 자연 너비를 유지하고 Width를 보이는 영역으로 사용합니다. 드래그·가로 휠·세로 휠로 이동하며 드래그를 끝낼 때 아래 항목을 선택하지 않습니다. 프로그램이나 방향키로 선택한 항목을 같은 프레임에 화면 안으로 드러냅니다. 탭과 공통 수평 입력 모듈을 사용합니다.

스크롤 콘텐츠는 위젯이 소유한 `UiPaintGroup`에 보관합니다. UV 카메라로 필요한 구간만 읽으며, 숨은 항목이 있는 쪽의1/4 너비에 선형 투명 마스크를 적용합니다. 배경색으로 가리지 않습니다. 배경과 겹친 콘텐츠의 최종 합성에 마스크를 한 번 적용하므로 임의 배경과 글자 RGB 커버리지를 유지합니다. 고대비에서는 페이드를 끕니다.

스크롤 위치·UV·실제 배경만 바뀌면 콘텐츠를 다시 캡처하거나 글자를 다시 측정하지 않습니다. 글꼴·배치·스타일·활성 상태·읽지 않음·초점·선택 애니메이션처럼 실제 그리기가 바뀌면 새 버전을 만듭니다. 아이콘은 `WithContentVersion` 또는 아이콘 제공자의 버전으로 변경을 알립니다. 버전을 제공하지 않은 사용자 아이콘은 외부 상태가 바뀔 수 있으므로 정적인 그림으로 간주하지 않습니다. 위젯 종료 시 캐시를 정리합니다.

## 스타일과 모션

Style은 NeutralOverNavBarPill(기본), NeutralOverCanvasPill, BrandOverNavBarPill, BrandOverCanvasPill입니다. 공식 표준 밝음/어두움·활성/비활성16조합을 사용하며 시스템 고대비 색상이 우선합니다. 폐기된 primaryPill/onBrandPill 별칭, iOS darkElevated 및 사용자 브랜드 토큰 전체 재정의는 제공하지 않습니다.

기본 글자15·줄 높이20·최소 높이32, 좌우 외부 여유16, 콘텐츠 여유16/6, 모서리16, 읽지 않음 점6과 오프셋6/3을 적용합니다. `HorizontalInset`과 `UnreadLabel`을 조절할 수 있습니다. 선택 이동은200ms cubic-bezier(0,0,.58,1)이며 ReducedMotion이면 즉시 반영합니다. 움직임이 끝나면 애니메이션 프레임을 예약하지 않습니다.

## 실행과 검증 범위

저장소 루트에서 `.\run-gallery.ps1`을 실행한 뒤 **Fluent 컨트롤 → 입력과 선택 → SegmentedControl**로 이동하세요. 네 스타일과 다섯 번째 가로 스크롤 예제마다 현재 설정의 소스를 붙였습니다.

[캐시·마스크 검증](../artifacts/fluent-segmented-cache-2026-11-18/result.json), [입력 검증](../artifacts/fluent-segmented-scroll-2026-11-18/result.json), [공통 마스크·UV 계약](horizontal-opacity-mask.ko.md). 실제 Linux 실행·고정 RC3 NativeAOT·외부 보조공학·실제 UIKit 화면 비교는 아직 검증하지 않았습니다.

공식 기준: [Microsoft 구현](https://github.com/microsoft/fluentui-apple/blob/03928222b0fde3621fd5b6ea5c7ff1aeefdc1f95/Sources/FluentUI_iOS/Components/SegmentedControl/SegmentedControl.swift), [토큰](https://github.com/microsoft/fluentui-apple/blob/03928222b0fde3621fd5b6ea5c7ff1aeefdc1f95/Sources/FluentUI_iOS/Components/SegmentedControl/SegmentedControlTokenSet.swift). 이전 조사에서 고정한 원본을 재사용했습니다.
