# Codex Instructions

## 응답 스타일

- 모든 외부 응답은 `네. 마스터님({이해도}%)` 형식으로 시작한다.
- 이해도가 90%를 넘지 않으면, 이해도를 높이기 위한 선택형 질문을 제시한다.
- 사용자가 명시적으로 다른 언어를 요구하지 않는 한 한국어로 답한다.

## .github 지침 활용

- Codex는 작업 시작 시 이 파일을 우선 기준으로 삼고, 프로젝트 세부 정책은 `.github/copilot-instructions.md`를 함께 참고한다.
- `.github/copilot-instructions.md`의 상대 경로는 `.github/` 기준으로 해석한다.
- `.github/copilot-instructions.md`와 이 파일이 충돌하면 이 파일을 우선한다.
- Codex 환경에 없는 도구나 MCP 호출 강제 규칙은 적용하지 않는다. 예: `ask_user`, `fetch_webpage`, `fetch_url`.
- `.github/copilot-instructions.md`의 대화 호칭 규칙은 Codex에서는 이 파일의 `네. 마스터님({이해도}%)` 규칙으로 대체한다.

## Repo-local Skills

- `.github/skills/*/SKILL.md`는 Codex가 활용해야 하는 저장소 로컬 스킬 카탈로그다.
- 관련 작업이 감지되면 해당 `SKILL.md`를 먼저 읽고, 필요한 범위만 작업에 반영한다.
- 스킬 문서가 추가 참조 파일이나 하위 폴더를 가리킬 때는 필요한 파일만 선별해서 읽는다.
- `.github/skills/silk-dot-net-skill/Silk.NET/`는 참고 자료로만 사용하고, 필요한 특정 파일만 읽는다.
- 스킬 내용과 현재 시스템/개발자 지침이 충돌하면 시스템/개발자 지침을 우선한다.

### Skill Trigger Map

- .NET/C# 작업: `.github/skills/dotnet-latest/SKILL.md`
- 성능/메모리/핫패스 최적화: `.github/skills/dotnet-performance/SKILL.md`
- 코드 생성/구조 변경: `.github/skills/dev-skill/SKILL.md`
- Vulkan 구현: `.github/skills/vulkan/SKILL.md`
- Vulkan 셰이더 또는 벤더별 최적화: `.github/skills/vulkan-shader-vendor-optimization/SKILL.md`
- Windows 네이티브 API, Win32, DirectWrite, WIC, IMM, COM, P/Invoke: `.github/skills/windows-native-import/SKILL.md`
- Vulkan P/Invoke 및 Silk.NET 대체 구현 참고: `.github/skills/silk-dot-net-skill/SKILL.md`
- NativeAOT 배포/호환성: `.github/skills/nativeaot/SKILL.md`
- ImGui 호환 API/동작 참고: `.github/skills/imgui/SKILL.md`
- 새 스킬 조사/추가: `.github/skills/skill-acquisition/SKILL.md`

## Project Policy Summary

- 소스코드 변경 후에는 가능한 경우 빌드 또는 관련 검증 명령으로 확인한다.
- FBA 샘플을 로컬 소스 기준으로 실행할 때는 `./run-fba.ps1 samples/fba/<file>.cs -NoCache` 경로를 우선한다.
- 성능 최적화는 `docs/optimization-policy.ko.md` 기준으로 가설, 변경, 검증, Before/After, 개선율, 리스크를 기록한다.
- 문서/API/샘플 변경 시 `docs/duxel-agent-reference.md`와 `docs/duxel-agent-reference.ko.md`를 최신 상태로 유지한다.
- 한국어/영문 문서는 쌍으로 존재하는 경우 함께 갱신한다.
- 버전 히스토리에는 AI, Codex, Claude, agent가 작업했다는 뉘앙스나 도구 사용 사실을 기록하지 않는다. 변경된 제품 기능, 사용자 가치, API, 성능, 버그, 배포 사실만 기록한다.
