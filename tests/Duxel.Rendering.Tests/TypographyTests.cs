using System.Buffers.Binary;
using System.Numerics;
using Duxel.Core;
using Duxel.App;
using Duxel.Platform.Windows;

internal static class TypographyTests
{
    internal static void Run(Action<bool,string> check)
    {
        var missingFontsRejected=false;
        try{DuxelApp.ValidateOptions(DuxelApp.Options(new Screen(static _=>{})) with {Font=new(){Typography=null!}});}
        catch(ArgumentNullException){missingFontsRejected=true;}
        check(missingFontsRejected,"missing typography configuration is rejected before app renderer initialization");
        var directory=Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        var fonts=new UiTypographyFonts(Path.Combine(directory,"segoeui.ttf"),Path.Combine(directory,"seguisb.ttf"),Path.Combine(directory,"segoeuib.ttf"));
        var backend=WindowsPlatformTextBackend.Instance;
        check(Weight(fonts.Regular)==400&&Weight(fonts.Semibold)==600&&Weight(fonts.Bold)==700,
            "typography selects actual Regular400, Semibold600 and Bold700 font faces rather than synthetic stroke thickening");
        check(backend.TryGetFontMetrics(fonts.Regular,14,out var metrics)&&metrics.Ascent>0&&metrics.Descent>0
            &&backend.TryGetFontMetrics(fonts.Regular,28,out var doubled)&&MathF.Abs(doubled.Ascent-metrics.Ascent*2)<.0001f,
            "DirectWrite font design metrics scale with requested em size");
        using var context=new UiContext(new(1,1,UiTextureFormat.Rgba8Unorm,[255,255,255,255]),new(1),new(2));
        context.SetPlatformTextBackend(backend);context.SetDirectTextEnabled(true);
        context.SetDirectTextPrimaryFontPath(fonts.Regular);context.SetDirectTextBaseFontSize(16);context.SetTypographyFonts(fonts);
        var idle=new UiInputState(new(-100,-100),false,false,false,false,false,false,0,0,[],[],default,default);
        UiDrawData Frame(Action<UiImmediateContext> render, UiTextSettings? settings = null)
        {
            context.NewFrame(new(1f/60,new(1000,1000),new(1,1)),idle,new(0,0,1000,1000),settings ?? UiTextSettings.Default);
            context.Render(new Screen(render));return context.GetDrawData();
        }
        using var regular=Fluent.Body1("Fluent typography");
        using var semibold=Fluent.Body1Strong("Fluent typography");
        using var bold=Fluent.Body1Stronger("Fluent typography");
        var selectedTextures=new HashSet<UiTextureId>();
        foreach(var view in new IUiView[]{regular,semibold,bold})
        {
            var size=default(UiVector2);
            using var data=Frame(ui=>{view.Render(ui);size=ui.GetItemRectSize();});
            check(size.Y==20,"Body1 weight variants reserve the official20px line box");
            selectedTextures.Add(data.DrawLists.SelectMany(list=>list.Commands).Single(command=>command.Kind==UiDrawCommandKind.Triangles).TextureId);
        }
        check(selectedTextures.Count==3,"different font faces keep independent raster texture/cache identities");
        if(!backend.TryRasterize(new(fonts.Regular,null,"Fluent typography",14),out var baselineRaster))
            throw new InvalidOperationException("Baseline reference rasterization failed.");
        try
        {
            void DrawBaseline(UiImmediateContext ui)
            {
                ui.SetCursorScreenPos(new(10,10));
                regular.Render(ui);
            }
            using var unaligned = Frame(DrawBaseline, UiTextSettings.Default with { PixelSnap = false });
            using var aligned = Frame(DrawBaseline);
            var top = unaligned.DrawLists.SelectMany(list => list.Vertices).Min(vertex => vertex.Position.Y);
            var alignedTop = aligned.DrawLists.SelectMany(list => list.Vertices).Min(vertex => vertex.Position.Y);
            var expectedTop = 10 + (20 - metrics.Ascent - metrics.Descent) * .5f + metrics.Ascent - baselineRaster.Baseline;
            check(MathF.Abs(top - expectedTop) < .001f,
                "unsnapped glyph baseline uses the real face ascent/descent within the requested line box");
            check(alignedTop == MathF.Round(expectedTop), "pixel snap aligns the completed glyph raster to a physical pixel");
        }
        finally{baselineRaster.PixelOwner?.Dispose();}
        var presets=new (string Name,float Height,Func<IUiView> Create)[]
        {
            ("Body1",20,()=>Fluent.Body1("Preset")),("Body1Strong",20,()=>Fluent.Body1Strong("Preset")),
            ("Body1Stronger",20,()=>Fluent.Body1Stronger("Preset")),("Body2",22,()=>Fluent.Body2("Preset")),
            ("Caption1",16,()=>Fluent.Caption1("Preset")),("Caption1Strong",16,()=>Fluent.Caption1Strong("Preset")),
            ("Caption1Stronger",16,()=>Fluent.Caption1Stronger("Preset")),("Caption2",14,()=>Fluent.Caption2("Preset")),
            ("Caption2Strong",14,()=>Fluent.Caption2Strong("Preset")),("Subtitle1",28,()=>Fluent.Subtitle1("Preset")),
            ("Subtitle2",22,()=>Fluent.Subtitle2("Preset")),("Subtitle2Stronger",22,()=>Fluent.Subtitle2Stronger("Preset")),
            ("Title1",40,()=>Fluent.Title1("Preset")),("Title2",36,()=>Fluent.Title2("Preset")),
            ("Title3",32,()=>Fluent.Title3("Preset")),("LargeTitle",52,()=>Fluent.LargeTitle("Preset")),
            ("Display",92,()=>Fluent.Display("Preset")),
        };
        foreach(var preset in presets)
        {
            using var view=(UiWidget)preset.Create();var size=default(UiVector2);
            using var data=Frame(ui=>{view.Render(ui);size=ui.GetItemRectSize();});
            check(size.Y==preset.Height&&size.X>0&&data.DrawLists.Any(list=>list.Commands.Count>0),
                "official named typography preset renders its real font and line height: "+preset.Name);
        }
        var reads=0;using var dynamic=Fluent.Title3(()=>{reads++;return "Dynamic";});
        using(var first=Frame(dynamic.Render)){}using(var second=Frame(dynamic.Render)){}
        check(reads==2,"generated typography getter overload executes every rendered frame");
        float beforeSize=0,afterSize=0;
        using(var data=Frame(ui=>{beforeSize=ui.GetFontSize();semibold.Render(ui);afterSize=ui.GetFontSize();})){}
        check(beforeSize==afterSize,"a named preset restores the surrounding font-size scope");
        context.SetContentScale(1.25f);
        var scaled=default(UiVector2);
        using(var data=Frame(ui=>{regular.Render(ui);scaled=ui.GetItemRectSize();})){}
        check(scaled.Y==20,"physical DPI scaling preserves the logical Fluent line box");
        context.SetContentScale(1);
        using var multiline=Fluent.Body1("One\nTwo");
        var multilineSize=default(UiVector2);
        using(var data=Frame(ui=>{multiline.Render(ui);multilineSize=ui.GetItemRectSize();})){}
        check(multilineSize.Y==40,"multiline typography uses exact baseline-to-baseline line spacing");
        string Raster(string fontPath)
        {
            if(!backend.TryRasterize(new(fontPath,null,"Fluent typography",14),out var raster))throw new InvalidOperationException("Font rasterization failed.");
            try{return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(raster.RgbaPixels.Span));}
            finally{raster.PixelOwner?.Dispose();}
        }
        check(Raster(fonts.Regular)!=Raster(fonts.Semibold)&&Raster(fonts.Semibold)!=Raster(fonts.Bold),
            "selected weight files produce distinct actual glyph pixels");
        using(var data=Frame(ui=>
        {
            ui.BeginLayer("typography-vector",new(false,1,default){Scene3D=new(Matrix4x4.Identity)});
            regular.Render(ui);ui.EndLayer();
        }))check(data.DrawLists.Any(list=>list.Scene3D.HasValue&&list.Vertices.Count>0),
            "typography preserves real vector glyph geometry inside a3D layer");
        using var unsupported=new UiContext(new(1,1,UiTextureFormat.Rgba8Unorm,[255,255,255,255]),new(1),new(2));
        unsupported.NewFrame(new(1f/60,new(100,100),new(1,1)),idle,new(0,0,100,100),UiTextSettings.Default);
        var rejected=false;
        try{unsupported.Render(new Screen(ui=>regular.Render(ui)));}catch(NotSupportedException){rejected=true;}
        check(rejected,"a backend without typography metrics fails explicitly instead of substituting atlas glyphs");
    }
    private static ushort Weight(string path)
    {
        var bytes=File.ReadAllBytes(path);
        var tables=BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(4,2));
        for(var i=0;i<tables;i++)
        {
            var entry=12+i*16;
            if(BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(entry,4))!=0x4F532F32)continue;
            var offset=checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(entry+8,4)));
            return BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(offset+4,2));
        }
        throw new InvalidOperationException("The actual font file has no OS/2 weight table.");
    }
    private sealed class Screen(Action<UiImmediateContext> render):UiScreen
    {public override void Render(UiImmediateContext ui)=>render(ui);}
}
