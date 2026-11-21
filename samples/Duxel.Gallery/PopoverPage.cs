using Duxel.Core;

namespace Duxel.Gallery;

internal sealed class PopoverPage : IObserverPage, IObserverNaturalPage, IGallerySourcePage
{
    private Content? _content;
    public IReadOnlyList<IGallerySample> Samples => (_content ??= new()).Samples;
    public string Title => "Popover";
    public string Description => "Place widgets inside a pop-up. Enter text, move focus, and try closing and reopening the pop-up.";
    internal UiFluentPopover? Preview => _content?.Preview;
    internal string? ModelName => _content?.ModelName;
    internal UiPopover<GalleryPopoverDecoration>? DecorationPreview => _content?.DecorationPreview;
    UiWidget IObserverNaturalPage.MeasureRoot => (_content ??= new()).Root;
    public void Render(UiImmediateContext ui) => (_content ??= new()).Root.Render(ui);
    public void Dispose() => _content?.Dispose();

    private sealed class Content : IDisposable
    {
        private string _name = GalleryText.Get("Dimoi");
        internal string ModelName => _name;
        private int _count;
        private bool _open, _rejectClose;
        internal UiFluentPopover Preview
        {
            get;
        }
        internal UiWidget Root
        {
            get;
        }
        internal UiPopover<GalleryPopoverDecoration> DecorationPreview
        {
            get;
        }
        internal IReadOnlyList<IGallerySample> Samples { get; }
        private string TriggerText => Preview.Options.TriggerMode switch
        {
            UiPopoverTriggerMode.Click => GalleryText.Get("Click"),
            UiPopoverTriggerMode.Hover => GalleryText.Get("Hover"),
            UiPopoverTriggerMode.Context => GalleryText.Get("Right-click"),
            _ => throw new InvalidOperationException("Unknown Popover trigger mode.")
        };
        internal Content()
        {
            var options = new UiPopoverOptions(Width : 360);
            var sample = GallerySample.Create(() =>
            {
                UiFluentPopover? owner = null;
                var input = Fluent.Input(GalleryText.Get("Name in popover"), new UiBinding<string>(() => _name, value => _name = value), 64, 280);
                owner = Fluent.Popover(GalleryText.Get("Open profile editor"), Dux.Column(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min),
                    Fluent.Subtitle2(GalleryText.Get("Edit profile")), Fluent.Label(GalleryText.Get("Name"), input), input,
                    Fluent.Button(GalleryText.Get("Run popover command"), () => _count++),
                    Fluent.Menu(GalleryText.Get("Menu in popover"), [new("execute", GalleryText.Get("Run menu command"))], _ => _count++),
                    Fluent.Button(GalleryText.Get("Close profile editor"), () => owner!.Close())), options : options);
                owner.Open = new(() => _open, value => { if (value || !_rejectClose) _open = value; });
                return owner;
            });
            Preview = sample.Preview;
            var factory = sample.Source;
            sample.BindSource(() => (_name, _count, _open, _rejectClose, Preview.Options), state =>
                $"var _name = {GallerySample.String(state._name)};\nvar _count = {state._count};\n"
                + $"var _open = {GallerySample.Boolean(state._open)};\nvar _rejectClose = {GallerySample.Boolean(state._rejectClose)};\n"
                + $"var options = {PopoverGallerySource.Options(state.Options)};\n\n" + factory);

            var decorationOptions = new UiPopoverOptions(Width : 280, DisableAutoFocus : true);
            var decorationSample = GallerySample.Create(() =>
            {
                UiPopover<GalleryPopoverDecoration>? decoration = null;
                decoration = new(GalleryText.Get("Open wide decorative popover"), Fluent.Button(GalleryText.Get("Close decorative popover"), () => decoration!.Close()), options : decorationOptions);
                return decoration;
            });
            DecorationPreview = decorationSample.Preview;
            var decorationFactory = decorationSample.Source;
            var declaration = GalleryPageSource.Declaration(nameof(GalleryPopoverDecoration));
            decorationSample.BindSource(() => (DecorationPreview.Options, DecorationPreview.IsOpen), state =>
                $"var decorationOptions = {PopoverGallerySource.Options(state.Options)};\n\n" + decorationFactory
                + (state.IsOpen ? "\n((UiPopover<GalleryPopoverDecoration>)example).Show();" : string.Empty)
                + "\n\n" + declaration);
            Samples = [sample, decorationSample];
            Root = Dux.ScrollView(Dux.Column(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min),
                Dux.Wrap(new(Spacing : 8, RunSpacing : 8),
                    Fluent.Button(GalleryText.Get("Toggle focus trap"), () => Preview.Options = Preview.Options with { TrapFocus = !Preview.Options.TrapFocus }),
                    Fluent.Button(GalleryText.Get("Toggle close on focus loss"), () => Preview.Options = Preview.Options with { CloseOnFocusLeave = !Preview.Options.CloseOnFocusLeave }),
                    Fluent.Button(GalleryText.Get("Change popover trigger"), () =>
                    {
                        Preview.Close();
                        var mode = (UiPopoverTriggerMode)(((int)Preview.Options.TriggerMode + 1) % 3);
                        Preview.Options = Preview.Options with
                        {
                            TriggerMode = mode,
                            DisableAutoFocus = mode == UiPopoverTriggerMode.Hover
                        };
                    }),
                    Fluent.Button(GalleryText.Get("Toggle popover arrow"), () => Preview.Options = Preview.Options with { WithArrow = !Preview.Options.WithArrow }),
                    Fluent.Button(GalleryText.Get("Toggle popover animation"), () => Preview.Options = Preview.Options with { MotionEnabled = !Preview.Options.MotionEnabled }),
                    Fluent.Button(GalleryText.Get("Change popover size"), () => Preview.Options = Preview.Options with { Size = (UiPopoverSize)(((int)Preview.Options.Size + 1) % 3) }),
                    Fluent.Button(GalleryText.Get("Change popover appearance"), () => Preview.Options = Preview.Options with { Appearance = (UiPopoverAppearance)(((int)Preview.Options.Appearance + 1) % 3) }),
                    Fluent.Button(GalleryText.Get("Toggle rejection of close"), () => _rejectClose = !_rejectClose)),
                Fluent.Body1(() => GalleryText.Format("Name: {0} / Commands: {1} / Open: {2} / Trigger: {3} / Animation: {4} / Focus trap: {5} / Close on focus loss: {6} / Reject close: {7}",
                    _name, _count, StateText(_open), TriggerText, StateText(Preview.Options.MotionEnabled),
                    StateText(Preview.Options.TrapFocus), StateText(Preview.Options.CloseOnFocusLeave), StateText(_rejectClose))),
                Fluent.Body1(GalleryText.Get("Try running a command from the nested menu. Escape closes the menu first. In hover mode the popover stays open while the pointer is over the menu. Editing state is preserved when you close it or switch pages.")),
                Fluent.Caption1(GalleryText.Get("The default popover is nonmodal, so the background remains interactive. Toggle the arrow and animation and compare sizes and appearances. The trigger cycles through click, hover, and right-click. In right-click mode, use the Menu key or Shift+F10 to open it with the keyboard. The entry transition lasts 400 ms and closing is immediate. Fluent design and behavior alignment is still in progress.")),
                sample,
                Fluent.Subtitle2(GalleryText.Get("Custom surface")),
                Fluent.Caption1(GalleryText.Get("The decoration extends beyond the body and moves and fades with the popover. Click the outer part of the decoration to dismiss it.")),
                decorationSample));
        }
        private static string StateText(bool value) => GalleryText.Get(value ? "On" : "Off");
        public void Dispose() => Root.Dispose();
    }

}
