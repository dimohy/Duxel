using System.Numerics;
using Duxel.Core;

internal static class DefaultTextRenderingTests
{
    internal static void Run(Func<string,Action<UiImmediateContext>,byte[]> capture,Action<bool,string> check)
    {
        const string content="Fluent Text 한글";
        using var text=Fluent.Text(content);using var body=Fluent.Body1(content);
        void Draw(UiImmediateContext ui,IUiView view,bool vector)
        {
            if(vector)ui.BeginLayer("default-text-vector",new(false,1,default){Scene3D=new(Matrix4x4.Identity,false)});
            ui.SetCursorScreenPos(new(20,20));view.Render(ui);
            if(vector)ui.EndLayer();
        }
        foreach(var vector in new[]{false,true})
        {
            var reference=capture("default-text-body1-"+vector,ui=>Draw(ui,body,vector));
            var generated=capture("default-text-generated-"+vector,ui=>Draw(ui,text,vector));
            check(reference.SequenceEqual(generated),"default Text matches actual Body1 pixels in "+(vector?"vector3D":"raster2D"));
        }
        var current=content;var reads=0;
        using var dynamic=Fluent.Text(()=>{reads++;return current;});
        var first=capture("default-text-dynamic-before",ui=>Draw(ui,dynamic,false));current="Changed Text";
        var second=capture("default-text-dynamic-after",ui=>Draw(ui,dynamic,false));
        check(reads==2&&!first.SequenceEqual(second),"default Text getter executes once and changes actual GPU pixels");
    }
}
