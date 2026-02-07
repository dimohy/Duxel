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

## 전체 한번에 다운로드

모든 FBA 샘플을 한번에 받으려면:

**PowerShell**
```powershell
$base = "https://raw.githubusercontent.com/dimohy/Duxel/main/samples/fba"
$files = @(
    "all_features.cs", "dsl_showcase.cs", "dsl_interaction.cs",
    "menu_submenu_zorder.cs", "advanced_layout.cs", "columns_demo.cs",
    "image_and_popups.cs", "input_queries.cs", "item_status.cs"
)
New-Item -ItemType Directory -Force -Path fba | Out-Null
$files | ForEach-Object { irm "$base/$_" -OutFile "fba/$_"; Write-Host "Downloaded $_" }
Write-Host "`nRun: dotnet run fba/all_features.cs"
```

**Bash**
```bash
BASE="https://raw.githubusercontent.com/dimohy/Duxel/main/samples/fba"
FILES=(all_features.cs dsl_showcase.cs dsl_interaction.cs menu_submenu_zorder.cs \
       advanced_layout.cs columns_demo.cs image_and_popups.cs input_queries.cs item_status.cs)
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
