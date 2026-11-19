namespace Duxel.Platform.Linux;

// Derived from the official AT-SPI2 interface contracts; parsed once when the bridge starts.
internal static class AtSpiSchema
{
    internal const string Xml = """
        <node>
          <interface name="org.a11y.atspi.Image">
            <property name="ImageDescription" type="s" access="read" />
            <property name="ImageLocale" type="s" access="read" />
            <method name="GetImageExtents">
              <arg direction="in" name="coordType" type="u" />
              <arg direction="out" type="(iiii)" />
            </method>
            <method name="GetImagePosition">
              <arg direction="in" name="coordType" type="u" />
              <arg direction="out" name="x" type="i" />
              <arg direction="out" name="y" type="i" />
            </method>
            <method name="GetImageSize">
              <arg direction="out" name="width" type="i" />
              <arg direction="out" name="height" type="i" />
            </method>
          </interface>
          <interface name="org.a11y.atspi.Accessible">
            <property name="version" type="u" access="read" />
            <property name="Name" type="s" access="read" />
            <property name="Description" type="s" access="read" />
            <property name="Parent" type="(so)" access="read"></property>
            <property name="ChildCount" type="i" access="read" />
            <property name="Locale" type="s" access="read" />
            <property name="AccessibleId" type="s" access="read" />
            <property name="HelpText" type="s" access="read" />
            <method name="GetChildAtIndex">
              <arg direction="in" name="index" type="i" />
              <arg direction="out" type="(so)" />
            </method>
            <method name="GetChildren">
              <arg direction="out" type="a(so)" />
            </method>
            <method name="GetIndexInParent">
              <arg direction="out" type="i" />
            </method>
            <method name="GetRelationSet">
              <arg direction="out" type="a(ua(so))" />
            </method>
            <method name="GetRole">
              <arg direction="out" type="u" />
            </method>
            <method name="GetRoleName">
              <arg direction="out" type="s" />
            </method>
            <method name="GetLocalizedRoleName">
              <arg direction="out" type="s" />
            </method>
            <method name="GetState">
              <arg direction="out" type="au" />
            </method>
            <method name="GetAttributes">
              <arg direction="out" type="a{ss}" />
            </method>
            <method name="GetApplication">
              <arg direction="out" type="(so)" />
            </method>
            <method name="GetInterfaces">
              <arg direction="out" type="as" />
            </method>
          </interface>
          <interface name="org.a11y.atspi.Application">
            <property name="ToolkitName" type="s" access="read" />
            <property name="Version" type="s" access="read"></property>
            <property name="ToolkitVersion" type="s" access="read" />
            <property name="AtspiVersion" type="s" access="read" />
            <property name="InterfaceVersion" type="u" access="read" />
            <property name="Id" type="i" access="readwrite" />
            <method name="GetLocale">
              <arg direction="in" name="lctype" type="u" />
              <arg direction="out" type="s" />
            </method>
            <method name="GetApplicationBusAddress">
              <arg direction="out" type="s" />
            </method>
          </interface>
          <interface name="org.a11y.atspi.Action">
            <property name="version" type="u" access="read" />
            <property name="NActions" type="i" access="read" />
            <method name="GetDescription">
              <arg type="i" name="index" direction="in" />
              <arg type="s" direction="out" />
            </method>
            <method name="GetName">
              <arg type="i" name="index" direction="in" />
              <arg type="s" direction="out" />
            </method>
            <method name="GetLocalizedName">
              <arg type="i" name="index" direction="in" />
              <arg type="s" direction="out" />
            </method>
            <method name="GetKeyBinding">
              <arg type="i" name="index" direction="in" />
              <arg type="s" direction="out" />
            </method>
            <method name="GetActions">
              <arg direction="out" type="a(sss)" />
            </method>
            <method name="DoAction">
              <arg direction="in" name="index" type="i" />
              <arg direction="out" type="b" />
            </method>
          </interface>
          <interface name="org.a11y.atspi.Component">
            <property name="version" type="u" access="read" />
            <method name="Contains">
              <arg direction="in" name="x" type="i" />
              <arg direction="in" name="y" type="i" />
              <arg direction="in" name="coord_type" type="u" />
              <arg direction="out" type="b" />
            </method>
            <method name="GetAccessibleAtPoint">
              <arg direction="in" name="x" type="i" />
              <arg direction="in" name="y" type="i" />
              <arg direction="in" name="coord_type" type="u" />
              <arg direction="out" type="(so)" />
            </method>
            <method name="GetExtents">
              <arg direction="in" name="coord_type" type="u" />
              <arg direction="out" type="(iiii)" />
            </method>
            <method name="GetPosition">
              <arg direction="in" name="coord_type" type="u" />
              <arg direction="out" name="x" type="i" />
              <arg direction="out" name="y" type="i" />
            </method>
            <method name="GetSize">
              <arg direction="out" name="width" type="i" />
              <arg direction="out" name="height" type="i" />
            </method>
            <method name="GetLayer">
              <arg direction="out" type="u" />
            </method>
            <method name="GetMDIZOrder">
              <arg direction="out" type="n" />
            </method>
            <method name="GrabFocus">
              <arg direction="out" type="b" />
            </method>
            <method name="GetAlpha">
              <arg direction="out" type="d" />
            </method>
            <method name="SetExtents">
              <arg direction="in" name="x" type="i" />
              <arg direction="in" name="y" type="i" />
              <arg direction="in" name="width" type="i" />
              <arg direction="in" name="height" type="i" />
              <arg direction="in" name="coord_type" type="u" />
              <arg direction="out" type="b" />
            </method>
            <method name="SetPosition">
              <arg direction="in" name="x" type="i" />
              <arg direction="in" name="y" type="i" />
              <arg direction="in" name="coord_type" type="u" />
              <arg direction="out" type="b" />
            </method>
            <method name="SetSize">
              <arg direction="in" name="width" type="i" />
              <arg direction="in" name="height" type="i" />
              <arg direction="out" type="b" />
            </method>
            <method name="ScrollTo">
              <arg direction="in" name="type" type="u" />
              <arg direction="out" type="b" />
            </method>
            <method name="ScrollToPoint">
              <arg direction="in" name="coord_type" type="u" />
              <arg direction="in" name="x" type="i" />
              <arg direction="in" name="y" type="i" />
              <arg direction="out" type="b" />
            </method>
          </interface>
          <interface name="org.a11y.atspi.Cache">
            <property name="version" type="u" access="read" />
            <method name="GetItems">
              <arg direction="out" name="nodes" type="a((so)(so)(so)iiassusau)" />
            </method>
            <signal name="AddAccessible">
              <arg name="nodeAdded" type="((so)(so)(so)iiassusau)" />
            </signal>
            <signal name="RemoveAccessible">
              <arg name="nodeRemoved" type="(so)" />
            </signal>
            <signal name="Ready" />
          </interface>
          <interface name="org.a11y.atspi.Value">
            <property name="version" type="u" access="read" />
            <property name="MinimumValue" type="d" access="read" />
            <property name="MaximumValue" type="d" access="read" />
            <property name="MinimumIncrement" type="d" access="read" />
            <property name="CurrentValue" type="d" access="readwrite" />
            <property name="Text" type="s" access="read" />
          </interface>
          <interface name="org.a11y.atspi.Selection">
            <property name="version" type="u" access="read" />
            <property name="NSelectedChildren" type="i" access="read" />
            <method name="GetSelectedChild"><arg direction="in" type="i" /><arg direction="out" type="(so)" /></method>
            <method name="SelectChild"><arg direction="in" type="i" /><arg direction="out" type="b" /></method>
            <method name="DeselectSelectedChild"><arg direction="in" type="i" /><arg direction="out" type="b" /></method>
            <method name="IsChildSelected"><arg direction="in" type="i" /><arg direction="out" type="b" /></method>
            <method name="SelectAll"><arg direction="out" type="b" /></method>
            <method name="ClearSelection"><arg direction="out" type="b" /></method>
            <method name="DeselectChild"><arg direction="in" type="i" /><arg direction="out" type="b" /></method>
          </interface>
          <interface name="org.a11y.atspi.Text">
            <property name="version" type="u" access="read" />
            <property name="CharacterCount" type="i" access="read" />
            <property name="CaretOffset" type="i" access="read" />
            <method name="GetTextBeforeOffset"><arg direction="in" type="i" /><arg direction="in" type="u" /><arg direction="out" type="s" /><arg direction="out" type="i" /><arg direction="out" type="i" /></method>
            <method name="GetTextAtOffset"><arg direction="in" type="i" /><arg direction="in" type="u" /><arg direction="out" type="s" /><arg direction="out" type="i" /><arg direction="out" type="i" /></method>
            <method name="GetTextAfterOffset"><arg direction="in" type="i" /><arg direction="in" type="u" /><arg direction="out" type="s" /><arg direction="out" type="i" /><arg direction="out" type="i" /></method>
            <method name="GetStringAtOffset"><arg direction="in" type="i" /><arg direction="in" type="u" /><arg direction="out" type="s" /><arg direction="out" type="i" /><arg direction="out" type="i" /></method>
            <method name="GetDefaultAttributes"><arg direction="out" type="a{ss}" /></method>
            <method name="GetDefaultAttributeSet"><arg direction="out" type="a{ss}" /></method>
            <method name="GetAttributes"><arg direction="in" type="i" /><arg direction="out" type="a{ss}" /><arg direction="out" type="i" /><arg direction="out" type="i" /></method>
            <method name="GetAttributeRun"><arg direction="in" type="i" /><arg direction="in" type="b" /><arg direction="out" type="a{ss}" /><arg direction="out" type="i" /><arg direction="out" type="i" /></method>
            <method name="GetAttributeValue"><arg direction="in" type="i" /><arg direction="in" type="s" /><arg direction="out" type="s" /></method>
            <method name="GetCharacterExtents"><arg direction="in" type="i" /><arg direction="in" type="u" /><arg direction="out" type="i" /><arg direction="out" type="i" /><arg direction="out" type="i" /><arg direction="out" type="i" /></method>
            <method name="GetRangeExtents"><arg direction="in" type="i" /><arg direction="in" type="i" /><arg direction="in" type="u" /><arg direction="out" type="i" /><arg direction="out" type="i" /><arg direction="out" type="i" /><arg direction="out" type="i" /></method>
            <method name="GetBoundedRanges"><arg direction="in" type="i" /><arg direction="in" type="i" /><arg direction="in" type="i" /><arg direction="in" type="i" /><arg direction="in" type="u" /><arg direction="in" type="u" /><arg direction="in" type="u" /><arg direction="out" type="a(iisv)" /></method>
            <method name="ScrollSubstringTo"><arg direction="in" type="i" /><arg direction="in" type="i" /><arg direction="in" type="u" /><arg direction="out" type="b" /></method>
            <method name="ScrollSubstringToPoint"><arg direction="in" type="i" /><arg direction="in" type="i" /><arg direction="in" type="u" /><arg direction="in" type="i" /><arg direction="in" type="i" /><arg direction="out" type="b" /></method>
            <method name="GetOffsetAtPoint"><arg direction="in" type="i" /><arg direction="in" type="i" /><arg direction="in" type="u" /><arg direction="out" type="i" /></method>
            <method name="GetText"><arg direction="in" type="i" /><arg direction="in" type="i" /><arg direction="out" type="s" /></method>
            <method name="GetCharacterAtOffset"><arg direction="in" type="i" /><arg direction="out" type="i" /></method>
            <method name="SetCaretOffset"><arg direction="in" type="i" /><arg direction="out" type="b" /></method>
            <method name="GetNSelections"><arg direction="out" type="i" /></method>
            <method name="GetSelection"><arg direction="in" type="i" /><arg direction="out" type="i" /><arg direction="out" type="i" /></method>
            <method name="AddSelection"><arg direction="in" type="i" /><arg direction="in" type="i" /><arg direction="out" type="b" /></method>
            <method name="SetSelection"><arg direction="in" type="i" /><arg direction="in" type="i" /><arg direction="in" type="i" /><arg direction="out" type="b" /></method>
            <method name="RemoveSelection"><arg direction="in" type="i" /><arg direction="out" type="b" /></method>
          </interface>
          <interface name="org.a11y.atspi.EditableText">
            <property name="version" type="u" access="read" />
            <method name="SetTextContents"><arg direction="in" type="s" /><arg direction="out" type="b" /></method>
            <method name="InsertText"><arg direction="in" type="i" /><arg direction="in" type="s" /><arg direction="in" type="i" /><arg direction="out" type="b" /></method>
            <method name="CopyText"><arg direction="in" type="i" /><arg direction="in" type="i" /></method>
            <method name="CutText"><arg direction="in" type="i" /><arg direction="in" type="i" /><arg direction="out" type="b" /></method>
            <method name="DeleteText"><arg direction="in" type="i" /><arg direction="in" type="i" /><arg direction="out" type="b" /></method>
            <method name="PasteText"><arg direction="in" type="i" /><arg direction="out" type="b" /></method>
          </interface>
          <interface name="org.a11y.atspi.Table">
            <property name="version" type="u" access="read" />
            <property name="NRows" type="i" access="read" />
            <property name="NColumns" type="i" access="read" />
            <property name="Caption" type="(so)" access="read" />
            <property name="Summary" type="(so)" access="read" />
            <property name="NSelectedRows" type="i" access="read" />
            <property name="NSelectedColumns" type="i" access="read" />
            <method name="GetAccessibleAt"><arg direction="in" type="i" /><arg direction="in" type="i" /><arg direction="out" type="(so)" /></method>
            <method name="GetIndexAt"><arg direction="in" type="i" /><arg direction="in" type="i" /><arg direction="out" type="i" /></method>
            <method name="GetRowAtIndex"><arg direction="in" type="i" /><arg direction="out" type="i" /></method>
            <method name="GetColumnAtIndex"><arg direction="in" type="i" /><arg direction="out" type="i" /></method>
            <method name="GetRowDescription"><arg direction="in" type="i" /><arg direction="out" type="s" /></method>
            <method name="GetColumnDescription"><arg direction="in" type="i" /><arg direction="out" type="s" /></method>
            <method name="GetRowExtentAt"><arg direction="in" type="i" /><arg direction="in" type="i" /><arg direction="out" type="i" /></method>
            <method name="GetColumnExtentAt"><arg direction="in" type="i" /><arg direction="in" type="i" /><arg direction="out" type="i" /></method>
            <method name="GetRowHeader"><arg direction="in" type="i" /><arg direction="out" type="(so)" /></method>
            <method name="GetColumnHeader"><arg direction="in" type="i" /><arg direction="out" type="(so)" /></method>
            <method name="GetSelectedRows"><arg direction="out" type="ai" /></method>
            <method name="GetSelectedColumns"><arg direction="out" type="ai" /></method>
            <method name="IsRowSelected"><arg direction="in" type="i" /><arg direction="out" type="b" /></method>
            <method name="IsColumnSelected"><arg direction="in" type="i" /><arg direction="out" type="b" /></method>
            <method name="IsSelected"><arg direction="in" type="i" /><arg direction="in" type="i" /><arg direction="out" type="b" /></method>
            <method name="AddRowSelection"><arg direction="in" type="i" /><arg direction="out" type="b" /></method>
            <method name="AddColumnSelection"><arg direction="in" type="i" /><arg direction="out" type="b" /></method>
            <method name="RemoveRowSelection"><arg direction="in" type="i" /><arg direction="out" type="b" /></method>
            <method name="RemoveColumnSelection"><arg direction="in" type="i" /><arg direction="out" type="b" /></method>
            <method name="GetRowColumnExtentsAtIndex"><arg direction="in" type="i" /><arg direction="out" type="b" /><arg direction="out" type="i" /><arg direction="out" type="i" /><arg direction="out" type="i" /><arg direction="out" type="i" /><arg direction="out" type="b" /></method>
          </interface>
          <interface name="org.a11y.atspi.TableCell">
            <property name="version" type="u" access="read" />
            <property name="ColumnSpan" type="i" access="read" />
            <property name="Position" type="(ii)" access="read" />
            <property name="RowSpan" type="i" access="read" />
            <property name="Table" type="(so)" access="read" />
            <method name="GetRowColumnSpan"><arg direction="out" type="b" /><arg direction="out" type="i" /><arg direction="out" type="i" /><arg direction="out" type="i" /><arg direction="out" type="i" /></method>
            <method name="GetColumnHeaderCells"><arg direction="out" type="a(so)" /></method>
            <method name="GetRowHeaderCells"><arg direction="out" type="a(so)" /></method>
          </interface>
        </node>
        """;
}
