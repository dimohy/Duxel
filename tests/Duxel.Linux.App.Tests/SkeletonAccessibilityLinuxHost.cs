using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Duxel.Core;

internal static class SkeletonAccessibilityLinuxHost
{
    internal static void Run(string output, string primary, string secondary)
    {
        using var screen = new Scene();
        var frames = NativeAccessibilityLinuxHost.Run(output, primary, secondary, screen, "Skeleton", "atspi-skeleton", () =>
        {
            if (screen.Loading || screen.Completions != 2)
                throw new InvalidOperationException("The native Skeleton client did not complete two real loading transitions.");
        });
        File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new NativeSkeletonHostResult(
            RuntimeInformation.FrameworkDescription, 3, frames, "Actual X11/Vulkan host, independent libatspi state/action client and retired registry"),
            NativeSkeletonJson.Default.NativeSkeletonHostResult));
    }

    private sealed class Scene : UiScreen, IDisposable
    {
        private readonly UiFluentSkeleton _skeleton;
        private readonly UiWidget _complete, _reload, _loaded;
        private readonly UiFluentBody1 _standalone = Fluent.Body1("Static native text 정세일");
        internal bool Loading { get; private set; } = true;
        internal int Completions
        {
            get; private set;
        }
        internal Scene()
        {
            _skeleton = Fluent.Skeleton(Dux.Column(new(Spacing: 8, MainAxisSize: UiMainAxisSize.Min),
                Fluent.SkeletonItem(new(Size: 48, Shape: UiSkeletonShape.Circle)),
                Fluent.SkeletonItem(new(Width: 240))), new(Paused: true));
            _skeleton.AccessibleName = "Native Skeleton";
            _complete = Fluent.Button("Complete Skeleton", () => { Loading = false; Completions++; _standalone.Source = "Updated standalone text 부산"; });
            _reload = Fluent.Button("Reload Skeleton", () => { Loading = true; _standalone.Source = "Static native text 정세일"; });
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
            if (Loading)
                _skeleton.Render(ui);
            else
                _loaded.Render(ui);
        }
        public void Dispose()
        {
            _skeleton.Dispose();
            _complete.Dispose();
            _standalone.Dispose();
            _reload.Dispose();
            _loaded.Dispose();
        }
    }
}

internal sealed record NativeSkeletonHostResult(string framework, int passed, int frames, string scope);
internal sealed record NativeSkeletonClientResult(string framework, int passed, string[] checks);
[JsonSerializable(typeof(NativeSkeletonHostResult))]
[JsonSerializable(typeof(NativeSkeletonClientResult))]
internal partial class NativeSkeletonJson : JsonSerializerContext;
