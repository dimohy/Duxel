using Duxel.Core;
using InputPresentation = Duxel.Core.UiFluentInputPresentation<Duxel.Core.UiFluentMediumInputSize, Duxel.Core.UiFluentOutlineInput>;
using FieldPresentation = Duxel.Core.UiFluentFieldPresentation<Duxel.Core.UiFluentMediumFieldSize, Duxel.Core.UiVerticalFieldLayout>;

internal static class FormLayoutRetentionTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        var settings = UiTextSettings.Default;
        void Frame(Action<UiImmediateContext> render, UiInputState? input = null)
        {
            context.NewFrame(new(1f / 60, new(640, 480), new(1, 1)), input ?? idle, new(0, 0, 640, 480), settings);
            context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding: 0); render(ui); }));
            using var data = context.GetDrawData();
        }
        var value = "a";
        var reads = 0;
        var writes = 0;
        var input = new MeasuredInput(new(() => { reads++; return value; }, text => { writes++; value = text; }));
        var slot = new OpaqueButton();
        input.AfterSlot = new("action", slot, new(24, 24));
        var divider = new MeasuredDivider();
        var checkedState = new UiState<bool>(false);
        var menu = new MeasuredMenu(checkedState.Binding);
        var field = new MeasuredField(new MeasuredInput(new UiState<string>("Field value").Binding));
        using var parent = Dux.Column(new(Spacing: 6, MainAxisSize: UiMainAxisSize.Min), input, divider, menu, field);
        void Measure(UiImmediateContext ui) => parent.MeasureConstrained(ui, UiBoxConstraints.Loose(new(500, 450)));
        for (var i = 0; i < 3; i++)
            Frame(Measure);
        int Measurements() => input.Measurements + divider.Measurements + menu.Measurements + field.Measurements;
        var measured = Measurements();
        for (var i = 0; i < 8; i++)
            Frame(Measure);
        check(Measurements() == measured, "Input, static Divider, Menu trigger and deterministic Field reuse their existing parent measurement");
        check(reads == 0 && writes == 0 && slot.Measurements == 0,
            "retained form measurement neither reads editor values nor measures explicitly allocated opaque input slots");
        Frame(parent.Render);
        Frame(parent.Render);
        check(reads == 2 && input.Renders == 2 && divider.Renders == 2 && menu.Renders == 2 && slot.Renders == 2,
            "retained forms continue real editor getters, slot children, divider and menu rendering each frame");
        var pointer = new UiVector2(input.Bounds.X + 30, input.Bounds.Y + input.Bounds.Height / 2);
        Frame(parent.Render, idle with
        {
            MousePosition = pointer,
            LeftMouseDown = true,
            LeftMousePressed = true
        });
        Frame(parent.Render, idle with
        {
            MousePosition = pointer,
            LeftMouseReleased = true
        });
        Frame(parent.Render, idle with
        {
            KeyEvents = [new(UiKey.End, true, default)],
            CharEvents = [new('x')]
        });
        check(value == "ax" && writes == 1, "retained Input parent preserves actual editor focus and caller binding updates");
        Frame(parent.Render, idle with
        {
            KeyEvents = [new(UiKey.Z, true, KeyModifiers.Ctrl)],
            Modifiers = KeyModifiers.Ctrl
        });
        check(value == "a", "retained Input parent preserves its owned undo history");
        measured = input.Measurements;
        input.Options = input.Options with
        {
            ContentBefore = "Prefix"
        };
        Frame(Measure);
        check(input.Measurements > measured, "input adornment options invalidate the owned parent measurement");
        measured = input.Measurements;
        input.AfterSlot = input.AfterSlot!.Value with
        {
            Size = new(24, 40)
        };
        Frame(Measure);
        check(input.Measurements > measured, "explicit input slot allocation changes invalidate parent measurement");
        measured = divider.Measurements;
        divider.Source = "Changed divider";
        Frame(Measure);
        check(divider.Measurements > measured, "static Divider source changes invalidate parent measurement");
        measured = field.Measurements;
        field.Options = new()
        {
            Label = "Changed field",
            Hint = "Hint"
        };
        Frame(Measure);
        check(field.Measurements > measured, "Field options invalidate parent measurement without caching editing state");
        measured = menu.Measurements;
        menu.SetItems([UiMenuOption.Checkbox("check", "Current check", checkedState.Binding)]);
        Frame(Measure);
        check(menu.Measurements > measured, "menu item replacement invalidates the original parent ownership chain");
        measured = Measurements();
        settings = settings with
        {
            Scale = 1.25f
        };
        Frame(Measure);
        check(Measurements() > measured, "text environment changes invalidate all participating form families");
        var atlasObserverCalls = 0;
        settings = UiTextSettings.Default with
        {
            MissingGlyphObserver = _ => atlasObserverCalls++
        };
        Frame(Measure);
        measured = Measurements();
        Frame(Measure);
        check(Measurements() == measured && atlasObserverCalls == 0, "native form measurement retains reuse without atlas observer callbacks");
        settings = UiTextSettings.Default;
        var dynamicText = "First";
        using var dynamicDivider = new UiDivider<Func<string>, UiGetterTextSource, UiFluentDividerPresentation>(() => dynamicText);
        check(!dynamicDivider.RetainsMeasurement, "getter Divider remains dynamic without an immutable-source promise");
        using var customInput = new UiInput<UiFluentTextInputBehavior, UiFluentInputPresentation<MutableSize, UiFluentOutlineInput>>("Dynamic", new UiState<string>("").Binding, 64, 180);
        using var customParent = Dux.Wrap(default, customInput);
        UiVector2 size = default;
        void CustomMeasure(UiImmediateContext ui) => size = customParent.MeasureConstrained(ui, UiBoxConstraints.Loose(new(400, 400)));
        MutableSize.HeightValue = 32;
        Frame(CustomMeasure);
        var oldSize = size;
        MutableSize.HeightValue = 60;
        Frame(CustomMeasure);
        check(!customInput.RetainsMeasurement && size.Y > oldSize.Y, "custom Input size traits remain dynamically measured by default");
        using var customAdornment = new UiInput<UiFluentTextInputBehavior,
            UiFluentInputPresentation<UiFluentMediumInputSize, UiFluentOutlineInput, MutableAdornment, UiInputTextAdornment>>(
                "Dynamic adornment", new UiState<string>("").Binding, 64, 180);
        using var adornmentParent = Dux.Wrap(default, customAdornment);
        MutableAdornment.Height = 20;
        Frame(ui => size = adornmentParent.MeasureConstrained(ui, UiBoxConstraints.Loose(new(400, 400))));
        oldSize = size;
        MutableAdornment.Height = 80;
        Frame(ui => size = adornmentParent.MeasureConstrained(ui, UiBoxConstraints.Loose(new(400, 400))));
        check(!customAdornment.RetainsMeasurement && size.Y > oldSize.Y,
            "custom Input adornment metrics remain live without a deterministic promise");
        using var customMenu = new UiMenu<MutableMenu>("Dynamic menu", [new("item", "Item")], static _ => { });
        using var menuParent = Dux.Wrap(default, customMenu);
        MutableMenu.Width = 80;
        Frame(ui => size = menuParent.MeasureConstrained(ui, UiBoxConstraints.Loose(new(400, 400))));
        oldSize = size;
        MutableMenu.Width = 160;
        Frame(ui => size = menuParent.MeasureConstrained(ui, UiBoxConstraints.Loose(new(400, 400))));
        check(!customMenu.RetainsMeasurement && size.X > oldSize.X,
            "custom Menu trigger measurements remain live without an explicit promise");
        using var opaqueField = new UiField<UiLabelFieldBehavior, FieldPresentation>(new OpaqueButton(), new(120, 32));
        check(!opaqueField.RetainsMeasurement, "explicit Field control dimensions do not imply an opaque child's measurement consent");
    }
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
    private sealed class MeasuredInput(UiBinding<string> value) : UiInput<UiFluentTextInputBehavior, InputPresentation>("Retained editor", value, 128, 260)
    {
        internal int Measurements, Renders;
        internal UiRect Bounds;
        protected override UiVector2 MeasureContent(UiControlContext context)
        {
            Measurements++;
            return base.MeasureContent(context);
        }
        protected override void RenderControl(UiImmediateContext ui)
        {
            Renders++;
            base.RenderControl(ui);
            var p = ui.GetItemRectMin();
            var s = ui.GetItemRectSize();
            Bounds = new(p.X, p.Y, s.X, s.Y);
        }
    }
    private sealed class MeasuredDivider() : UiDivider<UiFluentDividerPresentation>("Divider")
    {
        internal int Measurements, Renders;
        protected override UiVector2 MeasureContent(UiControlContext context)
        {
            Measurements++;
            return base.MeasureContent(context);
        }
        protected override void RenderControl(UiImmediateContext ui)
        {
            Renders++;
            base.RenderControl(ui);
        }
    }
    private sealed class MeasuredMenu(UiBinding<bool> state) : UiMenu<UiFluentMenuPresentation>("Retained menu", [UiMenuOption.Checkbox("check", "Current check", state)], static _ => { })
    {
        internal int Measurements, Renders;
        protected override UiVector2 MeasureContent(UiControlContext context)
        {
            Measurements++;
            return base.MeasureContent(context);
        }
        protected override void RenderControl(UiImmediateContext ui)
        {
            Renders++;
            base.RenderControl(ui);
        }
    }
    private sealed class MeasuredField(UiWidget child) : UiField<UiLabelFieldBehavior, FieldPresentation>(child)
    {
        internal int Measurements;
        protected override UiVector2 MeasureContent(UiControlContext context)
        {
            Measurements++;
            return base.MeasureContent(context);
        }
    }
    private sealed class OpaqueButton() : UiFluentButton("Slot")
    {
        internal int Measurements, Renders;
        protected override bool CanRetainMeasurement => false;
        protected override UiVector2 MeasureContent(UiControlContext context)
        {
            Measurements++;
            return base.MeasureContent(context);
        }
        protected override void RenderControl(UiImmediateContext ui)
        {
            Renders++;
            base.RenderControl(ui);
        }
    }
    private readonly struct MutableSize : IUiInputSize
    {
        internal static float HeightValue;
        public static float Height => HeightValue;
        public static float Padding => 12;
        public static UiControlTypographyScope BeginText(UiControlContext context) => context.BeginTypography<UiRegularTypeface, UiFluentType300>();
    }
    private readonly struct MutableAdornment : IUiInputAdornment
    {
        internal static float Height;
        public static bool IsPresent(string? content) => true;
        public static UiVector2 Measure(UiControlContext context, string? content, float iconSize) => new(20, Height);
        public static void Paint(UiControlContext context, string? content, UiRect bounds, UiColor color, in UiInputEditResult edit)
        {
        }
    }
    private readonly struct MutableMenu : IUiMenuPresentation
    {
        internal static float Width;
        public static UiVector2 MeasureTrigger(UiControlContext context, string label) => new(Width, 32);
        public static void PaintTrigger(UiControlContext context, string label, UiRect bounds, UiButtonInteraction interaction) =>
            UiFluentMenuPresentation.PaintTrigger(context, label, bounds, interaction);
        public static float MeasureItem(UiControlContext context, string label) => UiFluentMenuPresentation.MeasureItem(context, label);
        public static void PaintSurface(UiControlContext context, UiRect bounds) => UiFluentMenuPresentation.PaintSurface(context, bounds);
        public static void PaintItem(UiControlContext context, string label, UiRect bounds, UiButtonInteraction interaction, UiMenuOptions options) =>
            UiFluentMenuPresentation.PaintItem(context, label, bounds, interaction, options);
    }
}
