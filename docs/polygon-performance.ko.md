# 공통 엔진 최적화와 공 튀기기 검증

> Last synced: 2026-11-04
> 상태: 공통 엔진 구현, 동일 조건 반복 측정, 정확성·회귀·로컬 배포 검증 완료.

샘플은 `samples/fba/Duxel_perf_test_fba.cs`다. 기본값은 5,000개, Size 6,
요청 창 크기 1920×1080이다. 관측한 실제 framebuffer는 1920×1080,
UI viewport는 1920×1032, 캔버스는 1520×918.4625였다.

공통 구현은 다음과 같다.

- `SpatialGrid2D`: 음수 좌표를 포함하는 재사용 가능한 밀집 공간 인덱스.
  사전 지정한 셀 예산을 넘으면 명시적으로 실패한다.
- `Duxel.Core.Simulation.CircleCollisionSolver2D`: 호출자가 소유한 구조체 span을
  갱신하는 공통 원형 충돌 모듈. 외형과 렌더링은 호출자가 소유한다.
  최대 실제 지름으로 인접 셀 검색 범위를 정하고, 초기 위치로 후보를 찾으며,
  선택한 경계 안으로 충돌 보정 결과를 제한한다.
- `UiDrawListBuilder.AddRegularPolygons`: 순서를 유지하는 벡터 다각형 일괄 추가.
  기존 개별 다각형 경로와 정점·색상·삼각형이 정확히 일치한다.
- `AddCircleFilled`: 기존 shader의 음수 반지름 인코딩을 사용하여 원과 사각형을
  같은 6정점 인스턴스 스트림에 넣는다. 곡선 AA와 제출 순서를 유지한다.
  이전 `UiCircleFilledPrimitive` 입력도 계속 지원한다. 정적 삼각형 변환은
  음수 반지름도 제외하여 분석적 곡선 coverage를 유지한다.
- `UiRendererFrameMetrics.FramebufferSize`: 실제 렌더 대상 크기를 공개한다.

캔버스는 기존 `BeginLayer`의 GPU 벡터 경로를 사용한다. 배경의 정적 geometry와
움직이는 다각형을 구분한다. `ContentVersion`으로 멈춘 캔버스의 geometry를
재사용하며, 이동·외형·영역·배경 캐시 변경 시 갱신한다. 컨트롤은 별도 UI 영역이다.
레이어별 비트맵을 만들지 않는다.

배경 캐시는 샘플의 수동 draw-list 복사 대신 공통 중첩 `BeginLayer`와
`StaticCache`/`ContentVersion`을 사용한다. 공통 렌더 파이프라인도 기본 활성화한다.
`DUXEL_PERF_PIPELINE=0`은 비교용 직렬 경로를 명시적으로 선택한다.
`UiDrawList.GetCommandFeatures`와 `UiDrawData.GetCommandFeatures`는 변경되지 않은
명령의 콜백·GPU 기능 분류를 생산자와 렌더러에서 재사용한다. 명령 상태가 변경되면
다시 분류하고, 버전이 없는 외부 명령은 직접 확인한다. 특정 샘플에 대한 분기는 없다.

## 2026-11-04: 체크박스와 도형 수 변경

`Dynamic Canvas Layer`는 캔버스의3D 레이어 적용을 전환한다. `Global Static Cache`는
이 샘플의 정적 배경 레이어만 제어하며, 글리프·텍스처 캐시까지 일괄 해제하는 설정은 아니다.
`Render Profile`은 Render/MSAA1과 Display/MSAA4를 전환한다. VSync와는 독립 설정이다.
VSync ON은 공통 렌더러의 FIFO, OFF는 지원되는 Immediate/Mailbox 표시 모드를 사용한다.
이전 ON의 Mailbox는 표시 동기화를 유지하면서도 대기 중 프레임을 교체할 수 있어 렌더 FPS가
높게 유지될 수 있었다. [Khronos 표시 모드 규격](https://docs.vulkan.org/refpages/latest/refpages/source/VkPresentModeKHR.html).
실제 적용 상태는 `UiRendererFrameMetrics.VSyncEnabled`/`MsaaSamples`/`PresentationMode`로 확인한다.

`UiGpuParticleSystem.Append`는 기존 GPU 상태를 유지하고 새 seed만 추가한다.
`Truncate`는 선두의 남은 도형을 유지한다(최소1개). Clear와 Reset은 명시적인 초기화다.
시스템은 수정할 때 불변 `UiGpuParticleSnapshot`을 발행하고 `UiGpuParticleFrame`이 이를
생성 시점에 캡처한다. 따라서 UI가 도형을 추가해도 이미 대기 중인 프레임의 수는 바뀌지 않는다.
수정 후에는 새 프레임을 생성해야 한다. 이전 프레임의 `with { DeltaSeconds = ... }`는
이전 스냅샷을 유지한다. `Initial`은 초기 seed이며 현재 GPU 위치 조회가 아니다.

```csharp
using var system = new UiGpuParticleSystem(initialBodies);
// Each submitted frame captures a stable snapshot.
drawList.AddGpuParticles(new(system, bounds, deltaSeconds));
system.Append(addedBodies);
drawList.AddGpuParticles(new(system, bounds, nextDeltaSeconds));
// Keep the leading live bodies without resetting them.
system.Truncate(remainingCount);
```

GPU 버퍼는2의 거듭제곱 용량을 예약한다. 용량 안의 추가는 새 상태만 업로드하고,
용량 확장 시 기존 상태를 GPU 내부에서 복사하며 새 상태와 새 인덱스만 업로드한다.
삭제는 살아 있는 prefix를 그대로 사용한다. 삭제 후 같은 개수로 다시 추가하는 경우도
도형별 생성 ID로 구분하여 삭제된 상태를 잘못 재사용하지 않는다. 예약 용량은 확장 당시 활성 수의2배 미만이다. 삭제 후에는 즉시 축소하지 않아
다시 추가할 때 재할당을 줄인다. Clear/Dispose 시 해당 시스템을 해제한다.
그리기는 용량이 아닌 활성 수 기준이다.

집중 검사에서 기존 상태48바이트 전체의 일치, 대기 프레임, 용량 확장, 삭제/재추가,
삭제 후 이동 재개, CPU 기준 모델을 확인했다. 추가100개는 기존5000개를 유지하면서
4,800B만 상태 업로드했다. 자동 NativeAOT 입력 검사는 원본 샘플을 읽어 관측·클릭 코드만
붙인 별도 fixture를 만들고, 실제 Win32 버튼 메시지로 설정과 렌더러 결과를 검사한다.
fixture 바이너리는 성공 후 소유권·해시 기록을 보존하고 제거한다.

추가 회귀 중 거의 접선인 접촉의 미세한 법선 속도 부호 차이가 큰 회전을 만드는 기존 결함을
재현했다. CPU/GPU 모두 회전 반응을 실제 법선 반발 강도로 제한했다. 접선 속도만으로
유한한 회전을 만들던 불연속을 제거한 변경이며, 허용 오차를 늘리지 않았다.
[법선 반발에 비례하는 마찰 제한의 참고](https://box2d.org/documentation/md_simulation.html).
기존 순차 CPU 모델과는 별개이며, 완전한 강체 물리 엔진을 구현했다는 의미는 아니다.
실패 기록은 `glancing-before-fix`, `full-net10-final`에, 수정 검증은
`glancing-after-fix-net10`, `glancing-stability-net10-2/3`, `full-net10-glancing-fixed`,
`full-net11-glancing-fixed`에 보존한다. NET10/11 전체 검사는 각474개다.

최신 배포 실행 파일은 `samples/fba/artifacts/Duxel_perf_test_fba/net11.0-windows7.0/Duxel_perf_test_fba.exe`이며,
SHA256은 `A75CBAE0CC669A7932A21B4B4619382555EF99E087D561E2149AAA157279B546`이다.
SDK11.0.100-rc.3.26522.148 / 런타임.NET11.0.0-rc.3.26522.148 / NativeAOT Speed,
5000개·Size6·framebuffer1920×1080·캔버스1520×918.4625·Render/MSAA1·VSync off,
준비1초+측정6초×독립3회의 결과는 다음과 같다.

| 지표 | 최종 중앙값 |
| --- | ---: |
| 평균 FPS | 5561.846 |
| median / p95 / p99 프레임 ms | 0.16590 / 0.30840 / 0.46203 |
| 1% low FPS | 1880.374 |
| GPU 시간 | 151.733µs |
| draw / dispatch | 55 / 8 |
| 동적 전송 / 입자 초기화 전송 | 9200B / 0B |
| UI Render / renderer 구간 할당 | 3371.816B / 137.465B |

실행별 평균은5521.04~5600.16FPS였다. API 갱신만 적용한 중간 단계의 비교는
5494.694→5472.606FPS(-0.40%)로 범위가 겹쳐 격리된 개선/악화로 해석하지 않는다.
최종 단계는 회전 응답 정확성 수정도 포함하므로 이전 모델과 직접 개선율을 계산하지 않는다.
UI 할당은 프레임 스냅샷 참조 등으로 약8B/프레임 늘었다. 동적 전송은5000개 전체 재전송이 아니다.
상태는 GPU에 상주하며, 예약 용량 증가는 재할당을 줄이는 메모리 비용이다.
근거는 `artifacts/particle-controls-2026-11-04/performance-final-summary.json`,
`raw-statistics-audit.json`(9회), `image-regression.json`(기존142/142동일),
`native-controls-4`(실제 입력24검사)에 보존한다.

입력 검증 재현:

```powershell
& ./tests/RunPolygonControlsTests.ps1 -OutDir 'W:/MyWorks/Duxel/artifacts/controls-reproduction'
```

이 명령은 소유한 별도 검증 창에 메시지를 보내며 전역 마우스 커서를 이동하지 않는다.

아래2026-11-03 수치는 당시 실행 파일의 기록이며, 최신 변경의 격리된 개선율이 아니다.

## 2026-11-03: GPU 상주 경로와 동일 알고리즘 비교

`UiGpuParticleSystem`, `UiGpuParticleFrame`, `AddGpuParticles`와 Vulkan compute 경로는
샘플과 분리된 공통 엔진 기능이다. 초기 상태·인덱스만 업로드하고, GPU가 이동,
공간 인덱스, 충돌 응답, 정점 생성을 수행한다. 기존 벡터 그래픽 파이프라인이
그 GPU 정점 버퍼를 직접 읽는다. 프레임마다 입자 상태를 CPU로 읽지 않는다.
`ParticleJacobiSimulation`은 동일한 응답 알고리즘의 재사용 가능한 CPU 구현이다.

5,000개·Size6·두 번의 Jacobi 충돌 반복·동일 framebuffer/캔버스·Render/MSAA1에서
CPU/GPU 실행 순서를 교대하며 각3회 측정했다(`release-native-paired`). 공통 렌더 파이프라인은
활성 상태다. SDK는 `11.0.100-rc.3.26522.148`, 실행 이미지의 런타임은
`.NET 11.0.0-rc.3.26522.148`, X64 NativeAOT/Speed다. 측정 이미지의 SHA256은
`4839615A2D412268B0DB6AF5DD3C42DB333E628FA9494AD584A4AA70EB89FB4A`다.

| 같은 물리 알고리즘 | FPS 중앙값 | median / p95 / p99 ms | 1% low FPS | 동적 전송 B/프레임 |
| --- | ---: | --- | ---: | ---: |
| CPU Jacobi | 849.066 | 1.10910 / 1.42032 / 2.59723 | 325.517 | 685696 |
| GPU Jacobi | 5448.071 | 0.16620 / 0.33793 / 0.49069 | 1757.484 | 9200 |

FPS는 약6.42배(+541.65%)다. 실행별 평균 범위는 CPU838.64~852.48,
GPU5425.83~5462.73FPS다. GPU의 약151.26µs는 compute와 그리기를 함께 포함한다.
이전 CPU 정점 생성·충돌 비용을 GPU로 옮겼으므로 GPU 시간만 줄었다는 주장은 아니다.
GPU는 프레임당8dispatch로5,000개를 처리하며, 준비 이후 입자 초기 상태 업로드는0이다.
9.2KB 전송에는 계속 갱신되는 UI가 포함된다. UI할당은 약3364B/프레임으로 남아 있다.
이전 CPU 순차 응답 모델과는 처리 순서가 다르므로, 그 수치와 직접적인 같은 알고리즘
비교라고 표현하지 않는다. 프레임 시간은 제출 루프 기준이며 모니터 주사율이 아니다.

같은 실행 이미지의 설정별 추가3회 중앙값(`release-profile-cache-suite`)은 다음과 같다.

| 설정 | FPS 중앙값 | median / p95 / p99 ms | 1% low FPS | 동적 전송 B/프레임 |
| --- | ---: | --- | ---: | ---: |
| Render, 배경 캐시 on | 5476.160 | 0.16670 / 0.33889 / 0.48394 | 1741.088 | 9200 |
| Render, 배경 캐시 off | 5436.324 | 0.16650 / 0.34630 / 0.48556 | 1729.631 | 78320 |
| Display, 배경 캐시 on | 4254.199 | 0.21620 / 0.41766 / 0.55318 | 1520.836 | 9168 |
| Render, 동적 캔버스 off | 5657.287 | 0.15980 / 0.32950 / 0.48670 | 1747.663 | 9168 |
| Render, 정지 재사용 | 7763.649 | 0.11290 / 0.25649 / 0.40329 | 2087.907 | 9160 |

배경 캐시는 도형 구성 시간을 약76.35→3.18µs, 동적 전송을78320→9200B로 줄였다.
FPS 차이는 약+0.73%로, GPU가 주 병목인 현재 부하에서 CPU 절감만큼 FPS가 증가하지 않는다.
동적 캔버스는 직접 그리기보다 약3.20% 느리며 UI 구성 할당도 약648B/프레임 많다.
관리 비용을 숨기거나 이 차이를 성능 이득으로 해석하지 않는다. 정지 상태는 compute0회다.
프로필 사이 화질 차이와 이동/정지 사이 작업량 차이는 개선율로 계산하지 않는다.

기본 실행은 GPU 모드다. `DUXEL_PERF_SIMULATION`은 `gpu`, `cpu-jacobi`, `cpu`를 받는다.
`cpu`는 기존 순차 모델이며, 비교할 때 모델과 반복 횟수를 함께 기록한다.
현재 GPU 모드는 Add/Remove 시 기존 상태를 유지한다. 크기·속도·변 수·정지는
프레임 설정으로 반영한다. 그래픽 큐의 compute 지원이 없으면 명시적으로 실패한다.

GPU와 CPU Jacobi는 접근하는 접촉에만 반발을 적용한다. 이전 반복에서 분리된 접촉을
다시 반발시키지 않도록 좌표 크기×4×float 머신 엡실론 이내 침투에는 새 반발을 만들지 않는다.
5,000개·61단계의 CPU 스냅샷 기준 비교에서 최대 위치 오차0.000122픽셀,
방향 성분 오차0.0000661, 각속도 오차0.0000425였다. NaN·경계 이탈도 검사했다.
MSAA1/4 정적 GPU 생성 도형은 CPU 기준 이미지와 완전히 일치했다.
원형 경계 기반 모델이며, 연속 충돌 검출이나 완전한 강체 물리 엔진은 아니다.

GPU 도입 및 명령 분류 재사용 후 .NET10/11 전체 검사 각465개, 기존 GPU 이미지136/136 일치를 확인했다.
검증 레이어를 실제 로드한 동기화 검사에서도 오류가 없었다. 이는 관측한 NVIDIA 장치와
검사 범위의 결과이며, 모든 벤더와 모든 입력의 인증은 아니다.
일반 UI의 명령 탐색 비용은 `UiDrawList.GetCommandFeatures`로 공통화하여, 변경되지 않은
명령 상태를 UI/렌더 스레드가 재사용한다. ERD1200테이블·MSAA4의6초 workload를
시나리오별3회 검증했다(`erd-frame-feature-regression`). 카메라1259.94FPS,
엔티티 동시 이동1249.24FPS 중앙값이며, 이전18초 투어와 직접 개선율로 비교하지 않는다.

UI 할당은 샘플 `Render` 구간의 현재 스레드, renderer 할당은 렌더 스레드의 측정 범위다.
전체 프로세스 할당량으로 해석하지 않는다. 미수집 GPU 시간·renderer 할당은0이 아닌
JSON `null`과 표본 수로 기록한다. 계측을 끈 기본 NativeAOT 실행도 정상 종료를 검증했다.
21회 최신 실행의 원시 프레임 통계·설정 감사는 `release-native-raw-audit.json`,
현재 소스·바이너리 해시는 `gpu-current-source-manifest.json`을 따른다.

현재 이미지의 별도 상세 계측(`release-diagnostic/stage-summary.json`)은 준비 구간을
포함한638개 로그 표본이다. 중앙값은 프레임 fence 대기58.3µs, 이미지 획득0.5µs,
업로드3.3µs, 명령 기록34.0µs, 제출11.2µs, present49.8µs, GPU143.456µs다.
계측 빈도는60프레임당1회이며 이 실행의 FPS는 최종 비교표에 합산하지 않았다.
CPU/GPU 실행이 겹치므로 구간 시간을 더해서 프레임 시간이라고 해석하지 않는다.
현재 남은 비용은 GPU 실행·동기화 대기와 일반 UI 명령 전환이다.

GPU 계산은 그래픽 큐에서 실행한다. 별도 compute 큐의 비동기 중첩은 구현하지 않았다.
GPU→CPU 매 프레임 readback 경로는 사용하지 않으며, 성능 비교용으로 새로 추가하지 않았다.
시험용 상태 readback은 정확성 검사에만 있다. 연속 충돌 검출·범용 강체·다른 GPU 벤더
실측은 이번 구현의 검증 범위에 포함되지 않는다.

2026-11-03 측정 당시 실행 파일 경로는
`samples/fba/artifacts/Duxel_perf_test_fba/net11.0-windows7.0/Duxel_perf_test_fba.exe`다.
위 SHA256의 실행 파일로 기본 실행 검증과21회 반복 측정을 수행했다.

## 이전 CPU 최적화 단계의 실행 파일

이전 CPU 단계의 실행 파일 경로는 `samples/fba/artifacts/Duxel_perf_test_fba/net11.0-windows7.0/Duxel_perf_test_fba.exe`,
SHA256은 `FA7EA73F1FFDA7396786CFC5C328351E4A31C4CE6C9967A43DDCBD77185666A5`다.
정지 상태 재사용 계약 수정 후 독립3회 결과(`smart-canvas-profiled`)는 다음과 같다.

| 조건 | FPS 중앙값 | median / p95 / p99 ms | 1% low FPS |
| --- | ---: | --- | ---: |
| 이동, 동적 캔버스 | 1020.809 | 0.90790 / 1.26606 / 1.99403 | 414.442 |
| 이동, 직접 그리기 | 1051.348 | 0.89720 / 1.14252 / 1.72872 | 450.088 |
| 정지, 캔버스 재사용 | 6630.166 | 0.13600 / 0.26070 / 0.42250 | 2013.995 |

이동하는 동적 캔버스 3회 모두 평균1000FPS를 넘었으나, 모든 프레임1ms 보장은 아니다.
이동과 정지는 작업량이 다르므로 서로 개선율로 비교하지 않는다. 이동 시 draw call53,
GPU약108µs, 동적 전송685592B/프레임, UI할당약3316B/프레임이다. 정지 시 전체 동적 전송은
8952B/프레임으로 줄고, 캔버스 geometry는 재사용한다. 남은 전송에는 갱신되는 UI가 포함된다.
기존 UI할당약2707B보다 이동 시 할당이 증가한 점은 남은 개선 대상이다.

새 측정 경로는 평균FPS뿐 아니라 median/p95/p99/1%low를 원시 프레임에서 재계산한다.
`raw-statistics-audit.json`은 이전/후보/현재 합계30실행의 수치와 실제 창·캔버스 크기를 확인했다.
별도 `diagnostic-profiled`는 CPU 렌더 단계 로그를 포함하며 최종FPS 비교에 합산하지 않는다.

진단 로그44개 중앙값은 upload35.9µs, 명령 기록30.2µs, 제출21.9µs,
present69.55µs, GPU105.2µs였다. UI·물리 구간과 GPU 구간의 중첩 때문에
이 수치들을 단순 합산해 프레임 시간으로 해석하지 않는다.

같은 현재 엔진으로 별도 NativeAOT를 빌드한 ERD1200테이블 회귀6실행도 통과했다.
6초 workload·시나리오별 독립3회에서 카메라1276.02FPS, 엔티티 동시 이동1265.15FPS
중앙값이었다(`erd-common-regression`). 이전 ERD18초 측정과 기간·카메라 경로 범위가
달라 직접적인 성능 개선율로 계산하지 않는다. 기존 기본 ERD 실행 파일은 교체하지 않았다.

## 중간 측정

SDK 11.0.100-rc.3.26522.148 / NativeAOT / Speed / VSync off.
각 설정은 표시된 창에서 1초 워밍업 후 6초씩 독립 3회 실행했다.
아래는 ContentVersion 기반 정지 상태 재사용을 추가하기 **전** 후보의 기록이며,
현재 소스 전체의 최종 성능 결과가 아니다.

| 조건 | 기존 FPS 중앙값 | 공통 최적화 후보 FPS 중앙값 |
| --- | ---: | ---: |
| Render, 배경 캐시 on | 298.489 | 1076.373 |
| Render, 배경 캐시 off | 293.074 | 1001.251 |
| Display, 배경 캐시 on | 297.048 | 1067.128 |
| Render, 배경 캐시 on, 동적 캔버스 off | 측정 없음 | 1094.186 |

Render+cache의 draw call은 916→53이었다. 도형 수만큼 draw call이 발생하는
구조가 아니다. CPU 도형 정점 전송은 아직 매 프레임 발생하며 약 685KB다.
동적 레이어 자체에는 관리 비용이 있고, 현재 기록에서는 직접 그리기보다 약간
느리다. 레이어가 무료라는 주장이나, 이 차이를 레이어 적용의 성능 향상으로
표현하지 않는다. Render는 MSAA 1x, Display는 4x이므로 두 프로필의 FPS 차이를
같은 화질의 엔진 개선율로 계산하지 않는다.

기존 충돌 탐색의 셀 크기와 이동 중 후보 좌표 문제, 충돌 보정 후 경계 이탈도
수정했다. 이전 실행과 궤적이 비트 단위로 같은 물리 시뮬레이션이라는 의미는
아니다. 충돌은 실제 다각형 모서리가 아닌 원형 경계 기준이며, 연속 충돌 검출이나
완전한 강체 물리 엔진으로 검증한 것은 아니다.

근거는 `artifacts/ball-perf-2026-11-03/` 아래에 있다.

- `baseline-profiled`: 기존 CPU/렌더 구성의 9회 결과와 원시 프레임 시간.
- `unified-profiled`: 공통 최적화 후보의 12회 결과와 원시 프레임 시간.
- `gpu-unified-primitives`: GPU 검사 451개.
- `unified-primitives-image-comparison.json`: 변경 전후 GPU 이미지 136/136 일치.
- `core-tests-bounds`: 공간 탐색·겹침·동일 위치·경계 보정·대량 다각형 검사.

## GPU 설계 근거

GPU 공간 분할과 충돌 계산은 가능하다. 다만 현재의 순차적 양쪽 상태 갱신을
그대로 여러 GPU invocation에서 실행하면 같은 몸체에 동시 쓰기가 발생한다.
셀 색상별 패스 또는 이중 버퍼 기반 누적/적용 등 응답 알고리즘과 검증이 필요하다.
compute 결과를 매번 CPU로 가져와 정점을 생성하는 경로의 동기화 비용도 포함해야 한다.
적합한 목표 경로는 GPU 상주 상태 → 공간 인덱스 → 충돌 응답 → GPU에서 직접 그리기다.
현재 구현은 두 상태 버퍼를 사용하는 Jacobi 누적/적용 방식이다. 순차 CPU 모델의
실행 순서를 GPU에서 그대로 재현한다는 주장은 하지 않는다.

근거: [Khronos GPU particle compute](https://docs.vulkan.org/tutorial/latest/11_Compute_Shader.html),
[NVIDIA 공간 분할과 병렬 충돌 응답](https://developer.nvidia.com/gpugems/gpugems3/part-v-physics-simulation/chapter-32-broad-phase-collision-detection-cuda).

## 재현

공통 `run-fba.ps1`은 `scripts/BuildArtifactLifetime.ps1`로 NativeAOT 출력 소유권과
최대3개 보관을 관리한다. 동일 샘플 출력 루트의 변형들을 보수적으로 함께 센다.
새 출력 전에 공간을 확보하며, 완료 기록·전체 파일 해시·경로 경계·실행 프로세스를
확인한 출력만 정리한다. 미등록 과거 출력은 개수에 포함하지만 삭제하지 않는다.
실패·취소·실행 중·사용자 수정 출력은 보호하며, 보호된 출력만으로 한도가 차면
추가 빌드를 시작하기 전에 명시적으로 실패한다. 소유권 기록과 정리 계획·회수 용량은
출력 루트의 `.build-records`에 보존한다. 이름 있는 mutex로 기록 변경을 직렬화한다.
전용 검증의 성공·실패·취소·실행 중·경로 이탈·재실행·junction·해시 변경·미등록 출력
사례13개가 통과했으며, 테스트 바이너리는 결과·해시 보존 후 제거했다.
이전에 정리가 거부된 기준 바이너리는 이 정리 대상 루트 밖에 있으며 건드리지 않았다.

저장소 루트의 고정 SDK는 변경하지 않는다. .NET 11 선택 디렉터리에서 실행한다.

```powershell
Set-Location tests/runtime-net11
$build = @{
    Path = 'W:/MyWorks/Duxel/samples/fba/Duxel_perf_test_fba.cs'
    DotnetPath = 'C:/Program Files/dotnet/dotnet.exe'
    TargetFramework = 'net11.0-windows7.0'
    NoCache = $true
    NoLaunch = $true
    ExtraArgs = @('-p:OptimizationPreference=Speed')
}
& ../../run-fba.ps1 @build
```

측정은 `scripts/measure-polygon-performance.ps1`에 Executable과 OutDir을 지정한다.
다른 성능 샘플과 GPU 검사를 동시에 실행하지 않는다. 기본 3회이며 Cases에
`render-cache`, `render-uncached`, `display-cache`, `render-direct`를 지정할 수 있다.
FPS는 물리 모니터 주사율이 아닌 UI 프레임 시작 간격 기준이다.

저장소 루트에서 최신 비교 조건을 재현한다. CPU/GPU 비교에서는 각3회를 독립 실행하고
실행 순서를 교대한다. 출력 디렉터리는 이전 결과와 겹치지 않게 지정한다.

```powershell
$measure = @{
    Executable = 'W:/MyWorks/Duxel/samples/fba/artifacts/Duxel_perf_test_fba/net11.0-windows7.0/Duxel_perf_test_fba.exe'
    OutDir = 'W:/MyWorks/Duxel/artifacts/polygon-reproduction-gpu'
    Simulation = 'gpu' # cpu-jacobi: 같은 알고리즘의 CPU 기준
    Runs = 3
    Count = 5000
    Size = 6
    Seconds = 6
    Cases = @('render-cache', 'render-uncached', 'display-cache', 'render-direct', 'render-paused')
}
& ./scripts/measure-polygon-performance.ps1 @measure
```
