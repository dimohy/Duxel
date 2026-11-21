# 코딩 AI로 Duxel 앱 만들기

[English](duxel-agent-reference.md) · [Duxel 문서](README.ko.md)

AI에 대상 플랫폼, 만들 화면, 사용할 Duxel 버전을 알려 주세요. 앱에서 NuGet을 사용한다면 설치한 패키지 버전을 기준으로 삼습니다. 현재 브랜치의 API가 필요하면 이 저장소와 로컬 소스 실행 스크립트를 사용합니다.

## 작업에 맞는 예제

| 만들려는 것 | 참고 자료 |
| --- | --- |
| 리테인드 모드로 폼·도구·대화 상자 만들기 | [선언형 C# 대시보드](../samples/fba/declarative_dashboard_fba.cs), [갤러리](gallery-navigation.ko.md) |
| 즉시 모드로 직접 그리기와 입력 처리 | [즉시 모드 예제](../samples/fba/all_features.cs), [설계](design.ko.md) |
| 레이아웃, 테마, 사용자 정의 위젯 | [컨테이너](widget-containers.ko.md), [테마 공급자](fluent2-provider.ko.md), [사용자 정의 위젯](custom-widgets.ko.md) |
| 선언형 UI 불러오기 또는 생성 | [`.ui` 문법과 소스 생성](ui-dsl.ko.md) |

## 처음 사용할 프롬프트

> 이 Duxel 저장소를 기준으로 Windows 데스크톱 화면을 C# 파일 하나로 만들어 줘. 레이아웃은 `Dux`, 컨트롤은 `Fluent`를 사용하고, 사용자가 편집하는 값은 `UiState<T>`와 바인딩으로 연결해 줘. 리테인드 모드로 구성하고 프레임 사이에 위젯 트리를 유지해 줘. 기본 밝은 테마를 적용하고 어두운 테마로 바꾸는 방법도 보여 줘. 화면 일부를 직접 그려야 하면 즉시 모드 뷰를 사용해 줘. 코드를 쓰기 전에 공개 API나 갤러리 예제를 확인하고, 실제로 빌드하고 실행해서 주요 클릭이나 키보드 동작을 확인해 줘. 실행한 명령도 알려 줘.

## 결과 확인

- 가장 가까운 갤러리 예제부터 확인하세요. 예제의 C#에는 컨트롤과 함께 필요한 상태나 옵션이 들어 있습니다.
- GPU에 보관하는 기하의 메모리를 분석할 때는 그리기 목록의 데이터 크기와 Vulkan이 할당한 페이지 크기를 각각 확인하세요. [보존 렌더링](retained-rendering.ko.md)
- 실제 화면에서 키보드 포커스, 편집, 콘텐츠가 영역을 벗어날 때의 동작을 확인하세요. [키보드 동작](widget-keyboard.ko.md) · [접근성](widget-accessibility.ko.md)
- `./run-fba.ps1 samples/fba/declarative_dashboard_fba.cs -Managed -NoCache`로 로컬 소스 예제를 실행할 수 있습니다. 공개 패키지로 만들 때는 [파일 기반 앱 시작](getting-started-fba.ko.md)을 따르세요.
