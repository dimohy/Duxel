# SVG GPU 장면과 내부 카메라

2026-11-15 현재 소스 반영: 실행 경로에 공통 Component 스크롤과 Markdown 이미지 검증을 반영했습니다. 이 NativeAOT 수정본으로32개 화면 표시·384회 네이티브 포인터 변경과 정상 종료를 확인했습니다. 아래의 이전 성능 비교 수치는 보존된 `observer-rc3-aot-publish` 기준 빌드의 측정이며, 이번 실행은 회귀 확인으로 새 속도 개선 비교가 아닙니다.

Linux의 최신 실행 경로는 고정 .NET11 RC3 NativeAOT 옵저버입니다. 32개 표시·정상 종료와 별도 Linux 텍스트/벡터 런타임 이미지 비교를 확인했습니다. 아래의 기존 SVG 브라우저 픽셀 수치는 관리형 검증 범위를 그대로 유지합니다. [Linux 실행·NativeAOT 비교](../samples/Duxel.Gallery.Linux/README.ko.md).

`UiSvg`·`UiSvgIcon`은 소스 버전마다 XML을 한 번 해석하고 불변 벡터 장면을 GPU에 보관합니다. 확대·축소는 가시 출력과 카메라를 갱신하며 CPU 위젯 래스터나 원본 경로 재업로드를 추가하지 않습니다. 입력·배치·명령 제출의 CPU 작업까지0이라는 뜻은 아닙니다. 명시적 이미지 디코딩은 별도 비트맵 경로입니다.

## 현재 검증 범위

| 비교 | 결과 | 남은 범위 |
| --- | ---: | --- |
| 레이어·테두리 기본 원본 | CPU/GPU5/5 | 기존 분수 좌표196픽셀은0으로 교정 |
| 추가 테두리 범위 | CPU/GPU3/5 | 얇은 선284픽셀, 비균등 배율 CPU9·GPU11픽셀 |
| 기하 클립 | CPU/GPU9/9 | 이9종의 전체 범위 일치 |
| 그림자 | CPU/GPU9/9 | 이9종의 전체RGBA 일치 |
| 불투명도 그룹 | GPU10/11 | 필터943픽셀·채널값1 차이 |
| 작은 그라데이션 | GPU8/9 | 방사형1픽셀·채널값1 차이 |
| 복잡 원본 흰 배경 | GPU0/4 | 원본2종×직접SVG/이미지Canvas |
| 복잡 원본 투명 | GPU0/2 | 원본2종 전체 premultiplied RGBA |
| 별도 색 경계 원시RGBA | 5/6 | 투명1종의straight-alpha RGB 차이 |

허용차는0이며 기준 이미지를 현재 출력으로 교체하지 않습니다. 관리형SDK11.0.100/runtime11.0.0 검증이며 원래 RC3 NativeAOT 성능 관문과 별도입니다. Linux32개는 표시·정상 종료 검증이며 물리GPU 성능·전체 접근성을 인증하지 않습니다. 카메라 진단10조건 중9조건에서 GPU 명령 버퍼 시간이4.7~14.3% 줄었고 Red400%는 개선 미확정입니다. 동기 읽기 포함 지연은 범위가 겹치며, 앱FPS·RC3 AOT·벤더별 프로파일링과 별도입니다. 근거: `artifacts/svg-work-cache-linux-ui-2026-11-15/verification.json`.

## 옵저버 실행

```powershell
& 'L:/Cache/Duxel/linux-accessibility-2026-11-14/observer-svg-work-cache-refined-build/bin/Duxel.Gallery/release/Duxel.Gallery.exe'
```

**SVG → 브라우저 비교**는 복잡 원본2종, **클립·경계**는9종, **레이어·테두리**는 교정된5종, **테두리 범위**는 추가5종을 보여줍니다. 확대·회전·여백·모서리 컨트롤을 공유하고 처음 선택할 때만 로드합니다. 왼쪽은 독립 Chromium 기준, 오른쪽은 GPU 위젯입니다. 기준의 픽셀 크기에 맞춰 기본 래스터 배율을 정하며,64픽셀 기준을128픽셀로 따로 그려 비교하지 않습니다. 기본 첫 이미지 쌍은 실제 화면 전체 바이트 비교로도 검사합니다.

Linux 실행물은 `/home/dimohy/.cache/duxel/observer-idle-verified-20261115/publish/Duxel.Gallery.Linux`입니다. [Linux 가이드](../samples/Duxel.Gallery.Linux/README.ko.md)의 다섯 폰트를 순서대로 전달하세요. 유한 검증은 뒤에 `--verify-gpu-camera <새 결과 디렉터리>`를 붙입니다.32개 시나리오는 새 하위 탭의 전체 픽셀 비교와 별도입니다.

## 픽셀 비교

```powershell
$exe = 'L:/Cache/Duxel/linux-accessibility-2026-11-14/graphics-svg-normal-candidate-build/bin/Duxel.Graphics.Tests/release/Duxel.Graphics.Tests.exe'
& $exe 'L:/Cache/Duxel/svg-layer-fresh' gpu-transparent-layer-origin 3
& $exe 'L:/Cache/Duxel/svg-layer-camera-fresh' gpu-transparent-layer-origin-tiled 3
& $exe 'L:/Cache/Duxel/svg-domain-fresh' gpu-transparent-stroke-domains 3
```

항상 새 디렉터리를 사용합니다. 첫 두 프로필은5/5입니다.17×19 카메라 분할의 전체 결과는 단일64×64 출력과5종 모두 같습니다. 추가 범위는5종을 모두 저장한 뒤3/5 불일치로 종료합니다. `gpu-layer-origin`·`gpu-stroke-domains`는 흰 배경이며 나머지 프로필은 `gpu-clip-storage`, `gpu-shadow-pixels`, `gpu-gradient-pixels`, `gpu-opacity-pixels`, `gpu-svg-browser`와 각각의투명 대응 프로필입니다.

`comparison.json`은 모든 픽셀 차이·알파/RGB 차이와 히스토그램을 기록합니다. 새 결과에는 `GeneratedStrokePrograms`, `CommandCount`, `SceneBytes`도 포함됩니다. 기본5종의 생성 테두리 프로그램은1→0, 장면 데이터는 각각1424→752바이트입니다. 이는 논리 장면 데이터이며 전체VRAM·실행 시간 개선율이 아닙니다. `.gpu.rgba`는 투명 프로필에서premultiplied RGBA8이며 PNG 미리보기만straight alpha로 변환합니다.

```powershell
$managed = 'L:/Cache/Duxel/linux-accessibility-2026-11-14/svg-rectangular-stroke-managed-build/bin/Duxel.Svg.Managed.Tests/release/Duxel.Svg.Managed.Tests.exe'
& $managed --browser-stroke-domains 'L:/Cache/Duxel/svg-domain-cpu-fresh'
& $managed --browser-color-storage 'L:/Cache/Duxel/svg-color-cpu-fresh'
```

추가 테두리5종과 원시RGBA6종은 실패 항목도 제외하지 않습니다. premultiplied 일치를straight-alpha 일치로 바꾸지 않습니다.

## 장면·커버리지 계약

카메라는 가상 전체 크기와 정수 픽셀 원점을 유지합니다. 출력은 가시 교차 영역·샘플링 여유·필터 주변부로 제한하고 같은 출력은 재사용합니다. 곡선·점선·테두리의 카메라별 계산은 GPU에서 수행하며 임시 작업 버퍼는 GPU 완료 후 회수합니다.64MiB 작업 한도와 명시적 입력 검증을 유지합니다.

기하 클립·opacity 저장·마스크·필터의 경계를 구분합니다. 단일 페인트 사각 클립은 최초 컴파일에서 교차 기하로 통합하며 원래 객체·페인트 좌표는 유지합니다. opacity 저장 레이어는 그리기 경계의 별도 디더링 원점을 사용합니다. 축 정렬·대시 없는 miter 사각 테두리는 외곽/내곽 커버리지로 처리해 일반 경로 분할 작업을 만들지 않습니다. 공통 사각 인식은 클립과 테두리에서 공유합니다.

GPU 경로flag64는 외곽 경계와 내곽1레코드를 사용하고, 비정상 범위·개수·조합은 제출 전에 거부합니다. 기존6-vector 명령·88바이트push constant·8×8 workgroup을 유지합니다. Vulkan1.2용 임시SPIR-V 컴파일·검증·디스어셈블과 기존 보호 모듈의 바이트 보존을 확인합니다. 새로운 subgroup 폭 가정·배리어·외부 Skia 의존성은 추가하지 않습니다.

브라우저가 보고한 버전은 GraphiteDawnD3D11/Skia `ea1e651d7a105f5ca327dfd65d28957957279e2e`입니다. 이는 개별 draw 추적이 아닙니다. 추가 얇은 선의 남은 차이는 같은 버전의 [분석적 사각 렌더 단계](https://raw.githubusercontent.com/google/skia/ea1e651d7a105f5ca327dfd65d28957957279e2e/src/gpu/graphite/render/AnalyticRRectRenderStep.cpp)와 커버리지 계산을 대조 중입니다. 현재5종 통과를 모든 테두리의 완전 적합성으로 확대하지 않습니다.

## 독립 캡처와 이전 근거

`CaptureImageReferences.mjs`·`CaptureSvgImage.mjs`는 독립 SVG Image/Canvas 바이트를 저장합니다. 원본·기준 해시를 GPU 작업 전에 확인합니다. `BrowserReferenceContract.mjs`는 직접SVG의 DPR·실제 크기·위치를 확인합니다. 동일 조건의 재캡처가 다르면 기존 기준을 자동 교체하지 않습니다.

이전 근거는 `artifacts/svg-layer-dither-2026-11-15`, `artifacts/svg-layer-origin-2026-11-15`, `artifacts/svg-clip-composite-2026-11-15`, `artifacts/svg-graphite-layout-2026-11-15`에 보존합니다.

## 같은 출력의 카메라 성능 비교

```powershell
$exe = 'L:/Cache/Duxel/linux-accessibility-2026-11-14/graphics-svg-normal-candidate-build/bin/Duxel.Graphics.Tests/release/Duxel.Graphics.Tests.exe'
& $exe 'L:/Cache/Duxel/svg-camera-benchmark-fresh' gpu-svg-camera-bench 3
```

원본2종×배율5종마다16프레임 워밍업·32프레임 측정을 하고1픽셀 팬으로 실제 출력을 갱신합니다. 최대512×256의 전체 premultiplied RGBA를 읽고 해시를 기록합니다. `frames.csv`는 UI 구성 시간, 동기 렌더·읽기 시간, 이전 프레임 슬롯의 GPU 명령 버퍼 시간, 호출 스레드 할당량, XML·장면 업로드·CPU 래스터 수를 구분합니다. 해시와 파일 저장은 측정 시간 밖입니다. GPU 시간은 UI 그리기를 포함하며 SVG만의 시간이 아닙니다. 읽기용 CPU 픽셀 배열 할당을 제품의 SVG CPU 래스터로 해석하지 마세요.

기준/후보를3회 교대 실행한960개 측정 프레임의 전체 출력 해시가 같습니다. 기존 브라우저 비교15프로필의107개 원시 출력도 그대로입니다. 공통 셰이더는 합성·마스크 구조 명령을 먼저 유지하고, 각 그리기의1디바이스픽셀 여유 경계 밖이면 페인트·그라데이션 계산을 생략합니다. 기존 분석적 프린지·샘플·원본 순서·장면 데이터·88바이트push constant를 유지합니다. 별도 CPU 가시 목록이나 확대별XML 해석은 추가하지 않습니다.

이는 GPU 명령 버퍼 시간에서 확인한 제한된 개선입니다. Red400%와 전체 동기 지연의 개선은 확인되지 않았으며, 전체 명령 순회·출력 자원 생성·동기 읽기 비용을 별도로 조사해야 합니다. 원래 복잡 SVG의 브라우저 전체 일치도 아직 미완료입니다.

## GPU 작업 버퍼와 정상 표시 비용

카메라가 바뀔 때 네이티브 작업 버퍼를 매번 만들고 버리던 경로를 완료 버퍼 캐시로 통합했습니다. 업로드/장치 메모리를 구분하고, 해당 슬롯의 GPU 펜스와 `q + N` 반환 기한을 모두 만족해야 재사용합니다. 불변 장면 데이터는 별도 소유권을 유지합니다. 캐시는 완료된 버퍼만 최대64MiB·256개 보관하며, 진행 중인 버퍼와 전체VRAM 한도는 아닙니다. 렌더러 종료 시 모두 해제합니다.

`gpu-svg-camera-present-bench 3`은 기존 읽기 포함 검사와 같은 원본2종×배율5종을 사용하되 측정 프레임에서는 CPU 픽셀을 읽지 않습니다. 각 조건의 측정 뒤 두 카메라 위치의 전체RGBA를 검사합니다. 최종3회 교대 실행의1920개 표시와60쌍 출력이 정상이며 렌더 호출 시간은 조건별33.4~65.9% 감소했습니다. 이는 대기·제출을 포함한 호출 시간이고 앱FPS·입력부터표시 지연이 아닙니다. GPU 시간은 앞선 프레임 슬롯입니다.

이번 비교의 완료 버퍼 캐시는 논리 최대약63.3MiB였습니다. 일부 조건의 관리 스레드 할당량은 메타데이터 때문에352~832바이트/프레임 증가했으므로 메모리 감소로 설명하지 않습니다. 프로세스Private·작업 집합의 표본과 캐시 논리 바이트를 따로 기록하며 총VRAM은 미측정입니다. 기존 작업 한도 초과는 계속 명시적으로 실패합니다.

반복 실행은 `tests/Duxel.Graphics.Tests/RunSvgCameraComparison.ps1`의 `BaselineExecutable`, `CandidateExecutable`, `OutputDirectory` 매개변수를 해시테이블로 전달합니다. 입력 실행물·DLL·원본 해시를 실행 전후 확인하고3회 교대 실행의stdout/stderr·프로세스메모리·PID·종료 상태를 따로 남깁니다. 빌드를 자동 시작하지 않습니다.

## 갤러리 브라우저 비교 확대 성능

11월 17일 갤러리 변경에서 브라우저 비교 예제 27개에 조작 값을 반영하는 C# 소스를 붙였습니다. 기본 비교 화면에서는 확대 값을 바꿀 때 두 소스를 다시 만들고 구문을 분석했습니다. Windows Vulkan에서 .NET 11.0.0 관리형 실행과 V-Sync 끄기를 유지하고 확대 프레임을 40개씩 두 번 비교했습니다. 변경 전에는 실행마다 C# 구문 분석이 78회 늘었습니다. 측정 중 SVG XML 해석, GPU 장면 준비, CPU 비트맵 생성 횟수는 그대로였습니다.

확대·회전·여백·모서리 값은 미리보기에 즉시 반영하고, 소스는 조작이 끝나면 갱신합니다. 그래픽 검증기의 `gallery-svg-profile`은 실제 갤러리 화면으로 이 동작을 재현합니다. 두 비교에서 화면 구성 시간의 중앙값은 26.20~27.82ms에서 3.22~3.60ms로 86~88% 줄었습니다. 호출 스레드의 프레임당 할당량 중앙값은 10,343,740바이트에서 327,552바이트로 줄었습니다. 렌더러 호출 시간 중앙값은 수정 전 6.01~6.52ms, 수정 후 6.37~6.93ms였습니다. 조작 종료 뒤 두 소스와 두 벡터 미리보기가 같은 확대 값을 표시하는 것도 확인했습니다. 이 수치는 갤러리 한 화면의 관리형 진단 결과이며 앱의 실제 FPS와 NativeAOT 성능은 미측정입니다.

## 빠르게 확대·회전할 때의 미리보기

`UiSvg.UseFixedResolutionPreview`는 사용할 때만 켭니다. 조작 중에는 기본 해상도로 만든 GPU 텍스처를 사용합니다. 확대·회전·클립·색 곱셈·모서리 처리는 위젯의 기존 경로를 따릅니다. 갤러리 브라우저 비교에서는 조작이 끝나고 대기 시간이 지나면 이 옵션을 끄고, 최종 배율로 화면에 보이는 영역을 그립니다. 확대하는 동안에는 텍스처를 보간하므로 잠시 부드럽게 보일 수 있습니다. XML, GPU 장면 데이터, 변환에 따른 배치는 그대로 유지합니다.

RTX 5070 Ti Laptop GPU에서 Windows 관리형 .NET 11 Vulkan으로 1280×900, Immediate 표시 모드, 프레임 슬롯 3개, 조작별 측정 프레임 40개를 사용해 두 번 대조했습니다. 변경 전 프레임당 SVG 계산 명령은 중앙값 기준으로 확대 7회, 확대와 회전 9회였습니다. 변경 후 미리보기를 준비한 뒤에는 두 경우 모두 0회였습니다. 렌더러 호출 시간 중앙값은 확대에서 6.04~6.13ms에서 0.83~0.86ms로, 확대와 회전에서 5.99~6.14ms에서 0.86~0.89ms로 줄었습니다. 배율을 2.5배로 고정하고 회전만 하는 경우에는 변경 전부터 출력을 재사용했습니다. 렌더러 호출 시간에는 화면 제출이 포함되며 입력부터 화면 표시까지의 지연과 NativeAOT 성능은 측정하지 않았습니다.

변환 슬라이더를 누른 채 잠시 멈춰도 조작용 미리보기를 유지합니다. 포인터 캡처가 끝나면 조작 종료 조건에 따라 최종 해상도로 돌아갑니다. 회전한 SVG는 상위 클립들과 실제로 겹치는 부분을 구한 뒤 GPU 출력 크기를 정합니다. 카메라의 가상 전체 크기와 텍스처 샘플링 여유 1픽셀은 유지합니다.

WSLg lavapipe에서 소프트웨어 작업 스레드 4개를 사용해 Linux x64 NativeAOT 두 쌍을 비교했습니다. 2배 확대·37도 회전의 최종 QueuePresent 호출은 1,029.9~1,053.0ms에서 284.8~291.9ms로 줄었습니다. 회전 없는 2배·4배 확대는 측정 범위가 겹치고, 여전히 느립니다. 최종 화면끼리 비교했을 때 회전 가장자리의 9픽셀은 8비트 색 채널에서 최대 1만큼 달랐습니다. 기본 변환은 상단 진단 표시를 제외하고 일치했습니다. 이 시간은 입력부터 화면까지 걸린 시간을 뜻하지 않습니다.

## 고배율 확대에서 블러 처리 시간

`DUXEL_VK_PROFILE=1`과 `DUXEL_VK_GPU_PROFILE=1`을 함께 켜면 완료된 제출 번호와 벡터 처리 시간, 첫 블러 시간을 확인할 수 있습니다. 블러 셰이더는 작업 그룹마다 가중치를 한 번 정규화하고, 가로·세로 블러는 유효한 픽셀 범위만 순서대로 읽습니다. WSLg lavapipe의 Linux x64 .NET 11 NativeAOT에서 두 번씩 비교한 결과, 4배 확대의 최종 QueuePresent 호출은 698.3~705.1ms에서 542.6~544.1ms로 줄었습니다. 첫 블러는 409.0~411.5ms에서 263.1~266.8ms로 줄었습니다. Windows GPU에서 수정 전후 RGBA 출력 20개가 일치했습니다. Chromium과의 전체 픽셀 일치와 실제 GPU의 입력 지연은 별도 확인이 필요합니다.

곡선의 제어점 범위를 확인해 채우기 결과에 영향을 줄 수 없는 샘플은 곡선 탐색을 건너뜁니다. 해상도를 유지한 채 Linux x64 .NET 11 NativeAOT와 WSLg lavapipe에서 비교한 결과, 최종 QueuePresent 호출은 2배에서 547.0~555.5ms에서 420.4~428.2ms로, 4배에서 536.8~545.0ms에서 417.4~418.7ms로 줄었습니다. Windows GPU의 수정 전후 출력 35쌍은 모두 일치했습니다. 같은 입력에 대한 렌더러 변경 전후 비교이며 Chromium과의 완전 일치와 실제 GPU의 입력 지연은 별도 검증이 필요합니다.

브라우저 비교의 기본 모드는 브라우저 기준 해상도로 그려 둔 GPU 이미지를 재사용합니다. 확대·회전·스크롤을 멈춘 뒤에도 같은 이미지를 씁니다. 최종 배율의 픽셀을 비교하려면 **확대 시 고해상도로 다시 그리기**를 켜세요. 조작이 120ms 동안 없으면 보이는 범위를 최종 해상도로 그립니다. C# 예제는 변환이 끝난 뒤 갱신합니다. [전후 측정](optimization-session-2026-11-19.md)

Vulkan 셰이더는 경로의 네 표본을 먼저 검사합니다. 네 표본 모두 경로 밖이면 색 계산과 합성을 건너뜁니다. RTX 5070 Ti Laptop GPU에서는 카메라 조건 10개 중 9개의 GPU 시간이 조금 줄었습니다. 소프트웨어 Vulkan에서는 반복해서 빨라지는 결과를 얻지 못했습니다. [출력 비교와 시간 측정](optimization-session-2026-11-19.md#svg-material-evaluation-after-coverage)

고해상도 모드의 120ms는 슬라이더에서 손을 뗀 시점부터 셉니다. `gallery-svg-pointer`는 두 모드의 갤러리 슬라이더 입력과 렌더 요청을 각각 `pointer.tsv`에 남깁니다. [슬라이더 검증](optimization-session-2026-11-19.md#svg-slider-release-and-consecutive-gestures)

가로·세로 방향의 가우시안 블러는 원본을 충분히 덮는 사각형 안에서만 입력 픽셀을 읽습니다. 첫 번째 방향의 계산이 끝나면 그 방향으로 블러 반경만큼 사각형을 넓힙니다. 검사한 화면에서 수정 전후의 픽셀이 같았습니다. [블러 출력과 시간 비교](optimization-session-2026-11-19.md#svg-blur-skips-transparent-input)

SVG 경로의 표본을 검사할 때 픽셀에 대해 이미 계산한 카메라 값을 재사용합니다. 성능을 비교하기 전에 로그에서 Vulkan 장치 종류를 확인하세요. 이번 Ubuntu WSL에서는 드라이버를 따로 지정하지 않고 글꼴을 자동으로 선택해 실행했을 때도 CPU에서 동작하는 `llvmpipe`가 선택됐습니다. [장치와 시간 측정](optimization-session-2026-11-19.md#svg-path-coverage-reuses-camera-parameters)

`DUXEL_VK_PROFILE=1`, `DUXEL_VK_GPU_PROFILE=1`, `DUXEL_VK_PROFILE_EVERY=1`을 켜면 `gpuSubmission`으로 완료된 프레임의 시간을 찾을 수 있습니다. 첫 블러 시간에 더해 `vectorBlurFilters`에 필터 수, `vectorBlurGpuUs`에 블러 합계, `vectorOutsideBlurGpuUs`에 경로 처리와 복사 등 나머지 벡터 작업 시간을 표시합니다. 한 프레임에 필터가 16개를 넘으면 개수와 첫 블러 시간만 기록하고 두 합계는 미확인으로 표시합니다. [갤러리 필터 시간 측정](optimization-session-2026-11-19.md#svg-filter-timing-by-submission)

`vectorPathGpuUs`는 벡터 이미지를 그리는 연산 시간입니다. `vectorCopyGpuUs`는 장면 데이터, 의존성 테이블, 최종 텍스처를 복사하는 시간입니다. 테두리 준비나 동기화처럼 이 항목에 들지 않는 작업도 있습니다. 두 항목은 각각 64건을 넘기면 개수만 기록하고 시간 합계는 미확인으로 표시합니다. [갤러리 경로와 복사 시간](optimization-session-2026-11-19.md#svg-path-dispatch-and-transfer-timing)
