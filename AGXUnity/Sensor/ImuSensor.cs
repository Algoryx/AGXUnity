using agx;
using agxSensor;
using AGXUnity.Utils;
using UnityEngine;
using System;

namespace AGXUnity.Sensor
{
  [Serializable]
  [Flags]
  public enum OutputXYZ
  {
    None = 0,
    X = 1 << 0,
    Y = 1 << 1,
    Z = 1 << 2,
  }
  [Serializable]
  // TODO name could be ImuAttachmentConfig
  public class ImuAttachment
  {
    public enum ImuAttachmentType
    {
      Accelerometer,
      Gyroscope,
      Magnetometer
    }

    public ImuAttachmentType type { get; private set; }

    /// <summary>
    /// Detectable measurement range, in m/s^2 / radians/s / T
    /// </summary>
    [Tooltip("Measurement range - values outside of range will be truncated")]
    // TODO should really be three ranges, not 1... Special type needed, or maybe rangeX, rangeY, rangeZ
    // Could also be a bool for using maxrange
    //public Vec2 TriaxialRange = new Vec2(double.MinValue, double.MaxValue); // TODO double??
    public TriaxialRangeData TriaxialRange;

    /// <summary>
    /// Cross axis sensitivity
    /// </summary>
    [Tooltip("Cross axis sensitivity")]
    // TODO float or Matrix3x3...
    public float CrossAxisSensitivity;

    /// <summary>
    /// Bias reported in each axis under conditions without externally applied transformation
    /// </summary>
    [Tooltip("Bias reported in each axis under conditions without externally applied transformation")]
    public float ZeroRateBias;

    /// <summary>
    /// Applies an offset to the zero rate bias depending on the linear acceleration that the gyroscope is exposed to
    /// </summary>
    [Tooltip("Offset to the zero rate bias depending on the linear acceleration")]
    // TODO could be a matrix3x3
    public Vector3 LinearAccelerationEffects;

    /// <summary>
    /// Output flags - which, if any, of x y z should be used in output view
    /// </summary>
    public OutputXYZ OutputFlags = OutputXYZ.X | OutputXYZ.Y;// | OutputXYZ.Z;

    // Constructor to set different default values for different sensor types
    public ImuAttachment( ImuAttachmentType type, TriaxialRangeData triaxialRange, float crossAxisSensitivity, float zeroRateBias )
    {
      this.type = type;
      TriaxialRange = triaxialRange;
      CrossAxisSensitivity = crossAxisSensitivity;
      ZeroRateBias = zeroRateBias;
    }
  }

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
    /// When enabled, show configuration for the IMU attachment and create attachment when initializing object
    /// </summary>
    [field: SerializeField]
    [Tooltip( "When enabled, show configuration for the IMU attachment and create attachment when initializing object." )]
    [DisableInRuntimeInspector]
    public bool EnableAccelerometer { get; set; } = true;

    /// <summary>
    /// Accelerometer TODO
    /// </summary>
    [field: SerializeField]
    [DynamicallyShowInInspector( "EnableAccelerometer" )]
    [DisableInRuntimeInspector]
    public ImuAttachment AccelerometerAttachment { get; private set; } = new ImuAttachment(
      ImuAttachment.ImuAttachmentType.Accelerometer,
      new TriaxialRangeData(),
      0.01f,
      260f );

    /// <summary>
    /// When enabled, show configuration for the IMU attachment and create attachment when initializing object
    /// </summary>
    [field: SerializeField]
    [Tooltip( "When enabled, show configuration for the IMU attachment and create attachment when initializing object." )]
    [DisableInRuntimeInspector]
    public bool EnableGyroscope { get; set; } = true;

    /// <summary>
    /// Gyroscope TODO
    /// </summary>
    [field: SerializeField]
    [DynamicallyShowInInspector( "EnableGyroscope" )]
    [DisableInRuntimeInspector]
    public ImuAttachment GyroscopeAttachment { get; private set; } = new ImuAttachment(
      ImuAttachment.ImuAttachmentType.Gyroscope,
      new TriaxialRangeData(),
      0.01f,
      3f );

    /// <summary>
    /// When enabled, show configuration for the IMU attachment and create attachment when initializing object
    /// </summary>
    [field: SerializeField]
    [Tooltip( "When enabled, show configuration for the IMU attachment and create attachment when initializing object." )]
    [DisableInRuntimeInspector]
    public bool EnableMagnetometer { get; set; } = true;

    /// <summary>
    /// Magnetometer TODO
    /// </summary>
    [field: SerializeField]
    [DynamicallyShowInInspector( "EnableMagnetometer" )]
    [DisableInRuntimeInspector]
    public ImuAttachment MagnetometerAttachment { get; private set; } = new ImuAttachment(
      ImuAttachment.ImuAttachmentType.Magnetometer,
      new TriaxialRangeData(),
      0.01f,
      0f );

    [RuntimeValue("m/s")] public int test = 3;

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
    private double[] m_outputBuffer;

    protected override bool Initialize()
    {
      SensorEnvironment.Instance.GetInitialized();

      // TODO do we need to save these in private vars? We'll see

      var imu_attachments = new IMUModelSensorAttachmentRefVector();

      // Accelerometer
      if ( EnableAccelerometer ) {
        var modifiers = new ITriaxialSignalSystemNodeRefVector
        {
          //new TriaxialSpectralGaussianNoise( new Vec3( 1.75 * 1e-4 ) )
        };

        var accelerometer = new AccelerometerModel(
          AccelerometerAttachment.TriaxialRange.GenerateTriaxialRange(),
          new TriaxialCrossSensitivity( AccelerometerAttachment.CrossAxisSensitivity ),
          new Vec3( AccelerometerAttachment.ZeroRateBias ),
          modifiers
        );

        imu_attachments.Add( new IMUModelAccelerometerAttachment( AffineMatrix4x4.identity(), accelerometer ) );
      }

      // Gyroscope
      if ( EnableGyroscope ) {
        var gyroscope_modifiers = new ITriaxialSignalSystemNodeRefVector
        {
          //new GyroscopeLinearAccelerationEffects( new Vec3( 2.62 * 1e-4 ) ),
          //new TriaxialSpectralGaussianNoise( new Vec3( 1.75 * 1e-4 ) )
        };

        var gyroscope = new GyroscopeModel(
          GyroscopeAttachment.TriaxialRange.GenerateTriaxialRange(),
          new TriaxialCrossSensitivity( GyroscopeAttachment.CrossAxisSensitivity ),
          new Vec3( GyroscopeAttachment.ZeroRateBias ),
          gyroscope_modifiers
        );

        imu_attachments.Add( new IMUModelGyroscopeAttachment( AffineMatrix4x4.identity(), gyroscope ) );
      }

      // magnetometer
      MagnetometerModel magnetometer = null;
      if ( EnableMagnetometer ) {
        var magnetometer_modifiers = new ITriaxialSignalSystemNodeRefVector
        {
          //new magnetometerLinearAccelerationEffects( new Vec3( 2.62 * 1e-4 ) ),
          //new TriaxialSpectralGaussianNoise( new Vec3( 1.75 * 1e-4 ) )
        };

        magnetometer = new MagnetometerModel(
          MagnetometerAttachment.TriaxialRange.GenerateTriaxialRange(),
          new TriaxialCrossSensitivity( MagnetometerAttachment.CrossAxisSensitivity ),
          new Vec3( MagnetometerAttachment.ZeroRateBias ),
          magnetometer_modifiers
        );

        imu_attachments.Add( new IMUModelMagnetometerAttachment( AffineMatrix4x4.identity(), magnetometer ) );
      }

      if ( imu_attachments.Count == 0 ) {
        Debug.LogWarning( "No sensor attachments on IMU means the component will do nothing" );
        return true;
      }

      m_nativeModel = new IMUModel( imu_attachments );

      if ( m_nativeModel == null )
        return false; // TODO error

      PropertySynchronizer.Synchronize( this );

      // TODO moveme to function that can get called both when setting RB and from here
      var measuredRB = MeasuredBody.GetInitialized<RigidBody>().Native;
      SensorEnvironment.Instance.Native.add( measuredRB );

      var rbFrame = measuredRB.getFrame();
      if ( rbFrame == null )
        Debug.LogError( "Need RB to follow" );

      Native = new IMU( rbFrame, m_nativeModel );

      // For SWIG reasons, we will create a ninedof output and use the fields selectively
      m_outputID = SensorEnvironment.Instance.GenerateOutputID();
      m_output = new IMUOutputNineDoF();
      Native.getOutputHandler().add( m_outputID, m_output );
      uint outputCount = 0;
      outputCount += EnableAccelerometer ? Utils.Math.PopCount( (uint)AccelerometerAttachment.OutputFlags ) : 0;
      outputCount += EnableGyroscope ? Utils.Math.PopCount( (uint)GyroscopeAttachment.OutputFlags ) : 0;
      outputCount += EnableMagnetometer ? Utils.Math.PopCount( (uint)MagnetometerAttachment.OutputFlags ) : 0;
      Debug.Log( "Enabled field count: " + outputCount ); // TODO removeme
      m_outputBuffer = new double[ outputCount ];

      //Simulation.Instance.StepCallbacks.PreSynchronizeTransforms += Sync;
      Simulation.Instance.StepCallbacks.PostStepForward += OnPostStepForward;

      SensorEnvironment.Instance.Native.add( Native );

      return true;
    }

    public void GetOutput()
    {
      if ( Native == null )
        return;

      NineDoFValue view = Native.getOutputHandler().get(m_outputID).viewNineDoF()[0];

      int i = 0;

      if ( EnableAccelerometer ) {
        var a = AccelerometerAttachment.OutputFlags;
        if ( ( a & OutputXYZ.X ) != 0 ) m_outputBuffer[ i++ ] = view.x0;
        if ( ( a & OutputXYZ.Y ) != 0 ) m_outputBuffer[ i++ ] = view.y0;
        if ( ( a & OutputXYZ.Z ) != 0 ) m_outputBuffer[ i++ ] = view.z0;
      }

      if ( EnableGyroscope ) {
        var g = GyroscopeAttachment.OutputFlags;
        if ( ( g & OutputXYZ.X ) != 0 ) m_outputBuffer[ i++ ] = view.x1;
        if ( ( g & OutputXYZ.Y ) != 0 ) m_outputBuffer[ i++ ] = view.y1;
        if ( ( g & OutputXYZ.Z ) != 0 ) m_outputBuffer[ i++ ] = view.z1;
      }

      if ( EnableMagnetometer ) {
        var m = MagnetometerAttachment.OutputFlags;
        if ( ( m & OutputXYZ.X ) != 0 ) m_outputBuffer[ i++ ] = view.x2;
        if ( ( m & OutputXYZ.Y ) != 0 ) m_outputBuffer[ i++ ] = view.y2;
        if ( ( m & OutputXYZ.Z ) != 0 ) m_outputBuffer[ i++ ] = view.z2;
      }
    }

    // TODO removeme used for testing
    private void OnPostStepForward()
    {
      GetOutput();

      string output = "";
      for ( int i = 0; i < m_outputBuffer.Length; i++ )
        output += m_outputBuffer[ i ].ToString() + " ";

      Debug.Log( "Output: " + output );
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

      if ( Simulation.HasInstance ) {
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
