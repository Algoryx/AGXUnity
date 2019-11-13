using System;
using UnityEngine;
using UnityEditor;
using AGXUnity;

using GUI = AGXUnityEditor.Utils.GUI;
using Object = UnityEngine.Object;

namespace AGXUnityEditor.Tools
{
  [CustomTool( typeof( DeformableTerrainShovel ) )]
  public class DeformableTerrainShovelTool : CustomTargetTool
  {
    public DeformableTerrainShovel Shovel { get { return Targets[ 0 ] as DeformableTerrainShovel; } }

    public DeformableTerrainShovelTool( Object[] targets )
      : base( targets )
    {
    }

    public override void OnAdd()
    {
      AddChild( new LineTool( Shovel.TopEdge )
      {
        Color                = Color.yellow,
        Name                 = "Top Edge",
        IsSingleInstanceTool = false,
        UndoRedoRecordObject = Shovel
      } );
      AddChild( new LineTool( Shovel.CuttingEdge )
      {
        Color                = Color.red,
        Name                 = "Cutting Edge",
        IsSingleInstanceTool = false,
        UndoRedoRecordObject = Shovel
      } );
      AddChild( new LineTool( Shovel.CuttingDirection )
      {
        Color                = Color.green,
        Name                 = "Cutting Direction",
        IsSingleInstanceTool = false,
        UndoRedoRecordObject = Shovel
      } );
    }

    public override void OnSceneViewGUI( SceneView sceneView )
    {
    }

    public override void OnPreTargetMembersGUI()
    {
      GUI.Separator();

      HandleLineToolInspectorGUI( TopEdgeLineTool, "Top Edge" );
      GUI.Separator();

      HandleLineToolInspectorGUI( CuttingEdgeLineTool, "Cutting Edge" );
      GUI.Separator();

      HandleLineToolInspectorGUI( CuttingDirectionLineTool, "Cutting Direction" );
      GUI.Separator();
    }

    private void HandleLineToolInspectorGUI( LineTool lineTool, string name )
    {
      var backgroundStyle = new GUIStyle( InspectorEditor.Skin.label );
      backgroundStyle.normal.background = GUI.CreateColoredTexture( 1,
                                                                    1,
                                                                    Color.Lerp( EditorGUIUtility.isProSkin ?
                                                                                  GUI.ProBackgroundColor :
                                                                                  GUI.IndieBackgroundColor,
                                                                                lineTool.Color,
                                                                                0.15f ) );
      using ( new GUILayout.VerticalScope( backgroundStyle ) ) {
        if ( !GUI.Foldout( GetLineToggleData( name ),
                           GUI.MakeLabel( name, true ),
                           InspectorEditor.Skin ) )
          return;
        GUI.Separator();
        using ( new GUI.Indent( 24 ) )
          lineTool.OnInspectorGUI();
      }
    }

    private LineTool TopEdgeLineTool
    {
      get { return FindActive<LineTool>( tool => tool.Line == Shovel.TopEdge ); }
    }

    private LineTool CuttingEdgeLineTool
    {
      get { return FindActive<LineTool>( tool => tool.Line == Shovel.CuttingEdge ); }
    }

    private LineTool CuttingDirectionLineTool
    {
      get { return FindActive<LineTool>( tool => tool.Line == Shovel.CuttingDirection ); }
    }

    private EditorDataEntry GetLineToggleData( string name )
    {
      return EditorData.Instance.GetData( Shovel, name );
    }
  }
}
