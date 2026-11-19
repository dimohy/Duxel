# 캐시하는 외부 박스 그림자

[English](box-shadow.md)

`UiBoxShadow`는 CSS의 앞→뒤 순서인 외부 그림자 두 층과 문맥별 텍스처 임대를 관리합니다. `UiControlContext.BoxShadow(shadow, bounds, cornerRadius)`는 레이아웃 항목·입력·접근성을 추가하지 않고 둥근 사각형 그림자만 그립니다. 레이아웃에는 `shadow.Options.PaintBounds(bounds)`를 예약하고 소비가 끝날 때까지 소유자를 유지하세요.

```csharp
using var shadow = new UiBoxShadow(
    UiFluentShadow.Resolve(UiFluentColorScheme.Light, UiShadowElevation.Shadow8));
// 그리기 안에서 호출합니다.
context.BoxShadow(shadow, bounds, cornerRadius : 8);
```

`UiShadowLayer`는 Offset·BlurRadius·일반 RGBA Color·실수 Opacity를 받습니다. Gaussian의 sigma는 BlurRadius/2이며0이면 analytic 경계입니다. 안쪽 그림자와 spread는 이 계약에 포함되지 않습니다. 본체는 투명하게 유지합니다. 두 그림자를 먼저 합치고 analytic 본체를 제외합니다. 배경색 덮개나 다각형 외곽선을 사용하지 않습니다. Fluent 중립 Shadow2/4/8/16/28/64는 고정한 CSS 위치·흐림과 밝음0.12/0.14,어두움0.24/0.28의 ambient/key 투명도를 사용합니다. 토큰 기준을 맞춘 것이며 브라우저 픽셀과 완전히 같다는 뜻은 아닙니다.

둥근 사각형의 직선 구간은 정확히 적분하고 모서리는16점 Gauss-Legendre 적분과 정규 CDF 근사로 처리합니다.4-sigma 범위에 논리1픽셀을 더해 영역을 확보합니다. 최초에 실제 해상도로 analytic 형태를 계산하고 불변 RGBA8 컬러 텍스처 하나를 전송 대기열에 넣습니다. 같은 옵션·본체 크기·반경·콘텐츠 배율은 문맥 임대를 재사용하고 위치 이동은 다시 생성하지 않습니다. 문맥마다 크기·DPI·색의 키를 유지합니다. 옵션 변경은 다음 명시적 그리기에서 반영하며 이 자원은 프레임을 요청하거나 호출자의 ContentVersion을 무효화하지 않습니다. 직접 Dispose하세요. 이미 대기 중인 프레임의 원본 픽셀은 바뀌지 않습니다.

래스터 하나는4,194,304픽셀(RGBA16MiB)까지 허용합니다. 기존 공통 래스터 캐시는128개·64MiB 한도를 적용하며 살아 있는 임대·유지된 참조·현재 프레임 항목을 퇴출하지 않습니다. 잘못된 입력과 예산 초과는 생성·전송 전에 실패하고 해상도를 낮추지 않습니다. 최초 CPU 준비는 추가 작업이며 비용0이나 속도 개선을 주장하지 않습니다.

불투명3D 콘텐츠 안에서는 그림자를 깊이 읽기 전용 자식 재질로 제출합니다. 캡처한 부모와 WriteDepth만 다른 유지 자식은 그 차이를 유지하며 바뀐 부모 장면을 따릅니다. 공통 최종 출력은 연속된 같은 깊이 범위 안에서 불투명 쓰기→반투명 읽기 순서로 안정적으로 제출합니다.2D 면·프레젠테이션 범위·독립 깊이 범위·외부 콜백을 넘지 않으며 읽기 재질끼리의 그리기 순서는 유지합니다. 서로 교차하는 일반 반투명 면은 호출자가 뒤→앞 순서를 정해야 합니다. 이것은 별도의 그룹 투명도 합성과 구분됩니다.

`./run-gallery.ps1` 실행→**아바타**→**아바타 활동 그림자**에서 확인하세요. 실제 GPU는 패턴 배경·둥근 모서리·공통 잘라내기·깊이 제출 반전·캐시된 카메라 변경·획득 이미지 재사용을 검사합니다. CPU는 독립적인 촘촘한 원 적분과 대조합니다. 근거와 남은 원래 NativeAOT/성능/전체 적합성 범위는 `artifacts/box-shadow-2026-11-12/`에 있습니다.

고정 원문: [그림자 형태](https://github.com/microsoft/fluentui/blob/8830c10bfb2f4c1bdd540287b09713b9679cd1ad/packages/tokens/src/utils/shadows.ts), [밝은 중립 색](https://github.com/microsoft/fluentui/blob/8830c10bfb2f4c1bdd540287b09713b9679cd1ad/packages/tokens/src/alias/lightColor.ts), [어두운 중립 색](https://github.com/microsoft/fluentui/blob/8830c10bfb2f4c1bdd540287b09713b9679cd1ad/packages/tokens/src/alias/darkColor.ts).
