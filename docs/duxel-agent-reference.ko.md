# Duxel 에이전트 참조 문서

Last synced: 2026-11-19

SVG 갤러리는 `BeginChild`의 문서 스크롤 옵션으로 같은 펼친 코드 블록 안의 휠 입력과 잡고 있는 스크롤 막대의 이동을 묶습니다. [SVG 소스 스크롤 검증](optimization-session-2026-11-19.md#fast-scroll-over-svg-sample-source)

같은 레이어를 갱신할 때 기하 키와 캡처 목록을 유지하고, 같은 위치의 소유 캐시 레코드도 재사용합니다. 이미 제출한 프레임은 각자의 그리기 목록과 자원 사용권을 유지합니다. [할당량과 픽셀 검증](optimization-session-2026-11-19.md#reusing-cache-records-for-versioned-layers)

캐시할 레이어를 캡처할 때는 같은 중첩 깊이의 레이어끼리 작업용 버퍼를 공유합니다. 완성된 그리기 목록은 재사용하거나 GPU에 제출하는 동안 각자의 버퍼를 유지합니다. [메모리와 픽셀 검증](optimization-session-2026-11-19.md#sharing-retained-layer-capture-scratch)

위젯 재사용 문서에서 준비된 레이아웃과 기하 재사용, 캐시 상한, 대기 중인 프레임의 자원 수명 등 CPU 재사용 기준 4개의 결과를 볼 수 있습니다. [검증 범위](widget-cpu-retention.ko.md)

Windows .NET 11에서 프레임 슬롯 3개로 기본 위젯의 입력과 상태, GPU 픽셀을 확인했습니다. 현재 관리형 빌드와 이전 NativeAOT의 공통 이미지가 모두 일치했습니다. [검증 현황](acceptance-status.ko.md)

컨테이너 원장에서 ScrollView, 소스 생성기로 만든 공개 Footer, 갤러리 표시와 예제별 소스, Linux NativeAOT 성능 비교를 포함한 6개 기준의 결과를 볼 수 있습니다. [검증 현황](acceptance-status.ko.md)

갤러리 검증 원장에서 Windows 데스크톱 실행과 Input·Textarea 편집, Tooltip 동작을 포함한 10개 기준의 확인 결과를 볼 수 있습니다. [검증 현황](acceptance-status.ko.md)

SVG 텍스처 캐시가 한도에 이르면, 해당 위젯만 쓰는 이전 텍스처는 새 소스를 준비한 뒤 교체할 수 있습니다. 현재 프레임과 재사용 레이어가 참조하는 텍스처는 계속 보호합니다. [SVG 리소스 수명](svg-support.ko.md)

갤러리의 Tree 행 버튼 예제에서 F2로 보고서의 열기 버튼에 포커스를 옮기고, Enter로 실행한 뒤 Escape로 행으로 돌아갈 수 있습니다. Linux 외부 검사기는 실제 갤러리 창에서 X11 키 입력과 AT-SPI 포커스 상태를 확인합니다. [Tree 키보드 검증](fluent2-tree.ko.md)

갤러리 **입력과 선택 → RadioGroup**에서 부모 최대 폭 180px와 360px의 줄바꿈을 비교할 수 있습니다. C# 코드는 각 예제 바로 아래에 있습니다. [RadioGroup 예제 실행 방법](fluent2-radio-group.ko.md)

Vulkan은 변경된 사각형과 원을 각각 비교해 갱신 범위를 계산합니다. Windows 호·곡선 비교 검사에서 첫 부분 갱신의 계산 시간이 줄었고, 갱신 범위와 최종 픽셀은 일치했습니다. [첫 부분 갱신 측정](optimization-session-2026-11-19.md#first-partial-repair-during-an-arc-zoom-gesture)

코드 뷰는 부모가 너비와 높이를 정해 주면 그 크기를 그대로 쓰고, 화면에 보이는 경우에만 그리기를 준비합니다. 갤러리 호·곡선 비교 검사에서 확대, 회전, 코드 선택 모두 프레임당 관리 메모리 할당량이 줄었습니다. [입력 재현과 갱신 범위 측정](optimization-session-2026-11-19.md#shared-code-view-work-during-arc-zoom-rotation-and-selection)

갤러리 **SVG → 호·곡선 비교**에서 확대·회전·C# 코드 선택을 같은 페이지에서 확인할 수 있습니다. Fluent Nav는 같은 위치에서 그리는 동안 그룹의 접근성 식별자를 재사용합니다. Windows 관리형 검사에서는 세 동작 모두 프레임당 할당량이 줄었고, Vulkan 제출 시간은 동작에 따라 달랐습니다. [측정 결과와 재현 명령](optimization-session-2026-11-19.md#arc-comparison-and-navigation-allocations)

SVG 문서는 스크롤바를 끌 때 손잡이 위치를 먼저 계산한 뒤 본문을 배치합니다. 한 프레임에 손잡이 이동만 여러 번 들어오면 마지막 위치에서 한 번 배치합니다. Vulkan은 스크롤로 달라진 문서 영역을 다시 그립니다. Linux 관리형과 NativeAOT에서도 두 탭의 스크롤바를 끄는 동안 코드가 움직이고 원래 위치의 픽셀이 돌아오는 것을 확인했습니다. [Windows와 Linux 드래그 측정](optimization-session-2026-11-19.md#dragging-the-svg-source-scrollbar)

갤러리는 첫 표시용 프레임을 그릴 때 FPS 측정을 시작합니다. 이후에는 `UiFrameInfo.IdleWaitSeconds`를 사용해 호스트가 이벤트를 기다린 시간을 뺍니다. 내부 스크롤이 없는 읽기 전용 코드의 선택 범위는 연속 이동 입력을 모아 계산할 수 있습니다. Linux X11의 레이어·테두리 검사에서 스크롤바, 휠, 코드 선택을 관리형과 NativeAOT로 비교했습니다. [레이어·테두리 재현과 측정](optimization-session-2026-11-19.md#mixed-scrolling-and-source-selection-in-svg-layer-and-borders)

코드 선택을 끝내고 버튼을 놓은 뒤에도 포인터 아래의 읽기 전용 코드가 포커스를 유지할 수 있습니다. 같은 코드의 범위 안에서는 연속 휠 입력을 모아 처리하고, 방향이 바뀌거나 경계에 닿으면 입력 순서대로 처리합니다. [포커스가 남은 코드에서의 휠 검사](optimization-session-2026-11-19.md#wheel-input-after-releasing-a-code-selection)

SVG 비교 화면은 조작이 끝나면 최종 크기로 SVG를 다시 그린 뒤, 이어지는 프레임마다 C# 예제를 최대 두 개씩 갱신합니다. Windows 호·곡선 비교 검사는 이벤트 대기 모드에서도 다음 프레임이 예약되고 코드 여섯 개에 최종 변환 값이 반영되는지 확인합니다. [측정 결과와 검증 범위](optimization-session-2026-11-19.md#separate-svg-and-code-updates-after-a-gesture)

Vulkan 프로파일의 `frame` 항목으로 UI 프레임과 제출 번호, 획득한 이미지, 표시 순서를 연결할 수 있습니다. GPU 시간은 그 줄에서 완료를 확인한 제출의 `gpuSubmission` 번호로 연결합니다. [호·곡선 확대 측정](optimization-session-2026-11-19.md#vulkan-frame-correlation-during-arc-zoom)

Windows의 `gallery-svg-cold-warm input-profile` 검사는 한 프로세스에서 갤러리를 두 번 열고 호·곡선 조작을 같은 순서로 재생합니다. 엔진 단계와 함께 입력 및 표시 단계의 왼쪽 메뉴, 상단 옵션, 본문 시간을 따로 기록합니다. [갤러리 구간별 측정](optimization-session-2026-11-19.md#direct-timing-of-gallery-sections-during-repeated-zoom)

코드가 화면에서 잠깐 가려졌다 다시 나타나면, 엔진은 미리 계산한 문자와 커서 위치 한 벌을 재사용할 수 있습니다. 가려진 동안에는 문자 위치를 접근성 트리에 표시하지 않습니다. Windows 호·곡선 비교에서 확대 조작의 Vulkan 제출 시간 p95가 9.56ms에서 7.52ms로 줄었습니다. [측정 결과와 검증 범위](optimization-session-2026-11-19.md#arc-comparison-when-code-returns-to-the-viewport)

네이티브 텍스트 선택은 줄마다 선택 영역을 계산할 때 임시 버퍼를 쓰고, 결과 배열에서 바로 배율을 적용합니다. 호·곡선 비교에서 코드 선택 이동 16개를 묶어 처리할 때 프레임당 할당이 43.5KB에서 26.6KB로 줄었습니다. 선택 범위와 화면은 일치했습니다. [선택과 회전 측정 결과](optimization-session-2026-11-19.md#native-text-selection-fragments)

호·곡선 비교의 포인터 검사는 화면 구성의 시작과 끝을 기록하므로 Windows 할당 추적에서 해당 프레임을 찾을 수 있습니다. 회전 초반에는 값이 바뀌는 상태 라벨에서 DirectWrite 작업이 관측됐습니다. 글꼴을 고를 때 문자마다 임시 문자열을 만들던 비용을 줄였고, 글리프 누락 메시지는 진단 로그를 켰을 때 작성합니다. [회전 추적과 측정 범위](optimization-session-2026-11-19.md#rotation-startup-and-directwrite-diagnostics)

SVG 갤러리는 조작 중 각도와 크기를 실시간으로 표시하면서 같은 숫자 모양을 재사용합니다. 조작이 끝나면 기존 방식으로 라벨을 그립니다. Windows 호·곡선 비교를 세 번씩 실행했을 때 회전의 Vulkan 제출 시간 p95는 4.01ms에서 3.00ms로 줄었습니다. 갱신 강조를 끈 최종 화면은 픽셀까지 일치했습니다. [조작별 측정과 검증 범위](optimization-session-2026-11-19.md#svg-gallery-live-number-labels)

Windows에서 `gallery-svg-pointer arc win32 burst desktop`으로 호·곡선 비교의 화면 표시 시간을 확인할 수 있습니다. 테스트 창이 가려지지 않은 상태에서 Win32 메시지를 보내고, 같은 프레임에 그린 표식이 바탕화면에 나타날 때까지 걸린 시간을 기록합니다. [재현 방법과 측정 범위](optimization-session-2026-11-19.md#windows-visible-desktop-observations)

기존 즉시 모드 메뉴의 선택 표시는 공통 벡터 체크 표시로 그립니다. [메뉴 동작과 GPU 검증](menu-foundation.ko.md)

Linux에서 글꼴 아틀라스 캐시는 `$XDG_DATA_HOME/Duxel/FontAtlasCache`에 저장됩니다. `XDG_DATA_HOME`을 지정하지 않으면 `$HOME/.local/share/Duxel/FontAtlasCache`에 저장합니다. 새 사용자 환경에서도 처음 저장할 때 폴더를 만듭니다. 디스크 캐시를 끄려면 `DUXEL_FONT_DISK_CACHE=0`을 설정하세요.

Linux 갤러리의 외부 접근성 검사에서 별점 반점 선택, 요약 평가의 접근성 이름, 페이지 이동 후 선택 유지가 확인됐습니다. [별점 검증](fluent2-rating.ko.md)

Linux 갤러리의 NavigationBar에서 외부 접근성 클라이언트로 양쪽 버튼을 실행하고, 제목 변경과 보조 행의 기간 선택을 확인했습니다. [NavigationBar 검증](fluent2-navigation-bar.ko.md)

기본 RadioGroup 배치가 `FluentProvider.Direction`을 따릅니다. 갤러리의 영역별 배치 방향 예제에서 배치와 방향키 동작이 함께 바뀌고 선택값은 유지되는 것을 확인할 수 있습니다. [RadioGroup 방향](fluent2-radio-group.ko.md)

Linux 플랫폼 검사에서도 RadioGroup 부모 배치와 생성·즉시 모드의 GPU 검사를 공유합니다. 현재 관리형과 NativeAOT는 슬롯 1개와 3개에서 이미지가 일치했습니다. 갤러리에서는 부모 폭을 바꾸면서 줄바꿈이 달라지고 선택이 유지되는 것을 확인했습니다. [RadioGroup 플랫폼별 검증 결과](fluent2-radio-group.ko.md)

같은 예제에서 Checkbox·Switch 라벨의 배치, Switch 손잡이의 이동 방향과 가로 Slider의 표시·조작 방향을 확인할 수 있습니다. [FluentProvider](fluent2-provider.ko.md)

별도 Windows UI Automation 클라이언트에서 창을 닫으면 캐시된 `TooltipClosed`가 한 번 전달되고 일반 공급자는 사용할 수 없게 되는 것을 확인했습니다. Windows x64 .NET 11.0.0 관리형과 NativeAOT가 모두 통과했습니다. [Tooltip 종료 검증](tooltip-events.ko.md#검증)

실제 공휴일 조회 검사는 갤러리 버튼을 눌러 2026년 5월 대체공휴일이 달력에 표시될 때까지 기다립니다. Windows 관리형과 WSLg Linux x64 NativeAOT에서 통과했습니다. [실제 조회 실행 방법](calendar-holidays.ko.md#갤러리-검증)

별도 Linux AT-SPI와 Windows UI Automation 클라이언트에서 연결 끊김 뒤 날짜를 선택하고, 실제 공휴일 조회가 끝나도 선택 상태가 유지되는지 확인했습니다. Windows 관리형과 x64 NativeAOT 갤러리 모두 통과했습니다. [달력 검증](calendar-holidays.ko.md#갤러리-검증)

ERD 샘플은 장면을 구성하기 전에 전체 글꼴 아틀라스를 준비합니다. `DUXEL_ERD_TRACE_STARTUP=1`로 앱 시작 타이밍과 처음 여덟 UI 프레임을 확인할 수 있습니다. `DUXEL_ERD_FAST_STARTUP=1`을 설정하면 두 단계로 글꼴을 준비하는 방식과 비교할 수 있습니다. [조건과 근거](erd-layer-lab.ko.md)

보관 레이어의 그리기와 배치가 기본 글꼴 아틀라스와 글꼴 치수에 의존하지 않을 때만 `UiLayerOptions.FontAtlasIndependent`를 설정합니다. 글꼴에 의존하는 자식이 있으면 부모도 아틀라스 교체 시 다시 구성합니다. 기존 자식 캐시를 재사용해 부모를 구성한 경우에도 같은 규칙을 적용합니다. 배율·테마·텍스트 백엔드가 변경되면 이전과 같이 해당 캐시를 무효화합니다.

Windows에서 `directtext-cache 3`을 .NET 11 관리형과 NativeAOT로 검증했습니다. 각각 45개 검사를 통과했고 Vulkan RGB 이미지 24쌍이 바이트 단위로 일치했습니다. 래스터 글자는 아틀라스 교체에 맞춰 바뀌었으며, 고정 좌표에 그린 벡터 글자는 아틀라스의 줄 높이가 바뀌어도 캐시 결과와 다시 그린 결과가 같았습니다. 근거는 `W:/Cache/Duxel/gallery-source-20261117/atlas-vector-1922/comparison.json`입니다.

보관한 .NET 11 ERD NativeAOT 실행 파일에서 동작 중 Private Bytes가 계속 늘었고, 대기 중에는 거의 일정했습니다. 동작 기록에서 GC 커밋이 늘어난 61구간 모두에서 Gen0 수집이 있었습니다. [ERD 관측](erd-layer-lab.ko.md)

Linux 갤러리 실행기는 WSL 배포판의 CPU 아키텍처에 맞춰 게시 대상을 선택합니다. [Linux 실행](../samples/Duxel.Gallery.Linux/README.ko.md)

ERD 메모리 진단은 프레임 단계와 화면 내부의 할당량을 기록합니다. 테이블 1,200개의 NativeAOT 실행에서 같은 프레임의 기록을 대조하니 UI 렌더링은 4,416바이트, 일반 Vulkan 제출은 632바이트였습니다. [단계별 할당 표본](erd-layer-lab.ko.md)

ERD의 고정 안내문을 한 번만 만들도록 바꿔, 변경 전후 세 번씩 교대 실행에서 UI 구성 할당을 표본당 168바이트 줄였습니다. [변경 전후 비교](erd-layer-lab.ko.md)

같은 ERD NativeAOT 실행 파일에서 선택 관계선은 테이블이 움직일 때 표본당 1,784바이트, 카메라만 움직일 때 0바이트를 할당했습니다. [장면별 할당](erd-layer-lab.ko.md)

Windows 관리형과 Linux NativeAOT 갤러리 검증에 공휴일 연결이 끊긴 뒤에도 날짜를 선택할 수 있는지 확인하는 단계를 추가했습니다. [공휴일 제공자 검증](calendar-holidays.ko.md#갤러리-검증)

갤러리는 `Duxel.App.KoreanHolidayProvider`로 공개된 `holidays-kr` 연도별 데이터를 인증키 없이 가져옵니다. 조회에 실패하면 공휴일 표시만 생략하며 날짜를 계속 선택할 수 있습니다. 별도의 KASI 제공자는 포털의 서비스 거부와 응답 형식 오류를 구분합니다. [공휴일 응답 처리](calendar-holidays.ko.md#실행과-실패-처리)

갤러리 화면 표시 검사에서 DatePicker, TimePicker, DateTimePicker의 팝업과 휠을 직접 작동시켜 저장 값을 확인합니다. [날짜·시각 선택기 검증](date-time-pickers.ko.md)

별도 Windows UI Automation 클라이언트로 Windows x64 .NET 11 NativeAOT 갤러리의 TimePicker를 열고 분을 바꾼 뒤, 팝업을 닫아도 저장 값이 유지되는지 확인했습니다. [Windows 외부 접근성 검증](date-time-pickers.ko.md)

같은 클라이언트가 검증용 갤러리 창에 Windows 휠 메시지를 한 번씩 보내, 분만 위·아래로 한 칸씩 바뀌는지 확인했습니다. [Windows 휠 검증](date-time-pickers.ko.md)

외부 Windows 클라이언트로 DatePicker의 월과 일을 선택하고, DateTimePicker의 날짜와 시각을 각각 바꿨을 때 나머지 값이 유지되는지 확인했습니다. [Windows 날짜 선택기 검증](date-time-pickers.ko.md)

Windows .NET 11.0.0 관리형과 NativeAOT의 달력·날짜·시각 선택기 Vulkan PNG 59쌍이 모두 일치했습니다. [런타임 이미지 비교](date-time-pickers.ko.md)

Windows와 WSLg Linux의 공통 Vulkan 표시 검사에서 이름이 같은 장면 574쌍 중 214쌍이 일치했습니다. 작은 영역 갱신 160쌍은 모두 일치했습니다. [운영체제 간 픽셀 비교](../tests/Duxel.Vulkan.Linux.Tests/README.ko.md)

그중 RGBA 원본 8쌍이 앞선 기록과 같은 해시를 냈습니다. 둥근 도형 장면 6쌍은 운영체제 간 RGB 채널 값이 최대 1만 달랐고, 알파 값과 작은 영역 갱신 대조 장면 2쌍은 모두 일치했습니다. [원본 캡처 방법](../tests/Duxel.Vulkan.Linux.Tests/README.ko.md)

같은 Segoe UI 파일로 실행한 글자 장면에서 Vulkan에 전달하기 전부터 글자 텍스처가 달랐습니다. 최종 프레임에서는 글자가 있는 줄에만 차이가 있었습니다. [글자 원본 비교](../tests/Duxel.Vulkan.Linux.Tests/README.ko.md)

`glyph-texture-replay`는 같은 글자 RGBA8 데이터를 두 운영체제에서 다시 그립니다. Linux 관리형과 NativeAOT 사이에서는 8장이 모두 일치했습니다. Windows와 WSLg 사이에서 RGB 채널 값은 정수 좌표에서 최대 1, 소수점 좌표에서 최대 2 차이가 났습니다. [실행 방법과 결과](../tests/Duxel.Vulkan.Linux.Tests/README.ko.md)

Linux NativeAOT 실행 파일을 두 lavapipe 환경에서 실행하면 정수 좌표의 네 장은 모두 일치하고 소수점 좌표에서는 RGB 채널 값이 최대 3 달랐습니다. 두 환경은 시스템 라이브러리도 서로 다릅니다. [드라이버 비교](../tests/Duxel.Vulkan.Linux.Tests/README.ko.md)

Vulkan 초기화는 논리 장치를 만들기 전에 `dualSrcBlend`를 포함해 지원하지 않는 필수 기능을 이름으로 알려줍니다. [Linux Vulkan 요구 사항](linux-platform.ko.md)

Vulkan 초기화가 실패하면 생성한 스왑체인과 렌더링 자원을 먼저 정리한 뒤 장치·표면·인스턴스를 해제합니다. Linux 재시도 검증은 첫 스왑체인 구성이 끝날 때까지의 실패를 다룹니다. [Linux 재시도 검증](../tests/Duxel.Vulkan.Linux.Tests/README.ko.md)

첫 화면을 그리기 전에 앱 시작이 실패하면 세션이 UI 컨텍스트를 해제하고 이전 글꼴 진단 콜백을 복원합니다. 렌더 스레드를 시작할 준비가 된 뒤에 대기 신호를 연결합니다. Windows와 Linux의 재시도 검증은 첫 화면을 표시한 뒤 종료 로그 콜백이 실패하는 경우도 포함합니다. [앱 시작 재시도 검증](linux-platform.ko.md)

Win32 메시지 루프를 끝낸 뒤 창을 정리하면 같은 UI 스레드에서 다음 창을 열 수 있습니다. 루프가 실행 중일 때 들어온 스레드 종료 메시지는 계속 처리합니다. [Win32 스레드 종료 메시지](https://learn.microsoft.com/en-us/windows/win32/winmsg/wm-quit)

Linux x64 NativeAOT 갤러리에서도 외부 AT-SPI 클라이언트로 월·일 선택, 날짜와 시각의 값 유지, 팝업 닫기를 확인했습니다. [Linux 날짜 선택기 검증](linux-platform.ko.md)

별도로 실행한 Linux AT-SPI 클라이언트도 NativeAOT 갤러리의 TimePicker를 열어 분을 선택하고 저장된 시각을 읽을 수 있습니다. [Linux 외부 접근성 검증](linux-platform.ko.md)

Linux TimePicker의 분 휠에 보낸 X11 이벤트 한 번이 분을 한 칸씩 바꾸고 초를 유지하는 것도 확인했습니다. [X11 휠 검증](date-time-pickers.ko.md)

정식 .NET 11.0.0 Linux x64 NativeAOT 갤러리는 Vulkan 표시 시나리오 100개를 통과하고 정상 종료했습니다. WSLg 1.0.79에서 Windows UI Automation으로 화면 안의 갤러리 창을 확인했습니다. [실행 방법과 확인 범위](../samples/Duxel.Gallery.Linux/README.ko.md)

새 빌드와 검증, 성능 비교에는 .NET 11 정식 SDK 11.0.100과 런타임 11.0.0을 사용합니다. 저장소와 런타임 비교 설정은 정식 SDK만 선택합니다. 이전 결과의 RC3 버전은 당시 실행 기록으로 남습니다. [런타임 비교 설정](../tests/runtime-net11/README.md)

Toolbar와 SearchBox도 FluentProvider의 배치 방향을 따릅니다. 가로 도구 모음은 배치와 좌우 방향키가 함께 반전됩니다. 검색창은 검색·지우기 아이콘의 위치를 바꿔도 편집기와 입력값을 유지합니다. 갤러리 FluentProvider의 **영역별 배치 방향**에서 확인할 수 있습니다. [FluentProvider](fluent2-provider.ko.md#영역별-배치-방향).

Windows x64 갤러리는 .NET 11 정식 NativeAOT로 게시하고 실행할 수 있습니다. 예제 전체를 보려면 실행 파일 옆에 `Assets` 폴더를 함께 두고, 배포할 때 라이선스 고지 파일을 포함하세요. 정식 .NET 11.0.0의 관리형과 NativeAOT에서 공통 위젯 4,038쌍, 컨테이너 185쌍, RadioGroup 부모 할당 245쌍의 RGB 이미지가 일치했습니다. 리사이즈는 보류 중입니다. [도구 경로](native-toolchain-status.ko.md)와 [위젯 검증](widget-contract-validation.ko.md#정식-net-11-런타임-비교)을 참고하세요.

현재 Windows 관리형 RadioGroup과 보관해 둔 NativeAOT 실행 파일의 이미지 245쌍이 일치했습니다. Linux는 두 런타임을 현재 소스로 빌드해 이미지 244쌍이 같은지 확인했습니다. 현재 Windows 관리형에서 창을 즉시 닫아도 외부 클라이언트가 Tooltip 닫힘 알림을 한 번 받았습니다. [RadioGroup](fluent2-radio-group.ko.md)과 [Tooltip 이벤트](tooltip-events.ko.md)

Windows는 제거된 공급자를 해제한 다음 일반 Tooltip 알림을 보냅니다. 별도 .NET 11.0.0 UI Automation 클라이언트에서 포커스 이동, 대상 제거, 명시적 닫기, 창 숨김의 닫힘을 확인했습니다. 창을 즉시 닫을 때도 마지막 닫힘이 전달됩니다. [Tooltip 알림 순서](tooltip-events.ko.md)

Linux x64 관리형과 NativeAOT 갤러리에서 외부 AT-SPI Tooltip 검사가 각각 통과했습니다. 숨은 설명과 레이블 모드, 자식 추가·제거 알림, 이전 객체의 폐기 상태를 확인합니다. X11 키보드 검사에서는 Escape로 닫아도 포커스가 유지되고 Tab과 Shift+Tab으로 돌아오면 자동 Tooltip이 다시 열립니다. [Linux Tooltip 검사](tooltip-events.ko.md#linux-at-spi)

Windows 갤러리 GPU 통합 검사는 탐색 메뉴의 본문 영역, 스크롤로 드러난 위젯, 실제 탭 선택, C# 소스 갱신 후의 픽셀을 확인합니다. .NET 11.0.0에서 검사 326건이 통과했고 GPU 캡처 525장을 저장했습니다. 결과는 `W:/Cache/Duxel/gallery-source-20261117/gallery-uia-2082/gallery-9/result.json`에 있습니다.

달력은 단일 날짜·기간 선택을 모두 지원하는 Duxel 확장으로 추가 중입니다. 세계화 조사 후 IUiCalendarSystem/UiCultureCalendar/UiCalendarMonth/UiCalendarDateRange 모델22건을 확인했습니다. 화면·입력은 아직 미구현이며 다음 작업입니다. [달력 조사와 계약](calendar-globalization.ko.md).

Fluent.HeadsUpDisplay는 중앙 표시·차단/비차단·1초 자동 닫기·200ms 선형 전환을 제공합니다. KeyboardHeight는 호스트가 명시하는 값입니다. 갤러리의 다섯 예제 소스가 실제 설정을 따르며 카탈로그는47/55 연결입니다. [HUD 안내](fluent2-heads-up-display.ko.md).

갤러리의 한국어 안내와 설명은 긴 명사 나열 대신 실제 조작 방법을 문장으로 안내합니다. API 이름과 기능·검증 한계는 유지하며, 문구를 바꾸면 연결된 예제 소스와 접근성 검사도 함께 확인합니다. 검토 기록: `artifacts/gallery-korean-copy-2026-11-17/changes.json`.

Nav 목적지에 `UiNavSecondaryAction`으로 보조 버튼을 붙일 수 있습니다. 호버·포커스 시 표시하고 접근성 트리에는 항상 남기며, 우클릭·Shift+F10·메뉴 키로 같은 작업을 엽니다. `동작과 탐색 → Nav → 개요 고정`에서 선택 유지와 실행 횟수를 확인하세요. [API와 입력 규칙](gallery-navigation.ko.md).

공통 `Fluent.Nav`가 갤러리의 세로 TabList를 대체합니다. 일곱 기능 그룹의 알파벳 정렬·벡터 아이콘·미구현 배지와 고정 테마 헤더를 유지하며 접기·펼치기, RTL, 방향키 이동과 Navigation/Link/Pane 접근성을 제공합니다. `동작과 탐색 → Nav`에서 예제를 확인하세요. [계약과 API](gallery-navigation.ko.md).

Vulkan 경계 진단은 일반 삼각형 경로의4×MSAA 출력을 고정 타깃·큰 타깃·정수 지역 좌표로 비교합니다. 세 경우 모두 브라우저와1픽셀·최대56 차이가 남으며, 캡처 전제 조건의 통과를 SVG 일치로 처리하지 않습니다. Windows125% 배율을 전제로 하는 테스트 전용 모드입니다. [실행 계약](svg-browser-validation.ko.md).

관리형 `--browser-root-transforms` 비교는 소수 크기 루트와 정수 크기 대조군의 독립 기준을 보존하고, 흰색 초기 배경·투명 출력을 각각 검사합니다. 문제의 경계에서는 브라우저도 배경 단계에 따라 결과가 달라, 루트 변환만 고치는 방식의 정확성은 아직 확인되지 않았습니다. [검증 범위](svg-browser-validation.ko.md).

관리형 SVG의 `--probe-pixel` 진단으로 특정 픽셀에 기여한 실제 도형과 원본 트리 경로, 단독 색상, 고정소수점 샘플 마스크를 확인합니다. 부모 효과와 지원하지 않는 확장 계층은 명시적으로 구분합니다. 테스트 전용 기능이며 제품 렌더링이나 엄격한 브라우저 비교를 대체하지 않습니다. [사용법](svg-browser-validation.ko.md).

SVG 호는 conic 제어점·가중치를 보존하며 경계·클립·필터·테두리·점선·CPU·GPU 경로가 같은 표현을 사용합니다. 호6개는 투명 및 흰색 초기 GPU 대상에서 정확히 일치합니다. 배경을 나중에 합성하는 경로와 원래 복잡한 SVG 전체 비교는 아직 미완료입니다. 장면·테두리 셰이더 프로토콜은4이며 실제 이전 셰이더를 모듈 생성 전에 거부합니다. [검증과 실행](svg-browser-validation.ko.md).

SVG의 공통 source-over는 전경 RGBA8 저장 후 합성하며 CPU 진단과 Vulkan에 같은 순서를 적용합니다. 새 `gpu-blend-backdrops`와 관리형 `--browser-blend-backdrops`는 독립 브라우저 기준12개를 전체 픽셀로 확인합니다. 기존 복잡한 SVG 전체 일치와는 별도 범위이며 현재 셰이더 프로토콜은4입니다. [합성 검증](svg-browser-validation.ko.md).

갤러리는 고정 헤더에서 다크/라이트 테마를 전환하고 현재 모드를 표시합니다. 왼쪽 메뉴의 55개 구성요소는 일곱 기능 그룹 안에서 알파벳순으로 정렬하며, 그룹 제목에 Fluent 벡터 아이콘을 표시합니다. 미구현 배지는 유지합니다. [탐색과 실행](gallery-navigation.ko.md).

`UiSvg.Background`와 저수준 `UiTextureUpdate.VectorBackground`는 벡터 출력 대상의 초기 색입니다. 기본은 투명이며 내부 그룹·패턴·필터에는 전파하지 않습니다. 배경은 공통 텍스처 캐시와 GPU 출력 식별에 포함됩니다. 갤러리 **추가 샘플 → SVG → 브라우저 비교**에서 전환할 수 있습니다. 대응 셰이더 프로토콜은4입니다. 전체 브라우저 일치는 여전히 미완료입니다. [API](svg-widget-layout.ko.md), [검증](svg-browser-validation.ko.md).

브라우저 기준 입력은 관리형·GPU 비교기에서 DPR1과 직접 표시 영역의 정수 픽셀 정렬을 검증합니다. 보존된 직접 표시 기준은 현재 이 조건을 만족하지 못해 렌더링 전에 거부됩니다. `svg-reference-contracts-only`는12개 입력 계약, `gpu-svg-image-browser`는 원래 두 문서의 Canvas 전체 이미지를 검사합니다. 후자는 아직 둘 다 미통과이며 직접 표시의 완료를 대신하지 않습니다. [기준과 실행](svg-browser-validation.ko.md).

`svg-feature-composition-only` GPU 검증은 원본 premultiplied 텍스처·정수 합성·전체 브라우저 이미지를 구분합니다. 관측한 브라우저 렌더러와 기준 해시를 GPU 작업 전에 검증하고 결과에 기록합니다. 전체 이미지 검사는 96픽셀 차이로 미통과이며, 채택하지 않은 셰이더 후보는 격리하고 제품 셰이더는 유지했습니다. [범위](svg-browser-validation.ko.md).

`UiFluentCheckboxPalette.Resolve(context, state, value)`는 Fluent Checkbox와 Markdown 작업 목록에 시스템 색상과 컨텍스트의 비활성 상태를 적용합니다. [범위와 근거](fluent2-checkbox.ko.md).

공통 스타일 텍스트는 CanvasText·LinkText·GrayText와 Canvas 코드·표 배경을 사용하며, 접근성 색상 캐시도 시스템 팔레트를 반영합니다. [범위](system-forced-colors.ko.md).

컨텍스트를 받는 `UiFluentInputPalette.Resolve`는 입력 계열 프레임·텍스트와 공통 장식에 시스템 대비 색상을 적용합니다. 실제 GPU Input 검사14개와 기존 입력 계열 회귀 검사가 통과했으며 일반 선택 글자는 원래 조판 구간을 겹치지 않게 나눠 HighlightText로 그립니다. 증감 버튼도 전용 팔레트에서 ButtonText/GrayText와 ButtonFace를 사용합니다. Menu도 시스템 색상과 항목을 그린 뒤 호출하는 `IUiMenuPresentation.PaintFocus`를 사용합니다. Dropdown·Combobox도 전용 옵션 팔레트의 Canvas·CanvasText·GrayText와 바깥 포커스 선을 공유합니다. 스타일 선택도 공통 배경·내용 단계와 시스템 HighlightText를 사용합니다. IME 조합 문자열 주변의 원문 선택도 공통 원문·표시 좌표 변환으로 HighlightText를 유지합니다. 조합 절별 스타일과 실제 OS 전환은 남아 있습니다. [범위와 근거](system-forced-colors.ko.md).

SVG 원형 모서리의 테두리는 바깥·안쪽의 해석적 커버리지를 공유하며, 타원형 모서리·점선·일반 경로는 기존 GPU 테두리 작업을 유지합니다. 벡터 장면 셰이더의 프로토콜2 표식은 스택 크기의 ID0과 별개인 특수화 ID1을 사용하며 모듈 생성 전에 검사합니다. 소스와 `vector_scene.comp.spv`를 함께 배포해야 합니다. 실제 이전 모듈을 해시로 고정한 회귀 검사가 있으며 Observer 검증에는 문서 기능 하위 탭을 포함합니다. [검증과 남은 차이](svg-browser-validation.ko.md).

시스템 고대비 색상은 `IPlatformThemeProvider.ForcedColors` / `UiTheme.ForcedColors`로 밝음·어두움과 별도로 전달합니다. 사용자 지정 디자인에도 기본 적용하며 `RespectSystemForcedColors=false`로 명시적으로 제외합니다. Windows 알림에서 같은 밝음·어두움 안의 팔레트 변경도 갱신합니다. 이 공통 기반이 모든 위젯의 색상 적합성을 의미하지는 않습니다. [계약과 한계](system-forced-colors.ko.md).

SVG의 분석적 채움 정보 때문에 테두리 생성을 건너뛰지 않도록 수정했습니다. Observer **SVG → 문서 기능**에서 둥근 테두리 아래의 줄무늬·방사형 그라디언트를 유지합니다. [회귀 검사와 남은 브라우저 차이](svg-browser-validation.ko.md).

네이티브 폰트 아이콘은 `PlatformTextGlyphMesh.Outline`, `PlatformTextGlyphOutline`, `UiVectorCurve`를 통해 닫힌 불변 윤곽을 보존합니다. DirectWrite/HarfBuzz 곡선은 SVG 곡선 계산·GPU 채움을 공유합니다. 확대·회전 시 준비한 장면을 재사용하며 CPU 래스터 요청을 만들지 않습니다. 고정 폰트 윤곽의 브라우저 RGBA8 합성 기준은 정확히 일치하지만 기존 공식 SVG 불투명 직접 렌더링 기준에는 차이가 남습니다. 두 검증 범위를 구분합니다. [계약과 현재 근거](fluent2-rating.ko.md#네이티브-곡선-보존).

기본 복원 실행기에 절대 경로의 테스트 `-ValidationProject`와 `-ValidationArtifactsDirectory`를 추가로 지정할 수 있습니다. 해당 프로젝트를 먼저 복원한 뒤 기본 Linux·SVG를 복원합니다. HTTP 캐시는 자식 프로세스에만 설정하고 취약성 감사를 유지합니다. 추가 빌드 산출물도 저장소 밖에 둘 수 있습니다.

`Fluent.Rating`은 float 바인딩의 별점·반점·RTL·너비 줄바꿈을 공통 라디오 입력/접근성으로 제공합니다. `Fluent.RatingDisplay`는 평점·리뷰 수를 표시하는 비대화형 이미지이며 compact와 숫자 지역화를 지원합니다. 두 위젯은 모양·색상 표현을 공유합니다. Observer의 **평가** 탭에서 확인합니다. 복원된 자산을 재사용하려면 `./run-gallery.ps1 -NoRestore`를 명시합니다. [API·검증·남은 범위](fluent2-rating.ko.md).

Rating 항목의 접근성 이름은 `UiRatingOptions.ItemLabel`로 지정합니다. 외부 번역 데이터만 바뀌면 `RefreshItemLabels()`를 호출합니다. 입력·런타임 식별자는 유지합니다. 공통 폰트 아이콘은 `IPlatformVectorTextBackend`의 윤곽과 SVG 공통 GPU 커버리지 캐시를 사용합니다. MSAA 1의 중성 가장자리·캐시 재사용을 검사했으며 브라우저 픽셀 동등성은 아직 미완료입니다.

관리 검증 후 기본 lock 복원은 `scripts/RestoreManagedValidationDefaults.ps1`에 HTTP 캐시와 새 로그 디렉터리를 명시해 실행합니다. 쓰기 가능 공간·SDK를 먼저 확인하고 Linux→SVG 순서와 lock 해시를 검사합니다. 사용자 캐시 설정과 취약성 감사를 바꾸지 않습니다. [명령·범위](input-event-batches.ko.md#검증-후-기본-lock-복원).

순서 입력 회귀는595개이며 중첩 텍스트·예약·애니메이션 가시성·삭제·지연 GPU 제출40개를 포함합니다. 이전 프레임의 불변성과 최종 소비자 해제를 검사하며 전체 캐시·플랫폼 수용과 구분합니다. [실행·범위](input-event-batches.ko.md#중첩-메타데이터와-지연-프레임-제출).

입력 전용 레이어 재생은 예약·부모 텍스트 의존성을 유지한 뒤 표시용 명령 조정과 프레임/장면 임대를 생략합니다. 내용·클립·투명도·이동·3D 변환·중첩 레이어·텍스처 변경의 GPU 화면84쌍과 입력 회귀555개가 통과했습니다. 시간·FPS 개선율은 미측정입니다. [검증 기록](../artifacts/goal-audit-2026-11-16/cache-phase-verification.json).

입력 묶음 안에서 실제 버튼 콜백이 여백·회전·배율·스크롤을 바꾼 뒤의 클릭과 Input/Textarea/Markdown 편집을 검사합니다. 두 순회 순서의 새 검사84개를 포함한345개가 통과했습니다. [범위·실행 명령](input-event-batches.ko.md#이벤트-사이-위젯-위치-변경).

순서 입력은 공통 Input/Presentation 단계를 사용하며 마지막 단계에서 그리기를 한 번 구성합니다. 갤러리의 SVG 변환 슬라이더에는 `UiSliderOptions.CoalescePointerMoves`를 켰습니다. `UiCodeView`도 보이는 코드 안에서 연속 이동을 묶어 선택 범위를 바꾸며, 선택 시작점과 가장자리 자동 스크롤을 유지합니다. [입력과 표시 단계](input-event-batches.ko.md#입력과-표시-단계)

앱은 대기 중인 네이티브 입력을 도착 순서대로 목록에 쌓고, UI 스레드가 가져갈 때 한 번에 넘깁니다. `app-ordered-input-only`로 입력 순서와 넘겨준 목록의 보존 여부, 연속 입력의 메모리 할당량을 확인할 수 있습니다. [입력과 표시 단계](input-event-batches.ko.md#입력과-표시-단계)

그리기 빌더의 Build는 출력을 이전·소비하며 컨테이너를 명시적으로 반환합니다. 초기화·폐기는 채널을 포함한 미게시 소유 기하만 반환합니다. 콜백 실패 시 미종료 레이어와 대기 장면 임대를 정리하고 호출자의 이전 프레임은 유지합니다. [소유권 계약](input-event-batches.ko.md#미게시-그리기의-소유권). 공통 입력/표시 단계는 위 계약으로 활성화되어 있습니다.

`UiContext.InputProfilingEnabled`와 `InputProcessingMetrics`로 일반·묶음 Render의 준비·UI 구성·그리기 마무리를 구분합니다. 즉시 화면에서도 프레임 요청 없이 다음 호출의 측정을 켜고 직전 호출을 읽습니다. 시간 측정이 꺼져도 개수는 제공합니다. [계약·한계·실행 진단](input-event-batches.ko.md#cpu-처리-구간-측정).

Windows는 포커스를 잃으면 소유한 눌린 키의 해제를 전달해 이벤트 순서를 유지하고 Core 반복 마감을 지웁니다. 포커스 키 수명20개를 포함한 네이티브383개가 통과했습니다. 실제 키보드·TSF와 전체 포커스 수용은 별도입니다. [계약·실행 검사](input-event-batches.ko.md#windows-포커스-상실과-눌린-키).

`UiKeyRepeatSettings.Source`로 엔진 타이머와 플랫폼이 전달하는 반복 중 담당 주체를 고릅니다. Windows는 `Platform`, 수동 설정은 기존 `Engine`을 선택합니다. `UiState.KeyRepeatSource`로 현재 주체를 확인하며 전환 때 이전 마감을 지우고 플랫폼 방식의 경쟁 키 타이머는 오류로 알립니다. Core129개가 통과했고 포커스·수명과 입력 전용 CPU 수용은 별도로 남았습니다. [계약·실행 검사](input-event-batches.ko.md#반복-주체-선택).

Windows 키·문자 메시지는 묶인 반복 횟수를 보존합니다. `UiKeyEvent.IsRepeat`로 반복 누름을 구분하며 `repeat: false`는 이를 제외합니다. 완성된 Unicode 스칼라가 첫 단위의 대상·횟수를 유지하고 반복된 IME 결과는 중복을 억제합니다. 네이티브 메시지 하나의 UI 입력 단계는 유지합니다. [계약·최대 횟수 검사·한계](input-event-batches.ko.md#네이티브-반복-횟수).

입력 이벤트 시계와 애니메이션·프레임 시계를 구분합니다. 네이티브 큐의 `MSG.time`을 App으로 전달하며 클릭 연결과 키 반복은 `EventTimeSeconds`·`InputTimeSeconds`를 소비하고 호스트 시간은 한 번만 진행합니다. Core129개·네이티브383개가 통과했으며 입력 전용 CPU 구성과 전체 키보드 포커스·수명은 남았습니다. [시계 계약·명령·한계](input-event-batches.ko.md#이벤트-시간과-프레임-시간).

Windows App과 Observer는 네이티브 입력을 순서대로 전달합니다. 수집 시점의 IMM 문자열·문자 소유권·개별 배율이 실제 App·Core 소비 경로에 연결됐으며 네이티브 전달·시각·반복·수명 363개와 App 연결 37개 검사가 통과했습니다. 소유한 입력을 폐기하면 필수 계약인 `IUiImeHandler.ReleaseInput`을 호출하며 살아 있는 숨김 입력의 늦은 확정은 보존합니다. 사용자 정의 처리기도 이를 구현해야 합니다. 저수준 백엔드는 집계 전용 호스트를 위해 명시적인 기존 기본값을 유지합니다. 실제 키보드/TSF 서비스 수용·입력 전용 CPU 최적화는 남았습니다. [수집·실행 명령](input-event-batches.ko.md#windows-네이티브-순서-수집), [수명 계약](windows-ime-routing.ko.md#제거한-입력과-숨긴-입력).

Windows의 일반 창, 모달 창, 모달리스 창은 같은 백엔드 설정으로 입력 순서를 보존합니다. 일반 갤러리에서 왼쪽 방향키와 문자를 연달아 보내면 문자가 이동한 커서 위치에 들어갑니다. 지우기와 메뉴 이동 뒤에도 검색어와 화면에 표시되는 C# 예제 값이 일치합니다.

Windows .NET 11 관리형에서 `ShowModal`, `ShowModalAsync`, `ShowModeless`에 같은 Win32 입력 메시지를 보내 검증했습니다. 검색어 지우기, 한글과 영문 입력, 왼쪽 방향키 직후 문자 삽입이 모두 동작했고 각 창은 정상 종료했습니다.

Linux X11 백엔드는 입력 상태가 바뀌는 이벤트를 순서대로 전달합니다. 각 단계에는 해당 이벤트의 키 변화, 확정 문자, 버튼 누름·뗌, 휠 이동량과 수집 시점 배율이 들어갑니다. X11 화면이 있는 환경에서 `Duxel.Linux.App.Tests x11-ordered-input`을 실행하면 됩니다. 검증기는 자신이 만든 숨김 창에만 이벤트를 보냅니다.

Linux 갤러리를 실행한 뒤 외부 검증기의 `x11-gallery-search-input <갤러리 PID> <새 결과 JSON 경로> <X11 창 ID>`로 SearchBox의 연속 키 입력을 확인할 수 있습니다. AT-SPI로 텍스트와 커서 위치를 읽고 다른 메뉴에 다녀온 뒤에도 값이 유지되는지 검사합니다. 검증용 갤러리 창의 X11 ID는 10진수로 지정합니다.

같은 인자로 `x11-gallery-pixels`를 실행하면 SearchBox, 개요, RadioGroup의 1280 × 900 RGB 화면을 각각 두 번 저장합니다. 갤러리 창의 클라이언트 영역 전체가 X11 화면 안에 있어야 합니다. 빌드마다 두 캡처를 먼저 비교하면 캡처 사이에 달라지는 픽셀을 확인할 수 있습니다.

Ubuntu x64에서 .NET 11.0.0 Linux ARM64 갤러리를 교차 게시했습니다. QEMU의 Xvfb·lavapipe 환경에서 AT-SPI 등록과 Vulkan 스왑체인 생성을 확인했습니다. 두 검사 모두 첫 프레임이 표시됐는지 확인하기 전에 제한 시간에 도달했습니다. ARM64 실기기와 전체 렌더링 검증이 남아 있습니다.

WSLg에서 개요, SearchBox, RadioGroup을 두 번씩 캡처했습니다. 관리형과 NativeAOT 사이에서 달라진 픽셀은 상단의 FPS와 렌더러 수치 글자에만 있었고 나머지 픽셀은 모두 일치했습니다.

새로 빌드한 .NET 11.0.0 관리형과 NativeAOT에서도 갤러리 표시 상태 105개가 같은 순서와 값으로 기록됐습니다. 외부 클라이언트는 SearchBox, Tree, 가상 그리드에서 빌드마다 18건을 확인했습니다. [Linux 갤러리 검증](linux-platform.ko.md#net-1100-갤러리-검증)

외부 클라이언트의 `x11-gallery-fps` 모드는 실제 갤러리의 수치 표시와 앱의 프레임 기록을 대조해 FPS 갱신과 이벤트 대기 동작을 확인합니다. [Linux FPS 검증](linux-platform.ko.md#net-1100-갤러리-검증)

외부 클라이언트의 `x11-gallery-drawer` 모드는 두 갤러리 실행본에서 모달 패널의 포커스 이동과 복원, 다시 열었을 때 편집값 유지, 저장을 확인합니다. [Linux Drawer 검증](linux-platform.ko.md#net-1100-갤러리-검증)

Linux ARM64 진단 갤러리는 QEMU와 lavapipe에서 MSAA 4×와 1× 모두 첫 그래픽 펜스 대기 시간이 초과됐습니다. [ARM64 진단](linux-platform.ko.md#arm64-첫-프레임-진단)

사전 렌더링을 생략한 ARM64 진단 빌드에서 개요의 직접 그리기 명령 130개가 모두 완료됐습니다. 벡터 장면 기록을 켜면 첫 펜스 시간 초과가 재현됩니다. [ARM64 캡처와 단계 비교](linux-platform.ko.md#arm64-첫-프레임-진단)

ARM64 QEMU에서 계산을 뺀 복사는 완료됐고, 결과 복사를 생략해도 계산을 켜면 시간 초과가 재현됐습니다. 윤곽선 기록과 단일 명령 채우기에서도 각각 재현됐습니다. [ARM64 벡터 계산 진단](linux-platform.ko.md#arm64-첫-프레임-진단)

같은 ARM64 진단에서 버퍼 주소로 읽고 쓰는 최소 셰이더는 정상 종료했습니다. 원본 벡터 장면 셰이더와 최적화해 다시 빌드한 셰이더는 모두 첫 펜스에서 멈췄습니다. [ARM64 셰이더 비교](linux-platform.ko.md#arm64-첫-프레임-진단)

ARM64 QEMU 진단에서 장면 준비와 경계 검사만 실행하면 화면이 표시됐습니다. 채우기 색을 고정해도 레이어 스택 합성을 남기면 시간 초과가 재현됩니다. [ARM64 레이어 합성 비교](linux-platform.ko.md#arm64-첫-프레임-진단)

ARM64 QEMU의 게스트 코어에서 lavapipe가 간접 호출한 코드에서 신호 11이 발생했음을 확인했습니다. 같은 바이너리로 시스템 호출 추적을 켠 별도 실행은 벡터 복사를 생략한 상태로 두 프레임을 표시했습니다. [ARM64 오류 분석](linux-platform.ko.md#arm64-첫-프레임-진단)

ARM64 고정 색 진단은 결과 복사를 켰을 때 계산 셰이더가 쓴 색이 화면에 표시되는 것을 확인했습니다. 같은 복사 경로에서 원본 벡터 장면 셰이더를 사용하면 첫 프레임이 완료되지 않았습니다. [ARM64 복사와 표시 검증](linux-platform.ko.md#arm64-첫-프레임-진단)

ARM64 게스트 코어에서 16×17 채우기 작업의 JIT 실행을 확인했습니다. Cortex-A72와 Cortex-A76 에뮬레이션에서도 전체 벡터 명령은 화면 표시 전에 실패했습니다. [ARM64 채우기 진단](linux-platform.ko.md#arm64-첫-프레임-진단)

현재 Linux x64 NativeAOT 빌드는 .NET 11.0.0에서 WSLg 화면 표시 105개와 외부 SearchBox 검사 3개를 통과했습니다. 관리형과 시나리오 순서와 기록된 UI 상태가 일치했습니다. X11에서 키가 연달아 들어와도 SearchBox가 입력 순서와 커서 위치를 유지했습니다.

`UiContext.NewInputBatch`는 순서 있는 입력 상태를 소비하고 최종 그리기 하나를 만듭니다. Core·편집기·버튼·GPU 검사와 연결된 App 전달 경로를 네이티브 생산자 통합과 구분합니다. [계약·실행 검사·한계](input-event-batches.ko.md#core의-순서-있는-입력-소비).

미제출 그리기를 교체할 때 텍스처 명령과 픽셀 소유권을 다음 프레임에 유지합니다. 이미 소비자에게 넘긴 프레임은 소비자가 계속 소유합니다. [수명 계약·GPU 대조·실행 명령](input-event-batches.ko.md#미제출-그리기의-자원-수명).

주입 키·문자 묶음은 저장소를 재사용하며 유효 개수만 전달합니다. 작은 묶음이 이전 이벤트를 재전달하지 않습니다. [계약·회귀 명령·보존한 기존 집계 순서 대조](input-event-batches.ko.md).

Textarea·Markdown은 비활성 상태에서 늦은 IME 확정을 받아도 최종 커서를 저장합니다. Input·Textarea·Markdown의 실제 위젯과 예약된 확정 요청을 함께 검사합니다. [포커스 연결과 수용 범위](windows-ime-routing.ko.md).

Windows IME 포커스 전환은 조합 세대를 확인한 확정 요청을 소유 스레드로 전달합니다. 다음 입력의 후보 위치는 이전 조합 종료까지 보류하고, 오래된 위치·오류는 현재 입력에 적용하지 않습니다. [동작과 실제 서비스 검증의 경계](windows-ime-routing.ko.md).

Windows 문자 메시지는 완전한 Unicode 스칼라만 전달합니다. 서로게이트 쌍 사이에 프레임이나 논리 포커스가 바뀌어도 첫 코드 단위의 입력 대상을 유지하며, 일반 입력·IME 경로의 조합 수명 초기화를 연결합니다. [경계와 검증 범위](windows-ime-routing.ko.md).

Windows IME의 확정 결과와 다음 조합이 같은 메시지로 와도 둘 다 유지합니다. 속성만 갱신하면 원문을 다시 읽지 않고, 취소·결과 전용 알림은 조합 표시를 지웁니다. [동작과 검증 범위](windows-ime-routing.ko.md).

오버레이를 닫을 때 접근성 노드와 대기 중 Label 관계를 함께 제거합니다. 닫기·다시 열기의 Observer 회귀와 원래 목표의 남은 수용 기준은 [구현 수용 기준 점검](acceptance-status.ko.md)을 참고하세요.

Windows IME는 조합을 시작한 입력 대상을 유지하고 다른 입력에는 조합 문자열을 노출하지 않습니다. 늦은 확정도 원래 위젯으로 전달하며 비활성 한 줄 위젯의 커서를 저장합니다. [메시지 검사 범위와 남은 네이티브 프로토콜 작업](windows-ime-routing.ko.md).

한 줄 문자 입력·붙여넣기·IME 확정은 최종 삽입 경계를 공통 처리합니다. 선택 교체·길이 제한에서도 원문과 화면 커서·접근성 좌표를 일치시킵니다. [범위](windows-text-shaping.ko.md).

한 줄 IME 확정 시 커서의 네이티브 뒤쪽 경계를 보존하고, 조합 중 드래그에는 전체 표시 문장 폭을 사용합니다. **입력·IME → 한 줄 커서 예제**에서 조합 확정과 선택을 확인하세요. [검증 범위](windows-text-shaping.ko.md).

화면 DPI와 OS 글자 크기는 별도 플랫폼 배율을 사용합니다. Windows는 네이티브 접근성 텍스트 배율, Linux X11은 자체 XSETTINGS·X resources와 데스크톱 글꼴 크기를 따릅니다. 래스터 글자 비트맵은 소수 픽셀 스크롤 중에도 물리 `PixelSnap`을 적용합니다. 우선순위·검증 명령·실행 한계는 [시스템 글자 배율](system-typography.ko.md)을 참고하세요.

`run-gallery.ps1`은 Windows 증분 빌드와 실행 디렉터리를 분리합니다. 기존 창이 DLL을 사용 중이어도 새 실행을 준비하며, 실행 경로는 `Observer runtime`에 표시합니다.

한 줄 입력의 네이티브 행 정보는 버퍼 수명에 맞춰 재사용하며 클릭·커서·선택·스크롤·접근성 좌표를 공유합니다. IME 표시 문자열도 한 문장으로 조판하고 조합 끝 좌표를 사용합니다. Windows 토글의 세로 정렬은 실제 래스터 글자 경계를 사용합니다. **입력·IME → 한 줄 커서 예제**와 상단 토글에서 확인하세요. 실제 OS IME 자동화와 Linux 네이티브 실행은 별도입니다. [상세 범위](windows-text-shaping.ko.md).

Markdown의 HTML 엔티티는 표시 문자 수와 관계없이 원문 전체가 하나의 커서 단위입니다. 활성 편집 블록에서는 원문을 노출하며 파싱 결과를 재사용합니다. 네이티브 문단은 확장 문자열을 나누지 않는 줄 경계를 유지합니다. **Markdown → 문단 조판 예제**에서 확인하세요. [범위와 한계](styled-paragraphs.ko.md).

혼합 Markdown 문서는 표 앞뒤에 네이티브 문단 조각을 배치하고 원문 위치·활성 원문 표시·빈 줄·마우스와 선택 영역·이미지 영역을 보존합니다. 조각을 다시 파싱하거나 네이티브 글리프의 소수점 원점을 없애지 않습니다. 한 줄 입력·확장 문자·전체 편집 적합성은 이번 Windows 검증과 별도 작업으로 남아 있습니다. [범위](styled-paragraphs.ko.md)를 참고하세요.

Markdown 표 셀을 변경되지 않는 원문 조각과 네이티브 문단에 연결했습니다. 그림·RTL 커서·선택·열 사이 이동·이미지 접근성 영역이 같은 배치를 사용하며 열의 자연 너비와 가로 넘침은 유지합니다. 옵저버에 **표 셀 예제**를 추가했습니다. 혼합 문서의 바깥 블록도 문단 조각을 사용하며 남은 입력 경로·새 Linux 실행 검증은 남아 있습니다. [범위](styled-paragraphs.ko.md)를 참고하세요.

표가 없는 Markdown 문서의 작업 항목·인용문·구분선을 네이티브 문단에 연결했습니다. 이미지·작업 항목의 숨겨진 원문 경계를 공통 모듈로 처리하고 작업 표시는 Fluent 모양으로 그립니다. 집중 검사 11개와 정확한 GPU 비교 80쌍을 추가했으며 옵저버에 **구조적 문단 예제**를 제공합니다. 표 셀과 바깥 문단도 네이티브 배치를 사용하며 새 Linux 실제 실행 검증은 남아 있습니다. [현재 범위](styled-paragraphs.ko.md)를 참고하세요.

여러 줄 입력·Markdown의 일반 방향키와 길게 누르기는 네이티브 글자의 화면 경계와 양방향 커서 위치를 공유합니다. 같은 행의 선택 해제·이미지·줄바꿈 경계도 같은 배치를 따릅니다. 원문 순서의 편집·Ctrl+단어 이동·한 줄 입력의 이동은 별도 경로를 유지합니다. [검증 범위와 남은 한계](styled-paragraphs.ko.md)를 참고하세요.

일반 Markdown 이미지는 네이티브 문단 객체 배치·RTL 마우스 영역·원문 경계를 공유합니다. Windows에서 집중 검사 7개와 독립 네이티브 GPU 이미지 비교 48쌍이 통과했고, 실제 옵저버의 **이미지 예제** 버튼까지 42개 시나리오로 검증했습니다. Linux 소스는 컴파일했으나 새 네이티브 실행 검증은 남아 있습니다. [범위와 근거](styled-paragraphs.ko.md)를 참고하세요.

옵저버의 메시지 페이지는 자연 높이로 측정하는 열을 `UiScrollView` 안에 배치합니다. 내용이 탭 영역보다 길면 스크롤하며 높이 초과로 종료되지 않습니다. 2026-11-16에 보고된 높이 707.4625px와 작은 높이 240px를 포함한 페이지 검사 34개, 실제 옵저버 표시 시나리오 41개를 다시 통과했습니다. [종료 오류 재검증](../artifacts/fluent-messagebar-2026-11-15/messagebar-crash-recheck.json)을 참고하세요.

네이티브 문단의 저수준 객체 요청은 원문 U+FFFC 위치·크기·기준선을 받으며 Windows에서 실제 배치 영역을 반환합니다. 기존 생성자는 유지합니다. 실제 소유 위젯 배치와 기준 방향에 연결했습니다. Linux 코드는 연결했지만 새 실행 검증은 대기 중입니다. [객체 계약과 플랫폼 한계](styled-paragraphs.ko.md)를 확인하세요.

텍스트만 포함한 소유 인라인 흐름과 MessageBar 본문도 네이티브 문단을 공유하며 실제 자식의 접근성 ID를 유지합니다. 실제 위젯이 섞인 흐름도 네이티브 배치와 기준 방향을 공유합니다. 새 Linux 실행 검증은 남아 있습니다. [사용 범위와 예제](inline-text.ko.md)를 확인하세요.

텍스트 전용 Markdown은 공통 네이티브 문단의 줄바꿈·커서·선택·그림을 공유합니다. 숨겨진 기호는 원문 위치에 남고, Fluent의 지정 줄 높이를 유지합니다. 구조적 이미지·표·작업 항목·인용문·실제 자식 통합과 새 Linux 검증은 남아 있습니다. [문단 계약과 검증 범위](styled-paragraphs.ko.md)를 확인하세요.


일반 문자열용 `UiInlineText.RightToLeft`를 공통 네이티브 요청에 연결했습니다. 값이 바뀌면 갱신하고 같으면 재사용합니다. Windows70개·정확 GPU128쌍·실제 스위치 전환을 포함한40개 시나리오가 통과했습니다. Linux 소스도 연결했지만 Ubuntu 파일시스템 I/O 오류로 새 NativeAOT 빌드가 실패했으며 `/bin/true`도 실행되지 않습니다. 이전 `e529777d`를 유지하고 환경 복구 전에는 Linux 빌드를 반복하지 마세요. L:가 가득 차 새 검사 출력은 `W:/Cache/Duxel/literal-direction-20261116`을 사용합니다. [API·상태](inline-text.ko.md).

최신 Linux 스타일 문단 전달본은 `e529777d`이며 아래 이전 전달 기록보다 우선합니다. NativeAOT 스타일351개·기존334개·텍스트114개와 실제 옵저버41개·전달 실행기38개가 통과했습니다. Linux도 Windows 일반 문자열용 `UiInlineText`와 같은 문단 기능을 구현했으며 Markdown·실제 자식·UI 방향·이동 통합은 남아 있습니다. Windows73 소스 지문17개가 같아 검증 결과를 재사용했습니다. [근거](../artifacts/fluent-messagebar-2026-11-15/linux-styled-paragraph-verification.json).

[스타일 문단 스냅샷](styled-paragraphs.ko.md)을 Windows·Linux에 구현하고 일반 문자열용 `UiInlineText`에 연결했습니다. 타이포그래피 옵저버에서 혼합 굵기·여러 폰트·양방향 문장 예제를 스크롤하며 확인할 수 있습니다. Markdown 투영, 실제 인라인 자식과 UI 기준 방향 선택은 남아 있습니다. 이전 검증 실행본은 별도로 보존하며 API가 있다는 사실로 전체 적합성을 판단하지 않습니다.

Windows와 Linux의 동일 스타일 행에서 네이티브 행 위치를 공유합니다. Windows는 명시적 폰트 구간을 가로지르는 양방향 문맥을 보존하며, 독립 네이티브 픽셀·벡터·커서·Core 검사16개와 관련 회귀가 통과했습니다. Linux의 기존 네이티브334개·텍스트114개 검증은 유지하고, 현재 공통 옵저버 전달본 `7746d0b7`은 후보41개·전달38개가 통과했습니다. 입력·IME 예제에 한글·히브리어 폰트 구간을 가로지르는 RTL 임베딩을 추가했습니다. 혼합 스타일 전체 문단·기준 방향과 줄바꿈 통합·물리 지연은 남아 있습니다. [Windows 조판](windows-text-shaping.ko.md), [Linux 실행](linux-text-shaping.ko.md), [현재 검증](../artifacts/fluent-messagebar-2026-11-15/paragraph-font-context-verification.json).

남은 범위는 혼합 스타일 전체 문단 구성과 명시적 기준 방향·줄바꿈 통합입니다. Windows의 원래 접두어 폭 커서 결함4개와 Linux의 네이티브 클릭 경계 결함9개는 검증한 동일 스타일 행에서 수정했습니다. 집중 검사 수를 전체 RTL 완료량으로 계산하지 않습니다.

이전 확인(2026-11-16): 메시지 탭 높이 검사34개·실제 Windows38개와 빈 조판·0 이동량 계약52개가 통과했습니다. 당시 전달본 `798c1472`는 이전 기록이며 현재 원장의 실행본으로 교체했습니다. [이전 근거](../artifacts/fluent-messagebar-2026-11-15/zero-advance-verification.json).

현재 MessageBar 기준은 [최신 계약](fluent2-messagebar.ko.md)이며 아래 이전 검증 건수보다 우선합니다. 높이 초과 종료는 스크롤 뷰로 수정했습니다. Windows38·Linux NativeAOT41·전달38 시나리오 통과. 공통 `UiInlineFlowWidget.HorizontalAlignment` Left·Center·Right는 그림과 실제 자식/span 위치에 같은 행 오프셋을 적용합니다. MessageBar는 실제 본문 정렬도 LTR/RTL에 맞춥니다. CPU155·GPU176쌍 통과; 전체 RTL은 미완료이며 기존 Windows 조판 실패6개는 공통 DirectWrite 경로로 교정했습니다(확장 37개 통과). Linux 원장은 `c8246d` 실행본을 선택합니다. 원래 Windows NativeAOT 관문은 별도입니다.

키 기반 그룹: `UiKeyedWidget`와 `SetItems`로 실제 삽입·삭제·재정렬을 처리합니다. 생성 시 `keys:`로 안정적인 소유 키를 지정합니다. 전체 검증 후 즉시 채택하고 다음 준비 프레임에 적용하며 종료 후 실제로 폐기합니다. 같은 키는 활성·종료 중 같은 인스턴스를 유지합니다. 모델48·높이15·관련 회귀587·GPU424쌍·Windows36/Linux39 시나리오 통과. 실제 네이티브 클라이언트·전체 MessageBar는 미완료입니다. 팩터리/API 변경 시 `tests/Duxel.Graphics.Tests/Fixtures/FluentFactoryInventory.json`의 명시적 이름 계약도 검토하세요. [계약](fluent2-messagebar.ko.md).


MessageBarGroup과 공통 PresenceGroup은 실제 자식의 제어 표시·GPU 종료 그림·재배치·제거 후 포커스를 처리합니다. `SetVisible(index, bool)`로 닫고 다시 엽니다. 최초 진입 효과 없음, ReducedMotion 지원, 유휴 프레임 종료. 실제 동적 컬렉션과 전체 적합성 범위는 최신 계약을 참고하세요. 모델17개·GPU140쌍·Windows32탭 통과. [계약](fluent2-messagebar.ko.md).


공통 `UiInlineFlowWidget`는 실제 텍스트 span과 위젯 자식을 소유합니다. `Fluent.MessageBarBody`·`MessageBarTitle`를 사용할 수 있으며 제목/본문의 연속 조판, 실제 링크·입력, Margin·Transform 영역 확보를 유지합니다. 모델31개·기존 회귀340개·GPU152쌍 통과. Group의 전체 API 적합성·자동 공지·전체 MessageBar는 미완료입니다. [계약](fluent2-messagebar.ko.md).


2026-11-15 Windows 시작 실패를 수정했습니다. 새 MessageBar 팩터리에 실제 **메시지 표시** 탭을 연결했으며 시작 검사는 유지합니다. `./run-gallery.ps1`로 실행하세요. `./run-gallery.ps1 -Verify`는 실제 32개 탭을 순회하고 종료하는 표시 검사입니다. 실행 로그는 `%LOCALAPPDATA%/Duxel/ImplementationObserver/logs/`에 남습니다.

Linux 공통 글꼴 지표의 descent 부호를 교정해 버튼·배지·체크박스·라디오·스피너·본문·입력·숫자 입력의 세로 정렬을 바로잡았습니다. 기존 창을 닫고 `& ./artifacts/linux-present-path-2026-11-15/run-observer.ps1`로 다시 실행하세요. 25개 글꼴 지표와 32쌍 위젯/커서 픽셀 대조, NativeAOT 35개 표시 시나리오를 통과했습니다. 속도 개선을 측정한 변경은 아닙니다.


2026-11-15 [MessageBar 구현 중](fluent2-messagebar.ko.md): 본체·실제 액션 소유 및 반응형 배치, 공식4가지Intent·Light/Dark·아이콘·Rounded/Square를 추가했습니다. 모델45개·독립 GPU64쌍 통과. 현재 소스 팩터리64개·상위38/55 연결·17개 미연결이며 전체 적합성 완료 수가 아닙니다. 제목·그룹·모션·옵저버는 남았고 Linux 전달본은 유지합니다.

2026-11-15 [혼합 굵기 인라인 텍스트](inline-text.ko.md): 공통 `UiInlineText`가 실제600/400/700 글꼴·보존형 줄바꿈·단일 정적 접근성을 제공합니다. 모델32개·기존 텍스트64개·독립 GPU 픽셀32쌍을 확인했으며 공식 Fluent 목록과 Linux 전달본은 유지합니다.

2026-11-15 [접근성 공지](accessibility-announcements.ko.md): 시각 트리·포커스와 분리한 프레임 공지 API, 실제 Windows UIA 및 고정 Linux NativeAOT 전달을 확인했습니다. MessageBar 시각 구현과 전체 접근성은 남아 있습니다.

2026-11-15 [일반 텍스트 접근성](fluent2-text.ko.md): 같은 문구의 독립 정체성·실제 배치 경계·한 번의 소스 읽기·네이티브 내용 갱신을 연결했습니다. 실제 옵저버·실행기를 확인해 현재 배포에 반영했으며 성능 비교는 구간 중첩으로 방향 미확정입니다. 이전 실행물은 보존합니다.

2026-11-15 [Skeleton](fluent2-skeleton.ko.md): 로딩 범위·Wave/Pulse·상속·정적 모션·실제 native busy와 옵저버 스켈레톤 탭을 추가했습니다. 팩터리62개, 상위37/55 연결·18개 미연결이며 연결 수는 전체 적합성 완료 수가 아닙니다.

## Linux CPU 작업 스레드 최신 검증

기본24개 llvmpipe 작업 스레드를4개로 제한한 동일 RC3 NativeAOT의 독립3쌍 비교에서 세 화면 CPU37.60~38.51% 감소를 확인했습니다. MSAA·폰트·갱신 면적은 같고, 슬롯1/3의7,140개 검사 및 기존 기본 스레드와1,216쌍 원시 이미지가 모두 일치합니다. 최신 검증본 실행기는 실행한 프로세스에만 설정을 적용합니다. 직접 실행은 루트 실행기의 `-SoftwareRasterThreads 4`로 선택하며 생략하면 드라이버 기본값을 유지합니다. [실행·범위·남은 제한](../artifacts/linux-driver-worker-budget-2026-11-15/README.ko.md). WSL 하드웨어 경로는 필수 합성 기능 미지원으로 적용하지 않았습니다.

## Linux 배치 내부의 작은 갱신 (2026-11-15)

공통 Vulkan은 스트리밍 배치 안의 변경 도형과 같은 스타일 채움의 이동 경계를 비교합니다. 정적 눈금은 갱신 경계를 늘리지 않으며 클립·텍스처·마스크·상주 기하와 각 획득 이미지/MSAA 이력은 보수적인 계약을 유지합니다. 실제 슬라이더 면적 중앙값92.36%, Fluent46.67~48.68% 감소를 확인했습니다. 진단 고정 Fluent CPU11.54% 감소만 구간이 분리됐고 다른 CPU·입력 지연은 개선율을 주장하지 않습니다. Linux RC3 NativeAOT7140개·Windows7108개 검사와2424쌍 원시 픽셀이 일치합니다. [최신 즉시 실행·범위·근거](../artifacts/linux-sparse-damage-2026-11-15/README.ko.md). 전체 목표·물리 표시 지연 완료와 구분합니다.

## 공통 오버레이 수명과 Dialog 진행 상태 (2026-11-15)

Dialog는 `Open` 바인딩 또는 `Show()`/`Close()`와 기본 켜진250ms 입장·퇴장 모션을 제공합니다. `MotionEnabled: false`로 끌 수 있습니다. 닫힌 뒤에는 입력/접근성을 종료하고 캐시 그림만 재생하며, 재열기·숨김·폐기는 소유권과 예약을 정리합니다. 공통 좌표 범위가 클릭·IME를 변환합니다. 독립 계약은11/12입니다. Windows 관리형 UIA와 Linux 열린 Dialog RC3 NativeAOT는 통과했으며 Windows RC3 NativeAOT·전체 통합 검증이 남아 있습니다. [API·실행·근거](../artifacts/fluent-dialog-2026-11-15/README.ko.md).

## 정지 누름과 이벤트 대기 (2026-11-15)

공통 앱 루프는 누른 버튼의 상태 전환·포인터 이동·누름/놓기 이벤트를 비교합니다. 버튼을 누르고 멈춘 것만으로 무한 구성하지 않습니다. 명시적 프레임 요청·애니메이션 기한·텍스트 자동 스크롤·SpinButton 반복은 유지합니다. Linux 옵저버의 `--verify-idle-pointer <새 결과 경로>`는 강제 주기 프레임 없이3화면×3버튼의 정지 누름,24회 속도 지정 이동과 예정된 숫자 반복을 확인하고 자신이 연 창을 종료합니다. 이 PC의 즉시 실행은 `./artifacts/linux-held-input-idle-2026-11-15/run-observer.ps1`입니다. 최종 NativeAOT는32화면·384입력, Windows 관리형은614프레임 앱 세션과415개 조합 계약을 통과했습니다. 전체 목표 완료나 물리 표시 지연의 근거로 확대하지 않습니다. [실행·측정 범위](../artifacts/linux-held-input-idle-2026-11-15/README.ko.md).

## 실제 컴파일러 선택과 선언 제거 (2026-11-15)

생성기 검사는 .NET 런타임과 API 참조 버전, 실제 Roslyn 어셈블리 버전·커밋, 실행 모드와 로드 문맥을 구분해 기록합니다. 기본 검사는 기존4.10 패키지로 컴파일·실행합니다. `-p:DuxelUseSdkRoslyn=true`는 선택한 SDK의 실제 컴파일러로 생성기 드라이버를 실행합니다. `DuxelSdkRoslynDirectory`로 디렉터리를 명시할 수 있으며, 생략하면 `MSBuildToolsPath/Roslyn/bincore` 한 경로를 사용합니다. 잘못된 모드와 선택한 파일 누락은 DUXTEST001/002/003으로 빌드 전에 거부합니다.

SDK 모드는 선택한 파일을 `SdkRoslyn`에 복사하고 해제 가능한 독립 AssemblyLoadContext에서 검사를 실행하며 Duxel.Core 공개 계약은 공유합니다. Linux SDK의 ReadyToRun 컴파일러를 이식 가능한 컴파일 참조로 취급하거나 원본을 고치지 않고 실행합니다. API 컴파일 참조는4.10을 유지하고 실제 생성기를 실행하는 컴파일러를 명시적으로 선택합니다. 패키지 컴파일러가 있어도 선택한 SDK 실행 파일이 없으면 실패합니다. 동적 컴파일을 사용하는 관리형 검사이며 NativeAOT 런타임 검사는 아닙니다.

최종 Windows .NET11/패키지4.10, Windows .NET11/SDK Roslyn5.12, Linux 고정 SDK11.0.100-rc.3.26522.148·런타임11.0.0-rc.3.26522.148/해당 SDK Roslyn5.12에서 각각131개가 통과했습니다. 저장한 생성 소스6개는 세 프로필 모두 이전117개 검사 기준의 파일과 바이트가 같습니다. 제품 생성기 구현·런타임 위젯·제품 의존성은 바꾸지 않았습니다.

같은 파일과 독립 소스 트리에서 첫/중간/마지막 선언을 제거하면 남은 두 출력의 재사용은 세 프로필 모두0/1/2개입니다. 실제 타입 제거와 소스 내용 보존은 통과하지만, 첫 선언 제거의 원래2/2 캐시 기준은 미완료입니다. 파일을 나누어도 충족하지 못했습니다. 패키지의 정확한 Roslyn 소스 커밋은 `2b7d172669b2f7e55803b55f317cfcc2d4279d76`이며 [상태 테이블 구현](https://github.com/dotnet/roslyn/blob/2b7d172669b2f7e55803b55f317cfcc2d4279d76/src/Compilers/Core/Portable/SourceGeneration/Nodes/NodeStateTable.cs#L309)은 대응 위치를 비교합니다. 현재 경로의 검증된 한계이며 최적화 완료나 모든 다른 설계가 불가능하다는 뜻은 아닙니다.

기존 생성기 검사 게시·실행 명령에 `-p:DuxelUseSdkRoslyn=true`를 주면 SDK 모드입니다. 생략하거나 `false`를 주면 패키지 모드입니다. 작업 디렉터리/global.json 또는 명시한 dotnet 실행물로 SDK를 선택하고 새 디렉터리에 게시한 뒤 `dotnet exec <게시한 Duxel.Generators.Tests.dll> <새 결과 디렉터리>`로 실행합니다. 결과에는 `icon-removal-matrix.json`, `icon-incremental-limits.json`, `core-catalog.json`이 있습니다. 현재 컴파일된 초안 카탈로그는 팩터리59개, 필수 상위55개 중35개 연결·20개 미연결이며 연결 수를 전체 Fluent 적합성 완료로 세지 않습니다. 근거: `artifacts/generator-compiler-selection-2026-11-15/verification.json`.

2026-11-15 Linux 접근성 진단에 text-attributes-native와 probe-failure-contract를 추가했습니다. 두 세션 경로의 포커스·경계·속성 각48개를 확인하고, 자식의 관리형 실패를 소스 생성 JSON으로 부모까지 보존합니다. 전체 검사 재시도나 과거 실패의 원인 제거 완료로 해석하지 않습니다. [범위](linux-platform.ko.md).

Linux 실행기는 `PublishedExecutable`과 `SampleArguments`를 지원합니다. 명시한 기존 실행물을 SDK 게시 없이 열며, 없는 파일을 다른 빌드로 대체하지 않습니다. 최종 NativeAOT로 실행기 경유 32개 화면·384회 입력을 확인했습니다. Linux 샘플 가이드의 첫 명령은 이 PC의 검증된 NativeAOT 빌드를 지정하며, 현재 소스의 관리형 게시 명령은 별도로 설명합니다.

2026-11-15 고정 .NET11 RC3 Linux NativeAOT 옵저버와 동일 RC3 관리형 비교를 추가했습니다. 세 번 교대 측정에서 Fluent 최초 구성은 134.66→31.79ms, 실제 드래그 UI 구성은 0.678→0.257ms였습니다. Linux 검사 결과를 명시적 타입·소스 생성 JSON으로 바꾸어 두 런타임에서 같은 계약을 검사하고 전체 원시 이미지를 비교합니다. Windows AOT·전체 위젯 픽셀 적합성과는 별개입니다. [실행·검증 범위](../samples/Duxel.Gallery.Linux/README.ko.md).

2026-11-15 Linux의 입력 처리와 Vulkan 표시가 X11 연결을 공유해 발생한 긴 입력 지연을 전용 표시 연결로 교정했습니다. 옵저버의 `--verify-pointer-performance <새 결과 경로>`는 접근성을 켜고 실제 X11 드래그를 세 화면에서 검사합니다. 각 화면은 96회 예열·32회 측정합니다. 기존·수정본을 세 번 교대로 비교한 288개 측정에서 50ms 초과가 150→0회였습니다. 전체 32개 화면 표시·정상 종료도 확인했습니다. [최신 실행 가이드](../samples/Duxel.Gallery.Linux/README.ko.md).

2026-11-15 실제 브라우저 Graphite 버전에 맞춰 선형 위치 안정화를 공통 SVG 페인트에 반영했습니다. 그림자 CPU9/9·GPU 흰 배경9/9·투명9/9, 작은 그라데이션8/9·그룹8/11이며 전체 일치는 미완료입니다. [최신 검증·실행](svg-gpu-camera.ko.md).

2026-11-15 SVG RGB·알파 브라우저 캡처를 명시적 채널 옵션으로 통합했습니다. 실제 재캡처4개 해시 일치·실행 전 입력 거부3개 통과. 새 그림자 원본을 포함한 알파4/9와 전체 브라우저 일치는 미완료입니다. [API와 검증](svg-gpu-camera.ko.md).

2026-11-15 단일 페인트의 기하 클립 불투명도 통합을 교정했습니다. 관리271개, 클립 차이951→87픽셀이며 전체 브라우저 일치는 미완료입니다. [검증·최신 샘플](svg-gpu-camera.ko.md).

2026-11-15 GPU 투명 텍스처 전체 비교와 그룹 색 정밀도를 추가했습니다. 관리268개·GPU 읽기9개·Linux 표시32개 통과, 전체 흰 배경0/4·투명0/2는 미완료입니다. [실행·파일 형식·한계](svg-gpu-camera.ko.md).

2026-11-15 SVG 쉼표형 RGB 바이트 해석과 소스 색 저장의 절반 경계값을 교정했습니다. 관리265개·독립 색 입력32,895개·새 GPU SVG3종이 통과합니다. 전체 원본은0/4, 작은 그라데이션2/9, 새 원시 RGBA6종은5/6이며 투명 RGB 읽기 차이는 남습니다. GPU `gpu-color-storage`, 관리 `--browser-color-storage` 명령과 최신 Windows/Linux 실행물은 [가이드](svg-gpu-camera.ko.md)를 참고하세요.

2026-11-15 SVG 단일 그리기 opacity를 컴파일 시 재질에 합쳐 색 정밀도와 불필요한 그룹 합성을 개선합니다. 겹침·효과·중첩 저장 경계는 유지하며 단일·중첩 단색의 독립 브라우저 RGBA2종이 일치합니다. `gpu-opacity-pixels`는 별도11종 진단이며 전체 브라우저 일치와 구분합니다. [GPU 카메라 가이드](svg-gpu-camera.ko.md).

2026-11-15 SVG 공통 불투명도 알파66개와 관리 회귀191개가 통과합니다. GPU 카메라 경로를 유지한 색 정밀도 수정으로 복잡한 Canvas 차이는 줄었지만 전체 브라우저 일치는0/4, 작은 그라데이션은1/9입니다. Windows/Linux의 새 검증 빌드와 명령은 [GPU 카메라 가이드](svg-gpu-camera.ko.md)에 있습니다. CPU 사용량0이나 전체 픽셀 적합성을 주장하지 않습니다.

2026-11-15 실제 GPU 브라우저 전체 픽셀 비교는 gpu-svg-browser 프로필로 원래 해상도를 검사합니다. 현재0/4이며, GPU 색 경계값 반올림 수정과 브라우저 캡처의 실제 크기·위치 검사5건을 추가했습니다. [검증과 한계](svg-gpu-camera.ko.md).

2026-11-15 SVG 테두리와 점선의 길이·위상·분할·끝·접합은 현재 카메라에 맞춰 GPU에서 생성합니다. 기존 효과48·추가 테두리45·점선 경계 조건45개는 독립 scalar 픽셀 비교를 통과했습니다. 브라우저 전체 일치는 미완료입니다. [GPU 카메라](svg-gpu-camera.ko.md).

2026-11-15 [SVG GPU 장면·내부 카메라](svg-gpu-camera.ko.md): 위젯은 GPU에 벡터를 보관하고 카메라의 가시 영역만 그립니다. CPU 래스터 대체 경로는 없으며 명시적 비트맵 출력은 분리합니다. 복잡 문서 픽셀 적합성은 진행 중입니다.

2026-11-14 옵저버 이식 : 같은 29개 탭을 Windows/Linux 호스트에서 사용합니다. 이미지 디코더와 SVG 폰트를 ObserverServices로 주입하고 Linux/X11·Vulkan 실행·정상 종료를 확인했습니다. 오른쪽 상단 전역 갱신 영역 강조는 기본 꺼짐입니다. [Linux 실행 가이드](../samples/Duxel.Gallery.Linux/README.ko.md).

2026-11-14 [화면 갱신 관찰](repaint-observer.ko.md): 옵저버 상단을 Fluent Switch로 바꾸고 **갱신 관찰** 탭에 실제 Vulkan 갱신 범위의 짧은 초록 테두리, 고정/변경 패널과 60Hz 텍스트 변경을 제공합니다. 측정 재사용은 배치 계산용 탭으로 유지합니다. 강조 자체의 정리 비용과 내용 변경을 구분하며 성능 비교는 강조를 끄고 수행합니다.

2026-11-19 [화면 갱신 관찰](repaint-observer.ko.md)에서 이전 초록 테두리는 선이 지나간 부분만 다시 그려 지웁니다. Vulkan 프로파일의 `overlayStrips`와 `overlayPixels`에 추가로 그린 띠와 픽셀 수를 기록합니다.

2026-11-19 Vulkan은 같은 큐에서 텍스처만 업로드했으면 바로 그리기 명령을 제출합니다. `upSched(... frameWaitUs=...)`는 제출 직전 CPU가 업로드 완료를 기다린 시간입니다. 스테이징 메모리와 업로드 명령 버퍼는 업로드가 끝난 뒤 재사용합니다.

2026-11-19 갤러리 SVG 포인터 벤치마크에서 탭 이름 뒤에 `win32 highlight`를 붙이면 테스트 창에 보낸 마우스 메시지를 실제로 받았는지 확인하고, 그 입력에 해당하는 Vulkan 프레임을 제출할 때까지 걸린 시간을 기록합니다. [측정 범위와 실행 명령](optimization-session-2026-11-19.md#win32-pointer-message-to-vulkan-presentation).

2026-11-19 줄바꿈을 쓰는 `UiInlineText`는 스크롤바 공간을 빼기 전과 후의 배치를 각각 재사용합니다. 원문이나 글꼴 설정이 바뀌면 해당 배치를 새로 계산합니다. [측정 결과](optimization-session-2026-11-19.md#reusing-the-two-widths-of-a-wrapped-description).

2026-11-19 코드가 화면을 채우는 상태를 재는 벤치마크는 갤러리 Markdown 예제를 사용합니다. 보이는 줄 수와 소스 해시, 코드가 차지하는 높이를 기록합니다. 일반 벤치마크는 Breadcrumb 예제를 사용합니다. [측정 범위와 실행 명령](optimization-session-2026-11-19.md#code-dominated-gallery-viewport).

초록 테두리는 실제로 다시 그린 범위를 표시합니다. 오래된 스왑체인 이미지와 MSAA 표면을 복구한 영역도 포함합니다. 코드 뷰는 원문과 배치가 그대로면 접근성용 글자 좌표를 재사용하고, 화면에 보이는 줄에 해당하는 글리프만 그립니다.

2026-11-13 [Popover 열기 방식](fluent2-popover.ko.md): 클릭·호버·우클릭 단일 모드, 500ms 닫기 예약/취소·거부 시 대기, 가상 점 위치와 Menu/Shift+F10을 연결했습니다. 관찰 샘플 → Popover → 팝업 열기 방식.


2026-11-13 [Popover 화살표와 표면 캐시](fluent2-popover.ko.md): WithArrow·6/8px 형상·합성 그림자·정수 이동/배율 캐시·포인터 가림을 연결했습니다. Tooltip의 기존 래스터를 공유하며 전체 적합성은 미완료입니다.


2026-11-13 [Popover](fluent2-popover.ko.md): 임의 콘텐츠 소유·제어된 열기/거부·비모달/포커스 제한·Escape 복귀·자연 높이/스크롤·중첩·숨김/해제를 연결했습니다. 관찰 샘플 → Popover. 팩터리 56개·상위 32/55 연결·23개 미연결이며 전체 적합성은 미완료입니다.


2026-11-13 [팝업 입력 기반](popup-input-modes.ko.md): BeginPopup의 비모달 모드를 추가하고 포인터 겹침·키보드 격리·Card 포커스 그룹의 층을 분리했습니다. 관찰 샘플 → 팝업 입력. 소유 콘텐츠는 위의 Fluent Popover API를 사용합니다.

2026-11-13 [Toolbar 도구 모음](fluent2-toolbar.ko.md): 기존 자식 컨트롤·가로/세로·RTL·단일 Tab 진입·순환 방향키·실제 UIA 방향을 연결합니다. 관찰 샘플 → 도구 모음. 중첩 Toolbar와 전체 Tabster 적합성은 남아 있습니다.

2026-11-13 [SearchBox 검색 입력](fluent2-searchbox.ko.md): 기존 편집·IME와 검색/지우기·포커스 복귀·읽기 전용·권위 바인딩 회신·네이티브 검색 의미를 연결합니다. 관찰 샘플 → 검색 입력. 아이콘은 Fluent 폰트입니다.

2026-11-13 Persona는 여섯 크기·논리적 텍스트 위치/정렬·소유 Avatar/상태와 네 정보 슬롯을 연결합니다. 소비자별 아바타 시각 자원은 Tag 미디어와 공유합니다. 관찰 샘플 → 인물 정보. [API](fluent2-persona.ko.md).

2026-11-13 Tag/InteractionTag의 `UiTagContent.Media`에 `UiTagMedia.Avatar`/`Image`를 연결합니다. TagGroup은 항목별 한 번 실행하는 미디어 선택자를 지원하며 태그마다 합성·그림자 자원을 소유합니다. 관찰 샘플 → 선택 목록 → 담당자 미디어. [사용법](fluent2-tag.ko.md#빌린-아바타이미지-미디어).

2026-11-13 TagGroup의 선택적 `emphasized : UiBinding<string[]>`는 보이는 `selection`과 독립적입니다. 본문 Toggle은 강조, ×는 표시만 변경하며 숨긴 강조는 재추가 시 복원합니다. activated 콜백 없이도 사용할 수 있습니다. 관찰 샘플의 선택 태그에서 두 바인딩을 확인하세요. [API](fluent2-interaction-tag.ko.md#그룹의-독립-강조-값).

2026-11-13 InteractionTag의 `Selected : UiBinding<bool>`과 시각 전용 `UiTagOptions.Selected`를 지원합니다. getter 재확인으로 같은 프레임의 그리기·UIA Toggle을 반영하며 × Invoke는 강조를 바꾸지 않습니다. 관찰 샘플 → 선택 목록 → 강조 선택에서 변경 거부도 확인합니다. [InteractionTag](fluent2-interaction-tag.ko.md) 참고.
2026-11-13 InteractionTag: 본문 동작과 × 해제, 그룹 내 두 동작 탐색과 공유 선택을 지원합니다. **선택 목록 / 확인형 태그**에서 기본 태그와 비교하세요. [API](fluent2-interaction-tag.ko.md).

2026-11-13 태그 접근성: 그룹 재진입 포커스 기억·마지막 태그 해제 후 바깥 이동·문자열 ## 보존·Windows Toolbar 역할을 지원합니다. [사용법](fluent2-tag.ko.md).

2026-11-13 Tag: 선택 해제·공유 선택 바인딩·줄바꿈·키보드 탐색·SVG를 연결했습니다. `./run-gallery.ps1` → **선택 목록 / 선택한 기능 태그**. [API와 남은 범위](fluent2-tag.ko.md).

2026-11-13 선택 목록 검증: 독립 Invalid 옵션이 ComboBox·편집기의 네이티브 검증 상태에 반영됩니다. **선택 목록 / 검증 상태 전환**에서 확인하세요. 컨텍스트 이동·해제·부모 할당 회귀 근거는 `artifacts/combobox-lifetime-2026-11-13/`입니다.

2026-11-13 Combobox 후속: 닫기·포커스 이탈 시 초안 복원, 외부 Open 바인딩의 수락·거부, 실제 OpenChanged 알림, 결과 없음 정적 텍스트를 지원합니다. [API](fluent2-combobox.ko.md). 샘플 **선택 목록 / 도시 목록 열림 전환**에서 확인하세요.

2026-11-13 Combobox: 기존 입력·IME·되돌리기와 필터 팝업을 연결하고 가려진 선택·공급자 ID를 유지합니다. 단일·다중·자유 입력을 `./run-gallery.ps1` → **선택 목록 / 도시 검색**에서 확인하세요. [API와 한계](fluent2-combobox.ko.md).

2026-11-13 Dropdown: 단일·다중 선택 바인딩, 논리 항목, 네이티브 Selection·Value, 이름 검색과 공통 팝업 스크롤을 제공합니다. `./run-gallery.ps1` → **선택 목록**. [API와 한계](fluent2-dropdown.ko.md). 편집 가능한 입력은 [Combobox](fluent2-combobox.ko.md)를 사용하며 전체 적합성 검증은 남아 있습니다.

2026-11-13 AvatarGroup: 펼침·겹침·원 분할, 정확한 초과 수·구성원 메뉴, RTL과 이미지·한글·SVG를 기존 아바타·클립·메뉴로 제공합니다. `./run-gallery.ps1` → **아바타 그룹**. [API와 한계](fluent2-avatar-group.ko.md). 팩터리48개·상위25/55 연결·30개 미연결이며 전체 적합성은 미완료입니다.

2026-11-12 경로: `Fluent.Breadcrumb`는 반응형 접힘 메뉴, Tab/방향키·RTL, 비활성 포커스, 한글 Tooltip과 네이티브 탐색 랜드마크를 제공합니다. `./run-gallery.ps1` → **경로**. 기본 아이콘은 Fluent 폰트이며 SVG 아이콘도 별도로 전달할 수 있습니다. [API와 한계](fluent2-breadcrumb.ko.md). 검증: `artifacts/breadcrumb-2026-11-12/verification.json`. 노출47개·상위24/55 연결은 전체 적합성 완료가 아닙니다.

2026-11-12 Card 선택: `Fluent.Card(..., selection: binding)`·Selection으로 실제 모델을 연결합니다. 체크박스 Tab/Enter/Space·Toggle 이벤트, 자식 동작 분리·비활성·제거, 외부 값의 부모 재배치와 캐시 재사용을 검증했습니다. 관찰 샘플 카드 → 카드 선택 모드 전환. CPU55/8485,GPU12/2433,NativeCard20/Avatar23. 전용 슬롯·전체 적합성은 미완료입니다. [API](fluent2-card.ko.md); `artifacts/card-selection-2026-11-12/verification.json`.

2026-11-12 아바타 교정: 정세일→정 자동 이니셜(분해 한글 조합 포함), 한글 프리셋의 고정 글자 제거, 폰트 비트맵 OriginX 보존으로 상태 원 오른쪽 잘림 교정. 일반 텍스트/벡터 배치·부모 클립은 유지합니다. CPU55/8402,GPU12/2289,외부Avatar23·Card14. Card 선택 변경은 보존 중이며 아직 적합성 미검증입니다. [설명](fluent2-avatar.ko.md); `artifacts/avatar-korean-presence-2026-11-12/verification.json`.

2026-11-12 Card 루트 동작: `Fluent.Card(..., activated: callback)`·`Activated`와 자동 포커스(null: 콜백 유무에 따라 NoTab/Off), 실제 Group Invoke, 자식 동작 분리·포인터 취소, 공식 hover/pressed 색과 Shadow8 예약을 제공합니다. `IUiCardPresentation.Paint`는 이제 interaction을 받습니다. 관찰 샘플 카드 → 카드 루트 동작 전환. [API](fluent2-card.ko.md); `artifacts/card-action-2026-11-12/verification.json`.

2026-11-12 Card 포커스: `UiCardOptions.FocusMode` Off/NoTab/TabExit/TabOnly와 루트 전용 `Disabled`, 실제 Tab/Enter/Escape·자식 입력·UIA 포커스와 공통 GPU 윤곽선을 제공합니다. `./run-gallery.ps1` → 카드에서 전환합니다. 전체 적합성·선택·전용 슬롯은 미완료입니다. [API](fluent2-card.ko.md); `artifacts/card-focus-2026-11-12/verification.json`.

2026-11-12 Card 기반: `Fluent.Card(label, content, options)`가 지속되는 콘텐츠 트리를 소유하고 공통 박스 측정·analytic 표면·그림자 임대·정적 네이티브 Group 의미를 재사용합니다. 관찰 샘플 **카드**에서 실제 입력·버튼과 기본 크기·외관12개 조합을 확인합니다. CPU55개/8309회·GPU1557회·네이티브 Card9+Avatar23회와 기존 장면883장 일치를 확인했습니다. 카탈로그43개 노출/상위23개 연결/32개 미연결이며 Card 선택·포커스 그룹·전용 슬롯은 구현할 범위로 유지합니다. [API와 한계](fluent2-card.ko.md); `artifacts/fluent-card-2026-11-12/verification.json`.

2026-11-12 비활성 Avatar: `UiAvatarOptions.IsInactive`가 소유한 최종 해상도 페인트 그룹 하나에0.875배 축소/불투명도0.8을 적용합니다. 활동 링·그림자를 숨기고 단일 정적 Image 제공자를 유지합니다. `UiMenuIcon.WithContentVersion`으로 외부 그리기 버전을 명시하며 SVG 문서 버전은 자동으로 확인합니다. 사용자 `IUiAvatarPresentation.CanRetainPaint`의 기본값은 false입니다. `./run-gallery.ps1` → 아바타 → 비활성 전환에서 확인합니다. CPU54개/8283회·GPU1374회·네이티브23회와 기존 장면635개 픽셀 일치이며 원래 RC3/성능 완료를 주장하지 않습니다. [계약](fluent2-avatar.ko.md), `artifacts/inactive-avatar-2026-11-12/verification.json`.

2026-11-12 최종 해상도 페인트 캡처: `PaintGroup(..., rasterScale: .875f, textMode: UiPaintGroupTextMode.Projected)`로 변환된 글자를 목적지 해상도에 맞춰 만듭니다. 기존 프래그먼트 특수화와 명시적인 로컬 클립 초기화를 사용하며 SPIR-V 파일은 바꾸지 않았습니다. CPU54개/8269회, GPU1128회와 기존 장면635개 이미지의 픽셀 일치를 확인했습니다. 일반 렌더링의 쌍별 측정은 혼재하며 읽기 반환을 포함한 경과 시간이 느려진 회차도 있습니다. 성능 향상이나 원래 성능 기준 통과를 주장하지 않습니다. 비활성 Avatar 연결은 다음 작업입니다. 근거: `artifacts/projected-paint-2026-11-12/verification.json`; [계약](backdrop-composition.ko.md).

2026-11-12 순수 그리기의 런타임 캡처 : `UiPaintGroup`는 실제 검정·흰색 GPU 대상에 그리기를 캡처하고 내용·텍스처 의존성 버전으로 캐시합니다. 중첩 그룹과 대기 스냅샷의 독립 소유권을 지원합니다. CPU54개/8267회, 원래 RadioGroup 부모 할당264회·실제 Observer 표시 픽셀·일반 제출과 지연 해제를 포함한 GPU1119회가 통과했습니다. 입력·레이아웃 하위 트리 캡처와 변형된 LCD·비활성 Avatar는 남아 있습니다. 근거 : `artifacts/paint-group-2026-11-12/verification.json`; [계약](backdrop-composition.ko.md).

2026-11-12 배경 독립 합성 기반 검증 : CPU53개/8254회, 실제 compiled 경로·Observer 표시 픽셀을 포함한 GPU832회가 통과했습니다. 별도 Observer Release 빌드의 경고·오류는0개입니다. 근거 : `artifacts/backdrop-composite-2026-11-12/verification.json`. 런타임 그룹 자동 캡처는 아직 남아 있습니다.

2026-11-12 실험 단계의 배경 독립 그룹 합성을 검증 중입니다. `UiBackdropImage`는 검정·흰색 배경의 불변 참조 결과를 소유하고, `UiImmediateContext.BackdropImage` / `UiControlContext.BackdropImage`와 `UiDrawListBuilder.AddBackdropComposite`는 RGB별 커버리지를 알파와 분리해 보존합니다. 현재는 준비된 이미지 합성이며 위젯 그룹의 자동 캡처는 아닙니다. 두 새 셰이더는 격리 검증 후 승격했으며 기존 SPIR-V는 보존합니다. 오프스크린 캡처·비활성 Avatar 크기/그룹 불투명도·전체 회귀·성능 검증은 남아 있습니다. [현재 계약](backdrop-composition.ko.md).

2026-11-12 [Avatar 기본 기능 검증](fluent2-avatar.ko.md): 생성된 Fluent.Avatar가14개 크기·원형/사각·Cover 이미지·캐시된 이니셜·이름색30종·빌린 아이콘을 지원합니다. 정적 UIA Image와 커밋된 Name 이벤트, 관찰 샘플 아바타 탭을 제공합니다. 현재CPU49개/8021회(Avatar110,관찰175),AvatarGPU100·관찰3·기존둥근이미지40·네이티브Avatar10+기존56개 통과. 카탈로그41개 노출/55개 요구/33개 미노출이며 전체 동등성은 미완료입니다. 활동·상태·그림자·모션·강제색·전체RTL·원래 검증은 남아 있습니다. 테스트 글꼴을 앱의 맑은 고딕 등록과 맞추고 정상 한글과 이전 빈 상자를 GPU로 대조합니다. 근거: artifacts/fluent-avatar-2026-11-12/verification.json.


2026-11-12 이미지·SVG의 둥근 영역은32바이트 레코드 두 개를 사용하는 공통 UV 변환 GPU 경로로 처리합니다. Image Fit은 늘이기·전체 맞춤·채우기이며 반경 변경은 원본 텍스처를 재사용합니다. 관찰 샘플 SVG 탭에서1px 모서리 조절과 맞춤 버튼을 사용하세요. [이미지 계약](image-widgets.ko.md), artifacts/rounded-image-2026-11-12/ 참고. 원래 NativeAOT·성능·전체 Fluent 검증은 남아 있습니다.

2026-11-12 [Accordion 접기·펼치기](fluent2-accordion.ko.md): 본문 독점 소유·불변 제어 값·단일/다중/마지막 항목 접기 옵션·4개 크기·아이콘·SetHeader·앞/뒤 화살표를 연결했습니다. Tab 순서·UIA 없는 포커스 복귀·부모 클리핑·외부 ExpandCollapse/관계/이벤트와 관찰 샘플 접기·펼치기 탭을 확인했습니다. CPU47개 계약·7876회 검사(Accordion27·Observer164), 네이티브56개·GPU32개가 통과했습니다. 카탈로그40개 노출/상위55개/미연결34개이며 ScopeComplete=false입니다. 근거: `artifacts/fluent-accordion-2026-11-12/verification.json`. 모션·RTL·강제 색상·공식 픽셀 동등성·전체 Fluent·원래 네이티브/성능 검증은 남아 있습니다. 아래 이전 수치는 당시 기록입니다.

2026-11-12 [Label 관계 검증](label-widgets.ko.md): 라벨이 포커스 없는 실제 UIA Text 공급자를 게시하고 따로 렌더링한 주 대상과 캡처 종료 때 LabeledBy로 연결합니다. 두 렌더 순서·생략/연결 해제/해제·캡처 중단 복구와 외부 공급자 조회·관계 제거 이벤트를 확인했습니다. CPU46/46개 계약·7844회 검사(Label40·Observer159), 네이티브 Label16/Radio12/Toggle15, GPU Label28개 검사와 기존 이미지33개 동일성이 통과했습니다. 근거: `artifacts/label-relationships-2026-11-12/verification.json`. 아래 이전 Label LabeledBy 제한은 당시 기록이며 강제 색상·전체 적합성·고부하·NativeAOT/런타임 이미지·원래 전체 목표 검증은 남아 있습니다.

2026-11-12 [네이티브 접근성 검증](widget-contract-validation.ko.md): 일반 컨트롤 이름·Field 도움말 변경 이벤트를 게시 이후 전달하도록 공통 수집을 수정했습니다. 숨은 Tooltip·동일 프레임 계약은 유지합니다. 현재 관리 코드 계약46/46개·7827회 검사(Label24), 별도 소유 Win32/UIA Label13·Radio12·Toggle15개 검사가 통과했습니다. `scripts/Test-NativeAccessibility.ps1`은 명시한 바이너리와 새 출력만 사용하고 자동 빌드·SDK 전환 없이 실행을 관리합니다. 입력 누락·기존 출력·잘못된 런타임 거부를 확인했습니다. 근거: `artifacts/label-native-2026-11-12/verification.json`. Label LabeledBy·강제 시간 초과/중단·고정 RC3 NativeAOT·GPU·전체 적합성은 별도입니다. 아래 이전 수치는 당시 검증 기록입니다.

리사이즈 보류 중 기존 위젯·GPU 묶음은 [위젯 검증](widget-contract-validation.ko.md)의 명시적 `widgets-no-resize` 모드로 실행합니다. 결과에는 제외된 창·리사이즈 검사 3개 그룹을 기록합니다. 전체 그래픽·Fluent 수락으로 보고하거나 전용 GPU·네이티브·성능 게이트를 대체하지 마세요.

2026-11-12 [위젯 검증](widget-contract-validation.ko.md): 현재 관리 계약46/46개·7822회 검사 실행(중복 포함)이 통과했고 Image23개·기존 CPU704개·Control62개를 포함합니다. Image/SVG GPU74개와Observer7개는 별도 검증했습니다. 새 widgets-no-resize 기존 GPU 묶음은 프레임 자원2개·3개에서 각각4642개 검사가 통과했고4038개 PPM이 같습니다. 창·리사이즈3개 그룹을 명시적으로 제외합니다. 현재 팩터리 목록은 공개 이름39개·필수 상위55개·미연결35개이며 전체 적합성은 미완료입니다. SDK11.0.100/runtime11.0.0 관리 검증은 고정 RC3를 바꾸지 않습니다. 외부 네이티브/UIA·생성기 제거 캐시·고부하/장시간 증가·NativeAOT/런타임 이미지·리사이즈·전체 Fluent/SVG/이식 게이트는 별도입니다. 근거: artifacts/image-widget-2026-11-12/verification.json 및artifacts/widget-core-gpu-2026-11-12/verification.json.

2026-11-12 [검증된 소유 자식 크기](widget-measurement.ko.md): 자연 크기 Field와 Tooltip이 기존 UiRetainedLayout으로 소유자별 자식 크기 하나를 공유합니다. 명시적 제약과 기존 텍스트·리소스 키를 사용하며 최초 측정·소유권/폐기·불투명 자식·글리프 관찰·동일 프레임 무효화를 검증합니다. 기존 즉시 모드 AutoField는 캐시하지 않고 컨테이너 준비 기하는 별도로 유지합니다. 집중16·위임18·폼17·Field64·컨테이너179·다중 줄/Tooltip4114·관찰158 및 GPU소유28·Field1018·위임28·컨테이너263·관찰50이 통과했습니다. 이전 Field 이미지1425개와 관찰 본문72개가 동일합니다. 관찰 화면에서 실제 소유 자식 측정 수를 확인합니다. 선택적 캐시 참조는 Field/Tooltip에만 추가하며 기본 UiWidget 저장소나 전체 앱 시간·메모리·NativeAOT 완료로 확대하지 않습니다. 근거: artifacts/owned-child-sizing-2026-11-12/verification.json.

2026-11-12 [소유 측정 위임](widget-measurement.ko.md): Textarea 표현·크기·외관의 기본 측정 약속은 false이고 내장 정책은 기존 부모 재사용에 참여합니다. protected CanRetainOwnedMeasurement가 Tooltip·Field·명시적 사용자 래퍼의 살아 있는 소유권을 검증하며 임의 조합의 기본값은 false를 유지합니다. 편집·되돌리기·Tooltip 예약·표시·Escape와 기존 렌더링 대상 크기 확인은 계속 실행합니다. 집중18·다중 줄/Tooltip4114·Field64·폼17·컨테이너179·관찰157 및 GPU대조28·다중 줄2560·관찰50이 통과했습니다. 기존 본문72개가 동일하며 관찰 화면에 여러 줄 편집과 위임 Tooltip을 추가했습니다. 새 캐시·필드나 전체 네이티브·성능 완료를 뜻하지 않습니다. 근거: artifacts/delegated-layout-retention-2026-11-12/verification.json.

2026-11-12 [TabList 자연 크기 측정](fluent2-tablist.ko.md): CanRetainMeasurement의 기본값은 false이며 내장 Fluent는 부모 제약과 독립적인 자연 크기를 최대256개 유지합니다. 선택·옵션·리소스 키와 시도 무효화 보호를 사용하고 실제 입력은 계속 처리하며 오버플로 위치는 매번 다시 계산합니다. 폭 예약 일반 탭은 조상 측정에도 참여합니다. 집중17·탭197·오버플로24·관찰152·폼17·복합28·컨테이너179 및 GPU대조28·기존 탭800·오버플로48·관찰50이 통과했습니다. 이전 탭 이미지793개와 다른 관찰 본문72개가 동일합니다. 관찰 화면에 실제 탭·프로세스 단위 진단 측정 수·크기 변경 버튼을 제공합니다. 전체 성능·NativeAOT·수명 게이트는 남아 있습니다. 근거: artifacts/tab-measurement-retention-2026-11-12/verification.json.

2026-11-12 [폼 측정 재사용](widget-cpu-retention.ko.md): 내장 Input 크기·외관·장식, 정적 Divider·소스, Menu 트리거가 기존 부모 측정 재사용에 참여합니다. Field는 표현과 소유한 자식의 동의를 모두 요구합니다. 명시적 Input 슬롯의 실제 자식 렌더링과 자연 크기 Field의 자식 측정은 유지합니다. 집중17·Input202·Divider64·Field64·Menu376·컨테이너179·복합28·스케줄35·관찰149가 통과했습니다. 새 GPU대조28·기존 Input212·Divider525·관찰50 및 기존 페이지 본문72개 동일성을 확인했습니다. 측정 재사용 탭에 실제 편집·되돌리기·메뉴 체크와 별도 폼 부모 측정 수를 표시합니다. 새 리프 캐시·필드나 전체 성능·AOT 완료를 뜻하지 않습니다. 근거: artifacts/form-layout-retention-2026-11-12/verification.json.

2026-11-12 복합 측정 정확성: Field 크기·방향 특성은 기본적으로 측정을 유지하지 않으며 내장 특성은 기존 제한 캐시를 명시적으로 유지합니다. TabList는 보통 굵기 폭이 더 넓어도 선택 여부와 관계없이 두 글꼴의 최대 폭을 예약합니다. 수정 전26개 실패 재현 후 집중28·Field64·TabList197·관찰145가 통과했습니다. 기존 GPU TabList800·Field1018·관찰50 및 본문80개 동일성을 확인했습니다. 새 TabList 자연 크기 캐시나 전체 목표 완료를 뜻하지 않습니다. 근거: artifacts/composite-layout-retention-2026-11-12/verification.json.

2026-11-12 [콘텐츠 측정 재사용](widget-cpu-retention.ko.md): Text는 불변 소스와 결정론적 표현이 모두 동의해야 하며 글꼴·메트릭·Badge 동작/표현/아이콘 특성은 기본 false입니다. 기본 문자열·CounterBadge는 동의하고 getter Text/Badge는 계속 동적으로 측정합니다. 명시적 크기의 getter Progress는 선언된 기하를 유지하면서 렌더마다 실제 값을 한 번 읽습니다. Link는 기존 ReuseLayout을 전달합니다. Spinner는 표시 상태가 안정된 경우에만 재사용하며 지연 표시·애니메이션·정지를 유지합니다. 리프 캐시·필드는 추가하지 않았습니다. 집중26개·text47/badge73/link61/progress60/spinner186개·컨테이너179개·관찰145개·생성기115개·콘텐츠GPU28/기존리프GPU28/관찰GPU50개가 통과했습니다. 이전 본문73개는 같고 확장한 측정 페이지는 의도적으로 바뀌었습니다. 측정 재사용 탭에서8종과 실제 Link 포인터 콜백을 확인합니다. 전체 성능·NativeAOT·Link 의미 접근성은 별도입니다. 근거: artifacts/content-layout-retention-2026-11-12/verification.json.

2026-11-12 [리프 배치 재사용](widget-cpu-retention.ko.md): 정적 `IUiTogglePresentation.CanRetainMeasurement`·`IUiSliderPresentation.CanRetainMeasurement`로 알려진 Fluent 컨트롤을 기존 부모 캐시에 연결했습니다. 사용자 Checkbox/Switch 크기·라벨 특성과 `IUiRadioGroupLayout`은 기본 false이며 기본 제공 특성은 동의합니다. RadioGroup은 `ReuseLayout`을 전달하고 제네릭 Fluent 표현도 가변 사용자 배치를 고정하지 않습니다. 리프 캐시·필드는 추가하지 않으며 매 프레임 그리기·입력을 유지합니다. 집중14개·컨테이너179개·Radio91개·관찰142개·생성기115개·GPU28개·관찰GPU50개가 통과했고 이전 본문73개가 일치합니다. 관리 AB/BA/AB 진단의 반복 자식 측정768000회가0회로 줄고 할당67584바이트는 같았습니다. NativeAOT·전체 앱 성능 개선은 주장하지 않습니다. 관찰 샘플에 측정 재사용 탭을 추가했습니다. 근거: artifacts/leaf-layout-retention-2026-11-11/verification.json.

2026-11-11 [RadioGroup 접근성](fluent2-radio-group.ko.md): 실제 그룹·RadioButton 역할을 Selection·SelectionItem·호출자 바인딩·설명·이동 포커스에 연결했습니다. 현재 활성·클립·팝업 상태를 다시 검사하며 값 기반 ID는 순서 변경에도 유지합니다. 제거 후 getter와 동작은 정확한0x80040201을 반환합니다. 집중18개·기존 입력과 할당91개·Observer137개·외부9개/호스트3개·ObserverGPU47개가 통과했고 기존 본문73개가 일치합니다. 부모 할당과 일반 그리기를 유지했습니다. 전체 발화·성능·정확한 RC3 NativeAOT는 남아 있습니다. 근거: artifacts/radio-accessibility-2026-11-11/verification.json.

2026-11-11 [공통 Toggle 접근성](widget-accessibility.ko.md): Checkbox/Switch/ToggleButton 역할과 꺼짐/켜짐/혼합 상태를 단일 ToggleState·기존 동작/바인딩 정책·Windows Toggle 속성 알림에 연결했습니다. Checked는 두 상태 표현으로 유지합니다. Core21/관찰134/생성기115개·외부 Toggle11개/호스트4개·체크 메뉴6개/호스트3개가 통과했고 GPU47개·본문73개 일치로 그리기를 보존했습니다. 앞서 그린 공유 형제는 요청된 다음 프레임에 반영합니다. 전체 발화/강제 색상/NativeAOT는 남아 있습니다. 근거: artifacts/toggle-accessibility-2026-11-11/verification.json.

2026-11-11 [탭 넘침](fluent2-tab-overflow.ko.md): 선택 우선 헤더 배치·생성 점 세 개 Tab 동작·공유 Fluent 팝업을 옵션으로 연결했습니다. 메뉴 선택은 같은 프레임의 원래 헤더/본문에 반영하며 단일 Tab 정지점과 실제 본문 관계를 유지합니다. CPU20/GPU48·외부 클라이언트7/호스트2·관찰 샘플70. 컨테이너 -> 탭 넘침과 본문에서 폭/RTL·본문 카운터 유지를 확인합니다. 전체 변형/이벤트/애니메이션/메뉴 아이콘은 남아 있습니다.

2026-11-11 [Fluent Menu](fluent2-menu.ko.md): 생성 팩터리·공유 팝업·고정 밝음/어두움 색상·폭 138/300·행 32/간격 2·Regular Type300·RTL 글자. CPU57/GPU96·표현별 외부 클라이언트4/호스트2·관찰 샘플53. 노출36=상위17+변형19, 미연결38/55. 정확한 shadow16·전체 일치·탭 넘침 연결은 남아 있습니다.

2026-11-11 [공통 메뉴 동작](menu-foundation.ko.md): Dux.Menu와 UiMenu<TPresentation>가 실제 팝업·비활성 항목을 건너뛰는 키보드 포커스·닫기·Menu/MenuItem Invoke를 제공합니다. 기반 검증 CPU21·GPU20·외부 클라이언트4/호스트2·관찰 샘플51. 컨테이너 -> Menu 공통 동작에서 확인합니다. Fluent 외관·중첩/체크/아이콘/스크롤 메뉴·탭 넘침 연결은 남아 있습니다.

2026-11-11 [우선순위 넘침 배치](overflow-layout.ko.md): 수평·수직 기반 위젯이 숨긴 항목의 모델을 유지하고 현재 입력·UIA 게시에서 제외하며 필수 항목 클리핑을 보고합니다. CPU26·GPU66·외부 클라이언트4/호스트3·관찰 샘플49 통과. 컨테이너 -> Overflow 공통 배치에서 폭·우선순위·RTL을 바꿉니다. Menu와 TabList 넘침 연결은 남아 있습니다.

2026-11-11 [완료 산출물 정리](rejected-build-cleanup.ko.md): 직접 삭제가 정책상 거부되어 검증된22개 출력 폴더·333개 파일을 `artifacts/must_delete_20261111_161030`으로 이동했습니다. 실제 삭제0개이며 원래 경로 부재·이동 후 해시를 확인했습니다. 이동은 공간 회수가 아닙니다. 근거: `artifacts/completed-cleanup-2026-11-11/verification.json`.

2026-11-11 [Fluent 탭 콘텐츠](fluent2-tab-content.ko.md): 윤곽형·채움형 아이콘, 접근성 이름을 유지하는 아이콘 전용, SubtleCircular·FilledCircular가 기존 선택·포커스·UIA 경로를 재사용합니다. 코어197·GPU800·네이티브 클라이언트25/호스트3·관찰 CPU44/GPU40 검사가 통과했습니다. 관찰 창의 Fluent 탭에서 탭 표현과 아이콘까지 스크롤합니다. 공식 전체 일치·NativeAOT는 남아 있습니다.

2026-11-11 [접근성 포커스 게시](accessibility-focus.ko.md): 프레임의 최종 논리 포커스와 실제 HWND 포커스를 구분합니다. 부모가 자식의 포커스를 함께 주장하지 않으며 창 복귀 때 현재 자식의 포커스를 복원해 알립니다. 코어111·소유 외부 클라이언트11/호스트3 검사가 통과했고 프레임 타이머를 추가하지 않았습니다. 근거: `artifacts/focus-events-2026-11-11/verification.json`. 화면 읽기 음성·전체 데스크톱/Fluent·NativeAOT는 남아 있습니다.

2026-11-11 [접근성 관계 변경](accessibility-relationships.ko.md): 유지된 원본의 LabeledBy/ControllerFor 변경을 게시 후 알립니다. 이전 대상의 불변 조상은 제거 뒤에도 유지하고 새 네이티브 대상의 실제 실행 패턴을 보존합니다. 없는 대상은 게시 전에 거부하며 값 유지와 원본 제거에는 이벤트를 만들지 않습니다. 코어17·소유 네이티브 클라이언트12/호스트4 검사가 통과했고 실제 탭·본문은 별도 통합 검사로 확인합니다. 근거: `artifacts/relationship-events-2026-11-11/verification.json`. 포커스 이벤트는 위 범위로 검증했고 화면 읽기 음성·전체 Fluent/NativeAOT 게이트는 남아 있습니다.

2026-11-11 [탭 본문](fluent2-tablist.ko.md): `tabs.RenderPanel(ui, key, 위젯또는캐시한콜백)`이 이번 헤더가 선택한 본문만 실행하며 호출자가 소유한 콘텐츠를 빌려 사용합니다. Tab·Shift+Tab과 UIA 포커스를 공통 처리하고 Windows ControllerFor/LabeledBy로 실제 탭과 Pane/tabpanel을 연결합니다. 관찰 샘플 열 페이지가 사용합니다. CPU77(본문25)·관찰CPU39/GPU37·외부클라이언트21/호스트3 통과, 기존 본문58개가 상단128픽셀 아래에서 일치합니다. 최종 .NET11 빌드 세 개는 경고·오류0입니다. 근거: `artifacts/tab-panel-2026-11-11/verification.json`. 관계 변경 이벤트의 후속 검증은 위에 기록했으며 화면 읽기 음성·Fluent 외관·NativeAOT는 남아 있습니다.

2026-11-11 Immediate 회귀 검사: `observer-gallery`에서 실제 포인터 누름·해제로 Hello → Immediate를 전환하고, 이벤트 대기 중 다음 프레임 예약 및 선택 첫 GPU 본문과 후속 네 프레임의 일치를 확인합니다. 내장 메뉴·중첩 탭 검사를 보강하며, 구체적으로 확인하지 않은 데스크톱 증상까지 검증한 것은 아닙니다.

2026-11-11 [탭 선택 이벤트](fluent2-tablist.ko.md): 트리 게시 후 Windows IsSelected 속성 변경·ElementSelected·명시적 미선택·남은 부모의 무효화를 알립니다. 최초 검증은 CPU52(기본27+COM12+게시13), 외부 UIA 클라이언트8/호스트3개 통과이며 두 소유 프로세스가 exit0으로 종료했습니다. 초기·변화 없음·재정렬에는 이벤트를 만들지 않습니다. 근거: `artifacts/tab-selection-events-2026-11-11/verification.json`. 이후 위 본문 검증에서 실제 관계를 연결했으며 전체 외관·NativeAOT는 남아 있습니다.

2026-11-11 [Fluent TabList](fluent2-tablist.ko.md): `Fluent.TabList`·상속 가능한 `UiFluentTabList`가 선택 바인딩과 방향키 포커스를 분리한다. 세 크기·가로/세로·RTL·Transparent/Subtle·너비 예약, Enter/Space·Home/End·Escape, 비활성 건너뛰기와 키 기반 재정렬을 제공한다. Windows Tab/TabItem과 실제 Selection/SelectionItem COM 패턴을 연결했다. 최초 검증은 CPU39(기본27+COM12)·GPU192·관찰CPU29/GPU30·기존접근성49/컨테이너179/스케줄링35 통과다. 관찰 창 상단이 실제 팩터리를 사용한다. 현재 노출35=상위16+변형19, 상위55중 미연결39이며 원형·아이콘·넘침·전체 외관·현재 NativeAOT는 미완료다. 선택 이벤트 검증은 위에 별도로 기록했다.

2026-11-11 [관찰 창 배치·가상화·렌더링](../samples/Duxel.Gallery/README.ko.md): 현재10탭에 모든 구현 컨테이너 계열, 키 기반10억 행 가변 높이 목록, 중첩 스크롤, 정적 도형 캐시와 독립3D 투영을 추가했다. CPU28은 실제 열 수 전환·행 편집·끝 이동·재정렬 후 값 유지·위젯 해제·캐시 본문 재사용과 애니메이션 예약 켜기/끄기를 검사한다. GPU30은 고정 시각에서10페이지의 획득 이미지 재사용을 검사한다. `./run-gallery.ps1`의 실제 빌드·창 표시를 확인했다. Computer Use는 실제10탭 화면을 읽었지만 사용자 조작 감지로 클릭하지 못했으므로 OS 입력 검증은 미완료다. 근거: `artifacts/observer-layout-virtual-2026-11-11/verification.json`.

2026-11-11 [구현 관찰 샘플](../samples/Duxel.Gallery/README.ko.md): `./run-gallery.ps1`로 로컬 .NET11 소스를 빌드하고 같은 창의 개요·기존 API·Fluent·타이포그래피·입력·Tooltip·관찰 범위 탭을 연다. 현재 노출34이름의 데모 탭을 등록하며 새 이름의 누락·중복 매핑은 시작 시 실패한다. 실제 버튼 콜백·탭 왕복 값 유지·입력 검증을 포함한 CPU14와 고정 애니메이션 시각에서 획득 이미지 재사용을 비교한 GPU21검사가 통과했다. V-Sync 기본값은 꺼짐, 이벤트 대기 기본값은 켜짐이며 FPS 표시는 별도 프레임을 요청하지 않는다. Fluent의 Spinner는 기본 정지이며 재생 전환으로만 애니메이션을 예약한다. `UiSpinnerOptions.Paused`는 위상과 예약을 함께 멈추고 지연 표시의 일회성 기한은 유지한다. 탭 본문 포커스 테두리는 콘텐츠 여백 안의 가시 경계에 그린다. `SampleScreen.UseEmbeddedMenuBar = true`는 Immediate 메뉴를 페이지 안에 두어 앱 전체 뷰포트 여백을 변경하지 않는다. 탭 전환은 다음 프레임을 예약하고 부유 패널은 호출자 클립·콘텐츠 폭·사용 가능 영역과 분리된다. 전체 컨테이너·가상 목록·렌더링 계층 페이지와 실제 OS 입력 검증은 남아 있다.

2026-11-11 [Fluent Tooltip 팩터리](fluent2-tooltip.ko.md): `Fluent.Tooltip`과 상속 가능한 `UiFluentTooltip`을 기존 대상·표현에 정적으로 연결했다. 노출34이름은 상위15·변형19이며 상위55중 미연결40이다. 생성기115·팩터리CPU9·추가GPU24를 포함한 Tooltip792검사가 통과했다. 전체 Fluent 준수·현재 NativeAOT 비교와는 구분한다.

2026-11-11 [할당된 컨테이너 무효화](widget-containers.ko.md): 부모가 다시 측정하지 않고 같은 크기를 할당해도 Grid·Stack·단일 자식의 옵션 변경을 다음 프레임에 반영합니다. 렌더링 도중 변경은 그 프레임의 준비된 형제 배치를 유지합니다. 명시적 측정 결과를 재사용할 때도 준비 프레임을 갱신하며 변경 없는 배치의 측정 재사용을 유지합니다. CPU170/GPU252·ScrollView75·가상 목록75·수명101·접근성49검사가 통과했고 기존 컨테이너 이미지169개가 동일합니다. 기존 flow-container FBA는 현재 로컬 .NET11 소스로 실행해 실제 첫 표시와 정상 종료를 확인했습니다. NativeAOT·전체 목표 게이트는 남아 있습니다.

2026-11-11 [Tooltip UIA Scroll](tooltip-auto-size.ko.md): 표시 중인 Tooltip의 내용이 넘치면 두 축의 측정된 위치와 표시 비율을 제공한다. 접근성 스크롤 명령은 그리기에 쓰는 오프셋을 갱신하며 넘침이 사라지거나 닫히면 패턴을 해제한다.

2026-11-11 [Tooltip 스크롤바](tooltip-auto-size.ko.md): `ShowScrollbars = true`를 설정하면 넘치는 본문 위에 Duxel 스크롤바를 겹쳐 그린다. 두 축에서 손잡이 끌기와 트랙 클릭을 지원한다. 본문 밖에서도 끌 수 있으며 표면이나 스크롤 축이 사라지면 포인터 소유권을 해제한다. 생성된 사용자 레이아웃 FBA에서 사용한다.

2026-11-11 [Tooltip 창 종료](tooltip-events.ko.md): Windows 종료 요청 뒤 프레임 생성을 멈추고 백엔드를 폐기한다. 트리는 조상 정보를 제거하기 전에 표시 중인 Tooltip 닫힘을 알린다. 불변 이벤트 원본·조상 좌표와 네이티브 창 공급자의 참조를 유지해 즉시 HWND를 폐기해도 외부 UIA 클라이언트가 마지막 닫힘을 수신한다. 일반 공급자는 사용 불가가 되고 이벤트 스냅샷은 동작을 제공하지 않는다. 제품 종료 경로는 클라이언트 응답을 기다리지 않는다.

2026-11-11 [Tooltip 텍스트 이벤트](tooltip-events.ko.md): 접근성 게시 후 열린 Tooltip Name과 실제 대상 HelpText 또는 레이블 Name 변경을 알린다. Windows는 현재 공급자로 UIA 속성 이벤트를 전달한다. 텍스트가 바뀌어도 Tooltip의 열림 상태는 유지된다.

2026-11-11 [창 가시성과 외부 UIA](tooltip-events.ko.md): 실제 Windows 세션의 최소화·복원·숨김·재표시에서 별도 클라이언트가 열림 두 번과 닫힘 두 번을 받는다. 닫힘 캐시는 논리 식별자와 화면 밖 상태를 유지한다. 해제된 표면 공급자는 사용할 수 없으며 대상 HelpText는 계속 읽을 수 있다.

2026-11-11 [Tooltip 창 가시성](tooltip-targets.ko.md): Windows 백엔드가 숨김·최소화 상태와 숨김 횟수를 세션 프레임에 전달한다. 창을 숨기면 `HostHidden` 요청을 보내고 시각 표면과 접근성 표면을 해제한다. 최소화한 창의 그리기 영역이 0이면 입력 전용 경로에서 가시성 변화를 한 번 처리하고 GPU 프레임을 제출하지 않는다. 복원은 유지된 제어 값을 따른다. 가시성을 제공하지 않는 백엔드는 기본 표시 상태를 사용한다.

2026-11-11 [Tooltip 표면 입력](tooltip-auto-size.ko.md): 겹친 Tooltip 본문은 호버와 두 휠 축에 같은 포인터 판정을 사용한다. 입력 가능한 본문 중 가장 앞쪽이 입력을 받는다. 움직일 수 없는 휠 축은 바깥 영역에 남으며, 앞쪽 본문을 외부에서 닫으면 아래쪽 본문이 즉시 입력 대상이 될 수 있다.

2026-11-11 [Tooltip 표시 제어](tooltip-targets.ko.md): `UiTooltip<TBehavior, TPresentation>.Visible`은 첫 렌더링에서 자동(`null`) 또는 호출자 제어(`bool`)를 선택한다. `VisibleChangeRequested`는 상호작용·Escape·전환 요청을 전달하며 호출자의 수락은 다음 렌더링에 반영된다. 전환을 거절하면 제어 표면 여러 개가 각자의 스크롤 위치·텍스처 수명·접근성 관계로 표시될 수 있다. 생성된 Footer 샘플에서 호출자 제어를 연습한다. Fluent 팩터리와 전체 이벤트 동등성은 남아 있다.

2026-11-11 SDK 선택 복구: 사용자 요청으로 루트 `global.json`을 설치된 SDK `11.0.100-rc.3.26522.148`에 맞추고 사전 릴리스를 허용했으며 `latestPatch` 정책은 유지했다. 편집기 BuildHost 초기화 중 미설치 `10.0.301`을 요구하던 원인을 수정한다. 공개 대상 프레임워크는 유지하며 개발 검증은 계속 .NET11, .NET9/.NET10 배포용 컴파일은 전체 최종 완료 때만 실행한다.

2026-11-11 [Tooltip 표면](tooltip-surface.ko.md): 컴파일된 기본/반전 표현이 비대칭 CSS 패딩으로 Type200을 측정하고 둥근 본문/화살표와 순차 RGBA 그림자를 그린다. 창의 텍스처 슬롯 하나가 재사용/해제를 소유한다. 독립 Chromium CSS foreignObject 래스터12개의 허용 범위 비교24회를 포함해 CPU4045/Tooltip GPU740이 통과했다. 네이티브 DOM 스크린샷·DPI/경계 픽셀 동일성·전체 Tooltip 준수는 미검증이다. 팩터리/카탈로그는 추가하지 않았다. 글꼴 마스크 경계 아래 색 ID 예약 범위는 연결 문서를 따른다.

2026-11-11 샘플 서식: `samples/fba/widget_tree_theme_fba.cs`는 문장을 나누고 대입과 콜론 양옆에 공백을 둡니다. 이름 있는 인수와 화면의 이름/값 문구에도 적용합니다. 보간 형식 지정자·URL·FBA 지시문은 보존합니다. 공백 포맷터만으로 이름 있는 인수의 콜론 앞 공백이 적용되지는 않으므로 별도로 확인합니다.

2026-11-11 [Tooltip 자동 크기/넘침](tooltip-auto-size.ko.md): 후보/화살표 상태를 유지하며 실제 콘텐츠를 재측정한다. 측정 결과는 본문/전체 콘텐츠 영역을 제공하며 휠 클립/전달/초기화를 검증했다. 추가 참조768사례와 CPU4022/Tooltip GPU620이 통과했다. 공식 표면·전체 접근성/제어 동작·현재 NativeAOT는 남아 있다.

2026-11-11 [측정 기반 Tooltip 배치](tooltip-measured-layout.ko.md): 후보별 컴파일 오프셋에 측정 크기/물리 방향을 전달하고 명시 폭을 보존하며 대상 폭으로 콘텐츠를 재측정한다. 샘플은 실제 줄바꿈을 사용한다. 추가 참조384사례와 CPU3252/Tooltip GPU524가 통과했다. 상태를 유지하는 화면 자동 크기·공식 표면/동작·현재 NativeAOT는 남아 있다.

2026-11-11 [Tooltip 오프셋/덮기 검증](tooltip-offset-cover.ko.md): 부호 있는 두 축 오프셋·논리 이동 패딩·coverTarget·원문 이동/제한 모드를 실제 소비 경로에 연결했다. 추가 참조1536사례와 CPU2863/Tooltip GPU464가 통과했다. 자동 크기/줄바꿈·공식 표면/동작·전체 접근성 알림·현재 NativeAOT는 남아 있다.

2026-11-11 [Tooltip 충돌 검증](tooltip-collision.ko.md): 고정 Floating UI core1.6.8의 실제 실행으로 사각형768사례를 검사하며 향한 변 우선 선택·RTL 중앙 우선순위·화살표 재배치가 일치한다. 불변 후보·분리 경계·화살표 폭을 실제 소비 경로에 연결했다. CPU1326/Tooltip GPU416이 통과했다. Fluent 전체 표면/동작·현재 NativeAOT는 남아 있다.

2026-11-11 [공유 Tooltip 배치](tooltip-positioning.ko.md): 12개 논리 위치·RTL·화면 경계 flip/shift·사용자 화살표 기준점을 프레임 배치/옵션으로 실제 표현에 전달한다. 생성된 샘플이 이 경로를 사용한다. Floating UI/Fluent 배치 전체·둥근 화살표/그림자·현재 NativeAOT는 남아 있다.

2026-11-11 [Tooltip 표면 이벤트](tooltip-events.ko.md): 실제 대상 자식 ToolTip 역할과 컨트롤/콘텐츠 뷰 규칙, 게시 전 닫힘·게시 후 열림을 연결했다. 제거된 원본·조상의 이벤트 스냅샷으로 비동기 UIA 캐시를 유지한다. CPU520·외부 클라이언트10/호스트3이 통과했다. 공식 표면·속성 변경/창 종료 이벤트·NativeAOT 전체 검증은 남아 있다.

2026-11-11 [Tooltip 대상 접근성](tooltip-accessibility.ko.md): UiAccessibilityNode.Tooltip에 논리 SourceId·텍스트·관계를 게시하고 Windows HelpText/Name에 연결했다. Field·장식 자식·중첩·프레임 텍스트 일치를 검증했다. CPU502/Tooltip GPU288 및 실제 별도 UIA6이 통과했다. Fluent 팩터리·공식 표면·역할/이벤트·현재 NativeAOT는 남아 있다.

2026-11-11 [Tooltip 대상 조합](tooltip-targets.ko.md): 실제 자식 소유, 기본250ms 표시·해제, 단일 창 소유권, 팝업 지연 그리기와 Escape 소비를 구현했다. CPU473/Tooltip GPU224, 기존 입력·다중 행·컨테이너 회귀와 생성된 사용자 샘플이 통과했다. Fluent.Tooltip 팩터리·공식 표면·접근성 관계·현재 Native·성능은 남아 있다.

2026-11-11 [Tooltip 참고 계약](fluent2-tooltip.ko.md): 공식 지침·고정 React 소스·밝은/어두운 실행 화면으로 표시 지연·소유권·접근성·표면 기준과 구현 공백을 기록했다. 팩터리·제품 변경은 없다. 공식 URL64개 차이 없음, 관리 노출33개/상위14개/미연결41개다. 구현과 현재 Native 검증은 남아 있다.

2026-11-11 [공통 텍스트 드래그 소유권](text-drag-ownership.ko.md): 한 줄·다중 행이 실제 누름 소유권과 시각을 공유하고 포커스만 있는 held 입력은 선택하지 않습니다. CPU444·다중행2560 통과, 컨텍스트/스크롤바 수명과 포인터 GPU 포함. 실제 OS 캡처·단어 드래그·부분 가림·Native 전체 일치는 미완료입니다.

2026-11-11 [다중 행 원문 요소](multiline-elements.ko.md): 기본 편집의 결합·보충 문자 분할과 모든 모드의 동일 프레임/대기 IME 정렬을 수정했습니다. CPU393·다중행2464 통과, 이전 입력693장·다중행3457장 일치. [Native 상태](native-toolchain-status.ko.md)는 이전 P: 도구체인을 현재 사용할 수 없고 현 위치가 미확인임을 구분합니다.

2026-11-11 [한 줄 드래그](input-drag.ko.md): 입력에서 누른 드래그는 영역 밖 선택·가로 자동 스크롤을 이어가고 해제·숨김·클립/비활성·상위 팝업 차단 시 종료합니다. CPU336·입력커널400 통과, 이전 입력357장·다중 행3457장 일치. 실제 OS 캡처와 전체 텍스트/Native 일치는 미완료입니다.

2026-11-11 [공통 키보드 명령](text-commands.ko.md): Ctrl+Backspace/Delete와 반복은 온전한 단어 범위를 삭제하고 다중 행 Left/Right는 선택을 접습니다. CPU301·입력커널304·다중행2336 통과이며 전체 네이티브/Unicode/OS 일치는 미완료입니다.

2026-11-11 [한 줄 원문 요소](single-line-elements.ko.md): 공통 이동/삭제·포인터·방향 선택·반복이 텍스트 요소를 보존하고 경계 캐시는 버퍼 수명/ASCII 경로를 따릅니다. CPU231/입력커널240/FluentInput1362/접근성40/수명101/다중행2208 통과. 이전 입력117/다중행3265 이미지 일치. 샘플: `fluent_input_elements_fba.cs`. 전체 셰이핑/bidi/OS/공식 일치·현재 Native/성능은 미완료입니다.

2026-11-11 [비감싸기 다중 행 기하](multiline-hard-lines.ko.md): 읽기 전용/Fluent 편집기가 CRLF/CR 원문 범위·명시적 행·텍스트 요소 탐색·양축 바 공간·끝 커서를 보존합니다. CPU204/GPU2208 및 이전 이미지3,025장 일치. Textarea 샘플에 Wrap 전환을 추가했습니다. 전체 텍스트/OS/공식 일치·현재 Native/성능은 미완료입니다.

2026-11-11 [텍스트 편집 무결성](text-edit-integrity.ko.md): 제한 삽입이 들어오는 텍스트 요소를 보존하고 최대 길이에서도 선택 영역을 덮어씁니다. 외부 모델 변경은 undo/redo를 새 기준점으로 초기화하며 접근성 Value는 되돌릴 수 있는 편집으로 유지합니다. 기준 실패29개 재현 후 CPU175 통과. 전체 Unicode/OS 입력·Native/성능은 미완료입니다.

2026-11-11 [Fluent Textarea](fluent2-textarea.ko.md): 생성 `Fluent.Textarea`·세 크기/표현·자동 줄바꿈·Tab/Shift+Tab·Field/UIA 포커스/값·읽기 전용/placeholder/오류 옵션·스크롤바 캡처를 연결했습니다. CPU126/접근성40/수명101/GPU2048 통과. 샘플: `fluent_textarea_fba.cs`. 카탈로그 노출 33개·빠진 최상위 이름 41개이며 전체 일치·실제 OS 입력·현재 Native/성능은 미완료입니다.

2026-11-11 [다중 행 접근성 값 설정](fluent2-textarea.ko.md): 컴파일 다중 행 노드에서 CR/LF를 허용하고 정규화·바인딩·undo와 대기 요청 재검증을 연결했습니다. CPU93/접근성40/GPU320 통과. 한 줄 검증은 유지하며 접근성 포커스·전체 Fluent Textarea는 미완료입니다.

2026-11-11 [다중 행 자동 줄바꿈](multiline-wrap.ko.md): Wrap을 켜면 원문에 줄바꿈을 넣지 않고 소유 시각 행을 사용합니다. 클릭·커서·선택·시각 이동/반복·IME 미확정 표시를 연결했고 CPU66/GPU320·기존 이미지41일치. 샘플: `layout_multiline_wrap_fba.cs`. 전체 Fluent/복합 스크립트 일치·현재 Native/성능은 미완료입니다.

2026-11-11 [컴파일 다중 행 계약](fluent2-textarea.ko.md): 공개 제네릭 동작/표현·편집기/스크롤바 소유권이 순수 부모 측정·콘텐츠/줄 상자·편집/undo·컨텍스트 이동·해제를 지원합니다. CPU37/GPU192·기존 이미지41일치. 샘플: `layout_multiline_contract_fba.cs`. 기반 기능의 근거이며 전체 Fluent Textarea 일치·현재 Native/성능은 미완료입니다.

2026-11-11 [관리 할당 검증](allocation-validation.ko.md): 버튼 동일 값 대입의 엄격한 검사는 같은 독립 루프를 0.25초 예열합니다. 기본 JIT 독립 실행 3회에서 각각 수명 101검사·0바이트 통과, 제품 변경 없음. 콜드 할당 진단을 보존하며 Native·전앱 성능은 미완료입니다.

2026-11-11 [Textarea 기반](fluent2-textarea.ko.md): 기존 다중 행의 기하·편집 경로 분리, 경계 휠 전달·유한 기하 검증은 CPU17/GPU32·전후 이미지41일치. 이 초기 근거는 생성 Textarea 팩터리 이전 단계이며 수명 검사는 후속 측정 교정 후 통과했고 전체 컴포넌트/Native/성능 검증은 미완료입니다.

2026-11-10 샘플 서식: 초기 Container/Flex/Stack·단일 자식·Wrap/Grid·입력 할당·제약 컨트롤 FBA의 대입·이름 있는 인수·블록을 읽기 쉽게 정리했습니다. 표시 문구는 `Count : 0`, `Width : 280` 형식이며 FBA 지시문과 보간 형식 지정자는 유지합니다. [샘플 검증](sample-format.ko.md) 참조.

2026-11-10 [사용자 정의 컨테이너 계약](custom-container-contract.ko.md): 외부 생성 레이아웃 공개 API 15검사 통과, 실제 루트 클립으로 사용자 표현·입력 누출을 수정했습니다. 컨테이너 GPU 회귀 237개·좌표 회귀 2,317개 통과. 샘플: `layout_custom_contract_fba.cs`. 현재 Native·성능 게이트는 미완료, 컨테이너 3/6 유지.

2026-11-19 [사용자 정의 컨테이너의 Linux 런타임 비교](custom-container-contract.ko.md#linux-런타임-비교--2026-11-19): x64 관리형과 NativeAOT에서 외부 샘플의 초기 X11 화면과 네 상태 화면이 각각 일치했습니다.

공통 팝업 래스터화 코드가 한 호출 안에서 그림자 임시 배열을 재사용합니다. Linux x64 NativeAOT 혼합 크기 검사에서는 호출 뒤 임시 배열을 계속 보관하지 않는 방식이 기존 코드보다 할당량과 프로세스 메모리 최고치를 함께 줄였습니다. [메모리 비교](optimization-session-2026-11-19.md#popup-scratch-retention-controls)

외부 .NET 11 앱에서는 `Duxel.Windows.App` 패키지 하나를 참조해 같은 컨테이너를 생성할 수 있습니다. 빌드 산출물 경로를 따로 지정해도 `Duxel.App`은 해당 빌드에서 확인한 분석기를 패키지에 넣습니다.

사용자 정의 단일 자식 레이아웃은 두 축 모두에서 부모 제약을 지키는 루트 크기를 반환해야 합니다. 범위를 벗어나면 측정과 렌더링이 결과를 캐시에 넣거나 그리기 전에 거부합니다.

2026-11-11 [키 가상 목록](virtual-list.ko.md)은 double 오프셋과 뷰포트 근처 좌표로 10억 항목을 처리합니다. `ExtentIndex`를 설정하면 모델이 제공하는 가변 높이를 사용하고 기하가 바뀔 때 가시 키와 행 내부 오프셋을 보존합니다. 오버스캔 밖 행은 기존 소유권으로 해제합니다. 행 높이 자동 측정, 다른 축, 서로 다른 키 간 재활용 정책과 현재 NativeAOT 검증이 남아 있습니다. 컨테이너 3/6을 유지합니다. 샘플은 `layout_virtual_list_fba.cs`입니다.

2026-11-11 [가상 목록 접근성](virtual-list.ko.md#windows-접근성)은 Pane에 정확한 세로 Scroll과 오버스캔 컨트롤의 ScrollItem을 제공합니다. 생성 범위가 바뀌면 자식 목록 변경을 알립니다. `IUiVirtualListAutomationSource`는 인덱스를 사용하는 `ItemContainer` 검색과 `VirtualizedItem` 생성을 추가합니다. 보관 중인 공급자는 재정렬 뒤에도 같은 키를 가리키며, 생성 요청은 소유 스크롤 조상을 이동해 해당 행을 표시할 수 있습니다.

공통 `virtual-list-pixels` 실행기는 10억 행의 소수 오프셋을 Windows와 WSLg에서 확인합니다. 같은 Vulkan 드라이버를 사용한 WSLg 관리 실행과 NativeAOT의 프레임 해시 768쌍이 모두 일치했습니다.

Linux의 외부 `atspi-gallery-virtual-list` 검사는 실제 갤러리 행을 편집하고, 노출하는 행의 수가 제한 범위에 있는지와 행을 해제하고 키 순서를 바꾼 뒤에도 편집값이 남는지 확인합니다.

세로 스크롤바 손잡이를 끌다가 처음 누른 위치로 돌아오면 누를 때의 오프셋이 복구됩니다. 10억 행의 끝에서도 소수 부분을 보존합니다.

가변 높이 목록이 완전히 가려져 있으면 다시 보이는 프레임의 클립을 기준으로 가시 키를 복원합니다. 생성 범위에 남아 있는 행은 높이와 순서가 바뀌어도 키에 연결된 편집기 상태를 유지합니다.

2026-11-10 [소유 ScrollView](scroll-view.ko.md): Dux.ScrollView가 유한 뷰포트 측정·실제 자식 배치·오프셋·스크롤바 수명과 무효화를 소유합니다. CPU47·별도 GPU/좌표1933검사 통과. 월드 좌표 정밀도 부족은 명시적으로 거부하며 큰 콘텐츠 재배치/가상화·현재 Native는 미완료입니다. 컨테이너3/6 유지. 샘플: `layout_scroll_view_fba.cs`.

2026-11-11 [ScrollView 접근성](scroll-view.ko.md#windows-접근성)은 콘텐츠가 넘치는 축의 Scroll 패턴을 Pane에 제공합니다. 접근성 자식 요소는 ScrollItem으로 중첩 뷰포트에서 자신의 위치를 드러내며, 요소의 식별자와 포커스는 유지됩니다. 유지 중인 ScrollView와 Tooltip은 스크롤 속성이나 Scroll 패턴 제공 여부가 바뀌면 Windows UI Automation에 알립니다. 실행 중인 샘플의 위치·뷰포트 비율 알림을 별도 클라이언트로 확인했습니다.

2026-11-11 [RTL 스크롤](scroll-view.ko.md#windows-접근성)은 가로 오프셋을 오른쪽부터 계산하고 세로 스크롤바 영역을 왼쪽에 둡니다. ScrollView와 Tooltip의 UIA 가로 백분율도 이 방향을 따릅니다. 샘플의 `Direction` 버튼으로 양축 뷰의 방향을 바꾸고 양쪽 끝의 행을 확인할 수 있습니다.

2026-11-11 [Pane 알림](scroll-view.ko.md#windows-접근성)은 유지 중인 Pane과 렌더링된 접근성 자식의 클립된 화면 경계와 화면 밖 상태 변경을 게시합니다. 직접 자식 목록이 바뀌면 게시 후 Pane이 한 번 알립니다. 샘플의 `Toggle row`로 별도 UIA 클라이언트에서 추가와 제거를 확인할 수 있습니다.

2026-11-10 [중첩 스크롤 전달](scroll-routing.ko.md): 독립 휠 축의 경계 전달, 호출자 클립·팝업·창 순서 히트 검사, 공통 스크롤바 캡처/해제와 DirectText 실제 줄 높이를 반영합니다. 확대 관리 회귀 1,645검사·readback 1,025회 통과, 컨테이너 3/6 유지. 소유 스크롤 위젯·가상화·현재 Native는 미완료입니다. 샘플: `layout_scroll_routing_fba.cs`.

2026-11-10 [Field 부모 할당](fluent2-field.ko.md#부모-할당--2026-11-10): 실제 너비로 측정하고 정적 FieldWidth/Arrange·지역 복사본으로 설명 행을 예약한 나머지 높이를 실제 편집기에 줍니다. 80 x 64 측정·80 x 32 실제 경계 결함을 수정했습니다. Field 관리1018·별도 좌표회귀1563검사 통과, 자연 캐시·편집기/undo·Core UIA 메타데이터 유지. Panel 측정 거부/닫힌 독립 루트 경계 확인. Native 미검증·컨테이너3/6 유지. 로컬 샘플:layout_field_allocation_fba.cs.

2026-11-10 [할당 자식 좌표](widget-containers.ko.md#할당-자식-좌표--2026-11-10): 공통 자식 렌더가 상위 클립을 보존하고 기존 표/열 좌표 적용을 격리·복원합니다. 중첩 Padding의 실제 오프셋을 말단 14변형에서 반영합니다. 관리 1529검사(CPU 1113, 래스터/벡터 정확 GPU 비교 416) 통과. 당시 확인한 Field 부모 80 x 64·실제 80 x 32 결함은 위의 후속 Field 할당 체크포인트에서 수정했습니다. 확인한 표준 경로에서 Native 전제 도구가 없고 대체 L: 경로는 접근 거부되었습니다.

2026-11-10 [Badge 측정·할당](fluent2-badge.ko.md#측정과-부모-할당--2026-11-10): 공통 기하·루트 하나, 고정 글자/아이콘 중앙 정렬·클립·숨은 카운터 예약을 연결합니다. 읽기 전용 제공자와 기존 숫자 메모를 명시적 단계에서 공유합니다. 관리 374검사 통과, Native 미검증. 필드/새 캐시 없음. 로컬 샘플: layout_badge_allocation_fba.cs.

2026-11-10 [Link 측정·부모 할당](fluent2-link.ko.md#측정과-부모-할당--2026-11-10): 실제 너비로 줄바꿈하고 정적 Arrange는 지역 복사본을 반환하며 제한된 자연 캐시를 보존합니다. 입력·그리기·클립이 실제 루트를 공유합니다. CPU/입력·래스터/벡터·MSAA 1/4·Light/Dark·서로 다른 유지주기를 포함해 관리 495검사 통과. 필드/새 캐시 추가 없음; Native 미검증. 로컬 샘플: layout_link_allocation_fba.cs.

2026-11-10 [Divider 측정·부모 할당](fluent2-divider.ko.md#측정과-부모-할당--2026-11-10): 정적 Measure·CanArrange로 공통 루트 하나를 배치합니다. 실제 너비로 캡션을 줄바꿈하고 양 축으로 선·중앙 정렬·클립을 결정합니다. 읽기 전용 콘텐츠 제공자는 명시적 측정/렌더 단계마다 한 번 읽습니다. 관리525검사 통과, Native 미검증. 위젯/컨텍스트 필드·캐시 추가 없음. 로컬 샘플:layout_divider_allocation_fba.cs.

2026-11-10 [텍스트 부모 할당](fluent2-text.ko.md): CanArrange로 공통 텍스트 루트 하나를 배치하며 할당/측정 너비로 줄바꿈하고 폰트는 유지합니다. 기존 콘텐츠 제공자 읽기를 보존합니다. 래스터/벡터·MSAA1/4·Light/Dark·16/3프레임 유지 갱신을 포함해 관리321검사 통과. 위젯 필드 추가 없이 컨텍스트 카운터 하나를 추가했고 Native는 미검증입니다. 새 로컬 샘플:layout_text_allocation_fba.cs.

2026-11-10 [Progress 측정·할당](fluent2-progress.ko.md): 정적 소스/표현 측정으로 getter 호출을 분리합니다. getter 높이를 선언하거나 부모 강제 높이를 사용하며 정적 Arrange와 공통 렌더가 실제 루트를 반영합니다. 관리306검사 통과, Native 미검증입니다. 새 로컬 샘플:layout_progress_allocation_fba.cs. 전체 수치·리사이즈 보류를 유지합니다.

2026-11-10 [Spinner 부모 할당](fluent2-spinner.ko.md): 정적 Arrange와 공통 순수 측정/렌더가 부모 루트를 반영하며 관리 520검사 통과, Native 미검증입니다. 신규·수정 C#은 .editorconfig의 공백·줄바꿈을 따르고 표시 문구는 Width : 160 형식을 사용합니다. RadioGroup 샘플도 서식 수정 후 재빌드했습니다.


2026-11-11 Slider 수치 접근성: UiAccessibilityRangeInfo/Node.Range·타입 지정 RequestRangeValue·RangeChanged가 생성형/제네릭 Slider 바인딩을 Windows RangeValue10003에 연결합니다. 최종 관리28개·외부 클라이언트13개/호스트3개와 응답 대기 없는 Tooltip 종료 검사가 통과했습니다. 제거된 자식은 해제에 필요한 고정 정체성만 유지하고 데이터/동작은 거부하며 HWND 루트는 기존 호스트 수명에 따라 만료됩니다. 이전 S_OK/0 getter 실패를 엄격한 HRESULT 기대값을 유지한 채 수정했습니다. [Slider 계약](fluent2-slider.ko.md)과 artifacts/slider-range-2026-11-11/lifetime-verification.json을 참고하세요. 발화·기존 탭 키보드/플래그 전체 지원·NativeAOT는 남아 있습니다.

2026-11-11 SVG 곡선 텍스트 참조: SvgTextReferences가 DOM/스타일 해석 뒤 tref를 사양의 등가 span으로 컴파일하고 원래 요소/ID/클래스 선택자 정체성과 위치를 보존합니다. textPath 안의 tref가 비던 문제를 XML 추가 해석이나 다른 렌더러 없이 고쳤습니다. 샘플 곡선 문장도 참조 정의를 사용합니다. CPU112·관찰123·SVG GPU52·관찰 GPU7과 이전 직접 작성 샘플 본문의 픽셀 일치를 확인했습니다. artifacts/svg-support-2026-11-11/tref-verification.json과 [SVG 텍스트](svg-support.ko.md#곡선-위에서-참조하는-문자)를 참고하세요. 전체 준수·네이티브·AOT는 미완료입니다.

2026-11-11 SVG 문서 기능: SvgCssResources가 주석·따옴표 속성 문자열과 실제 자원 토큰을 구분하며 이스케이프한 실제 자원에도 기존 명시적 검사를 적용합니다. CPU102·관찰123·SVG GPU52·관찰 GPU7에서 symbol/use·xlink·패턴·마스크·방사형 그라디언트·마커·내장 PNG·자원 토큰을 확인했습니다. Observer → SVG는 이미지·텍스트 / 문서 기능의 실제 연결된 탭 본문과 자원5개를 제공하고1px·1°·1% 조절기를 공유합니다. artifacts/svg-support-2026-11-11/features-verification.json과 [SVG 안내](svg-support.ko.md)를 참고하세요. SVG/CSS/문자 체계·네이티브·AOT 전체 준수는 미완료입니다.

2026-11-11 SVG 수명 수정: SvgScene 해제 시 백엔드 소유자·그림 참조를 끊어 오래 보관한 SvgAsset이 백엔드 캐시를 붙잡지 않습니다. 텍스트32회 갱신에서 이전 네이티브 그림을 닫고 갱신 실패에서는 그림/글꼴을 유지하며 반복 해제에서 등록 글꼴·타입페이스 핸들을 닫습니다. CPU81·관찰120·SVG GPU44·관찰 GPU3을 확인했습니다. artifacts/svg-support-2026-11-11/lifetime-verification.json과 [수명 검증 한계](svg-support.ko.md#해제와-오래-보관한-자원-참조)를 참고하세요. 도달 가능 여부·핸들 검사는 전체 네이티브 메모리나 NativeAOT 인증이 아닙니다.

2026-11-11 SVG 명시적 글꼴 단계는 앞선 글꼴 미구현 기록을 대체합니다. SvgStore(fonts : new SvgFontOptions(defaultFamily, faces))가 SvgFontFace 파일·컬렉션 면을 보관하고 실제 등록 스타일을 선택하며 없는 글꼴·스타일·글리프는 게시 전에 거부합니다. 설치 글꼴 자동 교체는 없고 @font-face·문서 내장 글꼴은 미지원입니다. 유지 텍스트의 확대에서 XML·글꼴 결정·포함 여부 확인을 반복하지 않으며 FontStatistics는 네이티브 그리기와 구분합니다. Observer → SVG는 원본4개·Latin/굵게/한글/textPath·글꼴 지표와1px·1°·1% 컨트롤을 제공합니다. CPU74·관찰120·SVG GPU44·관찰 GPU3 근거와 한계는 artifacts/svg-support-2026-11-11/font-verification.json과 [SVG 안내](svg-support.ko.md)에 있습니다. 전체 문자 체계·기능·네이티브·AOT 검증은 남아 있습니다.

2026-11-11 SVG 조절 단위: 관찰 화면의 이동1px·회전1°·확대1%p를 포인터와 포커스된 좌우 방향키가 함께 사용하며 라벨/현재 값에 단위를 표시합니다. 세부 단위 입력6개를 포함한 관찰119개·관찰GPU3개·Slider GPU156개와 앱/Graphics 빌드 경고·오류0개를 확인했습니다. ShowStepMarkers는 기본true이며 SVG는 false로 지정해 미세 입력과 연속 막대를 함께 유지합니다. artifacts/svg-support-2026-11-11/unit-step-verification.json을 참고하세요. 글꼴/전체 SVG/네이티브 검증은 남아 있습니다.

2026-11-11 SVG 관찰/아이콘 단계: 선택형 Duxel.Svg가 한 번 해석한 장면/래스터를 Core UiSvg와 UiSvgIcon.MenuIcon/TabIcon에 공유합니다. 기본 색은 Authored이며 MultiplyForeground는 명시적으로 선택합니다. 실제 Observer → SVG에서 두 이미지 경로·확대/회전/이동·재로드/초기화·횟수를 확인하며 ./run-gallery.ps1로 실행합니다. 위젯 Dispose 뒤에도 유지 레이어의 텍스처 참조를 보호합니다. 이미지 API의 전체 UV·알파/이전 프레임 픽셀 보존·IsColorImage 샘플링도 고쳤습니다. 계약54개·관찰113개(아이콘11 포함)·SVG GPU32개/MSAA1-4·관찰GPU3개·메뉴 글리프GPU216개와 최종3빌드 경고/오류0개를 확인했습니다. [SVG 계약](svg-support.ko.md), artifacts/svg-support-2026-11-11/observer-verification.json을 참고하세요. 글꼴/텍스트·전체 기능·네이티브 수명/배포/AOT와 원래 목표는 남아 있습니다.

2026-11-11 최신 추가: UiMenuOption.Icon/SecondaryContent·UiMenuIcon.Create<TRegular, TFilled>()·UiMenuItemContent·UiControlContext.IsHovered로 컴파일된 아이콘 쌍·오른쪽 HelpText·독립 클립·RTL을 공통 메뉴에 연결했습니다. 실제 경로는 컨테이너 → Menu 공통 동작 → 아이콘 메뉴 열기입니다. artifacts/menu-content-2026-11-11/verification.json 및 [메뉴 계약](fluent2-menu.ko.md)을 참고하세요. TTF 아이콘은 일반2D에서 래스터 텍스처를 캐시하고3D 레이어에서 폰트 윤곽선 메시를 사용합니다.

사용자는 원래 활성 목표에 SVG 비트맵식 파일 입력과 SVG 기반 아이콘·확대/축소/회전 위젯 또는 이에 준하는 재사용 API를 추가했습니다. 위 자원/위젯 구현에서 두 경로가 SVG 해석/자원을 공유합니다. [SVG 목표](svg-support.ko.md)와 artifacts/svg-support-2026-11-11/scope.json이 남은 글꼴/전체 기능/네이티브 검증을 추적합니다. 관리 검사는 NativeAOT와 구분합니다. 원래 모든 목표 축·리사이즈 보류·MSVC/Windows SDK 경로 제공 전 탐색 금지를 유지합니다.

2026-11-10 [RadioGroup 부모 할당](widget-containers.ko.md):관리264검사·245이미지와 샘플 표시·종료 확인. C++ Build Tools 부재로 Native 미검증. 전체 목표 수치와 리사이즈 보류 유지.

2026-11-06 [컨테이너 유지 경로 통합](widget-containers.ko.md): Row/Column·Container·Stack도 Wrap/Grid와 선택적 유지 결과를 공유하며 부모 데이터 래퍼는 할당 크기에 렌더합니다. 사용자 박스/축/맞춤 정책은 기본 비캐시입니다. 관리/Native175검사·GPU121이미지로 재사용과 동적 변경을 확인했습니다. 이전 Native 재사용 비교의 유지 측정0B는 유지하지만1.7µs 표본을 포함하고 Container1만 개 생성은80,000B(+1.33%) 늘었습니다. 전체 속도·메모리 개선은 미확정입니다. 근거:`artifacts/container-retention-2026-11-06/verification.json`.

2026-11-06 [배치 유지](widget-containers.ko.md): 모든 UiWidget에 인스턴스 필드를 늘리지 않고 소유권을 따라 자식 변경을 부모로 전달합니다. 참여한 Wrap/Grid가 측정 영역을 유지하고 Fluent 버튼은 컴파일된 표현 정책으로 참여합니다. 미지의 사용자 측정은 기본 비캐시입니다. 관리/Native159검사·GPU109이미지로 입력·텍스트 변경·범위/수명을 확인했습니다. 무텍스트 버튼2만 개의 Native3회씩 비교에서 순수 측정 중앙값1827.4µs→타이머 해상도 근처0.2µs이며 양쪽0B·프로세스 메모리 범위 겹침입니다. 전체 프레임/FPS/Pi 결과는 아닙니다. 근거:`artifacts/layout-retention-2026-11-06/verification.json`.

2026-11-06 [Wrap/Grid](widget-containers.ko.md): 생성 `UiWrap`/`UiVerticalWrap`·`UiGrid`/`UiHorizontalGrid`가 같은 소유권·측정·입력 경로와 정적 축 선택을 사용합니다. `Dux.UniformGrid`로 기존 `Dux.Grid` 호출 해석을 보존합니다. 동적 옵션은 자식을 유지하고 `UiArrangedChildrenWidget`이 사용자 배치의 부모 제약을 검사합니다. 관리/RC3 Native 각각136검사와 GPU93이미지 동치,준비 후 Wrap1만+Grid1만 자식 순수 측정0B를 확인했습니다. 앱 처리량·Private 개선 주장은 아닙니다. 샘플:`layout_flow_containers_fba.cs`,근거:`artifacts/flow-containers-2026-11-06/verification.json`.

2026-11-06 [Stack/Positioned](widget-containers.ko.md): 상속 가능한 생성 구현이 flex와 다중 자식 소유권·측정 보호 아래 Build 부모 데이터 조회를 공유합니다. 가장자리 제약·동적 옵션·잘린 표준 포인터 입력·최상단 누름/뗌 버튼을 관리/Native95검사와 GPU53이미지 동치로 확인했습니다.1만 작은 위치 지정 자식의 준비 후 순수 측정은0B이며 전체 프레임 할당·처리량은 아닙니다. 이 문장은 Stack만 검증한 이전 시점이며 최신 Wrap/Grid 항목에서 묶음 단계를 검증했습니다. 근거:`artifacts/stack-containers-2026-11-06/verification.json`.

2026-11-06 [가중 컨테이너](widget-containers.ko.md): 생성 `UiExpanded`·`UiFlexible`·`UiSpacer`, `Dux.Expanded/Flexible/FlexSpacer`, Row/Column의 가중치·교차축 Stretch를 제공합니다. `Flex` 변경은 자식 상태를 보존하며 프레임을 요청합니다. 사용자 말단은 `UiControlContext.ConstrainToLayout`으로 할당 크기를 따를 수 있습니다. 기존 `Dux.Spacer(float)`는 유지합니다. 관리/Native67검사씩·GPU37이미지 동치,1만 자식 순수 측정0B. 전체 컨테이너는6단계 중1단계이며 처리량·전체 위젯 늘이기·증분 배치는 미완료입니다.

2026-11-06 [실제 고부하 앱·명령 누적 후보](optimization-evaluation.ko.md): 공통 빌더가 연속 primitive 명령의 개수·범위를 누적하고 다른 접근 전에 불변 명령을 확정한다. 관리/Native74검사씩·GPU100이미지 동치. 앱12회 교대 비교의 FPS·프레임·Private·작업 집합은 범위가 겹쳐 개선 미확정이다. 이전2048텍스트 메모리 진단과 다른 비교이며 전체 목표·리사이즈 보류는 유지한다.

2026-11-06 [유지된 텍스트 정리](directtext-layout.ko.md): 캐시 키와 레이어 보호 참조가 그대로면 제거 후보 없음 판정을 재사용한다. 고정RC3 Native에서2048개 유지 시 Render 할당99.74% 감소·GPU35검사 통과. 짧은 종료 Private는51.83% 높았으며 전체 앱 메모리 개선으로 보고하지 않는다. 원본과 해시는 `artifacts/retained-text-maintenance-2026-11-06/verification.json`을 참고한다.

2026-11-06 [공통 위젯 저장소](widget-cpu-retention.ko.md): UiBuildContext의 의존성·소유권 변경 저장소를 지연 생성하고 직접 위젯의 콜백/문맥 슬롯을 공유한다. 고정RC3 Native1만 개 비교에서 Container 생성 할당23.47%, 첫 Build 할당73.81%가 줄었고 변경 없는 후속 순회0B는 유지했다. 한정된 할당 결과이며 전체 앱 Private/FPS나 Fluent 전체 완료 근거가 아니다. 창 이동·리사이즈 조사는 사용자 요청으로 보류하며 소스·근거를 보존한다.

2026-11-06 [실시간 크기 변경 순서](windows-resize-coherence.ko.md): DC656134는 제한된 Native 반복 검사를 통과했지만 잔상·크기 추종 지연으로 사용자 확인에 실패했다. 현재 후보는 확정 WM_SIZE의 프레임 제출 연결과 교체 전 그래픽 준비 검사를 유지하며, 표시 펜스는 지연 자원 해제를 보호한다. 검증은 .NET11만 실행한다. 수치상 제출 검사는 실제 화면 승인 근거가 아니며 리사이즈 수용 원장을 참고한다.

2026-11-06 [Windows 타이틀 기본값](extended-title-bar-guide.ko.md): 기본 크롬을 네이티브 System으로 전환했습니다(`UseDuxelTitleBar=false`). 제목 글꼴·높이·버튼을 Windows가 그리며 앱은 System 모드에서도 직접 루트 콘텐츠 배치를 초기화합니다. 명시적인 Duxel/ExtendedContent는 유지합니다. 반복 리사이즈 회귀는 조사 중입니다. Native 미리보기 `804F7691`의 첫 표시·15초 종료 검사는 장시간 리사이즈 검증이 아닙니다.

2026-11-06 [컴파일된 컨테이너](widget-containers.ko.md): Flutter를 참고한 박스 제약·생성 UiContainer/UiRow/UiColumn이 UiWidget 소유권과 제약 기반 Build 측정을 공유한다. 실행 가능한 `layout_containers_fba.cs`에서 실제 Fluent 콜백을 확인한다. 유연한 자식 크기·고급 배치·스크롤/가상화·유지 레이아웃 등6단계의 남은 범위는 원장을 참고한다.

2026-11-06 [그리기 범위별 애니메이션·중첩 캐시](frame-scheduling.ko.md): Spinner·Progress는 공통 범위 검사로 화면 밖·완전 투명 상태의 깨우기를 줄인다. 캐시 레이어는 유지한 모션 범위로 관계없는 프레임에서도 숨김 기하를 재사용한다. 투영 가시성 생략과 합집합의 보수적인 추가 재구성은 남아 있다. 자식/부모 이동은 합산하고 클립은 명령의 로컬 좌표를 사용한다. 근거는 `artifacts/animation-visibility-2026-11-06/`와 `artifacts/hidden-animation-cache-2026-11-06/`이며 측정 체크포인트는 컨테이너·리사이즈 변경 이전이다.

사용자 검증 정책,2026-11-06: 이후 기능·회귀·성능 검증은 .NET11만 실행한다. .NET9·.NET10은 최종 완료 시 배포용 컴파일만 확인한다. 기존 결과는 보존하고 개발 검사를 반복하지 않는다. 고정 RC3 NativeAOT 성능 비교와 공개 TFM은 유지한다. 사용자의2026-11-11 SDK 복구 요청이 이전 루트 `global.json` 고정 유지 지시를 대체한다.

2026-11-06 [예약 프레임](frame-scheduling.ko.md): UI 시각 예약을 기존 앱 이벤트 대기에 연결합니다. Spinner 지연과 캐시 재생은 타이머 객체 없이 예약을 보존하며 표시 모션·커서 깜빡임을 유지합니다. 근거와 한계는 연결 문서를 참고합니다.

2026-11-06 [Input 실제 자식 슬롯](fluent2-input.ko.md): BeforeSlot/AfterSlot·원자적 SetSlots·키/소유권/조상 순환 거부, Build 수명과 focus-within을 지원한다. 소스 생성된 실제 버튼과 사용자 위젯을 사용하며 지우기 버튼 갤러리를 제공한다. .NET10 전체2959/GPU3561·고정RC3 Native각3561, 전체2737·집중833이미지 동치다. 자연 측정·Field/UIA/모션과 공식 전체 적합성은 남는다.


2026-11-06 [Input 앞/뒤 콘텐츠](fluent2-input.ko.md): 기본 텍스트 슬롯과 정적 사용자 아이콘/표현 조합, 편집 영역 분리·협소 폭 겹침 방지를 제공한다. .NET10 전체2934/GPU3408·고정RC3 Native전체각3408, 같은조건 전체2481·집중577이미지씩 동치다. Core 생성67개는 이전과 같다. 대화형 자식 슬롯·Field/UIA/모션과 전체 공식 적합성은 미완료다.


2026-11-06 [Badge 할당 검사](fluent2-badge.ko.md): 측정 메서드 분리·첫 호출 기록·캐시 무효화 대조로 정상0B와 실제320000B를 구분한다. 관리 독립3회/Native 집중각50·관리GPU전체2428 및 이전Native2097이미지 동치를 확인했다. 제품 소스는 같으며 원래12336B 원인은 미확정이다. 성능 향상으로 세지 않는다.


2026-11-06 [Input](fluent2-input.ko.md)은 기본/사용자 상속·생성·Dux·직접 API가 같은 편집과 수명을 사용한다. 3크기×4표현, Tab·readonly·placeholder·invalid와 IME 소유자 해제를 연결했다. NET10 전체2913·집중229, 고정 RC3 Native 전체각2427·집중각229, 같은 크기193이미지×2가 일치한다. 관리 GPU 전체 재실행은 기존 Badge 할당12336B 검사에서 실패했으며 전체 통과로 세지 않는다. 카탈로그30이름=상위11/55+변형19, 미연결44이며 공식 전체 외형·UIA·Field·슬롯·모션·Pi는 미완료다.


2026-11-06 [RadioGroup](fluent2-radio-group.ko.md)을 공통 위젯·생성 팩터리·Dux·직접 경로에 연결했다. 그룹 Tab/화살표/Space·선택 콜백 순서·수직/수평/아래 라벨 배치와 보유량에 상한을 둔 줄 레이아웃을 제공한다. 새 공통 텍스트 블록은 여러 줄 기준선·실제 잉크를 보존한다. 전체 공식 화면/UIA/Field/RTL은 계속 미완료다.

2026-11-06 [Checkbox 표현](fluent2-checkbox.ko.md): 기존 생성 위젯의 혼합/선택/비활성 상태·Regular14/20·8개 정적 표현 조합과 정확한12/16px 아이콘을 연결했다. 같은 동작·상속·수명 경로를 사용하며 `--checkbox` 갤러리로 확인한다. 공식 화면/UIA/RTL/긴 라벨과 전체 Fluent 완료는 미완료다.

2026-11-06 [깊이 버퍼 공유](shared-depth-target.ko.md): 공통 그래픽 큐의 깊이 버퍼를3개에서1개로 통합했다. 고정RC3 Native3회 교대 비교에서 깊이 할당66.67%·Private24.91% 감소, FPS/GPU 시간 범위는 겹친다. NET10전체1862·관리/Native 큐 검증1067, 같은 DPI 이전1109이미지×2·관리1117이미지×2가 정확히 같다. 전체 목표·장기 메모리·Pi 실측은 미완료다.

2026-11-05 [Panel과 직접 구성](panels-root-content.ko.md): 기본 샘플의 내부 Window를 제거하고 선택적 Panel·생성기·DSL 호환 경로를 제공한다. Windows 확대 중 메시지/GPU 동기 대기를 제거했다. 새 실행 경로와 검증은 [실행 안내](widget-gallery-run.ko.md)를 따른다.

2026-11-05 사용자 화면 결함 후속: [내부 창 배치](window-placement.ko.md)는 최초 위치/크기 적용·작업 영역 상대 좌표·AutoHeight·이동 해제 위치 유지 수정을 설명한다.
이제 [실행 가이드](widget-gallery-run.ko.md)는 gallery-window-fix 게시물을 가리킨다. NET10전체1004·관리/RC3 공통108·이전428이미지 동일. 실제 이동 지연은 새 인터랙티브 창의 프로필/사용자 확인 중이며 해결로 보고하지 않는다.

중간 화면 확인: [실행 가이드](widget-gallery-run.ko.md)의 .NET10 현재 소스와 검증된RC3 게시물을 구분해 사용한다. 기본·`--typography`·`--progress`·`--slider` 모드를 제공한다.
2026-11-05 [Fluent Slider](fluent2-slider.ko.md)는 생성된 상속 위젯·정적 동작/표현·포인터/키보드·단계/반전/수직을 연결했다.
NET10전체995·관리/RC3 공통99·이전423이미지 동일. 카탈로그24이름·알려진 요구289·누락265는 최종 지원률이 아니다. 기존 SliderFloat/VSlider/Dux.Slider 전체 이전·공식 팔레트/UIA/룩앤필은 미완료다.

2026-11-05 기본 Fluent Text: [Text 후속](fluent2-text.ko.md)은 실제 Body1(14px/20px)·기본 줄바꿈·상속/함수/형식 지정 즉시 경로를 설명한다.
비제네릭 ui.Text 계열은 컨텍스트 글꼴의 명시적 호환 표현으로 보존한다. 저수준 Fluent 사용은 DirectText·메트릭 백엔드·TypographyFonts가 필요하며 누락 시 실패한다.
공통 줄바꿈의 들어맞는 원본 문자열 재사용·개행 폭 계산을 수정했다. NET10전체963·관리/RC3 공통67·기존417이미지 동치. 전체 Text 변형/UIA/기본 즉시 API 이전과 앱 성능은 미완료다.

2026-11-05 공통 그라데이션: [둥근 그라데이션](rounded-gradients.ko.md)은 `AddRectFilledGradient`·`UiLinearGradient`와 실제 ProgressBar 연결을 설명한다.
32B 스트라이드를 유지한64B 도형·정적/동적/컴파일 레이어·부분 갱신을 검증했다. 동일256개 draw512→1·업로드61,440→16,384B이며 전체 FPS/Private 개선은 미검증이다.
NET10전체954·관리/고정RC3 GPU45·입력62·원본 셰이더 동일 크기323이미지 동치. 기본 위젯/전체Fluent/메모리 목표는 미완료다.

2026-11-05 진행 전환: [ProgressBar 후속](fluent2-progress.ko.md)은 정적 동작 타입·위젯 소유 상태·300ms ease·해제를 설명한다.
NET10 전체915·관리/Native 공통62검사와 기존323이미지 보존을 확인했다. 전환 저장은 구성당24B 추가이며 전체 메모리/속도 개선이 아니다.
기존 비전환 타입에는 상태 필드를 추가하지 않았다. 공식 전체 모션/마스크·UIA·기본 API 이전은 미완료다.

2026-11-05 진행 표시: [생성된 ProgressBar](fluent2-progress.ko.md)는 값/함수 소스·표현을 닫힌 타입에 연결하고 불확정/모션 감소 상태를 그린다.
기존 FBA의 `--progress` 모드로 확인한다. NET10 전체898·관리/Native 공통46검사·기존323이미지 동일·새 GPU7이미지를 검증했다.
기존 오버레이 API는 보존했다. 전체 기본 전환·300ms 전환·곡선 마스크·의미 정보·공식 룩앤필은 미완료다.

2026-11-05 키보드: [생성된 컨트롤 입력](widget-keyboard.ko.md)은 Tab·Enter/Space·포커스 표시·취소·수명과 미완료 범위를 설명한다.
NET10 전체871·집중159·생성기66, 동일 입력24검사의 고정RC3 NativeAOT 실행과 기존 이미지323개 보존을 확인했다.
`tests/Duxel.Control.Tests`는 같은 테스트 본문을 재사용하며 창을 열지 않는다. 전체 Tab 통합·공식 포커스 시각·UIA·성능은 미완료다.

2026-11-05 타이포그래피: [명명된 Fluent 텍스트17종](fluent2-typography.ko.md)은 실제400/600/700 글꼴과 공식 행높이를 생성된 타입에 연결한다.
`DuxelFontOptions.Typography`·`UiContext.SetTypographyFonts`와 메트릭 백엔드를 사용한다. 기존 FBA의 `--typography` 모드에17종을 표시한다.
NET10 전체847·생성기66·기존 이미지323개 동일, 고정 RC3 NativeAOT 두 모드의 첫 present/exit0을 확인했다.
일반 Text의 기본 글꼴 전환·전체 변형·UIA·공식 시각 비교·성능은 미완료다. 카탈로그22이름은 전체 지원률이 아니다.

2026-11-05 텍스트: [생성된 Text 위젯](fluent2-text.ko.md)은 문자열/함수 소스와 표현을 닫힌 타입으로 연결한다.
기존 Dux.Text·색상·줄바꿈 및 즉시 API가 공통 커널을 사용한다. 상속 ReadText와 사용자 측정/그리기 예제를 제공한다.
공식 전체 타이포그래피·의미 정보·룩앤필은 미완료이며 카탈로그 이름을 완전한 지원률로 세지 않는다.

2026-11-05 대규모 유지 위젯: [버튼1만개 입력 ID 재사용](widget-scale.ko.md)은
위젯 수명별4,096자 캐시·부모 변경·단일 콜백과 고정 RC3 CPU3회 비교를 설명한다.
전체801검사를 통과했다. CPU median12.55~14.13% 감소는 앱 FPS가 아니며,
최신 지연 저장은 일시 팩터리 할당 회귀를 해결했다. 유지 캐시의 추가 상주 비용과 전체 앱 검증은 남아 있다.

2026-11-05 선택 버튼: `Fluent.ToggleButton`·`Dux.ToggleButton`·즉시 `ui.ToggleButton`은
생성된 상속 위젯과 공통 bool 전이·입력 커널을 사용한다. [토글 기록](fluent2-toggle.ko.md)을 따른다.
전체 Fluent 적합성과 버튼1만개 성능은 아직 검증하지 않았다.

2026-11-05 목록 후속: [Fluent 전체 전환](fluent2-coverage.ko.md)의 TSV 초안을 생성기가 직접 읽는다.
표준 출처·누락 팩터리를 검사하며 `DuxelRequireCompleteFluentCatalog=true`는 현재 미완성 상태를 거부한다.
현재289이름은 최종 전체 분모가 아니며, 카탈로그 검사는 룩앤필 완료 증거가 아니다.

2026-11-05 최신: [컴파일된 토글·정체성](fluent2-toggle.ko.md)은 일반 `UiWidgetStyle` 생성자 연결,
bool/혼합 Checkbox·Switch와 표시 문구에 독립적인 유지 입력 ID를 설명한다.
NET10은787건 통과했으나 RC3 전체의 GPU 입자 동치 실패는 집중2회 통과 후에도 미해결이다.

2026-11-05 후속: [Fluent 전체 전환](fluent2-coverage.ko.md)은 확대된 공식 컴포넌트 범위,
특성 기반 표준 노출과 `Dux.Button()`의 생성된 Fluent 팩터리 연결을 설명한다.
전체 컴포넌트·비표준 비노출·동일 룩앤필은 아직 미검증이다.

2026-11-05: [위젯 구현 계약](widget-implementation.ko.md)은 첫 Fluent 2 테마, 기본 버튼 상속과
소스 생성기의 구체 동작·표현 연결을 설명한다. 다른 위젯 전환과 성능 검증은 미완료이며,
숫자 설정 생성 성공을 목표 완료로 집계하지 않는다.

2026-11-04: [기본 위젯 CPU 재사용 진행](widget-cpu-retention.ko.md)은 atlas glyph-run 캐시의
적용 범위·상한·콜백·프레임 소유권을 설명한다. 기본 DirectText 전체 구성 생략과 구분한다.

2026-11-04: [Pi 사양 시뮬레이션](raspberry-pi-simulation.ko.md)은 공식 사양·두 Mesa 버전의
필수 기능 불일치와 기존 프레임 기록의 지연 민감도를 구분한다. Pi 실측 FPS나 Linux 지원 증거가 아니다.

> Last synced: 2026-11-14
> 대상: Duxel로 앱, 샘플, 재사용 UI 컴포넌트를 작성하는 코딩 에이전트와 개발자
> 범위: Duxel 기능 맵, 아키텍처 경계, 권장 워크플로, 샘플 기준점, 바로 사용할 수 있는 생성 템플릿

2026-11-04 후속: 공통 Vulkan 렌더러는 화면 좌표의 정적·동적 목록을 스왑체인 이미지별로
보존하며 변경 영역만 다시 그린다. MSAA 이력·반투명 합성·스냅샷 수명과 전체 갱신 조건은
[변경 영역 렌더링](retained-rendering.ko.md)을 따른다. 라즈베리파이 실측·Linux/ARM64 지원을
이 Windows 검증으로 보장하지 않는다. 최신 후속 결과는 해당 문서와 SESSION_HANDOFF.md를 우선한다.

## Markdown 이미지의 Component 스크롤 (2026-11-15)

현재 고정 RC3 NativeAOT 옵저버는 `/home/dimohy/.cache/duxel/observer-image-component-rc3-aot-20261115/publish/Duxel.Gallery.Linux`입니다. 실행기를 통한32개 화면 표시·384회 네이티브 입력과 정상 종료를 확인했습니다. 기존 성능 기준 실행물은 보존했으며 새 비교 성능 향상을 주장하는 검사는 아닙니다.


Markdown은 내부 텍스트 뷰포트를 이미 공통 스크롤 트리에 등록합니다. 이미지의 `Component.ScrollTo`와 `ScrollToPoint`도 실제 그려진 이미지 영역과 기존 뷰포트 이동 요청을 재사용하며 링크·표 이미지에 적용됩니다. 가짜 텍스트 노드나 별도 투영 모델을 추가하지 않았습니다. View·Editor 검사에서 정확한 Unicode 원문, 커서, 선택, 런타임 식별자와 포커스를 보존합니다. 링크 이미지를 노출해도 링크로 이동하지 않으며 파싱한 Markdown과 이미지 자원을 재사용합니다.

중첩된 잘못된 점 요청에서 안쪽 영역의 클리핑이 먼저 `InvalidOperation`을 반환하는 오류를 찾았습니다. 공통 엔진에서 가장 바깥의 실제 표시 영역을 먼저 검사해 일관되게 `InvalidArgument`를 반환하도록 고쳤습니다. 수정 전에도 실패한 요청이 이미지 위치를 바꾸지는 않았으며, 이번 수정은 오류 분류와 검사 순서를 바로잡습니다.

집중 검증은 View/Editor, 로컬/중첩 LTR/중첩 RTL, 일반/링크/표 이미지, 일곱 배치와 정확한 가장자리 위치를 포함한 레이아웃930개입니다. 독립 네이티브 클라이언트는 명시/등록을 포함한12개 시나리오에서 각각68개를 통과했으며 세 좌표 공간을 확인합니다. 호스트 수명주기24개는 자신이 만든 창의 등록 해제를 확인합니다. 같은 Core 바이너리로 기존 공통 컴포넌트175개·텍스트 스크롤691개도 통과했습니다. 물리 화면 읽기, 전체 접근성 적합성이나 이미지 성능을 인증하는 검사는 아닙니다. 근거: `artifacts/linux-image-component-scroll-2026-11-15/verification.json`.

집중 프로필은 `image-component-model`과 `image-component-native`이며 뒤에 새 결과 경로와 명시적인 라틴/한글 폰트를 전달합니다. 네이티브 프로필은 기존 접근성 세션 또는 `dbus-run-session --` 안에서 실행합니다. 이전의 ‘편집기 이미지에 원문 기반 Component 구현이 더 필요하다’는 설명은 미검증 가정이었으며, 소스 확인과 실제 호출로 기존 뷰포트 경로를 재사용할 수 있음을 확인했습니다.

## 공통 뷰포트를 통한 컴포넌트 스크롤 (2026-11-15)

Linux `Component.ScrollTo`와 `ScrollToPoint`를 `UiAccessibilityTree.RequestScrollIntoView`의 실제 이동에 연결했습니다. `UiAccessibilityScrollAlignment`는 왼쪽 위·오른쪽 아래·위/아래/왼쪽/오른쪽 가장자리·최소 이동 또는 유한한 UI 클라이언트 좌표를 받습니다. 기존 `UiAccessibilityTextScroll` API는 유지하며 같은 정렬 계산을 사용합니다. 뷰포트를 덮는 큰 컴포넌트는 최소 이동을 반복해도 반대쪽 가장자리로 번갈아 움직이지 않습니다.

화면·창·부모 좌표를 UI 클라이언트 좌표로 변환합니다. 지정한 점은 가장 바깥의 실제 표시 스크롤 영역 안에 있어야 하며 각 소유자의 이동 한도를 지킵니다. 이동한 화면의 게시 후 요청을 완료합니다. 컨트롤의 식별자를 보존하고 포커스를 옮기거나 실행하지 않습니다. 공통 `ScrollView` 조상과 등록된 Markdown 내부 텍스트 뷰포트에 연결됩니다. 위의 이미지 집중 검증이 기존 버튼 검사의 범위를 확장합니다.

집중 검증은 바깥/안쪽 RTL 네 조합의 공통 모델175개, 가상화·팝업을 포함한 기존 텍스트 스크롤691개, 독립 네이티브 클라이언트의 명시/등록 세션별24개와 호스트 수명주기4개입니다. 지원 net8/net9/net10 Core/Linux 빌드가 경고 없이 통과했습니다. net10 검사에서 클립보드 MULTIPLE의 오버로드 선택 문제도 발견해 `pairs.Data.AsSpan()`으로 수정 가능한 범위를 명시했습니다. 기존 독립 선택 영역 클립보드 검사35개가 .NET10.0.15에서 통과했습니다. 전체 접근성 검사는 재실행하지 않았으며 물리 화면 읽기 적합성이나 새 성능 개선을 주장하지 않습니다. 근거: `artifacts/linux-component-scroll-2026-11-15/verification.json`.

새로 게시한 검사 실행물로 네이티브 경로만 실행합니다.

```bash
dbus-run-session -- /path/to/Duxel.Linux.App.Tests component-scroll-native \
  /path/to/fresh-result /path/to/latin.ttf /path/to/korean.ttf
```

레이아웃 집중 프로필은 `component-scroll-model`과 `text-scroll-common-regression`이며 같은 결과·폰트 인수를 받습니다. 네이티브 프로필은 자신이 만든 창만 열고 닫으며 하위 프로세스의 최초 실패 기록을 보존합니다.

## 현재 검증 기준 — 2026-11-03

2026-11-04: 공통 GPU 상태 갱신은 `UiGpuParticleSystem.Append`/`Truncate`와 프레임별
`UiGpuParticleSnapshot`을 사용한다. 기존 도형의 위치·방향·회전·충돌 상태를 유지하고
새 도형만 업로드한다. VSync ON은FIFO이며 실제 VSync/MSAA/표시 모드는
`UiRendererFrameMetrics`의 `VSyncEnabled`/`MsaaSamples`/`PresentationMode`로 확인한다.
NativeAOT 실제 입력24검사, NET10/11각474검사, 기존 이미지142/142일치를 통과했다.
최신 배포의5000개·Size6·Render/MSAA1·독립3회 평균 중앙값은5561.85FPS다.
조건·재현·최신 해시는 [현재 검증 기록](polygon-performance.ko.md)을 따른다.

공 튀기기 공통 엔진 최적화는 [진행 기록](polygon-performance.ko.md)을 따른다.
공통 API는 `SpatialGrid2D`, `Duxel.Core.Simulation.CircleCollisionSolver2D`,
`UiDrawListBuilder.AddRegularPolygons`와 `UiRendererFrameMetrics.FramebufferSize`다.
원과 사각형은 분석적 coverage를 유지하면서 같은 GPU 인스턴스 스트림으로 배칭한다.
샘플은 기본 5,000개·Size 6·큰 창이며 GPU 벡터 캔버스와 정적 배경 geometry를 구분한다.
`UiGpuParticleSystem`/`UiGpuParticleFrame`/`AddGpuParticles`는 GPU에 상태를 유지하고
이동·공간 탐색·충돌·정점 생성을 수행한다. 같은 모델의 CPU 구현은 `ParticleJacobiSimulation`이다.
`UiDrawList.GetCommandFeatures`와 `UiDrawData.GetCommandFeatures`는 변경되지 않은 명령의
콜백/GPU 기능 탐색을 재사용한다. 배경은 공통 `BeginLayer` 정적 캐시를 사용하며,
공통 렌더 파이프라인은 기본 활성화한다. 샘플 전용 draw-list 캐시는 사용하지 않는다.
공통 FBA 실행기는 `scripts/BuildArtifactLifetime.ps1`을 사용해 출력 소유권·해시·활성
프로세스를 확인하고 최대3개 보관한다. 미등록·실패·취소·수정·실행 중 출력은 보호한다.
GPU 구현·정확성·회귀·로컬 배포 검증을 완료했다. 최신 EXE는 위 진행 기록의 SHA256을
따른다. 동일 알고리즘 CPU849.07→GPU5448.07FPS(독립3회 중앙값), NET10/11각465검사,
기존 이미지136/136일치, 산출물 수명13검사 통과다. 수치의 조건과 한계는 위 기록을 따른다.

최신 기본 NativeAOT는 **DE068949**이며 [실행 파일](../samples/fba/artifacts/erd_layer_lab/net11.0-windows7.0/erd_layer_lab.exe)에 반영했다.1200테이블/MSAA4/.NET11 RC3/Speed의 독립3회 평균 중앙값은 카메라1093.47FPS,엔티티 동시 이동1044.80FPS,단일 실행 평균 최고1180.66FPS다. 두 시나리오 모두3/3회1000이상이다. 모든 프레임1ms보장은 아니다. GPU NET10/11각438·직접414·상태 패널 각9·동일 표시 창 런타임12/12·현재 자원 관측2/2를 통과했다. 설계,재현 명령,원시 측정과 한계는 [최종 결과](dynamic-3d-layers.ko.md#최종-검증-결과--2026-11-03)를 따른다. 아래의 이전 단계별 수치는 과거 기록이다.

추가 빌드 옵션을 잃던 공통 실행기의 PowerShell자동 변수 충돌을 수정했다(`InputArguments`,회귀7/7). Native 속도 프로필은 `-ExtraArgs @('-p:OptimizationPreference=Speed')`로 명시한다. 표본 수1의 불투명 경계도 실제 깊이 coverage를 사용하며,부드러운 곡선에는 기본4x를 사용한다. 레이어별 비트맵 캐시로 대체한 구조가 아니다.
## 목적

이 문서는 Duxel 코드를 작성할 때 가장 먼저 읽는 기준 문서다.

이 문서는 다음 두 대상을 동시에 위해 작성한다.

- Duxel 코드를 생성하거나 수정하기 전에 단일 고밀도 기준 문서가 필요한 코딩 에이전트
- 현재 Duxel 공개 표면을 빠르게 파악하고 싶은 개발자

이 문서는 에이전트가 소스 트리를 먼저 뒤지지 않고도 일반적인 Duxel 코드를 생성할 수 있을 정도의 정보 밀도를 목표로 한다.

일반적인 앱 코드, 샘플 코드, DSL 예제, 재사용 위젯은 우선 이 문서를 기준으로 작성한다. 렌더러 내부, 플랫폼 내부, 깊은 구현 검증이 필요한 경우에만 소스 읽기를 추가한다.

이 문서와 현재 소스가 충돌하면 현재 소스를 우선하고, 같은 변경 집합에서 이 문서도 함께 갱신한다.

## 최신화 규칙

이 파일은 살아 있는 기준 문서로 취급한다.

프로젝트 구조, 샘플 배치, 공개 API 방향, 실행 흐름, 문서 우선순위가 바뀌면 구현과 함께 이 문서도 갱신한다.

문서 상단의 `Last synced` 날짜는 의미 있는 갱신이 있을 때마다 반드시 함께 갱신한다.

## 동적 3D 레이어

`UiLayerOptions.Scene3D = new UiLayer3D(model * view * projection)`은 동적 레이어 도형을 실제 Vulkan 깊이 검사 경로로 투영한다. 여러 레이어가 렌더링 범위 하나와 swapchain 깊이 attachment를 공유하며 레이어별 offscreen 텍스처를 만들지 않는다. 동적 `EndLayer`는 프레임 소유 지오메트리를 복사·인덱스 재배치 없이 이전하고 id별 builder를 재사용한다. 일반 2D draw list의 순서는 유지한다. 좌표·clip·알파·수명·검증·측정 계약은 [동적 3D 레이어](dynamic-3d-layers.ko.md)와 `samples/fba/dynamic_3d_layers_bench.cs`를 참고한다.

`ContentVersion`은 버전이 바뀔 때까지 지오메트리를 유지하며 카메라 행렬은 독립적으로 갱신한다. `UiLayer3D.Viewport`와 `DepthScopeId`는 같은 렌더러에서 독립 위젯 카메라를 지원하고, `TryMapScreenPoint`는 포인터 광선을 원본 평면의 픽셀 좌표로 변환한다. `samples/fba/dynamic_layer_camera_fba.cs`를 참고한다. 해석적 벡터 경계는 확대 시 최종 해상도로 래스터화한다. `IPlatformVectorTextBackend`는 투영된 네이티브 글자의 윤곽 메시를 제공하며, 일반 2D 네이티브 텍스트와 래스터 이미지는 원본 해상도의 한계를 유지한다. `UiDrawListBuilder.GetClipRect()`는 현재 로컬 clip을 제공한다. `GetRendererFrameMetrics()`는 이전 제출 프레임의 계측값을 반환한다(Vulkan profiling을 켜야 측정된 카운터를 제공). `IsMouseDown/Clicked/Released(2)`는 가운데 버튼이며, `GetKeyModifiers()`로 Ctrl 조합을 확인한다. Vulkan은 경계가 확인된 화면 밖 3D 명령의 그리기를 생략하면서 GPU 데이터를 유지한다. 대형 편집 문서와 필요할 때만 렌더링하는 동작 비교는 [ERD 레이어 실험실](erd-layer-lab.ko.md)과 `scripts/measure-erd-layers.ps1`을 참고한다.

## Duxel이 무엇인가

Duxel은 .NET 8, .NET 9 및 .NET 10 기반 즉시 모드 GUI 프레임워크다.

현재 구현 방향:

- 렌더러: Vulkan
- 주 플랫폼: Windows 네이티브 백엔드
- 런타임 스타일: NativeAOT 친화
- 주요 UI API: `UiImmediateContext`
- 앱 수명주기 진입점: `UiScreen.Render(UiImmediateContext ui)`
- 패키지 표면:
  - `Duxel.App`
  - `Duxel.Windows.App`

두 패키지는 `net8.0`, `net9.0`, `net10.0` 런타임 자산을 함께 제공한다. 파일 기반 앱 기능에는 .NET 10 SDK가 필요하므로 FBA 샘플은 `net10.0`을 유지하며, 일반 프로젝트 사용자는 세 프레임워크 중 하나를 대상으로 할 수 있다.

이는 공개 `0.2.12-preview` 패키지 계약이다. `Duxel.App`과 `Duxel.Windows.App`은 `net8.0`, `net9.0`, `net10.0` 자산을 제공하며 FBA는 계속 .NET 10 SDK 워크플로를 사용한다.

Windows 앱을 생성할 때 소비자 실행 프로젝트는 플랫폼 분석이 명확하도록 `net8.0-windows7.0`, `net9.0-windows7.0`, `net10.0-windows7.0` 중 해당 버전을 우선한다. Duxel 패키지 프로젝트 자체는 의도적으로 이식 가능한 `net8.0`, `net9.0`, `net10.0` TFM을 사용한다.

`Duxel.Windows.App`은 Windows 단일 패키지 진입점이며 `Duxel.App`에 의존한다. 패키지 의존성은 analyzer asset 전파를 허용해 Windows 패키지 사용자도 통합 `Duxel.Core.Dsl.Generator`를 사용할 수 있어야 한다.

## 소스 없이 생성하는 기본 규칙

코딩 에이전트가 Duxel 코드 생성을 요청받았을 때는 다음 순서를 따른다.

1. 이 문서에서 시작한다.
2. 이 문서의 템플릿 중 가장 가까운 것을 고른다.
3. 이 문서의 기능 맵과 Cookbook 섹션을 사용한다.
4. 여기서 다루지 않는 내부 구현 작업일 때만 소스 코드를 읽는다.

대부분의 사용자-facing Duxel 작업은 이 문서만으로 충분해야 한다.

## 기준 정보 읽기 순서

권위 있는 기준이 필요할 때는 다음 순서로 읽는다.

1. 이 문서: `docs/duxel-agent-reference.ko.md`
2. 영문 대응 문서: `docs/duxel-agent-reference.md`
3. `README.ko.md`
4. 작업별 문서:
   - `docs/extended-title-bar-guide.ko.md`
   - `docs/fba-reference-guide.ko.md`
   - `docs/fba-run-samples.ko.md`
   - `docs/getting-started-fba.ko.md`
   - `docs/custom-widgets.ko.md`
   - `docs/ui-dsl.ko.md`
5. 설계 기준:
   - `docs/design.ko.md`
6. 더 깊은 검증이 필요할 때만 `src/` 아래 현재 소스 코드

## 저장소 맵

중요한 최상위 영역:

- `src/`
  - `Duxel.Core` — 즉시 모드 코어, 위젯, 레이아웃, 텍스트, 상태, 드로우 데이터, DSL 런타임
  - `Duxel.App` — 앱 파사드와 공용 런타임 연결
  - `Duxel.Windows.App` — Windows 러너 엔트리 표면
  - `Duxel.Platform.Windows` — Windows 플랫폼 백엔드
  - `Duxel.Vulkan` — Vulkan 렌더러 백엔드
- `samples/`
  - `samples/Duxel.Sample` — 프로젝트 스타일 참조 앱
  - `samples/fba/*.cs` — 파일 기반 앱 샘플
- `docs/` — 사용자 및 기여자 문서
- `global.json` — 설치된 .NET SDK `11.0.100-rc.3.26522.148`을 선택하고 사전 릴리스 및 patch roll-forward 허용
- `run-fba.ps1` — 로컬 소스 검증용 FBA 실행 경로

## 아키텍처 경계

기능을 추가하거나 수정할 때는 다음 경계를 유지한다.

- `Duxel.Core`
  - 즉시 모드 동작, 레이아웃, 위젯, 드로우리스트 생성, 상태, 텍스트 API 소유
- `Duxel.Platform.Windows`
  - 입력, 클립보드, IME/TSF 연결, 텍스트 백엔드 연결, 네이티브 윈도우 등 Windows 전용 동작 소유
- `Duxel.Vulkan`
  - 렌더링, GPU 리소스, 스왑체인, 제출 흐름 소유
- `Duxel.App`, `Duxel.Windows.App`
  - 앱 부트스트랩, 개발자 진입 흐름, 옵션 검증, 런타임 연결 소유

이 경계는 임의로 합치지 않는다.

### Windows 대화형 리사이즈 흐름

Win32 이동/크기 조절 모달 루프 동안 `Duxel.App`은 `IPlatformBackend.IsInteractingResize`를 연속 작업으로 취급한다. `Duxel.Platform.Windows`는 `WM_SIZE`가 확정한 실제 클라이언트 치수를 캐시하고 이때 크기 변경 sequence를 증가시킨다. `WM_SIZING`은 프레임을 요청하며 프레임버퍼 크기를 예측하거나 GPU를 기다리지 않는다. 독립 렌더 스레드와 invalidation callback이 연결된 호스트는 확정된 `WM_SIZE`에서 해당 크기의 프레임 제출과 동기화한다. 완료를 기다리는 동안 `QS_SENDMESSAGE`/`PM_QS_SENDMESSAGE`로 WSI 동기 메시지를 처리하며 종료·렌더 오류 시 취소한다. 크기 변경 종료 직후에도 최신 확정 치수를 읽는다. [수정 원리·실제 사용자 확인·남은 안정성 검사](windows-resize-coherence.ko.md)를 참조한다.

확정 프레임버퍼와 치수가 다른 오래된 draw data는 거부하고 새 레이아웃으로 재시도한다. 일치하는 프레임은 크기 변경 중에도 확정 픽셀 크기로 swapchain을 재생성하며, clip 비례 확대를 실제 해상도 재구성의 대체물로 쓰지 않는다. WSI 진입 전 그래픽 완료를0시간 제한으로 확인하고, 준비되지 않은 재생성은 idle skip 중에도 대기 작업으로 유지한다. 이전 views/MSAA/depth 타깃은 마지막 그래픽 제출 완료 뒤 회수한다. 이전 swapchain·semaphore는 지원 장치의 표시 fence 또는 이식 가능한 새 이미지 재획득 증명으로 회수한다. 이전 핸들을 `VkSwapchainCreateInfoKHR.oldSwapchain`에 전달하고 관련 없는 렌더러 자원을 보존한다.

MSAA 용량은 크기 변경 중 충분하면 재사용하고 종료 후 정확한 크기로 줄인다. 픽셀 해상도·샘플 수를 낮추지 않는다. VSync ON은 FIFO, VSync off는 Immediate 우선이다. 고정240 FPS 제한을 추가하지 않는다. RC3 단계 측정·942개 GPU 이미지 바이트 동치와 메모리/화면 검증의 한계는 [Panel·연속 크기 변경 근거](panels-root-content.ko.md)를 따른다.

Framebuffer 크기가 0보다 큰 유효 프레임은 draw list에 geometry가 없어도 렌더링할 수 있어야 한다. 렌더러는 clear를 기록하고 submit/present까지 수행해야 빈 첫 화면이 표시될 수 있다. 이는 일반 idle-frame skip을 끄는 것이 아니다. 변경이 없는 프레임은 렌더러에 제출되기 전에 앱 런타임이 계속 건너뛴다.

## 주요 공개 진입점

다운스트림 개발자가 가장 많이 만지는 표면:

- `UiScreen`
- `UiImmediateContext`
- 선언형 UI API:
  - `IUiView`
  - `DuxelView`
  - `Dux`
  - `DuxelView.Display`
- `DuxelAppOptions`
- `DuxelWindowOptions`
- `DuxelRendererOptions`
- `DuxelFontOptions`
- `DuxelFrameOptions`
- `DuxelDebugOptions`
- 컴파일된 디자인 API:
  - `UiCompiledDesign`
  - `IUiDesign`
  - `UiWindows11Design`
- `DuxelWindowsApp.Run(...)`
- 커스텀 위젯 API:
  - `IUiCustomWidget`
  - `MarkdownEditorWidget`
  - `MarkdownViewerWidget`

## 옵션 참조와 기본값

아래 기본값은 일반적인 Duxel 코드 생성 시 그대로 기대해도 되는 값이다.

### `DuxelAppOptions`

| 속성 | 형식 | 기본값 | 용도 |
|---|---|---|---|
| `Window` | `DuxelWindowOptions` | `new()` | 창 제목, 크기, VSync |
| `Renderer` | `DuxelRendererOptions` | `new()` | 검증 레이어, 프로필, AA, 텍스트 렌더링 |
| `Font` | `DuxelFontOptions` | `new()` | 폰트 경로, 아틀라스 크기, 시작 글리프 전략 |
| `Frame` | `DuxelFrameOptions` | `new()` | 줄 높이, idle 프레임 스킵, 폰트 재빌드 주기 |
| `Debug` | `DuxelDebugOptions` | `new()` | 로그와 프레임 캡처 |
| `Theme` | `UiTheme` | `UiCompiledDesign.Default.Theme` | fallback 테마 프리셋 |
| `Design` | `UiCompiledDesign?` | `null` | 선택적 컴파일 디자인. 지정하면 플랫폼 기본 테마 추적보다 우선 |
| `FontTextureId` | `UiTextureId` | `new(1)` | 폰트 텍스처 슬롯 |
| `WhiteTextureId` | `UiTextureId` | `new(2)` | 화이트 텍스처 슬롯 |
| `Screen` | `UiScreen` | (필수) | 즉시 모드 앱 진입점 |
| `Clipboard` | `IUiClipboard?` | `null` | 직접 클립보드 주입 |
| `ImageDecoder` | `IUiImageDecoder?` | `null` | 커스텀 이미지 디코드 경로 |
| `KeyRepeatSettingsProvider` | `IKeyRepeatSettingsProvider?` | `null` | 커스텀 키 반복 타이밍 |
| `ClipboardFactory` | `Func<IPlatformBackend, IUiClipboard?>?` | `null` | 플랫폼별 클립보드 팩토리 |

### 컴파일된 디자인

기본적으로 Windows 앱은 현재 OS 앱 테마에 따라 `UiCompiledDesign.Windows11` 또는 `UiCompiledDesign.Windows11Dark`로 해석되며, Windows가 앱 테마 변경을 알리면 활성 디자인도 갱신된다. 컨트롤의 시각적 모양을 런타임 테마 파싱이 아니라 코드 또는 소스 생성으로 고정해야 할 때는 `DuxelWindowsApp.Run<TDesign>(...)`, `DuxelApp.Options<TDesign>(...)` 또는 `DuxelAppOptions.Design`을 사용한다.

```csharp
DuxelWindowsApp.Run<UiWindows11Design>(
    new ProductScreen(),
    title: "Windows 11 styled Duxel",
    width: 980,
    height: 700);
```

커스텀 컴파일 타임 디자인은 `IUiDesign`을 구현하고 생성된/정적 값을 전달한다.

```csharp
public readonly struct ProductDesign : IUiDesign
{
    public static UiCompiledDesign Create()
        => UiCompiledDesign.Windows11 with
        {
            Theme = UiTheme.GitHubDark,
            Tokens = UiDesignTokens.Windows11 with { ControlCornerRadius = 6f }
        };
}

DuxelWindowsApp.Run<ProductDesign>(
    Dux.App(new ProductScreen()),
    title: "Product Surface");
```

`UiTheme`은 색상 팔레트, `UiStyle`은 레이아웃 치수, `UiDesignTokens`는 코너 반경, 보더 두께, 눌림 오프셋, 포커스 링 두께 같은 위젯 모양을 담당한다. 기존 런타임 테마 변경은 색상만 갱신하고, 컴파일된 디자인 토큰은 활성 외형 계약으로 유지된다.

`Design`이 `null`이고 `Theme`을 기본값 그대로 두면 Duxel은 플랫폼 테마 공급자를 따른다. 명시적 `Theme` 또는 `Design` 값은 앱이 직접 선택한 값으로 보고 OS 테마 변경으로 덮어쓰지 않는다.

다음 단계 숙제는 컨트롤 타입별 렌더링 전략을 교체하는 skin layer다. 현재는 테마, 토큰, modifier, 커스텀 위젯으로 상당 부분 조정할 수 있지만, `Button`, `TextField`, `Segmented`, `Scrollbar` 같은 기본 컨트롤의 렌더링 정책 자체를 디자인 단위로 갈아끼우는 공식 계층은 아직 남아 있다. 다음 세션은 [Declarative Control Skin Roadmap](declarative-control-skin-roadmap.ko.md)에서 시작한다.

### 선언형 UI

C#에서 SwiftUI/Compose 스타일 조합을 사용할 때는 그룹화된 `DuxelView` 팩터리로 생성한 `IUiView` 노드를 사용한다. 점 자동완성에서 `DuxelView.Layout`, `DuxelView.Controls`, `DuxelView.Text`, `DuxelView.Display`, `DuxelView.Menus`, `DuxelView.Windows`가 먼저 드러나므로 모든 헬퍼를 한 클래스에 몰아넣지 않고도 자연스럽게 화면을 작성할 수 있다. 짧은 앱 코드와 샘플에는 같은 표면을 제공하는 `Dux.*` 별칭을 사용할 수 있다.

```csharp
var running = Dux.State(true);
var project = Dux.State("Duxel Control Surface");
var progress = Dux.State(0.62f);
var channel = Dux.State(ReleaseChannel.Preview);
var tabItems = new[] { "Layout", "Theme", "Windows" };

var screen = Dux.Screen(
    Dux.Group(
        Dux.MainMenuBar(
            Dux.Menu(
                "File",
                Dux.MenuItem("Reset", () => progress.Value = 0f),
                Dux.MenuItem("Running", () => running.Value = !running.Value, selected: () => running.Value))),
            Dux.VStack(
                10f,
                Dux.Header(
                    () => project.Value,
                    Dux.Meta("Preview"),
                    Dux.Meta(() => running.Value ? "Running" : "Paused", UiTextTone.Success)),
                Dux.Tabs(
                    "dashboard-tabs",
                    Dux.Tab(
                        "Overview",
                        Dux.Section(
                            "Controls",
                            Dux.Form(
                                Dux.Field("Project", Dux.TextField("project", project)),
                                Dux.Field("Channel", Dux.EnumSegmented<ReleaseChannel>("channel", channel)),
                                Dux.Field("Running", Dux.Toggle("running", running)),
                                Dux.Field("Progress", Dux.Slider("progress", progress, 0f, 1f).ItemWidth(360f))))),
                    Dux.Tab(
                        "Tasks",
                        Dux.List(
                            "task-list",
                            tabItems,
                            item => Dux.Text(item),
                            new UiVector2(0f, 120f),
                            border: true))))));

enum ReleaseChannel
{
    Stable,
    Preview,
    Canary
}
```

`UiState<T>`는 앱 코드용 상태 홀더다. 컨트롤에는 `UiBinding<T>`로 변환되므로 `Dux.Checkbox("Running", running)`, `Dux.TextField("project", project)`처럼 짧게 쓸 수 있다. 선언형 alias인 `Dux.TextField(...)`, `Dux.TextArea(...)`, `Dux.NumberField(...)`, `Dux.Slider(...)`는 안정적인 control id를 받고 즉시 모드 `##` label을 앱 코드에서 숨긴다. 명시적인 상태 변경은 `state.Set(value)`와 `state.Update(current => next)`를 사용한다. 상태가 이미 다른 곳에 있으면 `Dux.Bind(get, set)`을 사용하고, 쓰기 가능한 파생 바인딩은 `UiBinding<T>.Map(...)`으로 만든다. 동적 텍스트와 조건부 view는 `Func<T>`를 받아 렌더링 중 값을 다시 평가한다. 재사용 가능한 view 객체는 `Build()`를 구현하는 `UiComponent`를 기준으로 만든다.

앱 규모 화면에서는 하나의 큰 렌더 메서드보다 작은 `UiComponent` 클래스를 선호한다. 컴포넌트는 생성자로 상태를 받거나 보유하고, `Build()`에서 `IUiView`를 반환한다. 권장 시작 형태는 `DuxelWindowsApp.Run<ProductDesign>(Dux.App(new ProductScreen(...)))`이며, 첫 프레임 전에 컴파일된 디자인과 루트 컴포넌트를 함께 묶는다. `Dux.App(...)`은 선언형 root view를 런타임이 요구하는 `UiScreen`으로 감싸는 semantic alias다.

공통 제품 화면 골격에는 `Dux.AppShell(...)`을 사용한다. AppShell은 선택적인 메뉴, 테마가 적용된 sidebar, 동적 header, meta 텍스트, command 콘텐츠, 선택된 page body를 한 번에 선언한다. 내비게이션 항목은 `Dux.NavItem(...)`으로 만든다. 활성 page는 일반적인 `UiState<int>`/`UiBinding<int>`로도 제어할 수 있지만, enum 기반 `UiState<T>`를 쓰면 앱 내비게이션이 타입 안전해진다.

```csharp
Dux.AppShell(
    () => projectName.Value,
    selectedPage,
    [
        Dux.NavItem(ProjectPage.Overview, "Overview", new OverviewPage(state), "Run controls"),
        Dux.NavItem(ProjectPage.Tasks, "Tasks", new TasksPage(state), () => $"{tasks.Count} active rows"),
        Dux.NavItem(ProjectPage.Notes, "Notes", new NotesPage(state), "Composition notes")
    ],
    new UiAppShellOptions(WindowTitle: "Workspace", SidebarTitle: "Workspace"),
    Dux.Meta(() => $"Runs {runs.Value}"),
    Dux.Meta(() => channel.Value));
```

고수준 레이아웃 헬퍼는 사용자가 화면에서 보는 제품 구조 그대로 작성하게 해준다. 제목 있는 영역은 `Dux.Section(...)`, 라벨이 붙은 설정은 `Dux.Form(...)`과 `Dux.Field(...)`, 반복 카드/메트릭 배치는 `Dux.Grid(...)`, 반복 행은 `Dux.List(...)`, 탭 흐름은 `Dux.Tabs(...)` / `Dux.Tab(...)`, 계층 콘텐츠는 `Dux.Tree(...)`, 구조화 데이터는 `Dux.Table(...)` / `Dux.TableColumn(...)`을 사용한다. 동적 목록과 grid에는 `Dux.List("tasks", tasks, task => task.Id, task => Dux.StatusRow(task.Name, task.Owner, () => task.Progress))` 또는 `Dux.Grid(tasks, task => task.Id, task => new TaskTile(task))` 같은 keyed overload를 우선 사용한다. 개별 view fragment에는 `Dux.Key(...)`와 `.Key(...)`로 같은 안정적인 ID 스코프를 줄 수 있다.

선언형 제어 흐름은 view tree 안에 유지한다. 한쪽 조건부 콘텐츠에는 `Dux.When(...)`과 `Dux.Unless(...)`, 상태별 fragment에는 `Dux.Switch(...)`와 `Dux.Case(...)`, 표시 행에 인덱스가 필요한 반복에는 `Dux.ForEachIndexed(...)`를 사용한다. 이 헬퍼들은 동적 값을 렌더링 중 평가하므로 앱 코드는 상태 변경을 `UiState<T>`에 두고, view는 현재 화면을 설명하는 형태로 유지할 수 있다.

디스플레이 헬퍼는 제품 UI에서 자주 쓰는 조각을 짧게 만든다. sidebar 기반 제품 shell에는 `Dux.AppShell(...)`, 제품 제목과 meta 행에는 `Dux.Header(...)`, padding이 있는 테마 패널에는 `Dux.Surface(...)` 또는 `Dux.Card(...)`, 라벨이 붙은 설정에는 `Dux.SettingsGroup(...)`과 `Dux.Setting(...)`, 의미 있는 상태/안내 메시지에는 `Dux.Callout(...)`, 비어 있거나 비활성화된 상태에는 `Dux.EmptyState(...)`, 조밀한 명령 행에는 `Dux.Toolbar(...)`, 조밀한 상태 행에는 `Dux.MetaBar(...)`와 `Dux.Meta(...)`, 세부 key/value 목록에는 `Dux.PropertyList(...)`와 `Dux.Property(...)`, 선택 가능한 progress 행에는 `Dux.StatusRow(...)`, 의미가 있는 상태 pill에는 `Dux.Badge(...)`와 `UiBadgeTone.Neutral`, `Accent`, `Success`, `Warning`, `Danger`, 대시보드 수치에는 `Dux.MetricCard(...)` 또는 `Dux.Metric(...)`을 사용한다. 명령의 제품 action hierarchy는 버튼 색상을 직접 push하지 않고 `Dux.PrimaryButton(...)`, `Dux.DangerButton(...)`, `.ButtonRole(...)`로 표현한다. Surface와 child 콘텐츠는 기본 Duxel 스크롤바 동작을 그대로 유지한다. 그룹화된 팩터리 표면은 `DuxelView.Display`다.

```csharp
Dux.Grid(
    3,
    Dux.MetricCard("Runs", () => runs.Value.ToString(), "Queued sessions"),
    Dux.MetricCard("Progress", () => $"{progress.Value:P0}", "Current workflow"),
    Dux.MetricCard(
        "Priority",
        () => priority.Value.ToString(),
        () => priority.Value >= 4 ? "Needs attention" : "Normal cadence",
        new UiMetricCardOptions(ValueTone: UiTextTone.Warning)));
```

제품 명령은 action role, tooltip, enabled 상태가 중요하면 `Dux.CommandBar(...)`를 우선 사용한다. 명령 의미를 값으로 유지하고 버튼의 실제 스타일은 컴파일된 design이 담당하게 한다.

```csharp
Dux.CommandBar(
    Dux.Command("Queue", QueueRun, UiButtonRole.Primary, tooltip: () => "Queue one run"),
    Dux.Command("Reset", Reset, enabled: () => canReset.Value));
```

설정 화면은 `Dux.SettingsGroup(...)`을 사용하면 각 행이 label, description, control을 함께 소유한다.

```csharp
Dux.SettingsGroup(
    Dux.Setting("Project", Dux.TextField("project", project), "Shown in the workspace shell."),
    Dux.Setting("Channel", Dux.EnumSegmented<ReleaseChannel>("channel", channel)));
```

선택 컨트롤은 `Dux.Combo(...)`, `Dux.ListBox(...)`, `Dux.RadioButton(...)`, `Dux.Selectable(...)`뿐 아니라 `UiBinding<int>` 또는 typed `UiBinding<T>`로 연결되는 조밀한 모드/채널 picker용 `Dux.Segmented(...)`를 제공한다. typed 선택지는 `Dux.Choice(value, label)`로 만들고, 상태가 enum이면 `Dux.EnumSegmented<TEnum>(...)`으로 바로 연결한다.

`UiModifier`와 fluent extension은 지역적인 컴파일 타임 작성 시각 규칙을 제공한다. 자주 쓰는 modifier는 `.FontSize(...)`, `.Foreground(...)`, `.Tone(...)`, `.Accent()`, `.Success()`, `.Warning()`, `.Danger()`, `.Muted()`, `.Title(...)`, `.Subtitle(...)`, `.Caption(...)`, `.ItemWidth(...)`, `.FillWidth()`, `.Padding(...)`, `.Frame(...)`, `.FillFrameWidth(...)`, `.Width(...)`, `.Height(...)`, `.Background(...)`, `.Border(...)`, `.CornerRadius(...)`, `.Tooltip(...)`, `.VisibleIf(...)`, `.Disabled(...)`, `.StyleColor(...)`, `.StyleVar(...)` 등이다. 텍스트 계층은 `Dux.Title(...)`, `Dux.Subtitle(...)`, `Dux.Caption(...)` 또는 재사용 가능한 `UiTextStyle` 값으로 작성할 수 있다. semantic 텍스트 색조는 `UiTextTone`을 사용하므로 앱 코드는 색상 상수를 들고 다니지 않고 `.Success()`나 `.Warning()`처럼 작성할 수 있다. view가 런타임 theme slot에만 의존하지 않고 자기 렌더링 모양을 직접 가져야 할 때는 shape modifier를 사용한다.

```csharp
Dux.Callout(
    "Run Status",
    () => status.Value,
    options: new UiCalloutOptions(Tone: UiTextTone.Success, Height: 88f));
```

`.Panel(...)`은 기본 제공 Windows 11 스타일 surface modifier다. 내부적으로 `UiPanelStyle`을 사용하며 기본값은 `UiStyleColor.FrameBg` / `UiStyleColor.Border`, `UiDesignToken.ControlCornerRadius`, 14 px padding, 현재 콘텐츠 폭 채우기다. fluent modifier보다 factory 호출이 더 잘 읽히면 `Dux.Panel(content, ...)`을 사용한다. 재사용 가능한 컴파일된 스타일은 일반 C# 타입이다. 같은 시각 규칙을 SwiftUI의 `ViewModifier`처럼 이름 붙이고 테스트하고 재사용하려면 `IUiViewStyle`을 구현한다. 스타일은 `UiStyleColor`와 `UiDesignToken` 슬롯을 사용할 수 있으므로, 모양은 C#에서 작성하고 색상/반경은 활성 컴파일 디자인에서 가져올 수 있다.

```csharp
readonly record struct DashboardPanelStyle(float Height = 0f) : IUiViewStyle
{
    public IUiView Apply(IUiView view)
        => view
            .Padding(14f)
            .Background(UiStyleColor.FrameBg)
            .Border(UiStyleColor.Border)
            .CornerRadius(UiDesignToken.ControlCornerRadius)
            .FillFrameWidth(Height);
}

Dux.TextWrapped(() => status.Value)
    .Style(new DashboardPanelStyle(116f));
```

`Frame`은 안정적인 레이아웃 크기를 제공하고, `FillFrameWidth`는 현재 콘텐츠 폭을 채우며, `Background`, `Border`, `CornerRadius`는 컴파일 타임에 작성된 view shape를 설명한다. `Background`와 `Border`는 명시적 `UiColor` 값 또는 활성 디자인의 semantic `UiStyleColor` 슬롯을 받을 수 있다. `CornerRadius`는 고정 float 또는 semantic `UiDesignToken`을 받을 수 있다. shape modifier 앞에 놓인 `Padding`은 장식된 view의 내부 padding으로 흡수되므로 왼쪽과 오른쪽 padding이 모두 shape 측정에 참여한다. `IUiViewStyle`은 같은 modifier들을 컴파일 타임에 작성된 스타일 타입으로 합성한다. `.Style<TStyle>()`은 기본 struct style을 적용하고, `.Style(style)`은 설정된 style 인스턴스를 적용한다. 반복 동적 콘텐츠에서는 keyed list/grid overload를 계속 사용하거나 장식된 fragment에 `.Key(...)`를 붙여 local interaction state를 안정적으로 유지한다. 선언형 메뉴 조합은 `Dux.MainMenuBar(...)`, `Dux.Menu(...)`, `Dux.MenuItem(...)`로 작성한다. 직접 `UiImmediateContext` 접근이 필요한 커스텀 선언형 노드는 `DuxelView.Custom(ui => ...)`를 확장 지점으로 사용한다.

### `DuxelWindowOptions`

| 속성 | 기본값 |
|---|---|
| `Width` | `1280` |
| `Height` | `720` |
| `Title` | `"Duxel"` |
| `VSync` | `true` |
| `IconPath` | `null` (Windows 시스템 기본 애플리케이션 아이콘) |
| `IntegrateSystemChrome` | `true` |
| `TitleBarMode` | `DuxelTitleBarMode.Default` |
| `UseDuxelTitleBar` | `true` |
| `DuxelTitleBarHeight` | `48f` |

`IntegrateSystemChrome`은 활성 시작 테마/디자인에서 Windows 11 DWM 캡션 색상, 텍스트 색상, 테두리 색상, 라운드 코너, 다크 모드 속성을 적용한다. 앱이 기본 플랫폼 추적 디자인을 사용할 때는 Windows `WM_SETTINGCHANGE` 테마 알림으로 Duxel 테마와 렌더러 clear color가 런타임에 갱신된다.

Windows는 `WS_VISIBLE` 없이 네이티브 HWND를 생성하고 설정한다. `WindowCreated`는 설정과 진단에 사용할 수 있는 유효한 숨김 HWND를 계속 전달하지만 애플리케이션이 직접 표시하면 안 된다. 렌더러가 1번 프레임을 성공적으로 present한 뒤 `IPlatformBackend.NotifyFirstFramePresented()`가 전용 창 메시지를 게시하고, Windows UI 스레드가 `ShowWindow`와 `UpdateWindow`를 호출한다. 따라서 Vulkan 장치·파이프라인·폰트·첫 화면 준비 시간이 빈 네이티브 창으로 노출되지 않는다.

`UseDuxelTitleBar`는 기본적으로 켜져 있으므로, 앱이 명시적으로 `UseDuxelTitleBar = false`를 지정하지 않는 한 Windows 앱은 네이티브 캡션을 제거하고 Vulkan surface 안에 Duxel 소유 타이틀바를 렌더링한다. 앱 런타임은 사용자 `UiScreen`을 감싸고, 상단 viewport inset을 예약하며, 앱 아이콘/제목/최소화/최대화/닫기 버튼을 그리고, 창 이동/최소화/최대화/닫기 명령은 `IWindowChromeController`를 통해 플랫폼에 위임한다. 이 크롬은 라이브러리 소유이므로 애플리케이션 화면이 버튼을 직접 그리거나 상태를 동기화하지 않는다. 최대화 버튼은 매 프레임 `IWindowChromeController.IsMaximized`를 읽어 복원 상태에서는 단일 사각형, 최대화 상태에서는 겹친 두 사각형 복원 아이콘을 표시하고 창이 복원되면 다시 최대화 아이콘으로 돌아간다.

`TitleBarMode`는 `System`, `Duxel`, `ExtendedContent`를 명시적으로 선택한다. 기본값 `Default`는 기존 소스 호환성을 위해 `UseDuxelTitleBar`에서 실제 모드를 결정하며, 명시한 비기본 모드가 우선한다. `ExtendedContent`는 전체 Vulkan 클라이언트 영역을 `(0, 0)`부터 사용할 수 있게 한다. Duxel은 불투명 Vulkan surface 위에 보이는 캡션 아이콘을 그리고 Windows/DWM은 네이티브 버튼 히트 코드와 명령, 시스템 메뉴, 리사이즈, 최대화, Snap Layout 계약을 유지한다.

`ExtendedContent`는 FBA 샘플에만 구현된 동작이 아니라 라이브러리 기능이다. 애플리케이션이 사용하는 공개 표면은 다음과 같다.

이는 현재 저장소 소스/로컬 패키지 계약이다. 해당 API가 포함된 정확한 패키지 버전을 게시하고 검증하기 전에는 공개 NuGet 버전이 지원한다고 선언하지 않는다.

| 공개 라이브러리 API | 역할 |
|---|---|
| `DuxelTitleBarMode.ExtendedContent` | 라이브러리 소유 확장 클라이언트/프레임 경로 활성화 |
| `ui.TryGetWindowIcon(out UiImageTexture)` | 네이티브 창과 작업 표시줄이 이미 사용하는 유효 아이콘 반환 |
| `ui.TryGetCaptionButtonBounds(out UiRect)` | `Y = 0`과 `DuxelTitleBarHeight`를 사용하는 오른쪽 고정 Duxel 논리 캡션 묶음 반환 |
| `ui.SetTitleBarDragRegions(ReadOnlySpan<UiRect>)` | 전달한 비상호작용 구간을 Windows 백엔드의 네이티브 캡션 히트 영역으로 위임 |

라이브러리는 DWM 프레임 확장, 공통 `DuxelCaptionButtonRenderer`, 네이티브 캡션 히트 코드, Snap Layout 호환성, 비클라이언트 호버/누름 상태, 시스템 스타일/메뉴, DPI 변환, 리사이즈 테두리, 최대화 모니터 작업영역 크기를 담당한다. 따라서 `Duxel`과 `ExtendedContent`는 같은 48픽셀 버튼 위치와 아이콘 기하를 사용한다. 공개 버튼 묶음은 복원·최대화 모두 논리 `Y = 0`과 `DuxelTitleBarHeight`를 유지하며, 최대화된 DWM 메타데이터의 `Y = -8` 같은 값이나 네이티브 30픽셀 높이를 Duxel 시각 위치에 사용하지 않는다. 애플리케이션 코드는 아이콘, 탭, 검색창, 메뉴, 제목 텍스트, 빈 드래그 구간의 시각 배치만 담당한다. `samples/fba/extended_title_bar_fba.cs`에서 `ExtendedTitleBarScreen`은 탭 모양 Home/Documents 컨트롤로 이 공개 애플리케이션 계약을 시연하며 빈 영역의 가운데 안내 문구는 의도된 샘플 UI다. `ExtendedTitleBarDiagnostics`는 검증 전용 P/Invoke 코드다. 코딩 에이전트는 진단 코드를 제품 구현으로 복제하지 말고 공개 API를 사용한다. Win32 히트 테스트 통과만으로는 부족하므로 버튼 픽셀과 창 아이콘 우클릭 시스템 메뉴를 육안 확인해야 한다. 요청이 일반 Duxel 크롬만 필요하면 `DuxelTitleBarMode.Duxel`, 애플리케이션 UI가 `y = 0`을 차지해야 할 때만 `ExtendedContent`를 선택한다.

`ExtendedContent`에서는 Duxel 소유 버튼 묶음의 경계를 조회하고 현재 UI 프레임에서 플랫폼 드래그 영역 스냅샷을 교체한다.

```csharp
if (ui.TryGetCaptionButtonBounds(out var captionButtons))
{
    var dragLeft = 320f;
    var dragRight = captionButtons.X;
    ui.SetTitleBarDragRegions(dragRight > dragLeft
        ? [new UiRect(dragLeft, 0f, dragRight - dragLeft, 48f)]
        : []);
}
else
{
    ui.SetTitleBarDragRegions([]);
}
```

사각형은 Duxel 논리 클라이언트 좌표를 사용한다. 호출할 때마다 이전 영역 집합을 원자적으로 교체하며, `[]`를 전달하면 비운다. 탭, 버튼, 메뉴, 텍스트 입력 등 상호작용 요소는 해당 사각형 밖에 두어 히트 테스트가 `HTCLIENT`로 남게 한다. 사용할 수 있는 클라이언트 폭이 아직 없으면 `TryGetCaptionButtonBounds`가 `false`를 반환할 수 있다. 전체 레이아웃, Win32/DWM 계약, AI 검증 절차는 `docs/extended-title-bar-guide.ko.md`, 실행 가능한 기준 샘플은 `samples/fba/extended_title_bar_fba.cs`를 참고한다.

`IconData`가 `IconPath`보다 우선한다. 둘 다 지정하지 않으면 Duxel은 Win32 시스템 기본 `IDI_APPLICATION`을 창 클래스에 명시해 네이티브 창, 작업 표시줄, Alt+Tab이 실행 파일이나 아이콘 캐시에 남은 Duxel 브랜드 아이콘을 다시 선택하지 않게 한다. Duxel 소유 타이틀바는 같은 유효 `HICON`을 WIC로 RGBA 변환해 `IWindowIconProvider.GetWindowIconImage()`로 전달받으므로 별도 fallback이 아니라 실제 창 아이콘과 같은 아이콘을 표시한다. 제목은 기본적으로 아이콘 다음 위치에 왼쪽 정렬한다. 아이콘, 제목 또는 타이틀바의 버튼이 아닌 영역을 오른쪽 클릭하면 네이티브 창 시스템 메뉴가 열리며, 창 드래그, 타이틀 영역 더블클릭 최대화/복원, Alt+Space도 유지된다. `Duxel.Windows.App`은 더 이상 브랜드 `ApplicationIcon`을 자동 주입하지 않으며, 브랜딩이 필요하면 앱의 `ApplicationIcon`, `IconPath`, `IconData`를 명시한다. 명시한 `IconPath`가 없거나 `IconData`가 올바르지 않으면 다른 아이콘으로 조용히 바꾸지 않고 시작 시 실패한다.

`ExtendedContent` 화면은 `ui.TryGetWindowIcon(out var windowIcon)`으로 이미 결정된 유효 창 아이콘 텍스처를 조회해 애플리케이션 소유 타이틀바에 그릴 수 있다. 따라서 샘플이나 앱이 별도 이미지를 다시 선택하지 않아도 콘텐츠 안의 아이콘과 작업 표시줄 아이콘이 일치한다. 아이콘 사각형을 드래그 영역으로 등록하면 네이티브 캡션 의미도 부여되며, Windows 백엔드가 그 비클라이언트 우클릭을 표준 시스템 메뉴로 연결한다. 복사 가능한 개발자 시작 코드, 전체 화면 구현, 라이브러리/샘플 책임표, 코딩 AI 완료 절차는 `docs/extended-title-bar-guide.ko.md`를 기준으로 한다.

### `DuxelRendererOptions`

| 속성 | 기본값 |
|---|---|
| `MinImageCount` | `3` |
| `EnableValidationLayers` | `Debugger.IsAttached` |
| `Profile` | `DuxelPerformanceProfile.Display` |
| `MsaaSamples` | `0` |
| `EnableGlobalStaticGeometryCache` | `true` |
| `FontLinearSampling` | `false` |
| `TextRendering` | `DuxelTextRenderingMode.DirectText` |

`DuxelTextRenderingMode.DirectText`가 기본 텍스트 경로다. Atlas 출력은 기본 시각 기준으로 쓰지 않는다. 명시적 `Atlas`는 atlas renderer A/B 프로파일링에만 사용한다. 명시적 `Auto`는 atlas 우선 렌더링을 유지하되 atlas가 따라오기 전 누락 글자만 DirectText 시각 fallback으로 즉시 보여야 하는 sample에서만 사용한다.

DirectWrite atlas glyph 경로를 수정할 때는 oversample 래스터라이즈 metric을 atlas에 넘기기 전에 논리 픽셀로 되돌려야 한다. Bitmap data는 `fontSize * oversample` 크기로 래스터라이즈할 수 있지만, glyph advance, text-run advance, offset, baseline은 downsample 뒤 logical em size 기준으로 정규화해야 한다. Cached placement metric이 바뀌면 font-atlas disk cache version도 올린다.

### `DuxelFontOptions`

| 속성 | 기본값 |
|---|---|
| `PrimaryFontPath` | Windows `segoeui.ttf` |
| `SecondaryFontPath` | Windows `malgun.ttf` |
| `SecondaryScale` | `1f` |
| `FastStartup` | `true` |
| `UseBuiltInAsciiAtStartup` | `true` |
| `StartupBuiltInScale` | `2` |
| `StartupBuiltInColumns` | `16` |
| `StartupFontSize` | `16` |
| `StartupAtlasWidth` | `512` |
| `StartupAtlasHeight` | `512` |
| `StartupPadding` | `1` |
| `StartupOversample` | `1` |
| `StartupRanges` | ASCII 범위 `0x20..0x7E` |
| `StartupGlyphs` | 비어 있음 |
| `FontSize` | `16` |
| `AtlasWidth` | `1024` |
| `AtlasHeight` | `1024` |
| `Padding` | `2` |
| `Oversample` | `2` |
| `InitialRanges` | ASCII 범위 `0x20..0x7E` |

### `DuxelFrameOptions`

| 속성 | 기본값 |
|---|---|
| `LineHeightScale` | `1.2f` |
| `PixelSnap` | `true` |
| `UseBaseline` | `true` |
| `FontRebuildMinIntervalSeconds` | `0.25` |
| `FontRebuildBatchSize` | `16` |
| `EnableIdleFrameSkip` | `true` |
| `IdleSleepMilliseconds` | `2` |
| `IdleWakeCheckMilliseconds` | `1000` |
| `IdleEventWaitMilliseconds` | `0` |
| `WindowTargetFps` | 빈 사전 |
| `IsAnimationActiveProvider` | `null` |

### `DuxelDebugOptions`

| 속성 | 기본값 |
|---|---|
| `Log` | `null` |
| `LogEveryNFrames` | `60` |
| `LogStartupTimings` | `false` |
| `CaptureOutputDirectory` | `null` |
| `CaptureFrameIndices` | 비어 있음 |

`LogStartupTimings = true`이면 시작 로그에 `PipelineCache[Hit]`, `PipelineCache[Miss]`, `PipelineCache[Incompatible]`, `PipelineCache[Saved]`, `PipelineCache[Unchanged]`, 캐시 I/O 불가 상태도 기록된다. 이 이벤트를 `StartupClear`, `FirstFramePresented`와 함께 사용해 캐시 동작과 장치 초기화·첫 화면 렌더링 비용을 구분한다.

## 바로 복붙 가능한 시작 템플릿

아래 템플릿은 그대로 코드 생성 출발점으로 사용할 수 있다.

### 최소 FBA 즉시 모드 앱

```csharp
#:property TargetFramework=net10.0
#:property platform=windows
#:package Duxel.$(platform).App@*-*

using Duxel.App;
using Duxel.Core;
using Duxel.Windows.App;

DuxelWindowsApp.Run(new DuxelAppOptions
{
  Window = new DuxelWindowOptions
  {
    Title = "Hello Duxel",
    Width = 1280,
    Height = 720,
    VSync = true,
  },
  Screen = new DemoScreen(),
});

public sealed class DemoScreen : UiScreen
{
  private int _count;
  private bool _enabled = true;
  private float _volume = 0.5f;
  private string _name = "Duxel";

  public override void Render(UiImmediateContext ui)
  {
    ui.Text("Hello from Duxel");

    if (ui.Button("Increment"))
    {
      _count++;
    }

    ui.SameLine();
    ui.Text($"Count: {_count}");

    ui.Checkbox("Enabled", ref _enabled);
    ui.InputText("Name", ref _name, 128);
    ui.SliderFloat("Volume", ref _volume, 0f, 1f);

  }
}
```

### 옵션을 조금 조정한 즉시 모드 앱

```csharp
using Duxel.App;
using Duxel.Core;
using Duxel.Windows.App;

DuxelWindowsApp.Run(new DuxelAppOptions
{
  Window = new DuxelWindowOptions
  {
    Title = "Configured Duxel App",
    Width = 1440,
    Height = 900,
    VSync = true,
  },
  Renderer = new DuxelRendererOptions
  {
    Profile = DuxelPerformanceProfile.Display,
    EnableValidationLayers = true,
    MsaaSamples = 0,
    EnableGlobalStaticGeometryCache = true,
  },
  Font = new DuxelFontOptions
  {
    FontSize = 16,
    FastStartup = true,
  },
  Frame = new DuxelFrameOptions
  {
    LineHeightScale = 1.2f,
    EnableIdleFrameSkip = true,
  },
  Screen = new DemoScreen(),
});
```

### 최소 커스텀 위젯

```csharp
using Duxel.Core;

public sealed class CounterWidget : IUiCustomWidget
{
  private int _count;

  public void Render(UiImmediateContext ui)
  {
    ui.Text("Counter Widget");
    if (ui.Button("Add"))
    {
      _count++;
    }

    ui.SameLine();
    ui.Text($"Value: {_count}");
  }
}
```

### Markdown 위젯 사용 예제

```csharp
using Duxel.Core;

public sealed class MarkdownScreen : UiScreen
{
  private readonly MarkdownEditorWidget _editor = new("editor")
  {
    Label = "Markdown",
    Height = 220f,
    ShowStats = true,
    Text = "# Hello\n\n- item 1\n- item 2",
  };

  private readonly MarkdownViewerWidget _viewer = new("viewer")
  {
    Height = 260f,
    ShowBorder = true,
    ShowStats = true,
  };

  public override void Render(UiImmediateContext ui)
  {
    ui.BeginPanel("Markdown Studio");
    _editor.Render(ui);
    _viewer.Markdown = _editor.Text;
    _viewer.Render(ui);
    ui.EndPanel();
  }
}
```

### 최소 DSL 앱 (프로젝트 기반)

```csharp
// Program.cs
using Duxel.App;
using Duxel.Core.Dsl;
using Duxel.Windows.App;

var screen = new UiDslScreen("Ui/Main.ui", "Ui/theme.duxel-theme");

DuxelWindowsApp.Run(new DuxelAppOptions
{
  Window = new DuxelWindowOptions { Title = "DSL Demo" },
  Screen = screen,
});
```

## 핵심 앱 패턴

### 즉시 모드 화면 패턴

일반적인 앱 화면이나 샘플 화면은 `UiScreen`으로 작성한다.

- `Render(UiImmediateContext ui)`를 override
- 매 프레임 UI를 다시 구성
- 상태는 화면 인스턴스나 외부 모델에 유지

내부 창 제목도 컨트롤과 같은 label/ID 규칙을 따른다. `표시 제목##stable-id`는 화면에 `표시 제목`만 그리되 전체 문자열은 창 상태 식별자로 유지한다.

기본 권장값: 사용자가 단순히 Duxel 샘플을 요청하면, DSL을 명시적으로 요구하지 않는 한 즉시 모드 `UiScreen` 예제를 생성한다.

### DSL 패턴

선언형 UI 작성, 상태 바인딩, 생성기 기반 워크플로는 `.ui` DSL을 사용한다.

- 파서: `UiDslParser`
- 런타임 실행: `UiDslDocument.Emit(...)`
- 바인딩 경로: `IUiDslValueSource`, `IUiDslEventSink`
- 문서: `docs/ui-dsl.ko.md`

### 커스텀 위젯 패턴

내장 즉시 모드 표면에 올리기 애매한 동작은 인스턴스 기반 재사용 위젯으로 만든다.

- `IUiCustomWidget` 구현
- 위젯 전용 상태는 객체 내부에 보관 가능
- `Render(UiImmediateContext ui)`에서 렌더링

기본 제공 재사용 위젯:

- `MarkdownEditorWidget`
- `MarkdownViewerWidget`

## `UiImmediateContext` 기능 맵

이 섹션은 에이전트와 개발자가 가장 자주 쓰는 기능 인벤토리다.

### 창과 레이아웃

- `Begin(...)` / `End()`
- `BeginChild(...)` / `EndChild()`
- `SameLine()`
- `NewLine()`
- `Spacing()`
- `Separator()`
- `Dummy(...)`
- `Columns(...)`, `NextColumn()`
- 테이블 API: `TableSetupColumn(...)`, `TableNextRow()`, `TableNextCell()` 등
- 들여쓰기/너비 제어:
  - `Indent()` / `Unindent()`
  - `SetNextItemWidth(...)`
  - `PushItemWidth(...)` / `PopItemWidth()`
- 커서/영역 제어:
  - `GetCursorPos()` / `SetCursorPos(...)`
  - `GetContentRegionAvail()`

### 텍스트와 타이포그래피

- `Text(...)`
- `TextColored(...)`
- `TextDisabled(...)`
- `TextWrapped(...)`
- `TextUnformatted(...)`
- `LabelText(...)`
- 폰트 제어:
  - `PushFont(...)` / `PopFont()`
  - `PushFontSize(...)` / `PopFontSize()`

### 버튼과 기본 선택 위젯

- `Button(...)`
- `SmallButton(...)`
- `InvisibleButton(...)`
- `ArrowButton(...)`
- `Checkbox(...)`
- `CheckboxFlags(...)`
- `RadioButton(...)`

실전 시그니처:

- `bool Button(string label)`
- `bool Button(string label, UiVector2 size)`
- `bool SmallButton(string label)`
- `bool InvisibleButton(string id, UiVector2 size)`
- `bool ArrowButton(string id, UiDir dir)`
- `bool Checkbox(string label, ref bool value)`
- `bool CheckboxFlags(string label, ref int flags, int flagsValue)`
- `bool RadioButton(string label, bool active)`
- `bool RadioButton(string label, ref int value, int buttonValue)`

### 텍스트 입력과 숫자 입력

- `InputText(...)`
- `InputTextWithHint(...)`
- `InputTextMultiline(...)`
- `InputInt(...)`, `InputInt2(...)`, `InputInt3(...)`, `InputInt4(...)`
- `InputFloat(...)`, `InputFloat2(...)`, `InputFloat3(...)`, `InputFloat4(...)`
- `InputDouble(...)`, `InputDouble2(...)`, `InputDouble3(...)`, `InputDouble4(...)`

실전 시그니처:

- `bool InputText(string label, ref string value, int maxLength)`
- `bool InputTextWithHint(string label, string hint, ref string value, int maxLength)`
- `bool InputTextMultiline(string label, ref string value, int maxLength, float height)`
- `bool InputTextMultiline(string label, ref string value, int maxLength, int visibleLines)`
- `bool InputInt(string label, ref int value)`
- `bool InputFloat(string label, ref float value, string format = "0.###")`
- `bool InputDouble(string label, ref double value, string format = "0.###")`

### 슬라이더와 드래그 입력

- `SliderFloat(...)`, `SliderFloat2(...)`, `SliderFloat3(...)`, `SliderFloat4(...)`
- `SliderInt(...)`, `SliderInt2(...)`, `SliderInt3(...)`, `SliderInt4(...)`
- `SliderAngle(...)`
- `VSliderFloat(...)`
- `DragFloat(...)`, `DragFloat2(...)`, `DragFloat3(...)`, `DragFloat4(...)`
- `DragInt(...)`, `DragInt2(...)`, `DragInt3(...)`, `DragInt4(...)`
- `DragFloatRange2(...)`
- `DragIntRange2(...)`

실전 시그니처:

- `bool SliderFloat(string label, ref float value, float min, float max)`
- `bool SliderInt(string label, ref int value, int min, int max)`
- `bool DragFloat(string label, ref float value, float speed = 0.01f, float min = float.NegativeInfinity, float max = float.PositiveInfinity, string format = "0.###")`
- `bool DragInt(string label, ref int value, float speed = 0.1f, int min = int.MinValue, int max = int.MaxValue)`

### 선택, 콤보, 리스트

- `Combo(...)`
- `BeginCombo(...)` / `EndCombo()`
- `ListBox(...)`
- `BeginListBox(...)` / `EndListBox()`
- `Selectable(...)`

실전 시그니처:

- `bool Combo(ref int currentIndex, IReadOnlyList<string> items, int popupMaxHeightInItems = 8, string? id = null)`
- `bool Combo(ref int currentIndex, int itemsCount, Func<int, string> itemsGetter, int popupMaxHeightInItems = 8, string? id = null)`
- `bool BeginCombo(string previewValue, int popupMaxHeightInItems = 8, string? id = null)`
- `bool ListBox(ref int currentIndex, IReadOnlyList<string> items, int visibleItems = 6, string? id = null)`
- `bool Selectable(string label, ref bool selected)`
- `bool Selectable(string label, bool selected)`

콤보 팝업의 배경, 항목 렌더링, hit test, scrollbar는 팝업 영역과 소유 content clip의 교집합을 함께 사용한다. 교집합 높이가 scrolling viewport가 되므로 제한된 팝업도 마지막 항목이 완전히 보일 때까지 스크롤할 수 있다. 내부 창 content 영역 밖의 항목은 하단 padding을 뚫고 렌더링되거나 입력을 받지 않는다.

### 트리, 탭, 메뉴, 팝업, 툴팁

- `TreeNode(...)`, `TreeNodeId(...)`, `TreePop()`
- `SetNextItemOpen(...)`
- `BeginTabBar(...)` / `EndTabBar()`
- `BeginTabItem(...)` / `EndTabItem()`
- `TabItemButton(...)`
- `BeginMainMenuBar()` / `EndMainMenuBar()`
- `BeginMenuBar()` / `EndMenuBar()`
- `BeginMenu(...)` / `EndMenu()`
- `MenuItem(...)`
- `OpenPopup(...)`
- `BeginPopup(...)` / `EndPopup()`
- `BeginPopupContextItem(...)` 등 컨텍스트 팝업 계열
- `BeginTooltip()` / `EndTooltip()`
- `SetTooltip(...)`

실전 시그니처:

- `bool BeginMenuBar()`
- `bool BeginMenu(string label)`
- `bool MenuItem(string label, bool selected = false, bool enabled = true)`
- `bool BeginPopup(string id)`
- `bool BeginPopupContextItem(string id)`
- `bool BeginTooltip()`
- `bool BeginTabBar(string id)`
- `bool BeginTabItem(string label)`
- `bool TreeNode(string label, bool defaultOpen = false)`
- `bool TreeNodeEx(string label, UiTreeNodeFlags flags = UiTreeNodeFlags.None)`

### 색상, 이미지, 시각 효과

- `ColorButton(...)`
- `ColorEdit3(...)`, `ColorEdit4(...)`
- `ColorPicker3(...)`, `ColorPicker4(...)`
- `Image(...)`
- `ImageWithBg(...)`
- `ImageButton(...)`

실전 시그니처:

- `bool ColorEdit3(string label, ref float r, ref float g, ref float b)`
- `bool ColorEdit4(string label, ref float r, ref float g, ref float b, ref float a)`
- `bool ImageButton(string id, UiTextureId textureId, UiVector2 size, UiColor? tint = null)`

### 진행률, 그래프, 피드백

- `ProgressBar(...)`
- `PlotLines(...)`
- `PlotHistogram(...)`

실전 시그니처:

- `void ProgressBar(float fraction, UiVector2 size, string? overlay = null)`

### 스타일과 스코프 오버라이드

- `PushStyleColor(...)` / `PopStyleColor()`
- `PushStyleVar(...)` / `PopStyleVar()`
- `PushId(...)` / `PopId()`
- `BeginDisabled()` / `EndDisabled()`

동적 리스트를 만들 때는 ID 충돌을 피하기 위해 명시적으로 ID를 관리한다.

### 상태와 상호작용 질의

- `IsItemHovered()`
- `IsItemActive()`
- `IsItemClicked()`
- `GetItemRectMin()`
- `GetItemRectMax()`
- `GetItemRectSize()`

### 커스텀 드로잉과 레이어

내장 위젯만으로 부족할 때는 드로우리스트와 캔버스 패턴을 사용한다.

- 사각형, 원, 선, 베지어, 텍스트, 이미지, 클립 제어, 텍스처 전환, 콜백 등 드로우리스트 primitive 지원
- 레이어/캔버스 지향 흐름은 레이어 벤치 샘플과 혼합 렌더링 샘플 참고
- retained static 드로우리스트는 `StaticGeometryKey`를 캐시 identity로 사용하므로, retained geometry 내용이 바뀌면 key도 바꾼다.
- `UiDrawList.StaticGeometryStamp`는 렌더러 캐시 검증에 쓰이는 저비용 geometry-content stamp다. 레이어 캐시는 자동으로 채우고, 커스텀 retained static 생성자는 필요 시 `UiDrawList.ComputeStaticGeometryStamp(...)`로 직접 설정할 수 있다.
- layer opacity와 append translation은 `UiDrawCommand.Opacity`/`UiDrawCommand.Translation`으로 전달되어 Vulkan vertex shader에서 적용되므로, retained static geometry key/stamp는 layer opacity나 배치 위치가 아니라 geometry content를 설명해야 한다.
- producer가 독립 draw group을 이미 알고 있고 의도한 stacking을 보존할 수 있으면 `UiDrawListBuilder.Split(count)`와 `SetCurrentChannel(index)`를 사용한다. 그 group들이 별도 draw list로 남아도 되고 focused gate에서 draw-list boundary가 이득임이 증명된 경우에만 `MergeChannelsAsDrawLists()`를 선택한다. `global_dirty_strategy_bench.cs`는 copy-free channel output이 더 느린 counterexample이다. 하나의 물리적으로 병합된 draw list가 필요하거나 focused gate가 copy-merge를 지지하면 `Merge()`를 사용한다. channel 출력 순서가 곧 명시적 draw order이므로, 겹치는 content의 stacking이 달라지면 안 되는 경우에는 channelize하지 않는다.

## 자주 만드는 UI용 Cookbook

### 기본 폼 창

설정 페이지, 다이얼로그, 툴 패널은 이 패턴을 기본으로 쓴다.

```csharp
public sealed class SettingsScreen : UiScreen
{
  private string _userName = "guest";
  private bool _autoSave = true;
  private int _quality = 2;
  private float _scale = 1.0f;

  public override void Render(UiImmediateContext ui)
  {
    ui.BeginPanel("Settings");

    ui.InputText("User Name", ref _userName, 128);
    ui.Checkbox("Auto Save", ref _autoSave);
    ui.SliderFloat("Scale", ref _scale, 0.5f, 2.0f);

    ui.Text("Quality");
    ui.RadioButton("Low", ref _quality, 0);
    ui.RadioButton("Medium", ref _quality, 1);
    ui.RadioButton("High", ref _quality, 2);

    if (ui.Button("Apply"))
    {
      // commit state
    }

    ui.EndPanel();
  }
}
```

### 메뉴바 패턴

```csharp
if (ui.BeginMainMenuBar())
{
  if (ui.BeginMenu("File"))
  {
    _ = ui.MenuItem("Open");
    _ = ui.MenuItem("Save");
    _ = ui.MenuItem("Exit");
    ui.EndMenu();
  }

  if (ui.BeginMenu("View"))
  {
    _ = ui.MenuItem("Show Grid", selected: true);
    ui.EndMenu();
  }

  ui.EndMainMenuBar();
}
```

### 팝업 패턴

```csharp
if (ui.Button("Open Popup"))
{
  ui.OpenPopup("demo-popup");
}

if (ui.BeginPopup("demo-popup"))
{
  ui.Text("Popup content");
  ui.EndPopup();
}
```

### 탭 패턴

```csharp
if (ui.BeginTabBar("main-tabs"))
{
  if (ui.BeginTabItem("General"))
  {
    ui.Text("General content");
    ui.EndTabItem();
  }

  if (ui.BeginTabItem("Advanced"))
  {
    ui.Text("Advanced content");
    ui.EndTabItem();
  }

  ui.EndTabBar();
}
```

### 트리 패턴

```csharp
if (ui.TreeNode("Assets", defaultOpen: true))
{
  ui.BulletText("Textures");
  ui.BulletText("Shaders");
  ui.BulletText("Fonts");
  ui.TreePop();
}
```

### 리스트와 콤보 패턴

```csharp
private readonly string[] _modes = ["Display", "Render", "Debug"];
private int _modeIndex;

public override void Render(UiImmediateContext ui)
{
  ui.BeginPanel("Selection");
  ui.Combo(ref _modeIndex, _modes, 8, "mode");
  ui.Text($"Current: {_modes[_modeIndex]}");
  ui.EndPanel();
}
```

### 두 열 설정 패턴

레거시 두 열 설정 레이아웃을 맞춰야 할 때만 `Columns(...)`를 사용한다. 새로운 구조화 데이터 뷰는 테이블 API를 우선한다.

### 커스텀 드로우 캔버스 패턴

```csharp
var canvas = ui.BeginWindowCanvas(new UiColor(0xFF101820));
var draw = ui.GetWindowDrawList();

draw.AddRect(canvas, new UiColor(0xFFFFFFFF));
draw.AddLine(
  new UiVector2(canvas.X, canvas.Y),
  new UiVector2(canvas.X + canvas.Width, canvas.Y + canvas.Height),
  new UiColor(0xFF39AFFF),
  2f);

ui.EndWindowCanvas();
```

## 생성 규칙 요약

- 빠른 데모는 앱 작업 영역에 직접 구성하고, 접기·펼치기 시연에만 `BeginPanel(...)` / `EndPanel()`을 명시적으로 사용
- 상태는 화면 또는 위젯 객체의 private 필드에 유지
- 사용자가 렌더러 작업을 명시하지 않는 한 즉시 모드 예제를 우선
- 기존 샘플의 이름과 패턴을 우선 재사용
- 새로운 구조화 데이터 뷰는 테이블 API 우선
- 이 저장소에서 로컬 소스 검증은 `run-fba.ps1` 우선
- 마크다운 편집/뷰어 요구는 내장 Markdown 위젯 우선

## 샘플 우선 탐색표

| 필요 항목 | 시작점 |
|---|---|
| 전체 기능 쇼케이스 | `samples/fba/all_features.cs` |
| 선언형 C# 대시보드 | `samples/fba/declarative_dashboard_fba.cs` |
| 선언형 DSL + 테마 | `samples/Duxel.ThemeDemo` |
| 레이아웃과 스타일 제어 | `samples/fba/advanced_layout.cs` |
| 레거시 컨럼 | `samples/fba/columns_demo.cs` |
| 이미지, 팝업, 툴팁 패턴 | `samples/fba/image_and_popups.cs` |
| 이미지 효과와 애니메이션 이미지 | `samples/fba/image_widget_effects_fba.cs` |
| 키보드와 마우스 질의 API | `samples/fba/input_queries.cs` |
| 아이템 생명주기와 상태 질의 | `samples/fba/item_status.cs` |
| 텍스트 정렬과 렌더 검증 | `samples/fba/text_render_validation_fba.cs` |
| 폰트 스타일과 크기 검증 | `samples/fba/font_style_validation_fba.cs` |
| 레이어 캐시와 dirty 전략 | `samples/fba/idle_layer_validation.cs`, `samples/fba/layer_dirty_strategy_bench.cs`, `samples/fba/global_dirty_strategy_bench.cs` |
| 레이어와 위젯 혼합 | `samples/fba/layer_widget_mix_bench_fba.cs` |
| DirectText 변경 문자열 성능 | `samples/fba/directtext_dynamic_text_bench_fba.cs` |
| 렌더링 스트레스 | `samples/fba/ui_mixed_stress.cs` |
| 물리/성능 벤치마크 | `samples/fba/Duxel_perf_test_fba.cs` |
| 앱 규모 참조 | `samples/Duxel.Sample` |

## 권장 실행 워크플로

### 최종 사용자 패키지 워크플로

Duxel을 라이브러리처럼 소비할 때는 패키지 모드를 사용한다.

- `dotnet run hello.cs`
- `dotnet run samples/fba/all_features.cs`

FBA 샘플은 다음과 같은 패키지 지시문을 사용한다.

```csharp
#:property TargetFramework=net10.0
#:property platform=windows
#:package Duxel.$(platform).App@*-*
```

### 기여자 로컬 소스 워크플로

로컬 소스 변경을 검증할 때는 `run-fba.ps1`를 사용한다.

권장 예시:

- `./run-fba.ps1 samples/fba/all_features.cs -NoCache`
- `./run-fba.ps1 samples/fba/all_features.cs -Managed`
- `./run-fba.ps1 samples/fba/perf_2d_render_fps.cs -Wait`

이유:

- 패키지 지시문을 로컬 프로젝트 참조로 바꿔 준다.
- 필요 시 Windows 엔트리 호출을 정규화한다.
- 기본적으로 NativeAOT 경로를 지원한다.
- `-Wait`로 NativeAOT GUI 샘플 종료까지 대기할 수 있어 자동 파일 로그 벤치마크 수집에 적합하다.
- 단순 패키지 모드와 달리 현재 워크스페이스 소스 변경을 반영한다.
- 샘플·자산·출력 파일을 리터럴 경로로 해석하며, 저장소 스크립트는 PowerShell provider에 파일 시스템 와일드카드를 넘기지 않고 리터럴 디렉터리를 열거한 뒤 파일 속성으로 필터링한다.

PowerShell 스크립트를 프로그래밍 방식으로 호출할 때 이름 있는 매개변수는 해시테이블 스플래팅을 사용한다. 네이티브 실행 파일에는 인수 배열을 사용한다. `-Path` 같은 토큰을 문자열 배열에 넣어 PowerShell 스크립트에 전달하면 위치 인수 값으로 바인딩될 수 있으므로 사용하지 않는다.

로컬 소스 수정 검증에 `dotnet run samples/fba/<file>.cs`를 기준으로 삼지 않는다.

## 빌드와 검증

기본 저장소 빌드:

- `dotnet build Duxel.slnx -c Release`

의미 있는 변경 뒤 최소 검증 기준:

- 솔루션 빌드, 또는
- 관련 샘플 1개 이상 실행

가능한 한 가장 작은 검증으로, 그러나 충분히 증명되는 검증을 선택한다.

이 저장소에서는 작업이 .NET, 성능, Vulkan, native import, FBA sample을 건드릴 때 `.github/copilot-instructions.md`와 관련 `.github/skills/*/SKILL.md`도 확인한다. Codex도 이를 로컬 프로젝트 지침으로 활용하되, 현재 대화의 계약은 `AGENTS.md`와 명시적인 user/developer instruction을 우선한다.

## 환경 변수와 런타임 토글

샘플과 진단에서 자주 쓰는 런타임 토글:

- `DUXEL_APP_PROFILE`
- `DUXEL_PERF_PROFILE`
- `DUXEL_PERF_BENCH_OUT`
- `DUXEL_PERF_BENCH_SECONDS`
- `DUXEL_PERF_INITIAL_POLYGONS`
- `DUXEL_PERF_GLOBAL_STATIC_CACHE`
- `DUXEL_SAMPLE_AUTO_EXIT_SECONDS`
- `DUXEL_IMAGE_PATH`
- `DUXEL_LAYER_BENCH_BACKEND`
- `DUXEL_LAYER_BENCH_OPACITY`
- `DUXEL_LAYER_BENCH_PARTICLES`
- `DUXEL_LAYER_BENCH_LAYOUTS`
- `DUXEL_LAYER_BENCH_PHASE_SECONDS`
- `DUXEL_LAYER_BENCH_DISABLE_FAST_RENDER`
- `DUXEL_LAYER_BENCH_OUT`
- `DUXEL_GLOBAL_DIRTY_BENCH_OUT`
- `DUXEL_GLOBAL_DIRTY_BENCH_PHASE_SECONDS`
- `DUXEL_GLOBAL_DIRTY_BENCH_DENSITY`
- `DUXEL_GLOBAL_DIRTY_BENCH_COLS`
- `DUXEL_GLOBAL_DIRTY_BENCH_ROWS`
- `DUXEL_GLOBAL_DIRTY_CHANNEL_DRAWLISTS`
- `DUXEL_PIPELINE_ORDER_BENCH_OUT`
- `DUXEL_PIPELINE_ORDER_PHASE_SECONDS`
- `DUXEL_PIPELINE_ORDER_ITEMS`
- `DUXEL_STATIC_LAYER_MOVE_ORDER_BENCH_OUT`
- `DUXEL_STATIC_LAYER_MOVE_ORDER_PHASE_SECONDS`
- `DUXEL_STATIC_LAYER_MOVE_ORDER_ITEMS`
- `DUXEL_STATIC_LAYER_MOVE_ORDER_AMPLITUDE`
- `DUXEL_STATIC_CACHE_REBUILD_BENCH_OUT`
- `DUXEL_STATIC_CACHE_REBUILD_PHASE_SECONDS`
- `DUXEL_STATIC_CACHE_REBUILD_WARMUP_SECONDS`
- `DUXEL_STATIC_CACHE_REBUILD_LAYERS`
- `DUXEL_STATIC_CACHE_REBUILD_DENSITY`
- `DUXEL_STATIC_CACHE_REBUILD_GPU_OVERDRAW`
- `DUXEL_DIRECT_TEXT_PAGE`
- `DUXEL_DTPAGE_UPLOAD_BENCH_OUT`
- `DUXEL_DTPAGE_UPLOAD_PHASE_SECONDS`
- `DUXEL_DTPAGE_UPLOAD_PAGE_SIZE`
- `DUXEL_DTPAGE_UPLOAD_REGION_WIDTH`
- `DUXEL_DTPAGE_UPLOAD_REGION_HEIGHT`
- `DUXEL_DTPAGE_UPLOAD_REGIONS`
- `DUXEL_DTPAGE_UPLOAD_PAGES`
- `DUXEL_DTPAGE_UPLOAD_WARMUP_FRAMES`
- `DUXEL_TEXT_RENDERING`
- `DUXEL_VK_COMMAND_SCHEDULER`
- `DUXEL_VK_COMMAND_SCHEDULER_MAX_WINDOW`
- `DUXEL_UI_COMMAND_SCHEDULER`
- `DUXEL_VK_COMMAND_DIAG`
- `DUXEL_VK_COMMAND_DIAG_EVERY`
- `DUXEL_VK_COMMAND_DIAG_FRAMES`
- `DUXEL_VK_COMMAND_DIAG_OUT`
- `DUXEL_VK_FONT_DIAG`
- `DUXEL_VK_FONT_DIAG_OUT`
- `DUXEL_VK_PROFILE`
- `DUXEL_VK_PROFILE_EVERY`
- `DUXEL_VK_PROFILE_OUT`
- `DUXEL_VK_GPU_PROFILE`
- `DUXEL_VK_UPLOAD_BATCH`
- `DUXEL_VK_UPLOAD_QUEUE`
- `DUXEL_VK_STATIC_GEOMETRY_UPDATE`
- `DUXEL_VK_STATIC_GEOMETRY_INPLACE_UPDATE`
- `DUXEL_VK_STATIC_GEOMETRY_ROTATING_UPDATE`
- `DUXEL_VK_STATIC_PRIMITIVE_TRIANGLES`

특정 토글에 의존하는 작업은 문서화하거나 사용하기 전에 관련 샘플에서 이름과 의미를 다시 확인한다.

Vulkan 커맨드 기록 프로파일링은 `DUXEL_VK_PROFILE=1`로 켠다. `DUXEL_VK_PROFILE_EVERY`는 로그 간격 프레임 수이고, `DUXEL_VK_PROFILE_OUT`은 프로파일 라인을 파일에 append한다. 프로파일 라인의 `device(vendor=... vid=... did=... type=... name=... gfxQ=... uploadQ=... xferCandQ=... tsBits=... tsPeriodNs=... gpuTs=...)`와 `policy(upload=... transferCandidate=... staticPrimTri=... staticUpdate=... staticUpdateReq=... scheduler=... schedWindow=... staticSecondaryMin=...)`는 나중에 NVIDIA, AMD, Intel, integrated/discrete 장치의 artifact를 비교할 때 어떤 장치와 자동 정책에서 나온 결과인지 증명한다. `uploadQ`는 upload command buffer가 실제로 사용하는 queue family이고, `xferCandQ`는 탐지된 non-graphics transfer-capable 후보일 뿐이다. 따라서 `policy(upload=graphics transferCandidate=1)`은 후보가 있다는 뜻이지 upload가 이미 transfer queue를 사용한다는 뜻이 아니다. `DUXEL_VK_UPLOAD_QUEUE=transfer`는 후보가 있을 때만 split transfer upload path를 opt-in으로 켠다. `policy(upload=transfer transferCandidate=1)`은 upload copy command buffer가 그 queue를 실제로 사용한다는 profile 증거다. `staticUpdateReq`는 요청된 mode이고 `staticUpdate`는 장치 정책으로 resolve된 mode다. `scheduler`는 resolved command-scheduler mode이며 `disabled`, `static`, `all` 중 하나다. `stateUs(pipe=... desc=... buf=... push=... scissor=...)`는 pipeline bind, descriptor bind, vertex/index/primitive buffer bind, push constant, scissor set 비용을 분리해서 보여준다. `clipCache(calc=... reuse=...)`는 실제 scissor rectangle 계산 횟수와 연속으로 같은 clip/translation을 재사용한 횟수를 기록한다. `staticSec(cand=... cmds=... draws=...)`는 현재 `staticSecondaryMin` threshold를 넘긴 static draw list 수와 그 후보 안의 기록된 command/draw 수를 기록한다. `cand=0`이면 static list가 존재해도 그 frame에서는 secondary command buffer가 다음 경로 후보라는 가설이 약하다는 뜻이다. `listWork(staticCmd=... dynCmd=... staticDraw=... dynDraw=... staticPipe=... dynPipe=... staticClip=... dynClip=... staticScissor=... dynScissor=... staticPush=... dynPush=... staticGeom=... dynGeom=... staticPrim=... dynPrim=...)`는 command-recording 작업을 static cached replay와 dynamic draw list로 나눠 기록한다. 다음 병목이 static replay policy인지 dynamic UI ordering/channelization인지 판단할 때 이 값을 우선 확인한다. `staticGeom(hit=... create=... replace=... update=... reuse=... hash=...)`는 upload phase에서 static geometry cache hit, buffer creation, content replacement, 같은 shape의 in-place content update, 같은 shape의 rotating-buffer reuse, fallback content hashing을 기록한다. `staticMem(active=... activeBytes=... retired=... retiredBytes=...)`는 memory-pressure 확인을 위해 active static geometry entry/bytes와 retired rotating-pool entry/bytes를 기록한다. `staticPrim(expand=... expandPrim=... layout=... force=... autoSkip=... autoSkipPrim=... autoSkipMut=... expandBytes=... autoSkipBytes=...)`는 frame별 static primitive triangle expansion 결정을 기록한다. `policy(... staticPrimTri=1 ...)`은 장치 정책상 경로가 허용됐다는 뜻일 뿐이므로, 실제 draw-list가 확장됐는지는 이 카운터로 확인한다. `autoSkipMut`은 같은 static geometry tag의 content가 바뀌는 중이라 확장을 억제한 skip subset이다. `pipeClass(font=... texTri=... colorTri=... texPrim=... colorPrim=... solid=...)`는 renderer policy 적용 뒤 실제로 선택된 pipeline class를 기록하므로 원본 source command kind만 보는 것보다 pipeline switch 분석에 더 정확하다. `sched(probe=... hit=... miss=... nochange=... lists=... merged=... us=...)`는 opt-in command scheduler의 동작과 비용을 기록한다. `upSched(sub=... prepSub=... wait=... flush=... bytes=... texRegions=... bufCopies=... submitUs=... prepUs=... waitUs=...)`는 staging upload scheduler submission, split-transfer graphics-prepare submission, upload fence wait, batch flush, staging byte volume, texture copy region, static/buffer copy command, 그리고 submission/wait 비용을 기록한다. `imgTrans(total=... toDst=... toShader=... present=... color=... xferStage=... gfxStage=... us=...)`는 image layout transition 횟수와 transfer-queue-compatible stage mask / graphics-stage-required barrier를 분리해서 기록한다. `state=`는 scissor를 제외한 state command 시간이고, scissor는 `clip=`에도 포함되므로 별도로 해석한다.

성능 데모는 확인하려는 병목에 맞아야 한다. 넓은 layer-widget scene은 최종 regression 용도로 사용하되, 질문이 좁으면 초점형 FBA 데모를 고르거나 추가한다. `samples/fba/pipeline_ordering_bench_fba.cs`는 dynamic solid/text pipeline ordering 비용을 확인하는 초점형 gate이며 alternating, grouped solids-then-text, copy-merge `channelized-solid-text`, copy-free `channel-drawlists-solid-text` phase를 포함하고 `DUXEL_PIPELINE_ORDER_BENCH_OUT`, `DUXEL_PIPELINE_ORDER_PHASE_SECONDS`, `DUXEL_PIPELINE_ORDER_ITEMS`를 사용한다. `samples/fba/dynamic_widget_ordering_bench_fba.cs`는 widget-like dynamic producer ordering과 row-clip churn을 확인하는 초점형 gate이며 alternating widget row, grouped solids-then-text, copy-merge channelized phase, copy-free channel-drawlists phase를 포함하고 `DUXEL_DYN_WIDGET_ORDER_BENCH_OUT`, `DUXEL_DYN_WIDGET_ORDER_PHASE_SECONDS`, `DUXEL_DYN_WIDGET_ORDER_WARMUP_SECONDS`, `DUXEL_DYN_WIDGET_ORDER_ITEMS`, `DUXEL_DYN_WIDGET_ORDER_ROW_CLIPS`를 사용한다. `samples/fba/vector_primitives_bench_fba.cs`는 primitive-heavy geometry를 확인하는 초점형 gate이며 `DUXEL_VECTOR_BENCH_WORKLOAD=mixed`, `rect-outline`, `axis-line`과 `DUXEL_VECTOR_BENCH_OUT`, `DUXEL_VECTOR_BENCH_PHASE_SECONDS`, `DUXEL_VECTOR_BENCH_COUNTS`를 사용한다. `samples/fba/Duxel_perf_test_fba.cs`는 polygon physics/perf smoke이며 기본은 Render profile과 global static backdrop cache다. `DUXEL_PERF_PROFILE=render|display`로 시작 profile을 override할 수 있고, `DUXEL_PERF_GLOBAL_STATIC_CACHE=0`은 retained static backdrop reference를 끈다. UI의 Render Profile checkbox는 재시작 없이 MSAA를 `1x`/`4x`로 바꿔 profile을 즉시 적용한다. `samples/fba/global_dirty_strategy_bench.cs`는 global static background cache와 dynamic overlay update를 비교하는 초점형 gate이며, `DUXEL_GLOBAL_DIRTY_CHANNEL_DRAWLISTS`로 그 배경/오버레이 channel 구조에서 copy-merge와 별도 draw-list 출력을 비교한다. `samples/fba/static_layer_moving_order_bench_fba.cs`는 moving static-layer replay schedule 재사용을 확인하는 초점형 gate이며 `DUXEL_STATIC_LAYER_MOVE_ORDER_BENCH_OUT`, `DUXEL_STATIC_LAYER_MOVE_ORDER_PHASE_SECONDS`, `DUXEL_STATIC_LAYER_MOVE_ORDER_ITEMS`, `DUXEL_STATIC_LAYER_MOVE_ORDER_AMPLITUDE`를 사용한다. `samples/fba/static_cache_rebuild_bench_fba.cs`는 cache replay, false-dirty static rebuild, mutating static geometry replacement/update 비용, Core cache-copy allocation pressure, static primitive triangle memory pressure를 분리하는 초점형 gate이며 측정 frame별 `avgAllocatedBytes`도 보고한다. 이 gate는 `DUXEL_STATIC_CACHE_REBUILD_BENCH_OUT`, `DUXEL_STATIC_CACHE_REBUILD_PHASE_SECONDS`, `DUXEL_STATIC_CACHE_REBUILD_WARMUP_SECONDS`, `DUXEL_STATIC_CACHE_REBUILD_LAYERS`, `DUXEL_STATIC_CACHE_REBUILD_DENSITY`, `DUXEL_STATIC_CACHE_REBUILD_PRIMITIVE_MODE`(`circles`, `rects`, `mixed`), `DUXEL_STATIC_CACHE_REBUILD_CIRCLE_SEGMENTS`, GPU-bound variant용 선택 토글 `DUXEL_STATIC_CACHE_REBUILD_GPU_OVERDRAW`를 사용한다. `samples/fba/texture_upload_barrier_bench_fba.cs`는 transfer-queue policy 변경 전 texture upload copy/barrier 동작을 분리하는 초점형 gate이며 `DUXEL_TEXTURE_UPLOAD_BENCH_OUT`, `DUXEL_TEXTURE_UPLOAD_PHASE_SECONDS`, `DUXEL_TEXTURE_UPLOAD_SIZE`, `DUXEL_TEXTURE_UPLOAD_REGION_SIZE`, `DUXEL_TEXTURE_UPLOAD_REGIONS`, `DUXEL_TEXTURE_UPLOAD_TEXTURES`, `DUXEL_TEXTURE_UPLOAD_WARMUP_FRAMES`를 사용한다. `samples/fba/directtext_page_upload_bench_fba.cs`는 platform glyph rasterizer 비용을 섞지 않고 DirectText page-style partial texture upload 동작을 확인하는 초점형 gate이며 `DUXEL_DTPAGE_UPLOAD_BENCH_OUT`, `DUXEL_DTPAGE_UPLOAD_PHASE_SECONDS`, `DUXEL_DTPAGE_UPLOAD_PAGE_SIZE`, `DUXEL_DTPAGE_UPLOAD_REGION_WIDTH`, `DUXEL_DTPAGE_UPLOAD_REGION_HEIGHT`, `DUXEL_DTPAGE_UPLOAD_REGIONS`, `DUXEL_DTPAGE_UPLOAD_PAGES`, `DUXEL_DTPAGE_UPLOAD_WARMUP_FRAMES`를 사용한다.

`BenchFrameRecorder`는 초점형 gate가 공유하는 raw-frame 누적기다. 통계에는 sample 수, 측정 시간, 평균 FPS, median/p95/p99 frame time, 1% low FPS가 포함된다. `layer_widget_mix_bench_fba.cs`는 이 필드를 JSON schema version 2로 기록하므로 consumer는 tail field가 있다고 가정하기 전에 `schemaVersion`을 확인해야 한다. Layer-widget과 DirectText gate는 sample slot `1,048,576`개를 할당하고 phase duration을 30초로 제한해서, 약 `10.7k` FPS로 관측된 DirectText Atlas 대조군을 포함한 유효 장기 실행이 이전 `262,144` sample 한도를 넘지 않게 한다.

`samples/fba/directtext_dynamic_text_bench_fba.cs`는 안정 DirectText cache hit와 변경 문자열 cache miss를 비교하는 초점형 gate다. `DUXEL_DIRECTTEXT_BENCH_OUT`은 필수이며 제어값은 `DUXEL_DIRECTTEXT_BENCH_PHASE_SECONDS`, `DUXEL_DIRECTTEXT_BENCH_WARMUP_FRAMES`, `DUXEL_DIRECTTEXT_BENCH_ROWS`, `DUXEL_DIRECTTEXT_BENCH_CORPUS_FRAMES`다. JSON에는 frame과 text-work의 median/p95/p99 tail, 1% low FPS, frame당 평균 할당량, generation별 collection 횟수가 포함된다.

`samples/fba/analytic_rounded_primitives_bench_fba.cs`는 rounded fill, fill/border 결합 panel, border-only rounded rectangle, circle을 위한 1x-MSAA 초점형 화질/성능 gate다. `DUXEL_ROUNDED_BENCH_OUT`이 없으면 큰 visual reference shape를 표시한다. 출력 경로가 있으면 `DUXEL_ROUNDED_BENCH_COUNTS`, `DUXEL_ROUNDED_BENCH_PHASE_SECONDS`, `DUXEL_ROUNDED_BENCH_WARMUP_SECONDS`로 제어되는 self-terminating benchmark를 실행하고 평균 FPS, median/p95/p99 frame time, 1% low FPS를 기록한다. 시작 시 rounded rectangle/circle이 CPU polygon vertex가 아니라 primitive instance를 만들고 replay 뒤에도 border-only geometry variant가 보존되는지 단언한다.

축 정렬 `UiDrawListBuilder.AddRect(...)` 외곽선은 triangle polyline geometry 대신 rect-filled primitive emission을 사용한다. Rounded outline, rounded fill, `AddCircleFilled(...)`는 GPU primitive instance로 유지되고 fragment shader가 signed distance와 derivative 기반 coverage를 계산하므로 caller가 넘긴 circle segment 수와 화질이 무관하다. Fill+border `AddRectFilledRounded(...)` overload는 결합 instance 하나와 blend pass 하나를 만든다. Border-only rounded rectangle은 vertex shader가 생성하는 겹치지 않는 perimeter cell 8개만 사용해 항상 비어 있는 중앙 영역의 full-panel fragment overdraw를 제거한다. 수평/수직 `UiDrawListBuilder.AddLine(...)`도 rect-filled primitive를 사용하고 diagonal line은 기존 quad triangle path를 유지한다.

`DUXEL_VK_STATIC_GEOMETRY_UPDATE`는 같은 shape의 static geometry content change 정책을 제어한다. 유효한 값은 `auto`, `replace`, `inplace`, `rotating`이다. `auto`는 검증된 NVIDIA discrete GPU에서는 `rotating`으로, AMD/Intel과 그 외 장치에서는 같은 정책을 증명하는 gate가 쌓일 때까지 `replace`로 resolve된다. `replace`는 allocation/replacement를 강제하고, `inplace`는 fence-waited existing-buffer reupload를 강제하며, `rotating`은 retired-buffer reuse를 강제한다.

`DUXEL_VK_STATIC_GEOMETRY_INPLACE_UPDATE=1`은 `DUXEL_VK_STATIC_GEOMETRY_UPDATE=inplace`의 backward-compatible explicit override다. static geometry cache entry의 vertex/index/primitive count와 expanded primitive triangle layout이 같으면 Vulkan은 모든 in-flight frame fence를 기다린 뒤 새 replacement buffer를 만들지 않고 기존 device-local buffer에 다시 업로드한다. 이 경로는 focused A/B용으로 유지한다. rotating update가 full in-flight fence wait를 피하므로 기본 경로 후보로 더 깔끔하다.

`DUXEL_VK_STATIC_GEOMETRY_ROTATING_UPDATE=1`은 `DUXEL_VK_STATIC_GEOMETRY_UPDATE=rotating`의 backward-compatible explicit override다. 같은 shape의 content change가 발생하면 현재 static geometry buffer를 frame-safe reuse pool에 retire하고, 안전해진 retired buffer가 있으면 그것을 활성화하며 없으면 새 buffer를 만들어 pool을 시드한다. Retired pool은 tag별로 frame count를 넘지 않도록 제한되고, 오래 재사용되지 않은 retired buffer는 `StaticGeometryRetiredReuseGraceFrames` 뒤에 trim된다. 이전 제출 frame이 아직 읽을 수 있는 buffer를 덮어쓰지 않으므로 in-place update보다 기본 경로 후보로 더 깔끔하다. 검증된 NVIDIA discrete GPU에서는 기본 resolve policy이고, 다른 vendor/device gate에서는 명시적으로 사용할 수 있다.

`DUXEL_VK_COMMAND_SCHEDULER=all` 또는 `1`은 모든 eligible draw list에 opt-in overlap-constrained Vulkan command scheduler를 켠다. `DUXEL_VK_COMMAND_SCHEDULER=static`은 같은 scheduler를 static-layer replay draw list에만 켜서, 측정된 static-layer replay 이득은 유지하면서 dynamic whole-list scheduling을 hot path에서 제외한다. 이 경로는 `UiDrawCommand` coverage bounds로 overlap 때문에 유지해야 하는 순서를 보존하면서 ready command를 pipeline class별로 묶고, 기록 시점에서 새로 인접해진 호환 command를 병합한다. `all` 모드에서는 dynamic draw-list 생성 단계에서 `UiDrawList.CommandScheduleStamp`도 계산해서 cache-hit scheduling이 Vulkan command recording에서 전체 command hash/compare를 반복하지 않게 한다. 기본 경로는 아니다. 초점형 프로파일링에서만 사용하고, 넓은 scene이 무제한 scheduling 비용을 내지 않도록 `DUXEL_VK_COMMAND_SCHEDULER_MAX_WINDOW`를 제한해서 사용한다.

Static cached layer replay는 `DUXEL_VK_COMMAND_SCHEDULER=static`, `all`, 또는 `1`일 때 layer capture 시점에 보수적인 command schedule shape stamp를 한 번 계산한다. 이 stamp는 안정적인 local command shape와 raw local command bounds를 기준으로 한다. Vulkan scheduling도 static-layer draw list에는 같은 보수적 bounds를 사용하므로, 안정적인 layer가 이동하거나 clipping되어도 매 프레임 overlap analysis나 replay-shape hashing을 반복하지 않고 cached safe order를 재사용할 수 있다.

Dirty static layer가 같은 draw-list shape로 다시 build되면 Core는 새 local vertex/index/command/primitive 배열을 할당하지 말고 기존 `UiLayerCachedList` storage를 in-place refresh해야 한다. Overwrite 뒤 `StaticGeometryStamp`와 command schedule shape stamp를 다시 계산하고, 안정적인 `duxel.layer.static:{layerId}:list:{i}` key는 유지하며, replay를 invalidate해서 opacity/translation/clip command가 갱신된 content stamp 아래에서 다시 만들어지게 한다.

`DUXEL_UI_COMMAND_SCHEDULER=1`은 `UiDrawListBuilder.Flush()`에서 더 이른 builder-stage scheduling 실험을 켠다. 이 토글은 `DUXEL_VK_COMMAND_SCHEDULER`와 별도로 둔다. 매 프레임 draw-list 내용이 바뀌는 동적 whole-list builder scheduling은 절감하는 기록 비용보다 더 비쌀 수 있으므로 구조 실험 전용이다. 기본 경로 후보로 보기 전에 stable layer/static schedule caching이나 명시적 draw channel을 우선한다.

GPU timestamp 계측은 `DUXEL_VK_GPU_PROFILE=1`로 추가로 켠다. 이 값은 그래픽 queue timestamp를 지원하는 장치에서만 활성화되고, 프로파일 라인의 `gpuRender=...`에 render pass 전후 GPU 실행 시간을 microsecond 단위로 추가한다. CPU command-recording 비용과 shader/GPU-side 비용을 분리할 때 사용한다.

Static cached rect/circle primitive를 triangle vertex/index geometry로 확장하는 경로는 `DUXEL_VK_STATIC_PRIMITIVE_TRIANGLES=auto`가 기본이다. 자동 정책은 NVIDIA/AMD discrete GPU에서만 켠 뒤 draw-list별 byte guard와 mutation guard를 적용한다. `auto`는 예상 expanded vertex/index bytes가 primitive-instance bytes의 `32x`를 넘으면 확장하지 않아 high-segment primitive-heavy cache를 static primitive-instance 경로에 남긴다. 또한 같은 static geometry tag의 content hash가 바뀌면 해당 tag는 `30`프레임 동안 확장을 억제해서 mutating static layer가 안정될 때까지 upload가 싼 primitive-instance 경로를 사용하게 한다. `1/true/on`은 확장을 강제하고 `0/false/off`는 비활성화를 강제한다. 이 경로는 cached static draw-list의 command 순서, clip, opacity, texture state를 유지하면서 확장된 primitive를 indexed triangle draw mode로 태운다. 실제 expand/skip 여부는 장치 정책 블록만 보지 말고 profile의 `staticPrim(...)` 카운터로 확인한다.

Circle, rounded rectangle, primitive border를 포함한 static list는 static primitive expansion을 강제해도 legacy triangle geometry로 확장하지 않는다. 이 shape는 analytic fragment coverage가 필요하므로 primitive-instance 경로에 남고, plain filled rectangle만 static triangle expansion 대상이 될 수 있다.

Opaque `VkPipelineCache` 내용과 재사용 판단은 Vulkan 드라이버가 소유하고, Duxel은 `vkGetPipelineCacheData`가 반환한 바이트의 영구 저장만 담당한다. 이 저장 파일은 vendor ID, device ID, `pipelineCacheUUID`로 구분한다. Duxel은 드라이버에 데이터를 다시 넘기기 전에 32바이트 Vulkan 헤더를 검증하고, 파일이 없거나 호환되지 않거나 읽을 수 없으면 빈 메모리 `VkPipelineCache`로 시작한다. 새 바이트는 고유 임시 파일에 기록한 뒤 원자적으로 교체한다. Reader는 교체 공유를 허용하므로 여러 Duxel 프로세스가 동시에 실행돼도 부분 기록 파일이 아니라 완전한 이전/새 파일을 읽는다. 영구 저장 실패는 다른 렌더러를 선택하지 않으며 이미 유효한 Vulkan pipeline의 렌더링을 막지 않는다. 드라이버는 이 Duxel 관리 파일과 별개인 내부 cache를 추가로 운영할 수 있다.

`staticPrim(...)` profile block의 `layout=...`은 해당 frame에서 materialize한 expanded primitive layout 수다. 안정 static circle cache는 생성/rebuild에서만 layout을 materialize해 `layout>0`을 기록하고 cache-hit frame에서는 `layout=0`이어야 한다. steady-hit에서 0이 아니면 CPU layout work가 반복되고 있다는 뜻이다.

Static primitive auto decision의 구현 경계는 `VulkanRendererBackend.StaticPrimitivePolicy.cs`다. Static geometry 코드는 upload code 안에 device/heuristic decision을 직접 넣지 말고 이 policy 경계를 호출해야 한다.

### GPU-driven 렌더러 아키텍처 (2026-07-03)

Vulkan backend는 **graphics pipeline 1개**, **bindless texture descriptor set 1개**, **셰이더 2개**(`Shaders/imgui.vert`, `Shaders/imgui.frag`)로 구성된 GPU-driven 렌더러다:

- **Bindless texture**: 전역 `sampler2D[]` combined-image-sampler 배열(용량 4096, UPDATE_AFTER_BIND + PARTIALLY_BOUND) 하나를 프레임당 1회 bind한다. Texture는 `TextureResources`의 free-list allocator에서 slot index를 받고, slot은 deferred texture-destroy 시점에 재활용된다. Per-texture descriptor set은 존재하지 않는다.
- **통합 dual-source blending**: 모든 draw가 premultiplied color와 per-channel blend factor를 출력한다(`One/OneMinusSrc1Color`, `One/OneMinusSrc1Alpha`). 일반 draw는 `blendFactor = vec4(alpha)`를 출력해 `SrcAlpha/OneMinusSrcAlpha`와 수학적으로 동일하고, ClearType subpixel 텍스트는 per-channel coverage를 출력하며 fragment texture-index push constant의 최상위 비트로 선택된다.
- **Vertex pulling**: pipeline에 vertex input state가 없다. Vertex shader가 buffer device address(`GL_EXT_buffer_reference`)로 20바이트/5-dword `UiVertex` 스트림과 32바이트/8-dword packed `PrimitiveInstance` 레코드를 직접 읽는다. Primitive record는 bounds, radius/type encoding, border thickness, fill color, border color를 담는다.
- **Push constant 레이아웃**: vertex range `[0,40)` = `scale`(8) + `translate`(8) + `opacity`(4) + `drawMode`(4) + vertex-buffer address(8) + primitive-buffer address(8); fragment range `[40,44)` = packed texture index + subpixel mode bit. `drawMode` 0 = indexed triangle pulling, 1 = analytic primitive instance(fill/circle은 6-vertex bounding quad, border-only rounded rectangle은 48-vertex 8-cell perimeter).
- **Per-draw state**: `vkCmdBindPipeline`은 프레임당 1회다. Draw별 변화는 push constant, index-buffer bind, dynamic scissor뿐이다. `vkCmdBindVertexBuffers`는 backend에 더 이상 존재하지 않는다.
- **Dynamic rendering**: render pass/framebuffer 객체가 없다. 프레임은 `RenderingAttachmentInfo`와 함께 `vkCmdBeginRenderingKHR`/`vkCmdEndRenderingKHR`로 기록하며, MSAA는 inline resolve(`resolveMode = AVERAGE`, MSAA target → swapchain image)로 처리한다. 암묵적 render-pass 전환 대신 명시적 image barrier를 사용한다: 렌더링 전 `UNDEFINED→COLOR_ATTACHMENT`, 후 `COLOR_ATTACHMENT→PRESENT_SRC`. Pipeline은 swapchain format을 담은 `PipelineRenderingCreateInfo`를 체인한다.
- **필수 device feature**(없으면 명시적 실패, fallback 없음): `shaderSampledImageArrayDynamicIndexing`, `descriptorBindingSampledImageUpdateAfterBind`, `descriptorBindingUpdateUnusedWhilePending`, `descriptorBindingPartiallyBound`, `runtimeDescriptorArray`, `bufferDeviceAddress`, `dualSrcBlend`, `dynamicRendering`(`VK_KHR_dynamic_rendering`).
- **Dynamic geometry 메모리**: 프레임별 vertex/primitive buffer는 BAR 메모리(`DEVICE_LOCAL|HOST_VISIBLE|HOST_COHERENT`)를 우선 선택하고, 장치에 없으면 명시된 host-visible 요구 집합으로 할당한다. Vertex pulling 성능에 필수적인 선택이다: 일반 host 메모리에 대한 셰이더 BDA 읽기는 정점마다 PCIe를 건너서 dynamic 장면이 측정 가능하게 느려진다.

### 렌더러 모듈 맵

`VulkanRendererBackend`는 19개 partial-class 모듈로 구성된다. 새 단일 목적 파일을 추가하지 말고 대응하는 모듈을 확장한다:

- `VulkanRendererBackend.cs` — `IRendererBackend` shell, constructor/bootstrap 순서, lifecycle/teardown, render-entry frame 순서, settings/API mutation.
- `VulkanRendererBackend.Device.cs` — instance/surface/physical-device/queue/device 셋업, 필수 feature 게이트, vendor/device policy state와 env parsing, pipeline cache identity.
- `VulkanRendererBackend.Swapchain.cs` — swapchain selection policy, 리사이즈 전용/전체 recreate 흐름, image view/MSAA target, semaphore capacity, live resize 중 non-swapchain renderer resource 보존.
- `VulkanRendererBackend.PipelineResources.cs` — bindless descriptor layout/pool/set, pipeline layout/push range, 단일 graphics pipeline, sampler, 검증·원자 저장 pipeline cache와 진단, embedded shader loading.
- `VulkanRendererBackend.Resources.cs` — generic buffer/image/view/memory helper(preferred-memory 선택을 포함한 `CreateBuffer`, `GetBufferDeviceAddress`), image layout transition policy와 `imgTrans(...)` counter.
- `VulkanRendererBackend.TextureResources.cs` — texture create/update/destroy, bindless slot allocator와 descriptor write, upload batching, font/white texture id classification, deferred destroy.
- `VulkanRendererBackend.UploadScheduler.cs` — staging buffer lifetime, upload batching(`DUXEL_VK_UPLOAD_BATCH`), upload queue policy(`DUXEL_VK_UPLOAD_QUEUE`), submission/wait accounting.
- `VulkanRendererBackend.DynamicGeometry.cs` — 프레임별 vertex/index/primitive buffer capacity/mapping(BAR 우선), dynamic geometry upload, frame geometry preparation.
- `VulkanRendererBackend.StaticGeometry.cs` — static cache identity/lifetime/LRU, content/shape derivation, frame preparation, policy application, replacement policy.
- `VulkanRendererBackend.StaticGeometryStorage.cs` — static device-local buffer materialization(device address 포함), static upload/layout writing, retired-buffer pool.
- `VulkanRendererBackend.StaticPrimitivePolicy.cs` — static primitive triangle-expansion auto policy(`DUXEL_VK_STATIC_PRIMITIVE_TRIANGLES`), byte/mutation guard, decision profiling.
- `VulkanRendererBackend.Frame.cs` — frame orchestration(acquire/submit/present/fence), shared frame-fence wait, frame command-recording 준비, frame completion.
- `VulkanRendererBackend.CommandRecording.cs` — `RecordCommandBuffer`(bindless set과 단일 pipeline을 1회 bind), frame/render-pass begin/end, recording state aggregation, frame/draw-list context, draw-list traversal.
- `VulkanRendererBackend.CommandState.cs` — draw-mode push state, texture-index push state, vertex/primitive address push + index-buffer bind state, transform/opacity push state, scissor state.
- `VulkanRendererBackend.CommandDraw.cs` — draw-path orchestration, draw dispatch(indexed/instanced), command classification, texture lookup cache, primitive instance encoding.
- `VulkanRendererBackend.CommandScheduling.cs` — opt-in overlap-constrained command scheduler(`DUXEL_VK_COMMAND_SCHEDULER`), schedule cache, merge expansion.
- `VulkanRendererBackend.CommandDiagnostics.cs` — command diag gating/label(`DUXEL_VK_COMMAND_DIAG`), font diagnostics(`DUXEL_VK_FONT_DIAG`), record profile counter와 `CommandRecordStats` 구성.
- `VulkanRendererBackend.Diagnostics.cs` — profile line 출력, device/policy 텍스트, GPU timestamp 해석, 실패 추적.
- `VulkanRendererBackend.Types.cs` — 공용 renderer record(device address를 포함한 `StaticGeometryBuffer`, slot index를 갖는 `TextureResource`, frame resource).

Upload command pool, staging buffer lifetime, `DUXEL_VK_UPLOAD_BATCH`, `DUXEL_VK_UPLOAD_QUEUE`, staging offset reservation, batch flush, graphics upload-prepare command submission, transfer upload-copy submission, single-time upload submission 책임은 `VulkanRendererBackend.UploadScheduler.cs`에 둔다. Texture update와 static geometry upload 코드는 자체 staging command 경로를 늘리지 말고 이 경계를 호출해야 한다. Upload batching은 기본 enabled이며, 진단이 필요할 때만 `DUXEL_VK_UPLOAD_BATCH=0`, `false`, `off`로 끈다. Transfer upload path는 opt-in 전용이다. 대상 vendor/device의 focused texture/page upload gate가 split path가 더 빠르다는 것을 증명하기 전까지 default graphics path를 유지한다. 로컬 NVIDIA `10de:2f58` gate에서는 `upSched(prepSub=1 ...)`와 더 긴 upload wait가 짧은 transfer-copy submission 이득을 상쇄해서 transfer가 generic texture update에서 약 `12-13%`, DirectText page update에서 약 `15-19%` 느렸다.

Image layout transition policy는 `Resources` 모듈에 둔다. Texture/page upload가 barrier-heavy인지 copy-heavy인지는 `imgTrans(total=... toDst=... toShader=... present=... color=... xferStage=... gfxStage=... us=...)`로 증명한다. `texture_upload_barrier_bench_fba.cs`에서는 같은 texture의 non-overlapping region batch가 `total=2`, many-texture update가 `total=2 * textureCount`로 나와야 한다. `xferStage`는 stage mask가 transfer queue와 호환된다는 뜻이고, `gfxStage`는 transfer-only upload recording 전에 graphics/fragment/color stage mask를 split하거나 옮겨야 한다는 뜻이다.

폰트 command 진단은 `DUXEL_VK_FONT_DIAG=1`로 켠다. 매우 자세한 command 로그는 `DUXEL_VK_FONT_DIAG_OUT`으로 파일에 기록할 수 있다.

일반 Vulkan command sequence 진단은 `DUXEL_VK_COMMAND_DIAG=1`로 켠다. `DUXEL_VK_COMMAND_DIAG_OUT`을 지정하면 draw command의 `pipe`, texture id, clip, static 여부를 파일에 기록해서 pipeline 전환 원인을 추적할 수 있다. 기본은 `DUXEL_VK_COMMAND_DIAG_EVERY=120`, `DUXEL_VK_COMMAND_DIAG_FRAMES=1`로 제한된다.

Draw-list command 병합에서 callback 없는 zero-element command는 draw-order barrier가 아니라 state placeholder로 취급한다. 실제 draw를 추가할 때 builder는 trailing empty placeholder를 제거한 뒤 contiguous command merge 가능성을 검사한다. 병합 조건에는 opacity, texture, clip, translation, kind, vertex offset, callback, user data를 계속 포함해야 한다. caller가 effective clip을 이미 알고 있으면 draw 하나를 위해 builder clip stack을 push/pop하지 말고 clipped `AddImage(...)` 같은 clipped draw helper를 우선 사용한다.

Sampler-free pipeline variant는 더 이상 존재하지 않는다. GPU-driven 렌더러 전환 이후 모든 draw가 bindless texture 배열을 샘플링하고, solid draw는 앱의 1x1 흰색 텍스처를 샘플링하며, pipeline이 하나뿐이므로 pipeline 선택 비용 자체가 사라졌다. `DUXEL_VK_TRIANGLE_COLOR_PIPELINE`, `DUXEL_VK_SOLID_UNIFIED_PIPELINE`, `DUXEL_VK_SOLID_UNIFIED_STATIC`은 분리 pipeline 및 solid-triangle sentinel과 함께 제거되었으므로 다시 도입하지 않는다.

텍스트 렌더러 A/B 프로파일링은 `DUXEL_TEXT_RENDERING=direct`, `atlas`, `auto`로 수행한다. 기본값은 `DirectText`다. 명시적 `atlas`는 atlas renderer 비교용이고, 명시적 `auto`는 atlas 우선 렌더링을 유지하되 atlas에 없는 글자만 DirectText로 즉시 시각 fallback하므로 그 fallback 동작이 의도된 경우에만 사용한다.

DirectText 페이지 텍스처 패킹은 `DUXEL_DIRECT_TEXT_PAGE=1`로만 켠다. DirectText bitmap을 1024x1024 page texture에 넣고 각 region 둘레에 1px border를 예약한 뒤 edge pixel을 border로 복제해서 atlas sampling bleed를 줄인다. Page 생성은 전체 1024x1024 pixel buffer가 아니라 border-inclusive packed region만 업로드하고, 같은 texture의 연속 non-overlapping partial update는 하나의 Vulkan upload/copy submission으로 batch될 수 있다. Upload batching이나 transfer-queue default를 바꾸기 전에는 `samples/fba/directtext_page_upload_bench_fba.cs`를 이 경로의 초점형 upload-policy gate로 사용한다. DirectText page quad는 `PushClipRect -> AddImage -> PopClipRect` sequence 대신 clipped image helper를 사용해야 한다. 기본값은 시각 품질 보존을 위해 off이며, 렌더된 글자 외형 검증 없이 기본 경로로 승격하지 않는다.

2026-07-10 기준 page-enabled 성능 실행은 `1819.709` FPS를 기록했지만, 렌더된 all-features capture는 UI가 누락된 대부분 검은 화면이었다. 이 결과는 기각된 진단 후보로 취급하고 page mode는 기본 off를 유지하며, 재검토 전에 시각 비교를 통과해야 한다.

## 제약과 비목표

아래 저장소 규칙은 작업이 명시적으로 바꾸지 않는 한 유지되는 전제로 본다.

- .NET 8, .NET 9 및 .NET 10 패키지 타깃
- Windows 우선 플랫폼 현실
- Vulkan 렌더러 전제
- NativeAOT 친화 방향
- fallback 경로를 가볍게 늘리지 않음
- 방어적 코드 남발로 근본 원인 수정을 대체하지 않음

무언가가 미지원이면 숨은 fallback 대신 명시적 실패나 명시적 범위 제한을 선호한다.

## 에이전트 작업 절차

권장 순서:

1. 작업이 코어 UI, 앱 부트스트랩, Windows 플랫폼, Vulkan 렌더링, DSL, 문서 중 무엇인지 식별한다.
2. 이 문서의 가장 가까운 섹션을 먼저 읽고, 예제가 더 필요할 때만 가장 가까운 샘플을 읽는다.
3. 아키텍처 경계를 지킨다.
4. 실제 문제를 해결하는 가장 작은 변경을 한다.
5. 빌드나 관련 샘플 실행으로 검증한다.
6. 공개 동작, 워크플로, 확장 지점이 바뀌면 문서를 갱신한다.
7. 안정적인 새 규칙이 생기면 이 문서와 영문 대응 문서를 함께 갱신한다.

## 함께 읽을 문서

- `README.ko.md` — 패키지 개요와 빠른 시작
- `docs/duxel-agent-reference.md` — 영문 대응 기준 문서
- `docs/extended-title-bar-guide.ko.md` — ExtendedContent 타이틀바의 앱 레이아웃, 플랫폼 계약, AI 검증 절차
- `docs/getting-started-fba.ko.md` — FBA 첫 시작 가이드
- `docs/fba-reference-guide.ko.md` — 패키지/프로젝트 전환과 `run-fba.ps1`
- `docs/fba-run-samples.ko.md` — 샘플 카탈로그와 원클릭 실행
- `docs/custom-widgets.ko.md` — 재사용 위젯 확장 경로
- `docs/ui-dsl.ko.md` — 선언형 UI 문서
- `docs/version-history.ko.md` — 그룹화된 버전 이력
- `docs/design.ko.md` — 현재 아키텍처와 설계 기준

## 유지보수 메모

미래의 에이전트나 개발자가 Duxel을 올바르게 쓰기 위해 꼭 알아야 하는 안정적인 워크플로 규칙을 새로 발견하면 여기에 반영하고 `Last synced` 날짜를 함께 갱신한다.

## 공통 레이어 배칭

Vulkan의 GPU 자원 바인딩은 참조형 레코드로 전달해 준비·분류·기록 단계의 큰 값 복사를 줄인다. GPU 버퍼 수명은 기존 fence/퇴역 절차가 관리하며 레코드의 GC가 GPU 자원을 해제하지 않는다. `UiRendererFrameMetrics.RendererAllocatedBytes`는 profiling을 켰을 때 렌더러 호출 본문의 해당 스레드 관리 할당량이며, 끄면 null이다. UI 장면 생성 할당 및 전체 프로세스 메모리와 구분한다. ERD 결과의 `rendererAllocatedBytesPerFrame`은 이전 실행 파일에 계측이 없으면 미확인으로 유지한다.

`UiDrawList.ComputeCommandTopologyStamp`는 명령의 인덱스 범위·정점 오프셋·종류·텍스처·콜백 유무를 판별한다. 정적 레이어가 같은 크기의 명령 배열을 재사용해도 이 정보가 바뀌면 새 명령 래퍼로 캐시를 갱신한다. 지오메트리 버전과 명령 위상 변경을 별도로 추적해 텍스처만 바꿔도 GPU 결합 계획이 재평가되며 정점·인덱스는 재업로드하지 않는다.

CPU 배칭은 다음 목록의 3D 상태가 같을 때만 명령·콘텐츠 이력을 검사한다. 묶인 출력 목록의 프레임 래퍼는 배처가 단독으로 빌려 준 기간에만 유효하며, `Frame.Dispose()` 이후 다음 프레임에서 재사용될 수 있다. 원본 목록과 지오메트리는 변경하지 않는다. 장기 보관할 카메라는 `UiLayer3D` 값을 복사한다. 래퍼 복사 할당은 제거했지만 묶인 `UiDrawData` 프레임 헤더 할당은 남아 있다.

`BenchFrameRecorder.FrameSeconds`는 기록 순서의 읽기 전용 표본 뷰다. 다음 기록·초기화로 내용이 달라질 수 있으므로 원시 CSV는 측정 종료 후 이 뷰에서 저장한다. `tests/Duxel.Performance.Bench <output.json> --retained`는 CPU 배처만 측정하며 결과의 처리 횟수를 화면 FPS로 해석하지 않는다. ERD의 목표 1000 FPS·시나리오별 현재값·달성률은 측정 요약의 `fpsTargets` 및 `fps-target.md`에 기록한다.

`UiLayer3D.PlaneModel = new UiLayerPlaneModel(translation, depthOffset)`은 공통 `Transform` 카메라와 평면별 원본 픽셀 이동·투영 후 정규화 깊이를 분리한다. 슬롯 워밍업 후 카메라 이동만으로는 레이어별 버퍼를 쓰지 않고, 모델 하나 변경에는 16B만 쓴다. 카메라 push constant와 최종 GPU 그리기는 별개다. `SharedCameraLayerDraws`와 `DUXEL_VK_SHARED_LAYER_CAMERA=0`으로 확인한다. 일반 회전·월드 Z·완성된 MVP 경로는 유지하며 자세한 좌표 계약은 연결된 레이어 문서를 따른다.

공통 행렬 기저 최적화는 `SharedBasisLayerDraws`로 확인하며 `DUXEL_VK_SHARED_LAYER_BASIS=0`으로 비교한다. 개별 CPU 가시성 행렬과 GPU에 전달한 행렬을 별도로 추적해 인접 묶음의 같은 카메라 기저를 중복 전송하지 않는다. 카메라 변경 시 도형 생성·업로드는 생략해도 보이는 벡터의 GPU 투영·래스터화는 수행한다.

Vulkan은 서로 다른 3D 행렬을 가진 resident 목록도 GPU 명령 데이터와 유지된 인덱스 스트림으로 결합한다. `CompiledLayerDraws`/`CompiledLayerCommands`는 실제 결합 드로우/원본 명령 수다. `CompiledIndexUploadBytes`, `LayerStateUploadBytes`, `CompiledIndexCacheBytes`는 추가 전송과 메모리다. `DUXEL_VK_COMPILED_LAYERS=0`은 비교용 직접 제출을 선택한다. 범위·상한·현재 추가 메모리는 [동적 3D 레이어](dynamic-3d-layers.ko.md)를 참고한다.

Vulkan은 목록 전체 경계를 상태 설정 전에 검사하고, 완전히 내부인 목록에서는 명령별 중복 검사를 생략한다. `UiRendererFrameMetrics.LayerTransformPushes`는 레이어 행렬 push 횟수, `CulledLayerLists`는 목록 전체 제외 횟수다. GPU 자원의 유지와 가시성은 분리한다.

`UiDrawData`는 `IDisposable`이다. `using var drawData = ui.GetDrawData()`로 렌더링·계측 종료 시 프레임 자원을 반환할 수 있다. 해제된 프레임의 목록 객체는 재사용되므로 이후 접근하지 않는다. 원본 지오메트리 캐시와 프레임별 행렬 상태의 수명은 분리된다.

`UiRetainedLayerBatcher.Prepare(UiDrawData)`는 동일한 3D 상태의 인접 resident 목록을 결합하고 CPU 벡터 데이터를 유지한다. Vulkan은 이를 자동 적용한다. 반환된 `Frame.DrawData`는 `Frame.Dispose()`까지 동기적으로 빌려 쓰며, 입력 데이터의 소유권은 호출자에게 남는다. 캐시 종료는 `Dispose()`로 처리한다. `UiRendererFrameMetrics.BatchedSourceLists`와 `RebuiltBatchGroups`는 엔진 배칭 계측값이다. 상세 조건과 제한은 [동적 3D 레이어](dynamic-3d-layers.ko.md)를 참고한다.

`UiDrawList.CommandStateStamp`는 변경 없는 명령 디스크립터 상태를 나타내는 0이 아닌 값이다. 명령 종류·범위·텍스처·클립·이동·불투명도를 바꾸면 `ComputeCommandStateStamp(commands)`로 다시 계산한다. 0은 미확인이며 디스크립터 재사용을 끈다. 명령을 수정한 draw list 복사본도 스탬프를 다시 계산하거나 0으로 지워야 한다. 지오메트리 바인딩과 화면 변환은 별도로 캐시를 무효화한다. 상주 레이어 재생과 유지 배칭은 스탬프를 자동 설정한다. 슬롯 준비 후 카메라만 움직이면 디스크립터 재구성을 생략하며 CPU 가시성 검사와 GPU 그리기는 계속 수행한다. 가려진 명령 데이터도 채운 뒤 소스를 캐시하며, 화면 변환 변경은 가려진 멤버까지 무효화한다. `UiRendererFrameMetrics.CompiledDescriptorBuilds`는 CPU 디스크립터 구성 횟수이며 GPU 전송 바이트와 구분한다. `DUXEL_VK_DESCRIPTOR_REUSE=0`으로 끌 수 있다. `scripts/measure-erd-layers.ps1 -CompareDescriptorReuse -Executable <exe>`는 같은 실행 파일로 두 경로를 비교한다.

`UiRendererFrameMetrics.PresentationSequence`는 Vulkan `QueuePresent`가 성공 또는 suboptimal을 반환했을 때만 증가한다. `PresentationTimestampTicks`는 `Stopwatch` 시각이며, `PresentationIntervalSeconds`는 성공한 요청 사이의 간격이다. 첫 간격은 0이다. 건너뛴 렌더 호출은 이전 지표를 유지할 수 있으므로 순번이 증가했을 때만 간격을 소비한다. 모니터의 실제 표시 횟수나 GPU 완료 횟수와 구분한다. ERD 상태 패널과 원시 CSV는 이 시계를 사용하고 `fpsTimingBasis: accepted-presentation`을 기록한다. 해당 필드가 없는 과거 결과는 UI 루프 간격을 사용했다. 측정 실행기는 서로 다른 시계의 비교를 거부한다.

화면 안에 완전히 들어오고 명령 상태가 유지된 컴파일 멤버는 원본 명령을 순회하지 않고 연속된 기존 인덱스 범위를 사용한다. 부분 가시성·미확인·변경 상태는 명령 단위 순회를 유지한다. 미리 계산한 종류 전환 요약은 멤버 경계와 빈 명령을 포함해 프로파일 통계를 보존한다. `CompiledCommandVisits`는 실제 원본 명령 루프 방문 횟수이고, `CompiledLayerCommands`는 제출한 비어 있지 않은 원본 명령 수다. `DUXEL_VK_COMMAND_SPANS=0` 또는 `-CompareCommandSpans -Executable <exe>`로 비교한다. 카메라·모델 갱신과 GPU 그리기는 계속 필요하다.

공통 Vulkan 레이어 가시성 경로는 직접 제출을 포함해 0이 아닌 `CommandStateStamp`로 전체 경계를 재사용한다. `ComputeCommandStateStamp`는 이제 경계 좌표와 `HasBounds`도 포함하며, 이 값이 바뀌면 스탬프도 갱신해야 한다. 캐시에는 원본 좌표의 경계만 저장하고 매 프레임 현재 카메라·모델·화면 변환으로 가시성을 다시 판정한다. 미확인 스탬프는 경계를 다시 합산하고 미확인·잘못된 경계는 보수적으로 표시한다. 목록 순서가 바뀌면 위치별 캐시 상태를 비교한다. `LayerBoundsCommandVisits`는 레이어 경계 합산에 방문한 명령 수이며 `CompiledCommandVisits`와 구분한다. `DUXEL_VK_LAYER_BOUNDS_REUSE=0` 또는 `-CompareLayerBoundsReuse -Executable <exe>`로 비교한다. CPU 경계만 재사용하며 레이어별 래스터 타깃을 추가하거나 GPU 벡터 그리기를 생략하지 않는다.

`UiImmediateContext.TryReplayLayer(id, in options)`는 스코프를 열지 않고 유효한 상주 내용을 제출한다. false이면 기존 `BeginLayer`/`EndLayer` 경로로 내용을 생성하거나 갱신한다. 일치하는 내용 버전(또는 정적 캐시), 변경되지 않은 항목과 저장된 내용이 필요하며 유효한 빈 내용도 true다. 실패한 조회는 스코프를 열거나 저장된 버전을 바꾸지 않는다. 성공한 재생은 현재 장면을 검증하고 불투명도·이동·부모의 텍스트 의존성과 프레임별 카메라 래퍼를 보존한다. 직접 재생에 성공했으면 `EndLayer`를 호출하지 않는다. ERD의 격자·관계선 묶음·선택 관계선·테이블·미니맵은 이 공통 엔진 API를 사용한다. `DUXEL_ERD_DIRECT_REPLAY=0`이면 기존 스코프를 사용하고, `scripts/measure-erd-layers.ps1 -CompareDirectLayerReplay -Executable <exe>`로 같은 실행 파일을 비교한다. 스코프 준비와 복사는 줄이지만 레이어별 조회·제출은 계속하며 장면 전체를 한 번에 제출하는 API는 아니다.

```csharp
if (!ui.TryReplayLayer(id, options))
{
    if (ui.BeginLayer(id, options)) DrawContent(ui);
    ui.EndLayer();
}
```

상주 지오메트리와 명령 상태는 첫 상주 제출에서 획득해 `UiDrawData`로 전달하는 CPU 프레임 단위 임대 하나로 보호한다. 반환 대기 중인 풀 버퍼는 이를 참조하는 마지막 프레임 세대가 끝나면 반환하므로, 새 프레임이 계속 겹쳐도 오래된 자원이 쌓이지 않는다. 빌린 프레임이 남아 있으면 상주 소스나 재생 명령 배열을 수정하기 전에 복사한다. 카메라만 이동하는 프레임은 기존 지오메트리를 계속 공유하며 GPU 전체 대기나 레이어 렌더 타깃을 추가하지 않는다.

`GetDrawData()`가 반환한 소유 객체는 소비 후 해제한다. 반복 호출이나 레코드 복사로 별도의 소유권이 생기지는 않는다. `UiContext`는 수령하지 않은 출력을 다음 프레임·다음 Render·EndFrame·종료 시 정리하고, 중단된 렌더의 보류 중인 상주 프레임 임대도 반환한다. 수령한 프레임은 UI 컨텍스트가 종료된 뒤에도 지오메트리를 유지할 수 있다. 이 보장은 지오메트리·명령 스냅샷에 관한 것이며 UiContext의 동시 쓰기를 허용하는 것은 아니다.

`UiDrawData.ScaleClipRects`와 `UiDrawList.ScaleClipRects`는 빈 경우도 독립 소유 복사본을 만든다. 원본의 지오메트리·카메라 프레임 임대는 복사하지 않고, 지오메트리·명령과 함께 텍스처 갱신 목록 컨테이너를 복사한다. 클립 배율 복사본의 텍스처 픽셀 데이터도 독립 소유 메모리에 복사한다. 원본과 클립 배율 복사본은 독립적으로 해제할 수 있다.

`UiImmediateContext.GetDrawDataFrameId()`는 UiContext 안에서 현재 Render 호출을 식별한다. 이 값은 `UiDrawData.DrawDataFrameId`로 전달되고 클립 복사본에도 유지되며 `UiRendererFrameMetrics`에 포함된다. 렌더러의 성공한 표시 순번인 `PresentationSequence`나 지오메트리 내용 버전과는 구분한다.

`GetPresentationReader()`는 일관된 최신 지표 스냅샷·성공한 표시 표본의 유한 FIFO·덮어쓴 표본 수를 제공하는 `IUiPresentationReader`를 반환한다. `Drain(Span<UiRendererFrameMetrics>)`는 단일 소비자용 표본을 꺼내며 UI 상태 버퍼가 종료된 뒤에도 마지막 결과를 읽을 수 있다. UI 편의 메서드도 같은 이력을 사용한다. 같은 표시 순번은 최신 지표만 갱신하고 표본을 중복 추가하지 않으며, 하나의 렌더러 이력에서 순번이 역행하면 거부한다. GPU 타임스탬프는 기존 프레임 슬롯 지연을 유지하므로 현재 소스 ID의 GPU 시간으로 재해석하지 않는다. UiContext 자체의 동시 쓰기를 허용하는 기능은 아니다.

ERD는 `presentation-history-v1`로 CPU 장면 통계를 소스 프레임 ID에 연결하고, 표시 순번의 연속성·실제 표시 시각에 따른 준비 구간·저장 시 마지막 결과를 검사한다. 기존 한 열짜리 프레임 CSV는 유지하고 `.presentations.csv`로 각 표본의 표시 순번·소스 ID를 기록한다. 별도 시간 버퍼 대신 BenchFrameRecorder.FrameSeconds를 재사용하고 같은 용량의 기존 버퍼를 소스 ID 저장에 사용한다. 실행기는 연결 행·순서·개수·덮어쓰기·준비 구간 정보를 감사하며, 수집 방식이 다르면 개선율을 내지 않는다. 아래의 제한된 선택형 파이프라인이 이 표시 이력을 사용한다.

`DuxelFrameOptions.EnableRenderPipeline`으로 UI 생성과 렌더러 실행을 겹친다(기본 false). 전용 소비자가 Vulkan 호출·설정·표시 피드백을 담당하고 UiContext는 생산자 하나만 쓴다. 대기 프레임1개와 실행 프레임1개로 제한하며 넘치는 프레임을 버리지 않고 생산자를 기다리게 한다. 설정은 소스 프레임과 함께 전달하고 리사이즈·최초 표시 알림은 실제 표시 성공 뒤 발생한다. 종료 시 대기 프레임을 처리하고 스레드를 합류시킨 뒤 UI·렌더러를 해제한다. 소비자 오류는 대기 프레임의 소유권을 해제하고 종료 경로를 깨우며 원래 예외를 전달한다.

`UiDrawData.CaptureTexturePayloads()`는 독점 소유권을 다른 스레드에 넘기기 전 텍스처 갱신 데이터 조각을 풀에서 빌린 프레임 소유 버퍼 하나에 복사한다. 픽셀 데이터가 없는 프레임은 버퍼를 만들지 않고 변경 없는 상주 지오메트리도 복사하지 않는다. 생산자가 원본 픽셀 메모리를 수정하기 전에 호출해야 한다. `ScaleClipRects`도 픽셀 데이터를 독립 복사하므로 원본 프레임 해제 뒤 사용할 수 있다. 레코드 복사는 독립 수명이 아닌 공유 소유권이다. 콜백이 있는 프레임은 완료 장벽을 통과한 뒤 다음 UI 프레임을 생성하고 일반 프레임은 겹쳐 실행한다. 이는 UI와 콜백의 동시 실행 방지이며 콜백의 스레드 ID 보장은 아니다. 외부 콜백은 UI 생산자 스레드에서 실행된다고 가정해서는 안 된다.

ERD는 `DUXEL_ERD_PIPELINE=1`로 이 엔진 옵션을 선택하고 실행 JSON의 `renderPipeline`에 기록한다. 측정 실행기의 `-CompareRenderPipeline`은 같은 실행 파일에서 Off/On 순서를 교대하고 결과의 모드를 검사한다. GPU 검사에는 큐의 생산자 대기·순서·종료·예외 전달·텍스처 소유권·Vulkan 소비 전 생성한 상주 지오메트리3개 버전이 포함된다. 전체 성능·입력 검증 전에는 명시적으로 켜는 옵션으로 유지한다. 병렬 APP_PROFILE의 `getDraw`에는 큐 대기 시간이 포함되고 `total`은 소스 생성부터 렌더 완료까지의 지연이며 직렬 CPU 작업량이 아니다. SubmitTimeMs는 마지막 완료 렌더 시간이다.
파이프라인의 준비 구간은 `fpsWarmupMethod: first-accepted-presentation-v1`이다. 두 번째 UI 호출이 아닌 최초 표시 성공 시각에서 정확히 Stopwatch 1초 뒤를 마감으로 사용한다. JSON에 시작 시각과 시계 주파수를 남기고 비교 실행기는 준비 시간·최초 측정 시각·samples와framesAfterWarmup 개수 일치를 검사한다. 준비 방식이 다르면 개선율을 계산하지 않는다. UI 기준이었던 앞선12회 결과는 render-queue-ab에 INVALID_FOR_FINAL_COMPARISON으로 분리 보존하며 현재 FPS로 사용하지 않는다. 직접 소스에서 확인한 원인과 수정 후 재측정 예산도 기록했다. ERD는 이제 엔진 파이프라인을 기본으로 켜며 `DUXEL_ERD_PIPELINE=0`으로 직렬 실행을 선택한다. 라이브러리 옵션 자체는 콜백의 스레드 제약이 있는 소비자를 위해 명시적으로 켜도록 유지한다.
생산자는 입력을 읽고 UI를 만들기 전에 대기 슬롯이 빌 때까지 기다린다. 따라서 실행 프레임과 대기 프레임 뒤에 세 번째 CPU 프레임을 미리 만들지 않으며 렌더러와 UI 작업은 계속 겹친다. 콜백 명령은 요소·지오메트리가0이어도 실행 작업이다. 빌더 출력·정적 추가·상주 재생에서 보존하고 같은 콜백이 연속돼도 병합하지 않으며 Vulkan이 명령 순서대로 호출한다. 콜백 프레임은 완료를 기다린 뒤 다음 UI를 만든다. 병렬 모드의 콜백은 소비자 스레드에서 실행하며 제출한 지오메트리 수정이나 재귀 렌더링·Vulkan 기록 상태 변경은 허용하지 않는다.
이전 ERD 검증: .NET11 RC NativeAOT,1200테이블,dense/resident,MSAA4,VSyncoff,모드별18초×3회. 현재 카메라908.61FPS/엔티티 동작957.30FPS,목표1000,미달. 같은 이미지의 직렬 기준667.91/666.71FPS. 근거: artifacts/perf-2026-11-03/render-queue-admission-ab 및 render-queue-admission-native-promotion.json. NET10·NET11 GPU각322검사,직접 제출298검사. 최신 수동 입력·메모리·런타임 성능 감사는 남아 있다.

`DuxelFrameOptions.UseEventDrivenPlatformLoop`으로 UI·렌더러 스레드가 동작하는 동안 플랫폼 소유 스레드는 이벤트를 기다린다. 명시적인 `IPlatformEventWakeBackend` 기능이 필요하며 없으면 GPU 준비 전에 실패한다. 라이브러리 기본값은 false다. Windows의 `WakeEvents()`는 호출 시점을 보존하는 스레드 안전 신호이며 MsgWaitForMultipleObjectsEx/MWMO_INPUTAVAILABLE로 이 신호와 입력 큐를 함께 기다린다. SafeHandle 참조로 네이티브 대기 중 핸들을 보호하고 이미 확인한 미처리 입력도 전달한다. 대기·반환 오류는 명시적으로 실패시키며 깨우기 실패를 주기적 확인 시간으로 대신하지 않는다.

Exit는 UI·플랫폼 대기를 깨우고 리사이즈 표시 승인 대기를 취소한다. 커서 변경과 작업 스레드 오류도 플랫폼 소유자를 깨운다. 동시 호출에 사용하는 잠금 아래에서 세션의 깨우기 콜백을 해제한 뒤 백엔드·핸들을 정리한다. 플랫폼이 잠든 동안에도 RequestFrame으로 UI·렌더링을 진행한다. 최초 표시·리사이즈 알림은 기존처럼 실제 표시 성공에 연결한다. 검사는 실제 App 어셈블리를 참조하며 WM_QUIT가 스레드의 큐에 속하므로 실제 앱 검사는 독립 소유 스레드를 쓴다. 검사 시간 제한에 의한 강제 깨우기는 성공으로 처리하지 않는다.

ERD는 이 옵션을 기본으로 켜며 `DUXEL_ERD_EVENT_LOOP=0`으로 폴링을 선택한다. `-ComparePlatformEventLoop`은 렌더링 파이프라인을 켠 같은 실행 파일에서 두 모드를 교대한다. JSON에는 모드·앱 실행 구간 CPU밀리초·단조 시계의 벽시계 밀리초·두 값의 비율인 `averageBusyCpuCores`(논리 코어1개를 계속 사용하면1)·프로세스 수명 동안의 최대 작업 집합을 기록한다. CPU 구간은 초기화·준비 구간·종료를 포함하며 최대 작업 집합은 GPU VRAM이나 안정 상태 힙 사용량이 아니다. 카메라 시계·지오메트리·품질 경로는 유지한다.
최신 일반 실행 수정 ERD: .NET11 RC NativeAOT,1200테이블,시나리오별18초×3회,현재883.41/980.45FPS,목표1000미달. event-loop-live-current 및 event-loop-native-promotion.json 참조. 별도 동일 엔진의 이벤트 루프 비교에서 CPU 코어 상당 사용량42.09%/40.27%감소,FPS 향상률은 미확정이며 최대 작업 집합은 약1.62GiB다. NET10·11 GPU각332검사,직접 제출308검사. 일반 실행은 표본을 보관하지 않고24MiB 측정 버퍼를 만들지 않는다.6초 실행 및 소스 경로의 보장이며 장기 실행 검증은 아니다. 상세 프레임 진단은 로그 출력 비용을 포함한다.

상세 CPU 시간 계측은 `DUXEL_VK_PROFILE_EVERY`로 출력할 프레임에서 수행한다. `DUXEL_VK_PROFILE_TIMING_SAMPLING=0`은 기존처럼 매 프레임의 상세 CPU 시간을 구하고1이 기본값이다. 잘못된 불리언 값은 명시적으로 실패한다. 렌더러의 공통 시간 함수는 비수집 시작값으로0을 반환하고 종료 함수도 이 값에 대해0을 반환해 가짜 경과 시간을 만들지 않는다. 드로우·업로드·상주·명령 카운터, 할당 계측, GPU 타임스탬프와 성공한 표시 시계는 별개로 유지한다. 지오메트리·셰이더·MSAA·카메라 동작은 바꾸지 않는다.

`UiRendererFrameMetrics.CpuTimingCollected`는 상세 CPU 시간 수집 프레임을 나타내며 다른 프레임의 카운터·GPU 계측을 생략했다는 뜻이 아니다. ERD JSON은 준비 구간 이후 실제 `cpuTimingCollectedFrames`·`gpuTimingSamples`를 기록한다. `-CompareCpuTimingSampling`은 같은 Native 이미지에서 로그 주기1,000,000과 엔진 파이프라인·이벤트 루프를 고정해 비교한다. 기준 모드는 측정 프레임 모두, 샘플 모드는 짧은 실행 동안0프레임에서 상세 CPU 시간을 수집했는지 검사하고 GPU 계측·드로우·준비 구간·프로세스 계약도 확인한다. 유휴 벤치마크는 의도적으로 표본을 기록하지 않으므로 이 비교에서는 자식 실행 전에 거부한다.
2026-11-03 최신 FPS 기록: .NET11 RC NativeAOT,1200개 테이블,dense/resident,MSAA4,VSync off,최초 표시 성공 후1초 준비,시나리오별18초×3회. 목표는1000 FPS다. 카메라 이동 중앙값946.46 FPS(94.65%,범위903.91–960.34), 엔티티 동시 이동978.39 FPS(97.84%,범위936.18–1000.71). 단일 실행 평균 최고1000.71 FPS와 반복 중앙값을 구분하며 두 시나리오 모두 안정 달성은 미달이다. 실시간 패널은 FPS/1000을 표시한다. 원시 기록은 artifacts/perf-2026-11-03/cpu-timing-ab, 최신 기계 판독 기록은 fps-current.json에 보존한다. 전체12회 표시 연결·준비 구간·계측·프로세스 감사 통과. 세부 CPU 시간은 기본적으로 로그 프레임에서만 수집하지만 드로우/업로드 카운터와 GPU 시간, 표시 시계는 매 프레임 유지한다. 계측 On/Off FPS 실행 범위가 겹쳐 개선율은 미입증이다.

임시 CPU 빌더의 자동 예약량은 이제 버퍼를 소유하는 목록의 최대 청크 크기를 기준으로 한다. 빌린 상주 레이어의 전체 정점·인덱스 수를 임시 버퍼에 다시 예약하지 않는다. 공개 장면 합계·GPU 지오메트리·명시적인 Reserve API는 유지한다. NET10/11 GPU각366개, 직접 제출342개가 통과했다. NativeAOT 성능·메모리 비교 전이므로 이 변경의 FPS 향상이나 작업 집합 감소는 아직 주장하지 않는다.

2026-11-03 임시 빌더 예약 수정 비교 완료: 같은 .NET11 RC NativeAOT/1200테이블/dense/resident/MSAA4/VSync off,각18초×3회 교대 실행. 카메라874.66→921.29 FPS(+5.33%), 엔티티 동시 이동880.82→932.94 FPS(+5.92%)로 실행 범위가 겹치지 않는다. 최대 작업 집합 중앙값은 약0.25%/0.32% 감소했으나 여전히 약1.6GiB이며 VRAM 측정값이 아니다. p95/p99/1% low 실행 범위는 겹쳐 개선율 미입증. 현재 FPS는921.29/932.94,목표1000 대비92.13%/93.29%,최신 이미지 단일 실행 평균 최고999.52다. 과거 이미지의 최고1000.71은 별도 이력으로 보존한다. 표시 연결·준비·프로세스·계측·지오메트리 감사12/12통과,카메라 생성0/프레임·엔티티 생성1/프레임. artifacts/perf-2026-11-03/reservation-ab와fps-current.json참고. 기본NativeAOT EXE/PDB는 측정 이미지AC3D25FB와 해시 재확인 후 갱신했다.

프레임의 정적 기하 바인딩은 조밀한 draw-list 번호로 직접 조회하는 배열을 사용한다. 프레임 준비·동적 업로드·GPU 결합 계획·드로우 기록에서 같은 프레임 바인딩을 공유한다. 프레임마다 이전 참조를 지우고 새 목록 수로 범위를 설정하므로 목록 축소·빈 프레임·동적 목록에는 오래된 바인딩이 적용되지 않는다. GPU 자원의 소유·fence·퇴역 정책은 유지한다. 해시 조회 제거의 성능 효과는 별도 NativeAOT 비교 전에는 미확정이다.

배열 직접 조회 비교 완료(2026-11-03): .NET11 RC NativeAOT,1200테이블,dense/resident,각18초×3회. 카메라921.10→946.04 FPS,엔티티 동시 이동1006.53→974.12 FPS. 실행 범위가 겹쳐 개선율·회귀율은 미확정이다. 현재 카메라 범위721.13–1013.41,동시 이동880.19–984.70이며 낮은 실행도 보존했다. 최신 이미지 단일 실행 평균 최고1013.41,중앙값 목표 달성률94.60%/97.41%; 안정1000목표는 둘 다 미달. p95/p99/1% low도 범위가 겹친다. 동시 이동의 최대 작업 집합 중앙값은1730801664→1731289088바이트로 약0.47MiB 증가했고 범위가 겹치지 않는다. 카메라 메모리 범위는 겹친다. 표시/준비/계측/프로세스/지오메트리 감사12/12통과. 측정 지오메트리 생성0/1회/프레임과 모든 GPU 계측을 유지한다. 근거:artifacts/perf-2026-11-03/frame-bindings-ab,최신 요약fps-current.json. 기본EXE/PDB는 측정 이미지917A9C59와 해시가 일치한다.

현재 소스.NET10/11 Managed/NativeAOT비교12회/72구간이완료됐다. 소스·표본·유형·종료 감사와GPU진단72구간은artifacts/perf-2026-11-03/runtime-current에 있다. 상세 표는dynamic-3d-layers.ko.md참고. MSAA1/직렬/UI프레임 시계이므로ERD의MSAA4/병렬/표시 성공 시계1000FPS목표와 구분한다. 최신 데스크톱 상태 패널 캡처는자동승인검토거부로미검증이며 우회하지 않았다.

GPU 결합 그룹의 기본 인덱스 한도는2,097,152개다. DUXEL_VK_COMPILED_INDEX_LIMIT는1,048,576/2,097,152/4,194,304/8,388,608개만 허용하며 다른 값은 명시적으로 거부한다. 디스크립터256개와 전체 인덱스 캐시512MiB 제한은 유지한다. UiRendererFrameMetrics.CompiledIndexLimit는 실제 설정값을 보고한다. ERD 측정 JSON의 compiledIndexLimit와 compiledIndexCachePeakBytes는 실제 설정과 측정 프레임에서 관측한 최대 인덱스 캐시 바이트이며 전체VRAM이 아니다. scripts/measure-erd-layers.ps1 -CompareCompiledIndexCapacity는 같은 실행 파일에서1Mi/2Mi를 교대 비교하고 설정·캐시·드로우·GPU·표시 표본을 검증한다.1.2M 인덱스 GPU 경계 검사에서2→1드로우,동일 픽셀,카메라 변경 시 재업로드0을 확인했다. NET10/11각390검사·직접 제출366검사·실행기10/10을 통과했다. ERD FPS 효과는 반복 비교 전까지 미확정이다.

인덱스 한도1Mi/2Mi 동일 NativeAOT 비교 완료(2026-11-03):1200테이블,dense/resident,MSAA4,VSync off,각18초 명목 실행×3회. 현재 카메라988.05 FPS(98.80%,범위899.53–1044.10),엔티티 동시 이동966.90 FPS(96.69%,828.92–1074.02),단일 실행 평균 최고1074.02다. 안정1000목표는 둘 다 미달.1Mi/2Mi FPS는923.78→988.05,1058.46→966.90으로 실행 범위가 겹쳐 개선율·회귀율은 미확정이다. 평균 드로우는97.59→91.91,80.69→74.65이며 인덱스 캐시 최대는270330504/270330552바이트로 모드 간 동일하다. GPU 경계의2→1드로우·픽셀 동치와 실제 실행 평균을 구분한다. 프레임 평균은 카메라 상태의 표본 가중치를 포함하므로 같은 시점의 순수 작업 감소율로 해석하지 않는다. 원시 프레임·표시 연결·실제 한도·캐시·준비·프로세스·생성0/1회 감사12/12통과. 기본EXE/PDB는측정이미지60FE419D와 해시가 일치한다. 최신요약fps-current.json,근거index-capacity-ab참고.

측정 조건 점검:App의자동종료는폰트 준비 전appStartUtc에서 시작하고,ERD카메라 투어는렌더 시작부터진행하며,표본은첫표시성공+1초이후다. 실제표본구간은명목18초중14.84–15.21초였고가시명령의평균도달랐다. 이사실만으로FPS변동의모든원인을확정하지않는다. 추가전체비교전에준비구간과측정용카메라/동작시작을분리해실험조건을교정해야한다. 기존결과는해당명목실행의관측값으로보존한다. runtime-current의.NET10/11비교는이한도변경전소스지문의동일조건비교이며새빌드의최종성능재측정으로승격하지않는다.

준비와 측정용 동작을 분리하려면 measure-erd-layers.ps1에 -ControlledWorkloadClock을 명시한다. DUXEL_ERD_WORKLOAD_CLOCK=post-warmup과DUXEL_ERD_MEASURE_SECONDS가 활성화되며,실제장면을초기위치에서준비한뒤첫표시성공+1초부터카메라·동작의단조시계를시작한다. 지정초는측정용동작시간이며앱자동종료duration+120은명시적인감시한도다. 일반실행및기존명목전체시간측정은wall-total로유지한다. idle/기존별도baseline실행파일과의혼합은실행전에거부한다. 준비중생성된큐프레임은소스프레임의시각으로제외하며workload.csv가각표시소스와입력시각을연결한다. source-epoch-v2준비방법은기존v1과구분되어직접개선율비교에혼합하지않는다. BenchWorkloadClock은Duxel.Core의할당없는프레임단계값을반환한다. 타임스탬프버퍼8MiB는새측정출력이있을때만추가된다. NET10/11각397·직접373·실행기15/15검사가통과했으며실제NativeAOT구간검증전까지새FPS달성값을주장하지않는다.

> 최신 측정: **목표 1000 FPS**, 카메라 **870.27 FPS(87.03%)**, 카메라＋엔티티 이동 **906.79 FPS(90.68%)**, 단일 실행 평균 최고 **935.32 FPS**. 모두 안정 목표 미달. .NET11 RC NativeAOT, 1200테이블, MSAA4, 준비 후 실제18초×각3회, 동일 초기 카메라. 측정 이미지 SHA256:94E22DE0FFBB5F47D9CAD2E36D6F03207F10CDB2DA6D150F121A403FCF0A52CC. 기본 배포 EXE/PDB도 최신 측정 이미지로 갱신했으며 해시 재검증을 통과했다. 최신 요약:`artifacts/perf-2026-11-03/fps-current.json`, 근거:`workload-controlled-capacity-ab/`. 이전 조건의 최고1074.02 FPS와 직접 비교하지 않는다.

실제 동적 3D 샘플과 창 제목 표시줄을 현재 렌더러로 실행한 GPU 버퍼에서 상태 패널 제목·하단 문구·간결한 줄 간격·콘텐츠에 맞는 크기를 확인했다. NET10/11 각각8/8검사,색상 도형 검사 포함,최종 픽셀 전체 동치. 근거:artifacts/perf-2026-11-03/status-panel-gpu-review.json. 데스크톱/DWM 캡처나 FPS 측정이 아니다. 초기 텍스처 순서 가설은 픽셀 동치로 기각했으며 엔진 결함으로 기록하지 않는다.

축 정렬 레이어 경계 판정은 정확한 확대·이동 행렬에서만 대각선의 두 끝점을 변환한다. 회전·원근은 일반 동차 좌표 판정과 AA 여유 영역을 유지한다. UiRendererFrameMetrics.AxisAlignedLayerCulling은 렌더러 설정이며 DUXEL_VK_AXIS_ALIGNED_CULLING은0/1만 허용한다(기본1). ERD JSON은 axisAlignedLayerCulling을 기록한다. -CompareAxisAlignedCulling은 같은 이미지에서 일반/특화 계산을 번갈아 실행하며 공통2Mi 한도와 -ControlledWorkloadClock을 지원한다. GPU NET10/11각403,직접379,일반403검사와 원시 GPU 이미지119개 전체 동치. 후보의 FPS는 측정 전이며 optimization-session-2026-11-03.md와 axis-culling-input-contract.json을 참고한다.

> 최신 측정·기본 배포: **목표 1000 FPS**, 카메라 **1054.95 FPS(105.49%)**, 카메라＋엔티티 이동 **1116.81 FPS(111.68%)**, 단일 실행 평균 최고 **1122.60 FPS**. 두 시나리오 모두 독립3회 실행 평균이1000이상이다. 범위는1044.20–1081.47/1031.41–1122.60 FPS. .NET11 RC NativeAOT,1200테이블,MSAA4,준비 후 실제18초×각3회,동일 초기 카메라. 모든 프레임의1ms이내 처리를 뜻하지 않는다. SHA256:09584FF4DF471401C9B53047BA793885BFA919290CEAA2B7D5EB27D501260AAA. 기본EXE/PDB해시 재확인 완료. 최신 요약:`artifacts/perf-2026-11-03/fps-current.json`,근거:`axis-culling-ab/`(감사12/12).

일반 실행 자원 관측은 `scripts/measure-erd-resources.ps1`로 수행한다. 명시한 EXE 해시·새 출력 경로·동일 이름의 기존 프로세스를 실행 전에 검사하고, 기존 창을 닫지 않는다. 벤치마크 출력은 비워 표본 배열을 만들지 않으며1초 간격으로 프로세스 Private Bytes·작업 집합·CPU·핸들·스레드·로그 프레임을 기록한다. 정상 창 닫기와 종료를 확인하며 비상 종료·누락·긴 표본 공백은 성공으로 처리하지 않는다. 프로세스 메모리는 VRAM이나 생존 객체 크기와 구분한다.

Windows x64에서 `-IncludePrivateAllocations`를 켜면 할당 시작 주소별 커밋·예약 크기를 JSON Lines로 남깁니다. 워밍업이 끝난 뒤 10초마다 기록하고 마지막 15초에는 1초마다 기록합니다.

Windows x64에서 `-IncludeVirtualMemory`를 사용하면 주소 공간의 커밋 크기를 영역별로 기록합니다. GC 카운터와 함께 읽는 방법은 [ERD 메모리 관측 가이드](erd-layer-lab.ko.md)에 있습니다.

`DUXEL_ERD_TRACE_VIEWPORT=1`로 ERD 화면 크기가 바뀐 프레임의 할당량과 레이어 재생성 건수를 기록할 수 있습니다. [ERD 진단 가이드](erd-layer-lab.ko.md)

검증 이미지09584FF4의 준비30초 후 관측: 동작179.97초에서 초기/후기1/3구간 Private Bytes 중앙값은118.31MiB,작업 집합은116.41MiB 증가했다. 원인은 미확정이며 누수 없음으로 판정하지 않는다. 핸들511–521,스레드13–21,정상 종료. 유휴59.84초에는 추가 로그 프레임0,CPU46.875ms(논리 코어1개 기준 약0.0783%),메모리 중앙값 변화-0.168/-0.082MiB,정상 종료. 관측 실행2/2완료와 메모리 검증 완료는 다르다. 근거:`artifacts/perf-2026-11-03/resource-observer-audit.json`과 각 `samples.csv`.

원인 구분용 `DUXEL_APP_PROFILE_MEMORY=1`은 App 프로필이 출력되는 프레임에만 GC 힙 추정·최근 수집의 커밋/힙 크기·세대별 수집 횟수·누적 할당을 덧붙인다(기본0). GC를 강제로 실행하지 않는다. 관측기의 `-IncludeGcMetrics`는 첫 프로필에서 기능을 확인하고,측정 구간의 `gc-samples.csv`를 저장한다. 이 진단은 새로 빌드한 후보에 필요하며 기존09584FF4이미지는 기능을 포함하지 않는다. 기존1000FPS 기록과 기본 EXE는 보호한다.
GC 진단의 NET10/NET11 GPU검사각406개·Release경고/오류0개·완료된 로그 레코드 파서6/6을 통과했다. NativeAOT 진단 후보B13F2B47의 첫 GC프로필도 확인했다. 기본09584FF4와1000FPS 기록은 유지하며,새180초 원인 구분 관측은 resource-gc-state.json에서 진행 상태를 확인한다. 진단 종료와 원인 구분 전에는 메모리 검증 완료로 취급하지 않는다.

픽셀 버퍼의 소유권은 `PlatformTextRasterizeResult.PixelOwner`와 `UiTextureUpdate.PixelOwner`의 선택적 `IMemoryOwner<byte>`로 전달한다. 렌더링에 사용할 동안 픽셀은 변경하지 않는다. 소유권이 없는 기존 업데이트는 `UiDrawData.CaptureTexturePayloads()`가 복사하고,독점 소유한 업데이트는 원본을 프레임이 보유해 추가 복사를 생략한다. 프레임 소비·폐기,대체된 큐 항목,측정만 한 텍스트의 캐시 제거와 컨텍스트 종료에서 소유권을 반환한다. GPU 텍스처의 수명은 기존 펜스/퇴역 경로를 따른다.

Windows 여러 줄 텍스트의 합성 비트맵은 반환 가능한 풀 버퍼를 사용한다. 버퍼를 지운 뒤 기존과 같은 픽셀 합성을 수행하며,측정 결과만 반환하는 호출도 버퍼를 반환한다. 3D 벡터 레이어·셰이더·폰트 윤곽·카메라 품질은 유지한다. 직접 래스터화 결과를 받는 호출자는 사용을 마친 뒤 PixelOwner를 Dispose하거나,텍스처 업데이트에 한 번 소유권을 넘긴다. 넘긴 버퍼를 호출자가 먼저 반환하거나 변경하지 않는다.

메모리 수정 검증: .NET11 GPU422·.NET10텍스트 아틀라스422·직접 제출398검사,변경 전후 GPU이미지119/119바이트 동치. 전용 수명 검사에는 소유/차용 혼합 프레임,소비 스레드 반환,컨텍스트보다 오래 살아 있는 프레임,미소비 프레임 폐기,캐시 제거,투명 영역을 포함한 실제 래스터 바이트 재사용이 포함된다. 최신 NativeAOT 후보52E300FA의 첫180초 관측은 Private Bytes 중앙값-10559488바이트,작업 집합-11585536바이트이며 아직 단일 실행이다. 정량 개선율은 변경 전후 각3회가 끝난 뒤 판정한다. 기본1000FPS검증본09584FF4는 유지한다.

최신 메모리 반복 검증(2026-11-03): 변경 전 B13F2B47/후52E300FA를 각각3회, 준비30초＋동작180초,1200테이블·카메라＋엔티티 이동·측정 출력 없음 조건에서 비교했다. 총6/6회 정상 종료와 원시 표본·GC·해시 감사를 완료했다. 초기/후기1/3구간 Private Bytes 증가 중앙값은116.33MiB→0.375MiB(증가량99.68% 감소,실행 범위 비중첩),관리 힙 증가123693912→-721148바이트,GC커밋 증가122568704→0바이트다. 전체 스레드 할당/기록된 UI프레임은9067.79→8064.53바이트(-11.06%,범위 비중첩)다. 전체 메모리99.68% 감소나 누수·VRAM 검증 완료를 뜻하지 않으며 CPU 범위는 겹쳐 향상률을 주장하지 않는다. 근거:`artifacts/perf-2026-11-03/pixel-owner-resources-summary.json`. 최신 소스의 MSAA 경계 수정과 FPS·런타임 재검증은 별도로 진행 중이다.

MSAA 깊이 경계와 실제 포함 셰이더 검증은 [동적3D 레이어](dynamic-3d-layers.ko.md)의2026-11-03수정 기록을 따른다. NET10/11각435·직접411·상태 패널 각9검사를 통과했으며 새로운 ERD FPS는 측정 중이다. 관련 저장소 스킬5개의 공식 문서 대조·갱신·형식 검증을 완료했다(로컬 Git무시 파일,기준SDK고정 유지).

최신 측정(2026-11-03): MSAA 경계 수정＋픽셀 소유권 재사용을 포함한 NativeAOT694550C9는1200테이블,dense/resident,MSAA4,VSync off,실제 준비 후18초×각3회에서 카메라937.19FPS(93.72%,범위919.51–982.09),카메라＋엔티티 이동960.30FPS(96.03%,942.55–970.95),단일 실행 평균 최고982.09FPS다. 두 시나리오 모두1000미달이다. 기존09584FF4의1000이상 기록은 보존하되 발견한 MSAA 경계 결함을 포함하므로 최신 화면 정확성의 증거로 재사용하지 않는다. 정확성 수정은 유지하며 단계 간 차이를 격리된 회귀율로 계산하지 않는다. 원시 프레임 통계·표시/소스 연결·준비 구간·초기 자세·설정·해시6/6감사 통과. 상위 일회성 실행 래퍼는 완료 뒤 PowerShell 스크립트에 native LASTEXITCODE를 잘못 검사해1로 끝났고,실제6개 자식은 기존 실행기가 정상 종료를 확인했다. 기록을 직접 감사했고 비용 큰 재실행은 하지 않았다. 근거:artifacts/perf-2026-11-03/coverage-erd-current 및 fps-current.json. 기본09584FF4EXE/PDB는 갱신하지 않았고 최신 후보는 기존binding-candidate경로다. 최종.NET10/11 Managed/AOT 비교와 처리량 개선은 남아 있다.

## 자동 팝업·전경 표시 레이어 — 2026-11-04

메뉴·팝업·컨텍스트 메뉴·콤보·툴팁·전경 그리기는 공통 동적 표시 레이어를 사용한다. 위젯 입력·레이아웃을 실행한 뒤 동일한 기하·명령의 불변 스냅샷을 재사용하며, 교체된 버퍼는 렌더 큐의 마지막 독자가 끝날 때 반환한다. 화면 고정 평면은 별도 깊이 이미지를 할당하지 않으며 기존 반투명도·ClearType·scissor를 유지한다. 일반 3D 장면의 깊이 검사는 유지한다.

`SetNextPopupPosition(UiVector2)`은 다음 `BeginPopup`의 화면 위치를 지정한다. `GetOverlayLayerMetrics()`는 현재 UI 프레임에서 완료된 표시 범위의 `ReusedLists`·`RebuiltLists`를 반환한다. 위치 변경은 해당 팝업 기하의 갱신이며 임의 위젯의 입력 생략이나 행렬만의 이동을 뜻하지 않는다. `UiLayer3D.PresentationDepthScopeId`는 예약 값이다.

새 전용 샘플: `samples/fba/overlay_layer_lab.cs`. 실행·비교 방법은 [표시 레이어 실험실](overlay-layer-lab.ko.md)을 참고한다.

공통 배처의 일시적인 빌더는 기하 소유권을 넘긴 뒤 같은 크기의 작업 배열을 다시 빌리지 않는다. 일반 프레임 빌더는 기존처럼 용량을 유지한다. .NET10·11 각각510개 검사 및 이전 이미지155개 동치를 확인했다. 이전 배포 대비 큰 부하 Private는 정지1.84MiB/이동3.32MiB 감소했으며 이 단계의 FPS 차이는 범위가 겹쳐 확정하지 않는다. 현재 NativeAOT EXE SHA256은 `40C51851C9469653BA40DBCD9B8FE36ABD2D3411538E1A5FC8090AB4DD786B92`다.

삭제가 거부된 임시 산출물은 개발 중단 사유로 삼지 않고 완료 시 정확한 대상과 [직접 정리 방법](rejected-build-cleanup.ko.md)을 안내한다. 실제 삭제가 끝난 것으로 보고하지 않는다.

다른 세션에서 이어갈 때는 [2026-11-04 대화 요약·인수인계](../SESSION_HANDOFF.md)를 읽는다. 완료된 통합, 실행 파일 해시, 검증 근거, 남은 개선 후보와 복사용 재개 프롬프트를 담았다. 현재 작업 트리와 근거를 다시 확인한 뒤 사용한다.

종합 기능 샘플은 `samples/fba/all_features.cs`이며 [검증 범위와 실행 방법](all-features-validation.ko.md)을 참고한다. `run-fba.ps1 -ArtifactRootDirectory`는 저장소 `artifacts` 아래 전용 NativeAOT 출력을 지정하며 기존 소유권·보관 한도를 유지한다. `TextWrapped`는 현재 열의 가용 폭을 사용하고 `UiFpsCounter.Tick()`은 고해상도 시간으로 매 프레임을 계산한다.

[공통 최적화 평가와 추가 개선](optimization-evaluation.ko.md)은 자동 GPU 재사용과 명시적 CPU 레이어 보존을 구분한다. 변경 영역이 빈 프레임은 streamed 기하 업로드와 draw 계획을 생략하지만 입력·레이아웃·제출은 유지한다. 기본 위젯·scroll·resize 집중 검사는 렌더링 검사의 `--widget-optimization-only` 모드로 재사용한다. 고정 RC3 비교에서 정지 FPS+5.63%와 업로드0B를 확인했으며 Private 감소는 미확정, 정지 작업 집합은 약0.50MiB 증가했다. 라즈베리파이 실기기 성능은 검증하지 않았다.


## DirectText 측정·캐시 갱신 — 2026-11-05

선택적 `IPlatformTextLayoutBackend`와 버전 계약, 공통 위젯별 단일 레이아웃 메모, 클리핑 후 지연 래스터화를 적용했다. 정적 레이어도 backend/버전 변경 시 무효화한다. 1만 버튼 CPU 중앙값79.0671→2.07605ms; 앱FPS나 Pi 수치가 아니다. [구현·동일 조건 측정·수명·GPU 검증](directtext-layout.ko.md), [별도 입자 정밀도 수정](particle-contact-precision.ko.md)을 참조한다.


## Badge·CounterBadge 후속

기본 Badge 팩터리와 생성 카탈로그에 공통 상속 위젯을 연결했다. 색·표현64조합과6크기·3모양,카운트·숨김·overflow·dot을 구현했다. 공식 화면 동등성·UIA·RTL/강제 색상은 미완료다. [API·생성 코드·수명·검증](fluent2-badge.ko.md)을 참조한다.

소스 읽기·옵션 변경은 게시 후 프레임 요청을 수행하고 동작별 값 형식 상태만 보관한다. 새 Badge 최종 검증은 .NET10 전체1095·집중49와 RC3 Native243×EXT/core이며, 전체 컴포넌트/UIA/공식 화면 완료와 구분한다. 최신 실행 방법은 --badge를 포함한 위젯 갤러리 가이드를 따른다.


## 공식 플랫폼 범위 후속 조사

Android Compose-v2 소스46개/분류54행을 실제 생성기 입력에 연결했다. 알려진 요구 이름은289→325,표준 팩터리 이름26,미연결299이며 최종 전체 분모는 아직 미확정이다. 생성기81·위젯 트리72와 관리/RC3 Native 카탈로그 동치를 확인했다. [출처·대응·회귀 검사·남은 범위](fluent2-platform-inventory.ko.md)를 따른다.
# Fluent 2 범위 교정 (2026-11-05)

[공식 사이트 기준 구현 범위](fluent2-scope.ko.md)를 우선한다. 생산 생성기는 공식 사이트의 상위55개 이름과 부모 연결 변형19개를 구분한다. 저장소 조사325개는 목표 분모가 아니다. 검증: artifacts/fluent-site-scope-2026-11-05/verification.json.

Text·생성된 타이포그래피 프리셋의 Source/Color 변경은 값 게시 후 유휴 프레임을 요청하며 같은 값은 요청하지 않는다. Dispose는 공급자를 해제하고 이후 변경을 거부한다. [텍스트 수명 검증](fluent2-text.ko.md) 참조.

[Divider](fluent2-divider.ko.md)는 생성된 정적/동적 위젯과 Dux.Divider/ui.Divider 공통 표현을 사용한다. UiTheme.FluentColorScheme은 공통 색 데이터이며 BadgeColorScheme은 호환 별칭이다. 갤러리 --divider, 실제 상위 팩터리8개·변형19개. 공식 룩앤필/UIA/강제 색상/임의 자식 슬롯은 미완료다.

공통 Button Label/Size는 값 게시 후 유휴 프레임을 요청하며 같은 값은 요청하지 않는다. 콜백 교체만으로는 재그리지 않는다. 사용자 OnDispose 실패/기반 호출 생략에도 Button·Text·Badge·Divider 엔진 소유 자원은 별도 공통 단계에서 회수한다. [갱신·해제 계약](widget-implementation.ko.md) 참조. 다른 위젯의 참여 검증은 계속한다.

Progress Source/Size·Slider Label/Options/Size·Toggle Label의 변경 알림과 바인딩/공급자 해제도 공통 계약을 따른다. Slider/Toggle 해제 후 Value 접근은 ObjectDisposedException이며 호출자 소유 상태는 폐기하지 않는다. 현재 표준27개 이름/기반7계열의 참여 확인과 전체 공식 기능 완료는 구분한다. [소유권 후속 감사](widget-implementation.ko.md) 참조.

## Button 실제 폰트와 크기

생성 기본 Button은 실제 Semibold14/20을 사용한다. small Regular12/16과 large Semibold16/22는 `UiButtonStyle`로 정적 연결한다. 주변 폰트 복원·상한 있는 메트릭 캐시·정적 레이어 무효화는 공통 경로이며 비제네릭 즉시 버튼은 아직 호환 표현이다. [상태·검증·남은 범위](fluent2-button.ko.md), [실행 가이드](widget-gallery-run.ko.md).

현재 소스의 `--buttons`에는 primary·outline·subtle·transparent도 있다. `UiButtonStyle`과 크기별 `UiFluentButtonPresentation<TAppearance>` 계열로 공통 동작에 정적 연결한다. [정확한 API와 남은 범위](fluent2-button.ko.md).

`--buttons`의 square/circular3크기도 생성된 공통 위젯을 사용한다. 모양은 `UiFluentPrimaryButtonAppearance<UiFluentCircularButtonShape>`처럼 표현 정책 안에 정적으로 조합한다. 사용자 프레임 알고리즘도 같은 확장 지점이며 [정확한 범위와 한계](fluent2-button.ko.md)를 참조한다.

아이콘 Button은 `UiFluentIconButtonPresentation<TSize,TAppearance,TIcon,TPosition>`을 생성 스타일에 연결한다. `--buttons`의 Add 아이콘은 고정 공식 폰트이며 사용자 특성의 생성 경로도 실제 소비한다. 아이콘 전용 UIA는 아직 미완료다. [구성·검증·한계](fluent2-button.ko.md).

공식 폰트와 `[UiFluentIcon]` 생성 경로는 [공식 아이콘](fluent2-icons.ko.md)을 참조한다. Add regular/filled/interactive를 제공하며 전체 심볼 API·공식 SVG/브라우저 일치·UIA 완료를 뜻하지 않는다.

아이콘 생성기는 선택된 메타데이터 값이 같으면 문자열 구성·출력을 재사용한다. 생성기 기능107개와 Core 생성 파일57개 바이트 일치를 확인했다. 같은 파일 선언 제거로 남은 출력이 재생성되는 비용은 미완료이며 [추적·한계](fluent2-icons.ko.md)를 참조한다.

현재 Fluent Semibold14/20 조건의 DirectText 측정기를 복구하고 고정RC3 독립3회씩 교대 비교했다. CPU78.9842→1.9952ms, Private77451264→36249600B이며 기존 기능의 같은 현재 폰트 비교다. 새 엔진 패치/GPU/앱 FPS 향상으로 계산하지 않는다. [조건과 한계](directtext-layout.ko.md).

[Switch 크기·배치·검증](fluent2-switch.ko.md)

[Link 탐색·상속·정적 표현·캐시](fluent2-link.ko.md)

[한 줄 입력 동작·표현·수명 기반](input-kernel.ko.md)
## Fluent Field 구성

`Fluent.Field(UiWidget control, UiVector2 controlSize, UiFieldOptions options)`와 `Dux.Field`는 실제 자식을 소유한다. 라벨 클릭·필수 표시·검증/도움말·오류 Input 전달과 공통 Build 수명을 사용한다. 기본/사용자 Field는 생성된 `UiField<TBehavior,TPresentation>`을 상속한다. 크기와 방향은 정적 표현 타입으로 선택한다. [API·수명·한계](fluent2-field.ko.md), [실행 가이드](widget-gallery-run.ko.md)를 참고한다. `CurrentField` 메타데이터는 Windows UIA 구현 완료를 의미하지 않는다.

## 공통 위젯 측정

`UiWidget.Measure(ui, availableSize)`·`UiControlContext.Measure(child, availableSize)`와 `MeasureContent` 확장을 사용한다. 기본 폼 컨트롤은 실제 표현과 측정을 공유하고 값 바인딩·콜백을 실행하지 않는다. `Fluent.Field(control)`는 자연 크기 모드이며 기존 크기 인수 생성자는 보존한다. 직접 API는 `AutoField<TBehavior,TPresentation>`이다. 지원하지 않는 측정을 렌더링으로 대신하지 않는다. [범위·수명·실패·예제](widget-measurement.ko.md).

# 위젯 접근성 기반

공통 의미 정보 트리·실제 Windows UIA Button/Input 공급자·COM 소스 생성·동작 큐와 남은 범위는 [widget-accessibility.ko.md](widget-accessibility.ko.md)를 참고한다. `UiWidget.AccessibleName`은 아이콘 컨트롤 이름을 지정하고 Field 메타데이터는 주 자식에 적용한다. Fluent 전체 역할·패턴·이벤트·공식 룩앤필 완료는 아니다.

# UIA 제거·외부 Field 검증

`UiAccessibilityTree.ElementsRemoved`는 발행 잠금 밖에서 알린다. Windows는 약한 공급자 키/용량을 즉시 정리하고 회수 공급자를 Windows 메시지 큐에서 분리한다. [접근성 문서](widget-accessibility.ko.md)를 참고한다. 집중 .NET10/RC3 NativeAOT40, 외부 Field14와 별도 수명 호스트3/클라이언트4를 통과했다. 장시간 메모리·이벤트·Fluent 전체 검증은 남아 있다. 근거: `artifacts/uia-lifetime-2026-11-06/verification.json`.

# Fluent Spinner 체크포인트

[Spinner](fluent2-spinner.ko.md)는 상속 가능한 생성 컨트롤·분리된 정적 동작/표현·공통 원환 기하와 실제 UIA ProgressBar 의미 정보를 사용한다. `Fluent.Spinner`/`Dux.Spinner` 또는 호출자 소유 즉시 모드 동작 상태를 사용한다. 갤러리는 `--spinner`다. 여덟 크기·표현·라벨 위치·지연·RTL/모션 감소를 구현했으며 공식 브라우저 동치·슬롯/강제 색상·알림·시각 지정 깨우기는 남아 있다. 카탈로그32=상위13/55+변형19이며 미연결 상위 이름은42개다.

공통 UiWidget Build 저장소가 키별 소유권·기존 정체성 공간을 재사용하고 임시 목록을 기존 Dictionary에 둔다. 고정RC3 Native 모드별 전후3회씩 생성·최초Build 할당 감소,변경 없는Build0B,수명101·컨테이너175검사와GPU121일치를 확인했다. [범위와 한계](widget-cpu-retention.ko.md#공통-위젯-build-저장-비용-개선--2026-11-06)를 따른다. 전체 목표 진척 수치는 유지하며 리사이즈는 별도 보류한다.

생성 Infrastructure 팩터리 Dux.Padding·Align·Center·SizedBox·Expand·ConstrainedBox는 UiSingleChildBox의 정적으로 선택한 배치/옵션/표현을 사용한다. 기존 Container도 소유권·캐시 기반을 공유한다. [제약·예제·관리/Native229검사·GPU159·최초 측정 저장 비용](widget-containers.ko.md#생성된-단일-자식-배치-위젯--2026-11-06)을 참고한다. 컨테이너3/6,말단/스크롤/전체 통합은 미완료다.

Slider/Toggle 표현의 정적 Arrange가 바뀐 부모 할당을 반영하며 생성 위젯의 측정·그리기·입력이 같은 경로를 사용한다. 사용자 즉시 컨트롤도 constrainLayout:true로 참여하고 기존 오버로드의 자연 크기는 유지한다. [계약·관리/Native172검사·GPU43](widget-containers.ko.md#slidertoggle의-부모-할당-반영--2026-11-06)을 참고한다. 나머지 말단 계열은 남아 있으며 컨테이너3/6을 유지한다.

Input 표현의 정적 Arrange가 부모 할당을 필드·편집·텍스트·소유 자식 기하에 적용하며 편집 정체성과 undo를 유지한다. 기존 즉시 호출은 자연 크기,새 constrainLayout 오버로드와 공개 MeasureInput은 같은 공통 경로를 사용한다. [관리/Native212검사·GPU25·한계](widget-containers.ko.md#편집-입력창의-부모-할당-반영--2026-11-06)를 참고한다. 컨테이너3/6,나머지 말단 계열은 미완료다.

2026-11-11 관찰 창 대기·포커스 경계 수정: Spinner122·관찰 페이지73·TabList197·예약35, GPU 패널16·관찰 창43검사 통과. 기본 Fluent 페이지는 정지하며 명시적 재생 전환을 제공합니다. 본문 포커스의 우측·하단 여백을 포함한 네 변을 실제 픽셀로 검증했습니다. 근거: artifacts/observer-idle-panel-2026-11-11/verification.json. NativeAOT·전체 목표 완료나 처리 속도 개선 측정은 아닙니다.


2026-11-11 공통 메뉴 탐색: UiMenuOptions.MaximumHeight와 화면 높이 한계·공통 스크롤바·Unicode 첫 글자 순환·키보드 행 표시를 지원합니다. 기본/Fluent/탭 넘침이 같은 경로를 사용합니다. 여러 글자 누적 검색과 전체 하위 메뉴·체크·UIA 준수는 미완료입니다. [메뉴 지침](fluent2-menu.ko.md)을 참고하세요.


메뉴 탐색 검증 근거: artifacts/menu-navigation-2026-11-11/verification.json. 관리 메뉴95·관찰75·탭 넘침20·예약35·접근성49·위젯 트리72, GPU124검사 통과. 별도 기본/Fluent 메뉴 클라이언트 각각4·호스트2 정상 종료는 기존 메뉴 회귀 근거이며 새 스크롤 UIA 전체 검증은 아닙니다.


2026-11-11 메뉴 스크롤 접근성: 메뉴의 Scroll과 행 ScrollItem을 기존 요청·알림 경로에 연결했습니다. 독립 팝업 경계는 바깥 페이지 스크롤 전파를 막습니다. 관리 메뉴117·관찰77·ScrollView75·가상 목록75, GPU132 및 별도 UIA 클라이언트6·호스트3검사 통과. 근거: artifacts/menu-scroll-uia-2026-11-11/verification.json. 폐기된 직접 공급자 읽기/외부 변경 요청은 거부되며 외부 보유 패턴 getter의 S_OK/0 관측은 별도로 기록했습니다. 전체 메뉴·발화·NativeAOT는 미완료입니다.


2026-11-11 Slider hover: 중립 버튼 hover 색을 제거하고 UiFluentSliderPalette.Resolve/UiSliderPaint로 Fluent의 compound-brand 및 neutral 상태 토큰을 분리했습니다. 밝은/어두운 hover도 브랜드 계열을 유지하며 비활성 색과 구분합니다. 고정 ProgressBar는 Slider hover의 영향을 받지 않습니다. [Slider 지침](fluent2-slider.ko.md)과 artifacts/slider-hover-2026-11-11/verification.json을 참고하세요.


2026-11-11 메뉴 확장: UiAccessibilityNode.Expansion/UiAccessibilityExpansionState와 ExpansionChanged, Expand/Collapse 요청을 연결했습니다. 실제 메뉴 버튼·탭 넘침의 멱등 상태, MenuOpened/MenuClosed, 게시 전 닫힘·게시 후 열림과 Windows ExpandCollapse를 공유합니다. 원래 Tooltip 종료 수명은 회귀로 확인합니다. 세부 계약과 제한은 [메뉴 지침](fluent2-menu.ko.md) 및 artifacts/menu-expansion-2026-11-11/verification.json을 참고하세요.

2026-11-11 체크 메뉴: UiMenuOption.Checkbox는 UiBinding<bool>를 빌리며 콜백 전에 상태를 갱신하고 메뉴를 유지합니다. 기본 두 표현은 논리 시작 위치의 체크 열을 공유합니다. 사용자 표현은 UiMenuItemContent 오버로드를 명시적으로 구현합니다. UiAccessibilityNode.Checked/ToggleChanged·Toggle 요청을 Windows 10015 패턴·30041/30086 속성에 연결했습니다. 관찰 경로는 컨테이너 → Menu 공통 동작 → 체크 메뉴 열기입니다. [메뉴 지침](fluent2-menu.ko.md), artifacts/menu-checkbox-2026-11-11/verification.json을 참고하세요. 메뉴 전체 외관·발화는 남은 범위입니다.

2026-11-11 라디오 메뉴: UiMenuRadioGroup(name, UiBinding<string?>)·UiMenuOption.Radio로 연속된 이름 있는 상호 배타 그룹을 만들며 독립적인 호출자 상태를 빌립니다. 실행은 그룹 표시를 반영하고 닫으며 선택된 라디오 해제는 거부합니다. MenuGroup은 Windows Group/Selection을, 자식은 실제 그룹을 컨테이너로 하는 SelectionItem을 제공합니다. 즉시 닫히는 선택·속성 이벤트는 읽기 전용 SelectionItem을 가진 변경 불가능한 스냅샷을 사용해 최종 캐시 상태를 보존하며 과거 소스의 변경 동작은 허용하지 않습니다. 관찰 경로는 컨테이너 → Menu 공통 동작 → 라디오 메뉴 열기입니다. artifacts/menu-radio-2026-11-11/verification.json 및 [계약](fluent2-menu.ko.md)을 참고하세요. 하위 메뉴·아이콘/보조 내용·shadow16/강제 색상·발화는 남은 범위입니다.


기존 SliderFloat/SliderInt·스칼라/벡터/배열 래퍼·SliderAngle·VSliderFloat/VSliderInt가 타입 지정 수치 큐와 현재 활성/클립/범위 재검증을 공유합니다. float는 연속값을 유지하며 수평 step은 수치 스냅 격자가 아닌 키보드 증감량입니다. 연속 컨트롤은 SmallChange0, 정수는1을 게시하며 기존 float의 중간값 짝수 반올림을 따릅니다. 각도는 도 단위를 게시하고 호출자에게 라디안을 반영하며 정수 게시 값은 래퍼의 최종 변환 결과입니다. 기존 정수/double의 float 기반 정밀도와 키보드/포커스 동작을 유지하므로 전체 정밀도나 새 Focus 기능을 주장하지 않습니다. 기존 탭 선택/본문 관계는 별도로 검증했으며 키보드/플래그 전체 지원은 남아 있습니다. 기존 API 집중16개(통합44개)·관찰127개·외부 기존 API 클라이언트6개/호스트3개·실제 포인터 선택 Immediate를 포함한 관찰 GPU47개가 통과했습니다. ./run-gallery.ps1 → 기존 API → Hello / Immediate에서 확인합니다. 근거: artifacts/slider-range-2026-11-11/legacy-verification.json.

### 기존 BeginTabBar/BeginTabItem 의미 정보 (2026-11-11)

스트리밍 방식의 기존 API가 TabList·실제 Tab SelectionItem·활성 TabPanel만 게시하고 ControllerFor/LabeledBy 상호 관계를 제공합니다. 본문 컨트롤은 해당 패널에 속하며 중첩 탭은 바깥 부모를 복원합니다. TabItemButton은 탭 선택에 참여하지 않는 Invoke 버튼입니다. 필수 선택에서 선택된 머리글의 해제를 거부하며, 대기 중인 대상의 닫힘·삭제를 실제 게시되지 않은 선택의 성공으로 처리하지 않습니다.

포인터·접근성 선택 모두 기존의 다음 프레임 전환과 프레임당 본문 하나를 유지합니다. UiAccessibilityTree의 wait=true 선택 요청은 선택된 프레임 게시 후 완료되며 그 전에 대상이 닫히거나 삭제되면 InvalidOperation/Unavailable을 반환합니다. 기존 Windows SelectionItem 메서드는 비동기 접수 방식을 유지하므로 확정된 속성/선택 알림을 받은 뒤 새 상태를 읽습니다. 머리글/본문은 기존의 비포커스 계약을 유지하며 키보드/플래그 전체 지원·발화는 남아 있습니다. 전환 완료 후 반복 깨우기나 애니메이션 기한을 만들지 않습니다.

기존 탭 관리25개·공통 TabList197개·실제 관찰131개·외부 기존 탭 클라이언트15개/호스트3개·생성형 탭 회귀 클라이언트25개/호스트3개가 통과했습니다. 관찰 GPU47개가 통과했고 비교 가능한73개 본문 캡처는 이전 수치 접근성 기준과 같습니다(FPS 머리글 제외). 테스트는 실제 TabList 범위에서 같은 이름의 머리글/본문을 구분합니다. 생성형 본문 검사는 기본 포커스 가능 기대값을 유지하며 기존 본문은 명시적으로 false를 검사합니다. ./run-gallery.ps1 → 기존 API → Hello / Immediate에서 확인합니다. 근거: artifacts/legacy-tabs-2026-11-11/verification.json. NativeAOT와 전체 목표 검증은 별도입니다.

## 독립 아이콘

`Fluent.Icon(painter, new(24, 24), foreground)`로 장식용 위젯을 만듭니다. 컴파일된 글꼴·벡터 painter는 `UiMenuIcon.Create<TRegular, TFilled>()`, 기존 `UiSvgIcon`은 `svgIcon.MenuIcon`을 전달합니다. 독립 아이콘은 기본 regular 표현을 사용하며 포인터 동작·포커스 정지·호버 애니메이션을 등록하지 않습니다. 전경색은 명시적으로 전달합니다. SVG 원본 색은 painter에서 `MultiplyForeground`를 선택한 경우에만 전달한 전경색과 곱해집니다.

위젯은 painter를 빌려 씁니다. 위젯 → `UiSvgIcon` → 문서/store 순서로 해제하세요. SVG 파일 갱신은 기존 SVG 텍스처 경로를 통해 프레임을 요청합니다. 그대로 다시 그릴 때는 주기 프레임을 요청하지 않고 래스터·텍스처를 재사용합니다. `Size`·`Color`·`Icon` 변경은 프레임을 요청하며 잘못된 크기와 빈 painter는 오류로 처리합니다. 크기는 논리 레이아웃 단위이고 부모 제약에 따라 실제 할당 크기가 달라질 수 있습니다. 접근성 이미지 역할이나 버튼은 제공하지 않으므로 동작에는 이름이 있는 컨트롤을 사용하세요.

저장소 루트에서 `./run-gallery.ps1`을 실행하고 **SVG** 탭에서 아이콘 메뉴 아래 **독립 SVG 아이콘 (Fluent.Icon)**을 확인하세요. 같은 페이지에서 비트맵 입력과 변환 가능한 `UiSvg`도 비교합니다. 새 팩터리의 샘플이며 전체 Fluent 적합성 완료를 뜻하지 않습니다.

## 이미지 위젯 API

PNG·SVG 비트맵 스냅샷은 불변 UiImageSource와 Fluent.Image를 사용합니다. 컨텍스트별 텍스처 수명·로딩·레이아웃·캐시 한도·Observer 확인 방법은 [이미지 위젯](image-widgets.ko.md)을 참고하세요. 전체 Fluent 이미지 스타일·접근성은 남아 있습니다.

최신 캐시 수명 근거 : artifacts/raster-lifetime-2026-11-12/verification.json. 관리45개 계약·7803회 검사 실행, 새 CPU10개, GPU설정별17개가 통과했습니다. 이전44개 결과는 역사적 근거입니다.

최신 Label 검증 : artifacts/fluent-label-2026-11-12/verification.json. 독립 라벨19개 검사를 포함한46개 계약·7822회 검사 실행, Label GPU28개·Observer50개가 통과했습니다. 원래 기본 위젯 GPU4642개와 기존4038개 이미지 일치도 확인했습니다. [독립 라벨 API와 실행](label-widgets.ko.md). 이전45개 결과는 역사적 근거입니다.

## 독립 상태 배지
`Fluent.PresenceBadge`는8개 상태·외근·6개 크기를 지원합니다. `./run-gallery.ps1` 실행→**아바타**→**상태 배지**에서 상태·외근·크기를 바꾸세요. 스크롤되는 페이지는 상태를 유지하며 주기 프레임을 요청하지 않습니다. 기본 아이콘은 컴파일된 Fluent 폰트이고 전달한 SVG painter는 캐시를 재사용합니다. [API·자원 수명·검증](fluent2-presence-badge.ko.md). Avatar 겹침과 활동 링은 아래 계약을 따르며 전체 비활동 표현은 남아 있습니다.

## 활동 링과 갤러리 스크롤

`avatar.Options = avatar.Options with { ShowActivityRing = true };`로 활동 링을 켭니다. `avatar.ActivityText = "활동 중";`으로 접근성 문구를 지정할 수 있습니다. 기본 이름에 활동 문구를 더하며 `AccessibleName`을 지정하면 그 값이 우선합니다. 이름은 속성 변경 때 저장합니다. 링을 표시해도 동작·포커스·호버·애니메이션 타이머가 없는 Image 하나입니다.

자연 크기에서는 본체가 선택한14개 크기를 유지합니다. 네이티브 레이아웃은 전체 시각 영역을 측정합니다. 본체48px 이하는 선2px,64px 이하는3px,그보다 크면4px이며 각 방향에 선 두께의2배를 예약합니다(본체32px→전체40px). 장식을 이웃 할당 영역에 덧그리지 않도록 공간을 확보합니다. 부모 영역이 작으면 본체와 간격을 같은 비율로 줄입니다. 원형·사각형과 밝음·어두움의 링 색은 기존 analytic 테두리 전용 primitive와 고정된 persona 팔레트를 재사용합니다. 간격에는 아무것도 칠하지 않아 아래 픽셀을 보존합니다. 크기·링 전환은 기존 이미지 텍스처 임대를 재사용합니다. `ShowActivityRing=false`는 일반 아바타로 돌아가며 Fluent의 비활동 표현 구현을 뜻하지 않습니다.

저장소 루트에서 `./run-gallery.ps1` 실행→**아바타**→**아바타 활동 링**을 누르세요. 샘플은 활동 문구를 한국어로 지정합니다. 페이지는 공통 `UiScrollView`를 사용해 콘텐츠가 창보다 커져도 flex 높이 초과 오류 없이 스크롤됩니다.800×400 창에서 실제 끝까지 스크롤하고 탭 전환 후 위치와 모델을 유지하는지 검사합니다.

현재 근거는 `artifacts/fluent-avatar-ring-2026-11-12/verification.json`이며 SDK11.0.100/runtime11.0.0 관리 실행입니다. 전체 Fluent 활동/비활동 전환·강제 색과 원래 고정 RC3 NativeAOT/전체 부하 검증은 남아 있습니다. [상태 배지](fluent2-presence-badge.ko.md)는 독립 위젯과 아래 Avatar 겹침에서 사용할 수 있으며 이미지·이니셜·아이콘과 링에 공통 투명 마스크를 적용합니다.

## 원형 잘라내기
CircleCutout으로 도형·이미지·텍스트·SVG와 유지/compiled 레이어에 실제 투명한 구멍을 만듭니다. `./run-gallery.ps1` 실행→**렌더링**→**원형 잘라내기**에서 확인하세요. [API와 검증 범위](circle-cutout.ko.md). Avatar 상태 배지가 이 마스크를 사용하며 아래 겹침 계약을 따릅니다.

검증 스냅샷: CPU51/51계약·8136회 검사(CircleCutout20·관찰189),마스크는 설정마다88회씩5가지 설정에서 통과했습니다. 실제 Khronos instance/device validation layer 로드를 확인했고 캡처 로그에 VUID/오류가 없었습니다. 프레임 자원2/3개·공유 camera/basis 설정·일반 compiled·양쪽 스케줄러를 포함합니다. 추가 GPU 검사는 primitive74·둥근 이미지40·SVG74·Avatar220·Presence552·관찰55회입니다. 설정별 반복을 포함한 횟수이며1455개의 독립 기능 요구사항을 뜻하지 않습니다. 샘플의 본문 실행 카운터는 실제 구성 뒤에 그려 같은 이벤트 프레임의 확정값을 표시하며 추가 프레임을 요청하지 않습니다.

## Avatar 상태 배지 겹침

`UiAvatarOptions.Presence`에 `new UiAvatarPresenceOptions(UiPresenceStatus.Available)`를 지정하고 null로 제거합니다. `Size = null`이면 고정한 공식 크기 규칙을 적용합니다. 본체28px 미만→6px,28px 이상→10px,40px 이상→12px,56px 이상→16px,64px 이상→20px,96px 이상→28px입니다. `UiBadgeSize`를 명시하면 그 크기를 사용합니다. 배지는 본체 오른쪽 아래, `RightToLeft`이면 왼쪽 아래에 맞춥니다. 투명 간격은 Medium까지1px,Large/ExtraLarge는2px입니다. 이미지·이니셜·폰트/SVG 아이콘·활동 링에 공통 원형 잘라내기를 적용한 뒤 범위를 종료하고 배지를 그립니다. 배경색으로 빈틈을 덮지 않습니다.

자연 크기는 본체·활동 링·배지의 합집합을 예약합니다. 본체보다 큰 명시 배지도 포함하며 실제 부모 영역 안에 전체를 같은 비율로 맞춥니다. `PresenceText = null`이면 기본 상태 문구를 사용합니다. 한국어 문구를 지정할 때는 필요한 외근 표현까지 포함한 완전한 문구를 전달하세요. 기본 접근성 이름은 사람·상태·활동을 속성 변경 때 합쳐 저장하고 `AccessibleName`이 우선합니다. 상호작용 없는 Image 제공자 하나를 유지하며 배지 자식 제공자·포커스·Invoke·애니메이션 타이머를 추가하지 않습니다. 기존 이미지 임대와 아이콘 소유자 수명 규칙을 유지합니다.

`./run-gallery.ps1` 실행→**아바타**→**아바타 상태 배지**에서 확인하세요. 상태·외근·자동/명시 크기·좌우 전환과 기존 이미지·한글 이니셜·SVG 아이콘·활동 링을 함께 조작할 수 있습니다. 탭을 바꿔도 상태가 유지됩니다. 관리 실행 검증과 한계는 `artifacts/avatar-presence-2026-11-12/`에 기록합니다. 전체 비활동 표현/그룹 투명도·모션/강제 색·Fluent 전체 적합성과 원래 고정 RC3 NativeAOT/성능 관문은 남아 있습니다.

검증:CPU51/51개 계약·8193회(Avatar185,관찰191),Light/Dark×MSAA1/4 Avatar GPU380회·관찰11회·네이티브Avatar17회입니다. 기존 Avatar 이미지233장이 바이트 단위로 일치합니다. SDK11.0.100/runtime11.0.0 관리 실행 검증이며 공통 마스크 계약상 외부 그리기 콜백과 서로 다른 원형 범위의 중첩은 지원하지 않습니다.

## Avatar 활동 그림자

`ShowActivityShadow = true`는 `ShowActivityRing`과 독립적으로 켭니다. 둘을 함께 켜도 같은 Image 이름에 캐시한 ActivityText를 한 번만 더합니다. 고정한 본체 크기 규칙은28px 이하→Shadow4,48px 이하→Shadow8,64px 이하→Shadow16,그보다 크면Shadow28입니다. 그림자만 켜면 본체 외곽,링도 켜면 전체 링 외곽을 사용합니다. 그림자 범위·링·큰 명시 배지까지 측정하고 실제 부모 영역 안에 같은 비율로 맞춥니다. 고정한 원문의 그림자 가상 요소처럼 그림자를 먼저 그린 뒤 본체/링 잘라내기와 배지를 처리합니다. 이미지·이니셜·폰트/SVG가 같은 경로를 사용합니다.

`./run-gallery.ps1` 실행→**아바타**→**아바타 활동 그림자**에서 확인하세요. 탭을 바꿔도 상태를 유지하며 주기 프레임을 요청하지 않습니다. [공통 그림자 API](box-shadow.ko.md). 전체 비활동 상태의0.875배 축소/그룹0.8투명도와 전환/강제 색은 남아 있으며 도형별 레이어 투명도로 그룹 합성을 대신하지 않습니다. 현재 관리 실행 근거는 `artifacts/box-shadow-2026-11-12/`입니다.

검증:CPU52/52개 계약·8247회(그림자37,Avatar201,관찰192),실제 compiled 경로4종×그림자GPU256회·Avatar392·잘라내기88·primitive74·둥근 이미지40·관찰12·네이티브Avatar19회입니다. 설정마다 반복한 검사이며 기존 이미지498장이 바이트 단위로 일치합니다. 샘플 설명은 공통 줄바꿈 텍스트를 사용하며800×400 스크롤 회귀도 포함합니다.


`Fluent.CardFooter(children, action)`은 12px 간격의 콘텐츠와 선택적인 오른쪽 끝 동작을 소유합니다. 공통 측정 캐시를 사용하며 접근성 계층을 추가하지 않습니다. `./run-gallery.ps1` → 카드에서 실제 푸터를 확인합니다. Preview는 남아 있습니다. [Card](fluent2-card.ko.md)를 참고하세요.


`Fluent.CardHeader(header, description, image, action)`는 선택적인 네 슬롯을 소유하며 이미지/제목/설명/동작 순서로 처리합니다. 설명이 있으면 두 자동 행을, 없으면 한 행을 사용합니다. [Card](fluent2-card.ko.md)를 참고하세요. Preview는 남아 있습니다.


2026-11-12: 캐시된 그리기 그룹은 정적 레이어와 같은 참조 수·리비전 저장소로 DirectText 원본을 유지합니다. 그룹이 사용하는 동안600프레임 뒤에도 원본을 보존하며, 버전 교체와 마지막 소유자 해제 시 참조를 반납합니다. 캐시된 중첩 그룹의 텍스트 의존성도 전파합니다. 레이어 저장소가 없어도 그리기 그룹의 텍스처 참조를 확인합니다. 이 수정으로 Implementation Observer 렌더링 탭의 `Paint-group source texture is missing` 종료를 해결했습니다. CPU55/8565,신규 수명15,GPU2477,네이티브50과 별도 프로세스의614프레임 앱 세션 두 경우가 통과했습니다. 근거: `artifacts/observer-rendering-freeze-2026-11-12/verification.json`.


`Fluent.Card(..., selection: binding, floatingAction: widget)`는 고유 크기를 늘리지 않는 위쪽·오른쪽4px 보조 슬롯을 소유합니다. 실제 위젯이 숨은 선택 체크박스를 대체하며 마지막에 그리고 먼저 탐색합니다. 체크박스 바인딩과 자식의 사용 가능 상태는 호출자가 정합니다. `OnSelectionChanged(UiBinding<bool>, bool)`는 프레임에 캡처한 바인딩으로 저장합니다. 사용자 정의 보조 슬롯도 공통 본체 클립과 둥근 포인터 경계를 따릅니다. [카드](fluent2-card.ko.md), 근거 artifacts/card-floating-action-2026-11-12/verification.json.


`UiDrawListBuilder.RoundedClip(bounds, radius)`와 `UiControlContext.RoundedClip`은 원형 잘라내기와 조합하는 중첩 분석적 페인트 클립입니다. 상대 마스크 연결은 캐시 복사 후에도 유지하며 부분 갱신은 모든 조상의 반지름을 추적합니다. 입력·레이아웃 동작은 추가하지 않습니다. [둥근 페인트 클리핑](rounded-paint-clip.ko.md). `./run-gallery.ps1` → 렌더링 → 둥근 클리핑에서 확인합니다. Card Preview는 남아 있습니다.


Card의 주 콘텐츠·보조 슬롯은 같은 둥근 본체로 페인트와 포인터 획득을 제한합니다. 사용자 정의 패딩0·큰 반지름을 지원하고 본체를 채우는 콘텐츠에는 Dux.Expand를 사용합니다. 루트 그림자·포커스와 높은 팝업 계층은 독립적이며 네이티브 Focus/Invoke/Toggle·키보드를 유지합니다. `./run-gallery.ps1` → 카드 → 둥근 경계와 입력에서 확인합니다. [카드](fluent2-card.ko.md), 근거 artifacts/card-rounded-content-2026-11-12/verification.json. Preview는 남아 있습니다.


`Fluent.CardPreview(content, logo)`는 미디어와 왼쪽·아래12px 위치의 고정32px 로고를 소유합니다. 로고는 고유 크기에 영향을 주지 않습니다. Card 주 콘텐츠에 `Dux.CardContent([...])`로 직접 슬롯을 나열합니다(구성 전용 래퍼 허용). Preview는 Card 여백까지 채웁니다. 세로는 좌우와 첫 항목 위·마지막 항목 아래, 가로는 위아래와 첫 항목 왼쪽·마지막 항목 오른쪽입니다. `UiCardOptions.Orientation` 기본값은 Vertical입니다. 가로의 마지막 Header/Footer는 남은 유한 폭을 채웁니다. 일반 사용자 콘텐츠 트리는 기존 레이아웃을 유지합니다. 주축 초과는 명시적으로 실패하며 CSS flex-shrink/min-content 전체 적합성은 남아 있습니다. 콘텐츠·로고의 실제 입력·접근성 공급자를 유지하고 Preview 위에 Card 테두리를 그립니다. 재사용 시 측정된 할당을 유지하고 주기 프레임을 요청하지 않습니다.

`./run-gallery.ps1` → **카드** → **미디어 미리보기**에서 세로·가로 SVG 카드, 폭·크기 전환, 고정 로고와 여백까지 채운 모서리를 확인합니다. SVG는 콘텐츠 수명 동안 한 번 읽습니다. 근거: `artifacts/card-preview-2026-11-12/verification.json`. 노출 팩터리46개는 여전히 상위55개 중23개를 연결하며, 이 변형을 새 상위 항목 완료로 세지 않습니다.


CardPreview 검증: 관리 CPU55개/8754회(Card407), 테스트만 추가한 뒤 영향받는 Observer223회 후속 검증, GPU12개 프로필2673회(Card608), 외부 Card27·CardFloating30·Avatar23회, 숨김/표시 앱 세션 각각614프레임. 기존 이미지2316장 중2300장 완전 동일,16장은 y32 위 동적 HUD만 다릅니다. 변경된 갤러리와 신규 이미지는 별도 검증했습니다. 일반 미리보기는 실행하지 않았습니다. 근거: `artifacts/card-preview-2026-11-12/verification.json`. 공식 전체 적합성과 원래 RC3 배포·성능 관문은 남아 있습니다.


2026-11-12 최소 할당 수정: CardContent와 직접 배치한 세로 CardPreview는 그림자·패딩을 뺀 Card 본체의 최소 크기를 유지합니다. 세로 Header/Footer는 먼저 고유 폭을 측정한 뒤 본체 폭으로 늘리고, 가로의 마지막 Header/Footer는 최대 폭이 느슨해도 남은 최소 폭을 채웁니다. Preview의 주축 고유 크기를 유지하며 가로 미디어는 최소 높이 안에서 가운데 배치됩니다. 준비한 부모 입력 스냅샷으로 재사용을 안정적으로 유지합니다. `./run-gallery.ps1` → **카드** → **부모의 최소 크기**에서 부모 최소 폭과 방향을 바꿉니다. 고정 Card 폭 없이 실제 제약 부모를 사용합니다. CSS min-content/flex-shrink 전체 적합성은 남아 있습니다.

근거: `artifacts/card-minimum-allocation-2026-11-12/verification.json`. 최초 계약 실패9건을 재현했고 최소 제약 전달과 슬롯 고유 폭을 교정한 뒤 전체 CPU55개/8772회(Card419·Observer226), GPU12개 프로필2725회(Card656), 외부 접근성80회와 숨김/표시614프레임 세션이 통과했습니다. 기존 RGB 이미지2332장 중2312장 완전 동일,20장은 y32 위 동적 HUD만 다릅니다. 신규32장과 변경된 Observer 갤러리는 기하·픽셀·정확한 재사용을 별도로 검사했습니다. 시간 개선율은 측정하지 않았습니다. 팩터리 노출46개, 상위55개 중 연결23개를 유지합니다. 원래 배포·성능·전체 적합성 관문은 남아 있습니다.

추가 프레임 회귀: Card GPU 프로필은 요청 MaxFramesInFlight3에서도656회 통과하여 총13개 프로필/3381회입니다. 요청2/3슬롯 사이 같은 이름의517장은 RGB 바이트 단위로 일치합니다. 실제 동시 프레임 수를 측정한 것은 아닙니다.

2026-11-13 [Popover 내부 메뉴](fluent2-popover.ko.md): 소유 메뉴의 포털은 호버 유지와 바깥 클릭 판정에 포함됩니다. 메뉴 위에서는 닫기 예약을 취소하며 메뉴 항목 클릭은 부모 Popover를 닫지 않고 명령을 한 번 실행합니다. Escape는 메뉴부터 닫고 다음 Escape에 부모를 닫습니다. 포커스 제한에서도 열린 메뉴의 트리거 포커스를 유지합니다. 관찰 샘플 → Popover → 팝업 내부 메뉴에서 확인합니다.

2026-11-13 [Popover 진입 모션](fluent2-popover.ko.md): 기본400ms 페이드·방향별10px 이동·즉시 닫기·같은 시점 캐시·실제 입력/IME/접근성 좌표·유한 예약을 연결했습니다. 관찰 샘플 → Popover → 팝업 모션. 전체 적합성은 미완료입니다.

2026-11-13 [SearchBox 지우기](fluent2-searchbox.ko.md): 여러 프레임 누름 유지 뒤 놓아도 실제 값·표시를 지우며 포커스·Tab·되돌리기·한글 재입력을 유지합니다. 관찰 샘플 → 검색 입력.

2026-11-13 [Popover 그리기 범위](fluent2-popover.ko.md): 사용자 정의 PaintBounds·뷰포트 기본값·범위/예산 검사·넓은 장식 샘플을 연결했습니다. 기본 Fluent 픽셀은 유지합니다. 네이티브 Popover 검사 실패는 별도 진단 중이며 전체 적합성 완료가 아닙니다.


`UiPopoverOptions.CloseOnFocusLeave`는 기본 `false`입니다. 켜면 트리거·콘텐츠·중첩 표면 안에서 머물던 포커스가 바깥의 다른 컨트롤로 이동할 때 닫으며, 이동한 포커스를 유지합니다. 자동 포커스를 꺼도 트리거에서 출발한 이탈을 감지합니다. 닫기 바인딩이 거부하면 표면을 유지하고, 영역에 다시 들어왔다가 나갈 때 재시도합니다. 새 Timer나 주기 프레임은 요청하지 않습니다. 관찰 샘플 → Popover → **포커스 이탈 시 닫기**에서 전환합니다. InfoLabel의 공식 InfoButton 동작을 위한 공통 기반이며 InfoLabel 자체의 구현 완료를 뜻하지 않습니다.


### 드래그로 이동하는 탭

`UiTabListOptions.Scroll = new UiTabListScrollOptions()`로 가로 헤더 스크롤을 켭니다. 기존 `Overflow` 메뉴 할당 방식과는 별도 선택이며 두 옵션을 동시에 켜지 않습니다. 상단 관찰 페이지는 이 스크롤 방식을 사용합니다.

탭 영역을 드래그하거나 휠로 움직입니다. 가려진 탭이 있는 쪽에만 `…`이 나타나므로 왼쪽·오른쪽·양쪽 표시가 가능합니다. 점 메뉴를 누르면 해당 방향의 탭 목록이 열리며 위치는 그대로 유지합니다. 항목을 고르면 원래 탭·본문을 선택하고 표시합니다. 가장자리 탭을 누르거나 키보드로 이동하면 선택한 탭뿐 아니라 그 방향의 이웃 탭 하나도 노출해 탐색을 이어갈 수 있습니다. 드래그를 놓는 동작은 탭 클릭으로 처리하지 않습니다.

하단 스크롤바는 별도 영역을 차지하지 않는 오버레이입니다. 기본120ms 나타남·400ms 정지 대기·160ms 사라짐을 적용하며 드래그 중에는 표시를 유지합니다. 공통 `UiScrollbarMotionOptions`로 시간·곡선·감소 모션을 설정하고, 탭별 재정의는 `UiTabListScrollOptions.Motion`으로 지정합니다. 애니메이션이 끝나면 다음 프레임 예약을 멈춥니다. [공통 전환 설계와 API](finite-transitions.ko.md)를 참조하세요.


전체 스크롤바는 동일한 [공통 모션 모듈](scrollbar-motion.ko.md)을 사용합니다.


공식 React 데모를 직접 비교한 기본 호버 반응은 투명 배경을 유지하는 손 모양 커서입니다. 비활성 헤더는 금지 커서와 비활성 글자색을 사용합니다. Duxel도 헤더의 실제 클립·팝업 가림을 확인한 뒤 해당 커서를 게시합니다. 키보드 Tab은 포커스 테두리를 표시하고 Enter·Space는 기존 헤더 버튼 계약으로 동작합니다. 별도 호버 배경색은 공식 기본 스타일에 추가하지 않습니다.

출처: [디자인 가이드](https://fluent2.microsoft.design/components/web/react/core/accordion/usage), [실제 React 데모](https://react.fluentui.dev/?path=/docs/components-accordion--docs). 브라우저·고정 소스 근거는 `artifacts/accordion-hover-2026-11-13/official-evidence.json`에 보존합니다. 이는 해당 상호작용의 검증이며 전체 Fluent 시각·모션 적합성 완료를 뜻하지 않습니다.


InfoLabel: `Fluent.InfoLabel(text, info, target, options, informationLabel)`은 입력을 참조하고 정보 팝업을 소유합니다. [API·크기·수명](fluent2-info-label.ko.md), 관찰 경로는 **추가 정보**입니다.

2026-11-13 [숫자 입력](fluent2-spinbutton.ko.md): 비어 있을 수 있는 숫자 바인딩, 범위 제한·편집 반영, 공통 문자열 편집, 네이티브 Spinner·Value·RangeValue와 길게 누르기 반복을 연결합니다. 관찰 샘플 → **숫자 입력**에서 생성 팩터리와 단위 파서를 확인합니다. 팩터리58개·공식 상위34/55 연결·21개 미연결이며 전체 적합성은 미완료입니다.


2026-11-13 [숫자 입력 포인터 포커스](fluent2-spinbutton.ko.md): 임시 포커스 초기화로 같은 편집 영역의 커서 클릭이 값을 반영하던 문제를 교정합니다. 실제 이동 대상의 명령은 반영한 값을 읽으며 중단된 입력 처리의 소유 포커스도 복구합니다. 회귀 근거: artifacts/spinbutton-pointer-focus-2026-11-13/verification.json.


2026-11-13 [목록 초기 구현](fluent2-list.ko.md): 임의 행 소유·단일/다중 선택·행 실행·복합 자식 탐색을 관찰 샘플 → **목록**에서 확인합니다. 네이티브 Grid·GridItem은 행·열 수, 논리 좌표, 차지하는 영역, 실제 셀과 소속 그리드를 제공하며 함께 반영하고 변경을 알립니다. 생명주기와 외부 클라이언트 검증은 남아 있으며 생성 팩터리와 카탈로그 수에는 아직 포함하지 않습니다.


2026-11-13 [목록 정렬·선택 표시](fluent2-list.ko.md): 자연 높이의 콘텐츠와 복합 샘플의 자식을 세로 중앙에 맞춥니다. 사용자 정의 측정 재사용 기본값은 false이며 체크 표시는 모든 선택 변경 후 그립니다. 행 포커스 테두리도 모든 행의 요청과 Tab 진입점 복구 후 그립니다. 근거: artifacts/list-paint-2026-11-13/verification.json, artifacts/list-focus-2026-11-13/verification.json. 네이티브 그리드 근거: artifacts/list-grid-2026-11-13/verification.json. 팩터리 검증은 남아 있습니다.


2026-11-13 [검색형 선택 목록 화살표](fluent2-combobox.ko.md): 공통 편집기의 포인터 소유권 유지 기능으로 여러 프레임 동안 누른 화살표 클릭을 보존합니다. 외부 Open 바인딩과 RTL도 같은 경로를 사용합니다. 관찰 샘플 → 선택 목록 → 도시 검색. 근거: artifacts/combobox-arrow-2026-11-13/verification.json.


Markdown 구현: [원문·표시 계약과 API](markdown-widgets.ko.md), Dux.MarkdownView / Dux.MarkdownEditor, 관찰 샘플 → Markdown. 편집 중인 코드 블록만 펜스를 표시하고 다른 코드 블록은 본문만 표시합니다. 조합 입력은 커서가 속한 줄의 글꼴을 사용하며 해제된 위젯에는 콜백을 다시 설정할 수 없습니다. 파이프 표의 열 정렬·빈 셀 원문 위치·가로 넘침과 원문 편집도 같은 버퍼를 사용합니다. 적합성 검증은 진행 중입니다.

MarkdownView 링크 이동: 일반·참조·자동·표 내부 링크의 목적지를 보관하고 손 모양 커서와 놓기 시 이동을 지원합니다. 드래그는 이동을 취소합니다. 기본 시스템 웹·메일 처리기, Navigate 콜백과 상대 주소용 절대 BaseUri는 markdown-widgets.ko.md를 참고합니다.
2026-11-13 목록 팩터리: Fluent.List의 일반·단일·다중 선택 오버로드를 관찰 샘플 → 목록에서 사용합니다. 팩터리59개·상위35/55 연결·20개 미연결입니다. 소유권·네이티브 검증과 남은 한계는 fluent2-list.ko.md를 참고합니다.
Markdown 이미지: ResolveImage는 정규화한 URI를 불변 UiImageSource에 연결하며 InvalidateImages는 재시도와 이벤트 대기 모드의 프레임 요청을 처리합니다. 기존 PNG·SVG 디코더와 텍스처 사용권을 재사용합니다. 관찰 샘플 → Markdown → 이미지 예제. 로딩 정책과 한계는 markdown-widgets.ko.md를 참고합니다.

MarkdownView 링크는 Tab·Shift+Tab·Enter, 표시된 각 부분의 포커스 테두리와 문서 내부 스크롤을 지원합니다. Windows에는 Hyperlink·Invoke·읽기 전용 주소 Value·ScrollItem과 실행당 한 번의 Invoked 이벤트를 제공합니다. 드래그는 텍스트를 선택합니다. 소유한 상위 ScrollView에는 접근성 없이도 표시 요청을 전달하며 수동·형제 스크롤을 보존합니다. 기존 창 스크롤과 실제 화면 읽기 프로그램 검증은 별도 범위입니다. markdown-widgets.ko.md를 참고하세요.

생성기·카탈로그 변경은 위젯 계약 실행기 외에 Duxel.Generators.Tests도 실행합니다. 독립 공식 사이트 fixture를 유지하며 실제 런타임·OS·아키텍처를 기록합니다. Windows와 런타임을 포함한 Linux x64 관리형 실행에서117건·생성 소스6개 일치가 통과했습니다. 선언 제거 캐시·Linux UI·ARM64는 남아 있습니다. 이식 대상 지정과 실행 명령은 widget-contract-validation.ko.md를 참고하세요.

Linux X11 창이 LinuxPlatformTextBackend를 소유합니다. 명시한 파일의 OpenType 셰이핑·측정·래스터·메트릭과 기존 아틀라스 팩터리를 공통 Vulkan 렌더러에 연결했습니다. 네이티브 폰트 캐시의 상한과 마지막 참조 해제 콜백으로 수명을 관리합니다. 한글·라틴 문자·커닝·결합 문자·여러 GPU 프레임을 검사합니다. Linux 벡터 텍스트·12도 기울임은 HarfBuzz7 이상 윤곽과 고정한 LibTessDotNet1.1.15를 사용합니다. 변경하지 않는 글리프 메시를 상한이 있는 캐시로 공유하고 기존 투영 렌더러로 그립니다. 벡터73건·래스터87건·플랫폼31건이 통과했으며 Cairo 내부51,933개 지점, GPU 기준6쌍과 프레임2/3개 간3쌍이 일치했습니다. 전체 타이포그래피·앱 진입점·Linux/ARM64·고정 RC3 NativeAOT 검증은 남아 있습니다. linux-platform.ko.md와 artifacts/linux-platform-2026-11-13/vector-verification.json을 참고하세요.

Linux 앱 진입점: Duxel.Linux.App/DuxelLinuxApp이 X11 플랫폼을 소유하고 실제 폰트를 명시해 기존 DuxelAppSession.RunCore를 사용합니다. 대화형 samples/Duxel.Linux.Sample은 기본적으로 V-Sync를 끄고 변경이 없으면 이벤트를 기다립니다. WaitEvents(0)은 무기한 대기한 후 네이티브 이벤트를 전달합니다. 앱17건·플랫폼34건에서 보이는 창의 대기, 작업 스레드 갱신, 버튼·XIM 영문 입력, 두 종료 경로와 검증 훅 없는 렌더러를 확인했습니다. linux-platform.ko.md, 한영 샘플 README와 artifacts/linux-app-2026-11-13/verification.json을 참고하세요. 사용자 지정 창 통합·전체 플랫폼·원래 고정 RC3 NativeAOT 검증은 남아 있습니다.

Linux 텍스트 클립보드: LinuxX11Clipboard/IUiClipboard는 독립 XCB 연결·대기하는 소유 스레드·임시 동기 읽기를 사용합니다. 실행기는 Clipboard/ClipboardFactory가 없을 때만 기본 서비스를 소유하며 호출자가 제공한 서비스는 해제하지 않습니다. UTF8/TEXT/Latin1 협상·TARGETS/TIMESTAMP/MULTIPLE·상한이 있는 INCR을 독립 Xlib·수명 검사35건으로 확인했습니다. 갱신된 앱21건에는 실제 Ctrl+C/V 한글·이모지 전송과 주입한 서비스의 수명 검사가 포함됩니다. 자동 검사는 별도 selection을 사용해 사용자의 CLIPBOARD/PRIMARY를 보존합니다. 샘플을 열기만 해서는 클립보드를 소유하거나 읽지 않습니다. 앱 종료 후 보존·클립보드 이미지·COMPOUND_TEXT·Wayland·완전한 표시 서버 정지 복구는 남아 있습니다. 갱신된 샘플·빌드 명령과 네이티브 의존성 libxcb.so.1은 linux-platform.ko.md와 한영 샘플 README에 있습니다. 근거는 artifacts/linux-clipboard-2026-11-13/verification.json입니다. 관리형 .NET11/x64 기능 근거이며 고정 RC3 NativeAOT·실기기 검증을 대신하지 않습니다.

Linux 비트맵·SVG 이미지: 실행기의 기본 ImageDecoder는 LinuxUiImageDecoder.Shared이며 직접 지정한 디코더는 유지합니다. 설치된 GdkPixbuf·GLib·GObject로 PNG/JPEG/GIF/BMP/ICO를 독립 RGBA8로 읽고 행 간격·알파·첫 프레임과 ICO 첫 항목을 지킵니다. PNG 압축 ICO는 내장 PNG를 명시적으로 전달하고 DIB ICO는 네이티브 코덱을 사용합니다. 잘못된 입력은 오류입니다. SVG는 고정한 Linux Skia4.148.0 네이티브 자산과 기존 SvgStore/SvgImageDecoder/UiSvg 경로를 유지합니다. 이미지63건·앱25건에서 독립 픽셀, 실제 X11 창의 PNG/SVG 이미지·위젯, 회전 시 XML·래스터 재사용, 대기와 기존 클립보드 계약을 확인했습니다. 기존 Windows SVG112건도 .NET11에서 통과했습니다. 샘플 → 오른쪽 PNG/SVG/회전으로 확인하세요. 실행·빌드 명령과 애니메이션·EXIF·색 처리·접근성·ARM64·고정 RC3의 남은 범위는 linux-platform.ko.md와 한영 샘플 README에 있습니다. 근거는 artifacts/linux-images-2026-11-13/verification.json입니다.

Linux 네이티브 접근성: DuxelLinuxApp은 전용 GIO 연결·컨텍스트·스레드로 AT-SPI를 기본 활성화합니다. Accessible/Application/Component/Action/Cache/Value가 공통 트리·지원 동작·실제 키보드 포커스와 외부 캐시 변경 이벤트를 전달합니다. 세션·접근성 버스가 없으면 실패하며 Run(options, session, new LinuxAtSpiOptions { Enabled = false })로 명시적으로 끌 수 있습니다. 하위 LinuxX11PlatformOptions.Accessibility는 직접 지정할 때 사용합니다. UiAccessibilityTree.CaptureSnapshot/Published는 한 리비전의 복사본과 변경된 프레임의 알림을 제공합니다. Button(id, label, size)는 문구가 바뀌어도 식별자를 유지하며 기존 한 줄 입력은 접근성 포커스를 유지합니다. 실제 앱34건, 실행 경로별 독립 네이티브 클라이언트17건, 시작 실패8회에서 잔여 스레드·시도별 디스크립터 증가 없음, Windows 컨트롤78건과 공통 XCB 클립보드35건을 확인했습니다. 현재 샘플·빌드·실행은 samples/Duxel.Linux.Sample/README.ko.md, 근거는 artifacts/linux-accessibility-2026-11-14/verification.json, 세부 내용은 linux-platform.ko.md에 있습니다. Text/EditableText·Selection·표/그리드·전체 관계와 현지화·실제 화면 읽기 프로그램·DPI·ARM64/하드웨어·고정 RC3 NativeAOT는 남아 있습니다.
Linux 숫자 접근성: 범위가 있는 컨트롤은 네이티브 AT-SPI Value로 경계·현재 값·증분·설명과 반영된 변경 이벤트를 제공합니다. RequestRangeValue가 기존 위젯의 반올림을 유지하며 잘못된 값·유한하지 않은 값·읽기 전용·비활성 쓰기는 값을 바꾸지 않고 실패합니다. 샘플 하단 정수 슬라이더는1씩 조정합니다. 실제 앱37건·실행 경로별 네이티브27건과 숫자 정책5건으로 이 범위를 확인했습니다. 설치된 libatspi setter가 오류 응답에서 종료되므로 거부 검사는 독립 gdbus를 사용합니다. 렌더링 훅이나 제품 내부 API 공개는 추가하지 않았습니다. 현재 근거는 artifacts/linux-accessibility-2026-11-14/value-verification.json입니다. Text/EditableText·Selection·그리드와 전체 플랫폼·접근성 검증은 남아 있으며 linux-platform.ko.md를 참고하세요.
진행률 범위 게시: 생성된 위젯과 일반 UiProgress가 읽기 전용 실제 값 범위를 제공하며 미확정 모드에서는 숫자를 생략하고 전환 전후 식별자를 유지합니다. 시각적 보간 값은 접근성 값으로 게시하지 않습니다. 명시적 즉시 오버로드는 id를 받으며 기존 ID 없는 오버로드는 그리기만 수행합니다. 부모 자식 키가 식별자의 기준이며 접근성 트리가 없으면 의미 ID를 계산하지 않습니다. Linux AT-SPI는 모드가 바뀌면 외부 캐시의 지원 인터페이스를 갱신합니다. 컨트롤95건과 실제 앱35건·실행 경로별 네이티브32건으로 이 범위를 확인했으며 실제 UIA·화면 읽기 프로그램의 전체 적합성은 별도입니다. 샘플 하단 진행률과 ‘진행률 모드’ 버튼으로 확인하세요. 현재 근거는 artifacts/linux-accessibility-2026-11-14/progress-verification.json, API는 fluent2-progress.ko.md에 있습니다.
Linux 자식 선택: 일곱 번째 네이티브 인터페이스가 선택 참조·개수, 단일 교체·다중 추가, 전체 자식/선택 항목 인덱스, 전체 선택·해제, 필수·비활성 정책과 캐시 상태·이벤트 갱신을 제공합니다. 공통 접수 단계에서 마지막 필수 선택 항목을 보호하며 여러 필수 선택 중 하나는 해제할 수 있습니다. ComboBox의 실제 팝업 ListBox 자식이 Selection을 제공하고 외부 ComboBox는 구조적 인덱스를 유지합니다. 일괄 동작은 알려진 거부 조건을 먼저 확인하고 현재 바인딩에 순서대로 반영하며 이후 거부·계층 변경 시 앞선 변경은 유지될 수 있습니다. 현재 앱35건·실행 경로별 네이티브48건·선택 정책7건·숫자 정책5건·컨트롤99건·클립보드35건의 근거는 artifacts/linux-accessibility-2026-11-14/selection-verification.json입니다. 640x520 샘플 하단에 단일·다중 도시 목록이 있습니다. 전체 선택/위젯/가상화/실제 환경 적합성과 Text/EditableText/표/그리드는 남아 있으며 linux-platform.ko.md와 fluent2-list.ko.md를 참고하세요. 이 부분 검증으로 원래 목표의 완료 수를 올리지는 않습니다.
공통 텍스트 상태: UiAccessibilityNode.TextInfo와 편집 결과 AccessibilityText가 변경할 수 없는 확정 텍스트·커서·선택을 전달합니다. UiAccessibilityTextContent는 서로게이트 쌍을 나누지 않고 Unicode 스칼라와 UTF16을 변환합니다. 변경 없는 프레임은 상태를, 커서만 바뀌면 내용·문자 맵을 재사용합니다. GetAccessibilityTextInfo(id)는 현재 렌더 프레임과 ID 범위만 읽습니다. RequestTextSelection은 정확한 게시 내용·런타임 ID를 사용하고 읽기 전용 선택을 허용하며 포커스·텍스트를 보존합니다. 오래된 내용·비활성 상태·유효하지 않은 글자 묶음 위치는 거부합니다. 원본과 보조 바인딩을 함께 맞추며 별도 편집기·전역 텍스트 캐시를 만들지 않았습니다. 샘플 맨 아래에서 스칼라·커서·선택을 확인하세요. 컨트롤146건과 실제 Linux 앱37건·실행 경로별 네이티브48건의 회귀 검사는 이 기반의 근거입니다. 네이티브 Text/EditableText의 경계·속성·좌표·범위 편집 연결은 아직 남아 있습니다. 현재 근거는 artifacts/linux-accessibility-2026-11-14/text-model-verification.json, API와 제한은 한영 accessibility-text 문서에 있습니다.
네이티브 Text 부분 연결: TextInfo를 게시하는 노드에 여덟 번째 AT-SPI 인터페이스를 제공합니다. 스칼라 문자 수·커서·범위·코드 포인트와 방향을 가진 연속 선택 하나를 기존 변경 불가능 내용·렌더 스레드 요청으로 처리합니다. 독립 libatspi 클라이언트에서 캐시를 지우지 않고 커서·선택 이벤트와 스칼라 위치·길이·문자열을 가진 변경 이벤트를 확인했습니다. 실제 Backspace/되돌리기로 이모지 한 문자를 삭제·복원했습니다. 현재 관리형 .NET11 앱37건·실행 경로별 네이티브66건·선택7건·숫자5건·컨트롤146건·클립보드35건은 이 범위의 근거이며 전체 경계·속성·좌표·EditableText·스타일·Markdown·보호 입력·물리 검증은 남아 있습니다. 현재 근거 artifacts/linux-accessibility-2026-11-14/native-text-verification.json, 계약 accessibility-text.ko.md. 원래 목표의 완료 수는 올리지 않습니다.
게시한 텍스트 좌표: UiAccessibilityNode.TextLayout은 현재 Content에 연결된 변경 불가능 UiAccessibilityTextLayout을 제공합니다. 문자/범위 상자·클립을 적용한 점 조회·실제 원문 줄 범위는 편집기의 내용 원점·줄 높이·스크롤·글자 묶음 상자·일반 줄바꿈 맵을 사용합니다. 결합 문자는 논리 상자를 공유하고 빈 범위는 너비0을 유지합니다. 같으면 좌표 객체를 재사용하고 과거 스냅샷은 재배치/해제 이후에도 유지합니다. 보조 편집 확정은 최종 텍스트 좌표로 바꾸며 뒤늦은 숫자 포커스 확정은 이전 초안 좌표를 제거합니다. 한 줄/여러 줄 포인터 입력은 접근성 키보드 포커스를 얻습니다. 네이티브 Text는 문자/실제 줄 단위 GetStringAtOffset과 화면/창/부모 좌표의 문자/범위/점 조회를 추가했습니다. 현재 앱37건·실행 경로별 네이티브75건·컨트롤163건·클립보드35건의 근거는 artifacts/linux-accessibility-2026-11-14/text-geometry-verification.json입니다. 조합 중/스타일/Markdown/줄바꿈 없는 기존 여러 줄 좌표, 다른 단위/속성·스크롤·EditableText는 남아 있으며 accessibility-text.ko.md를 참고하세요. 원래 완료 수는 올리지 않습니다.
검사하는 텍스트 편집: CanEditText·UiAccessibilityTextEdit·RequestTextEdit는 기존 선택 생성자를 유지하고 원문/런타임 검사와 실제 버퍼·이력을 사용합니다. Replace/Copy/Cut/Paste로 네이티브 Insert/Delete/SetContents를 처리하며 길이·글자 묶음/UTF16·읽기 전용 복사·최종 바인딩 게시·클립보드 IO/시간 초과 결과를 명시합니다. 아홉 번째 EditableText는 내용 메서드 여섯 가지를 내보내며 위치는 스칼라, InsertText 길이는 UTF8 바이트입니다. 스칼라 중간·음수 길이는 실패합니다. 별도 selection과 실제 Ctrl+Z/Y로 검사했습니다. 현재 관리형 앱37건·실행 경로별 네이티브92건·컨트롤189건·클립보드35건의 근거는 artifacts/linux-accessibility-2026-11-14/text-edit-verification.json입니다. 클립보드·편집기·외부 바인딩 전체의 트랜잭션은 아니며 전체 스타일/조합/물리 검증과 나머지 Text 적합성은 남아 있습니다. 한영 accessibility-text 가이드를 참고하세요.
네이티브 Unicode 경계: GetStringAtOffset의 다섯 단위를 지원합니다. AT-SPI 버스를 만들기 전에 Pango1.0 ABI libpango-1.0.so.0과 지원하는 리틀 엔디언 ABI를 확인하며 이번 검증 버전은1.52.1입니다. 문단별 네이티브 Unicode/언어 처리로 단어·문장 시작점과 CRLF/Unicode 문단 범위를 스칼라 좌표로 얻습니다. UI 문화권 또는 언어 미지정 und 태그를 사용합니다. 현재 Content/언어마다 변경 불가능 표 하나를 유지하고 커서 갱신 시 전달하며 내용/언어가 바뀌면 무효화하고 노드와 함께 제거합니다. 전역 텍스트 캐시나 측정한 속도 향상은 주장하지 않습니다. 검사59건은 앱/세션38건+경계21건이며 네이티브는 실행 경로별101건, 변경 없는 Core/컨트롤189건·클립보드35건의 근거는 artifacts/linux-accessibility-2026-11-14/text-boundaries-verification.json입니다. 여러 문단은 모듈에서, 실제 RPC는 한 줄 입력에서 검사했습니다. 이전 경계 API·속성·전체 스타일/조합·스크롤·물리 검증은 남아 있습니다. 한영 accessibility-text·Linux·샘플 가이드를 참고하세요.

이전 Text 조회 확장: Before/At/AfterOffset에서 스칼라·시작·끝 경계7종을 지원하며 Pango 끝점과 실제 그려진 줄을 재사용합니다. UiAccessibilityTextLayout.LineCount/GetLine의 변경 불가능 UiAccessibilityTextLine(Start, End, NextStart)에서 End는 구분자를 제외하고 자동 줄바꿈이면 End == NextStart입니다. 검사93건=앱/세션38+경계/배치55, 실행 경로별 네이티브127건, 새 공통 컨트롤193건·클립보드35건입니다. 여러 줄/자동 줄바꿈은 실제 Linux 배치 모듈 검사이며 여러 줄 네이티브 RPC 검증은 아닙니다. 정확한 한계는 artifacts/linux-accessibility-2026-11-14/legacy-text-boundaries-verification.json과 한영 accessibility-text 가이드를 참고하세요.

단일 색 원문 그리기: UiAccessibilityNode.TextAppearance의 UiAccessibilityTextAppearance(Content, Foreground)는 실제 일반 편집기의 그리기에서 수집합니다. 내용/색이 같으면 재사용하며 그리기 생략·다른 원문·스타일·조합에는 이전 속성을 재사용하지 않습니다. 네이티브 기본/범위/이름 조회는 fg-color만 제공하고 기본 속성과 빈 명시 속성 집합을 구분합니다. 검사93건·실행 경로별 네이티브134건·새 Windows 컨트롤201건·클립보드35건입니다. 폰트 정보·스타일별 범위·속성 변경 알림은 미완료입니다. artifacts/linux-accessibility-2026-11-14/text-attributes-verification.json과 한영 accessibility-text 가이드를 참고하세요.

원문 색 변경 알림 (2026-11-14): 유지 중인 텍스트 노드에서 게시하는 전경 RGB나 단일 색 그리기 정보의 제공 여부가 바뀌면 object:text-attributes-changed를 보냅니다. 원문/커서 객체만 바뀌면 보내지 않으며 게시하지 않는 알파 변경도 제외합니다. 소유한 검사 전용 래퍼에서 실제 편집기 색을 RGB17,83,201로 변경하고 일반 그리기로 복원합니다. 독립 libatspi는 원문/커서가 유지된 상태에서 변경마다 알림1건을 관찰하며 같은 색의 재그리기 확인 후200ms 동안 중복 알림이 없었습니다. 제품 샘플의 배치/동작은 유지합니다. 검사93건·실행 경로별 네이티브137건이며 공통 소스32개와 Core DLL이 같음을 확인해 컨트롤201건을 재사용하고 클립보드35건을 다시 검사했습니다. 근거는 artifacts/linux-accessibility-2026-11-14/paint-events-verification.json입니다. 전경색 알림만 검증했으며 실제 선택 폰트 정보·스타일별 범위·물리 화면 읽기 프로그램의 전체 적합성은 남아 있습니다.
## 선택된 원문 폰트 정보 (2026-11-14)

LinuxPlatformTextBackend는 선택 기능인 IPlatformTextFontRunBackend.GetFontRuns(request, nameLanguage)를 구현합니다. 원본의 UTF16 위치를 기준으로 읽기 전용 PlatformTextFontRun(StartUtf16, EndUtf16, Font) 목록을 반환합니다. 실제 배치·래스터·벡터 그리기와 같은 SelectFont 규칙을 사용합니다. 보조 평면 문자는 UTF16 길이를 유지하고 CR/LF 구분자에는 그려진 폰트 범위를 만들지 않습니다. 원문 탭 하나에는 네 칸 공백으로 펼쳤을 때 사용하는 폰트를 선택합니다. 이는 글리프 조형 전의 폰트 선택이며 실제 배치/조형 검사는 별도입니다. 포인트 크기를 임의로 환산하지 않습니다.

PlatformFontMetadata는 OpenType 패밀리/하위 패밀리 이름(name ID1/2), OS/2 굵기·원래 기울임, 실제 합성 기울임 여부와 정규화한 요청 이름 언어를 담습니다. HarfBuzz의 공개 ABI로 실제 조형에 쓰는 불투명 폰트 객체의 테이블을 읽습니다. 그 언어의 이름이 없으면 null을 유지하며 영어나 파일 이름으로 대체하지 않습니다. OS/2 테이블이 없으면 해당 필드는 null이고 잘못되거나 잘린 정보는 명시적으로 거부합니다. 기존의 개수가 제한된 폰트 리소스마다 가장 최근에 요청한 언어의 정보를 보관합니다. 반환한 변경 불가능 값은 언어 변경이나 네이티브 해제 뒤에도 유지됩니다. 조회마다 원문 범위 목록은 새로 만들며 속도 향상은 주장하지 않습니다.

샘플은 기존 두 폰트 실행 명령에서 같은 창을 표시하기 전에 `정A`의 선택 폰트를 시작 시 한 번 출력합니다. 소유한 실행 검사에서는 Malgun Gothic·DejaVu Sans와 굵기400이 출력되었습니다. .NET11/Linux x64 검사111건은 앱/세션38·경계/배치55·폰트 선택/정보18건입니다. 독립 fontTools4.66.1이 실제 입력 파일에서 읽은 이름/OS2 값과 일치하며 SHA256·이름 기록은 font-runs-independent-oracle.json에 있습니다. HarfBuzz8.3.0·실행 경로별 네이티브137건·새 공통 컨트롤201건·클립보드35건이 통과했습니다. 근거는 `artifacts/linux-accessibility-2026-11-14/font-runs-verification.json`입니다. 이 기반을 편집기/AT-SPI의 폰트별 범위로 게시하는 연결은 아직 미완료이며 포인트 크기 단위·스타일 문서·물리 검증도 남아 있습니다. 참고: [HarfBuzz 이름](https://harfbuzz.github.io/harfbuzz-hb-ot-name.html), [OpenType OS/2](https://learn.microsoft.com/en-us/typography/opentype/spec/os2).

편집기 폰트 속성 (2026-11-14): UiAccessibilityTextAppearance가 검증한 변경 불가능 스칼라 UiAccessibilityTextFontRun 목록과 FontRunCount/GetFontRun/GetFontRangeAt, 원본 PlatformTextFontRun 범위를 받는 생성자를 제공합니다. 구분자 간격은 유지하고 스칼라 분할·범위 겹침·잘못된 굵기/이름은 거부합니다. 공통 편집기 그리기는 원문·측정 설정·이름 언어가 바뀔 때만 선택적 직접 폰트 백엔드에 조회합니다. 같은 프레임 내용은 스냅샷을 재사용하고 색만 바뀌면 폰트 맵을 공유합니다. 실제 직접 측정/그리기와 같은 폰트 크기 계산을 사용하며 포인트 크기를 추정해 게시하지 않습니다. UiAccessibilityTree.FontNameLanguage의 초기값은 und이며 변경을 검사하고 프레임을 요청합니다. UiImmediateContext.SetAccessibilityFontNameLanguage로 설정할 수 있습니다. 샘플은 LinuxDemoScreen.FontNameLanguage에서 en을 명시하며 소유한 검사에서는 영문 대체 없이 ko 변경도 확인합니다. 네이티브 GetAttributes/GetAttributeRun은 실제 family-name/weight/style 명시 속성과 선택적인 fg-color 기본값, 스칼라 범위·그리지 않은 간격·문서 끝을 지원합니다. 게시한 속성이 같으면 인접 범위를 합칩니다. 원래 폰트와 합성 기울임을 구분하며 OS/2 bit9는 버전4부터 해석합니다. 원문이 같을 때 게시한 폰트 속성의 변경을 알리고 게시하지 않는 차이나 텍스트 변경의 중복 알림은 제외합니다. 실제 이름 언어 변경/복원과 같은 내용 재그리기를 독립 관찰했습니다. 현재 .NET11/Linux 검사114건=앱/세션38+경계/배치58+폰트 선택18, 네이티브142건씩·새 공통 컨트롤209건·클립보드35건입니다. 근거는 artifacts/linux-accessibility-2026-11-14/font-attributes-verification.json입니다. 일반 편집기 폰트 범위 게시를 검증했으며 포인트 크기·스타일/Markdown/조합·아틀라스만 쓰거나 혼합 대체 경로의 폰트 정보·물리 화면 읽기 프로그램은 미완료입니다. 선언된 스타일 플래그는 OpenType OS/2 참고 문서를 따릅니다.

명목 포인트 크기 (2026-11-14): UiAccessibilityTextSize(PhysicalPixels, ContentScale)는 직접 렌더러에 요청한 em 크기를 명시합니다. LogicalPixels = PhysicalPixels / ContentScale, Points = LogicalPixels *72/96이며 엔진의 논리96-DIP 기준과 렌더러 float 정밀도를 따릅니다. 명목 서식 크기이며 글리프의 실제 잉크 높이·줄 간격이나 모니터의 물리 인치가 아닙니다. Validate는 잘못된0/비유한 값과 표현할 수 없는 변환을 정보 조회·게시 전에 거부합니다. UiAccessibilityTextAppearance.Size는 nullable이며 기존2/3인자 생성자를 유지하고 모르는 크기는 생략합니다. 새4인자 생성자는 크기를 받고 캐시 키에 포함하며 색만 바뀐 스냅샷도 이를 유지합니다. 네이티브 size는 문화권에 독립적인 포인트 문자열입니다. 이름 조회와 알려진 기본 속성(fg-color+size), GetAttributeRun(true)에 포함하며 명시적인 폰트 속성과 구분합니다. 실제 크기 변경/복원은 알림1건씩, 같은 크기의 재그리기는200ms 동안 중복 알림이 없었습니다. 구성한 콘텐츠 배율1/1.25/1.5/2/2.5의 모듈 검사에서 컴파일된12논리픽셀 글자는9pt, 기존18은13.5pt입니다. 확대1.25는16.875pt이며 줄 간격을 두 배로 바꿔도 유지됩니다. 실제 네이티브24논리픽셀 입력은18pt를 반환하고 원문/커서 변경 없이13.5pt로 복원합니다. .NET11/Linux 검사127건=앱/세션38+경계/배치71+폰트 선택18, 네이티브146건씩·새 컨트롤214건·클립보드35건입니다. 근거는 artifacts/linux-accessibility-2026-11-14/text-size-verification.json입니다. 물리 DPI 변경/실기기와 스타일/Markdown/조합 글자 크기는 미검증입니다. 참고: https://www.w3.org/TR/css-values-4/#absolute-lengths 및 https://gnome.pages.gitlab.gnome.org/gtk/atk/enum.TextAttribute.html.

샘플에서 확인: GetAccessibilityTextAppearance(id)는 현재 렌더 프레임과 ID 범위에 이미 게시한 변경 불가능 그리기 정보를 읽으며 프레임을 요청하지 않습니다. Linux 샘플 하단에 실제 입력의 포인트 크기(처음에는 글자 : 13.5 pt)를 표시하므로 기존 두 폰트 실행 명령으로 새 값을 볼 수 있습니다. 정보가 없으면 생략합니다.

### Markdown 원문 좌표와 편집

MarkdownView와 MarkdownEditor는 실제 스타일 배치에서 확정 원문·커서·선택과 변경 불가능한 좌표를 게시합니다. 네이티브 위치는 마크다운 원문을 기준으로 합니다. 숨긴 기호의 너비는0이며, 제목·본문은 실제 글자 높이를, 표는 셀 정렬과 여백을 반영합니다. 포커스한 블록의 기호를 표시하면 새 배치를 게시하고 보관한 이전 배치는 유지합니다. 범위 교체는 원문을 편집한 뒤 문서를 다시 해석하며, 앱이 입력값을 정규화해도 최종 원문과 좌표를 같은 프레임에 맞춥니다. 읽기 전용 뷰는 변경을 거부합니다. 스타일별 폰트·색상 속성, 조합 중 좌표와 문서 전체 의미 구조는 미검증입니다. 이 계약은 렌더링된 글자만을 읽는 접근성 모델과 구분합니다.

검증: 관리형 .NET11/Linux x64 검사148건(앱/세션38·경계/배치92, 스타일21건 포함·폰트 선택18), 기존 샘플 실행 경로별 독립 네이티브146건·Windows 공통 컨트롤214건·클립보드35건·소유 샘플 자동 종료0입니다. 스타일21건은 실제 Linux CPU 배치 검사이며 실제 AT-SPI Markdown 화면 읽기나 하드웨어 GPU 검증은 아닙니다. 근거는 `artifacts/linux-accessibility-2026-11-14/styled-text-verification.json`입니다. 검사 전용 그리기 요청과 확인은 순번별로 한 번 생성하는 마커 파일을 사용해 파일 교체 중 경합을 없앴으며 기존 시간 제한을 유지합니다.

### 독립 네이티브 Markdown 검증

검사에서 소유한 편집기·뷰어 창을 검증 훅 없는 제품 렌더러와 독립 libatspi 프로세스로 실행했습니다. 명시적·등록 Linux 실행 경로에서 각각22건을 통과했습니다. 원문과 문자 위치, 숨긴 기호·제목·표의 좌표, 위치 조회, 원문 선택과 알림, 블록 기호 표시와 실제 X11 포커스, 삽입·삭제와 변경 알림, 코드 블록 교체, 읽기 전용 거부를 확인했습니다. 실제 X11 Ctrl+Z/Ctrl+Y로 원문 편집 이력도 확인했습니다. 네이티브 텍스트는 기호를 포함한 마크다운 원문입니다. 이는 네이티브 프로토콜 검증이며 물리 화면 읽기 도구, 스타일 속성과 조합 중 좌표는 남아 있습니다. 일반 샘플과 Observer의 배치는 유지했습니다.

최신 검사152건=앱/세션42+경계/배치92+폰트 선택18입니다. 기존 샘플의 네이티브146건씩도 별도로 통과했습니다. 공통 컨트롤214건·클립보드35건·샘플 자동 종료 근거는 제품 소스와 바이너리가 같은 것을 확인한 뒤 재사용하며, 검사 코드만 바뀐 이번 단계에서 다시 실행하지 않았습니다. 소스와 결과 원장은 `artifacts/linux-accessibility-2026-11-14/markdown-native-verification.json`입니다. 네이티브 자식12초·전체45초 제한은 유지했습니다.

### 스타일별 원문 그리기 속성

`UiAccessibilityTextAppearance`는 기존 단일 스타일 생성자를 유지하며 원문 범위별 스타일도 지원합니다. `HasUniformPaint`로 구분하고, 스타일 문서는 `PaintRunCount`·`GetPaintRun`·`GetPaintRangeAt`로 읽습니다. 스타일 문서에 단일 `Foreground`를 요청하면 예외로 알립니다. `UiAccessibilityTextSourcePaintRun`은 UTF-16 원문 범위를 받으며 스냅샷은 검증·복사한 뒤 문자 범위로 변환합니다. 숨긴 기호·줄 구분자·글자가 아닌 대체 객체에는 그리기·폰트 속성을 만들지 않습니다. 알려진 크기는 기존 픽셀·콘텐츠 배율·포인트 계약을 따릅니다.

스타일 그리기와 속성 게시가 같은 타이포그래피 범위와 색상 계산을 사용합니다. 원문 투영·자원·배율·언어·색상이 같으면 변경 불가능한 맵을 재사용하며 폰트 범위를 다시 조회하지 않습니다. 재사용을 검증했으며 속도 개선율을 측정한 것은 아닙니다. 일반 입력의 네이티브 기본 속성은 유지합니다. 스타일 문서는 기본 속성을 비우고, 각 원문 범위에 실제 폰트·굵기·기울임·색상·포인트 크기를 게시합니다. 내보내는 값이 같을 때만 범위를 합칩니다. 기호를 펼치면 원문 변경 없이 속성 알림1건을 보내며 같은 포커스의 재그리기는200ms 동안 중복 알림이 없습니다. 굵은 글씨 역할에 일반 폰트를 지정한 검사에서는 실제 굵기400을 보고하고, 강조는 실제 기울임 변형을, 링크는 실제 색상을 보고합니다.

최종 .NET11/Linux 검사157건=앱/세션42+경계/배치97(스타일26건)+폰트 선택18입니다. 독립 Markdown은 명시적·등록 실행 경로별34건, 기존 샘플은146건씩이며 새 Windows 공통 컨트롤220건·클립보드35건·소유 샘플 종료0을 확인했습니다. 근거는 `artifacts/linux-accessibility-2026-11-14/styled-paint-verification.json`입니다. 조합 중 좌표·속성, 물리 화면 읽기 도구, 하드웨어·ARM64와 고정 RC3 NativeAOT는 미검증입니다. 현재 네이티브 텍스트는 마크다운 원문 기호를 포함합니다.

### IME 조합 중 확정 원문의 좌표

`UiAccessibilityTextLayout.HasComposition`은 조합 표시를 반영한 좌표인지 알려 줍니다. 원문·문자 수·커서 위치는 확정된 텍스트를 유지합니다. 기존 글자는 조합 표시 때문에 이동한 실제 위치를 사용하며, 삽입 위치의 빈 범위는 조합 표시 뒤의 커서를 가리킵니다. 조합 글자만 있는 위치는 확정 원문의 위치로 반환하지 않습니다. 화면의 줄은 확정 원문 범위로 변환하고, 조합 글자만 있는 줄은 빈 범위를 가집니다. 줄 내용에 조합 글자를 넣지 않습니다. 결합 문자는 실제 조합 배치의 글자 경계에 맞추며, 한 줄 입력의 뒷부분은 그리기와 같은 반올림 위치를 사용합니다.

배치 캐시의 기준에 조합 글자·삽입 위치·너비를 포함해 바뀐 조합에 이전 좌표를 쓰지 않습니다. 같은 조합은 변경 불가능한 스냅샷을 재사용합니다. 취소하면 일반 좌표와 속성을 복원하고, 확정하면 원문을 한 번 변경합니다. 조합 중에는 오래된 일반·스타일 속성을 유지하지 않고 속성을 생략합니다. 물리 입력기 통합·네이티브 조합 이벤트·조합 중 그리기 속성은 미검증입니다.

단일 줄·여러 줄·Markdown에 제어된 IME47건을 적용해 실제 Linux 텍스트 배치로 검사했습니다. 원문과 문자 위치, 앞뒤 글자와 커서, 조합 영역 조회, 캐시, 줄바꿈·자동 줄바꿈, 결합 문자, 문서 앞뒤 삽입, 빈 문서, 취소와 확정을 확인했습니다. 최신 .NET11/Linux 검사204건=앱/세션42+경계/배치144(기존 일반71·스타일26·조합47)+폰트 선택18입니다. 기존 네이티브 Markdown34건·샘플146건씩과 새 Windows 컨트롤220건·클립보드35건·소유 샘플 종료0도 통과했습니다. 기존 네이티브 검사는 실제 IME 조합의 새 검증으로 보고하지 않습니다. 근거는 `artifacts/linux-accessibility-2026-11-14/composition-layout-verification.json`입니다.

### 표준 입력의 자동 줄바꿈 없는 좌표와 스크롤

표준 여러 줄 입력에서 Wrap=false일 때도 확정 원문의 줄 좌표를 게시합니다. 기존 접근성 항목에 줄 지도 하나를 보관하고 실제 그리기와 같은 측정 엔진을 사용합니다. 빈 줄·마지막 줄·문자 위치를 유지하고 그리기 영역 밖의 위치는 반환하지 않습니다. 기존 편집·그리기 경로를 바꾸거나 전역 원문 캐시를 만들지 않습니다. 비주얼 줄 지도가 없는 이 기존 경로의 조합 좌표는 커서·밑줄 통합 검증 전까지 생략합니다. 앞선 제어된 조합47건은 다른 경로의 검증입니다.

컴파일된 여러 줄 입력은 접근성의 양방향 스크롤 요청을 그리기 전에 적용하고, 보이는 크기·내용 크기·현재 위치와 화면으로 이동할 영역을 게시합니다. Markdown도 같은 도우미를 사용합니다. 좌표 캐시에는 원문·자원·버전·클립·조합과 함께 실제 폭·자동 줄바꿈 여부·스타일 투영을 포함합니다. 서로 다른 줄 지도의 지역 버전 번호가 같다는 이유로 이전 좌표를 재사용하지 않습니다. 올바른 표준 경로의 대조 검사에서 이전 방식의 실패를 확인했으며, 최종 키는 새 지도가 같은 배치를 만들 때 재사용도 유지합니다. 초기에 탐색 기능이 있는 Fluent 경로와 기본 여백을 사용한 대조는 결함 증거에서 제외했습니다. 속도 개선율을 주장하지 않습니다.

최종 .NET11/Linux 검사219건=앱/세션42+경계/배치159(일반71·스타일26·제어된 조합47·줄바꿈 없음/스크롤/캐시15)+폰트 선택18입니다. 실행 경로별 독립 문서·입력42건은 Markdown34건과 실제 표준 입력8건이며 기존 샘플146건씩도 통과했습니다. 새 Windows 컨트롤220건·클립보드35건·소유 샘플 종료0도 확인했습니다. 근거는 `artifacts/linux-accessibility-2026-11-14/unwrapped-scroll-verification.json`입니다. 네이티브 텍스트 부분 범위 스크롤·영역 조회, 기존 경로의 조합, 물리 입력기·화면 읽기, 하드웨어·ARM64와 고정 RC3 NativeAOT는 남아 있습니다.

### 원문을 확인하는 네이티브 텍스트 범위 스크롤

`UiAccessibilityTree.RequestTextScroll`은 `UiAccessibilityTextScroll`로 게시된 Content, 순서가 맞는 UTF-16 원문 범위와 배치 또는 유한한 클라이언트 좌표를 받습니다. `UiAccessibilityTextScrollPlacement`로 왼쪽 위·오른쪽 아래·네 방향 가장자리·가까운 위치를 지정합니다. 렌더 스레드가 현재 원문 좌표로 계산해 실제 스크롤 한도 안에서 그리기 전에 적용합니다. 원문·커서·선택·포커스를 바꾸지 않습니다. 읽기 전용 여러 줄 입력도 스크롤할 수 있으며 비활성·오래된 원문·사용할 수 없는 요청은 거부하고 조합 중에는 Busy를 반환합니다. 보이는 로컬 영역 밖의 지점은 거부합니다. 짧은 문서는 스크롤바가 없어도 이동 없이 성공합니다. wait=false의 Success는 게시 완료가 아닌 대기열 접수입니다.

AT-SPI Text에 ScrollSubstringTo와 ScrollSubstringToPoint를 연결했습니다. 문자 위치를 UTF-16으로 바꾸고 화면·창·부모 좌표는 공통 변환으로 클라이언트 좌표에 맞춥니다. 네이티브 호출은 프레임 게시 뒤에 완료됩니다. 현재 지원은 좌표가 있는 로컬 여러 줄 입력이며 로컬·상위 컨테이너의 범위 이동과 영역 조회는 아래 절을 참고합니다. 큰 범위나 내용 끝에서는 가능한 스크롤 한도를 적용하며 불가능한 정확한 배치를 약속하지 않습니다. 위치를 반영하기 전에 취소 여부를 확인하지만 강제로 만든 늦은 취소 경합을 독립 검증한 것은 아닙니다.

최종 .NET11/Linux 검사244건=앱/세션42+경계/배치184(일반71·스타일26·제어된 조합49·줄바꿈 없음15·원문 스크롤23)+폰트 선택18입니다. 독립 문서·입력59건씩은 Markdown34건·줄바꿈 없음8건·범위 스크롤17건이며 기존 샘플146건씩도 통과했습니다. 새 Windows 컨트롤226건·클립보드35건·소유 샘플 종료0도 확인했습니다. 네이티브에서 실제 위치·세 좌표계·가까운 위치·같은 요청·잘못된 요청·무이동과 선택·포커스 유지를 검사하고, CPU에서7가지 배치를 검사했습니다. 근거는 `artifacts/linux-accessibility-2026-11-14/text-range-scroll-verification.json`입니다. 계약: https://raw.githubusercontent.com/GNOME/at-spi2-core/main/xml/Text.xml 및 https://gnome.pages.gitlab.gnome.org/at-spi2-core/libatspi/enum.ScrollType.html.


### 보이는 영역의 원문 범위 조회

`UiAccessibilityTextLayout.GetBoundedRanges(bounds, xClip, yClip)`는 원본 Content에서 시작과 끝 문자 위치를 반환합니다. 문자 위치는 유니코드 스칼라 기준이며 끝 위치는 포함하지 않습니다. 영역은 클라이언트 좌표를 사용하고 각 축은 None·Minimum·Maximum·Both를 받습니다. 글자 셀은 조회 영역과 현재 그리기 영역 모두에 걸쳐 있어야 합니다. Minimum·Maximum은 지정한 가장자리를 넘는 셀을 제외하고 Both는 셀 전체가 들어와야 포함합니다. 경계에 닿기만 하는 셀은 제외합니다. 화면의 줄과 그려지지 않는 원문 구간마다 범위를 나눕니다. 숨긴 Markdown 기호·너비가 없는 줄 구분자·화면 밖 셀은 포함하지 않으며 결합 문자는 같은 셀에 속한 확정 원문의 문자 위치를 함께 유지합니다. 이 API는 원문 좌표를 조회하며 렌더링 결과만으로 문서를 다시 구성하지 않습니다. 원문·커서·선택·배치 캐시를 유지하고 조회 자체가 렌더링을 요청하지 않습니다.

AT-SPI Text.GetBoundedRanges는 정확한 원문과 문자 위치를 `a(iisv)`로 반환하며 화면·창·부모 좌표의 공통 변환을 사용합니다. 잘못된 크기·경계 규칙·좌표계는 명시적인 프로토콜 오류로 거부하고 비어 있거나 겹치지 않는 영역은 빈 배열을 반환합니다. 독립 libatspi 클라이언트에서 네이티브 GArray를 읽고 해제하는 경로도 확인했습니다. GNOME 계약: https://raw.githubusercontent.com/GNOME/at-spi2-core/main/xml/Text.xml.

현재 .NET11/Linux 검사271건은 앱·세션42건+경계·배치211건+폰트 선택18건입니다. 새 영역 조회 검사는 CPU27건과 실행 방식별 독립 네이티브23건입니다. 문서·입력82건씩과 기존 샘플146건씩, 새 Windows 컨트롤226건·전용 클립보드35건·소유 샘플 자동 종료0을 확인했습니다. 근거는 `artifacts/linux-accessibility-2026-11-14/bounded-ranges-verification.json`입니다. 초기 테스트가 글자 일부가 자연스럽게 잘릴 것으로 가정해 실패했으므로 실제 글자 중간에서 그리기 영역을 자르는 테스트로 바꿨습니다. 이 테스트로 화면 밖에 있는 글자의 보이지 않는 부분까지 조회되던 문제를 재현했고 교차 조건을 수정해 같은 검사를 통과했습니다. 실제 화면 읽기·IME·이미지 의미·가상 문서·상위 컨테이너 범위 이동·실기기·ARM64·고정 RC3 NativeAOT는 남아 있습니다.


### 한 줄 입력의 원문 범위 이동

`UiAccessibilityTree.RequestTextScroll`과 AT-SPI ScrollSubstringTo·ScrollSubstringToPoint가 한 줄 입력도 지원합니다. 내장 입력 위젯의 공통 편집 경로에서 그리기 전에 원문 범위를 가로로 이동하며 실제 내용 너비 안에서 제한합니다. 원문·커서·선택·포커스를 바꾸지 않습니다. 배치 값7개는 공통 계약을 사용하되 한 줄 입력에는 세로 스크롤이 없으므로 세로 배치와 지점의 Y 이동은 가능한 범위로 제한합니다. 지점은 현재 보이는 로컬 텍스트 영역 안에 있어야 합니다. 상위 컨테이너의 이동도 아래 절과 같이 연결합니다.

요청이 성공하면 원문·커서·선택 기준점·너비·폰트 측정 조건·활성 상태·적용된 스크롤 위치가 같은 동안 화면 위치를 유지합니다. 키보드·포인터 조작, 원문·배치·포커스 변경이나 명시적인 접근성 포커스·선택 요청이 있으면 다시 커서를 보이게 합니다. 같은 커서를 명시적으로 요청해도 적용합니다. 포커스가 있는 입력에서 커서가 깜박이며 다시 그려져도 기존 커서 위치로 스크롤을 되돌리지 않습니다. 조합 중에는 Busy로 거부합니다. 비활성 입력의 원문이 외부에서 짧아졌을 때 남던 스크롤도 그리기 전에 제한하며 짧은 원문에 스크롤 범위를 만들지 않습니다. 유지 상태는 기존 접근성 항목이 소유하며 전역 입력 캐시를 추가하지 않습니다.

현재 .NET11/Linux 검사310건은 앱·세션42건+경계·배치250건+폰트 선택18건입니다. 새 한 줄 입력 CPU38건과 제어된 조합 거부1건이 통과했습니다. 독립 네이티브21건씩을 추가해 문서·입력103건씩이며 기존146건씩도 통과했습니다. 네이티브에서 모든 배치·세 좌표계·포커스 유지·실제 X11 포커스와 Home 이동·650ms의 포커스 유지 관찰과 다른 입력의 동작으로 확인한 프레임 게시·같은 커서의 명시적 요청·끝 위치·원문 단축을 검사했습니다. 새 Windows 공통 컨트롤227건·전용 클립보드35건·소유 샘플 자동 종료0도 확인했습니다. 기존 공통 좌표 검사는 스크롤할 수 없는 짧은 입력을 수동 이동하던 테스트였으므로 실제 넘치는 내용을 사용하도록 바꾸고, 원문이 짧아지면 위치가 제한되는 검사도 추가했습니다. 근거는 `artifacts/linux-accessibility-2026-11-14/single-line-scroll-verification.json`입니다. 실제 화면 읽기·IME·기존 조합 경로의 좌표·전체 문서 의미·실기기·ARM64·고정 RC3 NativeAOT는 남아 있습니다.


### 상위 컨테이너까지 이어지는 원문 범위 이동

입력 내부의 스크롤을 적용한 뒤 기존 ScrollItem 계획을 재사용해 안쪽 컨테이너부터 바깥쪽으로 이동합니다. 화면 밖 텍스트도 게시된 상위 스크롤 영역이 있으면 원문을 확인한 후 요청을 받을 수 있습니다. 현재 원문 좌표와 입력의 전체 텍스트 영역으로 보일 부분을 계산합니다. 상위 위치는 다음 렌더에서 적용하며 동기 요청은 이동한 좌표가 게시된 뒤 완료합니다. 검사한 중첩 한 줄·여러 줄·Markdown 경로에서 배치7개·지점 지정·문서 끝의 빈 범위를 지원합니다. 지점을 지정하면 입력 내부를 먼저 앞쪽에 맞춰 상위 이동에 필요한 여유를 확보합니다. 지점은 가장 바깥의 스크롤 가능한 영역 안에 있어야 하며 스크롤하지 않는 상위 영역은 그 범위를 줄일 수 있지만 넓히지는 않습니다.

계획할 때마다 현재 프레임의 대상·런타임 식별자·활성화 여부·동일한 Content·조합 상태·지점 범위를 확인합니다. 원문·커서·선택 기준점·포커스는 바꾸지 않습니다. 단계 사이에 원문이 바뀌면 실패하고 대상이 사라지면 Unavailable로 완료합니다. 기존 요청 제한2초를 유지하며 취소된 계획의 상위 위치를 나중에 적용하지 않습니다. 다만 이후 단계가 실패했을 때 앞서 적용한 로컬 위치나 이미 게시한 위치를 되돌리는 트랜잭션은 제공하지 않습니다. wait=false는 여전히 대기열 접수만 의미합니다. 팝업 소유 경계와 기존 계획 시도 한도를 유지하며 RTL·고정 높이 가상 목록은 아래 절에서 확인하며 실제 화면 읽기는 추가 검증이 필요합니다.

현재 .NET11/Linux 검사401건은 앱·세션46건+경계·배치337건+폰트 선택18건입니다. 새 상위 이동 CPU87건은 입력3종·배치7개·게시 전 대기·원문 위치 조회·선택과 포커스 유지·지점·창 안이지만 소유 영역 밖인 지점의 거부·단계 사이의 원문 변경·실제 제한 시간과 취소·대상 제거·문서 끝 좌표를 검사합니다. 별도 소유 창에서 실행 방식별 독립 네이티브61건씩, 기존 문서·입력103건씩과 일반146건씩도 통과했습니다. 새 Windows 공통227건·ScrollView와 모션96건·넘침26건의 CPU 검사, 전용 클립보드35건·제품 샘플 자동 종료0을 확인했습니다. 수명 검사용 위젯3개는 private protected 정리 메서드 대신 파생 형식에 제공되는 OnDispose 콜백에서 해제를 관찰하도록 맞췄습니다. 해당 Windows 네이티브 수명 호스트는 컴파일만 확인했으며 실행하지 않았습니다. 근거는 `artifacts/linux-accessibility-2026-11-14/ancestor-text-scroll-verification.json`입니다.


### RTL 배치와 가상 목록의 원문 범위 이동

바깥·안쪽 ScrollView의 RightToLeft 옵션을 조합한4가지 배치에서 한 줄·여러 줄·Markdown 입력을 검사했습니다(CPU348건). 왼쪽 스크롤바 여백과 논리·물리 가로 좌표도 포함합니다. 별도 제품 창에서 양쪽이 RTL인 경우를 실행 방식별 독립 네이티브61건씩 확인했습니다.

고정 높이의10억 항목 가상 목록에서는123456789번 항목 부근의 실제 생성된 행과 미리 생성한 주변 행을 검사합니다. 세로 위치는300억 픽셀이 넘는 소수점 포함 double 값입니다. CPU66건으로 행의 실제 할당, 모든 배치와 지점, 행 식별자 유지, 원문 위치 조회, 원문·커서·포커스 유지, 같은 범위의 정확한 위치 유지, 문서 끝과 소유자 해제를 확인했습니다. 전체 모델을 열거하지 않고 생성한 행 수를 제한합니다. 독립 네이티브 검사도61건씩 통과했습니다. 아직 생성되지 않은 텍스트는 기존 모델 검색·항목 생성 API를 먼저 사용해야 하며, 가변 높이와 팝업 조합은 추가 검증이 필요합니다.

상위 가상 목록이 세로로만 움직일 때 지점 X=60으로 요청한 글자가 X=19에 놓이던 문제를 재현했습니다. 이제 가로·세로의 이동 능력을 각각 확인합니다. 상위 영역이 움직일 수 없는 축은 입력 내부에서 맞추고 나머지 축을 상위 계획에 맡깁니다. 같은 검사에서(60,60)에 도달하며 세로 위치의 double 정밀도도 유지합니다. 고정 행 안에서 즉시 모드 컨트롤을 사용할 때는 constrainLayout=true를 지정해 실제 할당을 따릅니다. 그렇지 않으면 명시한 너비가 스크롤바를 뺀 행 너비를 넘을 수 있으므로 이동 검사 전에 할당부터 확인합니다.

현재 .NET11/Linux 검사736건은 앱·세션54건+경계·배치664건+폰트 선택18건입니다. 실행 방식별 네이티브 일반146건·문서와 입력103건·기본 상위 이동61건·RTL 상위 이동61건·가상 행61건씩이 통과했습니다. 새 Windows 공통227건·ScrollView와 모션96건·가상 목록75건·넘침26건의 CPU 검사, 전용 클립보드35건·제품 샘플 자동 종료0도 확인했습니다. 근거는 `artifacts/linux-accessibility-2026-11-14/rtl-virtual-text-scroll-verification.json`입니다. 실행 파일의 text-scroll-virtual 프로필로 Linux 실제 폰트를 쓰는 CPU66건만 따로 검사할 수 있어 원인을 좁힐 때 창이나 네이티브 검사를 반복하지 않아도 됩니다. 실제 화면 읽기·IME·기존 조합 경로·가변 높이·전체 문서 의미·실기기·ARM64·고정 RC3 NativeAOT는 남아 있습니다.

### 기존 여러 줄 입력의 IME 조합

Wrap=false인 기존 편집기는 마우스를 놓고 입력을 쉬는 동안에도 포커스를 유지합니다. 한 줄 입력과 같은 포커스 관찰 방식을 사용합니다. IME 조합 중에는 기존 공통 조합 배치로 글자·밑줄·커서·입력기 위치·스크롤·접근성 원문 좌표를 함께 계산합니다. 조합을 취소하거나 확정하면 일반 줄 배치로 돌아가며, 확정 원문과 이미 게시한 변경 불가 스냅샷을 유지합니다. 소유자가 있는 입력과 없는 입력을 모두 검사합니다. 사용자 정의 조합 스냅샷은 그리기에 사용한 배치 맵을 제공해야 좌표를 게시할 수 있습니다.

제어된 조합 검사는 한 줄·줄바꿈·Markdown·기존 두 가지 입력에서104건이 통과했습니다. 실제 포인터 포커스, 한글·이모지·결합 문자, 개행, 긴 조합의 줄바꿈 또는 가로 스크롤, 취소, 한 번의 확정, 바깥 클릭의 포커스 해제를 확인했습니다. 조합을 주입한 검사이며 실제 운영체제 입력기를 검증한 것은 아닙니다. 현재 .NET11/Linux790건은 앱·세션54건+경계·배치718건+폰트 선택18건입니다. 독립 네이티브 검사는 실행 방식마다 일반146건·문서와 입력103건·기본/RTL/가상 상위 이동61건씩 통과했습니다. 새 Windows 공통227건·클립보드35건·소유 제품 샘플 자동 종료0도 확인했습니다. 근거는 `artifacts/linux-accessibility-2026-11-14/legacy-ime-layout-verification.json`입니다.

### 가변 높이 가상 목록의 원문 범위 이동

기존 누적 높이 API로10억 행의 높이를256.25/384.75/192.5픽셀로 반복하고,320억 픽셀이 넘는 소수점 포함 double 스크롤 위치를 검사했습니다. 공통 가상 텍스트 검사78건으로 한 줄·여러 줄·Markdown 입력의 모든 배치, 정확한 지점 조회, 문서 끝, 행 식별자·원문·선택·포커스 유지와 생성한 행 수 제한을 확인했습니다. 앞선 행이64.5픽셀 커져도 같은 행의 화면 위치를 정확히 유지합니다. 요청 접수 뒤32.25픽셀 더 커져도 새 누적 높이로 계산한 결과를 게시합니다. 실제 가변 높이 행에 할당한 영역을 사용하며, 이번 검증에서 엔진 API나 렌더링 동작은 변경하지 않았습니다.

전용 네이티브 검사는 명시적·등록된 실행 방식으로 소유 X11/Vulkan 창을 하나씩 열고 독립 원문 이동61건씩과 실행·해제4건을 확인했습니다. 앞선 두 전체 실행에서는 실패했습니다. 기존 일반 네이티브 클라이언트가146건을 기록한 뒤 종료 코드134로 끝났고, 두 번째 전체 실행에서도 같은 종료가 발생했습니다. 범위를 좁힌 일반 클라이언트 검사에서는 GrabFocus가 거부되었습니다. GDB 진단1회도 포커스 거부를 확인했으며 앞선 결과 기록 후 종료 원인은 아직 밝혀지지 않았습니다. 실패 기록을 보존하고 전체 성공으로 집계하지 않습니다. 이전 전체790건·Windows227건·클립보드35건·샘플 종료0은 이전 시점의 근거로 유지합니다. 현재 집중 검증의 근거는 `artifacts/linux-accessibility-2026-11-14/variable-height-text-scroll-verification.json`입니다. 실제 화면 읽기·입력기, 팝업 통합, 하드웨어·ARM64·고정 RC3 NativeAOT는 남아 있습니다.

후속 검증에서는 일반 네이티브 클라이언트가 이벤트 범위와 atspi_exit의 정리를 끝낸 뒤에만 결과를 기록하도록 수정했습니다. 좁힌 검사 두 번의 정상 종료를 확인했습니다. 최종 전체 실행도 종료 코드0이며 Linux872건은 앱·세션58건+경계·배치796건+폰트 선택18건입니다. 실행 방식마다 일반 네이티브146건·문서와 입력103건·기본/RTL/고정 높이 가상 행/가변 높이 가상 행의 원문 이동61건씩 통과했습니다. 이전 포커스 거부와 결과 기록 후 종료134는 보존합니다. 이번 통과만으로 그 원인을 확정하거나 제품 포커스 오류를 고쳤다고 판단하지 않습니다. 임시 포커스·종료 추적과 지원되지 않은 콜백 해제 실험은 제거했습니다. 현재 근거는 `artifacts/linux-accessibility-2026-11-14/native-cleanup-record-verification.json`입니다. 제품 바이너리는 같으며 Windows227건·클립보드35건·샘플 종료0은 다시 실행하지 않은 이전 근거입니다.

### 떠 있는 팝업의 원문 이동 경계

떠 있는 팝업의 원문을 이동할 때 논리적 소유자의 스크롤이(180,180)에서(196,279)로 바뀌고 팝업까지 닫혀 Unavailable로 끝나는 결함을 재현했습니다. 팝업 표면에 독립적인 콘텐츠 경계를 기록해 공통 이동 계획이 트리거의 바깥 스크롤로 넘어가지 않도록 수정했습니다. 팝업 안의 실제 뷰포트는 계속 스크롤됩니다. 접근성의 논리적 부모 관계는 유지하며 공개 API를 추가하지 않았습니다.

실제 폰트를 사용하는 CPU138건으로 일반·포커스 제한 팝업의 한 줄·여러 줄·줄바꿈 없는 Markdown 입력을 검사했습니다. 모든 배치, 정확한 지점과 원문 위치 조회, 팝업·소유자 위치 유지, 원문·커서·선택·포커스 유지, 팝업 바깥 지점의 거부, 대기 중 닫은 요청의 해제를 확인했습니다. 닫기 검사 전 팝업의 뷰포트를 초기화하고 실제 대기 상태를 확인하므로 이미 끝난 요청을 취소로 오인하지 않습니다. 같은 표면을 쓰는 실제 X11/Vulkan 창6개에서 세 입력과 두 실행 방식마다 독립 네이티브21건씩, 실행·해제12건도 통과했습니다. 네이티브 팝업의 실제 부모 패널에서 뷰포트 위치를 읽으며 기존 상위 이동 검사의 고정 좌표 기준은 유지합니다.

최종 .NET11/Linux1022건은 앱·세션70건+경계·배치934건+폰트 선택18건입니다. 독립 네이티브18개 실행이 통과했습니다. 팝업6개 외에도 일반146건·문서와 입력103건·기본/RTL/고정 높이/가변 높이 상위 이동61건씩이 유지됩니다. 새 Windows 공통227건·팝업240건의 CPU 검사, 클립보드35건과 소유 제품 샘플의2초 자동 종료 코드0도 확인했습니다. 근거는 `artifacts/linux-accessibility-2026-11-14/popup-text-scroll-verification.json`입니다. 이전 일반 클라이언트의 간헐적 포커스 거부·종료134 원인은 미확정이며 실제 화면 읽기·입력기, 하드웨어·ARM64, 전체 문서 의미와 고정 RC3 NativeAOT 검증은 남아 있습니다.

### 이미지 의미와 키보드 선택

렌더링한 이미지에 일반 대체 텍스트와 실제로 보이는 영역의 좌표를 가진 비대화형 Image 자식 노드를 게시합니다. 화면 밖의 이미지도 조회할 수 있으며 공통 항목 이동으로 가져올 수 있습니다. 사용할 수 있는 빈 대체 텍스트 이미지는 장식으로 처리해 생략하고, 사용할 수 없는 이미지는 화면의 안내 문구를 이름으로 사용합니다. 뷰에서 이미지로 만든 링크의 부모는 실제 Link 노드입니다. 편집 중인 블록은 정확한 원문을 보여주고 그 블록의 이미지 노드는 잠시 제거했다가 렌더링으로 돌아오면 다시 게시합니다. 좌표는 그리기와 같은 투영 이미지·배치 함수를 사용하며 바깥 구분선 문자가 없는 표 첫 칸의 안쪽 여백도 유지합니다. 이미지 주소 해석과 Markdown 분석 결과는 계속 재사용합니다.

키보드가 숨겨진 이미지 주소 안으로 들어가 한 번의 Shift+Right가 원문 위치8에서 이미지 끝24로 가지 않고9에 머무는 결함을 재현했습니다. 렌더링한 이미지는 전체 원문 경계를 한 번에 지나도록 좌우 이동과 위치 조회를 수정했고, 보이지 않는 문법 기호도 건너뜁니다. 편집 중인 원문 블록에서는 기존 문자 이동을 유지합니다. 선택 색을 불투명 이미지 위에 표시해 Shift 선택이 보이며, 선택을 해제하면 원래 그림으로 돌아옵니다. 이미지의 행은 줄바꿈 경계의 커서 방향 상태와 분리해 계산합니다. CPU84건으로 뷰·편집기, 일반·링크 이미지, 큰 이미지의 좌우·상하 이동과 Shift 조합을 확인했습니다. 실제 X11 Shift+Right·Left 입력과 소유 창의 픽셀 캡처로 이미지 전체 선택, 선택 색, 선택 해제와 원문 유지를 검증했습니다. 이미지 크기 조절 같은 객체 편집 기능을 추가한 것은 아닙니다.

기본 이미지 의미는 CPU31건과 실행 방식별 독립 네이티브21건으로 역할·이름·링크 부모·좌표·위치 조회·원문 이동·실제 키보드와 픽셀 변화를 검사했습니다. 최종 관리형 .NET11/Linux1141건은 앱·세션74건+경계·배치1049건+폰트 선택18건이며 독립 네이티브20개 실행이 통과했습니다. 새 Windows 공통227건·복합 및 Markdown415건의 CPU 검사, 전용 클립보드35건·소유 제품 샘플의2초 자동 종료 코드0도 통과했습니다. 네이티브 Image 인터페이스와 실제 화면 읽기·입력기, 원격 이미지 정책, 하드웨어·ARM64·고정 RC3 NativeAOT 검증은 남아 있습니다. 근거는 `artifacts/linux-accessibility-2026-11-14/markdown-image-interaction-verification.json`입니다.

## 자체 SVG·네이티브 이미지 검증 — 2026-11-14

SVG 백엔드는 자체 기하·래스터 처리와 Windows의 DirectWrite·WIC, Linux의 HarfBuzz·GdkPixbuf로 바뀌었습니다. Windows에는 Skia·HarfBuzz·외부 이미지 코덱 DLL을 배포하지 않습니다. Linux HarfBuzz 네이티브 자산은 대상 플랫폼에만 포함합니다. 기존 자원·재로드·LRU·확대·회전·폰트 보관 계약은 유지합니다. [플랫폼별 SVG 렌더링](svg-platform-rendering.ko.md)과 `artifacts/svg-managed-2026-11-14/verification.json`을 참고하세요. 앞의 Skia 검증 기록은 교체 전 결과입니다.

SVG → 브라우저 비교에는 원본 해시가 고정된 복잡한 실제 SVG와 Chromium 기준이 있습니다. [비교 계약·실행 안내](svg-browser-validation.ko.md)를 따르며 전체 픽셀 일치는 미완료입니다. 페인트 참조·선 생성·coverage·샘플링·합성·필터를 분리했습니다. `UiTextureUpdate.IsColorImage`는 직선 알파 RGBA8 입력을 표시하며, 공통 Core 변환과 Vulkan의 premultiplied 보간으로 PNG·SVG의 투명색 번짐을 방지합니다. 폰트 coverage는 변환하지 않습니다. 셰이더의 이미지 모드는0x40000000, coverage 모드는0x80000000이며 descriptor index에는 하위30비트를 사용합니다.

일반 SVG 경로에는 관측한 GPU 브라우저의4개 샘플·1/256픽셀 격자와 `SvgCubicTessellation`의 Wang 균일 분할을 적용합니다. 도형·글자·이미지의 `visibility` 상속과 자식의 재표시를 보관 장면에 컴파일하며, 숨긴 글자의 배치 간격과 숨긴 도형의 그룹 경계 기하는 유지합니다. 옵저버 문서 기능의 흰 마름모에서 숨김 상속을 확인합니다. 작은 기본 예제의 픽셀 일치와 전체 복잡한 원본의 미완료 상태를 구분합니다.

`SvgSamplePixel`과 `SvgRasterSurface.Samples`는 서로 다른 샘플 색을 합성까지 유지합니다. 기하 클립·그룹 불투명도와 이미지 마스크·공간 필터의 처리 경계를 구분하며 alpha/luminance 마스크를 지원합니다. 새 공개 API 없이 기존 장면·래스터 캐시를 사용합니다.

`SvgOrderedDither`는8×8 Morton 임계값을 사용하고 `PaintServerOpacity`는 패턴·그라데이션의 불투명도를 반올림된8비트 값으로 변환합니다. 패턴 예제3개는 전체 RGBA가 일치하지만 그라데이션·단색 일부의1바이트 차이와 전체 원본 일치는 미완료입니다.

`SvgStroke`는 뾰족한 연결의 내부 영역을 유지하고 원점을 옮긴 다각형의 방향을 double 정밀도로 계산합니다. 고정 해시의 독립 브라우저 예제8개가 연결과 큰 원점의 전체 RGBA를 검증합니다. 곡선 획 분할과 전체 원본의 일치는 별도의 미완료 항목입니다.

`SvgStrokeCurve`·`SvgStrokePath`·`SvgStrokeTessellation`은 보관 제어점, 점선 길이 측정, 매개변수·회전 결합 획 확장을 구분합니다. 브라우저 전체 RGBA 예제20개가 곡선·점선·첨점·연결·끝 모양·변환을 검증하며 Red 원본의 분리한 획2개도 정확히 일치합니다. 둥근 끝 직선의 analytic coverage와 전체 원본 일치는 미완료입니다.

`SvgViewport` 클립은 각 그리기·레이어 합성에 적용하며 analytic 사각형은 기하 교차를 사용합니다. 브라우저 전체 RGBA 예제16개가 소수 픽셀 경계를 검증합니다. Red 원본의 분리한 채우기·획 결합2개도 일치하며 폐기된 마지막 문서 클립 처리는 제거했습니다.

`UiAccessibilityNode.ImageInfo`는 `UiAccessibilityImageInfo`를 선택적으로 게시합니다. 화면에서 잘리기 전의 전체 이미지 영역, 설명, 명시한 경우의 언어를 변경하지 않는 값으로 보관합니다. 기존 `Node.Bounds`는 계속 잘린 영역입니다. 실제로 표시하는 Markdown 이미지는 이 정보를 제공하며, 장식용 이미지는 제외하고 로드 실패 안내문은 네이티브 이미지로 등록하지 않습니다. Linux는 이 정보가 있을 때만 `org.a11y.atspi.Image`를 제공합니다. 설명·언어·크기와 화면·창·부모 기준 위치·영역을 읽을 수 있으며, 언어가 없으면 빈 프로토콜 문자열을 반환합니다. 크기는 실제 렌더링 영역을 감싸는 정수 픽셀이므로 원본 비트맵 크기와 다를 수 있습니다. 원문 범위로 드러내기와 키보드 선택은 유지합니다. 실제 화면 읽기 검증은 남아 있습니다.

2026-11-14 SVG 원·타원·둥근 사각형·점선이 아닌 둥근 끝 직선은 SvgAnalyticRRect의 보간 AA·페인트 좌표·서브픽셀 폭 조정을 공유합니다. 브라우저 Canvas2D는 고정 리비전의 GraphiteDawnD3D11로 식별했습니다. 독립 Linux 입력19개가1/19→11/19 정확 일치하며 Windows 출력도 같습니다. 통과 예제11개로 남은8개 실패나 전체 원본0/2를 면제하지 않습니다. [브라우저 검증](svg-browser-validation.ko.md#공통-analytic-도형-coverage)을 참조하세요.


2026-11-14 [SVG 위젯 배치](svg-widget-layout.ko.md): 확대·회전 경계를 측정과 가로·세로 흐름에 반영합니다. Margin이 네 방향 바깥 공간을 확보하며 Offset은 부호 있는 여백의 호환 축약입니다. 브라우저 비교도 상단 조절 값을 공유하고 배치 크기를 표시합니다. 왼쪽 독립 기준은 고정합니다.


[공통 위젯 변환 계약](widget-transform-contract.ko.md)은 전체 위젯·상대 회전 중심·역변환 입력·접근성·팝업 앵커의 확대 범위를 유지합니다. 공통 `UiWidget.Margin`은 구현되어 SVG·버튼·체크박스·슬라이더가 상속하며 옵저버 `위젯 배치` 탭에서 확인합니다. 공통 회전은 진행 중입니다. SVG 콘텐츠 모서리는 기존 쌍 이미지 레코드를 재사용하며 CPU 모서리 래스터나 추가 레코드 stride를 만들지 않습니다.


아핀 클립의 변경 스냅샷은 모양과 뒤따르는 변환 행렬을 함께 비교합니다. 행렬만 바뀌어도 새로 드러나거나 가려진 픽셀을 갱신하며, 획득 이미지가 모두 안정되면 draw가 다시 0이 됩니다. 실제 MSAA1/4 검사는 직사각형·둥근 모서리·원형 클립과 부모/자식 역회전 변경을 포함합니다. 투영된 위젯 장면은 기존의 보수적인 전체 갱신을 유지하며, 임의 3D 장면의 부분 갱신을 보장하지 않습니다.

공통 변환 팝업: **위젯 배치**에서 회전·확대·여백을 조절한 뒤 **변환 팝업**, **변환 메뉴**, **변환 도구 설명**을 사용하세요. 트리거는 변환되지만 팝업은 화면 좌표로 바로 서서 열립니다. 부모의 회전·클립·포인터·접근성·IME 좌표를 공통 범위에서 분리하고, 닫으면 이전 위젯 좌표를 복원합니다. 지연 Tooltip도 화면 앵커를 보관하며 마우스가 창 밖에 있어도 호출자 표시 상태를 유지합니다. 선언형 Popover·메뉴·Tooltip과 기존 BeginPopup·BeginTooltip·모달·Combo·메뉴 경로를 검증했습니다. 임의 3D 카메라의 모든 조합과 화면 읽기 적합성은 남아 있습니다.

위젯 배치 → 기존 팝업 API에서 공통 회전·확대를 적용한 채 열기·실행·닫기를 확인합니다. 기존 Combo의 세 오버로드는 공통 화면 앵커 계산을 사용합니다. 팝업 너비는 원래 본문 너비와 변환된 트리거 너비 중 큰 값이며, 부모의 회전 클립을 벗어나 화면 안에 배치합니다. 화면 팝업은 부모의 벡터 텍스트 강제를 상속하지 않습니다.

화면 팝업은 진입 시점의 부모 카메라 레이어까지 텍스트 모드에서 분리합니다. 팝업 안에서 새로 연 카메라 레이어는 그대로 적용됩니다. 보관 레이어는 실제 벡터/화면 텍스트 모드를 함께 기억하므로 StaticCache와 동일 ContentVersion에서도 모드가 바뀌면 다시 구성하고, 이후에는 재사용합니다. MSAA1/4에서 부모 이동·회전, 중첩 2D 레이어, 변환 위젯, 모드 전환·복원 픽셀을 비교했습니다. 원래 고정 RC3 NativeAOT·전체 3D 입력·화면 읽기 검증과 구분합니다.


### SVG clip storage inspection (2026-11-15)

SVG → 클립·경계에서 독립 Chromium/GPU9쌍을 확대·회전하며 비교합니다. CPU/GPU9/9, 기존 불투명도 그룹9/11이며 전체 브라우저 일치는 미완료입니다. 장면은 재사용하고 클립 최적화는 원래 객체 경계를 유지합니다. 실행·검증은 [SVG GPU 카메라 가이드](svg-gpu-camera.ko.md)를 따릅니다.


### Opacity layer raster origin (2026-11-15)

SVG → 레이어·테두리에 독립 기준5쌍을 추가했습니다. 저장 레이어의 디더링 픽셀 원점을 페인트 좌표와 분리합니다. 기존 그룹10/11·레이어 원본5/5이며 추가 테두리 범위는3/5입니다. GPU 확대는 보관된 경계와 장면을 재사용합니다.


### Analytic rectangle stroke and visible reference parity (2026-11-15)

클립·테두리 사각 인식을 공통화하고 axis-aligned miter 테두리에 path flag64·내곽1레코드를 사용합니다. 잘못된 경계·개수·조합은 GPU 제출 전에 거부합니다. 폭0 테두리는 빈 장면과 같으며 분할 작업을 만들지 않습니다. 옵저버의 테두리 범위5종과 기준 픽셀 배율·실제 첫 이미지 쌍 비교를 추가했습니다. 얇은 선284픽셀·비균등 배율 GPU11픽셀은 미완료이며 전체 SVG 완료로 보지 않습니다.

### SVG 카메라 경계 선별과 진단

공통 Vulkan 벡터 셰이더는 그리기 경계 밖의 페인트 계산을 생략합니다. 구조 명령·한 픽셀 AA 여유·원래 순서는 유지하며 CPU 가시 목록을 만들지 않습니다. `gpu-svg-camera-bench 3`은 원본2종·배율5종의16워밍업/32측정 프레임에서 전체 출력 해시와 CPU/GPU 시간을 분리합니다.3회 교대 결과 GPU9/10조건에서4.7~14.3% 감소, Red400%·전체 동기 지연은 미확정입니다. [실행·측정 가이드](svg-gpu-camera.ko.md)를 참고하세요.

### 완료된 Vulkan 작업 버퍼 재사용과 Linux UI 계측

GPU 펜스와q+N반환기한 뒤에만 업로드/장치 작업버퍼를 재사용하며 완료 캐시는64MiB/256개로 제한합니다. 불변 장면 소유권과 기존 메모리 계약은 유지합니다. `gpu-vector-cache`는 슬롯1/3의 조기 재사용 금지·고정 크기에서 추가 할당 중단·원시RGBA 유지·펜스 후 해제를 검증합니다. `gpu-svg-camera-present-bench`는 측정 중 CPU 읽기를 제외합니다. Linux 옵저버의 `--verify-slider-performance`는 같은 일반 슬라이더를 단독/전체/진단고정 조건으로 측정합니다. 상단 진단행은FPS 표본주기로 갱신해 자체 관측 비용을 줄입니다. 최초 진입과 실제 포인터 지연은 별도 검증 대상입니다.


### 공통 애니메이션 예약과 Linux 연속 입력

위젯 애니메이션은 `UiFloatTransition`의 기본60Hz 간격으로 다음 시각을 예약하며 실제 입력·더 이른 반복 요청은 즉시 처리합니다. 명시적 연속 렌더링은 유지합니다. Linux120Hz 연속 입력3회 비교에서 Fluent CPU58.62%·구성74.02%·입력 중앙값81.96% 감소, 단독 슬라이더 CPU12.85% 증가는 별도로 기록했습니다. 검증한 고정 RC3 NativeAOT와 전체33페이지의 [실행 가이드](../artifacts/linux-animation-pacing-2026-11-15/README.ko.md)를 사용하세요. 물리 표시 체감·전체 제품 수용은 미완료입니다.


## Linux 표시 재사용과 기본 NativeAOT 실행

공통 렌더러는 마지막으로 실제 표시한 장면과 네이티브 무효화 순서를 함께 확인해, 같은 화면의 새 CPU 작업을 이미지 획득·제출·표시 없이 승인합니다. `UiRendererFrameMetrics.PresentationReused`가 이를 구분하며 실제 Present 순번·시각은 증가하지 않습니다. 노출·재표시·화면 변경은 실제 표시를 수행하고 콜백·GPU 애니메이션·변경된 페인트 그룹은 생략하지 않습니다. 현재 제품 호스트 연결은 Linux X11이며 다른 호스트는 기존 경로를 유지합니다. `DUXEL_VK_PRESENT_REUSE=0`은 동일 실행물 비교용으로 이 승인 경로만 끕니다.

고정 RC3 NativeAOT3쌍 교대 비교에서 단독 슬라이더 CPU는52.97%→24.90%(한 코어=100%), 감소53.00%였습니다. Fluent 전체 화면과 입력 지연은 실행별 범위가 겹쳐 이번 변경의 속도 개선으로 확정하지 않습니다. 원본 입력 확인 시간 초과는 기존 실행본에서도 발생했으며 미해결로 보존합니다. 독립 GPU588쌍·3704검사, 실제 노출/재표시21검사,33개 표시 시나리오·384입력·정지 누름9조건0회 구성·드래그·SpinButton 반복은 통과했습니다. 물리 표시 체감과 전체 제품 수용은 별도입니다.

`run-gallery-linux.ps1`은 `PublishedExecutable`을 생략하면 이제 WSL에서 고정 SDK로 NativeAOT를 게시합니다. `-LinuxDotnet`으로 정확한 설치 경로를 선택하고 `-Managed`는 명시적 관리형 비교에만 사용합니다. 누락된 SDK·버전 불일치는 오류로 알리며 관리형으로 대체하지 않습니다. 즉시 실행할 검증본과 해시·명령은 [표시 재사용 가이드](../artifacts/linux-present-path-2026-11-15/README.ko.md)를 따릅니다.


### Dialog 동기 호출 출처와 접근성 포커스

`UiButton` 동기 활성화 콜백의 `Dialog.Show()`는 실제 호출 버튼을 기억합니다. 일반 접근성 Invoke의 포커스를 바꾸지 않고 닫을 때 해당 버튼으로 복귀하며, 콜백 밖의 프로그램 호출은 기존 이전 포커스 계약을 유지합니다. 보류된 출처는 UI 컨텍스트를 보관하지 않는 토큰을 사용합니다. CPU107·Windows UIA13/호스트2·Linux AT-SPI14/호스트3과 레지스트리 퇴장 검사 통과; Dialog11/12이며 고정 RC3 Windows AOT·전체 통합은 남았습니다. [검증·범위](../artifacts/fluent-dialog-2026-11-15/focus-origin-verification.json).


### Linux 검사 결과와 열린 Dialog의 고정 NativeAOT

Linux.App.Tests의 익명 결과14곳과 관측 객체를 공통 타입·원천 생성 JSON으로 바꾸고 리플렉션 JSON을 껐습니다. `result-contract <새 결과 경로>`는 이름·대소문자·순서·값·서식을 검사하며 관리형/AOT25개 JSON이 같습니다. 원본 출력14곳·관측 객체1곳의 필드 목록도 보존합니다. 고정 RC3 NativeAOT의 실제 열린 Dialog는 독립 클라이언트14·호스트3과 레지스트리 퇴장을 통과했습니다. 전체 Linux 접근성 묶음은 재실행하지 않았고 Windows RC3 AOT·전체 통합·새 속도 개선율로 확대하지 않습니다. [재현 명령·범위](../artifacts/linux-probe-results-aot-2026-11-15/README.ko.md).

메시지 표시: 스크롤 가능한 페이지에서 **표시할 때 자동 공지**와 **차분한 공지로 고정**을 전환하고 공지 요청 수를 확인하세요. 실제 제목 관계와 표시·우선순위 변경에 따른 한 번의 공지를 지원하며, 재정렬·삭제는 공지를 반복하지 않습니다. 요청 수는 발화 완료 수가 아닙니다. MessageBar 12/13, 전체 적합성은 구현 중입니다. [API·범위](fluent2-messagebar.ko.md).

메시지 표시 → **오른쪽에서 왼쪽 배치**로 실제 슬롯과 액션 배치를 전환합니다. 공통 `UiWrapOptions.RightToLeft`는 자식의 X 위치를 반전하며 탐색·행 순서는 유지합니다. 임의 자식의 문단 양방향 조판을 보장하는 옵션은 아닙니다. 전체 MessageBar 12/13은 유지합니다.

MessageBar 현재 계약은 12/13입니다. 실제 제목 연결과 자동 공지를 Windows UIA 클라이언트 검사 8개·호스트 검사 1개, Linux 고정 RC3 NativeAOT AT-SPI 클라이언트 검사 9개·호스트 검사 3개로 확인했습니다. 전체 문단 RTL은 남았고 Windows NativeAOT는 별도 관문입니다. 기존 검증된 Linux 옵저버는 유지합니다.

메시지 표시의 여러 행·각진 모서리·방향·공지 옵션은 새 동적 메시지에도 동일하게 적용됩니다. 실제 조작 34개와 Windows 38·Linux 41·전달 실행기 38개 표시 시나리오를 확인했습니다. 현재 MessageBar 12/13, 전체 RTL·줄바꿈은 남았습니다.




Duxel 갤러리는 ./run-gallery.ps1 또는 기존 Linux 인수를 받는 ./run-gallery-linux.ps1로 실행합니다. 고정 헤더에서 밝음·어두움을 바꾸며 일곱 기능 그룹 안에 공식 상위 이름55개를 알파벳순으로 표시합니다. Fluent 아이콘과 미구현 배지16개를 제공하며 기존 예제 모델은 유지합니다. UiTabOption.Badge / UiTabBadge와 UiControlContext.PaintFluentBadge는 공통 Badge 경로를 사용합니다. UiImmediateContext.GetStyle(includeRequested: true)는 순서 있는 설정 입력에서 마지막 대기 테마를 읽으며 기본값은 현재 적용된 범위의 테마입니다. [현재 검증](gallery-navigation.ko.md)은 갤러리410개·Badge 배치73개·Rating 및 강제 색상238개·기존 계약704개, 일반 Windows 표시64시나리오와 Linux 호스트 빌드입니다. Linux 실제 실행·전체 구성요소 적합성은 별도로 남아 있습니다.


## Drawer와 좁은 화면의 탐색 메뉴

`Fluent.Drawer(title, content, options : new(...))`는 본문과 선택적인 하단 작업 버튼을 소유합니다. `Open`, `OpenChanged`, `Show()`, `Close()`로 열고 닫습니다. `UiDrawerOptions`는 화면 위에 겹치는 패널과 배치 공간을 차지하는 인라인 패널, 논리 방향 Start/End/Bottom, RTL, 기본 크기 320/592/940/전체, 크기 직접 지정, 모달·바깥 클릭 닫기, 고정 헤더·하단 작업과 본문 스크롤을 지원합니다. 인라인 패널에는 유한한 교차축 공간 또는 `InlineCrossExtent`가 필요합니다.

겹치는 패널은 보관한 내용을 확대하지 않고 이동시켜 열고 닫습니다. 애니메이션이 끝나면 추가 프레임을 요청하지 않습니다. 인라인 패널의 공간은 현재 즉시 바뀌며, 인라인 배치 애니메이션과 전체 플랫폼 적합성 검증은 남아 있습니다. `ScrollBody : false`는 갤러리 메뉴처럼 자체 스크롤이 있는 유한 높이 본문을 담을 때 사용합니다. 메뉴를 숨긴 호스트는 `RenderPage(..., navigationVisible : false)`로 호출하여 본문 Pane이 없는 Link를 참조하지 않게 합니다.

`./run-gallery.ps1` → **알림과 진행 상태 → Drawer**에서 두 패널을 확인하세요. 좁은 화면의 갤러리는 같은 그룹과 테마 토글을 탐색 패널에 표시합니다. 패널을 닫거나 테마를 바꿔도 입력 모델은 유지합니다. [공식 Fluent Drawer 지침](https://fluent2.microsoft.design/components/web/react/core/drawer/usage), [검증 기록](../artifacts/drawer-2026-11-17/verification.json).


2026-11-17 Drawer 호스팅 검증: 갤러리 564개 검사에 새 GPU·상호작용 검사 8개가 포함됩니다. 좁은 화면의 닫힌 메뉴, 다크·라이트 메뉴, 페이지 선택과 명시적 닫기, Drawer 예제의 두 표시 방식을 캡처했습니다. Windows 실행 검사는 열기·닫기·다시 열기·저장을 포함해 72개 표시 시나리오를 통과했습니다. 실제 접근성 명령을 실행하고, 패널 안의 보이는 컨트롤에 포커스가 있는지와 닫은 뒤 호출 버튼으로 복귀하는지 확인합니다. Linux 호스트 빌드는 통과했으며 실제 Linux 실행은 반복하지 않았습니다. 제품 모듈이 이전 Drawer 빌드와 바이트 단위로 같아 공통 회귀 검증 결과는 재사용합니다. 인라인 배치 애니메이션은 남아 있습니다. [검증 기록](../artifacts/drawer-hosting-2026-11-17/verification.json).


## 독립 예제와 동등한 소스

각 메뉴는 서로 다른 페이지를 유지합니다. 같은 페이지를 가리키는 메뉴가 생기면 시작 검사에서 실패합니다. 종합 화면을 공유하던 구성요소 21개를 독립 예제로 나누고, 기존 종합 화면은 **종합·회귀 예제** 그룹으로 옮겼습니다. 각 페이지 하단에는 선택·복사할 수 있는 읽기 전용 C# 소스를 표시합니다. 소스 영역은 본문과 별도로 가로·세로 스크롤합니다.

독립 예제의 소스는 실제 실행하는 팩터리 식을 `CallerArgumentExpression`으로 가져옵니다. 기존 복합 예제는 빌드할 때 포함한 실제 `.cs` 파일을 표시합니다. 실행할 때 저장소 파일을 읽지 않습니다. 복합 예제의 원본 파일은 갤러리 프로젝트의 보조 타입과 참조를 함께 사용합니다. 미구현 메뉴에는 실행 가능한 소스가 없음을 명시합니다.

`UiViewHost`는 호출자가 소유하는 `IUiView`를 유한한 배치 영역에 담습니다. 순서대로 그리는 화면의 각 위젯이 전체 영역을 상속하지 않게 하며 자식 클리핑·팝업 계층·중첩 창의 좌표를 구분합니다. 인라인 Drawer는 공통 `UiFloatTransition`으로 차지하는 공간과 보이는 영역을 함께 바꿉니다. 내용 크기는 유지하고 반대 방향으로 전환하면 현재 크기부터 이어갑니다. 닫히는 즉시 입력과 접근성 노드는 제거합니다. 처음부터 열린 인라인 패널은 바로 배치하고 이후 변경을 애니메이션으로 표시합니다. `MotionEnabled`가 false이면 즉시 반영합니다. [검증 기록](../artifacts/gallery-source-2026-11-17/verification.json).

본문 제목은 왼쪽 메뉴 이름과 같습니다. 제목과 설명은 가깝게 두고, 설명 아래에는 본문과 구분할 수 있도록 여백을 더 둡니다. 같은 예제의 설정과 상태, 미리보기를 모아서 보여 준 다음 소스를 표시합니다. 개요와 관찰 범위에는 소스가 없습니다.

## 갤러리 페이지 배치와 코드 표시

Windows 프로젝트·실행 파일은 `Duxel.Gallery`, Linux는 `Duxel.Gallery.Linux`입니다. 전체 메뉴의 제목과 설명은 공통 페이지 틀에서 한 번만 표시합니다. 자연 크기가 있는 예제는 스크롤 본문의 실제 너비로 측정하여 바로 다음에 소스를 배치합니다. Markdown은 현재 선택한 편집기·미리보기·중첩 미리보기 높이를 사용합니다. 배치 공간을 예약하는 캔버스 예제는 해당 뷰포트 다음에 소스를 표시합니다.

`UiCodeView(code, options)`는 선택 가능한 읽기 전용 C# 코드 위젯입니다. `UiCodeViewOptions`의 `Wrap`, `LineNumbers`, `MaximumVisibleLines`로 줄바꿈, 줄 번호, 초기 표시 줄 수를 설정합니다. 기본은 전체 표시·줄바꿈·줄 번호가 켜진 상태입니다. 전체 표시를 끄면 기본 한도 32줄로 접힙니다. `InitiallyExpanded`는 생성 시 전체 표시 여부를 설정합니다. 어휘 구문 강조는 원문이 바뀔 때만 다시 계산하고 원문 UTF-16 위치를 유지합니다. 개요·관찰 범위·미구현 페이지에는 소스가 없습니다.

갤러리 빌드는 저장소가 있는 드라이브의 `Cache/Duxel/nuget-http`를 자식 빌드 프로세스에만 지정하며 사용자와 시스템 환경 설정을 변경하지 않습니다.

갤러리는 두 영역의 헤더를 독립적으로 배치합니다. 왼쪽 메뉴의 제목·요약은 메뉴 위에 고정하고, 오른쪽 본문의 테마·상태·옵션은 오른쪽 영역의 위에 고정합니다. 창 전체를 가로지르는 하나의 헤더로 묶지 않습니다.

소스 복사는 코드 영역 오른쪽 위의 반투명 벡터 아이콘으로 표시합니다. 복사하면 아이콘 위에 “복사됨”이 1.2초 동안 표시되고 사라지며, 정지 후에는 프레임을 예약하지 않습니다. `UiCodeView.Metrics`의 측정·그리기 배치·구문 분석 횟수로 재사용을 확인할 수 있습니다. 스크롤바 판단용 너비와 확정 너비를 공통 `UiVisualTextVariants`의 두 슬롯에 보관하므로 같은 너비를 번갈아 측정해도 정적 코드의 줄바꿈을 다시 계산하지 않습니다. `UiSvg.AccessibleName`을 지정하면 Image 역할과 설명·위치를 게시하며, 이름 없는 장식 SVG는 접근성 노드를 추가하지 않습니다.

CodeView의 코드가 바뀌어도 앞뒤에서 내용과 글꼴이 같은 줄은 글자 배치를 재사용합니다. 줄 위치가 옮겨지면 새 문서 기준으로 글자 위치와 색을 맞춥니다. [CodeView와 SVG 프레임 측정](optimization-session-2026-11-19.md#codeview-reuse-after-an-svg-transform)

왼쪽 메뉴 헤더는 원본 `logo.svg`에서 재사용한 벡터 심볼과 제목·기능 설명·상태 배지를 표시합니다. 미구현 배지는 주황 계열로 구분합니다. 오른쪽 헤더의 테마·성능·옵션은 한 행이며 좁은 화면에서는 테마를 오른쪽에 고정하고 나머지 항목을 가로로 스크롤합니다. 종료·최소화로 클라이언트 크기가 0이면 세션은 위젯 배치를 건너뛰고 다음 네이티브 이벤트를 기다립니다.

코드 하이라이트는 보간 문자열의 본문·보간 구분자·내부 C# 식·이스케이프·메서드·연산자를 구분합니다. 원문과 UTF-16 경계를 유지하며 소스가 바뀔 때만 다시 분석합니다. 다크·라이트 색상과 강제 색상 모드를 지원합니다. 표시용 한글 예제 이름은 디모이입니다.

전체 코드의 자연 높이 측정과 그리기는 같은 줄 배치를 공유합니다. 코드의 하드 줄바꿈별 네이티브 스냅샷을 유지하고 너비를 넘는 긴 줄만 다시 줄바꿈합니다. 글자 텍스처 키는 글꼴·글리프·진행 폭·오프셋·소수점 위치와 백엔드 세대를 검사하며 정수 위치·원문 인덱스 변화만 있는 경우 비트맵을 재사용합니다. 현재 화면에서 그린 텍스트는 512개 콜드 캐시 예산 때문에 제거되지 않습니다. 숨겨진 항목은 한 프레임 뒤부터 제거할 수 있고, 레이어 소유권과 유휴 만료는 별도로 유지합니다.

Vulkan은 기존 펜스 퇴역 시점이 지난 작은 글자 텍스처만 같은 크기·형식으로 재사용합니다. 풀의 상한은 2,048개와 실제 할당 메모리 16MiB이며, 디스크립터 부족 시 완료된 유휴 항목을 먼저 회수하고 종료 시 모두 해제합니다. 이미지와 글자의 화질·클립·부분 갱신 계약은 유지합니다. 관련 검증은 `artifacts/gallery-source-2026-11-17/verification.json`을 확인합니다.

`UiCodeViewOptions.ShowToolbar`는 기본적으로 `true`이며 제목·줄 수·표시 옵션 도구 모음의 노출 여부를 제어합니다. 갤러리는 `MaximumVisibleLines: null, ShowToolbar: false`로 설정해 줄바꿈·줄 번호·전체 표시를 유지하면서 코드와 복사 아이콘만 표시합니다. `GallerySample`은 실제 예제 팩터리의 C# 표현식을 포착하고 예제 바로 아래에 독립적인 `UiCodeView`를 배치합니다. 카드와 타이포그래피는 예제별 배치를 사용하며 다른 복합 페이지의 분리는 진행 중입니다.

`Fluent.TagPicker(label, query, selection, items, options)`는 선택한 값을 태그로 표시하고 같은 필드에서 후보를 검색하는 복합 컨트롤입니다. 선택 순서를 유지하며 선택된 후보를 검색 결과에서 제외합니다. 빈 입력의 Backspace·왼쪽 방향키는 마지막 태그로 포커스를 옮기고 Delete로 해당 태그를 제거합니다. 선택 콜백이 변경을 거부하면 기존 태그와 검색어를 보존합니다. 외부 Label은 내부 입력에 연결됩니다. 보조 동작·자유 입력 등 Fluent 전체 적합성은 아직 완료되지 않았습니다.

갤러리 예제 코드에서는 공통 네임스페이스인 `using Duxel.Core;`만 생략합니다. 실제 프로젝트 파일과 범용 `UiCodeView`의 원문은 변경하지 않습니다. 다른 using과 문자열 안의 같은 문구는 보존합니다. 예제를 별도 앱에 옮길 때는 프로젝트 수준에서 `Duxel.Core`를 가져오세요.

`GallerySample.BindSource`는 현재 설정의 형식 있는 스냅샷을 비교하고 바뀐 경우에만 해당 코드 블록을 갱신합니다. Rating의 색상·모양·방향·선택 값·리뷰 수는 실제 설정과 동기화됩니다. 현재 예제별 소스 배치는 카드·타이포그래피·Rating·Avatar에 적용했으며, 다른 복합 페이지의 분리와 설정 동기화는 계속 진행 중입니다.

갤러리에서 편집한 값에 유니코드 줄·문단 구분 문자가 있으면 C# 이스케이프로 표시합니다. 복사한 코드를 컴파일해도 원래 값이 유지됩니다. `UiFluentIcon` 예제에는 아이콘 생성기와 지정된 카탈로그가 필요합니다. [Fluent 아이콘 가이드](fluent2-icons.ko.md)

Windows 그래픽 검증기의 `<새-결과-폴더> gallery-samples-only`는 카드, 컨테이너, Drawer, Markdown 화면에 표시된 코드를 `sources/`에 저장합니다. Markdown은 편집기, 미리보기, 중첩 스크롤과 이미지 예제를 포함합니다. 별도 프로젝트에서 컴파일하려면 `Duxel.Core`를 가져오고 예제에 필요한 패키지를 참조하세요. 실행할 때는 런타임 의존성과 참조하는 SVG 파일도 출력 폴더에 맞춰 배치하세요.

탐색 메뉴는 Fluent 컨트롤, Duxel 위젯, 통합·개발 도구를 별도 영역으로 구분합니다. 이름의 한글·영문 여부로 분류하지 않습니다. `Fluent.Nav`는 Fluent 2의 Nav 디자인과 동작을 Duxel 엔진에서 구현한 구성요소이며 Fluent 영역에 유지합니다. 범용 기반 타입 `UiNav<TPresentation>`과 해당 스타일 구현을 구분합니다. SVG·Markdown·배치·가상화는 Duxel 영역에, 갱신·측정·렌더링 관찰은 개발 도구에 배치합니다.

ScrollView의 클립은 뷰포트가 소유합니다. 큰 문서를 스크롤한 뒤 전체 콘텐츠 경계를 다시 교차하지 않아 소수 좌표의 상쇄 오차가 정상 화면을 종료시키지 않습니다. 이동된 원점이 물리 픽셀을 구분할 수 없는 경우에는 명시적으로 거부하며, 그런 크기의 콘텐츠는 가상화를 사용해야 합니다.

갤러리의 레이아웃 예제는 Row·Column·Wrap·UniformGrid·Stack·Align·Center·Padding·Container의 아홉 가지 활용마다 바로 아래에 코드를 표시합니다. 카운터·격자 열 수·오버레이 위치·정렬 변경은 해당 블록의 초기 설정에 반영하고, 변경이 없으면 구문 분석을 재사용합니다. 탭·메뉴·넘침을 함께 확인하는 기존 예제는 통합 예제의 ‘배치 통합’으로 옮겼습니다. 통합 예제의 개별 코드 분리는 아직 진행 중입니다.

`UiNavGroup.SectionHeader(value, label)`로 클릭할 수 없는 섹션 제목을 만듭니다. 그룹의 아이콘·펼침·선택 동작과 분리되며, 하위 목적지는 더 안쪽에 표시합니다. 섹션은 추가 중첩 단계가 아닙니다. [API·접근성 규칙](gallery-navigation.ko.md#섹션과-그룹-구분).

갤러리의 Windows 앱 아이콘은 Assets/Brand/duxel.ico를 명시적으로 사용합니다. 실행 파일의 ApplicationIcon 리소스와 Window.IconPath가 같은 파일을 가리키며, 16·20·24·32·40·48·64·128·256 크기를 포함합니다. 기존 SVG 로고에서 미리 생성한 ICO를 재사용하므로 창 아이콘을 위해 실행 시 SVG를 다시 해석하지 않습니다. 다른 앱의 기본 아이콘 동작은 기존 계약을 따릅니다. Fluent 기본 아이콘은 전용 TTF 글리프의 벡터 윤곽과 GPU 캐시를 사용하고, 별도 SVG 아이콘은 UiSvgIcon으로 지원합니다.

숫자 입력은 수량과 사용자 정의 단위 입력 각각의 코드 블록을 제공합니다. 현재 값·읽기 전용·크기·모양과 단위 파서·포매터를 함께 확인할 수 있습니다. 도구 모음은 방향·크기·RTL·자식 토글과 실행 횟수를 현재 코드에 반영합니다. 전체 페이지의 나머지 코드 분리와 설정 동기화는 계속 진행 중입니다.

아바타 그룹 갤러리는 실제 그룹 바로 아래에 개별 소스를 표시합니다. 배치·크기·표시 수·좌우 방향과 목록에서 선택한 구성원이 코드에 반영됩니다. SVG 이미지와 아이콘의 준비 코드도 포함하며, 상태가 같으면 기존 구문 분석 결과를 재사용합니다.

Persona와 Accordion 갤러리도 실제 예제 바로 아래에 소스를 표시합니다. Persona는 현재 이름·정보 줄·여섯 가지 표현 옵션·비활성 상태와 SVG 이미지 설정을 반영합니다. Accordion은 열린 항목·본문 메모·버튼 클릭 횟수와 네 가지 옵션을 반영합니다. 기존 입력과 위젯 인스턴스를 유지하며, 상태가 같으면 소스를 다시 분석하지 않습니다.

List 갤러리는 일반 목록·단일 선택·복합 다중 선택의 세 예제에 각각 소스를 붙입니다. 선택한 도시·구성원 배열·본문 메모·명령 횟수와 행 실행 시 선택 유지 설정을 반영하며, 복합 목록의 행 실행 핸들러도 함께 표시합니다. 각 소스는 해당 예제의 실제 팩토리를 사용하고, 상태가 같으면 구문 분석 결과를 재사용합니다.

Dropdown 갤러리는 단일 선택·다중 선택·Combobox 검색·확인형 태그·기본 태그·제어형 InteractionTag·아바타 태그의 일곱 예제에 각각 소스를 붙입니다. 현재 값과 표현 옵션, 검색·열림 상태·태그 강조 및 변경 거부·아바타 비활성 상태를 반영합니다. 공통 선택 항목은 GallerySample.Define으로 실제 값 생성식을 캡처해 재사용하며, SVG 리소스 준비와 Field 레이아웃도 각 예제 코드에 포함합니다.

Popover 갤러리는 Fluent 팝업과 사용자 정의 표면에 각각 소스를 붙입니다. 입력·명령 횟수·열림·닫기 거부·모션·포커스·표현·배치 옵션을 반영합니다. 사용자 정의 표면은 별도 컴파일 파일의 실제 선언을 리소스에서 한 번 읽어 캐시하고 코드에 포함합니다. 선언 파일은 공통 Core import와 Gallery 파일 범위 namespace, 완전한 타입 선언으로 구성하며, 문자열로 만든 별도 구현을 유지하지 않습니다.

Drawer 갤러리는 오버레이와 인라인 패널에 각각 소스를 붙입니다. 공유 메모·저장 횟수·열림과 모든 패널 옵션을 반영하며, 오버레이 코드는 실제 생성식과 본문 렌더링 후 호출하는 renderOverlays(ui) 훅을 포함합니다. 화면에서도 기존처럼 본문 클립 밖에서 오버레이를 렌더링합니다. Skeleton은 48·96 크기마다 소스를 표시하고 로딩·모션·외관 스위치가 바뀌면 기존 Watch 경로로 예제와 소스를 함께 갱신합니다.

Dialog 갤러리는 모달·비모달·경고 열기 예제와 모션 곡선 예제에 각각 소스를 붙입니다. 입력·검증 메시지·열림과 모든 Dialog 옵션을 반영하며, 대화 상자는 본문 밖의 renderOverlays(ui) 훅에서 렌더링합니다. 곡선 예제는 prepareMotion(ui)로 본문 표시 전에 값을 갱신합니다. 소스에는 목표와 곡선 설정만 반영하므로 진행 값이 매 프레임 변해도 소스를 다시 분석하지 않습니다. 저장 후 입력 유지·포커스 복원과 250ms 전환 종료 후 프레임 예약 중단을 검사합니다.

MessageBar 갤러리는 고정된 네 가지 의도와 키 기반 동적 메시지에 각각 소스를 붙입니다. 현재 배치·너비·모서리·방향·공지·모션 설정과 닫힘 상태, 실행 횟수, 동적 키 순서·다음 키를 반영합니다. 삽입·삭제·재정렬은 실제 실행하는 생성식과 콜백에서 소스를 가져오며, 공지 횟수 같은 진단 값은 소스 갱신 조건에서 제외합니다. 각 소스는 별도의 UiCodeView이고 기본 전체 표시·줄바꿈·줄 번호를 유지합니다.

공통 배치 위젯은 자식 스냅샷의 변경 번호로 준비된 경계의 유효성을 확인합니다. 자식 수가 같아도 순서나 인스턴스가 바뀌면 이전 경계를 재사용하지 않습니다. 서로 다른 높이를 가진 PresenceGroup 항목의 재정렬·교체와 빠른 MessageBar 재정렬을 회귀 검사합니다.

MessageBar 실제 실행 검증은 옵션을 조작하기 전에 상단 컨트롤을 드러내고, 조작 뒤에는 삽입 메시지까지 스크롤합니다. 화면 밖에서 잘린 접근성 경계를 실제 배치 크기로 간주하지 않으며, 현재 표시된 경계와 공개 옵션으로 검증합니다.

가상 목록 갤러리는 10억 행 예제의 실제 생성식·이동 명령과 바로 아래 소스를 함께 구성합니다. 행 소스와 가변 높이 인덱스는 별도 컴파일 파일의 실제 선언을 포함합니다. 높이 모드·키 순서·편집한 키별 값은 소스에 반영하고, 스크롤 오프셋과 생성·해제 계수만 바뀔 때는 소스를 재분석하지 않습니다.

`UiVirtualListOptions.Axis`에 `UiScrollAxis.Horizontal`을 지정하면 아래에 스크롤바를 표시합니다. `FlowDirection`으로 왼쪽 또는 오른쪽 시작을 지정하거나 상위 Fluent Provider 설정을 따를 수 있습니다. `ItemExtent`와 `ExtentIndex`는 선택한 축에 적용됩니다. 가로 휠, 손잡이와 접근성 요청은 시작점 기준의 같은 double 오프셋을 사용합니다. [가상 목록 갤러리 예제](virtual-list.ko.md)에서 바꿔 볼 수 있습니다.

Linux 검증기의 `atspi-gallery-virtual-horizontal` 모드는 D-Bus를 통해 실제 갤러리를 조작하고, 화면 밖에 있는 편집기를 `Component.ScrollTo`로 표시하는 동작을 검사합니다.

Windows와 Linux Vulkan 검증기의 `virtual-list-host-pixels`는 10억 항목의 끝에서 두 방향의 배경, 도형, 네이티브 글자까지 그린 RGBA 화면을 따로 저장합니다.

각 그리기 명령의 논리 클립, 실제 scissor, 렌더링 영역은 `.scissor.tsv`에 저장합니다.

`clip-host-pixels`는 테두리가 있는 사각형과 삼각형 메시로 MSAA 1×·4×에서 Vulkan 클립의 시작 경계를 비교합니다.

`Dux.VirtualGrid`는 가로와 세로로 스크롤하며 화면 주변의 셀을 키로 관리합니다. 보조공학 도구에서 먼 셀을 좌표로 조회한 뒤 표시를 요청하면 같은 접근성 객체에 내용이 채워집니다. [가상 그리드 예제](virtual-list.ko.md#두-축의-가상-그리드)

`virtual-grid-pixels`에서 두 축의 셀 배치와 스크롤바가 서로 차지하는 공간을 독립 Vulkan 기준 화면과 비교합니다.
`virtual-grid-host-pixels`에서는 같은 검사를 실행하고 환경 간 비교에 쓸 일부 화면을 원시 RGBA로 저장합니다.

Windows 그래픽 검증기의 `<새-결과-폴더> image-shader-bench 3 projected-clip`은 프로젝션 레이어에서 개별 클립을 적용한 테두리 사각형 256개의 GPU 시간과 픽셀 읽기 시간을 측정합니다. 전후 비교는 같은 검증기와 GPU에서 실행하세요.
`color-image`를 추가하면 같은 배치에서 텍스처를 샘플링합니다. Linux Vulkan 검증기는 글꼴 인수 뒤에 `projected-clip-bench` 또는 `projected-image-clip-bench`를 받습니다.

프로젝션 레이어의 셰이더는 개별 클립 밖의 픽셀을 버리기 전에 가장자리 커버리지를 계산합니다. 그래야 소수 좌표의 테두리에서도 미분 계산에 필요한 이웃 픽셀을 사용할 수 있습니다.

Windows 그래픽 검증기의 `<새-결과-폴더> radio-allocation-matched 3`은 창을 만들 때 클라이언트 영역을 물리 픽셀 400 × 300으로 요청합니다. Vulkan을 시작하기 전에 프레임버퍼 크기를 확인하고, 이미지 비교에는 기본 글자 크기 16픽셀을 사용합니다.

부모 배치와 클리핑을 확인하려면 각 환경에서 크기를 제한한 RadioGroup 캡처를 같은 환경의 즉시 모드 참조 이미지와 비교하세요. 두 환경이 이 검사를 통과해도 할당 영역 안의 픽셀은 서로 다를 수 있습니다.

위젯 배치 갤러리는 버튼·체크박스·슬라이더·입력·팝업·메뉴·도구 설명·기존 팝업 API의 여덟 예제에 각각 소스를 붙입니다. 공통 여백·회전·배율·중심·시각/배치 변환 모드는 각 실제 대상과 코드에 함께 적용합니다. 입력값과 실행 횟수는 관련 예제의 소스만 갱신하며, 기존 팝업 구현은 별도 컴파일 선언을 그대로 포함합니다.

배치 통합 갤러리는 탭 넘침·우선순위 Overflow·공통/Fluent/긴/체크/라디오/아이콘 메뉴를 여덟 예제로 나눠 각각 소스를 붙입니다. 실제 생성식과 탭 본문 선언을 재사용하며, 폭·방향·배치·우선순위·본문 카운터·공유 체크·라디오 선택을 반영합니다. 측정 결과와 호버 같은 일시적인 진단 변화는 소스를 다시 분석하지 않습니다.

배치 통합의 소스 바인딩은 문자열을 비교 상태로 만들지 않고 형식화된 값 스냅샷을 비교합니다. 같은 값이면 포매터를 호출하지 않습니다. 여덟 예제의 변경 없는 SynchronizeSource 1000회 반복은 워밍업 후 현재 스레드 관리 할당 0 B로 검증하며, 전체 프레임 성능과 구분합니다.

### 렌더링 예제와 이름 있는 순차 그리기 영역

갤러리의 렌더링 페이지는 정적 기하 캐시, 3D 투영, 참조 이미지 합성, 실제 그룹 합성, 투영 텍스트의 다섯 예제 바로 아래에 각각의 실제 생성 소스를 표시합니다. 내용 버전·클리핑·애니메이션 설정은 해당 소스만 바꿉니다. 배치 변경에는 별도의 캐시 버전을 사용하므로 화면에 표시하는 내용 버전이 달라지지 않습니다. 애니메이션은 켜져 있을 때만 프레임을 예약합니다.

`UiViewHost.AccessibleName`을 지정하면 할당된 유한 영역을 읽기 전용 접근성 패널로 공개하며, 패널 자체는 포커스를 받지 않습니다. 내부 위젯은 자신의 이름과 동작을 유지하고 패널의 자식이 됩니다. 이름을 지정하지 않으면 기존 접근성 계층을 유지합니다. 이름 있는 그리기 영역도 기존 상위 스크롤의 ScrollIntoView를 사용하므로 화면 밖 예제를 먼저 표시한 뒤 픽셀을 검증할 수 있습니다.

### 갤러리 고정 헤더의 검증 범위

렌더링 페이지의 하단 예제를 표시해도 좌측 제목·상단 옵션·테마 토글의 위치가 유지되는지 검사합니다. 1280×900 좌표 검사와 실제 1600×1125 프레임버퍼의 GPU 픽셀 비교를 수행했으며, 앞선 여러 페이지 조작을 포함한 순서에서도 확인했습니다. 이미지 미리보기에서 잘린 것처럼 판단했던 헤더는 원본 PNG에 정상 표시되어 있었고, 헤더 영역의 동일한 해시로 이를 정정했습니다. 제품 렌더러 변경은 필요하지 않았습니다. 모든 작은 창·리사이즈·Linux 상황을 검증했다는 뜻은 아닙니다. [검증 기록](../artifacts/gallery-fixed-header-2026-11-17/result.json).

### 팝업 입력 예제의 소스

갤러리의 팝업 입력 페이지는 내용에 필요한 높이만 차지하고 바로 아래에 실제 소스를 표시합니다. 소스에는 팝업이 소유하는 컨트롤, 짝을 맞춘 BeginPopup/EndPopup 호출, 현재 비모달·독점 모드, 열림 상태와 안팎의 실행 횟수가 반영됩니다. 팝업은 본문 클리핑을 벗어난 뒤 표시하므로 떠 있는 영역과 독점 모드의 배경 입력 차단을 유지합니다. 기반 입력 API를 설명하는 예제이며 Fluent Popover 위젯과는 별도로 제공합니다.

### Tooltip 예제와 직접 제어하는 표시 상태

갤러리의 Tooltip 페이지는 자동 설명·화살표·접근성 이름·직접 제어의 네 예제 바로 아래에 각각 소스를 표시합니다. 실제 Fluent.Tooltip 생성식을 사용하며 설명과 이름의 접근성 관계를 유지합니다. 직접 제어 예제의 소스만 Visible 변경을 따라가고, Escape가 VisibleChangeRequested 콜백으로 닫은 상태도 반영합니다. 자동 호버는 소스를 다시 분석하지 않습니다. 페이지는 생성할 때부터 루트 위젯을 소유하며 독립된 소스 파일로 제공합니다.

### 입력 예제와 반응형 검증 소스

입력·IME 페이지는 이름 검증과 연결 라벨을 가진 메모 편집을 두 예제로 나누고 각각 바로 아래에 소스를 표시합니다. 실제로 컴파일한 ValidatedNameExample 선언이 이름 상태를 관찰하고 Field 검증을 갱신하므로 복사한 코드도 같은 동작을 유지합니다. 메모 소스에는 현재 텍스트·줄바꿈·읽기 전용과 라벨의 텍스트·모든 옵션을 반영하며, 문자열 이스케이프와 플래그 결합식도 지원합니다. 커서·선택은 소스 상태에서 제외하고 값이 같으면 구문 분석 결과를 재사용합니다. 두 입력창 사이에 코드가 있으므로 접근성 값 변경 전에는 각 입력창을 화면에 표시해야 합니다. 이 예제 검사가 운영체제 IME 조합의 전체 수용 검증을 대신하지는 않습니다.

### 측정 예제와 소스 상태

측정 재사용 페이지는 기본 컨트롤과 합성 폼을 두 열로 비교하고 각 예제 바로 아래에 소스를 표시합니다. 실제 컴파일한 MeasurementPrimitives·MeasurementForm 선언을 사용하며 기존 측정 계측·입력 바인딩·420×420 및 420×430 패널을 유지합니다. 소스에는 현재 값, 라벨·본문·배지·Spinner·너비 설정과 탭 크기를 반영하고 측정 횟수·진행률 읽기 횟수는 제외합니다. 진단 수치가 바뀐다는 이유로 소스를 계속 다시 만들지 않습니다. 측정 재사용 횟수는 전체 CPU·GPU 비용이나 FPS와 구분해야 합니다.

### 갱신 관찰 소스와 진단 작업 분리

갱신 관찰 페이지는 내용에 맞는 높이로 실험을 표시하고 바로 아래에 실제 컴파일한 RepaintExperiment 소스를 붙입니다. 자동 변경·CPU 측정 설정은 동기화하지만 실행 중인 입력 텍스트·변경 횟수·구성 횟수·측정 스냅샷으로 코드를 다시 쓰지 않습니다. 코드 영역의 갱신이 관찰 결과를 흐리지 않도록 하기 위한 구분입니다. 기존 64픽셀 동적 패널·140픽셀 정적 패널과 프레임 예약·스냅샷 동작은 유지합니다. 본문 렌더 타이머는 형제 소스 영역과 외부 자연 배치 측정을 제외하며, 전역 CPU 구간 측정은 전체 UI Render 범위를 측정합니다. 순차 렌더 호스트와 컨트롤·내용의 소유권을 명시합니다.

Markdown 갤러리는 선택한 편집기·미리보기·중첩 미리보기 바로 아래에 해당 예제의 소스 하나를 표시합니다. 현재 문서와 선택된 Markdown 옵션은 타입이 있는 스냅샷으로 동기화하며, 커서·선택·스크롤 위치는 소스를 재작성하지 않습니다. 중첩 예제는 실제 ScrollView 생성식 두 개를 포함합니다. 소스에 포함된 MarkdownImageResources는 SVG·메모리 이미지를 캐시하는 실제 공통 구현이며, 사용할 수 없는 이미지는 기존 대체 텍스트 동작을 유지합니다.

Markdown 문서 소스는 읽기 쉬운 여러 줄 C# 원시 문자열로 표시합니다. 문서에 포함된 연속 따옴표보다 긴 구분자를 사용하며, CR·제어 문자는 이스케이프 문자열로 정확하게 보존합니다.

Fluent 종합 화면은 카운터·배지, 토글 값 공유, 혼합 체크박스, 라디오 선택, 슬라이더·진행률, 내부 링크, Spinner 재생을 일곱 예제와 인접 소스로 나눕니다. 각 탭 표현에도 별도 소스와 실제 아이콘 선언이 있습니다. 현재 바인딩 값은 독립적으로 동기화하며 Spinner 애니메이션과 유휴 프레임은 파싱된 소스를 유지합니다. ControlsPage와 TabExamples는 첫 렌더링 전부터 루트를 소유합니다.

종합 예제의 Spinner는 재생 버튼과 한 줄에 배치할 수 있도록 Width180을 지정합니다. 통합 회귀 검사는 실제 입력 전에 라디오·카운터 대상을 스크롤해 표시합니다. 예제 사이에 소스를 추가하면 의미적 계약을 바꾸지 않아도 입력 대상의 화면 위치가 달라집니다.

SVG 기본 예제는 이미지·벡터 비교, SVG 텍스트, 문서 기능별 인접 소스를 제공합니다. 현재 변환·모서리·이미지 맞춤을 반영하며, 생성 함수는 미리보기와 SvgStore 소유자를 함께 반환해 올바른 순서로 해제할 수 있게 합니다. 실제 페이지는 저장소와 비트맵 디코더 하나를 계속 공유합니다. 브라우저 비교·아이콘도 같은 인접 소스 경로를 사용하며, 전체 페이지 참조는 제거했습니다.

순차 콘텐츠에 위젯 트리를 자연 높이로 배치할 때는 ui.RenderNaturalHeight(widget)를 사용합니다. 사용 가능한 유한 너비와 무제한 높이로 측정한 전체 크기를 배정하며, 클립과 스크롤은 부모가 관리합니다. 기존 widget.Render의 유한 배치 검증은 그대로 유지합니다. 스크롤 화면의 남은 높이를 Column 전체 콘텐츠 한도로 잘못 전달하지 않으며, 예외 무시·클립 우회·임의 높이 제한을 사용하지 않습니다.

기존 BeginChild/EndChild도 접근성 Pane과 실제 스크롤 범위를 게시하고, 중첩 부모·부모 클립을 보존하며 ScrollIntoView·스크롤 요청을 처리합니다. 화면 밖 자식을 드러낸 뒤 실제 포인터로 조작할 수 있습니다. 900px SVG 회귀는 두 소스 블록 아래의 아이콘 메뉴를 스크롤해 표시한 뒤 엽니다. Pane 이름은 내부 범위 ID 대신 호출자가 지정한 표시 이름을 사용합니다.

SVG 브라우저 비교는 실제 SvgBrowserPair 호스트와 공통 SvgBrowserReference 지문 검증을 사용합니다. 각 비교에는 현재 변환·배경, 이식 가능한 자원 경로와 실제 보조 구현을 포함한 소스가 있습니다. SvgExampleSource가 자원 소유자를 반환하는 생성 코드 구성을 통합합니다. SVG 아이콘의 TabList·Menu·독립 Icon에도 각각 현재 소스가 있습니다. 활성 소스 목록은 선택된 탭을 따르며 이전의 혼합 전체 페이지 참조는 제거했습니다.

앱 테마를 바꾸면 선택적 IWindowThemeController를 통해 Windows 제목 표시줄·제목 글자·테두리 색도 동기화합니다. DuxelAppSession은 색 변경을 합쳐 창을 소유한 플랫폼 이벤트 스레드에서만 적용합니다. Windows의 기존 DWM 오류 검사 경로를 재사용하고 강제 색상 우선순위와 시스템 크롬 통합 해제 설정을 보존합니다. Windows 전체 테마 설정은 변경하지 않습니다.

갤러리의 기존 API 페이지는 Duxel.Sample과 공유하는 ImmediateSampleSection 콜백12개로 구성합니다. 실제 콜백의 소스와 형식화된 상태 스냅샷을 연결하고 해당 구역의 값이 바뀔 때만 소스 문자열과 구문 분석을 갱신합니다. 방문한 구역마다 UiCodeView를 하나씩 소유하고 컨트롤 바로 아래에 전체 소스·줄바꿈·줄 번호·복사 동작을 표시합니다. 비활성 탭과 접힌 구역에서는 소스를 숨기며 예제 상태는 유지합니다. 독립 실행 샘플에는 갤러리의 소스 표시를 추가하지 않습니다.

기존 BeginChild의 접근성 공개 요청은 이전에 게시한 스크롤 범위를 기준으로 자식을 그리기 전에 적용합니다. 그려진 좌표와 게시한 위치를 같은 프레임에 맞춰 공개 이동량의 중복 적용을 막습니다. 유한 크기의 중첩 갤러리 회귀는 접근성 요청의 완료를 기다리고 슬라이더 노출·표 편집·소스 상태를 검사하며, 큐 수락을 변경 완료로 취급하지 않습니다.

GalleryNavigation.LatestAddedMenus에 가장 최근 메뉴 추가 묶음을 한 번만 명시합니다. 현재 대상은 DatePicker·TimePicker·DateTimePicker·공휴일 제공자이며 메뉴 오른쪽에 녹색 NEW 배지를 표시합니다. 새 메뉴를 추가하면 이 묶음을 교체합니다. 기존 메뉴의 수정·경과 시간·방문 여부로 표시 기간을 늘리지 않습니다. 미구현 메뉴의 주황색 배지는 별도로 유지하며, 없는 메뉴나 미구현 메뉴를 NEW로 지정하면 시작 검증에서 실패합니다.

Combobox 완성 입력은 표시 중인 팝업 행과 숨은 원본 선택 항목을 같은 필터 기준으로 게시합니다. TagPicker에서 포인터로 항목을 선택하면 입력을 비우고 닫은 뒤 접근성 게시가 끝나고 나서 선택 제외 필터를 다시 구성합니다. 프레임별 ID 유일성 검사는 유지하며 중복 오류를 삼키거나 원본 선택 항목을 버리지 않습니다. 김민수·박지연의 실제 누름/놓기 선택, 재확장, 닫힌 목록 상태와 갤러리 Vulkan 팝업 경로를 회귀 검사합니다.

스크롤된 탭과 탐색 본문은 배치용 남은 높이 대신 실제 화면의 끝 좌표로 클립을 계산합니다. 기존 탭·Fluent 탭·탐색 페이지가 같은 계산을 사용하며, 부모 경계와 포커스 테두리는 보이는 영역을 따릅니다. [계약과 검증](fluent2-tab-content.ko.md).

독립 예제 22개는 공통 인라인 소스 위젯을 사용합니다. 상태가 바뀌는 16개는 타입별 비교로 초기화 코드를 갱신하고, 같은 상태는 소스와 구문을 재사용합니다. 선택 실행과 완료 기록은 부분 검사 재개를 돕지만 자동 캐시를 뜻하지 않습니다. [계약과 검증](gallery-navigation.ko.md).

Fluent.FluentProvider는 자식 영역의 팔레트·기하 토큰·글꼴·배치 방향을 측정과 그리기에 적용합니다. `Direction`으로 부모 방향을 따르거나 영역의 방향을 지정할 수 있습니다. 대응하는 탐색 위젯과 팝업은 이 방향을 사용하며, 지연 Tooltip에도 전달합니다. [API·한계·검증](fluent2-provider.ko.md).


Fluent.Shimmer는 Skeleton 자리 표시자와 실제 내용을 소유하고 공통 전환·퇴장 화면 캐시로 교체합니다. 완료 후 추가 프레임을 요청하지 않습니다. [API·예제·한계](fluent2-shimmer.ko.md).


갤러리 왼쪽 고정 메뉴는 기본 창에서 280 논리 픽셀을 사용합니다. 좁은 화면의 서랍 전환은 기존 동작을 유지합니다.


Windows에서 WM_CLOSE를 수락하면 안전한 자원 해제 전에 창을 먼저 숨깁니다. 기존 LogStartupTimings에는 종료 구간도 기록합니다. [측정·범위](windows-close-response.ko.md).

Windows x64 .NET 11 NativeAOT 갤러리는 TimePicker 팝업이 열린 상태에서 WM_CLOSE를 받아도 정상 종료합니다. [팝업을 열어 둔 상태의 종료 검증](windows-close-response.ko.md)

Windows .NET 11 관리형과 NativeAOT 갤러리 검증이 같은 표시 시나리오 104개를 순서대로 마치고 `DuxelAppSession.Exit()`로 정상 종료했습니다. [앱 내부 종료 검증](windows-close-response.ko.md)


Fluent.ProgressIndicator는 작업 이름·설명·확정/미확정 진행률을 소유합니다. 막대만 숨길 수 있고 상태·소스가 함께 바뀝니다. ReducedMotion은 미확정 막대를 불투명도 변화로 표시합니다. [API·한계·검증](fluent2-progress-indicator.ko.md).


Fluent.ActivityIndicator는 Fluent iOS의 270도 원호·750ms 회전·5개 크기 토큰을 사용합니다. 정지 시 숨겨도 공간을 유지하며 Start/Stop과 색상·이름·정지된 표시를 지원합니다. [API·한계·검증](fluent2-activity-indicator.ko.md).


Dux.Calendar/RangeCalendar는 문화권별 단일·기간 선택과 월·연도 휠을 제공합니다. Dux.DatePicker/TimePicker/DateTimePicker는 입력과 팝업을 연결하며 UiWheelPicker<T>를 재사용합니다. 공휴일은 IUiCalendarHolidayProvider 또는 불변 스냅샷으로 연결하고 네트워크 오류·연결 중단·시간 초과 시 오류 메시지 없이 표시만 생략하며 날짜 선택은 계속 사용할 수 있습니다. [달력](calendar-globalization.ko.md) · [입력 선택기](date-time-pickers.ko.md) · [공휴일](calendar-holidays.ko.md).

외부 검증기 `atspi-gallery-range-calendar`로 Linux 갤러리에서 종료일을 먼저 고르는 동작, 달력 문화권 변경, 선택 지우기를 확인했습니다. 현재 .NET 11 x64 관리형 빌드와 앞서 게시한 x64 NativeAOT 빌드에서 모두 통과했습니다. [RangeCalendar 검증](calendar-globalization.ko.md)

`x11-gallery-range-calendar` 모드는 Enter로 새 기간 선택을 시작하고 Esc로 취소해도 이전에 확정한 날짜가 유지되는지 확인합니다. 지정한 갤러리 창에 X11 키 입력을 보내고 AT-SPI로 결과를 읽습니다. [키보드 검증](calendar-globalization.ko.md)

`x11-gallery-range-calendar-pixels` 모드는 방향키로 기간을 확정하고 태국 불기 달력에서도 선택을 유지하는지 확인합니다. WSLg 관리형과 NativeAOT에서 진단 표시 아래 화면 여섯 쌍이 일치했습니다. [실행 방법](calendar-globalization.ko.md)

- SegmentedControl 본체 미리보기: `UiSegmentedControlWidget` / `UiSegmentOption` / `UiSegmentedControlOptions`. [API와 남은 범위](fluent2-segmented-control.ko.md). Fluent 팩터리는 미등록이며 미구현 배지를 유지합니다.

- SegmentedControl 미리보기는 공식4스타일의 표준 밝음/어두움·활성/비활성 팔레트, 15크기 body2, 좌우16 여백과 ease-out 선택 이동을 제공합니다. Style/HorizontalInset/UnreadLabel 옵션을 지원합니다. 스크롤·투명 마스크는 미완료이며 Fluent 팩터리는 미등록입니다.

- 가로 투명 마스크: `UiHorizontalOpacityMask`와 PaintGroup/BackdropImage/BackdropComposite 오버로드. UiImageUvTransform으로 캐시 카메라를 조절하며, 마스크·UV만 바꾸면 캡처를 유지합니다. [API·실행 예제](horizontal-opacity-mask.ko.md).

`Fluent.SegmentedControl`을 연결했습니다(50/55, 미연결5). 자연 너비·드래그/휠·선택 항목 노출과 UiPaintGroup 캐시·UV 카메라·숨은 쪽 투명 페이드를 제공합니다. 갤러리5개 예제에 현재 설정 소스를 표시하며 최신 NEW는 Select입니다. 실제 Linux·NativeAOT·외부 보조공학 수용은 남았습니다. [현재 계약](fluent2-segmented-control.ko.md).

Toast는 UiToastController·UiToasterWidget의 비모달 알림, 자동 닫힘·호버/초점 정지·F6 왕복·네이티브 활성 상태 연계를 제공합니다. 독립 UiToastWidget은 진행 막대/스피너와 긴 내용 ScrollView를 공유합니다. 화면 높이·글자 배율에 맞춰 초과 알림을 대기시키고 남은 시간을 보존합니다. 컨트롤러마다 살아 있는 호스트 하나만 허용하며 해제 뒤 알림을 유지한 채 재연결할 수 있습니다. 갤러리 네 예제에 현재 설정 소스를 표시합니다. Fluent.Toast와 호스트용 Fluent.Toaster를 연결했습니다. NEW는 Select에만 표시하며 미구현 배지는 제거했습니다. 전체 플랫폼 수용 검증은 남았습니다. [API와 실행](fluent2-toast.ko.md).

Toast 제목·본문·진행 문구는 네이티브 문단 조판과 RTL 줄 정렬을 사용합니다. 좁은 폭에서는 본문을 헤더 아래로 배치하고, 헤더 최소 폭도 없으면 알림을 대기시킵니다. 공통 UiInlineText.HorizontalAlignment는 문단 방향과 독립적으로 각 줄을 왼쪽·가운데·오른쪽에 정렬합니다. UiProgressValue.RightToLeft와 UiProgress의 같은 속성은 Fluent 진행 막대의 시작 방향을 바꿉니다.

Fluent.Select는 텍스트 항목 하나를 고르는 필드입니다. 최초 기본 선택, 닫힌 상태의 방향키·Home/End/Page·이름 검색, 접근성 선택 유지, 세 크기·네 외관과 공통 팝업을 제공합니다. 갤러리의 세 예제는 현재 값과 설정을 바로 아래 소스에 반영합니다. [현재 계약](fluent2-select.ko.md).

Tree의 내부 행 위젯이 검증한 계층·펼침·하위 항목 선택 모델을 사용하도록 연결했고 입력24건·GPU7건을 확인했습니다. Tree/TreeItem 계층과 RTL 배치를 제공하고 공개 팩터리와 갤러리를 연결했습니다. 외부 보조 기술을 사용한 검증은 남아 있습니다. [현재 계약과 남은 작업](fluent2-tree.ko.md).

접근성의 `CaptureSelection(id, runtimeId, out selection)`과 `ResolveSelectionContainer`는 게시된 한 시점의 계층 선택 정보를 반환합니다. 불변 스냅샷에서도 같은 조회를 지원합니다. Windows UIA와 Linux AT-SPI가 공통 소속 정보를 사용하며 Tree는 하위 독립 목록의 선택을 섞지 않습니다. 접힌 단일 선택 항목과 요청한 항목은 필요한 숨은 경로만 가상 게시하며 ItemContainer·Realize·ScrollIntoView로 조상을 펼칠 수 있습니다. RuntimeId·선택·초점을 보존합니다. 실제 Linux·외부 보조공학 수용 검증은 남아 있습니다. [Tree 계약](fluent2-tree.ko.md).

`UiTree<TPresentation>`은 공개된 계층 위젯이며 `IUiTreePresentation`으로 행 측정과 페인트를 분리합니다. 펼침 전용·단일 선택·말단 다중 선택 생성자와 크기/배경/방향 옵션, 한글 이름 검색을 제공합니다. 갤러리에서 Fluent.Tree와 행 안의 버튼을 확인할 수 있습니다. [실행과 계약](fluent2-tree.ko.md#공개-표현과-이름-검색).


Tree의 세 인수 `UiTreeItem`은 표현이 관리하는 문자열 라벨입니다. 기본 줄바꿈·크기별 글꼴과 `UiTreeOptions.TextOverflow = UiTreeTextOverflow.Ellipsis`를 지원하며 잘린 행은 전체 이름 Tooltip을 제공합니다. 기존 네 번째 위젯 인수는 명시적 콘텐츠를 유지합니다. [계약](fluent2-tree.ko.md#기본-문자열-라벨).

Windows와 Linux에서 `tree-gpu`, `tree-label-gpu`로 같은 Vulkan 검사를 실행할 수 있습니다. 체크박스와 화살표 클릭, RTL 계층, 테마별 호버, 긴 레이블 배치를 확인합니다. [Linux 실행기와 인수](../tests/Duxel.Vulkan.Linux.Tests/README.ko.md).

같은 실행기의 `container-pixels`에서는 ScrollView의 오프셋과 클립, 사용자 정의 컨테이너의 루트 클립, 부모 할당이 그대로인 Grid의 재배치를 확인합니다. 각 환경에서 따로 그린 기준 화면과 비교합니다.

갤러리 **컨테이너** 페이지의 ScrollView 예제에서 세로와 양방향 스크롤을 전환할 수 있습니다. 바로 아래에는 현재 설정에 맞는 소스가 표시됩니다. [ScrollView 사용법](scroll-view.ko.md).

Linux의 `atspi-gallery-tree`는 별도 libatspi 클라이언트로 갤러리 Tree를 조작하고 TreeGrid 셀 안의 버튼을 실행합니다. [실행 방법과 검사 범위](fluent2-tree.ko.md#linux에서-접근성-확인).


Tree 행 안에 컨트롤을 넣으려면 `UiTreeOptions.RowActions`를 켭니다. F2로 진입하고 방향키로 이동하며 Escape로 행에 돌아갑니다. 접근성에는 TreeGrid 역할과 펼쳐진 행마다 콘텐츠 셀 하나를 게시합니다. Linux AT-SPI 브리지는 이 표의 메타데이터로 Table과 TableCell 조회를 제공합니다. [Tree 샘플과 동작](fluent2-tree.ko.md#행-안의-버튼과-입력-컨트롤).

갤러리의 Tree 메뉴는 `Fluent.Tree` 예제 두 개를 제공합니다. `AlignSelectorToTop`은 여러 줄 행의 선택 표시와 클릭 영역을 위쪽에 둡니다. Tree, Accordion, Nav, 옵션 입력, 기존 Combo와 하위 메뉴의 펼침 화살표는 같은 160ms 전환을 사용합니다.

스위치 손잡이, Fluent ToggleButton의 선택 색상, Fluent Checkbox의 체크 표시도 부드럽게 전환됩니다. 바인딩과 접근성 상태는 즉시 바뀌고, 전환이 끝나면 이벤트 대기 상태로 돌아갑니다.


`Fluent.TextField`는 공통 한 줄 입력과 Field를 결합합니다. 입력값이 남아 있으면 초점이 이동해도 지울 수 있습니다. 갤러리에는 짧은 입력과 입력 후 검증 예제가 있습니다. [Input과 TextField 안내](fluent2-input.ko.md#textfield).

`Fluent.CardNudge`는 닫기 버튼이나 좌우 밀기로 닫을 수 있는 안내 카드입니다. 갤러리에서 현재 상태가 반영된 예제 소스를 볼 수 있습니다. [CardNudge API와 동작](fluent2-card.ko.md#cardnudge).

`Fluent.Carousel`은 슬라이드 위젯을 소유하고 바인딩된 키로 현재 슬라이드를 고릅니다. 이름이 있는 단계 선택과 이미지 미리보기를 지원합니다. 자동 재생을 쓰는 예제에는 일시 정지 기능이 있습니다. **콘텐츠와 목록 → Carousel**에서 두 예제와 소스를 확인할 수 있습니다. [Carousel API와 동작](fluent2-carousel.ko.md).

Linux의 `atspi-gallery-carousel`은 관리형과 NativeAOT 갤러리의 두 예제에서 외부 접근성 검사 9건을 실행합니다. 미리보기 선택, 첫 장과 마지막 장 사이 이동, 자동 재생과 일시 정지를 포함합니다. [Carousel 검증 명령](fluent2-carousel.ko.md).

`Fluent.NavigationBar`는 현재 화면의 제목, 양쪽 동작, 보조 컨트롤을 소유합니다. 좁은 화면에서는 줄을 나누고, 테마 배경·투명 배경·브랜드 배경을 지원합니다. **동작과 탐색 → NavigationBar**에서 두 예제를 볼 수 있습니다. [NavigationBar API와 동작](fluent2-navigation-bar.ko.md).

Windows에서는 `./run-gallery.ps1`, WSLg에서는 `./run-gallery-linux.ps1`로 갤러리를 엽니다. Linux 빌드는 설치된 글꼴을 자동으로 찾습니다. [Linux 갤러리 실행 안내](../samples/Duxel.Gallery.Linux/README.ko.md).

Fluent 메뉴와 옵션 목록에 Shadow16을 적용합니다. TabList의 선택 표시가 새 탭으로 이동하며 너비도 함께 바뀝니다. `UiTabListOptions.ReducedMotion`을 켜면 즉시 이동합니다. Fluent Checkbox와 RadioGroup의 나란한 라벨은 첫 줄 글자 윤곽의 중심을 표시 상자나 원 중앙에 맞춥니다.

Linux 갤러리의 RadioGroup은 `x11-gallery-radio-group`으로 세로 방향키 이동과 단일 선택을 확인할 수 있습니다. 지정한 창에 키를 보내고 AT-SPI로 포커스와 선택 상태를 읽습니다. [RadioGroup 사용법과 GPU 비교](fluent2-radio-group.ko.md)

갤러리의 SVG 미리보기는 변환 값을 즉시 반영합니다. 예제 아래 C# 소스는 마지막 조작 120ms 뒤에 갱신됩니다. `gallery-svg-profile` 진단과 측정 범위는 [SVG 성능 기록](svg-gpu-camera.ko.md)에서 확인할 수 있습니다.

갤러리의 SVG 브라우저 비교는 기본으로 `UiSvg.UseFixedResolutionPreview`를 켭니다. 조작을 멈춘 뒤에도 브라우저 기준 해상도로 그려 둔 GPU 이미지를 확대·회전합니다. 고해상도 옵션을 켜면 마지막 조작 120ms 뒤에 최종 배율로 다시 그립니다. [동작과 측정 결과](svg-gpu-camera.ko.md)

Vulkan 셰이더는 곡선의 제어점 범위를 벗어나 채우기에 영향을 줄 수 없는 샘플의 탐색을 건너뜁니다. 소프트웨어 Vulkan의 반복 측정과 Windows 출력 비교 결과는 [SVG 카메라 가이드](svg-gpu-camera.ko.md)에 있습니다.

고해상도 비교를 켠 경우에도 스크롤 중에는 고정 해상도 GPU 이미지를 재사용합니다. 내용이 120ms 동안 움직이지 않으면 보이는 범위를 최종 해상도로 그립니다. [스크롤 검증](svg-gpu-camera.ko.md)

Vulkan SVG 셰이더는 경로가 어느 표본도 덮지 않으면 색 계산을 건너뜁니다. Windows GPU 실측과 소프트웨어 Vulkan에서 확인한 한계는 [SVG 카메라 가이드](svg-gpu-camera.ko.md)에 있습니다.

갤러리는 변환 슬라이더에서 손을 뗀 시점부터 최종 해상도로 그릴 때까지 기다립니다. 바로 이어지는 조작이 중간 렌더 요청을 건너뛰는지는 `gallery-svg-profile`의 포인터 기록에서 확인합니다. [SVG 카메라 가이드](svg-gpu-camera.ko.md)

Vulkan 렌더러는 장면의 그리기 범위를 이용해 가로·세로 SVG 블러에서 읽을 픽셀을 줄입니다. 블러 가중치는 전체 커널을 기준으로 계산합니다. [카메라 성능과 필터 출력](svg-gpu-camera.ko.md)

SVG 경로의 표본을 검사할 때 픽셀의 카메라 값을 재사용합니다. 성능 로그의 Vulkan 장치 종류를 보고 GPU와 CPU 측정을 구분하세요. [카메라 성능](svg-gpu-camera.ko.md)

SVG 시간을 비교할 때는 `gpuSubmission`으로 같은 프레임을 찾아보세요. `vectorBlurGpuUs`는 블러 합계, `vectorOutsideBlurGpuUs`는 복사를 포함한 나머지 벡터 작업 시간입니다. [측정 항목](svg-gpu-camera.ko.md)

`vectorPathGpuUs`와 `vectorCopyGpuUs`로 벡터 이미지 그리기와 장면·의존성·텍스처 복사 시간을 나눠 확인할 수 있습니다. [경로와 복사 시간](svg-gpu-camera.ko.md)

갤러리의 `--verify-gpu-camera` 실행은 2배 확대와 37도 회전을 함께 적용한 경우도 검사합니다. 브라우저 비교의 두 SVG가 조작용 미리보기에서 최종 해상도로 돌아온 뒤 Vulkan 표시 응답을 확인합니다. 카메라를 바꾸면서 CPU에서 SVG 비트맵을 추가로 만들지 않는지도 검사합니다.

Linux 외부 검사 `x11-gallery-svg-pixels`는 실제 브라우저 비교 탭을 열고 1280×900 창을 기본 상태와 2배 확대·37도 회전 상태에서 캡처합니다. Ubuntu WSLg의 Mesa lavapipe에서 .NET 11 관리형과 Linux x64 NativeAOT는 상단 진단 표시 아래의 모든 픽셀이 같았습니다. 비교 범위는 갤러리 창에 보이는 부분입니다.

`UiSlider.IsPointerDragging`으로 슬라이더가 마지막 렌더링에서 포인터를 잡고 있었는지 확인할 수 있습니다. SVG 갤러리는 변환 슬라이더를 누르는 동안 미리보기를 유지합니다. 회전한 SVG를 최종 해상도로 그릴 때는 렌더러가 위젯 좌표에서 상위 클립을 모두 적용해 카메라의 출력 범위를 정합니다.

`svg-replacement-pressure`는 Vulkan에서 3,000×3,000과 3,200×3,200 SVG 텍스처를 번갈아 만듭니다. 현재 텍스처는 1개, GPU 완료를 기다리는 이전 텍스처는 프레임 슬롯 수 이내인지 확인합니다. 슬롯을 비우면 이전 텍스처가 모두 해제되어야 합니다. Windows Graphics와 Linux Vulkan 테스트에서 실행할 수 있습니다.

Linux Vulkan의 `radio-parent-pixels` 모드는 공통 RadioGroup 부모 할당 검사를 1배·4배 MSAA와 프레임 슬롯 1개·3개로 실행합니다. WSLg의 .NET 11.0.0 관리형·NativeAOT 이미지 104쌍이 모두 일치했습니다. [RadioGroup](fluent2-radio-group.ko.md)

Linux에서 NavigationBar의 갱신 범위를 확인하려면 갤러리에 Vulkan 프레임별 기록을 켜고, 같은 `DUXEL_VK_PROFILE_OUT`과 갤러리 PID를 사용해 `atspi-gallery-navigation-repaint` 검사를 실행합니다. 로그에서 새 화면 이미지의 초기화, 이전 갱신 이력의 무효화, 실제 그리기 명령 비교 결과를 확인할 수 있습니다.

Linux Vulkan 화면 재사용 검사는 합성 그룹의 텍스처 버전이 바뀔 때 부분 갱신과 전체 다시 그리기의 원시 픽셀을 대조합니다. 고정 .NET 11 관리형과 NativeAOT에서 슬롯 1개·3개의 해시도 비교합니다. [Linux Vulkan 픽셀 검사](../tests/Duxel.Vulkan.Linux.Tests/README.ko.md)

Linux 갤러리의 `x11-gallery-provider-radio-direction` 검사는 수평 라디오 항목 세 개의 좌우 방향키 이동과 포커스, 배치 방향 변경 뒤 선택 유지를 확인합니다. 같은 예제에서 체크박스·스위치·슬라이더 값도 확인합니다. [RadioGroup](fluent2-radio-group.ko.md)

`Fluent.DataGrid<T>`는 고정 열 제목, 행 가상화, 값 기준 정렬과 열별 편집을 지원합니다. 갤러리의 **Fluent 컨트롤 → 콘텐츠와 목록 → DataGrid**에서 사용해 볼 수 있습니다. [Fluent DataGrid](fluent2-data-grid.ko.md)

`Dux.VirtualGrid`는 스크롤 전후에 겹치는 셀을 재사용하고 화면 주변으로 새로 들어온 셀만 바인딩합니다. 기존 셀의 모델이 바뀌면 `Refresh()`를 호출하세요. 셀 안에 별도로 스크롤되는 위젯이 없다면 `UiVirtualGridOptions.OwnsWheelInput`을 켤 수 있습니다. 갤러리의 **가상 그리드**는 이 옵션을 사용해 연속 휠 입력을 합친 뒤 새 위치를 그립니다.

`DuxelAppSession.ShowFileDialogAsync`는 파일 열기·저장·폴더 선택 대화 상자를 열고 사용자가 고른 경로를 반환합니다. 갤러리의 **통합·개발 도구 → 시스템 연동 → 파일 대화 상자**에서 세 동작을 확인할 수 있습니다. [파일 및 폴더 대화 상자](file-dialogs.ko.md)

텍스트를 선택한 상태에서 포인터를 보이는 영역 밖에 두면 마우스 버튼을 누르는 동안 스크롤이 이어집니다. CodeView와 여러 줄 편집기는 텍스트가 넘치는 방향으로, 한 줄 입력란은 가로로 이동합니다. 편집기가 자체적으로 더 움직일 수 없으면 바깥 스크롤 영역이 이동하므로 문서 안에서 길게 펼쳐진 코드도 계속 선택할 수 있습니다.
