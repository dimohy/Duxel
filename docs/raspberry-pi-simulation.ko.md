# 라즈베리파이 사양 기반 시뮬레이션 — 2026-11-04

[English](raspberry-pi-simulation.md)

현재 Duxel은 조사한 Pi 드라이버에서 그대로 실행할 수 없다. 사양·드라이버 호환성 대조와 기존 프레임 기록의 민감도 분석을 완료했으며, Pi 실기기 실행이나 ARM 명령·GPU 하드웨어 에뮬레이션은 수행하지 않았다. 전체 최적화 목표는 계속 활성 상태다.

## 공식 사양과 고정 조건

| 대상 | CPU | GPU | RAM 버스 이론 대역폭 |
|---|---|---|---:|
| Pi 4 | Cortex-A72 4코어, 현대 보드 1.8GHz, 초기 보드 1.5GHz | VideoCore VI / V3D 4.2 | 8.0GB/s |
| Pi 5 | Cortex-A76 4코어, 2.4GHz | VideoCore VII / V3D 7.1 | 17.068GB/s |

클록·32비트 버스·2000/4267MT/s는 [공식 출시 설명](https://www.raspberrypi.com/news/introducing-raspberry-pi-5/)에서 확인했다. 대역폭은 `전송률×32/8`의 계산값이며 지속 실효 대역폭이 아니다. Pi 5의 현재 Vulkan 1.3 및 RAM 선택지는 [현재 제품 사양](https://www.raspberrypi.com/products/raspberry-pi-5/)을 따른다. GPU 세대와 드라이버 구분은 [Mesa V3D 문서](https://docs.mesa3d.org/drivers/v3d.html)를 따른다. 클록비로 Windows FPS를 환산하지 않았다.

드라이버 프로필은 개발자 발표에서 Trixie 기준으로 확인된 [Mesa 25.0.7](https://archive.fosdem.org/2026/schedule/event/BMFQSE-raspberry-pi-gpu-drivers-from-bookworm-to-trixie/)과, 조사일 최신 공식 릴리스인 [26.2.6](https://docs.mesa3d.org/relnotes/26.2.6.html)을 별도로 고정했다. 실제 사용자의 설치 버전을 추정한 것이 아니다.

## 실행 가능성 대조

`src/Duxel.Vulkan/VulkanRendererBackend.Device.cs`의 `RequireGpuDrivenRendererFeatures`가 검사하는 7개 기능을 각 Mesa 배포본의 `src/broadcom/vulkan/v3dv_device.c` 전체 `get_features` 초기화와 대조했다.

| 필수 기능 | Mesa 25.0.7 | Mesa 26.2.6 기본 설정 |
|---|---|---|
| ShaderSampledImageArrayDynamicIndexing | false 명시 | false, WebGPU override 제외 |
| DescriptorBindingSampledImageUpdateAfterBind | false | false |
| DescriptorBindingUpdateUnusedWhilePending | false | false |
| DescriptorBindingPartiallyBound | false | false |
| RuntimeDescriptorArray | false | false |
| BufferDeviceAddress | true | true |
| DynamicRendering | true | true |

미지정 필드는 C aggregate 초기화에 따라 0이다. 26.2.6의 `V3D_WEBGPU_OVERRIDE`는 기본 false이고, 소스 자체가 실제 지원 범위를 넘는 광고 및 실패 가능성을 명시한다. 이를 활성화하지 않았다. 두 버전 모두 필수 기능 5개가 부족해 현재 Duxel의 검사에서 명시적으로 거부된다. Vulkan 1.3이라는 이름만으로 descriptor indexing 지원을 보장하지 않는다.

원본은 [25.0.7 공식 배포본](https://archive.mesa3d.org/mesa-25.0.7.tar.xz)과 [26.2.6 공식 배포본](https://archive.mesa3d.org/mesa-26.2.6.tar.xz)이다. 다운로드 후 각각 공식 릴리스 노트의 SHA256과 대조했다.

- 25.0.7: `592272df3cf01e85e7db300c449df5061092574d099da275d19e97ef0510f8a6` — [검증값](https://docs.mesa3d.org/relnotes/25.0.7.html)
- 26.2.6: `b9436b3e1a8be76fbd4823dc884f90596ab7c6ac5e1867a305e66ffccfb31a92` — [검증값](https://docs.mesa3d.org/relnotes/26.2.6.html)

현재 저장소에는 Windows 창·입력·텍스트 구현이 있고 Linux 대응 구현은 없다. GPU 기능 조정만으로 Linux 앱 실행이 완성되는 것은 아니다. 일반 Vulkan 계층이나 별도 백엔드를 추가할 때도 기존 유지 기하·변경 영역·벡터 품질을 함께 지원해야 한다.

## 기존 기록을 이용한 지연 민감도

기존 `optimization-evaluation-2026-11-04/comparison-ref`의 전후 각 3회, 정지·이동 총 12회·55,850프레임을 재사용했다. 기존 측정기의 `-SummarizeOnly`로 프레임 수·평균·median/p95/p99·1% low를 재감사했다. 추가 앱 벤치는 실행하지 않았다.

고정 조건: .NET11 RC3 `11.0.100-rc.3.26522.148` / runtime `11.0.0-rc.3.26522.148`, NativeAOT Speed, 128팔레트×256견본, 1600×1000, MSAA1, VSync off, 12초·3회 독립 교대 실행. .NET10의 기존 기능 검증은 성능 입력으로 섞지 않았다. 실행 파일과 JSON/CSV 해시를 보존했다.

아래는 최종 후보의 기록된 프레임 간격 전체에 일정 배수를 곱한 결과다. **배수는 가정이며 Pi 4·5에 대응하지 않는다.** CPU·GPU·드라이버·대기 시간을 분리한 Pi 모델도 아니다. 물리 화면 주사율이 아닌 accepted-presentation 기록이다.

| 가정한 지연 배수 | 정지 평균 FPS 중앙값 | 이동 평균 FPS 중앙값 | 이동 p95 | 이동 60Hz 예산 초과 프레임 중앙값 |
|---:|---:|---:|---:|---:|
| 1×, Windows 원본 | 403.15 | 395.35 | 2.887ms | 0.00% |
| 2× | 201.57 | 197.67 | 5.774ms | 0.00% |
| 4× | 100.79 | 98.84 | 11.548ms | 0.70% |
| 8× | 50.39 | 49.42 | 23.097ms | 99.11% |
| 12× | 33.60 | 32.95 | 34.645ms | 99.98% |

따라서 이 부하가 4배 느려지는 조건에서는 대부분 프레임이 60Hz 예산 안에 들지만, 8배 조건에서는 60Hz 목표를 만족하지 못한다. Pi FPS 예측이나 보증으로 읽으면 안 된다. 기존 sampled UI 구성 CPU 약2.4ms가 남아 있는 점이 다음 공통 CPU 재사용 작업의 근거다.

## 대역폭·메모리 계산

동일 1600×1000 RGBA8 화면의 60Hz scanout은 논리적으로 384MB/s다. 단순 변경 영역 read+write는 `1600×1000×4×2×변경비율×60`으로 계산했다. 이동 동적 업로드 66,512B/frame은 60Hz에서 약3.99MB/s다. dirty 비율 0/1/10/100%, 이론 대역폭 활용률 25/50/100%를 독립 가정한 24개 경우를 저장했다. scanout은 정지 시에도 남는다.

| 가정 | 논리 트래픽 합 | Pi4 대역폭 25% 가정 대비 | Pi5 대역폭 25% 가정 대비 |
|---|---:|---:|---:|
| 변경 영역 1% + 이동 업로드 + scanout | 395.67MB/s | 19.78% | 9.27% |
| 변경 영역 100% + 이동 업로드 + scanout | 1155.99MB/s | 57.80% | 27.09% |

이는 타일 GPU, 압축·캐시, 블렌딩 overdraw, 텍스처·depth·MSAA resolve, 실제 dirty 비율, 동시 CPU/OS 부하를 제외한 논리 트래픽 분석이다. 실제 GPU 메모리 트래픽이나 충분한 GPU 성능을 입증하지 않는다. 업로드 하나만 낮추는 것보다 불필요한 재구성·갱신을 줄이는 방향의 근거로 사용한다.

Windows 관측 Private 중앙값은 정지172.15MiB·이동175.55MiB, 작업 집합은123.52/124.98MiB다. 이 값은 Linux/ARM64 예측값이 아니다. RGBA 이미지 한 장6.10MiB, 가정한 3장18.31MiB, MSAA4 color 한 장24.41MiB도 별도 산술값이다. 이들을 Private와 무조건 더하지 않으며, 드라이버·GPU 상주 기하·공유 RAM·OS 예약을 포함한 전체 상주량으로 주장하지 않는다. RAM 1/2/4/8GiB에 대한 8개 용량 문맥을 저장했지만 최소 RAM 지원 판정은 하지 않았다.

## 근거·재현과 다음 단계

근거 디렉터리: `artifacts/raspberry-pi-simulation-2026-11-04/`.

- `simulate.ps1`: 한정된 이번 분석의 재현 코드. 새 제품 런타임·벤치 경로가 아니다.
- `simulation.json`: 20개 프레임 민감도, 24개 대역폭, 8개 메모리 문맥 및 두 버전 기능 대조.
- `trace-audit/summary.json`, `audit.json`, `verification.json`: 재감사·계산 검증.
- `sources.json`: 출처·고정 버전·관측일. 두 원본 압축 파일과 선택 추출 소스도 보존한다.

```powershell
./artifacts/raspberry-pi-simulation-2026-11-04/simulate.ps1
```

검증한 것은 공식 사양·고정 드라이버 소스·기존 기록 재감사·모델 계산이다. 신규 런타임 변경이 없으므로 기존 .NET10/고정 RC3 회귀를 불필요하게 다시 실행하지 않았다. 입력·위젯·샘플·벡터 품질은 바꾸지 않았다.

다음 구현은 공통 CPU 레이아웃·기하 재사용과 캐시 상한/무효화를 검증하면서, 현재 GPU 필수 기능에 의존하지 않는 명시적 지원 경로 및 Linux 창·텍스트 경계를 설계하는 것이다. 실기기가 없다는 사용자 조건에 따라 현재 단계는 시뮬레이션으로 검증하며, 실기기 성능 검증 완료로 승격하지 않는다.
