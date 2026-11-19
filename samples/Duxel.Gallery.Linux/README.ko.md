# Linux Duxel 갤러리

현재 Linux x64 NativeAOT 빌드는 .NET 11.0.0에서 WSLg 화면 표시 105개와 외부 SearchBox 검사 3개를 통과했습니다. 관리형과 시나리오 순서와 기록된 UI 상태가 일치했습니다. X11에서 키가 연달아 들어와도 SearchBox가 입력 순서와 커서 위치를 유지했습니다.

```powershell
.\run-gallery-linux.ps1 -PublishedExecutable '/home/dimohy/.cache/duxel/gallery-linux-aot-2016/image/Duxel.Gallery.Linux'
```

이전 WSLg 검증. .NET 11.0.0 Linux x64 NativeAOT에서 화면 표시 시나리오 100개를 모두 통과했고 정상 종료했습니다. X11에 창이 등록됐으며 Windows UI Automation에서 관측한 49회 모두 화면 안에 있었습니다. WSLg 1.0.79로 실행하는 동안 이전 디코더 오류는 기록되지 않았습니다. Windows 쪽 화면 픽셀은 비교하지 않았습니다. 근거는 `W:/Cache/Duxel/gallery-source-20261117/linux-ga-1912`에 있습니다.

Windows PowerShell에서 WSLg로 실행합니다.

```powershell
.\run-gallery-linux.ps1
```

Linux에서는 게시한 `Duxel.Gallery.Linux` 실행 파일을 인자 없이 실행합니다. 갤러리가 Fontconfig에서 라틴 일반·굵게, 한글 일반·굵게, 고정폭 글꼴을 찾습니다. `fc-match` 명령은 Ubuntu의 `fontconfig` 패키지에 있으며, 한글 글꼴은 `fonts-noto-cjk` 등의 패키지에서 설치할 수 있습니다. WSL 배포판에 한글 글꼴이 없으면 마운트된 Windows의 맑은 고딕을 사용합니다. 직접 지정하려면 WSL 실행기의 `-Fonts`에 이 순서대로 경로 다섯 개를 전달합니다. 현재 바이너리에는 글꼴 인자 없이 `--verify <새 출력 경로>`도 사용할 수 있습니다.

기본 실행은 WSL에서 NativeAOT로 게시한 뒤 갤러리를 엽니다. 같은 Linux SDK로 관리형 빌드를 실행하려면 `.\run-gallery-linux.ps1 -Managed`를 사용합니다. `-PublishedExecutable`을 지정하면 이미 게시한 실행 파일을 바로 엽니다.

소스에서 실행할 때는 WSL 배포판의 아키텍처를 따릅니다. `x86_64`는 `linux-x64`, `aarch64`는 `linux-arm64`로 게시합니다. NativeAOT로 게시하려면 해당 배포판에 네이티브 빌드 도구가 필요합니다. 여기서 실제 실행을 검증한 환경은 WSLg x64입니다.

Ubuntu x64에서 .NET 11.0.0 Linux ARM64 갤러리를 교차 게시했습니다. QEMU의 Xvfb·lavapipe 환경에서 AT-SPI 등록과 Vulkan 스왑체인 생성을 확인했습니다. 두 검사 모두 첫 프레임이 표시됐는지 확인하기 전에 제한 시간에 도달했습니다. ARM64 실기기와 전체 렌더링 검증이 남아 있습니다.

작업 표시줄에 아이콘만 보일 때는 Windows 이벤트 뷰어에서 `Microsoft-Windows-TerminalServices-RDPClient/Operational`을 확인하세요. `WireDecoderError_DecodeW2S1InvalidData`와 `0x80070490`이 함께 기록되면 WSLg의 Windows 표시 경로에서 발생한 경우입니다. [WSLg 이슈 #1489](https://github.com/microsoft/wslg/issues/1489)

## 이전 실행 및 검증 기록

현재 공통 NativeAOT 옵저버 전달본은 `7746d0b7`이며 후보41개·전달38개가 통과했습니다. 변경하지 않은 Linux 문단 모듈은 기존 네이티브334개·텍스트114개 검증을 유지합니다. 공통 입력·IME 예제에는 한글·히브리어 폰트 구간을 가로지르는 RTL 임베딩을 추가했습니다. Windows 저장소 루트에서 `./artifacts/linux-present-path-2026-11-15/run-observer.ps1`를 실행하세요. 이전 `ce552ef3`는 보존했습니다. [현재 검증](../../artifacts/fluent-messagebar-2026-11-15/paragraph-font-context-verification.json)과 [Linux 의존성·실행](../../docs/linux-text-shaping.ko.md)이 아래 이전 실행 기록보다 우선합니다.

현재 실행 기준(2026-11-15)은 아래 이전 기록보다 우선합니다. 메시지 탭 높이 초과 종료를 스크롤 뷰로 수정했고 Windows38·Linux NativeAOT41·전달38 시나리오가 통과했습니다. `& 'W:/MyWorks/Duxel/artifacts/linux-present-path-2026-11-15/run-observer.ps1'`로 실행하세요. 원장은 `c8246d`를 선택하며 이전 `43119a` 실행본은 보존합니다. RTL은 실제 본문 행 정렬과 액션 위치에 적용됩니다. 전체 양방향 조판은 미완료이며 기존 Windows 조판 실패6개는 공통 DirectWrite 경로로 교정했습니다(확장 37개 통과). [검증 범위](../../docs/fluent2-messagebar.ko.md).

2026-11-15 메시지 탭의 높이 초과 종료를 수정했습니다. 전체 페이지에 스크롤 뷰를 적용했고 낮은 높이·125% 텍스트 및 실제 추가/순서 변경/삭제를 검증했습니다. **키를 가진 동적 메시지**에서 추가·삭제·순서 뒤집기를 확인하세요. Windows36·Linux NativeAOT39·전달 실행기36 시나리오 통과. Linux 원장은fff949 실행본을 선택합니다. 기존 창을 닫고 안내된 실행기로 다시 실행하세요.


메시지 표시 탭에 실제 Group을 연결했습니다. **닫기**, **모두 다시 열기**, **동작 줄이기**로 종료 페이드·재배치·포커스를 확인하세요. 공통 모델17개·GPU140쌍·Windows32탭과 Linux 고정 RC3 NativeAOT35표시/전달 실행기32탭을 확인했습니다. Linux 실행 원장도310c73 실행본으로 갱신했습니다. 기존 창을 닫고 안내된 실행기로 다시 실행하세요. 자동 공지·제목 관계·동적 컬렉션 전체 적합성은 남아 있습니다.


2026-11-15 Windows 시작 실패를 수정했습니다. 새 MessageBar 팩터리에 실제 **메시지 표시** 탭을 연결했으며 시작 검사는 유지합니다. `./run-gallery.ps1`로 실행하세요. `./run-gallery.ps1 -Verify`는 실제 32개 탭을 순회하고 종료하는 표시 검사입니다. 실행 로그는 `%LOCALAPPDATA%/Duxel/ImplementationObserver/logs/`에 남습니다.

Linux 공통 글꼴 지표의 descent 부호를 교정해 버튼·배지·체크박스·라디오·스피너·본문·입력·숫자 입력의 세로 정렬을 바로잡았습니다. 기존 창을 닫고 `& ./artifacts/linux-present-path-2026-11-15/run-observer.ps1`로 다시 실행하세요. 25개 글꼴 지표와 32쌍 위젯/커서 픽셀 대조, NativeAOT 35개 표시 시나리오를 통과했습니다. 속도 개선을 측정한 변경은 아닙니다.


2026-11-15 일반 Fluent 텍스트의 실제 한글 내용·내용 갱신·정체성을 네이티브 접근성 트리에 연결했습니다. 현재 실행 경로에도 반영했습니다. CPU64·Label 회귀63·원시 GPU24쌍·양쪽 독립 네이티브 검사를 통과했습니다. 새 성능3쌍은 구간 중첩으로 방향 미확정이며 이전 성능 실행물은 보존합니다.

2026-11-15 현재 실행본에는 **스켈레톤** 탭이 포함됩니다. 기존 Linux 최적화 실행본과4개 작업 스레드 조건으로3회씩 비교했으며 CPU·입력·구성 지표에서 구간이 분리된 악화는 관측하지 않았습니다. 새로운 속도 개선율은 주장하지 않습니다. 단일 배포 원장은 `artifacts/linux-observer-delivery.json`이며 이전 성능 측정 실행물과 원장을 보존합니다.

이 PC의 PowerShell에서 바로 실행:

```powershell
& 'W:/MyWorks/Duxel/artifacts/linux-present-path-2026-11-15/run-observer.ps1'
```


## Linux CPU 작업 스레드 최신 검증

기본24개 llvmpipe 작업 스레드를4개로 제한한 동일 RC3 NativeAOT의 독립3쌍 비교에서 세 화면 CPU37.60~38.51% 감소를 확인했습니다. MSAA·폰트·갱신 면적은 같고, 슬롯1/3의7,140개 검사 및 기존 기본 스레드와1,216쌍 원시 이미지가 모두 일치합니다. 최신 검증본 실행기는 실행한 프로세스에만 설정을 적용합니다. 직접 실행은 루트 실행기의 `-SoftwareRasterThreads 4`로 선택하며 생략하면 드라이버 기본값을 유지합니다. [실행·범위·남은 제한](../../artifacts/linux-driver-worker-budget-2026-11-15/README.ko.md). WSL 하드웨어 경로는 필수 합성 기능 미지원으로 적용하지 않았습니다.

최신 Linux 수정본은 [배치 내부 갱신·NativeAOT 즉시 실행 가이드](../../artifacts/linux-sparse-damage-2026-11-15/README.ko.md)를 사용하세요. 저장소 루트에서 `./artifacts/linux-sparse-damage-2026-11-15/run-observer.ps1`로 검증본을 즉시 실행합니다. 실제 갱신 면적46.67~92.36% 감소, 진단 고정 Fluent CPU11.54% 감소를 확인했으며 다른 CPU·입력 지연은 구간 중첩으로 개선율을 주장하지 않습니다. 아래 기록은 별도 과거 체크포인트입니다.

2026-11-15 공통 입력 루프 수정: 버튼을 누른 채 멈춘 상태를 계속 새 입력으로 판단하던 조건을 교정했습니다. 같은 RC3 NativeAOT의 세 번 교대 비교에서400ms당239~317회이던 불필요한 구성이 모든 수정 조건에서0회가 됐습니다. 이 정지 누름 조건의 CPU 작업은99.94~99.96% 감소했습니다. 연속 드래그와 예정된 SpinButton 반복은 유지합니다. 최종 실행물의32개 화면·384회 입력·정상 종료를 확인했습니다. 물리 표시 지연과 사용자 체감 전체의 완료 판정은 별개입니다. 이 PC에서는 저장소 루트에서 `./artifacts/linux-held-input-idle-2026-11-15/run-observer.ps1`로 검증한 실행물을 바로 엽니다. [검사 범위](../../artifacts/linux-held-input-idle-2026-11-15/README.ko.md).

2026-11-15 현재 소스 반영: 실행 경로에 공통 Component 스크롤과 Markdown 이미지 검증을 반영했습니다. 이 NativeAOT 수정본으로32개 화면 표시·384회 네이티브 포인터 변경과 정상 종료를 확인했습니다. 아래의 이전 성능 비교 수치는 보존된 `observer-rc3-aot-publish` 기준 빌드의 측정이며, 이번 실행은 회귀 확인으로 새 속도 개선 비교가 아닙니다.

2026-11-15 입력 지연 수정: Vulkan 표시에 입력용 X11 연결을 공유하지 않고 전용 연결을 사용합니다. 실제 네이티브 드래그를 세 번 교대로 비교했을 때 50ms를 넘긴 입력 반영은 기존 150/288회에서 수정본 0/288회로 줄었습니다. 수정본의 최댓값은 4.074ms였으며, 물리 마우스부터 모니터 표시까지의 지연을 측정한 수치는 아닙니다.

2026-11-15 최신 SVG GPU 카메라·경계 선별 검증 빌드와 실행 명령은 [SVG 가이드](../../docs/svg-gpu-camera.ko.md)에 있습니다. Linux 표시32개를 확인했으며 전체 브라우저 픽셀 일치는 미완료입니다.

Last synced: 2026-11-15

Windows 옵저버와 같은31개 탭·화면 소스를 사용합니다. Linux 호스트가 X11, Vulkan, Linux 이미지 디코더와 명시적인 폰트를 연결합니다. 상단 V-Sync는 기본 꺼짐, 이벤트 대기는 기본 켜짐입니다. 오른쪽 상단 **갱신 영역 강조**는 기본 꺼짐이며, 켜면 모든 탭과 팝업을 관찰할 수 있습니다.

## Windows에서 WSLg로 실행

이 PC에서 검증한 최신 NativeAOT 수정본은 아래 명령으로 실행합니다. 저장소 루트의 PowerShell에서 실행하며, 게시·컴파일 없이 기존 실행물을 엽니다. WSL에 .NET SDK를 설치할 필요는 없습니다. 경로는 이 PC의 검증 빌드이며 다른 PC에서는 직접 게시한 실행물 경로를 지정하세요.

```powershell
$fonts = @(
    '/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf',
    '/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf',
    '/mnt/c/Windows/Fonts/malgun.ttf',
    '/mnt/c/Windows/Fonts/malgunbd.ttf',
    '/usr/share/fonts/truetype/dejavu/DejaVuSansMono.ttf'
)
$launch = @{
    Distribution = 'Ubuntu'
    Fonts = $fonts
    PrivateAccessibilityBus = $true
    PublishedExecutable = '/home/dimohy/.cache/duxel/observer-text-accessibility-rc3-20261115/publish/Duxel.ImplementationObserver.Linux'
    SoftwareRasterThreads = 4
}
./run-gallery-linux.ps1 @launch
```

폰트 순서는 라틴 일반·라틴 굵게·한글 일반·한글 굵게·고정폭입니다. 위 경로는 검증한 WSL 호스트의 예시이며, Linux에 Windows 폰트가 필수인 것은 아닙니다. 설치된 다른 폰트 경로를 명시할 수 있습니다. 폰트 파일을 이 프로젝트에 복사하거나 배포하지 않습니다.

현재 소스를 새로 게시하려면 `$launch`에서 `PublishedExecutable`을 제거합니다. 이제 기본 경로는 WSL에서 `global.json`의 고정 SDK로 NativeAOT를 게시합니다. 기본 SDK 경로는 WSL 사용자 홈의 `.cache/duxel/toolchains/dotnet-<고정 버전>/dotnet`이며 다른 설치 위치는 `-LinuxDotnet /absolute/path/dotnet`으로 지정합니다. SDK 버전이 다르거나 도구가 없으면 실행 전에 실패하며 관리형으로 바꾸지 않습니다. 최초 게시에는 네이티브 컴파일 시간이 필요합니다. 관리형 비교에는 `-Managed`를 명시합니다. 이 경우에도 WSL에서 같은 고정 버전 SDK로 게시합니다. 기존 검증 실행물은 덮어쓰지 않습니다.

WSLg의 `DISPLAY`, Vulkan 드라이버, X11, FreeType, HarfBuzz, Cairo, GdkPixbuf, Pango와 접근성용 세션 D-Bus·AT-SPI가 필요합니다. `-PrivateAccessibilityBus`는 검증용 독립 접근성 버스를 명시적으로 시작합니다. 기존 데스크톱 접근성 버스를 사용하려면 이 옵션을 생략하세요. 누락된 기능을 다른 렌더러로 바꾸거나 접근성을 조용히 끄지 않습니다.

## Linux에서 직접 실행

게시된 `Duxel.ImplementationObserver.Linux`에 위 순서의 다섯 폰트 경로를 인수로 전달합니다. .NET11 SDK가 설치된 Linux에서 소스를 실행할 때는 저장소의 `tests/managed-validation` 디렉터리에서 다음 명령을 사용합니다. 경로는 자신의 저장소 위치로 바꾸세요.

```bash
dotnet run --project /path/to/Duxel/samples/Duxel.ImplementationObserver.Linux/Duxel.ImplementationObserver.Linux.csproj \
  -c Release -p:DuxelSourceTargetFramework=net11.0 \
  -p:DirectoryBuildTargetsPath=/path/to/Duxel/samples/Duxel.ImplementationObserver/LocalSource.targets \
  -- /path/latin.ttf /path/latin-bold.ttf /path/korean.ttf /path/korean-bold.ttf /path/mono.ttf
```

## 실행 검증

다섯 폰트 인수 뒤에 `--verify <새 결과 디렉터리>`를 붙이면 각 탭을 순회하고 해당 프레임의 Vulkan 표시 요청이 확인된 뒤 다음 탭으로 이동합니다. 모든 탭을 확인하면 자신이 연 창을 닫고 `result.json`을 기록합니다. 일반 실행은 자동 종료하지 않습니다.

Ubuntu/WSLg x64, 관리형 .NET11.0.0에서 29/29 탭의 표시 요청과 정상 종료를 확인했습니다. 픽셀을 브라우저와 비교하거나 전체 AT-SPI·화면 읽기 적합성을 인증하는 검사는 아닙니다. 기존 Linux 전체 접근성 검사에 남은 문제는 별도로 추적합니다. [공통 갱신 관찰 가이드](../../docs/repaint-observer.ko.md).

## GPU 카메라 개발 검증 빌드

현재 빌드는 `/home/dimohy/.cache/duxel/observer-idle-verified-20261115/publish/Duxel.ImplementationObserver.Linux`입니다. 위의 `$fonts` 배열을 만든 후 실행합니다.

```powershell
$exe = '/home/dimohy/.cache/duxel/observer-idle-verified-20261115/publish/Duxel.ImplementationObserver.Linux'
wsl.exe -d Ubuntu --exec dbus-run-session -- $exe @fonts
```

SVG → 브라우저 비교에서 확대·회전·여백을 확인합니다. 자동 검증은 뒤에 `--verify-gpu-camera <새 결과 경로>`를 붙입니다. 기존 29개 탭에 복잡한 SVG의 100%·200%·400% 표시를 추가한 32개 시나리오이며, 최초 카메라 출력부터 CPU 래스터가 늘지 않는지도 검사합니다. 실제 32/32 표시·정상 종료가 통과했고, 비트맵 예제의 CPU 출력 1회는 유지되며 확대의 추가 출력은 0회였습니다. 전체 픽셀 적합성은 [GPU 카메라 가이드](../../docs/svg-gpu-camera.ko.md)의 남은 범위와 구분합니다.

현재 WSL 선택 장치는 llvmpipe(CPU 소프트웨어 Vulkan)입니다. 32개 결과는 기능 검증이며 하드웨어 GPU 속도 증거가 아닙니다. GPU 카메라 가이드의 장치 범위를 확인하세요.

## 일반 UI 슬라이더 진단

다섯 폰트 인수 뒤에 `--verify-slider-performance <새 결과 디렉터리>`를 붙이면 단독Fluent 슬라이더·전체Fluent화면·진단행고정조건을 각16워밍업/32측정 프레임으로 비교합니다. 실제포인터 지연이나 픽셀일치 검사가 아니라 같은 위젯의 모델 변경·구성·GPU·표시 간격·갱신영역 검사입니다. 완료하면 자신이 연 창만 종료합니다.

상단 Present 수치를 매프레임 바꾸던 문제를 고쳐 전체 진단행을 기존 FPS 표본인0.5초 주기로 갱신합니다. 새 프레임을 스스로 요청하지 않습니다. llvmpipe에서3회 교대 비교의Fluent 표시간격 중앙값8.17→2.60ms, 보수면적905352→108489px를 확인했습니다. 단독슬라이더·진단행고정조건의 시간범위는 겹쳐 별도 속도개선으로 주장하지 않습니다. 네이티브 입력 정체는 아래의 연결 분리로 교정했고, 최초 구성 비용은 NativeAOT 항목에서 비교합니다. 물리 표시 지연은 미측정입니다.

## 네이티브 입력 회귀 검사

다섯 폰트 뒤에 `--verify-pointer-performance <새 결과 경로>`를 붙입니다. 일반 슬라이더, Fluent 전체 화면, 진단 행 고정 화면에서 X11 이벤트를 검사 창으로 직접 보내 실제 포인터 판정과 값 바인딩을 확인합니다. 각 화면은 96회 예열·32회 측정하며, 접근성을 켜고 자신이 연 창만 정상 종료합니다. 데스크톱 마우스를 움직이거나 다른 창에 입력하지 않습니다.

기존 모델 값 변경 검사와 달리 플랫폼 입력 경로를 통과합니다. 제출 확인 시간에는 검사 예약 약2ms가 포함되므로 물리 모니터의 표시 지연으로 해석하면 안 됩니다. 세 번의 교대 비교에서 단독 슬라이더 입력 반영 중앙값은 98.195→1.486ms, 진단 행 고정 화면은 70.297→1.351ms였습니다. Fluent 전체 화면의 중앙값 차이는 실행 간 범위가 겹쳐 개선으로 판단하지 않았으며, 긴 지연의 제거를 별도로 확인했습니다. 최초 구성 비용은 아래 NativeAOT 비교에서 확인했으며 물리 표시 지연은 미측정입니다.

## 고정 버전 Linux NativeAOT 실행물

현재 위 실행 경로는 SDK `11.0.100-rc.3.26522.148`·런타임 `11.0.0-rc.3.26522.148`로 만든 Linux x64 NativeAOT입니다. 실행에는 .NET 설치가 필요하지 않으며 기존 Linux 네이티브 라이브러리와 다섯 폰트는 그대로 필요합니다. 원래 Windows NativeAOT 검증과는 별개입니다.

동일 RC3 관리형과 세 번 교대로 비교했습니다. Fluent 첫 구성 중앙값은 134.6612→31.7940ms(76.39% 감소), 96회 예열한 실제 드래그 중 UI 구성은 0.67805→0.25695ms(62.10% 감소), 표시 제출 간격은 1.971045→1.545877ms(21.57% 감소)였습니다. 단독 슬라이더 입력 반영은 약1.34→1.53ms로 소폭 느려졌고, 모든 입력 지연이 빨라졌다는 뜻은 아닙니다. 최초 사용 측정은 프로세스별 첫 구성 기준이며 디스크·폰트 캐시를 비운 부팅 검사가 아닙니다.

`text` 87개·`vector` 73개·`render` 34개 계약이 관리형/NativeAOT에서 각각 통과했습니다. 전체 출력 파일 40개(그중 원시 RGBA 12개)가 바이트 단위로 일치하며, 이 범위의 이미지 비교를 전체 옵저버 픽셀 일치로 확대하지 않습니다. NativeAOT 옵저버의 32개 화면 표시·정상 종료도 확인했습니다.

새로 게시할 때는 Linux용 **.NET 11.0.100 정식 SDK**로 저장소 루트에서 실행합니다. Windows 실행기를 사용할 때는 `-LinuxDotnet`으로 Linux에 설치된 SDK 경로를 지정할 수 있습니다.

```bash
dotnet publish samples/Duxel.Gallery.Linux/Duxel.Gallery.Linux.csproj \
  -c Release -r linux-x64 --self-contained true -p:PublishAot=true \
  -p:DuxelSourceTargetFramework=net11.0 \
  -p:DirectoryBuildTargetsPath="$PWD/samples/Duxel.Gallery/LocalSource.targets" \
  -p:RuntimeFrameworkVersion=11.0.0 \
  --artifacts-path "$HOME/.cache/duxel/gallery-net11-build" \
  -o "$HOME/.cache/duxel/gallery-net11-publish" -warnaserror -m:1 -nr:false
```

검증해 둔 실행 경로를 덮어쓰지 않도록 새 출력 경로를 사용합니다. 이 과거 체크포인트 당시 Windows 실행기는 관리형 게시 경로였습니다. 현재 `run-gallery-linux.ps1`의 기본 소스 게시 경로는 고정 SDK NativeAOT이며 `-Managed`로만 관리형을 선택합니다.

게시된 실행물만 열려면 위 `$fonts`를 설정한 뒤 다음과 같이 실행합니다. SDK 게시 단계를 실행하지 않습니다.

```powershell
$launch = @{
    Distribution = 'Ubuntu'
    Fonts = $fonts
    PrivateAccessibilityBus = $true
    PublishedExecutable = '/home/dimohy/.cache/duxel/observer-idle-verified-20261115/publish/Duxel.ImplementationObserver.Linux'
}
./run-gallery-linux.ps1 @launch
```

검사할 때만 `SampleArguments = @('--verify-pointer-performance', '/tmp/새-결과-경로')`를 명시합니다. 출력 경로는 존재하지 않아야 합니다. `PublishedExecutable`이 없으면 기존 관리형 소스 게시 경로를 실행하고, 명시한 파일이 없으면 다른 실행물로 전환하지 않고 실패합니다.

메시지 표시: 스크롤 가능한 페이지에서 **표시할 때 자동 공지**와 **차분한 공지로 고정**을 전환하고 공지 요청 수를 확인하세요. 실제 제목 관계와 표시·우선순위 변경에 따른 한 번의 공지를 지원하며, 재정렬·삭제는 공지를 반복하지 않습니다. 요청 수는 발화 완료 수가 아닙니다. MessageBar 12/13, 전체 적합성은 구현 중입니다. [API·범위](../../docs/fluent2-messagebar.ko.md).

메시지 표시 → **오른쪽에서 왼쪽 배치**로 실제 슬롯과 액션 배치를 전환합니다. 공통 `UiWrapOptions.RightToLeft`는 자식의 X 위치를 반전하며 탐색·행 순서는 유지합니다. 임의 자식의 문단 양방향 조판을 보장하는 옵션은 아닙니다. 전체 MessageBar 12/13은 유지합니다.

MessageBar 현재 계약은 12/13입니다. 실제 제목 연결과 자동 공지를 Windows UIA 클라이언트 검사 8개·호스트 검사 1개, Linux 고정 RC3 NativeAOT AT-SPI 클라이언트 검사 9개·호스트 검사 3개로 확인했습니다. 전체 문단 RTL은 남았고 Windows NativeAOT는 별도 관문입니다. 기존 검증된 Linux 옵저버는 유지합니다.

메시지 표시의 여러 행·각진 모서리·방향·공지 옵션은 새 동적 메시지에도 동일하게 적용됩니다. 실제 조작 34개와 Windows 38·Linux 41·전달 실행기 38개 표시 시나리오를 확인했습니다. 현재 MessageBar 12/13, 전체 RTL·줄바꿈은 남았습니다.
