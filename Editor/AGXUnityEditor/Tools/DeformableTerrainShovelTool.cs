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
      TopEdgeLineTool = true;
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

    private bool TopEdgeLineTool
    {
      get { return FindActive<LineTool>( tool => tool.Line == Shovel.TopEdge ) != null; }
      set
      {
        if ( value && !TopEdgeLineTool )
          AddChild( new LineTool( Shovel.TopEdge ) );
        else if ( !value )
          RemoveChild( FindActive<LineTool>( tool => tool.Line == Shovel.TopEdge ) );
      }
    }
  }
}
