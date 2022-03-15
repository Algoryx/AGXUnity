using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace AGXUnity.Model
{
  [AddComponentMenu( "AGXUnity/Model/Wheel Loader" )]
  [DisallowMultipleComponent]
  public class WheelLoader : ScriptComponent
  {
    public enum DifferentialLocation
    {
      Rear,
      Center,
      Front
    }

    public enum WheelLocation
    {
      RightRear,
      LeftRear,
      RightFront,
      LeftFront
    }

    [SerializeField]
    private bool m_engineEnabled = true;

    [InspectorGroupBegin( Name = "Engine" )]
    public bool EngineEnabled
    {
      get { return m_engineEnabled; }
      set
      {
        m_engineEnabled = value;
        if ( Engine != null )
          Engine.setEnable( m_engineEnabled );
      }
    }

    [SerializeField]
    private float m_inletVolume = 0.015f;

    [ClampAboveZeroInInspector]
    public float InletVolume
    {
      get { return m_inletVolume; }
      set
      {
        m_inletVolume = value;
        if ( Engine != null )
          Engine.setInletVolume( m_inletVolume );
      }
    }

    [SerializeField]
    private float m_volumetricEfficiency = 0.9f;

    [ClampAboveZeroInInspector]
    public float VolumetricEfficiency
    {
      get { return m_volumetricEfficiency; }
      set
      {
        m_volumetricEfficiency = value;
        if ( Engine != null )
          Engine.setVolumetricEfficiency( m_volumetricEfficiency );
      }
    }

    [SerializeField]
    private float m_throttle = 0.0f;

    [FloatSliderInInspector( 0.0f, 1.0f )]
    public float Throttle
    {
      get { return m_throttle; }
      set
      {
        m_throttle = value;
        if ( Engine != null )
          Engine.setThrottle( m_throttle );
      }
    }

    [SerializeField]
    private float m_idleThrottleAngle = 0.17f;

    [ClampAboveZeroInInspector( true )]
    public float IdleThrottleAngle
    {
      get { return m_idleThrottleAngle; }
      set
      {
        m_idleThrottleAngle = Mathf.Min( value, 1.45f );
        if ( Engine != null )
          Engine.setIdleThrottleAngle( m_idleThrottleAngle );
      }
    }

    [SerializeField]
    private float m_throttleBore = 0.3062f;

    [ClampAboveZeroInInspector]
    public float ThrottleBore
    {
      get { return m_throttleBore; }
      set
      {
        m_throttleBore = value;
        if ( Engine != null )
          Engine.setThrottleBore( m_throttleBore );
      }
    }

    [SerializeField]
    private float m_numberOfRevolutionsPerCycle = 2.0f;

    [ClampAboveZeroInInspector]
    public float NumberOfRevolutionsPerCycle
    {
      get { return m_numberOfRevolutionsPerCycle; }
      set
      {
        m_numberOfRevolutionsPerCycle = Mathf.Max( value, 1.0f );
        if ( Engine != null )
          Engine.setNrRevolutionsPerCycle( m_numberOfRevolutionsPerCycle );
      }
    }

    [SerializeField]
    private Vector2 m_gearRatios = new Vector2( -10.0f, 10.0f );

    [InspectorGroupBegin(Name = "Gear Box")]
    public Vector2 GearRatios
    {
      get { return m_gearRatios; }
      set
      {
        m_gearRatios = value;
        if ( GearBox != null )
          GearBox.setGearRatios( new agx.RealVector( new double[] { m_gearRatios.x, m_gearRatios.y } ) );
      }
    }

    [SerializeField]
    private float m_rearDifferentialGearRatio = 1.0f;

    [InspectorGroupBegin( Name = "Differentials" )]
    public float RearDifferentialGearRatio
    {
      get { return m_rearDifferentialGearRatio; }
      set
      {
        m_rearDifferentialGearRatio = value;
        if ( Differentials[ (int)DifferentialLocation.Rear ] != null )
          Differentials[ (int)DifferentialLocation.Rear ].setGearRatio( m_rearDifferentialGearRatio );
      }
    }

    [SerializeField]
    private bool m_rearDifferentialLocked = false;

    public bool RearDifferentialLocked
    {
      get { return m_rearDifferentialLocked; }
      set
      {
        m_rearDifferentialLocked = value;
        if ( Differentials[ (int)DifferentialLocation.Rear ] != null )
          Differentials[ (int)DifferentialLocation.Rear ].setLock( m_rearDifferentialLocked );
      }
    }

    [SerializeField]
    private float m_centerDifferentialGearRatio = 10.0f;

    public float CenterDifferentialGearRatio
    {
      get { return m_centerDifferentialGearRatio; }
      set
      {
        m_centerDifferentialGearRatio = value;
        if ( Differentials[ (int)DifferentialLocation.Center ] != null )
          Differentials[ (int)DifferentialLocation.Center ].setGearRatio( m_centerDifferentialGearRatio );
      }
    }

    [SerializeField]
    private bool m_centerDifferentialLocked = true;

    public bool CenterDifferentialLocked
    {
      get { return m_centerDifferentialLocked; }
      set
      {
        m_centerDifferentialLocked = value;
        if ( Differentials[ (int)DifferentialLocation.Center ] != null )
          Differentials[ (int)DifferentialLocation.Center ].setLock( m_centerDifferentialLocked );
      }
    }

    [SerializeField]
    private float m_frontDifferentialGearRatio = 1.0f;

    public float FrontDifferentialGearRatio
    {
      get { return m_frontDifferentialGearRatio; }
      set
      {
        m_frontDifferentialGearRatio = value;
        if ( Differentials[ (int)DifferentialLocation.Front ] != null )
          Differentials[ (int)DifferentialLocation.Front ].setGearRatio( m_frontDifferentialGearRatio );
      }
    }

    [SerializeField]
    private bool m_frontDifferentialLocked = false;

    public bool FrontDifferentialLocked
    {
      get { return m_frontDifferentialLocked; }
      set
      {
        m_frontDifferentialLocked = value;
        if ( Differentials[ (int)DifferentialLocation.Front ] != null )
          Differentials[ (int)DifferentialLocation.Front ].setLock( m_frontDifferentialLocked );
      }
    }

    [InspectorGroupBegin( Name = "Wheels" )]
    [AllowRecursiveEditing]
    public RigidBody LeftRearWheel { get { return GetOrFindWheel( WheelLocation.LeftRear ); } }
    [AllowRecursiveEditing]
    public RigidBody RightRearWheel { get { return GetOrFindWheel( WheelLocation.RightRear ); } }
    [AllowRecursiveEditing]
    public RigidBody LeftFrontWheel { get { return GetOrFindWheel( WheelLocation.LeftFront ); } }
    [AllowRecursiveEditing]
    public RigidBody RightFrontWheel { get { return GetOrFindWheel( WheelLocation.RightFront ); } }

    [InspectorGroupBegin( Name = "Wheel Hinges" )]
    [AllowRecursiveEditing]
    public Constraint LeftRearHinge { get { return GetOrFindConstraint( WheelLocation.LeftRear, "Hinge", m_wheelHinges ); } }
    [AllowRecursiveEditing]
    public Constraint RightRearHinge { get { return GetOrFindConstraint( WheelLocation.RightRear, "Hinge", m_wheelHinges ); } }
    [AllowRecursiveEditing]
    public Constraint LeftFrontHinge { get { return GetOrFindConstraint( WheelLocation.LeftFront, "Hinge", m_wheelHinges ); } }
    [AllowRecursiveEditing]
    public Constraint RightFrontHinge { get { return GetOrFindConstraint( WheelLocation.RightFront, "Hinge", m_wheelHinges ); } }

    [SerializeField]
    private TwoBodyTireProperties m_tireProperties = null;

    [InspectorGroupBegin( Name = "Tire Models" )]
    [AllowRecursiveEditing]
    [InspectorSeparator]
    public TwoBodyTireProperties TireProperties
    {
      get
      {
        return GetOrCreateTireModelProperties();
      }
    }

    [InspectorSeparator]
    [AllowRecursiveEditing]
    public TwoBodyTire LeftRearTireModel
    {
      get
      {
        return GetOrCreateTireModel( WheelLocation.LeftRear );
      }
    }

    [AllowRecursiveEditing]
    public TwoBodyTire RightRearTireModel
    {
      get
      {
        return GetOrCreateTireModel( WheelLocation.RightRear );
      }
    }

    [AllowRecursiveEditing]
    public TwoBodyTire LeftFrontTireModel
    {
      get
      {
        return GetOrCreateTireModel( WheelLocation.LeftFront );
      }
    }

    [AllowRecursiveEditing]
    public TwoBodyTire RightFrontTireModel
    {
      get
      {
        return GetOrCreateTireModel( WheelLocation.RightFront );
      }
    }

    [InspectorGroupBegin( Name = "Controlled Constraints")]
    [AllowRecursiveEditing]
    public Constraint SteeringHinge
    {
      get
      {
        if ( m_steeringHinge == null )
          m_steeringHinge = FindChild<Constraint>( "WaistHinge" );
        return m_steeringHinge;
      }
    }

    [AllowRecursiveEditing]
    public Constraint LeftElevatePrismatic
    {
      get
      {
        if ( m_elevatePrismatics[ 0 ] == null )
          m_elevatePrismatics[ 0 ] = FindChild<Constraint>( "LeftLowerPrismatic" );
        return m_elevatePrismatics[ 0 ];
      }
    }

    [AllowRecursiveEditing]
    public Constraint RightElevatePrismatic
    {
      get
      {
        if ( m_elevatePrismatics[ 1 ] == null )
          m_elevatePrismatics[ 1 ] = FindChild<Constraint>( "RightLowerPrismatic" );
        return m_elevatePrismatics[ 1 ];
      }
    }

    [HideInInspector]
    public Constraint[] ElevatePrismatics
    {
      get
      {
        if ( m_elevatePrismatics[ 0 ] == null || m_elevatePrismatics[ 1 ] == null )
          return new Constraint[] { LeftElevatePrismatic, RightElevatePrismatic };
        return m_elevatePrismatics;
      }
    }

    [AllowRecursiveEditing]
    public Constraint TiltPrismatic
    {
      get
      {
        if ( m_tiltPrismatic == null )
          m_tiltPrismatic = FindChild<Constraint>( "CenterPrismatic" );
        return m_tiltPrismatic;
      }
    }

    [InspectorGroupEnd]
    [HideInInspector]
    public agx.Hinge BrakeHinge { get; private set; } = null;
    public agxPowerLine.PowerLine PowerLine { get; private set; } = null;
    public agxDriveTrain.CombustionEngine Engine { get; private set; } = null;
    public agxDriveTrain.GearBox GearBox { get; private set; } = null;
    public agxDriveTrain.Differential[] Differentials { get; private set; } = new agxDriveTrain.Differential[] { null, null, null };
    public agxDriveTrain.TorqueConverter TorqueConverter { get; private set; } = null;

    [HideInInspector]
    public float Speed
    {
      get
      {
        return Vector3.Dot( FrontBodyObserver.transform.TransformDirection( Vector3.forward ),
                            FrontBody.LinearVelocity );
      }
    }

    [HideInInspector]
    public RigidBody FrontBody
    {
      get
      {
        if ( m_frontBody == null )
          m_frontBody = FindChild<RigidBody>( "FrontBody" );
        return m_frontBody;
      }
    }

    [HideInInspector]
    public ObserverFrame FrontBodyObserver
    {
      get
      {
        if ( m_frontBodyObserver == null )
          m_frontBodyObserver = FrontBody.GetComponentInChildren<ObserverFrame>();
        return m_frontBodyObserver;
      }
    }

    public IEnumerable<Constraint> WheelHinges
    {
      get
      {
        yield return LeftFrontHinge;
        yield return RightFrontHinge;
        yield return LeftRearHinge;
        yield return RightRearHinge;
      }
    }

    protected override bool Initialize()
    {
      LicenseManager.LicenseInfo.HasModuleLogError( LicenseInfo.Module.AGXDriveTrain, this );

      PowerLine = new agxPowerLine.PowerLine();
      PowerLine.setName( name );

      Engine = new agxDriveTrain.CombustionEngine( InletVolume );
      Engine.setDischargeCoefficient( 0.2f );

      TorqueConverter = new agxDriveTrain.TorqueConverter();
      GearBox = new agxDriveTrain.GearBox();
      Differentials[ (int)DifferentialLocation.Rear ]   = new agxDriveTrain.Differential();
      Differentials[ (int)DifferentialLocation.Center ] = new agxDriveTrain.Differential();
      Differentials[ (int)DifferentialLocation.Front ]  = new agxDriveTrain.Differential();

      m_actuators[ (int)WheelLocation.LeftFront ]  = new agxPowerLine.RotationalActuator( LeftFrontHinge.GetInitialized<Constraint>().Native.asHinge() );
      m_actuators[ (int)WheelLocation.RightFront ] = new agxPowerLine.RotationalActuator( RightFrontHinge.GetInitialized<Constraint>().Native.asHinge() );
      m_actuators[ (int)WheelLocation.LeftRear ]   = new agxPowerLine.RotationalActuator( LeftRearHinge.GetInitialized<Constraint>().Native.asHinge() );
      m_actuators[ (int)WheelLocation.RightRear ]  = new agxPowerLine.RotationalActuator( RightRearHinge.GetInitialized<Constraint>().Native.asHinge() );

      foreach ( var wheelHinge in WheelHinges )
        wheelHinge.GetController<TargetSpeedController>().Enable = false;

      var engineTorqueConverterShaft    = new agxDriveTrain.Shaft();
      var torqueConverterGearBoxShaft   = new agxDriveTrain.Shaft();
      var gearBoxCenterDiffShaft        = new agxDriveTrain.Shaft();
      var centerDiffRearDiffShaft       = new agxDriveTrain.Shaft();
      var centerDiffFrontDiffShaft      = new agxDriveTrain.Shaft();
      var frontDiffFrontLeftWheelShaft  = new agxDriveTrain.Shaft();
      var frontDiffFrontRightWheelShaft = new agxDriveTrain.Shaft();
      var rearDiffRearLeftWheelShaft    = new agxDriveTrain.Shaft();
      var rearDiffRearRightWheelShaft   = new agxDriveTrain.Shaft();

      PowerLine.setSource( Engine );

      Engine.connect( engineTorqueConverterShaft );
      engineTorqueConverterShaft.connect( TorqueConverter );
      TorqueConverter.connect( torqueConverterGearBoxShaft );
      torqueConverterGearBoxShaft.connect( GearBox );
      GearBox.connect( gearBoxCenterDiffShaft );
      gearBoxCenterDiffShaft.connect( Differentials[ (int)DifferentialLocation.Center ] );

      Differentials[ (int)DifferentialLocation.Center ].connect( centerDiffFrontDiffShaft );
      centerDiffFrontDiffShaft.connect( Differentials[ (int)DifferentialLocation.Front ] );
      Differentials[ (int)DifferentialLocation.Front ].connect( frontDiffFrontLeftWheelShaft );
      Differentials[ (int)DifferentialLocation.Front ].connect( frontDiffFrontRightWheelShaft );
      frontDiffFrontLeftWheelShaft.connect( m_actuators[ (int)WheelLocation.LeftFront ] );
      frontDiffFrontRightWheelShaft.connect( m_actuators[ (int)WheelLocation.RightFront ] );

      Differentials[ (int)DifferentialLocation.Center ].connect( centerDiffRearDiffShaft );
      centerDiffRearDiffShaft.connect( Differentials[ (int)DifferentialLocation.Rear ] );
      Differentials[ (int)DifferentialLocation.Rear ].connect( rearDiffRearLeftWheelShaft );
      Differentials[ (int)DifferentialLocation.Rear ].connect( rearDiffRearRightWheelShaft );
      rearDiffRearLeftWheelShaft.connect( m_actuators[ (int)WheelLocation.LeftRear ] );
      rearDiffRearRightWheelShaft.connect( m_actuators[ (int)WheelLocation.RightRear ] );

      GearBox.setGearRatios( new agx.RealVector( new double[] { GearRatios.x, GearRatios.y } ) );
      GearBox.gearUp();

      GetSimulation().add( PowerLine );

      var f1 = new agx.Frame();
      var f2 = new agx.Frame();
      agx.Constraint.calculateFramesFromBody( new agx.Vec3(),
                                              agx.Vec3.X_AXIS(),
                                              gearBoxCenterDiffShaft.getRotationalDimension().getOrReserveBody(),
                                              f1,
                                              null,
                                              f2 );
      BrakeHinge = new agx.Hinge( gearBoxCenterDiffShaft.getRotationalDimension().getOrReserveBody(),
                                  f1,
                                  null,
                                  f2 );
      GetSimulation().add( BrakeHinge );

      try {
        GetOrCreateTireModelProperties();
        foreach ( WheelLocation location in Enum.GetValues( typeof( WheelLocation ) ) )
          GetOrCreateTireModel( location )?.GetInitialized<TwoBodyTire>();
      }
      catch ( Exception e ) {
        Debug.LogWarning( "Unable to initialize tire models: " + e.Message );
      }

      return true;
    }

    protected override void OnDestroy()
    {
      if ( Simulation.HasInstance ) {
        GetSimulation().remove( PowerLine );
        GetSimulation().remove( BrakeHinge );
      }

      PowerLine       = null;
      BrakeHinge      = null;
      Engine          = null;
      GearBox         = null;
      TorqueConverter = null;
      Differentials[ (int)DifferentialLocation.Rear ]   = null;
      Differentials[ (int)DifferentialLocation.Center ] = null;
      Differentials[ (int)DifferentialLocation.Front ]  = null;
      m_actuators[ (int)WheelLocation.RightRear ]  = null;
      m_actuators[ (int)WheelLocation.LeftRear ]   = null;
      m_actuators[ (int)WheelLocation.RightFront ] = null;
      m_actuators[ (int)WheelLocation.LeftFront ]  = null;

      base.OnDestroy();
    }

    private void Reset()
    {
      try {
        GetOrCreateTireModelProperties();
      }
      catch ( Exception e ) {
        Debug.LogError( "Unable to initialize tire models: " + e.Message );
      }
    }

    private T FindChild<T>( string name )
      where T : ScriptComponent
    {
      return transform.Find( name ).GetComponent<T>();
    }

    private RigidBody GetOrFindWheel( WheelLocation location )
    {
      if ( m_wheelBodies[ (int)location ] == null )
        m_wheelBodies[ (int)location ] = FindChild<RigidBody>( location.ToString() + "Tire" );
      return m_wheelBodies[ (int)location ];
    }

    private Constraint GetOrFindConstraint( WheelLocation location, string postfix, Constraint[] cache )
    {
      if ( cache[ (int)location ] == null )
        cache[ (int)location ] = FindChild<Constraint>( location.ToString() + postfix );
      return cache[ (int)location ];
    }

    private TwoBodyTire GetOrCreateTireModel( WheelLocation location )
    {
      if ( m_tireModels == null || m_tireModels.Length == 0 ) {
        m_tireModels = new TwoBodyTire[4] { null, null, null, null };
        var tireModels = GetComponents<TwoBodyTire>();
        if ( tireModels.Length == 4 ) {
          foreach ( WheelLocation wl in Enum.GetValues( typeof( WheelLocation ) ) ) {
            var trb = GetOrFindWheel( wl );
            var rrb = FindChild<RigidBody>( wl.ToString() + "Rim" );
            m_tireModels[ (int)wl ] = tireModels.FirstOrDefault( tireModel => tireModel.TireRigidBody == trb &&
                                                                              tireModel.RimRigidBody == rrb);
          }
        }
        else if ( tireModels.Length != 0 )
          Debug.LogWarning( "Tire models mismatch: Got " + tireModels.Length + ", expecting 0 or 4.", this );
      }

      var iLocation = (int)location;
      if ( m_tireModels[ iLocation ] != null )
        return m_tireModels[ iLocation ];

      var tireRigidBody = GetOrFindWheel( location );
      var rimRigidBody  = FindChild<RigidBody>( location.ToString() + "Rim" );
      if ( tireRigidBody == null || rimRigidBody == null )
        return null;

      var locks = ( from constraint in GetComponentsInChildren<Constraint>()
                    where constraint.Type == ConstraintType.LockJoint
                    select constraint ).ToArray();
      var tireLock = locks.FirstOrDefault( constraint => constraint.AttachmentPair.Match( tireRigidBody, rimRigidBody ) );
      TwoBodyTire tire = gameObject.AddComponent<TwoBodyTire>();
      tire.hideFlags = HideFlags.HideInInspector;
      bool valid = false;
      if ( tireLock != null )
        valid = tire.Configure( tireLock, tireRigidBody );
      else {
        valid = tire.SetTire( tireRigidBody );
        valid = valid && tire.SetRim( rimRigidBody );
      }
      if ( !valid ) {
        DestroyImmediate( tire );
        return null;
      }

      m_tireModels[ iLocation ] = tire;

      return tire;
    }

    private TwoBodyTireProperties GetOrCreateTireModelProperties()
    {
      if ( m_tireProperties != null )
        return m_tireProperties;

      m_tireProperties = ScriptAsset.Create<TwoBodyTireProperties>();
      m_tireProperties.RadialStiffness             = 2.0E6f;
      m_tireProperties.RadialDampingCoefficient    = 9.0E4f;
      m_tireProperties.LateralStiffness            = 4.0E6f;
      m_tireProperties.LateralDampingCoefficient   = 9.0E4f;
      m_tireProperties.BendingStiffness            = 1.0E6f;
      m_tireProperties.BendingDampingCoefficient   = 9.0E4f;
      m_tireProperties.TorsionalStiffness          = 1.0E6f;
      m_tireProperties.TorsionalDampingCoefficient = 9.0E4f;

      LeftRearTireModel.Properties   = m_tireProperties;
      RightRearTireModel.Properties  = m_tireProperties;
      LeftFrontTireModel.Properties  = m_tireProperties;
      RightFrontTireModel.Properties = m_tireProperties;

      return m_tireProperties;
    }

    private agxPowerLine.RotationalActuator[] m_actuators = new agxPowerLine.RotationalActuator[] { null, null, null, null };
 
    private RigidBody[] m_wheelBodies = new RigidBody[] { null, null, null, null };
    private ObserverFrame m_frontBodyObserver = null;
    private RigidBody m_frontBody = null;
    private Constraint[] m_wheelHinges = new Constraint[] { null, null, null, null };
    private TwoBodyTire[] m_tireModels = null;
    private Constraint m_steeringHinge = null;

    private Constraint[] m_elevatePrismatics = new Constraint[] { null, null };
    private Constraint m_tiltPrismatic = null;
  }
}
