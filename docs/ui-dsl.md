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

## 변환 파이프라인
1) `.ui` 파일 파싱 → AST(`UiDslDocument`, `UiDslNode`)
2) AST → C# 소스 생성(`UiDslCompiler`)
3) 빌드 시 생성 소스 포함(소스 생성기 or MSBuild Task)
4) 런타임에서 즉시 모드 방식으로 렌더 호출

## 런타임 동작(동등성 원칙)
- DSL 트리는 ImGui의 즉시 모드 호출을 **정해진 순서로 재현**한다.
- 렌더 프레임 루프에서 `Emit()`이 호출되며, 위젯 호출은 즉시 수행된다.

## 코드 생성 스케치
- `.ui` 하나당 `partial class` 생성
- 각 문서에 `Render(IUiDslEmitter emitter)` 생성
- `emitter.BeginNode(name, args)` → 자식 → `emitter.EndNode()`
- 위젯 동작은 `IUiDslEmitter` 구현체가 책임

## 컴파일/핫리로드 정책
- 빌드시 `.ui` → `.g.cs` 생성되어 실행파일에 포함.
- 핫리로드는 `.ui` 변경 후 재빌드로 반영.

## 상태
- DSL 파서/AST/컴파일러 기본 구현 완료.
- 소스 생성기/MSBuild 통합은 다음 단계에서 진행.
