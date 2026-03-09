# Version History

Duxel의 버전별 변경 내역을 누적 기록합니다.

## 0.1.15-preview (2026-03-05)

## 0.2.0-preview (2026-03-09)

### 변경 내역

- **[기능]** `samples/fba/all_features.cs`를 더 풍부한 종합 쇼케이스 워크스페이스로 확장했습니다 — 타이포그래피, 레이아웃, 팝업/컨텍스트 패턴, 입력 질의, 아이템 상태, 멀티셀렉트, 레이어/애니메이션 프리뷰, Markdown Studio, 내장 도구 시연 창을 전용 창 단위로 보강했습니다.
- **[개선]** 쇼케이스 표현과 레이아웃 헬퍼를 다듬었습니다 — 메뉴 아래 정렬되는 창 배치, 소형 유틸리티 창의 중앙 배치, compact hero 레이아웃 안정화, 설계/최적화 문서 링크를 포함한 README 문서 연결을 정리했습니다.
- **[개선]** 0.2.0-preview 릴리스에 맞춰 문서를 동기화했습니다 — README/가이드 메타데이터를 갱신하고, FBA 가이드 문서의 동기화 시점을 최신 릴리스 기준으로 맞췄으며, 현재 쇼케이스 범위를 버전 이력에 반영했습니다.
- **[버그]** 멀티라인 입력과 마크다운 편집기의 줄바꿈 처리를 수정했습니다 — CRLF/LF/CR 입력을 정규화하여 숨은 carriage return 문자 없이 안정적으로 편집되도록 했습니다.
- **[버그]** 한글 IME 조합 입력 지연을 수정했습니다 — Windows IME 메시지가 즉시 프레임을 요청하고 live composition 텍스트를 우선 사용하여 벅벅 끊기던 입력 반영을 제거했습니다.
- **[버그]** immediate UI 쇼케이스/라이브러리 상호작용의 레이아웃 회귀를 수정했습니다 — 텍스트 row clipping, child-local columns 폭 계산, compact hero 겹침을 해결하고, 내장 `Closable Window`가 클릭 누수 없이 항상 일반 창 위에 머무르도록 진짜 top-most window semantics를 추가했습니다.

### Packaging / Release

- 패키지 버전을 `0.2.0-preview`로 상향했습니다 (`Duxel.App`, `Duxel.Windows.App`, `Duxel.Core`, `Duxel.Vulkan`, `Duxel.Platform.Windows`).

## 0.1.15-preview (2026-03-05)

### 변경 내역

- **[기능]** 플랫폼 텍스트 백엔드 추상화 추가 — `IPlatformTextBackend` / `PlatformTextRasterizeRequest` / `PlatformTextRasterizeResult` 인터페이스로 아틀라스 파이프라인과 분리된 크로스 플랫폼 텍스트 래스터라이제이션 지원.
- **[기능]** DWrite 텍스트-런 래스터라이제이션 백엔드 추가 — `WindowsPlatformTextBackend`가 폰트 런당 단일 DWrite COM 호출로 래스터라이즈 (글리프별 호출 대비). `BuildFontRuns`로 혼합 스크립트(예: Latin+한글) 텍스트 분리.
- **[기능]** `SetDirectTextBaseFontSize` API 추가 — `UiContext` / `UiImmediateContext`에서 행 높이와 독립적으로 DWrite 기본 em 크기 제어. `DuxelFontOptions.FontSize`에서 자동 연결.
- **[개선]** 모든 위젯 텍스트 렌더링을 DWrite 직접 텍스트 경로로 마이그레이션 — Button, Tree, Tab, Table, Menu, Slider, Input, ListBox, Selectable, Separator, Tooltip, Combo, Drag 전체가 `MeasureTextInternal` / `AddTextInternal`을 사용하여 DWrite 가용 시 자동 활용.
- **[개선]** DWrite 텍스트 경로의 이중 래스터라이제이션 제거 — `TryMeasureDirectText`가 래스터라이즈 결과를 사전 캐싱하여 `TryRenderDirectText`가 항상 캐시 히트.
- **[개선]** 글리프별 DWrite 래스터라이제이션을 텍스트-런 API로 교체 — 글리프별 대신 폰트 런당 단일 COM 호출로 COM 오버헤드 대폭 감소.
- **[개선]** `TrimDirectTextCache` 할당 감소 — `List<>`를 고정 배열로 교체하고 `hasStale` 조기 종료 검사 추가.
- **[개선]** 폰트 아틀라스 디스크 캐시 토글 추가 — `DUXEL_FONT_DISK_CACHE` 환경변수로 아틀라스 직렬화 활성/비활성 제어.
- **[개선]** 폰트 아틀라스 진단 추가 — `DUXEL_FONT_ATLAS_DIAG`, `DUXEL_FONT_ATLAS_DIAG_LOG`, `DUXEL_FONT_ATLAS_DUMP_DIR` 환경변수로 아틀라스 빌드 추적 및 텍스처 덤프.
- **[개선]** Vulkan 폰트 명령 진단 추가 — `DUXEL_VK_FONT_CMD_DIAG`, `DUXEL_VK_FONT_CMD_DIAG_LOG`, `DUXEL_VK_FONT_BOUNDS_ASSERT` 환경변수로 폰트 텍스처 명령 추적 및 바운드 검증.
- **[개선]** `UiFontResource`에 `CodepointSignature` 추가 — 아틀라스 픽셀 데이터의 FNV-1a 해시로 코드포인트 세트 변경 시 캐시 무효화.
- **[개선]** 프레임별 동결 코드포인트 스냅숏 추가 — `frameCodepointSnapshot`으로 `OnMissingGlyph`에 의한 프레임 중간 코드포인트 변이 방지.
- **[버그]** 동적 아틀라스와 DWrite 텍스트 간 텍스처 ID 충돌 수정 — ID 범위 분리 (동적 아틀라스 `1_100_000_000`, DWrite 텍스트 `2_100_000_000`).
- **[버그]** `VulkanRendererBackend.UploadTextureData` 스테이징 버퍼 데이터 레이스 수정 — 펜스 대기 완료 후 호스트 메모리 쓰기로 순서 변경.
- **[버그]** 텍스트-런 API에서 한글 미출력 수정 — `BuildFontRuns`에서 공백 문자가 더 이상 폰트 전환을 유발하지 않아 공백 전용 런의 빈 알파 바운드 문제 해결.
- **[버그]** DWrite 기본 폰트 크기가 `LineHeight`(~21px) 대신 빌드 폰트 크기(16px)를 사용하도록 수정 — `_directTextBaseFontSize`에 정확한 em 크기 저장.
- **[버그]** DWrite 텍스트 수직 중앙 정렬 수정 — 래스터라이즈된 비트맵을 측정된 행 높이 내에서 중앙에 배치하는 Y 오프셋 추가.
- **[버그]** `TryRecreateSwapchain` surface-lost 처리 수정 — `RecreateSwapchain()`을 실패 시 `false` 반환하는 `TryRecreateSwapchain()`으로 교체하여 연쇄 Vulkan 오류 방지.
- **[버그]** 스테이징 버퍼 크기 정규화 검증 수정 — `GetExpectedTextureDataSize`가 포맷별 정확한 바이트 수 계산, `UploadTextureData`가 부족한 픽셀 버퍼를 패딩.

### Packaging / Release

- NuGet 패키지 버전을 `0.1.15-preview`로 상향 (전체 패키지: `Duxel.App`, `Duxel.Windows.App`, `Duxel.Core`, `Duxel.Vulkan`, `Duxel.Platform.Windows`).

## 0.1.14-preview (2026-02-28)

### 변경 내역

- **[버그]** 한글 Fallback 폰트 차단 수정 — `UiTextBuilder`의 `IsHangulCodepoint()` 가드가 한글 코드포인트에 대해 보조 폰트 탐색(예: `malgun.ttf`)을 차단해 글리프 깨짐 발생. 제한을 제거하여 한글도 fallback 폰트를 정상 사용.
- **[버그]** `DUXEL_DIRECT_TEXT=0` 시 DWrite 아틀라스 래스터라이저까지 비활성화되던 문제 수정 — 직접 텍스트 렌더링 경로뿐 아니라 아틀라스 글리프 래스터라이저까지 소프트웨어 TTF로 전환되어 힌팅 없는 저품질 한글 렌더링 발생. 이제 `DUXEL_DIRECT_TEXT`는 직접 텍스트 경로만 제어하고, 아틀라스 래스터라이저는 Windows에서 항상 DWrite 사용 (명시 `DUXEL_ENABLE_TTF_GLYPH_RASTERIZER=1`로만 TTF 전환).
- **[버그]** 동적 폰트 아틀라스 Stale 캐시 재사용 수정 — `ResolveDynamicFontResource`가 코드포인트 세트가 늘어났을 때(예: 109→115글리프)도 기존 캐시를 반환하여 특정 크기에서 한글 누락 발생. `UiFontResource`에 `CodepointSignature`를 추가해 캐시 유효성 검증.
- **[버그]** `SelectClosestCachedFontSize` 크기 불일치 수정 — 10% threshold로 58px 아틀라스를 64px에서 재사용해 글리프 메트릭/UV 좌표 불일치 발생. 근사 매칭 로직 제거, 각 정수 크기가 항상 자체 아틀라스 사용.
- **[버그]** 프레임 중간 코드포인트 드리프트 수정 — `OnMissingGlyph`이 같은 프레임 내에서 `activeCodepointSet`을 변경해 아틀라스 불일치 발생. 프레임 시작 시 `frameCodepointSnapshot`을 한 번만 생성하는 방식으로 변경.
- **[버그]** 새 글리프 발견 시 동적 폰트 캐시 미무효화 수정 — `pendingGlyphs` 증가 또는 렌더러 missing glyph 보고 시 `InvalidateDynamicFontResourceCache()`로 stale 텍스처 전체 폐기.
- **[버그]** 창 닫기 시 Vulkan `ErrorSurfaceLostKhr` 크래시 수정 — `RecreateSwapchain()`을 `TryRecreateSwapchain()`으로 교체해 surface-lost 예외를 안전하게 처리. 렌더 스레드를 `try/catch`로 감싸 종료 시점 Vulkan 예외 흡수.
- **[개선]** `ShouldClose`/`stopRequested` 감지 시 렌더 루프 즉시 중단 추가 — 창 닫기 신호 후 불필요한 추가 프레임 렌더링 방지.

### Packaging / Release

- NuGet 패키지 버전을 `0.1.14-preview`로 상향했습니다 (`Duxel.App`, `Duxel.Windows.App`).

## Documentation Update (2026-02-26)

### 변경 내역

- **[개선]** `README.md`를 영문 중심으로 재구성하고 `README.ko.md`를 추가했습니다.
- **[개선]** `docs/ui-dsl.ko.md`를 현재 파서/런타임(`UiDslParser`, `UiDslWidgetDispatcher`, `UiDslPipeline`) 기준으로 재작성했습니다.
- **[개선]** FBA 가이드 문서(`docs/getting-started-fba.ko.md`, `docs/fba-reference-guide.ko.md`, `docs/fba-run-samples.ko.md`)를 현재 샘플 지시문(`Duxel.$(platform).App`) 기준으로 정합화했습니다.
- **[개선]** 나머지 주요 `docs` 문서에 동기화 시점을 명시해 최신화 기준일을 명확히 했습니다.

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
- 관련 문서(`docs/ui-dsl.ko.md`, `docs/getting-started-fba.ko.md`)의 삭제 샘플 참조를 정리했습니다.
- ImGui 관련 분산 문서를 `docs/design.ko.md`로 통합하고, `docs/imgui-coverage.md`를 삭제했습니다.
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
