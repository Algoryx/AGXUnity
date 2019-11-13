using UnityEngine;
using UnityEditor;
using AGXUnity;
using GUI = AGXUnityEditor.Utils.GUI;

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
      TopEdgeLineToolEnable = true;
      CuttingEdgeLineToolEnable = true;
      CuttingDirectionLineToolEnable = true;
    }

    public override void OnSceneViewGUI( SceneView sceneView )
    {
    }

    public override void OnPreTargetMembersGUI()
    {
      GUI.Separator();

      if ( TopEdgeLineToolEnable ) {
        HandleLineToolInspectorGUI( TopEdgeLineTool, "Top Edge" );
        GUI.Separator();
      }
      if ( CuttingEdgeLineToolEnable ) {
        HandleLineToolInspectorGUI( CuttingEdgeLineTool, "Cutting Edge" );
        GUI.Separator();
      }
      if ( CuttingDirectionLineToolEnable ) {
        HandleLineToolInspectorGUI( CuttingDirectionLineTool, "Cutting Direction" );
        GUI.Separator();
      }
    }

    private void HandleLineToolInspectorGUI( LineTool lineTool, string name )
    {
      var backgroundStyle = new GUIStyle( InspectorEditor.Skin.label );
      backgroundStyle.normal.background = GUI.CreateColoredTexture( 4,
                                                                    4,
                                                                    Color.Lerp( EditorGUIUtility.isProSkin ?
                                                                                  GUI.ProBackgroundColor :
                                                                                  GUI.IndieBackgroundColor,
                                                                                lineTool.Color,
                                                                                0.15f ) );
      using ( GUI.AlignBlock.Center )
        GUILayout.Label( GUI.MakeLabel( name, 18, true ), InspectorEditor.Skin.label );
      GUI.Separator();
      using ( new GUI.Indent( 24 ) )
      using ( new GUILayout.VerticalScope( backgroundStyle ) )
        lineTool.OnInspectorGUI();
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

    private bool TopEdgeLineToolEnable
    {
      get { return TopEdgeLineTool != null; }
      set
      {
        if ( value && !TopEdgeLineToolEnable )
          AddChild( new LineTool( Shovel.TopEdge ) { Color = Color.yellow } );
        else if ( !value )
          RemoveChild( TopEdgeLineTool );
      }
    }

    private bool CuttingEdgeLineToolEnable
    {
      get { return CuttingEdgeLineTool != null; }
      set
      {
        if ( value && !CuttingEdgeLineToolEnable )
          AddChild( new LineTool( Shovel.CuttingEdge ) { Color = Color.red } );
        else if ( !value )
          RemoveChild( CuttingEdgeLineTool );
      }
    }

    private bool CuttingDirectionLineToolEnable
    {
      get { return CuttingDirectionLineTool != null; }
      set
      {
        if ( value && !CuttingDirectionLineToolEnable )
          AddChild( new LineTool( Shovel.CuttingDirection ) { Color = Color.green } );
        else if ( !value )
          RemoveChild( CuttingDirectionLineTool );
      }
    }
  }
}
