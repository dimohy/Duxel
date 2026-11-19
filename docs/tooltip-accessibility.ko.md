# Tooltip 대상 접근성

Last synced: 2026-11-11

`UiTooltipOptions.Relationship`은 대상이 소유한 주 의미 컨트롤에 툴팁 텍스트를 연결한다. 기본값은 `Description`이다. `UiAccessibilityNode.Tooltip`은 `SourceId`·`Text`·`Relationship`의 불변 `UiAccessibilityTooltip` 메타데이터를 `IsOpen`과 별도로 게시한다. `SourceId`는 논리적인 툴팁 출처이며 표시된 표면 노드도 이 ID를 사용한다. 숨은 상태에는 해당 표면 공급자가 없다.

| 관계 | 대상 의미 | Windows 대응 |
| --- | --- | --- |
| `Description` | 대상 이름을 유지하고 보충 설명 연결 | 대상 `HelpText`에 추가하며 Field 힌트·검증 문구 보존 |
| `Label` | 자식의 명시적 `AccessibleName` 또는 Field 레이블이 없으면 툴팁을 이름으로 사용 | 대상 `Name`; 레이블 문구는 도움말에 추가하지 않음 |
| `Inaccessible` | 툴팁 관계를 게시하지 않음 | 대상의 원래 이름·도움말 유지 |

이는 설명 텍스트에 관한 [공식 Fluent 지침](https://fluent2.microsoft.design/components/web/react/core/tooltip/usage)과 일반 툴팁 정보를 참조 컨트롤의 HelpText로 제공하는 [Windows ToolTip 계약](https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-supporttooltipcontroltype)을 따른다. [UIA 속성 식별자](https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-automation-element-propids)는 HelpText를 BSTR로 정의하고 Name을 구분한다. 이 플랫폼 대응은 대상 속성을 사용한다. [표시된 ToolTip 노드와 열림/닫힘](tooltip-events.ko.md)은 추가로 검증했다. 전체 속성 변경·종료 알림과 플랫폼 준수는 남아 있다.

주 편집기가 앞·뒤 장식 렌더링 전에 관계를 확보하므로 장식 버튼은 독립적인 이름·도움말을 유지한다. Field와 Tooltip의 두 중첩 순서 모두 Field 레이블·힌트·검증·필수·오류 상태를 보존한다. 가장 안쪽 Tooltip이 의미 관계를 소유하고 같은 우선순위의 시각 요청에서도 우선한다. 정상 종료와 예외 시 이전 문맥을 복원한다. 독립 주 컨트롤이 여럿인 대상 조합에는 명시적 그룹·대상 계약이 더 필요하다.

숨김·클립·비활성 상태에서도 설명을 유지한다. 대상 누락은 노드와 이전 공급자 식별자를 무효화하며 재등장 시 공급자는 새로 만들고 논리 출처는 유지한다. 대상 렌더링 시 텍스트를 확보하여 지연 그리기와 게시 의미가 같은 프레임에서 일치한다. 이후 텍스트 대입은 다음 대상 프레임에 반영한다. 어떤 관계 모드도 자식의 `AccessibleName` 속성을 직접 바꾸지 않는다.

## 검증

근거는 `artifacts/tooltip-accessibility-2026-11-11`이다. 원래 숨은 HelpText 누락 1건과 이후 CPU·GPU의 프레임 텍스트 불일치를 수정 전에 재현했다. 최종 고정 .NET11 RC3 검사는 CPU502(신규 의미 검사29), Tooltip GPU288(중첩·프레임 스냅샷 추가), 기존 접근성40·수명101과 입력·컨테이너 회귀를 포함한다.

샘플 Footer 버튼은 `Description`을 명시한다. 별도 Windows UIA 클라이언트가 실행 중인 샘플을 찾아 실제 대상 HelpText·Name, 이웃 Inset 버튼의 의미 분리와 포커스 가능 여부를 확인하고 설명 텍스트가 독립 컨트롤로 만들어지지 않았음을 검사한다. 네이티브 속성에 대한 집중 검증이며 전체 화면 읽기 프로그램 동작과 툴팁 이벤트 전달은 남아 있다. 실행 명령은 다음과 같다.

```powershell
./run-fba.ps1 samples/fba/layout_custom_contract_fba.cs -NoCache
```

[범용 대상 계약](tooltip-targets.ko.md)을 확장한 작업이다. `Fluent.Tooltip`, 공식 배치·화살표·그림자, 제어된 표시, 포커스 출처·문서 가시성, 현재 NativeAOT·런타임 비교와 새 경로 성능은 미완료다.
