---
name: "최신 .NET, C# 메모리·성능 최적화"
description: ".NET 10, C# 14 기준으로 Native AOT 친화적이며 최소 할당/최대 성능 코드를 작성한다. Span/ref struct/stackalloc 등 스택 중심 패턴을 적극 사용하고, 간단 테스트는 FBA(간단한 내장 벤치마크 접근)로 수행한다."
version: "1.0"
owner: "team"
---

# 목적
- .NET 10, C# 14 기준으로 **빠른 코드**와 **최소 메모리** 사용을 목표로 한다.
- Native AOT 친화적 코드를 기본값으로 한다.
- 가비지 컬렉션(GC) 사용을 최소화한다.
- 스택 메모리 활용(Span, ref struct, stackalloc)을 적극 우선한다.
- 간단 테스트는 **FBA(빠른/간단 내장 벤치마크 접근)** 를 우선 사용한다.

# 적용 시점
다음 요청에 자동 적용:
- “속도가 중요한 코딩이 필요할 때”
- “성능/메모리 최적화”, “GC 줄이기”, “할당 최소화”
- “Span”, “stackalloc”, “ref struct”, “Native AOT”
- “빠른 루프”, “저지연”, “고성능 파서”

# 핵심 원칙 (요약)
1. **할당 최소화**: 힙 할당/박싱/클로저/캡처 최소화.
2. **스택 우선**: Span/ReadOnlySpan, ref struct, stackalloc 활용.
3. **AOT 친화적**: 리플렉션/동적 코드 생성/런타임 코드 컴파일 회피.
4. **형식 설계**: 값 형식, `readonly struct`, `ref struct` 사용.
5. **JIT 힌트**: `AggressiveInlining`, 범위 체크 제거 가능 구조.
6. **풀링**: `ArrayPool<T>`, `MemoryPool<T>` 사용.
7. **파이프라인**: 파서/핫 루프는 분기 예측과 캐시 친화성 고려.
8. **측정**: FBA 기준으로 GC/할당량/시간 측정.

# 출력 요구사항
- 코드 예시는 .NET 10, C# 14 기준으로 작성.
- 불필요한 할당, LINQ, string concatenation, boxing을 피한다.
- `Span<T>`, `ReadOnlySpan<T>`, `ref struct`, `stackalloc` 우선.
- Native AOT에 불리한 API는 대체 제안 포함.

# 권장 패턴
- `ReadOnlySpan<char>` 기반 파싱
- `stackalloc`로 임시 버퍼
- `TryParse` 스타일로 예외 비용 제거
- `ref readonly`로 복사 최소화
- `ArrayPool<T>`로 대형 버퍼 재사용
- `ValueTask` (필요 시)로 async 할당 최소화

# 금지/회피 패턴
- LINQ(핫 루프), `string.Concat` 다중 사용, `StringBuilder` 과도 사용
- 리플렉션/동적 호출/런타임 코드 생성
- 빈번한 `new`/클로저 캡처/람다 할당

# FBA(간단 내장 벤치마크) 가이드
간단한 성능/할당 측정을 위해 다음 템플릿을 사용:
- 워밍업 1회
- N회 반복 측정
- `Stopwatch`로 시간 측정
- `GC.GetAllocatedBytesForCurrentThread()`로 할당량 측정

참고 템플릿: fba/FastBench.cs

# 예시
## 1) `ReadOnlySpan<char>` 기반 파서 (할당 최소화)
```csharp
using System;

public static class KeyValueParser
{
	public static bool TryParse(ReadOnlySpan<char> input, out ReadOnlySpan<char> key, out ReadOnlySpan<char> value)
	{
		int idx = input.IndexOf('=');
		if (idx <= 0 || idx >= input.Length - 1)
		{
			key = default;
			value = default;
			return false;
		}

		key = input.Slice(0, idx);
		value = input.Slice(idx + 1);
		return true;
	}
}
```

## 2) `ref struct` + `stackalloc` 버퍼 (스택 우선)
```csharp
using System;
using System.Buffers.Text;

public ref struct TempBuffer
{
	private Span<byte> _buffer;

	public TempBuffer(int minSize)
	{
		_buffer = minSize <= 256 ? stackalloc byte[minSize] : throw new ArgumentOutOfRangeException(nameof(minSize));
	}

	public Span<byte> Span => _buffer;
}

public static class HexEncoder
{
	public static bool TryEncodeHex(uint value, Span<byte> destination, out int written)
	{
		return Utf8Formatter.TryFormat(value, destination, out written, 'X');
	}
}
```

### 최신 스타일 팁
- `stackalloc`는 작은 버퍼에만 사용하고, 크기 상한을 둔다.
- 조건부 `stackalloc`로 스택/힙을 자동 선택하는 패턴을 권장한다.

```csharp
const int MaxStack = 256;
int length = input.Length;
Span<byte> buffer = length <= MaxStack ? stackalloc byte[length] : new byte[length];
```

## 3) `ArrayPool<T>`로 대형 버퍼 재사용 (GC 압력 감소)
```csharp
using System;
using System.Buffers;

public static class BufferWorker
{
	public static int Sum(ReadOnlySpan<int> data)
	{
		int[] rented = ArrayPool<int>.Shared.Rent(data.Length);
		try
		{
			data.CopyTo(rented);
			int sum = 0;
			for (int i = 0; i < data.Length; i++)
			{
				sum += rented[i];
			}
			return sum;
		}
		finally
		{
			ArrayPool<int>.Shared.Return(rented, clearArray: true);
		}
	}
}
```

## 4) FBA 템플릿으로 빠른 비교
```csharp
FastBench.Run("Parse", 1_000_000, () =>
{
	ReadOnlySpan<char> input = "A=123";
	_ = KeyValueParser.TryParse(input, out _, out _);
});
```

# 결과 보고 형식
- 개선 포인트(할당/GC/시간) 요약
- 전/후 비교 수치(FBA 기준)
- AOT 이슈 여부 및 대안

# 시각적 활성 표시
- 스킬이 실제로 적용되는 응답에는 눈에 띄는 표시를 포함한다.
- 예: "🟢 Skill Active: 최신 .NET, C# 메모리·성능 최적화" 같은 라벨을 응답 상단에 표기
