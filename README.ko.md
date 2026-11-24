<img src="samples/Duxel.Gallery/Assets/Brand/duxel-mark.svg" width="72" height="72" alt="Duxel 로고">

# Duxel · Windows와 Linux용 C# 크로스플랫폼 데스크톱 UI

**C# / .NET**으로 빠른 **Windows·Linux 데스크톱 앱**을 만들어 보세요. Duxel은 **Fluent 2** 컨트롤과 **Vulkan** 렌더러를 제공합니다. **AI 바이브 코딩**을 할 때 Gallery에서 필요한 컨트롤을 눌러 보고, 그 C# 코드를 AI에게 보여준 다음 파일 기반 앱(FBA)으로 바로 실행할 수 있습니다. 위젯과 상태 코드는 두 플랫폼에서 공유합니다.

[English](README.md) · [문서](docs/README.ko.md) · [예제](docs/fba-run-samples.ko.md) · [버전 이력](docs/version-history.ko.md)

## Duxel Gallery 0.4.0 실행하기

[.NET 11 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/11.0)와 Vulkan을 사용할 수 있는 그래픽 환경이 필요합니다. Gallery에서 컨트롤, 레이아웃, 이미지, SVG, Markdown을 직접 써 보고 C# 코드를 복사할 수 있습니다.

### Windows에서 시작하기

PowerShell 7에서 Gallery를 파일 기반 앱으로 실행합니다.

~~~powershell
curl.exe -fsSLO https://raw.githubusercontent.com/dimohy/Duxel/v0.4.0/samples/fba/duxel_gallery.cs && dotnet run --file duxel_gallery.cs
~~~

### Linux에서 시작하기

Linux의 Bash에서 실행합니다. X11/Xwayland와 Vulkan이 준비된 WSLg에서도 실행할 수 있습니다. [Linux 설치 안내](docs/linux-platform.ko.md)

~~~bash
curl -fsSLO https://raw.githubusercontent.com/dimohy/Duxel/v0.4.0/samples/fba/duxel_gallery_linux.cs && dotnet run --file duxel_gallery_linux.cs
~~~

![Duxel Gallery에서 SizedBox 레이아웃 예제와 C# 코드를 함께 보는 화면](docs/images/duxel-0.4.0-gallery.png)

## Fluent 2 앱 예제

계산기, 메모장, 대용량 목록, 움직이는 도형은 각각 C# 파일 하나로 실행합니다. Windows와 Linux에서 같은 소스를 씁니다. [예제별 한 줄 실행 명령](docs/fba-run-samples.ko.md)

| 예제 | 이렇게 써 보세요 |
| --- | --- |
| [계산기](samples/fba/duxel_calculator.cs) | 소수 연산을 이어서 하고 0으로 나누었을 때의 표시도 확인합니다. |
| [메모장](samples/fba/duxel_notepad.cs) | 로컬 메모를 편집하고 텍스트 파일로 저장합니다. |
| [대용량 목록](samples/fba/duxel_large_list.cs) | 10만 행을 스크롤하거나 마지막 행으로 바로 이동합니다. 화면에 가까운 행만 위젯으로 만듭니다. |
| [움직이는 도형](samples/fba/duxel_render_motion.cs) | 원의 수를 바꾸면서 같은 창에서 Fluent 2 컨트롤을 조작해 봅니다. |

![10만 항목 중 행 위젯 12개를 생성한 재고 목록 예제 화면](docs/images/duxel-0.4.0-inventory.png)

## 앱 만들기

- `Dux`로 배치하고 `Fluent`로 컨트롤을 만듭니다. `UiState<T>`로 상태를 연결하고, 같은 창의 일부에 즉시 모드로 직접 그릴 수 있습니다.
- Windows 또는 Linux 호스트를 프로젝트에서 참조하거나 C# 파일 상단에서 `#:package`로 지정합니다. 호스트와 공용 런타임은 .NET 8·9·10용 패키지 자산을 제공합니다. 0.4.0 Gallery는 .NET 11 SDK로 실행합니다.
- 렌더링 동작은 Gallery, 대용량 목록, 도형 예제에서 확인할 수 있습니다. 이전 버전에서 어떤 조건으로 측정했는지는 [성능 기록](docs/archive/README.md)에 남겼습니다.

[C# 앱 시작하기](docs/getting-started-fba.ko.md) · [API와 소스 위치](docs/duxel-agent-reference.ko.md) · [저장소 지침](AGENTS.md) · [AI 문서 색인](llms.txt)
