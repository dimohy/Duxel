# Linux Vulkan 원시 픽셀 계약

.NET 11.0.0과 프레임 슬롯 3개로 실행해 Windows에서 3,554개, WSLg Linux의 llvmpipe에서 3,570개 검사를 통과했습니다. 각 운영체제에서 전체 다시 그리기와 화면 재사용의 픽셀이 일치했습니다. 운영체제 간에는 이름이 같은 RGBA 574쌍 중 214쌍이 일치했고 작은 영역 갱신 장면 160쌍은 모두 일치했습니다. 텍스트와 일부 둥근 도형 장면을 포함한 360쌍에는 차이가 있었습니다. [장면별 비교 결과](W:/Cache/Duxel/gallery-source-20261117/cross-host-present-1938/comparison.json).

아래 Linux 명령 끝에 `host-pixels`를 붙이거나 Windows에서 `Duxel.Graphics.Tests <새-결과-폴더> presentation-reuse-host-pixels`를 실행합니다. 프레임 슬롯 3개로 맞추면 두 명령 모두 같은 전체 다시 그리기 장면의 320 × 240 RGBA 원본 10개를 저장합니다. 작은 영역 갱신 대조 장면 2쌍은 일치했습니다. 둥근 도형 장면 6쌍에서는 RGB 채널 값이 최대 1만 달랐고 알파 값은 모두 같았습니다. 4배 MSAA에서는 반복되는 반투명 막대에도 차이가 있었습니다. [채널별 차이](W:/Cache/Duxel/gallery-source-20261117/cross-host-rgba-1939/rgba-comparison.json).

글자 장면에서는 첫 프레임의 텍스처도 Vulkan에 보내기 전에 저장합니다. 같은 Segoe UI 파일을 사용해도 Windows와 Linux의 글자 텍스처 크기와 픽셀 불투명도가 이미 달랐습니다. 1배와 4배 MSAA 모두 글자가 있는 줄에서 489픽셀이 달랐고, 다른 줄은 일치했습니다. [글자 원본과 최종 프레임 비교](W:/Cache/Duxel/gallery-source-20261117/cross-host-text-1940/text-comparison.json).

두 운영체제의 Vulkan 경로에 같은 글자 텍스처를 넣어 비교할 수 있습니다. 아래 Linux 명령 뒤에 `glyph-texture-replay <RGBA 파일> <너비> <높이>`를 붙입니다. Windows에서는 `Duxel.Graphics.Tests <새 결과 폴더> glyph-texture-replay <RGBA 파일> <너비> <높이>`를 실행합니다. 글자 장면에서 저장한 RGBA8 원본을 넘기면 정수·소수점 좌표와 1배·4배 MSAA를 조합한 320 × 240 프레임 네 장을 저장합니다.

Windows와 Linux에서 가져온 원본을 하나씩 사용했습니다. Linux 관리형과 NativeAOT 사이에서는 프레임 8장이 모두 일치했습니다. Windows 관리형과 Linux 관리형 사이에서 RGB 채널 값은 정수 좌표에서 최대 1, 소수점 좌표에서 최대 2 차이가 났고 알파 값은 모두 일치했습니다. 이 검사는 입력과 위치를 고정한 텍스처 사각형을 비교합니다. 운영체제별 글자 배치와 래스터화는 별도 검증 범위입니다. [프레임별 비교 결과](W:/Cache/Duxel/gallery-source-20261117/glyph-texture-replay-2039/verification.json)

같은 Linux NativeAOT 실행 파일을 WSLg의 lavapipe 25.2.8과 Ubuntu 26.04 컨테이너의 26.0.8에서 실행했습니다. 두 환경에서 같은 X 서버를 사용했습니다. 정수 좌표의 네 장은 일치했고, 소수점 좌표의 네 장은 RGB 채널 값이 최대 3 달랐습니다. 알파 값은 모두 같았습니다. 컨테이너는 다른 시스템 라이브러리도 사용하므로 차이를 Mesa 한 가지 변경에 돌릴 수 없습니다. WSL Dozen에서 조회한 GPU 두 대는 모두 `dualSrcBlend`를 지원하지 않았습니다. [드라이버 비교와 장치 기능](W:/Cache/Duxel/gallery-source-20261117/glyph-texture-driver-2040/verification.json)

아래 실행 명령 뒤에 `early-init-cleanup`을 붙이면 X11 창 하나에서 초기화 실패 후 다시 시도하는 경로를 검사합니다. 표면, 장치 함수, 스왑체인, 그래픽 파이프라인을 준비한 직후와 초기화가 끝난 직후에 각각 예외를 발생시킵니다. 원래 예외가 호출자에게 전달되는지 확인하고 같은 창에서 렌더러를 다시 초기화합니다.

Windows와 같은 PresentationReuseTests·PaintGroupRenderingTests 소스를 사용합니다. 창·폰트 생성만 플랫폼별로 분리하며 독립 참조는 전체 재그리기를 강제합니다. 전체 접근성 실행기가 아닙니다.

명령 끝에 `tree-gpu`를 붙이면 Tree의 계층, 체크박스·펼치기 화살표 클릭, RTL 배치, 테마별 호버 픽셀을 검사합니다. `tree-label-gpu`를 붙이면 밝은 RTL·어두운 LTR에서 긴 레이블의 줄바꿈과 말줄임을 확인합니다. 두 모드 모두 Windows와 같은 640 × 420 검사 코드를 사용하며 PNG와 런타임·프레임 슬롯·통과 항목을 담은 `result.json`을 저장합니다.

명령 끝에 `container-pixels`를 붙이면 ScrollView와 사용자 정의 컨테이너를 따로 그린 기준 화면과 같은 환경에서 비교합니다. 밝은·어두운 테마, 1배·4배 MSAA, RTL, 클리핑, 할당 크기가 그대로인 Grid의 재배치를 검사합니다. Windows 명령 `Duxel.Graphics.Tests <새-결과-폴더> container-pixels`와 조건을 맞추려면 슬롯을 3개로 지정하세요.

명령 끝에 `virtual-list-pixels`를 붙이면 고정·가변 높이 가상 목록을 기준 화면과 비교합니다. Windows 명령 `Duxel.Graphics.Tests <새-결과-폴더> virtual-list-pixels`와 조건을 맞추려면 슬롯을 3개로 지정하세요.

`virtual-list-host-pixels`는 두 방향의 목록 끝에서 소수 오프셋을 적용한 RGBA 이미지를 저장합니다. 장면마다 배경, 도형, 네이티브 글자까지 포함한 화면을 각각 저장합니다. 플랫폼 간 비교에는 같은 글꼴 파일과 슬롯 3개를 사용합니다.

`clip-host-pixels`는 사각형과 삼각형 메시만 사용한 클립 경계 장면을 저장합니다.

`virtual-grid-pixels`는 두 축의 가상 그리드를 기준 화면과 비교합니다. Windows의 `Duxel.Graphics.Tests <새-결과-폴더> virtual-grid-pixels`와 같은 조건으로 실행하려면 슬롯을 3개로 지정하세요.

두 환경에서 `virtual-grid-host-pixels`를 쓰면 비교용 화면을 원시 RGBA로 추가 저장합니다. 파일 이름에 이미지 크기가 표시됩니다. 고정 크기와 클립이 적용된 가변 크기 그리드의 끝에서는 셀 테두리, 원, 스크롤바를 따로 그리고 Vulkan 클립 기록도 저장합니다.
사각형 테두리 비교에서는 좌표, 사각형별 클립, 레이어 종류를 각각 바꿔 확인합니다.
1× MSAA에서는 소수 좌표에 그린 테두리 픽셀과 클립 바로 바깥의 빈 픽셀도 도형의 좌표로 계산한 값과 비교합니다.

프로젝션 레이어의 사각형을 측정하려면 `projected-clip-bench`, 텍스처가 있는 이미지를 측정하려면 `projected-image-clip-bench`를 붙이세요. Windows의 `image-shader-bench 3 projected-clip` 및 `color-image` 옵션과 같은 장면을 사용합니다. 프레임별 시간, 그리기 호출 수, 업로드 크기, 이미지 해시를 저장합니다.

결과에는 RGBA 프레임마다 SHA-256 해시가 들어 있습니다. SDK 11.0.100, 런타임 11.0.0, 프레임 슬롯 3개로 WSLg의 Mesa lavapipe에서 확인했습니다. 관리 실행과 Linux x64 NativeAOT가 각각 612건을 통과했고 대응하는 프레임 628쌍의 해시가 모두 일치했습니다.

실행 인수는 `<새 결과 경로> <슬롯1 또는3> <Latin regular> <Latin bold> <Korean regular> <Korean bold> <monospace>`입니다. 모든 폰트를 창 생성 전에 검증합니다. 결과는 `result.json`이며 실패 시 원문 오류를 기록하고0이 아닌 코드로 종료합니다. 빌드는 `-p:DuxelValidationHooks=true`가 필요합니다. 고정 RC3 NativeAOT 명령·실행물 해시·검사 범위는 [통합 가이드](../../artifacts/linux-sparse-damage-2026-11-15/README.ko.md)와 해당 `commands.json`에 있습니다.

기본 화면 재사용 검사는 합성 그룹이 읽는 텍스처를 바꾼 뒤, 초기화된 이미지에서 그 텍스처를 쓰는 영역만 다시 그리는지 확인합니다. 1배·4배 MSAA에서 전체 다시 그리기와 픽셀이 일치해야 합니다. WSLg의 .NET 11.0.0 관리형과 NativeAOT에서 프레임 슬롯 1개와 3개 각각 합성 그룹 이미지 44장이 일치했습니다. [픽셀 비교 결과](W:/Cache/Duxel/gallery-source-20261117/paint-group-linux-2125/verification.json)
