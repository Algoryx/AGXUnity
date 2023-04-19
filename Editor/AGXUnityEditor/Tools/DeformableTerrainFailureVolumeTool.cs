using AGXUnity.Model;
using UnityEditor;
using UnityEngine;
using GUI = AGXUnity.Utils.GUI;

namespace AGXUnityEditor.Tools
{
  [CustomTool( typeof( DeformableTerrainFailureVolume ) )]
  public class DeformableTerrainFailureVolumeTool : CustomTargetTool
  {
    public DeformableTerrainFailureVolume DeformableTerrainFailureVolume { get { return Targets[ 0 ] as DeformableTerrainFailureVolume; } }

    public DeformableTerrainFailureVolumeTool( Object[] targets )
      : base( targets )
    {
    }

    public override void OnPostTargetMembersGUI()
    {
      if ( NumTargets > 1 )
        return;

      Undo.RecordObject( DeformableTerrainFailureVolume, "Terrain add/remove." );

      InspectorGUI.ToolListGUI( this,
                                DeformableTerrainFailureVolume.Terrains,
                                "Terrains",
                                terrain => DeformableTerrainFailureVolume.Add( terrain ),
                                terrain => DeformableTerrainFailureVolume.Remove( terrain ) );
    }
  }
}
