using agx;
using AGXUnity.Utils;
using UnityEngine;

namespace AGXUnity.Model
{
  [AddComponentMenu( "AGXUnity/Vehicle/Steering" )]
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#steering" )]
  public class Steering : ScriptComponent
  {
    /// <summary>
    /// Different steering mechanisms that the steering constraint supports
    /// </summary>
    public enum SteeringMechanism
    {
      Ackermann,
      BellCrank,
      RackPinion,
      Davis
    }

    // <summary>
    /// Native instance if this constraint is initialized - otherwise null.
    /// </summary>
    public agxVehicle.Steering Native { get; private set; }

    /// <summary>
    /// Gets or sets the current steering angle that the constraint tries to maintain for the steering wheel
    /// </summary>
    [HideInInspector]
    public double SteeringAngle
    {
      get => Native != null ? Native.getSteeringAngle() : 0.0f;
      set => Native?.setSteeringAngle( Mathf.Clamp( (float)value, -(float)MaxSteeringAngle, (float)MaxSteeringAngle ) );
    }

    /// <summary>
    /// Calculates the maximum steering angle that can be applied to the constraint. 
    /// Note that this value is only calculated at runtime and edit-time access will return 0.0f.
    /// </summary>
    [HideInInspector]
    public double MaxSteeringAngle => Native != null ? Native.getMaximumSteeringAngle( 0 ) : 0.0f;

    /// <summary>
    /// The left wheel in the steering constraint
    /// </summary>
    [field: SerializeField]
    public WheelJoint LeftWheel { get; set; } = null;

    /// <summary>
    /// The right wheel in the steering constraint
    /// </summary>
    [field: SerializeField]
    public WheelJoint RightWheel { get; set; } = null;

    /// <summary>
    /// Validates that the provided wheel joints both have a common attachment body (chassis or world)
    /// </summary>
    public bool ValidateWheelConnectedParents() => RightWheel.AttachmentPair.ConnectedObject == LeftWheel.AttachmentPair.ConnectedObject;

    /// <summary>
    /// Validates that the wheel joints both have a common steering axis.
    /// </summary>
    public bool ValidateWheelRotations() => Vector3.Dot( RightWheel.SteeringAxis, LeftWheel.SteeringAxis ) > 0.99f;

    [SerializeField]
    private SteeringMechanism m_mechanism = SteeringMechanism.Ackermann;

    /// <summary>
    /// The steering mechanism used internally to calculate the wheel angles given an input steering angle.
    /// </summary>
    [IgnoreSynchronization]
    [DisableInRuntimeInspector]
    public SteeringMechanism Mechanism
    {
      get => m_mechanism;
      set
      {
        if ( Native != null && value != m_mechanism ) {
          Debug.LogWarning( "Steering mechanism cannot be changed after the steering component has been initialized" );
          return;
        }
        else {
          m_mechanism = value;
          AssignDefaults();
        }
      }
    }

    private bool Show_alpha0 => Mechanism == SteeringMechanism.BellCrank || Mechanism == SteeringMechanism.RackPinion;
    private bool Show_lc => Mechanism != SteeringMechanism.Ackermann;
    private bool Show_lr => Mechanism == SteeringMechanism.RackPinion || Mechanism == SteeringMechanism.Davis;
    private bool Show_side => Mechanism == SteeringMechanism.Ackermann;


    [SerializeField]
    private float m_phi0 = -105.0f * Mathf.Deg2Rad;

    [Tooltip( "Specifies the initial angle of between the wheel axis and the kingpin rod." )]
    [DisableInRuntimeInspector]
    [IgnoreSynchronization]
    [InspectorGroupBegin( Name = "Parameters" )]
    public float Phi0
    {
      get => m_phi0;
      set
      {
        if ( Native != null )
          Debug.LogWarning( "Setting steering parameters at runtime is not supported" );
        else
          m_phi0 = value;
      }
    }

    [SerializeField]
    private float m_l = 0.16f;

    [ClampAboveZeroInInspector]
    [DisableInRuntimeInspector]
    [IgnoreSynchronization]
    [Tooltip( "Specifies the length of the kingpin rod." )]
    public float L
    {
      get => m_l;
      set
      {
        if ( Native != null )
          Debug.LogWarning( "Setting steering parameters at runtime is not supported" );
        else
          m_l = value;
      }
    }

    [SerializeField]
    private float m_alpha0 = 76 * Mathf.Deg2Rad;

    [DynamicallyShowInInspector( "Show_alpha0" )]
    [DisableInRuntimeInspector]
    [IgnoreSynchronization]
    [Tooltip( "Specifies the initial angle between the wheel axis and the tie rod." )]
    public float Alpha0
    {
      get =>
        Mechanism switch
        {
          SteeringMechanism.Davis => ( Mathf.PI - Phi0 ),
          SteeringMechanism.Ackermann => 0.0f,
          _ => m_alpha0
        };

      set
      {
        if ( Mechanism == SteeringMechanism.Davis )
          Debug.LogWarning( "Davis steering model has tie rod angle (PI - Phi0). Set value will be ignored" );
        else if ( Mechanism == SteeringMechanism.Ackermann )
          Debug.LogWarning( "Ackermann steering model has fixed tie rod angle. Set value will be ignored" );
        else if ( Native != null )
          Debug.LogWarning( "Setting steering parameters at runtime is not supported" );
        else
          m_alpha0 = value;
      }
    }

    [SerializeField]
    private float m_lc = 1.0f;

    [ClampAboveZeroInInspector]
    [DynamicallyShowInInspector( "Show_lc" )]
    [IgnoreSynchronization]
    [Tooltip( "Specifies the length of the steering arm." )]
    public float Lc
    {
      get => m_lc;
      set
      {
        if ( Native != null )
          Debug.LogWarning( "Setting steering parameters at runtime is not supported" );
        else
          m_lc = value;
      }
    }

    [SerializeField]
    private float m_lr = 0.25f;

    [DisableInRuntimeInspector]
    [FloatSliderInInspector( 0, 1 )]
    [IgnoreSynchronization]
    [DynamicallyShowInInspector( "Show_lr" )]
    [Tooltip( "Specifies the length of the rack." )]
    public float Lr
    {
      get => Mechanism switch
      {
        SteeringMechanism.BellCrank => 0.0f,
        SteeringMechanism.Ackermann => 0.0f,
        _ => m_lr
      };
      set
      {
        if ( Mechanism == SteeringMechanism.BellCrank )
          Debug.LogWarning( "Bell-Crank steering model has implicit rack length (0.0). Set value will be ignored" );
        else if ( Mechanism == SteeringMechanism.Ackermann )
          Debug.LogWarning( "Ackermann steering model has no rack. Set value will be ignored" );
        else if ( Native != null )
          Debug.LogWarning( "Setting steering parameters at runtime is not supported" );
        else
          m_lr = value;
      }
    }

    [SerializeField]
    private float m_gear = 1.0f;
    [DisableInRuntimeInspector]
    [IgnoreSynchronization]
    [Tooltip( "The gear ratio between the steering wheel rotation and the steering arm rotation." )]
    public float Gear
    {
      get => m_gear;
      set
      {
        if ( Native != null )
          Debug.LogWarning( "Setting steering parameters at runtime is not supported" );
        else
          m_gear = value;
      }
    }

    /// <summary>
    /// Specifies a side on which the steering arm is located.
    /// </summary>
    public enum SteeringArmSide
    {
      Left = 0,
      Right = 1
    };

    [SerializeField]
    private SteeringArmSide m_side = SteeringArmSide.Left;

    [DynamicallyShowInInspector( "Show_side" )]
    [DisableInRuntimeInspector]
    [IgnoreSynchronization]
    [Tooltip( "When using the Ackermann steering mechanism, this parameter decides which side the steering arm is located on." )]
    public SteeringArmSide Side
    {
      get => m_side;
      set
      {
        if ( Native != null )
          Debug.LogWarning( "Setting steering parameters at runtime is not supported" );
        else
          m_side = value;
      }
    }

    /// <summary>
    /// Assigns the default values to each parameter given the current steering mechanism.
    /// </summary>
    public void AssignDefaults()
    {
      agxVehicle.SteeringParameters tmp = Mechanism switch
      {
        SteeringMechanism.Ackermann => agxVehicle.SteeringParameters.Ackermann(),
        SteeringMechanism.BellCrank => agxVehicle.SteeringParameters.BellCrank(),
        SteeringMechanism.RackPinion => agxVehicle.SteeringParameters.RackPinion(),
        SteeringMechanism.Davis => agxVehicle.SteeringParameters.Davis(),
        _ => null
      };

      if ( tmp == null ) {
        Debug.LogError( "Failed to find default steering parameters" );
        return;
      }

      Phi0      = (float)tmp.phi0;
      L         = (float)tmp.l;
      Lc        = (float)tmp.lc;
      Gear      = (float)tmp.gear;
      Side      = (SteeringArmSide)tmp.side;
      Lr        = (float)tmp.lr;
      Alpha0    = (float)tmp.alpha0;
    }

    protected override bool Initialize()
    {
      if ( LeftWheel == null || RightWheel == null ) {
        Debug.LogError( "The Steering constraint requires both WheelJoints to be set", this );
        return false;
      }

      if ( !ValidateWheelConnectedParents() ) {
        Debug.LogError( "The WheelJoints in a steering constraint must have a common Connected Parent object" );
        return false;
      }

      if ( !ValidateWheelRotations() ) {
        Debug.LogError( "The WheelJoints in a steering constraint must have a common steering axis" );
        return false;
      }

      var leftNative = LeftWheel.GetInitialized().Native;
      var rightNative = RightWheel.GetInitialized().Native;

      var nativeParams = new agxVehicle.SteeringParameters();

      nativeParams.gear = Gear;
      nativeParams.l = L;
      nativeParams.lc = Lc;
      nativeParams.lr = Lr;
      nativeParams.alpha0 = Alpha0;
      nativeParams.phi0 = Phi0;
      nativeParams.side = (uint)Side;

      Native = Mechanism switch
      {
        SteeringMechanism.Ackermann => new agxVehicle.Ackermann( leftNative, rightNative, nativeParams ),
        SteeringMechanism.BellCrank => new agxVehicle.BellCrank( leftNative, rightNative, nativeParams ),
        SteeringMechanism.RackPinion => new agxVehicle.RackPinion( leftNative, rightNative, nativeParams ),
        SteeringMechanism.Davis => new agxVehicle.Davis( leftNative, rightNative, nativeParams ),
        _ => null
      };

      if ( Native == null )
        return false;

      Simulation.Instance.Native.add( Native );
      Native.setEnable( isActiveAndEnabled );

      return base.Initialize();
    }

    protected override void OnDestroy()
    {
      if ( Simulation.HasInstance )
        Simulation.Instance.Native.remove( Native );

      Native = null;

      base.OnDestroy();
    }

    protected override void OnEnable()
    {
      if ( Native != null && !Native.getEnable() )
        Native.setEnable( true );
    }

    protected override void OnDisable()
    {
      if ( Native != null && Native.getEnable() )
        Native.setEnable( false );
    }

    private void OnDrawGizmos()
    {
      if ( !isActiveAndEnabled || RightWheel == null || LeftWheel == null )
        return;
      try {


        var scale = 0.02f;

        var leftWJ = LeftWheel.WheelAttachmentPoint;
        var rightWJ = RightWheel.WheelAttachmentPoint;
        var axleAxis = (rightWJ - leftWJ).normalized;

        double[] betas = { agxMath.PI - Phi0, Phi0 };
        double wheelTrackLength = (rightWJ - leftWJ).magnitude;
        var b = wheelTrackLength * L;
        var rackLength = wheelTrackLength * Lr;
        var steerArmLength = Lc * L * wheelTrackLength;
        var m_R = ( 1 + 2 * L * Mathf.Cos( Phi0 ) - Lr ) * wheelTrackLength / 2.0 / Mathf.Cos( Alpha0 );

        Vec3[] wheelWorldAttachs = {
        LeftWheel.WheelAttachmentPoint.ToHandedVec3(),
        RightWheel.WheelAttachmentPoint.ToHandedVec3()
      };

        UnityEngine.Matrix4x4[] matrices = {
        LeftWheel.AttachmentPair.ConnectedFrame.Parent.transform.localToWorldMatrix,
        RightWheel.AttachmentPair.ConnectedFrame.Parent.transform.localToWorldMatrix
      };

        Vector3 leftLocal = LeftWheel.AttachmentPair.ConnectedFrame.Parent.transform.localToWorldMatrix * leftWJ;
        Vector3 rightLocal = RightWheel.AttachmentPair.ConnectedFrame.Parent.transform.localToWorldMatrix * rightWJ;

        Vec3 wheelAxis = axleAxis.ToHandedVec3();
        Vec3 steeringAxis = LeftWheel.SteeringAxis.ToHandedVec3();
        Vec3 forwardAxis = steeringAxis.cross(wheelAxis);

        if ( Mechanism == SteeringMechanism.Ackermann ) {
          double d = wheelTrackLength;
          double c = 2.0 * m_R;
          double[] psi = new double[]{
          -agxMath.Acos((d - c) / (2 * b)) + LeftWheel.GetCurrentAngle(),
          -agxMath.PI + agxMath.Acos((d - c) / (2 * b)) + RightWheel.GetCurrentAngle()
        };

          // Render spheres at free hub
          Vector3[] freeHub   =  {
          (leftLocal.ToHandedVec3() + wheelAxis * b * agxMath.Cos(psi[0]) + forwardAxis * b * agxMath.Sin(psi[0])).ToHandedVector3(),
          (rightLocal.ToHandedVec3() + wheelAxis * b * agxMath.Cos(psi[1]) + forwardAxis * b * agxMath.Sin(psi[1])).ToHandedVector3()
        };

          for ( int i = 0; i < 2; ++i ) {
            freeHub[ i ] = matrices[ i ] * freeHub[ i ];
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere( freeHub[ i ], scale );
          }

          Gizmos.color = Color.magenta;
          // Render tie rod
          GizmoUtils.DrawCylinder( freeHub[ 0 ], freeHub[ 1 ], scale );

          Gizmos.color = Color.blue;
          // Render kingpins
          for ( int i = 0; i < 2; ++i )
            GizmoUtils.DrawCylinder( freeHub[ i ], wheelWorldAttachs[ i ].ToHandedVector3(), scale );

          Gizmos.color = Color.red;
          // Render wheel track
          GizmoUtils.DrawCylinder( wheelWorldAttachs[ 0 ].ToHandedVector3(), wheelWorldAttachs[ 1 ].ToHandedVector3(), scale );
        }
        else if (
          Mechanism == SteeringMechanism.BellCrank ||
          Mechanism == SteeringMechanism.RackPinion ||
          Mechanism == SteeringMechanism.Davis ) {
          Vec3 tieRodPos = new Vec3( leftLocal.ToHandedVec3() + new Quat( betas[ 0 ], steeringAxis ) * wheelAxis * b + new Quat( Alpha0, steeringAxis ) * wheelAxis * m_R );
          Vec3 steerColumnLocal = tieRodPos + steerArmLength * forwardAxis + rackLength / 2.0 * wheelAxis;

          Vector3 steerColumnWorld = matrices[0] * steerColumnLocal.ToHandedVector3();

          if ( Mechanism == SteeringMechanism.BellCrank ) {
            double[] m_thetas = {
            SteeringAngle + agxMath.Atan2( -0.5 * rackLength, steerArmLength ),
            SteeringAngle + agxMath.Atan2( 0.5 * rackLength, steerArmLength )
          };

            // Transform steering column from chasis to world coordinate system.
            AffineMatrix4x4[] R = {
            AffineMatrix4x4.rotate( m_thetas[ 0 ], steeringAxis ),
            AffineMatrix4x4.rotate( m_thetas[ 1 ], steeringAxis )
          };

            Vector3[] steeringArmPos = {
            matrices[0] * ( steerColumnLocal + new Quat( -agxMath.PI / 2.0, steeringAxis ) * wheelAxis * steerArmLength * R[ 0 ] ).ToHandedVector3(),
            matrices[0] * ( steerColumnLocal + new Quat( -agxMath.PI / 2.0, steeringAxis ) * wheelAxis * steerArmLength * R[ 1 ] ).ToHandedVector3()
          };

            for ( int i = 0; i < 2; ++i ) {
              // Rendering the kingpin, that links to wheel anchor
              var delta = betas[i] + (i == 0 ? LeftWheel : RightWheel).GetCurrentAngle();
              Vector3[] kp = {
              matrices[i] * (i == 0 ? leftLocal : rightLocal),
              matrices[i] * ((i == 0 ? leftLocal : rightLocal) + (new Quat(delta, steeringAxis) * wheelAxis * b).ToHandedVector3())
            };
              Gizmos.color = Color.blue;
              GizmoUtils.DrawCylinder( kp[ 0 ], kp[ 1 ], scale );

              // Rendering the tie rod, that links kingpin and steering arm
              Gizmos.color = Color.magenta;
              GizmoUtils.DrawCylinder( kp[ 1 ], steeringArmPos[ i ], scale );
            }

            for ( int i = 0; i < 2; ++i ) {
              Gizmos.color = Color.red;
              // Rendering the steering arm, that links to steering column
              GizmoUtils.DrawCylinder( steerColumnWorld, steeringArmPos[ i ], scale );
              // Rendering one end of the steering arm
              Gizmos.DrawSphere( steeringArmPos[ i ], scale );
            }

            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere( steerColumnWorld, scale );
          }
          else { // Rack-Pinion or Davis
            var halfRack = 0.5 * rackLength;
            double[] _l = {
            -halfRack + steerArmLength * agxMath.Tan(SteeringAngle),
            halfRack + steerArmLength * agxMath.Tan(SteeringAngle)
          };

            double[] thetas = new double[2];
            double[] m_r = new double[2];

            for ( int i = 0; i < 2; ++i ) {
              thetas[ i ] = agxMath.Atan2( _l[ i ], steerArmLength );
              m_r[ i ] = agxMath.Sqrt( _l[ i ] * _l[ i ] + steerArmLength * steerArmLength );
            }

            if ( Mechanism == SteeringMechanism.RackPinion ) {
              // Transform steering column from chasis to world coordinate system.
              AffineMatrix4x4[] Rs = {
              AffineMatrix4x4.rotate( thetas[ 0 ], steeringAxis ),
              AffineMatrix4x4.rotate( thetas[ 1 ], steeringAxis )
            };

              Vector3[] steeringArmPos ={
              matrices[0] * ( steerColumnLocal + new Quat( -agxMath.PI / 2.0, steeringAxis ) * wheelAxis * m_r[0] * Rs[ 0 ] ).ToHandedVector3(),
              matrices[0] * ( steerColumnLocal + new Quat( -agxMath.PI / 2.0, steeringAxis ) * wheelAxis * m_r[1] * Rs[ 1 ] ).ToHandedVector3()
            };

              for ( int i = 0; i < 2; ++i ) {
                // Rendering the kingpin, that links to wheel anchor
                var delta = betas[i] + (i == 0 ? LeftWheel : RightWheel).GetCurrentAngle();
                Vector3[] kp = {
              matrices[i] * (i == 0 ? leftLocal : rightLocal),
              matrices[i] * ((i == 0 ? leftLocal : rightLocal) + (new Quat(delta, steeringAxis) * wheelAxis * b).ToHandedVector3())
            };
                Gizmos.color = Color.blue;
                GizmoUtils.DrawCylinder( kp[ 0 ], kp[ 1 ], scale );

                // Rendering the tie rod, that links kingpin and steering arm
                Gizmos.color = Color.magenta;
                GizmoUtils.DrawCylinder( kp[ 1 ], steeringArmPos[ i ], scale );
              }

              Gizmos.color = Color.red;
              // Rendering the middle steering arm
              GizmoUtils.DrawCylinder( steerColumnWorld, ( steeringArmPos[ 1 ] + steeringArmPos[ 0 ] )/2, scale );

              Gizmos.color = Color.yellow;
              // Rendering the rack
              GizmoUtils.DrawCylinder( steeringArmPos[ 1 ], steeringArmPos[ 0 ], scale );

              for ( int i = 0; i < 2; ++i ) {
                Gizmos.color = Color.red;
                // Rendering the steering arm, that links to steering column
                GizmoUtils.DrawCylinder( steerColumnWorld, steeringArmPos[ i ], scale );
                // Rendering one end of the steering arm
                Gizmos.DrawSphere( steeringArmPos[ i ], scale );
              }

              Gizmos.color = Color.yellow;
              Gizmos.DrawSphere( steerColumnWorld, scale );
            }
            else if ( Mechanism == SteeringMechanism.Davis ) {
              Vec3 getP( int i )
              {
                var dx = steerArmLength * agxMath.Tan(SteeringAngle);
                var t = (2.0 * i - 1.0) * rackLength / 2.0 + dx;

                return steerColumnLocal + new Quat( -agxMath.PI / 2.0, steeringAxis ) * wheelAxis * steerArmLength + t * wheelAxis;
              }

              Vector3[] ends = {
              matrices[0] * getP(0).ToHandedVector3(),
              matrices[0] * getP(1).ToHandedVector3()
            };

              for ( int i = 0; i < 2; ++i ) {
                var delta = betas[i] + (i == 0 ? LeftWheel.GetCurrentAngle() : RightWheel.GetCurrentAngle());
                Vector3[] kp = {
                matrices[0] * (i == 0 ? leftLocal : rightLocal),
                matrices[0] * (( i == 0 ? leftLocal : rightLocal ) + (new Quat(delta, steeringAxis) * wheelAxis * b).ToHandedVector3())
              };

                // Rendering kingpin
                Gizmos.color = Color.blue;
                GizmoUtils.DrawCylinder( kp[ 0 ], kp[ 1 ], scale );

                // Rendering tie rod
                Gizmos.color = Color.magenta;
                GizmoUtils.DrawCylinder( kp[ 1 ], ends[ i ], scale );
                // Rendering one end of tie rod
                Gizmos.DrawSphere( ends[ i ], scale );

                // Rendering one end of kingpin
                Gizmos.color = Color.red;
                Gizmos.DrawSphere( kp[ 1 ], scale );
              }

              // Rendering rack/bar
              Gizmos.color = Color.yellow;
              GizmoUtils.DrawCylinder( ends[ 1 ], ends[ 0 ], scale );
            }
          }
        }
      }
      catch ( Exception ) {
        Debug.LogWarning( "Failed to render steering gizmos, this is most likely due to incorrect steering parameters.", this );
      }
    }
  }
}
