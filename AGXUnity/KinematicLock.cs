using System.Collections.Generic;
using UnityEngine;

namespace AGXUnity
{
  public class KinematicLock : ScriptComponent
  {
    public agx.MergedBody Native { get; private set; }

    [SerializeField]
    private List<RigidBody> m_lockedBodies = new List<RigidBody>();
    public RigidBody[] LockedBodies => m_lockedBodies.ToArray();

    public void Add( RigidBody body )
    {
      m_lockedBodies.Add( body );
      if ( Native != null && m_lockedBodies.Count > 1 ) {
        var first = m_lockedBodies[0].GetInitialized().Native;
        var nativeBody = body.GetInitialized().Native;
        Native.add( new agx.MergedBody.EmptyEdgeInteraction( first, nativeBody ) );
      }
    }

    public void Remove( RigidBody body )
    {
      m_lockedBodies.Remove( body );
      if ( Native != null ) {
        var nativeBody = body.GetInitialized().Native;
        Native.remove( nativeBody );
      }
    }

    protected override bool Initialize()
    {
      Native = new agx.MergedBody();

      if ( m_lockedBodies.Count > 1 ) {
        var first = m_lockedBodies[0].GetInitialized().Native;
        for ( int i = 1; i < m_lockedBodies.Count; i++ ) {
          var body = m_lockedBodies[i].GetInitialized().Native;
          Native.add( new agx.MergedBody.EmptyEdgeInteraction( first, body ) );
        }
      }

      Simulation.Instance.Native.add( Native );

      return true;
    }
  }
}
