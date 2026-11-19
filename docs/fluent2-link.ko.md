# Fluent 2 Link

Last synced: 2026-11-10

Link는 탐색 목적지와 필수 앱 콜백을 갖는 텍스트 위젯이다. 렌더링만으로 브라우저나 프로세스를 실행하지 않는다. 앱이 상대 경로·외부 URI의 탐색 방식을 결정한다.

```csharp
var link = Fluent.Link("사용 안내", new Uri("/guide", UriKind.Relative), Navigate);
IUiView view = Dux.Link("참조 문서", new Uri("/reference", UriKind.Relative), Navigate);

[UiWidgetStyle(typeof(UiLink<,>), typeof(UiLinkNavigationBehavior),
    typeof(UiFluentLinkPresentation<UiFluentSubtleLinkAppearance, UiInlineLinkUnderline>))]
public partial class ProductLink;
```

기본 위젯과 사용자 위젯은 `UiLink<TBehavior, TPresentation>` 상속·생성 경로를 공유한다. 생성된 클래스를 상속하여 `OnNavigate(Uri)`를 확장할 수 있다. `Label`, `Destination`, `Navigate`, `Width`를 제공한다. 표시 라벨을 바꿔도 유지된 위젯의 입력 정체성은 보존한다. 해제 시 목적지·콜백·줄 문자열을 해제하고 외부 객체를 임의로 Dispose하지 않는다.

직접 API도 같은 입력·측정·그리기를 사용한다. `UiLinkContent<TPresentation>`를 보유해 줄 레이아웃을 재사용한다.

```csharp
ui.Link<UiLinkNavigationBehavior,
    UiFluentLinkPresentation<UiFluentDefaultLinkAppearance, UiStandaloneLinkUnderline>>(
    "사용 안내", destination, Navigate, content, width : 160);
```

포인터 놓기와 Enter로 탐색한다. Space는 탐색 링크를 활성화하지 않는다. Tab 정지점1개와 공통 포커스·클리핑·비활성·제거 시 캡처 취소를 사용한다. hover 시 손 모양 커서를 요청한다.

표현은 Default/Subtle/Inverted/OnBrand의 네 구현 타입과 Standalone/Inline 밑줄 타입을 선택한다. 라벨은 Regular14/20이다. 일반 링크는 hover/pressed/focus에 밑줄을, 인라인 표현은 rest에도 밑줄을 그린다. 키보드 포커스는 모든 줄에 이중 밑줄을 사용하며 비활성은 밑줄을 제거한다. 줄마다 실제 글꼴 baseline과 측정 폭을 사용한다. `Width > 0`은 가용 줄바꿈 폭이며0은 명시적 개행에 따른 자연 폭이다. 긴 단일 단어의 최소 폭 처리는 공통 텍스트 규칙을 따른다.

레이아웃 키는 RadioGroup과 공통으로 컨텍스트·폰트·크기·배율·텍스트 설정·백엔드 세대를 반영한다. 컨텍스트는 약하게 참조하며 source1024자·추가 줄 문자열4096자·각 폰트 경로1024자 이내에서만 캐시한다. 빈 줄 배열은 추가256칸까지만 보존하며 사용자 표현의 대체 문자열도 실제 보유 길이로 검사한다. 관찰자가 있으면 이전 캐시도 재사용하지 않는다. 사용자 표현의 재사용은 기본으로 꺼져 있고 결정적 측정 계약에 동의할 때 활성화한다. 실패한 측정은 부분 레이아웃을 정리하고 예외를 전달한다.

[공식 Link 지침](https://fluent2.microsoft.design/components/web/react/core/link/usage)은 조회 시 Preview다. 참조 커밋8830c10bfb2f4c1bdd540287b09713b9679cd1ad, 소스와 해시는 `artifacts/fluent-link-2026-11-06/plan.json`을 따른다. 기존 브랜드/회색 토큰의 실제 값을 재사용하며 원본 주석의 오래된 색으로 대체하지 않는다.

`--link` 갤러리는 여덟 표현과 실제 내부 페이지 탐색/돌아오기를 제공한다. 관리 .NET10과 고정 .NET11 RC3 NativeAOT 검증을 구분하며 최신 검사·픽셀 비교·소스/실행 파일 해시는 같은 폴더의 `verification.json`에 있다. 경로 간 동치 외에도 모든 상태의 실제 잉크와 인라인 rest/hover/disabled/focus 차이를 확인한다. 성능 개선율은 측정하거나 주장하지 않는다.

공식 브라우저 픽셀 동치와 underline의 실제 폰트 메트릭, UIA의 링크 목록·접근 가능한 이름, 강제 색상·논리적 RTL·텍스트 선택·방문 기록·컨텍스트 메뉴·disabledFocusable·문단 안의 혼합 인라인 구성은 미완료다. 인라인 밑줄 모드만으로 이 기능들이 완료됐다고 판단하지 않는다. 전체 Fluent·공통 엔진·Pi 목표는 계속 유지한다.

## 측정과 부모 할당 — 2026-11-10

`UiLink.MeasureContent`는 `UiControlContext.MeasureLink(label, content, width)`를 사용합니다. 기존의 제한된 UiLinkContent<TPresentation>·폰트 키를 재사용하며 측정은 입력 처리·탐색 콜백을 호출하지 않습니다. 자연 Width가 0이거나 220이어도 제약 부모의 실제 너비로 줄바꿈합니다. 독립 렌더와 기존 즉시 오버로드는 이전 자연 크기·Width 규칙을 유지합니다.

```csharp
using var link = Fluent.Link("Read the guide", new("/guide", UriKind.Relative), Navigate, width : 220);
using var box = Dux.SizedBox(link, 80, 64);
// The actual root and pointer/keyboard bounds are 80 x 64.
```

IUiLinkPresentation.Arrange(context, natural, allocatedSize)가 변경된 크기를 명시적으로 수용하며 기본 구현은 예외를 냅니다. 기본 표현은 지역 레이아웃 복사본의 Size만 바꾸고 텍스트 블록·폰트 크기·baseline·밑줄 측정을 유지합니다. 정렬된 복사본을 자연 레이아웃 캐시에 덮어쓰지 않습니다. 같은 너비의 측정/렌더는 이를 공유하고 높이만 바뀌면 줄을 재구축하지 않으며 실제 너비가 바뀌면 줄바꿈을 한 번 갱신합니다. 위젯/컨텍스트 필드·새 캐시는 추가하지 않습니다.

렌더는 실제 할당만큼 커서를 진행하고 같은 루트를 입력·그리기에 전달하며 글자·밑줄·포커스 장식을 해당 루트로 자릅니다. 잘린/0 루트는 보이는 영역 밖에서 활성화되지 않습니다. 표·열 상태를 스코프로 복원합니다. 명시적 즉시 오버로드는 content, width, constrainLayout : true를 받습니다. 잘못된 자연 크기와 다른 Size를 반환하는 Arrange는 입력 동작 전에 실패합니다.

고정 .NET11 RC3 관리 495검사 통과: CPU/입력/수명 61, 기존 GPU 160, 할당 픽셀 232, 서로 다른 유지 주기 픽셀 40, 재사용 횟수 2. 래스터/벡터·MSAA 1/4·Light/Dark·프레임 슬롯 3을 포함하며 준비 프레임을 포함한 readback은 673개입니다. 포인터는 할당된 빈 영역·캡처 중 이름 변경·좁거나 짧은 루트·상위 클립을 검사했고 키보드는 Enter 탐색·Space 제외·0 루트 포커스 해제를 보존합니다. 기존 캐시 수용 한도·다른 컨텍스트·observer·해제 검사도 포함합니다.

새 layout_link_allocation_fba.cs는 Width : 100, Height : 120, Short/Long과 앱 소유 탐색 상태를 제공합니다. 원본과 지시문 외 동일 본문의 로컬 참조 FBA 빌드·첫 표시·15초 종료를 수동 입력과 구분해 기록했습니다. 근거·해시: artifacts/link-allocation-2026-11-10/verification.json. 표준 VS C++ 탐색 부재로 현재 수정의 Native·런타임 이미지 비교는 미검증이며 이전 Native 근거는 당시 리비전에만 적용됩니다. FPS·메모리·할당 개선은 주장하지 않으며 전체 수치·리사이즈 보류를 유지합니다.

2026-11-12 후속 — [콘텐츠 측정 계약](widget-cpu-retention.ko.md): 결정론적 소스·표현은 부모 재사용에 참여하고 동적 문자열/배지 getter와 사용자 특성은 기본 재측정합니다. getter Progress의 선언 기하와 Spinner의 안정된 표시는 별도 약속이며 그리기·getter·애니메이션·이동 처리는 계속 실행합니다. 집중26개와 실제 GPU 비교는 이 범위의 근거이며 전체 NativeAOT·성능 완료가 아닙니다. 근거: artifacts/content-layout-retention-2026-11-12/verification.json.
