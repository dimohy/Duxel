using Duxel.App;
using Duxel.Core;
using Duxel.Linux.Sample;

// Test-only render wrapper; the product sample and its input geometry remain unchanged.
internal sealed class TextPaintProbe(LinuxDemoScreen screen, DuxelAppSession session) : UiScreen
{
    private int _requested, _painted;
    private string? _ackPath;
    internal void Observe(string resultPath)
    {
        var path = resultPath + ".paint-state";
        var state = Volatile.Read(ref _requested) + 1;
        if (state > 9 || !File.Exists(path + "." + state.ToString(System.Globalization.CultureInfo.InvariantCulture)))
            return;
        Volatile.Write(ref _ackPath, path + ".ack");
        Volatile.Write(ref _requested, state);
        session.RequestFrame();
    }
    public override void Render(UiImmediateContext ui)
    {
        var state = Volatile.Read(ref _requested);
        screen.FontNameLanguage = state == 4 ? "ko" : "en";
        if (state == 7)
            ui.PushFontSize(24);
        if (state == 1)
            ui.PushStyleColor(UiStyleColor.InputText, new UiColor(17, 83, 201));
        try
        {
            screen.Render(ui);
        }
        finally
        {
            if (state == 1)
                ui.PopStyleColor();
            if (state == 7)
                ui.PopFontSize();
        }
        if (state != 0 && state != _painted)
        {
            using var acknowledgement = new FileStream(Volatile.Read(ref _ackPath)! + "."
                + state.ToString(System.Globalization.CultureInfo.InvariantCulture), FileMode.CreateNew);
            _painted = state;
        }
    }
}
