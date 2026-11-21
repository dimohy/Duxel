# 위젯 계약 통합 검증

Linux x64 WSLg에서 .NET SDK 11.0.100과 런타임 11.0.0, llvmpipe로 검증했습니다. 관리형과 NativeAOT는 고정 크기 또는 화면 없는 검사 16회 중 각각 15회를 통과했습니다. 화면 표시 해시 2,592쌍, 컨테이너·가상 레이아웃 캡처 해시 4,160쌍, 별도로 내보낸 파일 458쌍이 일치했습니다. 내보낸 파일 일부는 해시 집계와 겹칩니다. 문단 검사 두 회는 Pango 전체 그리기에서 기존과 같은 7건과 알파 24바이트 차이를 재현했고, 글리프 묶음별 독립 비교 36건은 각각 통과했습니다. 실행 중 창 크기가 바뀐 SVG 교체 검사 네 회는 고정 크기 비교에서 제외했습니다. [검증 결과](L:/Cache/Duxel/linux-vulkan-ga-contract-20261120/verification.json).

Linux x64 .NET 11 관리형·NativeAOT에서 640×128 고정 창, 프레임 슬롯 1·3개, 1×·4× MSAA로 같은 문단 입력 36개를 Vulkan 글꼴 경로에서 비교했습니다. Duxel 글리프 묶음을 합성한 출력은 독립 마스크 기준과 288건 모두 픽셀 단위로 일치했습니다. Pango 전체와는 각 조건에서 기존과 같은 7건·24픽셀에 차이가 있었고, 다른 위치에는 차이가 없었습니다. 별도의 Cairo 대조에서는 텍스트가 닿지 않는 곳에 한 픽셀을 그린 전체 크기 임시 표면이 일곱 경우 모두 전체 그리기와 같았습니다. 이 검사는 글리프 묶음의 Vulkan 합성을 다루며 문단 위젯의 레이아웃과 배치는 기존 검증 범위에 따릅니다. [픽셀 비교](L:/Cache/Duxel/linux-styled-vulkan-20261121/verification.json)

Linux x64 .NET 11 관리형·NativeAOT에서 단일 색의 Pango 문단 36개를 정수 위치 세 곳에서 그렸습니다. 잉크 경계에 1픽셀 여백을 둔 전체 문단 표면은 108개 모두 640×128 기준과 같았습니다. 여백이 없으면 같은 문단의 세 위치에서 각각 1픽셀이 빠졌습니다. A8 임시 표면은 81,920바이트에서 중앙값 1,820바이트로 줄었습니다. 글리프 묶음마다 0으로 채운 버퍼를 제공하는 방식은 전체 크기와 자른 크기 모두 36개 중 11개가 달랐습니다. 후보는 검증용 복사본에서 실행했고 여러 글자 색과 위젯의 클립·캐시 수명은 별도 검증이 필요합니다. [표면 비교](L:/Cache/Duxel/linux-pango-surface-20261121/verification.json)

잉크 경계에 1픽셀 여백을 둔 문단 텍스처를 640×128 고정 창의 Vulkan 글꼴 경로에 올렸습니다. Linux x64 관리형·NativeAOT와 프레임 슬롯 1·3개에서 문단 36개, 위치 세 곳, 1×·4× MSAA를 대조했습니다. 흰색 전체 그리기, 소수점 경계에서 자른 단색, 인접 영역에 적용한 두 반투명 색을 포함해 전체 크기 기준과 2,592쌍이 픽셀 단위로 일치했습니다. 텍스처를 다시 올리지 않고 다음 프레임에 그린 5,184회도 첫 프레임과 같았습니다. 색은 동일한 문단 마스크에 영역별로 적용했습니다. 실제 위젯의 문단 캐시 연동과 Pango 전체 그리기 엄격 검사 7건은 남아 있습니다. [검증 결과](L:/Cache/Duxel/linux-pango-clipped-vulkan-20261121/verification.json)

실제 `UiInlineText` 위젯의 문단과 글꼴 텍스처 재사용 검사를 `inline-paragraph-cache`로 추가했습니다. Linux x64 .NET 11 관리형·NativeAOT에서 프레임 슬롯 1·3개와 1×·4× MSAA를 사용했습니다. 기존 위젯과 같은 상태로 새로 만든 위젯의 Vulkan 출력 80쌍이 모두 일치했습니다. 색·정수 위치·높이 클립만 바꿀 때 기존 레이아웃과 텍스처를 사용했습니다. 최근 너비로 복귀하면 이전 레이아웃을 재사용했고, 글자나 읽기 방향이 바뀌면 레이아웃을 다시 만들었습니다. [검증 결과](L:/Cache/Duxel/linux-inline-paragraph-cache-20261121/verification.json)

Linux 백엔드가 Pango 레이아웃의 잉크 경계에 1픽셀 여백을 둔 전체 문단 마스크를 생성할 수 있습니다. 기존 레이아웃과 같은 글꼴 선택과 방향을 사용하며 Pango 객체는 호출 안에서 해제합니다. 관리형과 NativeAOT에서 각각 36개 마스크가 독립 Pango 기준과 같았고 그릴 잉크가 없는 8개 입력은 빈 마스크를 반환했습니다. 기존 위젯의 Vulkan 픽셀 20개도 두 실행 방식에서 각각 이전 기록과 같았습니다. 전체 마스크를 사용할 위젯의 줄 배치·색·캐시 조건은 남아 있습니다. [검증 결과](L:/Cache/Duxel/linux-pango-native-raster-20261121/verification.json)

Linux의 `UiInlineText`는 글자 색이 불투명하고 글자 꾸밈이 없으며 Pango와 Duxel의 줄 배치가 맞는 작은 문단을 마스크 하나로 그립니다. 텍스트, 글꼴, 너비, 읽기 방향이 같은 요청은 위젯이 다시 생성돼도 글꼴 텍스처를 공유합니다. 반투명 글자, 줄 배치가 다른 문단과 벡터 글자는 글리프 묶음별로 그립니다. 실제 검사 문단의 첫 업로드는 7개에서 1개로 줄었고, Linux 관리형·NativeAOT 네 조합에서 각각 새 위젯과 픽셀 22쌍, 캐시 상태 22개가 일치했습니다. 별도 글리프 묶음 경로의 엄격 Pango 비교 7건은 남아 있습니다. [검증 결과](L:/Cache/Duxel/linux-inline-whole-mask-20261121/verification.json)

별도 NativeAOT 실행 파일 하나로 640×480 고정 Vulkan 창의 `UiInlineText` 12개를, 전체 마스크를 켜고 끈 조건에서 각각 세 번 실행했습니다. 모든 160프레임의 해시가 두 조건에서 같았습니다. 정지 구간의 구성 시간 중앙값은 61.40→27.50µs, 그리기 호출은 84→12회였습니다. 첫 프레임 글꼴 텍스처 업로드는 18→12개였고 바이트는 41,596→208,224로 늘었습니다. 내용이 바뀐 프레임의 업로드는 1,976→17,352바이트, 구성 시간은 377.80→413.30µs였습니다. 읽어오기를 포함한 정지 구간 렌더링 시간은 실행 범위가 겹쳤습니다. [비교 결과](L:/Cache/Duxel/linux-inline-whole-mask-ab-20261121/verification.json)

Linux x64 NativeAOT의 격리된 Vulkan 빌드에서 전체 문단 마스크를 RGBA와 단일 채널 R8로 각각 세 번 비교했습니다. 두 형식에서 160프레임의 픽셀 해시가 모두 일치했고, 첫 프레임의 글꼴 텍스처 전송량은 208,224→52,056바이트, 내용이 바뀐 프레임은 17,352→4,338바이트로 각각 75% 줄었습니다. MSAA 1×·4×와 프레임 슬롯 1개·3개를 사용한 회귀 검사에서도 실행당 22개 검사를 통과했고 캡처 해시가 일치했습니다. 다음 검증은 제품의 텍스처 갱신·폐기 경로와 다른 렌더러의 호환성을 대상으로 합니다. [성능 비교](L:/Cache/Duxel/linux-inline-mask-r8-20261121/verification.json) [회귀 검사](L:/Cache/Duxel/linux-inline-mask-r8-20261121/cache-verification.json)

기본 Vulkan 앱에서 조건을 충족하는 Linux 문단 마스크에 R8을 적용했습니다. 직접 만든 컨텍스트는 기본값인 RGBA를 사용합니다. llvmpipe의 관리형·NativeAOT 실행을 프레임 슬롯 1개와 3개로 각각 검사했습니다. 네 조합에서 27개 검사를 모두 통과했고 1×·4× MSAA의 문단 화면 22개가 RGBA와 일치했습니다. 생성, 홀수 바이트 크기의 영역 두 곳을 한 번에 갱신, 폐기 및 재생성도 RGBA와 비교했습니다. [제품 검증](L:/Cache/Duxel/linux-inline-mask-r8-product-20261121/verification.json)

실제 Linux Gallery 타이포그래피 페이지의 상단 설명을 바꾸며 R8과 RGBA를 세 번씩 비교했습니다. Linux x64 NativeAOT 실행 파일 하나, 1280×900 고정 창, MSAA4, llvmpipe를 사용했습니다. 각 실행에서 준비 프레임 96개 뒤 표본 96개를 기록했고, 텍스트가 바뀐 32개는 모두 새 Vulkan 표시 요청으로 이어졌습니다. 변경 프레임의 텍스처 전송량은 25,364→6,341바이트로 75% 줄었습니다. 변경 구간의 표시 간격 중앙값은 6.95→6.82ms, 준비 구간을 포함한 프로세스 CPU 시간은 1.66→1.64초, 최대 상주 메모리는 560,980→561,192KiB였으며 세 지표 모두 실행별 범위가 겹쳤습니다. 화면이 유지된 표본 64개는 기존 표시를 재사용했습니다. Windows 기준 실행 파일 비교와 물리 GPU 검증은 미완료입니다. [실행별 원본과 비교](L:/Cache/Duxel/linux-gallery-r8-ab-20261121/verification.json)

별도 Cairo 대조에서 잘라낸 표면의 표시점과 지우는 위치를 좌표 변환에 맞췄습니다. 글리프를 처음 그릴 때는 빈 A8 표면을 쓰고, 뒤의 묶음은 앞에서 잉크가 그려졌는지에 맞춰 표면 상태를 설정했습니다. 잉크 없는 글리프도 원래 순서대로 제출하고 한 줄의 잉크 경계에 1픽셀 여백을 두었을 때, 관리형과 NativeAOT 모두 독립 Pango 기준 36개와 정확히 일치했습니다. 제품의 기존 글리프 묶음 경로는 같은 7건에서 알파 24바이트가 달랐습니다. 다음 제품 검증은 줄 범위와 앞선 그리기 상태가 캐시 재사용과 클리핑에 미치는 영향을 확인합니다. [대조 결과](L:/Cache/Duxel/linux-pango-run-origin-20261121/verification.json)

Linux 제품의 글리프 묶음 래스터화에 줄의 잉크 경계와 앞선 그리기 상태를 반영했습니다. Vulkan에는 실제 잉크 주변만 잘라 올립니다. 캐시가 줄 경계나 앞선 그리기 상태가 달라진 마스크를 구분하고, 마스크가 정수 픽셀만큼 이동할 때는 재사용하는지 검사했습니다. 관리형과 NativeAOT에서 각각 411개 검사를 통과했고, 기존 Pango 기준과 달랐던 7건도 일치했습니다. 640×128 고정 창에서 1×·4× MSAA와 프레임 슬롯 1개·3개로 대조한 Vulkan 글꼴 화면 288개가 독립 Pango 기준과 같았습니다. 물리 GPU와 최초 래스터화 비용은 별도 검증 대상입니다. [검증 결과](L:/Cache/Duxel/linux-pango-product-runs-20261121/verification.json)

Linux 문단의 래스터화에서 Cairo의 A8 표면을 직접 읽고, 잉크 주변만 관리 배열로 복사하도록 바꿨습니다. 같은 NativeAOT 비교 빌드에서 두 방식을 각각 세 번 실행했습니다. 가장 긴 384묶음 문단의 래스터화는 14.41→4.26ms, 관리 힙 할당은 113.28→1.35MB였습니다. 네 입력의 마스크는 최적화 전과 정확히 같았고, 제품 빌드도 기존 문단 411개 검사와 텍스처 캐시 회귀를 통과했습니다. 별도 반복 문단 네 종류에서는 Pango 전체 그리기와 여전히 5·24·25·45픽셀이 달랐습니다. 해당 픽셀은 글리프 마스크가 겹치는 위치에 없었습니다. [성능 비교](L:/Cache/Duxel/linux-pango-run-cost-20261121/crop-verification.json) [제품 검증](L:/Cache/Duxel/linux-pango-run-cost-20261121/product-verification.json)

반복 문단 네 입력의 픽셀 차이는 Cairo에 글리프 묶음을 넘기는 방식에서 생겼습니다. Pango의 원래 묶음을 유지하면 Duxel 글꼴을 사용해 직접 그리거나 줄 단위 마스크로 합성해도 전체 Pango 그리기와 같았습니다. 같은 글리프 ID·순서·좌표를 스타일 경계에서 나눠 한 표면에 직접 그리자 제품의 5·24·25·45픽셀 차이가 그대로 재현됐습니다. 관리형과 NativeAOT의 원본 마스크도 일치했습니다. 제품 수정은 같은 방식으로 칠하는 구간의 원래 묶음을 유지할 수 있는지, 실제로 색이 다른 구간도 각각 올바르게 그리는지 확인한 뒤 적용합니다. [단계별 마스크 대조](L:/Cache/Duxel/linux-pango-run-trace-20261121/verification.json)

Linux 문단은 같은 글자색 규칙을 쓰는 구간에서 Pango의 원래 글리프 묶음을 유지합니다. 링크·구문·코드 토큰처럼 색 규칙이 다른 구간은 각각 그립니다. 반복 문단 네 입력이 Pango 전체 그리기와 정확히 일치했고, Vulkan 코드뷰에서는 키워드색과 문자열색을 각각 확인했습니다. 코드뷰가 줄 위치를 옮겨도 래스터화 정보가 유지되며, 앞줄의 색 규칙이 바뀌어도 뒤쪽의 변경 없는 줄은 네이티브 레이아웃을 재사용합니다. [제품 검증과 성능 비교](L:/Cache/Duxel/linux-pango-style-groups-20261121/verification.json)

현재 제품의 Linux x64 고정 크기 깊이 장면을 관리형·NativeAOT와 프레임 슬롯 1개·3개로 검증했습니다. 320×240 llvmpipe에서 실행당 검사 45개를 통과했고 캡처 106장은 네 실행에서 바이트 단위로 일치했습니다. 뷰포트 두 곳의 원근 장면은 독립 기준과 1×에서 52·56픽셀, 4×에서 175·174픽셀이 달랐습니다. 격리한 명시적 샘플 마스크 후보는 네 조합 모두 0픽셀이었습니다. 후보를 적용한 반투명 알파 128 장면은 제품과 1×에서 3,329픽셀, 4×에서 6,628픽셀이 달랐습니다. 불투명 재질만 대상으로 하는 경로에서 픽셀과 GPU 비용을 확인해야 합니다. 이번 실행은 llvmpipe 결과입니다. [제품 검증](L:/Cache/Duxel/linux-depth-boundary-20261121/verification.json) [격리 후보](L:/Cache/Duxel/linux-depth-boundary-20261121/candidate-verification.json)

Linux x64의 격리 복사본에서 불투명 재질에만 명시적 샘플 마스크를 사용했습니다. 320×240 고정 창에서 뷰포트 두 곳과 1×·4× MSAA의 원근 경계가 독립 기준과 일치했습니다. 반투명, 텍스처 부분 갱신, 교체된 외부 도형을 포함한 전체 화면 또는 비교 영역 18곳이 제품과 바이트 단위로 일치했습니다. NativeAOT 비교는 두 방식 세 번씩, 실행마다 예열 96개·측정 256프레임이었습니다. llvmpipe Vulkan 시간은 4.74→5.80ms(+22.23%), 프레임 시간 중앙값은 4.98→6.01ms(+20.83%)였습니다. 내부 버퍼를 강제로 수정한 대조군 네 조합에서 불투명 판정은 잘못 유지됐고 해당 캡처의 픽셀 차이는 0이었습니다. 후보는 제품에 반영하지 않았습니다. [검증 결과](L:/Cache/Duxel/linux-depth-routed-20261121/verification.json)

불투명 깊이 후보의 4× MSAA 셰이더에서 모든 샘플이 도형 밖에 있는 픽셀을 일괄 제외하는 대조를 실행했습니다. Linux x64 NativeAOT와 llvmpipe의 320×240 고정 창에서 1×·4× MSAA 및 프레임 슬롯 1개·3개의 캡처 124장이 기존 후보와 모두 일치했습니다. 두 셰이더는 같은 Glslang 15.1.0에서 빌드했고 Vulkan 1.2 대상으로 SPIR-V를 검증했습니다. 실행 전 후보를 다시 컴파일한 결과도 이전 캡처 124장과 같았습니다. NativeAOT 교대 측정에서 Vulkan 시간 중앙값은 5.42→5.33ms, 프레임 시간 중앙값은 5.64→5.59ms였으며 세 번씩의 실행 범위가 모두 겹쳤습니다. 성능 개선은 확인되지 않았습니다. [픽셀과 성능 대조](L:/Cache/Duxel/linux-depth-mask-bounds-20261121/verification.json)

정식 .NET 11 Linux 앱 검사는 관리형과 NativeAOT의 아홉 가지 시나리오에서 실행 방식마다 검사 859개를 대조했습니다. X11 입력 순서, 시작 재시도, 텍스트 배치와 키보드 동작, 결과 직렬화, 별도 AT-SPI 클라이언트를 통한 Dialog·MessageBar 검사를 포함합니다. 텍스트 스크롤은 이모지 글리프가 있는 글꼴을 지정한 뒤 양쪽에서 691개를 통과했습니다. 관리형 Dialog는 첫 모달 상태 확인에서 한 번 실패한 뒤 세 번 통과했고, NativeAOT는 네 번 모두 통과했습니다. [앱 검증 결과](L:/Cache/Duxel/linux-app-ga-contract-20261120/verification.json).

Windows의 `primitive-commands` 검사는 물리 픽셀 기준 320×240 고정 창을 만듭니다. 화면 전체를 덮는 불투명 도형이 1×·4× MSAA에서 프레임버퍼의 마지막 행까지 그려지는지 확인합니다. Vulkan 기본 레이어 뷰포트는 실제 프레임버퍼 크기를 사용합니다. 이전 Intel 실행은 새 검사에서 실패했고, 수정한 렌더러는 Intel과 NVIDIA에서 통과했습니다. [검증 결과](L:/Cache/Duxel/overlay-bottom-row-20261120/verification.json).

기본 도형의 경계에서 픽셀을 얼마나 덮는지 계산할 때 프래그먼트의 세밀한 미분값을 사용합니다. Intel과 NVIDIA를 320×240 고정 창에서 비교한 결과, RGB 채널 값이 1보다 크게 차이 난 이미지가 100개 중 20개에서 6개로 줄었습니다. 남은 6개는 1×·4× MSAA의 깊이 순서 검사 이미지입니다. 새 제품 빌드는 GPU마다 기본 도형 검사 76개를 통과했고, 각 GPU에서 검증한 후보 이미지 100개와 모두 일치했습니다. [제품 검증 결과](L:/Cache/Duxel/gradient-coverage-20261120/product-verification.json).

기본 검사 목록은 Graphics의 모든 `*-only` 모드를 분류하며 Control 실행 파일을 포함해 CPU 검사 묶음 138개를 실행합니다. Vulkan 픽셀 읽기와 네이티브 앱·입력 검사는 별도 항목으로 등록합니다. Observer 일괄 검사는 뷰포트 크기를 소수점 단위로 바꾸므로 리사이즈 검증을 재개할 때 따로 실행합니다. Gallery 통합은 고정 크기 시각 검사와 예제 소스 검사에서 확인합니다.

## 정식 .NET 11 런타임 비교

Windows x64 관리형과 NativeAOT를 SDK 11.0.100, 런타임 11.0.0, 프레임 슬롯 3으로 맞췄습니다. `widgets-no-resize`는 런타임마다 검사 4,642개를 통과했고 GPU RGB 이미지 4,038쌍이 바이트 단위로 일치했습니다. 별도 `containers`는 런타임마다 검사 263개를 통과했고 MSAA 1·4에서 이미지 185쌍이 일치했습니다. 제외한 창·리사이즈 검사는 `widgets-no-resize` 결과에 기록했습니다. 텍스트 생성 할당 검사는 양쪽 모두 같은 조건을 통과했고 각 측정값을 비교 결과에 남겼습니다. 근거 위치는 `W:/Cache/Duxel/gallery-source-20261117/common-widgets-ga-1908`와 `W:/Cache/Duxel/gallery-source-20261117/containers-ga-1909`입니다.

## 실제 컴파일러 선택과 선언 제거 (2026-11-15)

생성기 검사는 .NET 런타임과 API 참조 버전, 실제 Roslyn 어셈블리 버전·커밋, 실행 모드와 로드 문맥을 구분해 기록합니다. 기본 검사는 기존4.10 패키지로 컴파일·실행합니다. `-p:DuxelUseSdkRoslyn=true`는 선택한 SDK의 실제 컴파일러로 생성기 드라이버를 실행합니다. `DuxelSdkRoslynDirectory`로 디렉터리를 명시할 수 있으며, 생략하면 `MSBuildToolsPath/Roslyn/bincore` 한 경로를 사용합니다. 잘못된 모드와 선택한 파일 누락은 DUXTEST001/002/003으로 빌드 전에 거부합니다.

SDK 모드는 선택한 파일을 `SdkRoslyn`에 복사하고 해제 가능한 독립 AssemblyLoadContext에서 검사를 실행하며 Duxel.Core 공개 계약은 공유합니다. Linux SDK의 ReadyToRun 컴파일러를 이식 가능한 컴파일 참조로 취급하거나 원본을 고치지 않고 실행합니다. API 컴파일 참조는4.10을 유지하고 실제 생성기를 실행하는 컴파일러를 명시적으로 선택합니다. 패키지 컴파일러가 있어도 선택한 SDK 실행 파일이 없으면 실패합니다. 동적 컴파일을 사용하는 관리형 검사이며 NativeAOT 런타임 검사는 아닙니다.

최종 Windows .NET11/패키지4.10, Windows .NET11/SDK Roslyn5.12, Linux 고정 SDK11.0.100-rc.3.26522.148·런타임11.0.0-rc.3.26522.148/해당 SDK Roslyn5.12에서 각각131개가 통과했습니다. 저장한 생성 소스6개는 세 프로필 모두 이전117개 검사 기준의 파일과 바이트가 같습니다. 제품 생성기 구현·런타임 위젯·제품 의존성은 바꾸지 않았습니다.

같은 파일과 독립 소스 트리에서 첫/중간/마지막 선언을 제거하면 남은 두 출력의 재사용은 세 프로필 모두0/1/2개입니다. 실제 타입 제거와 소스 내용 보존은 통과하지만, 첫 선언 제거의 원래2/2 캐시 기준은 미완료입니다. 파일을 나누어도 충족하지 못했습니다. 패키지의 정확한 Roslyn 소스 커밋은 `2b7d172669b2f7e55803b55f317cfcc2d4279d76`이며 [상태 테이블 구현](https://github.com/dotnet/roslyn/blob/2b7d172669b2f7e55803b55f317cfcc2d4279d76/src/Compilers/Core/Portable/SourceGeneration/Nodes/NodeStateTable.cs#L309)은 대응 위치를 비교합니다. 현재 경로의 검증된 한계이며 최적화 완료나 모든 다른 설계가 불가능하다는 뜻은 아닙니다.

기존 생성기 검사 게시·실행 명령에 `-p:DuxelUseSdkRoslyn=true`를 주면 SDK 모드입니다. 생략하거나 `false`를 주면 패키지 모드입니다. 작업 디렉터리/global.json 또는 명시한 dotnet 실행물로 SDK를 선택하고 새 디렉터리에 게시한 뒤 `dotnet exec <게시한 Duxel.Generators.Tests.dll> <새 결과 디렉터리>`로 실행합니다. 결과에는 `icon-removal-matrix.json`, `icon-incremental-limits.json`, `core-catalog.json`이 있습니다. 현재 컴파일된 초안 카탈로그는 팩터리59개, 필수 상위55개 중35개 연결·20개 미연결이며 연결 수를 전체 Fluent 적합성 완료로 세지 않습니다. 근거: `artifacts/generator-compiler-selection-2026-11-15/verification.json`.

## Windows·Linux 생성기 검증

위젯 계약 실행기는 `Duxel.Generators.Tests`를 실행하지 않습니다. 컴파일러나 `FluentComponents.tsv`를 바꾸면 이 검사기도 실행해야 합니다. 제품 요구사항을 독립적으로 검토한 공식 사이트 fixture와 대조합니다. 2026-11-13 검사에서 CardFooter·CardHeader·CardPreview·InteractionTag의 검토 자료 누락을 찾았습니다. 부모 지침을 다시 확인해 보완했으며 상위 분모55개는 유지합니다.

현재 Windows와 Ubuntu24.04 x64에서 .NET11.0.0으로 각각117건이 통과했습니다. 저장한 생성 소스6개의 바이트도 OS 간에 같습니다. 결과에는 실제 프레임워크·OS·프로세스 아키텍처를 기록합니다. 기존 선언 제거 캐시 기준은 나머지 출력2개 중0개를 재사용해 여전히 미충족입니다. Linux 창·입력·텍스트·Vulkan, ARM64·Pi와 고정 RC3 NativeAOT·성능을 검증한 결과는 아닙니다.

`LocalSource.targets`의 기본값은 `net11.0-windows`입니다. 이식성 검증은 `DuxelSourceTargetFramework=net11.0`과 `DuxelBenchmarkFramework=net11.0`을 명시하며 생성기는 netstandard2.0을 유지합니다. Linux 검사는 기존 Windows SDK로 별도 게시한 런타임11.0.0 포함 관리형 실행물입니다. Ubuntu의 전역 .NET10 설치와 저장소의 RC3 고정값은 유지했습니다.

PowerShell에서 검증한 Linux 실행물을 실행합니다. 결과 디렉터리는 새 경로를 사용하세요.

```powershell
wsl.exe -d Ubuntu --exec /mnt/l/Cache/Duxel/linux-generator-2026-11-13/publish-final/Duxel.Generators.Tests /mnt/l/Cache/Duxel/linux-generator-2026-11-13/user-result
```

전체 빌드 인수·소스 해시·양쪽 결과는 `artifacts/goal-audit-2026-11-13/verification.json`에 있습니다.

2026-11-12 현재 Avatar 기본 기능 검증은 기본49개 계약·8021회 검사(Avatar110·관찰175)입니다. GPU avatar100(Light/Dark·MSAA1/4),observer-avatar3,rounded-image40을 별도 실행합니다. 네이티브 실행기는 Profile Avatar를 지원하며 호스트2+클라이언트8개를 통과했습니다. 같은 최종 바이너리에서 Accordion13·Label16·Radio12·Toggle15개도 통과했습니다. 테스트 글꼴을 앱의 맑은 고딕 기본 등록과 맞췄고, 정상 한글과 의도적으로 등록을 뺀 빈 글리프 상자·복원 후 픽셀을 대조합니다. 근거: artifacts/fluent-avatar-2026-11-12/verification.json. 활동·상태·전체 동등성·원래 NativeAOT/성능은 별도입니다. 아래의 과거 집계를 대체합니다.

최신 Accordion 근거는 `artifacts/fluent-accordion-2026-11-12/verification.json`입니다. CPU47개 계약·7876회 검사(Accordion27·Observer164), 네이티브 Accordion13+Label/Radio/Toggle43개, Accordion GPU32개가 통과했습니다. 기존 네이티브 실행기는 Profile=Accordion도 받습니다. CPU 기본 순서는 widget-lifetime-only를 먼저 실행해 카탈로그·구조 불일치를 일찍 검출합니다. 강제 색상·모션·RTL·전체 Fluent·네이티브 성능 검증은 별도입니다.

최신 근거는 `artifacts/label-relationships-2026-11-12/verification.json`입니다. CPU46/46개 계약·7844회 검사(Label40·Observer159), 별도 네이티브 Label16/Radio12/Toggle15, Label GPU28개 검사와 기존 PPM33개 동일성이 통과했습니다. 아래 네이티브 실행 명령을 그대로 사용합니다. 이전 수치는 당시 기록입니다. Label의 실제 Text 공급자·LabeledBy 관계를 포함하며 강제 색상·전체 Fluent·NativeAOT·성능 검증은 별도입니다.

네이티브 접근성은 이미 빌드한 Graphics 바이너리와 새 출력 디렉터리로 실행합니다.

```powershell
$p = @{
    Binary = '<Duxel.Graphics.Tests.dll의 절대 경로>'
    Profile = 'Label' # Label, Radio, Toggle 중 선택
    OutputDirectory = '<새 출력 디렉터리의 절대 경로>'
}
./scripts/Test-NativeAccessibility.ps1 @p
```

실행기는 직접 시작한 호스트·클라이언트만 관리합니다. 준비 신호·공유 시간 제한·런타임·결과 계약을 확인하고, 독립 로그·종료 코드·실행 파일 해시를 남깁니다. 실패하면 소유한 미종료 프로세스만 종료하며 자동 빌드나 SDK 전환은 하지 않습니다. 현재 근거는 `artifacts/label-native-2026-11-12/verification.json`입니다. CPU46/46개 계약·7827회 검사(Label24, 중복 실행 포함), 별도 네이티브 Label13·Radio12·Toggle15개 검사가 통과했습니다. 입력 누락·기존 출력·잘못된 런타임 거부를 확인했습니다. 강제 시간 초과·중단 정리는 미검증이며 고정 RC3 NativeAOT·GPU·전체 목표 검증은 별도입니다.

`raster-lifetime-only`는 대기 중인 CPU 프레임·유지 레이어의 캐시 압박 계약을 검사합니다. GPU 경로는 `dotnet exec <GraphicsBinary> <새-출력-경로> raster-lifetime 2`이며 `3`으로 독립 실행합니다. Image·SVG 각각 128개 캐시를 채우고 원본 위젯 해제·용량 압박 중 재생·레이어 제거·텍스처 퇴거를 확인합니다. CPU 프레임 네 개를 먼저 만든 뒤 GPU에 순서대로 제출합니다. 현재45개 계약에는 새 CPU10개 검사가 포함되며 GPU는 설정별17개 검사가 통과했습니다. 두 설정의 PPM9개와 Image/SVG 단계별4쌍이 같습니다. 읽기 완료를 기다리므로 GPU 동시 처리·장시간 증가 검증은 별도입니다. [이미지 수명 안내](image-widgets.ko.md)를 참고하세요.

기존 GPU·위젯 묶음을 `widgets-no-resize`로 실행할 수 있습니다. 일반 실행과 검사 본문을 공유하면서 `RootResizeRenderingTests`·`WindowInteractionTests`·`WindowRenderingTests`를 명시적으로 제외하고 이름을 `result.json/SkippedTests`에 남깁니다. 기본 실행은 그대로입니다. 새 모드와 네이티브 리사이즈용 `msaa4` 옵션을 함께 지정하면 플랫폼 생성 전에 거부합니다. 전용 GPU 모드·외부 UIA 세션·NativeAOT·고부하 성능·전체 Fluent/SVG 검증을 대신하지 않습니다.

아래 빌드 후 `dotnet exec <GraphicsBinary> <새-출력-경로> widgets-no-resize 2`를 실행하고 프레임 수 `3`으로 독립 실행하세요. 실제 빌드된 DLL의 절대 경로를 사용하며 GPU 실행은 순차적으로 합니다. stdout·stderr·결과·픽셀 파일을 보존하세요. 현재 근거는 `artifacts/widget-core-gpu-2026-11-12/verification.json`입니다. 각 설정에서 4642개 검사, 두 설정 사이 4038개 PPM 일치와 보류 옵션 거부를 확인했습니다. 관리 GPU 기능 검증이며 시간·프로세스 메모리 비교는 아닙니다.

[English](widget-contract-validation.md)

기존 관리 위젯 검사를 하나의 명시적 실행 경로로 묶고 각 검사를 독립 프로세스에서 실행합니다. 현재 기본 분류는 Graphics 계약45개와 별도 Control 키보드·진행률 계약1개로 총46개입니다. 독립 아이콘 계약은 컴파일된 아이콘과 빌려 쓰는 SVG painter를 검사합니다. GPU 초기화 뒤에만 실행되던 기존 CPU 계약22개 그룹도 `legacy-contracts-only`로 연결했습니다.

## 빌드와 실행

Windows의 PowerShell7과 등록된 Segoe UI·Consolas·맑은 고딕 fixture를 사용합니다. 저장소 루트에서 시작하고 필요하면 `$build`를 테스트 바이너리 밖의 쓰기 가능한 경로로 바꾸세요. `tests/managed-validation/global.json`은 SDK11.0.100을 고정하며 이번 실행은 런타임11.0.0입니다. 루트와 기존 RC3 고정을 바꾸지 않으며 고정 RC3 NativeAOT 검증을 대신하지 않습니다. SDK 설치나 네이티브 도구 경로 탐색은 수행하지 않습니다.

```powershell
$repo = (Get-Location).Path
$build = 'L:/Cache/Duxel/contracts/build'
$options = @(
    '-c', 'Release', '-warnaserror',
    '-p:DuxelBenchmarkFramework=net11.0-windows',
    ('-p:DirectoryBuildTargetsPath=' + (Join-Path $repo 'samples/Duxel.Gallery/LocalSource.targets')),
    '-p:UseArtifactsOutput=true', ('-p:ArtifactsPath=' + $build), '-m:1'
)
Push-Location (Join-Path $repo 'tests/managed-validation')
try {
    foreach ($name in @('Duxel.Graphics.Tests', 'Duxel.Control.Tests')) {
        $project = Join-Path $repo ('tests/{0}/{0}.csproj' -f $name)
        $buildArguments = @('build', $project) + $options
        & dotnet @buildArguments
        if ($LASTEXITCODE -ne 0) { throw ('Build failed : ' + $name) }
    }
} finally { Pop-Location }
$contractParameters = @{
    GraphicsBinary = Join-Path $build 'bin/Duxel.Graphics.Tests/release/Duxel.Graphics.Tests.dll'
    ControlBinary = Join-Path $build 'bin/Duxel.Control.Tests/release/Duxel.Control.Tests.dll'
    OutputDirectory = Join-Path $repo ('artifacts/widget-contracts/' + (Get-Date -Format 'yyyyMMdd_HHmmss'))
    ExpectedFramework = '.NET 11.0.0'
}
./scripts/Test-WidgetContracts.ps1 @contractParameters
```

결과 폴더는 새 경로여야 하며 기존 근거를 덮어쓰지 않습니다. 자동 빌드나 실패 후 다른 바이너리·런타임 선택은 없습니다. 실행 전에 실행 파일·의존성·자산·글꼴 해시를 기록하고 끝날 때 그대로인지 확인합니다. 바이너리 해시는 검사한 빌드를 식별하며, 실행기만으로 소스와 바이너리의 동등성을 인증하지는 않습니다. 실제 빌드 로그도 함께 보존하세요.

## 범위와 결과

`tests/Duxel.Graphics.Tests/contract-suites.json`이 공개된 `*-only` 식별자를 명시적으로 분류합니다. 소스 식별자가 분류에서 빠지면 자식 프로세스를 시작하기 전에 실패합니다. `catalog-only`는 목록 조회이며 `session-failure-only`는 실제 Win32·GPU 앱 세션 fixture이므로 기본 CPU 집합 밖에 명시했습니다. GPU 픽셀·외부 UIA 클라이언트와 앱 세션·생성기 컴파일/증분 검사·보류된 리사이즈·고부하/장시간 증가·NativeAOT/런타임 이미지·전체 Fluent/SVG/플랫폼 수락은 별도 요구사항입니다.

각 검사는 인수 배열과 숨긴 콘솔로 순차 실행합니다. stdout·stderr·PID·종료 코드·결과 검증을 구분합니다. 기본 시한은 검사당60초이며 실패하면 이후 검사를 멈추고 미실행 목록을 남깁니다. 종료 코드가0이어도 결과·정확한 런타임/모드·양수 검사 수와 배열 길이·빈 실패 파일 조건을 만족하지 않으면 거부합니다. `inputs.json`, 검사별 `process.json`·`result.json`·로그와 `summary.json`이 근거입니다. `assertionExecutions`에는 중복 검사가 포함될 수 있으므로 고유 요구사항 수가 아닙니다. `completedDefaultSuite`는 선언한 기본 집합만 뜻하며 `fullGoalComplete`는 false를 유지합니다.

집중 재실행은 새 매개변수 해시테이블과 새 결과 경로에 `Suite = @('owned-sizing-only', 'legacy-contracts-only')`를 추가합니다. 부분 실행을 기본 전체 실행 완료로 표시하지 않습니다. 실행기 자체는 같은 바이너리 경로와 새 결과 경로를 `./tests/Test-WidgetContracts.Runner.Tests.ps1`에 전달해 검증합니다. 정상 부분 실행/공백 경로·미등록 검사·누락 바이너리·미분류 모드·자식 종료0의 런타임 불일치·기존 결과 보존을6개 검사로 확인합니다. 강제 시한 초과·중단 분기는 이번 검증에서 실행하지 않았습니다.

현재 근거는 artifacts/contract-integration-2026-11-12/verification.json입니다. 기본42개가 모두 통과했고 검사 실행 수는7753개이며 기존 CPU704개·Control62개를 포함합니다. GPU·네이티브·고부하나 전체 목표의 완료 수치로 확대하지 않습니다.

최신 확장 검증 : artifacts/standalone-icon-2026-11-12/verification.json. 독립 아이콘 17개 검사를 포함한 43개 계약·7770회 검사 실행이 통과했습니다. 이전 42개 결과는 역사적 근거입니다. Observer SVG GPU 비교 7개는 별도 실행했습니다.

최신 Image 확장 검증 : artifacts/image-widget-2026-11-12/verification.json. 44개 계약·7793회 검사 실행(이미지 23개), SVG·Image GPU 74개, Observer SVG GPU 7개가 통과했습니다. 이전 42·43개 결과는 역사적 근거이며 전체 목표 완료를 뜻하지 않습니다.

최신 캐시 수명 근거 : artifacts/raster-lifetime-2026-11-12/verification.json. 관리45개 계약·7803회 검사 실행, 새 CPU10개, GPU설정별17개가 통과했습니다. 이전44개 결과는 역사적 근거입니다.

최신 Label 검증 : artifacts/fluent-label-2026-11-12/verification.json. 독립 라벨19개 검사를 포함한46개 계약·7822회 검사 실행, Label GPU28개·Observer50개가 통과했습니다. 원래 기본 위젯 GPU4642개와 기존4038개 이미지 일치도 확인했습니다. [독립 라벨 API와 실행](label-widgets.ko.md). 이전45개 결과는 역사적 근거입니다.

### 둥근 이미지·SVG 검증

현재 기본 계약은 Graphics47개와 Control1개로48개·7905회 검사입니다. 둥근 이미지24개·관찰169개를 포함하며 앞의 과거 집계를 대체합니다. GPU는 기존 Graphics 바이너리와 새 절대 출력 경로로 `rounded-image`(MSAA1/4·40개), `rounded-image-lifetime 2`와`rounded-image-lifetime 3`(각19개), `svg`(74개), `observer-svg`(7개)를 별도 실행합니다. GPU 프로세스는 순차 실행하세요. `image-shader-bench`는 GPU 타임스탬프를 명시적으로 켜고64프레임 준비 후256개 원시 표본을 씁니다. 기존 사각형 장면 진단이며 전체 앱 성능 기준이 아닙니다. 근거는 `artifacts/rounded-image-2026-11-12/verification.json`입니다. 리사이즈·고정 RC3 NativeAOT 완료를 뜻하지 않습니다.

## 독립 상태 배지
`Fluent.PresenceBadge`는8개 상태·외근·6개 크기를 지원합니다. `./run-gallery.ps1` 실행→**아바타**→**상태 배지**에서 상태·외근·크기를 바꾸세요. 스크롤되는 페이지는 상태를 유지하며 주기 프레임을 요청하지 않습니다. 기본 아이콘은 컴파일된 Fluent 폰트이고 전달한 SVG painter는 캐시를 재사용합니다. [API·자원 수명·검증](fluent2-presence-badge.ko.md). Avatar 겹침·잘라내기·활동은 후속 범위입니다.

## 활동 링과 갤러리 스크롤

`avatar.Options = avatar.Options with { ShowActivityRing = true };`로 활동 링을 켭니다. `avatar.ActivityText = "활동 중";`으로 접근성 문구를 지정할 수 있습니다. 기본 이름에 활동 문구를 더하며 `AccessibleName`을 지정하면 그 값이 우선합니다. 이름은 속성 변경 때 저장합니다. 링을 표시해도 동작·포커스·호버·애니메이션 타이머가 없는 Image 하나입니다.

자연 크기에서는 본체가 선택한14개 크기를 유지합니다. 네이티브 레이아웃은 전체 시각 영역을 측정합니다. 본체48px 이하는 선2px,64px 이하는3px,그보다 크면4px이며 각 방향에 선 두께의2배를 예약합니다(본체32px→전체40px). 장식을 이웃 할당 영역에 덧그리지 않도록 공간을 확보합니다. 부모 영역이 작으면 본체와 간격을 같은 비율로 줄입니다. 원형·사각형과 밝음·어두움의 링 색은 기존 analytic 테두리 전용 primitive와 고정된 persona 팔레트를 재사용합니다. 간격에는 아무것도 칠하지 않아 아래 픽셀을 보존합니다. 크기·링 전환은 기존 이미지 텍스처 임대를 재사용합니다. `ShowActivityRing=false`는 일반 아바타로 돌아가며 Fluent의 비활동 표현 구현을 뜻하지 않습니다.

저장소 루트에서 `./run-gallery.ps1` 실행→**아바타**→**아바타 활동 링**을 누르세요. 샘플은 활동 문구를 한국어로 지정합니다. 페이지는 공통 `UiScrollView`를 사용해 콘텐츠가 창보다 커져도 flex 높이 초과 오류 없이 스크롤됩니다.800×400 창에서 실제 끝까지 스크롤하고 탭 전환 후 위치와 모델을 유지하는지 검사합니다.

현재 근거는 `artifacts/fluent-avatar-ring-2026-11-12/verification.json`이며 SDK11.0.100/runtime11.0.0 관리 실행입니다. 전체 Fluent 활동/비활동 전환·그림자·배지 겹침/투명 잘라내기·강제 색과 원래 고정 RC3 NativeAOT/전체 부하 검증은 남아 있습니다. [상태 배지](fluent2-presence-badge.ko.md)는 현재 독립 위젯이며 이미지·이니셜·아이콘과 링에 공통으로 적용할 실제 투명 마스크가 필요합니다.

## 원형 잘라내기
CircleCutout으로 도형·이미지·텍스트·SVG와 유지/compiled 레이어에 실제 투명한 구멍을 만듭니다. `./run-gallery.ps1` 실행→**렌더링**→**원형 잘라내기**에서 확인하세요. [API와 검증 범위](circle-cutout.ko.md). Avatar 배지 연결은 후속 범위입니다.

검증 스냅샷: CPU51/51계약·8136회 검사(CircleCutout20·관찰189),마스크는 설정마다88회씩5가지 설정에서 통과했습니다. 실제 Khronos instance/device validation layer 로드를 확인했고 캡처 로그에 VUID/오류가 없었습니다. 프레임 자원2/3개·공유 camera/basis 설정·일반 compiled·양쪽 스케줄러를 포함합니다. 추가 GPU 검사는 primitive74·둥근 이미지40·SVG74·Avatar220·Presence552·관찰55회입니다. 설정별 반복을 포함한 횟수이며1455개의 독립 기능 요구사항을 뜻하지 않습니다. 샘플의 본문 실행 카운터는 실제 구성 뒤에 그려 같은 이벤트 프레임의 확정값을 표시하며 추가 프레임을 요청하지 않습니다.
