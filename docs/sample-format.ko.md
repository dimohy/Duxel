# 생성 레이아웃 샘플 서식

2026-11-10 다음 기존 FBA 예제의 서식을 정리했습니다.

- `layout_containers_fba.cs`: Container·Flex·Stack.
- `layout_single_child_fba.cs`: Padding·Align·Center·제약.
- `layout_flow_containers_fba.cs`: Wrap·Grid.
- `layout_input_allocation_fba.cs`: 소유 슬롯이 있는 편집 입력.
- `layout_constrained_controls_fba.cs`: 부모 할당을 받는 Slider·Switch·Checkbox.

대입과 이름 있는 인수는 ` = `, ` : ` 공백을 사용하며 선언·콜백·제어 흐름을 읽기 쉬운 블록으로 나눴습니다. 표시 문구는 `Count : 0`, `Columns : 3`, `Inset : 8`, `Width : 280` 형식입니다. 패키지·속성 지시문과 숫자 보간 형식 `{next:0}`은 고유 구문을 유지합니다. 기존 크기·콜백·소유권은 같습니다.

각 예제는 고정 .NET 11 RC3에서 현재 저장소 소스를 사용해 별도로 검증하고, 필요한 생성기 분석기 메타데이터를 명시하며 실제 앱을 15초 실행합니다. 로컬 복사본은 패키지 지시문만 프로젝트 참조로 교체합니다. 빌드·실행 상세 근거는 `artifacts/sample-format-2026-11-10/verification.json`에 기록합니다. 시작 성공만으로 수동 입력·공식 외형·성능 검증을 주장하지 않습니다. 서식 변경으로 전체 게이트 완료 수를 늘리지 않으며 현재 NativeAOT·런타임 비교·통합 성능은 미완료입니다.
