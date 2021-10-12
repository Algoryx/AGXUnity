using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using AGXUnity.Utils;

namespace AGXUnity.Rendering
{
  [AddComponentMenu( "AGXUnity/Rendering/Track Renderer" )]
  [ExecuteInEditMode]
  [DisallowMultipleComponent]
  public class TrackRenderer : ScriptComponent
  {
    [HideInInspector]
    public Model.Track[] Tracks
    {
      get
      {
        if ( m_tracks.Length == 0 )
          m_tracks = GetComponents<Model.Track>();
        return m_tracks;
      }
    }

    [SerializeField]
    public bool AutomaticScaling = true;


    [SerializeField]
    private GameObject m_resource = null;

    [IgnoreSynchronization]
    public GameObject Resource
    {
      get
      {
        if ( m_resource == null )
          m_resource = Resources.Load<GameObject>( @"Debug/BoxRenderer" );
        return m_resource;
      }
      set
      {
        m_resource = value;
        m_gizmosMesh = null;
        if ( m_root != null ) {
          DestroyImmediate( m_root );
          m_root = null;
        }
      }
    }

    public void OnTrackReset()
    {
      m_tracks = GetComponents<Model.Track>();
    }

    public UninitializedTrackData GetData( Model.Track track )
    {
      if ( m_uninitializedTrackData.TryGetValue( track, out var data ) )
        return data;
      return null;
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
      OnTrackReset();

      if ( Tracks.Length == 0 ) {
        m_uninitializedTrackData.Clear();
        return;
      }

      var containsNullEntries = m_uninitializedTrackData.FirstOrDefault( pair => pair.Key == null ).Value != null;
      if ( containsNullEntries )
        m_uninitializedTrackData = m_uninitializedTrackData.Where( pair => pair.Key != null ).ToDictionary( pair => pair.Key,
                                                                                                            pair => pair.Value );

      var numNodes = 0;
      foreach ( var track in Tracks ) {
        track.RemoveInvalidWheels();

        if ( !track.isActiveAndEnabled )
          continue;

        if ( track.Native != null )
          numNodes += (int)track.Native.getNumNodes();
        else {
          if ( !m_uninitializedTrackData.ContainsKey( track ) )
            m_uninitializedTrackData.Add( track, new UninitializedTrackData() );
          var trackData = m_uninitializedTrackData[ track ];
          trackData.Update( track );
          numNodes += trackData.TrackNodes.Length;
        }
      }

      // Avoid creating root in the prefab stage. We have to
      // do the rendering as gizmos.
      if ( PrefabUtils.IsPartOfEditingPrefab( gameObject ) )
        return;

      if ( m_root == null )
        m_root = RuntimeObjects.GetOrCreateRoot( this );

      if ( numNodes > m_root.transform.childCount ) {
        var numToAdd = numNodes - m_root.transform.childCount;
        // If we're rendering several tracks it doesn't matter (I think)
        // which of them that receives the OnSelectionProxy.
        var refTrack = Tracks[ 0 ];
        for ( int i = 0; i < numToAdd; ++i ) {
          var instance = Instantiate( Resource );
          Configure( refTrack, instance );
          m_root.AddChild( instance );
        }
      }
      else if ( numNodes < m_root.transform.childCount ) {
        var numToRemove = m_root.transform.childCount - numNodes;
        for ( int i = 0; i < numToRemove; ++i )
          DestroyImmediate( m_root.transform.GetChild( m_root.transform.childCount - 1 ).gameObject );
      }

      var nodeCounter = 0;
      foreach ( var track in Tracks ) {
        if ( !track.isActiveAndEnabled )
          continue;

        if ( track.Native != null ) {
          foreach ( var node in track.Native.nodes() ) {
            var renderInstance = m_root.transform.GetChild( nodeCounter++ );
            renderInstance.rotation = node.getRigidBody().getRotation().ToHandedQuaternion();
            renderInstance.position = node.getBeginPosition().ToHandedVector3() +
                                      renderInstance.TransformDirection( 0.5f * (float)node.getLength() * Vector3.forward );
            if (AutomaticScaling)
              renderInstance.localScale = 2.0f * node.getHalfExtents().ToVector3();
            else
              renderInstance.localScale = new Vector3(1, 1, 1);
          }
        }
        else {
          var nodes = m_uninitializedTrackData[ track ].TrackNodes;
          foreach ( var node in nodes ) {
            var renderInstance = m_root.transform.GetChild( nodeCounter++ );
            renderInstance.rotation = node.Rotation;
            renderInstance.position = node.Position +
                                      renderInstance.TransformDirection( node.HalfExtents.z * Vector3.forward );
            if ( AutomaticScaling )
              renderInstance.localScale = 2.0f * node.HalfExtents;
            else
              renderInstance.localScale = Vector3.one;
          }
        }
      }
    }

    private void Reset()
    {
      if ( Tracks.Length == 0 )
        Debug.LogError( "TrackRenderer requires Track component.", this );
    }

    private void OnDrawGizmos()
    {
      DrawGizmos( false );
    }

    private void OnDrawGizmosSelected()
    {
      DrawGizmos( true );
    }

    private void Configure( Model.Track track, GameObject instance )
    {
      instance.hideFlags = HideFlags.DontSaveInEditor;
      instance.GetOrCreateComponent<OnSelectionProxy>().Component = track;
      foreach ( Transform child in instance.transform )
        Configure( track, child.gameObject );
    }

    private void DrawGizmos( bool isSelected )
    {
      if ( Application.isPlaying )
        return;

      if ( !PrefabUtils.IsPartOfEditingPrefab( gameObject ) )
        return;

      // The conveyor belt has some rendering when selected.
      if ( GetComponent<Model.ConveyorBelt>() != null )
        return;

      var defaultColor = Color.Lerp( Color.black, Color.red, 0.15f );
      var selectedColor = Color.Lerp( defaultColor, Color.white, 0.15f );
      foreach ( var track in Tracks )
        DrawGizmosUninitialized( track, isSelected ? selectedColor : defaultColor );
    }

    public bool DrawGizmosUninitialized( Model.Track track, Color color )
    {
      if ( track == null )
        return false;

      var data = GetData( track );
      if ( data == null )
        return false;

      if ( m_gizmosMesh == null ) {
        if ( Resource == null )
          return false;

        var filters = Resource.GetComponentsInChildren<MeshFilter>();
        if ( filters.Length == 0 )
          return false;

        var combines = new CombineInstance[ filters.Length ];
        for ( int i = 0; i < combines.Length; ++i ) {
          combines[ i ].mesh = filters[ i ].sharedMesh;
          combines[ i ].transform = filters[ i ].transform.localToWorldMatrix;
        }

        m_gizmosMesh = new Mesh();
        m_gizmosMesh.CombineMeshes( combines );
      }

      Gizmos.color = color;
      foreach ( var node in data.TrackNodes ) {
        Gizmos.DrawWireMesh( m_gizmosMesh,
                             node.Position + node.Rotation * ( node.HalfExtents.z * Vector3.forward ),
                             node.Rotation,
                             2.0f * node.HalfExtents );
      }

      return true;
    }

    [System.NonSerialized]
    private Mesh m_gizmosMesh = null;

    public struct TrackDesc
    {
      public int NumberOfNodes;
      public float Width;
      public float Thickness;
      public float InitialTensionDistance;
    }

    public struct TrackWheelDesc
    {
      public Model.TrackWheelModel WheelModel;
      public float Radius;
      public Vector3 Position;
      public Quaternion Rotation;
      public Vector3 LocalPosition;
      public Quaternion LocalRotation;

      public agxVehicle.TrackWheelDesc Native
      {
        get
        {
          return new agxVehicle.TrackWheelDesc( Model.TrackWheel.ToNative( WheelModel ),
                                                Radius,
                                                new agx.AffineMatrix4x4( Rotation.ToHandedQuat(),
                                                                         Position.ToHandedVec3() ),
                                                new agx.AffineMatrix4x4( LocalRotation.ToHandedQuat(),
                                                                         LocalPosition.ToHandedVec3() ) );
        }
      }
    }

    public struct TrackNodeDesc
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

    public class UninitializedTrackData
    {
      public TrackDesc Track = new TrackDesc();
      public TrackWheelDesc[] TrackWheels = new TrackWheelDesc[] { };
      public TrackNodeDesc[] TrackNodes = new TrackNodeDesc[] { };

      public void Update( Model.Track track )
      {
        var reqUpdate = TrackWheels.Length != track.Wheels.Length ||
                        Track.NumberOfNodes != track.NumberOfNodes ||
                       !Math.Approximately( Track.Width, track.Width ) ||
                       !Math.Approximately( Track.Thickness, track.Thickness ) ||
                       !Math.Approximately( Track.InitialTensionDistance, track.InitialTensionDistance );
        if ( !reqUpdate ) {
          for ( int i = 0; !reqUpdate && i < TrackWheels.Length; ++i ) {
            var trackWheelDef = TrackWheels[ i ];
            var trackWheel    = track.Wheels[ i ];
            reqUpdate         = trackWheelDef.WheelModel != trackWheel.Model ||
                               !Math.Approximately( trackWheelDef.Radius, trackWheel.Radius ) ||
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
          TrackWheels[ i ].WheelModel    = track.Wheels[ i ].Model;
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

    private Model.Track[] m_tracks = new Model.Track[] { };
    private GameObject m_root = null;
    private Dictionary<Model.Track, UninitializedTrackData> m_uninitializedTrackData = new Dictionary<Model.Track, UninitializedTrackData>();
  }
}
