# UI DSL (ImGui 동작 동등 목표)

## 목표
- `.ui` 확장자의 DSL 파일로 위젯 트리를 정의한다.
- 런타임 동작은 ImGui의 즉시 모드 흐름과 동일하게 유지한다.
- 빌드 시 DSL을 C# 코드로 변환해 실행파일에 포함한다.
- 핫리로드 시 `.ui` 파일을 다시 빌드하여 즉시 반영한다.

## DSL 개요
- 들여쓰기 기반 구조(트리).
- 각 줄은 `NodeName`과 선택적 인자(`args`)로 구성.
- 공백/탭 혼용은 금지(옵션으로 허용 가능).
- 빈 줄 및 주석(`//` 또는 `#`) 허용.

예시:
```
Window "Main"
  Row
    Button "OK"
    Button "Cancel"
```

## 문법 규칙
- `NodeName`은 알파벳/숫자/`_` 조합을 권장.
- 인자는 공백으로 구분, 문자열 인자는 큰따옴표로 감싼다.
- 인자에는 이스케이프(`\"`, `\\`)를 허용한다.
- 컨테이너 위젯은 들여쓰기로 자식 블록을 표현하므로 `Begin` 접두사는 생략할 수 있다.
  - 예: `Group` → 내부적으로 `BeginGroup`으로 처리
  - 단, `Combo`/`ListBox`는 자식 블록이 있을 때만 `Begin*`으로 해석한다.

## 변환 파이프라인
1) `.ui` 파일 파싱 → AST(`UiDslDocument`, `UiDslNode`)
2) AST → C# 소스 생성(`UiDslCompiler`)
3) 빌드 시 생성 소스 포함(소스 생성기 or MSBuild Task)
4) 런타임에서 즉시 모드 방식으로 렌더 호출

> NativeAOT 배포 빌드에서는 런타임 파서를 사용하지 않는다.
> `.ui`를 AdditionalFiles로 포함하고 소스 생성기가 만든 `Render`를 호출한다.

## 런타임 동작(동등성 원칙)
- DSL 트리는 ImGui의 즉시 모드 호출을 **정해진 순서로 재현**한다.
- 렌더 프레임 루프에서 `Emit()`이 호출되며, 위젯 호출은 즉시 수행된다.

## 위젯 매핑 규칙 (UiImmediateContext 기준)
- DSL 노드 이름은 `UiImmediateContext`의 공개 위젯 API에 직접 매핑된다.
- 모든 `UiImmediateContext*.cs` 위젯 메서드를 DSL에서 사용할 수 있다.

### 인자 기본 규칙
- 값 타입은 문자열을 숫자/불리언으로 자동 파싱한다.
- `UiVector2`: `"x,y"` 또는 `x y` 형태로 전달한다.
- 색상(`UiColor`): `#RRGGBB`, `#RRGGBBAA`, `0xAARRGGBB`, 정수 값 모두 허용한다.
- 배열 값(`float[]`, `int[]`, `double[]`): `"1,2,3"` 형태로 전달한다.
- 열거형/플래그(`Ui*Flags`): 이름 문자열로 전달한다(예: `"NoArrowButton"`).
- `Id=`, `Text=`(또는 `Label=`/`Name=`) 형식의 **명명 인자**를 지원한다.
- 모든 인자는 `Key=Value` 형태로 표기할 수 있으며, **순서와 무관하게** 해석된다.
- 동일 위젯에서 위치 기반 인자와 명명 인자를 혼용할 수 있다.

### 상태 바인딩 규칙
- 상태가 필요한 위젯은 `id`와 `label`을 순서대로 전달한다.
  - 예: `Checkbox "vsync" "VSync" true`
- 동일 표현을 명명 인자로도 사용할 수 있다.
  - 예: `Checkbox Id="vsync" Text="VSync" true`
- `id`는 `UiDslState` 또는 `IUiDslValueSource`에 저장/조회되는 키로 사용된다.

### 예시
```
Window "DSL Demo"
  Row
    Button "play" "Play"
    Checkbox "vsync" "VSync" true
  SliderFloat "volume" "Volume" 0 1
  InputText "name" "Name" 64
  Combo "quality" "Quality" "Low|Medium|High"
```

`Begin` 생략 컨테이너 예시:
```
Window "DSL Demo"
  MenuBar
    Menu "File"
      MenuItem "file.new" "New"
      MenuItem "file.exit" "Exit"
  Group
    Text "Begin 생략 가능"
  Combo "quality" "Quality" "Medium"
    Selectable "quality.low" "Low"
    Selectable "quality.medium" "Medium"
    Selectable "quality.high" "High"
```

## 코드 생성 스케치
- `.ui` 하나당 `partial class` 생성
- 각 문서에 `Render(IUiDslEmitter emitter)` 생성
- `emitter.BeginNode(name, args)` → 자식 → `emitter.EndNode()`
- `Begin` 접두사를 생략한 경우 파서가 자동으로 정규화한다.
- 위젯 동작은 `IUiDslEmitter` 구현체가 책임

### 생성 결과 사용
- 소스 생성기는 `Duxel.Generated.Ui.UiDslGeneratedRegistry`를 생성한다.
- `GetRenderer("MainUi")`로 렌더 델리게이트를 얻어 `DuxelDslOptions.Render`에 전달한다.

## 컴파일/핫리로드 정책
- 빌드시 `.ui` → `.g.cs` 생성되어 실행파일에 포함.
- 핫리로드는 `.ui` 변경 후 재빌드로 반영.

## 상태
- DSL 파서/AST/컴파일러 기본 구현 완료.
- 소스 생성기/MSBuild 통합은 다음 단계에서 진행.

