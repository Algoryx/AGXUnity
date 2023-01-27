using AGXUnity.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AGXUnity.Model
{
  [Serializable]
  public class PagingBody<T>
  {
    public T Body;
    public float requiredRadius;
    public float preloadRadius;

    public PagingBody( T body, float requiredRadius, float preloadRadius )
    {
      Body = body;
      this.requiredRadius = requiredRadius;
      this.preloadRadius = preloadRadius;
    }
  }

  [AddComponentMenu( "AGXUnity/Model/Deformable Terrain Pager" )]
  [RequireComponent( typeof( Terrain ) )]
  [DisallowMultipleComponent]
  public class DeformableTerrainPager : DeformableTerrainBase
  {
    /// <summary>
    /// Native DeformableTerrainPager instance - accessible after this
    /// component has been initialized and is valid.
    /// </summary>
    public agxTerrain.TerrainPager Native { get; private set; } = null;

    [SerializeField]
    private List<PagingBody<DeformableTerrainShovel>> m_shovels = new List<PagingBody<DeformableTerrainShovel>>();

    /// <summary>
    /// Shovels along with their respective load radii that are associated with this terrainPager
    /// </summary>
    /// <remarks>
    /// Do not attempt to modify the load-radii by modifying this list, instead use <see cref="SetTileLoadRadius(DeformableTerrainShovel,float,float)"/>
    /// </remarks>
    [HideInInspector]
    public PagingBody<DeformableTerrainShovel>[] PagingShovels { get { return m_shovels.ToArray(); } }

    [SerializeField]
    private List<PagingBody<RigidBody>> m_rigidbodies = new List<PagingBody<RigidBody>>();

    /// <summary>
    /// Rigidbodies associated to this terrain.
    /// </summary>
    [HideInInspector]
    public RigidBody[] RigidBodies { get { return m_rigidbodies.Select( rb => rb.Body ).ToArray(); } }

    /// <summary>
    /// Rigidbodies along with their respective load radii that are associated with this terrainPager
    /// </summary>
    /// <remarks>
    /// Do not attempt to modify the load-radii by modifying this list, instead use <see cref="SetTileLoadRadius(RigidBody,float,float)"/>
    /// </remarks>
    [HideInInspector]
    public PagingBody<RigidBody>[] PagingRigidBodies { get { return m_rigidbodies.ToArray(); } }

    /// <summary>
    /// Unity Terrain component.
    /// </summary>
    public Terrain Terrain
    {
      get
      {
        return m_terrain == null ?
                 m_terrain = GetComponent<Terrain>() :
                 m_terrain;
      }
    }

    /// <summary>
    /// Unity Terrain data.
    /// </summary>
    [HideInInspector]
    public TerrainData TerrainData { get { return Terrain?.terrainData; } }

    /// <summary>
    /// Unity Terrain heightmap resolution.
    /// </summary>
    [HideInInspector]
    public int TerrainDataResolution { get { return TerrainUtils.TerrainDataResolution( TerrainData ); } }

    /// <summary>
    /// The size of the underlying AGX Terrain tiles
    /// </summary>
    [ClampAboveZeroInInspector]
    [HideInInspector]
    [field: SerializeField]
    public float TileSizeMeters { get; set; } = 28.0f;
    [HideInInspector]
    public int TileSize
    {
      get
      {
        var intSize = Mathf.CeilToInt( TileSizeMeters / ElementSize );
        return intSize + ( ( intSize + 1 ) % 2 );
      }
      set
      {
        var newCellCount = value + ((value + 1) % 2);
        TileSizeMeters = ElementSize * newCellCount;
      }
    }

    /// <summary>
    /// The overlap of adjacent AGX Terrain tiles
    /// </summary>
    [ClampAboveZeroInInspector]
    [HideInInspector]
    [field: SerializeField]
    public float TileOverlapMeters { get; set; } = 5.0f;
    [HideInInspector]
    public int TileOverlap
    {
      get => Mathf.CeilToInt( TileOverlapMeters / ElementSize );
      set => TileOverlapMeters = ElementSize * value;
    }

    [HideInInspector]
    [field: SerializeField]
    public bool AutoTileOnPlay { get; set; } = true;

    /// <summary>
    /// Associates the given shovel instance to this terrain.
    /// </summary>
    /// <param name="shovel">Shovel instance to add.</param>
    /// <param name="requiredRadius">The radius around the shovel instance where the terrain tiles are required to be loaded.</param>
    /// <param name="preloadRadius">The radius around the shovel instance for which to preload terrain tiles</param>
    /// <returns>True if added, false if null or already added</returns>
    public bool Add( DeformableTerrainShovel shovel, float requiredRadius = 5, float preloadRadius = 10 )
    {
      if ( shovel == null || m_shovels.Find( pagingShovel => pagingShovel.Body == shovel ) != null )
        return false;

      var pb = new PagingBody<DeformableTerrainShovel>( shovel, requiredRadius, preloadRadius);

      m_shovels.Add( pb );

      // Initialize shovel if we're initialized.
      if ( Native != null )
        Native.add( shovel.GetInitialized<DeformableTerrainShovel>().Native, requiredRadius, preloadRadius );

      return true;
    }

    /// <summary>
    /// Associates the given rigidbody instance to this terrain.
    /// </summary>
    /// <param name="rigidbody">Rigidbody instance to add.</param>
    /// <param name="requiredRadius">The radius around the rigidbody instance where the terrain tiles are required to be loaded.</param>
    /// <param name="preloadRadius">The radius around the rigidbody instance for which to preload terrain tiles</param>
    /// <returns>True if added, false if null or already added</returns>
    public bool Add( RigidBody rigidbody, float requiredRadius = 5, float preloadRadius = 10 )
    {
      if ( rigidbody == null || m_rigidbodies.Find( pagingRigidBody => pagingRigidBody.Body == rigidbody ) != null )
        return false;

      var pb = new PagingBody<RigidBody>( rigidbody, requiredRadius, preloadRadius );

      m_rigidbodies.Add( pb );

      // Initialize shovel if we're initialized.
      if ( Native != null )
        Native.add( rigidbody.GetInitialized<RigidBody>().Native, requiredRadius, preloadRadius );

      return true;
    }

    /// <summary>
    /// Disassociate rigidbody instance to this terrain.
    /// </summary>
    /// <param name="rigidbody">Rigidbody instance to remove.</param>
    /// <returns>True if removed, false if null or not associated to this terrain.</returns>
    public bool Remove( RigidBody rigidbody )
    {
      if ( rigidbody == null || m_rigidbodies.Find( pagingRigidBody => pagingRigidBody.Body == rigidbody ) == null )
        return false;

      if ( Native != null )
        Native.remove( rigidbody.Native );

      m_rigidbodies.RemoveAt( m_rigidbodies.FindIndex( pagingRigidBody => pagingRigidBody.Body == rigidbody ) );
      return true;
    }

    public bool Contains( RigidBody body)
    {
      return m_rigidbodies.Find( rb => rb.Body == body) != null;
    }

    /// <summary>
    /// Gets the tile load radii associated with the provided shovel
    /// </summary>
    /// <param name="shovel">The shovel to get the tile load radii for</param>
    /// <returns>The tile load radii associated with the shovel or (-1,-1) if shovel is not associated with pager</returns>
    public Vector2 GetTileLoadRadius( DeformableTerrainShovel shovel )
    {
      var pagingShovel = m_shovels.Find(pb => pb.Body == shovel);
      if ( pagingShovel != null )
        return new Vector2( pagingShovel.requiredRadius, pagingShovel.preloadRadius );

      if ( Native == null ) return new Vector2( -1, -1 );

      var radii = Native.getTileLoadRadius( shovel.RigidBody.Native );
      return new Vector2( (float)radii.first, (float)radii.second );
    }

    /// <summary>
    /// Sets the tile load radii associated with the provided shovel
    /// </summary>
    /// <param name="shovel">The shovel to set the tile load radii for</param>
    /// <param name="requiredRadius">The radius within which all terrain tiles must be loaded</param>
    /// <param name="preloadRadius">The radius within which to start preloading terrain tiles</param>
    public void SetTileLoadRadius( DeformableTerrainShovel shovel, float requiredRadius, float preloadRadius )
    {
      var pagingShovel = m_shovels.Find(pb => pb.Body == shovel);
      if ( pagingShovel != null ) {
        pagingShovel.requiredRadius = requiredRadius;
        pagingShovel.preloadRadius = preloadRadius;
      }

      if ( Native == null ) return;
      Native.setTileLoadRadiuses( shovel.RigidBody.Native, requiredRadius, preloadRadius );
    }

    /// <summary>
    /// Gets the tile load radii associated with the provided rigidbody
    /// </summary>
    /// <param name="rigidbody">The rigidbody to get the tile load radii for</param>
    /// <returns>The tile load radii associated with the rigidbody or (-1,-1) if rigidbody is not associated with pager</returns>
    public Vector2 GetTileLoadRadius( RigidBody rigidbody )
    {
      var pagingRigidBody = m_rigidbodies.Find(pb => pb.Body == rigidbody);
      if ( pagingRigidBody != null )
        return new Vector2( pagingRigidBody.requiredRadius, pagingRigidBody.preloadRadius );

      if ( Native == null ) return new Vector2( -1, -1 );

      var radii = Native.getTileLoadRadius( rigidbody.Native );
      return new Vector2( (float)radii.first, (float)radii.second );
    }

    /// <summary>
    /// Sets the tile load radii associated with the provided rigidbody
    /// </summary>
    /// <param name="rigidbody">The rigidbody to set the tile load radii for</param>
    /// <param name="requiredRadius">The radius within which all terrain tiles must be loaded</param>
    /// <param name="preloadRadius">The radius within which to start preloading terrain tiles</param>
    public void SetTileLoadRadius( RigidBody rigidbody, float requiredRadius, float preloadRadius )
    {
      var pagingRigidBody = m_rigidbodies.Find(pb => pb.Body == rigidbody);
      if ( pagingRigidBody != null ) {
        pagingRigidBody.requiredRadius = requiredRadius;
        pagingRigidBody.preloadRadius = preloadRadius;
      }

      if ( Native == null ) return;
      Native.setTileLoadRadiuses( rigidbody.Native, requiredRadius, preloadRadius );
    }

    /// <summary>
    /// Checks if the current DeformableTerrainPager parameters tile the underlying Unity Terrain
    /// The amount of tiles R can be calculated as (l - O - 1) / (S - O - 1) where l is heightmap size O is overlap and S is tile size
    /// Parameters are valid if O and S tile l, that is if R is an integer
    /// </summary>
    /// <returns>True if the parameters tile the Unity Terrain</returns>
    public bool ValidateParameters()
    {
      float r = (float)(TerrainDataResolution - TileOverlap - 1) / (TileSize - TileOverlap - 1);
      return Mathf.Approximately( r, Mathf.Round( r ) );
    }

    protected override bool Initialize()
    {
      // Only printing the errors if something is wrong.
      LicenseManager.LicenseInfo.HasModuleLogError( LicenseInfo.Module.AGXTerrain | LicenseInfo.Module.AGXGranular, this );

      if ( AutoTileOnPlay )
        RecalculateParameters();

      RemoveInvalidShovels();

      // Create a new adapter using the terrain attached to this gameobject as the root
      // This attaches DeformableTerrainConnector components to each connected Unity terrain which must be done before InitializeNative is called
      m_terrainDataSource = new UnityTerrainAdapter( Terrain, MaximumDepth );

      // Relying on UnityTerrainAdapter "AutoConnect" to connect neighboring tiles.
      if ( !TerrainUtils.IsValid( pager: this, issueError: true ) ) {
        m_terrainDataSource.Dispose();
        m_terrainDataSource = null;
        return false;
      }

      InitializeNative();

      Simulation.Instance.StepCallbacks.PostStepForward += OnPostStepForward;

      // Native terrain may change the number of PPGS iterations to default (25).
      // Override if we have solver settings set to the simulation.
      if ( Simulation.Instance.SolverSettings != null )
        GetSimulation().getSolver().setNumPPGSRestingIterations( (ulong)Simulation.Instance.SolverSettings.PpgsRestingIterations );

      SetEnable( isActiveAndEnabled );

      return true;
    }

    private void InitializeNative()
    {
      if ( TerrainData.size.x != TerrainData.size.z )
        Debug.LogError( "Unity Terrain is not square, this is not supported" );

      if ( !ValidateParameters() )
        Debug.LogWarning( "Tile settings used does not fill the Unity terrain" );

      // Align the paged terrain with the AGX terrain tile
      Vector3 rootPos =  GetComponent<DeformableTerrainConnector>().GetOffsetPosition(); // Place tiles starting at Unity terrain position
      agx.Quat rootRot =
          agx.Quat.rotate( Mathf.PI, agx.Vec3.Z_AXIS() )                       // Align AGX terrain X and Y axes to Unity terrain X and Y axes
        * agx.Quat.rotate( agx.Vec3.Z_AXIS(), agx.Vec3.Y_AXIS() );             // Rotate terrain so that Y is up as in Unity

      Native = new agxTerrain.TerrainPager(
        (uint)TileSize,
        (uint)TileOverlap,
        ElementSize,
        MaximumDepth,
        rootPos.ToHandedVec3(),
        rootRot,
        new agxTerrain.Terrain( 10, 10, 1, 0.0f ) );

      // Set the adapter as the data source for the DeformableTerrainPager
      Native.setTerrainDataSource( m_terrainDataSource );

      // Add Rigidbodies and shovels to pager
      foreach ( var shovel in m_shovels )
        Native.add( shovel.Body.GetInitialized<DeformableTerrainShovel>().Native, shovel.requiredRadius, shovel.preloadRadius );
      foreach ( var rb in m_rigidbodies )
        Native.add( rb.Body.GetInitialized<RigidBody>().Native, rb.requiredRadius, rb.preloadRadius );

      GetSimulation().add( Native );
    }

    protected override void OnDestroy()
    {
      if ( Simulation.HasInstance ) {
        GetSimulation().remove( Native );
        Simulation.Instance.StepCallbacks.PostStepForward -= OnPostStepForward;
      }
      Native = null;

      base.OnDestroy();
    }

    private void OnPostStepForward()
    {
      m_terrainDataSource.Update();
      UpdateHeights();
    }

    private void UpdateHeights()
    {
      var tiles = Native.getActiveTileAttachments();
      foreach ( var tile in tiles )
        UpdateTerrain( tile );
      TerrainData.SyncHeightmap();
    }

    private void UpdateTerrain( agxTerrain.TerrainPager.TileAttachments tile )
    {
      var terrain = tile.m_terrainTile;
      var modifications = terrain.getModifiedVertices();
      if ( modifications.Count == 0 )
        return;

      // We need to fetch the offset of the terrain tile since the DeformableTerrainPager
      // uses the height value of the data source when positioning the tiles.
      var scale = TerrainData.heightmapScale.y;
      var zOffset = tile.m_zOffset;
      var result = new float[,] { { 0.0f } };

      foreach ( var index in modifications ) {
        var gi = GetGlobalIndex( terrain, index );
        float h = (float)(terrain.getHeight( index ) + zOffset);

        result[ 0, 0 ] = h / scale;

        m_terrainDataSource.SetUnityHeightDelayed( result, gi );
      }
    }

    private Vector2Int GetGlobalIndex( agxTerrain.TerrainRef terrain, agx.Vec2i index )
    {
      var relTilePos = terrain.getPosition().ToHandedVector3() - transform.position;
      var elementsPerTile = TileSize - TileOverlap - 1;
      float tileOffset = elementsPerTile * ElementSize;
      Vector2Int tileIndex = new Vector2Int( Mathf.FloorToInt( relTilePos.x / tileOffset ),
                                             Mathf.FloorToInt( relTilePos.z / tileOffset ) );
      tileIndex *= elementsPerTile;
      tileIndex.x += (int)index.x;
      tileIndex.y += (int)index.y;
      return tileIndex;
    }

    public void RecalculateParameters()
    {
      if ( ValidateParameters() )
        return;

      int overlap_search_range = 5; // Search for overlaps in the range [overlap, overlap + range)

      // Start search from closest integer R-value
      float r = Mathf.Round((float)( TerrainDataResolution - TileOverlap - 1 ) / ( TileSize - TileOverlap - 1 ));

      var candidates = new List<Tuple<int, int>>();

      // Gather up to two candidates for each overlap in [overlap, overlap + range)
      // Candidates for a given overlap is created by searching first the rounded R and then by (R+1,R-1), (R+2,R-2) until candidates are found.
      // If both R+n and R-n are valid then both candidates are added
      // The size S is given by reordering the validity formula
      for ( int newOverlap = TileOverlap; newOverlap < TileOverlap + overlap_search_range; newOverlap++ ) {
        bool added = false;
        float newSize = ( TerrainDataResolution - newOverlap - 1 ) / r + newOverlap + 1;
        if ( IsValidSize( newSize ) )
          candidates.Add( Tuple.Create( newOverlap, Mathf.RoundToInt( newSize ) ) );

        for ( int rdiff = 1; !added; rdiff++ ) {
          newSize = ( TerrainDataResolution - newOverlap - 1 ) / ( r + rdiff ) + newOverlap + 1;
          if ( IsValidSize( newSize ) ) {
            candidates.Add( Tuple.Create( newOverlap, Mathf.RoundToInt( newSize ) ) );
            added = true;
          }
          if ( r - rdiff > 1 ) {
            newSize = ( TerrainDataResolution - newOverlap - 1 ) / ( r - rdiff ) + newOverlap + 1;
            if ( IsValidSize( newSize ) ) {
              candidates.Add( Tuple.Create( newOverlap, Mathf.RoundToInt( newSize ) ) );
              added = true;
            }
          }
        }
      }

      // Select the best candidate based on some metric
      var (o, s) = SelectCandidate( candidates, TerrainDataResolution, TileOverlap, TileSize );
      TileOverlap = o;
      TileSize = s;
    }

    private static Tuple<int, int> SelectCandidate( List<Tuple<int, int>> candidates,
                                                   int heightmapSize,
                                                   int desiredOverlap,
                                                   int desiredSize )
    {
      // Augument the list with the metric values for each candidate
      var cand = candidates
        .Select( ( c ) => Tuple.Create( c, RMetric( heightmapSize, c.Item1, c.Item2, desiredOverlap, desiredSize ) ) )
        .ToList();
      // Return the item with the lowest metric value
      cand.Sort( ( c1, c2 ) => (int)( c1.Item2 - c2.Item2 ) );
      return cand[ 0 ].Item1;
    }

    private static float RMetric( int heightmapSize, int overlap, int size, int desiredOverlap, int desiredSize )
    {
      // The R-Metric is defined as the difference in non-rounded R-value for the desired parameters and the actual R-Value of the calculated parameters
      float desiredR = ( heightmapSize - desiredOverlap - 1 ) / ( desiredSize - desiredOverlap - 1 );
      float actualR = ( heightmapSize - overlap - 1 ) / ( size - overlap - 1 );
      return Mathf.Abs( desiredR - actualR );
    }

    public static bool IsInteger( float v )
    {
      return Mathf.Approximately( v, Mathf.Round( v ) );
    }

    /// This function ensures sizes are integers and odd as is currently required by AGX
    /// If the odd requirement is lifted this function can be replaced by IsInteger
    public static bool IsValidSize( float s )
    {
      return IsInteger( s ) && s % 2 == 1;
    }

    private Terrain m_terrain = null;
    private UnityTerrainAdapter m_terrainDataSource = null;

    // -----------------------------------------------------------------------------------------------------------
    // ------------------------------- Implementation of DeformableTerrainBase -----------------------------------
    // -----------------------------------------------------------------------------------------------------------

    public override float ElementSize { get => TerrainData.size.x / ( TerrainDataResolution - 1 ); }
    public override DeformableTerrainShovel[] Shovels { get { return m_shovels.Select( shovel => shovel.Body ).ToArray(); } }
    public override agx.GranularBodyPtrArray GetParticles() { return Native?.getSoilSimulationInterface().getSoilParticles(); }
    public override agxTerrain.TerrainProperties GetProperties() { return Native?.getTemplateTerrain().getProperties(); }
    public override agxTerrain.SoilSimulationInterface GetSoilSimulationInterface() { return Native?.getSoilSimulationInterface(); }
    public override void OnPropertiesUpdated() { Native?.applyChangesToTemplateTerrain(); }
    public override bool Add( DeformableTerrainShovel shovel )
    {
      return Add( shovel, requiredRadius: default, preloadRadius: default );
    }
    public override bool Remove( DeformableTerrainShovel shovel )
    {
      if ( shovel == null || m_shovels.Find( pagingShovel => pagingShovel.Body == shovel ) == null )
        return false;

      if ( Native != null )
        Native.remove( shovel.Native );

      m_shovels.RemoveAt( m_shovels.FindIndex( pagingShovel => pagingShovel.Body == shovel ) );
      return true;
    }
    public override bool Contains( DeformableTerrainShovel shovel )
    {
      return m_shovels.Find( s => s.Body == shovel ) != null;
    }
    public override void RemoveInvalidShovels()
    {
      m_shovels.RemoveAll( shovel => shovel.Body == null );
      m_rigidbodies.RemoveAll( rb => rb.Body == null );
    }
    protected override bool IsNativeNull() { return Native == null; }
    protected override void SetShapeMaterial( agx.Material material, agxTerrain.Terrain.MaterialType type )
    {
      Native?.getTemplateTerrain().setMaterial( material, type );
      OnPropertiesUpdated();
    }

    protected override void SetTerrainMaterial( agxTerrain.TerrainMaterial material ) { 
      Native?.getTemplateTerrain().setTerrainMaterial( material );
      OnPropertiesUpdated();
    }

    protected override void SetEnable( bool enable )
    {
      if ( Native == null )
        return;

      if ( Native.isEnabled() == enable )
        return;

      Native.setEnable( enable );
      foreach ( var tile in Native.getActiveTileAttachments() ) {
        var terr = tile.m_terrainTile;
        terr.setEnable( enable );
        terr.getGeometry().setEnable( enable );
      }
    }
  }
}
