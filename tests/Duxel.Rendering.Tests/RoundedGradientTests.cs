using System.Numerics;
using Duxel.Core;
using Duxel.Vulkan;

internal static class RoundedGradientTests
{
    internal static UiRendererFrameMetrics MeshMetrics,PackedMetrics;
    internal static void Run(VulkanRendererBackend renderer,Func<string,Action<UiImmediateContext>,byte[]> capture,
        Func<Action<UiImmediateContext>,UiDrawData> build,Action<bool,string> check,UiTextureId white,UiRect clip,bool packedFirst=false)
    {
        var previousSamples=renderer.LastFrameMetrics.MsaaSamples;
        var color=new UiColor(255,255,255);var prefix=new UiRect(4,4,12,12);var root=new UiRect(36,32,140,48);
        foreach(var samples in new[]{1,4})
        {
            renderer.SetMsaaSamples(samples);
            foreach(var radius in new[]{0f,12f})
            {
                void Draw(UiImmediateContext ui,bool gradient)
                {
                    var list=ui.GetWindowDrawList();
                    list.AddRectFilled(prefix,new(255,0,0),white);
                    if(gradient)list.AddRectFilledGradient(root,new(new(root.X,root.Y),new(root.X+root.Width,root.Y),color,color,color),white,clip,radius);
                    else list.AddRectFilledRounded(root,color,white,radius,clip);
                }
                var expected=capture($"gradient-mask-reference-{samples}-{radius}",ui=>Draw(ui,false));
                var actual=capture($"gradient-mask-packed-{samples}-{radius}",ui=>Draw(ui,true));
                check(expected.SequenceEqual(actual),"constant gradient matches analytic mask with an odd slot prefix: MSAA"+samples+" radius"+radius);
            }
            void Layers(UiImmediateContext ui,bool gradient,bool cached)
            {
                for(var i=0;i<2;i++)
                {
                    if(ui.BeginLayer("gradient-layer-"+i,new(cached,1,default){Scene3D=new(Matrix4x4.Identity,false)}))
                    {
                        var list=ui.GetWindowDrawList();var bounds=root with {Y=root.Y+i*64};
                        list.AddRectFilled(prefix with {Y=4+i*64},new(255,0,0),white);
                        if(gradient)list.AddRectFilledGradient(bounds,new(new(bounds.X,bounds.Y),new(bounds.X+bounds.Width,bounds.Y),color,color,color),white,clip,12);
                        else list.AddRectFilledRounded(bounds,color,white,12,clip);
                    }
                    ui.EndLayer();
                }
            }
            var reference=capture("gradient-layers-reference-"+samples,ui=>Layers(ui,false,false));
            capture("gradient-layers-warm-"+samples,ui=>Layers(ui,true,true));
            var retained=capture("gradient-layers-retained-"+samples,ui=>Layers(ui,true,true));
            check(reference.SequenceEqual(retained),"retained projected gradients preserve packed addresses and masks at MSAA"+samples);
            check(renderer.LastFrameMetrics.CompiledLayerDraws>0,"projected gradient test executes the compiled-layer descriptor path at MSAA"+samples);
            void Depth(UiImmediateContext ui,bool gradient,bool reverse,float opacity=1)
            {
                void Back()
                {
                    if(ui.BeginLayer("gradient-depth-back",new(false,1,default){Scene3D=new(Matrix4x4.CreateTranslation(0,0,.5f))}))
                        ui.GetWindowDrawList().AddRectFilled(root,new(0,0,255),white,clip);
                    ui.EndLayer();
                }
                void Front()
                {
                    if(ui.BeginLayer("gradient-depth-front",new(false,opacity,default){Scene3D=new(Matrix4x4.Identity,opacity==1)}))
                    {
                        var list=ui.GetWindowDrawList();
                        if(gradient)list.AddRectFilledGradient(root,new(new(root.X,root.Y),new(root.X+root.Width,root.Y),color,color,color),white,clip,12);
                        else list.AddRectFilledRounded(root,color,white,12,clip);
                    }
                    ui.EndLayer();
                }
                if(reverse){Front();Back();}else{Back();Front();}
            }
            var depthReference=capture("gradient-depth-reference-"+samples,ui=>Depth(ui,false,false));
            var depth=capture("gradient-depth-"+samples,ui=>Depth(ui,true,false));
            var reversed=capture("gradient-depth-reversed-"+samples,ui=>Depth(ui,true,true));
            check(depthReference.SequenceEqual(depth)&&depth.SequenceEqual(reversed),"gradient masks preserve depth samples under reversed submission at MSAA"+samples);
            var alphaReference=capture("gradient-alpha-reference-"+samples,ui=>Depth(ui,false,false,.5f));
            var alpha=capture("gradient-alpha-"+samples,ui=>Depth(ui,true,false,.5f));
            check(alphaReference.SequenceEqual(alpha),"gradient material opacity preserves analytic coverage at MSAA"+samples);
        }
        renderer.SetMsaaSamples(1);
        var start=new UiColor(32,32,32);var middle=new UiColor(32,128,224);
        void Many(UiImmediateContext ui,bool packed)
        {
            var list=ui.GetWindowDrawList();
            for(var i=0;i<256;i++)
            {
                var bounds=new UiRect(8+i%16*20,8+i/16*10,16,6);
                var origin=new UiVector2(bounds.X-4,bounds.Y);var end=new UiVector2(bounds.X+12,bounds.Y);
                if(packed)list.AddRectFilledGradient(bounds,new(origin,end,start,middle,start),white,clip);
                else
                {
                    list.AddRectFilled(bounds,start,white,clip);
                    list.PushClipRect(bounds);list.PushTexture(white);
                    list.AddRectFilledMultiColor(new(origin.X,origin.Y,8,6),start,middle,middle,start);
                    list.AddRectFilledMultiColor(new(origin.X+8,origin.Y,8,6),middle,start,start,middle);
                    list.PopTexture();list.PopClipRect();
                }
            }
        }
        byte[] oldPixels,newPixels;
        UiRendererFrameMetrics oldMetrics,newMetrics;
        if(packedFirst)
        {
            newPixels=capture("gradient-many-packed",ui=>Many(ui,true));newMetrics=renderer.LastFrameMetrics;
            oldPixels=capture("gradient-many-triangles",ui=>Many(ui,false));oldMetrics=renderer.LastFrameMetrics;
        }
        else
        {
            oldPixels=capture("gradient-many-triangles",ui=>Many(ui,false));oldMetrics=renderer.LastFrameMetrics;
            newPixels=capture("gradient-many-packed",ui=>Many(ui,true));newMetrics=renderer.LastFrameMetrics;
        }
        MeshMetrics=oldMetrics;PackedMetrics=newMetrics;
        check(oldPixels.SequenceEqual(newPixels),"packed three-stop gradients preserve the previous mesh pixels");
        check(newMetrics.DrawCalls<oldMetrics.DrawCalls&&newMetrics.DynamicUploadBytes<oldMetrics.DynamicUploadBytes,
            $"256 gradients reduce actual draws {oldMetrics.DrawCalls}->{newMetrics.DrawCalls} and upload {oldMetrics.DynamicUploadBytes}->{newMetrics.DynamicUploadBytes}");
        using var data=build(ui=>Many(ui,true));
        var commands=data.DrawLists.SelectMany(list=>list.Commands).Where(command=>command.ElementCount>0).ToArray();
        check(commands.Length==1&&commands[0].PrimitiveInstanceCount==256&&commands[0].ElementCount==512,
            "adjacent gradients batch as256 instances in512 existing32B slots");
        void Changing(UiImmediateContext ui,int state)
        {
            if(ui.BeginLayer("gradient-changing",new(false,state==3?.5f:1,new(state==4?20:0,0))))
            {
                var origin=new UiVector2(root.X,root.Y);
                ui.GetWindowDrawList().AddRectFilledGradient(root,
                    new(origin,new(root.X+root.Width,root.Y),start,state==1?color:middle,start,state==2?.25f:.5f),
                    white,state==5?clip with {Width=100}:clip,12);
            }
            ui.EndLayer();
        }
        foreach(var samples in new[]{1,4})
        {
            renderer.SetMsaaSamples(samples);
            for(var state=0;state<6;state++)
            {
                var selected=state;
                renderer.ValidationInvalidateDamage();
                var full=capture($"gradient-change-full-{samples}-{state}",ui=>Changing(ui,selected));
                var equal=true;
                for(var repeat=0;repeat<4;repeat++)
                    equal&=full.SequenceEqual(capture($"gradient-change-retained-{samples}-{state}-{repeat}",ui=>Changing(ui,selected)));
                check(equal,$"gradient color, stop, opacity, translation and clip preserve acquired-image pixels: MSAA{samples} state{state}");
                check(renderer.LastFrameMetrics.DrawCalls==0&&renderer.LastFrameMetrics.DynamicUploadBytes==0,
                    $"unchanged gradient requires no draw or upload: MSAA{samples} state{state}");
            }
        }
        renderer.SetMsaaSamples(previousSamples);
    }
}
