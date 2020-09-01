using UnityEngine;
using UnityEditor;
using AGXUnity;

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
      InspectorGUI.ToolArrayGUI( this, ArticulatedRoot.RigidBodies, "Rigid Bodies" );
    }
  }
}
