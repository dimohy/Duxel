using Duxel.Core;
using System.Numerics;

internal static class RoundedImageRenderingTests
{
    internal static void Run(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool, string> check, int stride,
        Func<(int Draws, int Commands)> compiledStatistics, UiTextureId white)
    {
        var source = new UiImageSource(new(2, 2,
            [255, 0, 0, 255, 0, 255, 0, 255, 0, 0, 255, 255, 255, 255, 0, 255]));
        using var image = Fluent.Image(source);
        image.Size = new(64, 64);
        var statistics = default(UiRasterTextureStatistics);
        void Draw(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding: 0);
            var context = new UiControlContext(ui);
            context.Frame(context.ClipRect, new(0, 0, 0, 255), default, 0, 0);
            ui.SetCursorScreenPos(new(24.25f, 24.25f));
            image.Render(ui);
            statistics = ui.ImageTextureStatistics;
        }
        static bool Black(byte[] pixels, int index) => pixels[index] == 0 && pixels[index + 1] == 0 && pixels[index + 2] == 0;
        int At(int x, int y) => (y * stride + x) * 4;
        var original = capture("original", Draw);
        var uploads = statistics.Uploads;
        image.CornerRadius = 32;
        var circle = capture("circle", Draw);
        check(Black(circle, At(25, 25)) && !Black(original, At(25, 25)),
            "rounded image circle discards source-filled viewport corners");
        var matches = true;
        foreach (var (x, y) in new[] { (40, 40), (72, 40), (40, 72), (72, 72), (56, 56) })
            matches &= circle.AsSpan(At(x, y), 4).SequenceEqual(original.AsSpan(At(x, y), 4));
        check(matches, "rounded image preserves original quadrant UV sampling and interior material pixels");
        var edge = At(24, 56);
        var aa = false;
        for (var channel = 0; channel < 3; channel++)
            aa |= circle[edge + channel] > 0 && circle[edge + channel] < original[edge + channel];
        check(aa, "rounded image analytic boundary retains fractional coverage at a fractional pixel origin");
        check(circle.AsSpan().SequenceEqual(capture("replay", Draw)), "unchanged rounded image frame reproduces exact cached pixels");
        image.CornerRadius = 8;
        var rounded = capture("round8", Draw);
        check(Black(rounded, At(25, 25)) && rounded.AsSpan(At(56, 26), 4).SequenceEqual(original.AsSpan(At(56, 26), 4)),
            "rounded rectangle clips only its curved corners and preserves its straight edge interior");
        image.CornerRadius = 0;
        check(original.AsSpan().SequenceEqual(capture("reset", Draw)), "zero radius restores the exact original image path and pixels");
        check(statistics.Uploads == uploads, "rounding changes and replay do not upload or derive another bitmap");
        image.Source = new(new(2, 1, [255, 0, 0, 255, 0, 255, 0, 255]));
        var stretch = capture("wide-stretch", Draw);
        image.Fit = UiImageFit.Contain;
        var contain = capture("wide-contain", Draw);
        check(Black(contain, At(56, 30)) && !Black(contain, At(56, 56)),
            "Contain leaves transparent letterbox pixels around centered source content");
        image.Fit = UiImageFit.Cover;
        var cover = capture("wide-cover", Draw);
        check(cover[At(40, 56) + 1] > stretch[At(40, 56) + 1] && !Black(cover, At(56, 30)),
            "Cover crops centered source UVs instead of stretching or letterboxing the image");
        var wideUploads = statistics.Uploads;
        image.Fit = UiImageFit.Contain;
        image.CornerRadius = 24;
        capture("contain-rounded", Draw);
        check(statistics.Uploads == wideUploads, "fit and shape changes keep one native-resolution source texture");
        var document = new QuadrantDocument();
        using var svg = new UiSvg(document) { Size = new(64, 64), CornerRadius = 32 };
        var svgUploads = 0L;
        void DrawSvg(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding: 0);
            var context = new UiControlContext(ui);
            context.Frame(context.ClipRect, new(0, 0, 0, 255), default, 0, 0);
            ui.SetCursorScreenPos(new(24.25f, 24.25f));
            svg.Render(ui);
            svgUploads = ui.SvgTextureStatistics.GpuRenderRequests;
        }
        var svgFirst = capture("svg-circle", DrawSvg);
        var initialSvgUploads = svgUploads;
        svg.RotationDegrees = 90;
        var svgRotated = capture("svg-rotated", DrawSvg);
        check(svgFirst[At(40, 40)] == 255 && svgFirst[At(40, 40) + 2] == 0
            && svgRotated[At(40, 40)] == 0 && svgRotated[At(40, 40) + 2] == 255,
            "SVG source-space rounding follows rotated texture quadrants");
        svg.CornerRadius = 24;
        svg.Offset = new(3, -2);
        capture("svg-moved", DrawSvg);
        check(document.Rasters == 0 && svgUploads == initialSvgUploads,
            "GPU SVG radius rotation and translation retain one scene output without CPU rasterization");
        svg.Zoom = .5f;
        var smallSvg = capture("svg-zoom-half", DrawSvg);
        check(Black(smallSvg, At(65, 28)) && !Black(smallSvg, At(43, 40)),
            "zoomed SVG occupies its smaller layout bounds without smearing source UV boundaries");
        svg.Zoom = 1;
        svg.Margin = default;
        capture("svg-zoom-restored", DrawSvg);
        var restoredSvgUploads = svgUploads;
        svg.RotationDegrees = 45;
        svg.CornerRadius = 16;
        var sourceCorners = capture("svg-source-corners-45", DrawSvg);
        check(new[] { (69, 26), (112, 69), (69, 112), (26, 69) }.All(point => Black(sourceCorners, At(point.Item1, point.Item2))) &&
            !Black(sourceCorners, At(69, 38)) && svgUploads == restoredSvgUploads,
            "SVG pre-transform rounded corners rotate with the content instead of clipping its outer axis-aligned viewport");
        check(sourceCorners.AsSpan().SequenceEqual(capture("svg-source-corners-replay", DrawSvg)),
            "source-space SVG rounding replays identical pixels without another source raster");
        void SvgLayer(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding: 0);
            var context = new UiControlContext(ui);
            context.Frame(context.ClipRect, new(0, 0, 0, 255), default, 0, 0);
            var scene = new UiLayer3D(Matrix4x4.Identity)
            {
                Viewport = new(0, 0, stride, context.ClipRect.Height),
                PlaneModel = new(default, .2f)
            };
            if (ui.BeginLayer("svg-source-corners", new(true, 1, default)
            {
                ContentVersion = 1,
                Scene3D = scene
            }))
            {
                ui.SetCursorScreenPos(new(24.25f, 24.25f));
                svg.Render(ui);
            }
            ui.EndLayer();
        }
        var layer = capture("svg-source-corners-layer", SvgLayer);
        check(layer.AsSpan().SequenceEqual(capture("svg-source-corners-layer-replay", SvgLayer)) &&
            compiledStatistics() is { Draws: > 0, Commands: > 0 },
            "compiled SVG replay preserves source-space corner metadata and analytic coverage");
        using var firstLayerImage = new UiImage(source) { Size = new(64, 64), CornerRadius = 32 };
        using var secondLayerImage = new UiImage(source) { Size = new(64, 64), CornerRadius = 32 };
        using var thirdLayerImage = new UiImage(source) { Size = new(64, 64), CornerRadius = 32 };
        var images = new[] { firstLayerImage, secondLayerImage, thirdLayerImage };
        void Layers(UiImmediateContext ui, bool reverse)
        {
            ui.EnableRootViewportContentLayout(contentPadding: 0);
            var context = new UiControlContext(ui);
            context.Frame(context.ClipRect, new(0, 0, 0, 255), default, 0, 0);
            for (var order = 0; order < 3; order++)
            {
                var index = reverse ? 2 - order : order;
                var scene = new UiLayer3D(Matrix4x4.Identity)
                {
                    Viewport = new(0, 0, stride, context.ClipRect.Height),
                    PlaneModel = new(default, .2f + index * .1f)
                };
                if (ui.BeginLayer("rounded-compiled-" + index, new(true, 1, default)
                {
                    ContentVersion = 1,
                    Scene3D = scene
                }))
                {
                    ui.SetCursorScreenPos(new(24.25f + 80 * index, 24.25f));
                    images[index].Render(ui);
                }
                ui.EndLayer();
            }
        }
        var compiled = capture("compiled-layers", ui => Layers(ui, false));
        var compiledStats = compiledStatistics();
        check(compiledStats.Draws > 0 && compiledStats.Commands >= 3,
            "rounded image exercises actual compiled primitive descriptors for three retained depth layers");
        check(compiled.AsSpan().SequenceEqual(capture("compiled-reversed", ui => Layers(ui, true))),
            "compiled rounded image layer submission reversal preserves every pixel");
        var centers = true;
        for (var i = 0; i < 3; i++)
            centers &= !Black(compiled, At(56 + 80 * i, 56)) && Black(compiled, At(25 + 80 * i, 25));
        check(centers, "compiled rounded image layer descriptors preserve each member texture center and transparent mask corners");
        using var redImage = new UiImage(new(new(1, 1, [255, 0, 0, 255]))) { Size = new(64, 64), CornerRadius = 32 };
        var depthBounds = new UiRect(24.25f, 24.25f, 64, 64);
        void Depth(UiImmediateContext ui, bool maskedImage, bool reverse, float opacity)
        {
            ui.EnableRootViewportContentLayout(contentPadding: 0);
            var control = new UiControlContext(ui);
            control.Frame(control.ClipRect, new(0, 0, 0), default, 0, 0);
            void Back()
            {
                if (ui.BeginLayer("image-depth-back", new(false, 1, default)
                {
                    Scene3D = new(Matrix4x4.CreateTranslation(0, 0, .5f))
                }))
                    ui.GetWindowDrawList().AddRectFilled(depthBounds, new(0, 0, 255), white, control.ClipRect);
                ui.EndLayer();
            }
            void Front()
            {
                if (ui.BeginLayer("image-depth-front", new(false, opacity, default)
                {
                    Scene3D = new(Matrix4x4.Identity, opacity == 1)
                }))
                {
                    if (maskedImage)
                    {
                        ui.SetCursorScreenPos(new(depthBounds.X, depthBounds.Y));
                        redImage.Render(ui);
                    }
                    else
                        ui.GetWindowDrawList().AddRectFilledRounded(depthBounds, new(255, 0, 0), white, 32, control.ClipRect);
                }
                ui.EndLayer();
            }
            if (reverse)
            {
                Front();
                Back();
            }
            else
            {
                Back();
                Front();
            }
        }
        var depthReference = capture("depth-reference", ui => Depth(ui, false, false, 1));
        var depthImage = capture("depth-image", ui => Depth(ui, true, false, 1));
        check(depthReference.AsSpan().SequenceEqual(depthImage),
            "opaque rounded image preserves every analytic reference coverage and depth sample over a rear plane");
        check(depthImage.AsSpan().SequenceEqual(capture("depth-reversed", ui => Depth(ui, true, true, 1))),
            "overlapping rounded image depth planes preserve all pixels under reversed submission");
        check(depthImage[At(25, 25) + 2] == 255 && depthImage[At(56, 56)] == 255,
            "rounded image transparent corners reveal rear geometry while its opaque center occludes it");
        var alphaReference = capture("alpha-reference", ui => Depth(ui, false, false, .5f));
        check(alphaReference.AsSpan().SequenceEqual(capture("alpha-image", ui => Depth(ui, true, false, .5f))),
            "rounded image material opacity matches analytic reference blending over rear geometry");
    }

    private sealed class QuadrantDocument : IUiGpuSvgDocument
    {
        public UiGpuVectorScene GpuScene
        {
            get;
        } = GpuVectorTestScenes.Rectangles(new(64, 64),
            (new(0, 0, 32, 32), new(1, 0, 0, 1)), (new(32, 0, 32, 32), new(0, 1, 0, 1)),
            (new(0, 32, 32, 32), new(0, 0, 1, 1)), (new(32, 32, 32, 32), new(1, 1, 0, 1)));
        public UiVector2 IntrinsicSize => new(64, 64);
        public long Revision => 0;
        public event Action? Changed { add { } remove { } }
        internal int Rasters;
        public UiSvgRaster Rasterize(int width, int height)
        {
            Rasters++;
            var data = new byte[checked(width * height * 4)];
            for (var y = 0; y < height; y++)
                for (var x = 0; x < width; x++)
                {
                    var at = (y * width + x) * 4;
                    var right = x >= width / 2;
                    var bottom = y >= height / 2;
                    data[at] = (byte)(right == bottom ? 255 : 0);
                    data[at + 1] = (byte)(right ? 255 : 0);
                    data[at + 2] = (byte)(bottom && !right ? 255 : 0);
                    data[at + 3] = 255;
                }
            return new(width, height, data);
        }
    }
}
