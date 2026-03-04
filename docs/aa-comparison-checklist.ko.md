# AA 비교 체크리스트 (MSAA 4x vs FXAA)

> 마지막 동기화: 2026-02-26

이 문서는 Duxel 성능 샘플에서 **MSAA 4x**와 **FXAA**를 같은 조건에서 비교하기 위한 점수표 템플릿입니다.

## 1) 테스트 조건 고정

- 해상도:
- 창 크기:
- VSync: On / Off
- 폴리곤 수:
- 샘플 파일: `samples/fba/Duxel_perf_test_fba.cs`
- 테스트 날짜:
- 테스트 장비(GPU/드라이버):

### 실행 명령

- MSAA 4x 기준
  - `./run-fba.ps1 ./samples/fba/Duxel_perf_test_fba.cs`
- FXAA ON
  - `$env:DUXEL_FXAA='1'; ./run-fba.ps1 ./samples/fba/Duxel_perf_test_fba.cs`
- FXAA OFF
  - `$env:DUXEL_FXAA='0'; ./run-fba.ps1 ./samples/fba/Duxel_perf_test_fba.cs`

## 2) 캡처 규칙

- 각 모드에서 아래 2장씩 캡처
  - 정지 장면 1장
  - 고속 이동 장면 1장
- 동일 카메라/동일 타이밍으로 반복

### 문서 임베드(플레이스홀더 경로)

- 아래 경로에 파일을 두면 문서에서 바로 비교 가능
  - `artifacts/captures/off/frame_0120.bmp`
  - `artifacts/captures/off/frame_0600.bmp`
  - `artifacts/captures/on/frame_0120.bmp`
  - `artifacts/captures/on/frame_0600.bmp`

#### OFF (AA OFF)

![AA OFF - Frame 0120](../artifacts/captures/off/frame_0120.bmp)
![AA OFF - Frame 0600](../artifacts/captures/off/frame_0600.bmp)

#### ON (AA ON)

![AA ON - Frame 0120](../artifacts/captures/on/frame_0120.bmp)
![AA ON - Frame 0600](../artifacts/captures/on/frame_0600.bmp)

## 3) 품질 점수표 (1~5점)

| 항목 | 설명 | MSAA 4x | FXAA | 비고 |
|---|---|---:|---:|---|
| 경계 계단현상 | 도형 외곽 톱니 감소 정도 |  |  |  |
| 고속 shimmer | 이동 시 반짝임/떨림 억제 |  |  |  |
| 텍스트 선명도 | UI 텍스트 번짐/흐림 |  |  |  |
| 미세 디테일 보존 | 얇은 선/작은 도형 유지 |  |  |  |
| 체감 안정성 | 장시간 관찰 피로도 |  |  |  |

## 4) 성능 측정표

| 항목 | MSAA 4x | FXAA | 비고 |
|---|---:|---:|---|
| 평균 FPS |  |  |  |
| 최소 FPS |  |  |  |
| 1% Low FPS |  |  |  |
| 프레임 타임 평균(ms) |  |  |  |

> 권장: 같은 장면에서 3회 반복 후 평균값 사용

## 5) 합격 기준(권장)

### 품질 우선 기본값

- 텍스트 선명도 `>= 4`
- 경계 계단현상 `>= 4`
- 위 조건 충족 시 MSAA 4x 유지 권장

### 성능 우선 기본값

- FXAA가 평균 FPS `+10%` 이상 향상
- 텍스트 선명도 `>= 3`
- 위 조건 충족 시 FXAA 옵션 채택 가능

## 6) 최종 판정

- 선택 모드: MSAA 4x / FXAA
- 선택 이유:
- 트레이드오프:
- 후속 액션:
