using agx;
using agxSensor;
using AGXUnity.Utils;
using UnityEngine;

namespace AGXUnity.Sensor
{

  /// <summary>
  /// IMU Sensor Component
  /// </summary>
  [DisallowMultipleComponent]
  [AddComponentMenu( "AGXUnity/Sensors/IMU Sensor" )]
  //[HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#simulating-lidar-sensors" )]
  public class ImuSensor : ScriptComponent
  {
    /// <summary>
    /// Native instance, created in Start/Initialize.
    /// </summary>
    public IMU Native { get; private set; } = null;
    public IMUModel m_nativeModel = null;

    /// <summary>
    /// Local sensor rotation relative to the parent GameObject transform.
    /// </summary>
    [Tooltip("Local sensor rotation relative to the parent GameObject transform.")]
    public Vector3 LocalRotation = Vector3.zero;

    /// <summary>
    /// Local sensor offset relative to the parent GameObject transform.
    /// </summary>
    [Tooltip("Local sensor offset relative to the parent GameObject transform.")]
    public Vector3 LocalPosition = Vector3.zero;

    /// <summary>
    /// The local transformation matrix from the sensor frame to the parent GameObject frame
    /// </summary>
    public UnityEngine.Matrix4x4 LocalTransform => UnityEngine.Matrix4x4.TRS( LocalPosition, Quaternion.Euler( LocalRotation ), Vector3.one );

    /// <summary>
    /// The global transformation matrix from the sensor frame to the world frame. 
    /// </summary>
    public UnityEngine.Matrix4x4 GlobalTransform => transform.localToWorldMatrix * LocalTransform;

    // TODO tidy up etc
    public RigidBody MeasuredBody = null;

    //TODO probably move to own class
    private uint m_outputID = 0; // Must be greater than 0 to be valid
    private IMUOutputNineDoF m_output = null;

    //private void Sync()
    //{
    //  var xform = GlobalTransform;
//
    //  Native.getFrame().setTranslate( xform.GetPosition().ToHandedVec3() );
    //  Native.getFrame().setRotate( xform.rotation.ToHandedQuat() );
//
//
    //  //Debug.Log( Native.getFrame().getTranslate() );
    //  //Native.setFrame( new Frame(
    //  //                    new AffineMatrix4x4(
    //  //                      xform.rotation.ToHandedQuat(),
    //  //                      xform.GetPosition().ToHandedVec3() ) ) );
    //}

    protected override bool Initialize()
    {
      SensorEnvironment.Instance.GetInitialized();

      // TODO save the models in private vars to update values

      // Accelerometer
      var accelerometer_modifiers = new ITriaxialSignalSystemNodeRefVector
      {
        //new GyroscopeLinearAccelerationEffects( new Vec3( 2.62 * 1e-4 ) ),
        //new TriaxialSpectralGaussianNoise( new Vec3( 1.75 * 1e-4 ) )
      };

      var accelerometer_model = new AccelerometerModel(
        new TriaxialRange(),
        new TriaxialCrossSensitivity(0.0),
        new Vec3(0.0),
        accelerometer_modifiers
      );

      // Gyroscope
      var gyroscope_modifiers = new ITriaxialSignalSystemNodeRefVector {
        //new GyroscopeLinearAccelerationEffects( new Vec3( 2.62 * 1e-4 ) ),
        //new TriaxialSpectralGaussianNoise( new Vec3( 1.75 * 1e-4 ) )
      };

      var gyroscope_model = new GyroscopeModel(
        new TriaxialRange(new agx.RangeReal(-1, 1)),
        new TriaxialCrossSensitivity(0.01),
        new Vec3(0.0),
        gyroscope_modifiers
      );

      // Magnetometer
      var magnetometer_modifiers = new ITriaxialSignalSystemNodeRefVector {
        //new TriaxialGaussianNoise( new Vec3( 4.5e-7 ) )
      };

      var magnetometer_model = new MagnetometerModel(
        new TriaxialRange(),
        new TriaxialCrossSensitivity(0.01),
        new Vec3(0.0),
        magnetometer_modifiers
      );

      var imu_attachments = new IMUModelSensorAttachmentRefVector
      {
        new IMUModelAccelerometerAttachment(
          new AffineMatrix4x4(), accelerometer_model
        ),
        new IMUModelGyroscopeAttachment(
          new AffineMatrix4x4(), gyroscope_model
        ),
        new IMUModelMagnetometerAttachment(
          new AffineMatrix4x4(), magnetometer_model
        )
      };

      m_nativeModel = IMUModel.makeIdealNineDoFModel();
      //m_nativeModel = new IMUModel( imu_attachments );

      if ( m_nativeModel == null )
        return false;

      PropertySynchronizer.Synchronize( this );

      // TODO moveme to function that can get called both when setting RB and from here
      var measuredRB = MeasuredBody.GetInitialized<RigidBody>().Native;
      SensorEnvironment.Instance.Native.add( measuredRB );      

      var rbFrame = measuredRB.getFrame();
      if ( rbFrame == null )
        Debug.LogError( "Need RB to follow" );

      Native = new IMU( rbFrame, m_nativeModel );

      // TODO temp depth firsth output implementation
      m_outputID = SensorEnvironment.Instance.GenerateOutputID();
      m_output = new IMUOutputNineDoF();
      Native.getOutputHandler().add( m_outputID, m_output );

      //Simulation.Instance.StepCallbacks.PreSynchronizeTransforms += Sync;
      Simulation.Instance.StepCallbacks.PostStepForward += OnPostStepForward;

      SensorEnvironment.Instance.Native.add( Native );

      return true;
    }
    
    // TODO removeme used for testing
    private void OnPostStepForward()
    {
      NineDoFView view = Native.getOutputHandler().get( m_outputID ).viewNineDoF();

      // Debug.Log( "Test: " + view.size()); // Returns 1

      Debug.Log( "Pos: " + Native.getFrame().getTranslate() );

      Debug.Log( "Accelerometer 0: " + view[ 0 ].getTriplet( 0 ).length() );
      Debug.Log( "Gyroscope 1: " + view[ 0 ].getTriplet( 1 ).length() );
      Debug.Log( "Magnetometer 2: " + view[ 0 ].getTriplet( 2 ).length() );
    }

    protected override void OnEnable()
    {
      Native?.setEnable( true );
    }

    protected override void OnDisable()
    {
      Native?.setEnable( false );
    }


    protected override void OnDestroy()
    {
      if ( SensorEnvironment.HasInstance )
        SensorEnvironment.Instance.Native?.remove( Native );

      if ( Simulation.HasInstance ) 
      {
        //Simulation.Instance.StepCallbacks.PreSynchronizeTransforms -= Sync;
        Simulation.Instance.StepCallbacks.PostStepForward -= OnPostStepForward;
      }

      //TODO remove modules and modifiers, possibly
      m_output.Dispose();

      Native?.Dispose();
      Native = null;
      m_nativeModel?.Dispose();
      m_nativeModel = null;

      base.OnDestroy();
    }

    //TODO draw something useful
    private void OnDrawGizmosSelected()
    {
#if UNITY_EDITOR
      var xform = GlobalTransform;

      var pos = xform.GetPosition();
      var scale = UnityEditor.HandleUtility.GetHandleSize(pos) * 1.5f;
      Gizmos.DrawLine( pos, xform.MultiplyPoint( Vector3.forward * scale ) ); // Up
      Gizmos.DrawLine( pos, xform.MultiplyPoint( Vector3.left * scale ) );

      int numPoints = 25;
      Vector3[] disc = new Vector3[numPoints];

      Vector3 x = xform.MultiplyVector(Vector3.right * scale);
      Vector3 y = xform.MultiplyVector(Vector3.up * scale);

      for ( int i = 0; i < numPoints; i++ ) {
        float ang = Mathf.PI * 2 * i / numPoints;
        disc[ i ] = pos + x * Mathf.Cos( ang ) + y * Mathf.Sin( ang );
      }
      Gizmos.DrawLineStrip( disc, true );
#endif
    }
  }
}
