# Version History

Duxel의 버전별 변경 내역을 누적 기록합니다.

## 0.1.11-preview (2026-02-17)

### Packaging / NuGet

- NuGet 패키지 버전을 `0.1.11-preview`로 상향했습니다 (`Duxel.App`, `Duxel.Windows.App`).

### Benchmark / Tooling

- clip clamp A/B 자동화 스크립트(`scripts/run-vector-clip-ab.ps1`, `scripts/run-layer-widget-clip-ab.ps1`)를 타임아웃/프로세스 정리까지 포함해 안정화했습니다.
- 퍼포먼스 반복 측정 스크립트(`scripts/run-duxel-perf-ab.ps1`)를 통해 baseline/candidate 비교 및 요약 산출을 표준화했습니다.

### Samples / Rendering

- 전역 정적 캐시(`duxel.global.static:*`) 전략을 샘플 벤치에 적용해 all-dynamic 대비 비교가 가능한 경로를 강화했습니다.
- 레이어 dirty 전략 샘플/검증 흐름을 업데이트해 all vs single invalidation 비교 재현성을 높였습니다.

### Documentation

- `README.md` 최신 버전 섹션을 `0.1.11-preview` 기준으로 갱신했습니다.
- 최적화 정책/세션 문서를 최신 실험 결과 기준으로 누적 업데이트했습니다.

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
- `Duxel.Core`, `Duxel.Platform.Glfw`, `Duxel.Vulkan`, `Duxel.Platform.Windows`는 독립 NuGet 배포를 중단하고 상위 패키지에 번들링되도록 전환했습니다.
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
