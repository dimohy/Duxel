# Fluent 2 Carousel

`Fluent.Carousel(label, selected, slides, options)`은 `UiBinding<string>`의 키로 현재 슬라이드를 찾습니다. 각 `UiCarouselSlide`에 고유한 키와 보조 기술에서 읽을 이름, 콘텐츠 위젯을 지정합니다. Carousel이 콘텐츠 위젯을 소유합니다. 다른 슬라이드로 이동해도 입력 상태가 유지되고, 현재 슬라이드만 화면에 배치되어 입력을 받습니다.

이전·다음 버튼으로 슬라이드를 넘깁니다. `Wrap`을 켜면 처음과 마지막 슬라이드가 이어집니다. 단계 선택은 키보드와 보조 기술로도 조작할 수 있습니다. 모든 슬라이드에 `UiImageSource`를 지정하면 같은 이름을 읽어 주는 이미지 미리보기를 표시합니다. 미리보기가 한 줄에 안 들어가면 가로로 스크롤할 수 있습니다. `RightToLeft`로 탐색 방향을 바꾸고 `ReducedMotion`으로 단계 전환 효과를 끌 수 있습니다.

`AutoPlayInterval`에 1초 이상의 간격을 지정하면 자동 재생 스위치가 생깁니다. 기본값은 꺼짐이며 `IsPlaying`으로도 켜고 끌 수 있습니다. 재생 중에 마우스를 올리거나 키보드 포커스를 Carousel 안으로 옮기면 잠시 멈춥니다. `Wrap`이 꺼져 있으면 마지막 슬라이드에서 멈춥니다. 스위치를 끄면 다음 재생을 예약하지 않습니다.

Windows에서는 `./run-gallery.ps1`을 실행하세요. Windows에서 WSL 갤러리를 열려면 `./run-gallery-linux.ps1`을 실행하세요. **콘텐츠와 목록 → Carousel**의 각 예제 바로 아래에 C# 소스가 있습니다. 슬라이드를 바꾸거나 안쪽 버튼을 눌러 소스에 값이 반영되는지 확인할 수 있습니다. [Fluent 2 Carousel 가이드](https://fluent2.microsoft.design/components/web/react/core/carousel/usage)

Carousel 집중 검사 17건과 관련 회귀 검사를 합쳐 총 1,019건이 통과했습니다. Vulkan으로 갤러리와 이미지 미리보기를 캡처했고, Windows 갤러리에서 Carousel을 포함한 100개 화면 표시를 확인했습니다. 두 예제 소스는 Windows용과 플랫폼 공통 .NET 11 빌드에서 각각 컴파일했습니다.

WSLg에서는 Linux x64 .NET 11.0.0 NativeAOT 갤러리의 Carousel 화면이 Vulkan에 제출됐습니다. 외부 AT-SPI 클라이언트로 관리형과 NativeAOT에서 각각 9건을 확인했습니다. 첫 예제에서는 양 끝의 버튼 상태, 이전·다음 이동, 탭으로 장 선택, 이전 장의 콘텐츠 제거와 선택 횟수 유지를 확인했습니다. 두 번째 예제에서는 미리보기 이름으로 장을 고르고, 첫 장에서 마지막 장으로 돌아가고, 자동 재생으로 다음 장을 표시하는 동작을 확인했습니다. 일시 정지 후 4.4초 동안 같은 장을 유지했습니다. 화면 읽기 프로그램의 음성 확인은 남아 있습니다.

같은 접근성 버스에서 Linux 갤러리를 실행한 뒤 `dotnet <Duxel.Linux.App.Tests.dll> atspi-gallery-carousel <갤러리-PID> <새-result.json>`을 실행하면 다시 확인할 수 있습니다.
