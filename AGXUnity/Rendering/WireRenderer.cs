using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using AGXUnity.Utils;

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

    // TODO Do draw with DrawMeshInstanced we need the same scale for all instances, thus we need to draw spheres at each point in one call, and cylinders with the same length in another... Rewrite GetOrCreateMesh two have two renderers, plus calculate positions for cylinders in Render
    [SerializeField]
    private SegmentRenderMode m_renderMode = SegmentRenderMode.DrawMeshInstanced;

    [Description("Render particles using cloned GameObjects or with Graphics.DrawMeshInstanced.")]
    public SegmentRenderMode RenderMode
    {
      get { return m_renderMode; }
      set
      {
        m_renderMode = value;

        if ( !IsSynchronizingProperties ) //  && Wire != null 
          InitializeRenderer();
      }
    }

    private SegmentSpawner m_segmentSpawner = null;

    public float NumberOfSegmentsPerMeter = 2.0f;

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

    public Material Material
    {
      get { return m_material ?? m_segmentSpawner.DefaultMaterial; }
      set
      {
        m_material = value ?? m_segmentSpawner.DefaultMaterial;
        if (m_segmentSpawner != null)
          m_segmentSpawner.Material = m_material;
      }
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
      else if ( RenderMode == SegmentRenderMode.DrawMeshInstanced ) {

        GetOrCreateMesh();
        if (m_meshInstance == null)
        {
          Debug.LogError( "Problem initializing mesh!");
          return;
        }

        if ( !m_material.enableInstancing ) {
          Debug.LogError( "AGXUnity.Rendering.WireRenderer: " +
                          "The wire render material must have instancing enabled for this render mode to work.",
                          m_material );
          return;
        }

        Debug.Log("Initialize instanced");

        m_segmentMatrices = new List<Matrix4x4[]> { new Matrix4x4[1023] };
        m_meshInstanceProperties = new MaterialPropertyBlock();
      }
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

      // Let OnDrawGizmos handle rendering when in prefab edit mode.
      // It's not possible to use RuntimeObjects while there.
      if ( PrefabUtils.IsPartOfEditingPrefab( gameObject ) )
        return;

      if ( Wire != null && Wire.Native == null )
        RenderRoute( Wire.Route, Wire.Radius );
    }

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
        Render(Wire);
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

      var isValidDrawInstanceMode = RenderMode == SegmentRenderMode.DrawMeshInstanced &&
                                    m_positions.Count > 0 &&
                                    m_meshInstance != null &&
                                    m_material != null;


      agxWire.RenderIterator it = wire.Native.getRenderBeginIterator();
      agxWire.RenderIterator endIt = wire.Native.getRenderEndIterator();
      while ( !it.EqualWith( endIt ) ) {
        m_positions.Add( it.getWorldPosition().ToHandedVector3() );
        it.inc();
      }

      // TODO this is moved, check that it doesn't break anything
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
        // TODO adapt this and remove loop if max segments is actually 256 like implied above, otherwise complete for possible use of more than 1023 positions
        // TODO this loop is only for end spheres, need cylinders as well
        while ( m_positions.Count / 1023 + 1 > m_segmentMatrices.Count ) {
          m_segmentMatrices.Add(new Matrix4x4[1023]);
        }

        for ( int arrayIndex = 0; arrayIndex < (m_positions.Count / 1023 + 1); ++arrayIndex ) {
          Matrix4x4[] matrices = m_segmentMatrices[arrayIndex];
          int numGranulesInArray = Mathf.Min(1023, m_positions.Count - arrayIndex * 1023);
          for ( int i = 0; i < numGranulesInArray; ++i ) {
            matrices[i] = Matrix4x4.TRS(m_positions[i],
                                        Quaternion.identity,
                                        Vector3.one * wire.Radius * 2.0f );

          }
        }

        // Spheres
        if ( m_positions.Count < 1024 ) {
          Graphics.DrawMeshInstanced( m_meshInstance,
                                      0,
                                      m_material,
                                      m_segmentMatrices[ 0 ],
                                      m_positions.Count,
                                      m_meshInstanceProperties,
                                      m_shadowCastingMode,
                                      m_receiveShadows );
        }
        // DrawMeshInstanced only supports up to 1023 meshes for each call,
        // we need to subdivide if we have more particles than that.
        //else {
        //  for ( int i = 0; i < m_numSegments; i += 1023 ) {
        //    int count = Mathf.Min( 1023, m_numSegments - i );
        //    Graphics.DrawMeshInstanced( m_meshInstance,
        //                                0,
        //                                m_meshInstanceMaterial,
        //                                m_granuleMatrices[ i / 1023 ],
        //                                count,
        //                                m_meshInstanceProperties,
        //                                m_shadowCastingMode,
        //                                m_receiveShadows);
        //  }
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

    public Mesh GetOrCreateMesh()
    {
      if ( m_meshInstance != null )
        return m_meshInstance;

      GameObject tmp = Resources.Load<GameObject>( @"Debug/SphereRenderer" );
      MeshFilter[] filters = tmp.GetComponentsInChildren<MeshFilter>();
      MeshRenderer[] renderers = tmp.GetComponentsInChildren<MeshRenderer>();
      CombineInstance[] combine = new CombineInstance[ filters.Length ];

      for ( int i = 0; i < filters.Length; ++i ) {
        combine[ i ].mesh = filters[ i ].sharedMesh;
        combine[ i ].transform = filters[ i ].transform.localToWorldMatrix;
      }

      m_meshInstance = new Mesh();
      m_meshInstance.CombineMeshes( combine );

      m_shadowCastingMode = renderers[ 0 ].shadowCastingMode;
      m_receiveShadows = renderers[ 0 ].receiveShadows;

      return m_meshInstance;
    }

    private List<Matrix4x4[]> m_segmentMatrices;
    private MaterialPropertyBlock m_meshInstanceProperties = null;
    private Mesh m_meshInstance = null;
    private ShadowCastingMode m_shadowCastingMode = ShadowCastingMode.On;
    private bool m_receiveShadows = true;
  }
}
