using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using AGXUnity.Utils;

namespace AGXUnity.Rendering
{
  [AddComponentMenu( "AGXUnity/Rendering/Wire Renderer" )]
  [ExecuteInEditMode]
  [RequireComponent( typeof( Wire ) )]
  public class WireRenderer : ScriptComponent
  {
    [SerializeField]
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
        m_segmentSpawner.Material = m_material;
      }
    }

    public void OnPostStepForward( Wire wire )
    {
      if ( wire != null )
        Render( wire );
    }

    public void InitializeRenderer( bool destructLast = false )
    {
      if ( destructLast && m_segmentSpawner != null ) {
        m_segmentSpawner.Destroy();
        m_segmentSpawner = null;
      }

      m_segmentSpawner = new SegmentSpawner( Wire,
                                             @"Wire/WireSegment",
                                             @"Wire/WireSegmentBegin" );
      m_segmentSpawner.Initialize( gameObject );
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

      it.ReturnToPool();
      endIt.ReturnToPool();
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
  }
}
