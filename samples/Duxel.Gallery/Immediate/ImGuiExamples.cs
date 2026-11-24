using Duxel.Core;

public sealed class ImGuiExamples
{
    private bool _showAnotherPanel;
    private bool _disableControls;
    private float _floatValue = 0.5f;
    private int _counter;
    private float _clearR = 0.45f;
    private float _clearG = 0.55f;
    private float _clearB = 0.60f;

    public ImmediateSampleSection Section { get; }
    public ImGuiExamples()
    {
        Section =
        ImmediateSampleSection.Define("Hello", ui =>
        {
            ui.PushID("HelloExample");

            ui.Text("Hello, world!");
            ui.Checkbox("Another Panel", ref _showAnotherPanel);
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

            ui.PopID();

            if (_showAnotherPanel)
            {
                ui.BeginPanel("Another Panel");
                ui.Text("Hello from another panel!");
                if (ui.Button("Close"))
                {
                    _showAnotherPanel = false;
                }
                ui.EndPanel();
            }
        }, () => (_showAnotherPanel, _disableControls, _floatValue, _counter, _clearR, _clearG, _clearB), () => string.Join("\n",
            [
                "bool _showAnotherPanel = " + ImmediateSampleSection.Boolean(_showAnotherPanel) + ";",
                "bool _disableControls = " + ImmediateSampleSection.Boolean(_disableControls) + ";",
                "float _floatValue = " + ImmediateSampleSection.Number(_floatValue) + ";",
                "int _counter = " + ImmediateSampleSection.Integer(_counter) + ";",
                "float _clearR = " + ImmediateSampleSection.Number(_clearR) + ";",
                "float _clearG = " + ImmediateSampleSection.Number(_clearG) + ";",
                "float _clearB = " + ImmediateSampleSection.Number(_clearB) + ";"
            ]));
    }
    public void Render(UiImmediateContext ui) => Section.Render(ui);
}
