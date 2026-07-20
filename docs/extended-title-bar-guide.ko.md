# Duxel 확장 타이틀바 가이드

> 마지막 동기화: 2026-07-20
>
> 대상: Duxel 애플리케이션 개발자, 프레임워크 기여자, 코딩 AI 에이전트
>
> 플랫폼: Windows 11/Windows 네이티브 백엔드
>
> English: [extended-title-bar-guide.md](extended-title-bar-guide.md)

## 목적

`DuxelTitleBarMode.ExtendedContent`는 애플리케이션이 창의 최상단부터 탭, 검색창, 메뉴 등의 UI를 렌더링하게 한다. Duxel은 Vulkan surface 위에 최소화·최대화/복원·닫기 아이콘이 보이도록 그리고 Windows는 네이티브 캡션 동작을 계속 담당한다.

이 가이드는 다음 작업을 다룬다.

- 애플리케이션에서 확장 타이틀바를 활성화하는 방법
- 캡션 버튼 묶음과 겹치지 않는 레이아웃 계산
- 드래그 영역과 상호작용 영역을 올바르게 분리하는 방법
- DPI, 다중 모니터, 최대화 상태에서 지켜야 할 좌표 계약
- 개발자와 코딩 AI가 완료 전에 수행해야 할 검증
- Duxel Windows 백엔드를 수정할 때 보존해야 할 Win32/DWM 계약

완성된 실행 예제는 [`samples/fba/extended_title_bar_fba.cs`](../samples/fba/extended_title_bar_fba.cs)에 있다.

## 라이브러리 지원과 샘플 코드의 경계

이 기능은 Duxel 라이브러리에 구현되어 있다. 샘플이 별도 비공개 타이틀바 백엔드를 구현하는 것이 아니라, 애플리케이션 코드가 공개 라이브러리 계약으로 UI를 조합하는 방법을 보여준다.

이 API는 공개 `0.2.9-preview` 이상 패키지에서 사용할 수 있다. 그보다 새로운 미게시 로컬 변경을 검증할 때만 프로젝트/소스 참조 또는 `run-fba.ps1`을 사용한다.

| 관심사 | Duxel 라이브러리가 제공 | 애플리케이션 또는 샘플 책임 |
|---|---|---|
| Duxel 소유 타이틀바 | `Duxel` 모드에서 런타임이 유효 아이콘, 왼쪽 정렬 제목, 캡션 버튼, 시스템 메뉴 경로, 현재 창 상태에 따른 최대화/복원 아이콘을 그림 | `Duxel`을 선택하고 앱 화면에서 캡션 UI를 중복 구현하거나 동기화하지 않음 |
| 확장 클라이언트 영역 | `DuxelTitleBarMode.ExtendedContent`와 앱 런타임이 콘텐츠를 `(0, 0)`부터 제공 | 모드를 명시적으로 선택 |
| 유효 창 아이콘 | Windows 백엔드가 `IconData`, `IconPath`, Windows 기본 아이콘 중 하나를 결정하고 `TryGetWindowIcon`으로 텍스처 제공 | 애플리케이션 소유 타이틀바에서 크기와 위치 결정 |
| 확장 캡션 버튼 | `Duxel.App`이 `Duxel` 모드와 동일한 공통 48픽셀 Duxel 렌더러를 사용하고, `Duxel.Platform.Windows`는 네이티브 히트 코드, 명령, 호버 상태, Snap Layout 동작을 보존 | `TryGetCaptionButtonBounds`가 반환한 사각형을 예약하고 그 위치에 대체 버튼을 그리지 않음 |
| 캡션 버튼 경계 | 백엔드가 `(clientWidth - 48 * buttonCount, 0)`에 고정되고 `DuxelTitleBarHeight`를 사용하는 논리 묶음을 반환하며 원시 DWM `Y`/높이는 시각 위치에 사용하지 않음 | 매 프레임 현재 값을 조회해 레이아웃에 사용 |
| 창 드래그 | 백엔드가 등록 사각형을 네이티브 `HTCAPTION` 히트 테스트로 변환 | `SetTitleBarDragRegions`에 상호작용하지 않는 빈 영역만 등록 |
| DPI, 리사이즈, 최대화, 작업 표시줄 작업영역 | Windows 백엔드가 Win32/DWM 동작 담당 | 현재 논리 좌표로 앱 레이아웃 재계산 |
| 탭, 검색창, 메뉴, 제목 텍스트 | `ExtendedContent`에서는 Duxel이 고정 레이아웃을 강제하지 않음 | 원하는 애플리케이션 UI를 직접 배치 |

샘플의 `ExtendedTitleBarScreen` 클래스는 재사용 가능한 애플리케이션 측 레이아웃 참고 코드다. `ExtendedTitleBarDiagnostics` 클래스와 그 안의 Win32 P/Invoke 선언은 테스트 계측 전용이다. 실제 앱은 위 공개 API를 사용해야 하며 진단용 히트 테스트 구현을 복사하면 안 된다.

## 모드 선택

| 모드 | 타이틀바 콘텐츠 | 캡션 버튼 | 애플리케이션 콘텐츠 시작점 |
|---|---|---|---|
| `Default` | `UseDuxelTitleBar` 값에 따라 결정 | 선택된 실제 모드에 따름 | 선택된 실제 모드에 따름 |
| `System` | Windows | Windows | 네이티브 클라이언트 영역 |
| `Duxel` | Duxel | Duxel | `DuxelTitleBarHeight` 아래 |
| `ExtendedContent` | 애플리케이션 | Duxel 시각 요소 + Windows/DWM 동작 | `(0, 0)` |

새 코드에서는 의도를 분명하게 나타내기 위해 `TitleBarMode`를 명시한다.

```csharp
Window = new DuxelWindowOptions
{
    Title = "Tabbed Duxel App",
    Width = 1100,
    Height = 700,
    TitleBarMode = DuxelTitleBarMode.ExtendedContent,
    IntegrateSystemChrome = true,
};
```

`Default`는 기존 코드와의 호환성을 위해 다음처럼 해석된다.

- `UseDuxelTitleBar = true` → `Duxel`
- `UseDuxelTitleBar = false` → `System`
- 명시적인 `TitleBarMode` 값은 `UseDuxelTitleBar`보다 우선한다.

`IntegrateSystemChrome`는 DWM 색상, 다크 모드, 테두리, 모서리 같은 시스템 크롬의 시각 통합 옵션이다. 콘텐츠를 타이틀바로 확장하는 기능 자체는 아니며 `ExtendedContent`를 대신하지 않는다.

## Duxel 소유 타이틀바 빠른 시작

앱이 일반적인 크롬만 필요하고 탭 같은 컨트롤을 `y = 0`에 배치할 필요가 없다면 `Duxel`을 사용한다.

```csharp
Window = new DuxelWindowOptions
{
    Title = "Duxel App",
    TitleBarMode = DuxelTitleBarMode.Duxel,
};
```

라이브러리가 유효 창 아이콘, 왼쪽 정렬 제목, 최소화, 최대화/복원, 닫기 버튼을 그린다. 창 드래그, 타이틀 영역 더블클릭 최대화/복원, 우클릭 시스템 메뉴, Alt+Space, 리사이즈 동작도 라이브러리가 담당한다. 애플리케이션 코드는 대체 캡션 버튼을 그리면 안 된다. 복원 상태에서는 최대화 단일 사각형을 표시하고, 최대화 상태에서는 자동으로 겹친 두 사각형 복원 아이콘으로 바뀐다.

## ExtendedContent 개발자 빠른 시작

`Duxel.Windows.App`을 참조하고 `ExtendedContent`를 선택한 뒤 일반 `UiScreen`을 지정한다.

```csharp
using Duxel.App;
using Duxel.Core;
using Duxel.Windows.App;

DuxelWindowsApp.Run(new DuxelAppOptions
{
    Window = new DuxelWindowOptions
    {
        Title = "Tabbed Duxel App",
        Width = 1100,
        Height = 700,
        TitleBarMode = DuxelTitleBarMode.ExtendedContent,
        IntegrateSystemChrome = true,
    },
    Screen = new MainScreen(),
});
```

`MainScreen.Render` 안에서는 매 프레임 다음 순서를 따른다.

1. `y = 0`부터 타이틀바 배경을 그린다.
2. `TryGetWindowIcon`으로 이미 결정된 작업 표시줄/창 아이콘을 얻어 아이콘이 있는 레이아웃이라면 그린다.
3. 탭, 검색창, 메뉴 등 상호작용 컨트롤을 먼저 배치한다.
4. `TryGetCaptionButtonBounds`를 조회하고 모든 앱 컨트롤을 그 사각형 밖에 둔다.
5. 남은 비상호작용 구간만 `SetTitleBarDragRegions`에 등록한다.
6. 최소화·최대화·닫기 대체 버튼은 그리지 않는다. 시각 요소는 Duxel 라이브러리가, 동작은 Windows가 소유한다.

이 패턴의 전체 구현은 [`extended_title_bar_fba.cs`](../samples/fba/extended_title_bar_fba.cs) 샘플에 있다. `ExtendedTitleBarScreen`은 복사하거나 조정할 수 있지만 `ExtendedTitleBarDiagnostics`는 테스트에만 둔다.

## 공개 API 계약

### `TryGetWindowIcon`

```csharp
if (ui.TryGetWindowIcon(out var windowIcon))
{
    windowIcon.Prepare(ui, UiImageEffects.Default);
    // 애플리케이션 소유 타이틀바에 windowIcon.TextureId를 그린다.
}
```

이 API는 네이티브 창에 이미 지정된 유효 아이콘을 반환한다. 확장 타이틀바에 앱 아이콘을 배치할 때 사용하면 타이틀바, 작업 표시줄, Alt+Tab의 아이콘을 일치시킬 수 있다. 텍스처는 다른 `UiImageTexture`와 같은 방식으로 준비하고 그린다.

### `TryGetCaptionButtonBounds`

```csharp
bool available = ui.TryGetCaptionButtonBounds(out UiRect bounds);
```

반환되는 `bounds`는 최소화·최대화·닫기 버튼 묶음 전체를 감싸는 사각형이다.

- 좌표 단위: Duxel 논리 픽셀
- 좌표 원점: 현재 창의 Duxel 클라이언트 영역 `(0, 0)`
- 유효 모드: `ExtendedContent`
- 숨김·최소화 또는 DWM 경계를 제공할 수 없는 순간에는 `false`
- 최대화 상태에서는 `Y`가 음수일 수 있으므로 `Y == 0`을 가정하지 않는다.

버튼 하나의 폭이나 전체 버튼 폭을 하드코딩하지 않는다. Windows 테마, DPI, 접근성 설정, OS 버전에 따라 실제 경계가 달라질 수 있다.

### `SetTitleBarDragRegions`

```csharp
ui.SetTitleBarDragRegions([
    new UiRect(x, y, width, height),
]);
```

각 사각형은 Windows 비클라이언트 히트 테스트에서 `HTCAPTION`으로 처리된다.

- 좌표는 `TryGetCaptionButtonBounds`와 같은 Duxel 논리 클라이언트 좌표다.
- 호출할 때마다 이전 영역 집합 전체를 원자적으로 교체한다.
- `ui.SetTitleBarDragRegions([])`는 등록된 영역을 모두 비운다.
- 모든 좌표는 유한해야 하며 폭과 높이는 양수여야 한다.
- `ExtendedContent`가 아닌 모드에서 호출하면 `InvalidOperationException`이 발생한다.
- 마지막으로 등록한 영역은 다음 호출까지 유지되므로 동적 레이아웃은 매 프레임 다시 계산한다.

이 API는 “드래그 영역에서 버튼을 제외”하는 별도의 제외 목록을 받지 않는다. 탭, 버튼, 메뉴, 검색창 같은 상호작용 요소의 실제 사각형을 피해서 남은 빈 공간만 드래그 영역으로 등록한다.

## 권장 렌더 패턴

다음 코드는 왼쪽에 유효 창 아이콘, 탭, 새 탭 버튼을 배치하고, 그 오른쪽부터 캡션 버튼 묶음 앞까지를 드래그 영역으로 만든다.

```csharp
public sealed class MainScreen : UiScreen
{
    private const float TitleBarHeight = 48f;

    public override void Render(UiImmediateContext ui)
    {
        var viewport = ui.GetMainViewport();
        var width = viewport.Size.X;

        // 배경은 전체 상단에 그려도 되지만 상호작용 요소는 캡션 경계와 겹치면 안 된다.
        var drawList = ui.GetBackgroundDrawList();
        drawList.AddRectFilled(
            new UiRect(0f, 0f, width, TitleBarHeight),
            ui.GetColorU32(UiStyleColor.TitleBgActive));

        const float top = 8f;
        const float left = 48f;
        const float tabWidth = 112f;
        const float tabHeight = 32f;
        const float gap = 6f;

        if (ui.TryGetWindowIcon(out var windowIcon))
        {
            windowIcon.Prepare(ui, UiImageEffects.Default);
            drawList.AddImage(
                windowIcon.TextureId,
                new UiVector2(12f, 12f),
                new UiVector2(36f, 36f),
                new UiVector2(0f, 0f),
                new UiVector2(1f, 1f),
                new UiColor(0xFFFFFFFF));
        }

        ui.SetCursorScreenPos(new UiVector2(left, top));
        _ = ui.Button("Home##title-tab", new UiVector2(tabWidth, tabHeight));

        var newTabX = left + tabWidth + gap;
        ui.SetCursorScreenPos(new UiVector2(newTabX, top));
        _ = ui.Button("+##new-tab", new UiVector2(36f, tabHeight));

        var dragLeft = newTabX + 36f + 14f;
        if (ui.TryGetCaptionButtonBounds(out var captionButtons))
        {
            var dragRight = captionButtons.X - 8f;
            if (dragRight > dragLeft)
            {
                ui.SetTitleBarDragRegions([
                    new UiRect(0f, 0f, 48f, TitleBarHeight),
                    new UiRect(dragLeft, 0f, dragRight - dragLeft, TitleBarHeight),
                ]);
            }
            else
            {
                ui.SetTitleBarDragRegions([new UiRect(0f, 0f, 48f, TitleBarHeight)]);
            }
        }
        else
        {
            // 유효하지 않은 추정값으로 네이티브 버튼 영역을 덮지 않는다.
            ui.SetTitleBarDragRegions([new UiRect(0f, 0f, 48f, TitleBarHeight)]);
        }

        DrawApplicationContent(ui, viewport);
    }

    private static void DrawApplicationContent(UiImmediateContext ui, UiViewport viewport)
    {
        const float margin = 24f;
        ui.SetNextWindowPos(new UiVector2(margin, TitleBarHeight + margin));
        ui.SetNextWindowSize(new UiVector2(
            MathF.Max(1f, viewport.Size.X - margin * 2f),
            MathF.Max(1f, viewport.Size.Y - TitleBarHeight - margin * 2f)));
        ui.BeginWindow("Content");
        ui.Text("Application content");
        ui.EndWindow();
    }
}
```

여러 상호작용 요소가 타이틀바 중앙에 있다면 빈 공간을 여러 개의 `UiRect`로 나누어 전달한다.

```csharp
ui.SetTitleBarDragRegions([
    leftEmptyRegion,
    centerEmptyRegion,
    rightEmptyRegion,
]);
```

등록된 드래그 사각형끼리 겹치지 않게 계산하는 것이 권장된다. 겹쳐도 드래그 결과는 같지만 레이아웃 의도를 검토하기 어려워진다.

## 레이아웃 규칙

### 1. 캡션 버튼 영역 전체를 예약한다

`captionButtonBounds` 안에는 애플리케이션 버튼, 탭, 텍스트 입력, 툴팁 트리거 또는 드래그 영역을 놓지 않는다. 시각적 배경은 그 아래까지 이어서 그릴 수 있지만 포인터 상호작용은 Windows에 남겨야 한다. Duxel이 이 사각형에 캡션 아이콘 오버레이를 자동으로 그리므로 앱 코드가 버튼을 한 벌 더 그리면 안 된다.

### 2. 상호작용 요소의 실제 배치 결과로 드래그 영역을 계산한다

고정된 “탭은 300px” 같은 추정값보다 현재 프레임에서 계산한 마지막 컨트롤의 오른쪽 좌표를 사용한다. 탭 추가, 검색창 확장, 지역화 문자열, 글꼴 배율 때문에 레이아웃이 바뀌면 드래그 영역도 같은 프레임에 바뀌어야 한다.

### 3. 매 프레임 교체하거나 명시적으로 비운다

즉시 모드 UI에서 지난 프레임의 영역을 그대로 두면 사라진 버튼 위치가 계속 드래그 영역으로 남거나, 이동한 버튼 위에 이전 드래그 영역이 남을 수 있다. 렌더 경로마다 `SetTitleBarDragRegions(...)` 또는 `SetTitleBarDragRegions([])`가 실행되게 한다.

### 4. 물리 픽셀로 변환하지 않는다

Duxel이 네이티브 DWM 좌표와 현재 창 DPI를 논리 좌표로 변환한다. 애플리케이션이 `GetDpiForWindow`를 다시 적용하면 이중 스케일링이 발생한다.

### 5. 캡션 버튼이 오른쪽에 있다는 가정도 최소화한다

일반적인 Windows 환경에서는 버튼 묶음이 오른쪽에 있지만, 앱 레이아웃은 반환된 사각형 자체를 예약 영역으로 취급해야 한다. 전체 타이틀바를 사용하는 복잡한 레이아웃은 해당 사각형과 교차하지 않는 자유 구간들을 계산한다.

## 유지되는 Windows 동작

애플리케이션이 위 계약을 지키면 Duxel Windows 백엔드는 다음 동작을 유지한다.

| 동작 | 기반 계약 |
|---|---|
| 창 드래그 | 등록 영역이 `HTCAPTION` 반환 |
| 제목 영역 더블클릭 최대화/복원 | `HTCAPTION` 비클라이언트 동작 |
| 리사이즈 테두리 | DPI별 Windows 리사이즈 히트 테스트 |
| Windows 11 Snap Layout | 최대화 버튼이 `HTMAXBUTTON` 반환 |
| Alt+Space 시스템 메뉴 | `WS_SYSMENU`와 기본 창 프로시저 유지 |
| 최소화·최대화·닫기 | Duxel 렌더링 아이콘 오버레이와 DWM 우선 히트 테스트/네이티브 창 스타일 조합 |
| DPI 전환 | `WM_DPICHANGED`에서 프레임과 좌표 갱신 |
| 다중 모니터 최대화 | 현재 창과 가장 가까운 모니터의 작업영역 사용 |
| 작업 표시줄 보호 | 최대화 클라이언트 영역을 모니터 `rcWork`에 제한 |

앱이 드래그 영역 위에 자체 포인터 처리 요소를 겹치거나 캡션 버튼 위에 별도 네이티브/오버레이 창을 올리면 이 보장은 깨질 수 있다.

## DPI와 다중 모니터 점검

다음 상태 전환에서도 레이아웃을 다시 계산해야 한다.

- 창을 서로 다른 DPI의 모니터로 이동
- 복원 상태와 최대화 상태 전환
- 디스플레이 배율 변경
- 창 너비가 탭 묶음보다 작아지는 경우
- Windows 테마 또는 DWM 컴포지션 변경
- 작업 표시줄 위치나 작업영역 변경

`TryGetCaptionButtonBounds` 결과를 캐시해서 장기간 재사용하지 않는다. 현재 프레임의 결과를 현재 프레임 레이아웃에만 사용한다.

## 실행과 자동 진단

### 일반 실행

로컬 Duxel 소스 변경을 반영하려면 저장소 루트에서 다음 명령을 사용한다.

```powershell
./run-fba.ps1 samples/fba/extended_title_bar_fba.cs -NoCache
```

`dotnet run samples/fba/extended_title_bar_fba.cs`는 파일의 NuGet 패키지 참조를 사용하므로 아직 게시되지 않은 로컬 변경을 검증하지 않는다.

### NativeAOT 실제 HWND 진단

샘플에는 실제 Win32 창을 대상으로 하는 진단 모드가 내장되어 있다.

```powershell
./run-fba.ps1 samples/fba/extended_title_bar_fba.cs -NoCache -NoLaunch

$artifactDir = Join-Path (Get-Location) "samples/fba/artifacts/extended_title_bar_fba"
$exePath = Join-Path $artifactDir "extended_title_bar_fba.exe"
$diagnosticPath = Join-Path $artifactDir "diagnostics.txt"
$env:DUXEL_EXTENDED_TITLEBAR_DIAG_OUT = $diagnosticPath

$diagnosticProcess = Start-Process -FilePath $exePath -PassThru
if (-not $diagnosticProcess.WaitForExit(30000))
{
    Stop-Process -Id $diagnosticProcess.Id
    throw "Extended title-bar diagnostic timed out."
}

if ($diagnosticProcess.ExitCode -ne 0)
{
    throw "Diagnostic process failed with exit code $($diagnosticProcess.ExitCode)."
}

Get-Content -LiteralPath $diagnosticPath
if (Select-String -LiteralPath $diagnosticPath -Pattern '^FAIL')
{
    throw "One or more extended title-bar checks failed."
}
```

현재 진단 샘플은 다음 계약을 검사한다.

- `TryGetWindowIcon`을 통한 유효 창 아이콘 조회 가능 여부
- 창 핸들과 캡션/시스템 메뉴/리사이즈/최소화/최대화 스타일
- 네이티브 DWM 캡션 메타데이터 조회와 공개 API가 Duxel의 `Y = 0`, 논리 48픽셀 묶음과 일치하는지 여부
- 최소화 `HTMINBUTTON`, 최대화 `HTMAXBUTTON`, 닫기 `HTCLOSE`
- 복원/최대화 모두 Duxel 캡션 버튼 전체 높이에서 `HTMAXBUTTON` 유지
- 탭 `HTCLIENT`, 빈 드래그 영역 `HTCAPTION`
- 좌상단 리사이즈 `HTTOPLEFT`
- `WM_GETMINMAXINFO` 작업영역 계약
- 드래그 영역 더블클릭 최대화
- 최대화 상태의 공개 캡션 경계
- 최대화 클라이언트 영역과 모니터 작업영역 일치
- 최대화 후 복원

프레임워크 변경 완료 조건은 진단 파일에 `FAIL`이 없고 전체 Release 빌드가 경고와 오류 없이 성공하는 것이다.

```powershell
dotnet build Duxel.slnx -c Release --no-restore
```

## 수동 검증 매트릭스

애플리케이션을 배포하기 전에는 가능한 환경에서 다음 조합을 확인한다.

| 항목 | 권장 조합 |
|---|---|
| DPI | 100%, 125%, 150%, 200% |
| 모니터 | 주 모니터, 보조 모니터, 서로 다른 DPI 간 이동 |
| 창 상태 | 복원, 최대화, 최소화 후 복원 |
| Duxel 크롬 아이콘 | 복원 상태의 단일 사각형 최대화, 최대화 상태의 겹친 사각형 복원, 복원 후 다시 단일 사각형 최대화 |
| 작업 표시줄 | 아래/옆 위치, 자동 숨김 설정 |
| 입력 | 마우스, 터치패드, Alt+Space, 더블클릭 |
| Snap | 최대화 버튼 호버, 클릭, Windows 11 Snap Layout 선택 |
| 테마 | 밝게, 어둡게, 시스템 테마 변경 |

자동 진단은 Win32/DWM 계약을 검증하지만 불투명 Vulkan surface 위에 실제 픽셀이 보이는지는 증명하지 못한다. 앱별 레이아웃뿐 아니라 캡션 아이콘 3개, 호버/누름 피드백, 최대화/복원 아이콘 전환, 왼쪽 창 아이콘 우클릭 시스템 메뉴, 의도한 탭 모양을 반드시 육안 확인한다.

## 흔한 실수

- `UseDuxelTitleBar = true`만 설정하고 `ExtendedContent`가 활성화됐다고 가정
- `Duxel` 모드에서 앱 캡션 버튼을 직접 그리거나 라이브러리 상태 대신 최대화/복원 아이콘을 수동 캐시
- `IntegrateSystemChrome`를 콘텐츠 확장 옵션으로 오해
- 캡션 버튼 폭을 상수로 하드코딩
- 상단 전체를 하나의 드래그 영역으로 등록해 탭과 버튼이 클릭되지 않게 만듦
- 캡션 버튼 사각형을 드래그 영역에 포함
- 픽셀 좌표에 DPI 배율을 한 번 더 곱함
- `TryGetCaptionButtonBounds`가 `false`인데 이전 드래그 영역을 유지
- 창 크기나 탭 구성이 바뀌었는데 영역을 다시 등록하지 않음
- 최대화 상태에서 캡션 경계의 음수 `Y`를 오류로 처리
- 로컬 구현 검증에 NuGet 기반 `dotnet run`만 사용
- `HTMINBUTTON`/`HTMAXBUTTON`/`HTCLOSE` 진단 통과만으로 캡션 버튼 픽셀이 보인다고 판단

## 프레임워크 기여자 계약

`Duxel.App` 또는 `Duxel.Platform.Windows`의 Windows 타이틀바 구현을 수정할 때는 다음 불변 조건을 보존한다.

1. `WS_CAPTION`, `WS_SYSMENU`, `WS_THICKFRAME`, `WS_MINIMIZEBOX`, `WS_MAXIMIZEBOX`를 유지한다.
2. `WM_NCHITTEST`에서 Duxel 캡션 묶음을 `DwmDefWindowProc`보다 먼저 판정해 네이티브 캡션 버튼 코드를 반환하고 나머지 비클라이언트 동작에는 DWM을 계속 사용한다.
3. 최대화 버튼은 반드시 `HTMAXBUTTON`으로 식별돼야 한다.
4. 공개 캡션 경계는 논리 `Y = 0`과 `DuxelTitleBarHeight`를 유지하고 최대화된 DWM의 음수 `Y`나 네이티브 30픽셀 높이를 Duxel 시각 위치에 복사하지 않는다.
5. 리사이즈 경계가 드래그 영역보다 먼저 판정되게 한다.
6. 최대화 크기는 현재 모니터의 `rcWork`를 사용한다.
7. `WM_DPICHANGED`와 `WM_DWMCOMPOSITIONCHANGED`에서 확장 프레임을 다시 적용한다.
8. `Default`와 기존 `UseDuxelTitleBar` 해석을 깨지 않는다.
9. `Duxel`과 `ExtendedContent` 모두 `DuxelCaptionButtonRenderer`를 사용해 위치, 선, 상태 기반 최대화/복원 아이콘을 동일하게 유지한다.
10. `ExtendedContent`에서는 안정된 공개 Duxel 경계와 비클라이언트 호버/누름 상태로 그리며 클릭 명령·`HTMAXBUTTON`·Snap Layout은 Windows 경로에 유지한다.
11. 등록된 `HTCAPTION`/`HTSYSMENU` 영역의 `WM_NCRBUTTONUP`을 네이티브 시스템 메뉴로 연결해 앱이 그린 창 아이콘도 기본 타이틀바 동작을 유지한다.
12. `System`과 `Duxel` 모드의 기존 동작을 회귀시키지 않는다.
13. 공개 API, 샘플, 이 가이드와 `duxel-agent-reference` 한·영 문서를 같은 변경 집합에서 동기화한다.

## 코딩 AI 작업 절차

코딩 AI는 확장 타이틀바 요청을 받을 때 다음 순서를 따른다.

1. 이 가이드와 `samples/fba/extended_title_bar_fba.cs`를 먼저 읽는다.
2. 라이브러리 소유 일반 크롬은 `Duxel`, 애플리케이션 UI가 `y = 0`에 필요한 경우에만 `ExtendedContent`를 선택한다.
3. 요청이 앱 레이아웃 변경인지 플랫폼 백엔드 변경인지 구분한다.
4. 앱 작업이라면 공개 API만 사용하고 별도 Win32 히트 테스트를 중복 구현하지 않는다.
5. 원시 DWM 좌표를 읽거나 하드코딩하지 않고 공개 Duxel 캡션 버튼 경계를 매 프레임 조회한다.
6. 모든 상호작용 요소의 사각형을 먼저 확정한 뒤 남은 빈 구간만 드래그 영역으로 등록한다.
7. `false` 경로와 빈 영역 경로에서 이전 계산 구간을 제거한다. 고정된 왼쪽 아이콘 영역처럼 독립적으로 유효한 영역만 남길 수 있다.
8. 로컬 소스는 `run-fba.ps1`로 실행한다.
9. `ExtendedContent` 최소화/최대화/닫기 아이콘, 호버 피드백, 창 아이콘 우클릭 시스템 메뉴, 탭 모양을 육안 확인한다. 히트 테스트 진단만으로는 충분하지 않다.
10. `Duxel`과 `ExtendedContent` 최대화 아이콘이 최대화 시 복원 아이콘으로, 복원 후 다시 최대화 아이콘으로 바뀌는지 확인한다.
11. 공개 동작을 바꿨다면 한·영 문서와 샘플을 함께 갱신한다.
12. Release 전체 빌드와 NativeAOT 실제 HWND 진단을 수행한다.
12. 요구사항별 증거와 남은 환경 한계를 보고한 뒤에만 완료로 판단한다.

AI에 바로 전달할 수 있는 작업 지시는 다음과 같다.

```text
일반적인 라이브러리 소유 크롬은 Duxel 모드를, 앱 콘텐츠가 y=0부터 시작해야 하면 ExtendedContent를 선택하라.
docs/extended-title-bar-guide.ko.md와 samples/fba/extended_title_bar_fba.cs를 기준으로 삼아라.
Duxel 모드에서는 라이브러리 소유 캡션 버튼을 다시 구현하거나 최대화/복원 상태를 앱 코드에 캐시하지 마라.
Duxel 캡션 버튼 경계를 매 프레임 조회하고 그 사각형을 레이아웃과 드래그 영역에서 제외하라.
탭, 버튼, 메뉴, 입력 요소는 HTCLIENT로 남도록 빈 공간만 드래그 영역으로 등록하라.
캡션 폭이나 DPI를 하드코딩하지 마라.
완료 전에 Release 빌드와 NativeAOT HWND 진단을 실행하고 요구사항별 PASS 증거를 보고하라.
```

## 완료 체크리스트

- [ ] 앱 콘텐츠가 `y = 0`부터 시작해야 하면 `TitleBarMode = DuxelTitleBarMode.ExtendedContent`를 명시했다.
- [ ] `Duxel`과 `ExtendedContent`가 같은 캡션 버튼 렌더러를 사용하며 최대화 시 겹친 사각형 복원 아이콘으로, 복원 후 다시 최대화 아이콘으로 돌아간다.
- [ ] 애플리케이션 타이틀바 배경과 콘텐츠가 `(0, 0)`부터 렌더링된다.
- [ ] 캡션 버튼 묶음 경계를 매 프레임 조회한다.
- [ ] 캡션 버튼 경계와 앱 상호작용 요소가 겹치지 않는다.
- [ ] 드래그 영역이 상호작용 요소를 포함하지 않는다.
- [ ] 경계를 얻지 못하거나 빈 공간이 없으면 이전 계산 드래그 구간을 제거하고 독립적으로 유효한 영역만 남긴다.
- [ ] 복원/최대화에서 창 드래그와 더블클릭이 동작한다.
- [ ] 모든 리사이즈 테두리가 동작한다.
- [ ] Duxel이 그린 최대화 버튼에서 `HTMAXBUTTON`을 통해 Windows 11 Snap Layout이 동작한다.
- [ ] Alt+Space 시스템 메뉴가 열린다.
- [ ] DPI 변경과 모니터 이동 후 경계가 다시 맞는다.
- [ ] 최대화 상태가 작업 표시줄 영역을 침범하지 않는다.
- [ ] Release 빌드가 경고 0개, 오류 0개다.
- [ ] NativeAOT 진단 결과에 `FAIL`이 없다.

## 관련 문서

- [Duxel 에이전트 참조](duxel-agent-reference.ko.md)
- [FBA 시작 가이드](getting-started-fba.ko.md)
- [FBA 참조 가이드](fba-reference-guide.ko.md)
- [FBA 샘플 카탈로그](fba-run-samples.ko.md)
- [영문 확장 타이틀바 가이드](extended-title-bar-guide.md)
