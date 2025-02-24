using AGXUnity.Utils;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

namespace AGXUnity.Rendering
{
  [AddComponentMenu( "AGXUnity/Rendering/Wire Renderer" )]
  [ExecuteInEditMode]
  [RequireComponent( typeof( Wire ) )]
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#rendering-the-wire" )]
  public class WireRenderer : ScriptComponent
  {
    /// <summary>
    /// Shadow casting mode On for casting shadows, Off for no shadows.
    /// </summary>
    public ShadowCastingMode ShadowCastingMode = ShadowCastingMode.On;

    /// <summary>
    ///True for the wire to receive shadows, false to not receive shadows.
    /// </summary>
    public bool ReceiveShadows = true;

    [HideInInspector]
    public Wire Wire
    {
      get
      {
        return m_wire ?? ( m_wire = GetComponent<Wire>() );
      }
    }

    [SerializeField]
    private Material m_material = null;

    [AllowRecursiveEditing]
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
      }
    }

    public void OnPostStepForward( Wire wire )
    {
      SynchronizeData( false );
    }

    public bool InitializeRenderer()
    {
      if ( !CreateMeshes() ) {
        Debug.LogError( "AGXUnity.Rendering.WireRenderer: Problem initializing one or both meshes!", this );
        return false;
      }

      if ( !Material.enableInstancing )
        Debug.LogWarning( "AGXUnity.Rendering.WireRenderer: The wire render material does not have instancing enabled. This might cause degraded performance.",
                          Material );

      InitMatrices();
      m_positions.Clear();
      m_positions.Capacity = 256;

      return true;
    }

    protected override void OnEnable()
    {
      RenderPipelineManager.beginCameraRendering -= SRPRender;
      RenderPipelineManager.beginCameraRendering += SRPRender;
      Camera.onPreCull -= Render;
      Camera.onPreCull += Render;
    }

    protected override void OnDisable()
    {
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

      base.OnDestroy();
    }

    private static string DefaultMaterialName = "Default Wire Material";

    public static Material DefaultMaterial()
    {
      var material = RenderingUtils.CreateDefaultMaterial();
      material.hideFlags = HideFlags.NotEditable;

      material.name = DefaultMaterialName;

      RenderingUtils.SetColor( material, new Color( 0.55f, 0.55f, 0.55f ) );
      material.SetFloat( "_Metallic", 0.35f );
      RenderingUtils.SetSmoothness( material, 0.5f );

      return material;
    }

    private void SynchronizeData( bool isRoute )
    {
      if ( Wire == null )
        return;

      if ( !isRoute && Wire.Native == null )
        return;

      m_positions.Clear();

      if ( isRoute ) {
        foreach ( var node in Wire.Route )
          m_positions.Add( node.Position );
      }
      else {
        var it = Wire.Native.getRenderBeginIterator();
        var endIt = Wire.Native.getRenderEndIterator();
        while ( !it.EqualWith( endIt ) ) {
          m_positions.Add( it.getWorldPosition().ToHandedVector3() );
          it.inc();
        }

        it.ReturnToPool();
        endIt.ReturnToPool();
      }

      while ( m_positions.Count > m_segmentSphereMatrices.Length )
        m_segmentSphereMatrices = new Matrix4x4[ Mathf.NextPowerOfTwo( m_positions.Count ) ];

      m_numCylinders = 0;

      if ( m_positions.Count < 2 )
        return;

      if ( m_positions.Count > m_segmentCylinderMatrices.Length )
        m_segmentCylinderMatrices = new Matrix4x4[ Mathf.NextPowerOfTwo( m_positions.Count ) ];

      float radius = Wire.Radius;
      var sphereScale = 2.0f * radius * Vector3.one;
      for ( int i = 0; i < m_positions.Count; ++i ) {
        if ( i > 0 ) {
          m_segmentCylinderMatrices[ m_numCylinders ] = SegmentUtils.CalculateCylinderTransform( m_positions[ i - 1 ], m_positions[ i ], radius );
          m_numCylinders++;
        }

        m_segmentSphereMatrices[ i ] = Matrix4x4.TRS( m_positions[ i ], Quaternion.identity, sphereScale );
      }
    }

    private void SRPRender( ScriptableRenderContext _, Camera cam ) => Render( cam );

    private void Render( Camera cam )
    {
      if ( !RenderingUtils.CameraShouldRender( cam, gameObject, false ) )
        return;

      if ( !Application.isPlaying )
        SynchronizeData( true );

      if ( Wire == null )
        return;

      if ( !CreateMeshes() )
        return;

      Profiler.BeginSample( "CableRenderer.DrawInstanced" );

      if ( m_positions.Count == 0 )
        return;

      var rp = new RenderParams(m_material);

      if ( Material.enableInstancing ) {
        Graphics.RenderMeshInstanced( rp, m_sphereMeshInstance, 0, m_segmentSphereMatrices, m_positions.Count );
        Graphics.RenderMeshInstanced( rp, m_cylinderMeshInstance, 0, m_segmentCylinderMatrices, m_numCylinders );
      }
      else {
        for ( int i = 0; i < m_positions.Count; i++ ) {
          Graphics.RenderMesh( rp, m_sphereMeshInstance, 0, m_segmentSphereMatrices[ i ] );
          if ( i < m_positions.Count - 1 )
            Graphics.RenderMesh( rp, m_cylinderMeshInstance, 0, m_segmentCylinderMatrices[ i ] );
        }
      }
      Profiler.EndSample();
    }

    private bool CreateMeshes()
    {
      if ( m_sphereMeshInstance == null )
        m_sphereMeshInstance = Resources.Load<Mesh>( @"Debug/Models/LowPolySphere" );
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
    }

    private Wire m_wire = null;
    private Matrix4x4[] m_segmentSphereMatrices = new Matrix4x4[128];
    private Matrix4x4[] m_segmentCylinderMatrices = new Matrix4x4[128];
    private Mesh m_sphereMeshInstance = null;
    private Mesh m_cylinderMeshInstance = null;
    private List<Vector3> m_positions = new List<Vector3>();
    private int m_numCylinders = 0;
  }
}
