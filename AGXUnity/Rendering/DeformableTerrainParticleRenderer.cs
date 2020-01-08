using UnityEngine;
using AGXUnity.Utils;
using AGXUnity.Model;

namespace AGXUnity.Rendering
{
  [AddComponentMenu( "AGXUnity/Deformable Terrain Particle Renderer" )]
  [RequireComponent( typeof( DeformableTerrain ) )]
  public class DeformableTerrainParticleRenderer : ScriptComponent
  {
    [HideInInspector]
    public DeformableTerrain DeformableTerrain { get; private set; } = null;

    [SerializeField]
    private GameObject m_granuleInstance = null;

    public GameObject GranuleInstance
    {
      get { return m_granuleInstance; }
      set
      {
        if ( DeformableTerrain != null && value != m_granuleInstance )
          Destroy( transform.childCount );

        m_granuleInstance = value;

        if ( DeformableTerrain != null )
          Synchronize();
      }
    }

    protected override bool Initialize()
    {
      if ( GranuleInstance == null ) {
        Debug.LogWarning( "Granule prefab instance is null. Nothing to render.", this );
        return false;
      }

      DeformableTerrain = GetComponent<DeformableTerrain>();
      if ( DeformableTerrain == null )
        return false;

      Synchronize();

      return true;
    }

    protected override void OnEnable()
    {
      Simulation.Instance.StepCallbacks.PostStepForward += PostUpdate;
      Synchronize();
    }

    protected override void OnDisable()
    {
      // We may not "change GameObject hierarchy" when the actual
      // game object is being destroyed, e.g., when hitting stop.
      if ( gameObject.activeSelf )
        Destroy( transform.childCount );

      if ( Simulation.HasInstance )
        Simulation.Instance.StepCallbacks.PostStepForward -= PostUpdate;
    }

    protected override void OnDestroy()
    {
      DeformableTerrain = null;

      base.OnDestroy();
    }

    private void PostUpdate()
    {
      Synchronize();
    }

    private void Synchronize()
    {
      if ( GranuleInstance == null || DeformableTerrain == null || DeformableTerrain.Native == null )
        return;

      var soilSimulation = DeformableTerrain.Native.getSoilSimulationInterface();
      var granulars = soilSimulation.getSoilParticles();
      var numGranulars = (int)granulars.size();
      if ( numGranulars > transform.childCount )
        Create( numGranulars - transform.childCount );
      else if ( transform.childCount > numGranulars )
        Destroy( transform.childCount - numGranulars );

      for ( int i = 0; i < numGranulars; ++i ) {
        var granule = granulars.at( (uint)i );
        var instance = transform.GetChild( i );
        instance.position = granule.position().ToHandedVector3();
        instance.rotation = granule.rotation().ToHandedQuaternion();
        instance.localScale = Vector3.one * 2.0f * (float)granule.getRadius();
        granule.ReturnToPool();
      }
    }

    private void Create( int count )
    {
      for ( int i = 0; i < count; ++i ) {
        var instance = Instantiate( GranuleInstance );
        instance.transform.SetParent( transform );
      }
    }

    private void Destroy( int count )
    {
      var numRemaining = System.Math.Max( transform.childCount - count, 0 );
      while ( true ) {
        if ( transform.childCount <= numRemaining )
          break;

        var instance = transform.GetChild( transform.childCount - 1 );
        var prevChildCount = transform.childCount;

        instance.SetParent( null );

        // During OnDisable before OnDestroy, SetParent has no effect
        // for some reason. Exit loop and rely on Unity to remove our
        // children.
        if ( transform.childCount == prevChildCount )
          break;

        Destroy( instance.gameObject );
      }
    }
  }
}