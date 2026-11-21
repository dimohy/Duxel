using System.Collections.Immutable;
using Duxel.Core;

namespace Duxel.Gallery;

internal sealed class AccordionPage : UiWidget, IGallerySourcePage
{
    private readonly List<IGallerySample> _samples = [];
    public IReadOnlyList<IGallerySample> Samples => _samples;
    private readonly UiState<ImmutableArray<string>> _open = new(ImmutableArray<string>.Empty);
    private readonly UiState<string> _memo = new(GalleryText.Get("The text stays when you collapse and reopen the panel."));
    protected override IUiView Build(UiBuildContext context) => context.Child("root", () =>
    {
        _samples.Clear();
        var openStatus = GallerySample.Define<Func<ImmutableArray<string>, string>>("formatOpenPanels", () => (ImmutableArray<string> values) =>
            values.IsEmpty ? GalleryText.Get("No open panels") : string.Format(GalleryText.Get("Panels open: {0}"),
                string.Join(", ", values.Select(value => value switch
                {
                    "input" => GalleryText.Get("Keep input text"),
                    "button" => GalleryText.Get("Body button and keyboard navigation"),
                    "disabled" => GalleryText.Get("Disabled header"),
                    _ => throw new ArgumentOutOfRangeException(nameof(value))
                }))));
        var formatOpenPanels = openStatus.Value;
        var status = Fluent.Body1(formatOpenPanels(_open.Value));
        var clicks = 0;
        var clickFormat = GalleryText.Get("Body button clicks: {0}");
        var action = Fluent.Button(string.Format(clickFormat, clicks));
        action.Activated = () => action.Label = string.Format(clickFormat, ++clicks);
        var options = new UiAccordionOptions(Collapsible : true);
        var sample = GallerySample.Create(() => Fluent.Accordion(GalleryText.Get("Accordion example"),
            new(() => _open.Value, next =>
            {
                _open.Value = next;
                status.Source = formatOpenPanels(next);
            }), new UiAccordionItem[]
            {
                new("input", GalleryText.Get("Keep input text"), Dux.Column(new(Spacing : 8, MainAxisSize : UiMainAxisSize.Min),
                    Fluent.Body1(GalleryText.Get("Check that your input remains after reopening the panel or returning to this page.")),
                    Fluent.Input(GalleryText.Get("Accordion example note"), _memo.Binding, 256, 460))),
                new("button", GalleryText.Get("Body button and keyboard navigation"), Dux.Column(new(Spacing : 8, MainAxisSize : UiMainAxisSize.Min),
                    Fluent.Body1(GalleryText.Get("Use Tab to move from the header into the panel and on to the next header. Press Enter or Space to open or close a panel.")), action)),
                new("disabled", GalleryText.Get("Disabled header"), Fluent.Body1(GalleryText.Get("A disabled header cannot be opened with a pointer, keyboard, or accessibility request.")), false)
            }, 640, options));
        var accordion = sample.Preview;
        var factory = sample.Source;
        sample.BindSource(() => (accordion.Options, Open : _open.Value, Memo : _memo.Value, Clicks : clicks), state =>
        {
            var opened = state.Open.IsEmpty ? "ImmutableArray<string>.Empty"
                : "ImmutableArray.Create<string>(" + string.Join(", ", state.Open.Select(GallerySample.String)) + ")";
            return "using System.Collections.Immutable;\n\n"
                + $"var _open = new UiState<ImmutableArray<string>>({opened});\n"
                + $"var _memo = new UiState<string>({GallerySample.String(state.Memo)});\n"
                + openStatus.Source + "\nvar status = Fluent.Body1(formatOpenPanels(_open.Value));\n"
                + $"var clicks = {state.Clicks};\n"
                + $"var clickFormat = {GallerySample.String(clickFormat)};\n"
                + "var action = Fluent.Button(string.Format(clickFormat, clicks));\n"
                + "action.Activated = () => action.Label = string.Format(clickFormat, ++clicks);\n"
                + $"var options = new UiAccordionOptions(Multiple : {GallerySample.Boolean(state.Options.Multiple)},\n"
                + $"    Collapsible : {GallerySample.Boolean(state.Options.Collapsible)}, Size : UiAccordionSize.{state.Options.Size},\n"
                + $"    ChevronPosition : UiAccordionChevronPosition.{state.Options.ChevronPosition});\n\n"
                + factory;
        });
        _samples.Add(sample);
        var multiple = Fluent.Button(GalleryText.Format("Open multiple panels: {0}", GalleryText.Get("Off")));
        multiple.Activated = () =>
        {
            var enabled = !accordion.Options.Multiple;
            if (!enabled && _open.Value.Length > 1)
            {
                _open.Value = ImmutableArray.Create(_open.Value[0]);
                status.Source = formatOpenPanels(_open.Value);
            }
            accordion.Options = accordion.Options with { Multiple = enabled };
            multiple.Label = GalleryText.Format("Open multiple panels: {0}", GalleryText.Get(enabled ? "On" : "Off"));
        };
        var collapsible = Fluent.Button(GalleryText.Format("Collapse all panels: {0}", GalleryText.Get("Allowed")));
        collapsible.Activated = () =>
        {
            var enabled = !accordion.Options.Collapsible;
            accordion.Options = accordion.Options with { Collapsible = enabled };
            collapsible.Label = GalleryText.Format("Collapse all panels: {0}", GalleryText.Get(enabled ? "Allowed" : "Disallowed"));
        };
        var chevron = Fluent.Button(GalleryText.Format("Chevron: {0}", GalleryText.Get("Leading edge")));
        chevron.Activated = () =>
        {
            var end = accordion.Options.ChevronPosition == UiAccordionChevronPosition.Start;
            accordion.Options = accordion.Options with { ChevronPosition = end ? UiAccordionChevronPosition.End : UiAccordionChevronPosition.Start };
            chevron.Label = GalleryText.Format("Chevron: {0}", GalleryText.Get(end ? "Trailing edge" : "Leading edge"));
        };
        var size = Fluent.Button(SizeLabel(accordion.Options.Size));
        size.Activated = () =>
        {
            var next = (UiAccordionSize)(((int)accordion.Options.Size + 1) % 4);
            accordion.Options = accordion.Options with { Size = next };
            size.Label = SizeLabel(next);
        };
        return Dux.Column(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min),
            Fluent.Body1(GalleryText.Get("Panels start collapsed. Select a header to open its content and enter some text. You can also inspect the accessibility relationship between the header and panel.")),
            Dux.Row(new(Spacing : 8), multiple, collapsible, chevron, size), status, sample);
    });

    private static string SizeLabel(UiAccordionSize size) => GalleryText.Format("Header size: {0}", GalleryText.Get(size switch
    {
        UiAccordionSize.Small => "Small",
        UiAccordionSize.Medium => "Medium",
        UiAccordionSize.Large => "Large",
        UiAccordionSize.ExtraLarge => "Extra large",
        _ => throw new ArgumentOutOfRangeException(nameof(size))
    }));
}
