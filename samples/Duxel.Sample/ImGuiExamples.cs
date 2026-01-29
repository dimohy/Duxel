using Duxel.Core;

public sealed class ImGuiExamples
{
    private bool _showDemoWindow = true;
    private bool _showAnotherWindow;
    private bool _disableControls;
    private float _floatValue = 0.5f;
    private int _counter;
    private float _clearR = 0.45f;
    private float _clearG = 0.55f;
    private float _clearB = 0.60f;

    public void Render(UiImmediateContext ui)
    {
        ui.BeginWindow("ImGui Example: Hello, world");

        ui.Text("Hello, world!");
        ui.Checkbox("Demo Window", ref _showDemoWindow);
        ui.Checkbox("Another Window", ref _showAnotherWindow);
        ui.SliderFloat("float", ref _floatValue, 0f, 1f, 0.01f, "0.00");
        ui.ColorEdit3("clear color", ref _clearR, ref _clearG, ref _clearB);

        if (ui.Button("Button"))
        {
            _counter++;
        }
        ui.SameLine();
        ui.Text($"counter = {_counter}");

        ui.SeparatorText("Disabled");
        ui.Checkbox("Disable controls", ref _disableControls);
        ui.BeginDisabled(_disableControls);
        ui.SliderFloat("disabled float", ref _floatValue, 0f, 1f, 0.01f, "0.00");
        ui.Button("Disabled Button");
        ui.EndDisabled();

        ui.EndWindow();

        if (_showDemoWindow)
        {
            ui.ShowDemoWindow(ref _showDemoWindow);
        }

        if (_showAnotherWindow)
        {
            ui.BeginWindow("Another Window");
            ui.Text("Hello from another window!");
            if (ui.Button("Close"))
            {
                _showAnotherWindow = false;
            }
            ui.EndWindow();
        }
    }
}

