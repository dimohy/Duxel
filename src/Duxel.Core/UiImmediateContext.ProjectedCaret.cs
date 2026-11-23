using System.Numerics;

namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    private void PaintInputCaret(UiRect caret, UiColor color, UiRect clip)
    {
        var rect = KeepProjectedCaretVisible(caret, clip, out var camera);
        if (camera is not { WriteDepth: true } scene)
        {
            _builder.AddRectFilled(rect, color, _whiteTexture, clip);
            return;
        }

        // A later, smaller triangle on the same tilted plane can round to a farther depth.
        // Apply the offset only to the caret and keep depth testing against other surfaces.
        const float nearOffset = -4f / 16_777_216f;
        scene = scene with
        {
            Transform = scene.Transform * Matrix4x4.CreateTranslation(0, 0, nearOffset),
            WriteDepth = false
        };
        if (BeginLayer(GetID("##input-caret-depth"), new(false, 1, default) { Scene3D = scene }))
            _builder.AddRectFilled(rect, color, _whiteTexture, clip);
        EndLayer();
    }

    private UiRect KeepProjectedCaretVisible(UiRect caret, UiRect clip, out UiLayer3D? paintCamera)
    {
        paintCamera = null;
        if (!HasLayerCameraAboveScreenPortal() || _widgetTransformDepth != 0
            || _capturingPaintGroup is not null || caret.Height <= 0)
            return caret;

        UiLayer3D? camera = null;
        var translation = default(UiVector2);
        foreach (var layer in _layerFrames)
        {
            // A cached list can be replayed with a different camera between paints.
            if (layer.Options.StaticCache || layer.Options.ContentVersion.HasValue)
                return caret;
            camera ??= layer.Options.Scene3D;
            translation = new(translation.X + layer.Options.Translation.X, translation.Y + layer.Options.Translation.Y);
        }
        if (camera is not { IsScreenSpacePresentation: false, WidgetTransform: null } scene)
            return caret;

        var display = new UiRect(0, 0, _displaySize.X, _displaySize.Y);
        var viewport = scene.Viewport ?? display;
        if (viewport.Width <= 0 || viewport.Height <= 0)
            return caret;

        paintCamera = scene;
        var transform = scene.GetEffectiveTransform(display);
        float NormalizeX(float x) => 2f * (x + translation.X - viewport.X) / viewport.Width - 1f;
        float NormalizeY(float y) => 2f * (y + translation.Y - viewport.Y) / viewport.Height - 1f;
        var top = Vector4.Transform(new Vector4(NormalizeX(caret.X), NormalizeY(caret.Y), 0, 1), transform);
        var bottom = Vector4.Transform(new Vector4(NormalizeX(caret.X), NormalizeY(caret.Y + caret.Height), 0, 1), transform);
        if (top.W <= 0 || bottom.W <= 0)
            return caret;

        var pixelScale = new Vector2(viewport.Width * _displayFramebufferScale.X * .5f,
            viewport.Height * _displayFramebufferScale.Y * .5f);
        var vertical = new Vector2(bottom.X / bottom.W - top.X / top.W, bottom.Y / bottom.W - top.Y / top.W) * pixelScale;
        var length = vertical.Length();
        if (!(length > 0) || !float.IsFinite(length))
            return caret;

        var normal = new Vector2(-vertical.Y, vertical.X) / length;
        var horizontal = Vector4.Transform(new Vector4(2f / viewport.Width, 0, 0, 0), transform);

        // Solve locally for one physical pixel normal to the projected insertion edge,
        // including the change of homogeneous W across a tilted plane.
        float RequiredWidth(Vector4 point)
        {
            var relativeW = horizontal.W / point.W;
            var direction = new Vector2(horizontal.X - point.X * relativeW,
                horizontal.Y - point.Y * relativeW) * (pixelScale / point.W);
            var rate = MathF.Abs(Vector2.Dot(direction, normal)) - relativeW;
            return rate > 0 ? 1f / rate : float.PositiveInfinity;
        }

        var width = MathF.Max(RequiredWidth(top), RequiredWidth(bottom));
        var available = clip.X + clip.Width - caret.X;
        if (!float.IsFinite(width) || width <= caret.Width || width > available)
            return caret;
        return caret with { Width = width };
    }
}