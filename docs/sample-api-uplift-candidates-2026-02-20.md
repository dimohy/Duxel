# 샘플 API 승격 후보 정리 (2026-02-20)

> 문서 동기화: 2026-02-26

## 배경
샘플 코드에서 반복되는 구현 패턴(정렬, 클립, 오버레이, 벤치 유틸)을 라이브러리 API로 올리면,
- 샘플은 도메인 기능 데모에 집중
- 레이아웃/렌더 보일러플레이트 감소
- 동일 버그 재발 범위 축소

## 이번 세션에서 이미 승격 완료
1. `UiImmediateContext.DrawTextAligned(...)`
   - 파일: `src/Duxel.Core/UiImmediateContext.Widgets.Basic.cs`
   - 목적: 정렬 + 클립 + 폰트 크기 적용을 단일 API로 제공
   - 샘플 반영: `samples/fba/windows_calculator_fba.cs`
2. `UiImmediateContext.BeginWindowCanvas(...)` / `UiImmediateContext.EndWindowCanvas()`
  - 파일: `src/Duxel.Core/UiImmediateContext.Widgets.Basic.cs`
  - 목적: 윈도우 캔버스의 `PushTexture/PushClipRect/AddRectFilled/Pop*` 보일러플레이트 제거
  - 샘플 반영: `samples/fba/global_dirty_strategy_bench.cs`
3. `UiImmediateContext.DrawOverlayText(...)`
  - 파일: `src/Duxel.Core/UiImmediateContext.Widgets.Basic.cs`
  - 목적: 오버레이 텍스트 정렬/마진/배경 처리 표준화
  - 샘플 반영: `samples/fba/Duxel_perf_test_fba.cs`, `samples/fba/all_features.cs`

---

## 전수 스캔 기반 우선순위 후보

### P0. 캔버스 블록 헬퍼 (`BeginCanvas`/`EndCanvas` 또는 `WithCanvas`)
- 상태: **완료(1차)** — `BeginWindowCanvas/EndWindowCanvas`로 윈도우 캔버스 시나리오 우선 지원
- 반복 패턴:
  - `drawList.PushClipRect(canvas)`
  - `drawList.AddRectFilled(canvas, bg, white, canvas)`
  - 프리미티브 다수 그리기
- 대표 위치:
  - `samples/fba/global_dirty_strategy_bench.cs` (104, 105)
  - `samples/fba/layer_dirty_strategy_bench.cs` (114, 115)
  - `samples/fba/idle_layer_validation.cs` (574, 697)
  - `samples/fba/vector_primitives_bench_fba.cs` (183)
- 제안 API(예시):
  - `UiCanvasScope BeginCanvas(UiRect rect, UiColor? background = null, bool clip = true)`
  - `void EndCanvas(UiCanvasScope scope)`

### P0. 오버레이 텍스트 박스 헬퍼 (`DrawOverlayText`)
- 상태: **완료(1차)** — `DrawOverlayText` 추가 및 주요 샘플 반영
- 반복 패턴:
  - 텍스트 크기 측정 → 우상단 좌표 계산 → 배경 사각형 → 텍스트
- 대표 위치:
  - `samples/fba/Duxel_perf_test_fba.cs` (439, 449)
  - `samples/fba/all_features.cs` (960, 961, 972)
- 제안 API(예시):
  - `void DrawOverlayText(string text, UiOverlayAnchor anchor, UiColor textColor, UiColor? bgColor = null, UiVector2 padding = default)`

### P1. 키-값 행 렌더 헬퍼 (`DrawKeyValueRow` / `ListBoxKeyValueRow`)
- 상태: **완료(1차)** — `DrawKeyValueRow` 추가 및 계산기 샘플 전환
- 반복 패턴:
  - 행 선택 상태 + 인디케이터 + 라벨/값 위치 하드코딩
- 대표 위치:
  - `samples/fba/windows_calculator_fba.cs` (185, 190, 191)
- 제안 API(예시):
  - `void DrawKeyValueRow(UiRect rowRect, string key, string value, bool selected = false, UiColor? accent = null)`

### P1. FPS/프레임 타이밍 집계 유틸 (`UiFpsCounter`)
- 상태: **완료(1차)** — `UiFpsCounter` / `UiFpsSample` 도입 및 5개 샘플 전환
- 반복 패턴:
  - `UpdateFps(delta)` 구현이 다수 샘플에 중복
- 대표 위치:
  - `samples/fba/vector_primitives_bench_fba.cs` (92)
  - `samples/fba/ui_mixed_stress.cs` (172)
  - `samples/fba/layer_widget_mix_bench_fba.cs` (232)
  - `samples/fba/Duxel_perf_test_fba.cs` (533)
  - `samples/fba/all_features.cs` (940)
- 제안 API(예시):
  - `UiFpsSample UiFpsCounter.Tick(double deltaSeconds)`

### P1 적용 결과(2026-02-20)
- 신규 API 파일
  - `src/Duxel.Core/UiFpsCounter.cs`
- 보강 API 파일
  - `src/Duxel.Core/UiImmediateContext.Widgets.Basic.cs` (`DrawKeyValueRow`)
- 샘플 전환
  - `samples/fba/windows_calculator_fba.cs`
  - `samples/fba/all_features.cs`
  - `samples/fba/Duxel_perf_test_fba.cs`
  - `samples/fba/vector_primitives_bench_fba.cs`
  - `samples/fba/ui_mixed_stress.cs`
  - `samples/fba/layer_widget_mix_bench_fba.cs`

### P1. 벤치 설정 파서 유틸 (`BenchOptions`)
- 상태: **완료(1차)** — `BenchOptions` 추가 및 주요 벤치 샘플 파서 공통화
- 반복 패턴:
  - `ReadPhaseSeconds`, `ReadBenchDurationSeconds`, `ReadDensity*`, `Read...Backend`
- 대표 위치:
  - `samples/fba/layer_dirty_strategy_bench.cs` (351)
  - `samples/fba/global_dirty_strategy_bench.cs` (385)
  - `samples/fba/idle_layer_validation.cs` (210, 231, 336)
  - `samples/fba/Duxel_perf_test_fba.cs` (154)
- 제안 API(예시):
  - `BenchOptions.FromEnvironment(prefix, defaults)`

### P1(추가) 적용 결과(2026-02-20)
- 신규 API 파일
  - `src/Duxel.Core/BenchOptions.cs`
- 샘플 전환
  - `samples/fba/Duxel_perf_test_fba.cs`
  - `samples/fba/ui_mixed_stress.cs`
  - `samples/fba/global_dirty_strategy_bench.cs`
  - `samples/fba/layer_dirty_strategy_bench.cs`
  - `samples/fba/vector_primitives_bench_fba.cs`
  - `samples/fba/layer_widget_mix_bench_fba.cs`
  - `samples/fba/idle_layer_validation.cs`

### P2. 레이어 카드 스켈레톤 헬퍼 (`DrawLayerCardSkeleton`)
- 상태: **완료(1차)** — `DrawLayerCardSkeleton` 추가 및 주요 3개 샘플 전환
- 반복 패턴:
  - 카드 외곽 + 헤더 + 제목 + 본문 클립
- 대표 위치:
  - `samples/fba/idle_layer_validation.cs` (734, 751, 769)
  - `samples/fba/layer_dirty_strategy_bench.cs` (130, 142, 143)
  - `samples/fba/layer_widget_mix_bench_fba.cs` (308, 320, 336)
- 제안 API(예시):
  - `UiRect DrawLayerCardSkeleton(UiRect rect, UiColor headerColor, string title, ...)`

### P2 적용 결과(2026-02-20)
- 보강 API 파일
  - `src/Duxel.Core/UiImmediateContext.Widgets.Basic.cs` (`DrawLayerCardSkeleton`)
- 샘플 전환
  - `samples/fba/layer_dirty_strategy_bench.cs`
  - `samples/fba/layer_widget_mix_bench_fba.cs`
  - `samples/fba/idle_layer_validation.cs`

### P3. 레이어 카드 상위 헬퍼 (`DrawLayerCard`)
- 상태: **완료(1차)** — 제목/마커/hit-test 옵션을 포함한 상위 API 추가 및 3개 샘플 전환
- 반복 패턴:
  - 스켈레톤 호출 후 헤더 텍스트 배치
  - 헤더 마커 렌더
  - 카드 전체 hit-test (`InvisibleButton`)와 클릭 처리 연계
- 대표 위치:
  - `samples/fba/idle_layer_validation.cs` (드래그 클릭 로직)
  - `samples/fba/layer_widget_mix_bench_fba.cs` (헤더 마커 + 제목)
  - `samples/fba/layer_dirty_strategy_bench.cs` (헤더 제목)
- 제안 API(예시):
  - `UiRect DrawLayerCard(..., string? headerText, ..., out bool hitClicked, ..., string? hitTestId = null)`

### P3 적용 결과(2026-02-20)
- 보강 API 파일
  - `src/Duxel.Core/UiImmediateContext.Widgets.Basic.cs` (`DrawLayerCard`)
- 샘플 전환
  - `samples/fba/layer_dirty_strategy_bench.cs`
  - `samples/fba/layer_widget_mix_bench_fba.cs`
  - `samples/fba/idle_layer_validation.cs`

### P4. 레이어 카드 인터랙션 헬퍼 (`DrawLayerCardInteractive`)
- 상태: **완료(1차)** — 카드 렌더 + 입력 상태(start/drag/end) 반환을 단일 API로 승격
- 반복 패턴:
  - 카드 hit-test 이후 `IsItemClicked/IsItemActive/IsItemDeactivated` 조합
  - 샘플별 수동 드래그 상태 분기
- 대표 위치:
  - `samples/fba/idle_layer_validation.cs` (레이어 드래그 시작/유지/종료)
- 제안 API(예시):
  - `UiRect DrawLayerCardInteractive(..., out UiLayerCardInteraction interaction, ... , string? hitTestId = null)`

### P4 적용 결과(2026-02-20)
- 보강 API 파일
  - `src/Duxel.Core/UiImmediateContext.Widgets.Basic.cs` (`DrawLayerCardInteractive`)
  - `src/Duxel.Core/UiTypes.cs` (`UiLayerCardInteraction`)
- 샘플 전환
  - `samples/fba/idle_layer_validation.cs`
  - `samples/fba/layer_dirty_strategy_bench.cs`
  - `samples/fba/layer_widget_mix_bench_fba.cs`

---

## 권장 적용 순서
1. **P0 2개 먼저**: `BeginCanvas/WithCanvas`, `DrawOverlayText`
2. **P1 2개**: `UiFpsCounter`, `DrawKeyValueRow`
3. 이후 벤치 설정 파서/레이어 카드 스켈레톤/상위 헬퍼 확장

## 기대 효과
- 샘플 코드 라인 수 감소(특히 벤치/데모 샘플)
- 정렬/클립/오버레이 버그의 라이브러리 단일 수정 가능
- 신규 샘플 작성 난이도 하락
