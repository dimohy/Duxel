# Linux 앱 샘플

640×520 크기의 솔루션 내 샘플입니다. 실제 X11 입력·텍스트와 공통 Vulkan 렌더러를 사용합니다. V-Sync를 끄고 변경 없는 UI는 이벤트를 기다립니다. 프레임 수 표시는 갱신을 요청하지 않으며 포커스가 있는 입력창은 커서 깜빡임 프레임을 요청할 수 있습니다.

- 클릭 수 버튼과 한글·영문 입력을 확인하고 Ctrl+A/C/V로 클립보드를 사용하세요.
- 오른쪽 PNG·SVG 비트맵·SVG 위젯을 확인하세요. 회전은15도씩 바뀌며 처리한 장면·래스터를 재사용합니다.
- 하단 정수 슬라이더는1씩 조정하고 진행률은 해당 수준을 따라갑니다. ‘진행률 모드’로 확정·미확정을 전환하세요. 전환이 끝나면 대기하며 선택한 미확정 표시에는 화면에 보이는 동안 애니메이션을 적용합니다.
- 아래 왼쪽에서 도시 하나, 오른쪽에서 여러 도시를 선택하세요. 왼쪽 제주 행은 선택할 수 없습니다. 종료 버튼이나 창 닫기로 마칩니다.
- 맨 아래에서 실제 텍스트의 Unicode 스칼라 수·커서·선택을 확인하세요. 한글·이모지·방향키·Shift 선택을 사용해 보세요. 이 표시는 프레임을 예약하지 않으며 네이티브 Text/EditableText에서 일반 편집기 조회·범위/클립보드 편집을 지원하며 전체 적합성은 남아 있습니다.

AT-SPI가 기본으로 켜지므로 세션 D-Bus·org.a11y.Bus·접근성 레지스트리와 GIO/GLib/GObject·Pango1.0 ABI(libpango-1.0.so.0)가 필요합니다. Pango1.52.1에서 단어·문장·문단의 Unicode 경계와 재사용을 확인했습니다. 의도적으로 끄려면 `using Duxel.Platform.Linux;`와 `DuxelLinuxApp.Run(options, session, new LinuxAtSpiOptions { Enabled = false })`를 사용하세요. 비트맵에는 GdkPixbuf도 필요하며 Linux Skia 자산은 Duxel.Svg를 통해 제공합니다. 자체 제작한 이미지 자료는 포함하며 폰트는 포함하지 않습니다.

PowerShell에서 실행하세요.

```powershell
wsl.exe -d Ubuntu --exec /mnt/l/Cache/Duxel/linux-accessibility-2026-11-14/publish-sample/Duxel.Linux.Sample /usr/share/fonts/truetype/dejavu/DejaVuSans.ttf /mnt/c/Windows/Fonts/malgun.ttf
```

설치된 폰트의 명시적 경로 두 개가 필요합니다. 필요하면 이 호스트의 Windows 한글 폰트 경로를 사용할 문자를 지원하는 설치된 폰트로 바꾸세요. 현재 소스 빌드는 기존 .NET11 분리 검증 설정을 사용합니다.

```powershell
Set-Location W:/MyWorks/Duxel/tests/managed-validation
dotnet publish W:/MyWorks/Duxel/samples/Duxel.Linux.Sample/Duxel.Linux.Sample.csproj -c Release -warnaserror -r linux-x64 --self-contained true -p:PublishAot=false -p:RuntimeFrameworkVersion=11.0.0 -p:DuxelBenchmarkFramework=net11.0 -p:DuxelSourceTargetFramework=net11.0 -p:DirectoryBuildTargetsPath=W:/MyWorks/Duxel/samples/Duxel.ImplementationObserver/LocalSource.targets -p:UseArtifactsOutput=true -p:ArtifactsPath=L:/Cache/Duxel/linux-accessibility-2026-11-14/build -o L:/Cache/Duxel/linux-accessibility-2026-11-14/publish-sample -m:1
wsl.exe -d Ubuntu --exec chmod u+x /mnt/l/Cache/Duxel/linux-accessibility-2026-11-14/publish-sample/Duxel.Linux.Sample
```

현재 관리형 .NET11 검사127건(앱/세션38·경계/배치71·폰트 선택18)·명시적/등록 실행 경로별 네이티브146건·선택 정책7건·숫자 정책5건·컨트롤214건·클립보드35건이 통과했습니다. 근거는 `artifacts/linux-accessibility-2026-11-14/text-size-verification.json`입니다. 검사는 별도 클립보드 selection을 사용하고 소유한 검사 창만 닫습니다. 일반 실행은 창을 유지하며 명시적 DUXEL_SAMPLE_AUTO_EXIT_SECONDS 설정은 시작·종료 검사용입니다.

현재 WSL Vulkan은 llvmpipe 소프트웨어 렌더링입니다. 하드웨어/ARM64/Pi·물리 IME/화면 읽기 프로그램·전체 위젯/AT-SPI 적합성·고정 RC3 NativeAOT 검증은 남아 있습니다. 네이티브 일괄 선택은 현재 바인딩에 순서대로 반영하며 트랜잭션은 아닙니다. 저장소 RC3와 Ubuntu 전역 SDK는 바꾸지 않았습니다. 세부 계약과 남은 이미지·클립보드·플랫폼 범위는 [Linux 가이드](../../docs/linux-platform.ko.md)에 있습니다.

이전 Text 조회 확장: Before/At/AfterOffset에서 스칼라·시작·끝 경계7종을 지원하며 Pango 끝점과 실제 그려진 줄을 재사용합니다. UiAccessibilityTextLayout.LineCount/GetLine의 변경 불가능 UiAccessibilityTextLine(Start, End, NextStart)에서 End는 구분자를 제외하고 자동 줄바꿈이면 End == NextStart입니다. 검사93건=앱/세션38+경계/배치55, 실행 경로별 네이티브146건, 새 공통 컨트롤214건·클립보드35건입니다. 여러 줄/자동 줄바꿈은 실제 Linux 배치 모듈 검사이며 여러 줄 네이티브 RPC 검증은 아닙니다. 정확한 한계는 artifacts/linux-accessibility-2026-11-14/text-size-verification.json과 한영 accessibility-text 가이드를 참고하세요.

단일 색 원문 그리기: UiAccessibilityNode.TextAppearance의 UiAccessibilityTextAppearance(Content, Foreground)는 실제 일반 편집기의 그리기에서 수집합니다. 내용/색이 같으면 재사용하며 그리기 생략·다른 원문·스타일·조합에는 이전 속성을 재사용하지 않습니다. 네이티브 기본/범위/이름 조회는 fg-color만 제공하고 기본 속성과 빈 명시 속성 집합을 구분합니다. 검사93건·실행 경로별 네이티브146건·새 Windows 컨트롤214건·클립보드35건입니다. 폰트 정보·스타일별 범위·속성 변경 알림은 미완료입니다. artifacts/linux-accessibility-2026-11-14/text-size-verification.json과 한영 accessibility-text 가이드를 참고하세요.

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

검사 프로젝트를 검증 디렉터리에 게시한 현재 환경에서는 아래 PowerShell 명령으로 네이티브 검사를 다시 실행할 수 있습니다. 결과 디렉터리는 새 이름이어야 하므로 고유한 이름을 만듭니다. 검사 창은 자동으로 닫힙니다. 물리 입력기·화면 읽기 세션이 아닌 자동 프로토콜 검사입니다.

```powershell
$rangeCheckOutput = "/mnt/l/Cache/Duxel/linux-accessibility-2026-11-14/manual-range-$([guid]::NewGuid().ToString('N'))"
wsl.exe -d Ubuntu --exec /mnt/l/Cache/Duxel/linux-accessibility-2026-11-14/publish-app-tests/Duxel.Linux.App.Tests $rangeCheckOutput /usr/share/fonts/truetype/dejavu/DejaVuSans.ttf /mnt/c/Windows/Fonts/malgun.ttf
```


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


### 가상 행의 원문 이동만 검사하기

아래 명령은 현재 게시한 실행 파일에서 창을 열지 않고 실제 Linux 폰트를 쓰는66개 CPU 검사만 실행합니다. 매번 새 결과 폴더를 만들며 전체 제품 창·네이티브 검사는 위의 전체 실행 명령을 사용하세요.

```powershell
$virtualCheckOutput = "/mnt/l/Cache/Duxel/linux-accessibility-2026-11-14/focused-virtual-$([guid]::NewGuid().ToString('N'))"
wsl.exe -d Ubuntu --exec /mnt/l/Cache/Duxel/linux-accessibility-2026-11-14/publish-app-tests/Duxel.Linux.App.Tests text-scroll-virtual $virtualCheckOutput /usr/share/fonts/truetype/dejavu/DejaVuSans.ttf /mnt/c/Windows/Fonts/malgun.ttf
```

### 기존 여러 줄 입력의 IME 조합

Wrap=false인 기존 편집기는 마우스를 놓고 입력을 쉬는 동안에도 포커스를 유지합니다. 한 줄 입력과 같은 포커스 관찰 방식을 사용합니다. IME 조합 중에는 기존 공통 조합 배치로 글자·밑줄·커서·입력기 위치·스크롤·접근성 원문 좌표를 함께 계산합니다. 조합을 취소하거나 확정하면 일반 줄 배치로 돌아가며, 확정 원문과 이미 게시한 변경 불가 스냅샷을 유지합니다. 소유자가 있는 입력과 없는 입력을 모두 검사합니다. 사용자 정의 조합 스냅샷은 그리기에 사용한 배치 맵을 제공해야 좌표를 게시할 수 있습니다.

제어된 조합 검사는 한 줄·줄바꿈·Markdown·기존 두 가지 입력에서104건이 통과했습니다. 실제 포인터 포커스, 한글·이모지·결합 문자, 개행, 긴 조합의 줄바꿈 또는 가로 스크롤, 취소, 한 번의 확정, 바깥 클릭의 포커스 해제를 확인했습니다. 조합을 주입한 검사이며 실제 운영체제 입력기를 검증한 것은 아닙니다. 현재 .NET11/Linux790건은 앱·세션54건+경계·배치718건+폰트 선택18건입니다. 독립 네이티브 검사는 실행 방식마다 일반146건·문서와 입력103건·기본/RTL/가상 상위 이동61건씩 통과했습니다. 새 Windows 공통227건·클립보드35건·소유 제품 샘플 자동 종료0도 확인했습니다. 근거는 `artifacts/linux-accessibility-2026-11-14/legacy-ime-layout-verification.json`입니다.

```powershell
$compositionCheckOutput = '/mnt/l/Cache/Duxel/linux-accessibility-2026-11-14/composition-' + [Guid]::NewGuid().ToString('N')
wsl.exe -d Ubuntu --exec /mnt/l/Cache/Duxel/linux-accessibility-2026-11-14/publish-app-tests/Duxel.Linux.App.Tests text-layout-composition $compositionCheckOutput /usr/share/fonts/truetype/dejavu/DejaVuSans.ttf /mnt/c/Windows/Fonts/malgun.ttf
```

### 가변 높이 가상 목록의 원문 범위 이동

기존 누적 높이 API로10억 행의 높이를256.25/384.75/192.5픽셀로 반복하고,320억 픽셀이 넘는 소수점 포함 double 스크롤 위치를 검사했습니다. 공통 가상 텍스트 검사78건으로 한 줄·여러 줄·Markdown 입력의 모든 배치, 정확한 지점 조회, 문서 끝, 행 식별자·원문·선택·포커스 유지와 생성한 행 수 제한을 확인했습니다. 앞선 행이64.5픽셀 커져도 같은 행의 화면 위치를 정확히 유지합니다. 요청 접수 뒤32.25픽셀 더 커져도 새 누적 높이로 계산한 결과를 게시합니다. 실제 가변 높이 행에 할당한 영역을 사용하며, 이번 검증에서 엔진 API나 렌더링 동작은 변경하지 않았습니다.

전용 네이티브 검사는 명시적·등록된 실행 방식으로 소유 X11/Vulkan 창을 하나씩 열고 독립 원문 이동61건씩과 실행·해제4건을 확인했습니다. 앞선 두 전체 실행에서는 실패했습니다. 기존 일반 네이티브 클라이언트가146건을 기록한 뒤 종료 코드134로 끝났고, 두 번째 전체 실행에서도 같은 종료가 발생했습니다. 범위를 좁힌 일반 클라이언트 검사에서는 GrabFocus가 거부되었습니다. GDB 진단1회도 포커스 거부를 확인했으며 앞선 결과 기록 후 종료 원인은 아직 밝혀지지 않았습니다. 실패 기록을 보존하고 전체 성공으로 집계하지 않습니다. 이전 전체790건·Windows227건·클립보드35건·샘플 종료0은 이전 시점의 근거로 유지합니다. 현재 집중 검증의 근거는 `artifacts/linux-accessibility-2026-11-14/variable-height-text-scroll-verification.json`입니다. 실제 화면 읽기·입력기, 팝업 통합, 하드웨어·ARM64·고정 RC3 NativeAOT는 남아 있습니다.

```powershell
$variableCheckOutput = '/mnt/l/Cache/Duxel/linux-accessibility-2026-11-14/variable-' + [Guid]::NewGuid().ToString('N')
wsl.exe -d Ubuntu --exec /mnt/l/Cache/Duxel/linux-accessibility-2026-11-14/publish-app-tests/Duxel.Linux.App.Tests text-scroll-variable $variableCheckOutput /usr/share/fonts/truetype/dejavu/DejaVuSans.ttf /mnt/c/Windows/Fonts/malgun.ttf
$variableNativeOutput = '/mnt/l/Cache/Duxel/linux-accessibility-2026-11-14/variable-native-' + [Guid]::NewGuid().ToString('N')
wsl.exe -d Ubuntu --exec /mnt/l/Cache/Duxel/linux-accessibility-2026-11-14/publish-app-tests/Duxel.Linux.App.Tests text-scroll-variable-native $variableNativeOutput /usr/share/fonts/truetype/dejavu/DejaVuSans.ttf /mnt/c/Windows/Fonts/malgun.ttf
```

Windows PowerShell에서 제품 샘플 창을 유지하려면 자동 종료 설정을 해제해 실행합니다. 창을 닫으면 종료됩니다. WSLg의 DISPLAY가 필요하며 기존에 게시한 제품 샘플을 사용합니다.

```powershell
wsl.exe -d Ubuntu --exec env -u DUXEL_SAMPLE_AUTO_EXIT_SECONDS /mnt/l/Cache/Duxel/linux-accessibility-2026-11-14/publish-sample/Duxel.Linux.Sample /usr/share/fonts/truetype/dejavu/DejaVuSans.ttf /mnt/c/Windows/Fonts/malgun.ttf
```
후속 검증에서는 일반 네이티브 클라이언트가 이벤트 범위와 atspi_exit의 정리를 끝낸 뒤에만 결과를 기록하도록 수정했습니다. 좁힌 검사 두 번의 정상 종료를 확인했습니다. 최종 전체 실행도 종료 코드0이며 Linux872건은 앱·세션58건+경계·배치796건+폰트 선택18건입니다. 실행 방식마다 일반 네이티브146건·문서와 입력103건·기본/RTL/고정 높이 가상 행/가변 높이 가상 행의 원문 이동61건씩 통과했습니다. 이전 포커스 거부와 결과 기록 후 종료134는 보존합니다. 이번 통과만으로 그 원인을 확정하거나 제품 포커스 오류를 고쳤다고 판단하지 않습니다. 임시 포커스·종료 추적과 지원되지 않은 콜백 해제 실험은 제거했습니다. 현재 근거는 `artifacts/linux-accessibility-2026-11-14/native-cleanup-record-verification.json`입니다. 제품 바이너리는 같으며 Windows227건·클립보드35건·샘플 종료0은 다시 실행하지 않은 이전 근거입니다.

### 떠 있는 팝업의 원문 이동 경계

떠 있는 팝업의 원문을 이동할 때 논리적 소유자의 스크롤이(180,180)에서(196,279)로 바뀌고 팝업까지 닫혀 Unavailable로 끝나는 결함을 재현했습니다. 팝업 표면에 독립적인 콘텐츠 경계를 기록해 공통 이동 계획이 트리거의 바깥 스크롤로 넘어가지 않도록 수정했습니다. 팝업 안의 실제 뷰포트는 계속 스크롤됩니다. 접근성의 논리적 부모 관계는 유지하며 공개 API를 추가하지 않았습니다.

실제 폰트를 사용하는 CPU138건으로 일반·포커스 제한 팝업의 한 줄·여러 줄·줄바꿈 없는 Markdown 입력을 검사했습니다. 모든 배치, 정확한 지점과 원문 위치 조회, 팝업·소유자 위치 유지, 원문·커서·선택·포커스 유지, 팝업 바깥 지점의 거부, 대기 중 닫은 요청의 해제를 확인했습니다. 닫기 검사 전 팝업의 뷰포트를 초기화하고 실제 대기 상태를 확인하므로 이미 끝난 요청을 취소로 오인하지 않습니다. 같은 표면을 쓰는 실제 X11/Vulkan 창6개에서 세 입력과 두 실행 방식마다 독립 네이티브21건씩, 실행·해제12건도 통과했습니다. 네이티브 팝업의 실제 부모 패널에서 뷰포트 위치를 읽으며 기존 상위 이동 검사의 고정 좌표 기준은 유지합니다.

최종 .NET11/Linux1022건은 앱·세션70건+경계·배치934건+폰트 선택18건입니다. 독립 네이티브18개 실행이 통과했습니다. 팝업6개 외에도 일반146건·문서와 입력103건·기본/RTL/고정 높이/가변 높이 상위 이동61건씩이 유지됩니다. 새 Windows 공통227건·팝업240건의 CPU 검사, 클립보드35건과 소유 제품 샘플의2초 자동 종료 코드0도 확인했습니다. 근거는 `artifacts/linux-accessibility-2026-11-14/popup-text-scroll-verification.json`입니다. 이전 일반 클라이언트의 간헐적 포커스 거부·종료134 원인은 미확정이며 실제 화면 읽기·입력기, 하드웨어·ARM64, 전체 문서 의미와 고정 RC3 NativeAOT 검증은 남아 있습니다.

```powershell
$popupCheckOutput = '/mnt/l/Cache/Duxel/linux-accessibility-2026-11-14/popup-' + [Guid]::NewGuid().ToString('N')
wsl.exe -d Ubuntu --exec /mnt/l/Cache/Duxel/linux-accessibility-2026-11-14/publish-app-tests/Duxel.Linux.App.Tests text-scroll-popup $popupCheckOutput /usr/share/fonts/truetype/dejavu/DejaVuSans.ttf /mnt/c/Windows/Fonts/malgun.ttf
$popupNativeOutput = '/mnt/l/Cache/Duxel/linux-accessibility-2026-11-14/popup-native-' + [Guid]::NewGuid().ToString('N')
wsl.exe -d Ubuntu --exec /mnt/l/Cache/Duxel/linux-accessibility-2026-11-14/publish-app-tests/Duxel.Linux.App.Tests text-scroll-popup-native $popupNativeOutput /usr/share/fonts/truetype/dejavu/DejaVuSans.ttf /mnt/c/Windows/Fonts/malgun.ttf
```

### 이미지 의미와 키보드 선택

렌더링한 이미지에 일반 대체 텍스트와 실제로 보이는 영역의 좌표를 가진 비대화형 Image 자식 노드를 게시합니다. 화면 밖의 이미지도 조회할 수 있으며 공통 항목 이동으로 가져올 수 있습니다. 사용할 수 있는 빈 대체 텍스트 이미지는 장식으로 처리해 생략하고, 사용할 수 없는 이미지는 화면의 안내 문구를 이름으로 사용합니다. 뷰에서 이미지로 만든 링크의 부모는 실제 Link 노드입니다. 편집 중인 블록은 정확한 원문을 보여주고 그 블록의 이미지 노드는 잠시 제거했다가 렌더링으로 돌아오면 다시 게시합니다. 좌표는 그리기와 같은 투영 이미지·배치 함수를 사용하며 바깥 구분선 문자가 없는 표 첫 칸의 안쪽 여백도 유지합니다. 이미지 주소 해석과 Markdown 분석 결과는 계속 재사용합니다.

키보드가 숨겨진 이미지 주소 안으로 들어가 한 번의 Shift+Right가 원문 위치8에서 이미지 끝24로 가지 않고9에 머무는 결함을 재현했습니다. 렌더링한 이미지는 전체 원문 경계를 한 번에 지나도록 좌우 이동과 위치 조회를 수정했고, 보이지 않는 문법 기호도 건너뜁니다. 편집 중인 원문 블록에서는 기존 문자 이동을 유지합니다. 선택 색을 불투명 이미지 위에 표시해 Shift 선택이 보이며, 선택을 해제하면 원래 그림으로 돌아옵니다. 이미지의 행은 줄바꿈 경계의 커서 방향 상태와 분리해 계산합니다. CPU84건으로 뷰·편집기, 일반·링크 이미지, 큰 이미지의 좌우·상하 이동과 Shift 조합을 확인했습니다. 실제 X11 Shift+Right·Left 입력과 소유 창의 픽셀 캡처로 이미지 전체 선택, 선택 색, 선택 해제와 원문 유지를 검증했습니다. 이미지 크기 조절 같은 객체 편집 기능을 추가한 것은 아닙니다.

기본 이미지 의미는 CPU31건과 실행 방식별 독립 네이티브21건으로 역할·이름·링크 부모·좌표·위치 조회·원문 이동·실제 키보드와 픽셀 변화를 검사했습니다. 최종 관리형 .NET11/Linux1141건은 앱·세션74건+경계·배치1049건+폰트 선택18건이며 독립 네이티브20개 실행이 통과했습니다. 새 Windows 공통227건·복합 및 Markdown415건의 CPU 검사, 전용 클립보드35건·소유 제품 샘플의2초 자동 종료 코드0도 통과했습니다. 네이티브 Image 인터페이스와 일반 Component.ScrollTo, 실제 화면 읽기·입력기, 원격 이미지 정책, 하드웨어·ARM64·고정 RC3 NativeAOT 검증은 남아 있습니다. 근거는 `artifacts/linux-accessibility-2026-11-14/markdown-image-interaction-verification.json`입니다.

```powershell
$imageKeyOutput = '/mnt/l/Cache/Duxel/linux-accessibility-2026-11-14/image-keys-' + [Guid]::NewGuid().ToString('N')
wsl.exe -d Ubuntu --exec /mnt/l/Cache/Duxel/linux-accessibility-2026-11-14/publish-app-tests/Duxel.Linux.App.Tests markdown-image-keyboard $imageKeyOutput /usr/share/fonts/truetype/dejavu/DejaVuSans.ttf /mnt/c/Windows/Fonts/malgun.ttf
$imageNativeOutput = '/mnt/l/Cache/Duxel/linux-accessibility-2026-11-14/image-native-' + [Guid]::NewGuid().ToString('N')
wsl.exe -d Ubuntu --exec /mnt/l/Cache/Duxel/linux-accessibility-2026-11-14/publish-app-tests/Duxel.Linux.App.Tests markdown-image-accessibility-native $imageNativeOutput /usr/share/fonts/truetype/dejavu/DejaVuSans.ttf /mnt/c/Windows/Fonts/malgun.ttf
```

SVG 백엔드 변경(2026-11-14): 자체 기하·래스터 처리와 HarfBuzz 텍스트로 Skia를 대체했습니다. 내장 비트맵은 GdkPixbuf로 읽습니다. 기존 PNG·SVG·회전 조절기와 실행 명령은 유지합니다. Windows는 이 외부 SVG 라이브러리 없이 시스템 DirectWrite·WIC를 사용합니다. ../../docs/svg-platform-rendering.ko.md와 artifacts/svg-managed-2026-11-14/verification.json을 참고하세요.

