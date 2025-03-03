using AGXUnity.Utils;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

namespace AGXUnity.Rendering
{
  [AddComponentMenu( "AGXUnity/Rendering/Cable Renderer" )]
  [ExecuteInEditMode]
  [RequireComponent( typeof( Cable ) )]
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#cable-rendering" )]
  public class CableRenderer : ScriptComponent
  {
    public enum SegmentRenderMode
    {
      GameObject,
      DrawMeshInstanced
    }

    /// <summary>
    /// Choose how to render Cables. The performant way is instanced rendering - but this does not work with eg. Cable damage or custom shaders.
    /// </summary>
    [HideInInspector]
    [SerializeField]
    private SegmentRenderMode m_renderMode = SegmentRenderMode.DrawMeshInstanced;

    [Description( "Render particles using GameObjects, or with Graphics.DrawMeshInstanced." )]
    [HideInInspector]
    public SegmentRenderMode RenderMode
    {
      get { return m_renderMode; }
      set
      {
        if ( m_renderMode == SegmentRenderMode.GameObject && m_segmentSpawner != null )
          m_segmentSpawner.Destroy();

        m_renderMode = value;

        if ( !IsSynchronizingProperties )
          InitializeRenderer( true );
      }
    }
    bool GameObjectRendering => m_renderMode == SegmentRenderMode.GameObject;
    bool InstancedRendering => m_renderMode == SegmentRenderMode.DrawMeshInstanced;

    /// <summary>
    /// Shadow casting mode On for casting shadows, Off for no shadows.
    /// </summary>
    [HideInInspector]
    public ShadowCastingMode ShadowCastingMode = ShadowCastingMode.On;

    /// <summary>
    ///True for the cable to receive shadows, false to not receive shadows.
    /// </summary>

    [HideInInspector]
    public bool ReceiveShadows = true;

    [SerializeField]
    private SegmentSpawner m_segmentSpawner = null;

    [HideInInspector]
    public SegmentSpawner SegmentSpawner { get { return m_segmentSpawner; } }


    private Matrix4x4[] m_segmentSphereMatrices = new Matrix4x4[128];
    private Matrix4x4[] m_segmentCylinderMatrices = new Matrix4x4[128];
    private Vector4[] m_segmentColors = new Vector4[128];
    private MaterialPropertyBlock[] m_meshInstanceProperties = new MaterialPropertyBlock[128];
    private Mesh m_sphereMeshInstance = null;
    private Mesh m_cylinderMeshInstance = null;
    private List<Vector3> m_positions = new List<Vector3>();
    private int m_numCylinders = 0;

    [System.NonSerialized]
    private Cable m_cable = null;

    [HideInInspector]
    public Cable Cable
    {
      get
      {
        return m_cable ?? ( m_cable = GetComponent<Cable>() );
      }
    }

    [SerializeField]
    private Material m_material = null;
    [HideInInspector]
    public Material Material
    {
      get
      {
        if ( m_material == null ||
          ( m_material.name == DefaultMaterialName && !m_material.SupportsPipeline( RenderingUtils.DetectPipeline() ) ) )
          m_material = DefaultMaterial();
        return m_material;
      }
      set
      {
        m_material = value ?? DefaultMaterial();

        if ( GameObjectRendering && m_segmentSpawner != null )
          m_segmentSpawner.Material = m_material;
      }
    }

    private static string DefaultMaterialName = "Default Cable Material";

    public static Material DefaultMaterial()
    {
      Material mat;
      mat = new Material( Resources.Load<Shader>( "Shaders/Shader Graph/Cable And Wire" ) );

      mat.name = DefaultMaterialName;
      mat.hideFlags = HideFlags.NotEditable;
      mat.SetColor( "_Color", new Color( 0.17f, 0.17f, 0.17f ) );
      RenderingUtils.SetSmoothness( mat, 0.5f );
      mat.SetFloat( "_Metallic", 0.0f );

      return mat;
    }

    private CableDamage m_cableDamage = null;
    private CableDamage CableDamage { get { return m_cableDamage ?? ( m_cableDamage = GetComponent<CableDamage>() ); } }

    public void SetRenderDamages( bool value ) => m_renderDamages = value;

    private bool m_renderDamages = false;
    private Dictionary<int, (MeshRenderer, MeshRenderer)> m_segmentRenderers = new Dictionary<int, (MeshRenderer, MeshRenderer)>();

    public void InitializeRenderer( bool destroyLast = false )
    {
      if ( GetComponent<CableDamage>() == null ) {
        m_cableDamage = null;
        m_renderDamages = false;
      }

      if ( destroyLast ) {
        if ( m_segmentSpawner != null ) {
          m_segmentSpawner.Destroy();
          m_segmentSpawner = null;
        }
        m_segmentCylinderMatrices = null;
        m_segmentSphereMatrices = null;
        m_segmentColors = null;
      }

      if ( GameObjectRendering ) {
        m_segmentSpawner = new SegmentSpawner( Cable,
                                              @"Cable/CableSegment",
                                              @"Cable/CableSegmentBegin" );
        m_segmentSpawner.Material = Material;
        m_segmentSpawner.Initialize( gameObject );
      }
      else if ( InstancedRendering ) {
        if ( !CreateMeshes() )
          Debug.LogError( "AGXUnity.Rendering.CableRenderer: Problem initializing one or both meshes!", this );

        if ( !Material.enableInstancing )
          Debug.LogWarning( "AGXUnity.Rendering.CableRenderer: The cable render material does not have instancing enabled. This might cause degraded performance.", Material );

        InitMatrices();
        m_positions.Clear();
        m_positions.Capacity = 256;
      }
    }

    protected override void OnEnable()
    {
      InitializeRenderer( true );
      if ( Application.isPlaying )
        Simulation.Instance.StepCallbacks.PostStepForward += RenderGameObjects;
      RenderPipelineManager.beginCameraRendering -= SRPRender;
      RenderPipelineManager.beginCameraRendering += SRPRender;
      Camera.onPreCull -= Render;
      Camera.onPreCull += Render;
    }

    protected override void OnDisable()
    {
      if ( m_segmentSpawner != null ) {
        m_segmentSpawner.Destroy();
        m_segmentSpawner = null;
      }

      if ( Simulation.HasInstance && Application.isPlaying )
        Simulation.Instance.StepCallbacks.PostStepForward -= RenderGameObjects;
      RenderPipelineManager.beginCameraRendering -= SRPRender;
      Camera.onPreCull -= Render;
    }

    protected override bool Initialize()
    {
      InitializeRenderer();

      return true;
    }

    protected override void OnDestroy()
    {
      m_segmentCylinderMatrices = null;
      m_segmentSphereMatrices = null;
      m_segmentColors = null;

      if ( m_segmentSpawner != null ) {
        m_segmentSpawner.Destroy();
        m_segmentSpawner = null;
      }

      base.OnDestroy();
    }

    void SRPRender( ScriptableRenderContext context, Camera cam ) => Render( cam );

    void Render( Camera cam )
    {
      if ( !InstancedRendering ) return;
      if ( !RenderingUtils.CameraShouldRender( cam, gameObject, false ) )
        return;

      DrawInstanced( cam );
    }

    public void Update()
    {
      SynchronizeDataInstanced( false );
    }

    protected void LateUpdate()
    {
      // Late update from Editor. Exit if the application is running.
      if ( Application.isPlaying )
        return;

      RenderRoute();
    }

    private void RenderRoute()
    {
      if ( InstancedRendering ) {
        SynchronizeDataInstanced( true );
      }
      else if ( GameObjectRendering ) {
        if ( m_segmentSpawner == null )
          return;

        // Let OnDrawGizmos handle rendering when in prefab edit mode.
        // It's not possible to use RuntimeObjects while there.
        if ( PrefabUtils.IsPartOfEditingPrefab( gameObject ) )
          return;

        if ( !Cable.RoutePointCurveUpToDate )
          Cable.SynchronizeRoutePointCurve();

        m_segmentSpawner.Begin();
        try {
          var points = Cable.GetRoutePoints();
          for ( int i = 1; i < points.Length; ++i )
            m_segmentSpawner.CreateSegment( points[ i - 1 ], points[ i ], Cable.Radius );
        }
        catch ( System.Exception e ) {
          Debug.LogException( e, this );
        }
        m_segmentSpawner.End();
      }
    }

    private void RenderGameObjects()
    {
      if ( !GameObjectRendering )
        return;

      if ( m_segmentSpawner == null )
        return;

      var native = Cable.Native;
      if ( native == null ) {
        if ( m_segmentSpawner != null ) {
          m_segmentSpawner.Destroy();
          m_segmentSpawner = null;
        }
        return;
      }
      else if ( !m_segmentSpawner.IsValid )
        InitializeRenderer( true );

      var it = native.begin();
      var endIt = native.end();
      int i = 0;

      MaterialPropertyBlock block = new MaterialPropertyBlock();

      m_segmentSpawner.Begin();
      try {
        float radius = Cable.Radius;
        var prevEndPosition = it.EqualWith( endIt ) ?
                                Vector3.zero :
                                it.getBeginPosition().ToHandedVector3();
        while ( !it.EqualWith( endIt ) ) {
          var endPosition = it.getEndPosition().ToHandedVector3();

          var go = m_segmentSpawner.CreateSegment( prevEndPosition, endPosition, radius );

          int id = go.GetInstanceID();

          (MeshRenderer, MeshRenderer) meshRenderers;
          if ( m_segmentRenderers.TryGetValue( id, out meshRenderers ) && meshRenderers.Item1 != null ) {
            if ( m_renderDamages && CableDamage.DamageValueCount == (int)native.getNumSegments() ) {
              float t = CableDamage.DamageValue(i) / CableDamage.MaxDamage;
              block.SetColor( "_Color", Color.Lerp( CableDamage.Properties.MinColor, CableDamage.Properties.MaxColor, t ) );
              block.SetColor( "_InstancedColor", Color.Lerp( CableDamage.Properties.MinColor, CableDamage.Properties.MaxColor, t ) );
              meshRenderers.Item1.SetPropertyBlock( block );
              meshRenderers.Item2.SetPropertyBlock( block );
            }
            else {
              block.SetColor( "_Color", Material.color );
              block.SetColor( "_InstancedColor", Material.color );
              meshRenderers.Item1.SetPropertyBlock( block );
              meshRenderers.Item2.SetPropertyBlock( block );
            }
          }
          else {
            var renderers = go.GetComponentsInChildren<MeshRenderer>();
            m_segmentRenderers.Add( id, (renderers[ 0 ], renderers[ 1 ]) );
          }

          i++;
          prevEndPosition = endPosition;
          it.inc();
        }
      }
      catch ( System.Exception e ) {
        Debug.LogException( e, this );
      }
      m_segmentSpawner.End();

      it.ReturnToPool();
      endIt.ReturnToPool();
    }

    void DrawInstanced( Camera camera = null )
    {
      if ( Cable == null )
        return;

      if ( !CreateMeshes() )
        return;
      Profiler.BeginSample( "CableRenderer.DrawInstanced" );

      if ( m_positions.Count == 0 )
        return;

      var rp = new RenderParams(m_material);
      rp.shadowCastingMode = ShadowCastingMode;
      rp.receiveShadows = ReceiveShadows;

      if ( Material.enableInstancing ) {
        rp.matProps = new MaterialPropertyBlock();
        rp.matProps.SetVectorArray( "_Color", m_segmentColors );
        rp.matProps.SetVectorArray( "_InstancedColor", m_segmentColors );
        Graphics.RenderMeshInstanced( rp, m_sphereMeshInstance, 0, m_segmentSphereMatrices, m_positions.Count );
        Graphics.RenderMeshInstanced( rp, m_cylinderMeshInstance, 0, m_segmentCylinderMatrices, m_numCylinders );
      }
      else {
        for ( int i = 0; i < m_positions.Count; i++ ) {
          rp.matProps = m_meshInstanceProperties[ i ];
          Graphics.RenderMesh( rp, m_sphereMeshInstance, 0, m_segmentSphereMatrices[ i ] );
          if ( i < m_positions.Count - 1 )
            Graphics.RenderMesh( rp, m_cylinderMeshInstance, 0, m_segmentCylinderMatrices[ i ] );
        }
      }
      Profiler.EndSample();
    }

    private void SynchronizeDataInstanced( bool isRoute )
    {
      if ( Cable == null || !InstancedRendering )
        return;

      if ( !isRoute && Cable.Native == null )
        return;

      m_positions.Clear();

      if ( isRoute ) {
        foreach ( var node in Cable.Route )
          m_positions.Add( node.Position );
      }
      else {
        var it = Cable.Native.begin();
        var beginIt = Cable.Native.begin();
        var endIt = Cable.Native.end();

        while ( !it.EqualWith( endIt ) ) {
          if ( it.EqualWith( beginIt ) )
            m_positions.Add( it.getBeginPosition().ToHandedVector3() );
          m_positions.Add( it.getEndPosition().ToHandedVector3() );
          it.inc();
        }

        it.ReturnToPool();
        endIt.ReturnToPool();
        beginIt.ReturnToPool();
      }

      while ( m_positions.Count > m_segmentSphereMatrices.Length )
        m_segmentSphereMatrices = new Matrix4x4[ Mathf.NextPowerOfTwo( m_positions.Count ) ];

      m_numCylinders = 0;

      if ( m_positions.Count < 2 )
        return;

      if ( m_positions.Count > m_segmentCylinderMatrices.Length )
        m_segmentCylinderMatrices = new Matrix4x4[ Mathf.NextPowerOfTwo( m_positions.Count ) ];

      if ( m_positions.Count > m_segmentColors.Length )
        m_segmentColors = new Vector4[ Mathf.NextPowerOfTwo( m_positions.Count ) ];

      if ( m_positions.Count > m_meshInstanceProperties.Length )
        m_meshInstanceProperties = new MaterialPropertyBlock[ Mathf.NextPowerOfTwo( m_positions.Count ) ];

      float radius = Cable.Radius;
      var sphereScale = 2f * radius * Vector3.one;
      // rotation will be set by cylinder calculation and reused by sphere to align edges, first half sphere need its own calculation
      var rotation = (m_positions.Count > 1) ? Quaternion.FromToRotation( Vector3.down, m_positions[ 1 ] - m_positions[ 0 ] ) : Quaternion.identity;
      for ( int i = 0; i < m_positions.Count; ++i ) {
        if ( m_meshInstanceProperties[ i ] == null )
          m_meshInstanceProperties[ i ] = new MaterialPropertyBlock();
        if ( i > 0 ) {
          SegmentUtils.CalculateCylinderTransform( m_positions[ i - 1 ],
                                                  m_positions[ i ],
                                                  radius,
                                                  out var position,
                                                  out rotation,
                                                  out var scale );


          m_segmentCylinderMatrices[ m_numCylinders ] = Matrix4x4.TRS( position, rotation, scale );

          if ( m_renderDamages ) {
            float t = CableDamage.DamageValue(i - 1) / CableDamage.MaxDamage;
            var color = Color.Lerp(CableDamage.Properties.MinColor, CableDamage.Properties.MaxColor, t);
            m_segmentColors[ m_numCylinders ] = color;
          }
          else
            m_segmentColors[ m_numCylinders ] = Material.color;

          m_meshInstanceProperties[ m_numCylinders ].SetColor( "_Color", m_segmentColors[ m_numCylinders ] );
          m_numCylinders++;
        }

        // Last half sphere needs an additional color, copy last color
        if ( i + 1 == m_positions.Count ) {
          var lastCol = m_segmentColors[ m_numCylinders-1  ];
          m_segmentColors[ m_positions.Count ] = lastCol;
          m_meshInstanceProperties[ m_numCylinders ].SetColor( "_Color", lastCol );
        }

        m_segmentSphereMatrices[ i ] = Matrix4x4.TRS( m_positions[ i ], rotation, sphereScale );
      }
    }

    private bool CreateMeshes()
    {
      m_cylinderMeshInstance = null;
      m_sphereMeshInstance = null;
      if ( m_sphereMeshInstance == null )
        m_sphereMeshInstance = Resources.Load<Mesh>( @"Debug/Models/HalfSphere" );
      if ( m_cylinderMeshInstance == null )
        m_cylinderMeshInstance = Resources.Load<Mesh>( @"Debug/Models/CylinderCap" );

      return m_sphereMeshInstance != null && m_cylinderMeshInstance != null;
    }

    private void InitMatrices()
    {
      if ( m_segmentSphereMatrices == null )
        m_segmentSphereMatrices = new Matrix4x4[ 128 ];
      if ( m_segmentCylinderMatrices == null )
        m_segmentCylinderMatrices = new Matrix4x4[ 128 ];
      if ( m_segmentColors == null )
        m_segmentColors = new Vector4[ 128 ];
      if ( m_meshInstanceProperties == null )
        m_meshInstanceProperties = new MaterialPropertyBlock[ 128 ];
    }
  }
}
