using AGXUnity.Sensor;
using openplx.Sensors;
using openplx.Sensors.Optics;
using openplx.Sensors.Optics.Traits;
using UnityEngine;

namespace AGXUnity.IO.OpenPLX
{
  public class SensorMapper
  {
    private MapperData Data;

    public SensorMapper( MapperData data )
    {
      Data = data;
    }

    public GameObject MapLidar( LidarLogic lidar )
    {
      // TODO: OpenPLX LiDAR sensor mapping currently does not create any outputs and thus will not properly handle 
      var go = Data.CreateOpenPLXObject(lidar.getName());

      var lidarComp = go.AddComponent<LidarSensor>();
      lidarComp.LidarModelPreset = LidarModelPreset.LidarModelGenericHorizontalSweep;
      GenericSweepData modelData = (GenericSweepData)lidarComp.ModelData;

      if ( lidar is RayEmitter rayEmitter ) {
        if ( rayEmitter is BeamEmitter beamEmitter ) {
          if ( beamEmitter.beam_divergence() is ConicalBeamDivergence conical ) {
            modelData.BeamDivergence = (float)conical.divergence_angle();
            modelData.BeamExitRadius = (float)conical.waist_radius();
          }

          if ( beamEmitter is PulsedBeamEmitter pulsedEmitter ) {
            // TODO: Wavelength is not supported in AGXUnity
            //modelData.wavelength = (float)pulsedEmitter.wavelength();
          }
        }

      }
      if ( lidar is PulsedLidarLogic pulsed ) {
        if ( pulsed.ray_source() is HorizontalSweepRaySource horizontalSweep ) {
          modelData.Frequency = (float)horizontalSweep.frequency();

          modelData.ResolutionMode = GenericSweepData.ResolutionModes.TotalPoints;
          modelData.HorizontalResolution = horizontalSweep.horizontal_resolution();
          modelData.VerticalResolution = horizontalSweep.vertical_resolution();

          modelData.FoVMode = GenericSweepData.FoVModes.Window;
          modelData.HorizontalFoVWindow = new RangeReal(
            (float)( horizontalSweep.horizontal_fov().x() * Mathf.Rad2Deg ),
            (float)( horizontalSweep.horizontal_fov().y() * Mathf.Rad2Deg ) );
          modelData.VerticalFoVWindow = new RangeReal(
            (float)( horizontalSweep.vertical_fov().x() * Mathf.Rad2Deg ),
            (float)( horizontalSweep.vertical_fov().y() * Mathf.Rad2Deg ) );
        }
      }

      if ( lidar is DistortedRayEmission rayDistortions ) {
        foreach ( var dist in rayDistortions.ray_emission_distortions() ) {
          if ( dist is RayEmissionAngleGaussianNoise gaussian ) {

            var distortionAxis = gaussian.rotation_axis().ToVec3();
            agxSensor.LidarRayAngleGaussianNoise.Axis axis = 0;
            if ( distortionAxis.dot( agx.Vec3.X_AXIS() ) >= 0.95f )
              axis = agxSensor.LidarRayAngleGaussianNoise.Axis.AXIS_X;
            else if ( distortionAxis.dot( agx.Vec3.Y_AXIS() ) >= 0.95f )
              axis = agxSensor.LidarRayAngleGaussianNoise.Axis.AXIS_Y;
            else if ( distortionAxis.dot( agx.Vec3.Z_AXIS() ) >= 0.95f )
              axis = agxSensor.LidarRayAngleGaussianNoise.Axis.AXIS_Z;
            else {
              Data.ErrorReporter.reportError( new NonPrincipalAxisError( gaussian.rotation_axis() ) );
            }

            lidarComp.RayAngleGaussianNoises.Add( new LidarRayAngleGaussianNoise()
            {
              DistortionAxis = axis,
              Enable = true,
              Mean = (float)gaussian.gaussian_distribution().mean(),
              StandardDeviation = (float)gaussian.gaussian_distribution().standard_deviation()
            } );
          }
        }
      }

      foreach ( var dist in lidar.sensing_distortions() ) {
        if ( dist is LidarDetectionDistanceGaussianNoise gaussianDistanceNoise ) {
          if ( lidarComp.DistanceGaussianNoise.Enable == true ) {
            Data.ErrorReporter.reportError( new MultipleDistanceDistortionsError( gaussianDistanceNoise ) );
            continue;
          }
          lidarComp.DistanceGaussianNoise.Enable = true;
          lidarComp.DistanceGaussianNoise.Mean = (float)gaussianDistanceNoise.gaussian_distribution().mean();
          lidarComp.DistanceGaussianNoise.StandardDeviationBase = (float)gaussianDistanceNoise.gaussian_distribution().standard_deviation();
          lidarComp.DistanceGaussianNoise.StandardDeviationSlope = (float)gaussianDistanceNoise.standard_deviation_slope();
        }
      }

      lidarComp.LidarFrame = Data.MateConnectorCache[ lidar.mate_connector_attachment() ];

      // Epxlicitly exclude the lidar logic attachment system as this may contain LiDAR-visuals or collision geometries.
      var inclusion = lidarComp.LidarFrame.transform.parent.gameObject.AddComponent<ExplicitSensorEnvironmentInclusion>();
      inclusion.PropagateToChildrenRecusively = true;
      inclusion.Include = false;

      return go;
    }

    public GameObject MapImu( IMULogic imu )
    {
      var go = Data.CreateOpenPLXObject( imu.getName() );
      var imuComp = go.AddComponent<ImuSensor>();

      if ( !TryMapAttachment( imu.mate_connector_attachment(), imuComp, out var attachmentPosition, out var attachmentRotation ) )
        Debug.LogWarning( $"IMU '{imu.getName()}' has no mappable mate connector attachment and will be inactive." );

      foreach ( var source in imu.getNonReferenceValues<AccelerometerLogic>() ) {
        var target = new Accelerometer();
        ConfigureTriaxialSensor( source, target, attachmentPosition, attachmentRotation );
        MapGaussianAndSpectralNoise( source, target );
        imuComp.Subcomponents.Add( target );
      }

      foreach ( var source in imu.getNonReferenceValues<GyroscopeLogic>() ) {
        var target = new AGXUnity.Sensor.Gyroscope();
        ConfigureTriaxialSensor( source, target, attachmentPosition, attachmentRotation );
        MapGaussianAndSpectralNoise( source, target );
        foreach ( var distortion in source.sensing_distortions() ) {
          if ( distortion is GyroscopeSignalLinearAccelectionEffect linearAccelerationEffect ) {
            target.EnableLinearAccelerationEffects = true;
            target.LinearAccelerationEffects = linearAccelerationEffect.acceleration_effects().ToHandedVector3();
          }
        }
        imuComp.Subcomponents.Add( target );
      }

      foreach ( var source in imu.getNonReferenceValues<MagnetometerLogic>() ) {
        var target = new Magnetometer();
        ConfigureTriaxialSensor( source, target, attachmentPosition, attachmentRotation );
        MapGaussianAndSpectralNoise( source, target );
        imuComp.Subcomponents.Add( target );
      }

      return go;
    }

    private bool TryMapAttachment( openplx.Physics3D.Interactions.MateConnector attachment,
                                   ImuSensor imu,
                                   out Vector3 position,
                                   out Quaternion rotation )
    {
      position = Vector3.zero;
      rotation = Quaternion.identity;
      if ( attachment == null || !Data.MateConnectorCache.TryGetValue( attachment, out var attachmentObject ) )
        return false;

      var measuredBody = attachmentObject.GetComponentInParent<AGXUnity.RigidBody>();
      if ( measuredBody == null )
        return false;

      imu.MeasuredRigidBody = measuredBody;
      position = measuredBody.transform.InverseTransformPoint( attachmentObject.transform.position );
      rotation = Quaternion.Inverse( measuredBody.transform.rotation ) * attachmentObject.transform.rotation;
      return true;
    }

    private static void ConfigureTriaxialSensor( TriaxialSensorLogic source,
                                                 ImuSensorSubcomponent target,
                                                 Vector3 attachmentPosition,
                                                 Quaternion attachmentRotation )
    {
      var range = source.range();
      if ( range != null ) {
        target.TriaxialRange.Mode = TriaxialRangeData.ConfigurationMode.IndividualAxisRanges;
        target.TriaxialRange.RangeX = ToVector2( range.x() );
        target.TriaxialRange.RangeY = ToVector2( range.y() );
        target.TriaxialRange.RangeZ = ToVector2( range.z() );
      }

      var crossAxis = source.cross_axis_sensitivity();
      if ( crossAxis != null ) {
        target.UseCrossAxisSensitivityMatrix = true;
        target.CrossAxisSensitivityMatrix = new Matrix4x4(
          new Vector4( (float)crossAxis.e00(), (float)crossAxis.e10(), (float)crossAxis.e20(), 0 ),
          new Vector4( (float)crossAxis.e01(), (float)crossAxis.e11(), (float)crossAxis.e21(), 0 ),
          new Vector4( (float)crossAxis.e02(), (float)crossAxis.e12(), (float)crossAxis.e22(), 0 ),
          new Vector4( 0, 0, 0, 1 ) );
      }

      target.ZeroBias = source.zero_bias().ToHandedVector3();
      target.AttachmentPosition = attachmentPosition;
      target.AttachmentRotation = attachmentRotation;
    }

    private static Vector2 ToVector2( openplx.Math.Vec2 value ) => new Vector2( (float)value.x(), (float)value.y() );

    private static void MapGaussianAndSpectralNoise( AccelerometerLogic source, Accelerometer target )
    {
      foreach ( var distortion in source.sensing_distortions() ) {
        if ( distortion is AccelerometerSignalGaussianNoise gaussian )
          SetGaussianNoise( target, gaussian.distribution_x(), gaussian.distribution_y(), gaussian.distribution_z() );
        else if ( distortion is AccelerometerSignalSpectralGaussianNoise spectral )
          SetSpectralNoise( target, spectral.noise_density_x(), spectral.noise_density_y(), spectral.noise_density_z() );
      }
    }

    private static void MapGaussianAndSpectralNoise( GyroscopeLogic source, AGXUnity.Sensor.Gyroscope target )
    {
      foreach ( var distortion in source.sensing_distortions() ) {
        if ( distortion is GyroscopeSignalGaussianNoise gaussian )
          SetGaussianNoise( target, gaussian.distribution_x(), gaussian.distribution_y(), gaussian.distribution_z() );
        else if ( distortion is GyroscopeSignalSpectralGaussianNoise spectral )
          SetSpectralNoise( target, spectral.noise_density_x(), spectral.noise_density_y(), spectral.noise_density_z() );
      }
    }

    private static void MapGaussianAndSpectralNoise( MagnetometerLogic source, Magnetometer target )
    {
      foreach ( var distortion in source.sensing_distortions() ) {
        if ( distortion is MagnetometerSignalGaussianNoise gaussian )
          SetGaussianNoise( target, gaussian.distribution_x(), gaussian.distribution_y(), gaussian.distribution_z() );
        else if ( distortion is MagnetometerSignalSpectralGaussianNoise spectral )
          SetSpectralNoise( target, spectral.noise_density_x(), spectral.noise_density_y(), spectral.noise_density_z() );
      }
    }

    private static void SetGaussianNoise( ImuSensorSubcomponent target,
                                          openplx.Math.Distributions.Gaussian x,
                                          openplx.Math.Distributions.Gaussian y,
                                          openplx.Math.Distributions.Gaussian z )
    {
      target.EnableTotalGaussianNoise = true;
      target.TotalGaussianNoise = new Vector3( (float)x.standard_deviation(), (float)y.standard_deviation(), (float)z.standard_deviation() );
      target.TotalGaussianNoiseMean = new Vector3( (float)x.mean(), (float)y.mean(), (float)z.mean() );
    }

    private static void SetSpectralNoise( ImuSensorSubcomponent target, double x, double y, double z )
    {
      target.EnableGaussianSpectralNoise = true;
      target.GaussianSpectralNoise = new Vector3( (float)x, (float)y, (float)z );
    }
  }
}
