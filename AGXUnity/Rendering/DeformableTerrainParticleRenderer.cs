using UnityEngine;
using AGXUnity.Utils;
using AGXUnity.Model;

namespace AGXUnity.Rendering
{
  [AddComponentMenu( "AGXUnity/Deformable Terrain Particle Renderer" )]
  [RequireComponent( typeof( DeformableTerrain ) )]
  public class DeformableTerrainParticleRenderer : ScriptComponent
  {
    public enum GranuleRenderMode
    {
      GameObject = 0,
      DrawMeshInstanced = 1
    }

    [HideInInspector]
    public DeformableTerrain DeformableTerrain { get; private set; } = null;

    [Tooltip("Render particles using cloned GameObjects or with Graphics.DrawMeshInstanced")]
    [SerializeField]
    private GranuleRenderMode m_renderMode = GranuleRenderMode.GameObject;

    [SerializeField]
    private GameObject m_granuleInstance = null;
    
    public Mesh GranuleDrawInstancedMesh = null;
    public Material GranuleDrawInstancedMaterial = null;

    private Matrix4x4[] m_granuleMatrices;
    private int m_numGranulars = 0;
    private MaterialPropertyBlock m_properties = null;

    public GranuleRenderMode RenderMode
    {
      get { return m_renderMode; }
      set
      {
        if (m_renderMode == GranuleRenderMode.GameObject && value != GranuleRenderMode.GameObject)
        {
          Destroy(m_numGranulars);
        }

        m_renderMode = value;
      }
    }

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

      m_granuleMatrices = new Matrix4x4[1023];

      m_properties = new MaterialPropertyBlock();

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

    private void Update()
    {
      if (RenderMode == GranuleRenderMode.DrawMeshInstanced && m_numGranulars > 0)
      {
        if (m_numGranulars < 1024)
          Graphics.DrawMeshInstanced(GranuleDrawInstancedMesh, 0, GranuleDrawInstancedMaterial, m_granuleMatrices, m_numGranulars, m_properties, UnityEngine.Rendering.ShadowCastingMode.On, true);
        else // DrawMeshInstanced only supports up to 1023 meshes for each call, we need to subdivide if we have more particles than that
        { 
          for (int i = 0; i < m_numGranulars; i += 1023)
          {
            int count = Mathf.Min(1023, m_numGranulars - i);
            Graphics.DrawMeshInstanced(GranuleDrawInstancedMesh, 0, GranuleDrawInstancedMaterial, new System.ArraySegment<Matrix4x4>(m_granuleMatrices, i, count).Array, count, m_properties, UnityEngine.Rendering.ShadowCastingMode.On, true);
          }
        }
      }
    }

    private void Synchronize()
    {
      if ( DeformableTerrain == null || DeformableTerrain.Native == null )
        return;

      var soilSimulation = DeformableTerrain.Native.getSoilSimulationInterface();
      var granulars      = soilSimulation.getSoilParticles();
      m_numGranulars   = (int)granulars.size();

      if (RenderMode == GranuleRenderMode.DrawMeshInstanced)
      {
        if (GranuleDrawInstancedMaterial == null || GranuleDrawInstancedMesh == null)
          return;

        // Use 1023 as arbitrary block size since that is the amount of particles that can be drawn with DrawMeshInstanced
        if (m_numGranulars > m_granuleMatrices.Length)
          System.Array.Resize(ref m_granuleMatrices, (m_numGranulars / 1023 + 1) * 1023);

        for (int i = 0; i < m_numGranulars; i++)
        {
          var granule = granulars.at((uint)i);

          m_granuleMatrices[i] = Matrix4x4.TRS(
            granule.position().ToHandedVector3(),
            granule.rotation().ToHandedQuaternion(),
            Vector3.one * 2.0f * (float)granule.getRadius()); // Assuming unit size of the instance, scale to diameter of the granule.

          // Return the proxy class to the pool to avoid garbage.
          granule.ReturnToPool();
        }
      }
      else if (RenderMode == GranuleRenderMode.GameObject)
      {
        if (GranuleInstance == null)
          return;

        // More granular instances comparing to last time, create
        // more instances to match numGranulars.
        if (m_numGranulars > transform.childCount)
          Create(m_numGranulars - transform.childCount);
        // Less granular instances comparing to last time, destroy.
        else if (transform.childCount > m_numGranulars)
          Destroy(transform.childCount - m_numGranulars);

        Debug.Assert(transform.childCount == m_numGranulars);

        for (int i = 0; i < m_numGranulars; ++i)
        {
          var granule = granulars.at((uint)i);
          var instance = transform.GetChild(i);
          instance.position = granule.position().ToHandedVector3();
          instance.rotation = granule.rotation().ToHandedQuaternion();

          // Assuming unit size of the instance, scale to diameter
          // of the granule.
          instance.localScale = Vector3.one * 2.0f * (float)granule.getRadius();

          // Return the proxy class to the pool to avoid garbage.
          granule.ReturnToPool();
        }
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
