using UnityEngine;
using UnityEditor;
using AGXUnity;
using AGXUnity.Utils;
using GUI = AGXUnityEditor.Utils.GUI;

namespace AGXUnityEditor.Tools
{
  [CustomTool( typeof( Cable ) )]
  public class CableTool : RouteTool<Cable, CableRouteNode>
  {
    public Cable Cable { get; private set; }

    public CableTool( Cable cable )
      : base( cable, cable.Route )
    {
      Cable = cable;
      NodeVisualRadius = () => { return Cable.Radius; };
    }

    protected override string GetNodeTypeString( RouteNode node )
    {
      var cableNode = node as CableRouteNode;
      return GUI.AddColorTag( cableNode.Type.ToString().SplitCamelCase(), GetColor( cableNode ) );
    }

    protected override Color GetNodeColor( RouteNode node )
    {
      return GetColor( node as CableRouteNode );
    }

    protected override void OnPreFrameGUI( CableRouteNode node, GUISkin skin )
    {
      using ( new GUI.Indent( 12 ) ) {
        node.Type = (Cable.NodeType)EditorGUILayout.EnumPopup( GUI.MakeLabel( "Type" ), node.Type, skin.button );

        GUI.Separator();
      }
    }

    protected override void OnNodeCreate( CableRouteNode newNode, CableRouteNode refNode, bool addPressed )
    {
      if ( !addPressed && refNode != null )
        newNode.Type = refNode.Type;
      else
        newNode.Type = Cable.NodeType.FreeNode;
    }

    private Color GetColor( CableRouteNode node )
    {
      return node.Type == Cable.NodeType.BodyFixedNode ?
               Color.HSVToRGB( 26.0f / 300.0f, 0.77f, 0.52f ) :
               Color.HSVToRGB( 200.0f / 300.0f, 0.77f, 0.92f );
    }
  }

  [CustomEditor( typeof( CableProperties ) )]
  public class CablePropertiesEditor : BaseEditor<CableProperties>
  {
    protected override bool OverrideOnInspectorGUI( CableProperties properties, GUISkin skin )
    {
      if ( properties == null )
        return true;

      Undo.RecordObject( properties, "Cable properties" );

      using ( GUI.AlignBlock.Center )
        GUILayout.Label( GUI.MakeLabel( "Cable Properties", true ), skin.label );

      GUI.Separator();

      using ( new GUI.Indent( 12 ) ) {
        foreach ( CableProperties.Direction dir in CableProperties.Directions ) {
          OnPropertyGUI( dir, properties, skin );
          GUI.Separator();
        }
      }

      if ( UnityEngine.GUI.changed )
        EditorUtility.SetDirty( properties );

      return true;
    }

    private void OnPropertyGUI( CableProperties.Direction dir, CableProperties properties, GUISkin skin )
    {
      var data = EditorData.Instance.GetData( properties, "CableProperty" + dir.ToString() );
      if ( GUI.Foldout( data, GUI.MakeLabel( dir.ToString() ), skin ) ) {
        using ( new GUI.Indent( 12 ) ) {
          GUI.Separator();

          properties[ dir ].YoungsModulus = Mathf.Clamp( EditorGUILayout.FloatField( GUI.MakeLabel( "Young's modulus" ), properties[ dir ].YoungsModulus ), 1.0E-6f, float.PositiveInfinity );
          properties[ dir ].YieldPoint = Mathf.Clamp( EditorGUILayout.FloatField( GUI.MakeLabel( "Yield point" ), properties[ dir ].YieldPoint ), 0.0f, float.PositiveInfinity );
          properties[ dir ].Damping = Mathf.Clamp( EditorGUILayout.FloatField( GUI.MakeLabel( "Spook damping" ), properties[ dir ].Damping ), 0.0f, float.PositiveInfinity );
        }
      }
    }
  }
}
