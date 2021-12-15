using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using AGXUnity;
using AGXUnity.Utils;
using GUI = AGXUnity.Utils.GUI;
using Object = UnityEngine.Object;

namespace AGXUnityEditor.Tools
{
  [CustomTool( typeof( Cable ) )]
  public class CableTool : RouteTool<Cable, CableRouteNode>
  {
    public Cable Cable
    {
      get
      {
        return Targets[ 0 ] as Cable;
      }
    }

    public CableTool( Object[] targets )
      : base( targets )
    {
      NodeVisualRadius = () => { return Cable.Radius; };
    }

    protected override string GetNodeTypeString( RouteNode node )
    {
      var cableNode = node as CableRouteNode;
      return InspectorEditor.Skin.TagTypename( cableNode.Type.ToString() );
    }

    protected override Color GetNodeColor( RouteNode node )
    {
      return GetColor( node as CableRouteNode );
    }

    protected override void OnPreFrameGUI( CableRouteNode node )
    {
      using ( InspectorGUI.IndentScope.Single ) {
        node.Type = (Cable.NodeType)EditorGUILayout.EnumPopup( GUI.MakeLabel( "Type" ),
                                                               node.Type,
                                                               InspectorEditor.Skin.Popup );
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
  [CanEditMultipleObjects]
  public class CablePropertiesEditor : Editor
  {
    public override void OnInspectorGUI()
    {
      if ( Utils.KeyHandler.HandleDetectKeyOnGUI( this.targets, Event.current ) )
        return;

      var selected = from obj in this.targets select obj as CableProperties;
      if ( selected.Count() == 0 )
        return;

      Undo.RecordObjects( selected.ToArray(), "Cable properties" );

      var skin = InspectorEditor.Skin;
      Tuple<PropertyWrapper, CableProperties.Direction, object> changed = null;
      using ( InspectorGUI.IndentScope.Single ) {
        foreach ( CableProperties.Direction dir in CableProperties.Directions ) {
          var tmp = OnPropertyGUI( dir, selected.First() );
          if ( tmp != null )
            changed = tmp;
        }
      }

      if ( changed != null ) {
        foreach ( var properties in selected ) {
          changed.Item1.ConditionalSet( properties[ changed.Item2 ], changed.Item3 );
          EditorUtility.SetDirty( properties );
        }
      }
    }

    private Tuple<PropertyWrapper, CableProperties.Direction, object> OnPropertyGUI( CableProperties.Direction dir,
                                                                                     CableProperties properties )
    {
      Tuple<PropertyWrapper, CableProperties.Direction, object> changed = null;
      var data = EditorData.Instance.GetData( properties, "CableProperty" + dir.ToString() );
      if ( InspectorGUI.Foldout( data, GUI.MakeLabel( dir.ToString() ) ) ) {
        using ( InspectorGUI.IndentScope.Single ) {
          var wrappers = PropertyWrapper.FindProperties<CableProperty>( System.Reflection.BindingFlags.Instance |
                                                                        System.Reflection.BindingFlags.Public );
          foreach ( var wrapper in wrappers ) {
            // Poisson's ratio is only used in the Twist direction and
            // has HideInInspector in CableProperties. Overriding HideInInspector
            // and rendering Poisson's ratio if dir == CableProperties.Direction.Twist.
            var renderFloat = wrapper.GetContainingType() == typeof( float ) &&
                              ( InspectorEditor.ShouldBeShownInInspector( wrapper.Member ) ||
                                ( wrapper.Member.Name == "PoissonsRatio" && dir == CableProperties.Direction.Twist ) );
            if ( renderFloat ) {
              var value = EditorGUILayout.FloatField( InspectorGUI.MakeLabel( wrapper.Member ),
                                                      wrapper.Get<float>( properties[ dir ] ) );
              if ( UnityEngine.GUI.changed ) {
                changed = new Tuple<PropertyWrapper, CableProperties.Direction, object>( wrapper, dir, value );
                UnityEngine.GUI.changed = false;
              }
            }
          }
        }
      }
      return changed;
    }
  }
}
