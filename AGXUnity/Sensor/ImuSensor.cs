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
  // TODO probably move this to inside IMU comp
  public class ImuAttachment
  {
    public enum ImuAttachmentType
    {
      Accelerometer,
      Gyroscope,
      Magnetometer
    }

    /// <summary>
    /// Accelerometer / Gyroscope / Magnetometer
    /// </summary>
    public ImuAttachmentType Type { get; private set; }

    /// <summary>
    /// Detectable measurement range, in m/s^2 / radians/s / T
    /// </summary>
    [Tooltip("Measurement range - values outside of range will be truncated")]
    public TriaxialRangeData TriaxialRange;

    /// <summary>
    /// Cross axis sensitivity
    /// </summary>
    [Tooltip("Cross axis sensitivity")]
    public float CrossAxisSensitivity;

    /// <summary>
    /// Bias reported in each axis under conditions without externally applied transformation
    /// </summary>
    [Tooltip("Bias reported in each axis under conditions without externally applied transformation")]
    public Vector3 ZeroBias;

    public bool EnableLinearAccelerationEffects = false;
    /// <summary>
    /// Applies an offset to the zero rate bias depending on the linear acceleration that the gyroscope is exposed to
    /// </summary>
    [Tooltip("Offset to the zero rate bias depending on the linear acceleration")]
    public Vector3 LinearAccelerationEffects;

    public bool EnableTotalGaussianNoise = false;
    /// <summary>
    /// Base level noise in the measurement signal 
    /// </summary>
    public Vector3 TotalGaussianNoise;

    public bool EnableSignalScaling = false;
    /// <summary>
    /// Constant scaling to the triaxial signal
    /// </summary>
    public Vector3 SignalScaling = Vector3.one;

    public bool EnableGaussianSpectralNoise = false;
    /// <summary>
    /// Sample frequency dependent Gaussian noise
    /// </summary>
    public Vector3 GaussianSpectralNoise;

    /// <summary>
    /// Output flags - which, if any, of x y z should be used in output view
    /// </summary>
    public OutputXYZ OutputFlags = OutputXYZ.X | OutputXYZ.Y | OutputXYZ.Z;

    // Constructor enables us to set different default values per sensor type
    public ImuAttachment( ImuAttachmentType type, TriaxialRangeData triaxialRange, float crossAxisSensitivity, Vector3 zeroRateBias )
    {
      this.Type = type;
      TriaxialRange = triaxialRange;
      CrossAxisSensitivity = crossAxisSensitivity;
      ZeroBias = zeroRateBias;
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
      Vector3.one * 260f );

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
      Vector3.one * 3f );

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
      Vector3.one * 0f );

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
    private double[] m_outputBuffer;

    protected override bool Initialize()
    {
      SensorEnvironment.Instance.GetInitialized();

      Func<ImuAttachment, ITriaxialSignalSystemNodeRefVector> buildModifiers = a =>
      {
        var modifiers = new ITriaxialSignalSystemNodeRefVector();

        if ( a.EnableTotalGaussianNoise )
          modifiers.Add(new TriaxialGaussianNoise(a.TotalGaussianNoise.ToHandedVec3()));
        if ( a.EnableSignalScaling )
          modifiers.Add(new TriaxialSignalScaling(a.SignalScaling.ToHandedVec3()));
        if ( a.EnableGaussianSpectralNoise )
          modifiers.Add(new TriaxialSpectralGaussianNoise(a.GaussianSpectralNoise.ToHandedVec3()) );
        if ( a.EnableLinearAccelerationEffects && a.Type == ImuAttachment.ImuAttachmentType.Gyroscope )
          modifiers.Add( new GyroscopeLinearAccelerationEffects(a.GaussianSpectralNoise.ToHandedVec3()) );

        return modifiers;
      };
      var imu_attachments = new IMUModelSensorAttachmentRefVector();

      // Accelerometer
      if ( EnableAccelerometer ) {
        var modifiers = buildModifiers(AccelerometerAttachment);

        var accelerometer = new AccelerometerModel(
          AccelerometerAttachment.TriaxialRange.GenerateTriaxialRange(),
          new TriaxialCrossSensitivity( AccelerometerAttachment.CrossAxisSensitivity ),
          AccelerometerAttachment.ZeroBias.ToHandedVec3(),
          modifiers
        );

        imu_attachments.Add( new IMUModelAccelerometerAttachment( AffineMatrix4x4.identity(), accelerometer ) );
      }

      // Gyroscope
      if ( EnableGyroscope ) {
        var modifiers = buildModifiers(GyroscopeAttachment);

        var gyroscope = new GyroscopeModel(
          GyroscopeAttachment.TriaxialRange.GenerateTriaxialRange(),
          new TriaxialCrossSensitivity( GyroscopeAttachment.CrossAxisSensitivity ),
          GyroscopeAttachment.ZeroBias.ToHandedVec3(),
          modifiers
        );

        imu_attachments.Add( new IMUModelGyroscopeAttachment( AffineMatrix4x4.identity(), gyroscope ) );
      }

      // Magnetometer
      MagnetometerModel magnetometer = null;
      if ( EnableMagnetometer ) {
        var modifiers = buildModifiers(MagnetometerAttachment);

        magnetometer = new MagnetometerModel(
          MagnetometerAttachment.TriaxialRange.GenerateTriaxialRange(),
          new TriaxialCrossSensitivity( MagnetometerAttachment.CrossAxisSensitivity ),
          MagnetometerAttachment.ZeroBias.ToHandedVec3(),
          modifiers
        );

        imu_attachments.Add( new IMUModelMagnetometerAttachment( AffineMatrix4x4.identity(), magnetometer ) );
      }

      if ( imu_attachments.Count == 0 ) {
        Debug.LogWarning( "No sensor attachments on IMU means the component will do nothing" );
        return true;
      }

      if ( imu_attachments.Count == 1 ) {
        Debug.LogWarning( "KNOWN BUG: currently two sensors are needed for output to work properly! Suggested workaround: Enable another one and disable output" );
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
      uint outputCount = 0;
      outputCount += EnableAccelerometer ? Utils.Math.PopCount( (uint)AccelerometerAttachment.OutputFlags ) : 0;
      outputCount += EnableGyroscope ? Utils.Math.PopCount( (uint)GyroscopeAttachment.OutputFlags ) : 0;
      outputCount += EnableMagnetometer ? Utils.Math.PopCount( (uint)MagnetometerAttachment.OutputFlags ) : 0;

      var output = new IMUOutputNineDoF();
      // output.setPaddingValue( 0 ); // TODO remove all thoughts of padding
      Native.getOutputHandler().add( m_outputID, output );

      Debug.Log( "Enabled field count: " + outputCount ); // TODO removeme
      m_outputBuffer = new double[ outputCount ];

      Simulation.Instance.StepCallbacks.PostStepForward += OnPostStepForward;

      SensorEnvironment.Instance.Native.add( Native );

      return true;
    }

    public void GetOutput()
    {
      if ( Native == null )
        return;

      //var imuOutput = Native.getOutputHandler().get(m_outputID);
      //Debug.Log( "getElementSize: " + imuOutput.getElementSize() + ", hasUnreadData: " + imuOutput.hasUnreadData());
      //Debug.Log( "Tri:  " + imuOutput.viewTriaxialXYZ().size() );
      //Debug.Log( "Six:  " + imuOutput.viewSixDoF().size() );
      //Debug.Log( "Nine: " + imuOutput.viewNineDoF().size() );
      //return;

      NineDoFValue view = Native.getOutputHandler().get(m_outputID).viewNineDoF()[0];

      // This is all kind of a workaround to use a ninedof buffer with an arbitrary number
      // of doubles read based on settings. If Native isn't null we have at least one sensor.
      int i = 0, j = 0;

      var triplets = new Vec3[]
      {
        view.getTriplet( 0 ),
        view.getTriplet( 1 ),
        view.getTriplet( 2 )
      };

      if ( EnableAccelerometer ) {
        var a = AccelerometerAttachment.OutputFlags;
        if ( ( a & OutputXYZ.X ) != 0 ) m_outputBuffer[ i++ ] = triplets[ j ].x;
        if ( ( a & OutputXYZ.Y ) != 0 ) m_outputBuffer[ i++ ] = triplets[ j ].y;
        if ( ( a & OutputXYZ.Z ) != 0 ) m_outputBuffer[ i++ ] = triplets[ j ].z;
        j++;
      }

      if ( EnableGyroscope ) {
        var g = GyroscopeAttachment.OutputFlags;
        if ( ( g & OutputXYZ.X ) != 0 ) m_outputBuffer[ i++ ] = triplets[ j ].x;
        if ( ( g & OutputXYZ.Y ) != 0 ) m_outputBuffer[ i++ ] = triplets[ j ].y;
        if ( ( g & OutputXYZ.Z ) != 0 ) m_outputBuffer[ i++ ] = triplets[ j ].z;
        j++;
      }

      if ( EnableMagnetometer ) {
        var m = MagnetometerAttachment.OutputFlags;
        if ( ( m & OutputXYZ.X ) != 0 ) m_outputBuffer[ i++ ] = triplets[ j ].x;
        if ( ( m & OutputXYZ.Y ) != 0 ) m_outputBuffer[ i++ ] = triplets[ j ].y;
        if ( ( m & OutputXYZ.Z ) != 0 ) m_outputBuffer[ i++ ] = triplets[ j ].z;
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
