# FBA (Fast/Focused Benchmark Approach)

간단한 성능·할당 측정을 위한 최소 템플릿입니다.

## 사용 예시
- 워밍업 1회
- 반복 측정
- `Stopwatch`로 시간 측정
- `GC.GetAllocatedBytesForCurrentThread()`로 할당량 측정

템플릿: FastBench.cs

> 정밀한 벤치마크가 필요하면 별도 도구(BenchmarkDotNet 등)를 사용하되,
> 이 스킬은 “간단 테스트 목적”을 위해 FBA를 우선 사용합니다.
