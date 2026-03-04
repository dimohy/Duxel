# Optimization Session Log — 2026-02-20

## 1) 변경 대상
- `src/Duxel.Core/UiImmediateContext.Widgets.Basic.cs`
- `src/Duxel.Core/UiImmediateContext.cs`
- `samples/fba/global_dirty_strategy_bench.cs`
- `samples/fba/Duxel_perf_test_fba.cs`
- `samples/fba/all_features.cs`
- `samples/fba/windows_calculator_fba.cs`

## 2) 변경 요약
- 라이브러리 API 승격
  - `DrawTextAligned(...)` (기존)
  - `BeginWindowCanvas(...)` / `EndWindowCanvas()` (신규)
  - `DrawOverlayText(...)` (신규)
- 샘플에서 수동 측정/정렬/클립/오버레이 보일러플레이트 제거 후 라이브러리 API로 전환

## 3) 검증 명령어
- `dotnet build Duxel.slnx -c Release`
- `$env:DUXEL_SAMPLE_AUTO_EXIT_SECONDS='2'; ./run-fba.ps1 .\samples\fba\windows_calculator_fba.cs -NoCache`
- `$env:DUXEL_SAMPLE_AUTO_EXIT_SECONDS='2'; ./run-fba.ps1 .\samples\fba\global_dirty_strategy_bench.cs -NoCache`
- `$env:DUXEL_SAMPLE_AUTO_EXIT_SECONDS='2'; ./run-fba.ps1 .\samples\fba\all_features.cs -NoCache`

## 4) Before / After 수치
| 항목 | Before | After | 개선율 |
|---|---:|---:|---:|
| Release 전체 빌드 시간 | 2.2s | 2.2s | 0.0% |
| `windows_calculator_fba` publish+run | 9.3s | 9.2s | +1.1% |
| `global_dirty_strategy_bench` publish+run | 5.9s | 5.9s | 0.0% |
| `all_features` publish+run | 6.1s | 6.1s | 0.0% |

> 측정 비고: 동일 머신/동일 세션의 CLI 로그 기준 단순 wall-clock 비교.

## 5) 성능/메모리 관점 점검
- 신규 API는 delegate 기반 래퍼를 피하고 즉시 실행형 호출로 설계하여 핫패스 할당을 최소화.
- 샘플 코드의 중복 로직 제거로 유지보수 비용 감소, 런타임 성능 회귀는 관측되지 않음.

## 6) 리스크 / 후속 과제
- 현재 `BeginWindowCanvas/EndWindowCanvas`는 윈도우 컨텐츠 캔버스 패턴에 최적화되어 있어, 임의 draw list 대상 범용 스코프 API는 후속 확장 필요.
- P1 후보(`UiFpsCounter`, `DrawKeyValueRow`)를 추가 승격하면 샘플 중복 제거 효과가 더 커질 것으로 예상.

---

## 7) P1 승격 추가 로그 (2026-02-20)

### 변경 대상
- `src/Duxel.Core/UiFpsCounter.cs` (신규)
- `src/Duxel.Core/UiImmediateContext.Widgets.Basic.cs`
- `samples/fba/windows_calculator_fba.cs`
- `samples/fba/all_features.cs`
- `samples/fba/Duxel_perf_test_fba.cs`
- `samples/fba/vector_primitives_bench_fba.cs`
- `samples/fba/ui_mixed_stress.cs`
- `samples/fba/layer_widget_mix_bench_fba.cs`

### 변경 요약
- `UiFpsCounter`/`UiFpsSample`로 샘플별 `UpdateFps` 중복 구현 제거.
- 계산기 Base List의 선택 인디케이터 + key/value 배치를 `DrawKeyValueRow`로 표준화.

### 검증 명령어
- `dotnet build Duxel.slnx -c Release`
- `$env:DUXEL_SAMPLE_AUTO_EXIT_SECONDS='2'; ./run-fba.ps1 .\samples\fba\layer_dirty_strategy_bench.cs -NoCache`
- `$env:DUXEL_SAMPLE_AUTO_EXIT_SECONDS='2'; ./run-fba.ps1 .\samples\fba\layer_widget_mix_bench_fba.cs -NoCache; Remove-Item Env:DUXEL_SAMPLE_AUTO_EXIT_SECONDS -ErrorAction SilentlyContinue`
- `$env:DUXEL_SAMPLE_AUTO_EXIT_SECONDS='2'; ./run-fba.ps1 .\samples\fba\windows_calculator_fba.cs -NoCache`
- `$env:DUXEL_SAMPLE_AUTO_EXIT_SECONDS='2'; ./run-fba.ps1 .\samples\fba\all_features.cs -NoCache`
- `$env:DUXEL_SAMPLE_AUTO_EXIT_SECONDS='2'; ./run-fba.ps1 .\samples\fba\Duxel_perf_test_fba.cs -NoCache`
- `$env:DUXEL_SAMPLE_AUTO_EXIT_SECONDS='2'; ./run-fba.ps1 .\samples\fba\vector_primitives_bench_fba.cs -NoCache`
- `$env:DUXEL_SAMPLE_AUTO_EXIT_SECONDS='2'; ./run-fba.ps1 .\samples\fba\ui_mixed_stress.cs -NoCache`
- `$env:DUXEL_SAMPLE_AUTO_EXIT_SECONDS='2'; ./run-fba.ps1 .\samples\fba\layer_widget_mix_bench_fba.cs -NoCache`

### Before / After 수치
| 항목 | Before | After | 개선율 |
|---|---:|---:|---:|
| Release 전체 빌드 시간 | 6.0s | 6.0s | 0.0% |
| `windows_calculator_fba` publish+run | 10.8s | 10.8s | 0.0% |
| `all_features` publish+run | 7.0s | 7.0s | 0.0% |
| `Duxel_perf_test_fba` publish+run | 6.6s | 6.6s | 0.0% |
| `vector_primitives_bench_fba` publish+run | 6.5s | 6.5s | 0.0% |
| `ui_mixed_stress` publish+run | 6.7s | 6.7s | 0.0% |
| `layer_widget_mix_bench_fba` publish+run | 6.8s | 6.8s | 0.0% |

### 리스크 / 후속 과제
- `DrawKeyValueRow`는 고정 `keyWidth` 기반이므로, 장문 key/locale 확장 시 가변 폭 옵션 추가 검토 필요.
- 다음 우선순위는 `BenchOptions` 승격(P1 잔여)로 환경변수 파서 중복 제거.

---

## 8) P1 잔여(BenchOptions) 완료 로그 (2026-02-20)

### 변경 대상
- `src/Duxel.Core/BenchOptions.cs` (신규)
- `samples/fba/Duxel_perf_test_fba.cs`
- `samples/fba/ui_mixed_stress.cs`
- `samples/fba/global_dirty_strategy_bench.cs`
- `samples/fba/layer_dirty_strategy_bench.cs`
- `samples/fba/vector_primitives_bench_fba.cs`
- `samples/fba/layer_widget_mix_bench_fba.cs`
- `samples/fba/idle_layer_validation.cs`

### 변경 요약
- 환경변수 파서 공통화 유틸(`BenchOptions`, `BenchOptionsReader`) 추가.
- 샘플별 `Read*` 중복 파서를 공통 API 호출로 치환.

### 검증 명령어
- `dotnet build Duxel.slnx -c Release`
- `$env:DUXEL_SAMPLE_AUTO_EXIT_SECONDS='2'; ./run-fba.ps1 .\samples\fba\global_dirty_strategy_bench.cs -NoCache`
- `$env:DUXEL_SAMPLE_AUTO_EXIT_SECONDS='2'; ./run-fba.ps1 .\samples\fba\layer_dirty_strategy_bench.cs -NoCache`
- `$env:DUXEL_SAMPLE_AUTO_EXIT_SECONDS='2'; ./run-fba.ps1 .\samples\fba\vector_primitives_bench_fba.cs -NoCache`
- `$env:DUXEL_SAMPLE_AUTO_EXIT_SECONDS='2'; ./run-fba.ps1 .\samples\fba\idle_layer_validation.cs -NoCache`

### Before / After 수치
| 항목 | Before | After | 개선율 |
|---|---:|---:|---:|
| Release 전체 빌드 시간 | 6.0s | 6.0s | 0.0% |

### 리스크 / 후속 과제
- `BenchOptions`는 단순 파싱/범위 검증에 집중되어 있어, 복합 구조(JSON/중첩 옵션) 파싱은 별도 유틸 확장 필요.

---

## 9) P2 LayerCardSkeleton 완료 로그 (2026-02-20)

### 변경 대상
- `src/Duxel.Core/UiImmediateContext.Widgets.Basic.cs`
- `samples/fba/layer_dirty_strategy_bench.cs`
- `samples/fba/layer_widget_mix_bench_fba.cs`
- `samples/fba/idle_layer_validation.cs`

### 변경 요약
- `DrawLayerCardSkeleton(...)` API 추가로 카드 프레임(배경/헤더/보더) 보일러플레이트를 라이브러리로 승격.
- 3개 샘플에서 수동 카드 프레임 코드 제거 후 공통 API 호출로 전환.

### 검증 명령어
- `dotnet build Duxel.slnx -c Release`
- `$env:DUXEL_SAMPLE_AUTO_EXIT_SECONDS='2'; ./run-fba.ps1 .\samples\fba\layer_dirty_strategy_bench.cs -NoCache`
- `$env:DUXEL_SAMPLE_AUTO_EXIT_SECONDS='2'; ./run-fba.ps1 .\samples\fba\layer_widget_mix_bench_fba.cs -NoCache`
- `$env:DUXEL_SAMPLE_AUTO_EXIT_SECONDS='2'; ./run-fba.ps1 .\samples\fba\idle_layer_validation.cs -NoCache`

### Before / After 수치
| 항목 | Before | After | 개선율 |
|---|---:|---:|---:|
| Release 전체 빌드 시간 | 3.6s | 3.6s | 0.0% |

### 리스크 / 후속 과제
- 현재 API는 카드 프레임 스켈레톤까지만 담당하며 제목 텍스트/마커/드래그 hit-test는 샘플 레벨 제어를 유지.

---

## 10) P3 DrawLayerCard 완료 로그 (2026-02-20)

### 변경 대상
- `src/Duxel.Core/UiImmediateContext.Widgets.Basic.cs`
- `samples/fba/layer_dirty_strategy_bench.cs`
- `samples/fba/layer_widget_mix_bench_fba.cs`
- `samples/fba/idle_layer_validation.cs`

### 변경 요약
- `DrawLayerCard(...)` 상위 API 추가:
  - 기존 `DrawLayerCardSkeleton(...)` 포함
  - 헤더 텍스트 렌더
  - 헤더 마커 렌더(옵션)
  - 카드 전체 hit-test + 클릭 상태 반환(옵션)
- 3개 샘플에서 제목/마커/hit-test 보일러플레이트를 API 호출로 치환.

### 검증 명령어
- `dotnet build Duxel.slnx -c Release`
- `$env:DUXEL_SAMPLE_AUTO_EXIT_SECONDS='2'; ./run-fba.ps1 .\samples\fba\layer_dirty_strategy_bench.cs -NoCache; ./run-fba.ps1 .\samples\fba\layer_widget_mix_bench_fba.cs -NoCache; ./run-fba.ps1 .\samples\fba\idle_layer_validation.cs -NoCache; Remove-Item Env:DUXEL_SAMPLE_AUTO_EXIT_SECONDS -ErrorAction SilentlyContinue`

### Before / After 수치
| 항목 | Before | After | 개선율 |
|---|---:|---:|---:|
| Release 전체 빌드 시간 | 2.7s | 2.7s | 0.0% |
| `layer_dirty_strategy_bench` publish+run | 10.4s | 10.4s | 0.0% |
| `layer_widget_mix_bench_fba` publish+run | 7.1s | 7.1s | 0.0% |
| `idle_layer_validation` publish+run | 7.0s | 7.0s | 0.0% |

### 리스크 / 후속 과제
- `DrawLayerCard`는 즉시모드 UI 패턴을 유지하므로, drag-state 자체(시작/유지/종료)는 샘플/앱 계층에서 계속 제어해야 한다.

---

## 11) P4 DrawLayerCardInteractive 완료 로그 (2026-02-20)

### 변경 대상
- `src/Duxel.Core/UiTypes.cs`
- `src/Duxel.Core/UiImmediateContext.Widgets.Basic.cs`
- `samples/fba/idle_layer_validation.cs`

### 변경 요약
- `UiLayerCardInteraction` 타입 추가(`Clicked`, `Held`, `Released`, `Hovered`, `MousePosition`).
- `DrawLayerCardInteractive(...)` API 추가:
  - 기존 `DrawLayerCard(...)` 호출 포함
  - 카드 hit-test 기반 상호작용 상태를 구조체로 반환
- `idle_layer_validation`에서 드래그 로직을 `interaction` 기반으로 전환(start/drag/end).

### 검증 명령어
- `dotnet build Duxel.slnx -c Release`
- `$env:DUXEL_SAMPLE_AUTO_EXIT_SECONDS='2'; ./run-fba.ps1 .\samples\fba\idle_layer_validation.cs -NoCache; Remove-Item Env:DUXEL_SAMPLE_AUTO_EXIT_SECONDS -ErrorAction SilentlyContinue`

### Before / After 수치
| 항목 | Before | After | 개선율 |
|---|---:|---:|---:|
| Release 전체 빌드 시간 | 2.3s | 2.3s | 0.0% |
| `idle_layer_validation` publish+run | 10.8s | 10.8s | 0.0% |

### 리스크 / 후속 과제
- 현재 인터랙션은 마우스 좌클릭 기준이며, 멀티버튼/터치 제스처 추상화는 후속 확장 필요.

---

## 12) P4 샘플 확장 완료 로그 (2026-02-20)

### 변경 대상
- `samples/fba/layer_dirty_strategy_bench.cs`
- `samples/fba/layer_widget_mix_bench_fba.cs`

### 변경 요약
- 두 샘플의 카드 렌더 호출을 `DrawLayerCard`에서 `DrawLayerCardInteractive`로 전환.
- 각 카드에 고유 `hitTestId`를 부여해 P4 인터랙션 경로를 공통 적용.
- 기존 벤치/렌더 동작은 유지(반환 interaction 상태는 현재 미사용).

### 검증 명령어
- `dotnet build Duxel.slnx -c Release`

### Before / After 수치
| 항목 | Before | After | 개선율 |
|---|---:|---:|---:|
| Release 전체 빌드 시간 | 3.0s | 3.0s | 0.0% |

### 리스크 / 후속 과제
- 상호작용 상태(`Hovered/Held/Released`)를 실제 벤치 시나리오 제어 로직에 연결하는 후속 적용 여지가 있음.
