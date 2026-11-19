# 공통 레이어·위젯 최적화 평가와 개선

## 실제 고부하 앱과 명령 구간 누적 후보 — 2026-11-06

현재 소스의 Overlay 앱을 새 기준으로 고정했다. 이 앱은 메뉴 텍스트5종을 재사용하므로 앞선2048개 텍스트 캐시 진단의 Private 결과와 같은 조건이 아니다. 정지 장면에서 실제 draw·동적 업로드는0인데 UI Render가 약5~9ms,GPU는2.46µs인 진단을 얻었다. 소스에서는32768개 색상 사각형 등이 같은 불변 명령 전체를 반복 복사한다. 공통 빌더가 연속 명령의 개수·범위를 값 필드에 누적하고,다른 명령 접근·저장소 교체 전에 확정하도록 변경했다. 공개 명령의 불변성·입력·색상·호출 수·회수·생성기 연결은 유지한다. 빌더당 추가 값 필드4개의 비용과 실행 중 분기도 존재한다.

고정 RC3 SDK11.0.100-rc.3.26522.148/runtime11.0.0-rc.3.26522.148,win-x64 NativeAOT Speed.128팝업×256색상·배경 사각형1800개,실제1600×1000,MSAA1,VSync off,pipeline on,idle skip off. 최소96표시·1초 준비 후12초 측정,정지/이동 각각 독립3회씩 AB/BA/AB로 총12실행했다. 기존 `measure-overlay-layers.ps1`·앱 프로파일·250ms 메모리 수집을 재사용했으며 강제GC와 품질·부하 감소는 없다.

| 지표 | 정지 이전→후보 | 이동 이전→후보 |
| --- | ---: | ---: |
| FPS | 139.784→153.782 | 115.081→142.446 |
| 프레임 중앙값 ms | 7.0353→6.0980 | 7.0032→6.7267 |
| p95 ms | 8.2064→7.50848 | 10.52866→7.9197 |
| p99 ms | 9.272276→8.944864 | 66.191211→9.977248 |
| 1% low FPS | 53.532→47.081 | 13.528→51.340 |
| Private B | 180,019,200→180,150,272 | 182,091,776→180,436,992 |
| 작업 집합 B | 132,579,328→132,567,040 | 132,833,280→133,210,112 |
| GPU µs | 3.5683→3.7346 | 27.8728→29.3575 |

**위 시간·메모리 지표는 모두 실행별 범위가 겹쳐 개선 확정이 아니다.** 정지1% low 중앙값은12.05% 낮았고 이동 작업 집합은0.284% 높았다. GPU 중앙값도4.66%/5.33% 높았으나 모두 미확정이다. 같은 이름의 과거11/04 측정과 합산하거나 당시 FPS와 비교하여 새 회귀·개선율을 만들지 않는다. CPU 프로파일은 준비 구간을 포함한120프레임 간격 표본이며 프로세스 CPU 시간이나 정확한 측정 구간 회계가 아니다.

양쪽 모두 정지 캐시128재사용/0재구성·draw0/업로드0,이동127재사용/1재구성·업로드66,512B/프레임이다. 이동 draw 중앙값137.537→136.634의 차이도 범위가 겹친다. 샘플 콜백 내부 할당은 정지0·이동608B로 같으며 엔진 전체 할당 수치는 아니다. 두 실행 파일 모두 앞선 텍스트 캐시 수정을 이미 포함하므로 이 비교로2048키 진단의 Private 증가를 해소했다고 판단하지 않는다.

관리/Native 집중74개씩 통과하고 GPU 이미지100개가 모두 일치한다. 연속 개수·범위,clip/callback 경계,채널 전환,대기 프레임 불변성,초기화,그라데이션 MSAA1/4,팝업·텍스트·참조 수명 등을 포함한다. 공식 Fluent 참조 화면이나 데스크톱 캡처 검증은 아니다. 최초 새 검사 코드의 채널 API 이름·해제 방식 오류7건은 실제 API와 명시적인 소유권 해제로 고쳤고 기준을 완화하지 않았다.

근거:`artifacts/highload-app-2026-11-06/verification.json`,`comparison.json`,`cpu-diagnostics.json`. 원본·소스 사본·해시·12실행 기록:`L:/Cache/Duxel/highload-app-2026-11-06/`. 제품 소스의 전후 차이는 UiDrawListBuilder.cs 한 파일이다. 기존 디버그 심볼20개를 NTFS 무손실 압축해 공간을 확보했고 내용 해시는 모두 보존했다. 파일 삭제·이동은 없다. 전체 메모리·성능 목표는 미완료이며 창 리사이즈는 별도 보류한다. .NET11만 개발 검증한다.

평가일: 2026-11-04. 평가·개선·재검증 작업의 완료와 모든 장치에서의 성능 목표 달성은 구분한다.

## 판정

**방향은 목표에 부합하며 Windows 공통 경로의 효과를 확인했다. CPU 구성의 자동 생략과 라즈베리파이 실기기 성능까지 완료된 상태는 아니다.** 이전 최적화를 보존하고, 이번 평가에서 발견한 불필요한 GPU 업로드와 CPU primitive 명령 복사를 공통 엔진에서 개선했다.

| 목표 | 판정 | 근거와 한계 |
| --- | --- | --- |
| 기본 위젯 자동 적용 | 충족 | 일반 위젯도 공통 렌더러의 변경 판정과 부분 갱신을 사용한다. 별도 BeginLayer가 없어도 정지 draw=0이다. |
| 정적·동적 표시 레이어 재사용 | 충족 | 메뉴·팝업·콤보·툴팁·foreground의 CPU 스냅샷·GPU 상주 기하와 동적 복귀를 공통 경로에서 처리한다. |
| 기본 위젯 CPU 구성 전체 생략 | 부분 충족 | 명시적 ContentVersion/StaticCache와 앱 유휴 대기는 존재한다. 요청된 UI 프레임의 임의 사용자 Render·입력·레이아웃을 자동으로 건너뛰지는 않는다. |
| 바뀐 영역만 정확하게 갱신 | 충족(지원 범위) | 이미지별 이력, MSAA1/4, 이동·삭제·반투명·명령/clip/texture 변경 및 비동기 갱신 주기 검사 통과. 일반 투영3D·callback·시뮬레이션 payload는 전체 갱신한다. |
| 업로드·CPU 중복 작업 감소 | 충족 | 정지 위젯5종의 streamed upload가0B가 됐다. rectangle/circle/rounded primitive의 현재 명령을 ref로 처리한다. 샘플·셰이더·부하는 그대로다. |
| 전체 처리 성능 | 부분 충족 | 정지 FPS·median·p95 개선과 이동 p95 개선은 확인했다. 이동 평균·p99·1% low는 실행 범위가 겹친다. |
| 메모리 | 부분 충족 | 이전 단계의 Private 감소는 기존 원본으로 재검증했다. 이번 변경의 Private 감소는 미확정이며 정지 작업 집합은 약0.50MiB 증가했다. |
| 라즈베리파이 | 미검증 | 현재 제공 backend와 실행은 Windows x64다. Linux/ARM64 backend·드라이버·실기기 측정을 증명하지 않는다. |

여기서 UI 기하 캐시·GPU 상주 캐시·이미지 이력은 서로 다른 비용을 줄인다. Vulkan VkPipelineCache의 opaque 내용은 드라이버가 관리하고 Duxel은 영구 저장을 담당한다. 이를 이번 위젯 캐시와 혼동하지 않는다.

## 검증에서 발견한 부족한 부분과 수정

정지 일반 위젯은 draw=0인데도 streamed 기하를 매 프레임 업로드했다. 초기 진단의 화면별 값은 radio2776B, button5296B, checkbox4408B, slider10320B, input6384B였다. 변경 판정이 업로드 뒤에 있었기 때문이다.

변경 판정을 업로드·동적 용량 확장보다 앞에 배치하고, 변경이 빈 경우 streamed 업로드·파이프라인 bind·compiled 계획·draw 순회를 생략한다. 정적 캐시 수명 관리, 입력·레이아웃, texture 무효화, acquire·submit·present와 MSAA 보존은 유지한다. 투영3D·callback 등은 기존 전체 갱신 조건을 유지한다. 화면 전체를 덮는 bitmap 캐시나 품질 저하로 대체하지 않았다.

첫 후보는 업로드57632→0B를 달성했지만 정지 FPS382.07→374.21(-2.06%)로 하락했다. 이 결과는 `comparison/summary.json`과 `iteration-1-review.md`에 보존했다. UI 구성이 지배적인 조건에서 `AddRectFilledPrimitiveCommand`의 큰 명령 구조체를 값으로 읽고 다시 쓰던 경로를 ref 접근으로 바꿨다. 아래 수치는 두 변경을 포함한 최종 후보의 결과다. 기계어 수준의 복사 제거량이나 단일 수정의 독립 개선율로 확대하지 않는다.

## 최종 동일 조건 측정

SDK11.0.100-rc.3.26522.148/runtime11.0.0-rc.3.26522.148, win-x64 NativeAOT Speed. 1600×1000, MSAA1, VSync off, render pipeline on, idle skip off, 128 palettes×256 swatches. 최소96 accepted presentations와1초 워밍업 후12초씩 독립3회 교대(before/after, after/before, before/after). 두 실행 파일에 app profile120프레임·GC 진단을 동일하게 적용했고 강제GC는 하지 않았다. FPS는 accepted presentation 간격이며 물리 모니터 주사율이 아니다.

기준은 FPS 깜빡임 수정까지 포함한 이번 평가 시작 소스다. 이전 통합 전 실행 파일과의 옛 측정값에 새 개선율을 합산하지 않는다. 첫 후보12실행과 최종 후보12실행은 별도 원본으로 보존했다.

### 정지

| 지표 | 이전 | 이후 | 관측 변화 | 판정 |
| --- | ---: | ---: | ---: | --- |
| FPS | 381.678 | 403.149 | 5.63% | 개선 확인 |
| median ms | 2.568 | 2.423 | -5.64% | 개선 확인 |
| p95 ms | 3.012 | 2.860 | -5.03% | 개선 확인 |
| p99 ms | 4.182 | 3.945 | -5.66% | 미확정(범위 겹침) |
| 1% low FPS | 216.333 | 227.360 | 5.10% | 미확정(범위 겹침) |
| draw calls | 0.000 | 0.000 | — | 동일 |
| dynamic upload B/frame | 57632 | 0 | -100.00% | 개선 확인 |
| GPU µs | 4.149 | 4.799 | 15.67% | 미확정(범위 겹침) |
| Private MiB | 172.926 | 172.152 | -0.45% | 미확정(범위 겹침) |
| Working set MiB | 123.023 | 123.520 | 0.40% | 증가/악화 확인 |

### 이동

| 지표 | 이전 | 이후 | 관측 변화 | 판정 |
| --- | ---: | ---: | ---: | --- |
| FPS | 372.826 | 395.349 | 6.04% | 미확정(범위 겹침) |
| median ms | 2.677 | 2.468 | -7.81% | 미확정(범위 겹침) |
| p95 ms | 2.996 | 2.887 | -3.64% | 개선 확인 |
| p99 ms | 3.941 | 3.857 | -2.11% | 미확정(범위 겹침) |
| 1% low FPS | 220.256 | 219.432 | -0.37% | 미확정(범위 겹침) |
| draw calls | 137.173 | 137.133 | -0.03% | 미확정(범위 겹침) |
| dynamic upload B/frame | 66512 | 66512 | 0.00% | 동일 |
| GPU µs | 35.859 | 34.727 | -3.16% | 미확정(범위 겹침) |
| Private MiB | 175.543 | 175.551 | 0.00% | 미확정(범위 겹침) |
| Working set MiB | 124.629 | 124.977 | 0.28% | 미확정(범위 겹침) |

전체 범위·원본 프레임 CSV·메모리 CSV·재계산 감사는 `artifacts/optimization-evaluation-2026-11-04/comparison-ref/`에 있다. 정지 작업 집합 증가를 숨기지 않으며 Private 감소나 이동 평균 FPS 개선을 확정하지 않는다. 추가 캐시·화면 사본은 도입하지 않았다.

이전 단계의12초 raw 데이터도 기존 요약기로 별도 재계산했다(`reused-performance/summary.json`). 당시 Private 감소는 정지2.17%·이동3.02%, draw는1031→0/137.262였다. 이는 과거 단계의 결과이며 현재 후보의 수치와 합산하지 않는다.

## CPU·할당·메모리 해석

샘플링된 app profile의 실행별 median을 다시3회 median으로 비교하면 UI render는 정지2525.95→2412.80µs, 이동2672.50→2401.25µs였다. renderer는 정지570.20→517.80µs, 이동664.80→592.90µs였다. 정확한 측정 phase 경계와 일치하는 CPU 회계가 아니므로 원인 구분용 진단으로만 사용한다(`cpu-diagnostics-ref.json`). 변경 판정은 이제 record 대신 geometry/upload 준비 시간에 포함되므로 해당 두 phase의 단독 전후 비교로 개선을 주장하지 않는다.

UI frame600 이후의 관측한 profile 끝점 사이에서는 각 세대 GC 횟수 증가가 없었다. 이는 앱 전체의 GC 없음이나 할당0을 뜻하지 않는다. 팔레트 벤치의 Screen.Render 할당은0이지만 일반 종합 샘플의 UI 구성에는 화면별 수KiB 할당이 남았다. Private·작업 집합·관리 힙·업로드 대역폭을 같은 지표로 취급하지 않는다. 다음 CPU 최적화도 사용자 Render 부작용과 입력 계약을 보존하는 범위에서 측정해야 한다.

GPU VRAM 전체 상주량은 이번 비교에서 측정하지 않았다. 업로드0B가 기존 GPU 버퍼의 해제를 뜻하지도 않는다. 같은 시간 동안 더 많은 프레임을 처리한 상태의 작업 집합 차이는 그대로 기록하며, 이를 누수나 캐시 크기 감소의 증거로 확대하지 않는다.

라즈베리파이 적합성은 메모리 대역폭 감소만으로 판정할 수 없다. 현재 렌더러는 buffer device address, descriptor indexing/update-after-bind, dynamic rendering을 요청하고4096개 texture descriptor를 구성한다. 실제 대상 드라이버의 feature·limit 확인과 Linux/ARM64 platform/text backend 검증이 필요하다. 해당 장치가 이를 지원하거나 지원하지 않는다고 이번 Windows 결과로 단정하지 않는다.

## 정확성과 근거 재사용

- .NET10.0.9와 고정 RC3 각각 전체717검사 통과. Release net8/net9/net10 솔루션 빌드 경고0·오류0.
- 실제 종합 샘플 위젯·일반 child scroll·네이티브 resize 집중 검사: 두 런타임 각각70개 통과. scroll0→211.6875와 framebuffer1100×740→884×641에서 full repaint 무효화를 확인했다. 물리 마우스 자동화 대신 UiInputState 입력 경계를 사용했다.
- FPS16프레임/NF3프레임 갱신, button 유지, 128개 동기·128개 in-flight 픽셀 검사386개 통과. 기본 위젯의 geometry/command 변경 회귀를 유지했다.
- 이전 .NET10 전체 검사의 이미지317개가 모두 동일하다. 37개 창·6개 상호작용도 이번 엔진 변경 뒤 통과했으며 이후 ref 변경은 전체 기하 회귀와 이미지 동치로 확인했다.
- clip만 바뀐 경우 empty 상태를 벗어나 업로드를 재개하고 전체 갱신과 같은 픽셀을 내는 검사 추가. readback fixture는 resize 후 새 framebuffer 크기를 먼저 확정하여 올바른 용량을 할당한다.
- 기존 runtime·shader·sample의 관련 없는 변경을 보존했다. 이번 runtime 수정6개와 집중 검사4개 외 초기 소스 변경 여부는 최종 감사에 기록한다. 기존 보호·거부 대상의 삭제를 재시도하지 않았으며 기존 거부22개 대상과 직접 정리 절차는 [정리 안내](rejected-build-cleanup.ko.md)를 유지한다.

## 실행과 추적

전체 근거: `artifacts/optimization-evaluation-2026-11-04/`. 계획·중간 실패·원본 stdout/stderr·GC 진단·원본 통계와 해시를 보존한다. 평가 항목의18/18 완료는 위 판정표의 모든 목표가 충족됐다는 뜻이 아니다.

최종 실행 파일: `artifacts/optimization-evaluation-2026-11-04/native-ref-rc3/net11.0-windows7.0/overlay_layer_lab.exe`.
SHA256: `A7C5994C1A8B7E31F511DFA9E3409C90190923E2CCF22E11EF2AF8711C9354F1`.
평가 시작 실행 파일 SHA256: `3B5C4C972CD17936E0E9D28177AA122662B51F41EF3B420B6322F4A169EFA465`.

```powershell
& 'C:/Users/dimohy/.cache/duxel-dotnet-10.0.301/dotnet.exe' run --project tests/Duxel.Rendering.Tests -c Release -- artifacts/optimization-recheck-net10

$fixture = (Select-String -LiteralPath artifacts/all-features-2026-11-04/publish-command-key.stdout.log -Pattern 'all_features.cs -> (.+all_features.dll)').Matches.Groups[1].Value
& 'C:/Users/dimohy/.cache/duxel-dotnet-10.0.301/dotnet.exe' run --project tests/Duxel.Rendering.Tests -c Release -- artifacts/optimization-recheck-widgets --widget-optimization-only $fixture 'W:/MyWorks/Duxel/artifacts/all-features-2026-11-04/plan.json'

$measure = @{
    BeforeExecutable = 'W:/MyWorks/Duxel/artifacts/optimization-evaluation-2026-11-04/native-rc3/net11.0-windows7.0/overlay_layer_lab.exe'
    AfterExecutable = 'W:/MyWorks/Duxel/artifacts/optimization-evaluation-2026-11-04/native-ref-rc3/net11.0-windows7.0/overlay_layer_lab.exe'
    OutDir = 'W:/MyWorks/Duxel/artifacts/optimization-recheck-comparison'
    Runs = 3; Seconds = 12; Counts = @(128); Swatches = 256; Scenarios = @('idle','motion')
}
$env:DUXEL_APP_PROFILE='1'; $env:DUXEL_APP_PROFILE_EVERY='120'; $env:DUXEL_APP_PROFILE_MEMORY='1'
try { ./scripts/measure-overlay-layers.ps1 @measure }
finally { Remove-Item Env:DUXEL_APP_PROFILE; Remove-Item Env:DUXEL_APP_PROFILE_EVERY; Remove-Item Env:DUXEL_APP_PROFILE_MEMORY }
```

fixture IL이 정리됐다면 표준 `./run-fba.ps1 samples/fba/all_features.cs -NoCache -NoLaunch`로 로컬 fixture를 만들고 새 publish 로그의 경로를 사용한다. RC3 재게시·검사는 작업 폴더의 `toolchain/global.json`으로 SDK를 고정하여 수행하며 저장소 기본 global.json은 바꾸지 않는다. 원본 publish 로그와 comparison-contract.json을 함께 확인한다. 이전 all_features.NET10 실행 파일의 해시는 해당 이전 검증의 것으로, 이번 최종 RC3 overlay 실행 파일과 혼동하지 않는다.
