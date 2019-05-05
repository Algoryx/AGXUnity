using System.Linq;
using UnityEngine;
using UnityEditor;
using AGXUnity;
using AGXUnity.Utils;
using GUI = AGXUnityEditor.Utils.GUI;

namespace AGXUnityEditor.Tools
{
  [CustomTool( typeof( Wire ) )]
  public class WireTool : RouteTool<Wire, WireRouteNode>
  {
    public Wire Wire { get; private set; }

    public WireTool( Wire wire )
      : base( wire, wire.Route )
    {
      Wire = wire;
      NodeVisualRadius += () => { return Wire.Radius; };
    }

    protected override string GetNodeTypeString( RouteNode node )
    {
      var wireNode = node as WireRouteNode;
      return GUI.AddColorTag( wireNode.Type.ToString().SplitCamelCase(), GetColor( wireNode ) );
    }

    protected override Color GetNodeColor( RouteNode node )
    {
      return GetColor( node as WireRouteNode );
    }

    public override void OnPostTargetMembersGUI( InspectorEditor editor )
    {
      var skin = InspectorEditor.Skin;

      var beginWinches = editor.Targets<Wire, WireWinch>( wire => wire.BeginWinch ).Where( winch => winch != null );
      var endWinches   = editor.Targets<Wire, WireWinch>( wire => wire.EndWinch ).Where( winch => winch != null );

      if ( beginWinches.Count() > 0 ) {
        GUI.Separator();
        GUILayout.Label( GUI.MakeLabel( "Begin winch", true ), skin.label );
        using ( new GUI.Indent( 12 ) ) {
          if ( beginWinches.Count() != editor.NumTargets )
            AGXUnity.Utils.GUI.WarningLabel( "Not all selected wires has a begin winch.", skin );
          InspectorEditor.DrawMembersGUI( beginWinches.ToArray() );
        }
        GUI.Separator();
      }
      if ( endWinches.Count() > 0 ) {
        if ( beginWinches.Count() == 0 )
          GUI.Separator();

        GUILayout.Label( GUI.MakeLabel( "End winch", true ), skin.label );
        using ( new GUI.Indent( 12 ) ) {
          if ( endWinches.Count() != editor.NumTargets )
            AGXUnity.Utils.GUI.WarningLabel( "Not all selected wires has an end winch.", skin );
          InspectorEditor.DrawMembersGUI( endWinches.ToArray() );
        }
        GUI.Separator();
      }
    }

    protected override void OnPreFrameGUI( WireRouteNode node, GUISkin skin )
    {
      using ( new GUI.Indent( 12 ) ) {
        node.Type = (Wire.NodeType)EditorGUILayout.EnumPopup( GUI.MakeLabel( "Type" ), node.Type, skin.button );

        GUI.Separator();
      }
    }

    protected override void OnNodeCreate( WireRouteNode newNode, WireRouteNode refNode, bool addPressed )
    {
      if ( !addPressed && refNode != null )
        newNode.Type = refNode.Type;
      else
        newNode.Type = Wire.NodeType.FreeNode;
    }

    private Color GetColor( WireRouteNode node )
    {
      return node.Type == Wire.NodeType.BodyFixedNode ?
               Color.HSVToRGB( 26.0f / 300.0f, 0.77f, 0.52f ) :
             node.Type == Wire.NodeType.FreeNode ?
               Color.HSVToRGB( 200.0f / 300.0f, 0.77f, 0.92f ) :
             node.Type == Wire.NodeType.ConnectingNode ?
               Color.blue :
             node.Type == Wire.NodeType.ContactNode ?
               new Color( 0.85f, 0.15f, 1.0f ) :
             node.Type == Wire.NodeType.EyeNode ?
               Color.Lerp( Color.green, Color.black, 0.1f ) :
               Color.Lerp( Color.red, Color.black, 0.1f );
    }
  }
}
