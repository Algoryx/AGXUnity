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
using System.ComponentModel.Design;

namespace AGXUnity.Sensor
{
  /// <summary>
  /// WIP component for streaming data to agx sensor environment
  /// </summary>
  [AddComponentMenu("AGXUnity/Sensors/Sensor Environment")]
  [HelpURL("https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#sensors")]
  public class SensorEnvironment : UniqueGameObject<SensorEnvironment>
  {
    // TODO possible choice on what to stream
    // TODO looking at https://git.algoryx.se/algoryx/unreal/agxunreal/-/blob/feature/agx-lidar/AGXUnrealDev/Plugins/AGXUnreal/Source/AGXUnreal/Public/Sensors/AGX_SensorEnvironment.h?ref_type=heads


    /// <summary>
    /// Native instance, created in Start/Initialize.
    /// </summary>
    public agxSensor.Environment Native { get; private set; } = null;

    /// <summary>
    /// Keeping track of invisible objects is extra work, which can be skipped for performance reasons
    /// </summary>
    public bool DisabledObjectsVisibleToSensors = false;

    /// <summary>
    /// Show log messages on each thing added to the sensor environment
    /// </summary>
    public bool DebugLogOnAdd = false;

    // Internal lists
    private readonly List<MeshFilter> m_meshFilters = new();
    private readonly Dictionary<UnityEngine.Mesh, RtShape> m_rtShapes = new();
    private readonly Dictionary<UnityEngine.MeshFilter, RtShapeInstance> m_rtShapeInstances = new();
    private readonly List<LidarSensor> m_lidars = new();
    private readonly List<agxTerrain.Terrain> m_deformableTerrains = new();
    private readonly List<agxTerrain.TerrainPager> m_deformableTerrainPagers = new();
    private readonly List<agxWire.Wire> m_wires = new();
    private readonly List<agxCable.Cable> m_cables = new();

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

    public void RegisterLidarSensor(LidarSensor lidar)
    {
      if (m_lidars.Contains(lidar))
        return;

      if (!lidar.InitializeLidar(this, GenerateOutputID()))
      {
        Debug.LogWarning("Could not initialize lidar");
        return;
      }

      if (!Native.add(lidar.Native))
        Debug.LogWarning($"Lidar '{lidar.name}' not added to SensorEnvironment properly!");
      else if (DebugLogOnAdd)
        Debug.Log($"Sensor Environment '{name}' added Lidar '{lidar.name}'.");

      m_lidars.Add(lidar);
    }

    // Call this when adding MeshFilters during runtime from custom script
    public void RegisterMeshfilter(MeshFilter meshFilter)
    {
      if (!m_meshFilters.Contains(meshFilter))
        m_meshFilters.Add(meshFilter);
      
      if (m_rtShapeInstances.ContainsKey(meshFilter))
        return;

      UnityEngine.Mesh mesh = meshFilter.sharedMesh;
      bool newMesh = false;

      if (!m_rtShapes.TryGetValue(mesh, out RtShape rtShape))
      {
        rtShape = CreateShape(mesh);
        m_rtShapes[mesh] = rtShape;
        newMesh = true;
      }

      m_rtShapeInstances[meshFilter] = CreateShapeInstance(
        rtShape,
        meshFilter.transform.rotation,
        meshFilter.transform.position,
        meshFilter.transform.lossyScale);

      if (DebugLogOnAdd)
        Debug.Log($"SensorEnvironment '{name}' added shapeInstance for mesh on '{meshFilter.gameObject.name}', added shape: {newMesh}");
    }

    private RtShape CreateShape(UnityEngine.Mesh mesh)
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
    private RtShapeInstance CreateShapeInstance(RtShape rtShape, Quaternion rotation, Vector3 position, Vector3 scale)
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
    public bool AddAGXModel<T>(T scriptComponent) where T : class
    {
      if (scriptComponent == null || GetNative(scriptComponent) == null)
      {
        Debug.LogWarning("Problem getting native instance of AGX model!");
        return false;
      }

      bool added = false;
      switch (GetNative(scriptComponent))
      {
        case agxTerrain.Terrain dt:
          added = Native.add(dt);
          RtSurfaceMaterial.set(dt, m_rtDefaultSurfaceMaterial);
          m_deformableTerrains.Add(dt);
          break;

        case agxTerrain.TerrainPager dtp:
          added = Native.add(dtp);
          RtSurfaceMaterial.set(dtp, m_rtDefaultSurfaceMaterial);
          m_deformableTerrainPagers.Add(dtp);
          break;

        case agxWire.Wire w:
          added = Native.add(w);
          RtSurfaceMaterial.set(w, m_rtDefaultSurfaceMaterial);
          m_wires.Add(w);
          break;

        case agxCable.Cable c:
          added = Native.add(c);
          RtSurfaceMaterial.set(c, m_rtDefaultSurfaceMaterial);
          m_cables.Add(c);
          break;

        default:
          Debug.LogWarning("AGX type not handled by this method - for colliders, register the visual mesh instead");
          break;
      }

      if (!added)
        Debug.LogWarning($"Could not add {scriptComponent.GetType()} model in object '{scriptComponent.GetType().GetProperty("name")?.GetValue(scriptComponent, null)}'!");
      else if (DebugLogOnAdd)
        Debug.Log($"Sensor Environment '{name}' added {scriptComponent.GetType()} in object '{scriptComponent.GetType().GetProperty("name")?.GetValue(scriptComponent, null)}'.");

      return true;
    }

    private List<T> FindValidComponents<T>(bool includeInactive = false) where T : UnityEngine.Component
    {
      return FindObjectsOfType<T>(includeInactive)
          .Where(component =>
              component.gameObject.scene.IsValid() &&
              component.gameObject.transform.root.gameObject.scene == component.gameObject.scene)
          .ToList();
    }

    protected override bool Initialize()
    {
      var simulation = GetSimulation();
      simulation.setPreIntegratePositions(true); // From Python, check if this is needed

      // In order to properly dispose of Raytrace stuff (before cleanup()) we need to register this callback
      Simulation.Instance.RegisterDisposeCallback(DisposeRT);

      // TODO: temp material stuff
      m_rtLambertianOpaqueMaterialInstance = RtMaterialInstance.create(RtMaterialHandle.Type.OPAQUE_LAMBERTIAN);
      m_rtDefaultSurfaceMaterial = RtLambertianOpaqueMaterial.create();

      // Check ray trace device compatibility // TODO activate on setting
      //Debug.Log("isRaytraceSupported: " + RtConfig.isRaytraceSupported());
      //Debug.Log("verifyRaytraceSupported: " + RtConfig.verifyRaytraceSupported());
      //Debug.Log("listRaytraceDevices:" + RtConfig.listRaytraceDevices().Count());
      //if (RtConfig.getRaytraceDevice() != simulation.RayTraceDeviceIndex) // TODO From Unreal


      Native = agxSensor.Environment.getOrCreate(simulation);

      FindValidComponents<LidarSensor>(true).ForEach(RegisterLidarSensor);
      FindValidComponents<MeshFilter>(true).ForEach(RegisterMeshfilter);

      FindValidComponents<DeformableTerrain>(true).ForEach(c => AddAGXModel(c.GetInitialized<DeformableTerrain>()));
      FindValidComponents<DeformableTerrainPager>(true).ForEach(c => AddAGXModel(c.GetInitialized<DeformableTerrainPager>()));
      FindValidComponents<Wire>(true).ForEach(c => AddAGXModel(c.GetInitialized<Wire>()));
      FindValidComponents<Cable>(true).ForEach(c => AddAGXModel(c.GetInitialized<Cable>()));

      return true;
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
      foreach (var lidar in m_lidars)
      {
        if (lidar == null || lidar.Native == null)
          continue;

        lidar.UpdateTransform();
      }
    }


    private void RemoveInstance(MeshFilter meshFilter)
    {
      var instance = m_rtShapeInstances[meshFilter];
      instance.Dispose();
      m_rtShapeInstances.Remove(meshFilter);
    }
    private void UpdateShapeInstances()
    {
      // Walk through registered meshes and remove deleted from list plus optionally handle disabled meshes
      for (int i = m_meshFilters.Count - 1; i >= 0; i--)
      {
        var meshFilter = m_meshFilters[i];

        if (meshFilter == null)
        {
          m_meshFilters.RemoveAt(i);
          continue;
        }

        // Handle invisible objects
        if (!DisabledObjectsVisibleToSensors)
        {
          bool visible = meshFilter.gameObject.activeInHierarchy;
          bool containsKey = m_rtShapeInstances.ContainsKey(meshFilter);

          if (visible && !containsKey)
            RegisterMeshfilter(meshFilter);
          else if (!visible && containsKey)
            RemoveInstance(meshFilter);
        }
      }

      foreach (var shapeInstance in m_rtShapeInstances)
      {
        var meshFilter = shapeInstance.Key;

        shapeInstance.Value.setTransform(
            new AffineMatrix4x4(
                meshFilter.transform.rotation.ToHandedQuat(),
                meshFilter.transform.position.ToHandedVec3()),
                meshFilter.transform.lossyScale.ToHandedVec3());
      }
    }

    protected override void OnEnable()
    {
    }

    // Note that disabling this component does not stop the sensor simulation
    protected override void OnDisable()
    {
    }

    public void DisposeRT()
    {
      // TODO maybe lidars should have their own delegates
      foreach (var lidar in m_lidars)
      {
        if (lidar.Native != null)
          Native.remove(lidar.Native);
      }

      foreach (var dt in m_deformableTerrains)
        Native.remove(dt);
      m_deformableTerrains.Clear();

      foreach (var dtp in m_deformableTerrainPagers)
        Native.remove(dtp);
      m_deformableTerrainPagers.Clear();

      foreach (var w in m_wires)
        Native.remove(w);
      m_wires.Clear();

      foreach (var c in m_cables)
        Native.remove(c);
      m_cables.Clear();

      foreach (var rtShapeInstance in m_rtShapeInstances)
      {
        rtShapeInstance.Value.Dispose();
      }
      m_rtShapeInstances.Clear();
      foreach (var rtShape in m_rtShapes)
      {
        rtShape.Value.Dispose();
      }
      m_rtShapes.Clear();
      m_meshFilters.Clear();

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
  }
}
