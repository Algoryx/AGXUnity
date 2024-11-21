using AGXUnity.Utils;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace AGXUnity
{
  [AddComponentMenu( "AGXUnity/Cable Tunneling Guard" )]
  [DisallowMultipleComponent]
  [RequireComponent( typeof( AGXUnity.Cable ) )]
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#cable-tunneling-guard" )]
  [ExecuteInEditMode]
  public class CableTunnelingGuard : ScriptComponent
  {
    /// <summary>
    /// Native instance of the cable tuneling guard.
    /// </summary>
    public agxCable.CableTunnelingGuard Native { get; private set; }

    [System.NonSerialized]
    private Cable m_cable = null;

    /// <summary>
    /// The Cable ScriptComponent that this CableTunnelingGuard follows
    /// </summary>
    [HideInInspector]
    public Cable Cable { get { return m_cable ??= GetComponent<Cable>(); } }

    /// <summary>
    /// The mesh which is used to visualise a hull
    /// </summary>
    private Mesh m_mesh = null;

    [SerializeField]
    private double m_hullScale = 4;

    public double HullScale
    {
      get { return m_hullScale; }
      set
      {
        if ( m_hullScale != value ) {
          m_hullScale = value;
          UpdateRenderingMesh();
        }

        if ( Native != null ) {
          Native.setHullScale( m_hullScale );
        }
      }
    }

    private float DistanceFromLine( Vector3 point, Vector3 lineStart, Vector3 lineDirection )
    {
      Vector3 PV = point - lineStart;

      // Calculate at which value of t the point is closest to the line (where the vector V->projD_PV touches the line)
      float t = Vector3.Dot(PV, lineDirection) / lineDirection.sqrMagnitude;

      if ( t >= 0 && t <= 1 ) // Projection is within the interval and the distance is measured to t * lineDirection       
        return ( PV - lineDirection * t ).magnitude;
      else // Projection is outside the interval, return distance to closest endpoint
        return ( point - ( lineStart + Mathf.Clamp01( t ) * lineDirection ) ).magnitude;

    }

    private void UpdateRenderingMesh()
    {
      if ( m_mesh == null )
        m_mesh = new Mesh();
      if ( m_pointCurveCache != null && m_pointCurveCache.Length >= 2 ) {
        float segmentLength = ( Cable.GetRoutePoints()[ 0 ]-Cable.GetRoutePoints()[ 1 ] ).magnitude;
        CapsuleShapeUtils.CreateCapsuleMesh( Cable.Radius * (float)m_hullScale, segmentLength, 0.7f, m_mesh );

        #region boolean_mesh_generatino
        //double segmentLength = (Cable.GetRoutePoints()[0]-Cable.GetRoutePoints()[1]).magnitude;
        //var meshData = agxUtil.PrimitiveMeshGenerator.createCapsule(Cable.Radius * m_hullScale, segmentLength, 1f).getMeshData();

        //Mesh[] capsuleMeshes = new Mesh[m_pointCurveCache.Length-1];



        //for ( int i = 1; i < m_pointCurveCache.Length; i++ ) {
        //  var point = m_pointCurveCache[i];
        //  var prevPoint = m_pointCurveCache[i-1];

        //  Vector3 direction = point-prevPoint;
        //  Vector3 center = prevPoint + direction * 0.5f;
        //  var transform = Matrix4x4.TRS(center, Quaternion.FromToRotation(Vector3.up, direction), Vector3.one);

        //  Mesh capsuleMesh = new Mesh();
        //  var vertices = meshData.getVertices().Select( x => transform.MultiplyPoint( x.ToHandedVector3() ) ).ToArray();
        //  var indices = meshData.getIndices().Select( x => (int)x ).ToArray();
        //  int numIndices = indices.Length;
        //  int numVertices = vertices.Length;

        //  List<int> trimmedVertices = new List<int>(capsuleMesh.vertexCount/2);


        //  for ( int j = 0; j < vertices.Length; j++ ) {
        //    var vertex = vertices[j];
        //    // Check distance to neighboring lines
        //    if ( i > 1 ) {
        //      Vector3 prevDirection = prevPoint-m_pointCurveCache[i-2];
        //      if ( DistanceFromLine( vertex, m_pointCurveCache[ i-1 ], prevDirection ) > Cable.Radius*m_hullScale*1.01f ) {
        //        trimmedVertices.Add( j );
        //        continue;
        //      }
        //    }
        //    if ( i != m_pointCurveCache.Length-1 ) {
        //      Vector3 nextDirection = m_pointCurveCache[i+1] - point;
        //      if ( DistanceFromLine( vertex, point, nextDirection ) > Cable.Radius*m_hullScale*1.01f ) {
        //        trimmedVertices.Add( j );
        //        continue;
        //      }
        //    }
        //  }

        //  //Each trimmed vertex needs to be removed and then all faces with that vertex needs to be destroyed, and additionally the vertices that touched the trimmed vertex via a face needs to be assigned a new neighbour instead
        //  foreach ( var trimmed in trimmedVertices ) {
        //    if(trimmed != numVertices-1)
        //      vertices[ trimmed ] = vertices[ numVertices-1 ];
        //    for ( int j = 0; j < numIndices; j += 3 ) {
        //      if(trimmed != numVertices-1) {
        //        if ( indices[ j ] == numVertices )
        //          indices[ j ] = trimmed;
        //        if ( indices[ j+1 ] == numVertices )
        //          indices[ j+1 ] = trimmed;
        //        if ( indices[ j+2 ] == numVertices )
        //          indices[ j+2 ] = trimmed;
        //      }


        //      if (indices[ j ] == trimmed || indices[ j +1 ] == trimmed || indices[ j +2 ] == trimmed) {
        //        if( j != numIndices-3 ) {
        //          indices[ j ] = indices[ numIndices-3 ];
        //          indices[ j+1 ] = indices[ numIndices-2 ];
        //          indices[ j+2 ] = indices[ numIndices-1 ];
        //          j -= 3;
        //        }                
        //        numIndices -= 3;
        //      }
        //    }
        //    numVertices--;
        //  }

        //  capsuleMesh.vertices = vertices.Take( numVertices ).ToArray();
        //  capsuleMesh.triangles = indices.Take( numIndices ).ToArray();

        //  capsuleMesh.name = "CableTunnelingGuard - Hull mesh";
        //  capsuleMesh.RecalculateBounds();
        //  capsuleMesh.RecalculateNormals();
        //  capsuleMesh.RecalculateTangents();
        //  capsuleMeshes[ i-1 ] = capsuleMesh;
        //}

        //// Create a new CombineInstance array to combine meshes
        //CombineInstance[] combine = new CombineInstance[capsuleMeshes.Length];

        //for ( int i = 0; i < capsuleMeshes.Length; i++ ) {
        //  combine[ i ].mesh = capsuleMeshes[ i ];
        //}

        //// Create a new mesh and assign the combined mesh to it
        //Mesh combinedMesh = new Mesh();
        //combinedMesh.CombineMeshes( combine, mergeSubMeshes: false, useMatrices: false );
        //m_mesh = combinedMesh;
        #endregion
      }
      }

    [SerializeField]
    private double m_angleThreshold = 90.0 * 0.9;

    public double AngleThreshold
    {
      get { return m_angleThreshold; }
      set
      {
        m_angleThreshold = value;
        if ( Native != null ) {
          Native.setAngleThreshold( m_angleThreshold / 180.0 * Mathf.PI );
        }
      }
    }

    [SerializeField]
    private double m_leniency = 0;

    public double Leniency
    {
      get { return m_leniency; }
      set
      {
        m_leniency = value;
        if ( Native != null ) {
          Native.setLeniency( m_leniency );
        }
      }
    }

    [SerializeField]
    private uint m_debounceSteps = 0;

    public uint DebounceSteps
    {
      get { return m_debounceSteps; }
      set
      {
        m_debounceSteps = value;
        if ( Native != null ) {
          Native.setDebounceSteps( m_debounceSteps );
        }
      }
    }

    [SerializeField]
    private bool m_alwaysAdd = false;

    public bool AlwaysAdd
    {
      get { return m_alwaysAdd; }
      set
      {
        m_alwaysAdd = value;
        if ( Native != null ) {
          Native.setAlwaysAdd( m_alwaysAdd );
        }
      }
    }

    [SerializeField]
    private bool m_enableSelfInteraction = true;

    public bool EnableSelfInteraction
    {
      get { return m_enableSelfInteraction; }
      set
      {
        m_enableSelfInteraction = value;
        if ( Native != null ) {
          Native.setEnableSelfInteraction( m_enableSelfInteraction );
        }
      }
    }

    protected override bool Initialize()
    {
      var cable = Cable?.GetInitialized<Cable>()?.Native;
      if ( cable == null ) {
        Debug.LogWarning( "Unable to find Cable component for CableTunnelingGuard - cable tunneling guard instance ignored.", this );
        return false;
      }

      cable.addComponent( Native );

      return true;
    }

    protected override void OnDestroy()
    {
      var cable = Cable?.GetInitialized<Cable>()?.Native;
      if ( cable != null ) {
        cable.removeComponent( Native );
      }

      Native = null;

      base.OnDestroy();
    }

    protected override void OnEnable()
    {
      Native?.setEnabled( true );
    }

    protected override void OnDisable()
    {
      Native?.setEnabled( false );
    }

    private void Reset()
    {
      if ( GetComponent<Cable>() == null )
        Debug.LogError( "Component: CableDamage requires Cable component.", this );
    }

    private Vector3[] m_pointCurveCache = null;

    private bool CheckCableRouteChanges()
    {
      var routePointCahce = Cable.GetRoutePoints();
      if ( m_pointCurveCache != routePointCahce ) {
        m_pointCurveCache = routePointCahce;
        return true;
      }
      return false;
    }

    protected void LateUpdate()
    {
      // Late update from Editor. Exit if the application is running.
      if ( Application.isPlaying )
        return;

      if ( CheckCableRouteChanges() ) {
        UpdateRenderingMesh();
      }
    }

    private void OnDrawGizmosSelected()
    {
      //if(m_mesh != null ) {
      //  var color = Color.Lerp( Color.yellow, Color.red, 0.5f );
      //  color.a = 0.2f;
      //  Gizmos.color = color;
      //  Gizmos.DrawWireMesh( m_mesh, Vector3.zero);
      //}
      if ( enabled && m_pointCurveCache != null && m_pointCurveCache.Length != 0 ) {
        var color = Color.Lerp( Color.yellow, Color.red, 0.5f );
        color.a = 0.01f;
        Gizmos.color = color;
        Vector3 prevPoint = m_pointCurveCache[0];
        foreach ( var point in m_pointCurveCache.Skip( 1 ) ) {
          Vector3 direction = point-prevPoint;
          Vector3 center = prevPoint + direction * 0.5f;
          Gizmos.DrawWireMesh( m_mesh, center, Quaternion.FromToRotation( Vector3.up, direction ) );
          prevPoint = point;
        }
      }
    }
  }

}
