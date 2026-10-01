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
    [SerializeField] private bool m_useCrossAxisSensitivityMatrix;
    /// <summary>
    /// When enabled, uses <see cref="CrossAxisSensitivityMatrix"/> instead of the
    /// legacy scalar cross-axis sensitivity.
    /// </summary>
    public bool UseCrossAxisSensitivityMatrix { get => m_useCrossAxisSensitivityMatrix; set => PropertyUtil.Assign( ref m_useCrossAxisSensitivityMatrix, value, this ); }
    [SerializeField] private UnityEngine.Matrix4x4 m_crossAxisSensitivityMatrix = UnityEngine.Matrix4x4.identity;
    /// <summary>
    /// The upper-left 3x3 cross-axis sensitivity matrix. The remaining elements
    /// are ignored.
    /// </summary>
    public UnityEngine.Matrix4x4 CrossAxisSensitivityMatrix { get => m_crossAxisSensitivityMatrix; set => PropertyUtil.Assign( ref m_crossAxisSensitivityMatrix, value, this ); }
    [SerializeField] private Vector3 m_zeroBias = Vector3.zero;
    public Vector3 ZeroBias { get => m_zeroBias; set => PropertyUtil.Assign( ref m_zeroBias, value, this ); }
    [SerializeField] private bool m_enableTotalGaussianNoise;
    public bool EnableTotalGaussianNoise { get => m_enableTotalGaussianNoise; set => PropertyUtil.Assign( ref m_enableTotalGaussianNoise, value, this ); }
    [SerializeField] private Vector3 m_totalGaussianNoise = Vector3.zero;
    public Vector3 TotalGaussianNoise { get => m_totalGaussianNoise; set => PropertyUtil.Assign( ref m_totalGaussianNoise, value, this ); }
    [SerializeField] private Vector3 m_totalGaussianNoiseMean = Vector3.zero;
    /// <summary>
    /// Per-axis mean of the total Gaussian noise. AGX represents this constant
    /// offset through the model bias; it is added to <see cref="ZeroBias"/> when
    /// total Gaussian noise is enabled.
    /// </summary>
    public Vector3 TotalGaussianNoiseMean { get => m_totalGaussianNoiseMean; set => PropertyUtil.Assign( ref m_totalGaussianNoiseMean, value, this ); }
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

    [SerializeField] private Vector3 m_attachmentPosition = Vector3.zero;
    /// <summary>Position of this sensing element in the tracked rigid-body frame.</summary>
    public Vector3 AttachmentPosition { get => m_attachmentPosition; set => PropertyUtil.Assign( ref m_attachmentPosition, value, this ); }
    [SerializeField] private Quaternion m_attachmentRotation = Quaternion.identity;
    /// <summary>Orientation of this sensing element in the tracked rigid-body frame.</summary>
    public Quaternion AttachmentRotation { get => m_attachmentRotation; set => PropertyUtil.Assign( ref m_attachmentRotation, value, this ); }

    protected ITriaxialSignalSystemNodeRefVector Modifiers { get; private set; }
    protected TriaxialGaussianNoise TotalGaussianNoiseModifier { get; private set; }
    protected TriaxialSignalScaling SignalScalingModifier { get; private set; }
    protected TriaxialSpectralGaussianNoise GaussianSpectralNoiseModifier { get; private set; }

    internal abstract void AddNativeAttachment( IMUModelSensorAttachmentRefVector attachments );
    internal void SetOutputAxis( int axis, double value )
    {
      var output = Output;
      output[ axis ] = (float)value;
      Output = output;
    }

    internal void ClearOutput() => Output = Vector3.zero;

    protected void CreateCommonModifiers()
    {
      m_triaxialRange.SetOnChanged( SynchronizeNative );
      Modifiers = new ITriaxialSignalSystemNodeRefVector();
      TotalGaussianNoiseModifier = new TriaxialGaussianNoise( GetTotalGaussianNoise().ToHandedVec3() );
      SignalScalingModifier = new TriaxialSignalScaling( GetSignalScaling().ToHandedVec3() );
      GaussianSpectralNoiseModifier = new TriaxialSpectralGaussianNoise( GetGaussianSpectralNoise().ToHandedVec3() );
      Modifiers.Add( TotalGaussianNoiseModifier ); Modifiers.Add( SignalScalingModifier ); Modifiers.Add( GaussianSpectralNoiseModifier );
    }

    protected override void NativeSync()
    {
      SynchronizeModel();
      TotalGaussianNoiseModifier?.setNoiseRms( GetTotalGaussianNoise().ToHandedVec3() );
      SignalScalingModifier?.setScaling( GetSignalScaling().ToHandedVec3() );
      GaussianSpectralNoiseModifier?.setNoiseDensity( GetGaussianSpectralNoise().ToHandedVec3() );
    }
    protected abstract void SynchronizeModel();
    private Vector3 GetTotalGaussianNoise() => EnableTotalGaussianNoise ? TotalGaussianNoise : DisabledTotalGaussianNoise;
    protected Vector3 GetEffectiveZeroBias() => ZeroBias + ( EnableTotalGaussianNoise ? TotalGaussianNoiseMean : Vector3.zero );
    protected agx.AffineMatrix4x4 GetAttachmentTransform() => new agx.AffineMatrix4x4( AttachmentRotation.ToHandedQuat(), AttachmentPosition.ToHandedVec3() );
    protected TriaxialCrossSensitivity GetCrossAxisSensitivity()
    {
      if ( !UseCrossAxisSensitivityMatrix )
        return new TriaxialCrossSensitivity( CrossAxisSensitivity );

      var matrix = CrossAxisSensitivityMatrix;
      return new TriaxialCrossSensitivity( new Matrix3x3(
        matrix.m00, matrix.m01, matrix.m02,
        matrix.m10, matrix.m11, matrix.m12,
        matrix.m20, matrix.m21, matrix.m22 ) );
    }
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
      m_nativeModel = new AccelerometerModel( TriaxialRange.GenerateTriaxialRange(), GetCrossAxisSensitivity(), GetEffectiveZeroBias().ToHandedVec3(), Modifiers );
      attachments.Add( new IMUModelAccelerometerAttachment( GetAttachmentTransform(), m_nativeModel ) );
    }
    protected override void SynchronizeModel()
    {
      if ( m_nativeModel == null ) return;
      m_nativeModel.setRange( TriaxialRange.GenerateTriaxialRange() );
      m_nativeModel.setCrossAxisSensitivity( GetCrossAxisSensitivity() );
      m_nativeModel.setZeroGBias( GetEffectiveZeroBias().ToHandedVec3() );
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
      m_nativeModel = new GyroscopeModel( TriaxialRange.GenerateTriaxialRange(), GetCrossAxisSensitivity(), GetEffectiveZeroBias().ToHandedVec3(), Modifiers );
      attachments.Add( new IMUModelGyroscopeAttachment( GetAttachmentTransform(), m_nativeModel ) );
    }
    protected override void SynchronizeModel()
    {
      if ( m_nativeModel == null ) return;
      m_nativeModel.setRange( TriaxialRange.GenerateTriaxialRange() );
      m_nativeModel.setCrossAxisSensitivity( GetCrossAxisSensitivity() );
      m_nativeModel.setZeroRateBias( GetEffectiveZeroBias().ToHandedVec3() );
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
      m_nativeModel = new MagnetometerModel( TriaxialRange.GenerateTriaxialRange(), GetCrossAxisSensitivity(), GetEffectiveZeroBias().ToHandedVec3(), Modifiers );
      attachments.Add( new IMUModelMagnetometerAttachment( GetAttachmentTransform(), m_nativeModel ) );
    }
    protected override void SynchronizeModel()
    {
      if ( m_nativeModel == null ) return;
      m_nativeModel.setRange( TriaxialRange.GenerateTriaxialRange() );
      m_nativeModel.setCrossAxisSensitivity( GetCrossAxisSensitivity() );
      m_nativeModel.setZeroFluxBias( GetEffectiveZeroBias().ToHandedVec3() );
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
        if ( item.Bind( parent ) ) result.Add( item );
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
    private struct OutputFieldBinding
    {
      public ImuSensorSubcomponent Subcomponent;
      public int Axis;

      public OutputFieldBinding( ImuSensorSubcomponent subcomponent, int axis )
      {
        Subcomponent = subcomponent;
        Axis = axis;
      }
    }

    [Header( "Sensors" )]
    [SerializeField] private ImuSensorSubcomponentList m_subcomponents = new ImuSensorSubcomponentList();
    public ImuSensorSubcomponentList Subcomponents => m_subcomponents;
    public IMU Native { get; private set; }
    private IMUModel m_nativeModel;
    private List<ImuSensorSubcomponent> m_configuredSubcomponents = new List<ImuSensorSubcomponent>();
    private readonly List<OutputFieldBinding> m_outputFieldBindings = new List<OutputFieldBinding>();
    private double[] m_outputValues;
    private uint m_outputID;
    [SerializeField] private RigidBody m_measuredRigidBody;
    /// <summary>
    /// The rigid body measured by this IMU. If unset, the nearest rigid body in
    /// this GameObject's parent hierarchy is used.
    /// </summary>
    public RigidBody MeasuredRigidBody { get => m_measuredRigidBody; set => m_measuredRigidBody = value; }
    [RuntimeValue] public RigidBody TrackedRigidBody { get; private set; }

    protected override bool Initialize()
    {
      SensorEnvironment.Instance.GetInitialized();
      var rigidBody = MeasuredRigidBody ?? GetComponentInParent<RigidBody>();
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
      Native.getOutputHandler().add( m_outputID, CreateOutput() );
      Simulation.Instance.StepCallbacks.PostSynchronizeTransforms += OnPostSynchronizeTransforms;
      SensorEnvironment.Instance.Native.add( Native );
      return true;
    }

    private void OnPostSynchronizeTransforms()
    {
      if ( !gameObject.activeInHierarchy || Native == null )
        return;

      var output = Native.getOutputHandler().get( m_outputID );
      if ( output == null || m_outputFieldBindings.Count == 0 )
        return;

      uint sampleCount;
      m_outputValues = output.ReadValues( out sampleCount, m_outputValues );
      if ( sampleCount == 0 )
        return;

      var firstValue = ( sampleCount - 1 ) * m_outputFieldBindings.Count;
      for ( var index = 0; index < m_outputFieldBindings.Count; ++index ) {
        var binding = m_outputFieldBindings[ index ];
        binding.Subcomponent.SetOutputAxis( binding.Axis, m_outputValues[ firstValue + index ] );
      }
    }

    private IMUOutput CreateOutput()
    {
      var output = new IMUOutput();
      m_outputFieldBindings.Clear();
      for ( var sensorIndex = 0; sensorIndex < m_configuredSubcomponents.Count; ++sensorIndex ) {
        var subcomponent = m_configuredSubcomponents[ sensorIndex ];
        subcomponent.ClearOutput();
        for ( var axis = 0; axis < 3; ++axis ) {
          if ( ( subcomponent.OutputFlags & (OutputXYZ)( 1 << axis ) ) == OutputXYZ.None )
            continue;

          output.add( IMUOutput.makeSensorField( (uint)sensorIndex, (IMUOutput.SensorAxis)axis ) );
          m_outputFieldBindings.Add( new OutputFieldBinding( subcomponent, axis ) );
        }
      }
      return output;
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
