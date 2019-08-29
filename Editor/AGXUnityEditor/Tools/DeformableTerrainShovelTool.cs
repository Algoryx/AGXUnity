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
      //var lineVisualRadius = 0.05f;

      //TopEdgeVisual.Visible = true;
      //TopEdgeVisual.SetTransform( Shovel.TopEdge.Start.Position,
      //                            Shovel.TopEdge.End.Position,
      //                            lineVisualRadius );
      //Handles.Label( Shovel.TopEdge.End.Position +
      //               5.0f * AGXUnity.Rendering.Spawner.Utils.FindConstantScreenSizeScale( Shovel.TopEdge.End.Position,
      //                                                                                    sceneView.camera ) * lineVisualRadius * Vector3.up,
      //               GUI.MakeLabel( "Top edge", Color.yellow, true ),
      //               InspectorEditor.Skin.label );

      //CuttingEdgeVisual.Visible = true;
      //CuttingEdgeVisual.SetTransform( Shovel.CuttingEdge.Start.Position,
      //                                Shovel.CuttingEdge.End.Position,
      //                                lineVisualRadius );
      //Handles.Label( Shovel.CuttingEdge.End.Position +
      //               5.0f * AGXUnity.Rendering.Spawner.Utils.FindConstantScreenSizeScale( Shovel.CuttingEdge.End.Position,
      //                                                                                    sceneView.camera ) * lineVisualRadius * Vector3.up,
      //               GUI.MakeLabel( "Cutting edge", Color.red, true ),
      //               InspectorEditor.Skin.label );

      //CuttingDirectionVisual.Visible = true;
      //CuttingDirectionVisual.SetTransform( Shovel.CuttingDirection.Start.Position,
      //                                     Shovel.CuttingDirection.End.Position,
      //                                     lineVisualRadius );
      //Handles.Label( Shovel.CuttingDirection.End.Position +
      //               5.0f * AGXUnity.Rendering.Spawner.Utils.FindConstantScreenSizeScale( Shovel.CuttingDirection.End.Position,
      //                                                                                    sceneView.camera ) * lineVisualRadius * Vector3.up,
      //               GUI.MakeLabel( "Cutting direction", Color.green, true ),
      //               InspectorEditor.Skin.label );
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
