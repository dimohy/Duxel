# Duxel API와 소스 위치

AI로 앱을 만들 때는 대상 운영체제를 먼저 정하고, Gallery에서 동작하는 예제를 골라 API를 확인하세요. 0.4.0 FBA에는 NuGet 참조와 호스트를 포함한 전체 C# 파일이 있습니다. 새 API 호출을 제안하기 전에 현재 소스의 시그니처를 확인합니다.

| 찾는 것 | 먼저 살펴볼 곳 |
| --- | --- |
| 실행할 예제와 패키지 선택 | [FBA 시작하기](getting-started-fba.ko.md), [samples/fba](../samples/fba) |
| 컨트롤과 레이아웃의 실제 사용 | [GalleryComponentExamples.cs](../samples/Duxel.Gallery/GalleryComponentExamples.cs), [Gallery navigation](../samples/Duxel.Gallery/GalleryNavigation.cs) |
| Fluent 글꼴 스타일 | [Fluent.Text.cs](../src/Duxel.Core/Fluent.Text.cs), 예 `Fluent.Text(Text.Title2, "Title")` |
| 상태, 위젯, 레이아웃 | [Duxel.Core](../src/Duxel.Core) |
| 앱 옵션과 프레임 루프 | [Duxel.App](../src/Duxel.App) |
| 운영체제별 창과 입력 | [Windows host](../src/Duxel.Windows.App), [Linux host](../src/Duxel.Linux.App) |
| Vulkan, SVG | [Duxel.Vulkan](../src/Duxel.Vulkan), [Duxel.Svg](../src/Duxel.Svg) |

`Dux`와 `Fluent`로 만든 위젯 트리를 공유하고 운영체제별 옵션은 호스트에서 설정합니다. 수정한 예제는 지원하려는 플랫폼에서 각각 실행해 확인하세요.

Windows의 앱 색 모드를 따르려면 `Design`과 `Theme`을 기본값으로 두세요. Linux 0.4.0의 [대표 FBA](fba-run-samples.ko.md)는 시작할 때 데스크톱 설정 포털을 읽어 Fluent 디자인을 선택합니다.

[문서 색인](README.ko.md) · [English](duxel-agent-reference.md)
