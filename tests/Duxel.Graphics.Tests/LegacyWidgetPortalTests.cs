using Duxel.Core;

internal static class LegacyWidgetPortalTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        using var owner = new Owner { Transform = new(RotationDegrees: 90) };
        UiWidget current = owner;
        var idle = new UiInputState(new(200, 100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        var view = new View(ui =>
        {
            ui.EnableRootViewportContentLayout(contentPadding: 0);
            ui.SetCursorScreenPos(new(100, 60));
            current.Render(ui);
        });
        void Frame(UiInputState? input = null)
        {
            context.NewFrame(new(1f / 60, new(640, 480), new(1, 1)), input ?? idle, new(0, 0, 640, 480), UiTextSettings.Default);
            context.Render(view);
            using var data = context.GetDrawData();
        }
        Frame();
        var node = tree.CaptureSnapshot().Elements.Single(e => e.Node.Name == "Legacy child").Node;
        check(node.Bounds.X >= 260 && node.Bounds.X < 300 && node.Bounds.Y >= 180 && node.Bounds.Y < 210,
            "legacy BeginPopup uses explicit display placement inside a rotated widget");
        var point = new UiVector2(node.Bounds.X + node.Bounds.Width / 2, node.Bounds.Y + node.Bounds.Height / 2);
        Frame(idle with
        {
            MousePosition = point,
            LeftMouseDown = true,
            LeftMousePressed = true
        });
        Frame(idle with
        {
            MousePosition = point,
            LeftMouseReleased = true
        });
        check(owner.Clicks == 1, "legacy popup child uses the actual screen pointer");
        check(owner.PointerAfter == owner.PointerBefore, "EndPopup restores the owner's local pointer coordinates");
        for (var mode = 0; mode < 6; mode++)
            RunAdditional(check, mode);
        var executions = 0;
        using var preview = new Duxel.Gallery.LegacyPortalPreview(() => executions++)
        {
            Transform = new(RotationDegrees: 90)
        };
        current = preview;
        Frame();
        Frame();
        void ClickPreview(string label)
        {
            var bounds = tree.CaptureSnapshot().Elements.Single(e => e.Node.Name == label).Node.Bounds;
            var position = new UiVector2(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
            Frame(idle with
            {
                MousePosition = position,
                LeftMouseDown = true,
                LeftMousePressed = true
            });
            Frame(idle with
            {
                MousePosition = position,
                LeftMouseReleased = true
            });
        }
        ClickPreview("기존 팝업 API");
        check(tree.CaptureSnapshot().Elements.Any(e => e.Node.Name == "기존 팝업에서 실행"),
            "actual Observer compatibility sample opens from its transformed Fluent trigger");
        ClickPreview("기존 팝업에서 실행");
        check(executions == 1, "actual Observer compatibility sample executes its popup action");
        ClickPreview("기존 팝업 닫기");
        Frame();
        check(!tree.CaptureSnapshot().Elements.Any(e => e.Node.Name == "기존 팝업에서 실행"),
            "actual Observer compatibility sample closes and removes its popup content");
    }

    private static void RunAdditional(Action<bool, string> check, int mode)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        using var owner = new AdditionalOwner(mode) { Transform = new(RotationDegrees: 90), Spacing = context.GetStyleData().ItemSpacing.Y };
        var idle = new UiInputState(new(200, 100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        var view = new View(ui =>
        {
            ui.EnableRootViewportContentLayout(contentPadding: 0);
            ui.SetCursorScreenPos(new(100, 60));
            owner.Render(ui);
        });
        void Frame(UiInputState? input = null)
        {
            context.NewFrame(new(1f / 60, new(640, 480), new(1, 1)), input ?? idle, new(0, 0, 640, 480), UiTextSettings.Default);
            context.Render(view);
            using var data = context.GetDrawData();
        }
        void Click(UiVector2 point)
        {
            Frame(idle with
            {
                MousePosition = point,
                LeftMouseDown = true,
                LeftMousePressed = true
            });
            Frame(idle with
            {
                MousePosition = point,
                LeftMouseReleased = true
            });
        }
        Frame();
        if (mode == 0)
            check(owner.PopupStart == new UiVector2(222, 122) && owner.PointerInside == idle.MousePosition,
                "BeginTooltip positions content from the screen pointer under a rotated owner");
        else
        {
            if (mode >= 2)
            {
                var triggerWidth = mode == 5 ? 160 : 100;
                Click(new(140 - owner.Height / 2, 60 + triggerWidth / 2));
            }
            if (mode is >= 2 and <= 4)
            {
                check(context.State.NextScheduledFrameTimeSeconds.HasValue,
                    "legacy Combo schedules its disclosure when opened : " + mode);
                for (var frame = 0; frame < 12; frame++)
                    Frame();
                check(context.State.NextScheduledFrameTimeSeconds is null,
                    "legacy Combo returns to event wait once the chevron settles : " + mode);
            }
            if (mode is 3 or 4)
            {
                Click(new(140 - owner.Height + 50, 160 + owner.Spacing + owner.Height * 1.5f));
                check(owner.Index == 1, "legacy Combo screen pointer selects the second row : " + mode);
                check(context.State.NextScheduledFrameTimeSeconds.HasValue,
                    "legacy Combo schedules its disclosure when selection closes it : " + mode);
            }
            else
            {
                var node = tree.CaptureSnapshot().Elements.Single(e => e.Node.Name == "Additional child").Node;
                check(node.Bounds.Width > node.Bounds.Height && node.Bounds.X > 90 && node.Bounds.Y > 100,
                    "legacy popup content publishes upright screen geometry : " + mode);
                Click(new(node.Bounds.X + node.Bounds.Width / 2, node.Bounds.Y + node.Bounds.Height / 2));
                check(owner.Clicks == 1, "legacy modal/BeginCombo/menu child uses the screen pointer : " + mode);
            }
        }
        check(owner.PointerBefore == owner.PointerAfter, "legacy popup exit restores local coordinates : " + mode);
    }

    private sealed class AdditionalOwner : UiControlWidget
    {
        internal int Index, Clicks;
        internal float Height, Spacing;
        internal UiVector2 PointerBefore, PointerAfter, PointerInside, PopupStart;
        private bool _modal = true;
        private readonly int mode;
        private readonly UiWidget _button;
        internal AdditionalOwner(int value)
        {
            mode = value;
            _button = Fluent.Button("Additional child", () => Clicks++);
        }
        protected override UiVector2 MeasureContent(UiControlContext context) => new(200, 40);
        protected override void RenderControl(UiImmediateContext ui)
        {
            PointerBefore = ui.GetMousePos();
            Height = ui.GetFrameHeight();
            if (mode == 0)
            {
                if (ui.BeginTooltip())
                {
                    PopupStart = ui.GetCursorScreenPos();
                    PointerInside = ui.GetMousePos();
                    ui.Dummy(new(40, 20));
                    ui.EndTooltip();
                }
            }
            else if (mode == 1)
            {
                if (ui.BeginPopupModal("legacy-modal", ref _modal))
                {
                    _button.Render(ui);
                    ui.EndPopupModal(ref _modal);
                }
            }
            else if (mode == 2)
            {
                ui.SetNextItemWidth(100);
                if (ui.BeginCombo("Value", 2, "begin-combo"))
                {
                    _button.Render(ui);
                    ui.EndCombo();
                }
            }
            else if (mode == 3)
            {
                ui.SetNextItemWidth(100);
                ui.Combo(ref Index, ["Alpha", "Beta"], 2, "array-combo");
            }
            else if (mode == 4)
            {
                ui.SetNextItemWidth(100);
                ui.Combo(ref Index, 2, static i => i == 0 ? "Alpha" : "Beta", 2, "getter-combo");
            }
            else if (ui.BeginMenu("Legacy menu"))
            {
                _button.Render(ui);
                ui.EndMenu();
            }
            PointerAfter = ui.GetMousePos();
        }
        private protected override void ReleaseOwnedResources()
        {
            _button.Dispose();
            base.ReleaseOwnedResources();
        }
    }
    private sealed class Owner : UiControlWidget
    {
        internal int Clicks;
        internal UiVector2 PointerBefore, PointerAfter;
        private bool _opened;
        private readonly UiWidget _button;
        internal Owner() => _button = Fluent.Button("Legacy child", () => Clicks++);
        protected override UiVector2 MeasureContent(UiControlContext context) => new(100, 40);
        protected override void RenderControl(UiImmediateContext ui)
        {
            ui.Dummy(new(100, 40));
            PointerBefore = ui.GetMousePos();
            if (!_opened)
            {
                ui.OpenPopup("legacy-widget-popup");
                _opened = true;
            }
            ui.SetNextPopupPosition(new(260, 180));
            if (ui.BeginPopup("legacy-widget-popup", UiPopupInputMode.NonModal))
            {
                _button.Render(ui);
                ui.EndPopup();
            }
            PointerAfter = ui.GetMousePos();
        }
        private protected override void ReleaseOwnedResources()
        {
            _button.Dispose();
            base.ReleaseOwnedResources();
        }
    }
    private sealed class View(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
