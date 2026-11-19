using Duxel.Core;

internal static class PrimitiveCommandTests
{
    internal static void Run(Action<bool,string> check)
    {
        var clip=new UiRect(0,0,1000,1000);var white=new UiTextureId(2);var red=new UiColor(255,0,0);
        var builder=new UiDrawListBuilder(clip);
        for(var i=0;i<1000;i++)builder.AddRectFilled(new(i,20,1,4),red,white);
        var first=builder.Build();
        var run=first.SelectMany(list=>list.Commands).Single();
        check(run.ElementCount==1000&&run.Bounds==new UiRect(0,20,1000,4)&&run.HasBounds,
            "primitive burst publishes the complete immutable count and union bounds");
        builder._ResetForNewFrame();
        builder.AddRectFilled(new(10,10,5,5),red,white);builder.AddRectFilled(new(20,10,5,5),red,white);
        builder.PushClipRect(new(0,0,100,100));
        builder.AddRectFilled(new(30,10,5,5),red,white);builder.AddRectFilled(new(40,10,5,5),red,white);
        builder.PopClipRect();
        builder.AddCallback((_,_)=>{});
        builder.AddRectFilled(new(50,10,5,5),red,white);builder.AddRectFilled(new(60,10,5,5),red,white);
        var second=builder.Build();
        try
        {
            var commands=second.SelectMany(list=>list.Commands).ToArray();
            check(commands.Where(c=>c.Kind==UiDrawCommandKind.RectFilledPrimitives).Select(c=>c.ElementCount).SequenceEqual(new uint[]{2,2,2}),
                "clip and callback boundaries commit every primitive run");
            check(commands.Count(c=>c.Callback is not null)==1,"primitive accumulation preserves callback position");
            check(first.SelectMany(list=>list.Commands).Single()==run,"later reset and command writes cannot mutate a queued run");
        }
        finally { Release(second); }
        foreach(var list in first)list.ReleasePooled();first.Return();
        builder._ResetForNewFrame();
        builder.Split(2);
        for(var i=0;i<3;i++)builder.AddRectFilled(new(i,0,1,1),red,white);
        builder.SetCurrentChannel(1);
        for(var i=0;i<4;i++)builder.AddRectFilled(new(i,5,1,1),red,white);
        builder.SetCurrentChannel(0);builder.AddRectFilled(new(3,0,1,1),red,white);
        builder.Merge();
        var channels=builder.Build();
        try
        {
            check(channels.SelectMany(list=>list.Commands).Sum(c=>(long)c.ElementCount)==8,
                "channel switching commits pending primitive ranges before replacing command storage");
        }
        finally { Release(channels); }
        builder._ResetForNewFrame();
        builder.AddRectFilled(new(1,1,5,5),red,white);builder.AddRectFilled(new(7,1,5,5),red,white);
        builder._ResetForNewFrame();
        var empty=builder.Build();
        check(empty.Count==0,"reset cannot publish a stale pending primitive command");
        Release(empty);
    }
    private static void Release(UiPooledList<UiDrawList> lists)
    {foreach(var list in lists)list.ReleasePooled();lists.Return();}
}
