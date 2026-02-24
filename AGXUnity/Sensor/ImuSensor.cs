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
  // Data class to store IMU sensor attachment configuration
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
    [field: SerializeField]
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
      Type = type;
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
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#simulating-imu-sensors" )]
  public class ImuSensor : ScriptComponent
  {
    /// <summary>
    /// Native instance, created in Start/Initialize.
    /// </summary>
    public IMU Native { get; private set; } = null;
    private IMUModel m_nativeModel = null;

    /// <summary>
    /// When enabled, show configuration for the IMU attachment and create attachment when initializing object
    /// </summary>
    [field: SerializeField]
    [Tooltip( "When enabled, show configuration for the IMU attachment and create attachment when initializing object." )]
    [DisableInRuntimeInspector]
    public bool EnableAccelerometer { get; set; } = true;

    /// <summary>
    /// Accelerometer sensor attachment
    /// </summary>
    [field: SerializeField]
    [DynamicallyShowInInspector( "EnableAccelerometer" )]
    [DisableInRuntimeInspector]
    public ImuAttachment AccelerometerAttachment { get; private set; } = new ImuAttachment(
      ImuAttachment.ImuAttachmentType.Accelerometer,
      new TriaxialRangeData(),
      0.01f,
      Vector3.zero );

    /// <summary>
    /// When enabled, show configuration for the IMU attachment and create attachment when initializing object
    /// </summary>
    [field: SerializeField]
    [Tooltip( "When enabled, show configuration for the IMU attachment and create attachment when initializing object." )]
    [DisableInRuntimeInspector]
    public bool EnableGyroscope { get; set; } = true;

    /// <summary>
    /// Gyroscope sensor attachment
    /// </summary>
    [field: SerializeField]
    [DynamicallyShowInInspector( "EnableGyroscope" )]
    [DisableInRuntimeInspector]
    public ImuAttachment GyroscopeAttachment { get; private set; } = new ImuAttachment(
      ImuAttachment.ImuAttachmentType.Gyroscope,
      new TriaxialRangeData(),
      0.01f,
      Vector3.zero );

    /// <summary>
    /// When enabled, show configuration for the IMU attachment and create attachment when initializing object
    /// </summary>
    [field: SerializeField]
    [Tooltip( "When enabled, show configuration for the IMU attachment and create attachment when initializing object." )]
    [DisableInRuntimeInspector]
    public bool EnableMagnetometer { get; set; } = true;

    /// <summary>
    /// Magnetometer sensor attachment
    /// </summary>
    [field: SerializeField]
    [DynamicallyShowInInspector( "EnableMagnetometer" )]
    [DisableInRuntimeInspector]
    public ImuAttachment MagnetometerAttachment { get; private set; } = new ImuAttachment(
      ImuAttachment.ImuAttachmentType.Magnetometer,
      new TriaxialRangeData(),
      0.01f,
      Vector3.one * 0f );

    [RuntimeValue] public RigidBody TrackedRigidBody { get; private set; }
    [RuntimeValue] public Vector3 OutputRow1;
    [RuntimeValue] public Vector3 OutputRow2;
    [RuntimeValue] public Vector3 OutputRow3;

    private uint m_outputID = 0;
    public double[] OutputBuffer { get; private set; }

    protected override bool Initialize()
    {
      SensorEnvironment.Instance.GetInitialized();

      var rigidBody = GetComponentInParent<RigidBody>();
      if (rigidBody == null) 
      {
        Debug.LogWarning( "No Rigidbody found in this object or parents, IMU will be inactive" );
        return false; 
      }
      TrackedRigidBody = rigidBody;

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
          modifiers.Add( new GyroscopeLinearAccelerationEffects(a.LinearAccelerationEffects.ToHandedVec3()) );

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
      if ( EnableMagnetometer ) {
        var modifiers = buildModifiers(MagnetometerAttachment);

        var magnetometer = new MagnetometerModel(
          MagnetometerAttachment.TriaxialRange.GenerateTriaxialRange(),
          new TriaxialCrossSensitivity( MagnetometerAttachment.CrossAxisSensitivity ),
          MagnetometerAttachment.ZeroBias.ToHandedVec3(),
          modifiers
        );

        imu_attachments.Add( new IMUModelMagnetometerAttachment( AffineMatrix4x4.identity(), magnetometer ) );
      }

      if ( imu_attachments.Count == 0 ) {
        Debug.LogWarning( "No sensor attachments, IMU will be inactive" );
        return false;
      }

      m_nativeModel = new IMUModel( imu_attachments );

      if ( m_nativeModel == null )
      {
        Debug.LogWarning( "Could not create native imu model, IMU will be inactive" );
        return false;
      }

      PropertySynchronizer.Synchronize( this );

      var measuredRB = rigidBody.GetInitialized<RigidBody>().Native;
      SensorEnvironment.Instance.Native.add( measuredRB );

      var rbFrame = measuredRB.getFrame();
      if ( rbFrame == null ) 
      {
        Debug.LogWarning( "Could not get rigid body frame, IMU will be inactive" );
        return false;
      }
      Native = new IMU( rbFrame, m_nativeModel );

      // For SWIG reasons, we will create a ninedof output and use the fields selectively
      m_outputID = SensorEnvironment.Instance.GenerateOutputID();
      uint outputCount = 0;
      outputCount += EnableAccelerometer ? Utils.Math.PopCount( (uint)AccelerometerAttachment.OutputFlags ) : 0;
      outputCount += EnableGyroscope ? Utils.Math.PopCount( (uint)GyroscopeAttachment.OutputFlags ) : 0;
      outputCount += EnableMagnetometer ? Utils.Math.PopCount( (uint)MagnetometerAttachment.OutputFlags ) : 0;

      var output = new IMUOutputNineDoF();
      Native.getOutputHandler().add( m_outputID, output );

      OutputBuffer = new double[ outputCount ];

      Simulation.Instance.StepCallbacks.PostStepForward += OnPostStepForward;

      SensorEnvironment.Instance.Native.add( Native );

      return true;
    }

    public void GetOutput(IMUOutput output, double[] buffer)
    {
      if ( Native == null || buffer == null || output == null ) {
        Debug.LogError( "Null problem" );
        return;
      }

      NineDoFView views = output.viewNineDoF();
      if ( views == null ) {
        Debug.LogWarning( "No views" );
        return;
      }

      NineDoFValue view = views[ 0 ];

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
        if ( ( a & OutputXYZ.X ) != 0 ) buffer[ i++ ] = triplets[ j ].x;
        if ( ( a & OutputXYZ.Y ) != 0 ) buffer[ i++ ] = triplets[ j ].y;
        if ( ( a & OutputXYZ.Z ) != 0 ) buffer[ i++ ] = triplets[ j ].z;
        j++;
      }

      if ( EnableGyroscope ) {
        var g = GyroscopeAttachment.OutputFlags;
        if ( ( g & OutputXYZ.X ) != 0 ) buffer[ i++ ] = triplets[ j ].x;
        if ( ( g & OutputXYZ.Y ) != 0 ) buffer[ i++ ] = triplets[ j ].y;
        if ( ( g & OutputXYZ.Z ) != 0 ) buffer[ i++ ] = triplets[ j ].z;
        j++;
      }

      if ( EnableMagnetometer ) {
        var m = MagnetometerAttachment.OutputFlags;
        if ( ( m & OutputXYZ.X ) != 0 ) buffer[ i++ ] = triplets[ j ].x;
        if ( ( m & OutputXYZ.Y ) != 0 ) buffer[ i++ ] = triplets[ j ].y;
        if ( ( m & OutputXYZ.Z ) != 0 ) buffer[ i++ ] = triplets[ j ].z;
      }
    }

    private void OnPostStepForward()
    {
      if ( !gameObject.activeInHierarchy )
        return;

      GetOutput(Native.getOutputHandler().get(m_outputID), OutputBuffer);

      if ( Application.isEditor ) {
        for ( int i = 0; i < OutputBuffer.Length; i++ ) {
          if ( i < 3 )
            OutputRow1[ i ] = (float)OutputBuffer[ i ];
          else if ( i < 6 )
            OutputRow2[ i % 3 ] = (float)OutputBuffer[ i ];
          else
            OutputRow3[ i % 6 ] = (float)OutputBuffer[ i ];
        }
      }
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

      Native?.Dispose();
      Native = null;
      m_nativeModel?.Dispose();
      m_nativeModel = null;

      base.OnDestroy();
    }


    [NonSerialized]
    private Mesh m_nodeGizmoMesh = null;

    private void OnDrawGizmosSelected()
    {
#if UNITY_EDITOR
      if ( m_nodeGizmoMesh == null )
        m_nodeGizmoMesh = Resources.Load<Mesh>( @"Debug/Models/HalfSphere" );
        
      if ( m_nodeGizmoMesh == null )
        return;

      Gizmos.color = Color.yellow;
      Gizmos.DrawWireMesh( m_nodeGizmoMesh,
                      transform.position,
                      transform.rotation,
                      Vector3.one * 0.2f );
#endif
    }
  }
}
