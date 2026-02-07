# Duxel FBA 빠른 시작 가이드

> **Duxel** — .NET 10 전용 즉시 모드(Immediate-Mode) GUI 프레임워크  
> Vulkan 렌더러 + GLFW 백엔드 · MSAA 4x · NativeAOT 지원

단일 `.cs` 파일 하나로 GUI 앱을 바로 실행할 수 있는 **FBA(File-Based App)** 방식을 소개합니다.

---

## 필수 환경

| 항목 | 요구 사항 |
|---|---|
| **.NET SDK** | **10.0** 이상 ([다운로드](https://dotnet.microsoft.com/download/dotnet/10.0)) |
| **GPU** | Vulkan 1.0+ 지원 그래픽카드 |
| **OS** | Windows 10/11 (Linux/macOS 확장 예정) |

> .NET 10은 현재 프리뷰입니다. `dotnet --version`으로 10.0 이상인지 확인하세요.

---

## 30초 만에 첫 앱 실행

### 1. 파일 하나 만들기

`hello.cs` 파일을 만들고 아래 내용을 붙여넣으세요:

```csharp
#:property TargetFramework=net10.0
#:package Duxel.App@*-*

using Duxel.App;
using Duxel.Core;

DuxelApp.Run(new DuxelAppOptions
{
    Window = new DuxelWindowOptions { Title = "Hello Duxel", Width = 800, Height = 600 },
    Screen = new HelloScreen()
});

public sealed class HelloScreen : UiScreen
{
    private int _count;

    public override void Render(UiImmediateContext ui)
    {
        ui.BeginWindow("Hello");
        ui.Text("Hello, Duxel!");
        if (ui.Button("Click me"))
            _count++;
        ui.Text($"Clicked {_count} times");
        ui.EndWindow();
    }
}
```

### 2. 실행

```powershell
dotnet run hello.cs
```

끝! NuGet에서 `Duxel.App` 패키지를 자동으로 가져와서 Vulkan 윈도우가 열립니다.

---

## 제공되는 FBA 샘플

Duxel 레포지토리의 `samples/fba/` 폴더에 다양한 샘플이 준비되어 있습니다.

```powershell
git clone https://github.com/dimohy/Duxel.git
cd Duxel
```

### 전체 기능 데모 (추천!)

```powershell
dotnet run samples/fba/all_features.cs
```

400+ API를 사용하는 종합 데모입니다. 메뉴바, 슬라이더, 드래그, 입력, 컬러 피커, 테이블, 팝업, 툴팁, 드로잉 프리미티브, 드래그앤드롭, ListClipper 등 거의 모든 위젯이 포함되어 있습니다.

### 샘플 목록

| 실행 명령 | 설명 |
|---|---|
| `dotnet run samples/fba/all_features.cs` | **전체 위젯 종합 데모** — 400+ API, 메뉴/슬라이더/테이블/팝업/드로잉 |
| `dotnet run samples/fba/dsl_showcase.cs` | **DSL 선언적 UI** — 마크업으로 입력/탭/테이블/트리 레이아웃 |
| `dotnet run samples/fba/dsl_interaction.cs` | **DSL 인터랙션** — Drag/Slider/Color/Child/Popup 바인딩 |
| `dotnet run samples/fba/menu_submenu_zorder.cs` | **메뉴 Z-Order** — 중첩 메뉴/서브메뉴 올바른 가리기 테스트 |
| `dotnet run samples/fba/advanced_layout.cs` | **레이아웃** — PushID, Cursor, Scroll, StyleVar, ClipRect |
| `dotnet run samples/fba/columns_demo.cs` | **Columns** — Legacy Columns API 전체 시연 |
| `dotnet run samples/fba/image_and_popups.cs` | **이미지/팝업** — Image, Tooltip, TreeNodeV, TextLink |
| `dotnet run samples/fba/input_queries.cs` | **입력 쿼리** — 키보드/마우스 상태, Shortcut, 클립보드 |
| `dotnet run samples/fba/item_status.cs` | **아이템 상태** — IsItemActive/Clicked/Edited, GetItemRect |

---

## DSL 방식으로 UI 만들기

코드 대신 **마크업**으로 UI를 정의할 수도 있습니다:

```csharp
#:property TargetFramework=net10.0
#:package Duxel.App@*-*

using Duxel.App;
using Duxel.Core.Dsl;

var dslText = """
Window "My App"
  Text "Welcome to Duxel DSL!"
  Row
    Button Id="hello" Text="Say Hello"
    Button Id="bye" Text="Say Bye"
  SliderFloat Id="volume" Text="Volume" Min=0 Max=1
  Checkbox Id="mute" Text="Mute" Default=false
  Combo Id="quality" Text="Quality" Items="Low|Medium|High"
""";

var doc = UiDslParser.Parse(dslText);

DuxelApp.Run(new DuxelAppOptions
{
    Window = new DuxelWindowOptions { Title = "DSL Demo" },
    Dsl = new DuxelDslOptions
    {
        State = new UiDslState(),
        Render = emitter => doc.Emit(emitter)
    }
});
```

```powershell
dotnet run dsl_demo.cs
```

---

## FBA 작동 원리

FBA는 .NET 10의 [File-Based App](https://learn.microsoft.com/dotnet/core/whats-new/dotnet-10#file-based-apps) 기능을 활용합니다:

```csharp
#:property TargetFramework=net10.0   // 대상 프레임워크 지정
#:package Duxel.App@*-*              // NuGet 패키지 참조 (최신 프리릴리즈 포함)
```

- `#:property` — MSBuild 속성 설정
- `#:package` — NuGet 패키지 참조 (`@*-*`는 최신 프리릴리즈까지 포함하는 floating version)
- 프로젝트 파일(`.csproj`) 없이 단일 `.cs` 파일로 실행 가능

### 버전 지정 방식

```csharp
#:package Duxel.App@*-*         // 최신 프리릴리즈 포함
#:package Duxel.App@0.*-*       // 0.x 대의 최신 프리릴리즈
#:package Duxel.App@0.1.5-*     // 0.1.5의 모든 프리릴리즈
#:package Duxel.App@*            // 최신 stable만
```

---

## 나만의 FBA 앱 만들기

### 기본 템플릿

```csharp
#:property TargetFramework=net10.0
#:package Duxel.App@*-*

using Duxel.App;
using Duxel.Core;

DuxelApp.Run(new DuxelAppOptions
{
    Window = new DuxelWindowOptions
    {
        Title = "My App",
        Width = 1280,
        Height = 720,
        VSync = true
    },
    Screen = new MyScreen()
});

public sealed class MyScreen : UiScreen
{
    // 상태 변수를 여기에 선언
    private string _name = "World";
    private float _slider = 0.5f;
    private bool _checkbox = true;
    private int _comboIdx = 0;

    public override void Render(UiImmediateContext ui)
    {
        ui.BeginWindow("My Window");

        // 텍스트
        ui.Text($"Hello, {_name}!");

        // 입력
        ui.InputText("Name", ref _name, 64);

        // 슬라이더
        ui.SliderFloat("Value", ref _slider, 0f, 1f, 0f, "0.00");

        // 체크박스
        ui.Checkbox("Enable", ref _checkbox);

        // 콤보박스
        string[] items = ["Option A", "Option B", "Option C"];
        ui.Combo("Select", ref _comboIdx, items);

        // 버튼
        if (ui.Button("Reset"))
        {
            _name = "World";
            _slider = 0.5f;
        }

        // 프로그레스 바
        ui.ProgressBar(_slider, new UiVector2(200f, 16f), $"{_slider * 100f:0}%");

        ui.EndWindow();
    }
}
```

### 앱 옵션 상세

```csharp
DuxelApp.Run(new DuxelAppOptions
{
    Window = new DuxelWindowOptions
    {
        Width = 1280,           // 윈도우 너비
        Height = 720,           // 윈도우 높이
        Title = "My App",       // 타이틀바 텍스트
        VSync = true            // 수직 동기화 (false: 무제한 FPS)
    },
    Renderer = new DuxelRendererOptions
    {
        MinImageCount = 3,      // Triple Buffering
        EnableValidationLayers = false,  // Vulkan 디버그 (개발 시 true)
        EnableVSync = true
    },
    Font = new DuxelFontOptions
    {
        FontSize = 26,          // 기본 폰트 크기
        FastStartup = true,     // Built-in ASCII → 비동기 TTF 전환
        InitialGlyphs = ["한글 초기 글리프"]  // 시작 시 미리 로드할 문자
    },
    Theme = UiTheme.ImGuiDark,  // Dark / Light / Classic
    Screen = new MyScreen()
});
```

---

## 위젯 빠른 레퍼런스

| 카테고리 | 주요 API |
|---|---|
| **텍스트** | `Text`, `TextColored`, `TextWrapped`, `BulletText`, `LabelText` |
| **버튼** | `Button`, `SmallButton`, `ArrowButton`, `InvisibleButton`, `ImageButton` |
| **입력** | `InputText`, `InputInt`, `InputFloat`, `InputTextMultiline` |
| **슬라이더** | `SliderFloat`, `SliderInt`, `VSliderFloat`, `SliderAngle` |
| **드래그** | `DragFloat`, `DragInt`, `DragFloatRange2`, `DragIntRange2` |
| **토글** | `Checkbox`, `RadioButton`, `CheckboxFlags` |
| **선택** | `Combo`, `ListBox`, `Selectable` |
| **색상** | `ColorEdit3/4`, `ColorPicker3/4`, `ColorButton` |
| **트리** | `TreeNode`, `TreeNodeEx`, `CollapsingHeader` |
| **탭** | `BeginTabBar`, `BeginTabItem`, `TabItemButton` |
| **테이블** | `BeginTable`, `TableSetupColumn`, `TableHeadersRow` |
| **레이아웃** | `SameLine`, `NewLine`, `Separator`, `Indent`, `BeginGroup`, `Columns` |
| **팝업** | `OpenPopup`, `BeginPopup`, `BeginPopupModal`, `BeginPopupContextItem` |
| **툴팁** | `SetTooltip`, `BeginTooltip`, `SetItemTooltip` |
| **드로잉** | `AddLine`, `AddRect`, `AddCircle`, `AddTriangle`, `AddBezierCubic` |
| **윈도우** | `BeginWindow`, `BeginChild`, `SetNextWindowSize/Pos` |
| **입력 쿼리** | `IsKeyDown`, `IsMouseClicked`, `Shortcut`, `GetClipboardText` |

전체 API 목록: [imgui-coverage.md](https://github.com/dimohy/Duxel/blob/main/docs/imgui-coverage.md)

---

## 프로젝트 구조 (참고)

```
Duxel/
├── src/
│   ├── Duxel.App/          ← 앱 진입점, DSL 바인딩
│   ├── Duxel.Core/         ← UI 컨텍스트, 위젯, 드로우리스트
│   ├── Duxel.Core.Dsl.Generator/  ← .ui → C# 소스 생성기
│   ├── Duxel.Platform.Glfw/       ← GLFW 윈도우/입력
│   ├── Duxel.Platform.Windows/    ← Windows 전용 (IME)
│   └── Duxel.Vulkan/              ← Vulkan 렌더러
├── samples/
│   ├── fba/                ← FBA 단일 파일 샘플
│   ├── Duxel.Sample/       ← DSL + 소스 생성기 프로젝트 샘플
│   └── Duxel.PerfTest/     ← 성능 벤치마크
└── docs/                   ← 설계/DSL/API 문서
```

---

## 링크

- **GitHub**: https://github.com/dimohy/Duxel
- **NuGet**: https://www.nuget.org/packages/Duxel.App
- **DSL 레퍼런스**: [docs/ui-dsl.md](https://github.com/dimohy/Duxel/blob/main/docs/ui-dsl.md)
- **설계 문서**: [docs/design.md](https://github.com/dimohy/Duxel/blob/main/docs/design.md)

---

> **Duxel**은 .NET 생태계에서 Dear ImGui 수준의 즉시 모드 GUI를 순수 C#으로 구현하는 프로젝트입니다.  
> 피드백, 이슈, PR 환영합니다! ⭐
