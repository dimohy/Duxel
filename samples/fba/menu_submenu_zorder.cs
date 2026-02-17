// FBA: 메뉴/서브메뉴 Z-Order 동작 검증 — 중첩 메뉴가 뒤의 컨트롤을 올바르게 가리는지 테스트
#:property TargetFramework=net10.0
#:property platform=windows
#:package Duxel.$(platform).App@*-*

using Duxel.App;
using Duxel.Core.Dsl;

var dslText = """
Window "Menu Z-Order"
  MenuBar
    Menu "File"
      MenuItem Id="new" Text="New"
      MenuItem Id="open" Text="Open"
      Menu "Recent"
        MenuItem Id="recent-1" Text="Report_2025.dui"
        MenuItem Id="recent-2" Text="Notes.dui"
        Menu "Archived"
          MenuItem Id="arch-1" Text="Archive_1.dui"
          MenuItem Id="arch-2" Text="Archive_2.dui"
      MenuItem Id="exit" Text="Exit"
    Menu "View"
      MenuItem Id="show-grid" Text="Show Grid"
      MenuItem Id="show-guides" Text="Show Guides"
    Menu "Help"
      MenuItem Id="about" Text="About"
  SeparatorText "Interaction"
  Text "Hover and click controls while menus are open"
  SliderFloat Id="scale" Text="Scale" Min=0 Max=1 Default=0.5
  DragFloat Id="drag" Text="Drag" Min=0 Max=1 Default=0.25 Speed=0.01
  Checkbox Id="check" Text="Enable Feature" Default=true
  Button "Apply"
  SameLine
  Button "Reset"
""";

var doc = UiDslParser.Parse(dslText);

DuxelApp.Run(new DuxelAppOptions
{
    Window = new DuxelWindowOptions
    {
        Title = "Menu Z-Order"
    },
    Dsl = new DuxelDslOptions
    {
        Render = emitter => doc.Emit(emitter)
    }
});

