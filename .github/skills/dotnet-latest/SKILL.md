---
name: "최신 .NET, C# 사용 가이드"
description: ".NET 10, C# 14 기준의 최신 문법·스타일을 적용하고, 공식 Microsoft Learn 문서를 우선 참고한다."
version: "1.1"
owner: "team"
---
# 목적

- .NET 10, C# 14 기준의 최신 문법/스타일을 적용한다.
- 공식 문서(Microsoft Learn)를 우선 기준으로 삼는다.
- 성능 최적화는 별도 스킬(.github/skills/dotnet-performance)에 위임한다.

# 적용 시점

다음 요청에 자동 적용:

- !!!최우선!!! 코드 생성(새 파일/기능 구현/대량 코드 작성) 요청 시 자동 적용.
- “최신 C# 문법”, “최신 .NET 스타일”, “modern C#”
- “Microsoft Learn 기반으로”, “공식 문서 기준”
- “최신 API 사용법”
- “.NET FBA로 작성해줘”, “FBA로 작성해줘”
- C# 코드 리팩터/간단화/스타일 정리 요청
- 컬렉션/배열 초기화 축약(예: `Array.Empty<T>()`, `new T[0]`, `new List<T>()`) 관련 언급
- 기존 코드의 최신 문법 적용 요청(“최신 문법으로 바꿔줘”, “짧게 줄여줘” 등)

# 용어 해석

- **FBA(File-Based Application)**: .NET 10에서 동작하는 **파일 베이스 애플리케이션**(단일/소수 파일 중심의 간단한 앱) 요청으로 해석한다.

# 핵심 원칙 (요약)

1. **최신 스타일 우선**: 최신 언어 기능(컬렉션 표현식 등)을 권장.
2. **명확성 우선**: 단순하고 읽기 쉬운 표현을 선호.
3. **공식 근거**: Microsoft Learn 문서를 우선 참조.
4. **호환성**: .NET 10 / C# 14 기준, 필요 시 대안도 제시.
5. **컬렉션 표현식 우선**: 문맥이 허용하면 `[]`를 사용해 빈 컬렉션/배열을 간결화한다.

# 최신 문법/스타일 가이드

- 컬렉션 표현식(예: `Span<int> s = [1, 2, 3];`) 사용 가능.
- 빈 배열/컬렉션은 문맥이 충분하면 `[]`로 축약한다(예: `Image[] images = [];`).
- `Array.Empty<T>()`는 타입 문맥이 모호하거나 API 요구상 필요할 때만 사용한다.
- 컬렉션 표현식의 빈 리터럴 `[]`은 **타깃 타입**이 있어야 한다(`var v = []`는 불가).
- 컬렉션 표현식 스프레드(`..`)로 다른 컬렉션을 인라인 확장한다(예: `[1, 2, ..values]`).
- 대상 타입이 명확하면 **target-typed `new()`**로 타입 반복을 줄인다.
- `Span<T>`/`ReadOnlySpan<T>` 기반 API를 우선 사용.
- `stackalloc`은 작은 버퍼에만 사용하고 크기 상한을 둔다.
- 불필요한 예외 기반 제어 흐름을 피하고 `Try*` 패턴 선호.
- 단순 비교는 최신 패턴 매칭 사용(예: `args.Length is 0`), 오래된 비교(`args.Length == 0`)는 지양.
- `null` 비교는 패턴 매칭(`is null`, `is not null`)을 우선 사용.
- 논리 패턴(`and`, `or`, `not`)을 활용해 가독성 높은 범위/조건식을 구성한다.
- `switch` 식/문과 패턴 매칭을 우선 사용해 분기 로직을 데이터 중심으로 구성한다.
- 컬렉션 표현식은 `Span<T>`/`ReadOnlySpan<T>` 초기화에도 사용 가능함을 명시한다.
- 최신 C# 14 기능(예: extension members, null-conditional assignment, `field` backed properties, partial events/constructors) 사용을 허용한다.
- 파일 베이스 앱에서 사용할 수 있는 전처리 지시문(파일 기반 앱용)을 허용한다.
- 리스트/속성/관계형 패턴을 사용해 가독성 높은 검증 로직을 구성한다.

# 예시(최신 문법)

## 패턴 매칭

```csharp
if (args.Length is 0)
{
  return;
}

if (input is not null)
{
  // ...
}

static bool IsLetter(char c) => c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z');
```

## 리스트/속성/관계형 패턴

```csharp
if (numbers is [1, 2, ..])
{
  // starts with 1,2
}

if (order is { Status: OrderStatus.Open, Items.Count: > 0 })
{
  // open and has items
}

static bool IsSmall(int n) => n is > 0 and <= 10;
```

## switch 식

```csharp
static int ToPrice(int count) => count switch
{
  0 => 0,
  1 => 12,
  2 => 20,
  _ => 30,
};
```

## 컬렉션 표현식 + Span

```csharp
Span<int> s = [1, 2, 3];
ReadOnlySpan<int> rs = [1, 2, 3];
```

## 빈 컬렉션 축약

```csharp
Image[] images = [];
List<string> names = [];
ReadOnlySpan<byte> empty = [];
```

## 컬렉션 스프레드

```csharp
int[] all = [1, 2, ..other, 9];
List<int> list = [..prefix, value, ..suffix];
```

## target-typed new

```csharp
Dictionary<string, int> map = new();
MyOptions options = new() { Enabled = true };
```

## null-conditional assignment (C# 14)

```csharp
customer?.Order = GetCurrentOrder();
```

## field backed properties (C# 14)

```csharp
public string Message
{
  get;
  set => field = value ?? throw new ArgumentNullException(nameof(value));
}
```

## extension members (C# 14)

```csharp
public static class Enumerable
{
  extension<TSource>(IEnumerable<TSource> source)
  {
    public bool IsEmpty => !source.Any();
  }
}
```

## user-defined compound assignment (C# 14)

```csharp
public readonly struct Vec2
{
  public readonly int X;
  public readonly int Y;
  public Vec2(int x, int y) => (X, Y) = (x, y);

  public static Vec2 operator +(Vec2 left, Vec2 right) => new(left.X + right.X, left.Y + right.Y);
  public static Vec2 operator +=(Vec2 left, Vec2 right) => left + right;
}

var v = new Vec2(1, 2);
v += new Vec2(3, 4);
```

## nameof + unbound generic types (C# 14)

```csharp
string typeName = nameof(System.Collections.Generic.List<>); // "List"
```

## 람다 파라미터 modifier (C# 14)

```csharp
delegate bool TryParse<T>(string text, out T result);
TryParse<int> parse = (text, out result) => int.TryParse(text, out result);
```

# Microsoft Learn 활용

- 최신 문법/권장 패턴 확인은 Microsoft Learn 공식 문서를 우선 참조한다.
- 필요한 경우 **Microsoft Learn 검색/코드 샘플 도구**를 사용해 최신 예시를 확보한다.
  - 검색: `microsoft_docs_search`
  - 코드 샘플: `microsoft_code_sample_search`
  - 전체 문서: `microsoft_docs_fetch`

# Microsoft Learn 근거 링크

- C# 14 개요: https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-14
- 패턴 매칭(`is`, 상수/논리 패턴): https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/patterns
- `stackalloc` 표현식: https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/stackalloc
- 컬렉션 표현식: https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/proposals/csharp-12.0/collection-expressions
- target-typed `new`: https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/new-operator#target-typed-new

# 결과 보고 형식

- 최신 문법/스타일 적용 포인트 요약
- 호환성(타깃 프레임워크/언어 버전) 명시
- 참고한 Microsoft Learn 문서 링크

# 자동 치환 체크리스트

- `Array.Empty<T>()` → 문맥이 충분하면 `[]`
- `new T[0]` → `[]`
- `new List<T>()`/`new Dictionary<TKey, TValue>()` → 대상 타입이 명확하면 `[]`
- `ImmutableArray<T>.Empty` → 대상 타입이 명확하면 `[]`
- `new()` 기본 생성 → 대상 타입이 명확하면 컬렉션 표현식 또는 간결 초기화 우선

# 시각적 활성 표시

- 스킬이 실제로 적용되는 응답에는 눈에 띄는 표시를 포함한다.
- 예: "🟢 Skill Active: 최신 .NET, C# 사용 가이드" 같은 라벨을 응답 상단에 표기
