# 입자 접촉 판정 정밀도

2026-11-05.5000개·충돌 반복2회·MSAA4·60단계 및 허용 오차0.005를 유지한 공통 CPU/GPU 수정이다.

전체.NET10 검사44단계의3715번 입자에서 방향/각속도 차이가 발생했다. 정확한 입력을 보존하고 한 단계만 재생하여 동일 실패를 확인했다. 한 번의 충돌 반복 뒤 경계 인접 쌍의 penetration은 약0.0001707077, 접촉 tolerance는 약0.00017161811이었다. 부동소수점 sqrt와 뺄셈의 경계 판정 차이가 유한한 충돌 impulse로 증폭되는 원인으로 좁혔다. 실제 GPU sqrt 비트를 직접 관측한 것은 아니므로 해당 연산의 정확한 비트 차이를 확정하지 않는다. distanceSquared에만 precise를 추가한 후보는 실패해 채택하지 않았다.

양쪽 공통 판정은 `closing < 0 && contactMinimum > 0 && distanceSquared < contactMinimum * contactMinimum`, `contactMinimum = minimumDistance - contactTolerance`로 통일했다. 수학적으로 같은 접촉 경계이며 tolerance·힘·품질·부하를 낮추지 않는다. GPU 관련 중간값에는 precise를 적용했다. FP64나 새 Vulkan 기능을 요구하지 않으며 vulkan1.2/SPIR-V1.5를 유지한다. 원본 GLSL/SPV를 보관하고 임시 컴파일→spirv-val→역어셈블→검사→측정 후 일치하는 소스와 바이너리를 함께 반영했다.

이전 GPU 셰이더에서 실패하는2입자 회귀 검사를 추가했다. 제품 셰이더를 포함한.NET10 전체1035개와 고정RC3 NativeAOT 입자19개가 통과했다. Native 결과: `artifacts/particle-replay-2026-11-05/native-published-full/result.json`. 테스트 전용 셰이더 대체 없이 새 Native 실행 파일의 내장 모듈을 사용했다. 미게시 모듈을 거부하는 기존 artifact 검사는 그대로 유지한다.

같은 Native 실행 파일/입력,5000개·반복2·MSAA4·VSync off·워밍업4096/측정8192단계, 독립3회씩 AB/BA/AB. 제출 시간 중앙값0.1024→0.1026ms이며 p95/p99를 포함한 실행 범위가 겹친다. 속도 향상 주장은 하지 않는다. 숨긴 검증 surface의 제출 시간으로 전경 앱FPS를 의미하지 않는다. `benchmark-comparison.json`, `published-shader.json`, 각 원본 CSV를 참조한다. 로컬 NVIDIA 장치 검증이며 AMD·ARM·Pi 실측을 대신하지 않는다.

근거: [Khronos SPIR-V 환경](https://docs.vulkan.org/spec/latest/appendices/spirvenv.html), [GLSL 표준 명령](https://registry.khronos.org/SPIR-V/specs/1.0/GLSL.std.450.html), [GLSL4.60 precise](https://registry.khronos.org/OpenGL/specs/gl/GLSLangSpec.4.60.html).
