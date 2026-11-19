using System.Runtime.InteropServices;
using Duxel.Core;

internal static class TextRunCacheTests
{
    internal static void Run(Action<bool, string> check)
    {
        var glyphs = Enumerable.Range(32, 95).ToDictionary(c => c,
            c => new UiGlyphInfo(7.25f, -.5f, -9.25f, c == 32 ? 0 : 6.5f, 11.5f, new(.1f, .2f, .03f, .04f)));
        glyphs[0x1F600] = new(12, 0, -10, 12, 12, new(.3f, .2f, .05f, .05f));
        var font = new UiFontAtlas(64, 64, UiTextureFormat.Rgba8Unorm, new byte[64*64*4], glyphs,
            new Dictionary<uint,float> { [((uint)'A' << 16) | 'V'] = -.75f }, 12, -3, 1, -1);
        var clip = new UiRect(-100, -100, 1000, 1000);
        var draw = new UiDrawListBuilder(clip);
        var reference = new UiDrawListBuilder(clip);
        var strings = new[] { "AV letter", "A\nV\n😀!", "    ", "missing \u2603", "offscreen", "" };
        var cases = 0;
        foreach (var text in strings)
        foreach (var snap in new[] { true, false })
        foreach (var scale in new[] { .75f, 1f, 1.25f })
        foreach (var localClip in new[] { clip, new UiRect(5, 8, 40, 12), new UiRect(400,400,10,10) })
        {
            var settings = new UiTextSettings(scale, 1.1f, snap, cases % 2 == 0);
            for (var frame = 0; frame < 3; frame++)
            {
                draw._ResetForNewFrame(); reference._ResetForNewFrame();
                // An earlier command exercises rebasing and command coalescing.
                draw.AddRectFilled(new(0,0,3,3), new(0xFFFFFFFF), new(1));
                reference.AddRectFilled(new(0,0,3,3), new(0xFFFFFFFF), new(1));
                draw.AddText(font,text,new(10.25f,11.75f),new(0xFF123456),new(2),localClip,settings,15.5f);
                reference.AddTextCore(font,text,new(10.25f,11.75f),new(0xFF123456),new(2),localClip,settings,15.5f);
                Equal(draw,reference);
            }
            cases++;
        }
        check(true, $"text layout replay matches original geometry and commands in {cases} kerning,unicode,clip,scale,snap and baseline cases");

        draw._ClearFreeMemory();
        for (var frame = 0; frame < 3; frame++)
        {
            draw._ResetForNewFrame();
            draw.PushCommandUserData(frame);
            draw.AddText(font,"retained text",new(4,5),new(0xFFFFFFFF),new(2),clip,UiTextSettings.Default);
            var lists = draw.Build();
            check(lists[0].Commands[0].UserData is int value && value == frame,
                $"text replay preserves current command user data on frame{frame}");
            foreach (var list in lists) list.ReleasePooled(); lists.Return();
        }
        check(draw.TextRunReplayCount == 1, "third identical text request reuses glyph layout and geometry");

        var observations = 0;
        var observerSettings = UiTextSettings.Default with { MissingGlyphObserver = _ => observations++ };
        for (var i=0;i<3;i++)
        {
            UiTextBuilder.MeasureText(font,"\u2603",observerSettings);
            draw.AddText(font,"\u2603",default,new(0xFFFFFFFF),new(2),clip,observerSettings);
        }
        check(observations == 6, "measure and draw missing-glyph callbacks execute on every request");

        var other = new UiFontAtlas(64,64,UiTextureFormat.Rgba8Unorm,[],
            new Dictionary<int,UiGlyphInfo> { ['A'] = new(27,0,0,27,10,default) },
            new Dictionary<uint,float>(),12,-3,1,-1);
        check(UiTextBuilder.MeasureText(font,"A",UiTextSettings.Default).X
            != UiTextBuilder.MeasureText(other,"A",UiTextSettings.Default).X,
            "measurement cache distinguishes actual font objects");
        var retiredFont = MeasureTemporaryFont();
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        check(!retiredFont.IsAlive, "measurement cache does not keep a retired font atlas alive");
        check(UiTextBuilder.MeasureText(font,"A\nA",UiTextSettings.Default).Y
            != UiTextBuilder.MeasureText(font,"A\nA",UiTextSettings.Default,-1).Y,
            "measurement key distinguishes a null line height from an explicit negative value");

        // Immutable owned frame arrays must survive cache replacement and scratch reuse.
        draw._ResetForNewFrame();
        draw.AddText(font,"owned",default,new(0xFFFFFFFF),new(2),clip,UiTextSettings.Default);
        var held = draw.Build(); var heldVertices = held[0].Vertices.AsSpan().ToArray();
        for (var i=0;i<900;i++)
        {
            draw._ResetForNewFrame();
            for (var repeat=0;repeat<2;repeat++)
                draw.AddText(font,"capacity-"+i,new(0,10),new(0xFFFFFFFF),new(2),clip,UiTextSettings.Default);
            var lists=draw.Build(); foreach(var list in lists) list.ReleasePooled(); lists.Return();
        }
        var state=draw.TextRunCacheState;
        check(state.Entries <= 256 && state.GeometryBytes <= 262144,
            "changing text admission stays inside entry and geometry budgets");
        check(heldVertices.AsSpan().SequenceEqual(held[0].Vertices.AsSpan()),
            "queued frame geometry is independent of cache eviction and scratch reuse");
        foreach(var list in held) list.ReleasePooled(); held.Return();
        for(var i=0;i<181;i++) draw._ResetForNewFrame();
        check(draw.TextRunCacheState == (0,0), "idle text geometry and metadata are evicted");
        draw._ClearFreeMemory();
        check(draw.TextRunCacheState == (0,0), "explicit free-memory releases text cache ownership");

        var changing = new UiDrawListBuilder(clip);
        void ChangingRequest(int index)
        {
            changing._ResetForNewFrame();
            changing.AddText(font,"changing",new(index,2000),new(0xFFFFFFFF),new(2),clip,UiTextSettings.Default);
        }
        for(var i=0;i<600;i++) ChangingRequest(i);
        var allocatedBefore=GC.GetAllocatedBytesForCurrentThread();
        for(var i=600;i<1200;i++) ChangingRequest(i);
        check(GC.GetAllocatedBytesForCurrentThread()==allocatedBefore,
            "one-use text admission and eviction allocate no per-request metadata after warmup");
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static WeakReference MeasureTemporaryFont()
    {
        var font = new UiFontAtlas(1,1,UiTextureFormat.Rgba8Unorm,[255,255,255,255]);
        UiTextBuilder.MeasureText(font,"temporary font",UiTextSettings.Default);
        return new WeakReference(font);
    }

    private static void Equal(UiDrawListBuilder actual,UiDrawListBuilder expected)
    {
        var a=actual.Build();var b=expected.Build();
        if(a.Count!=b.Count) throw new InvalidOperationException("TEXT_RUN_LIST_COUNT");
        for(var i=0;i<a.Count;i++)
        {
            if(!MemoryMarshal.AsBytes(a[i].Vertices.AsSpan()).SequenceEqual(MemoryMarshal.AsBytes(b[i].Vertices.AsSpan()))
                ||!a[i].Indices.AsSpan().SequenceEqual(b[i].Indices.AsSpan())
                ||!a[i].Commands.AsSpan().SequenceEqual(b[i].Commands.AsSpan()))
                throw new InvalidOperationException("TEXT_RUN_GEOMETRY_OR_COMMAND_MISMATCH");
            a[i].ReleasePooled();b[i].ReleasePooled();
        }
        a.Return();b.Return();
    }
}
