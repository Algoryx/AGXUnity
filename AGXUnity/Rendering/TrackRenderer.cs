using System.Linq;
using UnityEngine;
using AGXUnity.Utils;

namespace AGXUnity.Rendering
{
  [ExecuteInEditMode]
  public class TrackRenderer : ScriptComponent
  {
    [HideInInspector]
    public Models.Track Track
    {
      get
      {
        if ( m_track == null )
          m_track = GetComponent<Models.Track>();
        return m_track;
      }
    }

    protected override bool Initialize()
    {
      return true;
    }

    protected override void OnDisable()
    {
      if ( m_root != null )
        DestroyImmediate( m_root );
      m_root = null;
    }

    private void Update()
    {
      if ( Track == null )
        return;

      if ( m_root == null )
        m_root = RuntimeObjects.GetOrCreateRoot( this );

      if ( Track.Native == null ) {
        m_uninitializedTrackData.Update( Track );

        if ( m_uninitializedTrackData.TrackNodes.Length > m_root.transform.childCount ) {
          var numToAdd = m_uninitializedTrackData.TrackNodes.Length - m_root.transform.childCount;
          for ( int i = 0; i < numToAdd; ++i ) {
            var instance = PrefabLoader.Instantiate<GameObject>( @"Debug/BoxRenderer" );
            Configure( instance );
            m_root.AddChild( instance );
          }
        }
        else if ( m_uninitializedTrackData.TrackNodes.Length < m_root.transform.childCount ) {
          var numToRemove = m_root.transform.childCount - m_uninitializedTrackData.TrackNodes.Length;
          for ( int i = 0; i < numToRemove; ++i )
            DestroyImmediate( m_root.transform.GetChild( m_root.transform.childCount - 1 ).gameObject );
        }

        int nodeCounter = 0;
        var nodes = m_uninitializedTrackData.TrackNodes;
        foreach ( Transform child in m_root.transform ) {
          child.rotation   = nodes[ nodeCounter ].Rotation;
          child.position   = nodes[ nodeCounter ].Position + child.TransformDirection( nodes[ nodeCounter ].HalfExtents.z * Vector3.forward );
          child.localScale = 2.0f * nodes[ nodeCounter ].HalfExtents;
          ++nodeCounter;
        }
      }
      else {
        int numNodes = (int)Track.Native.getNumNodes();
        int numChildren = m_root.transform.childCount;
        if ( numNodes > numChildren ) {
          var numToAdd = numNodes - numChildren;
          for ( int i = 0; i < numToAdd; ++i ) {
            var instance = PrefabLoader.Instantiate<GameObject>( @"Debug/BoxRenderer" );
            Configure( instance );
            instance.hideFlags = HideFlags.DontSaveInEditor;
            m_root.AddChild( instance );
          }
          numChildren = numNodes;
        }
        else if ( numNodes < numChildren ) {
          var numToRemove = numChildren - numNodes;
          for ( int i = 0; i < numToRemove; ++i )
            DestroyImmediate( m_root.transform.GetChild( m_root.transform.childCount - 1 ).gameObject );
          numChildren = numNodes;
        }

        foreach ( var node in Track.Native.nodes() ) {
          var child = m_root.transform.GetChild( (int)node.getIndex() );
          child.rotation = node.getRigidBody().getRotation().ToHandedQuaternion();
          child.position = node.getBeginPosition().ToHandedVector3() + child.TransformDirection( 0.5f * (float)node.getLength() * Vector3.forward );
          child.localScale = 2.0f * node.getHalfExtents().ToVector3();
        }
      }
    }

    private void Reset()
    {
      if ( Track == null )
        Debug.LogError( "TrackRenderer requires Track component.", this );
    }

    private void Configure( GameObject instance )
    {
      instance.hideFlags = HideFlags.DontSaveInEditor;
      instance.GetOrCreateComponent<OnSelectionProxy>().Component = Track;
      foreach ( Transform child in instance.transform )
        child.gameObject.GetOrCreateComponent<OnSelectionProxy>().Component = Track;
    }

    struct TrackDesc
    {
      public int NumberOfNodes;
      public float Width;
      public float Thickness;
      public float InitialTensionDistance;
    }

    struct TrackWheelDesc
    {
      public Models.TrackWheelModel Model;
      public float Radius;
      public Vector3 Position;
      public Quaternion Rotation;
      public Vector3 LocalPosition;
      public Quaternion LocalRotation;

      public agxVehicle.TrackWheelDesc Native
      {
        get
        {
          return new agxVehicle.TrackWheelDesc( Models.TrackWheel.ToNative( Model ),
                                                Radius,
                                                new agx.AffineMatrix4x4( Rotation.ToHandedQuat(),
                                                                         Position.ToHandedVec3() ),
                                                new agx.AffineMatrix4x4( LocalRotation.ToHandedQuat(),
                                                                         LocalPosition.ToHandedVec3() ) );
        }
      }
    }

    struct TrackNodeDesc
    {
      public static TrackNodeDesc Create( agxVehicle.TrackNodeDesc nodeDesc )
      {
        return new TrackNodeDesc()
        {
          HalfExtents = nodeDesc.halfExtents.ToVector3(),
          Position = nodeDesc.transform.getTranslate().ToHandedVector3(),
          Rotation = nodeDesc.transform.getRotate().ToHandedQuaternion()
        };
      }

      public Vector3 HalfExtents;
      public Vector3 Position;
      public Quaternion Rotation;
    }

    class UninitializedTrackData
    {
      public TrackDesc Track = new TrackDesc();
      public TrackWheelDesc[] TrackWheels = new TrackWheelDesc[] { };
      public TrackNodeDesc[] TrackNodes = new TrackNodeDesc[] { };

      public void Update( Models.Track track )
      {
        var reqUpdate = TrackWheels.Length != track.Wheels.Length ||
                        Track.NumberOfNodes != track.NumberOfNodes ||
                       !Mathf.Approximately( Track.Width, track.Width ) ||
                       !Mathf.Approximately( Track.Thickness, track.Thickness ) ||
                       !Mathf.Approximately( Track.InitialTensionDistance, track.InitialTensionDistance );
        if ( !reqUpdate ) {
          for ( int i = 0; !reqUpdate && i < TrackWheels.Length; ++i ) {
            var trackWheelDef = TrackWheels[ i ];
            var trackWheel    = track.Wheels[ i ];
            reqUpdate         = trackWheelDef.Model != trackWheel.Model ||
                               !Mathf.Approximately( trackWheelDef.Radius, trackWheel.Radius ) ||
                                Vector3.SqrMagnitude( trackWheelDef.Position - trackWheel.transform.position ) > 1.0E-5f ||
                                Vector3.SqrMagnitude( trackWheelDef.LocalPosition - trackWheel.Frame.LocalPosition ) > 1.0E-5f ||
                                ( Quaternion.Inverse( trackWheelDef.Rotation ) * trackWheel.transform.rotation ).eulerAngles.sqrMagnitude > 1.0E-5f ||
                                ( Quaternion.Inverse( trackWheelDef.LocalRotation ) * trackWheel.Frame.LocalRotation ).eulerAngles.sqrMagnitude > 1.0E-5f;
          }
        }

        if ( !reqUpdate )
          return;

        Track = new TrackDesc()
        {
          NumberOfNodes          = track.NumberOfNodes,
          Width                  = track.Width,
          Thickness              = track.Thickness,
          InitialTensionDistance = track.InitialTensionDistance
        };
        TrackWheels = new TrackWheelDesc[ track.Wheels.Length ];
        for ( int i = 0; i < TrackWheels.Length; ++i ) {
          TrackWheels[ i ].Model         = track.Wheels[ i ].Model;
          TrackWheels[ i ].Radius        = track.Wheels[ i ].Radius;
          TrackWheels[ i ].Position      = track.Wheels[ i ].transform.position;
          TrackWheels[ i ].Rotation      = track.Wheels[ i ].transform.rotation;
          TrackWheels[ i ].LocalPosition = track.Wheels[ i ].Frame.LocalPosition;
          TrackWheels[ i ].LocalRotation = track.Wheels[ i ].Frame.LocalRotation;
        }

        var nodes = agxVehicle.agxVehicleSWIG.findTrackNodeConfiguration( new agxVehicle.TrackDesc( (ulong)Track.NumberOfNodes,
                                                                                                    Track.Width,
                                                                                                    Track.Thickness,
                                                                                                    Track.InitialTensionDistance ),
                                                                          new agxVehicle.TrackWheelDescVector( ( from wheelDef in TrackWheels select wheelDef.Native ).ToArray() ) );
        TrackNodes = ( from node in nodes select TrackNodeDesc.Create( node ) ).ToArray();
      }
    }

    private Models.Track m_track = null;
    private GameObject m_root = null;
    private UninitializedTrackData m_uninitializedTrackData = new UninitializedTrackData();
  }
}
