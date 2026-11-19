# Fluent 2 Switch

Windows의 네이티브 기준선 보존 후 래스터 상단 투명 여백이 글자 높이에 포함되어 라벨이 낮아지던 회귀를 수정했습니다. 래스터의 세로 경계를 실제 글자 영역으로 잡고 원래 기준선 위치를 보존합니다. V-Sync·이벤트 대기 모드를 포함한5개 라벨, 크기2종·앞/뒤 배치·래스터/벡터·MSAA1/4의80개 검사에서 글자와 트랙 중심 차이가 픽셀 격자 오차0.5px 이내입니다. [검증 근거](../artifacts/fluent-messagebar-2026-11-15/windows-switch-vertical-verification.json).

2026-11-11 [공통 Toggle 접근성](widget-accessibility.ko.md): Checkbox/Switch/ToggleButton 역할과 꺼짐/켜짐/혼합 상태를 단일 ToggleState·기존 동작/바인딩 정책·Windows Toggle 속성 알림에 연결했습니다. Checked는 두 상태 표현으로 유지합니다. Core21/관찰134/생성기115개·외부 Toggle11개/호스트4개·체크 메뉴6개/호스트3개가 통과했고 GPU47개·본문73개 일치로 그리기를 보존했습니다. 앞서 그린 공유 형제는 요청된 다음 프레임에 반영합니다. 전체 발화/강제 색상/NativeAOT는 남아 있습니다. 근거: artifacts/toggle-accessibility-2026-11-11/verification.json.

Last synced: 2026-11-16

기본 `Fluent.Switch`는 사용자 위젯과 같은 `UiToggle` 상속·바인딩·소스 생성 경로를 사용한다. 기존 개별 즉시 `Switch` 호환 경로는 보존한다. 새 표현을 사용하려면 생성 위젯 또는 닫힌 제네릭 직접 호출을 사용한다.

```csharp
var enabled = new UiState<bool>(false);
var control = Fluent.Switch("Notifications", enabled.Binding);

[UiWidgetStyle(typeof(UiToggle<,,,>), typeof(bool), typeof(UiBooleanToggleValue),
    typeof(UiSpaceButtonBehavior),
    typeof(UiFluentSwitchPresentation<UiFluentSmallSwitchSize, UiSwitchLabelAbove>))]
public partial class CompactSetting;
```

`UiFluentMediumSwitchSize`/`UiFluentSmallSwitchSize`와 `UiSwitchLabelAfter`/`UiSwitchLabelBefore`/`UiSwitchLabelAbove`를 선택한다. 두 크기와 세 배치를 타입으로 연결하며 런타임 스타일 레지스트리를 사용하지 않는다. 기본 medium/after 표현도 같은 구현에 전달한다. 생성 클래스를 상속해 `OnChanged`를 확장할 수 있다. 상태와 콜백은 프레임 중 정상 처리하며, 대기 프레임에서는 바인딩을 쓰지 않는다.

크기·폰트는 medium40×20·Regular14/20, small32×16·Regular12/16이다. 원형 손잡이는 공식 Filled circle20 글리프를 소스 생성기로 확정하고18/14px 상자에서 그린다. 레이블 앞·뒤·위 배치는 각각의 측정 알고리즘을 사용하며, 라벨 없는 루트 높이는 주변 폰트 높이에 영향받지 않는다. 기존 폰트 자산을 재사용한다.

포인터 놓기와 Space 해제에 선택이 바뀐다. Enter는 활성화하지 않으며 비활성 컨트롤의 클릭을 무시한다. bool 바인딩만 제공하고 혼합 상태는 Checkbox에서 사용한다. 외부 바인딩은 위젯이 해제하지 않는다.

[공식 지침](https://fluent2.microsoft.design/components/web/react/core/switch/usage)은 조회 시 Preview다. 고정 React 참조는8830c10bfb2f4c1bdd540287b09713b9679cd1ad이며 원본·해시는 `artifacts/fluent-switch-2026-11-06/plan.json`에 있다. Checkbox와 겹치는 브랜드/중립 색은 공통 팔레트를 재사용하되 Switch의 투명 테두리·항상 같은 라벨 색·비활성 선택 표면을 별도로 해석한다.

`--switch` 갤러리는 여섯 생성 스타일과 공유 바인딩·비활성 예를 제공한다. 관리 .NET10과 고정 .NET11 RC3 NativeAOT를 구분한다. 검증 기록은 같은 폴더의 `verification.json`을 따른다. 시험 이미지의 일부 상태가 축소된 창 밖으로 잘리는 것을 발견해 두 열로 수정했고, 각 상태의 전체 경계와 실제 픽셀 존재를 검사한다. 생성/직접 경로의 동치만으로 올바른 화면이라고 판단하지 않는다.

공식 브라우저 픽셀 동치, 모션/감소된 모션, UIA·required/Field·disabledFocusable, RTL·강제 색상·줄바꿈 라벨과 지표 영역만의 hover 판정은 미완료다. 현재 포인터 입력은 공통 루트 경계를 사용한다. 색/크기/배치 개선을 전체 Switch 또는 Fluent 완료로 세지 않는다. 새로운 FPS·할당·Private·Pi 개선율을 주장하지 않는다.

2026-11-12 — 결정론적인 기본 제공 리프 표현을 기존 부모 측정 캐시에 연결했으며 그리기와 입력은 계속 실행합니다. 사용자 특성은 명시적 동의가 필요하고 라벨·옵션·크기·항목·환경 변경은 배치를 무효화합니다. [공통 리프 측정 계약](widget-cpu-retention.ko.md)과 artifacts/leaf-layout-retention-2026-11-11/verification.json을 참고하세요. 전체 NativeAOT·성능 게이트는 남아 있습니다.
