# 컴파일된 체크박스·스위치와 입력 정체성

2026-11-11 [공통 Toggle 접근성](widget-accessibility.ko.md): Checkbox/Switch/ToggleButton 역할과 꺼짐/켜짐/혼합 상태를 단일 ToggleState·기존 동작/바인딩 정책·Windows Toggle 속성 알림에 연결했습니다. Checked는 두 상태 표현으로 유지합니다. Core21/관찰134/생성기115개·외부 Toggle11개/호스트4개·체크 메뉴6개/호스트3개가 통과했고 GPU47개·본문73개 일치로 그리기를 보존했습니다. 앞서 그린 공유 형제는 요청된 다음 프레임에 반영합니다. 전체 발화/강제 색상/NativeAOT는 남아 있습니다. 근거: artifacts/toggle-accessibility-2026-11-11/verification.json.

Last synced: 2026-11-11

## 선택 유지 버튼 후속

`Fluent.ToggleButton(label,binding)`과 `Dux.ToggleButton(label,binding)`은 생성된 `UiFluentToggleButton`을 사용한다. 이 타입은 체크박스·스위치와 같은 `UiToggle<bool,...>` 값 전이·입력 커널을 상속하며 버튼 형태의 별도 표현을 선택한다. 즉시 `ui.ToggleButton(label,ref value)`도 동일한 구현을 사용한다. 누르는 동안의 상태와 유지되는 선택 상태는 구분한다. 사용자 상속의 OnChanged, 비활성 입력 차단, 라벨 변경 중 선택과 콜백, 선택별 기하의 색 차이를 .NET10 집중 검사로 확인했다.

현재 기본 표현은 기존 색 팔레트와 medium 버튼 측정을 사용한다. 공식 secondary checked 스타일을 검토했지만 전체 색 토큰·아이콘·크기·appearance·키보드·접근성·포커스·모션·화면 일치는 아직 미완료다. 새 카탈로그 이름은 완전한 Fluent 적합성 인증이 아니다. 공식 참조는 동일 고정 커밋의 `react-button/library/src/components/ToggleButton/useToggleButtonStyles.styles.ts`이며 원문을 `artifacts/fluent-toggle-button-2026-11-05/`에 보존한다.

사용자가 확인한 성능 목표는 버튼 1만 개에서도 효과적으로 동작하는 최종 공통 구조다. 생성기의 정적 연결만으로 달성했다고 보고하지 않는다. 유지·스크롤·일부/전체 변경의 CPU 시간, 할당, Private, 업로드·draw와 입력 정확성 및 코드 크기를 검증해야 하며 아직 실행 전이다.

Fluent 2 전환은 진행 중이다. 버튼·체크박스·스위치의 타입 연결과 입력 경로를 구현했으며 전체 컴포넌트·공식 룩앤필·키보드·접근성·모션이 완료됐다는 뜻이 아니다. 사용자가 실행 화면에서 스타일 변화가 보이지 않는다고 관측했으며, 기존 창·폰트·팔레트·여러 표현이 남아 있다는 사실을 현재 상태로 기록한다. 시작 확인을 시각적 일치 검증으로 사용하지 않는다.

## 공통 계약

`UiControlContext`는 기존 버튼 전용 컨텍스트를 일반화한 측정·그리기 접근점이다. `UiToggle<TValue,TValuePolicy,TBehavior,TPresentation>`은 값 전이, 입력 정책, 표현을 구분한다. bool 정책과 bool? 정책은 컴파일 시점에 선택하며, 혼합 상태(null)는 활성화 시 true, 다음 활성화 시 false가 된다. 값 바인딩은 한 번의 활성화에 한 번 갱신하며 사용자 상속의 `OnChanged` 훅을 지원한다.

`Dux.Checkbox()`는 생성된 bool/bool? 타입별 `Fluent.Checkbox()`를, `Dux.Toggle()`은 `Fluent.Switch()`를 사용한다. 직접 생성하는 `UiFluentCheckbox`, `UiFluentMixedCheckbox`, `UiFluentSwitch`도 같은 기반과 입력 경로를 사용한다. 기존 즉시 모드 `Checkbox`·`Toggle`은 이전 모양과 수치 스타일을 보존하는 표현 구현을 같은 제네릭 커널에 연결했다.

표준 카탈로그에는 Button·Checkbox·Switch·ToggleButton이 있다. 혼합 체크박스는 Checkbox의 별도 타입별 팩터리로 제공하고 컴포넌트 이름은 중복하지 않는다. 네 이름은 구현 연결 범위이며 네 컴포넌트의 공식 전체 상태를 검증했다는 뜻이 아니다.

## 일반 위젯 소스 생성

```csharp
[UiWidgetStyle(typeof(UiToggle<,,,>), typeof(bool?),
    typeof(UiNullableToggleValue), typeof(UiReleaseButtonBehavior),
    typeof(UiFluentCheckboxPresentation))]
public partial class MixedChoice;
```

`UiWidgetStyle`은 구체 UiWidget 파생 구현과 제네릭 인수로 상속 클래스를 생성한다. 공개 생성자의 인수 타입·기본값·참조 전달을 분석해 생성하며, 표준 팩터리도 같은 계약을 소비한다. 버튼 전용 생성자 문자열을 복제하지 않는다. 부적합한 값 정책·동작·표현·열린 타입·제네릭 제약은 DUXWGT004, 중복 팩터리 시그니처·잘못된 노출은 DUXWGT005로 거부한다. 레거시 `UiButtonStyle`도 같은 검증·생성 경로를 사용한다. 런타임 구현 선택이나 리플렉션은 사용하지 않는다.

## 표시 문구와 입력 ID

유지되는 컨트롤을 누른 뒤 Label을 바꾸면 활성 입력 연결이 끊기는 결함을 집중 검사에서 재현했다. 인스턴스 또는 소유된 자식 키의 범위 안에서는 기본 입력 ID를 표시 문구와 분리했다. 일반 즉시 모드와 소유되지 않은 Parent 팩터리는 기존 라벨 기반 ID를 유지한다. 별도 ID 문자열을 매 프레임 이어 붙이지 않는다. 버튼·체크박스의 누름 중 라벨 변경과 keyed child 재구성의 ID 보존을 검사한다.

## 공식 기준과 미완료 범위

[공식 체크박스 지침](https://fluent2.microsoft.design/components/web/react/core/checkbox/usage)과 참조 저장소의 Checkbox/Switch 스타일·동작 소스를 커밋 `8830c10bfb2f4c1bdd540287b09713b9679cd1ad`에 고정했다. 원본·URL·SHA256은 `artifacts/fluent-toggle-2026-11-05/sources.json`에 있다. medium 체크박스16, 스위치40×20 등의 근거를 확인했지만 아이콘·모든 토큰·상태·폰트·애니메이션·실제 화면 일치는 미완료다. 키보드·포커스·접근성도 별도 구현·검증이 필요하다.

## 검증과 발견된 실패

.NET10 전체787검사, 생성기50검사 및 net8/net9/net10 솔루션 Release 빌드 경고0·오류0을 확인했다. 새 RC3 NativeAOT 샘플과 관리 FBA는 첫 GPU present·exit0을 확인했다. 새 실행 파일 SHA256은 `7A84F1A70C4AC282E11E6B1176B0522A3593EB082FBAFD3EAC7AD582713520CD`다. 성능·시각 일치 검증과 구분한다.

RC3 전체 검증은 기존 GPU 입자60단계 CPU/GPU 동치 검사에서 방향0.022302791·각속도0.06961107 오차로 실패했다(기준0.005 유지). 이후 기존 `--gpu-particles-only` 경로의 독립2회는 통과했으나 원인 해결 또는 전체 회귀 통과로 승격하지 않는다. 첫 오차가 재발하면 정확한 직전 입력·CPU 기대값·GPU 결과를 보존하도록 기존 검사에 연결했다. 전체 재실행을 반복하지 않고 작은 재현과 원인 구분이 다음 작업이다. 원본 실패·두 집중 결과·예산은 같은 artifact 폴더에 보존한다.

2026-11-12 — 결정론적인 기본 제공 리프 표현을 기존 부모 측정 캐시에 연결했으며 그리기와 입력은 계속 실행합니다. 사용자 특성은 명시적 동의가 필요하고 라벨·옵션·크기·항목·환경 변경은 배치를 무효화합니다. [공통 리프 측정 계약](widget-cpu-retention.ko.md)과 artifacts/leaf-layout-retention-2026-11-11/verification.json을 참고하세요. 전체 NativeAOT·성능 게이트는 남아 있습니다.
