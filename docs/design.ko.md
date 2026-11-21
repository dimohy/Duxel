# Duxel 설계

[English](design.md) · [문서](README.ko.md)

Duxel은 플랫폼 호스트와 Vulkan 렌더러로 네이티브 데스크톱 애플리케이션을 실행합니다. 한 창에서 즉시 모드와 리테인드 모드를 각각 사용하거나 함께 쓸 수 있습니다.

## 애플리케이션 UI

**리테인드 모드**는 프레임 사이에 위젯 트리를 유지합니다. 레이아웃을 만들 때는 `Dux`, 테마가 적용되는 컨트롤에는 `Fluent`를 사용합니다. 위젯 트리의 루트는 화면이 살아 있는 동안 유지하고, `UiState<T>`, 바인딩 또는 위젯 속성으로 값을 갱신합니다. 위젯 트리 안에서 즉시 모드 콘텐츠를 표시하려면 `UiViewHost`에 `UiView`를 넣습니다. 즉시 모드 화면에서 위젯을 소유하고 렌더링할 수도 있습니다. [사용자 정의 위젯](custom-widgets.ko.md) · [레이아웃 컨테이너](widget-containers.ko.md)

**즉시 모드**는 렌더 콜백이 호출될 때 화면을 구성합니다. 입력을 처리하고 직접 그리려면 `UiScreen.Render(UiImmediateContext ui)`를 재정의합니다. 선언형 레이아웃은 실행 중에 `.ui` 파일을 읽거나 빌드할 때 C#으로 생성할 수 있습니다. [선언형 UI](ui-dsl.ko.md)

## 디자인과 테마

Fluent 2는 리테인드 위젯에 적용하는 디자인입니다. 테마를 바꿔도 위젯 트리의 수명과 상태 관리는 그대로 유지됩니다.

`UiCompiledDesign.Windows11`과 `UiCompiledDesign.Windows11Dark`는 Fluent 2 컨트롤에 밝은 테마와 어두운 테마를 제공합니다. 앱 옵션에서 디자인을 선택합니다. `UiTheme`로 색상을, `UiDesignTokens`로 컨트롤 크기와 간격을 바꿀 수 있습니다. 여러 앱에서 디자인을 재사용하려면 `IUiDesign`을 구현합니다. `Fluent.FluentProvider`를 사용하면 위젯 트리의 특정 영역에 테마·서체·지원되는 배치 방향을 적용할 수 있습니다. [Fluent 2 지원 범위](fluent2-coverage.ko.md) · [테마 공급자](fluent2-provider.ko.md)

## 플랫폼과 렌더링

플랫폼 호스트는 창, 입력, 클립보드, 네이티브 텍스트와 접근성을 연결합니다. Vulkan 백엔드는 UI가 만든 그리기 데이터를 화면에 표시합니다. 지원 환경과 렌더링 동작은 각 문서에서 확인할 수 있습니다. [Linux](linux-platform.ko.md) · [접근성](widget-accessibility.ko.md) · [렌더링 재사용](retained-rendering.ko.md)
