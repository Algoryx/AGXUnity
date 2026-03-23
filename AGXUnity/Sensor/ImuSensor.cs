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
    [NonSerialized]
    private Action m_onBaseSettingsChanged = null;
    [NonSerialized]
    private Action m_onModifierSettingsChanged = null;

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
    [SerializeField]
    private TriaxialRangeData m_triaxialRange;
    public TriaxialRangeData TriaxialRange
    {
      get => m_triaxialRange;
      set
      {
        m_triaxialRange = value;
        m_triaxialRange?.SetOnChanged( m_onBaseSettingsChanged );
        m_onBaseSettingsChanged?.Invoke();
      }
    }

    /// <summary>
    /// Cross axis sensitivity - how measurements in one axis affects the other axes. Ratio 0 to 1.
    /// </summary>
    [SerializeField]
    private float m_crossAxisSensitivity;
    public float CrossAxisSensitivity
    {
      get => m_crossAxisSensitivity;
      set
      {
        m_crossAxisSensitivity = value;
        m_onBaseSettingsChanged?.Invoke();
      }
    }

    /// <summary>
    /// Bias reported in each axis under conditions without externally applied transformation
    /// </summary>
    [SerializeField]
    private Vector3 m_zeroBias;
    public Vector3 ZeroBias
    {
      get => m_zeroBias;
      set
      {
        m_zeroBias = value;
        m_onBaseSettingsChanged?.Invoke();
      }
    }

    [SerializeField]
    private bool m_enableLinearAccelerationEffects = false;
    public bool EnableLinearAccelerationEffects
    {
      get => m_enableLinearAccelerationEffects;
      set
      {
        m_enableLinearAccelerationEffects = value;
        m_onModifierSettingsChanged?.Invoke();
      }
    }
    /// <summary>
    /// Applies an offset to the zero rate bias depending on the linear acceleration that the gyroscope is exposed to
    /// </summary>
    [SerializeField]
    private Vector3 m_linearAccelerationEffects;
    public Vector3 LinearAccelerationEffects
    {
      get => m_linearAccelerationEffects;
      set
      {
        m_linearAccelerationEffects = value;
        m_onModifierSettingsChanged?.Invoke();
      }
    }

    [SerializeField]
    private bool m_enableTotalGaussianNoise = false;
    public bool EnableTotalGaussianNoise
    {
      get => m_enableTotalGaussianNoise;
      set
      {
        m_enableTotalGaussianNoise = value;
        m_onModifierSettingsChanged?.Invoke();
      }
    }
    /// <summary>
    /// Base level noise in the measurement signal 
    /// </summary>
    [SerializeField]
    private Vector3 m_totalGaussianNoise = Vector3.zero;
    public Vector3 TotalGaussianNoise
    {
      get => m_totalGaussianNoise;
      set
      {
        m_totalGaussianNoise = value;
        m_onModifierSettingsChanged?.Invoke();
      }
    }

    [SerializeField]
    private bool m_enableSignalScaling = false;
    public bool EnableSignalScaling
    {
      get => m_enableSignalScaling;
      set
      {
        m_enableSignalScaling = value;
        m_onModifierSettingsChanged?.Invoke();
      }
    }
    /// <summary>
    /// Constant scaling to the triaxial signal
    /// </summary>
    [SerializeField]
    private Vector3 m_signalScaling = Vector3.one;
    public Vector3 SignalScaling
    {
      get => m_signalScaling;
      set
      {
        m_signalScaling = value;
        m_onModifierSettingsChanged?.Invoke();
      }
    }

    [SerializeField]
    private bool m_enableGaussianSpectralNoise = false;
    public bool EnableGaussianSpectralNoise
    {
      get => m_enableGaussianSpectralNoise;
      set
      {
        m_enableGaussianSpectralNoise = value;
        m_onModifierSettingsChanged?.Invoke();
      }
    }
    /// <summary>
    /// Sample frequency dependent Gaussian noise
    /// </summary>
    [SerializeField]
    private Vector3 m_gaussianSpectralNoise = Vector3.zero;
    public Vector3 GaussianSpectralNoise
    {
      get => m_gaussianSpectralNoise;
      set
      {
        m_gaussianSpectralNoise = value;
        m_onModifierSettingsChanged?.Invoke();
      }
    }

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

    internal void SetRuntimeCallbacks( Action onBaseSettingsChanged, Action onModifierSettingsChanged )
    {
      m_onBaseSettingsChanged = onBaseSettingsChanged;
      m_onModifierSettingsChanged = onModifierSettingsChanged;
      m_triaxialRange?.SetOnChanged( m_onBaseSettingsChanged );
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
    private static readonly Vector3 DisabledTotalGaussianNoise = Vector3.zero;
    private static readonly Vector3 DisabledSignalScaling = Vector3.one;
    private static readonly Vector3 DisabledGaussianSpectralNoise = Vector3.zero;
    private static readonly Vector3 DisabledLinearAccelerationEffects = Vector3.zero;

    /// <summary>
    /// Native instance, created in Start/Initialize.
    /// </summary>
    public IMU Native { get; private set; } = null;
    private IMUModel m_nativeModel = null;

    private AccelerometerModel m_accelerometerModel = null;
    private GyroscopeModel m_gyroscopeModel = null;
    private MagnetometerModel m_magnetometerModel = null;

    private ITriaxialSignalSystemNodeRefVector m_accelerometerModifiers = null;
    private ITriaxialSignalSystemNodeRefVector m_gyroscopeModifiers = null;
    private ITriaxialSignalSystemNodeRefVector m_magnetometerModifiers = null;

    private TriaxialGaussianNoise m_accelerometerTotalGaussianNoiseModifier = null;
    private TriaxialSignalScaling m_accelerometerSignalScalingModifier = null;
    private TriaxialSpectralGaussianNoise m_accelerometerGaussianSpectralNoiseModifier = null;

    private TriaxialGaussianNoise m_gyroscopeTotalGaussianNoiseModifier = null;
    private TriaxialSignalScaling m_gyroscopeSignalScalingModifier = null;
    private TriaxialSpectralGaussianNoise m_gyroscopeGaussianSpectralNoiseModifier = null;
    private GyroscopeLinearAccelerationEffects m_gyroscopeLinearAccelerationEffectsModifier = null;

    private TriaxialGaussianNoise m_magnetometerTotalGaussianNoiseModifier = null;
    private TriaxialSignalScaling m_magnetometerSignalScalingModifier = null;
    private TriaxialSpectralGaussianNoise m_magnetometerGaussianSpectralNoiseModifier = null;

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
    [DynamicallyShowInInspector( nameof( EnableAccelerometer ) )]
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
    [DynamicallyShowInInspector( nameof( EnableGyroscope ) )]
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
    [DynamicallyShowInInspector( nameof( EnableMagnetometer ) )]
    public ImuAttachment MagnetometerAttachment { get; private set; } = new ImuAttachment(
      ImuAttachment.ImuAttachmentType.Magnetometer,
      new TriaxialRangeData(),
      0.01f,
      Vector3.one * 0f );

    [RuntimeValue] public RigidBody TrackedRigidBody { get; private set; }
    [RuntimeValue] public Vector3 OutputRow1 { get; private set; }
    [RuntimeValue] public Vector3 OutputRow2 { get; private set; }
    [RuntimeValue] public Vector3 OutputRow3 { get; private set; }

    private uint m_outputID = 0;
    public double[] OutputBuffer { get; private set; }

    protected override bool Initialize()
    {
      SensorEnvironment.Instance.GetInitialized();

      AccelerometerAttachment.SetRuntimeCallbacks( SynchronizeAccelerometerSettings, SynchronizeAccelerometerModifiers );
      GyroscopeAttachment.SetRuntimeCallbacks( SynchronizeGyroscopeSettings, SynchronizeGyroscopeModifiers );
      MagnetometerAttachment.SetRuntimeCallbacks( SynchronizeMagnetometerSettings, SynchronizeMagnetometerModifiers );

      var rigidBody = GetComponentInParent<RigidBody>();
      if ( rigidBody == null ) {
        Debug.LogWarning( "No Rigidbody found in this object or parents, IMU will be inactive" );
        return false;
      }
      TrackedRigidBody = rigidBody;

      var imu_attachments = new IMUModelSensorAttachmentRefVector();

      // Accelerometer
      if ( EnableAccelerometer ) {
        m_accelerometerModifiers = CreateModifiersVectorForAttachment( AccelerometerAttachment );
        m_accelerometerTotalGaussianNoiseModifier = new TriaxialGaussianNoise( GetTotalGaussianNoise( AccelerometerAttachment ).ToHandedVec3() );
        m_accelerometerSignalScalingModifier = new TriaxialSignalScaling( GetSignalScaling( AccelerometerAttachment ).ToHandedVec3() );
        m_accelerometerGaussianSpectralNoiseModifier = new TriaxialSpectralGaussianNoise( GetGaussianSpectralNoise( AccelerometerAttachment ).ToHandedVec3() );
        m_accelerometerModifiers.Add( m_accelerometerTotalGaussianNoiseModifier );
        m_accelerometerModifiers.Add( m_accelerometerSignalScalingModifier );
        m_accelerometerModifiers.Add( m_accelerometerGaussianSpectralNoiseModifier );

        m_accelerometerModel = new AccelerometerModel(
          AccelerometerAttachment.TriaxialRange.GenerateTriaxialRange(),
          new TriaxialCrossSensitivity( AccelerometerAttachment.CrossAxisSensitivity ),
          AccelerometerAttachment.ZeroBias.ToHandedVec3(),
          m_accelerometerModifiers
        );

        imu_attachments.Add( new IMUModelAccelerometerAttachment( AffineMatrix4x4.identity(), m_accelerometerModel ) );
      }

      // Gyroscope
      if ( EnableGyroscope ) {
        m_gyroscopeModifiers = CreateModifiersVectorForAttachment( GyroscopeAttachment );
        m_gyroscopeTotalGaussianNoiseModifier = new TriaxialGaussianNoise( GetTotalGaussianNoise( GyroscopeAttachment ).ToHandedVec3() );
        m_gyroscopeSignalScalingModifier = new TriaxialSignalScaling( GetSignalScaling( GyroscopeAttachment ).ToHandedVec3() );
        m_gyroscopeGaussianSpectralNoiseModifier = new TriaxialSpectralGaussianNoise( GetGaussianSpectralNoise( GyroscopeAttachment ).ToHandedVec3() );
        m_gyroscopeLinearAccelerationEffectsModifier = new GyroscopeLinearAccelerationEffects( GetLinearAccelerationEffects( GyroscopeAttachment ).ToHandedVec3() );
        m_gyroscopeModifiers.Add( m_gyroscopeTotalGaussianNoiseModifier );
        m_gyroscopeModifiers.Add( m_gyroscopeSignalScalingModifier );
        m_gyroscopeModifiers.Add( m_gyroscopeGaussianSpectralNoiseModifier );
        m_gyroscopeModifiers.Add( m_gyroscopeLinearAccelerationEffectsModifier );

        m_gyroscopeModel = new GyroscopeModel(
          GyroscopeAttachment.TriaxialRange.GenerateTriaxialRange(),
          new TriaxialCrossSensitivity( GyroscopeAttachment.CrossAxisSensitivity ),
          GyroscopeAttachment.ZeroBias.ToHandedVec3(),
          m_gyroscopeModifiers
        );

        imu_attachments.Add( new IMUModelGyroscopeAttachment( AffineMatrix4x4.identity(), m_gyroscopeModel ) );
      }

      // Magnetometer
      if ( EnableMagnetometer ) {
        m_magnetometerModifiers = CreateModifiersVectorForAttachment( MagnetometerAttachment );
        m_magnetometerTotalGaussianNoiseModifier = new TriaxialGaussianNoise( GetTotalGaussianNoise( MagnetometerAttachment ).ToHandedVec3() );
        m_magnetometerSignalScalingModifier = new TriaxialSignalScaling( GetSignalScaling( MagnetometerAttachment ).ToHandedVec3() );
        m_magnetometerGaussianSpectralNoiseModifier = new TriaxialSpectralGaussianNoise( GetGaussianSpectralNoise( MagnetometerAttachment ).ToHandedVec3() );
        m_magnetometerModifiers.Add( m_magnetometerTotalGaussianNoiseModifier );
        m_magnetometerModifiers.Add( m_magnetometerSignalScalingModifier );
        m_magnetometerModifiers.Add( m_magnetometerGaussianSpectralNoiseModifier );

        m_magnetometerModel = new MagnetometerModel(
          MagnetometerAttachment.TriaxialRange.GenerateTriaxialRange(),
          new TriaxialCrossSensitivity( MagnetometerAttachment.CrossAxisSensitivity ),
          MagnetometerAttachment.ZeroBias.ToHandedVec3(),
          m_magnetometerModifiers
        );

        imu_attachments.Add( new IMUModelMagnetometerAttachment( AffineMatrix4x4.identity(), m_magnetometerModel ) );
      }

      if ( imu_attachments.Count == 0 ) {
        Debug.LogWarning( "No sensor attachments, IMU will be inactive" );
        return false;
      }

      m_nativeModel = new IMUModel( imu_attachments );

      if ( m_nativeModel == null ) {
        Debug.LogWarning( "Could not create native imu model, IMU will be inactive" );
        return false;
      }

      var measuredRB = rigidBody.GetInitialized<RigidBody>().Native;
      SensorEnvironment.Instance.Native.add( measuredRB );

      var rbFrame = measuredRB.getFrame();
      if ( rbFrame == null ) {
        Debug.LogWarning( "Could not get rigid body frame, IMU will be inactive" );
        return false;
      }
      Native = new IMU( rbFrame, m_nativeModel );

      // For SWIG reasons, we will create a ninedof output and use the fields selectively
      m_outputID = SensorEnvironment.Instance.GenerateOutputID();
      uint outputCount = 0;
      outputCount += EnableAccelerometer ? Utils.Math.CountEnabledBits( (uint)AccelerometerAttachment.OutputFlags ) : 0;
      outputCount += EnableGyroscope ? Utils.Math.CountEnabledBits( (uint)GyroscopeAttachment.OutputFlags ) : 0;
      outputCount += EnableMagnetometer ? Utils.Math.CountEnabledBits( (uint)MagnetometerAttachment.OutputFlags ) : 0;

      var output = new IMUOutputNineDoF();
      Native.getOutputHandler().add( m_outputID, output );

      OutputBuffer = new double[ outputCount ];

      PropertySynchronizer.Synchronize( this );

      Simulation.Instance.StepCallbacks.PostSynchronizeTransforms += OnPostSynchronizeTransforms;

      SensorEnvironment.Instance.Native.add( Native );

      return true;
    }

    private static ITriaxialSignalSystemNodeRefVector CreateModifiersVectorForAttachment( ImuAttachment attachment )
    {
      return new ITriaxialSignalSystemNodeRefVector();
    }

    private void SynchronizeAccelerometerSettings()
    {
      if ( m_accelerometerModel == null )
        return;

      m_accelerometerModel.setRange( AccelerometerAttachment.TriaxialRange.GenerateTriaxialRange() );
      m_accelerometerModel.setCrossAxisSensitivity( new TriaxialCrossSensitivity( AccelerometerAttachment.CrossAxisSensitivity ) );
      m_accelerometerModel.setZeroGBias( AccelerometerAttachment.ZeroBias.ToHandedVec3() );
    }

    private void SynchronizeGyroscopeSettings()
    {
      if ( m_gyroscopeModel == null )
        return;

      m_gyroscopeModel.setRange( GyroscopeAttachment.TriaxialRange.GenerateTriaxialRange() );
      m_gyroscopeModel.setCrossAxisSensitivity( new TriaxialCrossSensitivity( GyroscopeAttachment.CrossAxisSensitivity ) );
      m_gyroscopeModel.setZeroRateBias( GyroscopeAttachment.ZeroBias.ToHandedVec3() );
    }

    private void SynchronizeMagnetometerSettings()
    {
      if ( m_magnetometerModel == null )
        return;

      m_magnetometerModel.setRange( MagnetometerAttachment.TriaxialRange.GenerateTriaxialRange() );
      m_magnetometerModel.setCrossAxisSensitivity( new TriaxialCrossSensitivity( MagnetometerAttachment.CrossAxisSensitivity ) );
      m_magnetometerModel.setZeroFluxBias( MagnetometerAttachment.ZeroBias.ToHandedVec3() );
    }

    private void SynchronizeAccelerometerModifiers()
    {
      m_accelerometerTotalGaussianNoiseModifier?.setNoiseRms( GetTotalGaussianNoise( AccelerometerAttachment ).ToHandedVec3() );
      m_accelerometerSignalScalingModifier?.setScaling( GetSignalScaling( AccelerometerAttachment ).ToHandedVec3() );
      m_accelerometerGaussianSpectralNoiseModifier?.setNoiseDensity( GetGaussianSpectralNoise( AccelerometerAttachment ).ToHandedVec3() );
    }

    private void SynchronizeGyroscopeModifiers()
    {
      m_gyroscopeTotalGaussianNoiseModifier?.setNoiseRms( GetTotalGaussianNoise( GyroscopeAttachment ).ToHandedVec3() );
      m_gyroscopeSignalScalingModifier?.setScaling( GetSignalScaling( GyroscopeAttachment ).ToHandedVec3() );
      m_gyroscopeGaussianSpectralNoiseModifier?.setNoiseDensity( GetGaussianSpectralNoise( GyroscopeAttachment ).ToHandedVec3() );
      m_gyroscopeLinearAccelerationEffectsModifier?.setAccelerationEffects( GetLinearAccelerationEffects( GyroscopeAttachment ).ToHandedVec3() );
    }

    private void SynchronizeMagnetometerModifiers()
    {
      m_magnetometerTotalGaussianNoiseModifier?.setNoiseRms( GetTotalGaussianNoise( MagnetometerAttachment ).ToHandedVec3() );
      m_magnetometerSignalScalingModifier?.setScaling( GetSignalScaling( MagnetometerAttachment ).ToHandedVec3() );
      m_magnetometerGaussianSpectralNoiseModifier?.setNoiseDensity( GetGaussianSpectralNoise( MagnetometerAttachment ).ToHandedVec3() );
    }

    private static Vector3 GetTotalGaussianNoise( ImuAttachment attachment ) => attachment.EnableTotalGaussianNoise ? attachment.TotalGaussianNoise : DisabledTotalGaussianNoise;

    private static Vector3 GetSignalScaling( ImuAttachment attachment ) => attachment.EnableSignalScaling ? attachment.SignalScaling : DisabledSignalScaling;

    private static Vector3 GetGaussianSpectralNoise( ImuAttachment attachment ) => attachment.EnableGaussianSpectralNoise ? attachment.GaussianSpectralNoise : DisabledGaussianSpectralNoise;

    private static Vector3 GetLinearAccelerationEffects( ImuAttachment attachment ) => attachment.EnableLinearAccelerationEffects ? attachment.LinearAccelerationEffects : DisabledLinearAccelerationEffects;

    private void GetOutput( IMUOutput output, double[] buffer )
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

      // Usually only at first timestep
      if ( views.size() == 0 )
        return;

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

    private void OnPostSynchronizeTransforms()
    {
      if ( !gameObject.activeInHierarchy || Native == null )
        return;

      GetOutput( Native.getOutputHandler().get( m_outputID ), OutputBuffer );

      if ( Application.isEditor ) {
        for ( int i = 0; i < OutputBuffer.Length; i++ ) {
          if ( i < 3 ) {
            var outputRow1 = OutputRow1;
            outputRow1[ i ] = (float)OutputBuffer[ i ];
            OutputRow1 = outputRow1;
          }
          else if ( i < 6 ) {
            var outputRow2 = OutputRow2;
            outputRow2[ i % 3 ] = (float)OutputBuffer[ i ];
            OutputRow2 = outputRow2;
          }
          else {
            var outputRow3 = OutputRow3;
            outputRow3[ i % 6 ] = (float)OutputBuffer[ i ];
            OutputRow3 = outputRow3;
          }
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
        Simulation.Instance.StepCallbacks.PostSynchronizeTransforms -= OnPostSynchronizeTransforms;
      }

      base.OnDestroy();
    }
  }
}
