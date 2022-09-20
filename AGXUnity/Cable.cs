using System;
using System.Collections.Generic;
using UnityEngine;
using AGXUnity.Utils;

namespace AGXUnity
{
  [RequireComponent( typeof( CableRoute ) )]
  public class Cable : ScriptComponent
  {
    /// <summary>
    /// Cable node types.
    /// </summary>
    public enum NodeType
    {
      BodyFixedNode,
      FreeNode
    }

    public enum RouteType
    {
      /// <summary>
      /// The added route nodes is the actual route.
      /// </summary>
      Identity,
      /// <summary>
      /// The route will try to fulfill the given route as good as possible
      /// given the resolution per unit length.
      /// </summary>
      Segmenting
    }

    /// <summary>
    /// Native instance of the cable.
    /// </summary>
    public agxCable.Cable Native { get; private set; }

    /// <summary>
    /// Radius of this cable - default 0.05. Paired with property Radius.
    /// </summary>
    [SerializeField]
    private float m_radius = 0.05f;

    /// <summary>
    /// Get or set radius of this cable - default 0.05.
    /// </summary>
    [ClampAboveZeroInInspector]
    public float Radius
    {
      get { return m_radius; }
      set
      {
        m_radius = value;
      }
    }

    /// <summary>
    /// Convenience property for diameter of this cable.
    /// </summary>
    [ClampAboveZeroInInspector]
    public float Diameter
    {
      get { return 2.0f * Radius; }
      set { Radius = 0.5f * value; }
    }

    /// <summary>
    /// Resolution of this cable - default 5. Paired with property ResolutionPerUnitLength.
    /// </summary>
    [SerializeField]
    private float m_resolutionPerUnitLength = 5f;

    /// <summary>
    /// Get or set resolution of this cable. Default 5.
    /// </summary>
    [ClampAboveZeroInInspector( true )]
    public float ResolutionPerUnitLength
    {
      get { return m_resolutionPerUnitLength; }
      set
      {
        m_resolutionPerUnitLength = value;
      }
    }

    /// <summary>
    /// Linear velocity damping of this cable.
    /// </summary>
    [SerializeField]
    private float m_linearVelocityDamping = 0.0f;

    /// <summary>
    /// Get or set linear velocity damping of this cable. Default 0.0.
    /// </summary>
    [ClampAboveZeroInInspector( true )]
    public float LinearVelocityDamping
    {
      get { return m_linearVelocityDamping; }
      set
      {
        m_linearVelocityDamping = value;
        if ( Native != null )
          Native.setLinearVelocityDamping( new agx.Vec3( m_linearVelocityDamping, m_linearVelocityDamping, 0f ) );
      }
    }

    /// <summary>
    /// Angular velocity damping of this cable.
    /// </summary>
    [SerializeField]
    private float m_angularVelocityDamping = 0.0f;

    /// <summary>
    /// Get or set angular velocity damping of this cable. Default 0.0.
    /// </summary>
    [ClampAboveZeroInInspector( true )]
    public float AngularVelocityDamping
    {
      get { return m_angularVelocityDamping; }
      set
      {
        m_angularVelocityDamping = value;
        if ( Native != null )
          Native.setAngularVelocityDamping( new agx.Vec3( m_angularVelocityDamping, m_angularVelocityDamping, 0f ) );
      }
    }

    /// <summary>
    /// Shape material of this cable. Default null.
    /// </summary>
    [SerializeField]
    private ShapeMaterial m_material = null;

    /// <summary>
    /// Get or set shape material of this cable. Default null.
    /// </summary>
    [AllowRecursiveEditing]
    public ShapeMaterial Material
    {
      get { return m_material; }
      set
      {
        m_material = value;
        if ( Native != null ) {
          if ( m_material != null && m_material.Native == null )
            m_material.GetInitialized<ShapeMaterial>();

          if ( m_material != null )
            Native.setMaterial( m_material.Native );
          else {
            var currMaterial = Native.getMaterial();
            var currIsDefault = currMaterial != null &&
                                currMaterial.getName() == "DefaultCableMaterial";
                                Utils.Math.Approximately( (float)currMaterial.getBulkMaterial().getDensity(), 700.0f );
            if ( currMaterial == null || !currIsDefault ) {
              var defaultMaterial = new agx.Material( "DefaultCableMaterial" );
              defaultMaterial.getBulkMaterial().setDensity( 700.0 );
              defaultMaterial.getBulkMaterial().setYoungsModulus( 5.0E10 );
              Native.setMaterial( defaultMaterial );
            }
          }
        }
      }
    }

    /// <summary>
    /// Cable properties.
    /// </summary>
    [SerializeField]
    private CableProperties m_properties = null;

    /// <summary>
    /// Get cable bulk properties instance.
    /// </summary>
    [AllowRecursiveEditing]
    [IgnoreSynchronization]
    public CableProperties Properties
    {
      get { return m_properties; }
      set
      {
        m_properties = value;
        SynchronizeProperties();
      }
    }

    [SerializeField]
    private RouteType m_routeAlgorithm = RouteType.Segmenting;
    /// <summary>
    /// Route algorithm used by restored cables.
    /// </summary>
    [HideInInspector]
    public RouteType RouteAlgorithm
    {
      get { return m_routeAlgorithm; }
      set { m_routeAlgorithm = value; }
    }

    private CableRoute m_routeComponent = null;
    /// <summary>
    /// Get route to initialize this cable.
    /// </summary>
    [HideInInspector]
    public CableRoute Route
    {
      get
      {
        if ( m_routeComponent == null )
          m_routeComponent = GetComponent<CableRoute>();
        return m_routeComponent;
      }      
    }

    private PointCurve m_routePointCurve              = null;
    private float m_routePointResulutionPerUnitLength = -1.0f;
    private Vector3[] m_routePointsCache              = new Vector3[] { };

    /// <summary>
    /// Route point data:  o-----------x-----------x-----------------o
    ///                 CurrNode   CurrPoint   NextPoint          NextNode
    ///                        Position/Rotation
    /// </summary>
    public struct RoutePointData
    {
      /// <summary>
      /// Begin route node in current segment.
      /// </summary>
      public CableRouteNode CurrNode;

      /// <summary>
      /// End route node in current segment.
      /// </summary>
      public CableRouteNode NextNode;

      /// <summary>
      /// Position of the node.
      /// </summary>
      public Vector3 Position;

      /// <summary>
      /// Rotation of the node.
      /// </summary>
      public Quaternion Rotation;

      /// <summary>
      /// Current segment point.
      /// </summary>
      public PointCurve.SegmentPoint CurrPoint;

      /// <summary>
      /// Next segment point.
      /// </summary>
      public PointCurve.SegmentPoint NextPoint;

      /// <summary>
      /// Segment type.
      /// </summary>
      public PointCurve.SegmentType SegmentType;
    }

    /// <summary>
    /// Checks if route point curve is up to date.
    /// </summary>
    [HideInInspector]
    public bool RoutePointCurveUpToDate
    {
      get
      {
        return m_routePointCurve != null &&
               Utils.Math.Approximately( m_routePointResulutionPerUnitLength, ResolutionPerUnitLength ) &&
               Route.IsSynchronized( m_routePointCurve, 1.0E-4f );
      }
    }

    /// <summary>
    /// Traverse route points given current route nodes.
    /// </summary>
    /// <param name="callback">Callback for each route point.</param>
    /// <returns>True if point route is successful - otherwise false.</returns>
    public bool TraverseRoutePoints( Action<RoutePointData> callback )
    {
      if ( callback == null )
        return false;

      var result = SynchronizeRoutePointCurve();
      if ( !result.Successful )
        return false;

      m_routePointCurve.Traverse( ( curr, next, type ) =>
      {
        var routePointData = new RoutePointData()
        {
          CurrPoint = curr,
          NextPoint = next,
          SegmentType = type
        };

        var currIndex = m_routePointCurve.FindIndex( curr.Time );
        var nextIndex = currIndex + 1;
        routePointData.CurrNode = Route[ currIndex ];
        routePointData.NextNode = Route[ nextIndex ];

        var currRotation = routePointData.CurrNode.Rotation;
        var nextRotation = routePointData.NextNode.Rotation;
        var dirToNext    = ( next.Point - curr.Point ) / Vector3.Distance( curr.Point, next.Point );

        // Naive from-to rotation to dir.
        routePointData.Rotation = Quaternion.FromToRotation( Vector3.forward, dirToNext );
        var nodeX               = routePointData.Rotation * Vector3.right;
        var nodeY               = routePointData.Rotation * Vector3.up;

        // Current and next route node x and y axes in the 'dirToNext' plane.
        var currInPlaneX = Vector3.Normalize( Vector3.ProjectOnPlane( currRotation * Vector3.right, dirToNext ) );
        var currInPlaneY = Vector3.Normalize( Vector3.ProjectOnPlane( currRotation * Vector3.up, dirToNext ) );
        var nextInPlaneX = Vector3.Normalize( Vector3.ProjectOnPlane( nextRotation * Vector3.right, dirToNext ) );
        //var nextInPlaneY = Vector3.Normalize( Vector3.ProjectOnPlane( nextRotation * Vector3.up, dirToNext ) );

        // Rotating from current rotation (naive rotate from forward to dir) to
        // curr route node rotation.
        var twistToCurr         = Utils.Math.SignedAngle( nodeX, currInPlaneX, nodeY );
        routePointData.Rotation = Quaternion.AngleAxis( twistToCurr, dirToNext ) * routePointData.Rotation;

        // Calculating rotation angle from curr node to next node (note: not points!),
        // and lerp the current twist given local time of the current point.
        var twistCurrToNext     = Utils.Math.SignedAngle( currInPlaneX, nextInPlaneX, currInPlaneY );
        var lerpedTwistAngle    = Mathf.LerpAngle( 0.0f, twistCurrToNext, curr.LocalTime );

        routePointData.Rotation = Quaternion.AngleAxis( lerpedTwistAngle, dirToNext ) * routePointData.Rotation;
        routePointData.Position = curr.Point;

        callback( routePointData );
      }, result.SegmentLength );


      return true;
    }

    /// <summary>
    /// Calculates route points given current route nodes and resolution.
    /// </summary>
    /// <returns>Array of, equally distant, points defining the cable route.</returns>
    public Vector3[] GetRoutePoints()
    {
      if ( m_routePointsCache == null )
        m_routePointsCache = new Vector3[] { };

      if ( m_routePointsCache.Length == 0 && Route.NumNodes > 1 )
        SynchronizeRoutePointCurve();

      return m_routePointsCache;
    }

    public void RestoreLocalDataFrom( agxCable.Cable native )
    {
      if ( native == null )
        return;

      Radius                  = Convert.ToSingle( native.getRadius() );
      ResolutionPerUnitLength = Convert.ToSingle( native.getResolution() );
      LinearVelocityDamping   = Convert.ToSingle( native.getLinearVelocityDamping().maxComponent() );
      AngularVelocityDamping  = Convert.ToSingle( native.getAngularVelocityDamping().maxComponent() );
    }

    protected override void OnEnable()
    {
      if ( Native != null && Simulation.HasInstance )
        GetSimulation().add( Native );
    }

    protected override bool Initialize()
    {
      if ( !LicenseManager.LicenseInfo.HasModuleLogError( LicenseInfo.Module.AGXCable, this ) )
        return false;

      try {
        if ( Route.NumNodes < 2 )
          throw new Exception( $"{GetType().FullName} ERROR: Invalid number of nodes. Minimum number of route nodes is two." );

        agxCable.Cable cable = null;
        if ( RouteAlgorithm == RouteType.Segmenting ) {
          var result = SynchronizeRoutePointCurve();
          if ( !result.Successful )
            throw new Exception( $"{GetType().FullName} ERROR: Invalid cable route. Unable to initialize cable with " +
                                 $" {Route.NumNodes} nodes and resolution/length = {ResolutionPerUnitLength}." );

          cable = CreateNative( result.NumSegments / Route.TotalLength );

          var handledNodes = new HashSet<CableRouteNode>();
          var success = TraverseRoutePoints( routePointData =>
          {
            var routeNode = CableRouteNode.Create( NodeType.FreeNode, routePointData.CurrNode.Parent );
            routeNode.Position = routePointData.Position;
            routeNode.Rotation = routePointData.Rotation;

            var attachmentNode = routePointData.SegmentType == PointCurve.SegmentType.First && routePointData.CurrNode.Type != NodeType.FreeNode ?
                                   routePointData.CurrNode :
                                 routePointData.SegmentType == PointCurve.SegmentType.Last && routePointData.NextNode.Type != NodeType.FreeNode ?
                                   routePointData.NextNode :
                                 routePointData.SegmentType == PointCurve.SegmentType.Intermediate && routePointData.CurrNode.Type != NodeType.FreeNode ?
                                    routePointData.CurrNode :
                                    null;

            if ( attachmentNode != null && !handledNodes.Contains( attachmentNode ) ) {
              handledNodes.Add( attachmentNode );
              routeNode.Add( CableAttachment.AttachmentType.Rigid,
                             attachmentNode.Parent,
                             attachmentNode.LocalPosition,
                             attachmentNode.LocalRotation );
            }

            if ( !cable.add( routeNode.GetInitialized<CableRouteNode>().Native ) )
              throw new Exception( $"{GetType().FullName} ERROR: Unable to add node to cable." );
          } );

          if ( !success )
            throw new Exception( $"{GetType().FullName} ERROR: Invalid route - unable to find segment length given resolution/length = {ResolutionPerUnitLength}." );
        }
        else {
          cable = CreateNative( ResolutionPerUnitLength );
          foreach ( var node in Route ) {
            if ( !cable.add( node.GetInitialized<CableRouteNode>().Native ) )
              throw new Exception( $"{GetType().FullName} ERROR: Unable to add node to cable." );
          }
        }

        cable.setName( name );

        // Adding the cable to the simulation independent of if this
        // cable is enabled or not, only to initialize it. If this
        // component/game object is disabled, remove it later.
        GetSimulation().add( cable );
        if ( cable.getInitializationReport().getNumSegments() == 0 )
          throw new Exception( $"{GetType().FullName} ERROR: Initialization failed. Check route and/or resolution." );

        Native = cable;
      }
      catch ( Exception e ) {
        Debug.LogException( e, this );

        return false;
      }

      // Remove if this cable is inactive/disabled (the cable has been added above).
      if ( !isActiveAndEnabled )
        GetSimulation().remove( Native );

      if ( Properties != null )
        Properties.GetInitialized<CableProperties>();

      SynchronizeProperties();

      return true;
    }

    protected override void OnDisable()
    {
      if ( Native != null && Simulation.HasInstance )
        GetSimulation().remove( Native );
    }

    protected override void OnDestroy()
    {
      if ( Simulation.HasInstance )
        GetSimulation().remove( Native );

      Native = null;

      base.OnDestroy();
    }

    private void Reset()
    {
      Route.Clear();
    }

    private agxCable.Cable CreateNative( float resolutionPerUnitLength )
    {
      var native = new agxCable.Cable( Radius, new agxCable.IdentityRoute( resolutionPerUnitLength ) );
      native.addComponent( new agxCable.CablePlasticity() );
      native.getCablePlasticity().setYieldPoint( double.PositiveInfinity, agxCable.Direction.ALL_DIRECTIONS );

      return native;
    }

    private void SynchronizeProperties()
    {
      if ( Properties == null )
        return;

      if ( !Properties.IsListening( this ) )
        Properties.OnPropertyUpdated += OnPropertyValueUpdate;

      foreach ( CableProperties.Direction dir in CableProperties.Directions )
        OnPropertyValueUpdate( dir );
    }

    private void OnPropertyValueUpdate( CableProperties.Direction dir )
    {
      if ( Native != null ) {
        Native.getCableProperties().setYoungsModulus( Convert.ToDouble( Properties[ dir ].YoungsModulus ), CableProperties.ToNative( dir ) );
        Native.getCableProperties().setDamping( Convert.ToDouble( Properties[ dir ].Damping ), CableProperties.ToNative( dir ) );
        Native.getCableProperties().setPoissonsRatio( Convert.ToDouble( Properties[ dir ].PoissonsRatio ), CableProperties.ToNative( dir ) );

        var plasticityComponent = Native.getCablePlasticity();
        if ( plasticityComponent != null )
          plasticityComponent.setYieldPoint( Convert.ToDouble( Properties[ dir ].YieldPoint ), CableProperties.ToNative( dir ) );
      }
    }

    public PointCurve.SegmentationResult SynchronizeRoutePointCurve()
    {
      if ( RoutePointCurveUpToDate && m_routePointCurve.LastSuccessfulResult.Successful )
        return m_routePointCurve.LastSuccessfulResult;

      if ( m_routePointCurve == null )
        m_routePointCurve = new PointCurve();

      m_routePointCurve.LastSuccessfulResult = new PointCurve.SegmentationResult() { Error = float.PositiveInfinity, Successful = false };
      m_routePointsCache = new Vector3[] { };

      if ( m_routePointCurve.NumPoints == Route.NumNodes ) {
        for ( int i = 0; i < Route.NumNodes; ++i )
          m_routePointCurve[ i ] = Route[ i ].Position;
      }
      else {
        m_routePointCurve.Clear();
        foreach ( var node in Route )
          m_routePointCurve.Add( node.Position );
      }

      if ( m_routePointCurve.Finalize() ) {
        var numSegments = Mathf.Max( Mathf.CeilToInt( ResolutionPerUnitLength * Route.TotalLength ), 1 );
        var result = m_routePointCurve.FindSegmentLength( numSegments, PointCurve.DefaultErrorFunc, 5.0E-3f, 1.0E-3f );
        if ( result.Successful ) {
          m_routePointResulutionPerUnitLength = ResolutionPerUnitLength;
          var routePoints = new List<Vector3>();
          m_routePointCurve.Traverse( ( curr, next, type ) =>
          {
            routePoints.Add( curr.Point );
            if ( type == PointCurve.SegmentType.Last && Mathf.Abs( next.Time - 1.0f ) < Mathf.Abs( curr.Time - 1 ) )
              routePoints.Add( next.Point );
          }, result.SegmentLength );

          m_routePointsCache = routePoints.ToArray();

          return result;
        }
      }

      return new PointCurve.SegmentationResult() { Error = float.PositiveInfinity, Successful = false };
    }
  }
}
