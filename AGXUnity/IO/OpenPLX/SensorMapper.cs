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
  }
}
