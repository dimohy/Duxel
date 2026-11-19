# Textarea 구현 상태

[공통 텍스트 드래그 소유권](text-drag-ownership.ko.md): 한 줄·다중 행이 실제 누름 소유권과 시각을 공유하고 포커스만 있는 held 입력은 선택하지 않습니다. CPU444·다중행2560 통과, 컨텍스트/스크롤바 수명과 포인터 GPU 포함. 실제 OS 캡처·단어 드래그·부분 가림·Native 전체 일치는 미완료입니다.

[다중 행 원문 요소](multiline-elements.ko.md): 기본 편집의 결합·보충 문자 분할과 모든 모드의 동일 프레임/대기 IME 정렬을 수정했습니다. CPU393·다중행2464 통과, 이전 입력693장·다중행3457장 일치. [Native 상태](native-toolchain-status.ko.md)는 이전 P: 도구체인을 현재 사용할 수 없고 현 위치가 미확인임을 구분합니다.

[공통 키보드 명령](text-commands.ko.md): Ctrl+Backspace/Delete와 반복은 온전한 단어 범위를 삭제하고 다중 행 Left/Right는 선택을 접습니다. CPU301·입력커널304·다중행2336 통과이며 전체 네이티브/Unicode/OS 일치는 미완료입니다.

[비감싸기 원문 기하 통합](multiline-hard-lines.ko.md)으로 읽기 전용 CRLF/CR 범위와 읽기 전용/Fluent의 텍스트 요소 탐색·양축 바 공간·끝 커서를 연결했습니다. CPU204/GPU2208 및 이전 이미지3,025장 일치. 샘플에 Wrap 전환과 원문 구분자 보존을 넣었습니다.

공통 [텍스트 편집 무결성 규칙](text-edit-integrity.ko.md)이 길이 제한에서 들어오는 텍스트 요소가 잘리는 문제와 외부 모델 교체 뒤 남는 이력을 수정했습니다. 접근성 Value는 undo를 유지합니다. 관련 경로는 CPU175 통과이며 전체 Unicode/bidi·실제 OS 입력 검증은 미완료입니다.

`Fluent.Textarea(...)`가 생성된 `UiFluentTextarea` 위젯을 만듭니다. [다중 행 감싸기 기반](multiline-wrap.ko.md), 공통 Tab/Shift+Tab 탐색, Field 라벨 활성화, 접근성 Focus/Value를 연결했습니다. 전체 Fluent 외형·실제 OS 입력·현재 Native·통합 성능 검증은 미완료입니다.

```csharp
var notes = Fluent.Textarea("Notes", text.Binding, 4096, width : 400, height : 140);
notes.Options = new()
{
    Placeholder = "Write a note...",
    Flags = UiInputTextFlags.AutoSelectAll,
    Invalid = false
};
```

`UiMultilineInputOptions`는 제네릭 위젯과 새 제네릭 즉시 모드 오버로드에 적용합니다. 기존 시그니처는 유지합니다. `Flags`는 ReadOnly/AutoSelectAll을 지원하고 `Wrap`은 nullable이므로 명시하지 않으면 표현의 기본값을 유지합니다. 알 수 없는 플래그는 거부합니다. Field 오류 상태는 위젯 옵션을 바꾸지 않고 합치며 독립 위젯의 Invalid도 접근성에 게시합니다.

`UiFluentTextareaPresentation<TSize, TAppearance>`는 기존 입력 표현 타입으로 outline·filled darker·filled lighter를 지원하고 underline은 명시적으로 거부합니다. 크기별 루트 기하는 다음과 같습니다. 기본 높이는 textarea 최소 높이·바깥 테두리 2px·포커스 표시용 2px를 합친 소스 기반 선택이며 브라우저 스크린샷과의 일치 주장은 아닙니다. 줄 상자는 [공식 타이포그래피](https://fluent2.microsoft.design/typography)와 [타이포그래피 토큰 스타일](https://github.com/microsoft/fluentui/blob/master/packages/tokens/src/global/typographyStyles.ts)을 참고합니다.

| 크기 타입 | 기본 루트 높이 | 가로 / 세로 패딩 | 글자 / 줄 높이 |
| --- | ---: | ---: | ---: |
| `UiFluentSmallTextareaSize` | 44 | 8 / 4 | 12 / 16 |
| `UiFluentMediumTextareaSize` | 56 | 12 / 6 | 14 / 20 |
| `UiFluentLargeTextareaSize` | 68 | 14 / 8 | 16 / 22 |

읽기 전용은 CR/LF를 포함한 원래 모델을 보존하고 선택·복사·이동·스크롤을 허용합니다. 잘라내기·붙여넣기·undo/redo·삭제·Enter·문자 이벤트·IME 확정으로는 값을 바꾸지 않습니다. 읽기 전용 IME 확정은 소비해 버리고 조합 소유권을 해제합니다. 원문 인덱스 모드에서 CRLF는 텍스트 요소 하나인 hard break입니다. placeholder는 별도 소유 표시 행이며 콘텐츠에 클립하고 값·undo 이력·스크롤 범위에 넣지 않습니다. 읽기 전용/Fluent 비감싸기는 소유 명시적 행을 사용하고 기본 비감싸기 편집 동작은 기존 경로를 유지합니다. [비감싸기 원문 기하](multiline-hard-lines.ko.md)를 참고합니다.

Fluent Tab은 공백을 넣지 않고 탐색하며 기본 다중 행 동작의 Tab 공백 삽입은 유지합니다. Enter는 줄바꿈을 넣고 Escape는 Fluent 포커스를 유지합니다. 키보드 탐색 중에도 스크롤바 드래그 캡처 소유권을 유지합니다. `samples/fba/fluent_textarea_fba.cs`는 생성 팩터리·세 크기/표현·읽기 전용·placeholder·Field 검증·실시간 할당 변경을 보여 줍니다. 근거는 `artifacts/fluent-textarea-2026-11-11`에 있으며 카탈로그 이름은 노출을 뜻하고 전체 일치 완료를 뜻하지 않습니다.

[공식 사용 지침](https://fluent2.microsoft.design/components/web/react/core/textarea/usage)은 긴 자유 형식 텍스트와 기본 고정 크기·넘치는 내용의 스크롤을 설명합니다. [React 타입 계약](https://github.com/microsoft/fluentui/blob/master/packages/react-components/react-textarea/library/src/components/Textarea/Textarea.types.ts)은 세 가지 크기, outline/filled 표현과 선택적 크기 조절 방향을 정의합니다. 내려받은 참조 원본과 지문은 `artifacts/textarea-kernel-2026-11-11`에 있으며 전체 일치 검증을 의미하지 않습니다.

## 현재 기반

기존 `InputTextMultiline` 공개 시그니처를 유지하면서 할당 기하·편집 결과·프레임/텍스트 그리기·스크롤바 마무리를 분리했습니다. 유한 필드 기하와 양의 줄 높이는 편집 저장소 생성 전에 검증하며, 공개 높이 오버로드도 비유한 값을 커서 이동 전에 거부합니다.

`UiMultilineInput<TBehavior, TPresentation>`은 `IUiMultilineInputBehavior`와 `IUiMultilineInputPresentation`을 조합합니다. 표현의 측정/배치는 바인딩을 읽지 않고 메타데이터를 사용합니다. 렌더는 바인딩을 한 번 읽고 같은 편집기를 처리하며 부모가 준 루트 하나 안에 그립니다. `UiMultilineInputLayout`은 명시적 콘텐츠 사각형과 줄 상자를, `UiMultilineEditResult`는 편집 스냅샷을 전달합니다. `UiMultilineInputOwner`는 컨텍스트 이동·해제 시 편집 이력·양 축 스크롤바·드래그 시간을 소유합니다. 콘텐츠 축소는 오프셋을 보정하고 사라진 캡처 스크롤바를 해제하며 편집은 같은 프레임에 overflow 기하를 갱신합니다.

공통 커널은 움직이는 휠 축만 소비합니다. 확대 CPU 37검사는 순수 측정·20px 타이포그래피 줄 상자·할당 변경 중 편집/undo·컨텍스트 이동·해제·비활성 입력·대기 IME 확정 처리·작은 루트를 포함합니다. GPU 192검사도 통과했습니다. 기존 재사용 32개와 래스터/벡터·Light/Dark·MSAA 1/4·획득 이미지 재사용에서 독립 프레임/텍스트와 비교한 사용자 표현 160개입니다. 기존 캡처 순서를 유지하면 이전 이미지 파일 41개가 그대로 일치합니다. MSAA 그룹 사이에 추가 프레임을 넣었을 때 스크롤바 최근 상호작용 투명도가 달라진 5장의 차이는 원래 경과 프레임 순서를 유지하여 비교 환경을 바로잡았습니다.

`layout_multiline_contract_fba.cs`는 생성 사용자 스타일로 공개 API를 소비하며 실제 편집 모델·너비 변경·긴/빈 콘텐츠를 사용합니다. 팔레트는 확장 계약 예시이며 Fluent Textarea 외형이 아닙니다. 새 타입은 기반 기능으로 분류하고 Fluent 팩터리/카탈로그 이름은 늘리지 않습니다. 기본 동작은 포인터로 소유하는 다중 행 편집과 접근성 값 설정을 지원합니다. 접근성 포커스는 아직 지원하지 않습니다.

접근성 확장은 CPU93(새 검사 27개), 기존 접근성은 40검사, 다중 행 GPU320이 통과했습니다. 근거: `artifacts/multiline-accessibility-2026-11-11/verification.json`.

`UiAccessibilityNode.Multiline`은 선택 속성이며 기본값은 기존 한 줄 검증을 유지합니다. 컴파일 다중 행 노드는 이 속성을 설정하고 `UiStandardMultilineInputBehavior.AccessibilityValue`는 구현된 동작을 노출합니다. 값 요청은 CR/LF를 허용하고 기존 편집기의 CRLF/CR→LF 정규화·바인딩 한 번 쓰기·undo/redo·Field 메타데이터를 사용합니다. 탭·NUL 등 다른 제어문자는 계속 거부합니다. 요청 등록과 소비에서 가용성·최대 길이를 검증하므로 대기 중 비활성화·클리핑·최대 길이 축소를 우회하지 못합니다. 요청은 도착 순서를 유지하고 거부된 요청은 재실행하지 않습니다. 게시된 최대 길이는 정규화 전 UTF-16 요청 문자열에 적용됩니다. 관리 트리와 실제 편집기의 연결 검증이며 외부 Windows UIA 클라이언트나 전체 Textarea 접근성 검증을 의미하지 않습니다.

## 남은 작업

구현된 컴포넌트에는 [시각 행 감싸기 기반](multiline-wrap.ko.md)의 전체 일치, 외부 Windows UIA·실제 IME, 복합 스크립트·bidi/합자와 검증한 입력 요소·외부 모델 이력·명시적 행 계약을 넘어서는 전체 Unicode/브라우저 편집 동작이 남아 있습니다. 공통 팔레트/GPU 비교는 내부 조합 검증이며 공식 크기·표현·상태의 완전한 외형 일치 근거는 아닙니다. 사용자 보류 중인 리사이즈는 계속 보류합니다. 현재 Native·런타임 비교·전앱 성능과 행/placeholder 보유 메모리·재계산 비용도 미완료입니다.

별도 수명 회귀에서 관리 할당 검사의 불안정성이 드러났습니다. 기본 JIT의 변경 후 빌드는 제한된 3회 반복 중 2회, 변경하지 않은 버튼 동일 값 대입의 무할당 검사에 실패했고 기준 빌드는 3회 통과했습니다. tiered compilation을 끄거나 tiered PGO를 끈 비교에서는 양쪽 모두 통과했습니다. [후속 측정 교정](allocation-validation.ko.md)은 같은 독립 루프를 예열하여 기본 JIT 독립 실행 3회에서 각각 수명 101검사를 통과했습니다. 0바이트 판정과 제품 코드는 유지했습니다. 이전 실패 로그는 보존하며 정확한 콜드 런타임 할당 원인은 아직 미확정입니다.

로컬 샘플 검증은 패키지 지시문을 현재 저장소 프로젝트 참조로 교체하고 생성기 분석기 메타데이터를 명시합니다. 이 소스 버전이 필요하며 이전 배포 패키지에서의 사용 가능성을 입증하지 않습니다. 최종 관리 앱은 15초 동안 레이아웃/콘텐츠를 프로그램으로 30회 변경하고 정상 종료했습니다. 수동 OS 입력·성능 검증을 뜻하지 않습니다.

## 조상 측정 재사용 — 2026-11-12

`IUiMultilineInputPresentation.CanRetainMeasurement`와 `IUiTextareaSize.CanRetainMeasurement`의 기본값은 false입니다. 내장 Textarea 크기와 지원되는 결정적 Input 외관 특성이 참여합니다. 측정은 크기·옵션·텍스트 환경에 의존하며 편집 바인딩을 읽지 않습니다. 기존 부모 캐시가 이 기하를 유지해도 실제 렌더링에서는 값 검증·편집·줄바꿈·선택·스크롤·캐럿·IME·되돌리기를 계속 처리합니다. 크기·옵션·환경 변경은 기존 소유권 체인을 무효화합니다. 사용자 정의 크기·외관·표현은 안정적인 측정을 명시하지 않으면 동적으로 유지합니다. 새 편집기 캐시나 필드는 추가하지 않습니다. 근거: artifacts/delegated-layout-retention-2026-11-12/verification.json.
