# Optimization Session - 2026-02-19

## 1) Direct Text Runtime Toggle + A/B

- 변경 대상: `src/Duxel.Core/UiImmediateContext.cs`, `src/Duxel.Core/UiImmediateContext.Widgets.Basic.cs`, `scripts/run-duxel-perf-ab.ps1`
- 변경 요약:
  - 런타임 Direct 텍스트 토글 API 추가: `SetDirectTextEnabled(bool)`, `GetDirectTextEnabled()`
  - 환경변수 기본값 추가: `DUXEL_DIRECT_TEXT` (`0|false|off|no`면 비활성)
  - Direct 캐시 조회/렌더 경로에서 토글 상태 반영
  - A/B 스크립트에 Direct 토글 파라미터 추가:
    - `-BaselineDirectText`, `-CandidateDirectText`

### 검증 명령어

```powershell
dotnet build Duxel.slnx -c Release
./scripts/run-duxel-perf-ab.ps1 -Runs 3 -BenchSeconds 2.0 -InitialPolygons 2200 -BaselineName direct-off-only -CandidateName direct-on-only -BaselineProfile display -CandidateProfile display -BaselineDirectText:$false -CandidateDirectText
```

### Before / After

| 항목    | Before (Direct OFF) | After (Direct ON) |      개선율 |
| ------- | ------------------: | ----------------: | ----------: |
| Avg FPS |              375.24 |            397.27 |      +5.87% |
| Std FPS |               15.48 |              9.88 | 변동성 감소 |
| Runs    |                   3 |                 3 |           - |

- 원시 결과 요약 파일: `artifacts/duxel-perf-ab-summary.json`

### 리스크 / 후속 과제

- 리스크:
  - 2초 측정 윈도우는 분산 민감도가 높아 장기 평균과 차이 가능
  - 샘플/장면별 텍스트 비중이 다르면 개선율 편차가 큼
- 후속 과제:
  - 동일 조건으로 `Runs=5~10`, `BenchSeconds=6~10` 재측정
  - 계산기/텍스트 비중 높은 샘플 별도 A/B 스크립트 추가
