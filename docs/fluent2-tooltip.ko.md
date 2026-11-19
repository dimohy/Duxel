# Fluent 2 Tooltip 참고 계약

Last synced: 2026-11-11

Tooltip과 팝업이 공유하는 래스터화 코드는 한 번의 호출에서 두 그림자가 임시 배열의 영역 네 개를 함께 쓰도록 합니다. 호출이 끝나면 배열을 계속 보관하지 않으며, 출력 텍스처는 별도 배열을 소유합니다. Linux .NET 11 NativeAOT 혼합 크기 검사에서 기존 래스터화 코드보다 임시 할당이 약 29% 줄었고, 측정한 프로세스 메모리 최고치도 낮았습니다. [메모리 비교](optimization-session-2026-11-19.md#popup-scratch-retention-controls)

현재 기반: [컴파일된 Tooltip 대상](tooltip-targets.ko.md)은 실제 대상 조합, 지연 소유권, 지연 팝업 그리기와 Escape 소비를 구현하고 CPU·GPU·샘플 근거를 갖췄다. [대상 접근성](tooltip-accessibility.ko.md)은 설명·레이블·비노출 관계와 실제 Windows UIA 속성을 검증했다. 생성된 Fluent 팩터리는 기존 대상 동작과 Fluent 표현에 연결된다. 텍스트 속성 변경과 종료 알림도 외부 클라이언트로 검증했으며 공식 전체 준수 검증은 남아 있다. [표면 노드·열림/닫힘](tooltip-events.ko.md)은 실제 외부 UIA로 검증했다. 아래 목록은 전체 Fluent 검증 범위다.

`Fluent.Tooltip(target, text, options)`는 소스 생성된 대상 래퍼를 제공한다. 기존 즉시 모드 `BeginTooltip`/`SetTooltip`은 마우스에서 16px 떨어진 위치에 여백 6px로 즉시 표시하므로 Fluent 준수의 근거가 되지 않는다.

## 권위와 동작

[공식 Web 지침](https://fluent2.microsoft.design/components/web/react/core/tooltip/usage)은 대상에 연결되는 짧고 비필수적인 비대화형 텍스트를 정의한다. 대화형 내용에는 Popover를 사용한다. 호버나 포커스로 표시하고 이탈이나 포커스 해제로 닫는다. 툴팁으로 포인터를 옮겨도 표시를 유지해야 한다. 대상과의 기본 간격은 4px이며 선택적 화살표는 대상을 가리킨다.

고정한 React 구현의 추가 참고 기본값은 표시·해제 지연 각각 250ms, 위쪽 가운데 배치, normal/inverted 표현, 기본 화살표 없음, 표시 소유자 하나, 대상 사이의 즉시 전환, 포커스를 유지하는 Escape 해제, label/description/inaccessible 관계다. 시각적으로 숨겨도 설명 의미는 접근성에 남아야 한다. 제어된 표시 상태는 변경 알림 계약을 유지해야 한다. 이는 공식 컴포넌트의 참고 계약이며 별도 상위 컴포넌트가 아니다.

[iOS 지침](https://fluent2.microsoft.design/components/ios/core/tooltip/usage)은 작업 흐름·프로그램에 의한 표시와 바깥 탭 해제를 포함한다. 해당 페이지에는 Preview가 표시되어 있다. [Windows 지침](https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/tooltips)은 길게 누르기와 Ctrl 해제, 네이티브 창 배치 및 더 풍부한 내용을 허용한다. 플랫폼 차이를 명시적으로 기록하며 공식 사이트의 Web 기본값을 조용히 바꾸거나 Windows 테마를 따로 만들지 않는다.

## 관측한 외형 기준선

실행 중인 [공식 Storybook](https://storybooks.fluentui.dev/react/?path=/docs/components-tooltip--docs)의 밝은·어두운 테마를 확인했다. 배포 커밋은 미확인이며 고정 저장소 스냅샷과 같다고 가정하지 않는다.

| 속성 | 관측 |
| --- | --- |
| 글꼴 | Segoe UI, 12px, 굵기 400, 행 높이 16px |
| 기하 | 최대 폭 240px, 반경 4px, 투명 테두리 1px |
| CSS 여백 | 위 4px, 오른쪽 11px, 아래 6px, 왼쪽 11px; 바깥 기준 내용 여백에는 테두리도 포함 |
| 밝은 normal / inverted | 배경 `#FFFFFF` / `#333333`, 글자 `#242424` / `#FFFFFF` |
| 어두운 normal / inverted | 배경 `#292929` / `#3D3D3D`, 흰색 글자 |
| 그림자 | `(0, 0, 2px)` 다음 `(0, 4px, 8px)`의 연속 drop-shadow; 밝은 알파 .12/.14, 어두운 알파 .24/.28 |
| 화살표 | 선택 사항, 참고 높이 6px; 표면과 합친 실루엣에 필터 적용 |

스크린샷 다섯 장과 계산된 스타일은 `artifacts/fluent-tooltip-2026-11-11`에 보존했다. Escape 해제 후 대상 포커스 유지를 관측했다. 공식 참고 화면이며 Duxel 비교 결과는 아니다. 브라우저 테마는 Web Light로 복원했다.

## 구현 경로와 남은 검증

`Fluent.Tooltip(Fluent.Button("Save"), "Save a local copy")`는 `UiHoverFocusTooltipBehavior`와 `UiFluentTooltipPresentation`을 정적으로 연결한다. 실제 대상을 소유하며 기존 지연·Escape·배치·접근성·수명 구현을 공유한다. 화살표는 `Positioning.ArrowHeight = 6`, 이름 관계는 `Relationship = UiTooltipRelationship.Label`로 선택한다. 호출자 제어 `Visible`과 `VisibleChangeRequested`도 제공한다. `widget_tree_theme_fba.cs`의 `--tooltip` 모드와 관찰 창의 Tooltip 탭에서 생성 팩터리를 사용한다.

팩터리 단계는 CPU 집중9검사와 기존 제네릭 소비 경로를 기준으로 밝기·MSAA1/4를 비교한 GPU 추가24검사를 통과했다. 생성기115·카탈로그34이름 검사도 통과했고 Tooltip 집중 GPU 전체는792검사다. 실제 팩터리를 사용한 외부 종료·가시성 쌍은4/6·5/9를 통과했다. Duxel 소비 경로 검증이며 공식 브라우저 픽셀과의 동치는 아니다. 현재 NativeAOT·런타임 이미지 비교와 공식 전체 상태·외형·접근성 준수는 남아 있다. 아래는 원래 구현 요구 목록이며 대상·소유자·Escape·표면·팩터리 경로는 현재 구현되어 있다:

1. 안정적인 식별자, 순수 측정과 정적으로 선택한 동작·표현을 갖춘 실제 대상 래퍼를 구성한다.
2. 모든 대상 등록 후 창별 표시 소유자를 하나로 확정한다. 지연 시각, 대상 전환, 프레임 누락·폐기와 예약 깨우기를 구현하며 유휴 대상마다 상태 테이블을 만들지 않는다.
3. 자식 입력·팝업 처리 전에 Escape를 소비한다. 포커스를 유지하고 비활성 대상 설명을 포함한 label/description 관계를 구현한다.
4. 대상 기준 배치, 화살표, 표면 전체 그림자와 루트 클립을 적용하는 팝업 계층을 구현한다. 대상이 보이면 조상 클립을 벗어날 수 있지만 완전히 잘린 대상에는 표시하지 않는다.
5. 실제 소비 경로가 작동한 뒤 생성 팩터리를 연결한다. CPU 시간·수명·입력, GPU 밝기·배치·클립, 실행 샘플, 접근성, 고정 .NET11 Native·런타임 비교를 검증한다. 현재 Native 도구체인 위치는 미확인이다.

카탈로그는 노출 이름 35개다. 상위 55개 중 16개와 부모 연결 변형 19개이며 미연결 상위 이름은 39개다. 전체 변형·상태·외형·접근성의 분모는 미확정이다. 팩터리 연결은 Fluent Tooltip 전체 준수나 성능 개선을 뜻하지 않는다.

고정 참고는 `microsoft/fluentui` 커밋 `8830c10bfb2f4c1bdd540287b09713b9679cd1ad`의 Tooltip 소스와 내부 상수다. 복사한 `LICENSE.txt`는 MIT 허가문과 자산 관련 주석을 포함하며 GitHub 라이선스 API는 `NOASSERTION`을 반환했다. 출처는 단계별 계약, 카탈로그 차이 감사 및 참고 자료 명세를 따른다.

현재 TabList 카탈로그: `artifacts/fluent-tablist-2026-11-11/final-catalog-only/result.json`. [TabList 기본 구현](fluent2-tablist.ko.md)을 참조한다.

## 대상 측정 위임 — 2026-11-12

Tooltip은 CanRetainOwnedMeasurement(Target)으로 살아 있는 소유 대상에 조상 측정 재사용을 명시적으로 위임합니다. 팝업 표현과 표시 동작을 캐시하는 계약은 아닙니다. 예약·표시/숨김 시한·실제 대상 렌더링·팝업 그리기·Escape·소유권 정리는 계속 실행합니다. RenderTooltipTarget은 이제 공통 소유 자식 크기 캐시를 조회합니다. 변하지 않은 알려진 대상은 앞서 측정한 크기를 재사용하고, 불투명한 대상은 각 렌더링에서 계속 측정·검증합니다. 알 수 없는 대상은 동적으로 유지합니다. 빌린 대상이나 폐기된 대상에 대한 위임은 명시적으로 실패합니다. Field도 같은 소유권 검증을 사용합니다. 근거: artifacts/delegated-layout-retention-2026-11-12/verification.json.
