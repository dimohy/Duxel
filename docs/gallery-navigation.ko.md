# Duxel Gallery 사용하기

[Windows 또는 Linux에서 Gallery를 실행](../README.ko.md#duxel-gallery-040-실행하기)합니다. Gallery에서 실제 Duxel 위젯을 조작하고 C# 코드를 볼 수 있습니다.

1. 탐색 메뉴에서 컴포넌트를 고릅니다. 화면에서 상태나 옵션을 바꿔 봅니다.
2. CodeView에서 필요한 코드를 선택해 복사합니다. 오른쪽 버튼 메뉴에서 전체를 선택할 수도 있습니다.
3. [파일 기반 앱](getting-started-fba.ko.md)에 위젯 코드를 붙입니다. 창을 열고 실행하는 코드는 기존 앱의 플랫폼 호스트를 사용합니다.

| 관심 있는 기능 | 찾아볼 예제 |
| --- | --- |
| 입력과 컨트롤 | 입력 필드, 버튼, 스위치, 대화 상자와 MenuBar |
| 레이아웃 | Align, SizedBox, UniformGrid, 스크롤과 가상 목록 |
| 그래픽과 문서 | 이미지 파일 형식과 맞춤, SVG, Markdown, 렌더링 진단 |

설정에서 영어, 한국어, 일본어, 중국어 간체를 고를 수 있습니다.

## .NET 런타임 선택하기

이 저장소의 Gallery는 .NET 8·9·10·11에서 실행할 수 있습니다. 빌드에는 `global.json`에 지정된 SDK를 사용하고 실행할 버전의 런타임을 설치합니다. 아래의 `8`을 `9`, `10`, `11`로 바꿔 해당 버전을 선택할 수 있습니다.

~~~powershell
dotnet run --project samples/Duxel.Gallery -f net8.0-windows
~~~

~~~bash
dotnet run --project samples/Duxel.Gallery.Linux -f net8.0
~~~

## 상단 메뉴 모음 사용하기

저장소에서 실행한 Gallery에서 **Fluent 컨트롤 → 동작과 탐색 → MenuBar**를 엽니다. 파일 메뉴를 연 상태에서 편집으로 포인터를 옮겨 보세요. 키보드로는 좌우 방향키로 메뉴를 선택하고 아래 방향키로 엽니다. Escape를 누르면 메뉴가 닫히고 바로 앞 메뉴 항목에 초점이 돌아옵니다. 하위 메뉴, 구분선, 비활성 항목, 체크 항목도 확인할 수 있습니다. C# 코드를 복사하거나 [메모장 예제](fba-run-samples.ko.md#메모장)를 실행해 보세요.

[독립 실행 예제](fba-run-samples.ko.md) · [English](gallery-navigation.md)
