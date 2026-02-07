// FBA: DSL 마크업으로 UI를 선언적으로 구성하는 방법 시연 (정적 레이아웃 중심)
#:property TargetFramework=net10.0
#:package Duxel.App@*-*

using Duxel.App;
using Duxel.Core.Dsl;

var dslText = """
Window "DSL Showcase"
  Text "Buttons"
  Row
    Button Id="play" Text="Play"
    Button Id="stop" Text="Stop"
  SeparatorText "Inputs"
  InputText Id="name" Text="Name" MaxLength=64
  InputInt Id="age" Text="Age" Value=0
  InputFloat Id="speed" Text="Speed" Format="0.00"
  Checkbox Id="vsync" Text="VSync" Default=true
  SliderFloat Id="volume" Text="Volume" Min=0 Max=1
  SliderInt Id="quality" Text="Quality" Min=0 Max=3
  Combo Id="preset" Text="Preset" Items="Low|Medium|High"
  ListBox Id="list" Text="Items" Items="One|Two|Three|Four" HeightItems=4
  ColorEdit4 Id="tint" Text="Tint"
  ProgressBar 0.5 "220,16" "Half"
  SeparatorText "Tabs"
  TabBar "tabs"
    TabItem "Tab A"
      Text "Tab A content"
    TabItem "Tab B"
      Text "Tab B content"
  SeparatorText "Table"
  Table "table" 3
    TableSetupColumn "Name"
    TableSetupColumn "Value"
    TableSetupColumn "Notes"
    TableHeadersRow
    TableNextRow
    TableNextColumn
    Text "Row1"
    TableNextColumn
    Text "42"
    TableNextColumn
    Text "Hello"
    TableNextRow
    TableNextColumn
    Text "Row2"
    TableNextColumn
    Text "99"
    TableNextColumn
    Text "World"
  SeparatorText "Tree"
  TreeNode "Node 1"
    Text "Child 1"
    TreeNode "Node 1.1"
      Text "Leaf"
  TreeNode "Node 2"
    Text "Child 2"
""";

var doc = UiDslParser.Parse(dslText);
var state = new UiDslState();

DuxelApp.Run(new DuxelAppOptions
{
    Window = new DuxelWindowOptions
    {
        Title = "DSL Showcase"
    },
    Dsl = new DuxelDslOptions
    {
        State = state,
        Render = emitter => doc.Emit(emitter)
    }
});

