using Duxel.Core;

namespace Duxel.Gallery;

internal sealed class EditingPage : UiWidget, IGallerySourcePage
{
    private readonly UiState<string> _name = new("");
    private readonly UiState<string> _notes = new(GalleryText.Get("Try typing, selecting, copying, pasting, and undoing text.") + "\n"
        + GalleryText.Get("Your input stays when you switch tabs."));
    private readonly UiWidget _root;
    public IReadOnlyList<IGallerySample> Samples { get; }

    internal EditingPage()
    {
        var nameSample = GallerySample.Create(() => Dux.Column(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min),
            new ValidatedNameExample(_name),
            Fluent.Button(GalleryText.Get("Single-line caret example"), () => _name.Value = "abc אבגד 123 · AV e\u0301 · " + GalleryText.Get("Sample text")),
            Fluent.Body1(() => string.Format(GalleryText.Get("Name: {0}"), _name.Value))));
        var nameFactory = nameSample.Source;
        var declaration = GallerySample.LocalizeSource(GalleryPageSource.Declaration(nameof(ValidatedNameExample)));
        nameSample.BindSource(() => _name.Value, value => $"var _name = new UiState<string>({GallerySample.String(value)});\n\n"
            + nameFactory + "\n\n" + declaration);
        var editorDefinition = GallerySample.Define("editor", () => Fluent.Textarea(GalleryText.Get("Notes"), _notes.Binding, 16384, 620, 220));
        var editor = editorDefinition.Value;
        var labelDefinition = GallerySample.Define("memoLabel", () => Fluent.Label(GalleryText.Get("Notes"), editor));
        var memoLabel = labelDefinition.Value;
        var memoSample = GallerySample.Create(() => Dux.Column(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min),
            Dux.SizedBox(memoLabel, width : 620), editor,
            Dux.Wrap(new(Spacing : 16, RunSpacing : 12, CrossAlignment : UiWrapCrossAlignment.Center),
                Fluent.Button(GalleryText.Get("Toggle line wrapping"), () => editor.Options = editor.Options with { Wrap = editor.Options.Wrap == false }),
                Fluent.Button(GalleryText.Get("Toggle read-only"), () => editor.Options = editor.Options with
                {
                    Flags = (editor.Options.Flags & UiInputTextFlags.ReadOnly) == 0 ? UiInputTextFlags.ReadOnly : UiInputTextFlags.None
                }),
                Fluent.Button(GalleryText.Get("Clear notes"), () => _notes.Value = string.Empty),
                Fluent.Button(GalleryText.Get("Kerning and mixed-script example"), () => _notes.Value = "AV office\nabc אב 123\n"
                    + GalleryText.Get("Sample text and") + " e\u0301\nA \u202Bאב " + GalleryText.Get("Bidi sample character") + " גד\u202C Z"),
                Fluent.Button(GalleryText.Get("Toggle required label"), () => memoLabel.Options = memoLabel.Options with { Required = !memoLabel.Options.Required }),
                Fluent.Button(GalleryText.Get("Change label size"), () => memoLabel.Options = memoLabel.Options with { Size = (UiLabelSize)(((int)memoLabel.Options.Size + 1) % 3) }),
                Fluent.Button(GalleryText.Get("Toggle label weight"), () => memoLabel.Options = memoLabel.Options with { Semibold = !memoLabel.Options.Semibold }),
                Fluent.Button(GalleryText.Get("Toggle label availability"), () => memoLabel.Options = memoLabel.Options with { Disabled = !memoLabel.Options.Disabled }),
                Fluent.Button(GalleryText.Get("Toggle long label"), () => memoLabel.Text = memoLabel.Text == GalleryText.Get("Notes")
                    ? GalleryText.Get("Enter your notes. This long label wraps within the allotted width. Select it to focus the notes field below.")
                    : GalleryText.Get("Notes"))),
            Fluent.Body1(() => string.Format(GalleryText.Get("Note length: {0}"), _notes.Value.Length)),
            Fluent.Caption1(GalleryText.Get("Use Tab or Shift+Tab to move and Ctrl+Z to undo. The wrap and read-only buttons change how the notes editor behaves."))));
        var memoFactory = memoSample.Source;
        memoSample.BindSource(() => (_notes.Value, editor.Options, Label : memoLabel.Text, LabelOptions : memoLabel.Options), state =>
        {
            var wrap = state.Options.Wrap is { } value ? GallerySample.Boolean(value) : "null";
            var flags = string.Join(" | ", state.Options.Flags.ToString().Split(", ").Select(flag => "UiInputTextFlags." + flag));
            return $"var _notes = new UiState<string>({GallerySample.String(state.Value)});\n\n" + editorDefinition.Source
                + $"\neditor.Options = new UiMultilineInputOptions {{ Flags = {flags}, Wrap = {wrap},\n"
                + $"    Placeholder = {GallerySample.String(state.Options.Placeholder)}, Invalid = {GallerySample.Boolean(state.Options.Invalid)} }};\n\n"
                + labelDefinition.Source + $"\nmemoLabel.Text = {GallerySample.String(state.Label)};\n"
                + $"memoLabel.Options = new UiLabelOptions(Required : {GallerySample.Boolean(state.LabelOptions.Required)}, Disabled : {GallerySample.Boolean(state.LabelOptions.Disabled)},\n"
                + $"    Size : UiLabelSize.{state.LabelOptions.Size}, Semibold : {GallerySample.Boolean(state.LabelOptions.Semibold)});\n\n" + memoFactory;
        });
        Samples = [nameSample, memoSample];
        _root = Dux.Column(new(Spacing : 16, MainAxisSize : UiMainAxisSize.Min),
            Fluent.Subtitle2(GalleryText.Get("Name input and validation")), nameSample,
            Fluent.Subtitle2(GalleryText.Get("Notes editor and linked label")), memoSample);
        OwnChild("root", _root);
    }
    protected override IUiView Build(UiBuildContext context) => context.Child("root", () => _root);
}
