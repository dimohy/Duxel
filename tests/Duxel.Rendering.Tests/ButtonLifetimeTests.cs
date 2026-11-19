using System.Diagnostics;
using System.Runtime.CompilerServices;
using Duxel.Core;

internal static class ButtonLifetimeTests
{
    internal static void Run(Action<bool, string> check)
    {
        var glyphs = Enumerable.Range(32, 95).ToDictionary(c => c,
            c => new UiGlyphInfo(7, 0, -10, c == 32 ? 0 : 6, 12, new(0, 0, 1, 1)));
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255],
            glyphs, new Dictionary<uint, float>(), 12, -3, 1, -1), new(1), new(2));
        ControlFontFixture.Configure(context);
        var input = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame(IUiView view)
        {
            context.NewFrame(new(1f / 60, new(320, 240), new(1, 1)), input, new(0, 0, 320, 240), UiTextSettings.Default);
            context.Render(new Screen(view));
            context.GetDrawData().ReleasePooled();
        }
        using var button = Fluent.Button("Before");
        var requests = 0;
        var published = new List<(string, UiVector2)>();
        context.SetRequestFrameCallback(() =>
        {
            requests++;
            published.Add((button.Label, button.Size));
        });
        Frame(button);
        var before = requests;
        button.Label = "After";
        button.Label = new string("After".ToCharArray());
        button.Size = new(180, 40);
        button.Size = new(180, 40);
        check(requests - before == 2 && published.TakeLast(2).SequenceEqual(new[]
            { ("After", default(UiVector2)), ("After", new UiVector2(180, 40)) }),
            "button label/size publish before waking an idle host once per actual change");

        var warmStart = Stopwatch.GetTimestamp();
        var warmCalls = 0;
        do
        {
            MeasureIdenticalAssignments(button);
            warmCalls++;
        } while (Stopwatch.GetElapsedTime(warmStart).TotalSeconds < 0.25);
        var bytes = MeasureIdenticalAssignments(button);
        Console.WriteLine($"Button assignment allocation : {bytes}; warm calls : {warmCalls}; requests : {requests - before}");
        if (bytes != 0 || requests - before != 2)
            throw new InvalidOperationException($"Button same-value assignments : allocatedBytes = {bytes}, requests = {requests - before}");
        check(true, "ten thousand identical button assignments allocate nothing and schedule no redundant frames");

        var oldCalls = 0;
        var newCalls = 0;
        button.Activated = () => oldCalls++;
        Frame(button);
        input = input with { MousePosition = new(30, 25), LeftMouseDown = true, LeftMousePressed = true };
        Frame(button);
        button.Label = "Changed while held";
        button.Activated = () => newCalls++;
        input = input with { LeftMouseDown = false, LeftMousePressed = false, LeftMouseReleased = true };
        Frame(button);
        check(oldCalls == 0 && newCalls == 1,
            "a held button keeps its input identity while invoking the replacement callback exactly once on release");
        before = requests;
        button.Activated = null;
        check(requests == before, "changing only a button callback does not schedule an unnecessary repaint");
        var nullRejected = false;
        try
        {
            button.Label = null!;
        }
        catch (ArgumentNullException)
        {
            nullRejected = true;
        }
        check(nullRejected && button.Label == "Changed while held" && requests == before,
            "null button labels fail consistently with construction without changing state or waking a frame");
        using var inherited = new Derived("Inherited");
        Frame(inherited);
        before = requests;
        inherited.Label = "Updated subclass";
        check(requests - before == 1, "user-derived buttons inherit common invalidation without another implementation path");
        button.Activated = () => newCalls++;
        button.Dispose();
        check(button.Activated is null && button.Label.Length == 0, "disposing a button releases callback closures and the owned label");
        static bool Reject(Action action)
        {
            try
            {
                action();
                return false;
            }
            catch (ObjectDisposedException)
            {
                return true;
            }
        }
        before = requests;
        check(Reject(() => button.Label = "After disposal") && Reject(() => button.Label = null!)
            && Reject(() => button.Size = new(1, 1)) && Reject(() => button.Activated = () => newCalls++) && requests == before,
            "disposed buttons reject label/size/callback ownership before normalization without requesting frames");
        var throwing = new ThrowingButton("Throwing", () => newCalls++);
        var failureObserved = false;
        try
        {
            throwing.Dispose();
        }
        catch (AggregateException error)
        {
            failureObserved = error.InnerExceptions.Count == 1;
        }
        check(failureObserved && throwing.Activated is null && throwing.Label.Length == 0,
            "a failing user disposal hook remains observable while framework-owned button references are released");
        using var text = new SkippingText(() => "Owned getter");
        text.Dispose();
        check(text.Source is null, "a user text disposal hook cannot bypass framework-owned source release by omitting base");
        using var badge = new ThrowingBadge(() => "Owned badge");
        failureObserved = false;
        try
        {
            badge.Dispose();
        }
        catch (AggregateException)
        {
            failureObserved = true;
        }
        check(failureObserved && badge.Source is null, "a failing custom badge disposal hook still releases the framework source");
        using var divider = new SkippingDivider(() => "Owned divider");
        divider.Dispose();
        check(divider.Source is null, "a user Divider disposal hook cannot bypass framework source release");
    }

    // Warm and measure the same isolated loop; do not include the caller's OSR transition in the allocation interval.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static long MeasureIdenticalAssignments(UiFluentButton button)
    {
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 10000; i++)
        {
            button.Label = "After";
            button.Size = new(180, 40);
        }
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    private sealed class Screen(IUiView view) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => view.Render(ui);
    }
    private sealed class Derived(string text) : UiFluentButton(text);
    private sealed class ThrowingButton(string text, Action action) : UiFluentButton(text, action)
    {
        protected override void OnDispose() => throw new InvalidOperationException("Declared disposal failure.");
    }
    private sealed class SkippingText(Func<string> source) : UiText<Func<string>, UiGetterTextSource, UiContextTextPresentation>(source)
    {
        protected override void OnDispose() { }
    }
    private sealed class ThrowingBadge(Func<string> source) : UiBadge<Func<string>, UiGetterBadgeBehavior, UiFluentBadgePresentation>(source)
    {
        protected override void OnDispose() => throw new InvalidOperationException("Declared disposal failure.");
    }
    private sealed class SkippingDivider(Func<string> source) : UiFluentDynamicDivider(source)
    {
        protected override void OnDispose() { }
    }
}