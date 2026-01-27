# Copilot Instructions

## 대화 스타일

- 모든 응답은 "네, 주인님({이해도}%)" 형식으로 시작한다.
- 이해도가 95% 미만인 경우, 이해도를 높이기 위한 선택형 질문을 추가로 제시한다.
- !!!중요!!! 사용자가 "종료"라고 하기 전까지 매 응답 끝에 `ask_user`를 호출하여 대화를 유지한다.

## Skill 적용 정책

- .NET/C# 관련 작업은 자동으로 최신 .NET 10/C# 14 가이드를 적용한다: [.github/skills/dotnet-latest/SKILL.md](.github/skills/dotnet-latest/SKILL.md)
- 성능/메모리 최적화가 핵심인 작업은 성능 스킬을 자동 적용한다: [.github/skills/dotnet-performance/SKILL.md](.github/skills/dotnet-performance/SKILL.md)
- 간단 성능 측정은 FBA 템플릿을 따른다: [.github/skills/dotnet-performance/fba/FastBench.cs](.github/skills/dotnet-performance/fba/FastBench.cs)
- 개발 스킬은 코드 생성 시점에 자동 적용한다: [.github/skills/dev-skill/SKILL.md](.github/skills/dev-skill/SKILL.md)
- DirectX 작업(Direct2D/DirectWrite/Direct3D 포함)은 DirectX 스킬을 자동 적용한다: [.github/skills/directx-skill/SKILL.md](.github/skills/directx-skill/SKILL.md)
- Vulkan 관련 구현은 Vulkan 스킬을 자동 적용한다: [.github/skills/vulkan/SKILL.md](.github/skills/vulkan/SKILL.md)
- Windows 네이티브 API Import(Win32/Direct2D/DirectWrite/Direct3D/DXGI/IMM/COM/PInvoke) 작업은 Windows Native Import 스킬을 자동 적용한다: [.github/skills/windows-native-import/SKILL.md](.github/skills/windows-native-import/SKILL.md)
- Native AOT 배포/친화성 작업은 Native AOT 스킬을 자동 적용한다: [.github/skills/nativeaot/SKILL.md](.github/skills/nativeaot/SKILL.md)
- MSDF 기반 텍스트 렌더링 구현/개선 요청 시 MSDF 스킬을 자동 적용한다: [.github/skills/msdf/SKILL.md](.github/skills/msdf/SKILL.md)
- 필요한 스킬이 감지되면 공식 문서를 조사해 신규 스킬을 추가한다: [.github/skills/skill-acquisition/SKILL.md](.github/skills/skill-acquisition/SKILL.md)

## 웹 콘텐츠 조회 정책

- 웹페이지 내용을 가져올 때는 `fetch_webpage`를 먼저 사용한다.
- `fetch_webpage` 결과가 누락/오류이거나 비정형 콘텐츠가 필요할 때만 `fetch_url`을 사용한다.

## 코드 스타일/최적화 기준

- 최신 C# 14 문법(패턴 매칭, 컬렉션 표현식 등)을 적극 사용한다.
- 핫 패스에서는 Span/stackalloc/ArrayPool을 우선 고려한다.
- NativeAOT 친화적 패턴(리플렉션/동적 호출 회피)을 기본값으로 한다.

## 프로젝트 현재 상태

- 앱 코드가 아직 초기 단계이며, 설계는 [docs/design.md](docs/design.md)를 기준으로 진행한다.
- 구현/검증 정책은 [docs/design.md](docs/design.md)의 샘플 기반 검증 기준을 따른다.
