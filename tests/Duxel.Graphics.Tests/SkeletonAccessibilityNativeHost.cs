using System.Diagnostics;
using Duxel.Core;
using Duxel.Platform.Windows;

internal static class SkeletonAccessibilityNativeHost
{
    internal static void Run(Action<bool, string> check)
    {
        using var platform = new WindowsPlatformBackend(new()
        {
            Width = 640,
            Height = 420,
            Title = "Duxel owned Skeleton fixture",
            KeyRepeatSettingsProvider = new WindowsKeyRepeatSettingsProvider(),
            TrayOptions = new()
        });
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        context.SetAccessibilityTree(platform.AccessibilityTree);
        using var scene = new Scene();
        var limit = Environment.TickCount64 + 20000;
        var previous = Stopwatch.GetTimestamp();
        var frames = 0;
        while (!scene.Finished && !platform.ShouldClose && Environment.TickCount64 < limit)
        {
            platform.PollEvents();
            var now = Stopwatch.GetTimestamp();
            context.NewFrame(new((float)Stopwatch.GetElapsedTime(previous, now).TotalSeconds, new(640, 420), new(1, 1)),
                new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default),
                new(0, 0, 640, 420), UiTextSettings.Default);
            previous = now;
            context.Render(scene);
            using var draw = context.GetDrawData();
            if (++frames == 1)
            {
                Console.WriteLine("UIA_SKELETON_HOST_READY " + platform.WindowHandle);
                Console.Out.Flush();
            }
            Thread.Sleep(1);
        }
        check(scene.Finished && !scene.Loading && scene.Completions == 2, "Independent UIA client completes two actual Skeleton loading transitions");
    }

    private sealed class Scene : UiScreen, IDisposable
    {
        private readonly UiWidget _root, _complete, _reload, _finish, _loaded;
        private readonly UiFluentBody1 _standalone = Fluent.Body1("Static native text 정세일");
        internal bool Loading { get; private set; } = true;
        internal bool Finished
        {
            get; private set;
        }
        internal int Completions
        {
            get; private set;
        }
        internal Scene()
        {
            var root = Fluent.Skeleton(Fluent.SkeletonItem(new(Width: 240)), new(Paused: true));
            root.AccessibleName = "Native Skeleton";
            _root = root;
            _complete = Fluent.Button("Complete Skeleton", () => { Loading = false; Completions++; _standalone.Source = "Updated standalone text 부산"; });
            _reload = Fluent.Button("Reload Skeleton", () => { Loading = true; _standalone.Source = "Static native text 정세일"; });
            _finish = Fluent.Button("Finish Skeleton fixture", () => Finished = true);
            var loaded = Fluent.Input("Skeleton content ready", new UiBinding<string>(() => "정세일",
                _ => throw new InvalidOperationException("Loaded data is read-only in this fixture.")), 64);
            loaded.Options = loaded.Options with
            {
                Flags = UiInputTextFlags.ReadOnly
            };
            _loaded = loaded;
        }
        public override void Render(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding: 8);
            _standalone.Render(ui);
            _complete.Render(ui);
            _reload.Render(ui);
            _finish.Render(ui);
            if (Loading)
                _root.Render(ui);
            else
                _loaded.Render(ui);
        }
        public void Dispose()
        {
            _root.Dispose();
            _complete.Dispose();
            _standalone.Dispose();
            _reload.Dispose();
            _finish.Dispose();
            _loaded.Dispose();
        }
    }
}
