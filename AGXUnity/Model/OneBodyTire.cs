using UnityEngine;

namespace AGXUnity.Model
{
  /// <summary>
  /// One body tire model which enables effective contact reduction
  /// and the friction directions are updated given the tire frame.
  /// </summary>
  [AddComponentMenu( "AGXUnity/Model/One Body Tire" )]
  public class OneBodyTire : Tire
  {
    /// <summary>
    /// Native instance agxModel.OneBodyTire created in Start/Initialize.
    /// </summary>
    public agxModel.OneBodyTire Native { get; private set; } = null;

    [SerializeField]
    private RigidBody m_rigidBody = null;

    /// <summary>
    /// Tire rigid body.
    /// </summary>
    [IgnoreSynchronization]
    [AllowRecursiveEditing]
    public RigidBody RigidBody
    {
      get { return m_rigidBody; }
      set
      {
        if ( Native != null ) {
          Debug.LogWarning( "OneBodyTire assign RigidBody during runtime isn't supported.", this );
          return;
        }
        m_rigidBody = value;
      }
    }

    [SerializeField]
    private float m_radius = 1.0f;

    /// <summary>
    /// Radius of the tire.
    /// </summary>
    [IgnoreSynchronization]
    [ClampAboveZeroInInspector]
    public float Radius
    {
      get { return m_radius; }
      set
      {
        if ( Native != null ) {
          Debug.LogWarning( "OneBodyTire assign Radius during runtime isn't supported.", this );
          return;
        }
        m_radius = value;
      }
    }

    /// <summary>
    /// Assign rigid body and estimate the radius.
    /// </summary>
    /// <param name="rigidBody">Tire rigid body.</param>
    /// <returns>True if assigned, otherwise false.</returns>
    public bool SetRigidBody( RigidBody rigidBody )
    {
      if ( rigidBody == null ) {
        Debug.LogWarning( "OneBodyTire.SetTire: Tire rigid body is null, use property RigidBody instead.", this );
        return false;
      }

      RigidBody = rigidBody;
      Radius    = FindRadius( RigidBody );

      return true;
    }

    protected override bool Initialize()
    {
      if ( !LicenseManager.LicenseInfo.HasModuleLogError( LicenseInfo.Module.AGXTires, this ) )
        return false;

      if ( RigidBody == null ) {
        Debug.LogError( "OneBodyTire failed to initialize: Tire rigid body is null.", this );
        return false;
      }

      var nativeRigidBody = RigidBody.GetInitialized<RigidBody>()?.Native;
      if ( nativeRigidBody == null ) {
        Debug.LogError( "OneBodyTire failed to initialize: Rigid body initialization failed.", RigidBody );
        return false;
      }

      Native = new agxModel.OneBodyTire( nativeRigidBody, Radius, new agx.Frame( FindNativeTransform( RigidBody ) ) );

      GetSimulation().add( Native );

      return true;
    }

    protected override void OnDestroy()
    {
      if ( Simulation.HasInstance )
        GetSimulation().remove( Native );
      Native = null;
      base.OnDestroy();
    }
  }
}
