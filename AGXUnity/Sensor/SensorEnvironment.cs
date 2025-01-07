using agx;
using agxSensor;
using AGXUnity.Model;
using AGXUnity.Utils;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Profiling;

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
    private readonly Dictionary<ScriptComponent, bool> m_agxComponents = new();

    /**
     * The Ambient material used by the Sensor Environment.
     * This is used to simulate atmospheric effects on the Lidar laser rays, such as rain or fog.
     */
    //	public LidarAmbientMaterial AmbientMaterial = null;

    //TODO temporary solution, fix materials
    private RtSurfaceMaterial m_rtDefaultSurfaceMaterial;
    private uint m_currentOutputID = 1;
    private int m_currentEntityId = 0;

    // Always use this method in order to have each lidar use a unique output id
    public uint GenerateOutputID()
    {
      return m_currentOutputID++;
    }

    public void RegisterLidarSensor( LidarSensor lidar )
    {
      if ( m_lidars.Contains( lidar ) )
        return;

      //if ( !lidar.InitializeLidar( this, GenerateOutputID() ) ) {
      //  Debug.LogWarning( "Could not initialize lidar" );
      //  return;
      //}

      if ( !Native.add( lidar.Native ) )
        Debug.LogWarning( $"Lidar '{lidar.name}' not added to SensorEnvironment properly!" );
      else if ( DebugLogOnAdd )
        Debug.Log( $"Sensor Environment '{name}' added Lidar '{lidar.name}'." );

      m_lidars.Add( lidar );
    }

    // Call this when adding MeshFilters during runtime from custom script
    public void RegisterMeshfilter( MeshFilter meshFilter )
    {
      if ( !m_meshFilters.Contains( meshFilter ) )
        m_meshFilters.Add( meshFilter );

      if ( m_rtShapeInstances.ContainsKey( meshFilter ) )
        return;

      UnityEngine.Mesh mesh = meshFilter.sharedMesh;
      bool newMesh = false;

      if ( !m_rtShapes.TryGetValue( mesh, out RtShape rtShape ) ) {
        rtShape = CreateShape( mesh );
        m_rtShapes[ mesh ] = rtShape;
        newMesh = true;
      }

      var lidarSurfaceMaterialContainer = meshFilter.gameObject.GetComponent<LidarSurfaceMaterialContainer>();

      m_rtShapeInstances[ meshFilter ] = CreateShapeInstance(
        rtShape,
        meshFilter.transform.rotation,
        meshFilter.transform.position,
        meshFilter.transform.lossyScale,
        lidarSurfaceMaterialContainer );

      if ( DebugLogOnAdd )
        Debug.Log( $"SensorEnvironment '{name}' added shapeInstance for mesh on '{meshFilter.gameObject.name}', added shape: {newMesh}" );
    }

    private RtShape CreateShape( UnityEngine.Mesh mesh )
    {
      Profiler.BeginSample( "CreateShape" );

      var meshTriangles = mesh.triangles;
      var meshVertices = mesh.vertices;

      int tris = meshTriangles.Length;
      int verts = meshVertices.Length;

      UInt32Vector indices = new UInt32Vector(tris);
      Vec3Vector vertices = new Vec3Vector(verts);

      for ( int i = 0; i < tris; i++ )
        indices.Add( (uint)meshTriangles[ i ] );
      for ( int i = 0; i < verts; i++ )
        vertices.Add( meshVertices[ i ].ToVec3() );

      var rtShape = RtShape.create(vertices, indices);

      Profiler.EndSample();

      return rtShape;
    }

    private RtShapeInstance CreateShapeInstance( RtShape rtShape, Quaternion rotation, Vector3 position, Vector3 scale, LidarSurfaceMaterialContainer lidarSurfaceMaterialContainer )
    {
      RtMaterialInstance rtMaterialInstance = null;
      if ( lidarSurfaceMaterialContainer != null )
        rtMaterialInstance = lidarSurfaceMaterialContainer.LidarSurfaceMaterialDefinition.GetRtMaterialInstance();

      Profiler.BeginSample( "CreateShapeInstance" );
      RtInstanceData data = new RtInstanceData(rtMaterialInstance ?? m_rtDefaultSurfaceMaterial.ToMaterialInstance(), (RtEntityId)(++m_currentEntityId));
      RtShapeInstance shapeInstance = RtShapeInstance.create(Native.getScene(), rtShape, data);

      shapeInstance.setTransform(
        new AffineMatrix4x4(
          Extensions.ToHandedQuat( rotation ),
          Extensions.ToHandedVec3( position ) ),
        scale.ToHandedVec3() );


      Profiler.EndSample();
      return shapeInstance;
    }

    public bool AddAGXModel( ScriptComponent scriptComponent )
    {
      if ( scriptComponent == null )
        return false;

      var lidarSurfaceMaterialContainer = scriptComponent.gameObject.GetComponent<LidarSurfaceMaterialContainer>();
      RtSurfaceMaterial rtMaterial = m_rtDefaultSurfaceMaterial;
      if ( lidarSurfaceMaterialContainer != null && lidarSurfaceMaterialContainer.LidarSurfaceMaterialDefinition != null )
        rtMaterial = lidarSurfaceMaterialContainer.LidarSurfaceMaterialDefinition.GetRtMaterial();

      bool added = false;
      if ( scriptComponent is DeformableTerrain dt ) {
        var c = dt.Native;
        RtSurfaceMaterial.set( c, rtMaterial );
        added = Native.add( c );
      }
      else if ( scriptComponent is DeformableTerrainPager dtp ) {
        var c = dtp.Native;
        RtSurfaceMaterial.set( c, rtMaterial );
        added = Native.add( c );
      }
      else if ( scriptComponent is Wire w ) {
        var c = w.Native;
        RtSurfaceMaterial.set( c, rtMaterial );
        added = Native.add( c );
      }
      else if ( scriptComponent is Cable ca ) {
        var c = ca.Native;
        RtSurfaceMaterial.set( c, rtMaterial );
        added = Native.add( c );
      }
      else {
        Debug.LogWarning( "AGX type not handled by this method. Hint: for colliders, register the visual mesh instead" );
      }

      if ( DebugLogOnAdd )
        Debug.Log( $"Sensor Environment '{name}' added {scriptComponent.GetType()} in object '{scriptComponent.GetType().GetProperty( "name" )?.GetValue( scriptComponent, null )}'." );

      return true;
    }

    public bool RemoveAGXModel( ScriptComponent scriptComponent )
    {
      if ( scriptComponent == null ) {
        Debug.Log( "Component was null - Could not remove from sensor environment!" );
        return false;
      }

      if ( scriptComponent is DeformableTerrain dt ) {
        var c = dt.Native;
        Native.remove( c );
      }
      else if ( scriptComponent is DeformableTerrainPager dtp ) {
        var c = dtp.Native;
        Native.remove( c );
      }
      else if ( scriptComponent is Wire w ) {
        var c = w.Native;
        Native.remove( c );
      }
      else if ( scriptComponent is Cable ca ) {
        var c = ca.Native;
        Native.remove( c );
      }
      else {
        Debug.LogWarning( "AGX type not handled by this method" );
      }
      return true;
    }

    private List<T> FindValidComponents<T>( bool includeInactive = false ) where T : UnityEngine.Component
    {
      return FindObjectsOfType<T>( includeInactive )
          .Where( component =>
              component.gameObject.scene.IsValid() &&
              component.gameObject.transform.root.gameObject.scene == component.gameObject.scene )
          .ToList();
    }

    protected override bool Initialize()
    {
      //if ( !LicenseManager.LicenseInfo.HasModuleLogError( LicenseInfo.Module.AGXSensor, this ) )
      //  return false;

      var simulation = GetSimulation();
      simulation.setPreIntegratePositions( true ); // From Python, check if this is needed

      // In order to properly dispose of Raytrace stuff (before cleanup()) we need to register this callback
      Simulation.Instance.RegisterDisposeCallback( DisposeRT );

      // Default material
      m_rtDefaultSurfaceMaterial = RtLambertianOpaqueMaterial.create();

      // Check ray trace device compatibility // TODO activate on setting
      //Debug.Log("isRaytraceSupported: " + RtConfig.isRaytraceSupported());
      //Debug.Log("verifyRaytraceSupported: " + RtConfig.verifyRaytraceSupported());
      //Debug.Log("listRaytraceDevices:" + RtConfig.listRaytraceDevices().Count());
      //if (RtConfig.getRaytraceDevice() != simulation.RayTraceDeviceIndex) // TODO From Unreal

      Native = agxSensor.Environment.getOrCreate( simulation );

      // We need to initialize the surface materials
      var surfaceMaterials = FindObjectsOfType<LidarSurfaceMaterial>(true);
      foreach ( var sm in surfaceMaterials )
        sm.Init();

      FindValidComponents<MeshFilter>( true ).ForEach( RegisterMeshfilter );

      FindValidComponents<DeformableTerrain>( true ).ForEach( c => m_agxComponents.Add( c, c.gameObject.activeInHierarchy ) );
      FindValidComponents<DeformableTerrainPager>( true ).ForEach( c => m_agxComponents.Add( c, c.gameObject.activeInHierarchy ) );
      FindValidComponents<Wire>( true ).ForEach( c => m_agxComponents.Add( c, c.gameObject.activeInHierarchy ) );
      FindValidComponents<Cable>( true ).ForEach( c => m_agxComponents.Add( c, c.gameObject.activeInHierarchy ) );

      foreach ( var entry in m_agxComponents ) {
        if ( entry.Value )
          AddAGXModel( entry.Key.GetInitialized() );
      }

      return true;
    }

    public void FixedUpdate()
    {
      if ( Native == null )
        return;

      UpdateLidars();
      UpdateShapeInstances();
      UpdateAGXComponents();
    }

    private void UpdateLidars()
    {
      for ( int i = m_lidars.Count - 1; i >= 0; i-- ) {
        LidarSensor lidar = m_lidars[i];
        if ( lidar == null ) {
          m_lidars.RemoveAt( i );
          continue;
        }
      }
    }

    private void UpdateAGXComponents()
    {
      for ( int i = m_agxComponents.Count - 1; i >= 0; i-- ) {
        // Deleted objects
        var entry = m_agxComponents.ElementAt(i);
        var component = entry.Key;
        if ( component == null ) {
          RemoveAGXModel( component );
          m_agxComponents.Remove( component );
          continue;
        }

        // Update object visibility
        bool currentlyVisible = component.gameObject.activeInHierarchy || DisabledObjectsVisibleToSensors;
        bool previouslyVisible = entry.Value;
        if ( currentlyVisible != previouslyVisible ) {
          if ( currentlyVisible )
            AddAGXModel( component );
          else
            RemoveAGXModel( component );
          m_agxComponents[ component ] = currentlyVisible;
        }

      }
    }

    private void RemoveInstance( MeshFilter meshFilter )
    {
      var instance = m_rtShapeInstances[meshFilter];
      instance.Dispose();
      m_rtShapeInstances.Remove( meshFilter );
    }

    private void UpdateShapeInstances()
    {
      // Walk through registered meshes and remove deleted from list plus optionally handle disabled meshes
      for ( int i = m_meshFilters.Count - 1; i >= 0; i-- ) {
        var meshFilter = m_meshFilters[i];

        if ( meshFilter == null ) {
          m_meshFilters.RemoveAt( i );
          continue;
        }

        // Handle invisible objects
        if ( !DisabledObjectsVisibleToSensors ) {
          bool visible = meshFilter.gameObject.activeInHierarchy;
          bool containsKey = m_rtShapeInstances.ContainsKey(meshFilter);

          if ( visible && !containsKey )
            RegisterMeshfilter( meshFilter );
          else if ( !visible && containsKey )
            RemoveInstance( meshFilter );
        }
      }

      foreach ( var shapeInstance in m_rtShapeInstances ) {
        var meshFilter = shapeInstance.Key;

        shapeInstance.Value.setTransform(
            new AffineMatrix4x4(
                meshFilter.transform.rotation.ToHandedQuat(),
                meshFilter.transform.position.ToHandedVec3() ),
                meshFilter.transform.lossyScale.ToHandedVec3() );
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
      foreach ( var lidar in m_lidars ) {
        if ( lidar.Native != null )
          Native.remove( lidar.Native );
      }

      foreach ( var dt in m_deformableTerrains )
        Native.remove( dt );
      m_deformableTerrains.Clear();

      foreach ( var dtp in m_deformableTerrainPagers )
        Native.remove( dtp );
      m_deformableTerrainPagers.Clear();

      foreach ( var w in m_wires )
        Native.remove( w );
      m_wires.Clear();

      foreach ( var c in m_cables )
        Native.remove( c );
      m_cables.Clear();

      foreach ( var rtShapeInstance in m_rtShapeInstances ) {
        rtShapeInstance.Value.Dispose();
      }
      m_rtShapeInstances.Clear();
      foreach ( var rtShape in m_rtShapes ) {
        rtShape.Value.Dispose();
      }
      m_rtShapes.Clear();
      m_meshFilters.Clear();

      m_rtDefaultSurfaceMaterial.Dispose();
      m_rtDefaultSurfaceMaterial = null;

      Native = null;
    }

    protected override void OnDestroy()
    {
      if ( Simulation.HasInstance )
        Simulation.Instance.UnRegisterDisposeCallback( DisposeRT );

      base.OnDestroy();
    }
  }
}
