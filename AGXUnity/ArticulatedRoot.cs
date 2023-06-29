using UnityEngine;

namespace AGXUnity
{
  /// <summary>
  /// Hierarchal root component of articulated models, such as robots.
  /// This component enables one or more rigid body instances in the
  /// hierarchy tree, i.e, one or more rigid bodies as children to
  /// another rigid body.
  /// 
  /// Normally, if a rigid body instance finds another rigid body as
  /// its parent, the rigid body throws an exception because it's
  /// unknown when the transforms are written. This component handles
  /// these transform updates, assuring each parent transform has been
  /// written before its children.
  /// </summary>
  [AddComponentMenu( "AGXUnity/Articulated Root" )]
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#articulated-root" )]
  public class ArticulatedRoot : ScriptComponent
  {
    /// <summary>
    /// All (child) rigid body instances belonging to this articulated root.
    /// This array of bodies is cached during runtime.
    /// </summary>
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

    /// <summary>
    /// Finds all child rigid bodies using GetComponentsInChildren.
    /// </summary>
    /// <returns>Array of child rigid bodies.</returns>
    /// <seealso cref="RigidBodies"/>
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
