using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using AGXUnity.Utils;
using UnityEditor;

namespace AGXUnity.Rendering
{
  [AddComponentMenu( "AGXUnity/Rendering/Cable Renderer" )]
  [ExecuteInEditMode]
  [RequireComponent( typeof( Cable ) )]
  public class CableRenderer : ScriptComponent
  {
    /// <summary>
    /// Shadow casting mode On for casting shadows, Off for no shadows.
    /// </summary>
    public ShadowCastingMode ShadowCastingMode = ShadowCastingMode.On;

    /// <summary>
    ///True for the cable to receive shadows, false to not receive shadows.
    /// </summary>
    public bool ReceiveShadows = true;

    private List<Matrix4x4[]> m_segmentSphereMatrices = new List<Matrix4x4[]>();
    private List<Matrix4x4[]> m_segmentCylinderMatrices = new List<Matrix4x4[]>();
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

    private static Material DefaultMaterial()
    {
      return Resources.Load<Material>( "Materials/CableMaterial_01" );
    }

    private CableDamage m_cableDamage = null;
    private CableDamage CableDamage { get { return m_cableDamage ?? ( m_cableDamage = GetComponent<CableDamage>() ); } }

    public void SetRenderDamages(bool value) => m_renderDamages = value;

    private bool m_renderDamages = false, m_previousRenderDamages = false;
    private Dictionary<int, (MeshRenderer, MeshRenderer)> m_segmentRenderers = new Dictionary<int, (MeshRenderer, MeshRenderer)>();

    public bool InitializeRenderer( bool destructLast = false )
    {
      if ( !CreateMeshes() ) {
        Debug.LogError( "AGXUnity.Rendering.CableRenderer: Problem initializing one or both meshes!", this);
        return false;
      }

      if ( !Material.enableInstancing ) {
        Debug.LogError( "AGXUnity.Rendering.CableRenderer: The cable render material must have instancing enabled for this render mode to work.",
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

#if UNITY_EDITOR
    // Editing cable prefab in a prefab stage and when the editor is paused
    // requires Scene View GUI update callback.
    private void OnSceneView( SceneView sceneView )
    {
      var inPrefabStage = PrefabUtils.IsPartOfEditingPrefab( gameObject );
      var performDraw = EditorApplication.isPaused || inPrefabStage;
      if ( !performDraw )
        return;

      if ( inPrefabStage && m_positions.Count != Cable.Route.NumNodes )
        SynchronizeData( true );

      // In prefab stage we only want to render the cable in the Scene View.
      // If paused, we want to render the cable as if not paused.
      var camera = inPrefabStage ?
                     sceneView.camera :
                     null;
      Draw( camera );
    }
#endif

    public void Update()
    {
      SynchronizeData( false );
      Draw();
    }

    protected void LateUpdate()
    {
      // Late update from Editor. Exit if the application is running.
      if ( Application.isPlaying )
        return;

      RenderRoute();
    }

    void Draw( Camera camera = null )
    {
      if ( Cable == null )
        return;

      // In prefab stage we avoid calls from Update, LateUpdate so that we
      // don't render the cable in the Game View. Camera is only given as the
      // Scene View camera when editing prefabs.
      if ( camera == null && PrefabUtils.IsPartOfEditingPrefab( gameObject ) )
        return;

      if ( !CreateMeshes() )
        return;

      var forceSynchronize = m_positions.Count > 0 &&
                             ( m_segmentSphereMatrices.Count == 0 ||
                               m_segmentCylinderMatrices.Count == 0 );
      if ( forceSynchronize )
        SynchronizeData( Cable.State != States.INITIALIZED );

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
      if ( Cable == null )
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
        var endIt = Cable.Native.end();
        while ( !it.EqualWith( endIt ) ) {
          m_positions.Add( it.getEndPosition().ToHandedVector3() ); // TODO getBeginPosition?
          it.inc();
        }

        it.ReturnToPool();
        endIt.ReturnToPool();
      }

      while ( m_positions.Count / 1023 + 1 > m_segmentSphereMatrices.Count )
        m_segmentSphereMatrices.Add(new Matrix4x4[1023]);

      m_numCylinders = 0;

      float radius = Cable.Radius;
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

/*    private void Render()
    {
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

      if(m_renderMode == CableRenderMode.GameObject)
      { 
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

              // If using render damage
              if ((m_renderDamages || m_previousRenderDamages)){
                int id = go.GetInstanceID();

                (MeshRenderer, MeshRenderer) meshRenderers;
                if (m_segmentRenderers.TryGetValue(id, out meshRenderers) && meshRenderers.Item1 != null){
                  if (m_renderDamages && CableDamage.DamageValueCount == (int)native.getNumSegments()){ // TODO do we need the length check?
                    float t = CableDamage.DamageValue(i) / CableDamage.MaxDamage;
                    block.SetColor("_Color", Color.Lerp(CableDamage.Properties.MinColor, CableDamage.Properties.MaxColor, t));
                    meshRenderers.Item1.SetPropertyBlock(block);
                    meshRenderers.Item2.SetPropertyBlock(block);
                  }
                  else{
                    block.SetColor("_Color", Material.color);
                    meshRenderers.Item1.SetPropertyBlock(block);
                    meshRenderers.Item2.SetPropertyBlock(block);
                  }
                }
                else {
                  var renderers = go.GetComponentsInChildren<MeshRenderer>();
                  m_segmentRenderers.Add(id, (renderers[0], renderers[1]));
                }
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

          m_previousRenderDamages = m_renderDamages;

          it.ReturnToPool();
          endIt.ReturnToPool();
      }
      else if(m_renderMode == CableRenderMode.DrawMeshInstanced)
      
      
      // Create per-segment matrices if not yet created.
      ulong numSegments = native.getNumSegments();
      if (m_cylinderMatrices == null || (ulong)m_cylinderMatrices.Length != numSegments)
      {
          m_cylinderMatrices = new Matrix4x4[numSegments];
      }
      if( m_halfSphereMatrices == null || (ulong)m_halfSphereMatrices.Length != numSegments + 1)
      {
          m_halfSphereMatrices = new Matrix4x4[numSegments + 1];
      }
      // Update per-segment matrices.
      var it = native.begin();
      var endIt = native.end();
      try 
      {
        int i = 0;
        for (; !it.EqualWith(endIt); it.inc(), i++)
        {
          // TODO: Draw cylinders between segment end-points instead, as GameObject rendering mode does above.

          // TODO: Add support for cable damage color rendering.
          //       Should be possible by writing the damage factor as an array parameter to the material,
          //       and then read it from the shader, using instance index as lookup. Check out SetVectorArray,
          //       of MaterialPropertyBlock, and/or the Graphics.DrawMeshInstancedXXX variants.
          
          Quaternion rotation = Quaternion.FromToRotation(Vector3.up, it.getDirection().ToHandedVector3());
          m_cylinderMatrices[i] = Matrix4x4.TRS(
              it.getCenterPosition().ToHandedVector3(),
              rotation,
              new Vector3(Cable.Diameter, (float)it.getLength(), Cable.Diameter));
          
          m_halfSphereMatrices[i] = Matrix4x4.TRS(
              it.getBeginPosition().ToHandedVector3(),
              rotation * Quaternion.AngleAxis(180, Vector3.right),
              new Vector3(Cable.Diameter, Cable.Diameter, Cable.Diameter));
          
          if (i + 2 == m_halfSphereMatrices.Length)
          {
            m_halfSphereMatrices[i + 1] = Matrix4x4.TRS(
              it.getEndPosition().ToHandedVector3(),
              rotation,
              new Vector3(Cable.Diameter, Cable.Diameter, Cable.Diameter));
          }
        }
      }
      finally
      { 
          it.ReturnToPool();
          endIt.ReturnToPool();
      }
    }*/

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

    private void DrawGizmos( bool isSelected )
    {
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
