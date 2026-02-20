# Duxel FBA 샘플 — 바로 실행하기

> .NET 10 + Vulkan 즉시 모드 GUI 프레임워크 **Duxel**의 FBA 샘플을 **복사-붙여넣기 한 줄**로 바로 실행하세요.

## 필수 환경

- **.NET 10 SDK** 이상 ([다운로드](https://dotnet.microsoft.com/download/dotnet/10.0))
- **Vulkan 1.0+** 지원 GPU (대부분의 최신 GPU)

---

## 실행 방법

아래 명령어를 터미널에 복사-붙여넣기 하면 바로 실행됩니다.

### PowerShell

```powershell
irm https://raw.githubusercontent.com/dimohy/Duxel/main/samples/fba/<파일명> -OutFile <파일명>; dotnet run <파일명>
```

### Bash / macOS / Linux

```bash
curl -sLO https://raw.githubusercontent.com/dimohy/Duxel/main/samples/fba/<파일명> && dotnet run <파일명>
```

---

## 전체 위젯 종합 데모 ⭐

400+ API를 종합적으로 시연하는 올인원 데모입니다. **이것부터 실행해보세요!**

**PowerShell**
```powershell
irm https://raw.githubusercontent.com/dimohy/Duxel/main/samples/fba/all_features.cs -OutFile all_features.cs; dotnet run all_features.cs
```

**Bash**
```bash
curl -sLO https://raw.githubusercontent.com/dimohy/Duxel/main/samples/fba/all_features.cs && dotnet run all_features.cs
```

> 메뉴바 · 슬라이더 · 드래그 · 입력 · 컬러 피커 · 콤보/리스트박스 · 트리/탭 · 테이블 · 팝업/모달 · 툴팁 · 드로잉 프리미티브 · 드래그앤드롭 · ListClipper(10K) · 시간/FPS/VSync 토글

---

## DSL 선언적 UI

마크업으로 UI를 선언적으로 구성하는 데모입니다.

**PowerShell**
```powershell
irm https://raw.githubusercontent.com/dimohy/Duxel/main/samples/fba/dsl_showcase.cs -OutFile dsl_showcase.cs; dotnet run dsl_showcase.cs
```

**Bash**
```bash
curl -sLO https://raw.githubusercontent.com/dimohy/Duxel/main/samples/fba/dsl_showcase.cs && dotnet run dsl_showcase.cs
```

> DSL 마크업으로 Text · Button · Input · Checkbox · Slider · Combo · TabBar · Table · TreeNode 구성

---

## DSL 인터랙션

DSL 바인딩으로 상태 연동 · 동적 표시를 시연합니다.

**PowerShell**
```powershell
irm https://raw.githubusercontent.com/dimohy/Duxel/main/samples/fba/dsl_interaction.cs -OutFile dsl_interaction.cs; dotnet run dsl_interaction.cs
```

**Bash**
```bash
curl -sLO https://raw.githubusercontent.com/dimohy/Duxel/main/samples/fba/dsl_interaction.cs && dotnet run dsl_interaction.cs
```

> Drag · Slider · Color · Child · Popup · 상태 바인딩

---

## 메뉴/서브메뉴 Z-Order

중첩 메뉴가 뒤의 컨트롤을 올바르게 가리는지 검증하는 데모입니다.

**PowerShell**
```powershell
irm https://raw.githubusercontent.com/dimohy/Duxel/main/samples/fba/menu_submenu_zorder.cs -OutFile menu_submenu_zorder.cs; dotnet run menu_submenu_zorder.cs
```

**Bash**
```bash
curl -sLO https://raw.githubusercontent.com/dimohy/Duxel/main/samples/fba/menu_submenu_zorder.cs && dotnet run menu_submenu_zorder.cs
```

> MainMenuBar · 2단 서브메뉴 · 팝업 차단 레이어

---

## 고급 레이아웃

PushID, ItemWidth, Cursor, ScrollControl, StyleVar, TextWrap, FontScale 등을 시연합니다.

**PowerShell**
```powershell
irm https://raw.githubusercontent.com/dimohy/Duxel/main/samples/fba/advanced_layout.cs -OutFile advanced_layout.cs; dotnet run advanced_layout.cs
```

**Bash**
```bash
curl -sLO https://raw.githubusercontent.com/dimohy/Duxel/main/samples/fba/advanced_layout.cs && dotnet run advanced_layout.cs
```

> PushID · PushItemWidth · SetNextWindowBgAlpha · Scroll · PushStyleVar · PushTextWrapPos · Font Scale

---

## Legacy Columns

Columns API 전체 사용법을 보여줍니다.

**PowerShell**
```powershell
irm https://raw.githubusercontent.com/dimohy/Duxel/main/samples/fba/columns_demo.cs -OutFile columns_demo.cs; dotnet run columns_demo.cs
```

**Bash**
```bash
curl -sLO https://raw.githubusercontent.com/dimohy/Duxel/main/samples/fba/columns_demo.cs && dotnet run columns_demo.cs
```

> 2열/3열 · Border · ColumnWidth/Offset 쿼리 · 혼합 위젯

---

## 이미지 / 팝업 / 고급 위젯

Image, ImageButton, 고급 Popup, Tooltip, TextLink, TreeNodeV 등을 시연합니다.

**PowerShell**
```powershell
irm https://raw.githubusercontent.com/dimohy/Duxel/main/samples/fba/image_and_popups.cs -OutFile image_and_popups.cs; dotnet run image_and_popups.cs
```

**Bash**
```bash
curl -sLO https://raw.githubusercontent.com/dimohy/Duxel/main/samples/fba/image_and_popups.cs && dotnet run image_and_popups.cs
```

> Image · ImageWithBg · ImageButton · OpenPopupOnItemClick · ContextVoid · TextLink · BeginItemTooltip · ListBoxHeader/Footer

---

## 이미지 효과 실험실 (신규)

웹 PNG/JPG/GIF 소스 전환, GIF 애니메이션 재생, 이미지 효과(Zoom/Rotation/Alpha/Brightness/Contrast/Pixelate)를 실험하는 샘플입니다.

**PowerShell**
```powershell
irm https://raw.githubusercontent.com/dimohy/Duxel/main/samples/fba/image_widget_effects_fba.cs -OutFile image_widget_effects_fba.cs; dotnet run image_widget_effects_fba.cs
```

**Bash**
```bash
curl -sLO https://raw.githubusercontent.com/dimohy/Duxel/main/samples/fba/image_widget_effects_fba.cs && dotnet run image_widget_effects_fba.cs
```

선택적으로 로컬 이미지를 강제 지정할 수 있습니다.

**PowerShell (Custom 경로 지정)**
```powershell
$env:DUXEL_IMAGE_PATH='C:\images\sample.gif'; dotnet run image_widget_effects_fba.cs
```

> Web PNG/JPG/GIF 자동 다운로드 · GIF 프레임 지연 기반 재생 · 접힘 시 3px 본문 peek 유지

---

## 키보드/마우스 입력 쿼리

키보드·마우스 상태 조회, 단축키, 클립보드 등을 시연합니다.

**PowerShell**
```powershell
irm https://raw.githubusercontent.com/dimohy/Duxel/main/samples/fba/input_queries.cs -OutFile input_queries.cs; dotnet run input_queries.cs
```

**Bash**
```bash
curl -sLO https://raw.githubusercontent.com/dimohy/Duxel/main/samples/fba/input_queries.cs && dotnet run input_queries.cs
```

> IsKeyDown · IsKeyPressed · IsMouseDragging · Shortcut(Ctrl+S) · GetClipboardText

---

## 아이템 상태 쿼리

위젯의 Hovered/Active/Focused/Clicked/Edited 생명주기를 실시간 추적합니다.

**PowerShell**
```powershell
irm https://raw.githubusercontent.com/dimohy/Duxel/main/samples/fba/item_status.cs -OutFile item_status.cs; dotnet run item_status.cs
```

**Bash**
```bash
curl -sLO https://raw.githubusercontent.com/dimohy/Duxel/main/samples/fba/item_status.cs && dotnet run item_status.cs
```

> IsItemActive · Activated · Deactivated · DeactivatedAfterEdit · GetItemRectMin/Max/Size · IsRectVisible

---

## 레이어 캐시/GPU 버퍼 검증 (idle_layer_validation)

레이어 정적 캐시 및 GPU 상주 버퍼의 성능/정합성을 검증하는 벤치마크 샘플입니다.

**PowerShell**
```powershell
irm https://raw.githubusercontent.com/dimohy/Duxel/main/samples/fba/idle_layer_validation.cs -OutFile idle_layer_validation.cs; dotnet run idle_layer_validation.cs
```

**Bash**
```bash
curl -sLO https://raw.githubusercontent.com/dimohy/Duxel/main/samples/fba/idle_layer_validation.cs && dotnet run idle_layer_validation.cs
```

환경변수로 백엔드/opacity/레이아웃/입자 수 등을 제어할 수 있습니다.

**PowerShell (환경변수 예시)**
```powershell
$env:DUXEL_LAYER_BENCH_BACKEND='texture'
$env:DUXEL_LAYER_BENCH_OPACITY='0.5'
$env:DUXEL_LAYER_BENCH_PARTICLES='3000,9000'
$env:DUXEL_LAYER_BENCH_LAYOUTS='baseline,frontheavy'
$env:DUXEL_LAYER_BENCH_PHASE_SECONDS='2'
dotnet run idle_layer_validation.cs
```

| 환경변수 | 기본값 | 설명 |
| --- | --- | --- |
| `DUXEL_LAYER_BENCH_BACKEND` | `drawlist` | 레이어 캐시 백엔드 (`drawlist` / `texture`) |
| `DUXEL_LAYER_BENCH_OPACITY` | `1.0` | 레이어 opacity (0.2 ~ 1.0) |
| `DUXEL_LAYER_BENCH_PARTICLES` | `3000,9000,18000` | 벤치 입자 수 (콤마 구분) |
| `DUXEL_LAYER_BENCH_LAYOUTS` | `baseline` | 레이아웃 프리셋 (`baseline`, `frontheavy`, `uniform`, `dense`) |
| `DUXEL_LAYER_BENCH_PHASE_SECONDS` | `2.5` | 페이즈당 측정 시간(초) |
| `DUXEL_LAYER_BENCH_DISABLE_FAST_RENDER` | `false` | 빠른 렌더 경로 비활성화 |
| `DUXEL_LAYER_BENCH_OUT` | _(없음)_ | 벤치 결과 JSON 출력 경로 |

> 레이어 캐시 ON/OFF 비교 · drawlist/texture 백엔드 · opacity 회귀 · 입자 수/레이아웃 매트릭스 벤치

---

## 성능 벤치마크 (PerfTest)

대량 폴리곤 물리 시뮬레이션으로 DrawList 렌더링 성능을 테스트합니다.

**PowerShell**
```powershell
irm https://raw.githubusercontent.com/dimohy/Duxel/main/samples/fba/Duxel_perf_test_fba.cs -OutFile Duxel_perf_test_fba.cs; dotnet run Duxel_perf_test_fba.cs
```

**Bash**
```bash
curl -sLO https://raw.githubusercontent.com/dimohy/Duxel/main/samples/fba/Duxel_perf_test_fba.cs && dotnet run Duxel_perf_test_fba.cs
```

> 다각형 추가/제거 · 속도/크기/면수/회전 슬라이더 · FPS 표시 · 바운딩 충돌

---

## Windows 계산기

Windows 스타일 계산기에 사이버 backdrop, 리플 효과, FX 버튼, 반투명 UI를 적용한 데모입니다.

**PowerShell**
```powershell
irm https://raw.githubusercontent.com/dimohy/Duxel/main/samples/fba/windows_calculator_fba.cs -OutFile windows_calculator_fba.cs; dotnet run windows_calculator_fba.cs
```

**Bash**
```bash
curl -sLO https://raw.githubusercontent.com/dimohy/Duxel/main/samples/fba/windows_calculator_fba.cs && dotnet run windows_calculator_fba.cs
```

> 사이버 그리드 배경 · 버튼 리플 이펙트 · 네온 글로우 FX 버튼 · AnimateFloat 실시간 전환

---

## 계산기 쇼케이스 (RPN 트레이스)

RPN 토큰 추적, 멀티베이스 동시 표시, 32비트 토글 그리드를 시연합니다.

**PowerShell**
```powershell
irm https://raw.githubusercontent.com/dimohy/Duxel/main/samples/fba/windows_calculator_duxel_showcase_fba.cs -OutFile windows_calculator_duxel_showcase_fba.cs; dotnet run windows_calculator_duxel_showcase_fba.cs
```

**Bash**
```bash
curl -sLO https://raw.githubusercontent.com/dimohy/Duxel/main/samples/fba/windows_calculator_duxel_showcase_fba.cs && dotnet run windows_calculator_duxel_showcase_fba.cs
```

> Token→RPN→Eval 변환 과정 표시 · HEX/OCT/BIN 동시 표시 · 32비트 비트 토글 그리드

---

## 텍스트 렌더 검증

텍스트 정렬, 폰트 크기, 클립 동작을 검증하는 도구입니다.

**PowerShell**
```powershell
irm https://raw.githubusercontent.com/dimohy/Duxel/main/samples/fba/text_render_validation_fba.cs -OutFile text_render_validation_fba.cs; dotnet run text_render_validation_fba.cs
```

**Bash**
```bash
curl -sLO https://raw.githubusercontent.com/dimohy/Duxel/main/samples/fba/text_render_validation_fba.cs && dotnet run text_render_validation_fba.cs
```

> DrawTextAligned Left/Center/Right · PushFontSize · clipToContainer ON/OFF

---

## 레이어 Dirty 전략 벤치

레이어 dirty 전략 `all` vs `single` 분리 검증 벤치마크입니다.

**PowerShell**
```powershell
irm https://raw.githubusercontent.com/dimohy/Duxel/main/samples/fba/layer_dirty_strategy_bench.cs -OutFile layer_dirty_strategy_bench.cs; dotnet run layer_dirty_strategy_bench.cs
```

**Bash**
```bash
curl -sLO https://raw.githubusercontent.com/dimohy/Duxel/main/samples/fba/layer_dirty_strategy_bench.cs && dotnet run layer_dirty_strategy_bench.cs
```

> all vs single dirty 비교 · 캐시 재빌드 횟수 · FPS 차이 측정

---

## 레이어+위젯 혼합 벤치

레이어와 위젯을 혼합한 동적 벤치마크입니다.

**PowerShell**
```powershell
irm https://raw.githubusercontent.com/dimohy/Duxel/main/samples/fba/layer_widget_mix_bench_fba.cs -OutFile layer_widget_mix_bench_fba.cs; dotnet run layer_widget_mix_bench_fba.cs
```

**Bash**
```bash
curl -sLO https://raw.githubusercontent.com/dimohy/Duxel/main/samples/fba/layer_widget_mix_bench_fba.cs && dotnet run layer_widget_mix_bench_fba.cs
```

> DrawLayerCardInteractive 적용 · 위젯 믹스 부하 · 카드 드래그 인터랙션

---

## 전역 정적 캐시 벤치

전역 정적 캐시(`duxel.global.static:*`) 전략의 성능 효과를 측정합니다.

**PowerShell**
```powershell
irm https://raw.githubusercontent.com/dimohy/Duxel/main/samples/fba/global_dirty_strategy_bench.cs -OutFile global_dirty_strategy_bench.cs; dotnet run global_dirty_strategy_bench.cs
```

**Bash**
```bash
curl -sLO https://raw.githubusercontent.com/dimohy/Duxel/main/samples/fba/global_dirty_strategy_bench.cs && dotnet run global_dirty_strategy_bench.cs
```

> all-dynamic 대비 정적 캐시 성능 비교 · BeginWindowCanvas API 시연

---

## UI 복합 스트레스

다중 창/텍스트/테이블/리스트/입력/드로우를 동시에 렌더링하는 스트레스 테스트입니다.

**PowerShell**
```powershell
irm https://raw.githubusercontent.com/dimohy/Duxel/main/samples/fba/ui_mixed_stress.cs -OutFile ui_mixed_stress.cs; dotnet run ui_mixed_stress.cs
```

**Bash**
```bash
curl -sLO https://raw.githubusercontent.com/dimohy/Duxel/main/samples/fba/ui_mixed_stress.cs && dotnet run ui_mixed_stress.cs
```

> 다중 창 · 텍스트 · 테이블 · 리스트 · 입력 · 드로우 프리미티브 복합

---

## 벡터 프리미티브 벤치

라인/사각형/원 벡터 프리미티브 전용 벤치마크 + clip clamp A/B 비교입니다.

**PowerShell**
```powershell
irm https://raw.githubusercontent.com/dimohy/Duxel/main/samples/fba/vector_primitives_bench_fba.cs -OutFile vector_primitives_bench_fba.cs; dotnet run vector_primitives_bench_fba.cs
```

**Bash**
```bash
curl -sLO https://raw.githubusercontent.com/dimohy/Duxel/main/samples/fba/vector_primitives_bench_fba.cs && dotnet run vector_primitives_bench_fba.cs
```

> 라인/사각형/원 대량 렌더 · clip clamp 전략 A/B 비교

---

## 전체 한번에 다운로드

모든 FBA 샘플을 한번에 받으려면:

**PowerShell**
```powershell
$base = "https://raw.githubusercontent.com/dimohy/Duxel/main/samples/fba"
$files = @(
    "all_features.cs", "dsl_showcase.cs", "dsl_interaction.cs",
    "menu_submenu_zorder.cs", "advanced_layout.cs", "columns_demo.cs",
    "image_and_popups.cs", "image_widget_effects_fba.cs",
    "input_queries.cs", "item_status.cs",
    "windows_calculator_fba.cs", "windows_calculator_duxel_showcase_fba.cs",
    "text_render_validation_fba.cs",
    "idle_layer_validation.cs",
    "layer_dirty_strategy_bench.cs", "layer_widget_mix_bench_fba.cs",
    "global_dirty_strategy_bench.cs", "vector_primitives_bench_fba.cs",
    "Duxel_perf_test_fba.cs", "ui_mixed_stress.cs"
)
New-Item -ItemType Directory -Force -Path fba | Out-Null
$files | ForEach-Object { irm "$base/$_" -OutFile "fba/$_"; Write-Host "Downloaded $_" }
Write-Host "`nRun: dotnet run fba/all_features.cs"
```

**Bash**
```bash
BASE="https://raw.githubusercontent.com/dimohy/Duxel/main/samples/fba"
FILES=(all_features.cs dsl_showcase.cs dsl_interaction.cs menu_submenu_zorder.cs \
    advanced_layout.cs columns_demo.cs image_and_popups.cs image_widget_effects_fba.cs \
    input_queries.cs item_status.cs \
    windows_calculator_fba.cs windows_calculator_duxel_showcase_fba.cs \
    text_render_validation_fba.cs \
    idle_layer_validation.cs \
    layer_dirty_strategy_bench.cs layer_widget_mix_bench_fba.cs \
    global_dirty_strategy_bench.cs vector_primitives_bench_fba.cs \
    Duxel_perf_test_fba.cs ui_mixed_stress.cs)
mkdir -p fba
for f in "${FILES[@]}"; do curl -sL "$BASE/$f" -o "fba/$f" && echo "Downloaded $f"; done
echo -e "\nRun: dotnet run fba/all_features.cs"
```

---

## 링크

| | |
|---|---|
| **GitHub** | https://github.com/dimohy/Duxel |
| **NuGet** | https://www.nuget.org/packages/Duxel.App |
| **DSL 문서** | [docs/ui-dsl.md](https://github.com/dimohy/Duxel/blob/main/docs/ui-dsl.md) |
| **FBA 가이드** | [docs/getting-started-fba.md](https://github.com/dimohy/Duxel/blob/main/docs/getting-started-fba.md) |
