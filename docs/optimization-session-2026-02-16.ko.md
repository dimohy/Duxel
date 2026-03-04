# 최적화 세션 로그 (2026-02-16)

## 세션 개요

- 목표: GC 압력 완화, 재사용/풀링 강화, 렌더 핫패스 복잡도 감소, Vulkan 전송 경로 최적화
- 검증 기준: Release 빌드 + 벡터/레이어 혼합 A/B 벤치

## 개선표

| 항목 | 유형 | 대상 | 핵심 변경 | 검증 |
|---|---|---|---|---|
| `UiDrawCommand` 값형 전환 | GC | `src/Duxel.Core/UiTypes.cs` | `record class` → `readonly record struct` | Release 빌드, 벤치 통과 |
| `UiImmediateContext` 프레임 재초기화 재사용 | 풀링/재사용 | `src/Duxel.Core/UiImmediateContext.cs`, `src/Duxel.Core/UiContext.cs` | 프레임별 신규 생성 제거, `ReinitializeFrame` + transient state reset | Release 빌드, 샘플 런타임 통과 |
| `UiStateStorage` 재사용 | GC/재사용 | `src/Duxel.Core/UiTypes.cs`, `src/Duxel.Core/UiImmediateContext.cs` | `Clear()` 도입 후 프레임마다 재할당 제거 | Release 빌드 통과 |
| Draw buffer growth 정책 개선 | GC/메모리 | `src/Duxel.Core/UiTypes.cs` | `PooledBuffer<T>.Grow`를 최소 2배 성장으로 변경 | Release 빌드 통과 |
| 레이아웃 배열 용량 보존 | 메모리 재사용 | `src/Duxel.Core/UiImmediateContext.cs` | column/table 배열을 매 프레임 `Array.Empty`로 교체하지 않음 | Release 빌드 통과 |
| RecordCommandBuffer clip/scissor 분리 | 리팩토링/핫패스 | `src/Duxel.Vulkan/VulkanRendererBackend.cs` | scissor 계산을 `TryComputeScissorRect`로 추출 | Release 빌드, 벤치 통과 |
| Transfer queue 경로 분리 + 업로드 fence 지연대기 | 비동기/파이프라인 | `src/Duxel.Vulkan/VulkanRendererBackend.cs` | upload submit을 transfer queue로 분리, 즉시 대기 제거 후 submit 경계에서 동기화 | Release 빌드, 벤치 통과 |

## 벤치 결과 (Managed, 세션 말미 측정)

### Vector A/B (`./scripts/run-vector-clip-ab.ps1 -PhaseSeconds 0.8 -PrimitiveCounts "1000,2000" -Managed`)

| Primitives | Legacy FPS | Optimized FPS | 개선율 |
|---:|---:|---:|---:|
| 1000 | 1108.23 | 1078.67 | -2.67% |
| 2000 | 1879.96 | 1813.15 | -3.55% |
| 평균 | 1494.096 | 1445.912 | -3.225% |

### Layer/Widget A/B (`./scripts/run-layer-widget-clip-ab.ps1 -PhaseSeconds 0.5 -Managed`)

| Phase | Legacy FPS | Optimized FPS | 개선율 |
|---|---:|---:|---:|
| nocache-drawlist-100 | 784.38 | 745.73 | -4.93% |
| cache-drawlist-100 | 3927.27 | 3934.30 | +0.18% |
| cache-texture-100 | 4244.28 | 4109.15 | -3.18% |
| nocache-drawlist-170 | 959.83 | 917.11 | -4.45% |
| cache-drawlist-170 | 3149.30 | 3217.49 | +2.17% |
| cache-texture-170 | 3421.12 | 3471.72 | +1.48% |
| 평균 | 2747.699 | 2732.581 | -0.55% |

## 관찰

- 구조적/GC 최적화는 적용되었으나, 세션 말미 벤치에서는 평균 FPS가 소폭 하락한 구간이 존재함.
- 특히 transfer queue 관련 경로는 하드웨어/드라이버/큐 패밀리 토폴로지에 따라 편차가 큼.

## 재검증 (멈춤 복구 후)

### Vector A/B (`$env:DUXEL_VK_UPLOAD_BATCH='0'; ./scripts/run-vector-clip-ab.ps1 -PhaseSeconds 0.8 -PrimitiveCounts "1000,2000" -Managed`)

| Primitives | Legacy FPS | Optimized FPS | 개선율 |
|---:|---:|---:|---:|
| 1000 | 980.02 | 1028.21 | +4.92% |
| 2000 | 1742.96 | 1433.56 | -17.75% |
| 평균 | 1361.49 | 1230.885 | -9.593% |

- 본 재검증 구간에서는 `DeviceLost`가 재현되지 않았고 벤치가 정상 종료됨.
- `1000` 구간은 개선, `2000` 구간은 악화로 분산이 커서 추가 반복 측정이 필요함.

### Layer/Widget A/B (`$env:DUXEL_VK_UPLOAD_BATCH='0'; ./scripts/run-layer-widget-clip-ab.ps1 -PhaseSeconds 0.5 -Managed`)

| Phase | Legacy FPS | Optimized FPS | 개선율 |
|---|---:|---:|---:|
| nocache-drawlist-100 | 778.18 | 735.64 | -5.47% |
| cache-drawlist-100 | 4874.67 | 4610.90 | -5.41% |
| cache-texture-100 | 5137.12 | 5267.65 | +2.54% |
| nocache-drawlist-170 | 1031.68 | 1144.64 | +10.95% |
| cache-drawlist-170 | 3880.59 | 3771.76 | -2.81% |
| cache-texture-170 | 5069.59 | 3995.83 | -21.18% |
| 평균 | 3461.972 | 3254.404 | -5.996% |

- 본 재검증에서도 `DeviceLost`는 재현되지 않았음.
- 성능은 phase 편차가 커서, transfer/batching 경로의 조건부 적용(해상도/업로드량 기준) 검토가 필요함.

### 5회 반복 통계 (동일 조건, `DUXEL_VK_UPLOAD_BATCH=0`)

| 시나리오 | 회차별 개선율(%) | 평균(%) | 표준편차(%) | 최소(%) | 최대(%) |
|---|---|---:|---:|---:|---:|
| Vector A/B | -8.405, -13.420, -23.615, -18.640, -11.415 | -15.099 | 5.409 | -23.615 | -8.405 |
| Layer/Widget A/B | -1.447, -7.805, -1.615, -1.108, -0.663 | -2.528 | 2.659 | -7.805 | -0.663 |

- 반복 측정 기준에서도 평균적으로 optimized가 낮아 회귀가 재현됨.
- 편차는 Vector 쪽이 더 크며, 고부하 업로드 구간에서 동기화 비용이 크게 작용하는 징후가 있음.

## 후속 액션

- queue family ownership transfer(전용 transfer queue 사용 시) 경로를 명시적으로 보강
- upload batching(프레임당 다중 업로드를 단일 submit로 통합) 실험
- 동일 시나리오 5회 반복 평균/표준편차 기반으로 개선율 재판정

## 추가 안정화 기록 (프로세스 정리/행 방지)

| 변경 대상 | 변경 요약 | 검증 명령어 | Before | After | 개선율 | 리스크/후속 과제 |
|---|---|---|---|---|---:|---|
| `scripts/run-vector-clip-ab.ps1` | `run-fba`를 별도 `pwsh` 프로세스로 감시 실행하고 타임아웃 시 `taskkill /T /F`로 트리 강제 종료. `finally`에서 샘플 프로세스 정리를 항상 수행하도록 보강. | `$env:DUXEL_VK_UPLOAD_BATCH='0'; ./scripts/run-vector-clip-ab.ps1 -PhaseSeconds 0.2 -PrimitiveCounts "800" -Managed -RunFbaTimeoutSeconds 120` | 일부 실패/행 상황에서 수동 강제 종료 필요 | 벤치 완료 후 스크립트 즉시 종료, 강제 정리 경로 확보 | N/A(안정성 개선) | `taskkill` 의존(Windows 전용). 향후 공통 유틸화 필요 |
| `scripts/run-layer-widget-clip-ab.ps1` | 위와 동일한 타임아웃/강제정리 구조를 동일 적용. | `./scripts/run-layer-widget-clip-ab.ps1 -PhaseSeconds 0.2 -Managed -RunFbaTimeoutSeconds 120` | 일부 실패/행 상황에서 수동 정리 필요 | 강제 정리 경로 확보(실행 정책 동일화) | N/A(안정성 개선) | 짧은 스모크만 수행. 장시간 반복 측정 추가 필요 |

### 추가 재검증 (2026-02-16, hang 방지 패치 후)

- 명령어: `./scripts/run-vector-clip-ab.ps1 -PhaseSeconds 0.15 -PrimitiveCounts "800" -Managed -RunFbaTimeoutSeconds 90`
- 결과: 벤치 2회(legacy/optimized) 모두 정상 종료, 잔류 프로세스로 인한 터미널 행 미발생
- 성능: 평균 `legacy=432.093`, `optimized=431.512`, 개선율 `-0.134%`
- 해석: 현재 패치는 성능보다 안정성 확보 목적에 부합하며, 성능 회귀는 별도 튜닝 단계에서 다뤄야 함

## 샘플 실행 안정성 보강 (idle_layer_validation 오류 대응)

### 원인 요약

- `run-fba.ps1 -Managed` 실행에서 샘플/환경에 따라 프로세스가 분리되어 잔류하거나, 자동 종료 환경변수가 있어도 이벤트 대기 루프가 무한 블록되어 타임아웃으로 관측됨.

### 변경 내용

| 변경 대상 | 변경 요약 | 검증 명령어 | Before | After | 개선율 | 리스크/후속 과제 |
|---|---|---|---|---|---:|---|
| `run-fba.ps1` | `-ManagedTimeoutSeconds`, `-KillProcessTreeOnTimeout` 추가. Managed 실행 타임아웃/강제 종료 및 분리 샘플 프로세스 감시 종료 경로 보강. | `./run-fba.ps1 samples/fba/idle_layer_validation.cs -Managed -ManagedTimeoutSeconds 90 -KillProcessTreeOnTimeout` | 일부 실행에서 `dotnet`/샘플 잔류 및 터미널 행 | 타임아웃 기반 탈출/정리 경로 확보 | N/A(안정성 개선) | 권한 높은 프로세스는 외부 도구로 추가 정리 필요 가능 |
| `src/Duxel.App/DuxelApp.cs` | 공통 auto-exit(`DUXEL_SAMPLE_AUTO_EXIT_SECONDS`) 지원 + idle wait 시 auto-exit 모드에서 유한 대기(`16ms`)로 변경. | `DUXEL_SAMPLE_AUTO_EXIT_SECONDS=6`로 전 샘플 순회 실행 | 다수 샘플에서 auto-exit 미적용으로 타임아웃 | 전 샘플 auto-exit 동작, 무한대기 경로 제거 | N/A(안정성 개선) | auto-exit은 테스트/벤치 모드 전용 사용 권장 |

### 전수 실행 결과

- 1차 전수(17개): 5개 타임아웃(`image_widget_effects_fba`, `input_queries`, `listbox_scroll_test_fba`, `text_input_only`, `ui_mixed_stress`)
- 수정 후 재검증(5개): 전부 통과
- 최종 전수(17개): 전부 통과
- 최종 요약 파일: `artifacts/sample-run-check-summary-final.json`

## 추가 미세 최적화 (RecordCommandBuffer/Scissor 핫패스)

### 변경 내용

| 변경 대상 | 변경 요약 | 검증 명령어 | Before | After | 개선율 | 리스크/후속 과제 |
|---|---|---|---|---|---:|---|
| `src/Duxel.Vulkan/VulkanRendererBackend.cs` | `TryComputeScissorRect`에 정수 clip/기본 스케일(1x) fast-path 추가. `RecordCommandBuffer`의 per-command translation 보정 분기에서 `MathF.Abs` 기반 비교를 직접 `!= 0f` 비교로 경량화. | `dotnet build Duxel.slnx -c Release` | 핫패스 per-command 부동소수 연산/함수 호출 부담 존재 | 동일 기능으로 fast-path 우선 처리 및 분기 경량화 | N/A(구현 변경) | clip rect가 sub-pixel인 경우 기존 일반 경로로 폴백. 장시간 반복 측정으로 분산 확인 필요 |

### 재측정 결과

#### Layer/Widget A/B

- 명령어: `$env:DUXEL_VK_UPLOAD_BATCH='0'; ./scripts/run-layer-widget-clip-ab.ps1 -PhaseSeconds 0.8 -Managed -RunFbaTimeoutSeconds 180`
- 결과:
	- `nocache-drawlist-100`: `legacy=136.26`, `optimized=76.67`, `-43.73%`
	- `cache-drawlist-100`: `legacy=2261.78`, `optimized=2621.98`, `+15.93%`
	- `cache-texture-100`: `legacy=4149.59`, `optimized=5177.92`, `+24.78%`
	- 평균: `legacy=2182.545`, `optimized=2625.522`, `+20.296%`

#### Vector A/B

- 명령어: `$env:DUXEL_VK_UPLOAD_BATCH='0'; ./scripts/run-vector-clip-ab.ps1 -PhaseSeconds 0.8 -PrimitiveCounts "1000,2000" -Managed -RunFbaTimeoutSeconds 180`
- 결과:
	- `1000`: `legacy=1169.54`, `optimized=1153.01`, `-1.41%`
	- `2000`: `legacy=1964.57`, `optimized=2028.52`, `+3.25%`
	- 평균: `legacy=1567.057`, `optimized=1590.761`, `+1.513%`

### 해석

- Layer/Widget의 cache 경로(특히 texture backend)에서 개선폭이 크게 확인됨.
- nocache-drawlist 단일 phase는 여전히 음수 편차가 커 반복 측정(최소 5회)로 통계 재확인이 필요함.

## 공정 재검증 (콘솔 프로파일 출력 비활성)

사용자 지적에 따라 `DUXEL_VK_PROFILE`, `DUXEL_VK_PROFILE_EVERY`, `DUXEL_VK_FAIL_LOG`를 모두 해제한 상태에서, `Tee-Object` 없는 순수 벤치 실행 로그만 파일로 수집해 평균 라인을 파싱했다.

### 검증 명령/산출물

- 실행: `DUXEL_VK_UPLOAD_BATCH=0` 고정, Layer/Vector A/B 각 3회 반복
- 결과 파일: `artifacts/fair-benchmark-summary-no-profiler.json`

### 결과 요약

| 시나리오 | 회차별 개선율(%) | 평균(%) | 표준편차(%) | 최소(%) | 최대(%) |
|---|---|---:|---:|---:|---:|
| Layer/Widget A/B | -13.035, -0.637, +7.136 | -2.179 | 8.307 | -13.035 | +7.136 |
| Vector A/B | -22.847, +0.349, +7.956 | -4.847 | 13.101 | -22.847 | +7.956 |

### 해석

- 콘솔 프로파일 출력 오버헤드를 제거해도 분산이 매우 크며, 단순 로그 오버헤드만이 원인이라고 보기 어렵다.
- 현 시점에서는 평균 기준으로 여전히 optimized가 음수이며, 드라이버 상태/런 간 변동에 민감한 경로가 남아 있다.

## 추가 원인 분석 및 수정 (optimized-only scissor fast-path 제거)

### 원인 분석

- `optimized` 경로(`DUXEL_VK_LEGACY_CLIP_CLAMP=0`)에서만 정수 scissor fast-path가 동작하고, `legacy` 경로에는 해당 분기가 없었다.
- fast-path가 적중하지 않는 프레임/phase에서는 per-command 조건 검사 비용만 증가해 경로 비대칭이 생겼고, 적중률 변동이 큰 구간에서 런 간 편차를 키웠다.
- 즉, "optimized vs legacy" 비교에 순수 clamp 경로 외 비용(optimized 전용 분기 비용)이 섞여 측정이 흔들렸다.

### 조치

- `src/Duxel.Vulkan/VulkanRendererBackend.cs`의 optimized 전용 정수 scissor fast-path 블록 제거.
- 공통 scissor 계산 경로로 단순화하여 비교 경로의 비대칭을 축소.

### 공정 재측정 (출력 오버헤드 없음, 3회 반복)

- 조건: `DUXEL_VK_PROFILE*` 해제, `DUXEL_VK_FAIL_LOG` 해제, `DUXEL_VK_UPLOAD_BATCH=0`
- 결과 파일: `artifacts/fair-benchmark-summary-no-profiler-v2.json`

| 시나리오 | 평균(%) | 표준편차(%) | 최소(%) | 최대(%) |
|---|---:|---:|---:|---:|
| Layer/Widget A/B | -1.680 | 2.227 | -4.316 | +1.130 |
| Vector A/B | +3.053 | 2.483 | -0.438 | +5.123 |

### 비교 해석

- 이전 공정 측정(`fair-benchmark-summary-no-profiler.json`) 대비 표준편차가 크게 감소함.
	- Layer: `8.307% → 2.227%`
	- Vector: `13.101% → 2.483%`
- Vector는 평균이 음수에서 양수로 전환(`-4.847% → +3.053%`).
- Layer는 소폭 음수(`-1.680%`)가 남았지만 편차가 줄어, 기존의 큰 흔들림 원인(optimized 전용 분기)의 영향이 완화된 것으로 판단.

## 공격 리팩토링 시도/판정/복구

### 시도한 리팩토링

- 대상: `RecordCommandBuffer` 내부 command loop
- 내용: 연속 command의 clip 입력(`ClipRect + Translation`)이 동일하면 scissor 계산 결과를 재사용하도록 캐시 추가
- 의도: `TryComputeScissorRect` 호출 수를 줄여 per-command CPU 비용 절감

### 결과 판정

- 공정 3회 재측정 결과(`artifacts/fair-benchmark-summary-no-profiler-v3.json`):
	- Layer 평균 `-3.734%`, 표준편차 `7.942%`
	- Vector 평균 `+25.000%`, 표준편차 `21.355%`
- Vector 편차가 비정상적으로 커져(최대 `+48.86%`) 결과 신뢰도가 악화됨.
- 결론: 공격 리팩토링은 안정적 개선으로 볼 수 없어 채택하지 않음.

### 복구 조치

- clip 입력 캐시 리팩토링을 **원복**.
- 원복 후 단일 sanity 측정:
	- Layer A/B: `legacy=2360.524`, `optimized=2617.322`, `+10.879%`
	- Vector A/B: `legacy=1570.746`, `optimized=1579.459`, `+0.555%`

### 최종 상태

- 유지: optimized-only scissor fast-path 제거(분산 완화에 유의미)
- 복구: clip 입력 재사용 공격 리팩토링(불안정으로 폐기)

## 시각 정합성 수정 (레이어 clip 기준 통일)

### 원인 분석

- `BeginLayer`가 레이어 로컬 clip 기준을 `CurrentClipRect`가 아닌 `_clipRect`(루트 clip)로 계산하고 있었다.
- 이로 인해 부모 clip이 축소된 컨텍스트에서 레이어 내부 명령의 clip 기준이 동적 경로/정적 캐시 경로와 어긋날 수 있었고, 경계 누수 및 cache on/off 불일치가 발생할 여지가 있었다.

### 변경 내용

| 변경 대상 | 변경 요약 | 검증 명령어 | Before | After | 개선율 | 리스크/후속 과제 |
|---|---|---|---|---|---:|---|
| `src/Duxel.Core/UiImmediateContext.Layers.cs` | `BeginLayer`의 로컬 clip 계산 기준을 `CurrentClipRect`로 변경. 레이어 진입 시 로컬 clip을 `_clipStack`에 push하고 `EndLayer`에서 pop하여 레이어 내부 clip 스택과 빌더 clip을 동기화. | `dotnet build Duxel.slnx -c Release` | 부모 clip 무시 가능, 레이어 경계 누수 가능 | 부모 clip 기준 일관 적용 | N/A(정합성 개선) | 레이어 내부에서 수동 draw 시 clip 지정 누락 시 샘플/호출부 품질에 여전히 영향 가능 |

### 캡처 기반 검증

- A/B 캡처(OFF→ON):
	- 실행: `DUXEL_LAYER_BENCH_OUT=artifacts/idle-cache-ab-bench-after.json`, `DUXEL_LAYER_BENCH_PARTICLES=6500`, `DUXEL_LAYER_BENCH_LAYOUTS=baseline`, `DUXEL_LAYER_BENCH_PHASE_SECONDS=1.0`, `DUXEL_CAPTURE_OUT_DIR=artifacts/captures/idle_cache_ab_after`, `DUXEL_CAPTURE_FRAMES=20,95`, `./run-fba.ps1 samples/fba/idle_layer_validation.cs -NoCache`
	- phase 결과: `phase0(cache=false)`, `phase1(cache=true)` 정상 기록
- 픽셀 비교(OFF frame_0020 vs ON frame_0095):
	- 전체 화면: `diff 80,631 -> 71,507` (`4.8398% -> 4.2921%`)
	- 하단 레이어 영역(`y>=320`): `diff=0` (패치 전/후 동일하게 `0`)

### 해석

- cache on/off에서 관측된 차이는 레이어 본문 영역이 아니라 상단 동적 HUD(phase/수치 텍스트) 변화에 집중됨을 캡처로 확인했다.
- 레이어 영역(`y>=320`)은 픽셀 단위 동등성이 유지되어, 레이어 경계/본문 기준의 정합성은 현재 패치 기준으로 일치한다.
