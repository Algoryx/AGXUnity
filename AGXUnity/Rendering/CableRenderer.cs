using AGXUnity.Utils;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;
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


    private List<Matrix4x4[]> m_segmentSphereMatrices = new List<Matrix4x4[]>();
    private List<Matrix4x4[]> m_segmentCylinderMatrices = new List<Matrix4x4[]>();
    private List<Vector4[]> m_segmentColors = new List<Vector4[]>();
    private MaterialPropertyBlock m_meshInstanceProperties = null;
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
        return m_material == null ?
                     m_material = DefaultMaterial() :
                     m_material;
      }
      set
      {
        m_material = value ?? DefaultMaterial();

        if ( GameObjectRendering && m_segmentSpawner != null )
          m_segmentSpawner.Material = m_material;
      }
    }

    // TODO previous was elaborate way of spawning segment and looking at that renderers material. Check that this variant is ok
    private static Material DefaultMaterial()
    {
      Debug.Log( "Setting Default material from resources" );
      switch ( RenderingUtils.DetectPipeline() ) {
        case RenderingUtils.PipelineType.HDRP:
          return Resources.Load<Material>( "HDRP/Materials/CableMaterial" );
        default:
          return Resources.Load<Material>( "Materials/CableMaterial_01" );
      }
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
        if ( !CreateMeshes() ) {
          Debug.LogError( "AGXUnity.Rendering.CableRenderer: Problem initializing one or both meshes!", this );
        }

        if ( !Material.enableInstancing ) {
          Debug.LogError( "AGXUnity.Rendering.CableRenderer: The cable render material must have instancing enabled for this render mode to work.",
                          Material );
        }

        InitMatrices();
        m_positions.Clear();
        m_positions.Capacity = 256;
      }
    }

    public override void EditorUpdate()
    {
      var pipeline = RenderingUtils.DetectPipeline();
      if ( !Material.SupportsPipeline( pipeline ) ) {
        Debug.LogWarning( $"Material {Material.name} does not support the current rendering pipeline '{pipeline}'. Reverting to the default material." );
        Material = DefaultMaterial();
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
      if ( !RenderingUtils.CameraShouldRender( cam, gameObject, true ) )
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

      var pipeline = RenderingUtils.DetectPipeline();
      var colVar = "";
      if ( pipeline == RenderingUtils.PipelineType.BuiltIn )
        colVar = "_InstancedColor";
      else if ( pipeline == RenderingUtils.PipelineType.HDRP )
        colVar = "_BaseColor";

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
              block.SetColor( colVar, Color.Lerp( CableDamage.Properties.MinColor, CableDamage.Properties.MaxColor, t ) );
              meshRenderers.Item1.SetPropertyBlock( block );
              meshRenderers.Item2.SetPropertyBlock( block );
            }
            else {
              block.SetColor( colVar, Material.color );
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

      if ( m_meshInstanceProperties == null )
        m_meshInstanceProperties = new MaterialPropertyBlock();

      if ( !CreateMeshes() )
        return;

      if ( m_renderDamages && RenderingUtils.DetectPipeline() != RenderingUtils.PipelineType.BuiltIn ) {
        Debug.LogWarning( "Cable damage rendering using 'Draw Mesh Instanced' currently only supports the Build-in Render pipeline. Consider swapping to Game Object rendering or disabling cable damage", this );
      }

      var forceSynchronize = m_positions.Count > 0 &&
                             ( m_segmentSphereMatrices.Count == 0 ||
                               m_segmentCylinderMatrices.Count == 0 );
      if ( forceSynchronize )
        SynchronizeDataInstanced( Cable.State != States.INITIALIZED );

      // Spheres
      for ( int i = 0; i < m_positions.Count; i += 1023 ) {
        int count = Mathf.Min( 1023, m_positions.Count - i );

        if ( m_segmentColors.Count > 0 )
          m_meshInstanceProperties.SetVectorArray( "_InstancedColor", m_segmentColors[ i / 1023 ] );

        Graphics.DrawMeshInstanced( m_sphereMeshInstance,
                                    0,
                                    Material,
                                    m_segmentSphereMatrices[ i / 1023 ],
                                    count,
                                    m_meshInstanceProperties,
                                    ShadowCastingMode,
                                    ReceiveShadows,
                                    0,
                                    camera );
      }

      // Cylinders
      for ( int i = 0; i < m_numCylinders; i += 1023 ) {

        if ( m_segmentColors.Count > 0 )
          m_meshInstanceProperties.SetVectorArray( "_InstancedColor", m_segmentColors[ i / 1023 ] );

        int count = Mathf.Min( 1023, m_numCylinders - i );
        Graphics.DrawMeshInstanced( m_cylinderMeshInstance,
                                    0,
                                    Material,
                                    m_segmentCylinderMatrices[ i / 1023 ],
                                    count,
                                    m_meshInstanceProperties,
                                    ShadowCastingMode,
                                    ReceiveShadows,
                                    0,
                                    camera );
      }
    }

    private static void CalculateCylinderTransform( Vector3 start,
                                                    Vector3 end,
                                                    float radius,
                                                    out Vector3 position,
                                                    out Quaternion rotation,
                                                    out Vector3 scale )
    {
      var dir = end - start;
      var length = dir.magnitude;
      position = 0.5f * ( start + end );
      rotation = Quaternion.FromToRotation( Vector3.up, dir );
      scale = new Vector3( 2.0f * radius, 1f * length, 2.0f * radius );
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

      while ( m_positions.Count / 1023 + 1 > m_segmentSphereMatrices.Count )
        m_segmentSphereMatrices.Add( new Matrix4x4[ 1023 ] );

      m_numCylinders = 0;

      float radius = Cable.Radius;
      var sphereScale = 2f * radius * Vector3.one;
      // rotation will be set by cylinder calculation and reused by sphere to align edges, first half sphere need its own calculation
      var rotation = (m_positions.Count > 1) ? Quaternion.FromToRotation( Vector3.down, m_positions[ 1 ] - m_positions[ 0 ] ) : Quaternion.identity;
      for ( int i = 0; i < m_positions.Count; ++i ) {
        if ( i > 0 ) {
          if ( m_numCylinders / 1023 + 1 > m_segmentCylinderMatrices.Count )
            m_segmentCylinderMatrices.Add( new Matrix4x4[ 1023 ] );

          CalculateCylinderTransform( m_positions[ i - 1 ],
                                      m_positions[ i ],
                                      radius,
                                      out var position,
                                      out rotation,
                                      out var scale );
          m_segmentCylinderMatrices[ m_numCylinders / 1023 ][ m_numCylinders % 1023 ] =  Matrix4x4.TRS( position, rotation, scale );

          if ( m_numCylinders / 1023 + 1 > m_segmentColors.Count )
            m_segmentColors.Add( new Vector4[ 1023 ] );

          if ( m_renderDamages ) {
            float t = CableDamage.DamageValue(i - 1) / CableDamage.MaxDamage;
            var color = Color.Lerp(CableDamage.Properties.MinColor, CableDamage.Properties.MaxColor, t);
            m_segmentColors[ m_numCylinders / 1023 ][ m_numCylinders % 1023 ] = color;
          }
          else {
            m_segmentColors[ m_numCylinders / 1023 ][ m_numCylinders % 1023 ] = Material.color;
          }

          m_numCylinders++;
        }

        // Last half sphere needs an additional color, copy last color
        if ( i + 1 == m_positions.Count ) {
          var lastCol = m_segmentColors[ ( m_numCylinders-1 ) / 1023 ][ ( m_numCylinders-1 ) % 1023 ];
          if ( m_numCylinders / 1023 + 1 > m_segmentColors.Count )
            m_segmentColors.Add( new Vector4[ 1023 ] );
          m_segmentColors[ m_numCylinders / 1023 ][ m_numCylinders % 1023 ] = lastCol;
        }

        m_segmentSphereMatrices[ i / 1023 ][ i % 1023 ] = Matrix4x4.TRS( m_positions[ i ],
                                                                         rotation,
                                                                         sphereScale );
      }
    }

    private bool CreateMeshes()
    {
      if ( m_sphereMeshInstance == null )
        m_sphereMeshInstance = CreateMesh( @"Cable/HalfSphereRenderer" );
      if ( m_cylinderMeshInstance == null )
        m_cylinderMeshInstance = CreateMesh( @"Cable/CylinderCapRenderer" );

      return m_sphereMeshInstance != null && m_cylinderMeshInstance != null;
    }

    private Mesh CreateMesh( string resource )
    {
      GameObject tmp = Resources.Load<GameObject>( resource );
      MeshFilter[] filters = tmp.GetComponentsInChildren<MeshFilter>();
      CombineInstance[] combine = new CombineInstance[ filters.Length ];

      for ( int i = 0; i < filters.Length; ++i ) {
        combine[ i ].mesh = filters[ i ].sharedMesh;
        combine[ i ].transform = filters[ i ].transform.localToWorldMatrix;
      }

      var mesh = new Mesh();
      mesh.CombineMeshes( combine );

      return mesh;
    }

    private void InitMatrices()
    {
      if ( m_segmentSphereMatrices == null )
        m_segmentSphereMatrices = new List<Matrix4x4[]> { new Matrix4x4[ 1023 ] };
      if ( m_segmentCylinderMatrices == null )
        m_segmentCylinderMatrices = new List<Matrix4x4[]> { new Matrix4x4[ 1023 ] };
      if ( m_segmentColors == null )
        m_segmentColors = new List<Vector4[]> { new Vector4[ 1023 ] };
      if ( m_meshInstanceProperties == null )
        m_meshInstanceProperties = new MaterialPropertyBlock();
    }

    private void DrawGizmos( bool isSelected )
    {
      if ( Application.isPlaying || InstancedRendering )
        return;

      if ( Cable == null || Cable.Route == null || Cable.Route.NumNodes < 2 )
        return;

      if ( !PrefabUtils.IsPartOfEditingPrefab( gameObject ) )
        return;

      var defaultColor  = Color.Lerp( Color.black, Color.white, 0.15f );
      var selectedColor = Color.Lerp( defaultColor, Color.green, 0.15f );
      m_segmentSpawner?.DrawGizmos( Cable.GetRoutePoints(),
                                    Cable.Radius,
                                    isSelected ? selectedColor : defaultColor );
    }

    private void OnDrawGizmos()
    {
      DrawGizmos( false );
    }

    private void OnDrawGizmosSelected()
    {
      DrawGizmos( true );
    }
  }
}
