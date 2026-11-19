using System.Numerics;
using Duxel.Core;

internal static class ScrollRoutingRenderingTests
{
    internal static void Run(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool, string> check,
        UiState state, Action<UiInputState> setInput, bool vector)
    {
        var idle = new UiInputState(new(45, 45), false, false, false, false, false, false, 0, 0, [], [], default, default);
        const string inner = "pixel-inner";
        const string outer = "pixel-outer";
        var step = 0f;
        void Scene(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding : 0);
            if (vector)
                ui.BeginLayer("scroll-vector", new(false, 1, default) { Scene3D = new(Matrix4x4.Identity, false) });
            try
            {
                ui.SetCursorScreenPos(new(20, 20));
                step = ui.GetFrameHeight() * 3f;
                ui.BeginChild(outer, new(140, 150), border : true);
                ui.BeginChild(inner, new(90, 80), border : true);
                for (var row = 0; row < 12; row++)
                    ui.Text("Inner row : " + row);
                ui.Dummy(new(300, 30));
                ui.EndChild();
                for (var row = 0; row < 12; row++)
                    ui.Text("Outer row : " + row);
                ui.Dummy(new(300, 30));
                ui.EndChild();
            }
            finally
            {
                if (vector)
                    ui.EndLayer();
            }
        }
        void Offsets(float innerOffset, float outerOffset, bool horizontal)
        {
            state.SetScrollX(inner, horizontal ? innerOffset : 0);
            state.SetScrollY(inner, horizontal ? 0 : innerOffset);
            state.SetScrollX(outer, horizontal ? outerOffset : 0);
            state.SetScrollY(outer, horizontal ? 0 : outerOffset);
        }
        foreach (var horizontal in new[] { false, true })
        foreach (var boundary in new[] { false, true })
        {
            var name = $"scroll-{horizontal}-{boundary}-{vector}";
            setInput(idle);
            Offsets(boundary ? 10000 : 0, 0, horizontal);
            capture(name + "-warmup", Scene);
            var start = horizontal ? state.GetScrollX(inner) : state.GetScrollY(inner);
            setInput(idle with { MouseWheel = horizontal ? 0 : -1, MouseWheelHorizontal = horizontal ? -1 : 0 });
            capture(name + "-event", Scene);
            var actualInner = horizontal ? state.GetScrollX(inner) : state.GetScrollY(inner);
            var actualOuter = horizontal ? state.GetScrollX(outer) : state.GetScrollY(outer);
            check(actualInner == (boundary ? start : step) && actualOuter == (boundary ? step : 0),
                name + " actual offsets select the innermost movable owner");
            setInput(idle);
            var actual = capture(name + "-actual", Scene);
            Offsets(boundary ? start : step, boundary ? step : 0, horizontal);
            var expected = capture(name + "-reference", Scene);
            check(actual.AsSpan().SequenceEqual(expected),
                name + " acquired image matches independently assigned expected offsets");
        }
        setInput(idle with { MousePosition = new(-100, -100) });
    }
}
