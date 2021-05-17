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
      GameObject,
      DrawMeshInstanced
    }

    [HideInInspector]
    public DeformableTerrain DeformableTerrain { get; private set; } = null;

    [Tooltip("Render particles using cloned GameObjects or with Graphics.DrawMeshInstanced")]
    [SerializeField]
    private GranuleRenderMode m_renderMode = GranuleRenderMode.DrawMeshInstanced;

    public GranuleRenderMode RenderMode
    {
      get { return m_renderMode; }
      set
      {
        m_renderMode = value;

        if ( !IsSynchronizingProperties && DeformableTerrain != null )
          InitializeRenderMode();
      }
    }

    [SerializeField]
    private GameObject m_granuleInstance = null;

    public GameObject GranuleInstance
    {
      get { return m_granuleInstance; }
      set
      {
        var isChangedDuringRuntime = DeformableTerrain != null &&
                                     value != m_granuleInstance;
        if ( isChangedDuringRuntime )
          DestroyAll();

        m_granuleInstance = value;

        if ( isChangedDuringRuntime )
          InitializeRenderMode();
      }
    }

    protected override bool Initialize()
    {
      DeformableTerrain = GetComponent<DeformableTerrain>();
      if ( DeformableTerrain == null )
        return false;

      if ( !InitializeRenderMode() )
        return false;

      return true;
    }

    protected override void OnEnable()
    {
      Simulation.Instance.StepCallbacks.PostStepForward += PostUpdate;
      if ( State == States.INITIALIZED )
        InitializeRenderMode();
    }

    protected override void OnDisable()
    {
      // We may not "change GameObject hierarchy" when the actual
      // game object is being destroyed, e.g., when hitting stop.
      if ( gameObject.activeSelf )
        DestroyAll();

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

    private bool InitializeRenderMode()
    {
      DestroyAll();

      if ( GranuleInstance == null ) {
        Debug.LogError( "AGXUnity.Rendering.DeformableTerrainParticleRenderer: " +
                        "Render granule prefab instance is null.",
                        this );
        return false;
      }

      if ( RenderMode == GranuleRenderMode.DrawMeshInstanced ) {
        var filters = GranuleInstance.GetComponentsInChildren<MeshFilter>();
        if ( filters.Length != 1 ) {
          Debug.LogError( "AGXUnity.Rendering.DeformableTerrainParticleRenderer: " +
                          $"Invalid number of meshes ({filters.Length}) in GranuleInstance - expecting 1.",
                          GranuleInstance );
          return false;
        }

        if ( filters[ 0 ].sharedMesh == null ) {
          Debug.LogError( "AGXUnity.Rendering.DeformableTerrainParticleRenderer: " +
                          "Mesh filter shared mesh is null.",
                          filters[ 0 ].sharedMesh );
          return false;
        }

        var material = filters[ 0 ].GetComponent<MeshRenderer>()?.sharedMaterial;
        if ( material == null ) {
          Debug.LogError( "AGXUnity.Rendering.DeformableTerrainParticleRenderer: " +
                          "GranuleInstance doesn't contain a mesh renderer or a material.",
                          filters[ 0 ] );
          return false;
        }

        if ( !material.enableInstancing ) {
          Debug.LogError( "AGXUnity.Rendering.DeformableTerrainParticleRenderer: " +
                          "The granule render material must have instancing enabled for this render mode to work.",
                          material );
          return false;
        }

        m_meshInstance = filters[ 0 ].sharedMesh;
        m_meshInstanceMaterial = material;
        m_granuleMatrices = new Matrix4x4[ 1023 ];
        m_meshInstanceProperties = new MaterialPropertyBlock();
        m_meshInstanceScale = filters[ 0 ].transform.lossyScale;
      }

      Synchronize();

      return true;
    }

    private void Update()
    {
      var isValidDrawInstanceMode = RenderMode == GranuleRenderMode.DrawMeshInstanced &&
                                    m_numGranulars > 0 &&
                                    m_meshInstance != null &&
                                    m_meshInstanceMaterial != null;
      if ( !isValidDrawInstanceMode )
        return;

      if ( m_numGranulars < 1024 ) {
        Graphics.DrawMeshInstanced( m_meshInstance,
                                    0,
                                    m_meshInstanceMaterial,
                                    m_granuleMatrices,
                                    m_numGranulars,
                                    m_meshInstanceProperties,
                                    UnityEngine.Rendering.ShadowCastingMode.On,
                                    true );
      }
      // DrawMeshInstanced only supports up to 1023 meshes for each call,
      // we need to subdivide if we have more particles than that.
      else {
        for ( int i = 0; i < m_numGranulars; i += 1023 ) {
          int count = Mathf.Min( 1023, m_numGranulars - i );
          Graphics.DrawMeshInstanced( m_meshInstance,
                                      0,
                                      m_meshInstanceMaterial,
                                      new System.ArraySegment<Matrix4x4>( m_granuleMatrices,
                                                                          i,
                                                                          count ).Array,
                                      count,
                                      m_meshInstanceProperties,
                                      UnityEngine.Rendering.ShadowCastingMode.On,
                                      true );
        }
      }
    }

    private void Synchronize()
    {
      if ( DeformableTerrain == null || DeformableTerrain.Native == null )
        return;

      var soilSimulation = DeformableTerrain.Native.getSoilSimulationInterface();
      var granulars      = soilSimulation.getSoilParticles();
      m_numGranulars     = (int)granulars.size();

      var isValidDrawInstanceMode = RenderMode == GranuleRenderMode.DrawMeshInstanced &&
                                    m_meshInstance != null &&
                                    m_meshInstanceMaterial != null;
      var isValidDrawGameObjectMode = !isValidDrawInstanceMode &&
                                      RenderMode == GranuleRenderMode.GameObject &&
                                      GranuleInstance != null;
      if ( isValidDrawInstanceMode ) {
        // Use 1023 as arbitrary block size since that is the
        // amount of particles that can be drawn with DrawMeshInstanced.
        if (m_numGranulars > m_granuleMatrices.Length)
          System.Array.Resize(ref m_granuleMatrices, (m_numGranulars / 1023 + 1) * 1023);

        for (int i = 0; i < m_numGranulars; i++)
        {
          var granule = granulars.at((uint)i);

          // Assuming unit size of the instance, scale to diameter of the granule.
          m_granuleMatrices[ i ] = Matrix4x4.TRS( granule.position().ToHandedVector3(),
                                                  granule.rotation().ToHandedQuaternion(),
                                                  m_meshInstanceScale * 2.0f * (float)granule.getRadius() );

          // Return the proxy class to the pool to avoid garbage.
          granule.ReturnToPool();
        }
      }
      else if ( isValidDrawGameObjectMode ) {
        // More granular instances comparing to last time, create
        // more instances to match numGranulars.
        if (m_numGranulars > transform.childCount)
          Create(m_numGranulars - transform.childCount);
        // Less granular instances comparing to last time, destroy.
        else if (transform.childCount > m_numGranulars)
          Destroy(transform.childCount - m_numGranulars);

        Debug.Assert(transform.childCount == m_numGranulars);

        for ( int i = 0; i < m_numGranulars; ++i ) {
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

    private void DestroyAll()
    {
      Destroy( m_numGranulars );
      m_meshInstance = null;
      m_meshInstanceMaterial = null;
      m_granuleMatrices = null;
      m_meshInstanceProperties = null;
      m_meshInstanceScale = Vector3.one;
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

    private Matrix4x4[] m_granuleMatrices;
    private int m_numGranulars = 0;
    private MaterialPropertyBlock m_meshInstanceProperties = null;
    private Mesh m_meshInstance = null;
    private Vector3 m_meshInstanceScale = Vector3.one;
    private Material m_meshInstanceMaterial = null;
  }
}
