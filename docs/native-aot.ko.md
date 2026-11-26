# Fluent 2 앱의 NativeAOT 실행 파일 크기 줄이기

[English](native-aot.md) · [프로젝트 README](../README.ko.md)

[duxel_hello.cs](../samples/fba/duxel_hello.cs)는 시스템 글꼴을 사용해 Vulkan 창에 Fluent 2 텍스트 컨트롤 하나를 띄웁니다. 이 저장소의 Duxel 소스 프로젝트를 참조합니다.

## x64 실행 파일 실측

모든 항목은 .NET SDK **11.0.100**으로 Release 게시한 결과입니다. 실행 파일만 측정했고, 1 MB는 100만 바이트로 계산했습니다. 각 실행 파일의 첫 화면을 해당 운영체제에서 확인했습니다. Windows의 PDB와 Linux의 DBG는 별도 파일입니다.

| 소스와 게시 옵션 | Windows 11 x64 | Ubuntu 24.04 x64, WSLg |
| --- | ---: | ---: |
| 공개 Duxel 0.4.0 · NativeAOT 기본값 | 12,812,800바이트 | 13,566,208바이트 |
| 공개 Duxel 0.4.0 · 크기 우선 최적화 | 12,038,656바이트 | 13,431,344바이트 |
| 공개 Duxel 0.4.0 · 크기 우선 최적화와 고정 문화권 설정 | 11,818,496바이트 | 13,233,648바이트 |
| 저장소 소스 · 용량 절약 빌드와 아래 런타임 옵션 | **4,846,592바이트 (4.85 MB)** | **6,137,176바이트 (6.14 MB)** |

공개 패키지로 만든 Windows 실행 파일에 내장 리소스가 약 6.08 MB 들어 있었습니다. Fluent 아이콘 글꼴이 5.47 MB, Vulkan 셰이더가 0.62 MB였습니다. NativeAOT는 사용하지 않는 코드를 트리밍합니다. 앱이 어셈블리에서 읽을 수 있는 리소스는 코드가 줄어들어도 남을 수 있습니다. 용량 절약 옵션을 켜면 아이콘 글꼴을 내장하지 않고, Vulkan에 필요한 셰이더는 압축하며, 네이티브 파일 대화 상자를 쓸 수 없을 때 대신 열던 Duxel 파일 선택기는 제외합니다.

용량 절약 옵션을 적용한 Linux 실행 파일에서는 .NET 코드를 컴파일한 네이티브 명령이 3.09 MB, 호출 스택 복원 및 예외 처리용 테이블이 1.06 MB를 차지합니다. Linux에서는 아직 5 MB를 넘습니다.

## 용량 절약 빌드 재현하기

저장소를 복제한 뒤 대상 운영체제에서 저장소 루트로 이동합니다. [.NET 11 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/11.0)를 설치합니다. Windows에는 Visual Studio의 **C++를 사용한 데스크톱 개발** 워크로드가 필요합니다. Ubuntu에서는 Clang과 NativeAOT 개발 라이브러리를 설치하고 [X11과 Vulkan을 사용할 수 있는 그래픽 환경](linux-platform.ko.md)에서 실행합니다. [Microsoft NativeAOT 준비 사항](https://learn.microsoft.com/ko-kr/dotnet/core/deploying/native-aot/#prerequisites)

### Windows x64 · PowerShell

~~~powershell
dotnet publish samples/fba/duxel_hello.cs -c Release -r win-x64 `
  -p:PublishAot=true -p:DuxelCompact=true -p:OptimizationPreference=Size `
  -p:InvariantGlobalization=true -p:StackTraceSupport=false -p:EventSourceSupport=false `
  -p:UseSystemResourceKeys=true -p:IlcScanReflection=false `
  "-p:ArtifactsPath=$PWD/out/native-objects" -o out/native
(Get-Item ./out/native/duxel_hello.exe).Length
& ./out/native/duxel_hello.exe
~~~

### Linux x64 · Bash

~~~bash
dotnet publish samples/fba/duxel_hello.cs -c Release -r linux-x64 \
  -p:PublishAot=true -p:DuxelCompact=true -p:OptimizationPreference=Size \
  -p:InvariantGlobalization=true -p:StackTraceSupport=false -p:EventSourceSupport=false \
  -p:UseSystemResourceKeys=true -p:IlcScanReflection=false \
  "-p:ArtifactsPath=$PWD/out/native-objects" -o out/native
wc -c out/native/duxel_hello
./out/native/duxel_hello "$(fc-match -f '%{file}' sans-serif)"
~~~

창에 **Hello, Duxel**이 표시됩니다. 실행 파일에 .NET 런타임이 들어갑니다. 대상 환경에는 시스템 라이브러리와 Vulkan 드라이버가 필요하며, 앱에서 쓰는 글꼴과 파일도 준비해야 합니다.

## 내 앱에 맞는 옵션 고르기

1. 사용하는 앱 호스트와 기능을 참조합니다. 먼저 `-p:PublishAot=true`로 게시한 뒤, `-p:OptimizationPreference=Size`를 추가해 같은 환경에서 파일 크기와 동작을 비교합니다.
2. 내장 리소스를 줄여도 되는 앱에는 `DuxelCompact`를 켭니다. Fluent 아이콘을 쓰려면 `-p:DuxelEmbedFluentIconFonts=true`로 글꼴을 내장하거나 필요한 글꼴 파일을 실행 파일 옆에 둡니다. 저장이나 열기 기능이 있다면 대상 환경에서 네이티브 파일 대화 상자가 뜨는지 확인합니다.
3. `InvariantGlobalization`은 지원하는 언어와 지역의 변환 형식, 정렬, 비교를 점검한 뒤 켭니다. `StackTraceSupport=false`, `EventSourceSupport=false`, `UseSystemResourceKeys=true`를 켜면 진단 정보와 자세한 오류 메시지가 줄어듭니다. 배포 후 장애 분석에 필요한 설정을 유지합니다.
4. `IlcScanReflection=false`는 이 .NET 11 측정에 사용한 NativeAOT 컴파일러 옵션입니다. 리플렉션 메타데이터를 쓰는 앱은 해당 기능을 모두 네이티브 빌드에서 검증한 경우에만 이 옵션을 켭니다.
5. 외부 글꼴과 리소스까지 포함해 실제로 배포할 파일의 용량을 측정합니다. 장애 분석에 쓸 네이티브 심볼은 별도로 보관합니다. 빌드 설정을 비교할 때는 설정마다 `ArtifactsPath`를 따로 지정합니다.

[Microsoft NativeAOT 최적화 가이드](https://learn.microsoft.com/ko-kr/dotnet/core/deploying/native-aot/optimizing)
