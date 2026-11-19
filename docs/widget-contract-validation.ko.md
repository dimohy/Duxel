# 위젯 계약 통합 검증

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
