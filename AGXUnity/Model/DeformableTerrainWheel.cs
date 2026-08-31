using System.Linq;
using UnityEngine;

namespace AGXUnity.Model
{
  [AddComponentMenu( "AGXUnity/Deformable Terrain Wheel" )]
  [DisallowMultipleComponent]
  [RequireComponent( typeof( RigidBody ) )]
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#deformable-terrain-wheel" )]
  public class DeformableTerrainWheel : ScriptComponent
  {
    /// <summary>
    /// Native instance of this terrain wheel.
    /// </summary>
    [HideInInspector]
    public agxTerrain.TerrainWheel Native { get; private set; } = null;

    /// <summary>
    /// Rigid body component of this terrain wheel.
    /// </summary>
    public RigidBody RigidBody { get { return m_rb ?? ( m_rb = GetComponent<RigidBody>() ); } }

    [SerializeField]
    private DeformableTerrainWheelSettings m_settings = null;

    [AllowRecursiveEditing]
    public DeformableTerrainWheelSettings Settings
    {
      get { return m_settings; }
      set
      {
        if ( Native != null && m_settings != null && m_settings != value )
          m_settings.Unregister( this );

        m_settings = value;

        if ( Native != null && m_settings != null )
          m_settings.Register( this );
      }
    }

    /// <summary>
    /// Helper that will output a warning if the contact material in use by the terrain wheel doesn't have the correct force model.
    /// NB: if using multiple shape materials on the terrain this is not reliable!
    /// </summary>
    [field: SerializeField]
    [Tooltip( "Helper that will output a warning if the contact material in use by the terrain wheel doesn't have the correct force model." )]
    public bool WarnIfNotUsingCorrectForceModel { get; set; } = false;

    protected override bool Initialize()
    {
      var rb = RigidBody?.GetInitialized<RigidBody>()?.Native;
      if ( rb == null ) {
        Debug.LogWarning( "Unable to find RigidBody component for DeformableTerrainWheel - wheel instance ignored.", this );
        return false;
      }

      var cylinders = RigidBody.Shapes.Where( s => s is Collide.Cylinder );

      if ( cylinders.Count() != 1 ) {
        Debug.LogWarning( $"DeformableTerrainWheel requires exactly 1 Cylinder shape in the RigidBody, found {cylinders.Count()}.", this );
        return false;
      }

      var cylinder = (cylinders.First() as Collide.Cylinder).GetInitialized<Collide.Cylinder>().Native;
      if ( cylinder == null ) {
        Debug.LogWarning( "Unable to initialize Cylinder shape for DeformableTerrainWheel.", this );
        return false;
      }

      // TODO - this is needed because of a bug in agx. Remove this is that is fixed.
      var shapeMaterial = cylinder.getGeometry().getMaterial();
      if ( shapeMaterial == null ) {
        Debug.LogWarning( "Unable to initialize Cylinder shape for DeformableTerrainWheel - ShapeMaterial needs to be set!", this );
        return false;
      }

      Native = new agxTerrain.TerrainWheel( cylinder );

      if ( Settings == null ) {
        var settings = ScriptAsset.Create<DeformableTerrainWheelSettings>();
        Settings = settings;
      }

      GetSimulation().add( Native );

      return true;
    }

    protected override void OnEnable()
    {
      base.OnEnable();
    }

    protected override void OnDisable()
    {
      base.OnDisable();
    }

    private void LateUpdate()
    {
      if ( !WarnIfNotUsingCorrectForceModel || Native?.getActiveTerrain() == null )
        return;

      if ( !ActiveContactMaterialUsesTerrainWheelForceModel )
        Debug.LogWarning( "Active Contact Material is NOT using terrainWheelForceModel!" );
    }

    public bool ActiveContactMaterialUsesTerrainWheelForceModel => GetActiveContactMaterial()?.getFrictionModel()?.asTerrainWheelForceModel() != null;

    private agx.ContactMaterial GetActiveContactMaterial()
    {
      if ( Native == null || GetSimulation() == null )
        return null;

      var wheelShapeMaterial = Native.getWheelGeometry()?.getMaterial();
      var terrainShapeMaterial = Native.getActiveTerrain()?.getMaterial();

      if ( wheelShapeMaterial == null || terrainShapeMaterial == null )
        return null;

      var cm = GetSimulation()?.getMaterialManager()?.getContactMaterial( wheelShapeMaterial, terrainShapeMaterial );
      return cm;
    }

    protected override void OnDestroy()
    {
      if ( Settings != null )
        Settings.Unregister( this );

      if ( Simulation.HasInstance )
        GetSimulation().remove( Native );

      Native = null;

      base.OnDestroy();
    }

    private void Reset()
    {
      if ( GetComponent<RigidBody>() == null )
        Debug.LogError( "Component: DeformableTerrainWheel requires a RigidBody component.", this );
    }

    private RigidBody m_rb = null;
  }
}
