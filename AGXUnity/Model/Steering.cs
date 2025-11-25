using agx;
using AGXUnity.Utils;
using UnityEngine;

namespace AGXUnity.Model
{
  public class Steering : ScriptComponent
  {
    public enum SteeringMechanism
    {
      Ackermann,
      BellCrank,
      RackPinion,
      Davis
    }

    [SerializeField]
    private SteeringMechanism m_mechanism = SteeringMechanism.Ackermann;

    public SteeringMechanism Mechanism
    {
      get => m_mechanism;
      set
      {
        if ( Native != null && value != m_mechanism ) {
          Debug.LogWarning( "Steering mechanism cannot be changed after the steering component has been initialized" );
          return;
        }
        m_mechanism = value;
      }
    }

    // <summary>
    /// Native instance if this constraint is initialized - otherwise null.
    /// </summary>
    public agxVehicle.Steering Native { get; private set; }

    [HideInInspector]
    public double SteeringAngle
    {
      get => Native != null ? Native.getSteeringAngle() : 0.0f;
      set => Native?.setSteeringAngle( Mathf.Clamp( (float)value, -(float)MaxSteeringAngle, (float)MaxSteeringAngle ) );
    }

    [HideInInspector]
    public double MaxSteeringAngle => Native != null ? Native.getMaximumSteeringAngle( 0 ) : 0.0f;

    [field: SerializeField]
    public WheelJoint LeftWheel { get; set; } = null;

    [field: SerializeField]
    public WheelJoint RightWheel { get; set; } = null;

    [field: SerializeField]
    [DisableInRuntimeInspector]
    public SteeringParameters Parameters { get; set; } = null;

    protected override bool Initialize()
    {
      if ( LeftWheel == null || RightWheel == null )
        return false;

      var leftNative = LeftWheel.GetInitialized().Native;
      var rightNative = RightWheel.GetInitialized().Native;

      if ( Parameters != null ) {
        if ( Parameters.Mechanism != Mechanism )
          Debug.LogWarning( $"Provided steering parameters for '{name}' are configured for a different steering mechanism which might cause unexpected behaviour.", this );
        var nativeParams = Parameters.GetInitialized().Native;

        Native = Mechanism switch
        {
          SteeringMechanism.Ackermann => new agxVehicle.Ackermann( leftNative, rightNative, nativeParams ),
          SteeringMechanism.BellCrank => new agxVehicle.BellCrank( leftNative, rightNative, nativeParams ),
          SteeringMechanism.RackPinion => new agxVehicle.RackPinion( leftNative, rightNative, nativeParams ),
          SteeringMechanism.Davis => new agxVehicle.Davis( leftNative, rightNative, nativeParams ),
          _ => null
        };
      }
      else {
        Native = Mechanism switch
        {
          SteeringMechanism.Ackermann => new agxVehicle.Ackermann( leftNative, rightNative ),
          SteeringMechanism.BellCrank => new agxVehicle.BellCrank( leftNative, rightNative ),
          SteeringMechanism.RackPinion => new agxVehicle.RackPinion( leftNative, rightNative ),
          SteeringMechanism.Davis => new agxVehicle.Davis( leftNative, rightNative ),
          _ => null
        };
      }

      if ( Native == null )
        return false;

      Simulation.Instance.Native.add( Native );

      return base.Initialize();
    }

    private void OnDrawGizmos()
    {
      var scale = 0.02f;

      var leftWJ = LeftWheel.WheelAttachmentPoint;
      var rightWJ = RightWheel.WheelAttachmentPoint;
      var axleAxis = (rightWJ - leftWJ).normalized;

      var phi0 = Parameters != null ? Parameters.Phi0 : Mechanism switch
      {
        SteeringMechanism.Ackermann => -115.0f * Mathf.Deg2Rad,
        SteeringMechanism.BellCrank => -108.0f * Mathf.Deg2Rad,
        SteeringMechanism.RackPinion => -108.0f * Mathf.Deg2Rad,
        SteeringMechanism.Davis => 104.0f * Mathf.Deg2Rad,
        _ => 0.0f
      };
      var l = Parameters != null ? Parameters.L : Mechanism switch
      {
        SteeringMechanism.Ackermann => 0.16f,
        SteeringMechanism.BellCrank => 0.14f,
        SteeringMechanism.RackPinion => 0.14f,
        SteeringMechanism.Davis => 0.14f,
        _ => 0.0f
      };
      var alpha0 = Parameters != null ? Parameters.Alpha0 : Mechanism switch
      {
        SteeringMechanism.Ackermann => 0.0f,
        SteeringMechanism.BellCrank => 0.0f,
        SteeringMechanism.RackPinion => 0.0f,
        SteeringMechanism.Davis => 76.0f * Mathf.Deg2Rad,
        _ => 0
      };
      var lr = Parameters != null ? Parameters.Lr : Mechanism switch
      {
        SteeringMechanism.Ackermann => 0.0f,
        SteeringMechanism.BellCrank => 0.0f,
        SteeringMechanism.RackPinion => 0.25f,
        SteeringMechanism.Davis => 0.75f,
        _ => 0.0f
      };
      var lc = Parameters != null ? Parameters.Lc : Mechanism switch
      {
        SteeringMechanism.Ackermann => 0.0f,
        SteeringMechanism.BellCrank => 1.0f,
        SteeringMechanism.RackPinion => 1.0f,
        SteeringMechanism.Davis => -2.0f,
        _ => 0.0f
      };

      // Set implicit parameters 
      if ( Mechanism == SteeringMechanism.BellCrank )
        lr = 0.0f;

      if ( Mechanism == SteeringMechanism.Davis )
        alpha0 = (float)( agxMath.PI - phi0 );

      double[] betas = { agxMath.PI - phi0, phi0 };
      double wheelTrackLength = (rightWJ - leftWJ).magnitude;
      var b = wheelTrackLength * l;
      var rackLength = wheelTrackLength * lr;
      var steerArmLength = lc * l * wheelTrackLength;
      var m_R = ( 1 + 2 * l * Mathf.Cos( phi0 ) - lr ) * wheelTrackLength / 2.0 / Mathf.Cos( alpha0 );

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
        Vec3 tieRodPos = new Vec3( leftLocal.ToHandedVec3() + new Quat( betas[ 0 ], steeringAxis ) * wheelAxis * b + new Quat( alpha0, steeringAxis ) * wheelAxis * m_R );
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
  }
}
