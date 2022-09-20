using UnityEngine;
using AGXUnity.Utils;

namespace AGXUnity.Rendering
{
  [AddComponentMenu( "AGXUnity/Rendering/Cable Renderer" )]
  [ExecuteInEditMode]
  [RequireComponent( typeof( Cable ) )]
  public class CableRenderer : ScriptComponent
  {
    [SerializeField]
    private SegmentSpawner m_segmentSpawner = null;

    [HideInInspector]
    public SegmentSpawner SegmentSpawner { get { return m_segmentSpawner; } }

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
      get { return m_material ?? m_segmentSpawner.DefaultMaterial; }
      set
      {
        m_material = value ?? m_segmentSpawner.DefaultMaterial;
        m_segmentSpawner.Material = m_material;
      }
    }

    public void InitializeRenderer( bool destructLast = false )
    {
      if ( m_segmentSpawner != null ) {
        m_segmentSpawner.Destroy();
        m_segmentSpawner = null;
      }

      m_segmentSpawner = new SegmentSpawner( Cable,
                                             @"Cable/CableSegment",
                                             @"Cable/CableSegmentBegin" );
      m_segmentSpawner.Initialize( gameObject );
    }

    protected override void OnEnable()
    {
      OnEnableDisable( true );
    }

    protected override void OnDisable()
    {
      OnEnableDisable( false );
    }

    protected void LateUpdate()
    {
      // Late update from Editor. Exit if the application is running.
      if ( Application.isPlaying )
        return;

      Render( Cable.Route, Cable.Radius );
    }

    private void Render( CableRoute route, float radius )
    {
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
          m_segmentSpawner.CreateSegment( points[ i - 1 ], points[ i ], radius );
      }
      catch ( System.Exception e ) {
        Debug.LogException( e, this );
      }
      m_segmentSpawner.End();
    }

    private void Render()
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

      var it = native.begin();
      var endIt = native.end();

      m_segmentSpawner.Begin();
      try {
        float radius = Cable.Radius;
        var prevEndPosition = it.EqualWith( endIt ) ?
                                Vector3.zero :
                                it.getBeginPosition().ToHandedVector3();
        while ( !it.EqualWith( endIt ) ) {
          var endPosition = it.getEndPosition().ToHandedVector3();
          m_segmentSpawner.CreateSegment( prevEndPosition, endPosition, radius );
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

    private void OnEnableDisable( bool enable )
    {
      if ( enable ) {
        InitializeRenderer( true );
        if ( Application.isPlaying )
          Simulation.Instance.StepCallbacks.PostStepForward += Render;
      }
      else {
        if ( m_segmentSpawner != null ) {
          m_segmentSpawner.Destroy();
          m_segmentSpawner = null;
        }

        if ( Simulation.HasInstance && Application.isPlaying )
          Simulation.Instance.StepCallbacks.PostStepForward -= Render;
      }
    }

    private void DrawGizmos( bool isSelected )
    {
      if ( Application.isPlaying )
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
