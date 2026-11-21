# 갤러리 코드와 시작 시간

## 갤러리 코드와 시작 시간

가설: 모든 소스 뷰어를 미리 만들고 개요에 전체 구현 파일을 표시하면서 첫 프레임의 텍스트 배치 비용이 커졌습니다. 페이지 틀을 지연 생성하고 정보 페이지의 소스를 생략했으며, 코드의 어휘 분석을 캐시하고 네이티브 문단 배치를 사용합니다. 과거 단일 실행은 전체 글꼴 503.10ms, 첫 표시 3009.41ms였습니다. 최신 실행은 렌더러 376.74ms, 전체 글꼴 400.55ms, 첫 표시 720.36ms입니다. 서로 다른 갤러리 버전이며 기준 실행이 한 번뿐이므로 개선율, median/p95/p99, 1% low와 고정 RC3 NativeAOT 성능은 미검증입니다. 검증 명령은 ./run-gallery.ps1 -Verify -VerificationOutput <새 디렉터리> -NoRestore이며 근거는 artifacts/gallery-source-2026-11-17/verification.json입니다. 개요 구성이 바뀌어 부하가 동일하지 않습니다. Vulkan 인스턴스 준비와 실제 리사이즈 성능은 별도 측정이 필요합니다.

## 전체 코드·리사이즈와 텍스트 재사용

가설: 스크롤바 판단의 두 너비가 측정 캐시를 교체하고 512개 콜드 예산이 보이는 글자까지 제거합니다. 리사이즈는 같은 글리프를 새 문단 객체로 취급하고 작은 GPU 이미지의 메모리 해제를 반복합니다.

변경: 두 너비 캐시, 측정·그리기 배치 공유, 하드 줄바꿈별 네이티브 스냅샷, 원문 인덱스·정수 이동과 독립적인 정확한 글리프 래스터 키, 현재 그린 작업 집합 보호, 기존 펜스 퇴역 후 작은 글자 텍스처 풀을 적용했습니다. GPU 풀은 2,048개·실제 할당 16MiB 상한이며 디스크립터 압력과 종료 시 회수합니다. 보간 문자열의 본문·구분자·내부 식·이스케이프·메서드·연산자를 구분하고 원문 변경 때만 분석합니다.

.NET SDK11.0.100/runtime11.0.0 Windows 관리 실행 진단입니다. 1280×900 코드 구성 독립3회 전후 비교에서 내부 UI 평균17.323→1.204ms, 프레임 할당6,102,916→37,887B, 측정 재생성은768프레임당1536→0입니다. 전체 코드1654×1040의 별도3+3 교대 비교는 제출 평균의 실행 중앙값18.460→0.358ms, 제시 횟수/측정 벽시계 비율의 실행 중앙값47.02→243.58/s입니다. 배너의 평활 FPS와 다른 지표이며 각 비교의 소스 해시·배율·옵션이 일치합니다.

리사이즈: 동일 하네스 DLL, 명시적인0.8초 워밍업, 각3개 독립 프로세스 교대, 프로세스당3회×48개 동기 네이티브 크기 변경입니다. 구간 평균→프로세스 중앙값→3회 중앙값을 사용했습니다. 크기 적용 대기86.563→32.739ms, 62.18% 감소입니다. 이전753–758은 명시적 뷰포트 워밍업이 없어 정식 비교에서 제외했습니다. 기존 최대 텍스처 퇴역59.77ms가 풀 적용 진단747에서 평균0.004ms로 줄었고, 유휴 풀은1,071개·2,702,848B였습니다.

리스크: 구간 종료 시 Private Bytes 최댓값의 실행 중앙값400,228,352→459,882,496B(+56.89MiB)입니다. 작업 집합이나 연속 피크가 아니며 관리 캐시·JIT·네이티브 메모리의 기여도는 미분해입니다. 리사이즈 대기는 아직16.7ms를 넘습니다. 실제 앱 프레임의 median/p95/p99·1% low, 실제 Linux와 고정 RC3 NativeAOT 성능 수용은 미검증입니다. 원시 frames.csv의 내부 UI 백분위와 전체 프레임 지표를 혼동하지 않습니다.

검증: 코드 캡처 픽셀 전후 완전 일치, 네이티브 커서·선택18문단/5,273범위 정확히 일치. 갤러리843개, 공통4,642개, 텍스트 캐시35개, 리사이즈 픽셀39개, 종료·최소화11개 통과. tests/managed-validation에서 publish-760/Duxel.Graphics.Tests.exe <새 출력>에 gallery-resize, code-view-full-bench, observer-only, widgets-no-resize, directtext-cache, resize-coherence, session-failure-only 모드를 전달합니다. 근거는 artifacts/gallery-source-2026-11-17의 code-layout-comparison.json, full-source-comparison.json, resize-comparison.json, text-equivalence.json, verification.json입니다.

## 배치 통합 소스 상태 동기화의 반복 할당 제거

- 가설·범위 : 동일 상태를 비교하기 전에 카운터·체크·라디오 설정 문자열을 매번 만들고 있었다. 실제 IGallerySample.SynchronizeSource 호출만 분리해 측정한다. 전체 프레임·렌더러·호출자 열거·다른 스레드는 제외한다.
- 변경 : TabOverflowExamples/OverflowExamples의 읽기 스냅샷을 정수·불리언·선택값 튜플로 유지하고, 설정 문자열은 상태가 바뀐 뒤 포매터에서만 만든다. 기존 GallerySample.BindSource 경로를 재사용한다.
- 실행 : managed-validation의 SDK 11.0.100 / runtime 11.0.0, Release. 같은 gallery-samples-only 경로와 동일 입력 순서. 게시본1020(전)/1022(후). 각 새 프로세스에서 8개 소스를 1000회 워밍업하고, 다시 1000회(8000호출) 동기화한 현재 스레드의 GC.GetAllocatedBytesForCurrentThread 차이를 기록했다. 전후 각각 독립3회.
- 전 : 2,360,000 / 2,360,000 / 2,360,000 B. 후 : 0 / 0 / 0 B. 중앙값 2,360,000 → 0 B, 이 구간의 관리 할당 100.00% 감소. 입력·상태·소스 동기화 기능 검사도 모두 통과했다.
- 근거 : W:/Cache/Duxel/gallery-source-20261117/sync1021a.stdout.log, sync1021b.stdout.log, sync1021c.stdout.log 및 sync1023a.stdout.log, sync1023b.stdout.log, sync1023c.stdout.log. INTEGRATION_SOURCE_SYNC_ALLOCATION 행. 원장 : artifacts/gallery-integration-source-2026-11-17/allocation.json.
- 제한·리스크 : FPS, CPU 시간, median/p95/p99 프레임 시간, 1% low, 전체 프로세스 메모리와 GPU/VRAM은 측정하지 않았다. 모두 N/A이며 전체 앱의 속도나 메모리 개선율로 확장하지 않는다. 관리형 Windows 진단으로, 고정 RC3 NativeAOT 및 Linux 실제 실행의 성능 근거가 아니다. 상태가 바뀌어 소스를 새로 만드는 비용은 제외한다. 현재 경로의 무변경 동기화 0 B 검사를 회귀 검사에 유지한다.

## 측정 재사용 소스 동기화의 관리 실행 검증과 한정된 할당 측정

- 변경: 실제 계측 선언을 재사용하는 두 소스를 연결하고 현재 값·설정만 비교합니다. 측정 횟수·진행률 읽기 횟수는 소스 상태에서 제외하며 기존 측정 오버라이드와 패널 크기를 유지합니다.
- 검증: 공개한 Graphics.Tests의 gallery-samples-only 실행 measurement1074·measurement1078과 observer-only 실행 gallery1075에서 실제 SynchronizeSource 두 개를 1000회 워밍업 후 1000회씩 호출했습니다. 측정 호출은 실행당 2000회입니다.
- 환경: SDK11.0.100·runtime11.0.0, 관리 Release 실행. 강제 GC 없이 현재 스레드의 관리 할당을 측정했습니다. 서로 다른 프로세스 세 번 모두 0바이트입니다. 이전 페이지에는 예제별 소스 동기화 경로가 없어 Before·개선율은 N/A입니다. 전체 프레임·FPS·CPU 시간·RSS·VRAM·AOT·Linux 속도 개선을 주장하지 않습니다.
- 한계: 전체 UI 프레임, 값 변경 후 포맷, 다른 스레드와 전체 메모리는 포함하지 않습니다. 기존 측정·입력·메뉴·탭·Tooltip 계약과 실제 GPU 동작을 함께 검사했습니다.
- 근거: [결과](../../../artifacts/gallery-measurement-source-2026-11-17/result.json), [할당](../../../artifacts/gallery-measurement-source-2026-11-17/allocation.json).

| 항목 | Before | After | 개선율 |
| --- | --- | --- | --- |
| 변하지 않는 소스 동기화 2000호출의 현재 스레드 관리 할당 | N/A | 0/0/0바이트 | N/A |

## 스크롤된 탭 경계

가설: 스크롤을 반영한 커서에 남은 배치 높이를 다시 적용해 부모 탭의 접근성 높이가 0이 됩니다. 변경: 기존 탭·Fluent 탭·탐색 페이지의 클립을 실제 콘텐츠 끝 좌표로 공통 계산합니다. 검증: 이전 Core 대조는 GPU 가시성 및 실제 갤러리 부모 경계 검사 실패, 수정본은 GPU 42건·실제 갤러리 집중 31건·공통 4642건·갤러리 전체 1715건 통과입니다. 속도 개선을 측정한 변경이 아니므로 FPS 개선율은 주장하지 않습니다. 근거: artifacts/tab-panel-scroll-clip-2026-11-17/result.json.

## 독립 예제 소스 재사용

22개 예제를 기존 GallerySample·UiCodeView로 통합했습니다. 16개 상태를 실제 타입으로 비교해 변경 때만 소스를 갱신합니다. 전체 22개 바인딩의 변경 없는 동기화는 128회 반복에서 관리 할당 0B입니다. UI 전체 할당이나 FPS 측정은 아닙니다. GPU 268건에서 실제 선택과 표시 코드의 일치도 확인했습니다. Core·Svg·Vulkan·App·Windows DLL 지문이 같아 이전 공통 4642건·탭 GPU 42건·기존 소스 31건을 재사용했습니다. 자동 전역 캐시나 시간 개선율은 주장하지 않습니다. 근거: artifacts/gallery-factory-state-2026-11-17/coverage-and-reuse.json.

## Provider 테마 범위와 측정 키

기하 토큰이 다른 하위 영역에서 기존 측정 결과를 재사용하지 않도록 공통 키에 토큰 값을 추가했습니다. 정상적인 색상·토큰 복원과 준비 후 측정 재사용을 확인했습니다. Provider 집중 21건과 보완한 재사용 항목 1건, MSAA1·4 GPU12건, 공통4642건, Tooltip·입력4114건, 갤러리 GPU270건을 검증했습니다. 테스트 준비와 카탈로그 기대값만 바뀐 뒤에는 동일 Core의 통과 결과를 재사용했습니다. FPS·시간 개선율은 측정하지 않았습니다. 근거: artifacts/fluent-provider-2026-11-17/result.json.


Provider 글꼴 범위: 기존 UiTypographyFonts 상속·복원, 전체 글꼴 묶음을 포함하는 배치 키와 지연 Tooltip 전파를 구현했습니다. 영역 진입 시 전역 캐시를 비우지 않습니다. 집중 CPU9·갤러리3·글꼴 GPU6(MSAA1/4)·시스템 타이포그래피38·기존 Provider GPU12·공통4642·Tooltip/입력4114 통과. Linux 호스트 빌드만 확인했고 실제 Linux와 고정 RC3는 실행하지 않았습니다. publications1244/1249의 Core 지문이 같아 앞서 통과한 검사를 재사용했습니다. FPS나 시작 속도 개선은 주장하지 않습니다.


Shimmer는 기존 Skeleton·UiFloatTransition·UiPresencePaint를 재사용합니다. 로딩 중 숨긴 내용은 처리하지 않고 완료 후 추가 프레임 요청을 멈춥니다. CPU14·GPU8·카탈로그4·갤러리GPU270과 양쪽 호스트 빌드 통과. 새 위젯 이외 공통 엔진 소스는 바꾸지 않아 직전 공통4642·Tooltip4114·글꼴 검사는 반복하지 않았습니다. FPS 개선 수치는 측정·주장하지 않습니다.


Windows 종료 응답: 정리 동안 HWND가 계속 보이는 가설을 실제 갤러리 구간 계측으로 확인했습니다. 닫기 수락 시 먼저 숨기고 파괴 순서를 유지한 A/B 각3회 비교에서 숨김 중앙값116.94→13.77ms(88.22%감소), 프로세스130.91→82.80ms였습니다. UI 스레드 합류보다 Vulkan 해제가 주된 관측 구간입니다. 기본 화면·Win32표시상태 범위이며 모든 드라이버·무거운 화면의 종료 시간 개선은 주장하지 않습니다. docs/windows-close-response.ko.md 및 artifacts/gallery-close-latency-2026-11-17/result.json 참조. .NET11 managed, 양쪽 DLL은 Windows 플랫폼 외 동일. 기존 위젯 검사는 반복하지 않았습니다.


ProgressIndicator는 기존 ProgressBar의 값 보간·대기 파형과 텍스트 배치를 재사용합니다. 통과 CPU14·GPU8은 동일 Core 지문으로 재사용하고 잘못 가정한 모션 줄이기 검사만 현재 공식 불투명도 변화에 맞춰 보완했습니다. 집중 CPU1·추가GPU4·카탈로그4·갤러리GPU270 및 두 호스트 빌드 통과. 공통4642·Tooltip4114·종료A/B를 반복하지 않았습니다. 성능 향상 수치는 주장하지 않습니다.


ActivityIndicator는 공통 원호·원·클립·프레임 예약을 재사용하며 별도 타이머를 만들지 않습니다. CPU14·GPU12·카탈로그4·갤러리GPU270 및 양쪽 최종 호스트 빌드 통과. 이후 지원하지 않는 팔레트 분기를 명확히 했고 지원하는 밝음·어두움 값은 유지해 해당 검사를 반복하지 않았습니다. 기존 종료A/B·다른 위젯 검사는 재사용합니다. FPS 개선 수치는 주장하지 않습니다.


## HeadsUpDisplay content

HUD 본체는 공통 측정 키와 UiRetainedLayout으로 두 줄 문구·크기 준비 결과를 재사용합니다. 글꼴 축소는 기존 타이포그래피 범위의 추가 오버로드로 구현했으며 기본 bool 시그니처를 보존합니다. 검증은 CPU17/Vulkan14/갤러리3건입니다. Before/After 성능 수치와 개선율은 측정하지 않았으므로 미확인입니다. 전체 포털 통합과 실기기 비교가 남았습니다. 근거: artifacts/fluent-heads-up-display-2026-11-17/result.json.


## HUD portal and calendar model

HUD 포털은 공통 오버레이·포커스·퇴장 그림·예약 프레임을 사용합니다. 본체64·공통Dialog107·카탈로그4건을 확인했습니다. 소스 검사식의 생성자 이름 혼동을 바로잡은 뒤 관련2건만 다시 검사했습니다. 코어 지문이 같은 실행 결과와 변경과 무관한 검사는 재사용했습니다. 달력은 지역 정보를 한 번 캡처하고 월 그리드를 준비하는 모델20건을 확인했습니다. 속도 Before/After·개선율은 미측정이며 주장하지 않습니다. 실제Linux·RC3NativeAOT·달력UI가 남아 있습니다.
