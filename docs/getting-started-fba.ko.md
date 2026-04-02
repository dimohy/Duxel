# Duxel FBA 빠른 시작 가이드

> 마지막 동기화: 2026-03-25

단일 `.cs` 파일로 Duxel 앱을 실행하는 FBA(File-Based App) 기준 가이드입니다.

## 필수 환경

| 항목 | 요구 사항 |
|---|---|
| .NET SDK | 10.0 이상 |
| OS | Windows 10/11 |
| GPU | Vulkan 1.0+ 지원 |

## 30초 실행

`hello.cs`:

```csharp
#:property TargetFramework=net10.0
#:property platform=windows
#:package Duxel.$(platform).App@*-*

using Duxel.App;
using Duxel.Core;
using Duxel.Windows.App;

DuxelWindowsApp.Run(new DuxelAppOptions
{
    Window = new DuxelWindowOptions { Title = "Hello Duxel" },
    Screen = new HelloScreen()
});

public sealed class HelloScreen : UiScreen
{
    public override void Render(UiImmediateContext ui)
    {
        ui.BeginWindow("Hello");
        ui.Text("Hello, Duxel!");
        ui.EndWindow();
    }
}
```

```powershell
dotnet run hello.cs
```

## 샘플 실행

레포 샘플은 `samples/fba/`에 있습니다.

```powershell
dotnet run samples/fba/all_features.cs
./run-fba.ps1 samples/fba/all_features.cs
./run-fba.ps1 samples/fba/all_features.cs -Managed
```

- `dotnet run ...`: NuGet 패키지 기준 실행
- `run-fba.ps1`: 로컬 프로젝트 참조로 치환해 실행(기본 NativeAOT)

### 자주 쓰는 샘플

| 파일 | 설명 |
|---|---|
| `all_features.cs` | 타이포그래피, 레이아웃, 팝업/컨텍스트, 입력 질의, 아이템 상태, 멀티셀렉트, 레이어/애니메이션 전용 창까지 포함한 전체 위젯 종합 데모 |
| `hello_duxel_fba.cs` | 작은 `Hello`와 큰 `Duxel!`만 보여주는 초간단 인사 샘플 |
| `windows_calculator_fba.cs` | 계산기 UI 데모 |
| `text_render_validation_fba.cs` | 텍스트 렌더 검증 |
| `font_style_validation_fba.cs` | 폰트 스타일/크기 렌더링 검증 |
| `Duxel_perf_test_fba.cs` | 대량 폴리곤 벤치 |

## 프로필/환경 변수

```powershell
$env:DUXEL_APP_PROFILE='render'
./run-fba.ps1 samples/fba/Duxel_perf_test_fba.cs -Managed
Remove-Item Env:DUXEL_APP_PROFILE
```

## DSL 방식

DSL 기반 UI는 `UiDslScreen` 클래스와 `.ui`/`.duxel-theme` 파일을 프로젝트에서 사용합니다. 완전한 예제는 `samples/Duxel.ThemeDemo`를 참고하세요.

## 참고 문서

- [docs/fba-reference-guide.ko.md](docs/fba-reference-guide.ko.md)
- [docs/fba-run-samples.ko.md](docs/fba-run-samples.ko.md) · [English](docs/fba-run-samples.md)
- [docs/ui-dsl.ko.md](docs/ui-dsl.ko.md)
