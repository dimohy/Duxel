# C# 파일 하나로 데스크톱 앱 만들기

Duxel은 Windows와 Linux에서 위젯과 앱 상태를 공유합니다. Fluent 2로 컨트롤을 만들고 `Dux.Column`이나 `Dux.Row`로 배치합니다. 여러 컨트롤이 읽고 바꾸는 값은 `UiState<T>`로 연결합니다.

1. [.NET 11 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/11.0)를 설치하고 Vulkan을 쓸 수 있는 그래픽 세션을 준비합니다. Linux에서는 [Linux 설치 안내](linux-platform.ko.md)를 따릅니다.
2. [Duxel Gallery를 실행](../README.ko.md#duxel-gallery-040-실행하기)하고 필요한 컨트롤을 눌러 봅니다. 예제 아래의 C# 코드를 복사할 수 있습니다.
3. [`duxel_calculator.cs`](../samples/fba/duxel_calculator.cs)를 엽니다. 파일을 빌드할 때 운영체제에 맞는 호스트 패키지를 고릅니다. 위젯 트리와 계산 로직은 공유합니다.
4. 저장소 루트에서 `dotnet run --file samples/fba/duxel_calculator.cs`로 실행합니다. 예제의 Host 폴더에는 두 플랫폼의 시작 코드가 있습니다. 위젯이나 레이아웃을 바꿔 보세요. AI와 작업한다면 Gallery의 해당 페이지와 [API와 소스 위치](duxel-agent-reference.ko.md)를 알려주면 실제 사용 코드부터 찾을 수 있습니다.

## NuGet 패키지 선택

| 패키지 | 쓰는 곳 |
| --- | --- |
| [Duxel.Windows.App](https://www.nuget.org/packages/Duxel.Windows.App/0.4.0) | Windows 데스크톱 앱을 만듭니다. |
| [Duxel.Linux.App](https://www.nuget.org/packages/Duxel.Linux.App/0.4.0) | Linux X11 데스크톱 앱을 만듭니다. 호스트에 설치된 글꼴 파일을 설정합니다. |
| [Duxel.App](https://www.nuget.org/packages/Duxel.App/0.4.0) | 공용 런타임입니다. 두 호스트 패키지에서 자동으로 참조합니다. |
| [Duxel.Gallery](https://www.nuget.org/packages/Duxel.Gallery/0.4.0) / [Duxel.Gallery.Linux](https://www.nuget.org/packages/Duxel.Gallery.Linux/0.4.0) | 운영체제에 맞는 Gallery를 실행해 예제를 확인합니다. |

[예제별 한 줄 실행 명령](fba-run-samples.ko.md) · [English](getting-started-fba.md)
