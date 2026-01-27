---
name: "Native AOT"
description: "모든 코드를 NativeAOT 친화적으로 구성하고 배포를 표준화한다."
version: "1.0"
owner: "team"
---

# 목적
- Native AOT 배포를 안정적으로 수행한다.
- 리플렉션/동적 로딩/동적 코드 생성 문제를 사전에 차단한다.

# 적용 시점
- Native AOT 배포/실행 요청
- AOT 친화성 개선(트리밍, 리플렉션 제거) 요청

# 핵심 원칙
1. 리플렉션/동적 호출 최소화
2. DI/Serializer 등 리플렉션 의존 기능은 소스 생성기 사용
3. Trim 경고를 무시하지 않고 해결
4. P/Invoke 시그니처 명확화
5. 런타임 로드/플러그인 방식 회피

# 권장 설정
- PublishAot=true
- SelfContained=true
- StripSymbols=true
- RuntimeIdentifier 명시(win-x64 등)

# 진단 체크리스트
- AOT publish 성공 여부
- 실행 파일 크기/의존성 확인
- 트리밍 경고 발생 여부

# 참고 문서(공식)
- .NET Native AOT: https://learn.microsoft.com/dotnet/core/deploying/native-aot/
- Trimming: https://learn.microsoft.com/dotnet/core/deploying/trimming/

# 시각적 활성 표시
- 스킬이 실제로 적용되는 응답에는 눈에 띄는 표시를 포함한다.
- 예: "🟢 Skill Active: Native AOT" 같은 라벨을 응답 상단에 표기
