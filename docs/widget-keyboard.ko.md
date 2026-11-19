# 생성된 컨트롤의 키보드 입력과 포커스

Last synced: 2026-11-05

생성된 Fluent 버튼·토글 버튼·Checkbox·Switch와 사용자 상속 위젯이 공통 키보드 입력 경로를 사용한다. 동작 정책은 `IUiButtonBehavior`, 표현은 `IUiButtonPresentation`/`IUiTogglePresentation`으로 분리한다. 버튼은 Enter와 Space, Checkbox/Switch는 Space 전용 동작 타입을 생성 시 연결한다. 실행 중 테마 레지스트리를 탐색하지 않는다.

## 동작과 수명

Tab/ShiftTab은 직전 성공 프레임에 등록된 활성·가시 컨트롤 순서를 순회한다. 비활성·클립 밖 컨트롤은 등록하지 않으며, 가장 높은 등록 팝업 계층의 순서를 유지한다. Ctrl/Alt/Super+Tab은 일반 Tab으로 소비하지 않는다. Enter는 key-down에서, Space는 누름 상태를 유지하다 key-up에서 활성화한다. Space 반복 down만으로 콜백을 중복 실행하지 않는다. Enter 반복 이벤트는 기존 bool 반환 계약에 따라 프레임당 최대 한 번의 활성화로 합쳐진다.

마우스 누름은 해당 컨트롤로 포커스를 옮기고 키보드 포커스 표시를 감춘다. Tab은 표시를 다시 켠다. 제거·비활성·Escape·다른 컨트롤 클릭·`UiContext.AddFocusEvent(false)`는 예약된 Space 활성화를 취소한다. 텍스트 입력을 클릭하면 이전 버튼 포커스를 해제하며 Tab으로 버튼에 이동할 때 이전 편집 활성 상태를 해제한다. 기존 IsItemActive/Activated/DeactivatedAfterEdit 질의도 Space 상태를 반영한다.

`UiButtonInteraction`의 추가 init 속성 Focused·FocusVisible은 기존3개 위치 생성자와 분리를 유지한다. 사용자 표현은 이를 사용해 자유롭게 그릴 수 있다. `UiControlContext.FocusOutline`은 공통 윤곽 그리기 도우미다. 공식 Fluent의 모든 대비/상태/포커스 시각 일치를 인증한 것은 아니다.

탭 순서 두 버퍼는 매 성공 프레임 교체하고 이전 참조를 비운다. 현재 등록량의4배 또는64칸보다 큰 용량이120프레임 연속 유지되면 축소하며 Dispose에서 전부 해제한다. 최대 등록 수를 임의로 잘라 키보드 입력을 잃는 방식은 쓰지 않는다. 화면 크기에 비례한 참조·배열 상주 비용이 추가되므로 전체 메모리/성능 검증은 남아 있다.

## 공식 기준과 검증

2026-11-05 확인한 [Microsoft 버튼 규칙](https://github.com/microsoft/fluentui/blob/master/specs/Button.md)은 Tab 포커스와 Enter/Space 활성화를 명시한다. [Checkbox 규칙](https://github.com/microsoft/fluentui/blob/master/specs/Checkbox.md)은 Tab·Space와 키보드에서만 보이는 포커스 표시를 다룬다. [Windows 키보드 지침](https://learn.microsoft.com/en-us/windows/apps/design/input/keyboard-interactions)은 명령 버튼의 Enter와 선택 컨트롤의 Space를 구분한다. 이 문서들은 동작 기준이며 전체 시각·접근성 검증의 대체물이 아니다.

.NET10 전체871·집중159·생성기66검사와 net8/9/10 솔루션 경고0·오류0을 확인했다. 기존 이미지323개가 같다. 별도 `tests/Duxel.Control.Tests`는 같은 ControlKeyboardTests.cs를 연결하여 창·GPU 없이24개 입력/수명 검사를 실행한다. 테스트 본문을 별도로 복제하지 않는다. JSON 출력도 소스 생성기로 연결한다.

고정 .NET11 RC3 NativeAOT Speed(win-x64)의 실제 native code generation 후 같은24개 검사를 실행해 모두 통과했다. SDK11.0.100-rc.3.26522.148, 런타임11.0.0-rc.3.26522.148이며 실행 파일 SHA256은 E16D9913A5374321302FD8117D75F204675F1F0C22312A6E91897CDD860798F5다. 관리/Native 검사 이름과 순서를 대조한다. 이는 실제 공통 입력 경로의 NativeAOT 기능 검사이며 Windows 키보드 이벤트 전달·GPU 화면·UIA·성능 검증은 아니다.

초기 구현에서 포커스가 없는 null 상태도 해제로 판단해 매 프레임을 요청하는 결함을 찾았다. idle-before 검사는 수정 전에 실패했고, 실제 소유 포커스가 있을 때만 해제하도록 수정한 뒤 통과했다. 정적 화면의 이벤트 기반 실행을 보존하는 회귀 검사다.

```powershell
& 'C:/Users/dimohy/.cache/duxel-dotnet-10.0.301/dotnet.exe' run --project tests/Duxel.Control.Tests -c Release -- './artifacts/control-check.json'
& './artifacts/widget-keyboard-2026-11-05/native-controls-rc3/Duxel.Control.Tests.exe' './artifacts/control-native-check.json'
```

## 미완료 범위

모든 레거시 입력·슬라이더·복합 컨트롤이 Tab 순서에 통합된 것은 아니다. 새로 나타난 컨트롤은 성공 프레임 이후 순서에 포함되며, 바로 비활성화/제거된 이전 Tab 대상은 포커스를 해제한다. 같은 프레임의 임의 키 이벤트 순서·자동 스크롤·중첩 포커스 범위/복원·네이티브 focus 이벤트 전달과 전체 UIA는 추가 구현/검증이 필요하다. 기본 팔레트/전체 위젯/공식 상태·룩앤필 전환도 미완료다.

사용자 요청으로 연1,200개 ERD 창은 초기 키보드 코드로 게시한 별도 실행이다. 이후 idle 수정으로 그 실행 파일을 덮어쓰거나 실행 중인 창을 닫지 않았다. ERD 실행 성공을 키보드 검증이나 최종 성능 측정으로 확대하지 않는다. 기존 RC3 GPU 비교 실패·Private 상주 비용·Pi 실측 미검증도 유지한다.

근거: `artifacts/widget-keyboard-2026-11-05/verification.json`, controls-net10.json, controls-native-rc3.json, image-comparison.json, idle-before.stderr.log, source-hashes.json 및 erd-interactive-run.json. 전체 목표는 활성 상태다.
