using System.Linq;
using UnityEngine;
using UnityEditor;
using AGXUnity;
using AGXUnity.Utils;
using GUI = AGXUnity.Utils.GUI;

namespace AGXUnityEditor.Tools
{
  [CustomTool( typeof( Wire ) )]
  public class WireTool : RouteTool<Wire, WireRouteNode>
  {
    public Wire Wire
    {
      get
      {
        return Targets[ 0 ] as Wire;
      }
    }

    public WireTool( Object[] targets )
      : base( targets )
    {
      NodeVisualRadius += () => { return Wire.Radius; };
    }

    protected override string GetNodeTypeString( RouteNode node )
    {
      var wireNode = node as WireRouteNode;
      return InspectorEditor.Skin.TagTypename( wireNode.Type.ToString() );
    }

    protected override Color GetNodeColor( RouteNode node )
    {
      return GetColor( node as WireRouteNode );
    }

    public override void OnPostTargetMembersGUI()
    {
      var skin = InspectorEditor.Skin;

      var beginWinches = GetTargets<Wire, WireWinch>( wire => wire.BeginWinch ).Where( winch => winch != null );
      var endWinches   = GetTargets<Wire, WireWinch>( wire => wire.EndWinch ).Where( winch => winch != null );

      if ( beginWinches.Count() > 0 ) {
        GUILayout.Label( GUI.MakeLabel( "Begin winch", true ), skin.Label );
        using ( InspectorGUI.IndentScope.Single ) {
          if ( beginWinches.Count() != NumTargets )
            InspectorGUI.WarningLabel( "Not all selected wires has a begin winch." );
          InspectorEditor.DrawMembersGUI( beginWinches.ToArray() );
        }
      }
      if ( endWinches.Count() > 0 ) {
        GUILayout.Label( GUI.MakeLabel( "End winch", true ), skin.Label );
        using ( InspectorGUI.IndentScope.Single ) {
          if ( endWinches.Count() != NumTargets )
            InspectorGUI.WarningLabel( "Not all selected wires has an end winch." );
          InspectorEditor.DrawMembersGUI( endWinches.ToArray() );
        }
      }
    }

    protected override void OnPreFrameGUI( WireRouteNode node )
    {
      node.Type = (Wire.NodeType)EditorGUILayout.EnumPopup( GUI.MakeLabel( "Type" ),
                                                            node.Type,
                                                            InspectorEditor.Skin.Popup );
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
