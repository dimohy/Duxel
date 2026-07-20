# Duxel FBA 빠른 시작 가이드

> 마지막 동기화: 2026-07-20

단일 `.cs` 파일로 Duxel 앱을 실행하는 .NET 10 FBA(File-Based App) 기준 가이드입니다. Duxel `0.2.9-preview` 패키지는 일반 `net8.0`, `net9.0`, `net10.0` 프로젝트를 지원합니다.

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
| `declarative_dashboard_fba.cs` | `UiState<T>`, 재사용 `UiComponent`, 컴파일된 Windows 11 디자인 토큰, 제품 셸 헬퍼를 사용하는 선언형 C# 대시보드 |
| `hello_duxel_fba.cs` | 작은 `Hello`와 큰 `Duxel!`만 보여주는 초간단 인사 샘플 |
| `extended_title_bar_fba.cs` | 유효 창 아이콘, 탭 모양 앱 컨트롤, 복원·최대화 모두 Duxel 크롬과 동일한 캡션 버튼, 네이티브 Windows 동작, 공개 드래그/경계 API, 테스트 전용 Win32/DWM 진단을 사용하는 Windows 11 스타일 확장 타이틀바 샘플 |
| `windows_calculator_fba.cs` | 창 상태에 따라 바뀌는 최대화/복원 아이콘 등 라이브러리 소유 Duxel 크롬을 사용하는 계산기 UI 데모 |
| `text_render_validation_fba.cs` | 텍스트 렌더 검증 |
| `font_style_validation_fba.cs` | 폰트 스타일/크기 렌더링 검증 |
| `scrolling_static_layer_bench_fba.cs` | 스크롤/클립 정적 레이어 벤치 및 시각 회귀·캐시 무효화 검증 |
| `Duxel_perf_test_fba.cs` | 대량 폴리곤 벤치 |
| `pipeline_ordering_bench_fba.cs` | 동적 solid/text pipeline ordering 벤치 |
| `dynamic_widget_ordering_bench_fba.cs` | 위젯형 동적 ordering과 row clip churn 벤치 |
| `static_cache_rebuild_bench_fba.cs` | static cache rebuild, reuse, allocation pressure 벤치 |
| `static_layer_moving_order_bench_fba.cs` | moving static-layer replay ordering 벤치 |
| `texture_upload_barrier_bench_fba.cs` | texture upload copy/barrier policy 벤치 |
| `directtext_page_upload_bench_fba.cs` | DirectText page-style partial texture upload 벤치 |
| `directtext_dynamic_text_bench_fba.cs` | DirectText 안정 캐시와 변경 문자열의 frame-tail, text-work, 할당 비교 벤치 |
| `vector_primitives_bench_fba.cs` | primitive-heavy vector workload 벤치 |

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
