# Duxel

**.NET 10 전용 크로스플랫폼 즉시 모드(Immediate-Mode) GUI 프레임워크.**
Vulkan 렌더러 + GLFW 윈도우/입력 백엔드로 Dear ImGui 동등 수준의 위젯·렌더링·텍스트 품질을 목표합니다.

**현재 버전: `0.1.7-preview`** · Display/Render 프로필 · 동적 MSAA(1x/2x/4x/8x) · VSync 토글

[![NuGet](https://img.shields.io/nuget/vpre/Duxel.App)](https://www.nuget.org/packages/Duxel.App)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

Repository: https://github.com/dimohy/Duxel

## 0.1.7-preview 개선 사항

- `Duxel.Core`에 플랫폼 중립 이미지 API(`UiImageTexture`, `UiImageEffects`, `IUiImageDecoder`)를 추가해 이미지 처리 경로를 라이브러리 API로 정리했습니다.
- Windows 전용 디코더를 `Duxel.Platform.Windows`로 분리하고 `Duxel.App`에서 런타임 등록하도록 구성해 Core 계층의 플랫폼 종속성을 제거했습니다.
- FBA 이미지 샘플(`samples/fba/image_widget_effects_fba.cs`)에 웹 이미지 소스 선택(PNG/JPG/GIF)과 GIF 프레임 애니메이션 재생을 추가했습니다.
- 접힘/확장 UI 동작을 보정해 접힘 시 3px 본문 peek는 유지하면서도 비정상 캔버스 돌출이 발생하지 않도록 렌더링 클립 경로를 안정화했습니다.

## 주요 특징

- **즉시 모드 UI** — Dear ImGui 스타일의 Begin/End 패턴 기반 위젯 API
- **Vulkan 렌더러** — 프로필 기반 기본값(Display=MSAA2, Render=MSAA1), VSync 토글, Triple Buffering, Persistent Mapped Buffers
- **GLFW 윈도우/입력** — 키보드·마우스·스크롤·IME 입력 지원
- **스크롤바/팝업** — 통합 스크롤바 렌더러 (Child/Combo/ListBox/InputMultiline), 팝업 차단 레이어
- **NativeAOT 지원** — `PublishAot=true` 배포 가능 (리플렉션/동적 로딩 없음)
- **UI DSL** — `.ui` 파일로 선언적 UI 정의, 소스 생성기 기반 빌드 타임 코드 생성, 핫리로드 지원
- **폰트 아틀라스** — TTF 파싱(컴파운드 글리프 포함), HiDPI 스케일링, 빠른 시작을 위한 Built-in ASCII 폰트
- **ImGui API 전체 커버리지** — 400+ API 구현 완료 ([상세 목록](docs/imgui-coverage.md))

## 패키지 구조

| 패키지                             | 설명                                                            |
| ---------------------------------- | --------------------------------------------------------------- |
| **Duxel.App**                | 앱 진입점 (`DuxelApp.Run`), 옵션 설정, DSL 바인딩 통합        |
| **Duxel.Core**               | UI 컨텍스트, 위젯 API, 드로우 리스트, 폰트 아틀라스, DSL 런타임 |
| **Duxel.Core.Dsl.Generator** | `.ui` → C# 소스 생성기 (빌드 타임)                           |
| **Duxel.Platform.Glfw**      | GLFW 기반 윈도우/입력 백엔드                                    |
| **Duxel.Platform.Windows**   | Windows 전용 플랫폼 지원 (키 반복, IME)                         |
| **Duxel.Vulkan**             | Vulkan 렌더러 백엔드                                            |

## 빠른 시작

### NuGet 패키지 사용 (FBA — File-Based App)

```csharp
// hello.cs
#:property TargetFramework=net10.0
#:package Duxel.App@*-*

using Duxel.App;
using Duxel.Core;

DuxelApp.Run(new DuxelAppOptions
{
    Window = new DuxelWindowOptions { Title = "Hello Duxel" },
    Screen = new HelloScreen()
});

public sealed class HelloScreen : UiScreen
{
    protected override void OnUI(UiImmediateContext ui)
    {
        ui.BeginWindow("Hello");
        ui.Text("Hello, Duxel!");
        if (ui.Button("Click me"))
            ui.Text("Clicked!");
        ui.End();
    }
}
```

```powershell
dotnet run hello.cs
```

### DSL 방식

```csharp
// dsl_hello.cs
#:property TargetFramework=net10.0
#:package Duxel.App@*-*

using Duxel.App;
using Duxel.Core.Dsl;

var dslText = """
Window "Hello DSL"
  Text "Hello from DSL!"
  Button Id="greet" Text="Greet"
  Checkbox Id="dark" Text="Dark Mode" Default=true
  SliderFloat Id="volume" Text="Volume" Min=0 Max=1
""";

var doc = UiDslParser.Parse(dslText);

DuxelApp.Run(new DuxelAppOptions
{
    Window = new DuxelWindowOptions { Title = "DSL Hello" },
    Dsl = new DuxelDslOptions
    {
        State = new UiDslState(),
        Render = emitter => doc.Emit(emitter)
    }
});
```

### 프로젝트 참조 방식

```xml
<ItemGroup>
  <ProjectReference Include="src/Duxel.App/Duxel.App.csproj" />
</ItemGroup>
```

## 샘플

### 프로젝트 샘플

| 프로젝트                   | 설명                                      | 실행                                             |
| -------------------------- | ----------------------------------------- | ------------------------------------------------ |
| `Duxel.Sample`           | DSL `.ui` + 소스 생성기 + 바인딩 데모   | `dotnet run --project samples/Duxel.Sample/`   |
| `Duxel.Sample.NativeAot` | NativeAOT 배포 검증 (DSL + AOT)           | `dotnet publish -c Release`                    |
| `Duxel.PerfTest`         | 대량 폴리곤 물리 시뮬레이션 성능 벤치마크 | `dotnet run --project samples/Duxel.PerfTest/` |

### FBA 샘플 (`samples/fba/`)

FBA(File-Based App)는 단일 `.cs` 파일로 `dotnet run`만으로 바로 실행할 수 있는 샘플이며, 개발용 `run-fba.ps1`는 기본적으로 NativeAOT 게시를 수행합니다.

```powershell
# NuGet 패키지 참조(외부 사용자)
dotnet run samples/fba/all_features.cs

# 로컬 프로젝트 참조(개발자, 기본 NativeAOT)
./run-fba.ps1 samples/fba/all_features.cs

# 로컬 프로젝트 참조(개발자, Managed 실행)
./run-fba.ps1 samples/fba/all_features.cs -Managed

# 기본 동작 프로필 전환(코드 기본값: Display)
$env:DUXEL_APP_PROFILE='render'; ./run-fba.ps1 samples/fba/Duxel_perf_test_fba.cs -Managed
```

| 파일                       | 모드      | 설명                                          |
| -------------------------- | --------- | --------------------------------------------- |
| `all_features.cs`        | 즉시 모드 | 전체 위젯 종합 데모 (400+ API 사용)           |
| `dsl_showcase.cs`        | DSL       | DSL 정적 레이아웃 — 입력/탭/테이블/트리      |
| `dsl_interaction.cs`     | DSL       | DSL 인터랙션 — Drag/Slider/Color/Child/Popup |
| `menu_submenu_zorder.cs` | DSL       | 메뉴/서브메뉴 중첩 Z-Order 테스트             |
| `advanced_layout.cs`     | 즉시 모드 | PushID, Cursor, Scroll, StyleVar, ClipRect    |
| `columns_demo.cs`        | 즉시 모드 | Legacy Columns API 전체 시연                  |
| `image_and_popups.cs`    | 즉시 모드 | Image/Popup/Tooltip/TreeNodeV/TextLink        |
| `input_queries.cs`       | 즉시 모드 | 키보드/마우스 상태, Shortcut, 클립보드        |
| `item_status.cs`         | 즉시 모드 | IsItemActive/Focused/Clicked, MultiSelect     |
| `Duxel_perf_test_fba.cs` | 즉시 모드 | 대량 폴리곤 물리 시뮬레이션 성능 벤치마크     |
| `ui_mixed_stress.cs`     | 즉시 모드 | 다중 창/텍스트/테이블/리스트/입력/드로우 복합 스트레스 |

성능 자동 비교 스크립트(`./scripts/run-fba-bench.ps1`)의 기본 동작은 아래 2개 샘플을 순차 벤치합니다.

- `samples/fba/Duxel_perf_test_fba.cs`
- `samples/fba/ui_mixed_stress.cs`

단일 샘플만 벤치하려면 `-SamplePath`를 사용합니다.

```powershell
./scripts/run-fba-bench.ps1 -SamplePath samples/fba/Duxel_perf_test_fba.cs
```

## UI DSL

Duxel은 `.ui` 확장자의 선언적 DSL로 UI를 정의할 수 있습니다.

```
Window "My App"
  MenuBar
    Menu "File"
      MenuItem Id="new" Text="New"
      MenuItem Id="exit" Text="Exit"
  Row
    Button Id="play" Text="Play"
    Checkbox Id="vsync" Text="VSync" Default=true
  SliderFloat Id="volume" Text="Volume" Min=0 Max=1
  Combo Id="quality" Text="Quality" Items="Low|Medium|High"
  TabBar "tabs"
    TabItem "Settings"
      Text "Settings content"
    TabItem "About"
      Text "About content"
```

DSL 상세 문서: [docs/ui-dsl.md](docs/ui-dsl.md)

## 아키텍처

```
┌──────────────┐
│  DuxelApp    │  ← 앱 진입점 (옵션/DSL/테마 설정)
├──────────────┤
│  UiContext   │  ← 프레임 라이프사이클 (NewFrame → UI → Render → GetDrawData)
│  UiImmediate │  ← 즉시 모드 위젯 API (400+ 메서드)
│  Context     │
├──────────────┤
│  DSL Runtime │  ← .ui 파서/AST/이미터/상태 바인딩
│  DSL Gen     │  ← .ui → C# 소스 생성기
├──────────────┤
│  Vulkan      │  ← 렌더러 (DrawData 소비, 파이프라인/버퍼/텍스처 관리)
│  Renderer    │
├──────────────┤
│  GLFW        │  ← 윈도우/입력 백엔드
│  Platform    │
└──────────────┘
```

## API 옵션

```csharp
DuxelApp.Run(new DuxelAppOptions
{
    Window = new DuxelWindowOptions
    {
        Width = 1280,          // 기본값
        Height = 720,
        Title = "Duxel",
        VSync = true
    },
    Renderer = new DuxelRendererOptions
    {
        MinImageCount = 3,     // Triple Buffering
      EnableValidationLayers = false,
      Profile = DuxelPerformanceProfile.Display, // Display|Render
      MsaaSamples = 0         // 0=프로필 기본값, 또는 1/2/4/8 강제
    },
    Font = new DuxelFontOptions
    {
        FontSize = 26,
        FastStartup = true,    // Built-in ASCII → 비동기 TTF 전환
        InitialGlyphs = ["한글 글리프 문자열"]
    },
    Theme = UiTheme.ImGuiDark, // Dark/Light/Classic
    Screen = new MyScreen()    // 또는 Dsl = new DuxelDslOptions { ... }
});
```

## 빌드

```powershell
dotnet build
dotnet run --project samples/Duxel.Sample/
```

### NativeAOT 배포

```powershell
dotnet publish samples/Duxel.Sample.NativeAot/ -c Release
```

## 문서

- [설계 문서](docs/design.md) — 아키텍처, 설계 원칙, 품질 기준
- [UI DSL 레퍼런스](docs/ui-dsl.md) — DSL 문법, 위젯 매핑, 상태 바인딩
- [ImGui API 커버리지](docs/imgui-coverage.md) — ImGui 전체 API 대비 구현 현황
- [FBA 참조 가이드](docs/fba-reference-guide.md) — FBA 실행 방식 상세
- [TODO](docs/todo.md) — 개발 로드맵

## 라이선스

MIT
