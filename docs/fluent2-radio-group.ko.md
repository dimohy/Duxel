# Fluent 2 RadioGroup

기본 RadioGroup 배치는 상위 `FluentProvider.Direction`을 따릅니다. 오른쪽에서 왼쪽으로 배치하면 가로 항목은 오른쪽에서 시작하며, 표시 원과 옆 라벨의 자리도 바뀝니다. 아래 라벨은 가운데에 둡니다. 좌우 방향키는 화면에 보이는 순서대로 이동하며, 위아래 방향키와 선택값은 방향 전환 뒤에도 유지됩니다.

정식 .NET 11.0.0의 Windows 관리형과 NativeAOT에서 부모 할당 검사 264개를 각각 통과했습니다. MSAA 1·4와 프레임 슬롯 3에서 GPU RGB 이미지 245쌍이 모두 일치했습니다. 근거는 `W:/Cache/Duxel/gallery-source-20261117/radio-ga-1910/comparison.json`에 있습니다.

11월 19일에는 현재 Windows 관리형과 보관해 둔 Windows x64 NativeAOT 실행 파일에서 320 × 240으로 각각 검사 264건을 통과했고, 이미지 245쌍의 해시가 일치했습니다. Linux x64는 현재 소스로 관리형과 NativeAOT를 새로 빌드해 400 × 300으로 각각 212건을 통과했고, 이미지 244쌍이 모두 일치했습니다. Windows와 Linux를 400 × 300으로 맞췄을 때는 글자 없는 이미지 4쌍이 일치했습니다. 래스터 라벨 96쌍은 2,245~2,427픽셀, 벡터 라벨 96쌍은 21~755픽셀이 달랐습니다. 결과와 보관된 Windows 실행 파일의 해시는 `W:/Cache/Duxel/gallery-source-20261117/gallery-uia-2082`에 있습니다.

Linux 플랫폼 검사도 같은 부모 배치와 생성·즉시 모드 GPU 검사를 사용합니다. Linux 테스트 실행 파일에 새 결과 폴더, `radio-allocation`, 일반·중간 굵기·굵은 글꼴 경로를 순서대로 넘기면 됩니다. WSLg Ubuntu x64에서 Mesa lavapipe를 사용해 .NET 11 관리형·NativeAOT가 각각 212건을 통과했고 두 빌드의 이미지 244장이 모두 일치했습니다. 프레임버퍼는 Windows 400 × 300, WSLg 320 × 240이었습니다. 두 화면이 겹치는 320 × 240 영역에서 글자 없는 사용자 정의 배치 이미지 4장은 같았고, 라벨이 있는 240장은 차이가 있었습니다. 이 비교만으로 글자 렌더링과 다른 플랫폼 차이의 영향을 각각 판별할 수는 없습니다. 근거는 `W:/Cache/Duxel/gallery-source-20261117/radio-cross-host-1964/verification.json`입니다.

`radio-allocation-matched` 검사에서는 두 환경의 프레임버퍼를 400 × 300으로 맞추고 같은 Segoe UI 파일과 기본 글자 크기 16을 사용했습니다. DirectText 대체 경로를 껐습니다. Windows와 WSLg에서 글자 없는 전체 이미지 4장은 일치했습니다. 래스터 라벨 사례 96개에서는 프레임당 2,245~2,427픽셀, 벡터 라벨 사례 96개에서는 21~755픽셀이 달랐습니다. 글리프 위치·커버리지와 드라이버 영향은 각각 확인이 필요합니다. 근거는 `W:/Cache/Duxel/gallery-source-20261117/radio-matched-1965/verification.json`입니다.

같은 Segoe UI 일반 글꼴로 라벨 7개를 14·16px에서 측정했습니다. .NET 11 관리형에서 DirectWrite와 Linux 텍스트 백엔드를 직접 호출했습니다. 14개 모두 글자의 진행 폭과 벡터 폭·기준선이 일치했고, 벡터 잉크 경계의 최대 차이는 0.008px 미만이었습니다. 각 래스터 이미지 자체의 기준선은 0.73~1.89px 차이가 났고, 투명 여백을 잘라낸 알파 마스크도 14개 모두 달랐습니다. 차이는 Vulkan에 넘기기 전부터 확인됐습니다. 최종 위젯에서의 기준선 정렬은 별도로 확인해야 합니다. 근거는 `W:/Cache/Duxel/gallery-source-20261117/radio-text-backends-1966/verification.json`입니다.

같은 라벨을 배율 1의 `UiContext`에서 그려 제출할 사각형의 좌표도 확인했습니다. 픽셀 스냅을 끄면 이미지별 기준선 보정이 적용돼 28개 모두 실제 배치 기준선이 같았습니다. 스냅을 켰을 때 두 환경의 최대 차이는 Y = 40에서 0.266px, Y = 40.25에서 0.734px였습니다. 각 환경의 기준선은 목표 위치에서 0.5px 이내였습니다. 이 수치는 Vulkan이 텍스처를 샘플링하기 전 그리기 명령의 좌표입니다. 근거는 `W:/Cache/Duxel/gallery-source-20261117/radio-placement-1967/verification.json`입니다.

Windows와 WSLg의 `ScrollView`에 “More context.” 라벨을 그리고 Vulkan 픽셀을 읽었습니다. 콘텐츠 배율 100%·125%·150%에서 스냅을 켜고 끈 채 스크롤 위치 6개를 적용했습니다. 스냅을 켜면 각 환경과 배율에서 위치가 달라져도 글자 영역의 RGB 픽셀이 모두 같았습니다. 스냅을 끄면 같은 배율에서 Windows는 글자 모양 2개, WSLg는 1개를 기록했습니다. 검사에서는 스크롤 오프셋과 콘텐츠 배율을 코드로 설정했습니다. 근거는 `W:/Cache/Duxel/gallery-source-20261117/text-scroll-pixels-1968/verification.json`입니다.

저장소 루트에서 `./run-fba.ps1 samples/fba/layout_radio_allocation_fba.cs -TargetFramework net11.0-windows7.0 -NoCache`를 실행하세요. 세 배치에서 Width와 Height를 바꾸고 항목을 눌러 줄바꿈과 선택 영역을 확인할 수 있습니다.

갤러리에서 **입력과 선택 → RadioGroup**을 열고 **부모 폭에 따라 줄바꿈**까지 내리세요. **부모 폭 전환**을 누르면 부모의 최대 너비가 180과 360 논리 픽셀 사이에서 바뀝니다. 선택한 항목은 그대로 남고, 아래 C# 코드에 현재 너비와 선택값이 표시됩니다.

Windows 갤러리 검사에서 폭을 넓히면 긴 항목의 너비가 늘고 높이가 줄며, 선택값이 유지되고 두 코드 영역에 각 예제의 실제 생성 코드가 표시되는 것을 확인했습니다. Linux 외부 검사기는 `x11-gallery-radio-allocation-pixels` 뒤에 갤러리 프로세스 ID, 결과 JSON 경로, X11 창 ID를 받습니다. WSLg에서 .NET 11.0.0 관리형과 NativeAOT가 각각 7개 검사를 통과했고, 화면 캡처 네 쌍은 진단 헤더와 흐려지는 스크롤바를 제외하면 일치했습니다. 현재 Windows 관리형의 부모 할당 GPU 이미지 245장도 보관된 Windows NativeAOT 해시와 모두 일치했습니다. 근거는 `W:/Cache/Duxel/gallery-source-20261117/radio-gallery-allocation-2116`에 있습니다.

## 부모 할당의 런타임 비교 — 2026-11-18

동일한 소스를 SDK 11.0.100-rc.3.26522.148와 런타임 11.0.0-rc.3.26522.148로 고정해 Windows x64 관리형과 NativeAOT를 비교했습니다. MSAA 1·4와 프레임 슬롯 3에서 각각 264개 검사를 통과했습니다. GPU에서 읽어 저장한 RGB 이미지 245쌍이 모두 바이트 단위로 일치했습니다. 결과는 `W:/Cache/Duxel/gallery-source-20261117/radio-rc3-1901/comparison.json`에 있습니다. 이번 비교는 RadioGroup 부모 할당 검증입니다.

## 선택 접근성 — 2026-11-11

기존 생성·제네릭·직접 RadioGroup 경로가 이름 있는 `RadioGroup`과 `RadioButton` 자식을 공개합니다. Windows에서는 Group50026/Selection10001과 RadioButton50013/SelectionItem10010을 사용합니다. [Microsoft RadioButton 계약](https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-supportradiobuttoncontroltype)에 따라 라디오 항목은 Toggle·Invoke를 제공하지 않습니다. SelectionContainer는 실제 소유 그룹을 가리키며 단일 선택·필수 선택 정책을 공개합니다. 호출자가 제공한 null 또는 없는 값은 바인딩을 임의로 바꾸지 않고 선택 없음으로 표시합니다. 선택한 항목은 RemoveFromSelection으로 해제할 수 없습니다.

형식 있는 Select·Focus는 기존 요청 큐, 현재 활성·클립·팝업 검사와 실제 호출자 콜백을 재사용합니다. 선택하지 않은 항목으로 포커스만 이동할 수 있고 유휴 프레임과 기존 방향키 탐색에서도 유지됩니다. 항목 설명은 HelpText를 제공합니다. 명시적 그룹 이름과 Field·Tooltip 이름은 공통 규칙을 따릅니다. 항목 ID는 불변 값에 연결되어 순서 변경에도 유지되며 교체·제거된 값은 폐기합니다. ID는 콘텐츠 소유 기간에만 보관하며 Dispose 시 해제합니다. 부모 할당·옵션 기하·일반 포인터와 키 처리는 유지합니다.

집중 의미 검사18개·기존 입력과 할당 검사91개·실제 Observer137개가 통과했습니다. 외부 UIA 클라이언트9개/호스트3개로 실제 선택·속성 알림·소유 관계·비활성 상태·제거 후 정확한0x80040201을 확인했습니다. ObserverGPU47개와 기존 본문73개 일치로 시각 경로를 확인합니다. 외부 UIA와 정확한 소스 근거는 `artifacts/radio-accessibility-2026-11-11/verification.json`에 기록합니다. 이 집중 계약은 전체 내레이션·Fluent 상태·메모리와 성능·정확한 RC3 NativeAOT 검증을 완료하지 않습니다.

## RadioGroup 부모 할당 — 2026-11-10

실제 부모 너비로 텍스트를 줄바꿈합니다. 정적 `IUiRadioGroupPresentation.Arrange`는 자연 옵션 기하를 변경하지 않고 할당된 루트 크기를 반환합니다. Fluent 옵션의 자연 크기와 시작 정렬 입력 영역을 유지하므로 루트의 빈 공간은 선택되지 않습니다. 루트 클립은 그리기와 입력에 적용됩니다. 높이만 바뀌면 `ReuseLayout=true`인 표현의 자연 측정을 재사용하며 높이에 의존하는 사용자 표현은 false를 유지합니다. 미지원 또는 잘못된 Arrange는 명시적으로 실패합니다.

현재 소스의 .NET11 RC3 관리 검증264개가 MSAA1/4·프레임 슬롯3에서 통과했고 readback245이미지를 생성했습니다. 검사기와 새 `layout_radio_allocation_fba.cs` 샘플 빌드는 경고·오류0개입니다. 샘플은 세 배치·공유 선택·Width/Height를 제공하며 첫 프레임 제출과15초 정상 종료를 확인했습니다. 수동 입력·외형 승인은 별도입니다.

Visual Studio C++ Build Tools/vswhere.exe 부재로 Native 게시가 실패했습니다. Native 실행과 관리/Native 이미지 비교는 미검증이며 이전 L:/Cache 근거는 접근할 수 없습니다. 근거:`artifacts/radio-resume-2026-11-10/verification.json`. 엔진15/27·위젯9/21·컨테이너3/6·통합8/15·이식5/8을 유지하며 Fluent/전체 분모는 미확정입니다. 리사이즈 보류를 유지하며 성능 개선은 주장하지 않습니다.


2026-11-06. [공식 Radio group 지침](https://fluent2.microsoft.design/components/web/react/core/radiogroup/usage)은 현재 Preview로 표시된다. Microsoft Fluent UI `8830c10bfb2f4c1bdd540287b09713b9679cd1ad`의 Radio/RadioGroup 스타일·동작을 고정해 참고했다. 원본과 해시는 `artifacts/fluent-radio-2026-11-06/plan.json`에 보존한다. 다른 플랫폼 저장소의 전체 API로 범위를 확대하지 않았다.

`Fluent.RadioGroup`은 실제 단일 선택·그룹 입력·레이아웃·그리기를 가진 생성 위젯이다. 기존 개별 `RadioButton`에 이름만 붙이지 않았다. 레거시 RadioButton은 보존한다. `Dux.RadioGroup`과 생성 위젯, 제네릭 즉시 경로가 같은 구현을 사용한다.

```csharp
private readonly UiState<string?> _choice = new("balanced");
private static readonly UiRadioOption[] Choices =
[
    new("fast", "Efficient", Description: "Prefer shorter processing time."),
    new("balanced", "Balanced"),
    new("disabled", "Unavailable", false)
];

protected override IUiView Build(UiBuildContext context)
{
    return context.Child("choice", () =>
        Fluent.RadioGroup("Processing mode", _choice.Binding, Choices));
}
```

값은 문자열 키이며 중복/null 키와 null 라벨은 생성 전에 거부한다. 명시적인 빈 문자열 키는 허용하고, 선택 없음은 null로 구분한다. 외부 선택값이 현재 항목에 없으면 표시상 미선택이며 바인딩을 임의로 바꾸지 않는다. 권장 기본 선택은 위 예제처럼 호출자가 명시한다.

## 동작과 수명

Tab은 그룹을 한 번 방문하고, 선택된 활성 항목 또는 첫 활성 항목에 포커스를 표시한다. 화살표는 비활성 항목을 건너뛰고 양방향 순환하며 선택한다. Space는 눌렀다 놓을 때 선택하고 Enter는 변경하지 않는다. 이미 선택된 항목은 해제하지 않으며 같은 값의 콜백을 반복하지 않는다. 한 프레임에 여러 화살표 이벤트가 들어와도 중간 선택의 콜백 순서를 보존한다.

라벨/지표 클릭, 누른 항목과 다른 곳에서의 놓기, Escape, 항목 제거, 그룹 제거와 비활성화를 구분한다. `SetItems`는 검증 후 원본 배열을 복사한다. 순서가 바뀌어도 누른 키가 남아 있으면 새 위치에서 누름을 유지하며, 제거되면 취소한다. 표시 제목 변경은 생성 위젯의 입력 정체성을 바꾸지 않는다.

생성된 클래스를 상속해 `OnChanged`를 확장할 수 있다. 수명 종료 시 바인딩·항목·레이아웃·콜백 임시 목록을 해제한다. 바인딩 자체는 외부 소유다. 직접 호출용 `UiRadioGroupContent<TPresentation>`는 한 논리적 컨트롤의 입력 상태/스크래치 저장소이므로 컨트롤마다 별도로 보유한다. 선택 바인딩은 여러 그룹에서 공유할 수 있다.

## 컴파일된 표현

기본은 수직이다. 수평/아래 라벨 배치는 구현 타입을 선택한다.

```csharp
[UiWidgetStyle(typeof(UiRadioGroup<,>), typeof(UiFluentRadioGroupBehavior),
    typeof(UiFluentRadioGroupPresentation<UiRadioHorizontalStackedLayout>))]
public partial class ProductRadioGroup;
```

`UiRadioVerticalLayout`, `UiRadioHorizontalLayout`, `UiRadioHorizontalStackedLayout`은 배치 알고리즘을 제공한다. 사용자 동작과 표현도 각각 교체할 수 있다. 라벨은 Regular14/20, 설명은 Regular12/16이다. 지표16px·테두리1px·선택 원10px를 사용하며 선택된 원은 혼합 Checkbox와 같은 outlined brand 토큰을 공유한다. 런타임 구현 레지스트리나 리플렉션으로 스타일을 찾지 않는다.

폭은 줄바꿈을 위한 가용 폭이다. 일반 문장은 여러 줄로 배치하고 말줄임표로 대체하지 않는다. 아주 긴 단일 단어는 공통 텍스트의 최소 내용 폭을 사용할 수 있다. 모든 문자/폰트/좁은 폭의 공식 브라우저 일치가 완료됐다는 뜻은 아니다.

## 보존과 검증

동일한 폭·폰트·배율·백엔드 세대·컨텍스트에서는 측정과 줄 문자열을 보존한다. 컨텍스트는 약하게 참조한다. 추가 줄바꿈/줄 분리 문자열은 그룹당4,096자까지 보유하며 큰 문자열은 그 프레임에서 그린 뒤 보존하지 않는다. 폰트 경로는 각각1,024자, 제목 키는1,024자 이내에서만 캐시한다. 사용자 표현의 레이아웃 재사용은 기본으로 꺼져 있고 명시적으로 결정적 입력 계약에 동의할 때 선택한다. 측정 실패는 임시 행을 지우고 예외를 전달하여 다음 프레임에 부분 결과를 재사용하지 않는다.

새 `UiControlTextBlock`은 줄 문자열·폭을 측정 시 유지하고 각 줄을 명시적인 행 높이로 그린다. Bounds는 배치에 사용하며 글리프의 실제 잉크는 호출자의 clip을 따른다. advance 폭만으로 글리프 가장자리를 자르지 않는다. 기존 단일 줄 함수는 동작을 유지한다. 실제 readback에서 발견한 마지막 줄 잘림과 첫 MSAA4 중앙 정렬 비교 실패를 보존하고, 독립20px 행 위치·첫/마지막 줄의 실제 픽셀로 수정했다.

`--radio-only` 입력/캐시 검사 후 관리 .NET10과 고정 .NET11 RC3 NativeAOT GPU를 구분해 검증한다. `--radio` 갤러리에는 세 배치를 제공한다. 최신 수치·해시·원시 이미지 비교는 `artifacts/fluent-radio-2026-11-06/verification.json`을 따른다. 새로운 FPS/Private/Pi 개선율은 주장하지 않는다.

공식 브라우저 픽셀 일치, 전체 UIA 발화와 상태 범위, 논리적 RTL, 강제 색상, 전체 required/Field 연계, toolbar 내 특수 키보드 규칙과 전체 모션 검증은 남는다. 위 집중 Selection 계약은 전체 RadioGroup 또는 전체 Fluent 완료와 구분한다.

2026-11-06 공통 텍스트 캐시 보완: 추가 줄 배열 합계256칸을 초과하면 해당 프레임은 그대로 그린 뒤 캐시를 해제한다. 빈 줄이 많아 문자 수가 작아도 큰 배열을 무기한 보존하지 않는다. 근거 `artifacts/fluent-link-2026-11-06/verification.json`.

2026-11-12 — 결정론적인 기본 제공 리프 표현을 기존 부모 측정 캐시에 연결했으며 그리기와 입력은 계속 실행합니다. 사용자 특성은 명시적 동의가 필요하고 라벨·옵션·크기·항목·환경 변경은 배치를 무효화합니다. [공통 리프 측정 계약](widget-cpu-retention.ko.md)과 artifacts/leaf-layout-retention-2026-11-11/verification.json을 참고하세요. 전체 NativeAOT·성능 게이트는 남아 있습니다.

.NET 11 정식 버전의 부모 배치 검증은 Windows와 Linux에서 같은 400×300 프레임버퍼를 사용했습니다. Windows의 관리형과 NativeAOT는 각 264건을 통과했고 GPU 캡처 245장이 모두 일치했습니다. Linux에서는 두 빌드가 각각 212건을 통과했고 캡처 244장이 모두 일치했습니다. 운영체제 간에는 글꼴을 쓰지 않은 네 장이 일치했습니다. 라벨 영역을 제한한 48건의 픽셀 차이는 모두 할당된 영역 안에 있었습니다.

갤러리의 **입력과 선택 → RadioGroup**에서 세로로 배치된 항목을 조작할 수 있습니다. Linux에서 갤러리를 띄운 뒤 외부 검증기 `Duxel.Linux.App.Tests`의 `x11-gallery-radio-group <갤러리 PID> <새 결과 JSON 경로> <10진수 X11 창 ID>`를 실행하세요. 기본 선택, 위·아래 방향키에 따른 포커스와 선택, AT-SPI로 항목을 선택하는 동작을 확인합니다.

Linux Vulkan 검사 명령에 결과 폴더, 프레임 슬롯 수, 글꼴 경로 다섯 개를 넘긴 뒤 `radio-parent-pixels`를 붙이면 Windows와 같은 부모 할당 검사를 실행합니다. 세로·가로·가로 쌓기 배치와 소스 생성기로 만든 사용자 표시 방식을 확인합니다. 현재 WSLg의 .NET 11.0.0 관리형과 NativeAOT는 프레임 슬롯 1개와 3개, 1배와 4배 MSAA에서 각 실행마다 20건을 통과했습니다. 같은 슬롯에서 런타임을 비교한 104쌍과 이전 빌드와 비교한 208쌍이 모두 일치했습니다. 현재 Windows 관리형은 264건을 통과했습니다. 두 운영체제의 같은 320×240 영역을 비교했을 때 글자 없는 이미지 4쌍은 일치했고, 라벨이 있는 48쌍의 차이는 부모가 할당한 영역 안에 있었습니다. [Linux 이미지 해시](L:/Cache/Duxel/radio-parent-2148/verification.json) · [Windows와 Linux 비교](L:/Cache/Duxel/radio-parent-2148/cross-host.json)

현재 Linux 갤러리의 관리형과 NativeAOT는 각각 외부 AT-SPI·X11 검사 7건을 통과했습니다. 부모 최대 폭을 180에서 360 논리 픽셀로 바꿔도 선택이 유지됐습니다. 두 런타임의 본문 4쌍과 같은 런타임에서 반복해 캡처한 본문 4쌍이 모두 일치했습니다. [갤러리 화면 비교](L:/Cache/Duxel/radio-parent-2148/gallery-comparison.json)

FluentProvider 갤러리의 배치 방향 예제에서 매일·매주·매월을 선택할 수 있습니다. Linux 검사 `x11-gallery-provider-radio-direction <갤러리 PID> <새 결과 JSON> <10진수 X11 창 ID>`는 좌우 방향키가 화면에서 보이는 이웃으로 이동하고 배치 방향을 바꿔도 선택이 유지되는지 확인합니다. WSLg의 .NET 11.0.0 관리형과 NativeAOT에서 각각 10건을 통과했습니다. [캡처 비교](W:/Cache/Duxel/gallery-source-20261117/radio-direction-2126/verification.json)
