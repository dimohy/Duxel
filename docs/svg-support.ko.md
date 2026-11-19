# SVG 이미지와 전용 위젯 목표

현재 위젯 렌더링: [GPU 장면과 내부 카메라](svg-gpu-camera.ko.md). CPU Rasterize/Decode는 명시적 비트맵 출력이고 위젯 확대는 GPU 벡터를 유지합니다. 복잡 문서 픽셀 적합성은 진행 중입니다.

Last synced: 2026-11-14

현재는 자체 SVG 기하·래스터 처리와 Windows의 DirectWrite·WIC, Linux의 HarfBuzz·GdkPixbuf를 사용합니다. Skia·Svg.Skia 의존성을 제거했습니다. Windows SVG114개·GPU76개·Observer CPU288개/GPU7개와 플랫폼 검사 Windows18개/Linux17개가 통과했습니다. artifacts/svg-managed-2026-11-14/verification.json과 [플랫폼별 렌더링](svg-platform-rendering.ko.md)을 참고하세요. SVG 전체·네이티브·AOT 검증은 남아 있으며, 아래의 이전 검증 기록은 교체 전 백엔드에 관한 것입니다.

사용자는 기존 활성 목표에 두 경로를 추가했습니다. 일반 비트맵 파일을 읽는 자리에 SVG를 입력하고, SVG 기반 아이콘과 확대·축소·회전 전용 위젯 또는 이에 준하는 재사용 API를 제공합니다. 두 경로는 SVG 해석·자원 소유권을 공유하며 원래 엔진·위젯·컨테이너·통합·이식·Fluent 범위도 유지합니다. 권위 있는 범위는 artifacts/svg-support-2026-11-11/scope.json입니다.

선택형 Duxel.Svg 모듈에 공유 자원/래스터 계층을 구현했습니다. Core의 UiSvg는 확대·회전·이동·틴트를, UiSvgIcon은 메뉴/탭 어댑터를 제공합니다. 실제 Observer SVG 탭에서 두 이미지 경로와 아이콘을 확인합니다. 기존 내장 Fluent 아이콘은 TTF 글리프이며 일반2D에서는 래스터 텍스처를 캐시하고3D 레이어에서는 폰트 윤곽선 메시를 사용합니다. SVG는 유지된 장면을 필요한 해상도로 래스터화해 GPU 텍스처 사각형으로 그립니다. Fluent Image는 아직 카탈로그에 연결되지 않았습니다.

## SVG 아이콘과 실행

관찰 화면은 이동1px·회전1°·확대1%p 단위입니다. 포인터와 좌우 방향키가 같은 단위를 쓰며 UiSvg 자체 속성은 계속 유한한 실수를 받습니다. 이 컨트롤은 ShowStepMarkers를 false로 지정해 미세 조절에서도 막대가 연속적으로 보이게 합니다.

```csharp
using var icon = new UiSvgIcon(store.Load("regular.svg"), store.Load("filled.svg"),
    UiSvgIconColorMode.MultiplyForeground);
var option = new UiTabOption("home", "Home", Icon : icon.TabIcon);
var action = new UiMenuOption("open", "Open") { Icon = icon.MenuIcon };
```

기본 Authored는 원래 SVG 색을 보존합니다. MultiplyForeground는 원래 RGBA에 컨트롤 전경색을 명시적으로 곱하므로 흰색 SVG를 테마 단색 아이콘으로 쓸 수 있습니다. fill/currentColor를 교체하지 않습니다. 메뉴는 활성 상태 호버에만 filled를 선택하고 탭은 호버와 독립적인 선택 상태로 filled를 고릅니다. 페인터는 배치를 진행하지 않으며 위젯의 텍스처/캐시 경로를 공유하고 소스 변경 시 이벤트 대기 렌더링을 깨웁니다. 소비 컨트롤을 해제한 뒤 아이콘을, 그다음 저장소를 Dispose하세요.

저장소 루트에서 `./run-gallery.ps1`을 실행하고 SVG를 선택하세요. 일반 이미지와 전용 위젯을 비교하고 확대·회전·이동, 초기화·재로드와 아이콘 메뉴/탭을 조작합니다. 원본 파일은 samples/Duxel.Gallery/Assets/Svg이며 실행 시 출력 복사본을 읽습니다. 자세한 경로는 한영 샘플 README에 있습니다. 이번 검증에서 일반 앱 미리보기는 실행하지 않았으며 실제 페이지는 소유 GPU 검사 화면으로 렌더링했습니다.

이전 관찰/아이콘 단계의 근거는 자원/위젯/이미지54개, 아이콘11개와 실제 포인터 슬라이더/초기화/재로드/메뉴를 포함한 관찰113개, MSAA1/4 실제 GPU32개와 관찰 본문 재사용3개, 기존 메뉴376/탭197/스케줄링35개 및 메뉴 GPU216개입니다. SVG 검사·Graphics 검사·실제 Observer 앱의 최종 세 빌드는 경고·오류0개입니다. artifacts/svg-support-2026-11-11/observer-verification.json을 참고하세요. 이 기록은 아래의 명시적 글꼴 지원 전 단계이며 전체 기능·기기 수명/배포/NativeAOT·성능 측정은 남아 있습니다.

GPU 검사에서 이미지 계약 세 가지를 고쳤습니다. Image/ImageWithBg/ImageButton은 전체 UV를 사용하고 일반 이미지 효과는 원래 알파와 이전 프레임 픽셀을 유지합니다. UiTextureUpdate.IsColorImage는 SVG/런타임 이미지 ID를 폰트 커버리지 마스크로 분류하지 않도록 명시합니다. UiImageTexture와 SVG가 자동으로 지정합니다. 기본 비트맵 준비는 불변 원본 메모리를 재사용하고 RGB 효과 변경만 별도 스냅샷을 만듭니다. 기존 폰트 커버리지 동작은 실제 메뉴 글리프 회귀로 확인합니다.

## 문서 기능과 관찰 예제

`./run-gallery.ps1` → SVG → **문서 기능**을 선택하세요. 실제 탭 본문에서 반복 패턴·방사형 그라디언트·use로 재사용하는 currentColor 심볼·구멍을 낸 마스크·끝점 화살표 마커를 확인합니다. **이미지·텍스트**로 돌아가면 일반 이미지와 전용 위젯을 비교합니다. 두 본문은 자원을 유지하고1px·1°·1% 조절기를 공유하며, 탭 전환은 프레임을 요청하지만 애니메이션 타이머를 만들지 않습니다. 원본 자원5개는 샘플의 Assets/Svg에 있습니다.

집중 CPU 픽셀 검사는 기존 xlink use와 내장 PNG의 직선 알파도 확인합니다. 렌더러가 만든 결과를 기대값으로 복사하지 않고 알려진 안쪽·바깥쪽 픽셀과 선언된 반복 주기를 검사합니다. MSAA1/4 GPU 검사는 심볼 뷰포트·currentColor·반복 패턴의 투명도·마스크 기하·획득 이미지 재사용을 확인합니다. 명시된 사례의 근거이며 SVG 사양 전체 준수는 아닙니다.

CSS 자원 검사는 부분 문자열 대신 토큰을 구분합니다. 주석·따옴표로 감싼 속성 문자열에 설명용 `url(...)`·`@import`·`@font-face`가 있어도 실제 자원으로 오인하지 않습니다. 이스케이프한 이름을 포함한 실제 URL·규칙은 기존 로컬 참조·내장 래스터 정책을 그대로 적용합니다. 잘못된 URL·문자열·주석·이스케이프는 명시적으로 실패합니다. 이 검사는 자원 참조를 검사하며 CSS 렌더러를 대체하거나 CSS 전체 지원을 증명하지 않습니다.

문서 기능 단계의 근거는 CPU102개·관찰123개·SVG GPU52개·실제 관찰 GPU7개입니다. 실제 포인터로 기능 탭을 선택하고 화면 재사용도 확인합니다. artifacts/svg-support-2026-11-11/features-verification.json을 참고하세요. 이전 글꼴·수명 결과는 과거 단계로 보존합니다. 전체 기능·문자 체계와 네이티브·배포·AOT 검증은 남아 있습니다.

## 명시적 SVG 글꼴과 유지되는 텍스트

### 곡선 위에서 참조하는 문자

SVG1.1 tref를 [W3C 텍스트 사양](https://www.w3.org/TR/SVG11/text.html#TRefElement)의 등가 span 문자 데이터로 한 번 컴파일합니다. 고정 백엔드가 빈 화면으로 출력하던 textPath 안의 tref도 포함합니다. 이미 해석한 XML DOM과 백엔드의 타입 지정 복제 API를 사용해 원래 요소 이름·ID·클래스·스타일·위치 속성을 보존합니다. XML 추가 해석·다른 렌더러·프레임마다의 참조 조회를 도입하지 않습니다.

href와 xlink:href는 해결된 로컬 참조를 받습니다. 대상 마크업에서는 문자를 가져오고 표시 속성은 참조하는 요소에서 정합니다. 엔티티를 해석한 문자와 앞뒤 텍스트의 순서도 유지합니다. 원본 Reload는 바뀐 참조 내용을 새 장면으로 확정합니다. SVG → 이미지·텍스트의 곡선 문장은 이제 text.svg의 재사용 텍스트 정의를 읽습니다.

집중 검사는 일반·중첩·CSS 스타일/위치·곡선 참조를 직접 작성한 문자/span과 비교하며 서로 다른 tref/tspan 선택자도 확인합니다. 비트맵 디코딩·100회 재사용·해상도 확대·대상 Reload는 같은 자원 경로를 사용합니다. 현재 CPU112개·관찰123개·SVG GPU52개·관찰 GPU7개가 통과했습니다. 실제 참조 샘플 본문은 이전의 직접 작성 샘플과 픽셀이 같고 시간에 따라 달라지는 상단 FPS 문구만 다릅니다. 근거는 artifacts/svg-support-2026-11-11/tref-verification.json입니다. 전체 SVG/CSS·문자 체계·네이티브·AOT 검증은 남아 있습니다.

저장소를 만들 때 글꼴 파일을 등록합니다. 아래 이름은 앱에서 정한 별칭이며 스타일·두께는 실제 글꼴에서 읽습니다. 문서에서 굵게·기울임을 요청한다면 해당 스타일 파일도 등록하세요.

```csharp
using var store = new SvgStore(fonts : new SvgFontOptions("UI",
[
    new SvgFontFace("UI", "Assets/Fonts/ui-regular.ttf"),
    new SvgFontFace("UI", "Assets/Fonts/ui-bold.ttf"),
    new SvgFontFace("Korean", "Assets/Fonts/korean.ttf")
]));
var document = store.Load("label.svg");
using var label = new UiSvg(document) { Zoom = 2, RotationDegrees = 15 };
var fonts = store.FontStatistics;
```

SVG 또는 CSS에서 `font-family="UI, Korean"`처럼 지정합니다. 별도 지정이 없으면 루트의 DefaultFamily를 상속합니다. 등록한 글꼴 목록 순서대로 글자 묶음의 표현 가능 여부를 확인하며 없는 글꼴·스타일·글리프는 장면 게시 전에 오류로 알립니다. 설치된 다른 글꼴로 자동 교체하거나 스타일을 합성하지 않습니다. `text`·위치와 스타일을 지정한 `tspan`·`textPath`는 백엔드의 텍스트 조형 경로를 사용합니다. Latin/한글 혼합 CSS 문장은 글꼴별 tspan 원본과 픽셀을 비교합니다. 아랍어 RTL 검사는 출력 확인이며 전체 양방향·문자 체계 준수 검증은 아닙니다.

글꼴 파일 바이트는 저장소 생성 시 한 번 읽어 보관하고 같은 파일·컬렉션 면은 별칭 사이에 공유합니다. CollectionIndex 기본값은0, MaximumFontBytes는 파일당32MiB입니다. 원본 글꼴 파일이 바뀌어도 기존 장면은 바뀌지 않습니다. 글꼴을 교체하려면 새 저장소를 만들고 기존 소비자보다 기존 저장소를 나중에 해제하세요. DTD·스크립트·외부 글꼴 URL·문서 내장 SVG 글꼴·`@font-face`는 명시적으로 거부합니다. SvgFontOptions 없이 텍스트를 읽는 경우도 거부합니다.

텍스트도 SVG 버전당 한 번 장면으로 컴파일합니다. 재사용·해상도 변경에서는 XML 해석·등록 글꼴 결정·글리프 포함 여부 확인을 반복하지 않습니다. 네이티브 그리기에서 글리프를 래스터화할 수 있으므로 FontStatistics를 네이티브 텍스트 작업 전체로 해석하지 마세요. LoadedFaces는 중복 없는 파일·컬렉션 면 수, TypefaceRequests는 공급자 호출 수, GlyphChecks는 캐시되지 않은 글리프 확인 수입니다. FPS나 전체 메모리 지표가 아닙니다.

Observer → SVG의 text.svg는 보통·굵은 Latin, 한글, 곡선 텍스트를 보여 줍니다. 같은1px·1°·1% 컨트롤로 이미지와 텍스트 위젯을 함께 변환합니다. 원본 자원과 글꼴 지표를 표시하며 하단 설명은 자식 페이지를 스크롤해 확인할 수 있습니다. Windows 샘플은 OS Fonts 폴더의 Segoe UI 보통·굵게와 맑은 고딕을 명시적으로 등록하며 파일을 배포하지 않습니다. 이식 가능한 앱은 사용 권한이 있는 자체 글꼴 경로를 지정하세요.

이전 글꼴 단계의 근거는 자원·위젯·이미지·글꼴74개, 관찰120개, MSAA1/4 SVG GPU44개와 실제 관찰 본문 재사용3개입니다. 글꼴 GPU 검사는 같은 명시적 글꼴을 쓴 표준 SVG 엔진과 뷰포트 전체를 RGB 오차1 이내로 비교합니다. 통합 검사이며 독립적인 SVG 준수 시험은 아닙니다. artifacts/svg-support-2026-11-11/font-verification.json을 참고하세요. 전체 텍스트·문자 체계, 가변 글꼴, 네이티브 메모리·수명, 패키징·NativeAOT는 미검증입니다.

## 유지되는 전용 위젯

### 해제와 오래 보관한 자원 참조

저장소를 해제하면 유지한 네이티브 그림과 등록 글꼴·타입페이스 핸들을 닫습니다. 과거에 빌려 간 SvgAsset은 사용을 거부하며 해제된 백엔드 소유자와 렌더 캐시를 계속 붙잡지 않습니다. 재로드는 새 장면을 확정한 뒤 이전 그림을 닫고, 실패한 재로드는 확정된 그림과 글꼴 핸들을 유지합니다. 이미 게시한 래스터 스냅샷은 불변 픽셀 버퍼로 계속 사용할 수 있습니다.

수명 회귀 검사는 과거 자원·장면 참조를 의도적으로 남겨 네이티브 핸들 해제와 백엔드 소유자의 GC 도달 가능 여부를 확인합니다. 텍스트32회 갱신·갱신 실패·반복 해제도 검사합니다. 검사한 소유권 경계의 근거이며 실제 부하의 메모리 절감량이나 모든 네이티브 할당·기기 종료를 인증하지 않습니다. 근거는 artifacts/svg-support-2026-11-11/lifetime-verification.json입니다. 수명 단계의 집중 CPU 검사는81개이며 이전 글꼴74개는 과거 기록으로 보존합니다.

```csharp
using var view = new UiSvg(store.Load("icon.svg"))
{
    Size = new(240, 180),
    Zoom = 2,
    RotationDegrees = 45,
    Offset = new(8, 0)
};
// Inside UiScreen.Render:
view.Render(ui);
var uploads = ui.SvgTextureStatistics;
```

Size는 논리 뷰포트이며 기본값은 SVG 고유 크기입니다. 부모 제약은 측정과 실제 배치에 적용됩니다. Zoom은 뷰포트 중심을 기준으로 표시 크기를 바꾸며 뷰포트로 잘립니다. 양수 회전은 시계 방향입니다. Offset은 이미지를 이동하고 RasterScale은 배치를 유지하며 해상도만 바꿉니다. 픽셀 해상도는 크기 × Zoom × RasterScale × ContentScale을 올림합니다. Tint는 원래 RGBA에 곱하며 흰색은 색을 보존합니다. currentColor를 교체하지 않습니다.

위젯은 문서를 빌려 쓰고 명시적 재로드 알림을 구독합니다. 저장소보다 위젯을 먼저 Dispose하세요. 컨텍스트별 임대가 문서 객체/버전/픽셀 크기당 텍스처 하나를 공유하고 살아 있는 의존성을 유지합니다. 회전·이동·틴트·변경 없는 프레임에서는 래스터/업로드를 추가 요청하지 않습니다. 남아 있는 크기로 돌아오면 텍스처도 재사용합니다. 서로 다른 컨텍스트는 GPU 식별자를 따로 소유하고 CPU 장면/래스터를 공유합니다. 주기 타이머나 애니메이션 요청은 없습니다.

컨텍스트 캐시는 최대 128개, RGBA 텍스처 크기 합계 64MiB를 유지합니다. SVG 위젯만 보유하고 현재 프레임이나 재사용 레이어에서 쓰지 않는 이전 텍스처는 새 텍스처를 준비한 뒤 교체할 수 있습니다. 이미 발행한 프레임은 이전 텍스처 식별자를 그대로 사용합니다. 다른 위젯이나 레이어가 쓰는 텍스처는 한도에 계속 포함됩니다. 위젯의 Dispose는 임대를 해제합니다. 이미 제출한 GPU 작업이 남으면 렌더러가 완료를 기다린 뒤 실제 텍스처를 해제합니다. 재로드와 렌더링은 UI 스레드에서 순서대로 실행해야 합니다.

`svg-replacement-pressure`는 현재 SVG 텍스처와 제출한 프레임에서 아직 쓰는 이전 텍스처의 Vulkan 할당 크기를 기록합니다. 컨텍스트 캐시의 RGBA 용량과 별도로 집계하며, 프레임 슬롯이 1개와 3개일 때 각각 해제를 확인합니다.

사용 중인 텍스처가 한도를 넘으면 요청한 해상도를 유지한 채 실패를 반환합니다. 쓰는 위젯이 없는 텍스처는 캐시에 여유가 있는 동안 남을 수 있습니다. `SvgTextureStatistics`에서는 캐시에 등록된 텍스처 크기와 등록한 렌더 요청 수를 확인할 수 있습니다.

이전48개 검사 기록은 artifacts/svg-support-2026-11-11/widget-verification.json에 보존합니다. 현재54개는 위젯 Dispose 뒤의 유지 레이어 텍스처 보호와 일반 이미지 알파/불변 스냅샷/색 메타데이터 병합도 확인합니다. 캐시 부족 시 유지 레이어 명령이 참조하는 텍스처를 삭제하지 않으며 레이어를 제거하면 삭제할 수 있습니다. 수동 버전 레이어는 시각 입력이 바뀔 때 호출자가 ContentVersion을 갱신해야 합니다.

## 한 번 해석하고 재사용하기

```csharp
using Duxel.Core;
using Duxel.Svg;
using Duxel.Platform.Windows;

using var store = new SvgStore();
var decoder = new SvgImageDecoder(store, WindowsUiImageDecoder.Shared);
UiImageTexture.ImageDecoder = decoder;
var bitmap = UiImageTexture.LoadFromFile("icon.svg", new UiTextureId(101));
var svg = store.Load("icon.svg"); // Same resource; no second file read or XML parse.
var normal = svg.Rasterize(64, 48);
var enlarged = svg.Rasterize(256, 192);
var same = svg.Rasterize(64, 48); // Same immutable cached raster as normal.
```

현재 소스에서는 src/Duxel.Svg/Duxel.Svg.csproj를 참조합니다. Core는 Skia에 의존하지 않습니다. 앱 시작 시에도 같은 일회성 등록을 사용하도록 ImageDecoder 옵션에 이 디코더를 전달하세요. 저장소가 장면을 소유하므로 모든 소비자보다 오래 유지해야 합니다. IUiSvgDocument·UiSvgRaster는 Core 계약이며 프레임은 변경하지 않는다는 계약의 픽셀 메모리를 빌립니다. 비트맵 디코더는 복사본을 반환해 호출자가 캐시를 바꾸지 못하게 합니다.

Load는 경로를 정규화하고 최초 사용 이후 메모리에서 조회합니다. 원본 XML 바이트를 한 번 DOM으로 해석하고 백엔드는 그 DOM을 한 번 순회해 장면을 컴파일합니다. Rasterize는 장면을 그리며 XML을 읽지 않습니다. 자원 버전·픽셀 크기를 키로 하는 제한된 LRU에 결과를 보관합니다. 새 크기는 XML 없이 래스터화하며 캐시에 남은 크기로 돌아가면 같은 결과를 사용합니다. 위젯의 회전·위치는 XML/래스터 키 대신 기하 변환으로 처리합니다.

원본이 바뀌면 Reload를 명시적으로 호출하며 매 프레임 파일을 검사하지 않습니다. 같은 바이트면 장면·버전·래스터 정체성을 유지합니다. 다른 바이트는 새 장면을 완성한 뒤 해당 자원만 갱신하고 게시 후 Changed를 알립니다. 갱신이 실패하면 오류를 보고하고 이전 확정 자원을 보존합니다. Dispose는 소유한 장면/캐시를 해제하고 과거 소비자의 접근을 거부합니다. 기본 보관 래스터 예산은64MiB, 원본 상한은16MiB이며 명시적으로 바꿀 수 있습니다. 초과 요청은 임의 축소하지 않고 실패합니다.

Statistics는 SourceReads·XmlParses·SceneCompiles·Rasterizations·RasterCacheHits·CachedRasterBytes를 구분합니다. 마지막 값은 보관한 CPU 래스터 메모리이며 전체 네이티브/GPU 메모리가 아닙니다. 같은 크기100회 재사용에서 읽기/해석/컴파일/래스터 각1회, 동시800회 요청에서도 각각1회·캐시 적중799회를 기록합니다. 직선 알파·비율 유지·그라디언트/클립·viewBox 전용 아이콘·갱신·LRU 픽셀 동등성도 확인합니다. 실제 처리 횟수 계약이며 처리량/FPS 개선율을 뜻하지 않습니다.

현재 입력 정책은 DTD·스크립트·외부 자원·없는 참조를 금지하고 깨진 이미지 대체물 대신 오류를 반환합니다. 텍스트는 위의 명시적 글꼴 등록이 필요합니다. SVG 전체 기능·전체 문자 체계·네이티브 배포/AOT·전체 네이티브/기기 수명 검증은 남아 있습니다. 자원 모듈을 SVG 전체 준수로 해석하지 마세요.

집중 검증은 .NET11과 samples/Duxel.Gallery/LocalSource.targets로 빌드·실행하며 artifacts/svg-support-2026-11-11/product-verification.json을 따릅니다. 프로젝트는 tests/Duxel.Svg.Tests/Duxel.Svg.Tests.csproj이고 실행 파일에 소유한 결과 디렉터리를 전달합니다. API/캐시/결과 스냅샷은 이전 후보5검사와 별도입니다.

백엔드 계약은 SVG 기능 범위·정확한 오류·소유권·표시 배율의 품질·변화 없는 프레임 재사용·배포 자산·NativeAOT를 고려합니다. Direct2D는 일부 기능을 지원하고 미지원 요소/속성을 조용히 무시하므로 검사 없는 전달 방식은 적합하지 않습니다. 교체 전 자원 모듈은 Svg.Skia5.2.3을 사용했습니다. 현재 자체 백엔드와 플랫폼별 시스템 처리는 svg-platform-rendering.ko.md를 참고하세요. 문서와 분석기/JIT 통과는 앱 수준 NativeAOT의 증거가 아니며 전체 기능과 남은 경로의 검증을 계속해야 합니다.

검증에는 일반 SVG 파일 입력·본래 크기/viewBox/투명도·그라디언트와 클립 같은 표현·원본/자원 재사용·확대와 회전·위젯 배치/클립/수명·이벤트 대기·실제 관찰 예제·한영 API/실행 안내가 필요합니다. 백엔드 계약을 확정하기 전에는 기능/검증 분모를 추정하지 않습니다. 고정 NativeAOT/런타임 이미지 비교는 원래 목표에 계속 포함하며 MSVC/Windows SDK 경로 제공 전 탐색·설치를 하지 않습니다. 리사이즈는 계속 보류합니다.

UiSvg.CornerRadius는 고정된 둥근 뷰포트를 적용하며 회전·이동은 같은 래스터를 재사용합니다. 맞춤·원본 바깥 투명 UV·관찰 조절기·검증 한계는 [둥근 이미지 계약](image-widgets.ko.md#둥근-뷰포트와-이미지-맞춤)을 참고하세요.

복잡한 실제 원본과 브라우저의 직접 SVG·이미지 출력을 비교하는 절차는 [브라우저 비교](svg-browser-validation.ko.md)를 따릅니다. Observer의 SVG → 브라우저 비교에서 기준과 Duxel 결과를 나란히 확인합니다. Core·SVG·Vulkan은 공통 RGBA8 변환을 사용하며, 색 이미지는 premultiplied 상태로 보간합니다. 원본 XML은 계속 자원 버전마다 한 번만 처리합니다. 전체 픽셀 일치는 아직 미완료입니다.

일반 경로의4개 GPU 샘플·Wang 균일 곡선 분할과 부드러운 기본 도형의 coverage를 구분합니다. `visibility`의 숨김 상속·자식 재표시·글자 배치 유지도 지원합니다. 실제 캡처 조건과 독립 픽셀 예제, 남은 타원·복잡한 원본 차이는 위 브라우저 비교 문서에서 확인합니다.
