# 플랫폼별 SVG 렌더링

2026-11-15: SVG 위젯·아이콘은 보관된 Vulkan GPU 장면과 가시 영역 카메라를 사용합니다. 아래 CPU 래스터·LRU 설명은 명시적 비트맵 출력 또는 이전 체크포인트에 해당합니다. [현재 구조와 남은 픽셀 검증](svg-gpu-camera.ko.md)을 확인하세요.

SVG XML은 원본 버전마다 한 번 읽고, 변경하지 않는 기하·채색·글자 윤곽선으로 컴파일합니다. 필요한 크기의 래스터는 기존 제한형 LRU에 저장하며, 캐시에 남은 크기로 돌아가면 같은 픽셀을 재사용합니다. `SvgStore`·`SvgImageDecoder`·`UiSvg`·`UiSvgIcon`의 공개 계약은 유지합니다.

| 플랫폼 | SVG 기하·채색 | 텍스트 | 내장 비트맵 | 추가 네이티브 SVG 배포 파일 |
| --- | --- | --- | --- | --- |
| Windows | 자체 C# 구현 | 시스템 DirectWrite | 시스템 WIC | Skia·HarfBuzz·외부 이미지 DLL 없음 |
| Linux | 자체 C# 구현 | HarfBuzz14.2.0 네이티브 자산 | 설치된 GdkPixbuf | HarfBuzz 사용; 기존 GdkPixbuf·GLib·GObject 요구 사항 유지 |

Windows는 DirectWrite의 메모리 로더로 명시한 폰트 바이트를 보관합니다. 컬렉션 인덱스와 실제 OpenType 스타일을 검증하고, 문자 체계별로 글자를 배치해 윤곽선을 기록합니다. 다른 설치 폰트를 임의로 찾거나 스타일을 합성하지 않습니다. Linux도 같은 폰트 목록·소유권 규칙을 사용하고 실제 처리를 HarfBuzz에 맡깁니다. `SvgFontStatistics`는 등록·폰트 요청·캐시된 글자 지원 검사를 구분하며, 래스터 재생과 확대는 이 작업을 반복하지 않습니다.

Windows 폰트 보관 경로는 Windows10 빌드14393 이상이 필요합니다. 시스템 API 요구 사항이며 별도 폰트 라이브러리를 배포하지 않습니다. 이번 검증은 macOS·ARM64를 보장하지 않으며, macOS 내장 비트맵 처리는 구현되지 않았습니다.

검증한 경로는 부드러운 곡선·타원호가 있는 경로, 기본 도형, 채움 규칙, 변환, 채움·획·끝·연결 모양, CSS, 선형·방사형 그라데이션, 로컬 use·symbol, 패턴, 기본 클립·휘도 마스크, 끝 마커, 내장 비트맵, 명시적 폰트·tspan·RTL·textPath·로컬 텍스트 참조입니다. 이후 점선·위상·양수 pathLength, 그라데이션·패턴 참조 상속과 단일 Gaussian blur를 추가했습니다. 그 밖의 필터·중간 마커·일부 단위와 고급 채색·효과·텍스트 속성은 남아 있습니다. SVG 사양 전체를 구현한 것은 아니며, 샘플 통과만으로 모든 SVG 문서를 보장하지 않습니다.

글자 윤곽선은 컴파일 후에도 벡터 기하로 유지합니다. 이전 백엔드의 힌팅된 글자 비트맵은 과거 근거로 보존하고 폰트 렌더러로 사용하지 않습니다. GPU 비교에는 독립적으로 확보한 네이티브 벡터 윤곽선을 사용하며, 기준 폰트의 SHA-256까지 확인합니다. 전체 픽셀의 채널별 허용 차이는 기존과 같은1입니다. 폰트 선택·기하와 렌더러의 안티앨리어싱 방식을 구분해 검사합니다.

근거는 `artifacts/svg-managed-2026-11-14/verification.json`·`legacy-oracle.json`과 `tests/Duxel.Graphics.Tests/Fixtures/SvgFontOutlineReference.svg`입니다. Windows SVG 계약114개, 실제 Vulkan GPU 검사 MSAA1/4의76개, Observer CPU288개와 소유한 실제 창7개, Windows 플랫폼18개와 Linux 플랫폼17개를 검사했습니다. Windows 검사는 Skia·HarfBuzz 네이티브 모듈이 로드되지 않는지 확인하며, 배포 파일도 별도로 점검합니다.

관리형 Windows Observer는 다음 경로에서 실행합니다.

```powershell
Start-Process -FilePath 'L:\Cache\Duxel\linux-accessibility-2026-11-14\observer-managed-build\bin\Duxel.Gallery\release\Duxel.Gallery.exe' -WorkingDirectory 'W:\MyWorks\Duxel' -WindowStyle Normal
```

SVG 탭의 텍스트·문서 기능까지 확인할 수 있습니다. .NET11.0.100 관리형 검증 빌드이며 고정 RC3 NativeAOT 비교 결과는 아닙니다. 일반 Observer 창을 자동 실행하지 않았습니다.

공식 근거: [DirectWrite 메모리 폰트](https://learn.microsoft.com/en-us/windows/win32/api/dwrite_3/nf-dwrite_3-idwriteinmemoryfontfileloader-createinmemoryfontfilereference), [DirectWrite 글자 배치](https://learn.microsoft.com/en-us/windows/win32/api/dwrite/nf-dwrite-idwritetextanalyzer-getglyphs), [HarfBuzz](https://harfbuzz.github.io/what-is-harfbuzz.html), [SVG 경로](https://www.w3.org/TR/SVG2/paths.html), [SVG 채색](https://www.w3.org/TR/SVG2/pservers.html).

참조 라이브러리의 콘텐츠를 모을 때는 호출 앱의 런타임 식별자가 전달되지 않으므로, 라이선스 고지문은 양쪽 플랫폼에 텍스트로 포함합니다. Windows에 네이티브 DLL이나 패키지 의존성을 추가하는 것은 아닙니다. HarfBuzz 네이티브 자산은 계속 Linux·macOS 대상에만 배포합니다.

위 검사 수치는 의존성 교체 당시의 기록입니다. 이후 브라우저와의 동일성 검사, 공통 Core RGBA8 변환, Vulkan 이미지 보간과 SVG 채색·획·필터 모듈은 [브라우저 비교 안내](svg-browser-validation.ko.md)를 따릅니다. 투명도는 premultiplied RGBA8로 저장한 뒤 직선 알파로 내보내며, 같은 픽셀은 다른 직선 알파 RGB 표현을 가질 수 있습니다. 전체 그림의 일치는 아직 미완료입니다.

