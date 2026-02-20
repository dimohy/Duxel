# Version History

Duxel의 버전별 변경 내역을 누적 기록합니다.

## 0.1.13-preview (2026-02-20)

### 변경 내역

- **[버그]** `Duxel.Windows.App`/`Duxel.Platform.Windows` TargetFramework를 `net10.0-windows` → `net10.0`으로 변경 — FBA 샘플(`net10.0`)에서 `dotnet run` 시 NU1202 호환성 오류 해소, 향후 리눅스 크로스플랫폼 FBA 테스트 경로 확보

### Packaging / Release

- NuGet 패키지 버전을 `0.1.13-preview`로 상향했습니다 (`Duxel.App`, `Duxel.Windows.App`).

## 0.1.12-preview (2026-02-20)

### 변경 내역

- **[기능]** DirectWrite 기반 텍스트 렌더링 시스템 추가 — `WindowsDirectWriteGlyphRasterizer` 신규 구현, Direct Text 런타임 토글 API(`SetDirectTextEnabled`/`GetDirectTextEnabled`), `DUXEL_DIRECT_TEXT` 환경변수 지원, 텍스트 캐시 관리(LRU 256엔트리)
- **[기능]** Windows 플랫폼 백엔드 독립 분리 — `WindowsPlatformBackend`(975줄) 신규 구현, GLFW 플랫폼(`Duxel.Platform.Glfw`) 완전 제거
- **[기능]** 즉시 모드 애니메이션 프레임워크 추가 — `AnimateFloat` API(easing: OutCubic 등), `RequestFrame` 연속 렌더 요청, 애니메이션 트랙 상태 관리
- **[기능]** 폰트 크기 런타임 제어 API 추가 — `PushFontSize`/`PopFontSize`, `DrawTextAligned`의 `fontSize` 파라미터, 폰트 아틀라스 래스터라이저 분리(`UiFontAtlas.Rasterizers.cs`)
- **[기능]** 위젯/벤치 헬퍼 API 대량 승격 — `BeginWindowCanvas`/`EndWindowCanvas`, `DrawOverlayText`, `UiFpsCounter`, `DrawKeyValueRow`, `BenchOptions`, `DrawLayerCardSkeleton`/`DrawLayerCard`/`DrawLayerCardInteractive`(`UiLayerCardInteraction` 구조체)
- **[기능]** 레이아웃 시스템 확장 — `EnableRootViewportContentLayout`, `AlignRect`, `SetNextItemVerticalAlign`, `SameLine` 수직 정렬 지원
- **[기능]** 아이콘 시스템 추가 — `UiImmediateContext.Icons` 내장 아이콘 렌더 지원
- **[기능]** Windows 계산기 FBA — 사이버 backdrop/리플 효과/FX 버튼/반투명 UI 시연(`windows_calculator_fba.cs`), RPN 트레이스/멀티베이스 쇼케이스(`windows_calculator_duxel_showcase_fba.cs`)
- **[개선]** 위젯 API 시그니처 통일 — Combo/ListBox/Table/Tree에 `string? id` 파라미터 추가로 ID 충돌 방지
- **[개선]** IME 처리 안정성 개선 — `WindowsImeHandler` 리팩토링
- **[개선]** 10개+ FBA 샘플에서 보일러플레이트(FPS 측정/오버레이/벤치파서/카드렌더)를 라이브러리 API 호출로 전환, 코드 간결성 대폭 향상
- **[개선]** Direct Text ON/OFF A/B 벤치에서 평균 FPS +5.87% 개선 확인 (375→397 FPS)

### Packaging / Release

- NuGet 패키지 버전을 `0.1.12-preview`로 상향했습니다 (`Duxel.App`, `Duxel.Windows.App`).

## 0.1.11-preview (2026-02-17)

### Performance Highlights

- 전역 정적 캐시(`duxel.global.static:*`) 전략을 벤치 샘플에 적용해 정적 배경 재생성 비용을 줄이고, all-dynamic 대비 성능 차이를 재현 가능한 형태로 정리했습니다.
- 레이어 dirty 전략을 `all` vs `single`로 분리 검증해, 무효화 범위를 줄였을 때 캐시 재빌드 횟수와 FPS가 크게 개선되는 경로를 확인했습니다.
- 텍스트/레이어/클립 경로의 핫패스 실험을 통해 유효한 최적화는 유지하고, 성능 악화가 확인된 시도는 즉시 롤백해 기준 성능을 보호했습니다.

### Benchmark & Measurement

- clip clamp A/B 자동화(`scripts/run-vector-clip-ab.ps1`, `scripts/run-layer-widget-clip-ab.ps1`)에 타임아웃/프로세스 정리를 포함해 장시간 측정 안정성을 높였습니다.
- 반복 성능 비교 자동화(`scripts/run-duxel-perf-ab.ps1`)를 추가해 baseline/candidate 평균, 분산, 개선율 산출을 표준화했습니다.
- 성능 기록 정책과 세션 로그를 보강해 변경-검증-결과를 추적 가능한 형태로 문서화했습니다.

### Packaging / Release

- NuGet 패키지 버전을 `0.1.11-preview`로 상향했습니다 (`Duxel.App`, `Duxel.Windows.App`).

## 0.1.10-preview (2026-02-15)

### Rendering / Layer Cache

- Vulkan 렌더러의 texture 레이어 정적 태그 판별을 보강해 opacity suffix(`:oXXXXXXXX`)가 있는 경우에도 texture compose 재사용 경로가 정상 동작하도록 수정했습니다.
- layer static-cache 검증 시 backend/opacity 조합에서 재사용 태그 정합성을 재검토하고 회귀 포인트를 정리했습니다.

### Samples / Bench

- `samples/fba/idle_layer_validation.cs`에 `DUXEL_LAYER_BENCH_OPACITY` 환경변수를 추가해 opacity 고정 회귀 벤치 자동화를 지원합니다.
- `samples/fba/Duxel_perf_test_fba.cs`에서 충돌 시 각속도/회전 방향도 영향을 받도록 반응 모델(충격 + 감쇠)을 확장했습니다.

### Packaging / NuGet

- NuGet 패키지 버전을 `0.1.10-preview`로 상향했습니다 (`Duxel.App`, `Duxel.Windows.App`).

## 0.1.9-preview (2026-02-15)

### Packaging / NuGet

- `Duxel.App`, `Duxel.Windows.App` 패키지 설명(Description)을 최신 배포 구조 기준으로 정리했습니다.
- NuGet 배포는 `Duxel.App`, `Duxel.Windows.App` 두 패키지(0.1.9-preview)만 배포하도록 유지했습니다.

### Samples

- 프로젝트 샘플을 DSL 검증 중심으로 단순화해 `samples/Duxel.Sample`만 유지했습니다.
- 삭제: `samples/Duxel.PerfTest`, `samples/Duxel.Sample.NativeAot`
- FBA 샘플의 패키지 지시자를 `Duxel.Windows.App` 기준으로 일괄 전환했습니다.

### Documentation

- README의 프로젝트 샘플 표와 빌드/배포 안내를 현행 샘플 구조에 맞게 갱신했습니다.
- 관련 문서(`docs/ui-dsl.md`, `docs/getting-started-fba.md`)의 삭제 샘플 참조를 정리했습니다.
- ImGui 관련 분산 문서를 `docs/design.md`로 통합하고, `docs/imgui-coverage.md`를 삭제했습니다.
- `docs/todo.md`를 완료 항목 제거 후 "남은 일감" 전용 문서로 재구성했습니다.

## 0.1.8-preview (2026-02-15)

### Packaging / Distribution

- 배포 패키지 전략을 `Duxel.App`, `Duxel.Windows.App` 2개로 단순화했습니다.
- `Duxel.Core`, `Duxel.Vulkan`, `Duxel.Platform.Windows`는 독립 NuGet 배포를 중단하고 상위 패키지에 번들링되도록 전환했습니다.
- `Duxel.Windows.App`에 `Duxel.Platform.Windows`를 포함해 Windows 앱 사용자는 패키지 하나만 설치하면 되도록 구성했습니다.

### Architecture

- `Duxel.App`에서 Windows 직접 종속(`WindowsClipboard`, `WindowsImeHandler`, `WindowsUiImageDecoder`, `WindowsKeyRepeatSettingsProvider`)을 제거했습니다.
- 플랫폼별 구현 주입을 위한 옵션 훅을 추가했습니다:
  - `KeyRepeatSettingsProvider`
  - `ClipboardFactory`
  - `ImeHandlerFactory`

### DSL / Source Generator

- `Duxel.Core.Dsl.Generator`를 `Duxel.App` 패키지의 analyzer(`analyzers/dotnet/cs`)로 포함해 단일 설치에서도 소스 생성이 동작하도록 구성했습니다.

### Documentation

- `README.md`는 최신 버전 개선사항만 유지하고, 누적 이력은 본 문서로 분리했습니다.

---

## 0.1.7-preview

### Rendering / Performance

- Vulkan 백엔드에 TAA/FXAA 토글 경로를 보강하고, 런타임 AA 전환 시 리소스/파이프라인 재구성을 안전하게 처리하도록 개선했습니다.
- 성능 샘플과 체크리스트를 정비해 MSAA/FXAA 비교 실험을 반복 가능한 절차로 수행할 수 있게 했습니다.

### Core / Platform

- `Duxel.Core`에 플랫폼 중립 이미지 API(`UiImageTexture`, `UiImageEffects`, `IUiImageDecoder`)를 추가했습니다.
- Windows 전용 디코더를 `Duxel.Platform.Windows`로 분리하고 `Duxel.App`에서 런타임 등록하도록 구성해 Core 계층의 플랫폼 종속성을 제거했습니다.

### Samples / UI

- FBA 이미지 샘플에 웹 이미지 소스 선택(PNG/JPG/GIF)과 GIF 프레임 애니메이션 재생을 추가했습니다.
- 접힘/확장 UI 동작을 보정해 접힘 시 3px 본문 peek를 유지하면서 캔버스 돌출을 방지했습니다.
