using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using AGXUnity.Utils;

namespace AGXUnity.Rendering
{
  [AddComponentMenu( "" )]
  [ExecuteInEditMode]
  public class WireRenderer : ScriptComponent
  {
    [SerializeField]
    private SegmentSpawner m_segmentSpawner = null;

    public float NumberOfSegmentsPerMeter = 2.0f;

    [HideInInspector]
    public SegmentSpawner SegmentSpawner { get { return m_segmentSpawner; } }

    [SerializeField]
    private Material m_material = null;
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

      m_segmentSpawner = new SegmentSpawner( GetComponent<Wire>(), @"Wire/WireSegment", @"Wire/WireSegmentBegin" );
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

      Wire wire = GetComponent<Wire>();
      if ( wire != null && wire.Native == null )
        RenderRoute( wire.Route, wire.Radius );
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

      List<Vector3> positions      = new List<Vector3>();
      positions.Capacity           = 256;
      agxWire.RenderIterator it    = wire.Native.getRenderBeginIterator();
      agxWire.RenderIterator endIt = wire.Native.getRenderEndIterator();
      while ( !it.EqualWith( endIt ) ) {
        positions.Add( it.get().getWorldPosition().ToHandedVector3() );
        it.inc();
      }

      m_segmentSpawner.Begin();

      try {
        for ( int i = 0; i < positions.Count - 1; ++i ) {
          Vector3 curr       = positions[ i ];
          Vector3 next       = positions[ i + 1 ];
          Vector3 currToNext = next - curr;
          float distance     = currToNext.magnitude;
          currToNext        /= distance;
          int numSegments    = Convert.ToInt32( distance * NumberOfSegmentsPerMeter + 0.5f );
          float dl           = distance / numSegments;
          for ( int j = 0; j < numSegments; ++j ) {
            next = curr + dl * currToNext;

            m_segmentSpawner.CreateSegment( curr, next, wire.Radius );
            curr = next;
          }
        }
      }
      catch ( System.Exception e ) {
        Debug.LogException( e );
      }

      m_segmentSpawner.End();
    }
  }
}
