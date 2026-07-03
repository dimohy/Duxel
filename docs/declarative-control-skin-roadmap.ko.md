# Declarative Control Skin Roadmap

다음 세션에서 이어서 시작할 숙제다.

## 목표

Duxel 사용자가 C# 선언형 UI를 SwiftUI, Flutter, Jetpack Compose처럼 편하게 작성하면서도, 컨트롤의 실제 렌더링 모양을 앱/디자인 단위로 자유롭게 바꿀 수 있게 만든다.

현재 Duxel은 다음을 이미 제공한다.

- `IUiDesign` / `UiCompiledDesign`으로 앱 시작 전에 디자인 고정
- `UiTheme`으로 색상 팔레트 변경
- `UiDesignTokens`로 반경, 보더, 포커스 링, 눌림 오프셋 같은 공통 형상 변경
- `IUiViewStyle`, `.Background(...)`, `.Border(...)`, `.CornerRadius(...)`, `.Panel(...)` 같은 view-local shape modifier
- `UiComponent`, `DuxelView.Custom(...)`, `IUiCustomWidget` 기반 커스텀 위젯 작성

하지만 아직 부족한 것은 컨트롤 타입별 렌더링 전략을 교체하는 공식 skin layer다. 예를 들어 모든 `Button`을 pill 스타일로, `TextField`를 underline 스타일로, `Segmented`를 Material 스타일로 바꾸는 작업이 현재는 토큰/테마/커스텀 위젯에 흩어져 있다.

## 다음 작업 시작점

1. `UiControlSkin` 또는 `UiControlStyleSet` 이름의 컴파일된 skin 계약을 설계한다.
2. `UiCompiledDesign`에 skin set을 추가하되 기존 앱은 `Windows11` 기본 skin을 자동 사용하게 한다.
3. 우선 적용 대상은 다음 컨트롤로 제한한다.
   - `Button`
   - `InputText` / `TextField`
   - `Segmented`
   - `Slider`
   - `Checkbox` / `Toggle`
   - `Scrollbar`
4. 각 컨트롤 구현이 색상과 토큰을 직접 읽기 전에 skin resolver를 거치도록 정리한다.
5. skin은 런타임 테마 파싱 결과가 아니라 C# 타입 또는 source-generated 값으로 컴파일 타임에 고정될 수 있어야 한다.
6. per-view override도 가능해야 한다.
   - 예: `Dux.Button("Save").ControlStyle(MyButtonStyle.PrimaryPill)`
   - 예: `Dux.TextField("name", name).ControlStyle(MyTextFieldStyle.Underline)`
7. 기본 `Windows11` / `Windows11Dark` skin은 지금 개선한 모양을 보존한다.

## 설계 원칙

- 테마는 색상 팔레트, 토큰은 공통 수치, skin은 컨트롤 타입별 렌더링 정책을 담당한다.
- 앱 코드는 여전히 `Dux.Button(...)`, `Dux.TextField(...)`, `Dux.EnumSegmented(...)`처럼 짧아야 한다.
- 즉시 모드 내부 구현은 유지하되, 사용자 API는 선언형 값/스타일 중심이어야 한다.
- NativeAOT에서 reflection 없이 동작해야 한다.
- skin 교체가 성능 핫패스에 박싱/할당을 늘리면 안 된다.
- 기존 `UiTheme`, `UiDesignTokens`, `IUiViewStyle`과 충돌하지 않고 상위 개념으로 합성되어야 한다.

## 제안 API 초안

```csharp
public readonly struct ProductDesign : IUiDesign
{
    public static UiCompiledDesign Create()
        => UiCompiledDesign.Windows11Dark with
        {
            Tokens = UiDesignTokens.Windows11 with
            {
                ControlCornerRadius = 8f
            },
            ControlSkins = UiControlSkins.Windows11Dark with
            {
                Button = ProductButtonSkin.Pill,
                TextField = ProductTextFieldSkin.Underline,
                Segmented = ProductSegmentedSkin.Attached
            }
        };
}
```

```csharp
Dux.Button("Save", Save)
    .ControlStyle(ProductButtonStyle.PrimaryPill);

Dux.TextField("name", name)
    .ControlStyle(ProductTextFieldStyle.Underline);
```

## 구현 후보 구조

- `UiControlSkins`
  - `UiButtonSkin Button`
  - `UiTextFieldSkin TextField`
  - `UiSegmentedSkin Segmented`
  - `UiSliderSkin Slider`
  - `UiToggleSkin Toggle`
  - `UiScrollbarSkin Scrollbar`
- 각 skin은 `readonly record struct` 또는 readonly struct로 시작한다.
- 렌더링에 필요한 상태는 `UiImmediateContext`가 계산하고, skin은 색상/반경/패딩/경계/채움 정책을 반환한다.
- 완전 커스텀 draw delegate는 마지막 단계로 둔다. 먼저 값 기반 skin으로 시작한다.

## 검증 샘플

다음 세션에서는 최소 두 샘플을 만든다.

- `samples/fba/declarative_dashboard_fba.cs`
  - 기본 Windows 11 dark skin이 계속 깔끔하게 보이는지 확인
- 새 샘플 후보: `samples/fba/control_skin_showcase_fba.cs`
  - 같은 화면을 `Windows11`, `Pill`, `Underline`, `Compact` skin으로 바꿔 렌더링
  - Button, TextField, Segmented, Slider, Checkbox, Scrollbar를 한 화면에 배치

## 완료 기준

- `UiCompiledDesign`으로 컨트롤별 skin set을 지정할 수 있다.
- 최소 Button/TextField/Segmented의 렌더링 모양을 skin으로 바꿀 수 있다.
- 기존 theme/token 기반 Windows 11 기본 모양은 깨지지 않는다.
- per-view style override가 최소 한 컨트롤에서 동작한다.
- `dotnet build Duxel.slnx -c Release`가 경고 없이 통과한다.
- FBA 샘플 `declarative_dashboard_fba.cs`와 control skin showcase가 `./run-fba.ps1 ... -NoCache`로 실행된다.

