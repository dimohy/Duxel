# FluentProvider

갤러리의 **영역별 배치 방향** 예제에서 가로 RadioGroup의 순서와 표시 위치를 비교할 수 있습니다. 좌우 방향키는 화면에 보이는 순서를 따르며, 배치 방향을 바꿔도 기존 선택은 유지됩니다.

같은 예제의 **미리 알림** 체크박스와 **소리** 스위치도 방향을 따릅니다. 방향을 바꾸면 표시와 라벨의 좌우 위치가 바뀌고 스위치 손잡이가 반대 방향으로 움직입니다. 현재 체크 상태는 유지됩니다.

**알림 음량** Slider는 배치 방향을 바꾸면 채워진 막대, 손잡이와 눈금이 좌우로 뒤집힙니다. 마우스와 좌우 방향키도 표시 방향을 따르며, 전환할 때 현재 음량은 유지됩니다. 세로 Slider는 위아래 방향을 유지하고, 가로 Slider의 `Reversed`는 영역의 방향을 기준으로 한 번 더 뒤집습니다.

Windows .NET 11에서 방향 검사 42건, 렌더링 검사 3,155건, 갤러리 시나리오 104건을 통과했습니다. 기존 Slider 이미지 5장은 직전 실행과 바이트 단위로 일치했고, 변경한 음량이 반영된 예제 소스도 빌드했습니다. 근거는 `W:/Cache/Duxel/gallery-source-20261117/slider-direction-1956/verification.json`입니다.

Windows x64 관리형과 NativeAOT는 Slider 검사 168건씩 통과했습니다. 밝은·어두운 테마, 명시적 방향 반전, MSAA 1×·4×에서 Vulkan 이미지의 좌우 반전을 대조한 결과는 런타임별 8건 모두 픽셀 오차 0이었습니다. 두 런타임의 이미지 16장도 일치했습니다. WSLg x64 NativeAOT 갤러리는 화면 표시 시나리오 104건을 통과했고, 외부 AT-SPI 클라이언트로 RTL에서 바꾼 값이 LTR에서도 유지되는 것을 확인했습니다. 근거는 `W:/Cache/Duxel/gallery-source-20261117/slider-native-1957/verification.json`입니다.

Linux 플랫폼 테스트에서도 Windows와 같은 방향 검사 코드를 사용합니다. Linux 테스트 실행 파일에 새 결과 폴더와 `slider-direction`을 차례로 넘기면 됩니다. WSLg Ubuntu x64에서 Mesa lavapipe와 화면에 표시하지 않은 X11 창을 사용해 .NET 11 관리형·NativeAOT에서 각각 12건을 통과했습니다. 전체 프레임 이미지 16장은 Windows 관리형·NativeAOT 기준 이미지와 바이트 단위로 일치했습니다. 근거는 `W:/Cache/Duxel/gallery-source-20261117/slider-cross-host-1963/verification.json`입니다.

WSLg Linux x64 NativeAOT 갤러리에서 별도 AT-SPI 클라이언트로 두 컨트롤을 켜고 끈 뒤 방향을 바꿨습니다. 체크 상태가 유지되고 RadioGroup 항목의 좌우 위치가 바뀌는 것을 확인했습니다. 같은 실행 파일로 일반 화면 표시 시나리오 104개도 통과했습니다. 근거는 `W:/Cache/Duxel/gallery-source-20261117/toggle-native-1955/provider/result.json`과 같은 실행 폴더의 `linux-full/result.json`입니다.

Windows x64 NativeAOT 그래픽 테스트에서도 FluentProvider 방향 검사 33건을 모두 통과했습니다. 같은 실행 폴더의 `windows-direction/result.json`과 통합 결과 `verification.json`에서 확인할 수 있습니다.

갤러리는 `./run-gallery.ps1` → **테마와 설정 → FluentProvider**에서 실행합니다. 밝은 영역·어두운 영역·중첩 테마를 비교하고 영역별 테마와 배경을 바꿀 수 있습니다. 입력값과 각 예제 바로 아래 소스는 함께 바뀝니다. 네 번째 예제에서는 현재 플랫폼에 설정된 글꼴을 사용해 해당 영역만 고정폭 글꼴로 바꿉니다. NEW는 갤러리의 최신 추가 묶음에 표시합니다.

```csharp
var name = new UiState<string>(string.Empty);
var provider = Fluent.FluentProvider(
    Fluent.Input("이름", name.Binding),
    UiFluentProviderTheme.Dark,
    new(Padding : new(16), Width : 400));
provider.Theme = null; // Inherit the enclosing theme.
provider.PaintBackground = false;
```

`UiThemeProvider`는 자식을 소유하는 공통 구현이고 `UiFluentProvider`는 생성 DSL 어댑터입니다. Theme는 변경할 수 없는 UiFluentProviderTheme 인스턴스를 받습니다. Light·Dark를 재사용하거나 기존 UiTheme 팔레트에 UiDesignTokens와 UiTypographyFonts를 선택적으로 지정할 수 있습니다. null은 현재 부모 팔레트·토큰의 상속을 뜻합니다. 토큰을 생략한 팔레트 변경은 부모의 기하 토큰을 유지합니다. Options는 일반 UiContainerOptions이며 여백·크기·제약을 같은 배치 엔진으로 처리합니다.

측정과 그리기는 같은 범위를 사용하며 정상 종료와 예외에서 부모 설정을 복원합니다. 기하 토큰과 전체 글꼴 묶음은 공통 측정 키에 포함됩니다. 일반 글꼴을 그대로 두고 중간 굵기 글꼴만 바꿔도 필요한 배치를 다시 측정합니다. 기존 컨테이너처럼 자식의 첫 배치가 준비된 뒤 측정을 재사용합니다. 시스템 대비 색상과 비활성 불투명도는 하위 팔레트에서도 유지합니다. 자동·제어형 Tooltip은 지연 표시 시점에도 원래 영역의 팔레트·토큰·글꼴 경로를 사용합니다. 앱 테마와 제목 표시줄은 바꾸지 않습니다.

네이티브 UiTheme·기하 토큰·글꼴 묶음을 영역별로 지정할 수 있습니다. Fonts를 생략하면 부모 글꼴을 상속합니다. 지정하면 일반·중간 굵기·굵은 글꼴과 보조·고정폭 글꼴을 선택합니다. 일반 컨트롤 텍스트에도 해당 영역의 일반 글꼴을 적용합니다. 영역을 벗어나거나 예외가 발생하면 부모 글꼴과 텍스트 경로를 복원합니다. 전역 측정 자원과 글꼴 캐시, 운영체제 DPI와 글자 크기 설정은 유지됩니다. 전체 브랜드·타이포그래피 토큰 재정의와 웹 CSS·DOM 동작은 남아 있습니다.

검증 기록은 artifacts/fluent-provider-2026-11-17/result.json입니다. 통과한 항목은 DLL 지문을 확인해 재사용했으며, 측정 준비 시점을 보완한 항목과 오래된 Nav·Drawer 카탈로그 기대값만 집중 확인했습니다. 실제 Linux와 고정 RC3 NativeAOT는 실행하지 않았습니다.

공식 근거: https://fluent2.microsoft.design/components/web/react/core/fluentprovider/usage

## 영역별 글꼴

앱에서 선택한 설치 글꼴 경로를 재사용합니다. 기존 네이티브 텍스트 백엔드가 처리하므로 렌더링 라이브러리를 추가하지 않습니다.

```csharp
UiTypographyFonts fonts = appFontOptions.Typography;
var mono = fonts.Monospace ?? throw new InvalidOperationException("Configure a monospace font.");
var localFonts = fonts with { Regular = mono, Semibold = mono, Bold = mono };
provider.Theme = new(UiCompiledDesign.Windows11Dark.Theme, fonts : localFonts);
```

글꼴 검증 기록은 `artifacts/fluent-provider-fonts-2026-11-17/result.json`에 있습니다. 글꼴 상속·복원·캐시·Tooltip 검사, 갤러리 설정과 소스 동기화, Vulkan의 실제 글자 출력은 각각 확인했습니다. 전체 FluentProvider 적합성 검증이 끝났다는 뜻은 아닙니다.

## 영역별 배치 방향

`provider.Direction`의 기본값은 `UiFlowDirection.Inherit`입니다. `RightToLeft` 또는 `LeftToRight`를 지정하면 해당 영역과 자식 컨트롤에 적용됩니다. 오른쪽에서 왼쪽으로 배치하는 영역 안에서도 자식 Provider를 왼쪽부터 배치하도록 설정할 수 있습니다. 방향을 바꾸면 필요한 배치를 다시 측정하고 각 위젯과 현재 선택·입력값을 유지합니다.

NavigationBar, Nav, Breadcrumb, TabList, Carousel, SegmentedControl, Wrap, ScrollView, Dropdown, Select, Combobox, Tree, Tag, InteractionTag, TagGroup, TagPicker, 메뉴 팝업, Popover와 Tooltip이 이 방향을 따릅니다. 위젯의 `RightToLeft`가 `true`이면 오른쪽부터 배치하고, 기본값인 `false`이면 Provider를 따릅니다. 왼쪽부터 배치할 영역에는 중첩 Provider의 `LeftToRight`를 지정하세요. Tooltip은 나중에 나타나도 원래 영역의 방향을 따릅니다. RTL에서는 Tree를 접고 펼치는 방향키가 반대로 동작하며, TagPicker에서 검색창이 비어 있을 때 가까운 태그 쪽 방향키로 포커스를 옮길 수 있습니다. 문단 조판과 입력 중 커서 이동은 각각의 방향 설정을 사용합니다.

Toolbar는 버튼 순서와 좌우 방향키를 함께 반전합니다. SearchBox는 검색·지우기 아이콘을 반전하고, 안내 문구를 해당 방향의 시작 쪽에 표시합니다. FluentProvider 페이지 끝의 **영역별 배치 방향**에서 방향을 바꾼 뒤 도구 모음의 방향키와 검색어 지우기를 써 보세요. 아래 C# 소스에 선택값, 검색어, 방향이 반영됩니다. [FluentProvider 참조 구현](https://raw.githubusercontent.com/microsoft/fluentui/master/packages/react-components/react-provider/library/src/components/FluentProvider/useFluentProvider.ts)

고정 .NET 11 RC3의 Windows x64 관리형과 NativeAOT에서 각각 방향 25개, Toolbar 120개, SearchBox 144개 검사를 통과했습니다. Toolbar와 SearchBox의 MSAA 1·4 GPU RGB 이미지 362쌍은 모두 일치했습니다. 저장한 예제 소스와 Linux 대상 빌드가 통과했고, Windows 갤러리는 표시 시나리오 100개를 통과했습니다. 근거는 `W:/Cache/Duxel/gallery-source-20261117/provider-rc3-1904/comparison.json`에 있습니다.


`provider.Fonts = localFonts`는 Theme를 바꾸지 않고 글꼴만 지정합니다. null은 Theme.Fonts 또는 부모 글꼴로 돌아갑니다. 명시적 Fonts가 Theme.Fonts보다 우선하고 측정·그리기·예외 복원 경로는 같습니다. 태국어 달력과 입력 선택기가 이 범위를 재사용합니다.
