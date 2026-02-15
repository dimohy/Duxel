// FBA: 대량 폴리곤 물리 시뮬레이션 성능 벤치마크 — DrawList 프리미티브 렌더링 부하 테스트
#:property TargetFramework=net10.0
#:package Duxel.Windows.App@*-*

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using Duxel.App;
using Duxel.Core;

var profileName = Environment.GetEnvironmentVariable("DUXEL_APP_PROFILE");
var profile = string.Equals(profileName, "render", StringComparison.OrdinalIgnoreCase)
    ? DuxelPerformanceProfile.Render
    : DuxelPerformanceProfile.Display;
var fxaaName = Environment.GetEnvironmentVariable("DUXEL_FXAA");
var enableFxaa = string.Equals(fxaaName, "1", StringComparison.OrdinalIgnoreCase)
    || string.Equals(fxaaName, "true", StringComparison.OrdinalIgnoreCase)
    || string.Equals(fxaaName, "on", StringComparison.OrdinalIgnoreCase);
var taaName = Environment.GetEnvironmentVariable("DUXEL_TAA");
var enableTaa = string.Equals(taaName, "1", StringComparison.OrdinalIgnoreCase)
    || string.Equals(taaName, "true", StringComparison.OrdinalIgnoreCase)
    || string.Equals(taaName, "on", StringComparison.OrdinalIgnoreCase);

DuxelApp.Run(new DuxelAppOptions
{
    Window = new DuxelWindowOptions
    {
        Title = "Duxel Performance Test (FBA)",
        VSync = false
    },
    Renderer = new DuxelRendererOptions
    {
        Profile = profile,
        EnableTaaIfSupported = enableTaa,
        EnableFxaaIfSupported = enableFxaa
    },
    Font = new DuxelFontOptions
    {
        InitialGlyphs = PerfTestScreen.GlyphStrings
    },
    Screen = new PerfTestScreen(profile)
});

public sealed class PerfTestScreen : UiScreen
{
    private const float MinCollisionCellSize = 24f;
    private const float CollisionHitDecayPerSecond = 2.4f;
    private const float CollisionHitShrinkMax = 0.22f;
    private const float CollisionHitRedWeight = 0.28f;
    private const float CollisionAngularImpulseScale = 1.15f;
    private const float CollisionAngularDampingPerSecond = 0.7f;
    public static readonly IReadOnlyList<string> GlyphStrings = new[]
    {
        "Duxel Performance Test (FBA)",
        "Controls",
        "Render",
        "Profile",
        "Render Profile",
        "Restart required to apply",
        "VSync",
        "MSAA",
        "MSAA 1x/2x/4x/8x",
        "MSAA is forced to 1x while TAA/FXAA is enabled",
        "TAA",
        "FXAA",
        "TAA Exclude Font",
        "TAA Weight",
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
        ,"Renderer Status"
        ,"AA"
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
    private readonly double _benchDurationSeconds = ReadBenchDurationSeconds();
    private readonly string? _benchOutputPath = Environment.GetEnvironmentVariable("DUXEL_PERF_BENCH_OUT");
    private readonly int _initialPolygons = ReadInitialPolygonCount();
    private readonly DuxelPerformanceProfile _startupProfile;
    private bool _renderProfileForNextLaunch;
    private bool _initialized;
    private double _benchElapsedSeconds;
    private double _benchFpsSum;
    private int _benchFpsSamples;
    private bool _benchCompleted;
    private readonly Dictionary<long, int> _collisionCellHeads = new(4096);
    private int[] _collisionNext = [];
    private float[] _collisionRadii = [];
    private int[] _collisionTouchedStamp = [];
    private int _collisionStamp = 1;

    public PerfTestScreen(DuxelPerformanceProfile startupProfile)
    {
        _startupProfile = startupProfile;
        _renderProfileForNextLaunch = startupProfile == DuxelPerformanceProfile.Render;
    }

    public override void Render(UiImmediateContext ui)
    {
        var now = ui.GetTime();
        var delta = _lastTime == 0 ? 0.016 : Math.Clamp(now - _lastTime, 0.0, 0.05);
        _lastTime = now;

        var viewport = ui.GetMainViewport();
        var bounds = new UiRect(viewport.Pos.X, viewport.Pos.Y, viewport.Size.X, viewport.Size.Y);

        UpdateFps(delta);
        var renderBounds = DrawRenderWindow(ui, bounds, delta);
        DrawControls(ui, bounds, renderBounds);
        DrawRendererStatusOverlay(ui);

        DuxelApp.RequestFrame();
    }

    private static double ReadBenchDurationSeconds()
    {
        var value = Environment.GetEnvironmentVariable("DUXEL_PERF_BENCH_SECONDS");
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0d;
        }

        return double.TryParse(value, out var seconds) && seconds > 0d ? seconds : 0d;
    }

    private static int ReadInitialPolygonCount()
    {
        var value = Environment.GetEnvironmentVariable("DUXEL_PERF_INITIAL_POLYGONS");
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0;
        }

        return int.TryParse(value, out var count) && count > 0 ? count : 0;
    }

    private void EnsureInitialized(UiRect bounds)
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        if (_initialPolygons <= 0)
        {
            return;
        }

        EnsurePolygonCapacity(_initialPolygons);

        for (var i = 0; i < _initialPolygons; i++)
        {
            AddPolygon(bounds);
        }
    }

    private void TickBenchMode(UiImmediateContext ui, double delta)
    {
        if (_benchCompleted || _benchDurationSeconds <= 0d)
        {
            return;
        }

        _benchElapsedSeconds += delta;
        if (_fps > 0f)
        {
            _benchFpsSum += _fps;
            _benchFpsSamples++;
        }

        if (_benchElapsedSeconds < _benchDurationSeconds)
        {
            return;
        }

        _benchCompleted = true;
        var avgFps = _benchFpsSamples > 0 ? _benchFpsSum / _benchFpsSamples : 0d;
        var vsync = ui.GetVSync();
        var taaEnabled = ui.GetTaaEnabled();
        var fxaaEnabled = ui.GetFxaaEnabled();
        var taaExcludeFont = ui.GetTaaExcludeFont();
        var taaWeight = ui.GetTaaCurrentFrameWeight();
        var msaaSamples = ui.GetMsaaSamples();

        if (!string.IsNullOrWhiteSpace(_benchOutputPath))
        {
            var json = string.Format(
                CultureInfo.InvariantCulture,
                "{{\"avgFps\":{0:0.###},\"samples\":{1},\"elapsedSeconds\":{2:0.###},\"vsync\":{3},\"msaa\":{4},\"taa\":{5},\"fxaa\":{6},\"taaExcludeFont\":{7},\"taaWeight\":{8:0.###},\"count\":{9}}}",
                avgFps,
                _benchFpsSamples,
                _benchElapsedSeconds,
                vsync.ToString().ToLowerInvariant(),
                msaaSamples,
                taaEnabled.ToString().ToLowerInvariant(),
                fxaaEnabled.ToString().ToLowerInvariant(),
                taaExcludeFont.ToString().ToLowerInvariant(),
                taaWeight,
                _polygons.Count
            );
            File.WriteAllText(_benchOutputPath!, json);
        }

        Environment.Exit(0);
    }

    private UiRect DrawRenderWindow(UiImmediateContext ui, UiRect viewportBounds, double delta)
    {
        const float controlsWidth = 320f;
        const float margin = 16f;
        var renderX = viewportBounds.X + controlsWidth + margin * 2f;
        var renderY = viewportBounds.Y + margin;
        var renderWidth = MathF.Max(240f, viewportBounds.Width - controlsWidth - margin * 3f);
        var renderHeight = MathF.Max(240f, viewportBounds.Height - margin * 2f);

        ui.SetNextWindowPos(new UiVector2(renderX, renderY));
        ui.SetNextWindowSize(new UiVector2(renderWidth, renderHeight));
        ui.BeginWindow("Render");

        var contentPos = ui.GetCursorScreenPos();
        var contentAvail = ui.GetContentRegionAvail();
        var bounds = new UiRect(
            contentPos.X,
            contentPos.Y,
            MathF.Max(1f, contentAvail.X),
            MathF.Max(1f, contentAvail.Y)
        );

        EnsureInitialized(bounds);
        UpdatePolygons(delta, bounds);
        TickBenchMode(ui, delta);
        DrawScene(ui, bounds);

        ui.EndWindow();
        return bounds;
    }

    private void DrawScene(UiImmediateContext ui, UiRect bounds)
    {
        var drawList = ui.GetWindowDrawList();
        var clip = bounds;
        var whiteTexture = ui.WhiteTextureId;

        drawList.PushTexture(whiteTexture);
        drawList.PushClipRect(clip);

        var polygons = CollectionsMarshal.AsSpan(_polygons);
        for (var i = 0; i < polygons.Length; i++)
        {
            var body = polygons[i];
            var hit = body.HitResponse;
            var radiusScale = 1f - hit * CollisionHitShrinkMax;
            var radius = _baseSize * body.SizeScale * radiusScale;
            var center = body.Position;
            var sides = Math.Max(3, _baseSides + body.SideOffset);
            DrawPolygon(drawList, center, radius, sides, body.Rotation, BlendHitColor(body.BaseColor, hit));
        }

        drawList.PopClipRect();
        drawList.PopTexture();
    }

    private void DrawControls(UiImmediateContext ui, UiRect viewportBounds, UiRect renderBounds)
    {
        const float margin = 16f;
        ui.SetNextWindowPos(new UiVector2(viewportBounds.X + margin, viewportBounds.Y + margin));
        ui.SetNextWindowSize(new UiVector2(320f, MathF.Max(320f, viewportBounds.Height - margin * 2f)));
        ui.BeginWindow("Controls");
        if (ui.IsWindowAppearing())
        {
            ui.SetScrollY(0f);
        }

        ui.TextV("FPS: {0:0.0}", _fps);
        ui.TextV("Count: {0}", _polygons.Count);
        var vsync = ui.GetVSync();
        if (ui.Checkbox("VSync", ref vsync))
        {
            ui.SetVSync(vsync);
        }

        var msaaSamples = ui.GetMsaaSamples();
        if (ui.SliderInt("MSAA", ref msaaSamples, 1, 8))
        {
            ui.SetMsaaSamples(msaaSamples);
        }
        ui.TextV("MSAA: {0}x", ui.GetMsaaSamples());
        if (ui.GetTaaEnabled() || ui.GetFxaaEnabled())
        {
            ui.Text("MSAA is forced to 1x while TAA/FXAA is enabled");
        }

        var taaEnabled = ui.GetTaaEnabled();
        if (ui.Checkbox("TAA", ref taaEnabled))
        {
            ui.SetTaaEnabled(taaEnabled);
        }

        var fxaaEnabled = ui.GetFxaaEnabled();
        if (ui.Checkbox("FXAA", ref fxaaEnabled))
        {
            ui.SetFxaaEnabled(fxaaEnabled);
        }

        var taaExcludeFont = ui.GetTaaExcludeFont();
        if (ui.Checkbox("TAA Exclude Font", ref taaExcludeFont))
        {
            ui.SetTaaExcludeFont(taaExcludeFont);
        }

        var taaWeight = ui.GetTaaCurrentFrameWeight();
        if (ui.SliderFloat("TAA Weight", ref taaWeight, 0.05f, 1f, 0f, "0.00"))
        {
            ui.SetTaaCurrentFrameWeight(taaWeight);
        }

        ui.SeparatorText("Profile");
        ui.TextV("Current: {0}", _startupProfile == DuxelPerformanceProfile.Render ? "Render" : "Display");
        ui.Checkbox("Render Profile", ref _renderProfileForNextLaunch);
        ui.Text("Restart required to apply");
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
            EnsurePolygonCapacity(100);
            for (var i = 0; i < 100; i++)
            {
                AddPolygon(renderBounds);
            }
        }
        ui.SameLine();
        if (ui.Button("Remove"))
        {
            for (var i = 0; i < 100; i++)
            {
                RemovePolygon();
            }
        }
        ui.SameLine();
        if (ui.Button("Clear"))
        {
            _polygons.Clear();
        }

        if (ui.Button("Reset"))
        {
            ResetSimulation(renderBounds);
        }

        ui.EndWindow();
    }

    private static void DrawRendererStatusOverlay(UiImmediateContext ui)
    {
        var viewport = ui.GetMainViewport();
        var vsync = ui.GetVSync() ? "ON" : "OFF";
        var taa = ui.GetTaaEnabled() ? "ON" : "OFF";
        var fxaa = ui.GetFxaaEnabled() ? "ON" : "OFF";
        var weight = ui.GetTaaCurrentFrameWeight();
        var msaa = ui.GetMsaaSamples();
        var text = $"Renderer: VSync {vsync} | MSAA {msaa}x | TAA {taa} | FXAA {fxaa} | AA W {weight:0.00}";
        var margin = 8f;
        var textSize = ui.CalcTextSize(text);
        var pos = new UiVector2(viewport.Size.X - textSize.X - margin, 6f);
        var fg = ui.GetForegroundDrawList();
        fg.AddText(pos, new UiColor(210, 210, 210), text);
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

        var dt = (float)delta;
        var speed = _baseSpeed;
        var minX = bounds.X + 8f;
        var minY = bounds.Y + 8f;
        var maxX = bounds.X + bounds.Width - 8f;
        var maxY = bounds.Y + bounds.Height - 8f;

        var polygons = CollectionsMarshal.AsSpan(_polygons);
        for (var i = 0; i < polygons.Length; i++)
        {
            var body = polygons[i];
            var hit = MathF.Max(0f, body.HitResponse - dt * CollisionHitDecayPerSecond);
            body.HitResponse = hit;
            var radius = _baseSize * body.SizeScale * (1f - hit * CollisionHitShrinkMax);
            var dir = body.Direction;
            var pos = body.Position;
            var vx = dir.X * speed * body.SpeedScale;
            var vy = dir.Y * speed * body.SpeedScale;
            var px = pos.X + vx * dt;
            var py = pos.Y + vy * dt;

            if (px - radius < minX)
            {
                px = minX + radius;
                dir = new UiVector2(-dir.X, dir.Y);
            }
            else if (px + radius > maxX)
            {
                px = maxX - radius;
                dir = new UiVector2(-dir.X, dir.Y);
            }

            if (py - radius < minY)
            {
                py = minY + radius;
                dir = new UiVector2(dir.X, -dir.Y);
            }
            else if (py + radius > maxY)
            {
                py = maxY - radius;
                dir = new UiVector2(dir.X, -dir.Y);
            }

            body.Direction = dir;
            body.Position = new UiVector2(px, py);
            var angularDamping = MathF.Max(0f, 1f - dt * CollisionAngularDampingPerSecond);
            body.AngularSpeed *= angularDamping;
            body.Rotation += (float)(body.AngularSpeed * dt);
        }

        ResolvePolygonCollisions(polygons);
    }

    private void ResetSimulation(UiRect bounds)
    {
        var count = _polygons.Count;
        _polygons.Clear();
        EnsurePolygonCapacity(count);
        for (var i = 0; i < count; i++)
        {
            AddPolygon(bounds);
        }
    }

    private void ResolvePolygonCollisions(Span<PolygonBody> polygons)
    {
        var count = polygons.Length;
        if (count <= 1)
        {
            return;
        }

        if (_collisionNext.Length < count)
        {
            _collisionNext = new int[count];
        }

        if (_collisionRadii.Length < count)
        {
            _collisionRadii = new float[count];
        }

        if (_collisionTouchedStamp.Length < count)
        {
            _collisionTouchedStamp = new int[count];
        }

        _collisionStamp++;
        if (_collisionStamp == int.MaxValue)
        {
            Array.Clear(_collisionTouchedStamp, 0, _collisionTouchedStamp.Length);
            _collisionStamp = 1;
        }

        _collisionCellHeads.Clear();

        var cellSize = MathF.Max(MinCollisionCellSize, _baseSize * 2.5f);
        var invCellSize = 1f / cellSize;

        for (var i = 0; i < count; i++)
        {
            var body = polygons[i];
            var radius = _baseSize * body.SizeScale;
            _collisionRadii[i] = radius;

            var cellX = (int)MathF.Floor(body.Position.X * invCellSize);
            var cellY = (int)MathF.Floor(body.Position.Y * invCellSize);
            var key = PackGridKey(cellX, cellY);
            if (_collisionCellHeads.TryGetValue(key, out var head))
            {
                _collisionNext[i] = head;
                _collisionCellHeads[key] = i;
            }
            else
            {
                _collisionNext[i] = -1;
                _collisionCellHeads.Add(key, i);
            }
        }

        for (var i = 0; i < polygons.Length; i++)
        {
            var bodyA = polygons[i];
            var radiusA = _collisionRadii[i];
            var centerA = bodyA.Position;
            var cellAX = (int)MathF.Floor(centerA.X * invCellSize);
            var cellAY = (int)MathF.Floor(centerA.Y * invCellSize);

            for (var ny = cellAY - 1; ny <= cellAY + 1; ny++)
            {
                for (var nx = cellAX - 1; nx <= cellAX + 1; nx++)
                {
                    if (!_collisionCellHeads.TryGetValue(PackGridKey(nx, ny), out var head))
                    {
                        continue;
                    }

                    for (var j = head; j >= 0; j = _collisionNext[j])
                    {
                        if (j <= i)
                        {
                            continue;
                        }

                        var bodyB = polygons[j];
                        var radiusB = _collisionRadii[j];
                        var minDistance = radiusA + radiusB;

                        var dx = bodyB.Position.X - bodyA.Position.X;
                        var dy = bodyB.Position.Y - bodyA.Position.Y;
                        var distanceSquared = dx * dx + dy * dy;
                        var minDistanceSquared = minDistance * minDistance;
                        if (distanceSquared >= minDistanceSquared)
                        {
                            continue;
                        }

                        float normalX;
                        float normalY;
                        float distance;

                        if (distanceSquared <= float.Epsilon)
                        {
                            var angle = (float)(_random.NextDouble() * MathF.Tau);
                            normalX = MathF.Cos(angle);
                            normalY = MathF.Sin(angle);
                            distance = 0f;
                        }
                        else
                        {
                            distance = MathF.Sqrt(distanceSquared);
                            normalX = dx / distance;
                            normalY = dy / distance;
                        }

                        var overlap = minDistance - distance;
                        if (overlap > 0f)
                        {
                            var correctionX = normalX * (overlap * 0.5f);
                            var correctionY = normalY * (overlap * 0.5f);
                            bodyA.Position = new UiVector2(bodyA.Position.X - correctionX, bodyA.Position.Y - correctionY);
                            bodyB.Position = new UiVector2(bodyB.Position.X + correctionX, bodyB.Position.Y + correctionY);
                        }

                        var aAlong = bodyA.Direction.X * normalX + bodyA.Direction.Y * normalY;
                        var bAlong = bodyB.Direction.X * normalX + bodyB.Direction.Y * normalY;
                        var exchange = bAlong - aAlong;

                        var nextDirA = new UiVector2(bodyA.Direction.X + normalX * exchange, bodyA.Direction.Y + normalY * exchange);
                        var nextDirB = new UiVector2(bodyB.Direction.X - normalX * exchange, bodyB.Direction.Y - normalY * exchange);

                        var tangentX = -normalY;
                        var tangentY = normalX;
                        var relativeTangentVelocity =
                            (bodyB.Direction.X - bodyA.Direction.X) * tangentX
                            + (bodyB.Direction.Y - bodyA.Direction.Y) * tangentY;
                        var angularImpulse = relativeTangentVelocity * CollisionAngularImpulseScale;
                        var maxAngularSpeed = MathF.Max(0.35f, _rotationSpeed * 2.5f);

                        bodyA.Direction = nextDirA;
                        bodyB.Direction = nextDirB;
                        bodyA.AngularSpeed = Math.Clamp(bodyA.AngularSpeed - angularImpulse, -maxAngularSpeed, maxAngularSpeed);
                        bodyB.AngularSpeed = Math.Clamp(bodyB.AngularSpeed + angularImpulse, -maxAngularSpeed, maxAngularSpeed);
                        bodyA.HitResponse = 1f;
                        bodyB.HitResponse = 1f;
                        _collisionTouchedStamp[i] = _collisionStamp;
                        _collisionTouchedStamp[j] = _collisionStamp;
                    }
                }
            }
        }

        for (var i = 0; i < count; i++)
        {
            if (_collisionTouchedStamp[i] != _collisionStamp)
            {
                continue;
            }

            var body = polygons[i];
            body.Direction = NormalizeDirection(body.Direction);
        }
    }

    private static long PackGridKey(int cellX, int cellY)
        => ((long)cellX << 32) | (uint)cellY;

    private static UiVector2 NormalizeDirection(UiVector2 direction)
    {
        var lengthSquared = direction.X * direction.X + direction.Y * direction.Y;
        if (lengthSquared <= 1e-6f)
        {
            return new UiVector2(1f, 0f);
        }

        var invLength = 1f / MathF.Sqrt(lengthSquared);
        return new UiVector2(direction.X * invLength, direction.Y * invLength);
    }

    private static UiColor BlendHitColor(UiColor baseColor, float hit)
    {
        if (hit <= 0.0001f)
        {
            return baseColor;
        }

        var rgba = baseColor.Rgba;
        var r = (byte)((rgba >> 16) & 0xFFu);
        var g = (byte)((rgba >> 8) & 0xFFu);
        var b = (byte)(rgba & 0xFFu);
        var t = Math.Clamp(hit * CollisionHitRedWeight, 0f, 1f);
        var outR = (byte)Math.Clamp((int)MathF.Round(r + (255f - r) * (t * 0.45f)), 0, 255);
        var outG = (byte)Math.Clamp((int)MathF.Round(g * (1f - t * 0.22f)), 0, 255);
        var outB = (byte)Math.Clamp((int)MathF.Round(b * (1f - t * 0.35f)), 0, 255);
        return new UiColor(outR, outG, outB, 255);
    }

    private void EnsurePolygonCapacity(int additionalCount)
    {
        if (additionalCount <= 0)
        {
            return;
        }

        var required = _polygons.Count + additionalCount;
        if (_polygons.Capacity < required)
        {
            _polygons.Capacity = required;
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
            BaseColor = RandomColor(),
            HitResponse = 0f
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

    private static void DrawPolygon(UiDrawListBuilder drawList, UiVector2 center, float radius, int sides, float rotation, UiColor color)
    {
        var step = MathF.Tau / sides;
        var cosStep = MathF.Cos(step);
        var sinStep = MathF.Sin(step);

        var cosAngle = MathF.Cos(rotation);
        var sinAngle = MathF.Sin(rotation);

        Span<UiVector2> points = stackalloc UiVector2[sides];
        points[0] = new UiVector2(center.X + cosAngle * radius, center.Y + sinAngle * radius);

        for (var i = 1; i < sides; i++)
        {
            var nextCos = cosAngle * cosStep - sinAngle * sinStep;
            var nextSin = sinAngle * cosStep + cosAngle * sinStep;
            cosAngle = nextCos;
            sinAngle = nextSin;

            points[i] = new UiVector2(center.X + cosAngle * radius, center.Y + sinAngle * radius);
        }

        drawList.AddConvexPolyFilled(points, color);
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
        public UiColor BaseColor;
        public float HitResponse;
    }
}

