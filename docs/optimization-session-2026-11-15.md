# SVG GPU와 내부 카메라

## 실제 Graphite 버전의 선형 그라데이션 위치 안정화

브라우저 chrome://gpu가 GraphiteDawnD3D11·Skia ea1e651d7a105f5ca327dfd65d28957957279e2e를 보고함을 확인했다. 이는 전역 백엔드 보고이며 개별draw 추적은 아니다. 그동안 검토한 Ganesh 계수 모델 대신 같은 커밋의 Graphite fragment 소스를 대조했다. 선형 layout의 정규화 위치에1e-5를 더해 행/열의 stop 선택을 안정화하는 처리가 실제로 있었다. 맞춰 끼운 epsilon이나 전역 정밀도 변경이 아니라 확인한 생산 소스의 위치 단계 계약이다. radial·degenerate 선형과 source12/대상RGBA8/blur/역알파 계약은 유지했다.

변경: SvgGradientPaint와 vector_paint.glsl에 같은 선형 위치 처리를 반영했다. 기존 독립 그림자9종의 소스·네이티브 바이트를 그대로 fixture로 승격하고 공통 RGBA 검사·GPU 비교 소비 경로를 연결했다. 이전 Vulkan 생산 모듈을 넣은 격리 실행은4/9, 현재는CPU9/9·GPU 흰 배경9/9·투명9/9다. 장면XML 재처리·CPU 위젯 래스터·외부 라이브러리는 추가하지 않았다.

동일 입력 Before/After: 작은 그라데이션2/9→8/9, 불투명도 그룹2/11→8/11. radial-opaque1픽셀/채널1 차이는 남는다. 그룹의 채우기/테두리65→75·클립87→81·필터942→943픽셀(필터 알파4)도 기록한다. 전체는 여전히흰 배경0/4·투명0/2다. Red direct43136→43135, Canvas42414→42430; Yellow direct45207→45223, Canvas44089→44101. 투명Red42079→42095·Yellow58892→58895이며 알파277·4816은 같다. 작은 독립 원본의 교정을 전체 이미지 개선으로 승격하지 않는다.

관리280·제품132·읽기9·벡터44·효과49·테두리46·점선46·SVG90·카메라31·옵저버158, 새 Windows 빌드와 Linux32개 표시·정상 종료를 확인했다. source/reference hashes·negative control·실행 명령은 artifacts/svg-graphite-layout-2026-11-15/verification.json, docs/svg-gpu-camera(.ko).md와 CACHE/gradient-path-20261115에 있다. 브라우저 space7은 닫았고 사용자 공간은 조작하지 않았다. source formatting은 수정 파일만 수행했다.

SDK11.0.100/runtime11.0.0 관리형 기능·픽셀 검증이다. 워밍업·독립3회 교대·평균FPS·median/p95/p99·1% low·VRAM과 원래 RC3 NativeAOT 성능 관문은 미측정이며 속도 개선율도 없다. Linux는 소프트웨어 Vulkan이다. 다음은 남은 방사형·겹침/클립/필터와 전체 원본 오차다.

## 기하 클립의 단일 페인트 불투명도 통합

가설: 클립 때문에 단일 페인트의 불투명도 통합을 금지하면 클립의 완전히 덮인 내부에서도 불필요한 RGBA8 저장이 색을 바꾼다. 독립 브라우저 참조의 clipped/unclipped46×46 내부는 동일하지만 기존 GPU는848픽셀이 달랐다. FoldSinglePaintOpacity에서 기하 클립을 허용하고 마스크·필터·여러 페인트·중첩 opacity 저장 경계의 기존 제한은 유지했다. 클립을 제거하거나 허용 차이를 완화하지 않았다.

동일 원본의 CPU·GPU 전체 차이는951→87픽셀(90.85% 감소), 내부 불변성 차이는848→0이다. 이는 오류 픽셀 감소율이며 속도가 아니다. 관리271개·제품132개·GPU 읽기9·카메라31·효과49·SVG90·옵저버158, 새 Windows 빌드와 Linux32개 표시·정상 종료를 확인했다. 남은87개는 최대 채널 차이1이며 기하 경계81개·내부 보간6개다. 전체 원본의 흰 배경0/4·투명0/2와 픽셀 수는 그대로다.

Ego의 독립 네이티브 SVG Image/Canvas2D로 그림자 알파6종을 캡처했다. CPU 알파4/6, 두 선형 입력은64픽셀 차이이며 방사형·초점 이동·단색·두 구간 입력은 일치한다. 별도 alpha uniform32,895개는 기존 소스 저장 모델과 모두 같아서 저장과 보간을 분리했다. 중간 stop 알파를 미리12비트화하는 가설은 큰 선형576개·방사형88개 차이를 만들어 적용하지 않았다. 관측한 알파 저장 입력·바이트·해시를 회귀 검사에 추가했다. Skia gradient scale/bias 원문은 참고이며 외부 렌더러 의존성은 추가하지 않았다.

SDK11.0.100/runtime11.0.0 관리형 기능·픽셀 검증이다. 워밍업·FPS·median/p95/p99·1% low·VRAM·독립3회 교대와 원래 RC3 NativeAOT 성능 관문은 미측정이다. Linux는 소프트웨어 Vulkan이다. 명령은 docs/svg-gpu-camera(.ko).md, 실제 입력·결과는 CACHE/svg-effects-20261115와 artifacts/svg-clip-opacity-2026-11-15/verification.json. 다음은 남은 보간·경계·전체 알파와 원래 제품 목표다.

## 투명 GPU 읽기와 그룹 불투명도 저장 순서

가설: 흰 배경의 최종 픽셀만으로는 알파와 RGB 오류를 구별할 수 없고, 그룹 opacity를 먼저8비트로 반올림하면 저장된 색에 적용하는 순서가 달라진다. 검증 전용 GPU 텍스처 읽기로 premultiplied RGBA를 직접 비교했다. 최초 진단은 이미 회수된 계산 버퍼를 읽어 접근 오류가 났고, 실제 보관 텍스처를 전송 읽기·상태 복원하는 방식으로 수정했다. 제품 빌드에는 이 읽기 권한·복사가 없다. 읽기9개 계약은 변경 없는 캐시·채널·알파·입력 거부·프레임버퍼 복귀·해제를 확인한다.

변경: 단일 반투명 그라데이션은 소스 정밀도를 거친 뒤 알파 바이트로 저장한다. 별도 그룹은 raw authored opacity를 유지하고 저장된 RGBA에 곱한 결과를 소스 정밀도로 변환한다. 기하 커버리지는 별도다. CPU/GPU 공통 책임을 SvgCompositing.LayerSource와 vector_color.glsl로 분리했다. 대상 RGBA8·blur·straight-alpha 역변환 계약과 독립 브라우저 원본·해시는 유지했다.

동일 입력 Before/After: 단일 varying-alpha의384개 알파 차이가0으로 감소했다. 격리된 이전 셰이더 생산 모듈은 같은 입력에서384개를 다시 나타냈다. 그룹11종 중 중첩1272→8(99.37%), 겹침2296→24(98.95%), 마스크1368→24(98.25%)픽셀 차이로 감소했다. 클립1184→951, 필터1731→942이며 최대 채널 차이1은 남는다. 이는 색 차이 감소율이며 속도 개선율이 아니다. 전체 흰 배경0/4와 투명0/2는 미달이며 전체 이미지 차이 수는 그룹 수정 전후 같다.

검증: 관리268·제품132·GPU 읽기9·벡터44·효과49·테두리46·점선46·SVG90·카메라31·옵저버158, Linux 표시32개 정상 종료. 독립 투명 색 경계3/3, 작은 그라데이션2/9·그룹2/11, 원시 straight-alpha5/6. 명령은 docs/svg-gpu-camera(.ko).md, 입력·실행·해시는 artifacts/svg-transparent-2026-11-15/verification.json 및 CACHE/gpu-transparent-20261115에 있다.

SDK11.0.100/runtime11.0.0 관리형 기능·픽셀 진단이며 워밍업·속도 독립 반복·평균FPS·median/p95/p99·1% low·VRAM과 원래 고정RC3 NativeAOT 성능 관문은 미측정이다. Linux는 소프트웨어 Vulkan 표시 검증으로 물리GPU 속도·전체 접근성을 증명하지 않는다. Slogs의 CPU/GPU/갱신주기 분리 검증 기준을 적용했다. 다음은 남은 그라데이션 보간·클립/필터와 전체 원본의 알파 차이다.

## 색 저장 절반 경계와 소수 RGB 해석

기존12비트 소스 모델의 잔여 차이를 독립 WebGL uniform 출력으로 분리했다. NVIDIA/ANGLE Direct3D11의129×255개 float32 입력32,895개에서 기존 모델은 정확한0.5 plateau64개를 잘못 저장했다. 소스의 해당 경계를 인접한 아래 float32로 표현한 뒤 nearest-even 대상으로 보내면 전부 일치한다. 일반 target 변환은 바꾸지 않았다. BLEND 켜짐/꺼짐 결과가 같고 새 CaptureColorStorage.mjs 소비 경로의 재캡처도 같은 해시다. 모든 GPU의 네이티브 규격을 인증한 것은 아니다.

별도 SVG에서 쉼표형 RGB의 소수 성분이 브라우저처럼 바이트로 반올림되지 않는 문제도 확인했다. RGB는 바이트로 해석하고 rgba 알파는 스칼라로 보존했다. 새 불투명3종은 이전 SVG/Vulkan 생산 모듈을 넣은 격리 대조 실행에서1,792·2,048·1,024픽셀 차이로 실패하고 현재 GPU에서 모두0이다. 원본과 허용차0은 유지했다. 작은 그라데이션은1/9→2/9이며 방사형 opaque는62→1픽셀, 방사형 alpha는4→0이다. 전체 Canvas는 Red42,417→42,414, Yellow44,191→44,179로 여전히0/4다.

새 불투명3·투명3 원시 RGBA의 전체 판정은5/6이다. 마지막 투명색은 premultiplied 값이 같지만 straight-alpha RGB가1 다르다. 공통 unpremultiply를 소스 저장 모델로 바꾸는 후보는 BlurAxis 계약을 깨뜨려 폐기했고 원래 역알파 구현·blur 오라클은 유지했다. 새 전체 판정 CLI는 마지막1건도 실제로 실행하고 종료 코드1을 기록한다. 관리265개와 GPU3종 통과를 투명 전체 완료로 승격하지 않는다.

SDK11.0.100/runtime11.0.0 관리형 기능·픽셀 진단이다. 속도·FPS·median/p95/p99·1% low·VRAM과 원래 고정RC3 NativeAOT/3회 교대 성능 관문은 미측정이다. CPU 위젯 래스터나 새 외부 의존성은 없다. 근거 CACHE/browser-color-store-20261115 및 artifacts/svg-color-storage-2026-11-15/verification.json. 다음은 남은 보간·그룹 효과·straight-alpha 읽기와 전체 GPU 투명 출력이다.

## 단일 그리기 opacity의 재질 통합

별도 RGBA8 그룹을 만든 뒤 불투명도를 곱하면 브라우저의 단일 그리기와 색 변환 순서가 달랐다. SvgManagedScene.Opacity가 컴파일 때 단일 fill 또는 stroke를 찾아 SvgElementOpacityPaint로 합친다. RGB는 authored opacity를 유지하고 알파는 저장 계약을 적용한다. 겹침·복수 그리기·clip/mask/filter는 별도 합성을 유지하며, 중첩 opacity는 저장 경계이므로 곱으로 제거하지 않는다. 이 구분은 [Chromium 단일 그리기 alpha folding](https://raw.githubusercontent.com/chromium/chromium/main/cc/paint/paint_op_buffer_iterator.cc)과 독립 브라우저 픽셀로 확인했다. 구현은 직접 작성했으며 외부 의존성은 없다.

동일한 작은 그룹 그라데이션의 CPU/GPU 차이는2,088→8픽셀(99.62% 감소), 복잡 Yellow Canvas는45,692→44,191픽셀(3.29% 감소)이다. Red는42,417픽셀로 같다. 허용차0이며 전체는0/4다. 새11종에서 단색2종은 독립 브라우저 RGBA 전체가 같고 나머지는 최대1~2 차이가 남는다. 새11종의 CPU/GPU 출력은 전부 같다. 중첩 opacity를 곱한 초기 후보는 단색 알파161 대신160이 필요한 경계를 놓쳐 폐기했고 저장 경계를 유지한 현재 구현으로 교정했다. 재질 비트8의 초기 공통 검증 거부도 수정해 정상12조합·잘못된7조합을 제출 전에 확인한다.

관리259·제품132·GPU44·효과48·테두리45·점선45·카메라31·SVG90·옵저버158·Linux 표시32가 통과했다. SDK11.0.100/runtime11.0.0 관리형 기능·픽셀 진단이며 시간 개선 측정은 아니다. FPS·median/p95/p99·1% low·VRAM·독립3회 교대·고정RC3 NativeAOT 성능 관문은 미측정이다. 원시 근거 CACHE/browser-gradient-refine-20261115, 정리 artifacts/svg-opacity-fold-2026-11-15/verification.json. 다음은 그라데이션 보간·그룹 효과의 남은 채널값 차이와 전체 투명 출력이다.

## 그라데이션 소스 정밀도·불투명도 교정

가설: 브라우저의 원본 색 정밀도와 불투명도 변환 차이가 대다수 채널값1 차이에 기여한다. 실제 NVIDIA/ANGLE Canvas와 별도 WebGL highp/mediump를 읽어 작은 그라데이션9종·불투명도66개를 분리했다. half 계산 가설과 전체 target12비트 변환은 회귀를 만들어 폐기했다. RGBA8 대상 합성은 유지하며 그라데이션 디더링 뒤·기하 커버리지 앞의 소스 정밀도만 SvgShaderColor/vector_color.glsl로 분리했다. 공통 opacity는 가장 가까운 바이트·동률 아래쪽, gradient paint-server opacity는 동률 위쪽 변환을 사용한다. 외부 렌더링 라이브러리는 추가하지 않았다.

동일한 원본·흰 배경·해상도·허용차0의 GPU 비교에서 Red Canvas45,854→42,417픽셀(7.50% 감소), Yellow47,780→45,692픽셀(4.37% 감소)이다. 직접 참조의 차이는46,392→43,139,48,276→46,520이다. 전체 일치는 여전히0/4다. 작은9종은1/9이며 나머지 최대 차이는1, 그룹 합성은2,088픽셀 차이가 남는다. 독립 알파66개를 포함한 관리 회귀257개, 기존 GPU 효과·테두리·점선·카메라·옵저버는 통과했다. 원시 근거: CACHE/browser-gradient-current-20261115; 정리: artifacts/svg-gradient-2026-11-15/verification.json.

실행은 SDK11.0.100/runtime11.0.0 관리형이다. 픽셀 진단은 동일 입력의 전후1회이며 시간 벤치가 아니다. FPS·median/p95/p99·1% low·VRAM·성능 개선율은 미측정이다. 원래 고정RC3 NativeAOT/독립3회 교대 관문은 그대로 남는다. 색 정밀도는 관측 장치에 근거하며 다른 GPU 전체 일치는 미검증이다. 직접SVG 컨테이너의 실제 크기 정렬은 통과하지만 반복 해시가 달라 기존 참조를 교체하지 않았다. 다음 원인은 그룹 RGB 합성·보간과 전체 투명 출력이다.

## 가설과 변경

기존 SVG 위젯은 확대 배율마다 CPU 래스터와 Gaussian convolution, 픽셀 업로드를 요청했다. 원본 Red_Gallardo를4배 확대하면 기존 blur 작업량 제한 예외가 발생했다. XML·곡선·재질·글자 윤곽을 불변 GPU 장면으로 보관하고 Vulkan compute가 현재 카메라 출력만 생성하도록 변경했다. 화면 밖 전체 이미지 할당은 피하고 필터 주변 영역을 포함한다. 위젯·아이콘의 CPU 래스터 대체 경로는 없다. 명시적인 비트맵 출력은 분리한다.

카메라별 곡선 분할과 Y 단조 구간 교차 검색,64개 Y 밴드와16×16 장면 프로그램을 적용했다. 패턴은 현재 배율의 GPU 표면에서 생성하고 반복 샘플링한다. 필터는 부모의 픽셀 기준점을 유지한다. 타원·라운딩은 같은 로컬 좌표의 analytic coverage를 사용한다. 셰이더는 곡선·재질·analytic 모듈로 분리했다. 장면과 의존성 업로드는 유지하고 일회성 출력·blur 버퍼는 GPU 완료 뒤 해제한다.

## 검증과 측정의 범위

개발 검증은 SDK11.0.100/runtime11.0.0 관리형, 기본 GPU/효과 비교 MSAA1·글자/위젯 회귀 MSAA1/4·프레임 자원3개이며 원래 고정 RC3 NativeAOT 성능 비교와 구분한다. 명령 실행 위치는 tests/managed-validation이다. 테스트 결과 JSON과 원시 로그는 artifacts/svg-gpu-camera-2026-11-15/verification.json에서 연결한다.

- GPU 경로·카메라·blur 주변 영역·해제31개 검사 통과. 확대 후 CPU 래스터0회와 장면 재업로드 없음 확인.
- 복잡한 자동차2종,0.25/1/1.01/2/4배 확대의 실행·색상·재사용31개 검사 통과. 이는 전체 픽셀 일치 검사가 아니다.
- 독립 네이티브 글자 윤곽의 엄격 비교는 GPU 카메라 곡선 처리 후 MSAA1/4에서14개 검사가 통과했다.
- 세부 효과13종×배율3개의39개 비교는 초기24개에서37개 일치로 개선되었다. 남은2개는 곡선 테두리의 배율별 분할 차이다. 겹침 마스크·변환 라운딩·필터 패턴을 추가한16종×3배율은46/48 통과했고 남은2개는 같은 stroke 차이다.
- 엄격한 Red_Gallardo 전체 비교는836→276→189픽셀 차이로 줄었지만 여전히 실패다. 허용 오차1은 완화하지 않았다.

단일 진단 실행의 Red_Gallardo4배 렌더·GPU 읽기·PPM 기록은 초기 전체 대상269.38ms, 밴드 적용 전체 대상34.41ms, 카메라/축 blur 적용10.95ms, 곡선 카메라11.30ms였다. 각각 준비 상태와 변경 내용이 다르므로 정량 개선율은 확정하지 않는다. 실제 FPS·프레임 p95/p99·1% low·VRAM·working set/private 수치는 수집하지 않았다. 동일 화질과 고정 RC3 NativeAOT 독립3회 교대 비교도 미실행이다.

명시적 CPU 비트맵 경로에는 별도 axis convolution 최적화를 적용했다. 독립 scalar24개 검사와 같은 원본의3회 AB/BA/AB에서 최초 세 크기의 RGBA 해시를 보존했다. 중앙값1배282.4851→127.3205ms,1.01배230.6713→67.1214ms,2배1524.0418→347.1921ms였다.4배는 수정 전3/3 예외, 수정 후3/3 출력·중앙값3087.8488ms였다. 이 수치를 GPU 위젯 성능으로 합산하지 않는다.

## 남은 위험과 재현

복잡한 SVG·배율별 stroke 적합성, 해상도 의존 효과, 브라우저 엄격 일치와 원래 NativeAOT 성능 게이트는 미완료다. CPU fallback이나 원본 변경·허용 오차 완화로 통과시키지 않는다. 기존 사용자 창·빌드는 보존한다.

Duxel.Graphics.Tests <새 결과 폴더> gpu-vector 3은 기본 GPU 경로, gpu-svg-camera 3은 복잡한 카메라 확대, gpu-svg-pixels 3은 세부 효과의 독립 CPU 비교, svg 3은 기존 전체 픽셀 회귀, observer-svg 3은 실제 옵저버 화면을 검증한다. 모든 창은 검증 완료 후 자동 종료한다.
최종 기능 범위: Windows 전체 Observer158·CPU 계약315·SVG 제품132·독립 managed191, GPU 카메라31·경로31, native 글자14와 Linux32개 표시/종료가 통과했다. 통합 중 raw affine 참조 RGB가 alpha0 아래에서 사라지는 오류를 수정했고 관련 backdrop42개가 통과했다. 참조 데이터는 별도 불변 소스 식별자와 PreserveColorChannels 계약을 사용한다. 일반 색상 이미지의 premultiplied sampling은 유지한다. 헤더 변경으로 낡은 Immediate 테스트 좌표는 실제 탭/패널 경계로 바꾸었으며 기존 RGB와 replay 판정은 유지한다.

실제 장치: Windows NVIDIA GeForce RTX5070Ti Laptop GPU(DiscreteGpu), WSL Linux llvmpipe LLVM20.1.2(Cpu). 기존 장치 프로파일러로 확인했다. Linux32개는 Vulkan 기능·표시 검증이며 실제 하드웨어 GPU 성능을 인증하지 않는다. Linux 하드웨어 가속에는 호환 Vulkan 드라이버가 필요하다. 드라이버 설치·전역 환경 변경은 수행하지 않았다. 장치 확인용 profile 실행은 성능 비교에서 제외한다.
## GPU 테두리 프로그램과 카메라별 생성

초기 배율의 고정 stroke 윤곽이0.75배17픽셀·2배70픽셀, 복잡 Red_Gallardo189픽셀 차이를 만들었다. 곡선 patch·끝 모양·접합부를 최초 한 번 보관하고 compute 사전 처리에서 카메라별 윤곽을 생성하도록 변경했다. CPU 초기 윤곽 생성도 GPU 위젯 경로에서 제거했다. 기존 scalar 렌더는 독립 비교 대상으로 유지한다.

기존48개 효과 비교와 원본2종×MSAA1/4의 전체 scalar 비교90개 검사가 통과했다. 추가 테두리15종×3배율은 처음41/45였고 사각 점의 analytic coverage를 연결한 뒤43/45다. 남은2개는 곡선 대시의 길이 측정이 초기 배율에 묶인 문제다. 이전 검사나 원본·허용 오차는 바꾸지 않았다. 원래 브라우저 완전 일치의 완료 증거는 아니다.

단일4배 카메라 진단은 이전 근사 테두리10.75ms에서 정확한 GPU 테두리35.56ms로 늘었다.64개 경계 범위만 추가한 후보도40.09ms로 느렸다. 화면 밖 프로그램 생략, 기하 버퍼 하나로 통합,2D 배치 dispatch와 transfer→compute 의존성을 연결한 뒤11.65ms가 관측되었다. 최적화 전후 카메라12개 PPM은 바이트 단위로 같다. 시간은 동기 렌더·읽기·파일 기록을 포함한다. 독립3회 교대·고정RC3 NativeAOT 측정이 아니므로 개선율은 확정하지 않으며 FPS·p95/p99·1% low·VRAM은 미측정이다. 원시 근거는 CACHE/gpu-stroke-current-gpu-svg-camera, gpu-stroke-bands-gpu-svg-camera, gpu-stroke-batched-gpu-svg-camera에 보존한다.

구현은 SvgGpuStrokeProgram, VulkanRendererBackend.VectorStrokes, vector_stroke.comp/vector_stroke.glsl로 분리했다. 불변 장면 BDA는 재사용하고 작업 버퍼는 GPU 완료 뒤 해제한다. 원래 Vulkan1.2/SPIR-V1.5 계약을 유지하며 새 외부 라이브러리는 없다. 리스크·다음 단계는 곡선 대시의 GPU 길이 측정, 브라우저 원본의 최종 일치와 원래 성능 게이트다.
## 점선 길이·분할의 GPU 이전

남아 있던 곡선 대시0.75배6픽셀·2배45픽셀 차이는 초기 배율의 CPU 길이·분할 결과를 확대하던 원인이었다. 원본 곡선·간격·위상·pathLength를 불변 프로그램에 보관하고 현재 카메라에서 GPU 길이 측정, 보정, 분할과 끝·접합 생성으로 바꾸었다. 기존 테두리45/45와 별도 점선 경계15종×3배율45/45가 허용차1에서 통과했다. 닫힌 연결부의 위상 경계와 사각 끝의 대각선 돌출을 포함한다. CPU scalar 오라클·원본·허용차는 변경하지 않았다.

SvgGpuStrokeProgram.Dashes.cs와 vector_dash.glsl로 책임을 나누고 공통 테두리 emitter를 재사용했다. 원본 제어 다각형의 길이 상한으로 GPU 작업 용량을 계획하며 한도 초과는 명시적으로 실패한다. Float64 기능이나 외부 의존성을 추가하지 않았다. 잔여 리스크는 부동소수점 극한 범위, 매우 조밀한 대시의 반복 측정 비용과64MiB 작업 한도다. CPU 전체 사용량0 또는 임의 문서의 고정 비용을 주장하지 않는다.

원시 근거는 CACHE/gpu-dash-sealed-*와 artifacts/svg-gpu-dash-2026-11-15/verification.json이다. 첫 잘못된 빌드는 소스 TFM 지정 누락으로 net10을 생성했지만 실행·적합성 근거에서 제외했다. 이후 정확한 LocalSource.targets와 source TFM을 명시하여 .NET11 소스·호스트로 재빌드했다. 현재 카메라 시간은 단일 진단이며 독립3회 교대·고정RC3 NativeAOT 게이트는 여전히 미검증이다. 이번 단계의 개선율을 확정하지 않는다.

## 전체 GPU 브라우저 비교와 RGBA8 반올림

현재 GPU의 원래 해상도 전체를 정수 카메라 타일로 비교하는 경로를 추가했다. 원본2종과 직접SVG/이미지Canvas 네 경로는 모두 비일치다. Canvas의45,854·47,780 차이 픽셀 중41,440·47,412는 채널값1 차이다. 축소 미리보기·scalar 통과를 브라우저 일치로 승격하지 않았다. CPU 래스터 횟수0을 확인하며 RGBA 히스토그램과 실제 PNG/RGBA를 남긴다.

반올림 경계값에서는 공통 UiRgba8의 nearest-even과 GPU ties-up이 달랐다. GPU를 공통 계약과 맞췄고 같은 테스트의 이전 Vulkan 모듈은1/3/5로 실패, 수정 모듈은0/2/4로 통과했다. GPU42개와 기존48·45·45·90·카메라31 검사를 통과했다. 전체 브라우저 비교는 이 변경 전후 바이트 단위로 같아, 브라우저 불일치 개선으로 보고하지 않는다. 속도·메모리 개선율도 측정하지 않았다.

독립 브라우저 재캡처에서 Canvas 해시는 재현되지만 직접SVG의 실제 CSS 크기는 미세하게 달랐다. DPR만 확인하던 캡처 경로에 실제 크기·위치 계약을 연결했다. 정상 및 실패5건과 실제 소비 경로의 스크린샷 요청0건 차단을 확인했다. 기존 오라클은 교체하지 않았다. 남은 위험은 직접 캡처 정렬, 색 계산·경계·투명 출력의 엄격한 브라우저 일치다.

디더링의 장치 좌표·표 처리 형태는 [Skia 원본 구현](https://skia.googlesource.com/skia/+/5fdfc51ef3e8/src/gpu/ganesh/SkGr.cpp)과 독립 캡처로 조사했다. 라이브러리 의존성은 추가하지 않았다. 작은 색 참조에서 half/알파 정밀도 후보의 유의미한 개선은 확인하지 못해 적용하지 않았다. 근거는 CACHE/browser-fidelity-current-20261115와 artifacts/svg-browser-current-2026-11-15/verification.json에 보존한다.


## Rectangular clip and source-storage boundary

Hypothesis: intermediate clip/opacity storage and source precision before coverage cause boundary errors; compiling eligible rectangle intersections once removes unnecessary clip work without changing authored paint/object bounds.

Change: geometric clip module with explicit effect/storage guards, transformed-child opacity boundary, source12 after coverage, exterior-corner distance AA. Native nine-case fixtures and parent-filter bounds regression are retained.

Verification: previous SVG/Vulkan production DLLs with the new consumer pass0/9; new CPU and GPU pass8/9. Existing opacity group8/11→9/11. Full originals remain0/4 white and0/2 transparent with unchanged mismatch counts. Managed285 and focused GPU/Observer regressions pass. Evidence: artifacts/svg-clip-storage-2026-11-15/verification.json.

Performance: eligible clip stacks are removed at compile time; no new per-zoom XML/scene upload or CPU raster path. Timing improvement and FPS percentage are UNMEASURED; single full-output versus512×256 camera readbacks have different output scope and are not an equal-work performance A/B. Original RC3 NativeAOT gate remains unrun.

Risk: transformed source still90 pixels/max1; other filter/fill-stroke and complex originals remain unresolved. Object-bounds/paint coordinates must not be reduced to the drawn intersection.


## Opacity-layer geometric clip composite

Hypothesis: the remaining translated-source90 pixels arise from quantizing geometric coverage into the group's stored RGBA8 color before group opacity. A float32 order experiment using the retained opaque source and independent native output gives3 errors for source12(opacity) then coverage,0 for source12(opacity×coverage),248 for byte(opacity) then coverage.

Change: eligible rectangular clips remain analytic coverage on the group-pop command; source storage, opacity and coverage have separate lifetimes. Existing six-vector command layout is retained; the pop carries optional rectangle data. CPU/SVG compiler share eligibility, geometry and composite share rectangle AA. Filter/mask boundaries and viewport coverage contracts remain unchanged.

Before/After: exact native clip8/9→9/9 CPU and GPU white/transparent, remaining90→0 pixels. All nine are hash-checked managed regressions (291 checks total). Existing opacity stays9/11, complex originals0/4 white·0/2 transparent, gradients8/9; no unproved completion. Camera CPU raster/additional source upload remain0. Speed improvement percentage is UNMEASURED; no equal-work repeated performance comparison or RC3 NativeAOT validation.

Risk: broader unsupported effects still follow their existing composite semantics; full browser equality remains open. Evidence: artifacts/svg-clip-composite-2026-11-15/verification.json.


## Opacity storage layer dither origin

Hypothesis: source colors in a cropped opacity layer use local fragment pixels for the pinned Graphite8x8 dither LUT; the renderer used global device pixels. Five independently captured controls establish floored painted minima including stroke, with exact model reproduction of old interior bytes.

Change: shared PaintBounds separates ink/stroke/child transforms from SVG ObjectBounds. Explicit CPU raster surfaces carry RasterOrigin; GPU push commands retain layer bounds and shader stacks derive per-camera pixel origins. Gradient sampling coordinates remain unchanged. XML/scene upload is not added to zoom. Existing six-vector command layout is preserved and the layerSource shader helper is reused.

Before/After: original fill/stroke75→0 pixels, opacity9/11→10/11. New five controls0/5→4/5; fractional237→196 pixels remains. Current consumer and scene with the old Vulkan-only producer reproduce75 pixels/9-of11, confirming the production shader effect. CPU296 and focused GPU regressions pass. Complex originals0/4 white and0/2 transparent remain unchanged.

Speed/FPS/VRAM percentage: UNMEASURED. This is correctness and retained-camera structure evidence, not equal-work repeated performance qualification or RC3 AOT. Per-depth shader origin storage and conservative curved stroke bounds remain performance/fidelity considerations. Evidence: artifacts/svg-layer-dither-2026-11-15/verification.json.


## Analytic miter rectangle stroke

Hypothesis: generic four-sample stroke coverage persisted through an opacity layer, unlike the browser's analytic primitive. Outer/inner rectangle coverage is a distinct primitive contract.

Change: shared rectangle recognition, retained analytic stroke metadata, CPU coverage path and GPU flag64/one inner-bound record; Core rejects invalid ranges/counts/combinations. Zero-width strokes create no work. Existing generic/dashed/rotated paths remain distinct.

Before/After: original fractional196→0, basic native layer corpus4/5→5/5 CPU/GPU white/transparent and tiled camera. Each of five current producer scenes uses0 generated stroke programs/752 bytes versus previous1/1424 bytes. These counts do not measure GPU timing or VRAM. New independent thin/thick/reflected/scaled/opaque domains pass3/5: thin284, scale CPU9/GPU11 remain. Previous CPU domains were0/5; do not claim a prior GPU result that was not run. Complex originals, filter and radial discrepancies remain unchanged.

Observer: native reference raster scale is derived from captured pixel extent; actual default first-pair framebuffer bytes agree. New stroke-domain tab exposes unresolved cases.

Shader safety: previous SPIR-V recovered byte-exactly from protected delivered Vulkan module; current temporary compile matches runtime bytes and passes spirv-val/disassembly, Vulkan1.2, unchanged88-byte push constants/8x8/ArrayStride16. Initial source-binary replacement preceded this review; do not claim temporary validation occurred before that write. Source binary was independently verified before delivery. NVIDIA/AMD static heuristics reviewed; vendor profilers, repeated equal-work benchmark, physical Linux GPU and original RC3 AOT are unrun. No speed percentage claimed.

Cleanup: automatic review rejected recursive deletion with only blocked-by-policy reason. Three evaluated execution copies were moved intact into project must_delete; results and delivered reviews are preserved. Moving is not deletion.

Next performance hypothesis: vector_scene.comp still scans source commands per visible pixel (Red scene2237 commands). Measure actual cost, then consider bounded GPU camera-tile visibility bitsets while preserving every structural command and pixel. This is a hypothesis, not a measured bottleneck or speed result. Evidence: artifacts/svg-rectangular-stroke-2026-11-15/verification.json.


## GPU camera: reject offscreen paint before evaluation

- Hypothesis: the general path evaluated paint/gradient state before its four sample bounds checks. A conservative early bounds rejection can avoid that work without CPU visibility lists, scene changes or reordered structural commands.
- Change: move the existing analytic one-device-pixel bounds guard to the common draw path; keep layer/mask/pop/break handling first. Host ABI, subgroup assumptions, resource lifetimes and barriers are unchanged.
- Safety: copied baseline source/SPIR-V before modification; compiled only an isolated candidate directory; Vulkan1.2 spirv-val/disassembly passed. Existing DuxelShaderDirectory selected the candidate for an isolated Vulkan assembly; production SPIR-V was replaced only after benchmark and full correctness comparison. A final source compile matches production bytes exactly.
- Workload: unchanged complex originals2 × zooms .25/1/1.01/2/4;16 warmup +32 measured frames per case; alternate one-pixel camera origin, max512×256;3 independent alternating baseline/candidate processes. UI build, serialized full raw readback and prior-frame-slot GPU command-buffer timings are separate. Hashing/file I/O outside timers. Prior-slot timestamps remain inside the same warmed source/zoom block. Not isolated SVG compute, appFPS or original RC3 AOT qualification.
- Results: all960 paired measured frame hashes identical and all six runs reproduce the same full raw sequence.9/10 GPU-time median ranges separate: reductions4.7–14.3%. Red400%4574.6→4578.7us(+0.1%), inconclusive. Serialized render/readback ranges overlap for ALL10 cases; no end-to-end speedup claim. UI construction is small; the readback test itself allocates its CPU pixel arrays, which is not SVG CPU rasterization.
- Fidelity:15 prior comparison profiles preserve ALL107 complete raw outputs, including known native mismatches. Existing small controls are not a substitute for whole-original browser equality. Candidate vector47/effects49/stroke46/dash46/camera31/Observer SVG17/gallery158 pass. Current production vector47/camera31/Observer SVG17 pass and its320 measured raw hashes match baseline. Linux32 shared-page presentation/normal exit passes after current source publish.
- Static portability review: NVIDIA/AMD branch coherence/register heuristics reviewed; no new subgroup width, barriers, memory layouts or work buffers. Vendor ISA/profiler/register/physical Linux GPU validation is unrun.
- Preparation correction: known native mismatch profiles throw an assertion, yielding Windows exit -532462766, not1. Initial orchestration expected1 and stopped after retaining the complete3/5 report. Corrected classification requires exact known counts and the uncaught assertion exit; full equality remains failed. Earlier successful profiles were not rerun.
- Evidence: L:/Cache/Duxel/linux-accessibility-2026-11-14/camera-paint-cull-20261115/comparison-summary.json, full-output-preservation.json, independent CSV/raw/log files. No extra full benchmark runs beyond one baseline/candidate probe, the three planned alternating pairs and one production equivalence run.
- Remaining: Red400% full command scanning; normal presentation CPU/driver/resource cost needs a measurement without serialized CPU readback before attribution. Consider GPU tile selection and fence-safe output/work-buffer reuse only after discriminating measurements. Native fidelity and all original product goals stay active.


## Native vector workspace and ordinary Linux slider responsiveness

- Normal presentation probe separated CPU readback from actual QueuePresent. On RTX5070Ti Laptop GPU, texture preparation measured roughly0.4–1.5ms in the first probe. Replaced per-output native work-buffer destruction/recreation with upload/device-class best-fit reuse after both slot fence andq+N retirement deadline. Completed cache bounded64MiB/256 entries; immutable scene buffers excluded. All-fence drain covers settings/index reset, and renderer destruction frees pending/available work.
- Lifecycle checks with1/3 slots, including actual Khronos validation-layer insertion, pass55/57 checks without VUID/Validation Error. Warm fixed-camera allocations stop while reuse counts rise; raw output identical; explicit drain leaves zero cached/pending buffers. Core validation is not GPU-assisted or synchronization validation.
- Allocation refinement removed a per-output retirement array. Final normal3 alternating pairs: all1920 presentations succeed,60 post-block fullRGBA pairs match, render-call medians decrease33.4–65.9% with separated per-run median ranges. GPU cost remains distinct. Candidate sample cache peak66327296 logical bytes; some managed frame allocations still increase352–832 bytes due to metadata. Host private/working set separately sampled; no totalVRAM or memory-reduction claim. Intermediate earlier candidate timings retained but not final reproducible performance evidence, because that candidate DLL was overwritten before sealing; final runner now hashes all executable/DLL/source inputs before/after execution.
- Resource-cache broad comparison before the pure retirement-array refinement preserves all107 complete raw outputs across15 native profiles. Final refinement has the same buffer-return order and is covered by focused lifecycle/UI and60 normal-camera raw pairs; do not label the earlier107 as a fresh final-source rerun. Native fidelity failures remain active.
- User correction: the slow control was an ordinary slider across pages, not SVG. Added a finite Linux shared-widget model-sweep probe for minimal/full/frozen-diagnostic conditions. Does not simulate native pointer-to-display latency. Each scenario retains16 cold/warmup frames and32 measured frames; no diagnostic text is drawn by the probe itself.
- Found concrete common Observer cause: FPS updates every0.5s, but Present/Draw/Upload/Cache text was rebuilt every frame. Together with slider changes this expanded the single repair union from the header to the lower page. Refresh whole header on FPS sample instead. Three alternating llvmpipe pairs: Fluent interval median8.172307->2.603499ms(-68.14%), repair area905352->108489px(-88.02%). Minimal/frozen-control ranges overlap, not separate speedups. First Fluent composition149.6104ms in the initial probe remains unattributed and open; input latency also open.
- Current Linux device actually profiled as type=Cpu, llvmpipe LLVM20.1.2; /dev/dxg and D3D12 bridge exist but no dzn ICD is installed. No environment/global driver changes, source-driver builds or packages installed. Missing build prerequisites recorded only; user requested optimizing common widgets even on CPU, so do not substitute driver tuning for those fixes.
- Evidence: CACHE/camera-normal-cost-20261115/{final-pairs/summary.json,linux-slider-comparison.json,normal-probe-summary.json,full-output-preservation.json,validated-cache-slots-*}. CACHE=L:/Cache/Duxel/linux-accessibility-2026-11-14. Existing delivered work-cache preview is protected; refined Windows/new Linux review paths are separate.

## Linux native input stalls — separate input and presentation connections

- User still reported slow ordinary controls after the model-sweep improvement. Added `--verify-pointer-performance` to traverse X11 event polling, snapshot transfer, actual Fluent hit testing and value bindings. The probe sends events only to its owned window and never moves the desktop pointer. Native XSendEvent is not physical OS pointer injection or monitor latency.
- Hypothesis: Xlib input dispatch and Mesa Vulkan WSI contend on the same Display connection, which can leave incoming input waiting while unchanged held-button frames continue to present. Rendering time alone hides this: an unchanged frame can present in about2ms while native input waits hundreds of milliseconds. Enabling per-frame profiler file output changed timing and masked much of the stall; that run is retained as a diagnostic, excluded from performance claims.
- Root change: `LinuxX11PlatformBackend.SurfaceSource` owns one dedicated Xlib Display for Vulkan WSI. Before opening it, `XSync` commits the input client's window creation. Both clients reference the same server-side window. The existing input event connection and renderer API stay intact. The presentation connection closes with the platform, after RunCore has disposed the renderer/surface. No driver, environment, quality, shader, swapchain policy or package changes.
- Native contract: [VkXlibSurfaceCreateInfoKHR](https://docs.vulkan.org/refpages/latest/refpages/source/VkXlibSurfaceCreateInfoKHR.html) requires a valid Display and Window. [Xlib threading](https://www.x.org/releases/current/doc/libX11/libX11/libX11.html) documents per-display serialization. The measured A/B result, rather than the documentation alone, supports this change's effect on the selected llvmpipe host. Incremental-present was investigated but is not implemented in this fix; its absence was not established as the cause.
- Stable SDK11.0.100/runtime11.0.0 managed linux-x64, same five fonts/1280×900/default2 frame slots/V-Sync off/driver, accessibility ON. Each of three scenarios gets96 native warmup changes then32 measured changes. Three alternating independent baseline/candidate pairs:2304 actual changes including warmup,576 measured samples. Candidate/baseline binaries are hashed before/after all runs. No profiler IO in these comparisons. Ordinary app event-wait defaults remain enabled.

| Native input dispatch median | Before ms | After ms | Reduction | Evidence |
| --- | ---: | ---: | ---: | --- |
| Minimal slider | 98.1952 | 1.4859 | 98.49% | All three per-run median ranges separate |
| Full Fluent | 1.3237 | 1.3052 | Not confirmed | Ranges overlap; no median speedup claim |
| Frozen diagnostic row | 70.2972 | 1.3509 | 98.08% | All three per-run median ranges separate |

- Long input stalls above50ms: baseline150/288 measured samples, candidate0/288; baseline maximum769.383ms, candidate maximum4.074ms. Full Fluent's baseline had18 such stalls even though its median was low. Per-run dispatch p95: minimal baseline387.036–485.844ms/candidate2.135–2.495ms; full baseline6.187–286.928ms/candidate1.516–1.546ms; frozen baseline285.023–548.403ms/candidate1.656–3.511ms. Individual medians/p95/p99/max remain in `native-pointer-warm-pairs/summary.json`.
- Costs retained, not hidden: UI Build medians minimal0.0103→0.0464ms, full0.6250→0.6696ms, frozen0.3428→0.8435ms. Presentation interval medians1.5789→1.5922ms,1.7960→1.9737ms,1.9243→2.2294ms. None is a confirmed speedup; this fix removes long input stalls, not a universal FPS increase. One extra native connection is owned; total host memory was not measured, so no memory-reduction claim. Queue acknowledgement includes the probe's2ms scheduling and is not physical display latency. Existing RC3 AOT/physical Linux GPU acceptance remains unrun.
- Initial probe failed because a new screen's first press used the previous screen's published slider coordinates. The probe now waits for released input and three settled frames before pressing. All measured runs check at least20 distinct actual binding values per scenario. Failed evidence remains `native-pointer-baseline-1`; per-frame-profiler diagnostic is `native-pointer-baseline-2`. The earlier16-warmup comparison is diagnostic, superseded by96-warmup final comparison.
- Final whitespace-only formatting was followed by a fresh build in `observer-linux-input-isolated-publish`,384 native changes/normal exit and32 shared Observer+SVG camera presentations/normal exit. Final probe measured maximum input dispatch2.7215ms. Scoped folder whitespace verification passes; the earlier project-format workspace warning is retained rather than claimed as a clean format check.
- Commands: publish from `tests/managed-validation` using `dotnet publish .../Duxel.ImplementationObserver.Linux.csproj -c Release -r linux-x64 --self-contained true -p:DuxelSourceTargetFramework=net11.0 -p:DirectoryBuildTargetsPath=.../LocalSource.targets -warnaserror -m:1 -nr:false -p:BuildInParallel=false`; run the published executable with five explicit fonts followed by `--verify-pointer-performance <fresh path>` or `--verify-gpu-camera <fresh path>`. Exact producer paths/arguments are retained in `camera-normal-cost-20261115/run-native-pointer-warm-pairs.ps1`; current interactive commands are paired in the Linux READMEs. Default Linux/SVG lock targets restored last; no further workspace load.
- Remaining: first Fluent composition around150ms remains separately unattributed; physical pointer-to-monitor latency and all-widget interactive acceptance remain unverified. The original whole product goal stays active. No policy/memory changes or quota reset were performed in this stage.

## Pinned RC3 Linux NativeAOT — first-use and sustained composition

- Current managed first Fluent composition reproduced at147.4229ms. Disabling tiered compilation in one diagnostic child process increased it to302.18ms; this setting was not adopted. Standard/composite ReadyToRun first probes118.5677/76.155ms were diagnostic only. Chose actual NativeAOT under the original pinned RC3 rather than changing runtime defaults or declaring the warmed slider fix complete.
- Linux had only SDK10.0.115. Resolved the exact Linux SDK11.0.100-rc.3.26522.148 archive from Microsoft's official release metadata and verified SHA512 before extraction into a task-local toolchain directory. System SDK/PATH and Windows SDK/MSVC were not modified. The installed SDK/runtime and compiler/build logs are retained. Windows NativeAOT remains separately unrun; Linux progress does not clear that gate.
- Actual RC3 NativeAOT publish reports `Generating native code`; the16,642,728-byte executable is an ELF x64 PIE, not a managed apphost. Matching managed RC3 is independently published from the same source with runtime11.0.0-rc.3.26522.148. No per-process compilation override in final comparisons. Native dependencies remain the selected Linux libraries.
- Three alternating independent pairs run both first-use composition and96-warmup native pointer scenarios:12 normal-exit processes total, each input tree/font fingerprint checked before/after. First-use means first control use in a new process with file/font/page caches warm; it is not cold boot. Native pointer remains XSendEvent to owned windows, not physical monitor latency.

| Fluent metric | RC3 managed | RC3 NativeAOT | Reduction | Scope |
| --- | ---: | ---: | ---: | --- |
| First composition median | 134.6612ms | 31.7940ms | 76.39% | Per-run ranges127.1046–141.4251 vs28.3237–33.6591, separate |
| Native drag UI composition median | 0.67805ms | 0.25695ms | 62.10% | 96 warmup/32 measured per scenario, three pairs; ranges separate |
| Native drag presentation interval median | 1.971045ms | 1.545877ms | 21.57% | Ranges separate; not physical display latency or unlimited appFPS |

- Costs/limits retained: minimal-slider dispatch median roughly1.33815→1.53085ms gets slightly slower, not a latency win. Full/frozen dispatch median ranges overlap. Per-run means/medians/p95/p99/max for composition, dispatch, queue acknowledgement and presentation are retained in `observer-rc3-runtime-pairs/summary.json`. Sampling Linux VmHWM records roughly269MB managed versus225MB NativeAOT on cold probes; it is resident high-water observed during sampling, not Windows Private bytes or VRAM. Distribution including debug files99,676,394→53,032,259 bytes; excluding debug98,496,802→26,471,111 bytes. No total GPU memory claim.
- Replaced anonymous/object result serialization in existing Linux platform tests with explicit typed records and one source-generated JSON context. Reflection JSON is disabled and IsAotCompatible enabled so managed runs also exercise the actual NativeAOT-compatible result path. Original field names and all assertions preserved; no reflection fallback or warning suppression.
- Final strict managed/NativeAOT builds each pass text87/vector73/render34=194 identical contract checks. All40 emitted full image file pairs, including12 rawRGBA500×240 images, match byte-for-byte. Text/render JSON match entirely; vector numerical/check/schema data match, with only two CubicOutline font deployment paths differing. Those exact file locations are independently SHA256-equal; arbitrary path normalization or image tolerance is not used. Whole Observer/widget/SVG browser pixel qualification remains separate.
- NativeAOT Observer passes32 shared pages/camera presentations and normal exit. Extended existing PowerShell launcher with explicit `PublishedExecutable` and `SampleArguments` so an already published NativeAOT binary runs without SDK publishing. Missing explicit paths fail; no alternate build is selected. Launcher execution itself passes32 presentations and384 native input changes. Default source-publishing behavior remains managed. Existing delivered outputs/AppData untouched.
- Scoped C# whitespace and PowerShell parse checks pass; default Linux/SVG locks restored last. SDK11 RC3 Linux NativeAOT and managed exact commands, independent stdout/stderr benchmark logs, memory samples, inputs/outputs and hashes remain under `CACHE/camera-normal-cost-20261115`. The first long publish logs combine stdout/stderr; do not claim they are split. Final runtime runner logs are split. CACHE=L:/Cache/Duxel/linux-accessibility-2026-11-14.
- Native SDK download source: [official .NET11 release metadata](https://builds.dotnet.microsoft.com/dotnet/release-metadata/11.0/releases.json). Compilation interpretation follows [tiered compilation](https://learn.microsoft.com/en-us/dotnet/core/runtime-config/compilation) and [ReadyToRun deployment](https://learn.microsoft.com/en-us/dotnet/core/deploying/ready-to-run). Stable SDK R2R observations are not a same-runtime performance baseline for RC3 NativeAOT.
- Remaining: residual first-use native work, physical input-to-monitor latency, full interactive all-widget acceptance, Windows RC3 AOT, strict whole SVG fidelity and original accessibility/transforms/Markdown goals. Do not report the original product complete.


## 정지한 마우스 누름의 공통 입력 무효화

가설: 공통 앱 루프의 `HasInputDrivenInvalidation`이 누른 상태 자체를 새 입력으로 간주해 이벤트 대기를 건너뜁니다. 변경: 직전 버튼 상태와 비교하고 실제 누름·놓기·이동·예약 프레임은 유지했습니다. 검증: 동일 RC3 NativeAOT3쌍 교대, 화면3개×버튼3개,350ms 정착/400ms 측정. Before239~317회 구성→After0회; 정규화한 프로세스 CPU 작업 감소99.94~99.96%. 정지 FPS0은 의도된 결과라 최대FPS 개선으로 세지 않습니다. 24회 연속 이동과850ms SpinButton 반복1→4 유지, 최종32화면·384입력 정상 종료. Windows 관리형614프레임·4개 세션 검사와415조합 계약 통과. 추가 입력 게시 순서 조정은48→48구성으로 기각·제거. 구형 Rendering.Tests 드라이버 빌드 CS0436/CS0759 실패는 별도 보존, 현재 Graphics.Tests 엄격 빌드는 통과. 물리 표시 지연·전체 목표 완료를 주장하지 않습니다. 명령·정확한 수치·해시는 [검증 기록](../artifacts/linux-held-input-idle-2026-11-15/README.ko.md)과 `verification.json`에 있습니다.


## Dialog 소비 경로를 위한 공통 오버레이 상태·수명

가설: 기존 Popover의 검증된 포커스·소유권·취소·예약·수명을 공유하면 Dialog에서 중복된 입력 모델을 만들지 않을 수 있습니다. 변경: UiOverlayInputState와 공통 런타임으로 분리하고 상태별 ContainsScreenPoint가 구체적 형상을 처리합니다. 검증: Windows 엄격한 .NET11 게시,240 CPU·452 GPU·53 변환·25 팝업 입력·614프레임/4 앱 세션 통과; 기준437개 이미지 완전 일치. Linux 고정 RC3 NativeAOT32화면·384입력과 정지 누름9조건0회 구성, 속도 지정 드래그·예정된 숫자 반복 유지. Before/After 기능과 픽셀은 동일하며 속도·메모리 개선율은 미측정입니다. 리스크: Dialog 소비 구현과 전체 적합성은 아직 없으며437개 기존 Popover 이미지를 전체 Fluent 완료로 해석하지 않습니다. [계약·검증 명령 범위](../artifacts/fluent-dialog-2026-11-15/README.ko.md).


## Linux animation deadline pacing

Root cause: control animations marked active and immediately requested the next frame; the common app loop bypassed scheduled waiting while that marker was active. A linked progress transition therefore consumed unrestricted CPU between120Hz inputs. Shared default60Hz deadlines replace immediate animation wakes, preserving real input, earlier explicit/repeat deadlines, visibility/cache semantics and explicit continuous rendering.

Pinned SDK11.0.100-rc.3.26522.148/runtime11.0.0-rc.3.26522.148 NativeAOT; same software Vulkan, fonts, geometry and diagnostics. Three independent processes per binary in order A1/B1/B2/A2/A3/B3; each process contains3 rounds per scene,1s warmup and2s120Hz stream. No profiler I/O during measurement. Reproduce with the preserved launcher and `--verify-stream-pointer <fresh-directory>`. Raw samples: `L:/Cache/Duxel/linux-performance-recheck-20261115/stream-comparison.json`.

| Scope | Before | After | Reduction |
| --- | ---: | ---: | ---: |
| Fluent CPU, one core=100% | 221.24% | 91.55% | 58.62% |
| Fluent builds/s | 633.96 | 164.71 | 74.02% |
| Fluent dispatch median | 1.189ms | 0.215ms | 81.96% |
| Fluent dispatch p95 | 2.991ms | 1.080ms | 63.89% |
| Fluent frozen CPU | 211.88% | 77.69% | 63.33% |
| Isolated slider CPU | 51.11% | 57.68% | -12.85% |
| Isolated slider dispatch median | 0.183ms | 0.207ms | -13.04% |
| Fluent per-run maximum median | 7.242ms | 7.289ms | -0.65% |

Negative/control results are retained. Input stream interval and UI build rate are not equivalent to display FPS; these are not physical/compositor/monitor latency measurements. The change does not establish a speedup for the isolated nonanimated slider or every scene. Fixed-binary CPU regression618 checks and6 native Windows scheduling cases/24 checks pass. Final Linux acceptance and launcher/source/lock records are in `artifacts/linux-animation-pacing-2026-11-15/verification.json`. Full Linux accessibility remains outside this focused run; separate Dialog Escape failure remains unresolved.


## Unchanged native presentation and consumed Linux NativeAOT deployment

Hypothesis: the current llvmpipe/X11 route sends the complete image with xcb_put_image even when common damage is empty. Reuse existing logical snapshots before acquisition, preserve native expose/remap tokens and distinguish accepted CPU frames from actual presentation. No surface-quality, input-frequency or software-driver changes. Shared callbacks, Scene3D/GPU animation, texture/mask/group dependencies and semaphore ownership retain execution.

Fixed RC3 NativeAOT same-executable comparison uses DUXEL_VK_PRESENT_REUSE=0/1, order A1/B1/B2/A2/A3/B3,3 independent processes per condition,9 phases each,120Hz real widget input,1s warmup/2s measured. Raw per-run median/p95/p99/max and non-overlap decisions are in L:/Cache/Duxel/linux-performance-recheck-20261115/present-controlled-comparison.json. Isolated-slider process CPU(one core=100%)52.97465→24.89963%, reduction52.99709%, ranges52.17220–58.75477 versus23.83877–27.00097. Whole Fluent CPU89.46447→85.82969% and frozen82.71602→78.98519% have overlapping ranges: INVALID for speedup claims. Dispatch medians also overlap. Producer builds and sampled previous-presentation metrics are not display FPS or physical latency;1percent-low display FPS and new memory usage remain unmeasured.

Original uninstrumented candidate and prior executable both time out waiting for native input acknowledgement. Preserve those failures and do not call them fixed. Two focused traces and the six-process same-executable instrumented cohort pass; no FocusOut was observed, so focus loss is not assigned as the cause. No input recovery fallback or silent retry was added.

Independent GPU3704 checks/588 rawRGBA pairs across slots1/3 and MSAA1/4 pass. Final Linux binary passes33 presentation scenarios,384 native inputs, nine stationary holds with zero extra builds,24-step drag.19→.81, scheduled SpinButton1→4 and21 native expose/remap checks. The ordinary source launcher previously still published managed code; it now publishes pinned-SDK NativeAOT in WSL unless -Managed is explicit. Default source publishing passes30 pages independently. Missing SDK/version mismatch fail before publishing. Exact runnable binary, source hashes and remaining physical acceptance/WindowsAOT/accessibility limits: artifacts/linux-present-path-2026-11-15/qualification.json. No whole-product completion claim.


## Dialog activation origin and pending-context lifetime

Native Linux Escape failure is focus return, not failure to close: editor/surface absent, opener enabled but unfocused, native window active. The common CPU regression reproduces actual focus Finish Dialog instead of invoking Open modal. Shared synchronous UiButton callback origin now identifies the actual opener without changing ordinary accessibility Invoke focus. Pending origin uses an opaque token, not a strong rendering-context reference. Programmatic calls outside callbacks retain previous-focus semantics; no binding-only/asynchronous provenance expansion is claimed.

Before: actual-opener CPU and Linux native assertions fail. After: current-token CPU107 includes ordinary Invoke, programmatic return and GC collection of a disposed UI context while its unmounted Dialog stays alive. Windows native UIA13/host2 and Linux AT-SPI14/host3/registry retirement pass. Previous functional-origin Dialog GPU160 and Observer RC3 NativeAOT33 scenarios pass; final token delivery is tracked separately. No throughput/memory-size speedup percentage is claimed. Initial lifetime fixture CS1503 used UiView where UiContext.Render requires UiScreen; corrected fixture preserves assertions and the failed build log. Evidence: artifacts/fluent-dialog-2026-11-15/focus-origin-verification.json. Original pointer acknowledgement timeout remains unresolved.


## Linux probe result contracts and opened NativeAOT Dialog

Detect:14 anonymous JSON writes and List<object> observation storage remained in the Linux app driver before NativeAOT publishing. Apply: shared typed records/source-generated metadata, reflection JSON disabled, preserved field names/casing/order and values, explicit result-contract mode. Original sources and14 output bodies are archived;15 original field lists match the new output including the observation. Managed/native13 contract checks each produce25 byte-identical result/golden JSON files.

Strict pinned SDK11.0.100-rc.3.26522.148 managed and NativeAOT publishing both exit0 without warning suppression. Native output is15,947,032-byte x64ELF exporting DotNetRuntimeContractDescriptor, no application/CoreCLR DLL distribution. Actual NativeAOT X11/Vulkan Dialog host and independently spawned NativeAOT libatspi client pass14 client/3 host checks and registry retirement; client JSON equals the previous managed result. No full Linux accessibility retry, physical reader/display, Windows AOT, Pi or new performance improvement claim. The change is deployment/verification correctness, not a measured speedup. Commands, hashes, limitations and final lock/format evidence: artifacts/linux-probe-results-aot-2026-11-15/verification.json.


## Linux 스트리밍 도형 배치의 작은 변경 범위

- 날짜/범위:2026-11-15, 공통 Vulkan damage·PrimitiveDamage 모듈, 공유 GPU 계약의 플랫폼 팩터리 및 Linux 전용 NativeAOT 호스트.
- 가설:105개 도형을 묶은 슬라이더 명령의 전체 경계가 정적 눈금까지 갱신한다. 첫 명령 단위 후보는 실제 Fluent 면적을 줄이지 못하고 입력 p95·p99가 악화되어 전달하지 않았다. 제한된 실제 명령 진단으로 배치 내부를 식별했고 임시 추적을 제거했다.
- 변경: 변경 analytic record와 동일 스타일 채움의 움직이는 끝을 비교한다. 작은 크기의 radius clamp·상주 기하·텍스처·마스크·상태 및 미지원 구조는 전체 명령 경계를 유지한다. 클립·프레임버퍼 좌표·각 획득 이미지와 MSAA 이력을 보존한다.
- SDK/조건:11.0.100-rc.3.26522.148 NativeAOT, llvmpipe LLVM20.1.2,1280×900, MSAA4, V-Sync off/event-wait on, 동일 폰트·실행물, `DUXEL_VK_SPARSE_DAMAGE=0/1`. 표시 재사용은 양쪽 모두 켜짐. 독립120Hz 입력, 화면당3라운드의1초 워밍업+2초 측정, A1/B1/B2/A2/A3/B3의3독립 프로세스 쌍.
- Before/After: 갱신 면적 중앙값 단독 슬라이더10680→816(-92.36%), Fluent108489→57855(-46.67%), 진단 고정 Fluent108489→55680(-48.68%). CPU(한 코어100%)23.977→21.851 및91.265→79.617은 구간 중첩으로 INVALID이며 개선율 주장 없음. 진단 고정 Fluent78.006→69.004(-11.54%), 범위70.262~82.844→63.781~69.321로 분리됨. 입력 지연 비교도 구간 중첩이며 음수 결과를 보존한다.
- 프레임 통계: producer 구성률은 실제 표시 FPS가 아니다. 이벤트 대기의 이전 표시·재사용 표본으로 display FPS·1%low를 계산할 수 없어 미측정으로 남긴다. dispatch/composition의 raw median/p95/p99/max는 원시 비교 JSON에 보존한다. 메모리 개선 주장 없음.
- 검증: Linux RC3 NativeAOT 슬롯1/3 각각3570개·608픽셀 쌍, Windows .NET11 관리형 슬롯1/3 각각3554개·604픽셀 쌍. 합계14248개·2424쌍 일치. 참조는 제출 시에도 전체 재그리기를 강제한다. 가시성·16/3·수평/수직·분수·반투명·라운딩 한계·128개 정적 도형·구조·마스크·그룹·실패·소유권을 포함한다. 실제 옵저버33화면·384입력·9정지 추가구성0·24움직임·숫자반복1→4·21화면복원 통과.
- 검증기 교정: 새 호스트의 조건부 내부 접근, 폰트 재설정, placeholder atlas의16px 행 높이와 명시적 흰색 카운터, Windows와 같은 품질 계측 설정 및 최상위 예외 기록을 교정했다. 가시성 검사로 이후 검사를 시작 전에 차단하며 이전 실패를 보존한다. 성능 실행은 계측을 켜지 않는다.
- 실행: `artifacts/linux-sparse-damage-2026-11-15/commands.json`, 원시 `L:/Cache/Duxel/linux-performance-recheck-20261115/primitive-controlled-comparison.json`. 단일 최신 실행기와 보호된 ELF의 SHA-256은 해당 verification.json이 권위다.
- 리스크/남은 일: CPU llvmpipe·물리 표시 지연과 사용자 체감 미검증, 원본 간헐적 최종 입력 ACK 시간 초과 미해결, 전체 Linux 접근성·Windows RC3 NativeAOT·전체 제품은 별도 미완료. 앞선 표시 재사용52.997%는 별도 실행물·비교이며 이번 결과에 더하지 않는다. 전체 접근성 재실행·리사이즈·MSVC/SDK 재탐색은 하지 않았다.


## Linux llvmpipe 작업 스레드 비용

- 가설: 작은 UI 갱신에24개 래스터 작업 스레드의 동기화가 과도하다. 기존 공식 `LP_NUM_THREADS` 설정을 재사용하고 소스·셰이더·폰트·MSAA·입력·갱신 면적은 유지했다.
- 선택: 기본/1/4의3개 예비 프로세스 후1개의 Fluent p99 이상치를 보존하고4개를 독립3쌍으로 검증했다. 실제 작업 스레드24→4, 총66→25개를 프로세스에서 확인했다.
- Before/After: 단독 슬라이더 CPU19.458509→12.142710%(37.596916% 감소), Fluent69.899893→43.491509%(37.780293%), 진단 고정 Fluent64.314616→39.549066%(38.506877%). 한 코어=100%이며 세 비교 모두 범위가 분리됐다.
- 입력: Fluent p95만0.5318→0.401625ms로 범위가 분리됐다. 다른 입력 중앙값/p95/p99는 중첩하며 악화값도 보존한다. 물리 표시 지연·표시 FPS·1%low·메모리는 미측정이다.
- 품질: 고정 RC3 NativeAOT 슬롯1/3의7,140개 검사,1,216쌍 원시 픽셀이 기존 기본 스레드 실행과도 일치한다.33페이지·384입력·9정지·24단계 드래그·반복·21복원을 확인했다.
- 적용: 최신 검증본 실행기는 같은 실행물 해시와 하나의 runtime 설정을 읽고 실행할 Linux 프로세스에만4개를 적용한다. 일반 루트 실행기는 명시적 `SoftwareRasterThreads` 인수로 선택하며 생략 시 기존 드라이버 기본값을 유지한다. 전역 환경·패키지·ICD는 변경하지 않았다.
- 리스크: 큰 SVG·창 크기·다른 CPU에서4개의 최적성을 입증하지 않았다. WSL GPU 드라이버는 실제 GPU를 열거했지만 dualSrcBlend 필수 기능 미지원으로 실행 관문이 실패했으며 활성화하지 않았다. 기존 간헐적 입력 ACK 실패와 실제 사용자 체감은 미확인이다.
- 근거: `artifacts/linux-driver-worker-budget-2026-11-15/verification.json`, `commands.json`, 외부 원시 `worker-controlled-comparison.json`. 이전 개선율과 합산하지 않는다.


## Skeleton 옵저버 전달 전 성능 대조

- 변경: 새 위젯·접근성·31번째 관찰 탭. 성능 최적화나 새 개선율 주장이 아닌 전달 전 회귀 대조입니다.
- 조건: 고정 .NET11 RC3 NativeAOT·4작업 스레드·1280×900·MSAA4·같은 폰트·이벤트 대기·VSync꺼짐·120Hz 입력. 각 조건3독립 프로세스, 시나리오별3회/1초 예열+2초 측정. 실제 순서와 원시 입력 해시를 보존했습니다.
- CPU 한 코어 기준 중앙값 Before→After: 슬라이더12.08961→11.79215%, Fluent46.41784→46.13362%, 진단 고정39.39995→40.29253%. 모두 범위가 겹치며 개선율을 주장하지 않습니다. 마지막 음수 변화도 숨기지 않습니다.
- 입력 p95:0.33162→0.33374ms /0.48560→0.39788ms /0.36112→0.32884ms. 추적한 CPU/입력/구성 지표에서 구간이 분리된 악화는 관측하지 않았지만 다른 부하의 무퇴행을 인증하지 않습니다.
- 첫 candidate1/2는 프로세스 종료 전에 다음 검증 창을 열어 겹쳤으므로 제외했습니다. timeout 원문과 제외 이유, 정확한2회 대체 예산을 보존했습니다. 과거 ACK 시간 초과의 원인을 해결했다고 주장하지 않습니다.
- 실제 표시 FPS·raw 프레임 간격·1%low·메모리·물리 표시 지연은 이 비교로 측정하지 않았습니다. producer 구성률을 표시 FPS로 사용하지 않습니다.
- 근거: artifacts/fluent-skeleton-2026-11-15/observer-comparison.json. 실행기 단일 원장 artifacts/linux-observer-delivery.json. 이전 성능 실행물과 원장은 보존합니다.


## 일반 텍스트 의미 정보 게시의 옵저버 비용 대조

- 변경: 기존 UiText 배치 루트에서만 정적 의미 정보를 수집합니다. 입력 정체성 기본값·표현·소스 읽기 횟수는 유지하며 접근성 트리 없이는 식별자 문자열을 만들지 않습니다. 텍스트별 지연 식별자 참조1개가 추가됩니다. 메모리 개선을 주장하지 않습니다.
- 조건: 이전45c6a5와 새d110b4의 고정 RC3 NativeAOT·4스레드·동일1280×900/MSAA4/폰트/이벤트 대기/VSync꺼짐/120Hz 입력. 각3독립 프로세스·시나리오당3회/1초 예열+2초 측정. 단일 전경 PowerShell 루프가 wsl 종료를 기다리고 실패 시 중단합니다. 기존 compare-stream-results.py에 scope를 데이터로 전달했습니다.
- CPU 중앙값 Before→After:12.68723→13.25769%,46.72854→49.16636%,40.65653→42.02906%. 음수 변화4.50/5.22/3.38%를 보존하며 모든 범위가 겹쳐 방향은 INVALID/미확정입니다. 특히 candidate2의 큰 값도 제외하지 않았습니다.
- Fluent 입력p95:0.4090→0.7361ms. 다른 입력·구성 지표도 원시 범위와 음수 값을 보존합니다. 실제 표시 FPS·raw 표시 간격·1%low·물리 표시 지연·메모리는 이 비교에서 측정하지 않았습니다.
- 기능: CPU64·Label회귀63, GPU24원시쌍, 실제 UIA/AT-SPI 정적 내용 갱신, 옵저버34표시·9유휴·21복원·실행기120Hz입력·실제4작업 스레드 확인. 기능·픽셀·전달 경로를 검증했으며 성능 향상·무퇴행 증명과 구분합니다. 이전2ad2a9/45c6a5 실행물은 보존합니다.
- 근거: artifacts/text-accessibility-2026-11-15/verification.json 및 observer-comparison.json. 현재 단일 배포 원장 artifacts/linux-observer-delivery.json. 원래 간헐적 ACK 실패·전체 제품 목표는 미완료입니다.
