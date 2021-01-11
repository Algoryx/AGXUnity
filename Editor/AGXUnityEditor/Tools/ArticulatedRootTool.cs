using UnityEngine;
using UnityEditor;
using AGXUnity;

using GUI = AGXUnity.Utils.GUI;

namespace AGXUnityEditor.Tools
{
  [CustomTool( typeof( ArticulatedRoot ) )]
  public class ArticulatedRootTool : CustomTargetTool
  {
    public ArticulatedRoot ArticulatedRoot { get { return Targets[ 0 ] as ArticulatedRoot; } }

    public ArticulatedRootTool( Object[] targets )
      : base( targets )
    {
    }

    public override void OnPostTargetMembersGUI()
    {
      if ( Targets.Length == 1 )
        InspectorGUI.ToolArrayGUI( this, ArticulatedRoot.RigidBodies, "Rigid Bodies" );
      else {
        for ( int i = 0; i < NumTargets; ++i ) {
          var articulatedRoot = Targets[ i ] as ArticulatedRoot;
          InspectorGUI.ToolArrayGUI( this,
                                     articulatedRoot.RigidBodies,
                                     $"{GUI.AddColorTag( articulatedRoot.name, InspectorGUISkin.BrandColor )}: Rigid Bodies" );
          if ( i < NumTargets - 1 )
            InspectorGUI.Separator();
        }
      }
    }
  }
}
