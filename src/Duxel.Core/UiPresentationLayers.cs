namespace Duxel.Core;

// Foreground, menus, combos, context menus and tooltips share one ordered
// presentation path. Surfaces remain screen anchored and do not inherit a widget camera.
internal sealed class UiPresentationLayers
{
    private static long _nextId;
    private readonly string _key = "duxel.layer.static:\u001fpresentation:" + Interlocked.Increment(ref _nextId);
    private readonly List<UiDrawListBuilder> _builders = [];
    private readonly List<UiDrawList> _output = [];
    private readonly List<UiDrawList> _depthScratch = [];
    private UiRect _clip;
    private UiTextureId _white;
    private UiDrawListSharedData? _shared;
    private UiLayerFrameLifetime? _lifetime;
    internal bool HasResidentFrame
    {
        get; private set;
    }

    internal UiOverlayLayerMetrics Metrics
    {
        get
        {
            var reused = 0;
            var rebuilt = 0;
            foreach (var builder in _builders)
            {
                reused += builder.ReusedPresentationLists;
                rebuilt += builder.RebuiltPresentationLists;
            }
            return new(reused, rebuilt);
        }
    }

    internal UiDrawListBuilder Get(int tier)
    {
        while (_builders.Count <= tier)
        {
            var index = _builders.Count;
            var builder = new UiDrawListBuilder(_clip);
            builder.ConfigurePresentation(_key + ":" + index, index, _lifetime!);
            builder._SetDrawListSharedData(_shared!);
            builder.PushTexture(_white);
            _builders.Add(builder);
        }
        return _builders[tier];
    }

    internal void Reset(UiRect clip, UiTextureId white, UiDrawListSharedData shared, UiLayerFrameLifetime lifetime)
    {
        (_clip, _white, _shared, _lifetime) = (clip, white, shared, lifetime);
        foreach (var builder in _builders)
        {
            builder._SetClipRect(clip);
            builder._SetDrawListSharedData(shared);
            builder._ResetForNewFrame();
            builder.PushTexture(white);
        }
    }

    internal UiPooledList<UiDrawList> Build(UiDrawListBuilder baseBuilder)
    {
        _output.Clear();
        HasResidentFrame = false;
        baseBuilder.AppendBuiltTo(_output);
        foreach (var builder in _builders)
        {
            builder.AppendBuiltTo(_output);
            HasResidentFrame |= builder.HasPresentationFrame;
        }
        OrderDepthMaterials();
        var result = UiPooledList<UiDrawList>.RentAndCopy(_output);
        _output.Clear();
        return result;
    }

    private void OrderDepthMaterials()
    {
        for (var start = 0; start < _output.Count;)
        {
            if (_output[start].Scene3D is not { IsScreenSpacePresentation: false } scope)
            {
                start++;
                continue;
            }
            var end = start + 1;
            while (end < _output.Count && _output[end].Scene3D is { IsScreenSpacePresentation: false } scene
                && scene.DepthScopeId == scope.DepthScopeId && scene.Viewport == scope.Viewport)
                end++;
            var sawRead = false;
            var needsOrder = false;
            for (var i = start; i < end; i++)
            {
                if (!_output[i].Scene3D!.Value.WriteDepth)
                    sawRead = true;
                else
                    needsOrder |= sawRead;
            }
            if (needsOrder)
            {
                var segment = start;
                for (var i = start; i < end; i++)
                {
                    var callback = false;
                    foreach (var command in _output[i].Commands)
                        callback |= command.Callback is not null;
                    if (!callback)
                        continue;
                    OrderSegment(segment, i);
                    segment = i + 1;
                }
                OrderSegment(segment, end);
            }
            start = end;
        }
    }
    private void OrderSegment(int start, int end)
    {
        // Stable material passes. Never cross a 2D surface, depth scope or external callback.
        for (var pass = 0; pass < 2; pass++)
            for (var i = start; i < end; i++)
                if (_output[i].Scene3D!.Value.WriteDepth == (pass == 0))
                    _depthScratch.Add(_output[i]);
        for (var i = start; i < end; i++)
            _output[i] = _depthScratch[i - start];
        _depthScratch.Clear();
    }

    internal void DiscardUnpublishedDrawing()
    {
        foreach (var builder in _builders)
            builder._ResetForNewFrame();
    }

    internal void RemoveEmptyCommands()
    {
        foreach (var builder in _builders)
            builder._PopUnusedDrawCmd();
    }

    internal void Release()
    {
        foreach (var builder in _builders)
        {
            builder._ClearFreeMemory();
            builder.ReleasePresentation();
        }
    }
}
