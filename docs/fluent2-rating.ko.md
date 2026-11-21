# Fluent Rating — 대화형 별점

`Fluent.Rating`은 `UiBinding<float>` 값을 공유하는 대화형 별점입니다. 기본 최대값은5, 선택 단위는1, 크기는28입니다. `Step = .5f`로 반점을 선택합니다. 0은 미선택이며 외부에서 설정한 값은 변경하지 않고 가장 가까운 반점으로 표시합니다.

```csharp
var score = new UiState<float>(2.5f);
var rating = Fluent.Rating("서비스 평가", score.Binding,
    new UiRatingOptions { Step = .5f });
```

`UiRatingOptions`는 `Maximum`, `Step`, `Size`, `RightToLeft`를 제공합니다. 크기는 Small12/Medium16/Large20/ExtraLarge28입니다. 값은 유한한0..Maximum이어야 하며 선택 단위는1 또는0.5입니다. 잘못된 옵션은 기존 옵션을 바꾸기 전에 실패합니다. 별은 부모 너비가 부족하면 크기를 줄이지 않고 다음 줄로 이동합니다. 상속한 공통 Margin·Transform·클립 경로를 사용합니다.

호버는 미리보기이며 값을 쓰지 않습니다. 같은 반점 영역에서 눌렀다 놓으면 한 번 선택합니다. 방향키는 선택 단위로 순환하며 RTL에서는 좌우 방향을 반대로 적용합니다. Tab은 그룹을 나갑니다. 공통 비활성 범위에서는 입력과 접근성 선택을 거부합니다.

## 공통 구현과 접근성

`UiRating<TBehavior, TPresentation>`은 기존 `UiControlWidget`을 상속하며 정적 동작/표현 계약을 분리합니다. 생성된 `UiFluentRating`과 `Fluent.Rating`은 구체적 타입을 직접 연결합니다. 숫자 문자열은 옵션을 구성할 때 한 번 만들고 입력 결과는 준비한 식별자와 점수의 대응으로 처리하므로 매 프레임 숫자 파싱을 하지 않습니다.

RadioGroup과 Rating은 같은 라디오 입력·포커스·접근성 게시 경로를 사용합니다. Rating은 미선택0을 허용하는 단일 선택 그룹이고 각 점수/반점은 RadioButton·SelectionItem입니다. 루트는 독립 Tab 정지점이고 각 항목은 접근성 Focus/Select 요청을 받습니다. 입력 전용 단계는 동작·배치를 유지하고 표시는 마지막 단계에만 생성합니다. 별은 기존 컴파일 Fluent 아이콘 글꼴의 regular/filled glyph를 사용하며 반점은 공통 클립으로 나눕니다.

## 실행과 검증

현재 소스와 일치하는 패키지 자산이 이미 복원되어 있다면 저장소 루트에서 실행합니다.

```powershell
./run-gallery.ps1 -NoRestore
```

**Fluent 컨트롤 → 입력과 선택 → Rating**에서 한 점·반점의 값 공유, 지우기, RTL, 너비56의 줄바꿈을 확인합니다. `-NoRestore`는 명시적으로 기존 NuGet 자산을 재사용하는 빌드 모드입니다. 자산이 없으면 실패하며 자동 복원으로 바뀌지 않습니다. 의존성이 바뀌었다면 이 모드를 사용하기 전에 먼저 복원해야 합니다. 일반 복원이 필요한 환경에서는 정상적인 캐시 공간을 준비하고 `./run-gallery.ps1`을 사용합니다. 전역 L: 캐시 공간 부족은 별도 환경 문제입니다.

관리 .NET11에서 입력14개와 GPU32쌍, Radio 접근성18개·호환91개·공통 계약500개·순서 입력595개·Observer356개가 통과했습니다. GPU 비교는0/.5/2.5/5, LTR/RTL, 너비140/56, 슬롯1/3의 독립 글리프 배치 기준입니다. 브라우저 SVG 픽셀 동등성을 뜻하지 않습니다. 정상 Observer45화면은 평가 탭을 포함한 QueuePresent 확인이고 Linux는 호스트 빌드만 확인했습니다.

## 공식 기준과 남은 범위

[공식 Rating 지침](https://fluent2.microsoft.design/components/web/react/core/rating/usage)과 [React 구현 스냅샷](https://github.com/microsoft/fluentui/tree/64f3279c84b67f800d383244b2d73782c9913843/packages/react-components/react-rating/library/src)을 기준으로 검토했습니다. 지침의 일반 레이아웃 문구와 달리 실제 interactive 구현은 extra-large28을 기본값으로 선택하므로 해당 구현의12/16/20/28 값을 사용합니다.

강제 색상, 브라우저와의 픽셀 대조와 플랫폼 서비스 수용은 남았습니다. 카탈로그 연결은 전체 Rating 적합성 완료가 아닙니다. [대화형 검증·소스 해시](../artifacts/fluent-rating-2026-11-16/verification.json).

## 대화형 항목 이름의 지역화

항목 이름은 기본적으로 `CultureInfo.CurrentCulture`의 숫자 형식으로 표시합니다. `UiRatingOptions.ItemLabel`로 화면 읽기 프로그램에 전달할 문구를 지정할 수 있습니다.

```csharp
var rating = Fluent.Rating("서비스 평가", score.Binding, new UiRatingOptions
{
    Step = .5f,
    ItemLabel = static value => $"5점 만점에 {value:0.#}점"
});
```

입력 식별자는 언어와 관계없이 고정합니다. 언어를 바꾸거나 이름을 교체해도 선택값·접근성 런타임 식별자·누르고 있는 항목을 유지합니다. 이름은 생성 시와 언어 형식/함수가 교체될 때 한 번씩 계산합니다. 외부 번역 데이터만 바뀌었다면 `rating.RefreshItemLabels()`를 호출합니다. 함수는 이름만 반환하는 순수 함수여야 하며 null·빈 문자열·공백만 있는 이름은 허용하지 않습니다. 함수가 실패하면 일부 이름만 바뀐 상태를 게시하지 않습니다. 위젯을 해제하면 함수를 통한 외부 객체 참조도 해제합니다.

Observer **평가** 탭의 반점 예제에 한국어 항목 이름을 적용했습니다. 공식 페이지에서 4 선택 후 2 호버 시 값 유지·미리보기, 방향키로 5 선택, 기본 아이콘 28 크기를 확인했습니다. 강제 색상은 브라우저 기준 화면만 수집했으며 엔진 수용 완료가 아닙니다.

공식 브라우저의 SVG viewBox는 20입니다. Rating은 크기에 따라 24 글리프로 바꾸지 않고 20 글리프를 확대합니다. 아이콘의 원본은 컴파일된 Fluent 폰트이므로 `IPlatformVectorTextBackend`가 필요합니다. 공통 경로는 글리프의 내부 변을 제거한 윤곽을 한 번 준비하고 SVG와 같은 GPU 벡터 채움 엔진으로 가장자리 커버리지를 만듭니다. 일반 글자의 래스터 출력은 변경하지 않습니다.

커버리지 이미지는 네이티브 문단 기준선 대신 em 캔버스의 픽셀 격자에 맞춰 이중 필터링을 피합니다. 같은 윤곽·크기·카메라는 GPU 결과를 재사용합니다. 가시 영역과 카메라 계산도 SVG와 공유하며 큰 가상 크기 전체를 이미지로 만들지 않습니다. 글리프 캐시 항목 수는 기존 텍스트 캐시 한도에 맞추고, GPU 픽셀 예산은 캐시당 64 MiB를 유지합니다.

MSAA 1의 중성 가장자리 커버리지와 따뜻한 캐시의 픽셀 불변성을 검사했습니다. 독립 브라우저 기준의 고정 150×40 영역에서 다른 픽셀은 불투명 삼각형 출력의 383개에서 137개로 줄었지만 최대 채널 오차 107은 남았습니다. 이는 정확한 픽셀 동등성이 아닙니다. [비교·검증](../artifacts/fluent-rating-2026-11-16/coverage-verification.json).

## 네이티브 곡선 보존

폰트 아이콘은 DirectWrite 또는 HarfBuzz가 제공한 직선·베지어 곡선을 `PlatformTextGlyphMesh.Outline`에 보존합니다. `PlatformTextGlyphOutline`은 채움 규칙을, `UiVectorCurve`는 각 선분의 제어점을 담습니다. 제공자는 모든 윤곽을 닫고 버퍼를 변경하지 않아야 합니다. 기존 삼각형 전용 제공자도 명시적인 표현 방식으로 지원합니다. SVG와 글리프는 곡선의 단조 구간 계산과 GPU 채움 렌더러를 공유합니다.

확대·축소·회전 시 준비한 네이티브 윤곽과 GPU 장면을 재사용합니다. Windows 검사는 최초 메시 요청 두 번 이후 네 번의 변환에서 추가 메시 요청이 없고 CPU 글리프 래스터 요청도 없음을 확인합니다. 작업 횟수 검사이며 FPS 개선 측정은 아닙니다. 일반 텍스트 렌더링은 유지합니다.

독립 브라우저 기준은 폰트 윤곽을 투명 RGBA8로 먼저 확정한 뒤 RGB41 배경에 합성합니다. 고정 150×40 영역이 Windows GPU 결과와 정확히 일치합니다. 이 추가 기준으로 기존 공식 SVG의 불투명 배경 직접 렌더링 기준을 대체하지 않습니다. 기존 기준과는125픽셀 차이가 남고 최대 채널 오차는54입니다. 폰트 기하와 중간 RGBA8 양자화에 따른 차이를 구분하며 배경에 맞춘 임의 알파 보정이나 픽셀 허용 오차 완화는 적용하지 않습니다. [현재 검증 기록](../artifacts/fluent-rating-2026-11-16/curves-verification.json).

## 표시 전용 평가

`Fluent.RatingDisplay`는 입력을 받지 않는 평가 이미지입니다. 접근성에는 이름이 있는 Image 하나로 게시하며 Tab 정지점이나 선택 항목을 만들지 않습니다. 기본 크기는 Medium16입니다.

```csharp
var summary = Fluent.RatingDisplay("평균 평가", new UiRatingDisplayValue(3.5f, 1234));
var compact = Fluent.RatingDisplay("평균 평가", () => new UiRatingDisplayValue(score.Value, 1234),
    new UiRatingDisplayOptions { Compact = true });
```

`Compact`는 채워진 아이콘 하나와 평점·리뷰 수를 표시합니다. 일반 표시는 반점과 비선택 부분을 채워진 배경색으로 그립니다. 두 위젯 모두 `Shape`(Star/Circle/Square), `Color`(Neutral/Brand/Marigold)를 지원하고 공통 아이콘 표현을 사용합니다. 표시 옵션의 `ShowValue`·`ShowCount`는 화면만 숨기며 접근성 이름에는 값을 유지합니다. `ValueText`·`CountText`·`AccessibleLabel`로 표시와 접근성 문구를 명시할 수 있습니다.

숫자는 현재 스레드의 `CultureInfo.CurrentCulture`로 형식화합니다. 언어 형식 인스턴스를 교체하면 공통 측정 캐시 키도 바뀌어 부모 배치부터 다시 계산합니다. 같은 언어 이름의 사용자 지정 숫자 형식도 구분합니다. 이미 사용 중인 가변 CultureInfo를 직접 수정하는 대신 새 인스턴스로 교체하십시오. 정적 값은 측정을 재사용하며 getter 형식은 최신 값을 읽습니다. 점수는 유한한 0..Maximum, 리뷰 수는 0 이상이고 표시 전용 Maximum은 2 이상입니다.

Observer **평가** 탭에서 전체/compact 표시, 리뷰 수, 모양·색상과 RTL을 함께 확인합니다. GPU 글리프 대조는 브라우저 렌더링 동등성 검증과 구분합니다.

관리 검증은 Rating 132개(기존 46개, 표시 전용 CPU 14개·GPU 72쌍), 측정 재사용 110개, 공통 계약 500개, Observer 356개와 순서 입력 595개가 통과했습니다. 표시 전용 GPU 검사는 현재 기본 색 구성에서 모양 3종·색 3종·전체/compact·LTR/RTL·슬롯 1/3을 대조하며 모든 테마·텍스트 크기의 시각 적합성을 보증하지 않습니다. [표시 전용 검증](../artifacts/fluent-rating-2026-11-16/display-verification.json).

후속 검증에서는 지역화·벡터 출력과 밝은/어두운 테마를 포함한 Rating 219개, 독립 아이콘 17개, Radio 접근성 18개·호환성 91개와 Observer 356개가 통과했습니다. 표시 전용 GPU 대조는 144쌍입니다. 실제 Observer는 45개 화면 제출, Linux는 호스트 빌드를 확인했습니다. [브라우저·이름·벡터 검증과 남은 문제](../artifacts/fluent-rating-2026-11-16/labels-vector-verification.json).

이후 WSLg의 Linux x64 .NET 11.0.0 관리형 빌드와 앞서 게시한 NativeAOT 빌드에서 외부 `atspi-gallery-rating` 검사 3건씩을 통과했습니다. 접근성 클라이언트로 3.5점을 선택할 수 있고, 요약 평가는 점수와 리뷰 수를 담은 이미지 하나로 읽을 수 있습니다. 요약 평가에는 포커스가 가지 않으며 다른 페이지를 다녀와도 선택한 점수가 유지됩니다. 근거는 `W:/Cache/Duxel/gallery-source-20261117/rating-gallery-atspi-2034/verification.json`입니다.

Windows 갤러리에서도 실제 창을 열어 외부 UI Automation과 마우스 입력으로 여덟 가지를 확인했습니다. 3.5점 위에 포인터를 올리면 선택은 2.5점으로 유지됐고, 클릭하자 기본형과 요약형의 표시가 함께 3.5점으로 바뀌었습니다. 두 표시는 초점을 받지 않는 이미지이며 점수와 리뷰 수를 접근성 이름에 담습니다. 리뷰 수를 1,235건으로 늘리자 두 이름도 바뀌었습니다. 기본형에서는 별 다섯 자리, 요약형에서는 별 하나와 동일한 점수·리뷰 수를 실제 화면에서 확인했습니다. Windows .NET 10.0.15와 .NET 11.0.0에서 강제 색상 단위 검사도 포함한 `rating-only` 1,387개씩이 통과했습니다. 근거는 `L:/Cache/Duxel/fluent-rating-live-20261120/verification.json`입니다. 공식 웹 예제와의 픽셀 대조 및 Windows의 시스템 강제 색상 모드는 남았습니다.
