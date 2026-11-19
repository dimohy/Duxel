# Linux X11 플랫폼 기반

외부 AT-SPI 클라이언트로 실제 Linux NativeAOT 갤러리의 **날짜와 시간 → TimePicker**를 열었습니다. 분 휠에서 05분을 선택하고 **확인**으로 팝업을 닫았습니다. 저장된 값을 다시 읽어 초가 유지됐는지 확인했습니다. 실행 모드는 `Duxel.Linux.App.Tests atspi-gallery-time-picker <갤러리-PID> <새-결과.json>`입니다. 갤러리와 클라이언트는 같은 접근성 세션에서 실행하며 검증 때는 `C.UTF-8`을 사용했습니다. [날짜·시각 검증](date-time-pickers.ko.md)

`x11-gallery-time-picker-wheel <갤러리-PID> <새-결과.json> <윈도-ID>` 모드는 검증용 갤러리 창의 분 휠에 X11 위·아래 이벤트를 보냅니다. 각 이벤트가 적용된 시각은 AT-SPI에서 다시 읽어 확인합니다. [X11 휠 검증](date-time-pickers.ko.md)

`atspi-gallery-date-pickers <갤러리-PID> <새-결과.json>` 모드는 같은 외부 클라이언트에서 DatePicker와 DateTimePicker를 조작합니다. 월과 일을 선택하고, DateTimePicker에서 날짜와 시각을 각각 바꿔 다른 값이 유지되는지 확인합니다. [날짜 선택기 검증](date-time-pickers.ko.md)

최신 전달본은 `e529777d`입니다. Linux 공통 스타일 문단과 실제 Core 소비 경로를 구현했고 NativeAOT 스타일351개·기존 위치334개·텍스트 기준114개와 실제 옵저버41개·전달38개가 통과했습니다. `& ./artifacts/linux-present-path-2026-11-15/run-observer.ps1`로 실행해 타이포그래피 탭을 확인하세요. 아래 이전 전달 ID는 과거 기록입니다. [현재 검증](../artifacts/fluent-messagebar-2026-11-15/linux-styled-paragraph-verification.json).

2026-11-16: Linux가 명시적 폰트만 등록한 전용 Pango 맵으로 동일 스타일 행 전체를 조판하며 네이티브 커서·클릭·선택 위치를 Core와 공유합니다. 네이티브334개·기준114개·옵저버41개·전달38개 통과, 현재 전달본은 `7746d0b7`입니다. [현재 조판·의존성·실행 가이드](linux-text-shaping.ko.md)가 아래 이전 텍스트 경로 설명보다 우선합니다. 혼합 스타일 전체 문단 구성은 남아 있습니다.

2026-11-15 [일반 텍스트 접근성](fluent2-text.ko.md): 같은 문구의 독립 정체성·실제 배치 경계·한 번의 소스 읽기·네이티브 내용 갱신을 연결했습니다. 실제 옵저버·실행기를 확인해 현재 배포에 반영했으며 성능 비교는 구간 중첩으로 방향 미확정입니다. 이전 실행물은 보존합니다.

2026-11-15 현재 Linux 옵저버는 단일 배포 원장 `artifacts/linux-observer-delivery.json`의 해시·명시적4개 소프트웨어 래스터 작업 스레드 설정을 검증해 실행합니다. [Skeleton](fluent2-skeleton.ko.md)은 실제 AT-SPI busy·로딩 완료 전환을 확인했으며 전체 접근성 완료와는 별개입니다.

## Linux CPU 작업 스레드 최신 검증

기본24개 llvmpipe 작업 스레드를4개로 제한한 동일 RC3 NativeAOT의 독립3쌍 비교에서 세 화면 CPU37.60~38.51% 감소를 확인했습니다. MSAA·폰트·갱신 면적은 같고, 슬롯1/3의7,140개 검사 및 기존 기본 스레드와1,216쌍 원시 이미지가 모두 일치합니다. 최신 검증본 실행기는 실행한 프로세스에만 설정을 적용합니다. 직접 실행은 루트 실행기의 `-SoftwareRasterThreads 4`로 선택하며 생략하면 드라이버 기본값을 유지합니다. [실행·범위·남은 제한](../artifacts/linux-driver-worker-budget-2026-11-15/README.ko.md). WSL 하드웨어 경로는 필수 합성 기능 미지원으로 적용하지 않았습니다.

Last synced: 2026-11-16

## 최신 배치 갱신 최적화 실행본 (2026-11-15)

[바로 실행하는 최신 NativeAOT 가이드](../artifacts/linux-sparse-damage-2026-11-15/README.ko.md)를 사용하세요. 슬라이더의105개 도형 배치에서 변하지 않는 눈금까지 갱신하던 범위를 개별 도형·채움 경계로 좁혔습니다. 실제 갱신 면적은 단독 슬라이더92.36%, Fluent46.67~48.68% 감소했습니다. 진단 고정 Fluent CPU11.54% 감소를 확인했고 다른 CPU·입력 지연은 구간 중첩으로 개선율을 주장하지 않습니다. Linux RC3 NativeAOT와 Windows의 독립 전체 렌더링14248개 검사·2424픽셀 쌍,33화면·384입력·9정지·21복원이 통과했습니다. 실제 사용자 체감·물리 표시 지연과 이전 간헐적 입력 확인 실패의 원인은 미확인입니다. 아래 기록은 개별 과거 체크포인트입니다.

## 최신 연속 입력 수정 실행본 (2026-11-15)

[검증한 NativeAOT 실행 가이드](../artifacts/linux-animation-pacing-2026-11-15/README.ko.md)를 사용하세요. 공통 애니메이션의 과다 구성을 수정했고33페이지·384입력·9정지 조건을 확인했습니다. 아래 기록의 이전 바이너리와 구분합니다. 물리 표시 지연과 사용자 체감은 아직 미확인입니다.

2026-11-14 [Linux Implementation Observer](../samples/Duxel.Gallery.Linux/README.ko.md) : Windows와 같은 화면 소스를 사용하며, 별도 Linux 호스트에서 X11/Vulkan으로29개 탭의 표시 요청과 정상 종료를 확인했습니다. 전역 갱신 관찰과 측정 재사용 개선을 함께 확인할 수 있습니다. 기존 전체 접근성 검증의 미해결 범위와 구분합니다.

## Markdown 이미지의 Component 스크롤 (2026-11-15)

해당 이미지 스크롤 검증 시점의 고정 RC3 NativeAOT 옵저버는 `/home/dimohy/.cache/duxel/observer-image-component-rc3-aot-20261115/publish/Duxel.Gallery.Linux`입니다. 실행기를 통한32개 화면 표시·384회 네이티브 입력과 정상 종료를 확인했습니다. 기존 성능 기준 실행물은 보존했으며 새 비교 성능 향상을 주장하는 검사는 아닙니다.


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

## 네이티브 접근성

공통 텍스트 모델이 변경할 수 없는 확정 텍스트·커서·선택 스냅샷을 게시하고 Unicode 스칼라/UTF-16 변환과 선택 요청을 검사합니다. 샘플 맨 아래 표시는 같은 렌더 프레임의 상태를 읽습니다. 네이티브 Text에서 문자·커서·선택의 조회와 변경을 부분 지원합니다. 문자·실제 줄 단위 조회와 일반 편집기 좌표를 연결했으며 검사한 범위 편집과 클립보드도 연결했습니다. 단어·문장·문단은 Pango를 사용하며 원문·언어가 같으면 경계 표 하나를 유지합니다. 이전 경계 조회와 일반 입력의 글꼴·색·크기 속성을 연결했으며 전체 스타일 편집은 남아 있습니다. [공통 텍스트 계약](accessibility-text.ko.md)을 참고하세요.

`DuxelLinuxApp.Run`은 AT-SPI를 기본 활성화하며 하위 `LinuxX11PlatformOptions.Accessibility`는 직접 지정할 때 사용합니다. 연결 가능한 세션 D-Bus·`org.a11y.Bus`·접근성 레지스트리와 GIO·GLib·GObject·Pango1.0 ABI(`libpango-1.0.so.0`)가 필요합니다. 버스를 만들기 전에 Pango 지원을 확인하며 없으면 명시적으로 실패합니다. 이번 검증 버전은1.52.1입니다. 서비스가 없으면 명시적으로 실패합니다. 의도적으로 끄려면 다음과 같이 지정하세요.

```csharp
using Duxel.Platform.Linux;
DuxelLinuxApp.Run(options, session, new LinuxAtSpiOptions { Enabled = false });
```

Accessible·Application·Component·Action·Cache·Value·Selection·Text·EditableText를 부분 구현했습니다. 전용 GIO 연결·컨텍스트·스레드가 네이티브 콜백을 처리하며 Xlib는 호출하지 않습니다. 소유 스레드의 좌표를 원자적으로 게시하고 동작은 공통 렌더 스레드 큐로 전달합니다. 네이티브 포커스는 실제 X11 키보드 포커스도 얻습니다. 소유한 연결을 닫으면 데스크톱 등록도 제거됩니다. CallTimeoutMilliseconds의 기본값은5000이며1부터60000까지 허용합니다.

숫자 컨트롤은 기존 범위 동작으로 경계·현재 값·증분·설명과 반영된 변경 이벤트를 제공합니다. 잘못된 값·유한하지 않은 값·읽기 전용·비활성 쓰기는 값을 바꾸지 않고 실패합니다. 진행률은 보정한 실제 목표 값을 게시하고 보간 값은 범위 이벤트로 보내지 않습니다. 미확정 모드에서는 식별자를 유지하며 Value를 제거하고 외부 캐시에도 인터페이스 변경을 전달합니다. [진행률 계약](fluent2-progress.ko.md)을 참고하세요.

선택은 전체 자식 인덱스와 선택된 항목의 인덱스를 구분합니다. 단일 목록은 교체하고 다중 목록은 추가합니다. 없는 선택 항목을 조회하면 네이티브 null 참조를, 잘못된 변경에는 false를 반환합니다. 부모 선택 이벤트는 자식 상태 이벤트 뒤에 전달합니다. 필수 소유자는 마지막 선택 항목을 보호하고 전체 해제를 거부합니다. ComboBox는 실제 팝업 ListBox를 유지하고 그 목록에서 네이티브 Selection을 제공합니다. 일괄 요청은 알려진 필수·비활성 거부 조건을 먼저 확인한 후 현재 바인딩에 순서대로 반영합니다. 이후 거부나 계층 변경이 발생하면 false로 중단하고 앞선 변경은 유지하며 트랜잭션은 제공하지 않습니다. [Selection 프로토콜](https://github.com/GNOME/at-spi2-core/blob/main/xml/Selection.xml)과 [목록 계약](fluent2-list.ko.md)을 참고하세요.

`UiAccessibilityTree.CaptureSnapshot()`은 한 리비전의 복사본을 반환하며 이후 게시·해제에도 유지됩니다. `Published`는 변경된 프레임 게시 후에만 발생하고 콜백 중 재게시·해제는 거부합니다. `Button(id, label, size)`는 문구가 바뀌어도 식별자를 유지합니다. 기존 한 줄 입력은 접근성 포커스를 받고 유지합니다. 접근성이 필요한 즉시 진행률은 명시적 id 오버로드를 사용하며 기존 ID 없는 그리기는 의미 식별자를 만들지 않습니다.

현재 관리형 .NET11/Linux x64 검사736건(앱/세션54·경계/배치664·폰트 선택18), 명시적·등록 실행 경로별 독립 네이티브146건·문서·입력103건·기본/RTL/가상 상위 이동61건씩, 선택 정책7건·숫자 정책5건·클립보드35건과 별도 Windows 공통 컨트롤227건이 통과했습니다. 640×520 샘플에는 입력·PNG/SVG·정수 슬라이더/진행률과 단일·다중 도시 목록이 있습니다. 버스 시작 실패8회에서는 소유 스레드를 정리하고 준비 실행 후 시도별 디스크립터 증가가 없었습니다. 현재 소스·바이너리 근거는 `artifacts/linux-accessibility-2026-11-14/rtl-virtual-text-scroll-verification.json`입니다. 설치된 libatspi setter가 오류 응답에서 종료되므로 숫자 거부 검사는 독립 gdbus를 사용합니다. [공식 setter 소스](https://github.com/GNOME/at-spi2-core/blob/main/atspi/atspi-value.c)에도 null 확인 없이 응답을 해제하는 경로가 있으며 제품 오류는 유지합니다.

전체 위젯·가상화·실제 화면 읽기 프로그램, Text/EditableText·표/그리드/Collection·남은 관계와 현지화 검증은 남아 있습니다. DPI·물리 IME/조합·표시 서버/네이티브 RPC 복구·고급 창 기능·하드웨어/ARM64/Pi·고정 RC3 NativeAOT도 별도 조건입니다. 아홉 인터페이스의 부분 구현이 전체 접근성·플랫폼 완료를 뜻하지는 않습니다.

`Duxel.Platform.Linux`는 기존 `IPlatformBackend`·`IInputBackend`·`IPlatformEventWakeBackend`·`IVulkanSurfaceSource` 계약을 구현합니다. X11 연결과 창 하나를 소유하며 공통 Vulkan 렌더러를 사용합니다. 솔루션에 포함했으며 아직 패키지로 배포하지 않습니다.

클라이언트·프레임버퍼 크기 알림, UTF-8 창 제목, 닫기·표시·숨김, 포인터 버튼·독립 휠 축, XKB 키보드·반복 설정, XIM을 통한 확정된 UTF-8 입력, 포커스 이탈 시 키 해제와 네이티브 커서를 구현했습니다. XIM의 조합·상태 표시는 입력기에 맡기며 인라인 조합은 미구현입니다. 창 조작은 생성 스레드에서 수행합니다. `WakeEvents`만 스레드 간 호출을 지원하며 eventfd로 미리 전달한 신호도 유지합니다. 작업 스레드에서 Xlib를 호출하지 않습니다. 해제 시 깨우기 파일 디스크립터를 동기화해 닫고 보관된 표면 생성기도 무효화합니다.

Vulkan 바인딩은 Windows의 `vulkan-1.dll` 또는 Linux의 `libvulkan.so.1`을 명시적으로 선택합니다. 87개 호출이 프로세스 수명의 지연 로더를 공유합니다. 장치나 렌더링의 대체 경로는 추가하지 않았습니다. X11 표면에는 `VK_KHR_surface`·`VK_KHR_xlib_surface`가 필요하며 기존 렌더러의 장치 기능 요구와 셰이더를 유지합니다.

Duxel은 Vulkan 장치를 만들기 전에 선택한 장치에서 지원하지 않는 필수 기능의 이름을 오류에 표시합니다. `Selected Vulkan device is missing required features: dualSrcBlend.`는 해당 장치가 `dualSrcBlend`를 지원하지 않는다는 뜻입니다. WSL의 Dozen에서는 GPU를 찾더라도 이 기능을 제공하지 않을 수 있습니다.

X11에서는 `Duxel.Linux.App.Tests startup-retry <기본 글꼴> <보조 글꼴>`을 실행하세요. Vulkan 표면을 만든 직후, 클립보드를 설정하는 중, 첫 프레임을 표시한 뒤 종료 로그를 남기는 중에 각각 예외를 발생시킵니다. 검증기는 원래 예외가 전달되고 이전 글꼴 로그 콜백이 복원되는지 확인한 뒤, 같은 세션에서 다시 화면을 표시하고 종료합니다. Windows에서는 `Duxel.Graphics.Tests startup-retry <기본 글꼴> <보조 글꼴>`로 같은 검사를 실행하며, 매번 Win32 창 핸들이 해제됐는지도 확인합니다.

64비트 Linux, 사용 가능한 X11 DISPLAY, libX11.so.6, XKB의 반복 키 감지, 환경에 맞는 입력 로캘·XIM과 libvulkan.so.1이 필요합니다. 초기화에 실패하면 명시적으로 알리고 확보한 자원을 해제합니다. Linux LP64 ABI를 구현했으며 현재 실행 근거는 ARM64가 아닌 x64입니다. 표시 서버·로캘·장치 기능이 없을 때 모의 백엔드로 바꾸지 않습니다.

2026-11-13 Ubuntu24.04.3/WSL x64의 관리형 .NET11.0.0에서31건이 통과했습니다. 외부 X11 이벤트 전달, 실제 표면의 표시 지원 큐, 미리 전달한 신호·작업 스레드의 깨우기와 공통 렌더러의 기본 도형3프레임 제출·표시를 확인했습니다. 실제 드라이버는 **llvmpipe**이므로 소프트웨어 드라이버의 기능 근거이며 하드웨어·Pi 성능이 아닙니다. Windows 로더4건과 기존 기본 도형 GPU74건도 통과했고 Windows에서는 인스턴스·장치의 Khronos 검증 레이어를 켰습니다. Ubuntu 로더에는 해당 레이어가 노출되지 않아 Linux 검증 레이어 실행으로 보고하지 않습니다. 표시 성공만으로 Linux 픽셀 일치를 입증하지 않습니다.

PowerShell에서 검증한 전용 창 검사를 실행합니다. 결과 디렉터리는 새 경로를 사용하세요.

```powershell
wsl.exe -d Ubuntu --exec timeout 25s /mnt/l/Cache/Duxel/linux-platform-2026-11-13/publish-qualified/Duxel.Platform.Linux.Tests /mnt/l/Cache/Duxel/linux-platform-2026-11-13/user-result render
```

별도 Linux 실행물은 .NET11.0.0을 포함하며 Ubuntu의 전역 SDK와 저장소 RC3 고정값은 바꾸지 않습니다. 소스 빌드는 `tests/Duxel.Platform.Linux.Tests`, `DuxelBenchmarkFramework=net11.0`, `DuxelSourceTargetFramework=net11.0`과 기존 LocalSource.targets 지정을 사용합니다. 근거·소스 해시는 `artifacts/linux-platform-2026-11-13/verification.json`에 있습니다.

`LinuxPlatformTextBackend`에 텍스트 측정, 픽셀 없이 경계 계산, RGBA 커버리지 출력, 폰트 메트릭과 기존 아틀라스 글리프 래스터 계약을 연결했습니다. X11 창이 각 백엔드를 소유하고 해제합니다. 아틀라스 팩터리는 프로세스 수명의 별도 서비스를 사용하므로 창을 닫아도 해제된 래스터라이저가 전역에 남지 않습니다. 두 서비스는 각각 명시한 파일·크기별 폰트 자원을 최대32개 보관하고 먼저 들어온 항목부터 참조를 해제합니다. Cairo 자체 캐시가 네이티브 폰트 참조를 보관할 수 있으므로 FreeType의 face·library는 Cairo의 마지막 참조가 해제될 때 콜백으로 정리합니다. 백엔드 수명 중 폰트 파일은 변경하지 않습니다.

텍스트에는 FreeType·Cairo·HarfBuzz·Pango>=1.48·PangoCairo·PangoFc·Fontconfig·GLib/GObject가 필요합니다. 명시적으로 받은 불변 폰트 파일만 전용 맵에 등록하고 조판된 각 런의 실제 파일·인덱스를 대조합니다. 전역 폰트 설정이나 암묵적인 대체 폰트를 사용하지 않습니다. Pango가 완성된 행의 스크립트와 순서를 계산하고 기존 Cairo 회색조 렌더러·HarfBuzz 윤곽 모듈이 그 글리프 위치를 공유합니다. 잘못된 UTF16은 실패로 알리며 탭은 공백 네 개로 펼치고 CRLF·CR·LF는 줄바꿈으로 처리합니다. [현재 위치 기능과 검증 범위](linux-text-shaping.ko.md)를 확인하세요. 혼합 스타일 전체 문단·줄바꿈과 기준 방향 통합·이형 문자 시퀀스·컬러 폰트는 남아 있습니다.

두 폰트와 새 결과 디렉터리를 지정해 텍스트·아틀라스·공통 렌더러 검사를 실행합니다.

```powershell
wsl.exe -d Ubuntu --exec timeout 35s /mnt/l/Cache/Duxel/linux-platform-2026-11-13/publish-qualified/Duxel.Platform.Linux.Tests /mnt/l/Cache/Duxel/linux-platform-2026-11-13/user-text-result text /usr/share/fonts/truetype/dejavu/DejaVuSans.ttf /mnt/c/Windows/Fonts/malgun.ttf
```

두 번째 경로는 이 호스트의 Windows 폰트를 검사에만 사용한 것입니다. Linux 백엔드에 포함하거나 필수로 요구하지 않습니다. 두 경로 모두 검사 문자를 포함하는 설치된 폰트로 직접 지정할 수 있습니다. 검사는 PGM 커버리지 이미지·정형 결과를 저장하고 자신이 연 임시 창만 닫습니다. 관리형 .NET11 Linux 결과와 해시는 `artifacts/linux-platform-2026-11-13/text-verification.json`에 별도로 기록합니다. 텍스트 제출·표시 성공만으로 GPU 픽셀 일치를 입증하지 않습니다.

같은 백엔드에 `IPlatformVectorTextBackend`·`IPlatformFontVariantBackend`를 연결했습니다. 벡터 윤곽 추출에는 **HarfBuzz7 이상**이 필요하며8.3.0에서 검사했습니다. 이미 읽은 OpenType 폰트의 이차·삼차 곡선 콜백, 오차에 따른 곡선 분할과 LibTessDotNet1.1.15의 nonzero 채우기 규칙을 사용합니다. 의존성 버전을 고정했으며 글자 내부의 구멍을 단일 윤곽 삼각분할이나 비트맵 근사로 처리하지 않습니다. 파일·크기·시뮬레이션·글리프·오차별로 변경하지 않는 인덱스 버퍼를 최대512개 공유합니다. 캐시 퇴출과 백엔드 해제는 이미 반환한 관리형 버퍼를 무효화하지 않습니다. 콜백 오류는 저장했다가 네이티브 호출이 끝난 후 다시 던집니다. 백엔드는 변경하지 않는 네이티브 콜백 테이블을 소유하고 해제합니다.

`GetFontVariantResource(path, PlatformFontSimulation.Oblique)`는 반복 호출해도 같은 자원 키를 반환합니다.12도 기울임을 Cairo와 HarfBuzz에 동일하게 적용하며 결합 문자 위치도 반영합니다. 키는 백엔드의 텍스트·경계·메트릭·메시 메서드에서 사용하며 파일 경로는 아닙니다. `None`은 기존 플랫폼 계약에 맞춰 입력 자원을 그대로 유지합니다. 기본 폰트와 기울인 폰트는 별도 캐시 항목으로 관리합니다.

```csharp
using var text = new LinuxPlatformTextBackend();
var oblique = text.GetFontVariantResource(fontPath, PlatformFontSimulation.Oblique);
var mesh = text.CreateTextMesh(new(oblique, secondaryFontPath, "Hello 한글", 24));
```

같은 두 폰트를 명시해 벡터 검사를 실행합니다.

```powershell
wsl.exe -d Ubuntu --exec timeout 40s /mnt/l/Cache/Duxel/linux-platform-2026-11-13/publish-qualified/Duxel.Platform.Linux.Tests /mnt/l/Cache/Duxel/linux-platform-2026-11-13/user-vector-result vector /usr/share/fonts/truetype/dejavu/DejaVuSans.ttf /mnt/c/Windows/Fonts/malgun.ttf
```

관리형 .NET11/Linux x64에서 벡터73건·래스터87건·플랫폼31건이 통과했습니다. TrueType·CFF·한글·결합 문자·기울임의 여덟 사례를 독립적인 Cairo 래스터 출력과 비교했으며 내부51,933개 지점이 모두 일치했습니다. 직접 작성한 재현 가능한 CFF 폰트로 삼차 곡선을 검사합니다. 폰트 생성기는 명시적으로 실행하며 테스트가 자동 실행하지 않습니다. 기존 검증 전용 Vulkan 픽셀 읽기 기능으로 실제 인덱스 텍스트 이미지 여섯 쌍을 기준 삼각형과 비교하고, 처리 중인 프레임2/3개 사이의 이미지 세 쌍도 비교해 모두 바이트 단위로 일치했습니다. 기본·회전 장면을 검사합니다. 이는 llvmpipe/MSAA1의 기능 비교이며 하드웨어·Pi 성능, 래스터 안티앨리어싱 전체 일치나 NativeAOT 검증을 뜻하지 않습니다. 픽셀을 읽기 위해 제품 Vulkan 코드나 렌더링 품질을 바꾸지 않았습니다. 입력·출력·해시는 `artifacts/linux-platform-2026-11-13/vector-verification.json`에 있습니다.

`Duxel.Linux.App`에 `DuxelLinuxApp.Run(options[, session])`과 뷰·폰트를 받는 실행 메서드를 추가했습니다. 기존 `DuxelAppSession.RunCore`를 재사용하고 X11 플랫폼을 소유하며 창 생성·프레임 갱신 콜백을 연결합니다. 공통 렌더 스레드가 끝난 후 네이티브 자원을 해제합니다. 어셈블리가 로드되면 공통 실행기를 등록하며 일반 진입점으로는 Linux 전용 실행 메서드를 사용합니다. 뷰를 받는 메서드는 기본적으로 V-Sync를 끄고 이벤트를 기다립니다. 직접 전달한 옵션의 V-Sync·프레임 설정은 유지합니다. 폰트 역할마다 실제 파일을 지정해야 하며 Windows 기본 폰트로 조용히 대체하지 않습니다.

공통 이벤트 계약도 명확히 했습니다. `WaitEvents(0)`은 무기한 기다리고 대기 중인 입력을 전달한 후 반환합니다. 양수 시간 제한도 기다린 후 이벤트를 전달합니다. 직후 별도로 `PollEvents`를 호출하면 다음 입력 프레임이 시작되며 일회성 입력 상태가 지워집니다. Linux 구현을 이 계약에 맞췄고 확정 입력 전달·시간 제한0에서 작업 스레드로 깨우는 회귀 검사를 추가했습니다. 선택적인 초기 중앙 배치는 실제 기본 X11 화면 크기를 사용합니다. 이후 창 관리자가 위치를 바꿀 수 있습니다.

PowerShell에서 대화형 샘플을 실행합니다.

```powershell
wsl.exe -d Ubuntu --exec /mnt/l/Cache/Duxel/linux-accessibility-2026-11-14/publish-sample/Duxel.Linux.Sample /usr/share/fonts/truetype/dejavu/DejaVuSans.ttf /mnt/c/Windows/Fonts/malgun.ttf
```

샘플은 종료 버튼이나 창의 닫기 버튼을 누를 때까지 열려 있습니다. 한글·영문, 클릭 수 버튼, 텍스트 입력과 UI 프레임 수를 표시합니다. 프레임 수 표시는 스스로 갱신을 요청하지 않습니다. 입력창에 포커스가 있으면 커서 깜빡임을 위한 프레임이 생길 수 있습니다. 소스는 `samples/Duxel.Linux.Sample`이며 빌드·실행 방법은 한영 README에 있습니다. 이 간단한 샘플은 전달받은 두 폰트를 모든 역할에 명시적으로 사용합니다. 앱에서는 `DuxelFontOptions.Typography`에 기본·중간 굵기·굵은 글씨·고정폭 폰트를 각각 지정할 수 있습니다.

실제 .NET11/Linux 앱 검사17건이 **검증 훅 없는 제품 렌더러**에서 통과했습니다. 개별 세션과 등록된 기본 세션 모두 보이는 창에서400ms 동안 추가 UI 프레임 없이 대기하고, 작업 스레드의 요청으로 다시 그리며, 실제 X11 버튼·XIM 영문 입력을 받고 세션 종료·창 닫기로 끝납니다. 네이티브 창 이미지는 표시 상태와 화면 안의 위치를 확인한 후에만 읽습니다. WSLg는 새 창을 일시적으로 숨기거나 화면 밖에 배치할 수 있습니다. 별도 배포한 샘플도 기존 자동 종료 검사 설정으로 시작·종료를 확인했습니다. 시간 제한0의 대기·입력 전달과 숨긴 창의 초기 중앙 배치를 포함한 플랫폼 회귀34건도 통과했습니다. 근거는 `artifacts/linux-app-2026-11-13/verification.json`에 있습니다.

이 실험적 실행기는 솔루션 안에서 사용하며 아직 패키지로 배포하지 않습니다. 사용자 지정·확장 제목 표시줄, 소유 창, 트레이, 창 아이콘, 최소·고정 크기와 최소화·최대화 버튼 숨김은 창을 만들기 전에 미지원으로 알립니다. 이 통합은 아직 남아 있습니다. 전체 타이포그래피, 앱 종료 후 클립보드 보존·이미지·전체 접근성 통합, DPI, 실제 IME·조합, 표시 서버 연결 끊김 처리, 추가 입력·수명 검사, Linux 검증 레이어와 ARM64·Pi 실행도 남아 있습니다. 새 삼각분할 의존성도 원래 고정 RC3 NativeAOT·런타임 검증을 거쳐야 합니다. 이번 검사만으로 원래 Linux·ARM64 또는 전체 제품 게이트를 완료하지 않습니다. 검사 때문에 일반 Observer나 사용자 창을 닫지는 않았습니다.

`LinuxX11Clipboard`는 `libxcb.so.1`을 사용해 `IUiClipboard`를 구현합니다. 전용 연결·스레드가 보관한 텍스트를 전달하며 동기 읽기는 별도 임시 연결을 사용합니다. 기본 창이 이벤트를 기다리는 동안에도 클립보드 요청을 처리합니다. 생성만으로 selection을 읽거나 소유하지 않습니다. Linux 실행기는 `Clipboard`와 `ClipboardFactory`가 모두 없을 때만 기본 서비스를 생성·해제합니다. 직접 전달하거나 팩터리로 제공한 서비스의 수명은 호출자가 관리합니다. 앱이 종료되면 selection 소유권을 해제하며 클립보드 관리자를 통한 종료 후 보존은 구현하지 않았습니다.

```csharp
using var clipboard = new LinuxX11Clipboard();
clipboard.SetText("Hello 한글 😀");
var text = clipboard.GetText();
```

`LinuxX11ClipboardOptions`의 기본값은 `CLIPBOARD`, 현재 `DISPLAY`, 전송 제한 시간5초, 텍스트 상한16MiB, 청크64KiB입니다. 읽기·송신 중 전송·쓰기 대기열은 각32개로 제한합니다. 검사에서는 `Selection`에 별도 atom을 지정하세요. `TARGETS`로 인코딩을 협상하며 UTF8_STRING·text/plain;charset=utf-8·TEXT를 제공합니다. STRING은 모든 문자가 Latin1으로 표현될 때만 제공합니다. TIMESTAMP와 상한이 있는 MULTIPLE 변환을 지원합니다. 큰 데이터는 INCR로 전달하며 UTF-8 문자 중간에서 청크가 나뉘어도 처리합니다. 잘못된 문자·예고한 변환 거부·손상되거나 너무 큰 데이터·시간 초과는 명시적 오류입니다. selection이 없거나 지원하는 텍스트 형식이 없으면 빈 문자열을 반환합니다. Dispose는 이벤트 대기 중인 읽기를 깨우고 소유한 작업을 기다립니다. 네이티브 서버 호출은 응답하는 표시 서버가 필요하며 완전한 연결 끊김·정지 복구는 미검증입니다. 클립보드 이미지·COMPOUND_TEXT·Wayland 전용 전송은 미구현입니다.

별도 텍스트 검사는 독립 Xlib/LP64 상대와35건을 통과했습니다. 양방향 전송·240000바이트 INCR·TARGETS/TIMESTAMP/MULTIPLE·요청자 종료·시간 초과·해제 시 취소를 확인했습니다. 실제 앱 검사21건은 두 세션 경로에서 Ctrl+A/C/Ctrl+V로 공통 입력 위젯의 선택 영역을 한글·이모지로 교체하고 직접 전달한 클립보드·팩터리의 수명을 보존하는지 확인합니다. 자동 검사는 모두 새 별도 selection을 사용하며 사용자의 CLIPBOARD·PRIMARY를 읽거나 쓰지 않습니다. 근거와 현재 해시는 `artifacts/linux-clipboard-2026-11-13/verification.json`에 있습니다. 한영 샘플 README의 갱신된 실행물에서 입력란의 Ctrl+A/C/V 안내를 따라 확인할 수 있습니다. 관리형 .NET11/x64/WSLg 기능 근거이며 하드웨어·ARM64·고정 RC3 검증은 남아 있습니다.

클립보드 근거: [ICCCM selection](https://www.x.org/releases/X11R7.7/doc/xorg-docs/icccm/icccm.html), [XCB API](https://xcb.freedesktop.org/manual/group__XCB____API.html).

Linux 실행기의 기본 `IUiImageDecoder`는 `LinuxUiImageDecoder.Shared`이며 직접 지정한 디코더는 유지합니다. 설치된 `libgdk_pixbuf-2.0.so.0`·`libgobject-2.0.so.0`·`libglib-2.0.so.0` 코덱으로 PNG/JPEG/GIF/BMP/ICO를 읽습니다. 독립된 RGBA8 버퍼로 복사한 후 네이티브 이미지·오류를 해제합니다. RGB 행의 채움 바이트는 제외하고 불투명 알파를 추가하며 RGBA는 미리 곱하지 않은 색을 유지합니다. GIF는 첫 정지 프레임을 읽습니다. ICO는 DIB·내장 PNG 모두 디렉터리의 첫 항목을 읽습니다. 검사한 GdkPixbuf ICO 코덱은 PNG 압축 아이콘을 거부하므로 디코딩 전에 이 인코딩을 구별해 내장 프레임을 PNG 로더에 직접 전달합니다. 실패한 디코딩을 다른 백엔드로 재시도하지 않습니다. 누락·손상 파일과 사용할 수 없는 코덱은 명시적 오류입니다. `CanDecode`는 허용 확장자를 알리며 설치된 각 코덱을 검사하지는 않습니다.

SVG는 GdkPixbuf SVG 플러그인 대신 기존 `SvgStore`·`SvgImageDecoder`·`UiSvg` 경로를 사용합니다. `Duxel.Svg`는 자체 SVG 처리와 Linux HarfBuzz 텍스트 자산을 사용하며, Windows에서는 DirectWrite·WIC를 사용합니다. Linux 샘플은 네이티브 PNG, SVG 비트맵, 전용 SVG 위젯을 표시합니다. SVG 회전 버튼은15도씩 바꾸며 처리한 장면과 기존 해상도의 래스터를 재사용합니다. 공통 이미지 파일 API에서 두 형식을 받으려면 명시적으로 구성합니다.

```csharp
using var store = new SvgStore();
var options = DuxelApp.Options(screen) with
{
    ImageDecoder = new SvgImageDecoder(store, LinuxUiImageDecoder.Shared)
};
DuxelLinuxApp.Run(options);
```

위에서 설명한 명시적 폰트 옵션도 함께 지정하세요. 세션과 SVG를 사용하는 위젯을 해제할 때까지 저장소를 유지합니다. 기본 실행기는 비트맵 디코더만 연결하며 SVG 구성은 명시적으로 선택합니다. 매 프레임 파일을 디코딩하지 말고 한 번 읽은 이미지·텍스처를 유지하세요. `SvgStore`는 최초 XML 처리 후 장면과 래스터를 보관합니다.

관리형 .NET11/Linux 이미지 검사63건이 통과했습니다. 독립 PNG/RGB/BMP/GIF/JPEG/ICO 픽셀·홀수 너비 행·알파·여러 아이콘 프레임·한글 파일명·잘못된 입력·동시 실행 수명·공개 PNG/SVG API를 확인합니다. SVG를20회 반복 디코딩해도 파일 읽기·XML 처리·장면 구성·래스터화는 각1회입니다. 갱신된 제품 앱25건에서는 실제 X11 창의 PNG·SVG 비트맵·위젯 픽셀을 비교하고 회전할 때 XML·래스터 작업이 늘지 않는지 확인했습니다. 기존 입력·클립보드·대기 검사도 유지했습니다. 기본 서비스를 쓰는 독립 샘플도 명시적 자동 종료 검사 모드에서 정상 종료했습니다. `artifacts/linux-images-2026-11-13/verification.json`과 갱신한 한영 샘플 README를 참고하세요. 첫 프레임 디코딩은 애니메이션 재생·자동 EXIF 방향 보정·전체 색 프로필/HDR 처리나 전체 플랫폼 완료를 뜻하지 않습니다. 전체 접근성·하드웨어/ARM64·고정 RC3 NativeAOT는 남아 있습니다.

이미지 근거: [GdkPixbuf 파일 읽기](https://docs.gtk.org/gdk-pixbuf/ctor.Pixbuf.new_from_file.html), [읽기 전용 픽셀](https://docs.gtk.org/gdk-pixbuf/method.Pixbuf.read_pixels.html), [행 간격](https://docs.gtk.org/gdk-pixbuf/method.Pixbuf.get_rowstride.html), [Linux Skia 자산](https://www.nuget.org/packages/SkiaSharp.NativeAssets.Linux.NoDependencies/4.148.0).

참고: [Xlib 표준](https://xorg.freedesktop.org/archive/current/doc/libX11/libX11/libX11.html), [Vulkan Xlib 표면 계약](https://docs.vulkan.org/refpages/latest/refpages/source/VkXlibSurfaceCreateInfoKHR.html).

텍스트 참고: [Cairo FreeType 소유권](https://www.cairographics.org/manual/cairo-FreeType-Fonts.html), [Cairo 글리프 API](https://www.cairographics.org/manual/cairo-text.html), [HarfBuzz OpenType 함수](https://harfbuzz.github.io/harfbuzz-hb-ot-font.html), [HarfBuzz 폰트 크기](https://harfbuzz.github.io/harfbuzz-hb-font.html).

벡터 참고: [HarfBuzz 그리기 콜백](https://harfbuzz.github.io/harfbuzz-hb-draw.html), [폰트 기울임](https://harfbuzz.github.io/harfbuzz-hb-font.html#hb-font-set-synthetic-slant), [LibTessDotNet1.1.15 소스·라이선스](https://github.com/speps/LibTessDotNet/tree/v1.1.15).

## 이전 텍스트 경계 조회 (2026-11-14)

GetTextBeforeOffset·GetTextAtOffset·GetTextAfterOffset에서 AT-SPI 경계 일곱 종류를 지원합니다. 문자 스칼라, 단어 시작/끝, 문장 시작/끝, 실제 그려진 줄 시작/끝입니다. 기존 Pango 처리에서 단어·문장 끝도 함께 수집하고 같은 원문/언어의 변경 불가능 표를 재사용합니다. 문자는 Unicode 스칼라 하나이며 편집 시 글자소 경계 검사와 구분합니다. 잘못된 위치/종류는 명시적으로 거부합니다. 시작점 범위는 구분자를 뒤에, 끝점 범위는 앞에 포함합니다. 실제 끝 경계가 문서 끝이면 현재 범위는 비어 있고, 마지막 구분자가 남아 있으면 그 구간을 유지합니다.

UiAccessibilityTextLayout의 LineCount·GetLine(index)는 스칼라 원문 좌표의 변경 불가능 UiAccessibilityTextLine(Start, End, NextStart)을 제공합니다. End는 구분자를 제외하고 NextStart는 포함하며 자동 줄바꿈이면 End == NextStart입니다. 기존 GetLineRangeAt의 구분자 포함 계약은 유지합니다. 정확히 자동 줄바꿈 위치이면 다음 그려진 줄을 선택하는 반개구간 규칙입니다. 스타일/조합/기존 줄바꿈 없는 경로에서 좌표가 없으면 여전히 NotSupported를 반환하며 논리 줄 분할로 대체하지 않습니다.

현재 .NET11/Linux x64 검사93건은 앱/세션38건과 경계/배치55건입니다. 실제 Linux 여러 줄 그리기, 빈 줄·마지막 줄·자동 줄바꿈과 재배치 후 원래 스냅샷 보존을 포함합니다. 실제 한 줄 입력의 독립 libatspi 검사는 실행 경로별127건이며 공통 Windows 컨트롤214건·클립보드35건은 별도입니다. 여러 줄 경계는 모듈 검사이며 여러 줄 네이티브 RPC나 물리 화면 읽기 프로그램의 검증은 아닙니다. 근거는 `artifacts/linux-accessibility-2026-11-14/text-size-verification.json`입니다. 텍스트 속성·전체 스타일/조합 편집·제한 범위/스크롤·물리/하드웨어/NativeAOT 검증은 남아 있습니다.

## 단일 색 편집기 그리기 속성 (2026-11-14)

UiAccessibilityNode.TextAppearance는 공통 한 줄/여러 줄 편집기의 실제 그리기에서 변경 불가능 UiAccessibilityTextAppearance(Content, Foreground)를 게시합니다. 확정 원문의 색이며 안내 문구·선택·캐럿이나 배경과 합성된 픽셀의 색이 아닙니다. 내용/색이 같으면 같은 객체를 재사용하고 색이 바뀌면 새 스냅샷을 만듭니다. 기존 트리 항목에 수집 프레임을 함께 보관하므로 사용자 정의 표시가 원문 그리기를 생략하면 이전 프레임의 속성을 물려받지 않습니다. 원문 재조정이나 늦은 숫자 확정에서 Content가 달라지면 이전 속성을 제거합니다. 스타일/Markdown과 조합 중에는 단일 색 속성을 게시하지 않습니다.

AT-SPI GetDefaultAttributes/GetDefaultAttributeSet은 수집한 전경색을 fg-color로 제공하며 8비트 채널에257을 곱한 부호 없는16비트 RGB를 사용합니다. GetAttributes·GetAttributeRun(false)는 [0, CharacterCount) 범위의 명시 속성 집합을 비워 반환하고 GetAttributeRun(true)는 기본 속성을 포함합니다. GetAttributeValue는 기본 전경색을 조회하며 모르는 이름이면 빈 문자열을 반환합니다. 빈 원문의 범위는 [0,0)이고 문서 끝 위치는 허용하며 잘못된 위치는 거부합니다. 그리기 정보가 없으면 명시적으로 NotSupported를 반환합니다. 현재는 단일 전경색 계약이며 폰트 이름/굵기/포인트 크기·배경/알파·스타일별 범위·맞춤법/언어와 속성 변경 알림은 남아 있습니다. 경로에서 폰트 이름을 추정하지 않습니다.

현재 관리형 .NET11/Linux x64 검사93건은 앱/세션38건과 경계/배치55건이며 독립 네이티브 클라이언트는 실행 경로별134건입니다. 실제 기본/범위/이름 조회·한글/이모지의 스칼라 위치·빈 원문을 검사합니다. 새 Windows 컨트롤214건에는 원문/색 일치·재사용·실제 색 변경·SearchBox/Combobox 재조정·늦은 숫자 확정과 그리기 생략을 포함하며 클립보드35건은 별도입니다. 근거는 `artifacts/linux-accessibility-2026-11-14/text-size-verification.json`입니다. 전체 텍스트 속성이나 물리 화면 읽기 프로그램의 검증은 아닙니다. 계약: [AT-SPI 기본 속성](https://gnome.pages.gitlab.gnome.org/at-spi2-core/libatspi/method.Text.get_default_attributes.html), [ATK 속성 이름](https://gnome.pages.gitlab.gnome.org/gtk/atk/enum.TextAttribute.html).

원문 색 변경 알림 (2026-11-14): 유지 중인 텍스트 노드에서 게시하는 전경 RGB나 단일 색 그리기 정보의 제공 여부가 바뀌면 object:text-attributes-changed를 보냅니다. 원문/커서 객체만 바뀌면 보내지 않으며 게시하지 않는 알파 변경도 제외합니다. 소유한 검사 전용 래퍼에서 실제 편집기 색을 RGB17,83,201로 변경하고 일반 그리기로 복원합니다. 독립 libatspi는 원문/커서가 유지된 상태에서 변경마다 알림1건을 관찰하며 같은 색의 재그리기 확인 후200ms 동안 중복 알림이 없었습니다. 제품 샘플의 배치/동작은 유지합니다. 검사93건·실행 경로별 네이티브146건이며 공통 소스32개와 Core DLL이 같음을 확인해 컨트롤214건을 재사용하고 클립보드35건을 다시 검사했습니다. 근거는 artifacts/linux-accessibility-2026-11-14/text-size-verification.json입니다. 전경색 알림만 검증했으며 실제 선택 폰트 정보·스타일별 범위·물리 화면 읽기 프로그램의 전체 적합성은 남아 있습니다.
## 선택된 원문 폰트 정보 (2026-11-14)

LinuxPlatformTextBackend는 선택 기능인 IPlatformTextFontRunBackend.GetFontRuns(request, nameLanguage)를 구현합니다. 원본의 UTF16 위치를 기준으로 읽기 전용 PlatformTextFontRun(StartUtf16, EndUtf16, Font) 목록을 반환합니다. 실제 배치·래스터·벡터 그리기와 같은 SelectFont 규칙을 사용합니다. 보조 평면 문자는 UTF16 길이를 유지하고 CR/LF 구분자에는 그려진 폰트 범위를 만들지 않습니다. 원문 탭 하나에는 네 칸 공백으로 펼쳤을 때 사용하는 폰트를 선택합니다. 이는 글리프 조형 전의 폰트 선택이며 실제 배치/조형 검사는 별도입니다. 포인트 크기를 임의로 환산하지 않습니다.

PlatformFontMetadata는 OpenType 패밀리/하위 패밀리 이름(name ID1/2), OS/2 굵기·원래 기울임, 실제 합성 기울임 여부와 정규화한 요청 이름 언어를 담습니다. HarfBuzz의 공개 ABI로 실제 조형에 쓰는 불투명 폰트 객체의 테이블을 읽습니다. 그 언어의 이름이 없으면 null을 유지하며 영어나 파일 이름으로 대체하지 않습니다. OS/2 테이블이 없으면 해당 필드는 null이고 잘못되거나 잘린 정보는 명시적으로 거부합니다. 기존의 개수가 제한된 폰트 리소스마다 가장 최근에 요청한 언어의 정보를 보관합니다. 반환한 변경 불가능 값은 언어 변경이나 네이티브 해제 뒤에도 유지됩니다. 조회마다 원문 범위 목록은 새로 만들며 속도 향상은 주장하지 않습니다.

샘플은 기존 두 폰트 실행 명령에서 같은 창을 표시하기 전에 `정A`의 선택 폰트를 시작 시 한 번 출력합니다. 소유한 실행 검사에서는 Malgun Gothic·DejaVu Sans와 굵기400이 출력되었습니다. .NET11/Linux x64 검사111건은 앱/세션38·경계/배치55·폰트 선택/정보18건입니다. 독립 fontTools4.66.1이 실제 입력 파일에서 읽은 이름/OS2 값과 일치하며 SHA256·이름 기록은 font-runs-independent-oracle.json에 있습니다. HarfBuzz8.3.0·실행 경로별 네이티브146건·새 공통 컨트롤214건·클립보드35건이 통과했습니다. 근거는 `artifacts/linux-accessibility-2026-11-14/text-size-verification.json`입니다. 이 기반을 편집기/AT-SPI의 폰트별 범위로 게시하는 연결은 아직 미완료이며 포인트 크기 단위·스타일 문서·물리 검증도 남아 있습니다. 참고: [HarfBuzz 이름](https://harfbuzz.github.io/harfbuzz-hb-ot-name.html), [OpenType OS/2](https://learn.microsoft.com/en-us/typography/opentype/spec/os2).

편집기 폰트 속성 (2026-11-14): UiAccessibilityTextAppearance가 검증한 변경 불가능 스칼라 UiAccessibilityTextFontRun 목록과 FontRunCount/GetFontRun/GetFontRangeAt, 원본 PlatformTextFontRun 범위를 받는 생성자를 제공합니다. 구분자 간격은 유지하고 스칼라 분할·범위 겹침·잘못된 굵기/이름은 거부합니다. 공통 편집기 그리기는 원문·측정 설정·이름 언어가 바뀔 때만 선택적 직접 폰트 백엔드에 조회합니다. 같은 프레임 내용은 스냅샷을 재사용하고 색만 바뀌면 폰트 맵을 공유합니다. 실제 직접 측정/그리기와 같은 폰트 크기 계산을 사용하며 포인트 크기를 추정해 게시하지 않습니다. UiAccessibilityTree.FontNameLanguage의 초기값은 und이며 변경을 검사하고 프레임을 요청합니다. UiImmediateContext.SetAccessibilityFontNameLanguage로 설정할 수 있습니다. 샘플은 LinuxDemoScreen.FontNameLanguage에서 en을 명시하며 소유한 검사에서는 영문 대체 없이 ko 변경도 확인합니다. 네이티브 GetAttributes/GetAttributeRun은 실제 family-name/weight/style 명시 속성과 선택적인 fg-color 기본값, 스칼라 범위·그리지 않은 간격·문서 끝을 지원합니다. 게시한 속성이 같으면 인접 범위를 합칩니다. 원래 폰트와 합성 기울임을 구분하며 OS/2 bit9는 버전4부터 해석합니다. 원문이 같을 때 게시한 폰트 속성의 변경을 알리고 게시하지 않는 차이나 텍스트 변경의 중복 알림은 제외합니다. 실제 이름 언어 변경/복원과 같은 내용 재그리기를 독립 관찰했습니다. 현재 .NET11/Linux 검사114건=앱/세션38+경계/배치58+폰트 선택18, 네이티브146건씩·새 공통 컨트롤214건·클립보드35건입니다. 근거는 artifacts/linux-accessibility-2026-11-14/text-size-verification.json입니다. 일반 편집기 폰트 범위 게시를 검증했으며 포인트 크기·스타일/Markdown/조합·아틀라스만 쓰거나 혼합 대체 경로의 폰트 정보·물리 화면 읽기 프로그램은 미완료입니다. 선언된 스타일 플래그는 OpenType OS/2 참고 문서를 따릅니다.

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

`UiAccessibilityNode.ImageInfo`는 `UiAccessibilityImageInfo`를 선택적으로 게시합니다. 화면에서 잘리기 전의 전체 이미지 영역, 설명, 명시한 경우의 언어를 변경하지 않는 값으로 보관합니다. 기존 `Node.Bounds`는 계속 잘린 영역입니다. 실제로 표시하는 Markdown 이미지는 이 정보를 제공하며, 장식용 이미지는 제외하고 로드 실패 안내문은 네이티브 이미지로 등록하지 않습니다. Linux는 이 정보가 있을 때만 `org.a11y.atspi.Image`를 제공합니다. 설명·언어·크기와 화면·창·부모 기준 위치·영역을 읽을 수 있으며, 언어가 없으면 빈 프로토콜 문자열을 반환합니다. 크기는 실제 렌더링 영역을 감싸는 정수 픽셀이므로 원본 비트맵 크기와 다를 수 있습니다. 원문 범위로 드러내기와 키보드 선택은 유지합니다. 실제 화면 읽기 검증은 남아 있습니다.

## 접근성 종료 실패의 집중 검사

`Duxel.Linux.App.Tests`의 `text-attributes-native <새 결과 디렉터리> <라틴 폰트> <한글 폰트>`는 별도 세션과 기본 등록 세션에서 실제 AT-SPI 포커스·글자 경계·글꼴 속성만 검사합니다. 각 외부 클라이언트 48개와 호스트 5개 검사를 통과했습니다. 전체 접근성 검사를 다시 실행하거나 과거 실패의 원인이 제거됐음을 인증하는 명령은 아닙니다.

일반 클라이언트와 집중 클라이언트의 관리형 예외는 `<결과 경로>.failure.json`에 종류·예외 타입·메시지를 남긴 뒤 그대로 다시 던집니다. 부모는 이 기록을 오류에 포함하며 stdout/stderr와 실패 종료도 보존합니다. `probe-failure-contract`는 의도한 자식 예외가 부모까지 보존되는지 창을 열지 않고 검사합니다. 기록이 없는 종료를 관리형 오류로 추측하지 않습니다.

기존 guarded 로그의 종료134는 문서 끝 글꼴 속성 검사의 관리형 예외였습니다. 부모 드라이버가 이 실패를 받고 자신의 검사 창을 닫았습니다. 이전 포커스 거절 사례와는 다른 실패이며, 첫 BadDrawable 사례와 두 원인의 재발 방지는 계속 미해결로 유지합니다. 현재 집중 검사의 성공을 전체 해결로 확대하지 않습니다.


## Linux 표시 재사용과 기본 NativeAOT 실행

공통 렌더러는 마지막으로 실제 표시한 장면과 네이티브 무효화 순서를 함께 확인해, 같은 화면의 새 CPU 작업을 이미지 획득·제출·표시 없이 승인합니다. `UiRendererFrameMetrics.PresentationReused`가 이를 구분하며 실제 Present 순번·시각은 증가하지 않습니다. 노출·재표시·화면 변경은 실제 표시를 수행하고 콜백·GPU 애니메이션·변경된 페인트 그룹은 생략하지 않습니다. 현재 제품 호스트 연결은 Linux X11이며 다른 호스트는 기존 경로를 유지합니다. `DUXEL_VK_PRESENT_REUSE=0`은 동일 실행물 비교용으로 이 승인 경로만 끕니다.

고정 RC3 NativeAOT3쌍 교대 비교에서 단독 슬라이더 CPU는52.97%→24.90%(한 코어=100%), 감소53.00%였습니다. Fluent 전체 화면과 입력 지연은 실행별 범위가 겹쳐 이번 변경의 속도 개선으로 확정하지 않습니다. 원본 입력 확인 시간 초과는 기존 실행본에서도 발생했으며 미해결로 보존합니다. 독립 GPU588쌍·3704검사, 실제 노출/재표시21검사,33개 표시 시나리오·384입력·정지 누름9조건0회 구성·드래그·SpinButton 반복은 통과했습니다. 물리 표시 체감과 전체 제품 수용은 별도입니다.

`run-gallery-linux.ps1`은 `PublishedExecutable`을 생략하면 이제 WSL에서 고정 SDK로 NativeAOT를 게시합니다. `-LinuxDotnet`으로 정확한 설치 경로를 선택하고 `-Managed`는 명시적 관리형 비교에만 사용합니다. 누락된 SDK·버전 불일치는 오류로 알리며 관리형으로 대체하지 않습니다. 즉시 실행할 검증본과 해시·명령은 [표시 재사용 가이드](../artifacts/linux-present-path-2026-11-15/README.ko.md)를 따릅니다.


### Linux 검사 결과와 열린 Dialog의 고정 NativeAOT

Linux.App.Tests의 익명 결과14곳과 관측 객체를 공통 타입·원천 생성 JSON으로 바꾸고 리플렉션 JSON을 껐습니다. `result-contract <새 결과 경로>`는 이름·대소문자·순서·값·서식을 검사하며 관리형/AOT25개 JSON이 같습니다. 원본 출력14곳·관측 객체1곳의 필드 목록도 보존합니다. 고정 RC3 NativeAOT의 실제 열린 Dialog는 독립 클라이언트14·호스트3과 레지스트리 퇴장을 통과했습니다. 전체 Linux 접근성 묶음은 재실행하지 않았고 Windows RC3 AOT·전체 통합·새 속도 개선율로 확대하지 않습니다. [재현 명령·범위](../artifacts/linux-probe-results-aot-2026-11-15/README.ko.md).

### .NET 11.0.0 갤러리 검증

WSLg x64에서 새로 빌드한 관리형과 NativeAOT 갤러리에 `--verify <새 출력 경로>`를 실행했습니다. 두 실행에서 105개 시나리오가 같은 순서로 표시됐고 기록한 화면 상태가 일치했습니다. 각 실행의 프레임과 화면 표시 순번은 실행 시점에 따라 달랐으며, 두 갤러리 모두 정상 종료했습니다.

외부 `Duxel.Linux.App.Tests` 클라이언트는 `x11-gallery-pixels`, `atspi-gallery-tree`, `atspi-gallery-virtual-grid`로 각 빌드에서 18건을 확인했습니다. 검색어 입력 순서, Tree 접기와 혼합 선택, TreeGrid 행 안의 버튼, 10억 행 × 10억 열 가상 그리드의 먼 셀로 갔다 돌아왔을 때 첫 셀의 편집값을 확인했습니다. SearchBox·개요·RadioGroup의 1280 × 900 화면 6쌍에서는 상단 성능 수치 영역에서만 픽셀이 달랐습니다. [실행 결과와 이미지 해시](L:/Cache/Duxel/linux-gallery-2153/verification.json)

실제 FPS 표시를 확인하려면 외부 클라이언트에서 `x11-gallery-fps <갤러리 PID> <새 result.json 경로> <XID>`를 실행합니다. 갤러리를 시작할 때 `DUXEL_APP_PROFILE=1`, `DUXEL_APP_PROFILE_EVERY=1`, `DUXEL_APP_PROFILE_OUT=<새 로그 경로>`를 설정하고, 클라이언트에도 같은 로그 경로를 전달합니다. 클라이언트는 AT-SPI로 성능 표시를 읽고 이벤트 대기 모드에서 2.2초 동안 표시 값과 앱 프레임 수가 그대로인지 확인합니다. X11 마우스 입력으로 화면을 깨웠을 때 대기 시간 때문에 FPS 표본이 갱신되지 않는지 확인하고, 연속 렌더링을 켜면 표본 수집이 재개되는지 확인합니다. 마지막에 이벤트 대기 모드로 돌립니다. [FPS 검증 결과](L:/Cache/Duxel/linux-fps-2154/verification.json)

갤러리의 Drawer는 `x11-gallery-drawer <갤러리 PID> <새 result.json 경로> <XID>`로 검사합니다. Enter로 열면 포커스가 패널 안으로 이동합니다. 클라이언트는 AT-SPI에서 패널의 역할, 모달 상태, 설명을 확인하고 메모를 편집합니다. Escape로 닫으면 이전 접근성 객체가 사라지고 포커스가 열기 버튼으로 돌아옵니다. 다시 열면 새 접근성 객체에서 편집한 값을 읽을 수 있고, 저장을 실행하면 저장 횟수가 한 번 늘면서 포커스가 돌아옵니다. 관리형과 NativeAOT 갤러리 모두 다섯 건을 통과하고 정상 종료했습니다. [Drawer 검증 결과](L:/Cache/Duxel/linux-drawer-2155/verification.json)

### ARM64 첫 프레임 진단

SDK 11.0.100과 런타임 팩 11.0.0으로 만든 Linux ARM64 NativeAOT 진단 실행 파일을 QEMU 10.0.13, Xvfb, lavapipe에서 두 번 실행했습니다. 렌더러가 실제 적용한 MSAA는 각각 4×와 1×였습니다. 두 실행 모두 1280 × 900 스왑체인을 만들고 첫 그래픽 프레임을 제출했지만, 프레임 표시 함수를 호출하기 전에 펜스 대기가 10초를 넘겼습니다. 이후 QEMU에서 두 프로세스 모두 신호 11로 종료됐습니다. 이 에뮬레이션 환경에서는 어느 샘플 수에서도 첫 프레임이 끝나지 않았습니다. 실기기 동작과 원인이 되는 그래픽 명령은 아직 확인하지 못했습니다. [진단 로그와 실행 파일 해시](L:/Cache/Duxel/arm64-msaa-2156/verification.json)

MSAA 1×에서 화면 밖의 사전 렌더링을 생략하고 비교했습니다. 첫 그리기의 상태만 설정했을 때, 첫 명령 하나를 제출했을 때, 처음 8개와 전체 130개를 제출했을 때 모두 화면이 표시됐습니다. 개요 페이지에는 컴파일된 레이어 후보가 없어서 두 전체 비교 모두 직접 그리기 경로를 사용했습니다. 캡처에는 1280 × 900 개요가 표시되며 SVG 로고처럼 사전 렌더링에 의존하는 내용은 빠져 있습니다. [캡처와 그리기 비교 기록](L:/Cache/Duxel/arm64-draw-range-2158/verification.json)

다른 진단 실행 파일에서는 화면에 직접 그리는 130개 명령을 그대로 두고 페인트 그룹과 벡터 장면 기록을 각각 켰습니다. 페인트 그룹을 켠 실행은 화면을 두 번 표시하고 정상 종료했습니다. 벡터 장면을 켜면 첫 펜스에서 다시 시간 초과가 나고 QEMU에서 신호 11로 종료됐습니다. 이번 개요 페이지의 재현 조건은 벡터 장면 기록을 포함할 때로 좁혀졌습니다. 어느 명령에서 멈추는지와 ARM64 실기기 동작은 추가 확인이 필요합니다. [사전 렌더링 단계 비교](L:/Cache/Duxel/arm64-offscreen-2159/verification.json)

벡터 계산을 빼고 전송만 남기면 두 프레임이 정상 표시됐습니다. 계산을 남기고 결과 복사를 빼면 전체 벡터 경로를 켰을 때처럼 펜스 대기 시간이 초과됐습니다. 윤곽선 기록만 켠 실행과 채우기 계산만 켠 실행에서도 각각 재현했습니다. 채우기 비교는 윤곽선·흐림·의존 장면이 없는 단일 명령 네 개로 진행했습니다. 이 결과로 에뮬레이터의 벡터 계산 경로까지 범위를 좁혔습니다. 진단 중 일부 단계로 만든 벡터 이미지는 품질 평가에 사용하지 않았습니다. [계산과 복사 비교](L:/Cache/Duxel/arm64-vector-pass-2160/verification.json) · [윤곽선과 채우기 비교](L:/Cache/Duxel/arm64-vector-shaders-2161/verification.json)

같은 채우기 작업 네 개에 최소 셰이더를 적용했습니다. 메모리에 접근하지 않는 경우, GPU 버퍼 주소로 쓰는 경우, 주소로 읽고 쓰는 경우에 각각 두 프레임이 정상 표시됐습니다. 저장소의 벡터 장면 셰이더와 같은 GLSL을 `glslangValidator -Os`로 재컴파일한 셰이더는 모두 첫 펜스에서 멈췄습니다. 두 셰이더는 SPIR-V 유효성 검사를 통과했습니다. 이번 실행에서는 벡터 결과를 이미지로 복사하지 않아 픽셀을 평가하지 않았습니다. [최소 컴퓨트 비교](L:/Cache/Duxel/arm64-compute-minimal-2162/verification.json) · [셰이더 빌드 비교](L:/Cache/Duxel/arm64-shader-compiler-2163/verification.json)

같은 ARM64 실행 파일에서 채우기 작업 네 개와 화면 그리기 명령 130개를 유지했습니다. 장면 준비만 실행하거나 명령의 경계만 검사하면 화면이 두 번 표시됐습니다. 채우기 색을 고정하고 레이어 스택 합성을 남기면 첫 펜스에서 시간 초과가 재현됐습니다. 도형 범위 계산과 채우기 색 계산을 각각 남긴 실험에서도 시간 초과가 났습니다. QEMU와 lavapipe에서 실행했으며 벡터 결과를 이미지로 복사하는 작업은 생략했습니다. [장면 연산 비교](L:/Cache/Duxel/arm64-vector-ops-2164/verification.json) · [명령 루프 비교](L:/Cache/Duxel/arm64-vector-loop-2166/verification.json)

같은 합성 루프에서 스택 크기를 8단계로 고정하거나 스택에 넣고 빼기 전에 범위를 검사해도 첫 펜스에서 시간 초과가 났습니다. 두 변경은 각각 따로 실행했습니다. [스택 비교](L:/Cache/Duxel/arm64-vector-stack-2167/verification.json)

스택을 없앤 루프에서 채우기 색을 고정했을 때는 화면이 두 번 표시됐습니다. 스택 없이 실제 도형과 색 계산을 복원하자 첫 펜스에서 다시 시간 초과가 났습니다. 이 부하에서는 스택 제거만으로 첫 프레임이 완료되지 않았습니다. [스택을 쓰지 않는 루프 비교](L:/Cache/Duxel/arm64-vector-root-2168/verification.json)

원본 셰이더에 `GALLIVM_PERF=nopt`를 전달한 실행은 첫 펜스가 반환되기 전에 QEMU 게스트에서 신호 11로 종료됐습니다. 종료 코드는 139였고 실행 파일과 셰이더 해시는 앞선 대조군과 일치했습니다. [Gallivm 진단](L:/Cache/Duxel/arm64-vector-gallivm-2169/verification.json)

ARM64 게스트의 코어 덤프에서 첫 펜스 대기 중 신호 11을 받은 스레드를 찾았습니다. 실행 중이던 명령은 로드된 ELF 파일 밖의 실행 영역에 있었고, 복귀 주소는 `libvulkan_lvp.so`의 간접 호출 직후를 가리켰습니다. 해당 명령 `ldr q0, [x9, #304]`가 읽으려던 주소는 게스트 코어의 메모리 맵에 없었습니다. 주소가 어디서 만들어졌는지는 밝혀지지 않았습니다. 같은 실행 파일과 셰이더에 시스템 호출 추적을 켠 별도 실행은 두 프레임을 표시하고 정상 종료했습니다. 두 실행 모두 벡터 결과 복사를 생략한 진단입니다. [게스트 코어 분석과 추적 결과](L:/Cache/Duxel/arm64-vector-dump-2173/verification.json)

전체 벡터 명령과 결과 복사를 켜면 시스템 호출 추적을 끈 한 번과 켠 두 번 모두 첫 펜스에서 시간 초과가 났습니다. 세 실행 모두 같은 벡터 출력 5개와 직접 그리기 명령 130개를 기록했으며, 화면을 표시하기 전에 종료됐습니다. [전체 벡터 계산과 복사 비교](L:/Cache/Duxel/arm64-vector-present-2174/verification.json)

별도 .NET 11 ARM64 NativeAOT 진단은 단일 명령 네 개가 고정 색을 쓰게 하고, 결과 복사를 끈 실행과 켠 실행을 비교했습니다. 둘 다 화면을 두 번 표시하고 정상 종료했습니다. 복사를 켰을 때만 UI 색상과 합성된 표시색 픽셀 2,163개가 화면에 나타났습니다. 복사와 시스템 호출 추적을 유지한 채 원래 장면 셰이더를 사용하면 첫 펜스가 반환되기 전에 게스트 신호 11이 발생했습니다. 이 검증은 고정 색이 계산에서 화면까지 전달되는 경로를 확인했으며, 실제 SVG 도형과 픽셀 검증은 남아 있습니다. [고정 색 복사 비교](L:/Cache/Duxel/arm64-vector-copy-control-2175/verification.json)

게스트 코어에서 계산 작업과 셰이더의 포인터를 따라 JIT 진입 주소를 확인했습니다. 작업 그룹은 8×8×1이고 그룹 수는 2×3×1로, 앞선 실험의 채우기 네 개 중 16×17 출력과 일치했습니다. ARM64 명령의 오프셋을 따로 해석한 결과도 디버거에서 본 매핑 밖 주소와 같았습니다. 전체 벡터 명령을 Cortex-A72와 Cortex-A76 모델에서 실행해도 첫 펜스가 반환되기 전에 신호 11이 발생했습니다. 주소가 생성·배치되는 단계의 원인과 실기기 동작은 아직 확인하지 못했습니다. [주소와 CPU 모델 검증](L:/Cache/Duxel/arm64-vector-guest-va-2176/verification.json)
