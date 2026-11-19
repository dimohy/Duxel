# Windows 텍스트 조판

Windows 네이티브 조합은 포커스가 바뀌어도 시작한 입력 대상을 유지합니다. 다른 입력에는 조합 문자열을 노출하지 않으며 늦게 확정한 한 줄 입력의 커서를 포커스 복귀 시 복원합니다. [전달 계약·네이티브 메시지 검사·남은 프로토콜 작업](windows-ime-routing.ko.md).

일반 문자 입력·붙여넣기·IME 확정은 한 줄 삽입 완료 처리를 공유합니다. 선택 영역 교체와 길이 제한을 적용한 최종 문장에서 네이티브 뒤쪽 커서 경계를 보존합니다. 문자 이벤트 묶음은 글자소 경계를 순서대로 처리한 뒤 최종 네이티브 행을 준비합니다. [일반 입력 검증 기록](../artifacts/fluent-messagebar-2026-11-15/single-line-insertion-verification.json).

한 줄 IME 확정은 새 문장에서 확정한 글자의 네이티브 뒤쪽 경계를 유지합니다. 양방향 경계에서 기본 앞쪽 위치로 돌아가 커서·후보 창·접근성 좌표가 튀던 문제를 수정했습니다. 조합 중 드래그의 스크롤 범위도 조합을 포함한 전체 표시 문장의 폭을 사용합니다. [확정·포인터 검증 기록](../artifacts/fluent-messagebar-2026-11-15/ime-commit-verification.json)은 합성 IME 핸들러를 통한 실제 입력 위젯 검사와 OS IME 프로토콜 검증을 구분합니다.

OS 글자 배율을 DPI와 분리해 적용하며, 일반 문자열·Fluent 타이포그래피·스타일 문단의 래스터 원점은 `PixelSnap`에 따라 물리 픽셀에 맞춥니다. 소수 픽셀 스크롤에서 글자 가장자리가 바뀌는 현상을 줄이되 네이티브 글리프 사이의 상대 위치는 보존합니다. [시스템 글자 배율](system-typography.ko.md).

IME 조합은 확정된 원문을 바꾸지 않고 앞·조합·뒤를 하나의 표시 문자열로 조판합니다. 조합 끝의 네이티브 뒤쪽 경계·밑줄 조각·전체 폭·후보 창 커서·접근성 좌표가 이 행을 공유합니다. 같은 조합은 표시 문자열과 행 정보를 재사용하며, 확정은 한 번만 기록하고 조합 정보를 해제합니다. 합성 IME 핸들러를 사용하는 실제 입력 위젯에서 라틴·히브리어·아랍어·한글36개 검사와 독립 픽셀32쌍을 검증했습니다. 실제 OS IME 상호작용 자동화와 새 Linux 네이티브 실행은 별도입니다. [IME 근거](../artifacts/fluent-messagebar-2026-11-15/single-line-ime-verification.json).

Windows 래스터는 실제 글자 영역의 세로 경계와 그 경계에 상대적인 기준선을 반환합니다. 투명 상단 여백으로 토글 라벨이 낮아지던 회귀도 이 공통 경로에서 수정했습니다. [토글 정렬 근거](../artifacts/fluent-messagebar-2026-11-15/windows-switch-vertical-verification.json).

이번 Windows 검증은 한 줄 입력 집중19개·선택자4개·독립 입력 픽셀16쌍을 포함합니다. 관련 GPU680쌍, 리사이즈를 제외한 위젯4642개, 실제 옵저버44개 시나리오가 통과했습니다. 검사 범위는 서로 중복되며 제품 완료 기준과 합산하지 않습니다. [검증 근거](../artifacts/fluent-messagebar-2026-11-15/single-line-native-verification.json).

검증 도구의 `text-accessibility-only`는 접근성 검사 전에 실제 Windows 텍스트의 배치·래스터 메트릭 일치도 검사합니다. 글꼴·크기·유니코드·여러 줄의594개 조합을 전체 GPU 위젯 검사보다 먼저 확인할 수 있습니다.

래스터를 배치할 때 왼쪽 원점도 적용하며, 글리프 분석에는 원래 네이티브 기준선을 유지합니다. 아이콘만 별도로 보정하던 중복 경로는 제거했습니다. 숫자 입력이 확정 과정에서 문자열을 바꾸면 최종 그림·접근성 좌표도 새 문자열로 준비합니다. 변형 선택자는 [Unicode17 속성 목록](https://www.unicode.org/Public/17.0.0/ucd/PropList.txt)의 범위를 따라 앞 글꼴 구간에 남기고 원문을 보존합니다. 테스트의 이모지·한글은 글리프가 있는 보조 글꼴을 명시합니다. 기본 글꼴이나 누락 글리프 실패 정책은 변경하지 않습니다.

일반 문자열의 한 글꼴 경로도 여러 글꼴과 같은 네이티브 문단 조판을 사용합니다. 짧은 한 글꼴 문자열을 별도로 조판하던 래스터·벡터 분기를 제거해 커닝·결합 문자·원래 글리프 런의 소수점 원점을 같은 규칙으로 보존합니다. 이 변경의 수용 근거는 독립 시스템 조판과 실제 입력 그림의 비교이며, 검사와 전체 제품 완료는 구분합니다.

한 줄 입력은 변경되지 않는 네이티브 행 정보를 입력 버퍼 수명 동안 재사용합니다. 일반 입력의 클릭·커서·양방향 선택 조각·가로 스크롤·접근성 영역을 같은 좌표에 연결하고 IME 표시 문자열도 전체 문맥으로 구성합니다. 좌우 방향키와 길게 누르기는 화면 방향으로 이동하며 Shift는 원문 기준점을 보존합니다. Delete·Backspace·Ctrl+단어 이동은 원문 순서를 유지합니다. **입력·IME → 한 줄 커서 예제**에서 한글·결합 문자·히브리어 혼합 문장을 확인하세요. 실제 OS IME 자동화와 새 Linux 네이티브 실행 적합성은 별도 검증 범위입니다. 새 성능 수치는 주장하지 않습니다.

Last synced: 2026-11-16

[스타일 문단 API와 일반 문자열 UI](styled-paragraphs.ko.md)는 Windows·Linux에서 줄바꿈, 폰트·굵기 구간, 커서와 원래 래스터·벡터 글리프 그림에 하나의 네이티브 결과를 공유합니다. 타이포그래피 옵저버에서 예제를 확인하세요. Markdown·실제 자식 흐름·UI 기준 방향 연결은 남아 있습니다.

최신 폰트 구간 검증: 한글·라틴·히브리어 사이에서 폰트가 바뀌어도 양방향 문맥을 유지하도록 하나의 명시적 폰트 TextLayout에 구간을 적용합니다. 공통 문단 엔진이 단일 파일 폰트 컬렉션을 소유하며 시각적 절대 글리프 위치와 래스터용 원래 런의 순서·방향·소수점 기준선 원점을 함께 저장합니다. 원점을 글리프 오프셋으로 옮기면 힌팅 픽셀이 달라지므로 래스터 분석에는 원래 값을 전달합니다. 긴 문장도 네이티브 절대 위치를 보존하고 기존 오프셋 형식으로 한 번만 변환해, 실제로 발견한 부동소수점 정밀도 회귀를 고쳤습니다. 단일 폰트의 긴 문장도 같은 컬렉션 소유 모듈을 사용하며 사용하지 않는 별도 위치 어댑터는 삭제했습니다.

원래 네이티브 글리프 런을 직접 분석한 RGB 바이트·벡터 원점·원문 커서·실제 Core 소비·빈 문장을 포함한 집중16개가 통과했습니다. 기존 조판52개·위치29개·관련 CPU155개·스타일199개·통합416개·여러 줄 입력2560개·Markdown312개·GPU 이미지152쌍·SVG14개도 통과했습니다. 공통 입력·IME 예제에는 한글 폰트 구간을 가로지르는 RTL 임베딩 문장을 추가했습니다. 폰트 문맥 기반을 검증한 것이며 혼합 스타일 Core 문단·명시적 문단 방향·줄바꿈·시각적 키보드 이동 완료는 아닙니다. [검증 근거](../artifacts/fluent-messagebar-2026-11-15/paragraph-font-context-verification.json).

구간 계약은 [SetFontCollection](https://learn.microsoft.com/en-us/windows/win32/api/dwrite/nf-dwrite-idwritetextlayout-setfontcollection)·[SetFontSize](https://learn.microsoft.com/en-us/windows/win32/api/dwrite/nf-dwrite-idwritetextlayout-setfontsize)·Microsoft의 [공개 dwrite.h](https://github.com/microsoft/win32metadata/blob/main/generation/WinSDK/RecompiledIdlHeaders/um/dwrite.h)를 따릅니다. 반환한 관리형 배열은 COM 객체를 보관하지 않습니다. Windows에 Skia/HarfBuzz 의존성을 추가하지 않았으며 새 성능 향상은 측정하지 않았습니다.

최신 행 위치 확인(2026-11-16): Windows의 기존 커서 결함4개를 수정했습니다. 동일 스타일 행에서 완성된 네이티브 행의 위치를 실제 커서·클릭 경계·선택 조각·조합 밑줄·span 영역에 공유합니다. Windows 집중29개·조판52개·여러 줄 입력2560개·Markdown312개가 통과했습니다. Linux도 전용 Pango 폰트 맵으로 같은 위치 기능을 구현했으며 네이티브334개·기준114개·옵저버41개·전달38개가 통과했습니다. [Linux 지원 범위·의존성](linux-text-shaping.ko.md)을 확인하세요. 혼합 스타일·여러 글꼴의 전체 문단 구성과 명시적 기준 방향·줄바꿈 통합은 남아 있습니다. 아래 실패 기록은 수정 전 근거입니다.

문단 위치의 알려진 결함(2026-11-16): 완성된 행의 네이티브 커서와 일반·인라인 흐름을 대조한 12개 검사 중 4개가 실패했습니다(`AV` 커닝, 혼합 히브리어 각각 두 소비 경로). 캐시 재사용 6개와 기존 조판 52개는 통과했습니다. 문단·커서·선택의 정확성을 아직 보장하지 않습니다. [실패 근거와 다음 범위](../artifacts/fluent-messagebar-2026-11-15/paragraph-geometry-verification.json).

최신 확인(2026-11-16): 빈 조판 결과는 네이티브 GlyphRunAnalysis를 호출하지 않습니다. Core는 유효한 0 이동량을 보존하고 잘못된 이동량을 캐시하기 전에 거부하며 실패한 픽셀 소유권을 해제합니다. publish53에서 확장 52개 통과. [검증](../artifacts/fluent-messagebar-2026-11-15/zero-advance-verification.json). 아래 37개 기록은 이전 검증입니다.

Windows 일반 텍스트의 비트맵·벡터 경로와 SVG 텍스트는 공통 DirectWrite 모듈을 사용합니다. 프로젝트가 공통 소스를 포함하며 Core→SVG 의존성을 만들지 않습니다. Windows에 Skia·HarfBuzz 런타임 의존성을 추가하지 않습니다. Linux의 기존 HarfBuzz 경로는 유지합니다.

준비된 입력이16384 UTF16자를 넘으면 처음부터 네이티브 TextLayout 경로를 선택합니다. 이 값은 내부 경로 선택 기준이며 정확한 네이티브 길이 한계라는 주장은 아닙니다. 짧은 입력은 기존 스크립트·bidi 분석과 GetGlyphs/GetGlyphPlacements를 사용합니다. 실패를 잡아 다른 글꼴이나 명목 글리프로 바꾸지 않습니다.

긴 입력은 호출자가 소유한 글꼴 팩터리와 face를 빌려 전용 컬렉션을 만듭니다. 메모리 글꼴도 원래 팩터리를 유지합니다. 출력한 각 face의 파일 로더 정체성·파일 키·컬렉션 인덱스·합성 스타일을 확인해 시스템 글꼴의 임의 대체를 거부합니다. NoWrap의 DrawGlyphRun 결과에서 위치·이동량·원문 클러스터를 읽으며 문맥 클러스터를 임의로 나누지 않습니다. 내부 TextLayout 크기는 위젯의 배정 크기를 차지하거나 출력을 잘라내는 클립이 아닙니다.

소유한 layout·format·collection을 해제한 뒤 자체 컬렉션 로더만 등록 해제합니다. 호출자 팩터리와 face는 폐기하지 않습니다. 로더·렌더러의 소유 참조가0이 되는지 확인하며, 열거자는 보유한 파일 참조를 해제합니다. 장시간 네이티브 메모리 누수 측정은 별도 범위입니다.

현대 한글 자모에는 Unicode17 §3.12 결합 규칙을 글리프 입력에만 적용합니다. 원문 문자열은 보존하며 내부 UTF16 위치를 원문 위치에 연결합니다. LV+T도 지원하고 중간 결합 문자가 있으면 합치지 않습니다. 0 너비 문자는 반 em의 임의 너비를 얻지 않으며, 선택 글꼴에 없는 문자는 명시적으로 실패합니다. 원문 클러스터 보존과 편집기 전체의 커서·선택·네이티브 문자 영역 검증은 별개입니다.

## 검증과 재현

[최신 검증](../artifacts/fluent-messagebar-2026-11-15/long-shaping-verification.json): 확장 37개 모두 통과. 기존 실패6개와40000자 기대값을 유지했습니다. 경계 길이16383·16384·16385·32767·32768·32769,39000자 영문·44000자 아랍어·105000 UTF16자의 조합형 한글을 포함합니다. 한글 마지막 클러스터104997, 긴 oblique 합성 글꼴, 실제 SVG 메모리 글꼴과 동일 asset 재사용도 확인했습니다. 독립 시스템 TextLayout의 위치 기준11개(긴 입력3개)가0.002 DIP 허용 오차에서 일치합니다. 한글만 명시적 완성형 기준을 사용하며 다른 입력은 원문 그대로 대조합니다.

관련 CPU155개·통합 계약416개·GPU152쌍·SVG 글꼴14개·실제 Windows 옵저버38개가 통과했습니다. SVG GPU 검사는 기존 독립 ABC 윤곽선 픽셀을 MSAA1/4에서 대조합니다. Windows/SVG의 .NET8·9·10 빌드는 경고·오류0입니다. 전체 SVG 브라우저 픽셀 일치나 속도 개선율을 뜻하지 않습니다.

이 PC의 고정 검증 실행본으로 재현합니다. 새 결과 폴더 이름을 사용하세요.

```powershell
& 'L:/Cache/Duxel/fluent-messagebar-20261115/publish-48/Duxel.Graphics.Tests.exe' 'L:/Cache/Duxel/long-shaping-manual-1' 'windows-shaping-only'
& 'W:/MyWorks/Duxel/run-observer.ps1'
```

추가 가시성 검사에서 글리프가 없는 제어문자 구간의 클러스터를 읽는 예외를 교정했습니다. 40,000개의 0 너비 문자 뒤 A를 짧은 A와 32×32 전체 픽셀로 비교했고, 픽셀이 동일하며 알파가 비어 있지 않음을 확인했습니다.

초기26/27·30/33 실패, 가시성 검사 실패와 원래40000자 실패를 보존했습니다. 긴 영문 기준의 입력 정규화 불일치는 원문 기준으로 교정했으며 허용 오차를 완화하지 않았습니다. 메모리 글꼴의 팩터리 경계를 맞추고 경로 선택 경계도 확인했습니다.

여러 글꼴·스타일 span의 문단 전체 bidi·줄바꿈·편집·네이티브 문자 영역은 미완료입니다. MessageBar는12/13이며 전체 RTL 계약007이 남아 있습니다. Windows NativeAOT는 별도 도구 체인 관문입니다. Linux 전달 실행본c8246d는 보존했고 이번 변경의 Linux 실행·성능을 새로 주장하지 않습니다.

공식 근거: [글리프 배치](https://learn.microsoft.com/en-us/windows/win32/api/dwrite/nf-dwrite-idwritetextanalyzer-getglyphplacements), [전용 글꼴 컬렉션](https://learn.microsoft.com/en-us/windows/win32/directwrite/custom-font-collections), [원문 클러스터](https://learn.microsoft.com/en-us/windows/win32/api/dwrite/ns-dwrite-dwrite_glyph_run_description), [Unicode17 한글 결합](https://www.unicode.org/versions/Unicode17.0.0/core-spec/chapter-3/).
