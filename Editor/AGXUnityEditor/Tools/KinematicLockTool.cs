using AGXUnity;
using UnityEditor;
using UnityEngine;

namespace AGXUnityEditor.Tools
{
  [CustomTool( typeof( KinematicLock ) )]
  public class KinematicLockTool : CustomTargetTool
  {

    public KinematicLock Lock { get { return Targets[ 0 ] as KinematicLock; } }

    public KinematicLockTool( Object[] targets )
      : base( targets )
    {
    }

    public override void OnPostTargetMembersGUI()
    {
      if ( NumTargets != 1 )
        return;

      Undo.RecordObject( Lock, "Locked bodies" );
      InspectorGUI.ToolListGUI( this,
                                Lock.LockedBodies,
                                "Locked Bodies",
                                OnAddLockedBody,
                                OnRemoveLockedBody,
                                null );
    }

    private void OnAddLockedBody( RigidBody locked )
    {
      if ( locked == null )
        return;

      Lock.Add( locked );
    }

    private void OnRemoveLockedBody( RigidBody locked )
    {
      Lock.Remove( locked );
    }
  }
}
