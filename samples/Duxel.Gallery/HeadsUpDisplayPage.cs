using Duxel.Core;

namespace Duxel.Gallery;

internal sealed class HeadsUpDisplayPage : IObserverPage, IObserverNaturalPage, IGallerySourcePage
{
    private Content? _content;
    public string Title => "HeadsUpDisplay";
    public string Description => "Show task status at the center of the screen. Press to dismiss progress notifications. Success and failure notifications close after one second.";
    public IReadOnlyList<IGallerySample> Samples => (_content ??= new()).Samples;
    UiWidget IObserverNaturalPage.MeasureRoot => (_content ??= new()).Root;
    public void Render(UiImmediateContext ui) => (_content ??= new()).Root.Render(ui);
    public void Dispose() => _content?.Root.Dispose();
    private sealed class Content
    {
        internal List<IGallerySample> Samples { get; } = [];
        internal UiWidget Root { get; }
        internal Content()
        {
            var previews = new List<UiHeadsUpDisplayContent>();
            var examples = new List<UiWidget>();
            foreach (var kind in Enum.GetValues<UiHeadsUpDisplayKind>())
            {
                var options = new UiHeadsUpDisplayContentOptions(kind, kind switch
                {
                    UiHeadsUpDisplayKind.Activity => GalleryText.Get("Saving"),
                    UiHeadsUpDisplayKind.Success => GalleryText.Get("Save complete"),
                    UiHeadsUpDisplayKind.Failure => GalleryText.Get("Save failed"),
                    UiHeadsUpDisplayKind.Custom => GalleryText.Get("Custom icon"),
                    _ => throw new ArgumentOutOfRangeException(nameof(kind))
                }, kind == UiHeadsUpDisplayKind.Custom ? UiMenuIcon.Create<StatusIcon, StatusIcon>() : default);
                var sample = GallerySample.Create(() => new UiHeadsUpDisplayContent(options));
                previews.Add(sample.Preview);
                sample.BindSource(() => sample.Preview.Options, state =>
                    "var options = new UiHeadsUpDisplayContentOptions(\n    Kind : UiHeadsUpDisplayKind." + state.Kind
                    + ",\n    Label : " + GallerySample.String(state.Label)
                    + ",\n    ReducedMotion : " + GallerySample.Boolean(state.ReducedMotion)
                    + (state.Kind == UiHeadsUpDisplayKind.Custom ? ",\n    Icon : UiMenuIcon.Create<StatusIcon, StatusIcon>()" : string.Empty)
                    + ");\nvar content = new UiHeadsUpDisplayContent(options);"
                    + (state.Kind == UiHeadsUpDisplayKind.Custom ? "\n\n" + IconSource : string.Empty));
                Samples.Add(sample);
                examples.Add(Dux.Column(new(Spacing : 8, MainAxisSize : UiMainAxisSize.Min), Fluent.Subtitle2(kind switch
                {
                    UiHeadsUpDisplayKind.Activity => GalleryText.Get("Activity preview"),
                    UiHeadsUpDisplayKind.Success => GalleryText.Get("Success preview"),
                    UiHeadsUpDisplayKind.Failure => GalleryText.Get("Failure preview"),
                    UiHeadsUpDisplayKind.Custom => GalleryText.Get("Custom icon preview"),
                    _ => throw new ArgumentOutOfRangeException(nameof(kind))
                }), sample));
            }
            var portalSample = GallerySample.Create(() => Fluent.HeadsUpDisplay(new(Label : GalleryText.Get("Saving"))));
            var portal = portalSample.Preview;
            portal.Content.Tapped = () => portal.Hide();
            portalSample.BindSource(() => (Content : portal.Content.Options, Options : portal.Options), state =>
                "var content = new UiHeadsUpDisplayContentOptions(\n    Kind : UiHeadsUpDisplayKind." + state.Content.Kind
                + ", Label : " + GallerySample.String(state.Content.Label) + ");\n"
                + "var options = new UiHeadsUpDisplayOptions(\n    IsBlocking : " + GallerySample.Boolean(state.Options.IsBlocking)
                + ", IsPersistent : " + GallerySample.Boolean(state.Options.IsPersistent)
                + ",\n    ReducedMotion : " + GallerySample.Boolean(state.Options.ReducedMotion)
                + ", KeyboardHeight : " + state.Options.KeyboardHeight.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + ");\nvar hud = Fluent.HeadsUpDisplay(content, options);\nhud.Content.Tapped = () => hud.Hide();\n"
                + "var show = Fluent.Button(" + GallerySample.String(GalleryText.Get("Show")) + ", hud.Show);\nvar example = Dux.Column(new(Spacing : 8, MainAxisSize : UiMainAxisSize.Min), show, hud);");
            Samples.Insert(0, portalSample);
            examples.Insert(0, Dux.Column(new(Spacing : 8, MainAxisSize : UiMainAxisSize.Min),
                Fluent.Subtitle2(GalleryText.Get("Show at screen center")),
                Dux.Wrap(new(Spacing : 8, RunSpacing : 8),
                    Fluent.Button(GalleryText.Get("Show progress"), () =>
                    {
                        portal.Content.Options = new(Label : GalleryText.Get("Saving"));
                        portal.Options = portal.Options with { IsPersistent = true };
                        portal.Show();
                    }),
                    Fluent.Button(GalleryText.Get("Show success"), () => portal.ShowSuccess(GalleryText.Get("Save complete"))),
                    Fluent.Button(GalleryText.Get("Show failure"), () => portal.ShowFailure(GalleryText.Get("Save failed"))),
                    Fluent.Button(GalleryText.Get("Toggle HUD input blocking"), () => portal.Options = portal.Options with { IsBlocking = !portal.Options.IsBlocking }),
                    Fluent.Button(GalleryText.Get("Simulate on-screen keyboard area"), () => portal.Options = portal.Options with
                    { KeyboardHeight = portal.Options.KeyboardHeight == 0 ? 200 : 0 })),
                portalSample));
            Root = Dux.Column(new(Spacing : 16, MainAxisSize : UiMainAxisSize.Min),
                Fluent.Body1(GalleryText.Get("Compare the centered display in each state. Keyboard simulation reserves space at the bottom to test layout without opening a keyboard.")),
                Fluent.Button(GalleryText.Get("Reduce HUD motion"), () =>
                {
                    portal.Options = portal.Options with { ReducedMotion = !portal.Options.ReducedMotion };
                    previews.ForEach(preview => preview.Options = preview.Options with { ReducedMotion = portal.Options.ReducedMotion });
                }),
                Dux.Column(new(Spacing : 16, MainAxisSize : UiMainAxisSize.Min), examples.ToArray()));
        }
    }
    private readonly struct StatusIcon : IUiButtonIcon
    {
        public static void Paint(UiControlContext context, UiRect bounds, UiColor color, UiButtonInteraction interaction) =>
            context.Circle(new(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2), 12, color);
    }
    private const string IconSource = """
        readonly struct StatusIcon : IUiButtonIcon
        {
            public static void Paint(UiControlContext context, UiRect bounds, UiColor color,
                UiButtonInteraction interaction) =>
                context.Circle(new(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2), 12, color);
        }
        """;
}
