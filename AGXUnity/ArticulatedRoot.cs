using System;
using System.Linq;
using UnityEngine;

namespace AGXUnity
{
  public class ArticulatedRoot : ScriptComponent
  {
    [AllowRecursiveEditing]
    public RigidBody[] RigidBodies
    {
      get
      {
        return m_cachedRigidBodies.Length == 0 ?
                 GetRigidBodies() :
                 m_cachedRigidBodies;
      }
      private set
      {
        m_cachedRigidBodies = value ?? new RigidBody[] { };
      }
    }

    public RigidBody[] GetRigidBodies()
    {
      return GetComponentsInChildren<RigidBody>();
    }

    protected override bool Initialize()
    {
      // Depth or breadth first search is valid because we
      // want to update parents before its children. It's
      // very unlikely for Unity to collect the components
      // in random order?
      RigidBodies = GetRigidBodies();
      if ( RigidBodies.Length == 0 )
        Debug.LogWarning( "ArticulatedRoot without bodies - this component can be removed.", this );

      Simulation.Instance.StepCallbacks.PostSynchronizeTransforms += OnPostSynchronizeTransforms;

      return true;
    }

    protected override void OnEnable()
    {
      // TODO: This is called before Initialize. How do we handle
      //       enabling and disabling of this articulated root?
      //       If a child rigid body is disabled we don't want to
      //       accidentally enable it.
    }

    protected override void OnDisable()
    {
    }

    protected override void OnDestroy()
    {
      RigidBodies = null;
      if ( Simulation.HasInstance )
        Simulation.Instance.StepCallbacks.PostSynchronizeTransforms -= OnPostSynchronizeTransforms;

      base.OnDestroy();
    }

    private void OnPostSynchronizeTransforms()
    {
      for ( int i = 0; i < m_cachedRigidBodies.Length; ++i )
        m_cachedRigidBodies[ i ].OnPostSynchronizeTransformsCallback();
    }

    private RigidBody[] m_cachedRigidBodies = new RigidBody[] { };
  }
}
