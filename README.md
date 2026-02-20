# Duxel

**.NET 10 전용 크로스플랫폼 즉시 모드(Immediate-Mode) GUI 프레임워크.**
Vulkan 렌더러 + Windows 네이티브 윈도우/입력 백엔드 기반으로 고품질 즉시 모드 위젯·렌더링·텍스트 품질을 목표합니다.

**현재 버전: `0.1.12-preview`** · Display/Render 프로필 · 동적 MSAA(1x/2x/4x/8x) · VSync 토글

[![NuGet](https://img.shields.io/nuget/vpre/Duxel.App)](https://www.nuget.org/packages/Duxel.App)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

Repository: https://github.com/dimohy/Duxel

## 0.1.12-preview 개선 사항 (최신)

- **[기능]** DirectWrite 기반 텍스트 렌더링 시스템 추가 — Direct Text 런타임 토글, 텍스트 캐시, `PushFontSize`/`PopFontSize` 지원
- **[기능]** Windows 플랫폼 백엔드 독립 분리(GLFW 제거), 즉시 모드 애니메이션 프레임워크(`AnimateFloat`), 아이콘 시스템 추가
- **[기능]** 위젯/벤치 헬퍼 API 대량 승격 — `BeginWindowCanvas`, `DrawOverlayText`, `UiFpsCounter`, `DrawKeyValueRow`, `BenchOptions`, `DrawLayerCard`/`DrawLayerCardInteractive` 등
- **[기능]** Windows 계산기 FBA(사이버 효과), 계산기 쇼케이스 FBA, 텍스트 렌더 검증 FBA 등 신규 샘플 추가
- **[개선]** Combo/ListBox/Table/Tree 위젯 API 시그니처 통일, IME 처리 안정성 개선, 10개+ 샘플 보일러플레이트를 라이브러리 API로 전환

이전 버전 변경 내역은 [Version History](docs/version-history.md)에서 누적 확인할 수 있습니다.

## 주요 특징

- **즉시 모드 UI** — Begin/End 패턴 기반 위젯 API
- **Vulkan 렌더러** — 프로필 기반 기본값(Display=MSAA2, Render=MSAA1), VSync 토글, Triple Buffering, Persistent Mapped Buffers
- **Windows 윈도우/입력** — 키보드·마우스·스크롤·IME 입력 지원
- **스크롤바/팝업** — 통합 스크롤바 렌더러 (Child/Combo/ListBox/InputMultiline), 팝업 차단 레이어
- **NativeAOT 지원** — `PublishAot=true` 배포 가능 (리플렉션/동적 로딩 없음)
- **UI DSL** — `.ui` 파일로 선언적 UI 정의, 소스 생성기 기반 빌드 타임 코드 생성, 핫리로드 지원
- **폰트 아틀라스** — TTF 파싱(컴파운드 글리프 포함), HiDPI 스케일링, 빠른 시작을 위한 Built-in ASCII 폰트
- **ImGui 호환성** — 400+ API 구현 및 동등성 목표/기준/현황 문서화 ([설계 문서](docs/design.md#imgui-호환성-통합-문서))

## 패키지 구조

| 패키지                             | 설명                                                            |
| ---------------------------------- | --------------------------------------------------------------- |
| **Duxel.App**                | OS 비종속 앱 파사드(공용 실행 파이프라인, 플랫폼 서비스 주입점) |
| **Duxel.Windows.App**        | Windows 올인원 앱 패키지 (`DuxelWindowsApp.Run`)              |

내부 구성요소(별도 NuGet 배포 안 함, 상위 패키지에 포함):

- `Duxel.Core` — UI 컨텍스트, 위젯 API, 드로우 리스트, 폰트 아틀라스, DSL 런타임
- `Duxel.Platform.Windows` — Windows 전용 플랫폼 백엔드 (윈도우/입력/키 반복/IME/클립보드)
- `Duxel.Vulkan` — Vulkan 렌더러 백엔드

## 빠른 시작

### NuGet 패키지 사용 (FBA — File-Based App)

```csharp
// hello.cs
#:property TargetFramework=net10.0
#:package Duxel.Windows.App@*-*

using Duxel.App;
using Duxel.Windows.App;
using Duxel.Core;

DuxelWindowsApp.Run(new DuxelAppOptions
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
#:package Duxel.Windows.App@*-*

using Duxel.App;
using Duxel.Windows.App;
using Duxel.Core.Dsl;

var dslText = """
Window "Hello DSL"
  Text "Hello from DSL!"
  Button Id="greet" Text="Greet"
  Checkbox Id="dark" Text="Dark Mode" Default=true
  SliderFloat Id="volume" Text="Volume" Min=0 Max=1
""";

var doc = UiDslParser.Parse(dslText);

DuxelWindowsApp.Run(new DuxelAppOptions
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
  <ProjectReference Include="src/Duxel.Windows.App/Duxel.Windows.App.csproj" />
</ItemGroup>
```

## 샘플

### 프로젝트 샘플

| 프로젝트                   | 설명                                      | 실행                                             |
| -------------------------- | ----------------------------------------- | ------------------------------------------------ |
| `Duxel.Sample`           | DSL `.ui` + 소스 생성기 + 바인딩 데모   | `dotnet run --project samples/Duxel.Sample/`   |

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
| `image_widget_effects_fba.cs` | 즉시 모드 | 웹 PNG/JPG/GIF + GIF 애니메이션 + 이미지 효과(Zoom/Rotation/Alpha/Pixelate) |
| `input_queries.cs`       | 즉시 모드 | 키보드/마우스 상태, Shortcut, 클립보드        |
| `item_status.cs`         | 즉시 모드 | IsItemActive/Focused/Clicked, MultiSelect     |
| `windows_calculator_fba.cs` | 즉시 모드 | Windows 계산기 — 사이버 backdrop/리플 효과/FX 버튼 |
| `windows_calculator_duxel_showcase_fba.cs` | 즉시 모드 | 계산기 쇼케이스 — RPN 토큰 추적/멀티베이스/비트 그리드 |
| `text_render_validation_fba.cs` | 즉시 모드 | 텍스트 렌더링 정렬/크기/클립 검증 도구 |
| `text_input_only.cs`     | 즉시 모드 | 텍스트 입력 전용 테스트                       |
| `listbox_scroll_test_fba.cs` | 즉시 모드 | ListBox 스크롤 동작 테스트                    |
| `idle_layer_validation.cs` | 즉시 모드 | 레이어 캐시/정적 GPU 버퍼 검증 — opacity·백엔드·레이아웃 벤치 |
| `layer_dirty_strategy_bench.cs` | 즉시 모드 | 레이어 dirty 전략 all vs single 벤치          |
| `layer_widget_mix_bench_fba.cs` | 즉시 모드 | 레이어+위젯 혼합 벤치 — DrawLayerCardInteractive 적용 |
| `global_dirty_strategy_bench.cs` | 즉시 모드 | 전역 정적 캐시 전략 벤치 — all-dynamic 대비 비교 |
| `vector_primitives_bench_fba.cs` | 즉시 모드 | 벡터 primitive(라인/사각형/원) 전용 벤치 + clip clamp A/B 비교 |
| `Duxel_perf_test_fba.cs` | 즉시 모드 | 대량 폴리곤 물리 시뮬레이션 성능 벤치마크     |
| `ui_mixed_stress.cs`     | 즉시 모드 | 다중 창/텍스트/테이블/리스트/입력/드로우 복합 스트레스 |

성능 자동 비교 스크립트(`./scripts/run-fba-bench.ps1`)의 기본 동작은 아래 2개 샘플을 순차 벤치합니다.

- `samples/fba/Duxel_perf_test_fba.cs`
- `samples/fba/ui_mixed_stress.cs`

단일 샘플만 벤치하려면 `-SamplePath`를 사용합니다.

```powershell
./scripts/run-fba-bench.ps1 -SamplePath samples/fba/Duxel_perf_test_fba.cs
```

clip clamp A/B 자동화(레이어 혼합/벡터 전용) 상세 사용법과 결과 해석은 [docs/fba-clip-ab-bench.md](docs/fba-clip-ab-bench.md)를 참고하세요.

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
│  Windows     │  ← 윈도우/입력 백엔드
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
      FastStartup = true,    // 시작 시 경량 atlas 사용 + 백그라운드 전체 atlas 빌드
      UseBuiltInAsciiAtStartup = true, // 시작 atlas를 Built-in ASCII로 생성할지 여부
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

### NativeAOT 배포 (DSL 프로젝트 샘플)

```powershell
dotnet publish samples/Duxel.Sample/ -c Release -r win-x64 /p:PublishAot=true
```

## 문서

- [설계 문서](docs/design.md) — 아키텍처, 설계 원칙, 품질 기준
- [UI DSL 레퍼런스](docs/ui-dsl.md) — DSL 문법, 위젯 매핑, 상태 바인딩
- [FBA 참조 가이드](docs/fba-reference-guide.md) — FBA 실행 방식 상세
- [TODO](docs/todo.md) — 개발 로드맵

## 라이선스

MIT
