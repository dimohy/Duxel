// FBA: DSL 마크업 + 인터랙션(Drag/Slider/Color/Child/Popup) 시연
#:property TargetFramework=net10.0
#:package Duxel.Windows.App@*-*

using Duxel.App;
using Duxel.Core.Dsl;

var dslText = """
Window "DSL Interaction"
  MenuBar
    Menu "File"
      MenuItem Id="new" Text="New"
      MenuItem Id="exit" Text="Exit"
  SeparatorText "Drag/Slider"
  DragFloat Id="dragf" Text="Drag F" Value=0.5 Speed=0.01 Min=0 Max=1
  DragInt Id="dragi" Text="Drag I" Value=10 Speed=1 Min=0 Max=100
  DragFloat2 Id="drag2" Text="Drag2" Speed=0.01 Min=-1 Max=1
  SliderFloat2 Id="sl2" Text="Slider2" Min=0 Max=1
  VSliderFloat Id="vsl" Text="VSlider" Size="24,140" Min=0 Max=1
  SeparatorText "Colors"
  ColorPicker3 Id="pick" Text="Pick"
  ColorButton "color" "#33AAFF" "28,28"
  SeparatorText "Child"
  Child Id="child" Size="320,180" Border=true
    Text "Child content"
    Checkbox Id="child-check" Text="Check me" Default=false
    InputText Id="child-name" Text="Name" MaxLength=32
    ProgressBar Fraction=0.25 Size="200,16" Overlay="Quarter"
  SeparatorText "Popup"
  Text "Right-click anywhere for context popup"
  PopupContextWindow "ctx"
    MenuItem Id="copy" Text="Copy"
    MenuItem Id="paste" Text="Paste"
""";

var doc = UiDslParser.Parse(dslText);
var state = new UiDslState();

DuxelApp.Run(new DuxelAppOptions
{
    Window = new DuxelWindowOptions
    {
        Title = "DSL Interaction"
    },
    Dsl = new DuxelDslOptions
    {
        State = state,
        Render = emitter => doc.Emit(emitter)
    }
});

