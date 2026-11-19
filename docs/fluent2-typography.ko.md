# Fluent 2 명명된 타이포그래피

Last synced: 2026-11-05

Fluent의 명명된 텍스트 프리셋17종을 생성된 상속 위젯·팩터리에 연결했다. 글자 크기만 바꾸지 않고 실제 Regular400·Semibold600·Bold700 글꼴 파일을 선택하고, DirectWrite의 ascent/descent로 공식 행높이 안에 기준선을 배치한다. 기본 위젯과 사용자 위젯은 같은 `UiText`·표현·생성 경로를 사용한다. 이는 전체 Text API나 Fluent 전체 룩앤필의 완료가 아니다.

## 공식 기준과 연결

[Text 사용 지침](https://fluent2.microsoft.design/components/web/react/core/text/usage)과 microsoft/fluentui 커밋 `8830c10bfb2f4c1bdd540287b09713b9679cd1ad`의 TextPresetProps·tokens/global/typographyStyles.ts·fonts.ts를 대조했다(2026-11-05). 참조 원본과17개 대응표는 `artifacts/fluent-typography-2026-11-05/official-presets.json`에 있다. 명명된 프리셋은 안정 React Text 패키지의 API 선언이며 웹 문서의 Preview 제목을 패키지 채널로 해석하지 않는다. Windows/모바일 전체 합집합 검토는 계속 필요하다.

| 팩터리 | 크기 / 행높이 | 굵기 |
| --- | ---: | --- |
| Body1 / Body1Strong / Body1Stronger | 14 / 20 | 400 / 600 / 700 |
| Body2 | 16 / 22 | 400 |
| Caption1 / Caption1Strong / Caption1Stronger | 12 / 16 | 400 / 600 / 700 |
| Caption2 / Caption2Strong | 10 / 14 | 400 / 600 |
| Subtitle1 | 20 / 28 | 600 |
| Subtitle2 / Subtitle2Stronger | 16 / 22 | 600 / 700 |
| Title1 / Title2 / Title3 | 32 / 40, 28 / 36, 24 / 32 | 600 |
| LargeTitle / Display | 40 / 52, 68 / 92 | 600 |

각 `Fluent.<이름>(string)`과 `Fluent.<이름>(Func<string>)`는 소스 정책·글꼴 역할·크기/행높이 표현이 고정된 구체 위젯을 생성한다. 런타임 테마 레지스트리나 프리셋 이름 탐색은 없다. getter는 매 렌더 한 번 실행하며 `UiFluentTitle1` 등의 상속도 가능하다. 사용자 표현은 `UiControlContext.Typography<TTypeface, TMetrics>`를 조합할 수 있다. 숫자 토큰만 생성하는 대신 실제 글꼴 선택·측정·행 배치·그리기를 소비한다.

`DuxelFontOptions.Typography`로 실제 파일을 지정하며 `UiContext.SetTypographyFonts`는 직접 구성하는 호스트용이다. Windows 기본 파일은 Segoe UI의 segoeui.ttf·seguisb.ttf·segoeuib.ttf다. 보조 한국어 파일은 malgun.ttf·malgunbd.ttf이며 Semibold 역할에도 Malgun Bold를 사용하므로 한국어600 굵기의 시각 일치를 보장하지 않는다. `WindowsPlatformTextBackend.Instance`와 `IPlatformFontMetricsBackend.TryGetFontMetrics`는 창 생성 없이 실제 글꼴 메트릭을 제공한다. 기준은 [DWRITE_FONT_METRICS](https://learn.microsoft.com/en-us/windows/win32/api/dwrite/ns-dwrite-dwrite_font_metrics)의 디자인 단위다.

## 실행과 검증

기존 FBA에 `--typography` 모드를 추가했다. 새 샘플 전용 렌더러 없이17종 모두를 keyed child로 유지한다.

```powershell
./run-fba.ps1 samples/fba/widget_tree_theme_fba.cs -NoCache -NoLaunch
# 위 명령이 출력한 실행 파일 경로에 --typography 전달
& './artifacts/fluent-typography-2026-11-05/native-gallery-rc3/net11.0-windows7.0/widget_tree_theme_fba.exe' --typography
```

두 번째 명령은 보존된 고정 RC3 게시물을 연다. 첫 명령은 현재 선택 SDK로 로컬 소스를 게시하므로 두 실행 파일의 조건을 혼동하지 않는다.

.NET10 전체847·생성기66검사, net8/9/10 솔루션 경고0·오류0을 확인했다. 실제 OS/2 굵기400/600/700·글꼴별 다른 래스터 픽셀과 텍스처·17종 행높이·기준선·DPI·여러 줄·getter·주변 글꼴 복원·3D 벡터 기하·미지원 명시 실패를 검사한다. 직전323개 회귀 이미지는 모두 같다. 공식 브라우저 스크린샷과의 일치 검사는 아니다.

고정 SDK11.0.100-rc.3.26522.148·런타임11.0.0-rc.3.26522.148·win-x64 NativeAOT Speed 게시에서 실제 native code generation을 확인했다. 기본/타이포그래피 모드 모두 첫 GPU present와 exit0이다. 실행 파일 SHA256은 `5E473E7E844F1B7F6BD28612DBA34D65AD06C56545B29148818A388ADE3FEAF9`다. 화면 육안·입력·공식 시각 일치·성능 측정으로 확대하지 않는다.

## 제한과 남은 일

- 후속 [기본 Text](fluent2-text.ko.md)는 `Fluent.Text`·`Dux.Text`를 Body1에 연결했다. 비제네릭 즉시 Text의 기본 전환과 전체 Text 변형은 남아 있다.
- align·wrap·truncate·italic·underline·strikethrough·의미 정보·UIA와 공식 전체 변형/상태·시각 비교가 남아 있다. 표준 카탈로그22이름·알려진 요구289이름·누락267은 전체 지원률이 아니다.
- DirectText와 글꼴 메트릭 백엔드가 필요하다. atlas로 조용히 바꾸지 않는다. 래스터512물리 픽셀 초과는 명시적으로 거부하며 기존3D 벡터 경로와 품질은 유지한다.
- 메트릭 캐시는 최대64개 경로, 각2,048자까지 보관한다. 초과 요청은 캐시에 넣지 않고 실제 메트릭을 구한다. 기존 글꼴 face 자원의 프로세스 수명 문제까지 해결한 것은 아니다.
- 여러 줄의 부분 문자열 할당은 남아 있다. 이번 작업에서 FPS·CPU·Private·Pi 개선율을 측정하지 않았다. 이전 유지 ID 캐시의 추가 상주 비용과 RC3 전체 GPU 비교 실패도 미해결이다.

검증 기록: `artifacts/fluent-typography-2026-11-05/verification.json`, `image-comparison.json`, `native-gallery-run.json`, `source-hashes.json`. 전체 목표는 활성 상태다.

2026-11-12 후속 — [콘텐츠 측정 계약](widget-cpu-retention.ko.md): 결정론적 소스·표현은 부모 재사용에 참여하고 동적 문자열/배지 getter와 사용자 특성은 기본 재측정합니다. getter Progress의 선언 기하와 Spinner의 안정된 표시는 별도 약속이며 그리기·getter·애니메이션·이동 처리는 계속 실행합니다. 집중26개와 실제 GPU 비교는 이 범위의 근거이며 전체 NativeAOT·성능 완료가 아닙니다. 근거: artifacts/content-layout-retention-2026-11-12/verification.json.
