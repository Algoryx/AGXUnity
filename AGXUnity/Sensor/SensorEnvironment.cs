using System.Collections;
using UnityEngine;
using AGXUnity.Utils;
using System.Linq;
using System.Collections.Generic;
using agxSensor;
using agx;
using agxCollide;
using System.Runtime.InteropServices;
using System;
using UnityEngine.Profiling;
using AGXUnity.Model;

namespace AGXUnity.Sensor
{
  /// <summary>
  /// WIP component for streaming data to agx sensor environment
  /// </summary>
  [AddComponentMenu( "AGXUnity/Sensors/Sensor Environment" )]
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#sensors" )]
  public class SensorEnvironment : UniqueGameObject<SensorEnvironment>
  {
    // TODO possible choice on what to stream
    // TODO looking at https://git.algoryx.se/algoryx/unreal/agxunreal/-/blob/feature/agx-lidar/AGXUnrealDev/Plugins/AGXUnreal/Source/AGXUnreal/Public/Sensors/AGX_SensorEnvironment.h?ref_type=heads


    /// <summary>
    /// Native instance, created in Start/Initialize.
    /// </summary>
    public agxSensor.Environment Native { get; private set; } = null;

    // TODO if we want to ignore invisible objects, how to deal with enabling / disabling during runtime? Or new ones spawned??
    //public bool IgnoreInvisibleObjects = true;


    /**
    * List of all Lidar Sensor Components that should be active in the simulation.
    * Any Lidar Sensor Components that should be active has to be added by the user to this Array.
    */
    public List<LidarSensor> LidarSensors;


    public bool DebugLogOnAdd = false;

    private Dictionary<UnityEngine.Mesh, RtShape> m_rtShapes = new Dictionary<UnityEngine.Mesh, RtShape>();
    private Dictionary<UnityEngine.MeshFilter, RtShapeInstance> m_rtShapeInstances = new Dictionary<UnityEngine.MeshFilter, RtShapeInstance>();

    private List<agxTerrain.Terrain> deformableTerrains = new List<agxTerrain.Terrain>();
    private List<agxTerrain.TerrainPager> deformableTerrainPagers = new List<agxTerrain.TerrainPager>();
    private List<agxWire.Wire> wires = new List<agxWire.Wire>();
    private List<agxCable.Cable> cables = new List<agxCable.Cable>();

    /**
     * The Ambient material used by the Sensor Environment.
     * This is used to simulate atmospheric effects on the Lidar laser rays, such as rain or fog.
     */
    //	public LidarAmbientMaterial AmbientMaterial = null;

    //TODO temporary solution, fix materials
    private RtMaterialInstance m_rtLambertianOpaqueMaterialInstance;
    private RtSurfaceMaterial m_rtDefaultSurfaceMaterial;

    private uint m_currentOutputID = 1;

    // Always use this method in order to have each lider use a unique output id
    private uint GenerateOutputID()
    {
      return m_currentOutputID++;
    }


    RtShape CreateShape(UnityEngine.Mesh mesh)
    {
      Profiler.BeginSample("CreateShape");

      var meshTriangles = mesh.triangles;
      var meshVertices = mesh.vertices;

      int tris = meshTriangles.Length;
      int verts = meshVertices.Length;

      UInt32Vector indices = new UInt32Vector(tris);
      Vec3Vector vertices = new Vec3Vector(verts);

      for (int i = 0; i < tris; i++)
        indices.Add((uint)meshTriangles[i]);
      for (int i = 0; i < verts; i++)
        vertices.Add(meshVertices[i].ToVec3());

      var rtShape = RtShape.create(vertices, indices);

      Profiler.EndSample();

      return rtShape;
    }


    private int m_entityId = 0;
    RtShapeInstance CreateShapeInstance(RtShape rtShape, Quaternion rotation, Vector3 position, Vector3 scale)
    {
      Profiler.BeginSample("CreateShapeInstance");
      RtInstanceData data = new RtInstanceData(m_rtLambertianOpaqueMaterialInstance, (RtEntityId)(++m_entityId));
      RtShapeInstance shapeInstance = RtShapeInstance.create(Native.getScene(), rtShape, data);

      shapeInstance.setTransform(
        new AffineMatrix4x4(
          Extensions.ToHandedQuat(rotation),
          Extensions.ToHandedVec3(position)),
        scale.ToHandedVec3());


      Profiler.EndSample();
      return shapeInstance;
    }

    private object GetNative<T>(T scriptComponent) => scriptComponent.GetType().GetProperty("Native")?.GetValue(scriptComponent, null);
    private bool AddAGXModel<T>(T scriptComponent) where T : class
    {
      if (scriptComponent == null || GetNative(scriptComponent) == null)
      {
        Debug.LogWarning("Problem...: " + (GetNative(scriptComponent) == null));
        return false;
      }

      bool added = false;
      switch (GetNative(scriptComponent))
      {
        case agxTerrain.Terrain dt:
          added = Native.add(dt);
          RtSurfaceMaterial.set(dt, m_rtDefaultSurfaceMaterial);
          deformableTerrains.Add(dt);
          break;

        case agxTerrain.TerrainPager dtp:
          added = Native.add(dtp);
          RtSurfaceMaterial.set(dtp, m_rtDefaultSurfaceMaterial);
          deformableTerrainPagers.Add(dtp);
          break;

        case agxWire.Wire w:
          added = Native.add(w);
          RtSurfaceMaterial.set(w, m_rtDefaultSurfaceMaterial);
          wires.Add(w);
          break;

        case agxCable.Cable c:
          added = Native.add(c);
          RtSurfaceMaterial.set(c, m_rtDefaultSurfaceMaterial);
          cables.Add(c);
          break;

        default:
          Debug.LogWarning("Unknown type");
          break;
      }

      if (!added)
        Debug.LogWarning($"Could not add {scriptComponent.GetType()} model in object '{scriptComponent.GetType().GetProperty("name")?.GetValue(scriptComponent, null)}'!");
      else if (DebugLogOnAdd)
        Debug.Log($"Sensor Environment '{name}' added {scriptComponent.GetType()} in object '{scriptComponent.GetType().GetProperty("name")?.GetValue(scriptComponent, null)}'.");

      return true;
    }

    private List<T> FindValidComponents<T>() where T : UnityEngine.Component
    {
      return FindObjectsOfType<T>(true)
          .Where(component =>
              component.gameObject.scene.IsValid() &&
              component.gameObject.activeInHierarchy &&
              component.gameObject.transform.root.gameObject.scene == component.gameObject.scene)
          .ToList();
    }

    protected override bool Initialize()
    {
      var simulation = GetSimulation();
      simulation.setPreIntegratePositions(true); // From Python, don't think this is needed

      // TODO: temp material stuff
      m_rtLambertianOpaqueMaterialInstance = RtMaterialInstance.create(RtMaterialHandle.Type.OPAQUE_LAMBERTIAN);
      m_rtDefaultSurfaceMaterial = RtLambertianOpaqueMaterial.create();


      // In order to properly dispose of Raytrace stuff (before cleanup()) we need to register this callback
      Simulation.Instance.RegisterDisposeCallback(DisposeRT);

      Native = agxSensor.Environment.getOrCreate(simulation);

      if (Native == null)
      {
        // TODO error message, or remove this check
        Debug.LogWarning("No native yet");
        return false;
      }

      // TODO in Unreal there seem to be a property on the Simulation thing that can choose the raytrace device... Choose what to do with that
      //if (RtConfig.getRaytraceDevice() != simulation.RayTraceDeviceIndex)
      {

      }


      RegisterAllMeshfilters();

      // TODO for now the list of lidars is public but maybe we should auto-add them
      //RegisterAllLidarSensors();
      foreach (var lidar in LidarSensors)
      {
        if (!lidar.InitializeLidar(this, GenerateOutputID()))
        {
          Debug.LogWarning("Lidar not initialized");
          continue;
        }

        if (!Native.add(lidar.Native))
          Debug.LogWarning($"Lidar '{lidar.name}' not added properly!");
        else if (DebugLogOnAdd)
          Debug.Log($"Sensor Environment '{name}' added Lidar '{lidar.name}'.");
      }

      // TODO determine how these types will be found and added / removed, for now just at init
      var deformableTerrains = FindValidComponents<DeformableTerrain>();
      foreach (DeformableTerrain dt in deformableTerrains)
        AddAGXModel(dt.GetInitialized<DeformableTerrain>());
      var deformableTerrainPagers = FindValidComponents<DeformableTerrainPager>();
      foreach (DeformableTerrainPager dtp in deformableTerrainPagers)
        AddAGXModel(dtp.GetInitialized<DeformableTerrainPager>());
      var wires = FindValidComponents<Wire>();
      foreach (Wire w in wires)
        AddAGXModel(w.GetInitialized<Wire>());
      var cables = FindValidComponents<Cable>();
      foreach (Cable c in cables)
        AddAGXModel(c.GetInitialized<Cable>());

      //Debug.Log("Lidar Sensor Count: " + LidarSensors.Count);
      //Debug.Log("isRaytraceSupported: " + RtConfig.isRaytraceSupported());
      //Debug.Log("verifyRaytraceSupported: " + RtConfig.verifyRaytraceSupported());
      //Debug.Log("listRaytraceDevices:" + RtConfig.listRaytraceDevices().Count());
      //Debug.Log("m_rtShapes: " + m_rtShapes.Count);
      //Debug.Log("m_rtShapeInstances: " + m_rtShapeInstances.Count);

      return true;
    }

    public void RegisterAllMeshfilters()
    {
      var filters = FindValidComponents<MeshFilter>();

      RtShape rtShape = null;
      foreach (var meshFilter in filters)
      {
        UnityEngine.Mesh mesh = meshFilter.sharedMesh;

        if (!m_rtShapes.TryGetValue(mesh, out rtShape))
        {
          rtShape = CreateShape(mesh);
          m_rtShapes[mesh] = rtShape;
        }

        m_rtShapeInstances[meshFilter] = CreateShapeInstance(
          rtShape,
          meshFilter.transform.rotation,
          meshFilter.transform.position,
          meshFilter.transform.lossyScale);
      }
    }

    public void FixedUpdate()
    {
      if (Native == null)
        return;

      UpdateLidars();
      UpdateShapeInstances();
    }

    private void UpdateLidars()
    {
      foreach (var lidar in LidarSensors)
      {
        //TODO could have entire method to subtract from this list but just skip invalid / disabled for now
        if (lidar == null || lidar.Native == null || !lidar.isActiveAndEnabled)
          continue;

        lidar.UpdateTransform();
      }
    }

    private void UpdateShapeInstances()
    {
      foreach (var shapeInstance in m_rtShapeInstances)
      {
        var mesh = shapeInstance.Key;
        if (mesh == null || !mesh.gameObject.activeSelf)
          continue;

        shapeInstance.Value.setTransform(
          new AffineMatrix4x4(
            mesh.transform.rotation.ToHandedQuat(),
            mesh.transform.position.ToHandedVec3()),
          mesh.transform.lossyScale.ToHandedVec3()); //TODO verify global scale and not local
      }
    }

    protected override void OnEnable()
    {
    }

    protected override void OnDisable()
    {
    }

    public void DisposeRT()
    {
      // TODO maybe lidars should have their own delegates
      foreach (var lidar in LidarSensors)
      {
        if (lidar.Native != null)
          Native.remove(lidar.Native);
      }

      foreach (var dt in deformableTerrains)
        Native.remove(dt);
      deformableTerrains.Clear();

      foreach (var dtp in deformableTerrainPagers)
        Native.remove(dtp);
      deformableTerrainPagers.Clear();

      foreach (var w in wires)
        Native.remove(w);
      wires.Clear();

      foreach (var c in cables)
        Native.remove(c);
      cables.Clear();

      foreach (var rtShapeInstance in m_rtShapeInstances)
      {
        rtShapeInstance.Value.Dispose();
      }
      m_rtShapeInstances = null;
      foreach (var rtShape in m_rtShapes)
      {
        rtShape.Value.Dispose();
      }
      m_rtShapes = null;

      m_rtDefaultSurfaceMaterial.Dispose();
      m_rtDefaultSurfaceMaterial = null;
      m_rtLambertianOpaqueMaterialInstance.Dispose();
      m_rtLambertianOpaqueMaterialInstance = null;

      Native = null;
    }

    protected override void OnDestroy()
    {
      if (Simulation.HasInstance)
        Simulation.Instance.UnRegisterDisposeCallback(DisposeRT);

      base.OnDestroy();
    }

    /*
    X bool AddLidar(UAGX_LidarSensorComponent* Lidar);
    bool AddMesh(UStaticMeshComponent* Mesh, int32 LOD = -1);
    bool AddAGXMesh(UAGX_SimpleMeshComponent* Mesh);
    bool AddInstancedMesh(UInstancedStaticMeshComponent* Mesh, int32 LOD = -1);
    bool AddInstancedMeshInstance(UInstancedStaticMeshComponent* Mesh, int32 Index, int32 LOD = -1);
    bool AddTerrain(AAGX_Terrain* Terrain);
    bool AddWire(UAGX_WireComponent* Wire);
    bool RemoveLidar(UAGX_LidarSensorComponent* Lidar);
    bool RemoveMesh(UStaticMeshComponent* Mesh);
    bool RemoveInstancedMesh(UInstancedStaticMeshComponent* Mesh);
    bool RemoveInstancedMeshInstance(UInstancedStaticMeshComponent* Mesh, int32 Index);
    bool RemoveTerrain(AAGX_Terrain* Terrain);
    bool RemoveWire(UAGX_WireComponent* Wire);

        */
  }
}
