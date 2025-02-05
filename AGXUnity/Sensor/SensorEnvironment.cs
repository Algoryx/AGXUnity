using agx;
using agxSensor;
using AGXUnity.Collide;
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

    private readonly Dictionary<DeformableTerrain,agxTerrain.Terrain> m_deformableTerrains = new();
    private readonly Dictionary<DeformableTerrainPager,agxTerrain.TerrainPager> m_deformableTerrainPagers = new();
    private readonly Dictionary<HeightField,agxCollide.HeightField> m_heightfields = new();
    private readonly Dictionary<Wire,agxWire.Wire> m_wires = new();
    private readonly Dictionary<Cable,agxCable.Cable> m_cables = new();
    private readonly Dictionary<Track,agxVehicle.Track> m_tracks = new();

    private readonly Dictionary<ScriptComponent, bool> m_agxComponents = new();
    private readonly List<GameObject> m_newlyAdded = new();

    private static readonly System.Type[] s_supportedComponents = new[]
    {
      typeof(DeformableTerrain),
      typeof(DeformableTerrainPager),
      typeof(HeightField),
      typeof(Cable),
      typeof(Wire),
      typeof(Track)
    };


    /**
     * The Ambient material used by the Sensor Environment.
     * This is used to simulate atmospheric effects on the Lidar laser rays, such as rain or fog.
     */
    [SerializeField]
    private AmbientMaterial m_ambientMaterial = null;

    public AmbientMaterial AmbientMaterial
    {
      get => m_ambientMaterial;
      set
      {
        m_ambientMaterial = value;
        if ( Native != null ) {
          RtMaterialInstance nativeMat = m_ambientMaterial?.GetInitialized<AmbientMaterial>()?.Native;
          if ( nativeMat == null )
            nativeMat = new RtMaterialInstance(); // Create a null instance to set unset the ambient mat
          Native.getScene().setMaterial( nativeMat );
        }
      }
    }

    //TODO temporary solution, fix materials
    [SerializeField]
    private LidarSurfaceMaterialDefinition m_defaultSurfaceMaterial;

    [DisableInRuntimeInspector]
    public LidarSurfaceMaterialDefinition DefaultSurfaceMaterial
    {
      get => m_defaultSurfaceMaterial;
      set
      {
        if ( Native == null )
          m_defaultSurfaceMaterial = value;
        else
          Debug.LogWarning( "Changing default surface material during runtime is not supported!" );
      }
    }

    private RtSurfaceMaterial InternalDefaultMaterial = null;

    private uint m_currentOutputID = 1;
    private int m_currentEntityId = 0;

    // Always use this method in order to have each lidar use a unique output id
    public uint GenerateOutputID()
    {
      return m_currentOutputID++;
    }

    /// <summary>
    /// Registers a gameobject in the sensor environment to queue adding it and child objects to the environment. 
    /// If this is not called for non-AGXUnity objects, they will be invisible to sensors in the scene.
    /// </summary>
    /// <param name="newlyCreated">The newwly created object to be registered</param>
    public void RegisterCreatedObject( GameObject newlyCreated ) => m_newlyAdded.Add( newlyCreated );

    // Call this when adding MeshFilters during runtime from custom script
    private void RegisterMeshfilter( MeshFilter meshFilter )
    {
      if ( !m_meshFilters.Contains( meshFilter ) )
        m_meshFilters.Add( meshFilter );

      if ( m_rtShapeInstances.ContainsKey( meshFilter ) )
        return;

      m_newlyAdded.Remove( meshFilter.gameObject );

      UnityEngine.Mesh mesh = meshFilter.sharedMesh;
      bool newMesh = false;

      if ( mesh == null )
        return;

      if ( !m_rtShapes.TryGetValue( mesh, out RtShape rtShape ) ) {
        rtShape = CreateShape( mesh );
        m_rtShapes[ mesh ] = rtShape;
        newMesh = true;
        if ( rtShape == null )
          Debug.LogWarning( $"Failed to create RtShape for mesh '{mesh.name}'" );
      }
      if ( rtShape == null )
        return;

      var material = LidarSurfaceMaterial.FindClosestMaterial(meshFilter.gameObject);

      m_rtShapeInstances[ meshFilter ] = CreateShapeInstance(
        rtShape,
        meshFilter.transform.rotation,
        meshFilter.transform.position,
        meshFilter.transform.lossyScale,
        material );

      if ( DebugLogOnAdd )
        Debug.Log( $"SensorEnvironment '{name}' added shapeInstance for mesh on '{meshFilter.gameObject.name}', added shape: {newMesh}" );
    }

    private RtShape CreateShape( UnityEngine.Mesh mesh )
    {
      Profiler.BeginSample( "CreateShape" );

      var meshTriangles = mesh.triangles;
      var meshVertices = mesh.vertices;
      var meshNormals = mesh.normals;

      int tris = meshTriangles.Length;
      int verts = meshVertices.Length;
      int norms = meshNormals.Length;

      UInt32Vector indices = new UInt32Vector(tris);
      Vec3Vector vertices = new Vec3Vector(verts);
      Vec3Vector normals = new Vec3Vector(norms);

      for ( int i = 0; i < tris; i++ )
        indices.Add( (uint)meshTriangles[ i ] );
      for ( int i = 0; i < verts; i++ )
        vertices.Add( meshVertices[ i ].ToVec3() );
      for ( int i = 0; i < norms; i++ )
        normals.Add( meshNormals[ i ].ToVec3() );

      var rtShape = RtShape.create(vertices, indices, normals);

      Profiler.EndSample();

      return rtShape;
    }

    private RtShapeInstance CreateShapeInstance( RtShape rtShape, Quaternion rotation, Vector3 position, Vector3 scale, LidarSurfaceMaterialDefinition material )
    {
      RtMaterialInstance rtMaterialInstance = null;
      if ( material != null )
        rtMaterialInstance = material.GetRtMaterial();

      Profiler.BeginSample( "CreateShapeInstance" );
      RtInstanceData data = new RtInstanceData(rtMaterialInstance ?? InternalDefaultMaterial, (RtEntityId)(++m_currentEntityId));
      RtShapeInstance shapeInstance = RtShapeInstance.create(Native.getScene(), rtShape, data);

      shapeInstance.setTransform(
        new AffineMatrix4x4(
          Extensions.ToHandedQuat( rotation ),
          Extensions.ToHandedVec3( position ) ),
        scale.ToHandedVec3() );

      Profiler.EndSample();
      return shapeInstance;
    }

    private bool AddAGXModel( ScriptComponent scriptComponent )
    {
      if ( scriptComponent == null )
        return false;

      m_newlyAdded.Remove( scriptComponent.gameObject );

      m_agxComponents.TryAdd( scriptComponent, false );

      RtSurfaceMaterial rtMaterial = LidarSurfaceMaterial.FindClosestMaterial(scriptComponent.gameObject)?.GetRtMaterial() ?? InternalDefaultMaterial;

      bool added = false;
      if ( scriptComponent is DeformableTerrain dt ) {
        var c = dt.Native;
        RtSurfaceMaterial.set( c, rtMaterial );
        added = Native.add( c );
        m_deformableTerrains.Add( dt, c );
      }
      else if ( scriptComponent is DeformableTerrainPager dtp ) {
        var c = dtp.Native;
        RtSurfaceMaterial.set( c, rtMaterial );
        added = Native.add( c );
        m_deformableTerrainPagers.Add( dtp, c );
      }
      else if ( scriptComponent is Wire w ) {
        var c = w.Native;
        RtSurfaceMaterial.set( c, rtMaterial );
        added = Native.add( c );
        m_wires.Add( w, c );
      }
      else if ( scriptComponent is Cable ca ) {
        var c = ca.Native;
        RtSurfaceMaterial.set( c, rtMaterial );
        added = Native.add( c );
        m_cables.Add( ca, c );
      }
      else if ( scriptComponent is Track track ) {
        var t = track.Native;
        RtSurfaceMaterial.set( t, rtMaterial );
        added = Native.add( t );
        m_tracks.Add( track, t );
      }
      else if ( scriptComponent is HeightField hf ) {
        var h = hf.Native;
        RtSurfaceMaterial.set( h, rtMaterial );
        added = Native.add( h );
        m_heightfields.Add( hf, h );
      }

      if ( DebugLogOnAdd && added )
        Debug.Log( $"Sensor Environment '{name}' added {scriptComponent.GetType()} in object '{scriptComponent.GetType().GetProperty( "name" )?.GetValue( scriptComponent, null )}'." );

      return true;
    }

    private bool RemoveAGXModel( ScriptComponent scriptComponent )
    {
      if ( scriptComponent is DeformableTerrain dt ) {
        var c = dt.Native ?? m_deformableTerrains.GetValueOrDefault(dt);
        Native.remove( c );
        m_deformableTerrains.Remove( dt );
      }
      else if ( scriptComponent is DeformableTerrainPager dtp ) {
        var c = dtp.Native ?? m_deformableTerrainPagers.GetValueOrDefault( dtp );
        Native.remove( c );
        m_deformableTerrainPagers.Remove( dtp );
      }
      else if ( scriptComponent is Wire w ) {
        var c = w.Native ?? m_wires.GetValueOrDefault( w );
        Native.remove( c );
        m_wires.Remove( w );
      }
      else if ( scriptComponent is Cable ca ) {
        var c = ca.Native ?? m_cables.GetValueOrDefault( ca );
        Native.remove( c );
        m_cables.Remove( ca );
      }
      else if ( scriptComponent is Track track ) {
        var c = track.Native ?? m_tracks.GetValueOrDefault( track );
        Native.remove( c );
        m_tracks.Remove( track );
      }
      else if ( scriptComponent is HeightField hf ) {
        var c = hf.Native ?? m_heightfields.GetValueOrDefault( hf );
        Native.remove( c );
        m_heightfields.Remove( hf );
      }
      else {
        Debug.LogWarning( "AGX type not handled by this method" );
      }
      return true;
    }

    private List<T> FindValidComponents<T>( bool includeInactive = false ) where T : UnityEngine.Component
    {
#if UNITY_2022_2_OR_NEWER
      return FindObjectsByType<T>( FindObjectsSortMode.None )
#else
      return FindObjectsOfType<T>( includeInactive )
#endif
          .Where( component =>
              component.gameObject.scene.IsValid() &&
              component.gameObject.transform.root.gameObject.scene == component.gameObject.scene )
          .ToList();
    }

    protected override bool Initialize()
    {
      if ( !LicenseManager.LicenseInfo.HasModuleLogError( LicenseInfo.Module.AGXSensor, this ) )
        return false;

      var simulation = Simulation.Instance.GetInitialized().Native;
      simulation.setPreIntegratePositions( true ); // From Python, check if this is needed

      // Default material
      if ( DefaultSurfaceMaterial != null )
        InternalDefaultMaterial = DefaultSurfaceMaterial.GetInitialized<LidarSurfaceMaterialDefinition>().GetRtMaterial();
      else
        InternalDefaultMaterial = RtLambertianOpaqueMaterial.create();

      Native = agxSensor.Environment.getOrCreate( simulation );

      if ( AmbientMaterial != null ) {
        var ambMat = AmbientMaterial.GetInitialized<AmbientMaterial>().Native;

        Native.getScene().setMaterial( ambMat );
      }

      FindValidComponents<MeshFilter>( true ).ForEach( RegisterMeshfilter );

      FindValidComponents<ScriptComponent>( true ).ForEach( c => TrackIfSupported( c ) );

      UpdateEnvironment();

      Simulation.Instance.StepCallbacks.PreSynchronizeTransforms += UpdateEnvironment;
      ScriptComponent.OnInitialized += LateInitializeScriptComponent;

      return true;
    }

    private void TrackIfSupported( ScriptComponent sc )
    {
      if ( s_supportedComponents.Contains( sc.GetType() ) )
        m_agxComponents.Add( sc, false );
      m_newlyAdded.Remove( sc.gameObject );
    }

    private void UpdateEnvironment()
    {
      if ( Native == null )
        return;
      Profiler.BeginSample( "SensorEnvironment.UpdateEnvironment" );

      Profiler.BeginSample( "SensorEnvironment.AddNewComponents" );
      while ( m_newlyAdded.Count != 0 ) {
        var c = m_newlyAdded.Last();
        foreach ( var subcomp in c.GetComponentsInChildren<ScriptComponent>() )
          TrackIfSupported( subcomp );
        foreach ( var mesh in c.GetComponentsInChildren<MeshFilter>() )
          RegisterMeshfilter( mesh );
        m_newlyAdded.Remove( c );
      }
      Profiler.EndSample();

      UpdateShapeInstances();
      UpdateAGXComponents();

      Profiler.EndSample();
    }

    private void LateInitializeScriptComponent( ScriptComponent c ) => m_newlyAdded.Add( c.gameObject );

    private void UpdateAGXComponents()
    {
      Profiler.BeginSample( "SensorEnvironment.UpdateAGXComponents" );
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
      Profiler.EndSample();
    }

    private void RemoveInstance( MeshFilter meshFilter )
    {
      if ( !m_rtShapeInstances.ContainsKey( meshFilter ) )
        return;
      var instance = m_rtShapeInstances[meshFilter];
      instance.Dispose();
      m_rtShapeInstances.Remove( meshFilter );
    }

    private void UpdateShapeInstances()
    {
      Profiler.BeginSample( "SensorEnvironment.UpdateShapeInstances" );
      // Walk through registered meshes and remove deleted from list plus optionally handle disabled meshes
      for ( int i = m_meshFilters.Count - 1; i >= 0; i-- ) {
        var meshFilter = m_meshFilters[i];

        if ( meshFilter == null ) {
          m_meshFilters.RemoveAt( i );
          RemoveInstance( meshFilter );
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
      Profiler.EndSample();
    }

    protected override void OnEnable()
    {
    }

    // Note that disabling this component does not stop the sensor simulation
    protected override void OnDisable()
    {
    }

    internal void DisposeRT()
    {
      foreach ( var (_, dt) in m_deformableTerrains )
        Native.remove( dt );
      m_deformableTerrains.Clear();

      foreach ( var (_, dtp) in m_deformableTerrainPagers )
        Native.remove( dtp );
      m_deformableTerrainPagers.Clear();

      foreach ( var (_, hf) in m_heightfields )
        Native.remove( hf );
      m_heightfields.Clear();

      foreach ( var (_, w) in m_wires )
        Native.remove( w );
      m_wires.Clear();

      foreach ( var (_, c) in m_cables )
        Native.remove( c );
      m_cables.Clear();

      foreach ( var (_, track) in m_tracks )
        Native.remove( track );
      m_tracks.Clear();

      foreach ( var rtShapeInstance in m_rtShapeInstances ) {
        rtShapeInstance.Value?.Dispose();
      }
      m_rtShapeInstances.Clear();
      foreach ( var rtShape in m_rtShapes ) {
        rtShape.Value?.Dispose();
      }
      m_rtShapes.Clear();
      m_meshFilters.Clear();

      Native = null;
    }

    protected override void OnDestroy()
    {
      if ( Simulation.HasInstance )
        Simulation.Instance.StepCallbacks.PreSynchronizeTransforms -= UpdateEnvironment;

      ScriptComponent.OnInitialized -= LateInitializeScriptComponent;

      base.OnDestroy();
    }
  }
}
