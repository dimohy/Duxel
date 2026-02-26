# FBA clip clamp A/B 벤치 가이드

> 마지막 동기화: 2026-02-26

이 문서는 `DUXEL_VK_LEGACY_CLIP_CLAMP` 토글 기반으로 legacy/optimized 경로를 같은 샘플에서 비교하는 방법을 정리합니다.

## 대상 스크립트

- `scripts/run-layer-widget-clip-ab.ps1`
  - 샘플: `samples/fba/layer_widget_mix_bench_fba.cs`
  - 목적: 레이어 캐시 + 위젯 + primitive 혼합 부하에서 A/B 비교
- `scripts/run-vector-clip-ab.ps1`
  - 샘플: `samples/fba/vector_primitives_bench_fba.cs`
  - 목적: 벡터 primitive(라인/사각형/원) 전용 부하에서 A/B 비교

## 공통 동작

- legacy 실행 시 `DUXEL_VK_LEGACY_CLIP_CLAMP=1`
- optimized 실행 시 `DUXEL_VK_LEGACY_CLIP_CLAMP=0`
- 각 실행 결과를 JSON으로 저장한 뒤, phase별 FPS와 평균 개선율(%)를 출력

## 실행 예시

### 1) Layer + Widget 혼합 A/B

```powershell
./scripts/run-layer-widget-clip-ab.ps1 -PhaseSeconds 1.0 -Managed
```

선택 옵션:
- `-SamplePath` : 다른 샘플 파일 지정
- `-LegacyOut`, `-OptimizedOut` : 결과 JSON 경로 지정
- `-PhaseSeconds` : phase 측정 시간(초)
- `-Managed` : NativeAOT 대신 managed 실행

### 2) Vector A/B

```powershell
./scripts/run-vector-clip-ab.ps1 -PhaseSeconds 1.0 -PrimitiveCounts "6000,12000,24000" -Managed
```

선택 옵션:
- `-SamplePath` : 다른 샘플 파일 지정
- `-LegacyOut`, `-OptimizedOut` : 결과 JSON 경로 지정
- `-PhaseSeconds` : phase 측정 시간(초)
- `-PrimitiveCounts` : 비교할 primitive 개수 목록(쉼표 구분)
- `-Managed` : NativeAOT 대신 managed 실행

## 결과 해석

- phase별 `pct`가 양수면 optimized가 legacy보다 빠름
- 평균 `%`는 전체 phase 평균 FPS 기준 개선율
- 레이어 혼합 벤치는 캐시 히트/미스 패턴 영향이 크므로 phase별 편차를 함께 확인
- 벡터 벤치는 primitive 개수 증가 구간에서 개선 추세를 우선 확인

## 참고

- 벤치 실행 정책/배경은 [docs/fba-reference-guide.md](docs/fba-reference-guide.md)를 참고하세요.
