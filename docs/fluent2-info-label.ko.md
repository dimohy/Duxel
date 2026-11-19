# InfoLabel

`Fluent.InfoLabel`은 독립 라벨과 정보 버튼을 조합합니다. 라벨은 별도로 렌더링한 입력을 참조하고 정보 팝업의 콘텐츠는 직접 소유합니다.

```csharp
var name = new UiState<string>("정세일");
var input = Fluent.Input("이름", name.Binding, 100, 240);
var label = Fluent.InfoLabel("사용자 이름", Fluent.Body1("다른 사용자에게 표시할 이름입니다."),
    input, new(Required: true), informationLabel: "추가 정보");
var form = Dux.Column(new(Spacing: 8, MainAxisSize: UiMainAxisSize.Min), label, input);
```

- 라벨 클릭은 연결한 입력으로 포커스를 옮깁니다. 정보 버튼은 별도의 Tab 대상입니다.
- 실제 클릭을 놓거나 Enter·Space를 누르면 정보 팝업을 엽니다. Escape·바깥 클릭·포커스 이탈로 닫습니다.
- 버튼의 접근성 이름은 표시 라벨과 지역화한 `InformationLabel`을 합칩니다. 줄바꿈은 이름 안에서 공백으로 정리합니다.
- 팝업은 기본 `Note` 역할, 위쪽 시작 위치, 화살표와 폭264를 사용합니다. `PopoverOptions`, `Open`, `OpenChanged`, `Show`, `Close`로 제어합니다. 포커스 제한을 명시하면 Dialog 역할을 사용합니다.
- `Text`, `Target`, `Options`를 변경할 수 있습니다. 대상은 소유하지 않으며, 해제 시 라벨 연결과 팝업 콘텐츠를 정리합니다. 정보가 없으면 버튼을 만들지 않습니다.

| 크기 | 아이콘 | 버튼 클릭 영역 | 텍스트 줄 높이 |
| --- | ---: | ---: | ---: |
| Small | 12 | 20×20 | 16 |
| Medium | 16 | 24×24 | 20 |
| Large | 20 | 24×24 | 22 |

기본 아이콘은 Fluent 아이콘 폰트의 regular/filled 글리프입니다. 호버·열린 상태에서는 filled 글리프와 브랜드 전경색을 사용합니다. 이 경로는 SVG XML을 처리하지 않습니다. SVG 입력과 변환 위젯은 별도의 `Image`·`Icon` 경로를 사용합니다.

정보 버튼은 텍스트의 마지막 줄 뒤에 배치하며 공간이 부족하면 다음 줄로 이동합니다. Duxel의 부모는 자식 할당 영역을 클립하므로 CSS의 음수 여백을 라벨 안쪽2/2/1px 여백으로 표현합니다. 아이콘과 텍스트의 상대 정렬을 유지하면서 전체 클릭 영역을 할당 안에 포함합니다. 명시한 부모 클립과 폭·높이 제한은 그대로 적용합니다.

실행: `./run-gallery.ps1` → **추가 정보**. 크기·라벨을 바꾸고 입력 연결, 팝업 안 버튼, Tab·Escape와 탭 왕복을 확인하세요. 변하지 않은 상태는 주기 프레임을 예약하지 않습니다.

기준: [공식 사용 지침](https://fluent2.microsoft.design/components/web/react/core/infolabel/usage/), [고정 InfoButton 스타일](https://github.com/microsoft/fluentui/blob/8830c10bfb2f4c1bdd540287b09713b9679cd1ad/packages/react-components/react-infolabel/library/src/components/InfoButton/useInfoButtonStyles.styles.ts).

현재 검증은 생성 API, 의미 관계·입력·소유권 CPU 검사와 실제 GPU 픽셀을 포함합니다. 물리 스크린리더, 강제 색상 테마, RTL 전체 외관과 NativeAOT는 별도 검증 범위입니다. 전체 Fluent 적합성 완료를 뜻하지 않습니다.
