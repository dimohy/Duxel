# 공통 둥근 그라데이션

`UiDrawListBuilder.AddRectFilledGradient(rect, gradient, texture, clip, radius)`는 세 정지점의 선형 그라데이션을 사각형 또는 둥근 사각형 안에 그린다. `UiLinearGradient.Start/End`는 로컬 좌표이며 MiddlePosition은 0과 1 사이의 값이다. 끝점 밖은 끝 색상을 유지한다. 유한하지 않거나 길이가 0인 선, 잘못된 중간 위치·반경은 거부한다.

기존 32B 사각형 스트림의 두 슬롯으로 도형 하나를 표현한다. 첫 슬롯은 경계·반경·양 끝 색상, 두 번째는 선의 원점·방향·중간 위치·색상이다. LinearGradient 명령은 두 슬롯을 한 인스턴스로 해석한다. 일반 도형의 크기는 늘리지 않았고 홀수 슬롯 시작에도 빈 정렬 슬롯이 필요 없다. 정적·동적 업로드, 컴파일 레이어, 변환·불투명도를 기존 경로로 처리한다.

생성된 Fluent ProgressBar와 형식 지정 즉시 API의 불확정 상태가 이 공통 도형을 사용한다. 배경과 이동 색상을 하나의 색상장으로 그리고 기존 분석적 둥근 경계로 마스킹한다. 별도 배경과 두 메시 사각형 제출을 제거했으며 확정·모션 감소 동작은 유지한다.

| 동일한 256개 그라데이션 | 이전 메시 | 공통 도형 | 감소율 |
| --- | ---: | ---: | ---: |
| 실제 draw calls | 512 | 1 | 99.80% |
| 동적 기하 업로드 | 61,440B | 16,384B | 73.33% |

출력 픽셀은 같다. .NET10 전체954개·관리/고정RC3 GPU45개·입력62개 검사를 통과했다. Native는 별도 프로세스3회에서 비교 순서를 교대했다. MSAA1x/4x·홀수 슬롯·둥근 마스크·정적 컴파일·깊이 순서·불투명도·색상/중간 위치/이동/클립 변경과 획득 이미지 재사용을 검사했다. 변경이 없는 그라데이션은 draw/upload0이다. net8/9/10 Release 솔루션 빌드 경고0·오류0이다.

과거323이미지는400×300, 현재는320×240이므로 직접 비교를 동치로 세지 않았다. 보존한 원본 셰이더로 같은 크기의 기준을 다시 실행했고 기존323이미지가 새 셰이더와 정확히 같다. 새 그라데이션/Progress GPU 검사만 기준 실행에서 제외했다. 초기 테스트의 빈 명령 집계는 실제 그리기 명령만 세도록 수정했다.

원본 SPIR-V를 보존하고 후보 컴파일·spirv-val·디스어셈블·기능/기하 비용 검사를 거쳐 반영했다. Vulkan1.2/SPIR-V1.5 계약을 유지한다. Khronos validation layer를 지정한 Native 실행의 stderr는 비어 있다. 실제 GPU는 NVIDIA RTX5070 Ti Laptop이며 AMD 실기기·점유율·벤더별 시간은 미검증이다.

근거: `artifacts/rounded-gradient-2026-11-05/verification.json`. 이는 draw/upload 감소이며 전체 앱 FPS·프레임 분포·Private/작업 집합·Pi 성능 개선이 아니다. 추가 셰이더 보간 비용·기존 메모리 비용·전체RC3 물리 비교 실패·전체Fluent 적합성은 남아 있다.

명령: `dotnet run --project tests/Duxel.Rendering.Tests -c Release -- <결과 디렉터리>`. `tests/Duxel.Graphics.Tests`는 같은 GPU 본문을 관리/Native에서 사용하며 마지막 `packed-first` 인수로 비교 순서를 바꾼다. Rendering.Tests의 `--legacy-rendering-reference`는 원본 셰이더 비교용으로 새 그라데이션/Progress GPU 검사만 제외한다.
