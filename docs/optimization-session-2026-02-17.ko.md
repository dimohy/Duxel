# Optimization Session - 2026-02-17

## 1) Text glyph clip culling (`UiDrawListBuilder.AddText`)

- 변경 대상: `src/Duxel.Core/UiDrawListBuilder.cs`
- 변경 요약: glyph 사각형이 clip rect 밖이면 정점/인덱스 생성을 생략하고 advance만 진행.
- 검증 명령어:
  - `dotnet build Duxel.slnx -c Release`
  - `./run-fba.ps1 samples/fba/idle_layer_validation.cs -NoCache`
- 측정 비고: 이 단계 단독 Before/After 고정 측정은 미수행(다음 단계와 연속 적용됨).
- 리스크/후속 과제:
  - 긴 텍스트 + 다양한 클립 조건에서 시각 회귀 재검증 필요.
  - 텍스트 밀도 고정 시나리오 벤치 분리 권장.

## 2) Right-edge line fast-forward (`UiDrawListBuilder.AddText`)

- 변경 대상: `src/Duxel.Core/UiDrawListBuilder.cs`
- 변경 요약: pen X가 clip right를 넘은 경우 남은 라인을 개행까지 fast-forward.
- 검증 명령어:
  - `dotnet build Duxel.slnx -c Release`
  - `DUXEL_LAYER_BENCH_OUT=artifacts/text-culling-bench.json`
  - `DUXEL_LAYER_BENCH_OUT=artifacts/text-culling-bench-v2.json`
  - `./run-fba.ps1 samples/fba/idle_layer_validation.cs -NoCache`

### 개선표 (동일 벤치 조건, phase 0 / cache=false)

| 항목 | Before | After | 개선율 |
|---|---:|---:|---:|
| avgFps | 192.290 | 191.337 | -0.496% |
| avgCpu(%) | 7.362 | 7.243 | +1.616% (낮을수록 좋음) |

- 결과 해석:
  - FPS 기준 소폭 악화(노이즈 가능 범위).
  - CPU는 소폭 개선.
  - 단기 결론: 현 워크로드에서는 체감 이득이 제한적이며, 추가 최적화는 `TextV` 포맷 할당 축소 쪽이 우선.
- 리스크/후속 과제:
  - 다국어/서로게이트/개행 혼합 텍스트 회귀 테스트 필요.
  - 5회 이상 반복 측정 후 중앙값 비교로 노이즈 제거 필요.

## 3) `TextV` 계열 1/2 인자 오버로드 추가

- 변경 대상: `src/Duxel.Core/UiImmediateContext.Widgets.Basic.cs`
- 변경 요약:
  - `TextV`, `TextColoredV`, `TextDisabledV`, `TextWrappedV`, `LabelTextV`, `BulletTextV`에 1/2 인자 오버로드 추가.
  - `FormatInvariant` 1/2 인자 fast path 추가.
  - 목적: `params object[]` 배열 할당 빈도 축소.
- 검증 명령어:
  - `dotnet build Duxel.slnx -c Release`
  - `DUXEL_LAYER_BENCH_OUT=artifacts/text-culling-bench-v3.json`
  - `./run-fba.ps1 samples/fba/idle_layer_validation.cs -NoCache`

### 개선표 (v2 → v3, 동일 벤치 조건, phase 0 / cache=false)

| 항목 | Before(v2) | After(v3) | 개선율 |
|---|---:|---:|---:|
| avgFps | 191.337 | 192.027 | +0.361% |
| avgCpu(%) | 7.243 | 7.821 | -7.980% (악화) |

- 결과 해석:
  - FPS는 소폭 개선.
  - CPU는 악화(측정 노이즈 또는 워크로드 편차 가능성).
  - 단기 결론: 할당 축소 방향은 유지하되, 효과 확정에는 반복 측정이 필요.
- 리스크/후속 과제:
  - API 오버로드 추가로 호출 해석이 바뀌는 케이스가 없는지 회귀 확인 필요.
  - GC/EventPipe 기반 할당량 측정으로 실제 힙 절감 여부를 별도로 검증 필요.

## 4) `UiTextBuilder.BuildText` 오프스크린 glyph culling 동기화

- 변경 대상: `src/Duxel.Core/UiTextBuilder.cs`
- 변경 요약:
  - `UiDrawListBuilder.AddText`와 동일하게 glyph 단위 clip culling 추가.
  - 라인 시작점이 clip 하단을 넘으면 조기 종료.
  - BMP/Surrogate 경로 모두 동일 처리.
- 검증 명령어:
  - `dotnet build Duxel.slnx -c Release`
- Before/After 수치:
  - 이번 단계는 아직 단독 벤치 미실행.
- 리스크/후속 과제:
  - `BuildText` 호출 경로(직접 drawlist 구성하는 샘플)에서 텍스트 누락 회귀 점검 필요.

## 5) `UiFontAtlas.GetKerning` 소형 다중 슬롯 캐시 추가

- 변경 대상: `src/Duxel.Core/UiFontAtlas.cs`
- 변경 요약:
  - 기존 single last-hit 캐시에 더해 8-slot 순환 캐시 추가.
  - `Kerning` 딕셔너리 조회 전에 소형 캐시를 먼저 확인.
  - miss 결과(0f)도 캐시에 저장해 반복 miss 비용 완화.
- 검증 명령어:
  - `dotnet build Duxel.slnx -c Release`
  - `DUXEL_LAYER_BENCH_OUT=artifacts/text-culling-bench-v4.json`
  - `./run-fba.ps1 samples/fba/idle_layer_validation.cs -NoCache`
- Before/After 수치:
  - 기준: `v3` vs `v4` (phase 0 / cache=false)
  - avgFps: 192.027 → 195.242 (+1.674%)
- 리스크/후속 과제:
  - 폰트별 kerning 분포에 따라 체감 이득이 달라질 수 있어 벤치로 확인 필요.

## 6) `MeasureText` LRU 시도 및 롤백

- 변경 대상: `src/Duxel.Core/UiTextBuilder.cs`
- 변경 요약:
  - 접근 tick 기반 LRU 성향 캐시로 변경 시도.
  - 벤치에서 2회 연속 성능 하락 확인 후 즉시 롤백.
- 검증 명령어:
  - `dotnet build Duxel.slnx -c Release`
  - `DUXEL_LAYER_BENCH_OUT=artifacts/text-culling-bench-v5.json`
  - `DUXEL_LAYER_BENCH_OUT=artifacts/text-culling-bench-v5b.json`
  - `DUXEL_LAYER_BENCH_OUT=artifacts/text-culling-bench-v6-revert.json`
  - `./run-fba.ps1 samples/fba/idle_layer_validation.cs -NoCache`

### 개선표 (phase 0 / cache=false)

| 단계 | avgFps | 비교 기준 | 개선율 |
|---|---:|---:|---:|
| v4 (변경 전) | 195.242 | - | - |
| v5 (LRU 1차) | 183.902 | v4 대비 | -5.808% |
| v5b (LRU 2차) | 185.895 | v4 대비 | -4.787% |
| v6 (롤백 후) | 196.236 | v4 대비 | +0.509% |

- 결과 해석:
  - LRU 시도는 본 워크로드에서 일관되게 악화.
  - 롤백 후 성능이 v4 수준으로 회복되어 롤백 유지 결정이 타당.
- 리스크/후속 과제:
  - 측정 캐시는 단순화된 현재 정책 유지.
  - 다음 후보는 `MeasureText` 호출 빈도 자체를 줄이는 상위 레벨 캐시(위젯 레벨) 검토.

## 7) 위젯 레벨 프레임 캐시 시도 및 롤백

- 변경 대상:
  - `src/Duxel.Core/UiImmediateContext.cs`
  - `src/Duxel.Core/UiImmediateContext.Widgets.Basic.cs`
  - `src/Duxel.Core/UiImmediateContext.Layout.cs`
- 변경 요약:
  - 프레임 단위 텍스트 측정 캐시를 `UiImmediateContext`에 도입하고 `Basic/Layout` 호출부를 전환 시도.
  - 벤치에서 성능 악화 확인 후 즉시 롤백.
- 검증 명령어:
  - `dotnet build Duxel.slnx -c Release`
  - `DUXEL_LAYER_BENCH_OUT=artifacts/text-culling-bench-v7-framecache.json`
  - `DUXEL_LAYER_BENCH_OUT=artifacts/text-culling-bench-v8-revert-framecache.json`
  - `./run-fba.ps1 samples/fba/idle_layer_validation.cs -NoCache`

### 개선표 (phase 0 / cache=false)

| 단계 | avgFps | 비교 기준 | 개선율 |
|---|---:|---:|---:|
| v6 (시도 전) | 196.236 | - | - |
| v7 (프레임 캐시 적용) | 185.680 | v6 대비 | -5.379% |
| v8 (롤백 후) | 193.611 | v6 대비 | -1.338% |

- 결과 해석:
  - 프레임 캐시 시도는 오버헤드가 이득을 상회해 악화.
  - 롤백으로 급락은 해소되어, 검증된 기존 경로 유지가 타당.
- 리스크/후속 과제:
  - 단일 런 노이즈를 줄이기 위해 필요 시 3~5회 반복 중앙값 비교를 추가 수행.

## 8) 레이어 Dirty MVP (전역 Dirty-rect 1단계)

- 변경 대상: `samples/fba/idle_layer_validation.cs`
- 변경 요약:
  - 레이어 density 변경 시 `MarkAllLayersDirty` 대신 해당 레이어 body만 `MarkLayerDirty` 적용.
  - 무효화 범위를 줄여 캐시 재빌드 부담을 완화.
- 검증 명령어:
  - `dotnet build Duxel.slnx -c Release`
  - `DUXEL_LAYER_BENCH_OUT=artifacts/text-culling-bench-v9-layer-dirty-mvp.json`
  - `./run-fba.ps1 samples/fba/idle_layer_validation.cs -NoCache`

### 개선표 (phase 0 / cache=false)

| 항목 | Before(v8) | After(v9) | 개선율 |
|---|---:|---:|---:|
| avgFps | 193.611 | 195.265 | +0.854% |
| avgCpu(%) | 7.270 | 7.512 | -3.329% (악화) |

- 결과 해석:
  - FPS는 소폭 개선.
  - CPU는 단일 런 기준 악화되어 반복 측정으로 재확인 필요.
- 리스크/후속 과제:
  - density 조작 시나리오 중심 별도 벤치(변경 빈도 높은 구간)로 효과 확인 필요.

## 9) 전역 Dirty-rect 2단계 (수집 + 부분 합성 경로)

- 변경 대상:
  - `src/Duxel.Core/UiTypes.cs`
  - `src/Duxel.Core/UiContext.cs`
  - `src/Duxel.Vulkan/VulkanRendererBackend.cs`
- 변경 요약:
  - `UiDrawData`에 동적 dirty rect 메타데이터 추가.
  - `UiContext`에서 비정적 커맨드 clip union 기반 동적 영역 수집.
  - Vulkan layer compose 경로에서 `canReuse=false`일 때 동적 커버리지 scissor로 compose 범위 제한.
  - 동적 커맨드가 없는 경우(비재사용 프레임) overlay compose pass 스킵.

### 측정 결과 (phase 0 / cache=false)

| 단계 | avgFps | 비교 기준 | 개선율 |
|---|---:|---:|---:|
| v9 (1단계 기준) | 195.265 | - | - |
| v10 (2단계 초기) | 188.423 | v9 대비 | -3.504% |
| v11 (2단계 경량화) | 194.751 | v9 대비 | -0.263% |

- 결과 해석:
  - 초기 구현은 오버헤드가 커서 역효과.
  - 경량화 후 성능은 v9와 사실상 동급(노이즈 범위).
  - 즉, 2단계 인프라는 들어갔지만 “극적 FPS 상승”은 아직 미달.
- 리스크/후속 과제:
  - 실제 이득은 동적 영역이 매우 작은 장면에서만 커질 가능성.
  - 3~5회 반복 중앙값으로 재검증 필요.

## 10) 전용 샘플 재검증 (post-fix, dirty 전략 분리)

- 변경 대상: `samples/fba/layer_dirty_strategy_bench.cs`
- 변경 요약:
  - `BeginLayer` 내부에서 layer body를 바깥 drawlist가 아닌 활성 레이어 drawlist로 기록하도록 수정.
  - 수정 전에는 캐시 비교가 제대로 성립하지 않아 전략 간 차이가 축소되어 측정됨.
- 검증 명령어:
  - `dotnet build Duxel.slnx -c Release`
  - `$env:DUXEL_DIRTY_BENCH_OUT='artifacts/layer-dirty-strategy-bench-v2.json'`
  - `$env:DUXEL_DIRTY_BENCH_LAYERS='48'`
  - `$env:DUXEL_DIRTY_BENCH_DENSITY='2600'`
  - `$env:DUXEL_DIRTY_BENCH_PHASE_SECONDS='2'`
  - `./run-fba.ps1 samples/fba/layer_dirty_strategy_bench.cs -NoCache`

### 개선표 (동일 샘플, phase 0=all / phase 1=single)

| 항목 | all | single | 개선율(single vs all) |
|---|---:|---:|---:|
| avgFps | 21.037 | 110.736 | +426.398% |
| cacheBuildCount | 1968 | 218 | -88.923% |
| samples | 41 | 218 | +431.707% |

- 결과 해석:
  - post-fix 기준으로 `single` dirty 전략이 `all` 대비 매우 큰 성능 우위를 확인.
  - 레이어 단위 무효화 범위 축소가 캐시 재빌드 횟수를 직접적으로 줄이며 FPS를 크게 끌어올림.
  - “현재 전개 중인 최적화 방향(정밀 invalidation)”이 효과를 크게 보이는 전용 재현 샘플 확보 완료.
- 리스크/후속 과제:
  - 절대 수치는 장면 파라미터(`LAYERS`, `DENSITY`)에 민감하므로 기본값 문서화 필요.
  - backend=`texture` 조건에서도 동일 추세 확인 권장.

### 후속 검증 (backend=texture, 동일 파라미터)

| 항목 | all | single | 개선율(single vs all) |
|---|---:|---:|---:|
| avgFps | 21.037 | 162.131 | +670.686% |
| cacheBuildCount | 1968 | 316 | -83.943% |
| samples | 41 | 316 | +670.732% |

- 추가 해석:
  - `texture` backend에서도 동일 방향의 대폭 개선을 확인(오히려 격차 확대).
  - 전용 샘플 기준, 정밀 invalidation 전략의 유효성은 backend 독립적으로 재현됨.

## 11) 전역(비레이어) 정적 캐시 전략 도입 및 검증

- 변경 대상:
  - `src/Duxel.Core/UiDrawListBuilder.cs`
  - `src/Duxel.Core/UiContext.cs`
  - `src/Duxel.Vulkan/VulkanRendererBackend.cs`
  - `samples/fba/global_dirty_strategy_bench.cs` (신규)
- 변경 요약:
  - `UiDrawListBuilder`에 `PushCommandUserData/PopCommandUserData` 추가(전역 static tag 스코프).
  - `UiContext` 동적 dirty-rect 수집 시 `duxel.global.static:` prefix를 정적 커맨드로 제외.
  - Vulkan static drawlist 인식 로직이 `duxel.global.static:` prefix도 수용하도록 확장.
  - 레이어 API 없이 동작하는 전용 샘플 추가:
    - phase0: `all-dynamic` (무거운 배경을 매 프레임 재생성)
    - phase1: `global-static-cache` (배경 drawlist 1회 생성 후 전역 재사용)
- 검증 명령어:
  - `dotnet build Duxel.slnx -c Release`
  - `$env:DUXEL_GLOBAL_DIRTY_BENCH_OUT='artifacts/global-dirty-strategy-bench-v2.json'`
  - `$env:DUXEL_GLOBAL_DIRTY_BENCH_COLS='8'`
  - `$env:DUXEL_GLOBAL_DIRTY_BENCH_ROWS='6'`
  - `$env:DUXEL_GLOBAL_DIRTY_BENCH_DENSITY='9600'`
  - `$env:DUXEL_GLOBAL_DIRTY_BENCH_PHASE_SECONDS='2'`
  - `./run-fba.ps1 samples/fba/global_dirty_strategy_bench.cs -NoCache`

### 개선표 (전역 비레이어 샘플, v2)

| 항목 | all-dynamic | global-static-cache | 개선율(cache vs dynamic) |
|---|---:|---:|---:|
| avgFps | 796.894 | 3294.825 | +313.461% |
| samples | 1561 | 6438 | +312.428% |

- 결과 해석:
  - 사용자 요청대로 레이어 없이도 전역 캐시 전략에서 매우 큰 FPS 상승을 재현.
  - 정적 배경을 프레임마다 재생성하지 않고 drawlist 캐시를 재사용할 때 효과가 극대화됨.
- 리스크/후속 과제:
  - 현재 샘플은 CPU 생성 비용 절감 효과가 큰 구성이라 실제 앱에서는 위젯 구성별 편차가 발생 가능.
  - 실제 화면에도 동일 패턴(정적 배경/동적 오버레이 분리)을 점진 적용해 재측정 필요.

## 12) 실제 샘플 1차 이식: `idle_layer_validation` Fast Render 전역 static-cache

- 변경 대상: `samples/fba/idle_layer_validation.cs`
- 변경 요약:
  - Fast Render 창에서 정적 배경/파티클을 전역 drawlist 캐시로 1회 생성 후 재사용.
  - 시간 의존 요소(커서/트레일)만 동적으로 렌더링.
  - `Particle Count` 및 `Disable Fast Render` 변경 시 캐시 무효화.
- 검증 명령어:
  - `dotnet build Duxel.slnx -c Release`
  - `$env:DUXEL_LAYER_BENCH_OUT='artifacts/text-culling-bench-v12-global-fastcache-idle.json'`
  - `$env:DUXEL_LAYER_BENCH_PARTICLES='3000,9000,18000'`
  - `$env:DUXEL_LAYER_BENCH_PHASE_SECONDS='1'`
  - `$env:DUXEL_LAYER_BENCH_LAYOUTS='baseline'`
  - `./run-fba.ps1 samples/fba/idle_layer_validation.cs -NoCache`

### 측정 결과 (baseline / drawlist)

| phase | particles | cache | avgFps |
|---:|---:|---:|---:|
| 0 | 3000 | false | 189.450 |
| 1 | 3000 | true | 1124.029 |
| 2 | 9000 | false | 814.419 |
| 3 | 9000 | true | 906.620 |
| 4 | 18000 | false | 680.271 |
| 5 | 18000 | true | 792.591 |

- 결과 해석:
  - 실제 샘플에서도 전역 static-cache 이식 후 `cache=true` 구간이 전반적으로 우세.
  - 특히 3000 입자 구간에서 큰 격차를 확인해, 정적 경로 재사용 효과가 재현됨.
- 리스크/후속 과제:
  - phase별 편차가 있어 3~5회 반복 중앙값 비교가 필요.
  - 동일 이식 패턴을 Layer Lab 외 다른 정적 위젯 구간으로 확대해 효과 지속성 확인 필요.

## 13) 샘플 정리: 텍스트 정적 캐시 라벨 제거 + 레이어 정렬 경량화

- 변경 대상: `samples/fba/idle_layer_validation.cs`
- 변경 요약:
  - `Static Header Text Cache` 라벨/기능 및 관련 env 파서 제거.
  - 레이어 정렬을 매 프레임 수행하지 않고, Z 변경 시(`클릭`, `리셋`)에만 수행하도록 최적화.
- 검증 명령어:
  - `dotnet build Duxel.slnx -c Release`
  - `$env:DUXEL_LAYER_BENCH_OUT='artifacts/text-culling-bench-v13-no-static-label.json'`
  - `$env:DUXEL_LAYER_BENCH_PARTICLES='3000,9000,18000'`
  - `$env:DUXEL_LAYER_BENCH_PHASE_SECONDS='1'`
  - `$env:DUXEL_LAYER_BENCH_LAYOUTS='baseline'`
  - `./run-fba.ps1 samples/fba/idle_layer_validation.cs -NoCache`

### 비교 (v12 → v13)

| phase | particles | cache | avgFps(v12) | avgFps(v13) | 개선율 |
|---:|---:|---:|---:|---:|---:|
| 0 | 3000 | false | 189.450 | 183.106 | -3.348% |
| 1 | 3000 | true | 1124.029 | 1158.368 | +3.055% |
| 2 | 9000 | false | 814.419 | 835.679 | +2.611% |
| 3 | 9000 | true | 906.620 | 919.329 | +1.402% |
| 4 | 18000 | false | 680.271 | 674.859 | -0.795% |
| 5 | 18000 | true | 792.591 | 798.280 | +0.718% |

- 결과 해석:
  - 라벨 제거로 UX를 단순화했고, 정렬 경량화는 대부분 phase에서 소폭 개선으로 관측.
  - 일부 phase 하락은 단일 런 노이즈 범위로 판단되며 반복 측정으로 확정 필요.

## 14) 레이어 재진입 표시 보장 패치 (완전 클립 프레임 처리)

- 변경 대상: `src/Duxel.Core/UiImmediateContext.Layers.cs`
- 변경 요약:
  - static layer 캐시 확정 시점에 `HasRenderableLayerContent` 검사 추가.
  - 프레임이 완전 클립되어 실제 렌더 가능한 커맨드가 없으면 캐시를 확정하지 않고 `Dirty=true` 유지.
  - 의도: 화면 밖 클립 상태에서 다시 보일 때 최소 1회는 정상 빌드/표시 보장.
- 검증 명령어:
  - `dotnet build Duxel.slnx -c Release`
- 결과 해석:
  - 캐시가 빈 상태로 고정되는 케이스를 방지해 재진입 표시 안정성을 강화.

## 15) `Duxel_perf_test_fba.cs` 반복 성능 테스트 (5회)

- 결론 요약:
  - 본 샘플은 코어 렌더 경로 최적화(공통 draw/render 경로)의 혜택 대상.
  - 단, 레이어 전용 최적화의 직접 효과는 제한적(샘플 특성상 레이어 사용 없음).
- 검증 명령어:
  - `$env:DUXEL_PERF_BENCH_SECONDS='2.0'`
  - `$env:DUXEL_PERF_INITIAL_POLYGONS='2200'`
  - `./run-fba.ps1 samples/fba/Duxel_perf_test_fba.cs -Managed -ManagedTimeoutSeconds 180 -KillProcessTreeOnTimeout`
  - 산출물: `artifacts/duxel-perf-test-managed-run1.json` ~ `...run5.json`

### 측정 결과 (avgFps)

| run | avgFps | samples |
|---|---:|---:|
| run1 | 466.940 | 850 |
| run2 | 448.410 | 818 |
| run3 | 481.776 | 876 |
| run4 | 453.900 | 827 |
| run5 | 469.980 | 860 |

### 집계 통계

| 항목 | 값 |
|---|---:|
| 평균 | 464.199 |
| 최소 | 448.410 |
| 최대 | 481.776 |
| 표준편차 | 11.877 |

- 결과 해석:
  - 현재 기준선은 `avgFps ≈ 464.2` (2200 polygons, 2s, managed).
  - 이후 최적화 검증은 동일 샘플/동일 조건으로 반복해 변화 추세를 비교 가능.

## 16) `Duxel_perf_test_fba.cs` 자동 A/B 반복 스크립트 추가

- 변경 대상: `scripts/run-duxel-perf-ab.ps1` (신규)
- 변경 요약:
  - perf 샘플 전용 반복 측정 자동화 스크립트 추가.
  - baseline/candidate 각각 N회 실행 후 per-run + 집계(avg/min/max/std) + 개선율(%) 출력.
  - 결과 아티팩트(`artifacts/duxel-perf-<label>-runN.json`)와 통합 요약(`artifacts/duxel-perf-ab-summary.json`) 자동 생성.
- 검증 명령어:
  - `dotnet build Duxel.slnx -c Release`
  - `./scripts/run-duxel-perf-ab.ps1 -Runs 3 -BaselineName current -CandidateName fxaa-on -CandidateFxaa`

### 개선표 (current vs fxaa-on, 3회)

| 항목 | current | fxaa-on | 개선율(candidate vs baseline) |
|---|---:|---:|---:|
| avgFps | 469.630 | 466.563 | -0.653% |
| stdFps | 5.330 | 14.837 | -178.368% (변동성 악화) |

- 리스크/후속 과제:
  - 이번 비교는 기능 설정(FXAA on/off) 차이에 따른 측정이며, 코드 최적화 A/B와는 구분 필요.
  - 코드 변경 A/B 시에는 동일 렌더 설정에서 baseline/candidate 라벨만 분리해 반복 측정 권장.

## 17) `Duxel_perf_test_fba.cs` 전역 캐시 토글 추가 + on/off A/B(5회)

- 변경 대상:
  - `samples/fba/Duxel_perf_test_fba.cs`
  - `scripts/run-duxel-perf-ab.ps1`
- 변경 요약:
  - perf 샘플에 `DUXEL_PERF_GLOBAL_STATIC_CACHE` 기반 전역 캐시 토글 추가.
  - `DrawScene`에 정적 배경 경로를 추가하고, 캐시 ON일 때는 `duxel.global.static:*` 태그로 빌드한 drawlist 재사용.
  - A/B 스크립트에 `-BaselineGlobalStaticCache` / `-CandidateGlobalStaticCache` 옵션을 추가해 동일 조건 on/off 반복 비교 지원.
- 검증 명령어:
  - `dotnet build Duxel.slnx -c Release`
  - `./scripts/run-duxel-perf-ab.ps1 -Runs 5 -BaselineName global-off -CandidateName global-on -CandidateGlobalStaticCache`

### 개선표 (global-off vs global-on, 5회)

| 항목 | global-off | global-on | 개선율(on vs off) |
|---|---:|---:|---:|
| avgFps | 453.010 | 460.352 | +1.620% |
| minFps | 436.230 | 456.380 | +4.619% |
| maxFps | 465.890 | 462.990 | -0.622% |
| stdFps | 11.073 | 2.815 | +74.578% (낮을수록 좋음) |

- 결과 해석:
  - 전역 캐시 ON에서 평균 FPS가 소폭 상승(+1.62%).
  - 변동성(stdFps)은 크게 개선되어 프레임 안정성이 높아짐.
  - 단일 최대값은 off가 근소 우세이나, 전체 추세는 on이 더 안정적.

## 18) 공통 옵션 추가: 전역 정적 캐시 기본 ON + 전용 비교 샘플 시각화

- 변경 대상:
  - `src/Duxel.App/DuxelApp.cs`
  - `src/Duxel.Core/UiContext.cs`
  - `src/Duxel.Vulkan/VulkanRendererBackend.cs`
  - `samples/fba/global_dirty_strategy_bench.cs`
- 변경 요약:
  - 공통 렌더러 옵션 `EnableGlobalStaticGeometryCache` 추가(기본값 `true`).
  - `Duxel.App`에서 해당 옵션을 `UiContext` 및 Vulkan 렌더러 옵션으로 전달.
  - Core/Vulkan의 `duxel.global.static:` 태그 인식이 옵션값에 따라 동작하도록 분기.
  - 전용 비교 샘플(`global_dirty_strategy_bench`)에 수동 ON/OFF 토글, 현재 FPS, phase 평균 FPS, 개선율(%) 표시 추가.

- 검증 명령어:
  - `dotnet build Duxel.slnx -c Release`
  - 시각 비교 실행: `./run-fba.ps1 samples/fba/global_dirty_strategy_bench.cs -NoCache`

### 성능 비교(전용 샘플 기준, 기존 동일 파라미터 산출물)

| 항목 | all-dynamic | global-static-cache | 개선율(cache vs dynamic) |
|---|---:|---:|---:|
| avgFps | 796.894 | 3294.825 | +313.461% |
| samples | 1561 | 6438 | +312.428% |

- 참조 산출물: `artifacts/global-dirty-strategy-bench-v2.json`
- 결과 해석:
  - 전용 샘플에서 전역 캐시 ON/OFF 성능 차이는 여전히 큰 폭으로 재현됨.
  - 이번 변경으로 샘플 UI에서도 FPS/개선율이 즉시 표시되어 시각적 비교가 가능.
