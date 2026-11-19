# DirectText 레이아웃과 지연 래스터화

## 유지된 텍스트의 정리 비용 — 2026-11-06

정적 레이어가512개보다 많은 텍스트를 보호할 때 제거 후보가 없어도 전체 크기의 정리 배열을 매 프레임 만들던 문제를 고쳤다. 실제 캐시 키가 모두 보호됨을 확인한 결과를 기억하고, 캐시 구성원이나 레이어 보호 참조가 바뀌면 무효화한다. 두 저장소의 개수가 같다는 이유만으로 생략하지 않는다. 제거할 때는 실제 후보 수만큼 배열을 만들고 정렬 비교기를 재사용한다. 기존 한도·만료·회수 순서와 소스 생성기의 위젯 연결은 유지한다.

고정 SDK11.0.100-rc.3.26522.148/runtime11.0.0-rc.3.26522.148,win-x64 NativeAOT Speed,Windows 실제 텍스트2048개,font14,320×240,정적 레이어,준비96프레임·측정1000프레임,독립3회씩 AB/BA/AB,강제GC 없음. 각 실행 결과의 중앙값은 다음과 같다.

| 지표 | 이전 | 이후 | 변화 |
| --- | ---: | ---: | ---: |
| 할당 B/Render | 98,732.768 | 256 | 99.74% 감소 |
| Render 중앙값 µs | 377 | 0.7 | 99.81% 감소 |
| Render p95 µs | 653.2 | 0.8 | 99.88% 감소 |
| Render p99 µs | 901.2 | 1.2 | 99.87% 감소 |
| 측정 후 Private B | 21,004,288 | 31,891,456 | 51.83% 증가 |

할당·시간의 실행별 범위는 겹치지 않는다. 측정은 `UiContext.Render`만 포함하며 `NewFrame`·`GetDrawData`·픽셀 소유권 해제는 밖에 있다. 전체 프레임 시간·FPS·GPU 시간으로 해석하지 않는다. Private 증가는 남은 관측이다. 기준은 각 세대29~30회,후보는0회 자연GC가 발생했다. 이 차이가 일부를 설명할 수 있으나 원인 확정이나 장기 메모리 안정성 증거가 아니다. 전체 앱 Private·작업 집합·GPU 상주량·업로드·draw 비교는 미완료다.

2048개 보호·콘텐츠 구성1회,새 비보호 키 제거·의존성 교체·레이어 제거 후512개 한도 복귀·602유휴 프레임 후 만료를 확인했다. 최종 Native GPU35검사는 기존594개 글꼴 지표 조합,600개 중첩 캐시 텍스처 재생·변경·부모/자식 제거,1만 버튼 MSAA4 다섯 장면의 바이트 동치를 포함한다. 프레임 슬롯3이며 데스크톱 캡처나 사용자 승인은 아니다.

근거:`artifacts/retained-text-maintenance-2026-11-06/verification.json`,`comparison.json`,`paired-summary.json`,소스 전후 사본. 원본·실행 파일:`L:/Cache/Duxel/retained-text-maintenance-2026-11-06/`. 최종 Native SHA256:`A47882ECA51BEDCCC84C63E1E5A8E40DBD7AD0985E8565508FF5A0E0AC4B29B4`. 명령:`Duxel.Graphics.Tests.exe <출력 폴더> retained-text-maintenance`,GPU 집중 검사는 `<출력 폴더> directtext-cache 3`. .NET11만 개발 검증하며.NET9/10은 전체 목표 완료 시 배포 컴파일만 한다.

2026-11-05. 공통 엔진의 집중 최적화 결과다. 전체 Fluent 2 구현이나 앱 전체 최적화 완료를 뜻하지 않는다.

`IPlatformTextLayoutBackend`는 `IPlatformTextBackend`의 선택적 확장이다. `TryLayout`은 같은 요청의 래스터 결과와 정확히 같은 크기·기준선·ascent·advance를 반환한다. `TextLayoutVersion`은 같은 요청의 결과가 바뀔 때 증가해야 한다. 불변 제공자는 0을 사용할 수 있다. Windows 구현은 같은 DirectWrite glyph-run 분석으로 bounds까지 계산하고, 실제 화면에 필요한 경우에만 alpha/RGBA를 생성한다. 기존 backend 계약은 유지된다.

공통 `UiWidget`은 scoped 위젯당 레이아웃 한 개를 보관한다. 기본 Fluent 버튼과 이를 상속한 사용자 버튼 모두 같은 경로를 이용한다. 텍스트·정확한 float 글자 크기·폰트 경로·backend 객체·버전을 비교한다. 텍스트는 최대256자, 경로를 포함한 문자열은 최대1024자이며 초과 시 이전 문자열을 해제한다. backend는 약한 참조이고 위젯 Dispose 시 저장소가 해제된다. 버전·backend 변경은 정적 레이어도 무효화한다. 동작·입력·포커스·배치·그리기 호출은 계속 실행된다.

클리핑 후 필요한 텍스트만 래스터화하고, 공통512개 캐시는 아직 업로드되지 않은 항목을 먼저 회수하여 표시 중 텍스처를 보호한다. 레이아웃/래스터 지표 불일치는 명시적으로 실패한다. 소스 생성기의 정적 위젯 호출 경로를 유지하며 런타임 테마 탐색을 추가하지 않는다.

## 같은 조건의 측정

고정 SDK11.0.100-rc.3.26522.148 / runtime11.0.0-rc.3.26522.148, win-x64 NativeAOT Speed, 실제 Segoe UI14, 서로 다른 라벨10000개,1280×720 CPU 구성. 같은 실행 파일과 폰트를 사용하여 독립 프로세스3회씩 AB/BA/AB 순서로 비교했다. 양쪽 모두 현재의 정확한 키와 텍스처 보호 회수 정책을 사용한다. 기존 릴리스 전체와 비교한 수치가 아니다.

| 지표 | eager | lazy |
| --- | ---: | ---: |
| CPU 중앙값 |79.0671ms|2.07605ms|
| CPU p95 |96.8242ms|2.113125ms|
| 프레임당 할당 |86,779,292.31B|664B|
| 측정 후 Private 중앙값 |101,425,152B|36,364,288B|
| 측정 후 Working Set 중앙값 |110,657,536B|45,838,336B|

CPU 중앙값97.37%, Private64.15% 감소. 세 실행 범위는 겹치지 않았다. 두 경로 모두 draw-data 명령462개이며 lazy의 측정 구간 layout/raster 호출은0이다. 이것은 CPU 구성 측정이며 앱 FPS·GPU 속도·장기 메모리·라즈베리파이 성능 수치가 아니다. 원본: `artifacts/directtext-layout-2026-11-05/comparison-versioned.json`, `benchmark-versioned-execution.json`, 각 CSV.

## 검증

.NET10 runtime10.0.9 전체1035개 통과. 텍스트 집중19개에는 실제 Windows 지표594조합, 라벨·폰트·제공자·버전 변경, 수명·약한 참조·클리핑·정적 레이어가 포함된다. GPU183개는 관리 실행 및 RC3 NativeAOT core/EXT×프레임 자원1/2/3에서 각각 통과했다. 같은 DPI에서 이전 실행 파일의157이미지와 현재 Native942이미지를 바이트 비교했다.10000개 버튼의 절반은 사용자 상속 버튼이며 idle/idle/scroll/부분 변경/전체 변경5단계에서 eager/lazy35쌍과 런타임 간60이미지가 일치한다. 실제 GPU viewport1600×900, scale1.25; CPU 벤치의1280×720과 구분한다. 이전96-DPI 이미지와 현재120-DPI 이미지를 섞지 않았다. 근거: `pixel-comparison.json`.

전체 검사에서 발견한 별도 입자 판정 문제는 [입자 접촉 정밀도](particle-contact-precision.ko.md)에서 설명한다. 생성·테마·Linux/ARM64·전체 앱 메모리와 WinUI 수준 체감은 별도 목표로 남는다.

DirectWrite 근거: [GetAlphaTextureBounds](https://learn.microsoft.com/en-us/windows/win32/api/dwrite/nf-dwrite-idwriteglyphrunanalysis-getalphatexturebounds), [CreateAlphaTexture](https://learn.microsoft.com/en-us/windows/win32/api/dwrite/nf-dwrite-idwriteglyphrunanalysis-createalphatexture).


최종 제품 Native 호스트에서도183개와157이미지 추가 대조 통과.120DPI 크기 변경 회귀는 실제 버튼 영역을 기준으로 클릭하여 관리/Native 각3×48변경 뒤 입력을 확인했다. 고정 좌표를 사용한 이전 검사의 실패와 수정 기록은 verification.json에 보존했다. 실제 WinUI 체감은 별도 사용자 확인 중이다.

## 현재 Fluent 폰트 계약의 재검증 (2026-11-06)

기존 CPU 측정기는 어댑터의 폰트 메트릭 인터페이스와 컨텍스트의 타이포그래피 폰트 설정이 없어 첫 프레임에서 멈췄다. 양쪽에 같은 regular/semibold/bold를 설정하고 메트릭을 전달하며 실제 컴파일된 Semibold14/행높이20을 결과에 기록하도록 수정했다. 이 복구를 위해 생산 엔진 코드를 바꾸지는 않았다.

고정 RC3 NativeAOT 실행 파일 하나로 eager/lazy, lazy/eager, eager/lazy 독립6프로세스를 실행했다. 현재 장면의 중앙값은 CPU78.9842→1.9952ms(-97.4739%), p95 100.52194→2.06032ms, p99 108.633964→2.089660ms, 할당88927336→664B/회, Private77451264→36249600B(-53.1969%), 작업 집합87511040→45772800B다. 실행 사이 범위는 겹치지 않았다. 워밍업0.5초·측정 최소1초/12표본이며 강제 GC는 없다. eager는 각13표본이므로 짧은 집중 검증이며 장시간 메모리·꼬리 지연 증거는 아니다. 원시 데이터·입력·해시: `artifacts/directtext-current-2026-11-06/summary.json`, `measurement-plan.json`.

최적화 경로는 측정 중 레이아웃/래스터/메트릭 백엔드 호출0회였고 참조 경로는 실행마다 래스터123344회였다. 두 경로의 DrawData 명령은462개다. 명령 수는 GPU draw calls가 아니고 CPU 구성 반복률도 앱 FPS가 아니다. 기본 Windows 백엔드는 이미 최적화된 레이아웃 기능을 제공하므로 현재 폰트 계약에서 기존 최적화를 다시 검증한 결과이며 새 생산 엔진 개선율이 아니다. 과거 다른 폰트 계약의 절대 수치와 비교하지 않는다. .NET10 입력 검증과 Native 수치 비교를 구분한다. GPU 업로드/draw·실제 고부하 앱 Private 증가·Pi 성능은 별도 측정이 필요하다.
