# Global UI Dirty-Rect Plan (MVP → Full)

## 목표
- 레이어뿐 아니라 일반 위젯/텍스트/팝업까지 포함해 변경 영역만 재합성하여 프레임 시간을 단축한다.

## 왜 레이어만이 아닌가
- `TextV` 숫자 변경, caret blink, 툴팁/팝업 이동처럼 화면의 작은 부분만 바뀌는 경우가 많다.
- 현재는 해당 변화가 있으면 상위 drawlist 재처리가 상대적으로 크게 발생한다.

## 단계별 전개

### 1단계 (완료 방향: 레이어 MVP)
- 레이어 캐시 무효화를 `MarkAllLayersDirty`에서 `MarkLayerDirty` 중심으로 축소.
- 현재 샘플에서 density 변경 시 해당 레이어 body만 dirty 처리.

### 2단계 (전역 UI Dirty-Rect 수집)
- 프레임 중 `Add*` 호출 시 최종 clip + geometry bounds를 누적하여 DirtyRectUnion 생성.
- 창/팝업 이동 시 이전/현재 rect를 둘 다 dirty에 포함.
- 커서/선택 하이라이트, caret blink 등 고빈도 변화도 별도 dirty source로 등록.

### 3단계 (합성/제출 최적화)
- Vulkan 백엔드에서 scissor 기반 부분 재합성 경로 추가.
- DirtyRectUnion이 전체 화면 임계치(예: 35~45%)를 넘으면 기존 full path로 fallback.

### 4단계 (안정화)
- OFF/ON 시각 동일성 캡처 비교 자동화.
- 1% low / frame-time variance를 기준으로 성능 회귀 감시.

## 리스크
- DirtyRect 계산/병합 자체의 CPU 오버헤드.
- 겹치는 창/팝업 계층에서 누락 dirty 발생 시 시각 아티팩트.

## 검증 기준
- 기능: OFF/ON 이미지 diff 허용 오차 내 유지.
- 성능: 작은 UI 변화 시나리오에서 frame-time 중앙값 개선.
