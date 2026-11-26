# Linux용 Duxel Gallery

[English](README.md) · [Linux와 WSLg 설치 안내](../../docs/linux-platform.ko.md)

X11 또는 Xwayland 데스크톱에서 Fluent 2 위젯 예제와 C# 코드를 확인합니다. 상단 메뉴를 비롯한 예제는 [Gallery 사용법](../../docs/gallery-navigation.ko.md)에서 찾아볼 수 있습니다.

저장소 루트에서 실행합니다.

~~~bash
dotnet run --project samples/Duxel.Gallery.Linux -f net11.0
~~~

저장소에 지정된 SDK와 Fontconfig, 데스크톱 글꼴, Vulkan 드라이버를 준비합니다. 해당 런타임을 설치하면 `net8.0`, `net9.0`, `net10.0`도 선택할 수 있습니다. 공개 패키지 실행 방법은 [Linux 설치 안내](../../docs/linux-platform.ko.md)에 있습니다.
