using System.Collections.Generic;
using UnityEngine;
using AGXUnity.Utils;

namespace AGXUnity.Models
{
  public class Track : ScriptComponent
  {
    public agxVehicle.Track Native { get; private set; } = null;

    [SerializeField]
    private int m_numberOfNodes = 64;

    [IgnoreSynchronization]
    [ClampAboveZeroInInspector]
    public int NumberOfNodes
    {
      get { return m_numberOfNodes; }
      set
      {
        if ( Native != null ) {
          Debug.LogWarning( "Invalid to change number of nodes on an initialized track.", this );
          return;
        }
        m_numberOfNodes = value;
      }
    }

    [SerializeField]
    private float m_thickness = 0.05f;

    [IgnoreSynchronization]
    [ClampAboveZeroInInspector]
    public float Thickness
    {
      get { return m_thickness; }
      set
      {
        if ( Native != null ) {
          Debug.LogWarning( "Invalid to change thickness of nodes on an initialized track.", this );
          return;
        }
        m_thickness = value;
      }
    }

    [SerializeField]
    private float m_width = 0.35f;

    [IgnoreSynchronization]
    [ClampAboveZeroInInspector]
    public float Width
    {
      get { return m_width; }
      set
      {
        if ( Native != null ) {
          Debug.LogWarning( "Invalid to change width of nodes on an initialized track,", this );
          return;
        }
        m_width = value;
      }
    }

    [SerializeField]
    private float m_initialTensionDistance = 1.0E-3f;

    [IgnoreSynchronization]
    public float InitialTensionDistance
    {
      get { return m_initialTensionDistance; }
      set
      {
        if ( Native != null ) {
          Debug.LogWarning( "Invalid to change initial tension distance on an initialized track.", this );
          return;
        }
        m_initialTensionDistance = value;
      }
    }

    [SerializeField]
    private List<TrackWheel> m_wheels = new List<TrackWheel>();

    /// <summary>
    /// Registered track wheel instances.
    /// </summary>
    [HideInInspector]
    public TrackWheel[] Wheels
    {
      get { return m_wheels.ToArray(); }
    }

    /// <summary>
    /// Associate track wheel instance to this track.
    /// </summary>
    /// <param name="wheel">Track wheel instance to add.</param>
    /// <returns>True if added, false if null or already added.</returns>
    public bool Add( TrackWheel wheel )
    {
      if ( wheel == null || m_wheels.Contains( wheel ) )
        return false;

      m_wheels.Add( wheel );

      return true;
    }

    /// <summary>
    /// Disassociate track wheel instance from this track.
    /// </summary>
    /// <param name="wheel">Track wheel instance to remove.</param>
    /// <returns>True if removed, false if null or not associated to this track.</returns>
    public bool Remove( TrackWheel wheel )
    {
      if ( wheel == null )
        return false;

      return m_wheels.Remove( wheel );
    }

    /// <summary>
    /// True if <paramref name="wheel"/> is associated to this track.
    /// </summary>
    /// <param name="wheel">Track wheel instance.</param>
    /// <returns>True if <paramref name="wheel"/> is associated to this track.</returns>
    public bool Contains( TrackWheel wheel )
    {
      return m_wheels.Contains( wheel );
    }

    /// <summary>
    /// Verifies so that all added track wheels still exists. Wheels that
    /// has been deleted are removed.
    /// </summary>
    public void RemoveInvalidWheels()
    {
      m_wheels.RemoveAll( wheel => wheel == null );
    }

    protected override bool Initialize()
    {
      RemoveInvalidWheels();

      if ( m_wheels.Count == 0 ) {
        Debug.LogError( "Component: Track requires at least one wheel to initialize.", this );
        return false;
      }

      if ( m_wheels.Find( wheel => wheel.GetInitialized<TrackWheel>() == false ) != null ) {
        Debug.LogError( "Component: Track failed to initialize - one or several wheels failed to initialize.", this );
        return false;
      }

      Native = new agxVehicle.Track( (ulong)NumberOfNodes,
                                     Width,
                                     Thickness,
                                     InitialTensionDistance );

      foreach ( var wheel in Wheels )
        Native.add( wheel.Native );

      GetSimulation().add( Native );

      return true;
    }

    protected override void OnDestroy()
    {
      if ( GetSimulation() != null && Native != null )
        GetSimulation().remove( Native );
      Native = null;

      base.OnDestroy();
    }

    private static Mesh m_gizmosMesh = null;

    private static Mesh GetOrCreateGizmosMesh()
    {
      if ( m_gizmosMesh != null )
        return m_gizmosMesh;

      var tmp = Resources.Load<GameObject>( @"Debug/BoxRenderer" );
      var filter = tmp.GetComponentInChildren<MeshFilter>();
      if ( filter == null )
        return null;
      m_gizmosMesh = filter.sharedMesh;
      return m_gizmosMesh;
    }

    //private void OnDrawGizmos()
    //{
    //  if ( Wheels.Length == 0 )
    //    return;

    //  var boxMesh = GetOrCreateGizmosMesh();
    //  if ( boxMesh == null )
    //    return;

    //  var wheels = new agxVehicle.TrackWheelDescVector();
    //  foreach ( var wheel in Wheels ) {
    //    wheels.Add( new agxVehicle.TrackWheelDesc( TrackWheel.ToNative( wheel.Model ),
    //                                               wheel.Radius,
    //                                               new agx.AffineMatrix4x4( wheel.RigidBody.transform.rotation.ToHandedQuat(),
    //                                                                        wheel.RigidBody.transform.position.ToHandedVec3() ),
    //                                               wheel.Frame.NativeLocalMatrix ) );
    //  }
    //  var nodes = agxVehicle.agxVehicleSWIG.findTrackNodeConfiguration( new agxVehicle.TrackDesc( (ulong)NumberOfNodes,
    //                                                                                              Width,
    //                                                                                              Thickness,
    //                                                                                              InitialTensionDistance ),
    //                                                                    wheels );
    //  foreach ( var node in nodes ) {
    //    Gizmos.DrawWireMesh( boxMesh,
    //                     node.transform.getTranslate().ToHandedVector3() + node.transform.transform3x3( new agx.Vec3( 0, 0, node.halfExtents.z ) ).ToHandedVector3(),
    //                     node.transform.getRotate().ToHandedQuaternion(),
    //                     2.0f * node.halfExtents.ToVector3() );
    //  }
    //}
  }
}
