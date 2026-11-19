// FBA: shared 3D renderer with a global camera and independent widget cameras.
#:property TargetFramework=net10.0
#:property platform=windows
#:package Duxel.$(platform).App@*-*

using System.Globalization;
using System.Numerics;
using Duxel.App;
using Duxel.Core;

DuxelApp.Run(new DuxelAppOptions
{
    Window = new() { Title = "Duxel - global and widget cameras", Width = 1280, Height = 800, VSync = false },
    Renderer = new() { MsaaSamples = 1 },
    Screen = new LayerCameraScreen(),
});

public sealed class LayerCameraScreen : UiScreen
{
    private sealed class Camera
    {
        public float Distance = 2.8f;
        public float TargetDistance = 2.8f;
        public Vector2 Pan;
        public Vector2 TargetPan;

        public Matrix4x4 Update(float delta, float aspect)
        {
            var amount = 1f - MathF.Exp(-12f * delta);
            Distance += (TargetDistance - Distance) * amount;
            Pan = Vector2.Lerp(Pan, TargetPan, amount);
            return Matrix4x4.CreateLookAt(new(Pan.X, Pan.Y, Distance), new(Pan.X, Pan.Y, 0), Vector3.UnitY)
                * Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 3, aspect, .1f, 30);
        }
    }

    private readonly Camera[] _cameras = [new(), new(), new()];
    private readonly UiTextureId _image = new(500_001);
    private readonly string[] _worldIds = ["camera-world-0", "camera-world-1", "camera-world-2"];
    private bool _imageCreated;
    private bool _animateGeometry;
    private bool _wasDragging;
    private UiVector2 _previousMouse;
    private UiVector2 _lastSize;
    private UiRect _controls;
    private long _revision;
    private int _builds;
    private double _lastTime;
    private readonly double _autoExitSeconds = BenchOptions.ReadDouble("DUXEL_CAMERA_DEMO_SECONDS", 0, 0);

    public override void Render(UiImmediateContext ui)
    {
        var viewport = ui.GetMainViewport();
        var mouse = ui.GetMousePos();
        var displaySize = new UiVector2(viewport.Pos.X + viewport.Size.X, viewport.Pos.Y + viewport.Size.Y);
        var now = ui.GetTime();
        var delta = (float)Math.Clamp(_lastTime == 0 ? .016 : now - _lastTime, 0, .05);
        _lastTime = now;
        if (_autoExitSeconds > 0 && now >= _autoExitSeconds) { DuxelApp.Exit(); return; }
        var display = new UiRect(0, 0, displaySize.X, displaySize.Y);
        if (_lastSize != displaySize) { _revision++; _lastSize = displaySize; }
        if (!_imageCreated)
        {
            var pixels = new byte[32 * 32 * 4];
            for (var y = 0; y < 32; y++)
            for (var x = 0; x < 32; x++)
            {
                var index = (y * 32 + x) * 4;
                var value = ((x / 4 + y / 4) & 1) == 0 ? (byte)245 : (byte)45;
                pixels[index] = pixels[index + 1] = pixels[index + 2] = value;
                pixels[index + 3] = 255;
            }
            ui.QueueTextureUpdate(new(UiTextureUpdateKind.Create, _image, UiTextureFormat.Rgba8Unorm, 32, 32, pixels));
            _imageCreated = true;
        }

        var panelWidth = MathF.Min(340, viewport.WorkSize.X * .3f);
        var panelHeight = MathF.Min(230, (viewport.WorkSize.Y - 60) * .5f);
        var panelA = new UiRect(display.Width - panelWidth - 16, viewport.WorkPos.Y + 16, panelWidth, panelHeight);
        var panelB = panelA with { Y = panelA.Y + panelHeight + 18 };
        var regionA = new UiRect(panelA.X + 8, panelA.Y + 28, panelA.Width - 16, panelA.Height - 36);
        var regionB = new UiRect(panelB.X + 8, panelB.Y + 28, panelB.Width - 16, panelB.Height - 36);
        var selected = Contains(regionA, mouse) ? 1 : Contains(regionB, mouse) ? 2 : 0;
        if (!Contains(_controls, mouse))
        {
            var camera = _cameras[selected];
            camera.TargetDistance = Math.Clamp(camera.TargetDistance * MathF.Exp(-ui.GetMouseWheel() * .15f), .7f, 12f);
            if (ui.IsMouseDown(1) && _wasDragging)
                camera.TargetPan -= new Vector2(mouse.X - _previousMouse.X, mouse.Y - _previousMouse.Y) * (.0025f * camera.Distance);
        }
        _wasDragging = ui.IsMouseDown(1);
        _previousMouse = mouse;
        var worldCamera = _cameras[0].Update(delta, display.Width / display.Height);
        for (var layer = 0; layer < 3; layer++)
        {
            var model = Matrix4x4.CreateRotationY((layer - 1) * .45f) * Matrix4x4.CreateTranslation((layer - 1) * .4f - .35f, 0, layer * -.15f);
            DrawLayer(ui, _worldIds[layer], display, new(model * worldCamera), layer);
        }
        DrawWidget(ui, panelA, regionA, "Widget A - wheel to zoom", "camera-widget-a", _cameras[1], delta, 3);
        DrawWidget(ui, panelB, regionB, "Widget B - independent camera", "camera-widget-b", _cameras[2], delta, 4);

        const float padding = 8;
        ui.PushStyleVar(UiStyleVar.WindowPadding, new UiVector2(padding, padding));
        ui.PushStyleVar(UiStyleVar.ItemSpacing, new UiVector2(8, 2));
        var text = string.Create(CultureInfo.InvariantCulture,
            $"Wheel: zoom | Right drag: pan\nEach widget has its own camera\nGeometry builds: {_builds} | {(_animateGeometry ? "live" : "resident")}");
        var textSize = ui.CalcTextSize(text);
        var titleHeight = ui.GetFrameHeight() + padding;
        var height = titleHeight + textSize.Y + ui.GetFrameHeight() + padding + 6;
        ui.SetNextWindowPos(new(viewport.WorkPos.X + 12, viewport.WorkPos.Y + 12));
        ui.SetNextWindowSize(new(MathF.Max(360, textSize.X + padding * 2), MathF.Ceiling(height)));
        ui.BeginPanel("Layer cameras");
        ui.Text(text);
        if (ui.Button(_animateGeometry ? "Freeze geometry" : "Animate geometry")) { _animateGeometry = !_animateGeometry; _revision++; }
        var controlPos = ui.GetWindowPos();
        var controlSize = ui.GetWindowSize();
        _controls = new(controlPos.X, controlPos.Y, controlSize.X, controlSize.Y);
        ui.EndPanel();
        ui.PopStyleVar(2);
        if (_animateGeometry) _revision++;
        DuxelApp.RequestFrame();
    }

    private void DrawWidget(UiImmediateContext ui, UiRect panel, UiRect region, string title, string id, Camera camera, float delta, int seed)
    {
        ui.GetWindowDrawList().AddRectFilledRounded(panel, new(0xFF292929), ui.WhiteTextureId, 6, panel);
        ui.SetCursorScreenPos(new(panel.X + 8, panel.Y + 4));
        ui.Text(title);
        var scene = new UiLayer3D(camera.Update(delta, region.Width / region.Height)) { Viewport = region };
        DrawLayer(ui, id, region, scene, seed);
        // Input uses the inverse of the exact same projection as rendering.
        if (scene.TryMapScreenPoint(ui.GetMousePos(), new(0, 0, _lastSize.X, _lastSize.Y), out var local))
        {
            var marker = new UiRect(ui.GetMousePos().X - 3, ui.GetMousePos().Y - 3, 6, 6);
            if (Contains(region, local)) ui.GetWindowDrawList().AddRectFilled(marker, new(0xFF00FFFF), ui.WhiteTextureId);
        }
    }

    private void DrawLayer(UiImmediateContext ui, string id, UiRect bounds, UiLayer3D scene, int seed)
    {
        var options = new UiLayerOptions(false, 1, default) { Scene3D = scene, ContentVersion = _animateGeometry ? null : _revision };
        if (ui.BeginLayer(id, options))
        {
            _builds++;
            var draw = ui.GetWindowDrawList();
            var color = new UiColor((uint)(0xFF000000 | ((seed + 1) * 0x3171A3 + (_animateGeometry ? (int)_revision * 13 : 0)) & 0xFFFFFF));
            var cx = bounds.X + bounds.Width * .5f;
            var cy = bounds.Y + bounds.Height * .5f;
            for (var i = 0; i < 40; i++)
            {
                var x = bounds.X + bounds.Width * (.1f + (i % 8) * .1f);
                var y = bounds.Y + bounds.Height * (.15f + (i / 8) * .12f);
                draw.AddRectFilled(new(x, y, bounds.Width * .025f, bounds.Height * .025f), color, ui.WhiteTextureId);
            }
            draw.AddRectFilledRounded(new(cx - bounds.Width * .3f, cy - bounds.Height * .22f, bounds.Width * .35f, bounds.Height * .35f), color,
                new UiColor(0xFFFFFFFF), ui.WhiteTextureId, MathF.Min(bounds.Width, bounds.Height) * .06f, 2, bounds);
            draw.AddImage(_image, new(cx + bounds.Width * .08f, cy - bounds.Height * .2f), new(cx + bounds.Width * .32f, cy + bounds.Height * .2f), default, new(1, 1), new(0xFFFFFFFF));
            ui.SetCursorScreenPos(new(bounds.X + bounds.Width * .2f, bounds.Y + bounds.Height * .76f));
            ui.Text("Text / curves / image");
        }
        ui.EndLayer();
    }

    private static bool Contains(UiRect rect, UiVector2 point) => point.X >= rect.X && point.Y >= rect.Y
        && point.X < rect.X + rect.Width && point.Y < rect.Y + rect.Height;
}
