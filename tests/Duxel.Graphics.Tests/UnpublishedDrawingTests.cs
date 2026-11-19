using Duxel.Core;

internal static class UnpublishedDrawingTests
{
    private static readonly UiRect Clip = new(0, 0, 400, 300);

    internal static void Run(Action<bool, string> check)
    {
        foreach (var mode in new[] { "reset", "free", "deferred" })
        {
            var builder = new UiDrawListBuilder(Clip);
            var owned = Owned();
            builder.AppendLayerReference(owned, 1, default, null);
            Discard(builder, mode);
            check(owned.Commands.Count == 0 && owned.RectFilledPrimitives!.Count == 0, $"Unpublished drawing / {mode} returns owned output buffers");
            var empty = builder.Build();
            check(empty.Count == 0, $"Unpublished drawing / {mode} leaves no stale drawing");
            empty.Return();
            owned.ReleasePooled();
            var retained = Owned();
            builder.AppendLayerReference(retained with
            {
                OwnsBuffers = false
            }, 1, default, null);
            Discard(builder, mode);
            check(retained.Commands.Count == 1 && retained.RectFilledPrimitives!.Count == 1, $"Unpublished drawing / {mode} never returns borrowed retained buffers");
            retained.ReleasePooled();
            builder._ClearFreeMemory();
        }
        foreach (var append in new[] { false, true })
        {
            var builder = new UiDrawListBuilder(Clip);
            var owner = Owned();
            builder.AppendLayerReference(owner, 1, default, null);
            var destination = new List<UiDrawList>();
            if (append)
                builder.AppendBuiltTo(destination);
            else
            {
                var built = builder.Build();
                destination.AddRange(built);
                built.Return();
            }
            var repeated = builder.Build();
            check(destination.Count == 1 && repeated.Count == 0, $"Unpublished drawing / append={append} transfers a drawing only once");
            repeated.Return();
            builder._ResetForNewFrame();
            check(owner.Commands.Count == 1, $"Unpublished drawing / append={append} reset cannot return consumer-owned geometry");
            destination[0].ReleasePooled();
            check(owner.Commands.Count == 0, $"Unpublished drawing / append={append} final consumer returns its buffers");
            builder._ClearFreeMemory();
        }
        var channels = new UiDrawListBuilder(Clip);
        channels.Split(3);
        var owners = new[] { Owned(), Owned(), Owned() };
        for (var index = 0; index < owners.Length; index++)
        {
            channels.SetCurrentChannel(index);
            channels.AppendLayerReference(owners[index], 1, default, null);
        }
        channels._ResetForNewFrame();
        check(owners.All(owner => owner.Commands.Count == 0), "Unpublished drawing / reset releases inactive and active channels");
        channels._ClearFreeMemory();
        foreach (var owner in owners)
            owner.ReleasePooled();

        foreach (var mode in new[] { "reset", "free", "deferred" })
        {
            var pending = new UiDrawListBuilder(Clip);
            pending.Split(3);
            for (var index = 0; index < 3; index++)
            {
                pending.SetCurrentChannel(index);
                pending.AddRectFilled(new(1, 1, 10, 10), new(255, 100, 50), new(2), Clip);
            }
            Discard(pending, mode);
            var after = pending.Build();
            check(after.Count == 0, $"Unpublished drawing / {mode} clears pending primitive metadata before channel storage");
            after.Return();
            pending._ClearFreeMemory();
        }
        var splitOwner = new UiDrawListBuilder(Clip);
        splitOwner.Split(2);
        var publishedOwner = Owned();
        var unpublishedOwner = Owned();
        splitOwner.AppendLayerReference(publishedOwner, 1, default, null);
        var published = splitOwner.Build();
        splitOwner.SetCurrentChannel(1);
        splitOwner.AppendLayerReference(unpublishedOwner, 1, default, null);
        splitOwner._ResetForNewFrame();
        check(publishedOwner.Commands.Count == 1 && unpublishedOwner.Commands.Count == 0,
            "Unpublished drawing / channel cleanup distinguishes a transferred owner from a pending owner");
        foreach (var list in published)
            list.ReleasePooled();
        published.Return();
        splitOwner._ClearFreeMemory();

        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        var frame = new UiFrameInfo(.016f, new(400, 300), new(1, 1));
        var failedLayer = Owned();
        var failedOverlay = Owned();
        context.NewFrame(frame, idle, Clip, UiTextSettings.Default);
        var rejected = false;
        try
        {
            context.Render(new Screen(ui =>
            {
                ui.BeginLayer("owned-failure");
                ui.GetWindowDrawList().AppendLayerReference(failedLayer, 1, default, null);
                ui.GetForegroundDrawList().AppendLayerReference(failedOverlay, 1, default, null);
                throw new InvalidOperationException("Owned unfinished layer");
            }));
        }
        catch (InvalidOperationException ex) when (ex.Message == "Owned unfinished layer") { rejected = true; }
        check(rejected && failedLayer.Commands.Count == 0 && failedOverlay.Commands.Count == 0,
            "Unpublished drawing / failed callbacks discard unfinished layer and overlay ownership");
        context.NewFrame(frame, idle, Clip, UiTextSettings.Default);
        var recovered = true;
        try
        {
            context.Render(new Screen(ui =>
            {
                if (ui.BeginLayer("owned-failure"))
                    ui.Text("Recovered layer");
                ui.EndLayer();
            }));
            using var drawing = context.GetDrawData();
        }
        catch (InvalidOperationException ex) when (ex.Message == "Nested layers must use distinct ids.") { recovered = false; }
        check(recovered, "Unpublished drawing / a failed layer does not retain an active builder id");
        context.Dispose();
        failedLayer.ReleasePooled();
        failedOverlay.ReleasePooled();

        using var retainedContext = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(retainedContext);
        void RetainedFrame(int version)
        {
            retainedContext.NewFrame(frame, idle, Clip, UiTextSettings.Default);
            retainedContext.Render(new Screen(ui =>
            {
                ui.GetWindowDrawList().AddRectFilled(new(1, 1, 10, 10), new(255, 100, 50), new(2), Clip);
                if (ui.BeginLayer("live-drawing", new(true, 1, default)
                {
                    ContentVersion = version
                }))
                    ui.Text("Live retained drawing " + version);
                ui.EndLayer();
            }));
        }
        RetainedFrame(1);
        using var firstFrame = retainedContext.GetDrawData();
        var firstCommands = firstFrame.DrawLists.Sum(list => list.Commands.Count);
        RetainedFrame(2);
        using (retainedContext.GetDrawData())
        {
        }
        retainedContext.Dispose();
        check(firstCommands > 0 && firstFrame.DrawLists.Sum(list => list.Commands.AsSpan().Length) == firstCommands,
            "Unpublished drawing / a claimed mixed frame survives later rendering and context disposal");
    }

    private static UiDrawList Owned()
    {
        var source = new UiDrawListBuilder(Clip);
        source.AddRectFilled(new(1, 1, 20, 20), new(255, 100, 50), new(2), Clip);
        var result = source.CloneOutput();
        source._ClearFreeMemory();
        return result;
    }
    private static void Discard(UiDrawListBuilder builder, string mode)
    {
        switch (mode)
        {
            case "reset":
                builder._ResetForNewFrame();
                break;
            case "free":
                builder._ClearFreeMemory();
                break;
            case "deferred":
                builder.DiscardDeferred();
                break;
            default:
                throw new InvalidOperationException("Unknown discard mode.");
        }
    }
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
