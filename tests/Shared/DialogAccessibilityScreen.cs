using System.Globalization;
using Duxel.Core;

namespace Duxel.Validation;

internal sealed class DialogAccessibilityScreen : UiScreen, IDisposable
{
    private readonly UiWidget _root;
    internal readonly UiState<string> Name = new("Initial name");
    internal UiFluentDialog Dialog
    {
        get;
    }
    internal int Frame
    {
        get; set;
    }
    internal bool Finished
    {
        get; private set;
    }
    internal DialogAccessibilityScreen()
    {
        Dialog = Fluent.Dialog("Dialog profile", Fluent.Input("Dialog name", Name.Binding, 128, 260),
            [Fluent.Button("Save profile", CloseDialog), Fluent.Button("Cancel profile", CloseDialog)],
            new(Description: "Profile description", CloseLabel: "Close dialog"));
        void Open(UiDialogType type)
        {
            Dialog.Options = Dialog.Options with
            {
                Type = type
            };
            Dialog.Show();
        }
        var marker = Fluent.Input("Native frame", new UiBinding<string>(() => Frame.ToString(CultureInfo.InvariantCulture),
            _ => throw new InvalidOperationException("The native frame marker is read-only to the probe.")), 32, 120);
        _root = Dux.Column(new(Spacing: 8, MainAxisSize: UiMainAxisSize.Min),
            Fluent.Button("Finish Dialog", () => Finished = true), marker,
            Dux.Row(new(Spacing: 8),
                Fluent.Button("Open modal", () => Open(UiDialogType.Modal)),
                Fluent.Button("Open non-modal", () => Open(UiDialogType.NonModal)),
                Fluent.Button("Open alert", () => Open(UiDialogType.Alert))), Dialog);
    }
    private void CloseDialog() => Dialog.Close();

    public override void Render(UiImmediateContext ui)
    {
        ui.EnableRootViewportContentLayout(contentPadding: 0);
        _root.Render(ui);
    }
    public void Dispose() => _root.Dispose();
}
