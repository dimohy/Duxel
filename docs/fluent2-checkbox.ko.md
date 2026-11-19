# Fluent 2 Checkbox

Checkbox와 Markdown 작업 목록 지표가 `UiFluentCheckboxPalette.Resolve(context, state, value)`를 공유합니다. 시스템 대비 모드에서는 활성 라벨·테두리·마크에 CanvasText, 비활성 상태에 GrayText를 적용합니다. 선택된 배경은 Canvas이며 선택되지 않은 상태와 혼합 상태의 배경은 투명하게 유지합니다. 호버·누름에서도 같은 시스템 색상 역할을 사용합니다. 컨텍스트의 비활성 상태를 작업 목록에도 적용해, 일반 테마에서 활성 마크 색상이 남던 네 화면을 고쳤습니다. 기존 스킴 전용 오버로드는 원래 팔레트 계약을 유지합니다. GPU 화면 400개를 오차 없이 비교하며 세 값·네 상태·두 크기·모양·라벨 위치, 밝음·어두움과 실제 Markdown 작업 목록을 다룹니다. 같은 최종 검사 DLL로 이전 제품을 실행하면 204개가 실패합니다. 일반 테마 화면 200개 중 196개는 같고 비활성 작업 목록을 고친 네 개만 달라졌으며, 기존 Markdown 화면 677장은 모두 같습니다. 기존 포함 전체 검사 1,387개가 통과했습니다. [공식 Checkbox 스타일](https://github.com/microsoft/fluentui/blob/master/packages/react-components/react-checkbox/library/src/components/Checkbox/useCheckboxStyles.styles.ts)과 분리된 Chromium CSS 진단을 근거로 색상 역할을 정했습니다. 실제 Fluent 구성요소 전체를 실행하거나 브라우저 픽셀 전체와 비교한 검증은 아닙니다. 근거: `artifacts/checkbox-system-colors-2026-11-17/verification.json`.

2026-11-11 [공통 Toggle 접근성](widget-accessibility.ko.md): Checkbox/Switch/ToggleButton 역할과 꺼짐/켜짐/혼합 상태를 단일 ToggleState·기존 동작/바인딩 정책·Windows Toggle 속성 알림에 연결했습니다. Checked는 두 상태 표현으로 유지합니다. Core21/관찰134/생성기115개·외부 Toggle11개/호스트4개·체크 메뉴6개/호스트3개가 통과했고 GPU47개·본문73개 일치로 그리기를 보존했습니다. 앞서 그린 공유 형제는 요청된 다음 프레임에 반영합니다. 전체 발화/강제 색상/NativeAOT는 남아 있습니다. 근거: artifacts/toggle-accessibility-2026-11-11/verification.json.

2026-11-06. 기존에 연결된 Checkbox의 초기 표현을 개선했다. 새로운 상위 컴포넌트를 추가한 수로 계산하지 않는다. [공식 사용 지침](https://fluent2.microsoft.design/components/web/react/core/checkbox/usage)은 현재 Preview로 표시되며, 구현 참고는 기존에 고정한 Microsoft Fluent UI `8830c10bfb2f4c1bdd540287b09713b9679cd1ad`의 Checkbox 스타일·동작이다. `artifacts/fluent-checkbox-2026-11-06/sources.json`에 출처와 재사용 해시를 기록했다.

`Fluent.Checkbox(label, binding)`의 bool/bool? 생성 위젯과 `Dux.Checkbox`는 같은 `UiToggle` 동작·상태·수명·입력 구현을 사용한다. null은 혼합 상태이며 활성화하면 true, 이후에는 true/false로 전환한다. 라벨과 지표 모두 클릭할 수 있고 Tab/Space로 조작한다. Enter는 값을 바꾸지 않는다. 레거시 즉시 Checkbox/숫자 스킨 경로는 보존하며 전체 기본 API 이전이 완료됐다고 보지 않는다.

## 컴파일 시점 표현 선택

기본은 medium·square·label after다. 크기16/20px 지표·12/16px 공식 마크, square/circular의 서로 다른 프레임·혼합 마크, before/after의 독립 배치 구현을 닫힌 제네릭으로 연결한다. 사용자 구현도 같은 인터페이스로 교체할 수 있다.

```csharp
[UiWidgetStyle(typeof(UiToggle<,,,>), typeof(bool?),
    typeof(UiNullableToggleValue), typeof(UiSpaceButtonBehavior),
    typeof(UiFluentCheckboxPresentation<UiFluentLargeCheckboxSize,
        UiFluentCircularCheckboxShape, UiCheckboxLabelBefore>))]
public partial class ProductCheckbox;
```

생성된 `ProductCheckbox`를 상속하고 `OnChanged`를 확장할 수 있다. 동작 정책과 배치/그리기를 별도로 선택한다. 런타임 테마 레지스트리나 문자열 검색을 추가하지 않았다.

`IUiTogglePresentation.BeginText`의 기본 구현은 빈 스코프라 기존 사용자/레거시 표현은 유지된다. Fluent Checkbox는 Regular14px/20px 행 높이를 선택하고 종료·예외 시 폰트를 복원한다. 명시적 DirectText·메트릭 백엔드·TypographyFonts가 필요하다. Atlas만 설정한 저수준 소비자는 이를 추가해야 하며 다른 폰트로 조용히 대체하지 않는다.

## 상태와 공식 아이콘

unchecked와 mixed는 투명 배경, checked는 compound brand 배경이다. mixed는 브랜드 테두리·사각형/원형 마크이며 이전 가로 막대·채운 배경을 교체했다. disabled는 투명 배경·disabled stroke/foreground를 사용한다. hover/pressed에서 브랜드와 중립 색상을 구분한다. 실제 `grey`·`brandWeb` 표에서 해석한24개 상태 조합의 독립 fixture를 검사한다. 주석의 오래된 브랜드 색이나 반올림으로 값을 추정하지 않는다.

`[UiFluentIcon("checkmark", UiFluentIconStyle.Filled, 12)]`처럼 정확한 크기를 생성 시점에 선택한다. 고정 크기 출력은 해당 글리프와 폰트의 직접 호출 하나다. 크기0인 기존 선언은 기존20/24 선택을 유지한다. 없는 글리프·지원하지 않는 크기는 DUXWGT008이며 다른 크기를 대신 쓰지 않는다. 기존2인수 특성 생성자도 보존했다. Checkbox의6개 마크는 기존 번들 폰트를 재사용하며 새 폰트나 런타임 JSON을 추가하지 않았다.

## 실행과 검증 범위

```powershell
$run = @{
    Path = 'samples/fba/widget_tree_theme_fba.cs'
    Managed = $true
    NoCache = $true
    DotnetPath = 'C:\Users\dimohy\.cache\duxel-dotnet-10.0.301\dotnet.exe'
}
.\run-fba.ps1 @run -ExtraArgs @('--checkbox')
```

8조합·혼합/선택/비선택·disabled를 같은 상태로 직접 확인할 수 있다. `tests/Duxel.Rendering.Tests`의 `--checkbox-only`는 GPU 실행 전 입력·콜백·타이포그래피·공식 색상 fixture를 검사한다. GPU 검사는8개 사용자 생성 스타일×라이트/다크×MSAA1/4×raster/vector에서 직접 경로와 전체 픽셀을 비교한다. 라벨 없는 마크를 제거한 음성 대조군으로 실제 마크가 그려졌는지도 확인한다. 정확한 최신 수치·실행 파일·회귀 이미지는 `artifacts/fluent-checkbox-2026-11-06/verification.json`을 따른다.

공식 브라우저와 픽셀 일치, UIA, 논리적 RTL, 강제 색상, 긴 라벨의 공식 줄바꿈/Field, 포커스·모션의 완전한 공식 동치는 미완료다. 이 검증은 전체 Checkbox 또는 전체 Fluent 지원 완료를 뜻하지 않는다. 실행 시작/첫 제출/자동 종료는 사용자의 화면·수동 입력 승인과 구분한다. 새 FPS/Private/Pi 개선율은 측정하거나 주장하지 않았다.

2026-11-12 — 결정론적인 기본 제공 리프 표현을 기존 부모 측정 캐시에 연결했으며 그리기와 입력은 계속 실행합니다. 사용자 특성은 명시적 동의가 필요하고 라벨·옵션·크기·항목·환경 변경은 배치를 무효화합니다. [공통 리프 측정 계약](widget-cpu-retention.ko.md)과 artifacts/leaf-layout-retention-2026-11-11/verification.json을 참고하세요. 전체 NativeAOT·성능 게이트는 남아 있습니다.
