# ProgressIndicator

`./run-gallery.ps1` → **Fluent 컨트롤 → 알림과 진행 상태 → ProgressIndicator**에서 확인합니다. 진행률 증가·대기 전환·막대 숨김·모션 줄이기를 조작하면 각 예제 바로 아래 소스도 현재 옵션을 반영합니다.

```csharp
var progress = Fluent.ProgressIndicator(new(
    Label : "파일 업로드",
    Description : "선택한 파일을 서버로 보내고 있습니다.",
    PercentComplete : .25f,
    Width : 360,
    ValueText : "파일 1/4개"));
progress.Options = progress.Options with { PercentComplete = .75f };
```

ProgressBar가 막대 자체라면 ProgressIndicator는 작업 이름·막대·설명을 함께 배치하는 위젯입니다. UiProgressIndicatorWidget이 자식을 소유하고 UiFluentProgressIndicator가 생성된 Fluent 어댑터 역할을 합니다. PercentComplete는 0~1이며 유한한 범위 밖 값은 0~1로 제한합니다. null이면 미확정 진행률입니다. 기본 너비는 180, 막대 높이는 2 논리 픽셀입니다. Width·BarHeight는 양의 유한 값이어야 합니다. NaN과 무한대는 렌더링 전에 거부합니다.

ProgressHidden을 켜면 막대를 그리거나 접근성 요소로 게시하지 않으며 막대 애니메이션도 예약하지 않습니다. 이름과 설명은 그대로 남습니다. 설명은 너비에 맞춰 줄바꿈합니다. 기존 ProgressBar의 300ms 진행률 보간과 대기 파형을 재사용합니다. ReducedMotion은 확정 값 보간을 즉시 끝내고 미확정 상태의 가로 이동을 불투명도 변화로 바꿉니다. 완전 정지 옵션은 아니므로 대기 상태의 불투명도 변화는 계속됩니다.

진행률 요소는 포커스를 받지 않는 읽기 전용 요소입니다. 이름은 일반 AccessibleName, 보이는 Label, Label이 없을 때 AccessibleLabel 순으로 결정합니다. 확정 값의 범위는 0~100이고 목표 값을 즉시 게시하므로 시각적 보간 중에도 실제 진행률을 읽습니다. Description은 도움말, ValueText는 네이티브 ItemStatus와 값 텍스트에 제공합니다. 미확정 상태는 Busy로 표시하고 숫자 범위는 생략합니다.

`UiProgressIndicatorWidget<TBehavior, TPresentation>`을 상속하면 작업 이름과 설명, 접근성 코드를 그대로 사용하면서 막대의 전환과 모양을 바꿀 수 있습니다. 독립 진행률 막대와 같은 `IUiProgressBehavior<TBehavior>`, `IUiProgressPresentation`을 사용합니다. 표현 타입은 막대의 크기를 정하고 그립니다. 화면에 보이는 값이 전환 중이어도 접근성 도구는 요청한 진행률을 읽습니다. Fluent 팩터리는 기존 Fluent 동작과 표현을 선택합니다.

현재 문자열 레이블·설명과 네이티브 상태 메타정보를 지원합니다. React 노드·CSS 슬롯·aria-valuetext의 브라우저 동작 전체를 복제하거나 모든 색상·모션·외부 접근성·모달 조합의 적합성 검증을 완료했다는 뜻은 아닙니다. 전체 목표에는 이 검증과 남은 기능이 계속 포함됩니다.

검증: artifacts/fluent-progress-indicator-2026-11-17/result.json. CPU 14건과 기본 GPU 8건은 동일 Core 지문을 확인해 재사용했습니다. 모션 줄이기를 정지로 가정했던 검사 1건은 현재 공식 구현의 불투명도 변화에 맞춰 보완했고, 해당 CPU 1건과 추가 Vulkan 4건만 확인했습니다. 카탈로그 4건·갤러리 GPU 270건·Windows/Linux 호스트 빌드 통과. 실제 Linux와 고정 RC3 NativeAOT는 실행하지 않았습니다.

공식 API: https://github.com/microsoft/fluentui/blob/master/packages/react/src/components/ProgressIndicator/ProgressIndicator.types.ts
현재 공식 모션 구현: https://github.com/microsoft/fluentui/blob/master/packages/react-components/react-progress/library/src/components/ProgressBar/useProgressBarStyles.styles.ts

## Windows 갤러리와 공식 범위

[Fluent 웹 Progress bar](https://fluent2.microsoft.design/components/web/react/core/progressbar/usage/)의 확정·미확정 선형 막대를 Windows 갤러리에서 확인했습니다. 외부 UI Automation과 실제 마우스 검사 11개가 통과했습니다. 진행률을 25%에서 50%로 올리면 360픽셀 막대의 채운 길이가 90픽셀에서 180픽셀로 늘었습니다. 대기 상태에서는 숫자 범위를 제공하지 않고 상태 문구를 유지했습니다. 막대를 숨기면 접근성 요소도 사라지고 작업 이름과 설명은 남았습니다. 확정 진행률은 읽기 전용으로 게시되며 외부 값 변경 요청을 거부합니다. [Microsoft UI Automation 계약](https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-supportprogressbarcontroltype)

Windows .NET 10.0.15와 .NET 11.0.0에서 ProgressIndicator 19개와 공통 컨트롤 227개를 각각 통과했습니다. 캡처 네 장과 검사 기록은 `L:/Cache/Duxel/fluent-progress-live-20261120/verification.json`에 보존했습니다.

[Fluent Android Progress indicator](https://fluent2.microsoft.design/components/android/core/progressindicator/usage)는 선형과 원형을 모두 정의합니다. 앞선 Windows 검증은 선형 막대를 다뤘습니다. Windows 원형 예제는 아래에서 확인할 수 있습니다. Android 기기의 TalkBack과 웹 브라우저 픽셀 대조는 별도 검증이 필요합니다. Duxel의 웹 지침 예제는 아래에서 확인할 수 있습니다.

## 원형 진행 표시

`Fluent.CircularProgressIndicator`는 확정값을 위쪽부터 채우고, 값이 `null`이면 270도 원호를 1초 주기로 회전시킵니다. 확정값 변경은 750ms 동안 보간하며 접근성 도구는 요청한 값을 즉시 읽습니다. 기본 크기는 12이고 `Size`로 크기를 지정합니다. 두 축을 지정하면 작은 축으로 원을 그립니다. 기본 색은 현재 테마의 중립색이며 `Color`로 바꿀 수 있습니다. `ReducedMotion`을 켜면 확정값을 즉시 그리며, 대기 원호는 멈춰 있어도 접근성에는 대기 상태를 전달합니다. [고정한 Android 원본](https://github.com/microsoft/fluentui-android/blob/428d208f50dcce27a8825fb60ddfdaa517eeb7b1/fluentui_progress/src/main/java/com/microsoft/fluentui/tokenized/progress/CircularProgressIndicator.kt)

```csharp
var ring = Fluent.CircularProgressIndicator(.25f);
ring.AccessibleName = "파일 동기화";
ring.Size = new(40, 40);
ring.Value = .5f;
ring.Value = null;
ring.ReducedMotion = true;
```

갤러리의 **Fluent 컨트롤 → 알림과 진행 상태 → ProgressIndicator**에서 본문을 내려 **원형 진행 표시** 예제를 확인할 수 있습니다. Windows에서 마우스 입력, 읽기 전용 범위, 대기 상태의 범위 제거를 확인했습니다. 25%·50%와 대기·모션 줄이기 캡처의 원호 영역도 대조했습니다. 검증 기록은 `L:/Cache/Duxel/fluent-circular-progress-20261120/verification.json`에 있습니다.

## 웹 진행률 전환과 오류

`FinishIndeterminateCycle = true`를 설정하면 확정값을 받아도 현재의 대기 애니메이션 주기가 끝날 때까지 화면의 막대를 유지합니다. 확정값은 접근성 도구에 즉시 전달합니다. 기본값은 `false`이며, 오류 상태나 모션 줄이기를 켠 경우 즉시 전환합니다. `UiProgressIndicatorOptions`와 `UiProgressValue`에서 설정할 수 있습니다.

진행률을 아는 작업이 실패하면 마지막 값과 함께 `Color = UiProgressColor.Error`를 설정합니다. `Description`에는 복구 방법을, `ValueText`에는 짧은 상태를 넣습니다. 진행률을 모르던 작업은 `ProgressHidden = true`로 막대를 숨기고 설명을 표시할 수 있습니다. [Fluent 웹 진행률 지침](https://fluent2.microsoft.design/components/web/react/core/progressbar/usage/)

갤러리의 **Fluent 컨트롤 → 알림과 진행 상태 → ProgressIndicator**에서 본문을 내려 **웹 진행률 전환과 오류**를 확인할 수 있습니다. 외부 UI Automation과 마우스로 진행률 25%의 오류, 진행률을 모르던 작업의 실패, 다시 시도, 대기 중 완료를 확인했습니다. 근거는 `L:/Cache/Duxel/fluent-progress-web-20261120/verification.json`입니다.

## 강제 색상

시스템 강제 색상이 적용되면 선형 막대의 바탕에는 `CanvasText`, 채우기에는 `Highlight`를 사용합니다. 확정·대기·모션 줄이기가 같은 색을 공유하며, 오류와 복구 방법은 상태 문구와 설명으로 계속 읽을 수 있습니다. 원형은 시스템 전경색 `CanvasText`를 사용합니다. [공식 Fluent의 고정 소스](https://github.com/microsoft/fluentui/blob/bdcf886547c910b749eab2aa9c22c91152dfe6ab/packages/react-components/react-progress/library/src/components/ProgressBar/useProgressBarStyles.styles.ts)

고정 크기 Windows Vulkan 창에서 밝은색·어두운색 팔레트를 각각 적용했습니다. 두 팔레트의 캡처가 .NET 10과 11에서 바이트 단위로 일치했고, 각 팔레트에서 오류와 복구 문구, 읽기 전용 범위를 Duxel 접근성 트리로 확인했습니다. 검증 기록은 `L:/Cache/Duxel/fluent-progress-contrast-20261120/verification.json`에 있습니다.

## 공식 Storybook과 픽셀 비교

[공식 Storybook](https://storybooks.fluentui.dev/react/?path=/docs/components-progressbar--docs)과 Windows Vulkan에서 같은 배경, 너비 160, 50%와 오류 상태 75%를 비교했습니다. Storybook의 50% 채움 색은 밝은색 `#0F6CBD`, 어두운색 `#479EF5`였고 당시 Duxel 기본값은 각각 `#005FB8`, `#60CDFF`였습니다. 어두운색 바탕도 Storybook `#333333`과 Duxel `#3A3A3A` 사이에 차이가 있었습니다.

검증 창에서만 막대 색을 Storybook과 맞췄습니다. 640픽셀을 비교하자 사각형 막대의 차이는 밝은색 324개·어두운색 640개에서 각각 8개로, 둥근 막대는 328개·640개에서 각각 20개로 줄었습니다. 남은 차이는 시작·끝과 채움 경계에 모였습니다. 높이 2의 오류 막대는 오류색 값이 같아도 브라우저의 가장자리 합성이 달라 두 테마에서 각각 320픽셀이 달랐습니다. 캡처와 입력 조건은 `L:/Cache/Duxel/fluent-progress-browser-20261120/verification.json`에 있습니다.

### 기본 테마와 사용자 지정 색

Windows 11 기본 테마의 진행률 채움은 밝은색 `#0F6CBD`, 어두운색 `#479EF5`입니다. 막대 바탕은 각각 `#E6E6E6`, `#333333`입니다. 같은 테마를 쓰는 진행률 컨트롤은 이 기본값을 공유합니다. 일반 모드에서 색을 바꾸려면 `UiTheme.ProgressBarBg`와 `UiTheme.ProgressBarFill`을 지정합니다. `ProgressBarFill`은 브랜드 색과 대기 막대에 쓰입니다. 강제 색상 모드의 선형 막대에는 시스템의 `CanvasText`와 `Highlight`가 우선합니다.

새 기본값을 사용한 Windows 창 두 개가 앞서 색만 맞췄던 검증 창의 캡처와 일치했습니다. .NET 10과 11의 밝은색·어두운색, 사용자 지정, 강제 색상 검사를 각각 통과했습니다. 고정 크기 위젯 이미지 4,046장 중 4,040장이 이전과 일치했고, 차이가 있는 6장은 바뀐 진행률 영역을 담고 있습니다. 가장자리 차이는 이전 브라우저 대조 결과와 같습니다. 근거는 `L:/Cache/Duxel/fluent-progress-palette-20261120/verification.json`입니다.

### 얇은 막대의 배치 대조

공식 Storybook의 75% 오류 막대를 같은 색·너비로 다시 그리고 픽셀 배율 1·2, 세로 위치, 모서리와 클립 조건을 바꿨습니다. 2픽셀 막대를 다시 배치한 세 위치에서는 밝은색과 어두운색 모두 보관한 Windows 캡처와 320픽셀 중 162개가 달랐습니다. 이전 위치에서는 320픽셀 모두 달랐습니다.

Storybook의 네 배치와 간단한 CSS 대조군의 캡처가 두 배율에서 모두 일치했습니다. 위치가 같은 사각형과 둥근형은 여덟 쌍 중 여섯 쌍의 중앙 행이 같았습니다. 나머지 두 쌍에서는 모서리 설정만 바꾸어도 중앙의 가장자리 행이 달랐습니다. 따라서 2픽셀 막대의 비교 수치는 캡처 위치와 브라우저의 그리기 방식에 따라 달라집니다. 검증에는 기존 Duxel 캡처를 재사용했으며 세로 위치에 따라 Duxel의 색이나 불투명도를 보정하지 않았습니다. 근거는 `L:/Cache/Duxel/fluent-progress-edges-20261120/verification.json`입니다.
