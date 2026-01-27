#:property TargetFramework=net10.0
#:project ../../src/Dux.App/Dux.App.csproj

using Dux.App;
using Dux.Core;

DuxApp.Run(new DuxAppOptions
{
    Window = new DuxWindowOptions
    {
        Width = 1280,
        Height = 720,
        Title = "Dux Vulkan GLFW Demo",
        VSync = true,
    },
    Font = new DuxFontOptions
    {
        FontSize = 18,
        InitialGlyphs =
        [
            "Dux Vulkan GLFW Demo",
            "ImGui Vulkan example equivalent in Dux",
            "Show Demo Window",
            "Show Another Window",
            "Value",
            "Button",
            "counter",
            "Disabled",
            "Disable controls",
            "Disabled Value",
            "Disabled Button",
            "Another Window",
            "Hello from another window.",
        ]
    },
    Debug = new DuxDebugOptions
    {
        Log = message => Console.WriteLine($"[DuxTrace] {message}"),
        LogEveryNFrames = 120
    },
    Screen = new VulkanExampleScreen()
});

sealed class VulkanExampleScreen : UiScreen
{
    private bool _showDemo = true;
    private bool _showAnotherWindow = true;
    private bool _disableControls;
    private float _value = 0.5f;
    private int _counter;

    public override void Render(UiImmediateContext ui)
    {
        ui.BeginWindow("Vulkan Example (GLFW)");
        ui.Text("ImGui Vulkan example equivalent in Dux");
        ui.Checkbox("Show Demo Window", ref _showDemo);
        ui.Checkbox("Show Another Window", ref _showAnotherWindow);
        ui.SliderFloat("Value", ref _value, 0f, 1f, 0.01f, "0.00");
        if (ui.Button("Button"))
        {
            _counter++;
        }
        ui.SameLine();
        ui.Text($"counter = {_counter}");

        ui.SeparatorText("Disabled");
        ui.Checkbox("Disable controls", ref _disableControls);
        ui.BeginDisabled(_disableControls);
        ui.SliderFloat("Disabled Value", ref _value, 0f, 1f, 0.01f, "0.00");
        ui.Button("Disabled Button");
        ui.EndDisabled();
        ui.EndWindow();

        if (_showDemo)
        {
            ui.ShowDemoWindow(ref _showDemo);
        }

        if (_showAnotherWindow)
        {
            ui.BeginWindow("Another Window");
            ui.Text("Hello from another window.");
            if (ui.Button("Close"))
            {
                _showAnotherWindow = false;
            }
            ui.EndWindow();
        }
    }
}
