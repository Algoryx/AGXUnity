using AGXUnity.Model;
using AGXUnity.Utils;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

using static agx.agxSWIG.UnityHelpers;

namespace AGXUnity.Rendering
{
  [AddComponentMenu( "AGXUnity/Deformable Terrain Particle Renderer" )]
  [RequireComponent( typeof( DeformableTerrainBase ) )]
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#rendering-the-particles" )]
  public class DeformableTerrainParticleRenderer : ScriptComponent
  {
    public enum GranuleRenderMode
    {
      GameObject,
      DrawMeshInstanced
    }

    public enum SynchronizeMode
    {
      PostStepForward,
      Update
    }

    [HideInInspector]
    public DeformableTerrainBase ParticleProvider { get; private set; } = null;

    // Create a union type for matrices to allow for more efficient conversion between AffineMatrix4x4f and Matrix4x4
    [StructLayout( LayoutKind.Explicit )]
    class MatrixUnion
    {
      [FieldOffset(0)]
      public Matrix4x4[] unityMats;

      [FieldOffset(0)]
      public agx.AffineMatrix4x4f[] agxMats;
    }

    [SerializeField]
    private GranuleRenderMode m_renderMode = GranuleRenderMode.DrawMeshInstanced;

    [Description( "Render particles using cloned GameObjects or with Graphics.DrawMeshInstanced." )]
    public GranuleRenderMode RenderMode
    {
      get { return m_renderMode; }
      set
      {
        m_renderMode = value;

        if ( !IsSynchronizingProperties && ParticleProvider != null )
          InitializeRenderMode();
      }
    }

    [SerializeField]
    private SynchronizeMode m_syncMode = SynchronizeMode.PostStepForward;

    [Description( "Synchronize granular transforms for rendering when the transforms has been " +
                 "changed (PostStepForward) or in Update whenever the transforms has been changed." )]
    public SynchronizeMode SyncMode
    {
      get { return m_syncMode; }
      set
      {
        m_syncMode = value;
      }
    }
    private bool m_needsSynchronize = true;

    [SerializeField]
    private GameObject m_granuleInstance = null;

    public GameObject GranuleInstance
    {
      get { return m_granuleInstance; }
      set
      {
        var isChangedDuringRuntime = ParticleProvider != null &&
                                     value != m_granuleInstance;
        if ( isChangedDuringRuntime )
          DestroyAll();

        m_granuleInstance = value;

        if ( isChangedDuringRuntime )
          InitializeRenderMode();
      }
    }

    [field: SerializeField]
    [Tooltip("When enabled, the particles are filtered based on their agx.Material and only those where the material matches the terrain particle material are rendered")]
    public bool FilterParticles { get; set; } = false;

    protected override bool Initialize()
    {
      ParticleProvider = GetComponent<DeformableTerrainBase>();
      if ( ParticleProvider == null ) {
        Debug.LogError( "DeformableTerrainParticleRenderer parent game object '" + gameObject.name + "' has no particle provider!" );
        return false;
      }

      if ( !InitializeRenderMode() )
        return false;

      return true;
    }

    protected override void OnEnable()
    {
      // We hook into the rendering process to render even when the application is paused.
      // For the Built-in render pipeline this is done by adding a callback to the Camera.OnPreCull event which is called for each camera in the scene.
      // For SRPs such as URP and HDRP the beginCameraRendering event serves a similar purpose.
      RenderPipelineManager.beginCameraRendering -= SRPRender;
      RenderPipelineManager.beginCameraRendering += SRPRender;
      Camera.onPreCull -= Render;
      Camera.onPreCull += Render;
      Simulation.Instance.StepCallbacks.PostStepForward += PostUpdate;

      if ( State == States.INITIALIZED )
        InitializeRenderMode();
    }

    protected override void OnDisable()
    {
      Camera.onPreCull -= Render;
      RenderPipelineManager.beginCameraRendering -= SRPRender;
      // We may not "change GameObject hierarchy" when the actual
      // game object is being destroyed, e.g., when hitting stop.
      if ( gameObject.activeSelf )
        DestroyAll();

      if ( Simulation.HasInstance )
        Simulation.Instance.StepCallbacks.PostStepForward -= PostUpdate;
    }

    protected override void OnDestroy()
    {
      ParticleProvider = null;

      base.OnDestroy();
    }

    private void PostUpdate()
    {
      if ( m_syncMode == SynchronizeMode.PostStepForward )
        Synchronize();
      else
        m_needsSynchronize = true;
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

        var renderers = GranuleInstance.GetComponentsInChildren<MeshRenderer>();
        if ( renderers.Length != 1 ) {
          Debug.LogError( "AGXUnity.Rendering.DeformableTerrainParticleRenderer: " +
                          $"Invalid number of mesh renderers ({renderers.Length}) in GranuleInstance - expecting 1.",
                          GranuleInstance );
          return false;
        }

        m_meshInstance = filters[ 0 ].sharedMesh;
        m_shadowCastingMode = renderers[ 0 ].shadowCastingMode;
        m_receiveShadows = renderers[ 0 ].receiveShadows;
        m_meshInstanceMaterial = material;
        m_granuleMatrices = new List<MatrixUnion> { new MatrixUnion() };
        m_granuleMatrices[ 0 ].unityMats = new Matrix4x4[ 1023 ];
        m_meshInstanceProperties = new MaterialPropertyBlock();
      }

      Synchronize();

      return true;
    }

    private void Update()
    {
      if ( m_syncMode == SynchronizeMode.Update && m_needsSynchronize ) {
        Synchronize();
        m_needsSynchronize = false;
      }
    }

    private void SRPRender( ScriptableRenderContext _, Camera cam ) => Render( cam );

    private void Render( Camera cam )
    {
      if ( !RenderingUtils.CameraShouldRender( cam ) )
        return;

      var isValidDrawInstanceMode = RenderMode == GranuleRenderMode.DrawMeshInstanced &&
                                    m_numRendered > 0 &&
                                    m_meshInstance != null &&
                                    m_meshInstanceMaterial != null;
      if ( !isValidDrawInstanceMode )
        return;

      if ( m_numRendered < 1024 ) {
        Graphics.DrawMeshInstanced( m_meshInstance,
                                    0,
                                    m_meshInstanceMaterial,
                                    m_granuleMatrices[ 0 ].unityMats,
                                    m_numRendered,
                                    m_meshInstanceProperties,
                                    m_shadowCastingMode,
                                    m_receiveShadows,
                                    0,
                                    cam );
      }
      // DrawMeshInstanced only supports up to 1023 meshes for each call,
      // we need to subdivide if we have more particles than that.
      else {
        for ( int i = 0; i < m_numRendered; i += 1023 ) {
          int count = Mathf.Min( 1023, m_numRendered - i );
          Graphics.DrawMeshInstanced( m_meshInstance,
                                      0,
                                      m_meshInstanceMaterial,
                                      m_granuleMatrices[ i / 1023 ].unityMats,
                                      count,
                                      m_meshInstanceProperties,
                                      m_shadowCastingMode,
                                      m_receiveShadows,
                                      0,
                                      cam );
        }
      }
    }

    private void Synchronize()
    {
      var granulars = ParticleProvider?.GetParticles();
      if ( granulars == null ) return;

      int numGranulars = (int)granulars.size();

      var isValidDrawInstanceMode = RenderMode == GranuleRenderMode.DrawMeshInstanced &&
                                    m_meshInstance != null &&
                                    m_meshInstanceMaterial != null;
      var isValidDrawGameObjectMode = !isValidDrawInstanceMode &&
                                      RenderMode == GranuleRenderMode.GameObject &&
                                      GranuleInstance != null;
      if ( isValidDrawInstanceMode ) {

        if ( FilterParticles ) {
          m_numRendered = 0;
          int start = 0;
          int i = 0;
          var uuid = ParticleProvider.GetParticleMaterialUuid();
          while ( start != numGranulars ) {
            if ( i + 1 > m_granuleMatrices.Count ) {
              m_granuleMatrices.Add( new MatrixUnion() );
              m_granuleMatrices[ m_granuleMatrices.Count - 1 ].unityMats = new Matrix4x4[ 1023 ];
            }
            m_numRendered += PopulateMatricesSelectMaterial( granulars, m_granuleMatrices[ i++ ].agxMats, ref start, uuid );
          }
          uuid.ReturnToPool();
        } else {
          m_numRendered = numGranulars;

          // Use 1023 as arbitrary block size since that is the
          // amount of particles that can be drawn with DrawMeshInstanced.
          while ( m_numRendered / 1023 + 1 > m_granuleMatrices.Count ) {
            m_granuleMatrices.Add( new MatrixUnion() );
            m_granuleMatrices[ m_granuleMatrices.Count - 1 ].unityMats = new Matrix4x4[ 1023 ];
          }

          for ( int arrayIndex = 0; arrayIndex < ( m_numRendered / 1023 + 1 ); ++arrayIndex )
            PopulateMatrices( granulars, m_granuleMatrices[ arrayIndex ].agxMats, arrayIndex * 1023 );
        }
      }
      else if ( isValidDrawGameObjectMode ) {
        // More granular instances comparing to last time, create
        // more instances to match numGranulars.
        if ( m_numRendered > transform.childCount )
          Create( m_numRendered - transform.childCount );
        // Less granular instances comparing to last time, destroy.
        else if ( transform.childCount > m_numRendered )
          Destroy( transform.childCount - m_numRendered );

        Debug.Assert( transform.childCount == m_numRendered );

        for ( int i = 0; i < m_numRendered; ++i ) {
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
      Destroy( m_numRendered );
      m_meshInstance = null;
      m_meshInstanceMaterial = null;
      m_granuleMatrices = null;
      m_meshInstanceProperties = null;
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

    private List<MatrixUnion> m_granuleMatrices;
    private int m_numRendered = 0;
    private MaterialPropertyBlock m_meshInstanceProperties = null;
    private Mesh m_meshInstance = null;
    private ShadowCastingMode m_shadowCastingMode = ShadowCastingMode.On;
    private bool m_receiveShadows = true;
    private Material m_meshInstanceMaterial = null;
  }
}
