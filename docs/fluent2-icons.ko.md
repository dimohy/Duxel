# 컴파일된 공식 Fluent 아이콘

2026-11-15 실제 패키지·SDK 컴파일러 행렬: 프로필별131개가 통과하고 생성 파일6개는 그대로입니다. 독립 파일에서도 첫/중간/마지막 선언 제거의 남은 출력 재사용은0/1/2개이며 원래 첫 제거2/2 기준은 미완료입니다. [선택과 근거](widget-contract-validation.ko.md).

Last synced: 2026-11-06

2026-11-13 재검증: 생성기 전체117건이 Windows·Ubuntu x64의 .NET11.0.0에서 각각 통과했고 저장한 생성 소스6개가 바이트 단위로 같습니다. 선언 제거 시 나머지 출력 캐시는 양쪽 모두0/2로 미충족입니다. 아래의 과거 검증과 구분하며 [실행 경로와 범위](widget-contract-validation.ko.md)를 참고하세요.

정확한 크기는 `[UiFluentIcon("checkmark", UiFluentIconStyle.Filled, 12)]`로 선택한다. 지원 선택값은0(기존20/24),12,16,20,24,28,32,48이며 해당 고정 크기가 실제 카탈로그에 없으면 DUXWGT008이다. 고정 출력에는 크기 선택 분기가 없다. 기존2인수 생성자는 보존한다. [Checkbox](fluent2-checkbox.ko.md)의6개12/16px 마크가 실제 소비 예다.

번들 폰트와 글리프 매핑은 [Microsoft Fluent System Icons](https://github.com/microsoft/fluentui-system-icons/tree/9bfae3d782fda4c8764f2128cec4d0dda160b283/fonts)의 커밋 `9bfae3d782fda4c8764f2128cec4d0dda160b283`에서 가져왔다. 원본 MIT 라이선스를 `Assets/FluentIcons`에 함께 배포한다. regular10265개·filled10389개 매핑의 모든 코드가 실제 폰트 cmap에 있는지 확인했다. 두 폰트는 unitsPerEm500/ascent500/descent0이다. 아이콘 자산 수이며 Fluent 컴포넌트 요구사항을 추가한 수가 아니다.

`UiFluentAddIcon`, `UiFluentAddFilledIcon`, `UiFluentAddInteractiveIcon`은 생성된 `IUiButtonIcon` 구현이다. 실제20/24px 글리프와 폰트 타입에 직접 연결한다. Interactive는 기본 상태에서 regular, 호버/누름에서 filled를 선택하므로 아이콘이 상태에 따라 바뀌는 표현에 사용한다. 이전 사용자·검사 도형과 공식 자산 경로를 구분한다.

```csharp
[UiFluentIcon("add", UiFluentIconStyle.Interactive)]
public readonly partial struct ProductAddIcon;

[UiButtonStyle(typeof(UiReleaseButtonBehavior),
    typeof(UiFluentIconButtonPresentation<UiFluentMediumButtonIconSize,
        UiFluentSubtleButtonAppearance,ProductAddIcon,UiButtonIconAfter>))]
public partial class AddButton;
```

로컬 프로젝트 참조/FBA 빌드는 고정 JSON2개를 `AdditionalFiles`로 전달한다. 갤러리도 위와 같은 사용자 특성 경로를 실제 소비한다. 외부 빌드에서는 선택한 스타일에 맞는 `FluentSystemIcons-Regular.json` 및/또는 `FluentSystemIcons-Filled.json`을 정확히 하나씩 제공해야 한다. 이름은 공식 소문자 계열 식별자이며20/24 크기가 모두 필요하다. 누락·중복·잘못된 카탈로그, 잘못된 전용 문자 코드, 없는 이름/크기, 지원하지 않는 선언에는 `DUXWGT008`을 표시한다. 다른 글리프로 대신하지 않는다.

증분 생성기는 빌드 입력을 해석하고 요청한 선언에만 UTF-16 상수와 직접 `FontIcon<TFont>` 호출을 만든다. 런타임 이름 레지스트리나 사용하지 않는 수천 개 아이콘 타입을 만들지 않는다. 같은 입력의 결정성은 검사했으며 변경 범위별 증분 동작 전체 검증은 별도다.

실제 그리기는 공통 DirectText 래스터/벡터 백엔드이며 주변 폰트 범위를 복원한다. 고정 폰트의 메트릭을 사용하고 서로 다른 글리프 advance는 실제 폭 측정으로 반영한다. 아이콘 메트릭을 위해 텍스트 메트릭 캐시 칸이나 위젯별 메타데이터 객체를 추가하지 않았다. 폰트 파일 총5466852바이트와 라이선스가 빌드/게시 출력에 복사된다. 파일 크기를 RAM 사용량으로 해석하지 않는다. 실행 중 JSON 조회·폰트 해시 계산은 없고 검사는 GPU 초기화 전에 자산 해시를 확인한다. 적합한 DirectText 백엔드가 필요하며3D 레이어에는 벡터 텍스트 백엔드도 필요하다. Linux/ARM64 지원을 뜻하지 않는다.

검증과 정확한 소스·폰트·실행 파일 해시는 `artifacts/fluent-icon-assets-2026-11-06`에 있다. 실제 외곽선과 픽셀 샘플을 구분한다. 얇은 래스터 선은 완전히 흰 픽셀이 없을 수 있고, 서로 다른24px regular/filled 벡터 외곽선도 MSAA1에서 같은 픽셀을 만들 수 있다. 모든 조건에서 상태 선택은 해당 고정 폰트 결과와 정확히 같아야 하며 검증한 래스터/벡터 조건에서 두 스타일이 구별되어야 한다. 공식 SVG/브라우저 래스터 일치, 아이콘 전용 접근성 이름/UIA, 논리 RTL, 전체 심볼 API와 앱 전체 성능은 미검증/미완료다. FPS·Private·Pi 성능 개선 주장은 없다.

현재 검증: .NET10 전체1862개/생성기96개, 고정RC3 Native EXT/core각1009개, net8/9/10 경고0·오류0. 새 관리/Native 이미지192건과 이전 Native2026건/관리1290건이 정확히 같다. 최신 갤러리는 시작·첫 제출·자동 종료만 검증했다.

## 증분 출력과 남은 선언 제거 비용

아이콘 생성기는 선언·카탈로그에서 선택된 글리프 코드·이름·스타일만 불변 값으로 추린다. 정상 바인딩은 구문 위치·의미 모델을 보유하지 않는다. 값이 같으면 소스 문자열 구성과 Roslyn 출력 모두를 재사용한다. 오류에는 현재 위치를 보유하므로 오류 선언을 옮기면 새 소스 트리·범위를 반영한다.

생성기 기능107개에는 실제 추적 드라이버 검사가 포함된다. 무변경·사용하지 않는 글리프 변경·무관한 코드 변경은 출력3개를 모두 재사용하고, 선택한 regular/filled 글리프 변경은 의존 스타일2개만 바꾼다. 없는 글리프·잘못된 데이터는 오래된 출력을 제거하고 오류를 보고한다. 무관한 AdditionalFiles도 캐시를 유지한다. 전후 Core 생성 파일57개의 이름·바이트가 모두 같으며 런타임 소스·아이콘 자산5개·기존 Native/갤러리 해시도 확인했다. 이 호환 범위에서 이전 .NET10 렌더링1862개·고정RC3 Native EXT/core각1009개 근거를 재사용했다. 새 GPU·성능 측정으로 다시 보고하지 않는다. 솔루션과 현재 관리 FBA 소비자는 컴파일을 통과했다.

한 가지 최적화 기준은 미충족이다. 같은 소스 파일의 첫 선언을 제거하면 Roslyn 순서 항목이 이동해 나머지 출력2개도 다시 구성한다. 내용은 같고 제거된 출력도 사라지지만, 기대 캐시2개·관측0개를 `strictRemovalCacheComplete=false`로 따로 기록했다. 기능 검사 통과를 이 성능 기준의 충족으로 계산하지 않는다. 실제 추적·미완료 기준·호환 근거는 `artifacts/fluent-icon-incremental-2026-11-06`에 있다. 증분 최적화 전체 완료나 앱 FPS 개선 주장이 아니다.
