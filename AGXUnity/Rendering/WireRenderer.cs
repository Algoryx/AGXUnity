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
    public enum SegmentRenderMode
    {
      GameObject,
      DrawMeshInstanced
    }

    [SerializeField]
    private SegmentRenderMode m_renderMode = SegmentRenderMode.DrawMeshInstanced;

    [Description("Render particles using cloned GameObjects or with Graphics.DrawMeshInstanced.")]
    [HideInInspector]
    public SegmentRenderMode RenderMode
    {
      get { return m_renderMode; }
      set
      {
        if ( m_renderMode == SegmentRenderMode.GameObject && m_segmentSpawner != null )
          m_segmentSpawner.Destroy();

        m_renderMode = value;

        if ( !IsSynchronizingProperties ) //  && Wire != null 
          InitializeRenderer();
      }
    }

    private SegmentSpawner m_segmentSpawner = null;

    /*
     * Only used in GameObject rendering mode
     */
    [HideInInspector]
    public float NumberOfSegmentsPerMeter = 2.0f;

    /*
     * Only used in DrawMeshInstanced rendering mode
     */
    [HideInInspector]
    public ShadowCastingMode ShadowCastingMode = ShadowCastingMode.On;
    /*
     * Only used in DrawMeshInstanced rendering mode
     */
    [HideInInspector]
    public bool ReceiveShadows = true;

    [HideInInspector]
    public SegmentSpawner SegmentSpawner { get { return m_segmentSpawner; } }

    [NonSerialized]
    private Wire m_wire = null;

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

    private List<Vector3> m_positions;

    private int m_numCylinders = 0;

    [HideInInspector]
    public Material Material
    {
      get { return m_material ?? DefaultMaterial(); }
      set
      {
        m_material = value ?? DefaultMaterial();
        if (m_segmentSpawner != null)
          m_segmentSpawner.Material = m_material;
      }
    }

    private Material DefaultMaterial()
    {
      if (m_segmentSpawner == null)
        m_segmentSpawner = new SegmentSpawner( Wire,
                                         @"Wire/WireSegment",
                                         @"Wire/WireSegmentBegin" );
      return m_segmentSpawner.DefaultMaterial;    
    }

    public void Update()
    {
      if ( Wire != null && m_renderMode == SegmentRenderMode.DrawMeshInstanced)
        Render( Wire );
    }

    public void OnPostStepForward( Wire wire )
    {
      if ( wire != null && m_renderMode == SegmentRenderMode.GameObject)
        Render( wire );
    }

    public void InitializeRenderer( bool destructLast = false )
    {
      if ( destructLast && m_segmentSpawner != null ) {
        m_segmentSpawner.Destroy();
        m_segmentSpawner = null;
      }

      if (m_renderMode == SegmentRenderMode.GameObject)
      {
        m_segmentSpawner = new SegmentSpawner( Wire,
                                               @"Wire/WireSegment",
                                               @"Wire/WireSegmentBegin" );
        m_segmentSpawner.Initialize( gameObject );
      }
      else if ( RenderMode == SegmentRenderMode.DrawMeshInstanced ) 
      {
        if (!CreateMeshes())
        {
          Debug.LogError( "AGXUnity.Rendering.WireRenderer: " +
                          "Problem initializing one or meshes!");
          return;
        }

        if ( !Material.enableInstancing ) {
          Debug.LogError( "AGXUnity.Rendering.WireRenderer: " +
                          "The wire render material must have instancing enabled for this render mode to work.",
                          Material );
          return;
        }

        InitMatrices();
      }
    }

    private void InitMatrices()
    {
      if (m_segmentSphereMatrices == null)
        m_segmentSphereMatrices = new List<Matrix4x4[]> { new Matrix4x4[1023] };
      if (m_segmentCylinderMatrices == null)
        m_segmentCylinderMatrices = new List<Matrix4x4[]> { new Matrix4x4[1023] };
      if (m_meshInstanceProperties == null)
        m_meshInstanceProperties = new MaterialPropertyBlock();
    }

    protected override bool Initialize()
    {
      InitializeRenderer( true );

      return base.Initialize();
    }

    protected override void OnDestroy()
    {
      if ( m_segmentSpawner != null )
        m_segmentSpawner.Destroy();
      m_segmentSpawner = null;

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

      // Let OnDrawGizmos handle rendering when in prefab edit mode and using GameObjects for drawing.
      // It's not possible to use RuntimeObjects while there.
      if ( PrefabUtils.IsPartOfEditingPrefab( gameObject ) && RenderMode == SegmentRenderMode.GameObject)
        return;

      if ( Wire != null && Wire.Native == null )
        RenderRoute( Wire.Route, Wire.Radius );
    }

#if UNITY_EDITOR

    private int m_counter = 0;
    // DrawMeshInstanced stuff is sometimes lost when in Editor pause. We don't know when - Quick and dirty fix, just redraw it occasionally
    void OnRenderObject()
    {
      if ( !EditorApplication.isPaused)
        return;
      if ( Wire != null && m_counter++ > 10 && CreateMeshes())
      {
        InitMatrices();
        DrawWireInstanced();
        m_counter = 0;
      }
    }
#endif

    private void RenderRoute( WireRoute route, float radius )
    {
      if ( route == null )
        return;

      if (m_renderMode == SegmentRenderMode.GameObject)
      {
        m_segmentSpawner.Begin();

        try {
          WireRouteNode[] nodes = route.ToArray();
          for ( int i = 1; i < nodes.Length; ++i )
            m_segmentSpawner.CreateSegment( nodes[ i - 1 ].Position, nodes[ i ].Position, radius );
        }
        catch ( System.Exception e ) {
          Debug.LogException( e );
        }

        m_segmentSpawner.End();
      }
      else if (m_renderMode == SegmentRenderMode.DrawMeshInstanced)
      {
        if ( m_positions == null ) {
          m_positions = new List<Vector3>();
          m_positions.Capacity = 256;
        }
        m_positions.Clear();

        try {
          WireRouteNode[] nodes = route.ToArray();
          for ( int i = 0; i < nodes.Length; ++i )
            m_positions.Add(nodes[ i ].Position);
        }
        catch ( System.Exception e ) {
          Debug.LogException( e );
        }

        InitMatrices();
        // TODO This could be optimized by only recalculating it when the wire was altered
        CalculateDrawInstancedData(Wire);
        DrawWireInstanced();
      }
    }

    private void Render( Wire wire )
    {
      if ( wire.Native == null ) {
        if ( m_segmentSpawner != null ) {
          m_segmentSpawner.Destroy();
          m_segmentSpawner = null;
        }
        return;
      }

      if ( m_positions == null ) {
        m_positions = new List<Vector3>();
        m_positions.Capacity = 256;
      }
      m_positions.Clear();

      agxWire.RenderIterator it = wire.Native.getRenderBeginIterator();
      agxWire.RenderIterator endIt = wire.Native.getRenderEndIterator();
      while ( !it.EqualWith( endIt ) ) {
        m_positions.Add( it.getWorldPosition().ToHandedVector3() );
        it.inc();
      }

      it.ReturnToPool();
      endIt.ReturnToPool();

      if (m_renderMode == SegmentRenderMode.GameObject)
      {
        m_segmentSpawner.Begin();
  
        try
        {
          for ( int i = 0; i < m_positions.Count - 1; ++i ) {
            Vector3 curr        = m_positions[i];
            Vector3 next        = m_positions[i + 1];
            Vector3 currToNext  = next - curr;
            float distance      = currToNext.magnitude;
            currToNext         /= distance;
            int numSegments     = Convert.ToInt32(distance * NumberOfSegmentsPerMeter + 0.5f);
            float dl            = distance / numSegments;
            for ( int j = 0; j < numSegments; ++j ) {
              next = curr + dl * currToNext;
  
              m_segmentSpawner.CreateSegment(curr, next, wire.Radius);
              curr = next;
            }
          }
        }
        catch (System.Exception e)
        {
          Debug.LogException(e);
        }
  
        m_segmentSpawner.End();
      }
      else 
      {
        CalculateDrawInstancedData(wire);
        DrawWireInstanced();
      }
    }

    private void CalculateDrawInstancedData(Wire wire)
    {
      while ( m_positions.Count / 1023 + 1 > m_segmentSphereMatrices.Count ) {
        m_segmentSphereMatrices.Add(new Matrix4x4[1023]);
      }

      m_numCylinders = 0;

      float segmentLength = float.MaxValue;
      for ( int i = 0; i < m_positions.Count - 1; ++i ) {
        segmentLength = Mathf.Min(segmentLength, (m_positions[i + 1] - m_positions[i]).sqrMagnitude);
      }
      if (segmentLength > 0)
        segmentLength = Mathf.Sqrt(segmentLength);

      Vector3 cylinderScale = new Vector3(wire.Radius * 2.0f, segmentLength / 2, wire.Radius * 2.0f);
      for ( int i = 0; i < m_positions.Count; ++i ) {
        if (i < m_positions.Count - 1){
          Vector3 curr        = m_positions[i];
          Vector3 next        = m_positions[i + 1];
          Vector3 currToNext  = next - curr;
          float distance      = currToNext.magnitude;
          currToNext         /= distance;
          int numSegments     = Convert.ToInt32(Mathf.Ceil(distance / segmentLength));
          Quaternion rotation = Quaternion.FromToRotation( Vector3.up, currToNext );

          curr += segmentLength / 2f * currToNext;

          for ( int j = 0; j < numSegments; ++j ) {
            if (m_numCylinders / 1023 + 1 > m_segmentCylinderMatrices.Count)
              m_segmentCylinderMatrices.Add(new Matrix4x4[1023]);

            m_segmentCylinderMatrices[m_numCylinders / 1023][m_numCylinders % 1023] = 
              Matrix4x4.TRS(curr,
                            rotation,
                            cylinderScale );

            if (j < numSegments - 2)
              curr = curr + segmentLength  * currToNext;
            else
              curr =  next - segmentLength / 2f  * currToNext;

            m_numCylinders++;
          }
        }

        m_segmentSphereMatrices[i / 1023][i % 1023] = 
          Matrix4x4.TRS(m_positions[i],
                        Quaternion.identity,
                        Vector3.one * wire.Radius * 2.0f );
      }

    }

    private void DrawWireInstanced()
    {
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
                                    ReceiveShadows);
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
                                    ReceiveShadows);
      }
    }

    private void DrawGizmos( bool isSelected )
    {
      if ( Application.isPlaying )
        return;

      if ( Wire == null || Wire.Route == null || Wire.Route.NumNodes < 2 )
        return;

      if ( !PrefabUtils.IsPartOfEditingPrefab( gameObject ) )
        return;

      var routePoints = Wire.Route.Select( routePoint => routePoint.Position ).ToArray();

      var defaultColor  = Color.Lerp( Color.black, Color.white, 0.55f );
      var selectedColor = Color.Lerp( defaultColor, Color.green, 0.15f );
      m_segmentSpawner?.DrawGizmos( routePoints,
                                    Wire.Radius,
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

    public bool CreateMeshes()
    {
      if (m_sphereMeshInstance == null)
        m_sphereMeshInstance = CreateMesh(@"Debug/LowPolySphereRenderer");
      if (m_cylinderMeshInstance == null)
        m_cylinderMeshInstance = CreateMesh(@"Debug/LowPolyCylinderRenderer");

      return (m_sphereMeshInstance != null || m_cylinderMeshInstance != null);
    }

    public Mesh CreateMesh(string resource)
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

    private List<Matrix4x4[]> m_segmentSphereMatrices, m_segmentCylinderMatrices;
    private MaterialPropertyBlock m_meshInstanceProperties = null;
    private Mesh m_sphereMeshInstance = null, m_cylinderMeshInstance = null;
  }
}
