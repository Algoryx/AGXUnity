using agxSensor;
using System.Collections.Generic;
using UnityEngine;

namespace AGXUnity.Sensor
{
  internal sealed class CameraBackend : UnityCameraBackendImplementation
  {
    private Dictionary<agxSensor.Camera, CameraSensor> m_cameraMap = new Dictionary<agxSensor.Camera, CameraSensor>();
    private Dictionary<agxSensor.ICameraActiveIllumination, CameraSensor.Illuminator> m_lightMap = new Dictionary<agxSensor.ICameraActiveIllumination, CameraSensor.Illuminator>();
    private Dictionary<agxSensor.CameraColorOutput, CameraSensor.ColorOutput> m_colorOutputMap = new Dictionary<agxSensor.CameraColorOutput, CameraSensor.ColorOutput>();

    public void MapCamera( agxSensor.Camera agxCamera, CameraSensor unityCamera )
    {
      if ( !m_cameraMap.ContainsKey( agxCamera ) )
        m_cameraMap.Add( agxCamera, unityCamera );

      if ( !unityCamera.CameraComponent.usePhysicalProperties )
        unityCamera.CameraComponent.usePhysicalProperties = true;
    }

    public void MapLight( agxSensor.ICameraActiveIllumination agxLight, CameraSensor.Illuminator illuminator )
    {
      if ( !m_lightMap.ContainsKey( agxLight ) )
        m_lightMap.Add( agxLight, illuminator );
    }

    public void MapColorOutput( agxSensor.CameraColorOutput agxOutput, CameraSensor.ColorOutput output )
    {
      if ( !m_colorOutputMap.ContainsKey( agxOutput ) )
        m_colorOutputMap.Add( agxOutput, output );
    }

    public CameraSensor GetMappedCamera( agxSensor.Camera agxCamera )
    {
      if ( !m_cameraMap.TryGetValue( agxCamera, out CameraSensor uCamera ) ) {
        Debug.LogWarning( "Unity camera backend got an agxSensor camera that has not been registered." );
        return null;
      }

      return uCamera;
    }

    public CameraSensor.Illuminator GetMappedLight( agxSensor.ICameraActiveIllumination agxLight )
    {
      if ( !m_lightMap.TryGetValue( agxLight, out CameraSensor.Illuminator illuminator ) ) {
        Debug.LogWarning( "Unity camera backend got an agxSensor light that has not been registered." );
        return null;
      }

      return illuminator;
    }

    #region Singleton 
    static CameraBackend s_instance = null;

    public static CameraBackend Instance
    {
      get
      {
        if ( s_instance == null )
          s_instance = new CameraBackend();
        return s_instance;
      }
    }

    private CameraBackend()
    {
      install( this );
    }

    ~CameraBackend()
    {
      uninstall();
    }
    #endregion

    protected override void synchronize( agxSensor.Camera camera, double dt ) { }

    protected override void execute( agxSensor.Camera camera, double dt ) { }

    protected override void complete( agxSensor.Camera camera ) { }

    protected override void result( agxSensor.Camera camera, double dt ) { }

    protected override void synchronizeGraphics( agxSensor.Camera camera, agxSensor.Matrix4x4 viewMatrix )
    {
      //  const osg::Matrixd agxToGL {0.0, 0.0, -1.0, 0.0, -1.0, 0.0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0, 0.0, 0.0, 1.0};
      //  if (auto neoCamera = getOrCreateBackendCameraFor(camera)) {
      //    const auto& agxMatrix = *reinterpret_cast<osg::Matrixd*>(viewMatrix);
      //    neoCamera->setViewMatrix(agxMatrix * agxToGL);
      //  }
    }

    protected override void cleanup( agxSensor.Camera camera )
    {
      m_cameraMap.Remove( camera );
    }

    protected override void setCameraLensSingleElement( agxSensor.Camera camera, CameraLensSingleElement lens, CameraLensSingleElementParameters parameters )
    {
      var uCamera = GetMappedCamera( camera );
      if ( uCamera == null )
        return;

      uCamera.FocalLength = (float)parameters.focalLength;
      uCamera.fStop = (float)parameters.fStop;

      if ( parameters.autofocus ) {
        Debug.LogWarning( "Autofocus is not yet supported" );
      }
      else
        uCamera.FocusDistance = (float)lens.getFocusDistance();
    }

    protected override void setCameraCMOSSensor( agxSensor.Camera camera, CameraCMOSSensor sensor, CameraCMOSSensorParameters parameters )
    {
      var uCamera = GetMappedCamera( camera );
      if ( uCamera == null )
        return;

      uCamera.SensorSize = new Vector2( (float)parameters.sizeX, (float)parameters.sizeY );
      uCamera.ISO = (int)parameters.iso;
      uCamera.ShutterSpeed = (float)parameters.shutterSpeed;

      uCamera.Resolution = new Vector2Int( parameters.resolutionX, parameters.resolutionY );

      //    if (parameters->autoExposure)
      //      neoCameraSensor->setAutoExposure(parameters->exposure.dynamicRange);
      //    else
      //      neoCameraSensor->setManualExposureCompensation(parameters->exposure.compensation);

      //    updateAllIlluminationStrengths(neoCamera);
      //  }
    }

    protected override void setCameraLensDistortionNone( agxSensor.Camera camera, CameraLens lens )
    {
      //  if (auto neoCamera = getOrCreateBackendCameraFor(camera))
      //    neoCamera->getLens()->setDistortionModelNone();
    }

    protected override void setCameraLensDistortionBrownConrady( agxSensor.Camera camera, CameraLens lens, LensDistortionBrownConradyCoefficients coefficients )
    {
      //  if (auto neoCamera = getOrCreateBackendCameraFor(camera))
      //    neoCamera->getLens()->setDistortionModelBrownConrady(
      //      coefficients->k1, coefficients->k2, coefficients->k3, coefficients->p1, coefficients->p2);
    }

    protected override void synhronizeGraphicsActiveIllumination( agxSensor.Camera camera, CameraActiveIllumination illumination, agxSensor.Matrix4x4 transform )
    {
      //var illum = GetMappedLight( illumination );
      //if ( illum == null )
      //  return;
      //  if (auto neoCamera = self().getOrCreateBackendCameraForInternal(camera))
      //    if (auto neoCameraIllumination = self().getOrCreateBackendIlluminationForInternal(neoCamera, illumination)) {
      //      const auto& agxMatrix = *reinterpret_cast<osg::Matrixd*>(transform);
      //      neoCameraIllumination->transform->setMatrix(agxMatrix);

      //      if (neoCamera->getReferenceIlluminance() != neoCameraIllumination->referenceIlluminance)
      //        updateIlluminationStrength(neoCamera, neoCameraIllumination);
      //    }
    }

    protected override void setPointLightActiveIllumination( agxSensor.Camera camera, CameraActiveIllumination illumination, CameraPointLightParameters parameters )
    {
      var uCamera = GetMappedCamera( camera );
      if ( uCamera == null )
        return;


      //    if (auto neoCameraIllumination = self().getOrCreateBackendIlluminationForInternal(neoCamera, illumination)) {
      //      auto light = neoCameraIllumination->source->getLight();

      //      const auto position = light->getPosition();
      //      light->setPosition(osg::Vec4(position.x(), position.y(), position.z(), 1.0f));

      //      light->setConstantAttenuation(0.0f);
      //      light->setLinearAttenuation(0.0f);
      //      light->setQuadraticAttenuation(1.0f);

      //      light->setSpotExponent(0.0f);
      //      light->setSpotCutoff(180.0f);

      //      neoCameraIllumination->intensity = parameters->intensity;
      //      neoCameraIllumination->color[0] = parameters->color[0];
      //      neoCameraIllumination->color[1] = parameters->color[1];
      //      neoCameraIllumination->color[2] = parameters->color[2];
      //      neoCameraIllumination->flashDuration = parameters->flashDuration;

      //      updateIlluminationStrength(neoCamera, neoCameraIllumination);
      //    }
    }

    protected override void setSpotlightActiveIllumination( agxSensor.Camera camera, CameraActiveIllumination illumination, CameraSpotlightParameters parameters )
    {
      //  if (auto neoCamera = self().getOrCreateBackendCameraForInternal(camera))
      //    if (auto neoCameraIllumination = self().getOrCreateBackendIlluminationForInternal(neoCamera, illumination)) {
      //      auto light = neoCameraIllumination->source->getLight();

      //      const auto position = light->getPosition();
      //      light->setPosition(osg::Vec4(position.x(), position.y(), position.z(), 1.0f));

      //      light->setConstantAttenuation(0.0f);
      //      light->setLinearAttenuation(0.0f);
      //      light->setQuadraticAttenuation(1.0f);

      //      const agx::Real innerAngle = std::min(parameters->angle.inner, parameters->angle.outer);
      //      if (innerAngle == parameters->angle.outer)
      //        light->setSpotExponent(0.0f);
      //      else  // To get an okay-ish dual-angle approximation we assume inner angle will hold 90% intensity
      //        light->setSpotExponent(std::min(128.0f, static_cast<float>(-0.1053605157 / std::log(std::cos(innerAngle)))));
      //      light->setSpotCutoff(static_cast<float>(parameters->angle.outer / agx::PI * 180.0));

      //      neoCameraIllumination->intensity = parameters->intensity;
      //      neoCameraIllumination->color[0] = parameters->color[0];
      //      neoCameraIllumination->color[1] = parameters->color[1];
      //      neoCameraIllumination->color[2] = parameters->color[2];
      //      neoCameraIllumination->flashDuration = parameters->flashDuration;

      //      updateIlluminationStrength(neoCamera, neoCameraIllumination);
      //    }
    }

    protected override void setCameraColorOutputAddress( agxSensor.Camera camera, CameraColorOutput agxOutput, ByteSpan outputAddress )
    {
      if ( !m_colorOutputMap.TryGetValue( agxOutput, out var output ) )
        return;

      output.NativeOutputSpan = outputAddress;
    }

    private agxSensor.CameraColorOutput.ChannelType Convert( agxSensor.CameraOutputChannelType src )
    {
      return src switch
      {
        CameraOutputChannelType.I8 => CameraColorOutput.ChannelType.I8,
        CameraOutputChannelType.U8 => CameraColorOutput.ChannelType.U8,
        CameraOutputChannelType.I16 => CameraColorOutput.ChannelType.I16,
        CameraOutputChannelType.U16 => CameraColorOutput.ChannelType.U16,
        CameraOutputChannelType.I32 => CameraColorOutput.ChannelType.I32,
        CameraOutputChannelType.U32 => CameraColorOutput.ChannelType.U32,
        CameraOutputChannelType.F32 => CameraColorOutput.ChannelType.F32,
        CameraOutputChannelType.I64 => CameraColorOutput.ChannelType.I64,
        CameraOutputChannelType.U64 => CameraColorOutput.ChannelType.U64,
        CameraOutputChannelType.F64 => CameraColorOutput.ChannelType.F64,
        _ => CameraColorOutput.ChannelType.U8
      };
    }

    protected override void setCameraColorOutput( agxSensor.Camera camera, CameraColorOutput agxOutput, CameraColorOutputParameters parameters )
    {
      if ( !m_colorOutputMap.TryGetValue( agxOutput, out var output ) )
        return;

      output.ChannelCount = parameters.channelCount;
      output.ChannelType = Convert( parameters.channelType );
      output.Resolution = new Vector2Int( parameters.resolutionX, parameters.resolutionY );
      output.Gamma = (float)parameters.gamma;
      output.IlluminanceCutoff = new Vector2( (float)parameters.relativeIlluminanceCutoff.lower, (float)parameters.relativeIlluminanceCutoff.upper );
      output.Framerate = (float)parameters.framerate;
      //    neoCameraOutput->setColorMappingMatrix(*reinterpret_cast<const osg::Matrixd*>(&parameters->colorMappingMatrix));
    }

    protected override void captureCameraColorOutput( agxSensor.Camera camera, CameraColorOutput agxOutput )
    {
      if ( !m_colorOutputMap.TryGetValue( agxOutput, out var output ) )
        return;

      output.Capture();
    }

    protected override bool hasCameraColorOutputUnreadData( agxSensor.Camera camera, CameraColorOutput agxOutput, bool markAsRead )
    {
      if ( !m_colorOutputMap.TryGetValue( agxOutput, out var output ) )
        return false;
      return output.HasUnreadData( markAsRead );
    }

    protected override void setCameraDepthOutput( agxSensor.Camera camera, CameraDepthOutput output, CameraDepthOutputParameters parameters )
    {
      //  if (auto neoCameraOutput = self().getOrCreateBackendDepthOutputForInternal(camera, output)) {
      //    neoCameraOutput->setImageSize(agx::Vec2i {parameters->resolutionX, parameters->resolutionY});
      //    neoCameraOutput->setDepthUnit(parameters->depthUnit);

      //    if (parameters->framerate > 0.0)
      //      neoCameraOutput->setConstantCapture(parameters->framerate);
      //    else
      //      neoCameraOutput->setManualCapture();

      //    neoCameraOutput->setRelativeIlluminanceCutoff(
      //      agx::RangeReal {parameters->relativeIlluminanceCutoff.lower, parameters->relativeIlluminanceCutoff.upper});

      //    switch (parameters->channelType) {
      //      case agxSensor::CameraOutputChannelType::U8:
      //        neoCameraOutput->setOutputChannelType(NeoCameraImageOutput::U8);
      //        break;
      //      case agxSensor::CameraOutputChannelType::I8:
      //        neoCameraOutput->setOutputChannelType(NeoCameraImageOutput::I8);
      //        break;
      //      case agxSensor::CameraOutputChannelType::U16:
      //        neoCameraOutput->setOutputChannelType(NeoCameraImageOutput::U16);
      //        break;
      //      case agxSensor::CameraOutputChannelType::I16:
      //        neoCameraOutput->setOutputChannelType(NeoCameraImageOutput::I16);
      //        break;
      //      case agxSensor::CameraOutputChannelType::U32:
      //        neoCameraOutput->setOutputChannelType(NeoCameraImageOutput::U32);
      //        break;
      //      case agxSensor::CameraOutputChannelType::I32:
      //        neoCameraOutput->setOutputChannelType(NeoCameraImageOutput::I32);
      //        break;
      //      case agxSensor::CameraOutputChannelType::F32:
      //        neoCameraOutput->setOutputChannelType(NeoCameraImageOutput::F32);
      //        break;
      //      case agxSensor::CameraOutputChannelType::U64:
      //        neoCameraOutput->setOutputChannelType(NeoCameraImageOutput::U64);
      //        break;
      //      case agxSensor::CameraOutputChannelType::I64:
      //        neoCameraOutput->setOutputChannelType(NeoCameraImageOutput::I64);
      //        break;
      //      case agxSensor::CameraOutputChannelType::F64:
      //        neoCameraOutput->setOutputChannelType(NeoCameraImageOutput::F64);
      //        break;
      //      default:
      //        break;
      //    }
      //  }
    }

    protected override void captureCameraDepthOutput( agxSensor.Camera camera, CameraDepthOutput output )
    {
      //  if (auto neoCameraOutput = self().getOrCreateBackendDepthOutputForInternal(camera, output))
      //    neoCameraOutput->capture();
    }

    protected override bool hasCameraDepthOutputUnreadData( agxSensor.Camera camera, CameraDepthOutput output, bool markAsRead )
    {
      //  if (auto neoCameraOutput = self().getOrCreateBackendDepthOutputForInternal(camera, output))
      //    return neoCameraOutput->hasUnreadData(markAsRead);
      //  else
      //    return false;
      return false;
    }
  }
}

//NeoCameraSensorBackend* s_neoCameraSensorBackendInstance = nullptr;

//void NeoCameraSensorBackend::configure(
//  osg::Group* sceneGraph, osg::Node* sceneNode, agxSDK::Simulation* simulation /*= nullptr*/, bool override /*= false*/)
//{
//  bool uninitialized =
//    self().m_sceneGraph == nullptr && self().m_sceneNode == nullptr && self().m_simulation == nullptr;
//  if (uninitialized || override) {
//    bool sceneGraphChanged = self().m_sceneGraph != sceneGraph || self().m_sceneNode != sceneNode;
//    bool simulationChanged = self().m_simulation != simulation;

//    self().m_sceneGraph = sceneGraph;
//    self().m_sceneNode = sceneNode;
//    self().m_simulation = simulation;

//    bool sceneGraphInitialized = self().m_sceneGraph != nullptr && self().m_sceneNode != nullptr;
//    bool simulationInitialized = self().m_simulation != nullptr;

//    if (sceneGraphChanged && sceneGraphInitialized) {
//      for (auto& entry : self().m_sensorMap)
//        entry.second->registerToSceneGraph(self().m_sceneGraph, self().m_sceneNode);
//    }

//    if (simulationChanged && simulationInitialized)
//      for (auto& entry : self().m_sensorMap)
//        entry.second->setConnectedSimulation(self().m_simulation);
//  }
//}

//void NeoCameraSensorBackend::injectAsCameraDefault()
//{
//  agxSensor::Camera::setDefaultCameraBackend(*self().m_backend);
//}

//NeoCamera* NeoCameraSensorBackend::getOrCreateBackendCameraFor(agxSensor::Camera* camera)
//{
//  return self().getOrCreateBackendCameraForInternal(camera);
//}

//void NeoCameraSensorBackend::eraseBackendCameraOf(agxSensor::Camera* camera)
//{
//  return self().eraseBackendCameraOfInternal(camera);
//}

//bool NeoCameraSensorBackend::hasBackendCamera(agxSensor::Camera* camera)
//{
//  return self().hasBackendCameraInternal(camera);
//}

//NeoCameraSensorBackend& NeoCameraSensorBackend::self()
//{
//  if (s_neoCameraSensorBackendInstance == nullptr)
//    s_neoCameraSensorBackendInstance = new NeoCameraSensorBackend();
//  return *s_neoCameraSensorBackendInstance;
//}

//void NeoCameraSensorBackend::setCameraColorOutputAddress(
//  agxSensor::Camera* camera, agxSensor::CameraColorOutput* output, void* outputAddress)
//{
//  if (auto neoCameraOutput = self().getOrCreateBackendColorOutputForInternal(camera, output))
//    neoCameraOutput->setExternalData(outputAddress);
//}

//void NeoCameraSensorBackend::setCameraDepthOutputAddress(
//  agxSensor::Camera* camera, agxSensor::CameraDepthOutput* output, void* outputAddress)
//{
//  if (auto neoCameraOutput = self().getOrCreateBackendDepthOutputForInternal(camera, output))
//    neoCameraOutput->setExternalData(outputAddress);
//}

//void NeoCameraSensorBackend::updateAllIlluminationStrengths(NeoCamera* neoCamera)
//{
//  auto entries = self().m_sensorIlluminationMap.find(neoCamera);
//  if (entries != self().m_sensorIlluminationMap.end())
//    for (auto& lightEntryPair : entries->second)
//      updateIlluminationStrength(neoCamera, lightEntryPair.second.get());
//}

//NeoCamera* NeoCameraSensorBackend::getOrCreateBackendCameraForInternal(agxSensor::Camera* camera)
//{
//  auto entry = m_sensorMap.find(camera);
//  if (entry == m_sensorMap.end()) {
//    NeoCameraRef neoCamera = new NeoCamera();

//    bool sceneGraphInitialized = m_sceneGraph != nullptr && m_sceneNode != nullptr;
//    if (sceneGraphInitialized)
//      neoCamera->registerToSceneGraph(m_sceneGraph, m_sceneNode);

//    bool simulationInitialized = m_simulation != nullptr;
//    if (simulationInitialized)
//      neoCamera->setConnectedSimulation(m_simulation);

//    return m_sensorMap.insert_or_assign(camera, neoCamera).first->second.get();
//  }
//  else
//    return entry->second.get();
//}

//void NeoCameraSensorBackend::eraseBackendCameraOfInternal(agxSensor::Camera* camera)
//{
//  m_sensorColorOutputMap.erase(camera);

//  auto camerEntry = m_sensorMap.find(camera);
//  if (camerEntry != m_sensorMap.end()) {
//    m_sensorIlluminationMap.erase(camerEntry->second);
//    m_sensorMap.erase(camerEntry);
//  }
//}

//bool NeoCameraSensorBackend::hasBackendCameraInternal(agxSensor::Camera* camera)
//{
//  return m_sensorMap.find(camera) != m_sensorMap.end();
//}

//NeoCameraSensorBackend::ActiveIllumination* NeoCameraSensorBackend::getOrCreateBackendIlluminationForInternal(
//  agxOSG::NeoCamera* neoCamera, agxSensor::CameraActiveIllumination* illumination)
//{
//  auto entry = m_sensorIlluminationMap.find(neoCamera);
//  if (entry == m_sensorIlluminationMap.end())
//    entry = m_sensorIlluminationMap.insert_or_assign(neoCamera, NeoCameraSensorIlluminationMap::mapped_type {}).first;

//  auto illuminationEntry = entry->second.find(illumination);
//  if (illuminationEntry == entry->second.end()) {
//    std::unique_ptr<ActiveIllumination> lightEntry(new ActiveIllumination {
//      new osg::MatrixTransform(), new osg::LightSource(), 0.0, {0.0, 0.0, 0.0},
//           neoCamera->getReferenceIlluminance()
//    });
//    lightEntry->source->getLight()->setPosition(osg::Vec4 {0.0f, 0.0f, 0.0f, 1.0f});
//    lightEntry->source->getLight()->setDirection(osg::Vec3 {1.0f, 0.0f, 0.0f});
//    lightEntry->transform->addChild(lightEntry->source);
//    neoCamera->add(lightEntry->transform);
//    return entry->second.insert_or_assign(illumination, std::move(lightEntry)).first->second.get();
//  }
//  else
//    return illuminationEntry->second.get();
//}

//NeoCameraColorImageOutput* NeoCameraSensorBackend::getOrCreateBackendColorOutputForInternal(
//  agxSensor::Camera* camera, agxSensor::CameraColorOutput* output)
//{
//  auto entry = m_sensorColorOutputMap.find(camera);
//  if (entry == m_sensorColorOutputMap.end())
//    entry = m_sensorColorOutputMap.insert_or_assign(camera, NeoCameraSensorColorOutputMap::mapped_type {}).first;

//  auto outputEntry = entry->second.find(output);
//  if (outputEntry == entry->second.end()) {
//    if (auto neoCamera = getOrCreateBackendCameraFor(camera)) {
//      NeoCameraColorImageOutputRef neoCameraOutput = new NeoCameraColorImageOutput();
//      neoCamera->add(neoCameraOutput);
//      return entry->second.insert_or_assign(output, neoCameraOutput).first->second.get();
//    }
//    else
//      return nullptr;
//  }
//  else
//    return outputEntry->second.get();
//}

//NeoCameraDepthImageOutput* NeoCameraSensorBackend::getOrCreateBackendDepthOutputForInternal(
//  agxSensor::Camera* camera, agxSensor::CameraDepthOutput* output)
//{
//  auto entry = m_sensorDepthOutputMap.find(camera);
//  if (entry == m_sensorDepthOutputMap.end())
//    entry = m_sensorDepthOutputMap.insert_or_assign(camera, NeoCameraSensorDepthOutputMap::mapped_type {}).first;

//  auto outputEntry = entry->second.find(output);
//  if (outputEntry == entry->second.end()) {
//    if (auto neoCamera = getOrCreateBackendCameraFor(camera)) {
//      NeoCameraDepthImageOutputRef neoCameraOutput = new NeoCameraDepthImageOutput();
//      neoCamera->add(neoCameraOutput);
//      return entry->second.insert_or_assign(output, neoCameraOutput).first->second.get();
//    }
//    else
//      return nullptr;
//  }
//  else
//    return outputEntry->second.get();
//}

//#endif
