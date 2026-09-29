using agx;
using agxSensor;
using AGXUnity.Util;
using AGXUnity.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AGXUnity.Sensor
{
  [Serializable]
  [Flags]
  public enum OutputXYZ { None = 0, X = 1 << 0, Y = 1 << 1, Z = 1 << 2 }

  /// <summary>Common configuration and native lifecycle for an IMU sensor attachment.</summary>
  [Serializable]
  public abstract class ImuSensorSubcomponent : Subcomponent<ImuSensor>
  {
    private static readonly Vector3 DisabledTotalGaussianNoise = Vector3.zero;
    private static readonly Vector3 DisabledSignalScaling = Vector3.one;
    private static readonly Vector3 DisabledGaussianSpectralNoise = Vector3.zero;

    [SerializeField] private TriaxialRangeData m_triaxialRange = new TriaxialRangeData();
    public TriaxialRangeData TriaxialRange {
      get => m_triaxialRange;
      set { m_triaxialRange = value; m_triaxialRange?.SetOnChanged( SynchronizeNative ); SynchronizeNative(); }
    }
    [SerializeField] private float m_crossAxisSensitivity = 0.01f;
    public float CrossAxisSensitivity { get => m_crossAxisSensitivity; set => PropertyUtil.Assign( ref m_crossAxisSensitivity, value, this ); }
    [SerializeField] private Vector3 m_zeroBias = Vector3.zero;
    public Vector3 ZeroBias { get => m_zeroBias; set => PropertyUtil.Assign( ref m_zeroBias, value, this ); }
    [SerializeField] private bool m_enableTotalGaussianNoise;
    public bool EnableTotalGaussianNoise { get => m_enableTotalGaussianNoise; set => PropertyUtil.Assign( ref m_enableTotalGaussianNoise, value, this ); }
    [SerializeField] private Vector3 m_totalGaussianNoise = Vector3.zero;
    public Vector3 TotalGaussianNoise { get => m_totalGaussianNoise; set => PropertyUtil.Assign( ref m_totalGaussianNoise, value, this ); }
    [SerializeField] private bool m_enableSignalScaling;
    public bool EnableSignalScaling { get => m_enableSignalScaling; set => PropertyUtil.Assign( ref m_enableSignalScaling, value, this ); }
    [SerializeField] private Vector3 m_signalScaling = Vector3.one;
    public Vector3 SignalScaling { get => m_signalScaling; set => PropertyUtil.Assign( ref m_signalScaling, value, this ); }
    [SerializeField] private bool m_enableGaussianSpectralNoise;
    public bool EnableGaussianSpectralNoise { get => m_enableGaussianSpectralNoise; set => PropertyUtil.Assign( ref m_enableGaussianSpectralNoise, value, this ); }
    [SerializeField] private Vector3 m_gaussianSpectralNoise = Vector3.zero;
    public Vector3 GaussianSpectralNoise { get => m_gaussianSpectralNoise; set => PropertyUtil.Assign( ref m_gaussianSpectralNoise, value, this ); }
    [SerializeField] public OutputXYZ OutputFlags = OutputXYZ.X | OutputXYZ.Y | OutputXYZ.Z;
    [RuntimeValue] public Vector3 Output { get; private set; }

    protected ITriaxialSignalSystemNodeRefVector Modifiers { get; private set; }
    protected TriaxialGaussianNoise TotalGaussianNoiseModifier { get; private set; }
    protected TriaxialSignalScaling SignalScalingModifier { get; private set; }
    protected TriaxialSpectralGaussianNoise GaussianSpectralNoiseModifier { get; private set; }

    internal abstract void AddNativeAttachment( IMUModelSensorAttachmentRefVector attachments );
    internal void SetOutput( Vec3 output ) => Output = new Vector3( (float)output.x, (float)output.y, (float)output.z );

    protected void CreateCommonModifiers()
    {
      Modifiers = new ITriaxialSignalSystemNodeRefVector();
      TotalGaussianNoiseModifier = new TriaxialGaussianNoise( GetTotalGaussianNoise().ToHandedVec3() );
      SignalScalingModifier = new TriaxialSignalScaling( GetSignalScaling().ToHandedVec3() );
      GaussianSpectralNoiseModifier = new TriaxialSpectralGaussianNoise( GetGaussianSpectralNoise().ToHandedVec3() );
      Modifiers.Add( TotalGaussianNoiseModifier ); Modifiers.Add( SignalScalingModifier ); Modifiers.Add( GaussianSpectralNoiseModifier );
    }

    protected override void SynchronizeNative()
    {
      SynchronizeModel();
      TotalGaussianNoiseModifier?.setNoiseRms( GetTotalGaussianNoise().ToHandedVec3() );
      SignalScalingModifier?.setScaling( GetSignalScaling().ToHandedVec3() );
      GaussianSpectralNoiseModifier?.setNoiseDensity( GetGaussianSpectralNoise().ToHandedVec3() );
    }
    protected abstract void SynchronizeModel();
    private Vector3 GetTotalGaussianNoise() => EnableTotalGaussianNoise ? TotalGaussianNoise : DisabledTotalGaussianNoise;
    private Vector3 GetSignalScaling() => EnableSignalScaling ? SignalScaling : DisabledSignalScaling;
    private Vector3 GetGaussianSpectralNoise() => EnableGaussianSpectralNoise ? GaussianSpectralNoise : DisabledGaussianSpectralNoise;
  }

  [Serializable]
  public sealed class Accelerometer : ImuSensorSubcomponent
  {
    private AccelerometerModel m_nativeModel;
    internal override void AddNativeAttachment( IMUModelSensorAttachmentRefVector attachments )
    {
      CreateCommonModifiers();
      m_nativeModel = new AccelerometerModel( TriaxialRange.GenerateTriaxialRange(), new TriaxialCrossSensitivity( CrossAxisSensitivity ), ZeroBias.ToHandedVec3(), Modifiers );
      attachments.Add( new IMUModelAccelerometerAttachment( AffineMatrix4x4.identity(), m_nativeModel ) );
    }
    protected override void SynchronizeModel()
    {
      if ( m_nativeModel == null ) return;
      m_nativeModel.setRange( TriaxialRange.GenerateTriaxialRange() );
      m_nativeModel.setCrossAxisSensitivity( new TriaxialCrossSensitivity( CrossAxisSensitivity ) );
      m_nativeModel.setZeroGBias( ZeroBias.ToHandedVec3() );
    }
  }

  [Serializable]
  public sealed class Gyroscope : ImuSensorSubcomponent
  {
    private static readonly Vector3 DisabledLinearAccelerationEffects = Vector3.zero;
    [SerializeField] private bool m_enableLinearAccelerationEffects;
    public bool EnableLinearAccelerationEffects { get => m_enableLinearAccelerationEffects; set => PropertyUtil.Assign( ref m_enableLinearAccelerationEffects, value, this ); }
    [SerializeField] private Vector3 m_linearAccelerationEffects = Vector3.zero;
    public Vector3 LinearAccelerationEffects { get => m_linearAccelerationEffects; set => PropertyUtil.Assign( ref m_linearAccelerationEffects, value, this ); }
    private GyroscopeModel m_nativeModel;
    private GyroscopeLinearAccelerationEffects m_linearAccelerationEffectsModifier;
    internal override void AddNativeAttachment( IMUModelSensorAttachmentRefVector attachments )
    {
      CreateCommonModifiers();
      m_linearAccelerationEffectsModifier = new GyroscopeLinearAccelerationEffects( GetLinearAccelerationEffects().ToHandedVec3() );
      Modifiers.Add( m_linearAccelerationEffectsModifier );
      m_nativeModel = new GyroscopeModel( TriaxialRange.GenerateTriaxialRange(), new TriaxialCrossSensitivity( CrossAxisSensitivity ), ZeroBias.ToHandedVec3(), Modifiers );
      attachments.Add( new IMUModelGyroscopeAttachment( AffineMatrix4x4.identity(), m_nativeModel ) );
    }
    protected override void SynchronizeModel()
    {
      if ( m_nativeModel == null ) return;
      m_nativeModel.setRange( TriaxialRange.GenerateTriaxialRange() );
      m_nativeModel.setCrossAxisSensitivity( new TriaxialCrossSensitivity( CrossAxisSensitivity ) );
      m_nativeModel.setZeroRateBias( ZeroBias.ToHandedVec3() );
      m_linearAccelerationEffectsModifier?.setAccelerationEffects( GetLinearAccelerationEffects().ToHandedVec3() );
    }
    private Vector3 GetLinearAccelerationEffects() => EnableLinearAccelerationEffects ? LinearAccelerationEffects : DisabledLinearAccelerationEffects;
  }

  [Serializable]
  public sealed class Magnetometer : ImuSensorSubcomponent
  {
    private MagnetometerModel m_nativeModel;
    internal override void AddNativeAttachment( IMUModelSensorAttachmentRefVector attachments )
    {
      CreateCommonModifiers();
      m_nativeModel = new MagnetometerModel( TriaxialRange.GenerateTriaxialRange(), new TriaxialCrossSensitivity( CrossAxisSensitivity ), ZeroBias.ToHandedVec3(), Modifiers );
      attachments.Add( new IMUModelMagnetometerAttachment( AffineMatrix4x4.identity(), m_nativeModel ) );
    }
    protected override void SynchronizeModel()
    {
      if ( m_nativeModel == null ) return;
      m_nativeModel.setRange( TriaxialRange.GenerateTriaxialRange() );
      m_nativeModel.setCrossAxisSensitivity( new TriaxialCrossSensitivity( CrossAxisSensitivity ) );
      m_nativeModel.setZeroFluxBias( ZeroBias.ToHandedVec3() );
    }
  }

  /// <summary>A serializable, ordered, polymorphic collection of IMU subcomponents.</summary>
  [Serializable]
  public class ImuSensorSubcomponentList : IReadOnlyList<ImuSensorSubcomponent>
  {
    [SerializeReference] private List<ImuSensorSubcomponent> m_backing = new List<ImuSensorSubcomponent>();
    public event Action OnChange;
    public int Count => m_backing.Count;
    public ImuSensorSubcomponent this[ int index ] => m_backing[ index ];
    public void Add( ImuSensorSubcomponent item )
    {
      if ( item == null ) throw new ArgumentNullException( nameof( item ) );
      if ( m_backing.Contains( item ) ) throw new InvalidOperationException( "The same subcomponent cannot be added more than once." );
      if ( item.Parent != null ) throw new InvalidOperationException( "The subcomponent is already owned by another component." );
      m_backing.Add( item ); OnChange?.Invoke();
    }
    public bool Remove( ImuSensorSubcomponent item ) { if ( !m_backing.Remove( item ) ) return false; OnChange?.Invoke(); return true; }
    public IEnumerator<ImuSensorSubcomponent> GetEnumerator() => m_backing.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    internal List<ImuSensorSubcomponent> GetAttachedItems( ImuSensor parent )
    {
      var result = new List<ImuSensorSubcomponent>(); var unique = new HashSet<ImuSensorSubcomponent>();
      foreach ( var item in m_backing ) {
        if ( item == null ) { Debug.LogWarning( "The IMU sensor collection contains a null sensor. It will be ignored.", parent ); continue; }
        if ( !unique.Add( item ) ) { Debug.LogWarning( "The IMU sensor collection contains the same sensor more than once. Duplicate entries will be ignored.", parent ); continue; }
        if ( item.Attach( parent ) ) result.Add( item );
      }
      return result;
    }
  }

  [DisallowMultipleComponent]
  [DoNotGenerateCustomEditor]
  [AddComponentMenu( "AGXUnity/Sensors/IMU Sensor" )]
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#simulating-imu-sensors" )]
  public class ImuSensor : ScriptComponent
  {
    [Header( "Sensors" )]
    [SerializeField] private ImuSensorSubcomponentList m_subcomponents = new ImuSensorSubcomponentList();
    public ImuSensorSubcomponentList Subcomponents => m_subcomponents;
    public IMU Native { get; private set; }
    private IMUModel m_nativeModel;
    private List<ImuSensorSubcomponent> m_configuredSubcomponents = new List<ImuSensorSubcomponent>();
    private uint m_outputID;
    [RuntimeValue] public RigidBody TrackedRigidBody { get; private set; }

    protected override bool Initialize()
    {
      SensorEnvironment.Instance.GetInitialized();
      var rigidBody = GetComponentInParent<RigidBody>();
      if ( rigidBody == null ) { Debug.LogWarning( "No Rigidbody found in this object or parents, IMU will be inactive" ); return false; }
      TrackedRigidBody = rigidBody;
      var attachments = new IMUModelSensorAttachmentRefVector();
      m_configuredSubcomponents = m_subcomponents.GetAttachedItems( this );
      foreach ( var subcomponent in m_configuredSubcomponents ) subcomponent.AddNativeAttachment( attachments );
      if ( attachments.Count == 0 ) { Debug.LogWarning( "No sensor subcomponents, IMU will be inactive" ); return false; }
      m_nativeModel = new IMUModel( attachments );
      var measuredRigidBody = rigidBody.GetInitialized<RigidBody>().Native;
      SensorEnvironment.Instance.Native.add( measuredRigidBody );
      var rigidBodyFrame = measuredRigidBody.getFrame();
      if ( rigidBodyFrame == null ) { Debug.LogWarning( "Could not get rigid body frame, IMU will be inactive" ); return false; }
      Native = new IMU( rigidBodyFrame, m_nativeModel );
      m_outputID = SensorEnvironment.Instance.GenerateOutputID();
      Native.getOutputHandler().add( m_outputID, new IMUOutputNineDoF() );
      Simulation.Instance.StepCallbacks.PostSynchronizeTransforms += OnPostSynchronizeTransforms;
      SensorEnvironment.Instance.Native.add( Native );
      return true;
    }

    private void OnPostSynchronizeTransforms()
    {
      if ( !gameObject.activeInHierarchy || Native == null )
        return;

      var output = Native.getOutputHandler().get( m_outputID );
      var views = output?.viewNineDoF();
      if ( views == null || views.size() == 0 )
        return;

      var value = views[ 0 ];
      // IMUOutputNineDoF exposes up to three triaxial values. Additional
      // subcomponents remain at their default output until a general IMU output
      // representation is introduced.
      var count = Mathf.Min( 3, m_configuredSubcomponents.Count );
      for ( var index = 0; index < count; ++index )
        m_configuredSubcomponents[ index ].SetOutput( value.getTriplet( (uint)index ) );
    }
    protected override void OnEnable() => Native?.setEnable( true );
    protected override void OnDisable() => Native?.setEnable( false );
    protected override void OnDestroy()
    {
      if ( SensorEnvironment.HasInstance ) SensorEnvironment.Instance.Native?.remove( Native );
      if ( Simulation.HasInstance ) Simulation.Instance.StepCallbacks.PostSynchronizeTransforms -= OnPostSynchronizeTransforms;
      base.OnDestroy();
    }
  }
}
