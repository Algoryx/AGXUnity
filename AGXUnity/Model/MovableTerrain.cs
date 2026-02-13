using AGXUnity.Collide;
using AGXUnity.Utils;
using System;
using UnityEngine;
using System.Collections.Generic;

using Mesh = UnityEngine.Mesh;
using UnityEngine.Serialization;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace AGXUnity.Model
{
  public abstract class MovableAdapter : DeformableTerrainBase
  {
    public sealed override float ElementSize { get => ElementSizeGetter; }
    protected abstract float ElementSizeGetter { get; }
  }

  [AddComponentMenu( "AGXUnity/Model/Movable Terrain" )]
  [RequireComponent( typeof( MeshFilter ) )]
  [RequireComponent( typeof( MeshRenderer ) )]
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#movable-terrain" )]
  [DisallowMultipleComponent]
  public class MovableTerrain : MovableAdapter
  {
    // Constructor is used here to override the default value defined in the base class
    MovableTerrain()
    {
      m_maximumDepth = 0.5f;
    }

    /// <summary>
    /// Native deformable terrain instance - accessible after this
    /// component has been initialized and is valid.
    /// </summary>
    public agxTerrain.Terrain Native { get; private set; } = null;

    /// <summary>
    /// Unity Mesh component.
    /// </summary>
    public MeshFilter TerrainMesh
    {
      get
      {
        return m_terrainMesh == null ?
                 m_terrainMesh = GetComponent<MeshFilter>() :
                 m_terrainMesh;
      }
    }

    /// <summary>
    /// Unity Renderer component.
    /// </summary>
    public MeshRenderer TerrainRenderer
    {
      get
      {
        return m_terrainRenderer == null ?
                 m_terrainRenderer = GetComponent<MeshRenderer>() :
                 m_terrainRenderer;
      }
    }

    [HideInInspector]
    public RigidBody RigidBody
    {
      get
      {
        Component obj = this;
        while ( obj != null ) {
          var rb = obj.GetComponent<RigidBody>();
          if ( rb != null ) return rb;
          obj = obj.transform.parent;
        }
        return null;
      }
    }

    public enum Placement
    {
      Automatic,
      Manual
    };

    /// <summary>
    /// Specifies how to position and initialize the terrain:
    /// * <b>Manual</b> - Use the terrain sizes and GameObject position/rotation directly to create the terrain.
    /// * <b>Automatic</b> - Create the terrain based on a list of Bed Geometries. This mode will calculate the
    /// bounds of the bed geometries to set the position and size of the terrain after which it will raycast against
    /// the geometry list to find the initial and minimum heights of the terrain
    /// </summary>
    [field: SerializeField]
    [HideInInspector]
    [Tooltip( "Specifies how to position and initialize the terrain:\n" +
              "* <b>Manual</b> - Use the terrain sizes and GameObject position/rotation directly to create the terrain.\n" +
              "* <b>Automatic</b> - Create the terrain based on a list of Bed Geometries. This mode will calculate the " +
              "bounds of the bed geometries to set the position and size of the terrain after which it will raycast against " +
              "the geometry list to find the initial and minimum heights of the terrain" )]
    public Placement PlacementMode { get; set; } = Placement.Automatic;

    [SerializeField]
    private Vector2 m_sizeMeters = new Vector2(2, 2);

    /// <summary>
    /// Specifies the size of the terrain in Meters
    /// </summary>
    [ClampAboveZeroInInspector]
    [HideInInspector]
    [IgnoreSynchronization]
    [Tooltip( "Specifies the size of the terrain in Meters" )]
    public Vector2 SizeMeters
    {
      get => m_sizeMeters;
      set => SetTerrainSizeResolution( value, Resolution );
    }

    [SerializeField]
    private Vector2Int m_sizeCells = new Vector2Int(20, 20);

    /// <summary>
    /// Specifies the size of the terrain in Cell count
    /// </summary>
    [ClampAboveZeroInInspector]
    [HideInInspector]
    [IgnoreSynchronization]
    [Tooltip( "Specifies the size of the terrain in Cell count" )]
    public Vector2Int SizeCells
    {
      get => m_sizeCells;
      set => SetCellCountElementSize( value, ElementSize );
    }

    /// <summary>
    /// Specifies the amount of cells in the local X-axis
    /// </summary>
    [HideInInspector]
    [IgnoreSynchronization]
    [Tooltip( "Specifies the amount of cells in the local X-axis" )]
    [DelayedInspector]
    public int Resolution
    {
      get => m_sizeCells.x;
      set => SetTerrainSizeResolution( SizeMeters, value );
    }

    [SerializeField]
    private float m_elementSize = 0.1f;

    /// <summary>
    ///  The size of each underlying tile in the terrain, in meters.
    /// </summary>
    [HideInInspector]
    [ClampAboveZeroInInspector]
    [IgnoreSynchronization]
    [Tooltip( "The size of each underlying tile in the terrain, in meters." )]
    public new float ElementSize
    {
      get => m_elementSize;
      set => SetCellCountElementSize( SizeCells, value );
    }

    /// <summary>
    /// When enabled, the maximum depth will be added as height during initialization of the terrain
    /// </summary>
    [field: SerializeField]
    [field: FormerlySerializedAs( "<InvertDepthDirection>k__BackingField" )]
    [InspectorPriority( -1 )]
    [Tooltip( "When enabled, the maximum depth will be added as height during initialization of the terrain." )]
    public bool MaxDepthAsInitialHeight { get; set; } = true;

    /// <summary>
    /// The compaction that all terrain cells are initialized to.
    /// </summary>
    [DisableInRuntimeInspector]
    [ClampAboveZeroInInspector( true )]
    [InspectorPriority( -1 )]
    [field: SerializeField]
    [Tooltip( "The compaction that all terrain cells are initialized to." )]
    public float InitialCompaction { get; set; } = 1.0f;

    [field: SerializeField]
    private float m_terrainBedMargin = 0.01f;

    /// <summary>
    /// Adds a margin by which to shrink the terrain relative to the bed geometries' bounds. 
    /// Can be negative, in which case the resulting terrain will be expanded.
    /// </summary>
    [HideInInspector]
    [IgnoreSynchronization]
    [DelayedInspector]
    [Tooltip( "Adds a margin by which to shrink the terrain relative to the bed geometries' bounds. " +
              "Can be negative, in which case the resulting terrain will be expanded." )]
    public float TerrainBedMargin
    {
      get => m_terrainBedMargin;
      set
      {
        if ( m_terrainBedMargin == value )
          return;
        if ( Native != null ) {
          Debug.LogError( "Cannot change Terrain bed margin after it's been initialized" );
          return;
        }
        m_terrainBedMargin = value;
        RecalculateAutomaticBed();
      }
    }

    [field: SerializeField]
    private float m_terrainBedHeightOffset = 0;

    /// <summary>
    /// Adds an offset to the terrain to allow the bottom of the bed to be above/below the provided bed geometries.
    /// </summary>
    [HideInInspector]
    [IgnoreSynchronization]
    [DelayedInspector]
    [Tooltip( "Adds an offset to the terrain to allow the bottom of the bed to be above/below the provided bed geometries." )]
    public float TerrainBedHeightOffset
    {
      get => m_terrainBedHeightOffset;
      set
      {
        {
          if ( m_terrainBedHeightOffset == value )
            return;
          if ( Native != null ) {
            Debug.LogError( "Cannot change Terrain bed margin after it's been initialized" );
            return;
          }
          m_terrainBedHeightOffset = value;
          RecalculateAutomaticBed();
        }
      }
    }

    [SerializeField]
    private bool m_renderSides = true;

    /// <summary>
    /// When enabled, the sides of the terrain will be rendered down to the minimum height of the terrain.
    /// This can cause some visual inconsistencies at the edges for lower resolutions and requires some extra processing during update.
    /// </summary>
    [InspectorPriority( -1 )]
    [InspectorGroupBegin( Name = "Rendering Options" )]
    [Tooltip( "When enabled, the sides of the terrain will be rendered down to the minimum height of the terrain." +
              "This can cause some visual inconsistencies at the edges for lower resolutions and requires some extra processing during update." )]
    public bool RenderSides
    {
      get => m_renderSides;
      set
      {
        if ( m_renderSides == value )
          return;
        m_renderSides = value;
        RecreateMesh();
      }
    }

    [SerializeField]
    private bool m_heightsAsVertexColors = false;

    /// <summary>
    /// When enabled, the generate mesh will contain non-white vertex colors with the following enclosed values:
    /// <b>R</b> - The current height.
    /// <b>G</b> - The minimum height.
    /// <b>B</b> - The current height above the minimum height.
    /// All of these are in meters and use the corresponding cell index to fetch values.
    /// </summary>
    [Tooltip( "When enabled, the generate mesh will contain non-white vertex colors with the following enclosed values:\n" +
              "<b>R</b> - The current height.\n" +
              "<b>G</b> - The minimum height.\n" +
              "<b>B</b> - The current height above the minimum height.\n" +
              "All of these are in meters and use the corresponding cell index to fetch values." )]
    [InspectorPriority( -1 )]
    public bool HeightsAsVertexColors
    {
      get => m_heightsAsVertexColors;
      set
      {
        if ( m_heightsAsVertexColors == value )
          return;
        m_heightsAsVertexColors = value;
        RecreateMesh();
      }
    }


    [SerializeField]
    private List<Shape> m_bedShapes = new List<Shape>();

    /// <summary>
    /// The geometries to use when Placement mode is set to Automatic to compute terrain placement and heights.
    /// </summary>
    public Shape[] BedGeometries => m_bedShapes.ToArray();

    /// <summary>
    /// Adds a terrain bed geometry to be used when Automatic placement is used. 
    /// The geometries added this way are ignored if Manual placement is used instead.
    /// </summary>
    /// <param name="bedGeom">A shape that is a part of the terrain bed that the terrain represents</param>
    /// <returns>True if the bed geometry was successfully added, false otherwise</returns>
    public bool AddBedGeometry( Shape bedGeom )
    {
      if ( Native != null ) {
        Debug.LogWarning( $"Failed to add bed geometry: Cannot add bed geometry to terrain '{name}' after it's been initialized" );
        return false;
      }
      if ( m_bedShapes.Contains( bedGeom ) ) {
        Debug.LogWarning( $"Failed to add bed geometry: Terrain '{name}' already contains bed geometry '{bedGeom.name}'" );
        return false;
      }
      m_bedShapes.Add( bedGeom );

      RecalculateAutomaticBed();

      return true;
    }

    /// <summary>
    /// Removes a terrain bed geometry from the geometries used by the Automatic placement. 
    /// </summary>
    /// <param name="bedGeom">The geometry to remove from the bed geometries</param>
    /// <returns>True if the bed geometry was successfully removed, false otherwise</returns>
    public bool RemoveBedGeometry( Shape bedGeom )
    {
      if ( Native != null ) {
        Debug.LogWarning( $"Failed to remove bed geometry: Cannot remove bed geometry from terrain '{name}' after it's been initialized" );
        return false;
      }
      if ( !m_bedShapes.Contains( bedGeom ) ) {
        Debug.LogWarning( $"Failed to remove bed geometry: Terrain '{name}' does not contain bed geometry '{bedGeom.name}'" );
        return false;
      }
      m_bedShapes.Remove( bedGeom );

      RecalculateAutomaticBed();

      return true;
    }

    private void SetCellCountElementSize( Vector2Int cellCount, float elementSize )
    {
      if ( SizeCells == cellCount && ElementSize == elementSize )
        return;

      if ( Native != null ) {
        Debug.LogError( "Cannot change terrain placement parameters after the terrain has been initialized" );
        return;
      }

      if ( PlacementMode == Placement.Automatic ) {
        Debug.LogWarning( ( cellCount != SizeCells ? "Terrain size" : "Element size" ) + " cannot be set explicitly when Placement Mode is set to Automatic. Ignoring change..." );
        return;
      }

      m_sizeCells = cellCount;
      m_elementSize = elementSize;

      m_sizeMeters.x = m_sizeCells.x * m_elementSize;
      m_sizeMeters.y = m_sizeCells.y * m_elementSize;

      SetupMesh();
    }

    private void SetTerrainSizeResolution( Vector2 terrainSize, int resolution )
    {
      if ( SizeMeters == terrainSize && Resolution == resolution )
        return;

      if ( Native != null ) {
        Debug.LogError( "Cannot change terrain placement parameters after the terrain has been initialized" );
        return;
      }

      if ( PlacementMode == Placement.Automatic && SizeMeters != terrainSize ) {
        Debug.LogWarning( "Terrain size cannot be set explicitly when Placement Mode is set to Automatic. Ignoring change..." );
        return;
      }

      m_sizeCells.x = Mathf.Max( resolution, 2 );
      if ( PlacementMode == Placement.Automatic )
        RecalculateAutomaticBed();
      else {
        m_sizeMeters = terrainSize;
        m_elementSize = m_sizeMeters.x / m_sizeCells.x;
        m_sizeCells.y = Mathf.CeilToInt( Resolution * m_sizeMeters.y / m_sizeMeters.x );

        SetupMesh();
      }
    }

    public override void EditorUpdate()
    {
      if ( TerrainMesh.sharedMesh == null )
        RecreateMesh();

#if UNITY_EDITOR
      // If the current material is the default (not an asset) and does not support the current rendering pipeline, replace it with new default.
      var mat = TerrainRenderer.sharedMaterial;
      if ( mat == null || ( !AssetDatabase.Contains( mat ) && !mat.SupportsPipeline( RenderingUtils.DetectPipeline() ) ) ) {
        TerrainRenderer.sharedMaterial = RenderingUtils.CreateDefaultMaterial();
        RenderingUtils.SetMainTexture( TerrainRenderer.sharedMaterial, AssetDatabase.GetBuiltinExtraResource<Texture2D>( "Default-Checker-Gray.png" ) );
      }
#endif
    }

    protected override bool Initialize()
    {
      // Only printing the errors if something is wrong.
      LicenseManager.LicenseInfo.HasModuleLogError( LicenseInfo.Module.AGXTerrain | LicenseInfo.Module.AGXGranular, this );

      InitializeNative();
      if ( Native == null )
        return false;

      if ( TerrainRenderer.sharedMaterial == null ) {
        TerrainRenderer.sharedMaterial = RenderingUtils.CreateDefaultMaterial();
        RenderingUtils.SetMainTexture( TerrainRenderer.sharedMaterial, Resources.Load<Texture2D>( "Default-Checker-Gray.png" ) );
      }

      Simulation.Instance.StepCallbacks.PostStepForward += OnPostStepForward;

      // Native terrain may change the number of PPGS iterations to default (25).
      // Override if we have solver settings set to the simulation.
      if ( Simulation.Instance.SolverSettings != null )
        GetSimulation().getSolver().setNumPPGSRestingIterations( (ulong)Simulation.Instance.SolverSettings.PpgsRestingIterations );

      SetEnable( isActiveAndEnabled );

      return true;
    }

    protected override void OnDestroy()
    {
      if ( TerrainProperties != null )
        TerrainProperties.Unregister( this );

      if ( Simulation.HasInstance ) {
        GetSimulation().remove( Native );
        Simulation.Instance.StepCallbacks.PostStepForward -= OnPostStepForward;
      }
      Native = null;

      base.OnDestroy();
    }

    private agx.AffineMatrix4x4 TerrainToWorldMatrix => agx.AffineMatrix4x4.rotate( agx.Vec3.Z_AXIS(), agx.Vec3.Y_AXIS() ) * transform.localToWorldMatrix.ToAffine4x4();

    private agxTerrain.Terrain CreateBed()
    {
      List<agxCollide.Geometry> temps = new List<agxCollide.Geometry>();
      var geoms = new agxCollide.GeometryPtrVector();

      var worldToTerrain = TerrainToWorldMatrix.inverse();

      foreach ( var geom in m_bedShapes ) {
        var temp = geom.CreateTemporaryNative();
        temp.setTransform( geom.transform.localToWorldMatrix.ToAffine4x4() * worldToTerrain );

        geoms.Add( temp );
        temps.Add( temp );
      }
      var terrain = agxTerrain.Terrain.createTerrainBedFromGeometries( (uint)Resolution, geoms, TerrainBedMargin, TerrainBedHeightOffset, true );

      if ( MaxDepthAsInitialHeight ) {
        int numCells = (int)(terrain.getResolutionX() * terrain.getResolutionY());
        var heights = new agx.RealVector(numCells);
        var heightArr = new double[ numCells ];
        Array.Fill( heightArr, MaximumDepth );
        heights.Set( heightArr );
        terrain.setHeights( heights );
      }
      return terrain;
    }

    /// <summary>
    /// Force a recalculation of the terrain properties given the terrain bed geometries
    /// </summary>
    public void RecalculateAutomaticBed()
    {
      if ( PlacementMode != Placement.Automatic ) {
        Debug.LogWarning( "Cannot recalculate automatic terrain bed unless Automatic Placement Mode is selected" );
        return;
      }

      if ( Native != null ) {
        Debug.LogWarning( "Cannot recalculate automatic terrain bed for an initialized terrain" );
        return;
      }

      if ( m_bedShapes.Count > 0 ) {
        // Create temporary bed
        Native = CreateBed();

        // Set derived sizes without triggering implicit recalculation
        this.m_elementSize = (float)Native.getElementSize();
        this.m_sizeCells = new Vector2Int( (int)Native.getHeightField().getResolutionX(), (int)Native.getHeightField().getResolutionY() );
        this.m_sizeMeters = new Vector2( (float)Native.getSize().x, (float)Native.getSize().y );

        // Set position
        this.transform.position = ( TerrainToWorldMatrix.transformPoint( Native.getPosition() ) ).ToHandedVector3();
      }

      // Create render mesh
      SetupMesh();

      Native = null;
    }

    private void InitializeNative()
    {
      if ( PlacementMode == Placement.Automatic ) {
        if ( m_bedShapes.Count == 0 ) {
          Debug.LogError( $"Failed to initialize terrain '{name}', Placement mode is set to Automatic but no bed geometries were provided." );
          return;
        }

        Native = CreateBed();
        transform.position = TerrainToWorldMatrix.transformPoint( Native.getPosition() ).ToHandedVector3();
      }
      else {
        var heights = new agx.RealVector((int)(SizeCells.x * SizeCells.y));
        var heightArr = new double[ SizeCells.x * SizeCells.y ];
        var depth = MaximumDepth;
        if ( MaxDepthAsInitialHeight ) {
          depth = 0;
          Array.Fill( heightArr, MaximumDepth );
        }
        heights.Set( heightArr );

        Native = new agxTerrain.Terrain( (uint)SizeCells.x,
                                         (uint)SizeCells.y,
                                         ElementSize,
                                         heights,
                                         false,
                                         depth );
      }

      if ( InitialCompaction != 1.0f )
        Native.setCompaction( InitialCompaction );

      Simulation.Instance.Native.add( Native );

      var rb = RigidBody;
      if ( rb != null )
        RigidBody.GetInitialized<RigidBody>().Native.add( Native.getGeometry(), GetTerrainOffset() );
      else
        Native.setTransform( GetTerrainOffset() * new agx.AffineMatrix4x4( transform.rotation.ToHandedQuat(),
                                                                           transform.position.ToHandedVec3() ) );
      SetupMesh();
    }

    private void OnPostStepForward()
    {
      if ( Native == null )
        return;

      UpdateHeights( Native.getModifiedVertices() );
    }

    /// <summary>
    /// Generator method which generates a set of indices on the border of the terrain.
    /// Corners are generated twice, once for each side they connect.
    /// Side index corresponds to: 
    ///   (0) y = 0, x = 0 -> width-1
    ///   (1) y = 0 -> height-1, x = width-1
    ///   (2) y = height - 1, x = width-1 -> 0
    ///   (3) y = height - 1 -> 0, x = 0
    /// </summary>
    /// <param name="width">The width of the box to generate side indices for</param>
    /// <param name="height">The height of the box to generate side indices for</param>
    /// <returns>A tuple containing indices (x, y, sideIdx)</returns>
    private IEnumerable<(int, int, int)> SideIndexGenerator( int width, int height )
    {
      for ( int x = 0; x < width; x++ )
        yield return (x, 0, 0);
      for ( int y = 0; y < height; y++ )
        yield return (width-1, y, 1);
      for ( int x = width - 1; x >= 0; x-- )
        yield return (x, height - 1, 2);
      for ( int y = height - 1; y >= 0; y-- )
        yield return (0, y, 3);
    }

    private void RecreateMesh()
    {
      if ( Native != null || PlacementMode == Placement.Manual )
        SetupMesh();
      else
        RecalculateAutomaticBed();
    }

    private void SetupMesh()
    {
      // Reuse or create new mesh
      if ( TerrainMesh.sharedMesh == null ) {
        TerrainMesh.sharedMesh = new Mesh();
        TerrainMesh.sharedMesh.name = "Terrain mesh";
        TerrainMesh.sharedMesh.MarkDynamic();
      }

      int width;
      int height;
      float elemSize;
      System.Func<int, int, float> heightGetter;
      System.Func<int, int, float> minHeightGetter;

      // Setup getters and terrain info to avoid having to check Native == null in subsequent code
      if ( Native != null ) {
        width = (int)Native.getHeightField().getResolutionX();
        height = (int)Native.getHeightField().getResolutionY();
        elemSize = (float)Native.getElementSize();
        heightGetter = ( int x, int y ) => (float)Native.getHeight( new agx.Vec2i( width - x - 1, height - y - 1 ) );
        minHeightGetter = ( int x, int y ) => (float)Native.getMinimumHeight( new agx.Vec2i( width - x - 1, height - y - 1 ) );
      }
      else {
        width = SizeCells.x;
        height = SizeCells.y;
        elemSize = ElementSize;
        if ( MaxDepthAsInitialHeight ) {
          heightGetter = ( _, _ ) => MaximumDepth;
          minHeightGetter = ( _, _ ) => 0.0f;
        }
        else {
          heightGetter = ( _, _ ) => 0.0f;
          minHeightGetter = ( _, _ ) => -MaximumDepth;
        }
      }

      // Early out if size is 0
      if ( width * height == 0 )
        return;

      // Create a grid of vertices matching that of the undelying heightfield.
      int numVertices = width * height;
      int numIndices = (width - 1) * 6 * (height - 1);

      // When rendering sides we add additional vertices and indices
      if ( RenderSides ) {
        int skirtVertices = ( width + height ) * 2;
        numVertices += skirtVertices;
        numIndices += ( skirtVertices - 1 ) * 6;
      }

      var vertices = new Vector3[numVertices];
      var uvs = new Vector2[numVertices];
      var indices = new int[numIndices];
      // null colors = all white
      Color[] colors = null;
      if ( HeightsAsVertexColors )
        colors = new Color[ numVertices ];
      int i = 0;

      // Base local position to center mesh on terrain center
      float x0 = -width / 2.0f + 0.5f;
      float y0 = -height / 2.0f + 0.5f;

      // Iterate all voxel centers
      for ( var y = 0; y < height; y++ ) {
        float yPos = (y0 + y) * elemSize;
        for ( var x = 0; x < width; x++ ) {
          var terrHeight = heightGetter( x, y );
          float xPos = (x0 + x) * elemSize;
          vertices[ y * width + x ].x = xPos;
          vertices[ y * width + x ].z = yPos;
          vertices[ y * width + x ].y = terrHeight;

          uvs[ y * width + x ].x = xPos;
          uvs[ y * width + x ].y = yPos;

          if ( HeightsAsVertexColors ) {
            var minHeight = minHeightGetter( x, y );
            colors[ y * width + x ].r = terrHeight;
            colors[ y * width + x ].g = minHeight;
            colors[ y * width + x ].b = terrHeight - minHeight;
          }

          if ( x != width - 1 && y != height - 1 ) {
            indices[ i++ ] = y * width + x;
            indices[ i++ ] = ( y + 1 ) * width + x;
            indices[ i++ ] = ( y + 1 ) * width + ( x + 1 );

            indices[ i++ ] = y * width + x;
            indices[ i++ ] = ( y + 1 ) * width + ( x + 1 );
            indices[ i++ ] = y * width + ( x + 1 );
          }
        }
      }

      if ( RenderSides ) {
        int baseIndex = width * height;
        int skirtIndex = 0;
        (int, int) last = (0,0);
        foreach ( (int x, int y, int side) in SideIndexGenerator( width, height ) ) {
          var minHeight = minHeightGetter( x, y );
          vertices[ baseIndex + skirtIndex ].x = vertices[ y * width + x ].x;
          vertices[ baseIndex + skirtIndex ].z = vertices[ y * width + x ].z;
          vertices[ baseIndex + skirtIndex ].y = minHeight;

          var heightDiff = vertices[ y * width + x ].y - minHeight;

          // Extend UV along the axis perpendicular to the side, causes seams in corners
          // but cant really avoid that without stretching texture on the sides
          if ( side == 0 ) {
            uvs[ baseIndex + skirtIndex ].x = uvs[ y * width + x ].x;
            uvs[ baseIndex + skirtIndex ].y = uvs[ y * width + x ].y - heightDiff;
          }
          else if ( side == 1 ) {
            uvs[ baseIndex + skirtIndex ].x = uvs[ y * width + x ].x + heightDiff;
            uvs[ baseIndex + skirtIndex ].y = uvs[ y * width + x ].y;
          }
          else if ( side == 2 ) {
            uvs[ baseIndex + skirtIndex ].x = uvs[ y * width + x ].x;
            uvs[ baseIndex + skirtIndex ].y = uvs[ y * width + x ].y + heightDiff;
          }
          else {
            uvs[ baseIndex + skirtIndex ].x = uvs[ y * width + x ].x - heightDiff;
            uvs[ baseIndex + skirtIndex ].y = uvs[ y * width + x ].y;
          }

          // height == minHeight for edges
          if ( HeightsAsVertexColors ) {
            colors[ baseIndex + skirtIndex ].r = minHeight;
            colors[ baseIndex + skirtIndex ].g = minHeight;
            colors[ baseIndex + skirtIndex ].b = 0.0f;
          }

          // Skip first vertex but after that look back to build quad
          if ( side != 0 || x != 0 || y != 0 ) {
            indices[ i++ ] = y * width + x;
            indices[ i++ ] = baseIndex + skirtIndex - 1;
            indices[ i++ ] = last.Item2 * width + last.Item1;

            indices[ i++ ] = y * width + x;
            indices[ i++ ] = baseIndex + skirtIndex;
            indices[ i++ ] = baseIndex + skirtIndex - 1;
          }

          last = (x, y);
          skirtIndex++;
        }
      }

      TerrainMesh.sharedMesh.Clear();
      TerrainMesh.sharedMesh.vertices = vertices;
      // If HeightsAsVertexColors is disabled then setting colors to null uses the default white vertex colors
      TerrainMesh.sharedMesh.colors = colors;
      m_terrainVertices = vertices;
      m_terrainColors = colors;
      m_terrainUVs = uvs;
      TerrainMesh.sharedMesh.uv = uvs;
      TerrainMesh.sharedMesh.indexFormat = numIndices >= Mathf.Pow( 2, 16 ) ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
      TerrainMesh.sharedMesh.SetIndices( indices, MeshTopology.Triangles, 0 );
      TerrainMesh.sharedMesh.RecalculateNormals();
    }

    private void UpdateHeights( agxTerrain.ModifiedVerticesVector modifiedVertices )
    {
      if ( modifiedVertices.Count == 0 )
        return;

      for ( int i = 0; i < modifiedVertices.Count; i++ ) {
        var mod = modifiedVertices[i];
        int idx = (int)(mod.y * SizeCells.x + mod.x) + 1;

        float height = (float)Native.getHeight(mod);
        m_terrainVertices[ SizeCells.x * SizeCells.y - idx ].y = height;

        // If vertex colors are requested these have to be recomputed here on height change
        if ( HeightsAsVertexColors ) {
          float minHeight = (float)Native.getMinimumHeight(mod);
          m_terrainColors[ SizeCells.x * SizeCells.y - idx ].r = height;
          m_terrainColors[ SizeCells.x * SizeCells.y - idx ].g = minHeight;
          m_terrainColors[ SizeCells.x * SizeCells.y - idx ].b = height - minHeight;
        }

        // Recompute UVs for sides since these are dependent on the height difference between the actual and minimum heights for the edge points
        if ( RenderSides ) {
          int x = SizeCells.x - (int)mod.x - 1;
          int y = SizeCells.y - (int)mod.y - 1;
          int baseIndex = SizeCells.x * SizeCells.y;
          float heightDiff = height - (float)Native.getMinimumHeight(mod);
          if ( y == 0 )
            m_terrainUVs[ baseIndex + x ].y = m_terrainUVs[ SizeCells.x * SizeCells.y - idx ].y - heightDiff;
          if ( x == SizeCells.x - 1 )
            m_terrainUVs[ baseIndex + SizeCells.x + y ].x = m_terrainUVs[ SizeCells.x * SizeCells.y - idx ].x + heightDiff;
          if ( y == SizeCells.y - 1 )
            m_terrainUVs[ baseIndex + 2 * SizeCells.x + SizeCells.y - x - 1 ].y = m_terrainUVs[ SizeCells.x * SizeCells.y - idx ].y + heightDiff;
          if ( x == 0 )
            m_terrainUVs[ baseIndex + 2 * SizeCells.x + 2 * SizeCells.y - y - 1 ].x = m_terrainUVs[ SizeCells.x * SizeCells.y - idx ].x - heightDiff;
        }
      }

      TerrainMesh.mesh.vertices = m_terrainVertices;
      if ( HeightsAsVertexColors )
        TerrainMesh.mesh.colors = m_terrainColors;
      if ( RenderSides )
        TerrainMesh.mesh.uv = m_terrainUVs;
      TerrainMesh.mesh.RecalculateNormals();
    }

    private agx.AffineMatrix4x4 GetTerrainOffset()
    {
      agx.AffineMatrix4x4 terrainOffset = agx.AffineMatrix4x4.rotate(agx.Vec3.Z_AXIS(), agx.Vec3.Y_AXIS());

      var rb = RigidBody;
      if ( rb == null )
        return terrainOffset;
      // Using the world position of the shape - which includes scaling etc.
      var shapeInWorld = new agx.AffineMatrix4x4(transform.rotation.ToHandedQuat(),
                                                  transform.position.ToHandedVec3());
      var rbInWorld = new agx.AffineMatrix4x4(rb.transform.rotation.ToHandedQuat(),
                                                  rb.transform.position.ToHandedVec3());
      return terrainOffset * shapeInWorld * rbInWorld.inverse();
    }

    private void OnDrawGizmosSelected()
    {
      // Box is cluttery when using automatic placment and redundant when using RenderSides
      if ( PlacementMode == Placement.Automatic || RenderSides )
        return;

      Vector3 size = new Vector3((SizeCells.x - 1) * ElementSize, MaximumDepth, (SizeCells.y - 1) * ElementSize);
      Vector3 pos = new Vector3(0, MaxDepthAsInitialHeight ? MaximumDepth / 2 - 0.001f : -MaximumDepth / 2 - 0.001f, 0);

      Gizmos.matrix = transform.localToWorldMatrix;
      Gizmos.color = new Color( 0.5f, 1.0f, 0.5f, 0.5f );
      Gizmos.DrawCube( pos, size );
      Gizmos.color = new Color( 0.2f, 0.5f, 0.2f, 1.0f );
      Gizmos.DrawWireCube( pos, size );
    }

    private Vector3[] m_terrainVertices = null;
    private Vector2[] m_terrainUVs = null;
    private Color[] m_terrainColors = null;
    private MeshFilter m_terrainMesh = null;
    private MeshRenderer m_terrainRenderer = null;

    // -----------------------------------------------------------------------------------------------------------
    // ------------------------------- Implementation of DeformableTerrainBase -----------------------------------
    // -----------------------------------------------------------------------------------------------------------

    protected override float ElementSizeGetter => ElementSize;
    public override agx.GranularBodyPtrArray GetParticles() { return Native?.getSoilSimulationInterface().getSoilParticles(); }
    public override agx.Uuid GetParticleMaterialUuid() => Native?.getMaterial( agxTerrain.Terrain.MaterialType.PARTICLE ).getUuid();
    public override agxTerrain.SoilSimulationInterface GetSoilSimulationInterface() { return Native?.getSoilSimulationInterface(); }
    public override agxTerrain.TerrainProperties GetProperties() { return Native?.getProperties(); }

    public override void ConvertToDynamicMassInShape( Collide.Shape failureVolume )
    {
      if ( Native != null )
        Native.convertToDynamicMassInShape( failureVolume.GetInitialized<Collide.Shape>().NativeShape );
    }

    public override void SetHeights( int xstart, int ystart, float[,] heights )
    {
      if ( Native == null ) {
        Debug.LogWarning( "Setting heights from an uninitialized MovableTerrain is not yet supported." );
        return;
      }

      int height = heights.GetLength(0);
      int width = heights.GetLength(1);
      int resolutionX = (int)Native.getResolutionX();
      int resolutionY = (int)Native.getResolutionY();

      if ( xstart + width >= resolutionX || xstart < 0 || ystart + height >= resolutionY || ystart < 0 )
        throw new ArgumentOutOfRangeException( "", $"Provided height patch with start ({xstart},{ystart}) and size ({width},{height}) extends outside of the terrain size ({resolutionX},{resolutionY})" );

      float scale = 1.0f;

      for ( int y = 0; y < height; y++ ) {
        for ( int x = 0; x < width; x++ ) {
          float value = heights[ y, x ];
          heights[ y, x ] = value / scale;

          agx.Vec2i idx = new agx.Vec2i( resolutionX - 1 - x - xstart, resolutionY - 1 - y - ystart );
          Native.setHeight( idx, value );
        }
      }
    }
    public override void SetHeight( int x, int y, float height )
    {
      if ( Native == null ) {
        Debug.LogWarning( "Setting heights from an uninitialized MovableTerrain is not yet supported." );
        return;
      }

      int resolutionX = (int)Native.getResolutionX();
      int resolutionY = (int)Native.getResolutionY();

      if ( x >= resolutionX || x < 0 || y >= resolutionY || y < 0 )
        throw new ArgumentOutOfRangeException( "(x, y)", $"Indices ({x},{y}) is outside of the terrain size ({resolutionX},{resolutionY})" );

      agx.Vec2i idx = new agx.Vec2i( resolutionX - 1 - x, resolutionY - 1 - y );
      Native.setHeight( idx, height );
    }
    public override float[,] GetHeights( int xstart, int ystart, int width, int height )
    {
      if ( Native == null ) {
        Debug.LogWarning( "Getting heights from an uninitialized MovableTerrain is not yet supported." );
        return new float[,] { { 0 } };
      }

      if ( width <= 0 || height <= 0 )
        throw new ArgumentOutOfRangeException( "width, height", $"Width and height ({width} / {height}) must be greater than 0" );

      int resolutionX = (int)Native.getResolutionX();
      int resolutionY = (int)Native.getResolutionY();

      if ( xstart + width >= resolutionX || xstart < 0 || ystart + height >= resolutionY || ystart < 0 )
        throw new ArgumentOutOfRangeException( "", $"Requested height patch with start ({xstart},{ystart}) and size ({width},{height}) extends outside of the terrain size ({resolutionX},{resolutionY})" );

      float[,] heights = new float[ height, width ];
      for ( int y = 0; y < height; y++ ) {
        for ( int x = 0; x < width; x++ ) {
          agx.Vec2i idx = new agx.Vec2i( resolutionX - 1 - x - xstart, resolutionY - 1 - y - ystart );
          heights[ y, x ] = (float)Native.getHeight( idx );
        }
      }
      return heights;
    }
    public override float GetHeight( int x, int y )
    {
      if ( Native == null ) {
        Debug.LogWarning( "Getting heights from an uninitialized MovableTerrain is not yet supported." );
        return 0;
      }

      int resolutionX = (int)Native.getResolutionX();
      int resolutionY = (int)Native.getResolutionY();

      if ( x >= resolutionX || x < 0 || y >= resolutionY || y < 0 )
        throw new ArgumentOutOfRangeException( "(x, y)", $"Indices ({x},{y}) is outside of the terrain size ({resolutionX},{resolutionY})" );

      agx.Vec2i idx = new agx.Vec2i( resolutionX - 1 - x, resolutionY - 1 - y );
      return (float)Native.getHeight( idx ) - MaximumDepth;
    }

    public override void TriggerModifyAllCells()
    {
      int resX = (int)Native.getResolutionX();
      int resY = (int)Native.getResolutionY();
      var agxIdx = new agx.Vec2i( 0, 0 );
      var uIdx = new Vector2Int( 0, 0 );
      for ( int y = 0; y < resY; y++ ) {
        agxIdx.y = resY - 1 - y;
        for ( int x = 0; x < resX; x++ ) {
          agxIdx.x = resX - 1 - x;
          OnModification?.Invoke( Native, agxIdx, null, uIdx );
        }
      }
    }

    public override bool ReplaceTerrainMaterial( DeformableTerrainMaterial oldMat, DeformableTerrainMaterial newMat )
    {
      if ( Native == null )
        return true;

      if ( oldMat == null || newMat == null )
        return false;

      return Native.exchangeTerrainMaterial( oldMat.Native, newMat.Native );
    }

    public override void SetAssociatedMaterial( DeformableTerrainMaterial terrMat, ShapeMaterial shapeMat )
    {
      if ( Native == null )
        return;

      Native.setAssociatedMaterial( terrMat.Native, shapeMat.Native );
    }

    public override void AddTerrainMaterial( DeformableTerrainMaterial terrMat, Shape shape = null )
    {
      if ( Native == null )
        return;

      if ( shape == null )
        Native.addTerrainMaterial( terrMat.Native );
      else
        Native.addTerrainMaterial( terrMat.Native, shape.NativeGeometry );
    }


    protected override bool IsNativeNull() { return Native == null; }
    protected override void SetShapeMaterial( agx.Material material, agxTerrain.Terrain.MaterialType type ) { Native.setMaterial( material, type ); }
    protected override void SetTerrainMaterial( agxTerrain.TerrainMaterial material ) { Native.setTerrainMaterial( material ); }
    protected override void SetEnable( bool enable )
    {
      if ( Native == null )
        return;

      if ( Native.getEnable() == enable )
        return;

      Native.setEnable( enable );
      Native.getGeometry().setEnable( enable );
    }
  }
}
