// Fast 2D Rendering Performance Benchmark
// Measures FPS with complex UI rendering

#:package Duxel.Windows.App@*-*

using System;
using System.Diagnostics;
using Duxel.Core;
using Duxel.Windows;

// Configuration: Stress test complexity
const int GRID_COLS = 12;
const int GRID_ROWS = 8;
const int ITEM_COUNT = GRID_COLS * GRID_ROWS;
const bool ENABLE_TEXT = true;
const bool ENABLE_ANIMATIONS = true;

// Initialize
var app = new DuxelWindowsApp();
app.Initialize(width: 1600, height: 1200, title: "2D Render Perf Benchmark");

// Measurement state
var frameCount = 0;
var sw = Stopwatch.StartNew();
var lastReportFrame = 0;
var lastReportMs = 0L;
var frameTimesMs = new double[120]; // Rolling window
var frameIndex = 0;

// Animation state
var animationTime = 0f;
const float ANIM_SPEED = 0.016f; // ~60fps baseline

while (app.ProcessEvents())
{
    app.BeginFrame();
    
    // Measure frame start
    var frameStartMs = sw.ElapsedMilliseconds;
    
    // Render grid of interactive elements
    RenderStressTestGrid();
    
    // Measure frame end
    var frameEndMs = sw.ElapsedMilliseconds;
    var frameTimeMs = frameEndMs - frameStartMs;
    frameTimesMs[frameIndex % frameTimesMs.Length] = frameTimeMs;
    frameIndex++;
    
    animationTime += ANIM_SPEED;
    frameCount++;
    
    // Report every 60 frames
    if ((frameCount - lastReportFrame) >= 60)
    {
        var elapsedMs = frameEndMs - lastReportMs;
        var fps = (frameCount - lastReportFrame) * 1000.0 / elapsedMs;
        var avgFrameMs = frameTimesMs.AsSpan().Average();
        var maxFrameMs = frameTimesMs.AsSpan().Max();
        var minFrameMs = frameTimesMs.AsSpan().Min();
        
        Console.WriteLine($"[PERF] Frame={frameCount} FPS={fps:F1} Avg={avgFrameMs:F2}ms Min={minFrameMs:F2}ms Max={maxFrameMs:F2}ms");
        
        lastReportFrame = frameCount;
        lastReportMs = frameEndMs;
    }
    
    app.EndFrame();
}

void RenderStressTestGrid()
{
    Ui.BeginFrame(app.DrawData);
    
    Ui.SetNextWindowPos(new Vector2(10, 10));
    Ui.SetNextWindowSize(new Vector2(1580, 1180));
    Ui.PushStyleVar(UiStyleVar.WindowPadding, new Vector2(5, 5));
    
    if (Ui.Begin("2D Render Benchmark", UiWindowFlags.NoMove | UiWindowFlags.NoResize))
    {
        Ui.Text($"FPS: {60.0:F1} | Items: {ITEM_COUNT} | Frame: {frameCount}");
        Ui.Separator();
        
        // Create grid of items
        var itemSize = new Vector2(120, 100);
        var spacing = new Vector2(8, 8);
        
        for (var row = 0; row < GRID_ROWS; row++)
        {
            for (var col = 0; col < GRID_COLS; col++)
            {
                var idx = row * GRID_COLS + col;
                var x = col * (itemSize.X + spacing.X);
                var y = row * (itemSize.Y + spacing.Y);
                
                Ui.SetCursorPos(new Vector2(x, y));
                Ui.PushID(idx);
                
                // Animated color
                var hue = (animationTime + idx * 0.02f) % 1.0f;
                var color = HsvToRgb(hue, 0.7f, 0.9f);
                Ui.PushStyleColor(UiCol.Button, color);
                
                // Button with changing state
                var isHovered = Ui.IsMouseHoveringRect(
                    Ui.GetCursorScreenPos(),
                    Ui.GetCursorScreenPos() + itemSize
                );
                
                if (isHovered)
                {
                    Ui.PushStyleColor(UiCol.Button, color * 1.2f);
                }
                
                var label = $"Item {idx}";
                if (Ui.Button(label, itemSize))
                {
                    // Button clicked
                }
                
                if (isHovered)
                {
                    Ui.PopStyleColor(1);
                }
                
                Ui.PopStyleColor(1);
                Ui.PopID();
                
                // Draw additional elements for complexity
                if (ENABLE_TEXT)
                {
                    Ui.SetCursorPos(new Vector2(x, y + itemSize.Y + 2));
                    Ui.Text($"Val: {(int)(animationTime * 100) % 100}");
                }
                
                // Horizontal layout for next item
                if (col < GRID_COLS - 1)
                {
                    Ui.SameLine();
                }
            }
        }
    }
    
    Ui.PopStyleVar(1);
    Ui.End();
    
    Ui.EndFrame();
}

UiColor HsvToRgb(float h, float s, float v)
{
    var c = v * s;
    var x = c * (1 - MathF.Abs((h * 6) % 2 - 1));
    var m = v - c;
    
    float r = 0, g = 0, b = 0;
    
    if (h < 1.0f / 6.0f) { r = c; g = x; b = 0; }
    else if (h < 2.0f / 6.0f) { r = x; g = c; b = 0; }
    else if (h < 3.0f / 6.0f) { r = 0; g = c; b = x; }
    else if (h < 4.0f / 6.0f) { r = 0; g = x; b = c; }
    else if (h < 5.0f / 6.0f) { r = x; g = 0; b = c; }
    else { r = c; g = 0; b = x; }
    
    return new UiColor(
        (byte)((r + m) * 255),
        (byte)((g + m) * 255),
        (byte)((b + m) * 255),
        255
    );
}

static class SpanExtensions
{
    public static double Average(this Span<double> values)
    {
        double sum = 0;
        foreach (var v in values)
        {
            sum += v;
        }
        return sum / values.Length;
    }
    
    public static double Max(this Span<double> values)
    {
        var max = double.MinValue;
        foreach (var v in values)
        {
            if (v > max) max = v;
        }
        return max;
    }
    
    public static double Min(this Span<double> values)
    {
        var min = double.MaxValue;
        foreach (var v in values)
        {
            if (v < min) min = v;
        }
        return min;
    }
}
