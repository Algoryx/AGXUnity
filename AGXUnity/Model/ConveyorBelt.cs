using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using AGXUnity.Utils;

using Object = UnityEngine.Object;

namespace AGXUnity.Model
{
  /// <summary>
  /// Conveyor belt model using one or several neighboring AGXUnity.Model.Track
  /// instances constrained to each other.
  /// </summary>
  [AddComponentMenu( "AGXUnity/Model/Conveyor Belt" )]
  [DoNotGenerateCustomEditor]
  [DisallowMultipleComponent]
  public class ConveyorBelt : ScriptComponent
  {
    [SerializeField]
    private int m_numberOfTracks = 3;

    /// <summary>
    /// Number of tracks in this belt.
    /// </summary>
    [ClampAboveZeroInInspector]
    [IgnoreSynchronization]
    public int NumberOfTracks
    {
      get { return m_numberOfTracks; }
      set
      {
        m_numberOfTracks = System.Math.Max( value, 1 );
        SynchronizeNumberOfTracks();
      }
    }

    [SerializeField]
    private int m_numberOfNodes = 64;

    /// <summary>
    /// Number of nodes of each track in this belt.
    /// </summary>
    [IgnoreSynchronization]
    [ClampAboveZeroInInspector]
    public int NumberOfNodes
    {
      get { return m_numberOfNodes; }
      set
      {
        if ( State == States.INITIALIZED ) {
          Debug.LogWarning( "Invalid to change number of nodes on an initialized belt.", this );
          return;
        }
        m_numberOfNodes = value;

        using ( BeginResourceRequests() ) {
          foreach ( var track in Tracks ) {
            AboutToChange( track );
            track.NumberOfNodes = NumberOfNodes;
          }
        }
      }
    }

    [SerializeField]
    private float m_thickness = 0.025f;

    /// <summary>
    /// Thickness of this belt.
    /// </summary>
    [IgnoreSynchronization]
    [ClampAboveZeroInInspector]
    public float Thickness
    {
      get { return m_thickness; }
      set
      {
        if ( State == States.INITIALIZED ) {
          Debug.LogWarning( "Invalid to change thickness of nodes on an initialized belt.", this );
          return;
        }
        m_thickness = value;

        using ( BeginResourceRequests() ) {
          foreach ( var track in Tracks ) {
            AboutToChange( track );
            track.Thickness = value;
          }
        }
      }
    }

    [SerializeField]
    private float m_width = 1.0f;

    /// <summary>
    /// Width of this belt.
    /// </summary>
    [IgnoreSynchronization]
    [ClampAboveZeroInInspector]
    public float Width
    {
      get { return m_width; }
      set
      {
        if ( State == States.INITIALIZED ) {
          Debug.LogWarning( "Invalid to change width of nodes on an initialized belt,", this );
          return;
        }
        m_width = value;

        SynchronizeTracksWidth();
      }
    }

    /// <summary>
    /// Width of each track, i.e., belt width = NumberOfTracks * TrackWidth.
    /// </summary>
    [ClampAboveZeroInInspector]
    public float TrackWidth
    {
      get { return Width / NumberOfTracks; }
      set { Width = value * NumberOfTracks; }
    }

    [SerializeField]
    private float m_initialTensionDistance = 1.0E-3f;

    /// <summary>
    /// Initial tension distance of this belt.
    /// </summary>
    [IgnoreSynchronization]
    public float InitialTensionDistance
    {
      get { return m_initialTensionDistance; }
      set
      {
        if ( State == States.INITIALIZED ) {
          Debug.LogWarning( "Invalid to change initial tension distance on an initialized belt.", this );
          return;
        }
        m_initialTensionDistance = value;

        using ( BeginResourceRequests() ) {
          foreach ( var track in Tracks ) {
            AboutToChange( track );
            track.InitialTensionDistance = InitialTensionDistance;
          }
        }
      }
    }

    [SerializeField]
    private int m_connectingConstraintStride = 4;

    /// <summary>
    /// Stride of the connecting constraints, resulting in
    /// NumberOfNodes / ConnectingConstraintStride constraints
    /// between neighboring tracks.
    /// </summary>
    /// <remarks>
    /// If NumberOfNodes % this value != 0, the stride will be
    /// decremented until the stride is an even multiple of
    /// NumberOfNodes.
    /// </remarks>
    [IgnoreSynchronization]
    [ClampAboveZeroInInspector]
    public int ConnectingConstraintStride
    {
      get { return m_connectingConstraintStride; }
      set
      {
        if ( State == States.INITIALIZED ) {
          Debug.LogWarning( "Invalid to change constraint stride on an initialized belt.", this );
          return;
        }
        m_connectingConstraintStride = value;
      }
    }

    [SerializeField]
    private ConveyorBeltProperties m_properties = null;

    /// <summary>
    /// Belt (specific) properties of this belt.
    /// </summary>
    [AllowRecursiveEditing]
    public ConveyorBeltProperties Properties
    {
      get { return m_properties; }
      set
      {
        if ( State == States.INITIALIZED && m_properties != null )
          m_properties.Unregister( this );

        m_properties = value;

        if ( State == States.INITIALIZED && m_properties != null )
          m_properties.Register( this );
      }
    }

    [SerializeField]
    private TrackProperties m_trackProperties = null;

    /// <summary>
    /// Track (specific) properties of this belt.
    /// </summary>
    [IgnoreSynchronization]
    [AllowRecursiveEditing]
    public TrackProperties TrackProperties
    {
      get { return m_trackProperties; }
      set
      {
        m_trackProperties = value;

        using ( BeginResourceRequests() ) {
          foreach ( var track in Tracks ) {
            AboutToChange( track );
            track.Properties = TrackProperties;
          }
        }
      }
    }

    [SerializeField]
    private TrackInternalMergeProperties m_internalMergeProperties = null;

    /// <summary>
    /// Internal merge properties of this belt.
    /// </summary>
    [AllowRecursiveEditing]
    public TrackInternalMergeProperties InternalMergeProperties
    {
      get { return m_internalMergeProperties; }
      set
      {
        m_internalMergeProperties = value;

        using ( BeginResourceRequests() ) {
          foreach ( var track in Tracks ) {
            AboutToChange( track );
            track.InternalMergeProperties = InternalMergeProperties;
          }
        }
      }
    }

    [SerializeField]
    private ShapeMaterial m_material = null;

    /// <summary>
    /// Shape material of this belt.
    /// </summary>
    [AllowRecursiveEditing]
    public ShapeMaterial Material
    {
      get { return m_material; }
      set
      {
        m_material = value;

        using ( BeginResourceRequests() ) {
          foreach ( var track in Tracks ) {
            AboutToChange( track );
            track.Material = Material;
          }
        }
      }
    }

    [SerializeField]
    private List<GameObject> m_rollers = new List<GameObject>();

    /// <summary>
    /// Rollers added to this belt.
    /// </summary>
    [HideInInspector]
    public GameObject[] Rollers
    {
      get { return m_rollers.ToArray(); }
    }

    /// <summary>
    /// Track instances in this belt.
    /// </summary>
    [HideInInspector]
    public Track[] Tracks
    {
      get
      {
        // TODO: Cache this.
        return GetComponents<Track>();
      }
    }

    /// <summary>
    /// Runtime connecting constraints, i.e., the constraints between nodes
    /// in neighboring tracks. ConnectingConstraints.GetLength( 0 ) == NumberOfTracks - 1
    /// and ConnectingConstraints.GetLength( 1 ) == NumberOfNodes / usedStride.
    /// Note that usedStride is the first even multiple less than or equal to NumberOfNodes.
    /// </summary>
    [HideInInspector]
    public agx.Constraint[,] ConnectingConstraints { get; private set; } = null;

    /// <summary>
    /// Add roller to this belt. All previously added TrackWheel components
    /// will be used in this belt. If one or more if the previously added
    /// TrackWheel components belongs to another track/belt, add is ignored
    /// (returning false with a log warning).
    /// </summary>
    /// <remarks>
    /// If <paramref name="roller"/> contains more TrackWheel components than
    /// tracks in this belt, the residual TrackWheel components will be destroyed. 
    /// </remarks>
    /// <param name="roller"></param>
    /// <returns></returns>
    public bool Add( GameObject roller )
    {
      if ( roller == null || m_rollers.Contains( roller ) )
        return false;

      var rb = roller.GetComponentInParent<RigidBody>();
      if ( rb == null )
        return false;

      // If any previously added TrackWheel component belongs to another
      // track/belt we cannot (right now) add this roller to this belt.
      var alreadyPresentTrackWheels = roller.GetComponents<TrackWheel>();
      if ( alreadyPresentTrackWheels.Length > 0 ) {
        // Don't think disabled tracks will appear here.
        var tracks = FindObjectsOfType<Track>();
        if ( tracks.Any( track => alreadyPresentTrackWheels.Any( wheel => track.Contains( wheel ) ) ) ) {
          Debug.LogWarning( $"Roller {roller.name} already has {roller.GetComponents<TrackWheel>().Length} TrackWheel components " +
                             "belonging to other belt/track instances." );
          return false;
        }
      }

      using ( BeginResourceRequests() ) {
        var rotationAxisOffset = 0.5f * ( Width - TrackWidth );
        for ( int i = 0; i < NumberOfTracks; ++i ) {
          var trackWheel = i < alreadyPresentTrackWheels.Length ?
                             alreadyPresentTrackWheels[ i ] :
                             AddComponent<TrackWheel>( roller );
          trackWheel.Configure( roller,
                                rotationAxisOffset,
                                (aName, aModel) =>
                                {
                                  return ( aModel == TrackWheelModel.Sprocket && aName.StartsWith( "drivtrumma" ) ) ||
                                         ( aModel == TrackWheelModel.Idler && aName.StartsWith( "vandtrumma" ) );
                                } );
          rotationAxisOffset -= TrackWidth;
          AboutToChange( Tracks[ i ] );
          Tracks[ i ].Add( trackWheel );
        }

        // Destroying leftover TrackWheel components.
        for ( int i = NumberOfTracks; i < alreadyPresentTrackWheels.Length; ++i )
          DestroyObjectImmediate( alreadyPresentTrackWheels[ i ] );

        m_rollers.Add( roller );
      }

      return true;
    }

    /// <summary>
    /// Remove roller from this belt. All TrackWheel components previously
    /// added to the <paramref name="roller"/> will be removed.
    /// </summary>
    /// <param name="roller">Roller to remove.</param>
    /// <returns>True if successfully removed, otherwise false (check console log).</returns>
    public bool Remove( GameObject roller )
    {
      if ( roller == null || !m_rollers.Contains( roller ) )
        return false;

      var trackWheels = roller.GetComponents<TrackWheel>();
      // This shouldn't happen when/if undo is working.
      if ( trackWheels.Length == 0 ) {
        Debug.LogWarning( $"Roller {roller.name} doesn't contain any TrackWheel instances." );
        return false;
      }

      using ( BeginResourceRequests() ) {
        foreach ( var trackWheel in trackWheels ) {
          var track = Tracks.FirstOrDefault( subject => subject.Contains( trackWheel ) );
          if ( track == null )
            Debug.Log( "TrackWheel wasn't removed from any track but will be destroyed.", trackWheel );
          else {
            AboutToChange( track );
            track.Remove( trackWheel );
          }
          DestroyObjectImmediate( trackWheel );
        }

        m_rollers.Remove( roller );
      }

      return true;
    }

    /// <summary>
    /// Finds if given roller has been added to this belt.
    /// </summary>
    /// <param name="roller">Roller to check.</param>
    /// <returns>True if this belt contains <paramref name="roller"/>, otherwise false.</returns>
    public bool Contains( GameObject roller )
    {
      return m_rollers.Contains( roller );
    }

    /// <summary>
    /// Types of requests the resource handler should support.
    /// "Optional" requests doesn't rely on the return value.
    /// "Required" requests must perform the request for the
    /// functionality to be preserved.
    /// </summary>
    public enum ResourceHandlerRequest
    {
      /// <summary>
      /// Optional: Begin of zero to many requests. <seealso cref="ResourceHandler"/>
      /// </summary>
      Begin,
      /// <summary>
      /// Required: Add component of given type to GameObject context. <seealso cref="ResourceHandler"/>
      /// </summary>
      AddComponent,
      /// <summary>
      /// Required: Destroy context immediately. <seealso cref="ResourceHandler"/>
      /// </summary>
      DestroyObject,
      /// <summary>
      /// Optional: Given context is about to be changed. <seealso cref="ResourceHandler"/>
      /// </summary>
      AboutToChange,
      /// <summary>
      /// Optional: End of requests.
      /// </summary>
      End
    }

    /// <summary>
    /// Resource request handler to manage undo.
    /// </summary>
    /// <example>
    /// Object MyResourceHandler( Belt.ResourceHandlerRequest request, Object context, Type type )
    /// {
    ///   if ( request == Belt.ResourceHandlerRequest.Begin )
    ///     return null; // E.g., Undo.SetCurrentGroupName
    ///   else if ( request == Belt.ResourceHandlerRequest.End )
    ///     return null; // E.g., Undo.CollapseUndoOperations
    ///   else if ( request == Belt.ResourceHandlerRequest.AboutToChange )
    ///     Undo.RecordObject( context, $"{context.name} changes." );
    ///   else if ( request == Belt.ResourceHandlerRequest.AddComponent )
    ///     return Undo.AddComponent( context as GameObject, type );
    ///   else if ( request == Belt.ResourceHandlerRequest.DestroyObject )
    ///     Undo.DestroyObjectImmediate( context );
    ///   return null;
    /// }
    /// </example>
    public Func<ResourceHandlerRequest, Object, Type, Object> ResourceHandler { get; set; } = null;

    /// <summary>
    /// Adding Track components given current NumberOfTracks when this component
    /// has been added to a game object. Note that this method is invoked automatically
    /// in the editor by the Belt Tool.
    /// </summary>
    /// <returns>This instance.</returns>
    public ConveyorBelt AddDefaultComponents()
    {
      SynchronizeNumberOfTracks();
      return this;
    }

    /// <summary>
    /// Synchronizing some values which are hard to catch (or are not defined)
    /// to support undo/redo.
    /// </summary>
    public void UndoRedoPerformed()
    {
      m_numberOfTracks = GetComponents<Track>().Length;
    }

    /// <summary>
    /// Removes deleted rollers from our list of rollers.
    /// </summary>
    public void RemoveInvalidRollers()
    {
      var numRemoved = m_rollers.RemoveAll( roller => roller == null );
      if ( numRemoved == 0 )
        return;

      // Remove TrackWheel instances from our tracks where the
      // parent of the frame is null (our deleted roller).
      foreach ( var roller in Rollers ) {
        var trackWheels = roller.GetComponents<TrackWheel>();
        foreach ( var trackWheel in trackWheels ) {
          if ( trackWheel.Frame.Parent != null )
            continue;
          var track = Tracks.FirstOrDefault( subject => subject.Contains( trackWheel ) );
          if ( track != null )
            track.Remove( trackWheel );
        }
      }
    }

    protected override bool Initialize()
    {
      RemoveInvalidRollers();

      var tracks = FindTracks();
      if ( tracks.Length == 0 ) {
        Debug.LogWarning( "Belt: No tracks found.", this );
        return false;
      }
      if ( tracks.Any( track => track.GetInitialized<Track>() == null ) ||
           // Tracks has zero nodes when the license isn't loaded or
           // doesn't include the tracks module.
           tracks.Any( Track => Track.Native.getNumNodes() == 0 ) ) {
        Debug.LogError( "Belt: One or more tracks failed to initialize.", this );
        return false;
      }

      m_tracks = tracks;
      if ( m_tracks.Length > 1 ) {
        ulong numNodes = m_tracks.First().Native.getNumNodes();
        ulong stride = System.Math.Min( System.Math.Max( (ulong)ConnectingConstraintStride, 1 ),
                                        numNodes );
        while ( stride > 1 && (numNodes % stride) != 0 )
          --stride;
        if ( (int)stride != ConnectingConstraintStride )
          Debug.Log( $"Belt: Connecting constraint stride changed from {ConnectingConstraintStride} to {stride} " +
                     $"to match node count: {m_tracks.First().Native.getNumNodes()}.", this );

        for ( int i = 0; i < m_tracks.Length; ++i )
          m_tracks[ i ].Native.addGroup( GetGroupName( i ) );

        ConnectingConstraints = new agx.Constraint[ m_tracks.Length - 1, (int)( numNodes / stride ) ];

        for ( int i = 1; i < m_tracks.Length; ++i ) {
          var prev = m_tracks[ i - 1 ];
          var curr = m_tracks[ i ];

          GetSimulation().getSpace().setEnablePair( GetGroupName( i - 1 ),
                                                    GetGroupName( i ),
                                                    false );
          var connectingConstraintIndex = 0;
          for ( ulong nodeIndex = 0; nodeIndex < prev.Native.getNumNodes(); ++nodeIndex ) {
            if ( ( nodeIndex % stride ) != 0 )
              continue;

            var prevTrackNode    = prev.Native.getNode( nodeIndex );
            var currTrackNode    = curr.Native.getNode( nodeIndex );
            var constraintCenter = 0.5 * ( prevTrackNode.getCenterPosition() +
                                           currTrackNode.getCenterPosition() );
            var constraintAxis   = prevTrackNode.getDirection().normal();
            var otherAxis        = ( constraintCenter - prevTrackNode.getCenterPosition() ).normal();

            // Constraint frame we're about to create:
            //   x or TRANSLATIONAL_1: Along otherAxis, i.e., separation of the two nodes.
            //   y or TRANSLATIONAL_2: Up/down between the two nodes.
            //   z or TRANSLATIONAL_3: Along the track.

            var prevTrackNodeFrame = new agx.Frame();
            var currTrackNodeFrame = new agx.Frame();
            agx.Constraint.calculateFramesFromWorld( constraintCenter,
                                                     constraintAxis,
                                                     otherAxis,
                                                     prevTrackNode.getRigidBody(),
                                                     prevTrackNodeFrame,
                                                     currTrackNode.getRigidBody(),
                                                     currTrackNodeFrame );
            ConnectingConstraints[ i - 1, connectingConstraintIndex++ ] = new agx.Hinge( prevTrackNode.getRigidBody(),
                                                                                         prevTrackNodeFrame,
                                                                                         currTrackNode.getRigidBody(),
                                                                                         currTrackNodeFrame );
          }
        }

        foreach ( var constraint in ConnectingConstraints ) {
          if ( constraint == null ) {
            Debug.LogWarning( "Unexpected null connecting constraint.", this );
            continue;
          }

          constraint.setName( "connecting" );

          // Properties will be applied during BeltProperties.Register
          // when all properties are synchronized for this belt after
          // Initialize is done.
          GetSimulation().add( constraint );
        }
      }

      return true;
    }

    protected override void OnDestroy()
    {
      if ( ConnectingConstraints != null && Simulation.HasInstance ) {
        foreach ( var constraint in ConnectingConstraints )
          GetSimulation().remove( constraint );
      }
      if ( Properties != null )
        Properties.Unregister( this );

      m_tracks = null;

      base.OnDestroy();
    }

    private void SynchronizeNumberOfTracks()
    {
      var tracks = GetComponents<Track>();
      if ( tracks.Length < NumberOfTracks ) {
        using ( BeginResourceRequests() ) {
          for ( int i = tracks.Length; i < NumberOfTracks; ++i ) {
            var track = AddComponent<Track>( gameObject );
            // Track will copy/move all properties of other Track
            // instances on the game object. We only have to make
            // sure the first is property configured.
            if ( i == 0 ) {
              track.Width                   = TrackWidth;
              track.Thickness               = Thickness;
              track.NumberOfNodes           = NumberOfNodes;
              track.InitialTensionDistance  = InitialTensionDistance;
              track.Properties              = TrackProperties;
              track.InternalMergeProperties = InternalMergeProperties;
              track.Material                = Material;

              if ( GetComponent<Rendering.TrackRenderer>() == null )
                AddComponent<Rendering.TrackRenderer>( gameObject );
            }
            track.hideFlags = HideFlags.HideInInspector;

            foreach ( var roller in Rollers ) {
              var trackWheel = AddComponent<TrackWheel>( roller ).Configure( roller );
              AboutToChange( track );
              track.Add( trackWheel );
            }
          }
          SynchronizeTracksWidth();
        }
      }
      else if ( tracks.Length > NumberOfTracks ) {
        using ( BeginResourceRequests() ) {
          for ( int i = NumberOfTracks; i < tracks.Length; ++i ) {
            var removedTrackWheels = ( from roller in Rollers
                                       from trackWheel in roller.GetComponents<TrackWheel>()
                                       where tracks[ i ].Remove( trackWheel )
                                       select trackWheel ).ToArray();
            foreach ( var removedTrackWheel in removedTrackWheels )
              DestroyObjectImmediate( removedTrackWheel );
            DestroyObjectImmediate( tracks[ i ] );
          }
          SynchronizeTracksWidth();
        }
      }
    }

    private void SynchronizeTracksWidth()
    {
      using ( BeginResourceRequests() ) {
        foreach ( var track in Tracks ) {
          AboutToChange( track );
          track.Width = TrackWidth;
        }
        SynchronizeWheelsPositions();
      }
    }

    private void SynchronizeWheelsPositions()
    {
      using ( BeginResourceRequests() ) {
        var rotationAxisOffset = 0.5f * ( Width - TrackWidth );
        foreach ( var track in Tracks ) {
          foreach ( var trackWheel in track.Wheels ) {
            AboutToChange( trackWheel );
            trackWheel.Frame.LocalPosition = rotationAxisOffset * Vector3.up;
          }
          rotationAxisOffset -= TrackWidth;
        }
      }
    }

    private ResourceRequestsScope BeginResourceRequests()
    {
      // Nested call to BeginResourceRequest, we still want to
      // group these actions in the current undo group.
      if ( m_currentResourceRequestScope != null )
        return null;

      ResourceHandler?.Invoke( ResourceHandlerRequest.Begin, null, null );
      m_currentResourceRequestScope = new ResourceRequestsScope( this );
      return m_currentResourceRequestScope;
    }

    private void EndResourceRequests()
    {
      m_currentResourceRequestScope = null;
      ResourceHandler?.Invoke( ResourceHandlerRequest.End, null, null );
    }

    private T AddComponent<T>( GameObject context )
      where T : Object
    {
      if ( ResourceHandler != null )
        return ResourceHandler.Invoke( ResourceHandlerRequest.AddComponent,
                                       context,
                                       typeof( T ) ) as T;
      return context.AddComponent( typeof( T ) ) as T;
    }

    private void DestroyObjectImmediate( Object objectToDestroy )
    {
      if ( ResourceHandler != null )
        ResourceHandler.Invoke( ResourceHandlerRequest.DestroyObject, objectToDestroy, null );
      else
        DestroyImmediate( objectToDestroy );
    }

    private void AboutToChange( Object objectToBeChanged )
    {
      ResourceHandler?.Invoke( ResourceHandlerRequest.AboutToChange, objectToBeChanged, null );
    }

    private class ResourceRequestsScope : IDisposable
    {
      public ResourceRequestsScope( ConveyorBelt belt )
      {
        m_belt = belt;
      }

      public void Dispose()
      {
        m_belt?.EndResourceRequests();
      }

      private ConveyorBelt m_belt = null;
    }

    private Track[] FindTracks()
    {
      return ( from track in GetComponents<Track>()
               let wheel = track.Wheels.FirstOrDefault()
               where wheel != null && wheel.Frame.Parent != null
               orderby wheel.Frame.CalculateLocalPosition( wheel.RigidBody.gameObject ).y
               select track ).ToArray();
    }

    private string GetGroupName( int trackIndex )
    {
      return $"{name}_track_{trackIndex}";
    }

    private void OnDrawGizmosSelected()
    {
      if ( m_nodeGizmoMesh == null )
        m_nodeGizmoMesh = Resources.Load<GameObject>( "Debug/BoxRenderer" ).GetComponent<MeshFilter>().sharedMesh;
      var initialized = m_tracks != null && m_tracks.Length > 0;
      var tracks      = initialized ? m_tracks : FindTracks();
      var color       = Color.black;
      foreach ( var track in tracks ) {
        Gizmos.color = color;

        if ( initialized ) {
          foreach ( var node in track.Native.nodes() ) {
            Gizmos.DrawWireMesh( m_nodeGizmoMesh,
                                 node.getRigidBody().getPosition().ToHandedVector3() +
                                 node.getRigidBody().getFrame().transformVectorToWorld( 0, 0, 0.5 * node.getLength() ).ToHandedVector3(),
                                 node.getRigidBody().getRotation().ToHandedQuaternion(),
                                 2.0f * node.getHalfExtents().ToVector3() );
          }
        }
        else {
          var renderer = track.GetComponent<Rendering.TrackRenderer>();
          if ( renderer == null )
            continue;

          if ( !renderer.DrawGizmosUninitialized( track, color ) )
            continue;
        }
        color = new Color( color.r + 1.0f / tracks.Length,
                           color.g + 1.0f / tracks.Length,
                           color.b + 1.0f / tracks.Length,
                           1.0f );
      }

      if ( ConnectingConstraints != null ) {
        Gizmos.color = new Color( 243.0f / 255.0f,
                                  139.0f / 255.0f,
                                  0.0f );
        foreach ( var constraint in ConnectingConstraints )
          Gizmos.DrawWireSphere( constraint.getAttachment( (ulong)0 ).get( agx.Attachment.Transformed.ANCHOR_POS ).ToHandedVector3(),
                                 0.025f );
      }
    }

    [NonSerialized]
    private Track[] m_tracks = null;
    [NonSerialized]
    private Mesh m_nodeGizmoMesh = null;
    [NonSerialized]
    private ResourceRequestsScope m_currentResourceRequestScope = null;
  }
}
