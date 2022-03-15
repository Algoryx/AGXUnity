using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using AGXUnity.Utils;

#if UNITY_EDITOR
using UnityEditor;
#endif

using System.ComponentModel;
using UnityEngine.Rendering;

namespace AGXUnity.Rendering
{
  [AddComponentMenu( "AGXUnity/Rendering/Wire Renderer" )]
  [ExecuteInEditMode]
  [RequireComponent( typeof( Wire ) )]
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
      get { return m_material == null ?
                     m_material = DefaultMaterial() :
                     m_material; }
      set
      {
        m_material = value ?? DefaultMaterial();
      }
    }

    public void Update()
    {
      Draw();
    }

    public void OnPostStepForward( Wire wire )
    {
      SynchronizeData( false );
    }

    public bool InitializeRenderer()
    {
      if ( !CreateMeshes() ) {
        Debug.LogError( "AGXUnity.Rendering.WireRenderer: Problem initializing one or both meshes!", this);
        return false;
      }

      if ( !Material.enableInstancing ) {
        Debug.LogError( "AGXUnity.Rendering.WireRenderer: The wire render material must have instancing enabled for this render mode to work.",
                        Material );
        return false;
      }

      InitMatrices();
      m_positions.Clear();
      m_positions.Capacity = 256;

      return true;
    }

    protected override void OnEnable()
    {
#if UNITY_EDITOR
      // Used to draw in a prefab stage or when the editor is paused.
      // It's not possible in OnEnable to check if our gameObject is
      // part of a prefab stage.
#if UNITY_2019_1_OR_NEWER
      SceneView.duringSceneGui += OnSceneView;
#else
      SceneView.onSceneGUIDelegate += OnSceneView;
#endif
#endif
    }

    protected override void OnDisable()
    {
#if UNITY_EDITOR
#if UNITY_2019_1_OR_NEWER
      SceneView.duringSceneGui -= OnSceneView;
#else
      SceneView.onSceneGUIDelegate -= OnSceneView;
#endif
#endif
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

    /// <summary>
    /// Catching LateUpdate calls since ExecuteInEditMode attribute.
    /// </summary>
    protected void LateUpdate()
    {
      // During play we're receiving callbacks from the wire
      // to OnPostStepForward.
      if ( Application.isPlaying )
        return;

      RenderRoute();
    }

#if UNITY_EDITOR
    // Editing wire prefab in a prefab stage and when the editor is paused
    // requires Scene View GUI update callback.
    private void OnSceneView( SceneView sceneView )
    {
      var inPrefabStage = PrefabUtils.IsPartOfEditingPrefab( gameObject );
      var performDraw = EditorApplication.isPaused || inPrefabStage;
      if ( !performDraw )
        return;

      if ( inPrefabStage && m_positions.Count != Wire.Route.NumNodes )
        SynchronizeData( true );

      // In prefab stage we only want to render the wire in the Scene View.
      // If paused, we want to render the wire as if not paused.
      var camera = inPrefabStage ?
                     sceneView.camera :
                     null;
      Draw( camera );
    }
#endif

    private static Material DefaultMaterial()
    {
      return Resources.Load<Material>( "Materials/WireMaterial_01" );
    }

    private static Matrix4x4 CalculateCylinderTransform( Vector3 start, Vector3 end, float radius )
    {
      CalculateCylinderTransform( start,
                                  end,
                                  radius,
                                  out var position,
                                  out var rotation,
                                  out var scale );
      return Matrix4x4.TRS( position, rotation, scale );
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
      scale = new Vector3( 2.0f * radius, 0.5f * length, 2.0f * radius );
    }

    private void RenderRoute()
    {
      SynchronizeData( true );
      Draw();
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

      while ( m_positions.Count / 1023 + 1 > m_segmentSphereMatrices.Count )
        m_segmentSphereMatrices.Add(new Matrix4x4[1023]);

      m_numCylinders = 0;

      float radius = Wire.Radius;
      var sphereScale = 2.0f * radius * Vector3.one;
      for ( int i = 0; i < m_positions.Count; ++i ) {
        if ( i > 0 ){
          if (m_numCylinders / 1023 + 1 > m_segmentCylinderMatrices.Count)
            m_segmentCylinderMatrices.Add(new Matrix4x4[1023]);

          m_segmentCylinderMatrices[ m_numCylinders / 1023 ][ m_numCylinders % 1023 ] = CalculateCylinderTransform( m_positions[ i - 1 ],
                                                                                                                    m_positions[ i ],
                                                                                                                    radius );
          m_numCylinders++;
        }

        m_segmentSphereMatrices[ i / 1023 ][ i % 1023 ] = Matrix4x4.TRS( m_positions[ i ],
                                                                         Quaternion.identity,
                                                                         sphereScale );
      }
    }

    private void Draw( Camera camera = null )
    {
      if ( Wire == null )
        return;

      // In prefab stage we avoid calls from Update, LateUpdate so that we
      // don't render the wire in the Game View. Camera is only given as the
      // Scene View camera when editing prefabs.
      if ( camera == null && PrefabUtils.IsPartOfEditingPrefab( gameObject ) )
        return;

      if ( !CreateMeshes() )
        return;

      var forceSynchronize = m_positions.Count > 0 &&
                             ( m_segmentSphereMatrices.Count == 0 ||
                               m_segmentCylinderMatrices.Count == 0 );
      if ( forceSynchronize )
        SynchronizeData( Wire.State != States.INITIALIZED );

      // Spheres
      for ( int i = 0; i < m_positions.Count; i += 1023 ) {
        int count = Mathf.Min( 1023, m_positions.Count - i );
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

    private bool CreateMeshes()
    {
      if ( m_sphereMeshInstance == null )
        m_sphereMeshInstance = CreateMesh( @"Debug/LowPolySphereRenderer" );
      if ( m_cylinderMeshInstance == null )
        m_cylinderMeshInstance = CreateMesh( @"Debug/LowPolyCylinderRenderer" );

      return m_sphereMeshInstance != null && m_cylinderMeshInstance != null;
    }

    private Mesh CreateMesh( string resource )
    {
      GameObject tmp = Resources.Load<GameObject>( resource );
      MeshFilter[] filters = tmp.GetComponentsInChildren<MeshFilter>();
      MeshRenderer[] renderers = tmp.GetComponentsInChildren<MeshRenderer>();
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
      if ( m_meshInstanceProperties == null )
        m_meshInstanceProperties = new MaterialPropertyBlock();
    }

    /// <summary>
    /// Currently only used in the Prefab Stage where normal rendering
    /// is ignored.
    /// </summary>
    /// <param name="isSelected">True if the wire is selected.</param>
    private void DrawGizmos( bool isSelected )
    {
      //if ( Application.isPlaying )
      //  return;

      //if ( Wire == null || Wire.Route == null || Wire.Route.NumNodes < 2 )
      //  return;

      //if ( !PrefabUtils.IsPartOfEditingPrefab( gameObject ) )
      //  return;

      //if ( !CreateMeshes() )
      //  return;

      //var routePoints = Wire.Route.Select( routePoint => routePoint.Position ).ToArray();

      //var defaultColor  = Color.Lerp( Color.black, Color.white, 0.55f );
      //var selectedColor = Color.Lerp( defaultColor, Color.green, 0.15f );
      //Gizmos.color = isSelected ? selectedColor : defaultColor;

      //var radius = Wire.Radius;
      //var sphereScale = 2.0f * radius * Vector3.one;
      //Gizmos.DrawWireMesh( m_sphereMeshInstance,
      //                     0,
      //                     routePoints[ 0 ],
      //                     Quaternion.identity,
      //                     sphereScale );
      //for ( int i = 1; i < routePoints.Length; ++i ) {
      //  Gizmos.DrawWireMesh( m_sphereMeshInstance,
      //                       0,
      //                       routePoints[ i ],
      //                       Quaternion.identity,
      //                       sphereScale );
      //  CalculateCylinderTransform( routePoints[ i - 1 ],
      //                              routePoints[ i ],
      //                              radius,
      //                              out var position,
      //                              out var rotation,
      //                              out var scale );
      //  Gizmos.DrawWireMesh( m_cylinderMeshInstance,
      //                       0,
      //                       position,
      //                       rotation,
      //                       scale );
      //}
    }

    private void OnDrawGizmos()
    {
      DrawGizmos( false );
    }

    private void OnDrawGizmosSelected()
    {
      DrawGizmos( true );
    }

    private Wire m_wire = null;
    private List<Matrix4x4[]> m_segmentSphereMatrices = new List<Matrix4x4[]>();
    private List<Matrix4x4[]> m_segmentCylinderMatrices = new List<Matrix4x4[]>();
    private MaterialPropertyBlock m_meshInstanceProperties = null;
    private Mesh m_sphereMeshInstance = null;
    private Mesh m_cylinderMeshInstance = null;
    private List<Vector3> m_positions = new List<Vector3>();
    private int m_numCylinders = 0;
  }
}
