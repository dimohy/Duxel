using Duxel.Core;

namespace Duxel.Gallery;

[UiFluentIcon("calendar", UiFluentIconStyle.Regular, 20)]
internal readonly partial struct SegmentCalendarIcon;
[UiFluentIcon("mail", UiFluentIconStyle.Regular, 20)]
internal readonly partial struct SegmentMailIcon;

internal sealed class SegmentedControlPage : IObserverPage, IObserverNaturalPage, IGallerySourcePage
{
    private Content? _content;
    public string Title => "SegmentedControl";
    public string Description => "Choose one of several items. Compare four styles and try dragging and wheel scrolling. A fade marks sides with hidden items.";
    public IReadOnlyList<IGallerySample> Samples => (_content ??= new()).Samples;
    internal UiSegmentedControlWidget Preview(int index) => (_content ??= new()).Previews[index];
    UiWidget IObserverNaturalPage.MeasureRoot => (_content ??= new()).Root;
    public void Render(UiImmediateContext ui) => (_content ??= new()).Root.Render(ui);
    public void Dispose() => _content?.Root.Dispose();
    private sealed class Content
    {
        internal List<IGallerySample> Samples { get; } = [];
        internal List<UiSegmentedControlWidget> Previews { get; } = [];
        internal UiWidget Root { get; }
        internal Content() => Root = Dux.Column(new(Spacing : 20, MainAxisSize : UiMainAxisSize.Min),
            Fluent.Body1(GalleryText.Get("Try selection, keyboard navigation, right-to-left layout, icons, unread markers, and four styles. Horizontal scrolling reuses cached content and fades only edges with hidden items.")),
            Example(GalleryText.Get("Neutral navigation bar"), [new("day", GalleryText.Get("Day")), new("week", GalleryText.Get("Week"), false), new("month", GalleryText.Get("Month"), Unread : true)], false,
                UiSegmentedControlStyle.NeutralOverNavBarPill),
            Example(GalleryText.Get("Neutral canvas"), [new("all", GalleryText.Get("All")), new("assigned", GalleryText.Get("Assigned to me")), new("saved", GalleryText.Get("Saved items"))], false,
                UiSegmentedControlStyle.NeutralOverCanvasPill),
            Example(GalleryText.Get("Brand navigation bar"), [new("calendar", GalleryText.Get("Calendar"), Icon : UiMenuIcon.Create<SegmentCalendarIcon, SegmentCalendarIcon>()),
                new("mail", GalleryText.Get("Mail"), Icon : UiMenuIcon.Create<SegmentMailIcon, SegmentMailIcon>(), Unread : true)], true,
                UiSegmentedControlStyle.BrandOverNavBarPill),
            Example(GalleryText.Get("Brand canvas"), [new("all", GalleryText.Get("All")), new("active", GalleryText.Get("In progress")), new("done", GalleryText.Get("Done"), Unread : true)], false,
                UiSegmentedControlStyle.BrandOverCanvasPill),
            Example(GalleryText.Get("Horizontal scroll"), Enumerable.Range(1, 10).Select(i => new UiSegmentOption(i.ToString(), GalleryText.Format("Option {0}", i))).ToArray(), false,
                UiSegmentedControlStyle.NeutralOverCanvasPill, scrolling : true));
        private UiWidget Example(string label, UiSegmentOption[] items, bool icons, UiSegmentedControlStyle style, bool scrolling = false)
        {
            var state = new UiState<string?>(items[0].Value);
            var sample = GallerySample.Create(() => Fluent.SegmentedControl(label, state.Binding, items,
                new(Style : style, UnreadLabel : GalleryText.Get("Unread"), IsFixedWidth : !scrolling)));
            var preview = sample.Preview;
            Previews.Add(preview);
            sample.BindSource(() => (preview.Options, state.Value, preview.ContentVersion), value =>
            {
                var rows = string.Join(",\n    ", preview.Items.ToArray().Select(item => "new(" + GallerySample.String(item.Value) + ", "
                    + GallerySample.String(item.Label) + ", Enabled : " + GallerySample.Boolean(item.Enabled)
                    + (item.Icon.IsEmpty ? "" : ", Icon : UiMenuIcon.Create<Segment" + (item.Value == "calendar" ? "Calendar" : "Mail")
                        + "Icon, Segment" + (item.Value == "calendar" ? "Calendar" : "Mail") + "Icon>()")
                    + ", Unread : " + GallerySample.Boolean(item.Unread) + ")"));
                return (icons ? "// Icons are generated from [UiFluentIcon] declarations in this sample.\n" : "")
                    + "var value = new UiState<string?>(" + GallerySample.String(value.Value) + ");\n"
                    + "UiSegmentOption[] items = [\n    " + rows + "\n];\n"
                    + "var control = Fluent.SegmentedControl(" + GallerySample.String(label) + ", value.Binding, items,\n"
                    + "    new(Width : " + GallerySample.Number(value.Options.Width) + ", EqualWidth : " + GallerySample.Boolean(value.Options.EqualWidth)
                    + ", RightToLeft : " + GallerySample.Boolean(value.Options.RightToLeft) + ", ReducedMotion : " + GallerySample.Boolean(value.Options.ReducedMotion)
                    + ",\n        Style : UiSegmentedControlStyle." + value.Options.Style + ", HorizontalInset : " + GallerySample.Number(value.Options.HorizontalInset)
                    + ", UnreadLabel : " + GallerySample.String(value.Options.UnreadLabel)
                    + ", IsFixedWidth : " + GallerySample.Boolean(value.Options.IsFixedWidth) + "));"
                    + (icons ? "\n\n[UiFluentIcon(\"calendar\", UiFluentIconStyle.Regular, 20)]\ninternal readonly partial struct SegmentCalendarIcon;\n"
                        + "[UiFluentIcon(\"mail\", UiFluentIconStyle.Regular, 20)]\ninternal readonly partial struct SegmentMailIcon;" : "");
            });
            Samples.Add(sample);
            return Dux.Column(new(Spacing : 8, MainAxisSize : UiMainAxisSize.Min), Fluent.Subtitle2(label),
                Dux.Wrap(new(Spacing : 8, RunSpacing : 8),
                    Fluent.Button(GalleryText.Format("Change width for {0}", label), () => preview.Options = preview.Options with { Width = preview.Options.Width == 320 ? 480 : 320 }),
                    Fluent.Button(GalleryText.Format("Toggle equal widths for {0}", label), () => preview.Options = preview.Options with { EqualWidth = !preview.Options.EqualWidth }),
                    Fluent.Button(GalleryText.Format("Reverse direction for {0}", label), () => preview.Options = preview.Options with { RightToLeft = !preview.Options.RightToLeft }),
                    Fluent.Button(GalleryText.Format("Change style for {0}", label), () => preview.Options = preview.Options with
                    { Style = (UiSegmentedControlStyle)(((int)preview.Options.Style + 1) % 4) }),
                    Fluent.Button(GalleryText.Format("Toggle reduced motion for {0}", label), () => preview.Options = preview.Options with { ReducedMotion = !preview.Options.ReducedMotion })),
                Fluent.Body1(() => GalleryText.Get("Selected: ") + state.Value), sample);
        }
    }
}
