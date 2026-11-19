using Duxel.Core;

internal static class InputPresentationPhaseTests
{
    private static readonly UiRect Clip = new(0, 0, 400, 300);
    private static readonly UiFrameInfo Frame = new(.016f, new(400, 300), new(1, 1));
    private static readonly UiInputState Idle = new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);

    internal static void Run(Action<bool, string> check)
    {
        RunRawBuilders(check);
        using var context = Context();
        var phases = new List<UiRenderPhase>();
        var rawCounts = new List<int>();
        var builderPhases = new List<UiRenderPhase>();
        var layerBuilds = 0;
        var version = 0;
        var screen = new Screen(ui =>
        {
            phases.Add(ui.RenderPhase);
            var control = new UiControlContext(ui);
            check(control.RenderPhase == ui.RenderPhase && control.IsPresentationPass == ui.IsPresentationPass,
                "Input presentation / control and immediate views share one phase");
            var builder = ui.GetWindowDrawList();
            builderPhases.Add(builder.RenderPhase);
            builder.AddRectFilled(new(1, 1, 30, 30), new(200, 20, 30), new(2), Clip);
            builder.AddLine(new(1, 1), new(30, 30), new(255, 255, 255));
            builder.AddCircleFilled(new(50, 50), 10, new(255, 100, 20), new(2), Clip);
            var raw = builder.CloneOutput();
            rawCounts.Add(raw.Commands.Count);
            raw.ReleasePooled();
            version++;
            if (ui.BeginLayer("phase-layer", new(true, 1, default)
            {
                ContentVersion = version
            }))
            {
                layerBuilds++;
                ui.Text("Final retained layer");
            }
            ui.EndLayer();
        });
        context.NewInputBatch(Frame, [Idle, Idle, Idle], Clip, UiTextSettings.Default);
        context.Render(screen);
        using (var drawing = context.GetDrawData())
        {
            check(phases.SequenceEqual(new[] { UiRenderPhase.Input, UiRenderPhase.Input, UiRenderPhase.Presentation }),
                "Input presentation / all callbacks run, only final step presents");
            check(rawCounts.Count == 3 && rawCounts[0] == 0 && rawCounts[1] == 0 && rawCounts[2] > 0,
                "Input presentation / attached raw builders create no intermediate geometry commands / counts=" + string.Join(',', rawCounts)
                    + "; builder phases=" + string.Join(',', builderPhases));
            check(context.InputProcessingMetrics is { CompletedSteps: 3, BuiltDrawings: 1, Completed: true },
                "Input presentation / one drawing for three completed logical steps");
            check(drawing.DrawLists.Any(list => list.Commands.Count > 0) && layerBuilds == 3,
                "Input presentation / real layer content traverses with final nonempty output");
        }
        var replayed = false;
        context.NewFrame(Frame, Idle, Clip, UiTextSettings.Default);
        context.Render(new Screen(ui =>
        {
            replayed = !ui.BeginLayer("phase-layer", new(true, 1, default)
            {
                ContentVersion = version
            });
            ui.EndLayer();
        }));
        using (var drawing = context.GetDrawData())
            check(replayed && drawing.DrawLists.Any(list => list.Commands.Count > 0),
                "Input presentation / final layer cache is nonempty and reusable by a later ordinary frame");

        using var failed = Context();
        failed.NewInputBatch(Frame, [Idle, Idle, Idle], Clip, UiTextSettings.Default);
        var attempts = 0;
        try
        {
            failed.Render(new Screen(_ => { if (++attempts == 2) throw new InvalidOperationException("Owned phase failure"); }));
        }
        catch (InvalidOperationException ex) when (ex.Message == "Owned phase failure") { }
        check(failed.InputProcessingMetrics is { RequestedSteps: 3, CompletedSteps: 1, BuiltDrawings: 0, Completed: false },
            "Input presentation / an intermediate failure reports no completed drawing");

        using var images = Context();
        images.NewFrame(Frame, Idle, Clip, UiTextSettings.Default);
        images.Render(new Screen(_ => { }));
        using (images.GetDrawData())
        {
        }
        using var image = Fluent.Image(new(new(1, 1, [255, 100, 50, 255])));
        image.Size = new(20, 20);
        var imageSteps = 0;
        images.NewInputBatch(Frame, [Idle, Idle, Idle], Clip, UiTextSettings.Default);
        images.Render(new Screen(ui => { if (++imageSteps == 1) image.Render(ui); }));
        using (var drawing = images.GetDrawData())
            check(drawing.TextureUpdates.Count == 1 && drawing.TextureUpdates[0].Kind == UiTextureUpdateKind.Create,
                "Input presentation / an intermediate-only image preserves its texture creation for the final consumer");

        using var paintContext = Context();
        using var group = new UiPaintGroup();
        var captures = 0;
        var paintScreen = new Screen(ui => ui.PaintGroup(group, new(0, 0, 24, 24), 1, control =>
        {
            captures++;
            control.Frame(new(1, 1, 20, 20), new(200, 20, 30), default, 0, 0);
        }));
        paintContext.NewInputBatch(Frame, [Idle, Idle, Idle], Clip, UiTextSettings.Default);
        paintContext.Render(paintScreen);
        using (var drawing = paintContext.GetDrawData())
        {
            var groups = drawing.TextureUpdates.Where(update => update.Kind == UiTextureUpdateKind.RenderPaintGroup).ToArray();
            check(captures == 1 && groups.Length == 1 && groups[0].PaintGroup!.Content.DrawLists.Any(list => list.Commands.Count > 0),
                "Input presentation / pure paint groups capture final content instead of caching an empty input pass");
        }
        paintContext.NewFrame(Frame, Idle, Clip, UiTextSettings.Default);
        paintContext.Render(paintScreen);
        using (var drawing = paintContext.GetDrawData())
            check(captures == 1 && drawing.TextureUpdates.All(update => update.Kind != UiTextureUpdateKind.RenderPaintGroup),
                "Input presentation / final paint group snapshot is reusable without recapture");
    }

    private static void RunRawBuilders(Action<bool, string> check)
    {
        var color = new UiColor(255, 100, 50);
        var rawFont = new UiFontAtlas(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255],
            new Dictionary<int, UiGlyphInfo> { ['X'] = new(8, 0, 0, 8, 16, new(0, 0, 1, 1)) },
            new Dictionary<uint, float>(), 16, 0, 0, 'X');
        using var particles = new UiGpuParticleSystem([new() { Position = new(30, 30), Direction = new(1, 0), SpeedScale = 1, SizeScale = 1, Color = color.Rgba }]);
        (string Name, Action<UiDrawListBuilder> Draw)[] cases =
        [
            ("rectangle", b => b.AddRectFilled(new(1, 1, 20, 20), color)),
            ("rounded", b => b.AddRectFilledRounded(new(1, 1, 20, 20), color, new(2), 4, Clip)),
            ("stroke", b => b.AddRect(new(1, 1, 20, 20), color, 3, 2)),
            ("line", b => b.AddLine(new(1, 1), new(30, 30), color)),
            ("quad", b => b.AddQuadFilled(new(1, 1), new(30, 1), new(30, 30), new(1, 30), color)),
            ("triangle", b => b.AddTriangleFilled(new(1, 1), new(30, 1), new(15, 30), color)),
            ("circle", b => b.AddCircleFilled(new(30, 30), 10, color, new(2))),
            ("ellipse", b => b.AddEllipseFilled(new(30, 30), new(20, 10), color)),
            ("bezier", b => b.AddBezierCubic(new(1, 1), new(10, 30), new(20, 30), new(30, 1), color)),
            ("polygon", b => b.AddRegularPolygons([new(new(30, 30), 10, 7, 0, color)])),
            ("gradient", b => b.AddRectFilledGradient(new(1, 1, 30, 30), new(new(0, 0), new(30, 0), color, color, color), new(2), Clip)),
            ("ring", b => b.AddAnnularSector(new(40, 40), 20, 4, 0, MathF.PI, color, color, new(2), Clip)),
            ("image", b => b.AddImage(new(2), new(1, 1), new(30, 30), default, new(1, 1), color)),
            ("rounded-image", b => b.AddImageRounded(new(2), new(1, 1, 30, 30), UiImageUvTransform.Identity, color, 4, Clip)),
            ("text", b => b.AddText(rawFont, "X", new(1, 1), color, new(1), Clip, UiTextSettings.Default, 16)),
            ("callback", b => b.AddCallback((_, _) => { }))
            ,("backdrop", b => b.AddBackdropComposite(new(10), new(11), new(1, 1, 30, 30)))
            ,("GPU particles", b => b.AddGpuParticles(new(particles, Clip, .016f)))
        ];
        using var context = Context();
        context.NewInputBatch(Frame, [Idle, Idle, Idle], Clip, UiTextSettings.Default);
        context.Render(new Screen(ui =>
        {
            var builder = ui.GetBackgroundDrawList();
            foreach (var item in cases)
            {
                builder._ResetForNewFrame();
                builder.PushTexture(ui.WhiteTextureId);
                item.Draw(builder);
                var lists = builder.Build();
                check(ui.IsPresentationPass ? lists.Any(list => list.Commands.Count > 0) : lists.Count == 0,
                    $"Input presentation / {item.Name} produces commands only during {ui.RenderPhase}");
                foreach (var list in lists)
                    list.ReleasePooled();
                lists.Return();
            }
            (string Name, string Parameter, Action Invalid)[] invalid =
            [
                ("callback", "callback", () => builder.AddCallback(null!)),
                ("gradient", "gradient", () => builder.AddRectFilledGradient(new(1, 1, 20, 20), new(default, default, color, color, color), new(2), Clip)),
                ("rounded-image", "radius", () => builder.AddImageRounded(new(2), new(1, 1, 20, 20), UiImageUvTransform.Identity, color, -1, Clip)),
                ("polygon", "polygons", () => builder.AddRegularPolygons([new(new(20, 20), 10, 2, 0, color)])),
                ("ring", "radius", () => builder.AddAnnularSector(new(30, 30), 2, 4, 0, 1, color, color, new(2), Clip))
            ];
            foreach (var item in invalid)
            {
                var rejected = false;
                try
                {
                    item.Invalid();
                }
                catch (ArgumentException ex) when (ex.ParamName == item.Parameter) { rejected = true; }
                check(rejected, $"Input presentation / {item.Name} validates parameters during {ui.RenderPhase}");
            }
        }));
        using var drawing = context.GetDrawData();
    }

    private static UiContext Context()
    {
        var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        return context;
    }
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
