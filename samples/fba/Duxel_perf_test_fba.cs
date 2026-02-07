// FBA: 대량 폴리곤 물리 시뮬레이션 성능 벤치마크 — DrawList 프리미티브 렌더링 부하 테스트
#:property TargetFramework=net10.0
#:package Duxel.App@*-*

using System;
using System.Collections.Generic;
using Duxel.App;
using Duxel.Core;

DuxelApp.Run(new DuxelAppOptions
{
    Window = new DuxelWindowOptions
    {
        Title = "Duxel Performance Test (FBA)"
    },
    Font = new DuxelFontOptions
    {
        FontSize = 16,
        InitialGlyphs = PerfTestScreen.GlyphStrings
    },
    Screen = new PerfTestScreen()
});

public sealed class PerfTestScreen : UiScreen
{
    public static readonly IReadOnlyList<string> GlyphStrings = new[]
    {
        "Duxel Performance Test (FBA)",
        "Controls",
        "Polygons",
        "Add",
        "Remove",
        "Clear",
        "Speed",
        "Size",
        "Sides",
        "Rotation",
        "FPS",
        "Count",
        "Reset",
        "Bounds",
        "Paused"
    };

    private readonly List<PolygonBody> _polygons = [];
    private readonly Random _random = new(1337);
    private float _baseSpeed = 160f;
    private float _baseSize = 22f;
    private int _baseSides = 5;
    private float _rotationSpeed = 1.8f;
    private bool _paused;
    private double _lastTime;
    private float _fps;
    private int _fpsFrames;
    private double _fpsTime;

    public override void Render(UiImmediateContext ui)
    {
        var now = ui.GetTime();
        var delta = _lastTime == 0 ? 0.016 : Math.Clamp(now - _lastTime, 0.0, 0.05);
        _lastTime = now;

        var viewport = ui.GetMainViewport();
        var bounds = new UiRect(viewport.Pos.X, viewport.Pos.Y, viewport.Size.X, viewport.Size.Y);

        UpdateFps(delta);
        UpdatePolygons(delta, bounds);

        DrawScene(ui, bounds);
        DrawControls(ui, bounds);
    }

    private void DrawScene(UiImmediateContext ui, UiRect bounds)
    {
        var drawList = ui.GetBackgroundDrawList();
        var clip = bounds;
        var whiteTexture = ui.WhiteTextureId;

        foreach (var body in _polygons)
        {
            var radius = _baseSize * body.SizeScale;
            var center = body.Position;
            var sides = Math.Max(3, _baseSides + body.SideOffset);
            DrawPolygon(drawList, clip, whiteTexture, center, radius, sides, body.Rotation, body.Color);
        }
    }

    private void DrawControls(UiImmediateContext ui, UiRect bounds)
    {
        ui.SetNextWindowPos(new UiVector2(bounds.X + 16f, bounds.Y + 16f));
        ui.SetNextWindowSize(new UiVector2(320f, 420f));
        ui.BeginWindow("Controls");

        ui.TextV("FPS: {0:0.0}", _fps);
        ui.TextV("Count: {0}", _polygons.Count);
        if (ui.Checkbox("Paused", ref _paused))
        {
            if (_paused)
            {
                _lastTime = ui.GetTime();
            }
        }

        ui.SeparatorText("Polygons");
        ui.SliderFloat("Speed", ref _baseSpeed, 20f, 600f, 0f, "0");
        ui.SliderFloat("Size", ref _baseSize, 6f, 80f, 0f, "0");
        ui.SliderInt("Sides", ref _baseSides, 3, 10);
        ui.SliderFloat("Rotation", ref _rotationSpeed, 0f, 6f, 0f, "0.00");

        if (ui.Button("Add"))
        {
            for (var i = 0; i < 100; i++)
            {
                AddPolygon(bounds);
            }
        }
        ui.SameLine();
        if (ui.Button("Remove"))
        {
            RemovePolygon();
        }
        ui.SameLine();
        if (ui.Button("Clear"))
        {
            _polygons.Clear();
        }

        if (ui.Button("Reset"))
        {
            ResetSimulation(bounds);
        }

        ui.EndWindow();
    }

    private void UpdateFps(double delta)
    {
        _fpsFrames++;
        _fpsTime += delta;
        if (_fpsTime >= 0.5)
        {
            _fps = (float)(_fpsFrames / _fpsTime);
            _fpsFrames = 0;
            _fpsTime = 0;
        }
    }

    private void UpdatePolygons(double delta, UiRect bounds)
    {
        if (_paused || _polygons.Count == 0)
        {
            return;
        }

        var speed = _baseSpeed;
        var minX = bounds.X + 8f;
        var minY = bounds.Y + 8f;
        var maxX = bounds.X + bounds.Width - 8f;
        var maxY = bounds.Y + bounds.Height - 8f;

        foreach (var body in _polygons)
        {
            var radius = _baseSize * body.SizeScale;
            var velocity = new UiVector2(body.Direction.X * speed * body.SpeedScale, body.Direction.Y * speed * body.SpeedScale);
            var pos = new UiVector2(body.Position.X + velocity.X * (float)delta, body.Position.Y + velocity.Y * (float)delta);

            if (pos.X - radius < minX)
            {
                pos = pos with { X = minX + radius };
                body.Direction = new UiVector2(-body.Direction.X, body.Direction.Y);
            }
            else if (pos.X + radius > maxX)
            {
                pos = pos with { X = maxX - radius };
                body.Direction = new UiVector2(-body.Direction.X, body.Direction.Y);
            }

            if (pos.Y - radius < minY)
            {
                pos = pos with { Y = minY + radius };
                body.Direction = new UiVector2(body.Direction.X, -body.Direction.Y);
            }
            else if (pos.Y + radius > maxY)
            {
                pos = pos with { Y = maxY - radius };
                body.Direction = new UiVector2(body.Direction.X, -body.Direction.Y);
            }

            body.Position = pos;
            body.Rotation += (float)(body.AngularSpeed * delta);
        }
    }

    private void ResetSimulation(UiRect bounds)
    {
        _polygons.Clear();
        for (var i = 0; i < 32; i++)
        {
            AddPolygon(bounds);
        }
    }

    private void AddPolygon(UiRect bounds)
    {
        var radius = _baseSize * (0.6f + (float)_random.NextDouble() * 0.8f);
        var x = bounds.X + radius + (float)_random.NextDouble() * MathF.Max(1f, bounds.Width - radius * 2f);
        var y = bounds.Y + radius + (float)_random.NextDouble() * MathF.Max(1f, bounds.Height - radius * 2f);
        var angle = (float)(_random.NextDouble() * MathF.Tau);
        var direction = new UiVector2(MathF.Cos(angle), MathF.Sin(angle));

        _polygons.Add(new PolygonBody
        {
            Position = new UiVector2(x, y),
            Direction = direction,
            SpeedScale = 0.6f + (float)_random.NextDouble() * 0.9f,
            SizeScale = 0.6f + (float)_random.NextDouble() * 0.8f,
            SideOffset = _random.Next(-2, 3),
            Rotation = (float)(_random.NextDouble() * MathF.Tau),
            AngularSpeed = (_random.NextDouble() * 2.0 - 1.0) * _rotationSpeed,
            Color = RandomColor()
        });
    }

    private void RemovePolygon()
    {
        if (_polygons.Count == 0)
        {
            return;
        }

        _polygons.RemoveAt(_polygons.Count - 1);
    }

    private UiColor RandomColor()
    {
        var r = (uint)(80 + _random.Next(160));
        var g = (uint)(80 + _random.Next(160));
        var b = (uint)(80 + _random.Next(160));
        return new UiColor(0xFF000000u | (r << 16) | (g << 8) | b);
    }

    private static void DrawPolygon(UiDrawListBuilder drawList, UiRect clip, UiTextureId whiteTexture, UiVector2 center, float radius, int sides, float rotation, UiColor color)
    {
        var step = MathF.Tau / sides;
        var prev = GetPoint(center, radius, rotation, 0, step);
        var first = prev;

        for (var i = 1; i < sides; i++)
        {
            var next = GetPoint(center, radius, rotation, i, step);
            drawList.AddTriangleFilled(center, prev, next, color, whiteTexture, clip);
            prev = next;
        }

        drawList.AddTriangleFilled(center, prev, first, color, whiteTexture, clip);
    }

    private static UiVector2 GetPoint(UiVector2 center, float radius, float rotation, int index, float step)
    {
        var angle = rotation + step * index;
        return new UiVector2(center.X + MathF.Cos(angle) * radius, center.Y + MathF.Sin(angle) * radius);
    }

    private sealed class PolygonBody
    {
        public UiVector2 Position;
        public UiVector2 Direction;
        public float SpeedScale;
        public float SizeScale;
        public int SideOffset;
        public float Rotation;
        public double AngularSpeed;
        public UiColor Color;
    }
}

