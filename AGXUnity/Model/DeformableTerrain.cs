using agx;
using AGXUnity.Collide;
using AGXUnity.Utils;
using System;
using UnityEngine;

namespace AGXUnity.Model
{
  [AddComponentMenu( "AGXUnity/Model/Deformable Terrain" )]
  [RequireComponent( typeof( Terrain ) )]
  [DisallowMultipleComponent]
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#deformable-terrain" )]
  public class DeformableTerrain : DeformableTerrainBase
  {
    /// <summary>
    /// Native deformable terrain instance - accessible after this
    /// component has been initialized and is valid.
    /// </summary>
    public agxTerrain.Terrain Native { get; private set; } = null;

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

    [HideInInspector]
    public int TerrainDataResolution { get { return TerrainUtils.TerrainDataResolution( TerrainData ); } }

    /// <summary>
    /// Resets heights of the Unity terrain and recreate native instance.
    /// </summary>
    public void ResetHeights()
    {
      ResetTerrainDataHeightsAndTransform();

      var nativeHeightData = TerrainUtils.WriteTerrainDataOffset( Terrain, MaximumDepth );
      transform.position = transform.position + MaximumDepth * Vector3.down;

      Native.setHeights( nativeHeightData.Heights );

      PropertySynchronizer.Synchronize( this );
    }

    /// <summary>
    /// If, e.g., OnDestroy wasn't called to reset the heights of the
    /// terrain this method can recover some data of the previous terrain
    /// height data. This method will subtract MaximumDepth from each
    /// entry in terrain data.
    /// </summary>
    public void PatchTerrainData()
    {
      TerrainUtils.WriteTerrainDataOffset( Terrain, -MaximumDepth );
    }

    protected override bool Initialize()
    {
      // Only printing the errors if something is wrong.
      LicenseManager.LicenseInfo.HasModuleLogError( LicenseInfo.Module.AGXTerrain | LicenseInfo.Module.AGXGranular, this );

      m_initialHeights = TerrainData.GetHeights( 0, 0, TerrainDataResolution, TerrainDataResolution );

      InitializeNative();

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
      ResetTerrainDataHeightsAndTransform();

      if ( TerrainProperties != null )
        TerrainProperties.Unregister( this );

      if ( Simulation.HasInstance ) {
        GetSimulation().remove( Native );
        Simulation.Instance.StepCallbacks.PostStepForward -= OnPostStepForward;
      }
      Native = null;

      base.OnDestroy();
    }

    private void InitializeNative()
    {
      var nativeHeightData = TerrainUtils.WriteTerrainDataOffset( Terrain, MaximumDepth );

      transform.position = transform.position + MaximumDepth * Vector3.down;

      Native = new agxTerrain.Terrain( (uint)nativeHeightData.ResolutionX,
                                       (uint)nativeHeightData.ResolutionY,
                                       ElementSize,
                                       nativeHeightData.Heights,
                                       false,
                                       0.0f );

      Native.setTransform( Utils.TerrainUtils.CalculateNativeOffset( transform, TerrainData ) );

      GetSimulation().add( Native );
    }

    private void ResetTerrainDataHeightsAndTransform()
    {
      if ( m_initialHeights == null )
        return;

      TerrainData.SetHeights( 0, 0, m_initialHeights );
      transform.position = transform.position + MaximumDepth * Vector3.up;

#if UNITY_EDITOR
      // If the editor is closed during play the modified height
      // data isn't saved, this resolves corrupt heights in such case.
      UnityEditor.EditorUtility.SetDirty( TerrainData );
      UnityEditor.AssetDatabase.SaveAssets();
#endif
    }

    private void OnPostStepForward()
    {
      if ( Native == null )
        return;

      UpdateHeights( Native.getModifiedVertices() );
    }

    private void UpdateHeights( agxTerrain.ModifiedVerticesVector modifiedVertices )
    {
      if ( modifiedVertices.Count == 0 )
        return;

      var scale  = TerrainData.heightmapScale.y;
      var resX   = TerrainDataResolution;
      var resY   = TerrainDataResolution;
      var result = new float[,] { { 0.0f } };
      foreach ( var index in modifiedVertices ) {
        var unityIndex = new Vector2Int((int)(resX - index.x - 1), (int)(resY - index.y - 1));
        var h = (float)Native.getHeight( index );

        result[ 0, 0 ] = h / scale;

        TerrainData.SetHeightsDelayLOD( unityIndex.x, unityIndex.y, result );
        OnModification?.Invoke( Native, index, Terrain, unityIndex );
      }

      TerrainData.SyncHeightmap();
    }

    private Terrain m_terrain = null;
    private float[,] m_initialHeights = null;

    // -----------------------------------------------------------------------------------------------------------
    // ------------------------------- Implementation of DeformableTerrainBase -----------------------------------
    // -----------------------------------------------------------------------------------------------------------
    public override float ElementSize => TerrainData.size.x / ( TerrainDataResolution - 1 );
    public override agx.GranularBodyPtrArray GetParticles() { return Native?.getSoilSimulationInterface()?.getSoilParticles(); }
    public override Uuid GetParticleMaterialUuid() => Native?.getMaterial( agxTerrain.Terrain.MaterialType.PARTICLE ).getUuid();
    public override agxTerrain.SoilSimulationInterface GetSoilSimulationInterface() { return Native?.getSoilSimulationInterface(); }
    public override agxTerrain.TerrainProperties GetProperties() { return Native?.getProperties(); }

    public override void ConvertToDynamicMassInShape( Shape failureVolume )
    {
      if ( !IsNativeNull() )
        Native.convertToDynamicMassInShape( failureVolume.GetInitialized<Shape>().NativeShape );
    }

    public override void SetHeights( int xstart, int ystart, float[,] heights )
    {
      int height = heights.GetLength(0);
      int width = heights.GetLength(1);
      int resolution = TerrainDataResolution;

      if ( xstart + width >= resolution || xstart < 0 || ystart + height >= resolution || ystart < 0 )
        throw new ArgumentOutOfRangeException( "", $"Provided height patch with start ({xstart},{ystart}) and size ({width},{height}) extends outside of the terrain bounds [0,{TerrainDataResolution - 1}]" );

      float scale = TerrainData.size.y;
      float depthOffset = 0;
      if ( Native != null )
        depthOffset = MaximumDepth;

      for ( int y = 0; y < height; y++ ) {
        for ( int x = 0; x < width; x++ ) {
          float value = heights[ y, x ] + depthOffset;
          heights[ y, x ] = value / scale;

          agx.Vec2i idx = new agx.Vec2i( resolution - 1 - x - xstart, resolution - 1 - y - ystart);
          Native?.setHeight( idx, value );
        }
      }

      TerrainData.SetHeights( xstart, ystart, heights );
    }
    public override void SetHeight( int x, int y, float height )
    {
      if ( x >= TerrainDataResolution || x < 0 || y >= TerrainDataResolution || y < 0 )
        throw new ArgumentOutOfRangeException( "(x, y)", $"Indices ({x},{y}) is outside of the terrain bounds [0,{TerrainDataResolution - 1}]" );

      if ( Native != null )
        height += MaximumDepth;

      agx.Vec2i idx = new agx.Vec2i( TerrainDataResolution - 1 - x, TerrainDataResolution - 1 - y );
      Native?.setHeight( idx, height );

      TerrainData.SetHeights( x, y, new float[,] { { height / TerrainData.size.y } } );
    }
    public override float[,] GetHeights( int xstart, int ystart, int width, int height )
    {
      if ( width <= 0 || height <= 0 )
        throw new ArgumentOutOfRangeException( "width, height", $"Width and height ({width} / {height}) must be greater than 0" );

      int resolution = TerrainDataResolution;

      if ( xstart + width >= resolution || xstart < 0 || ystart + height >= resolution || ystart < 0 )
        throw new ArgumentOutOfRangeException( "", $"Requested height patch with start ({xstart},{ystart}) and size ({width},{height}) extends outside of the terrain bounds [0,{TerrainDataResolution - 1}]" );

      float scale = TerrainData.size.y;
      float [,] heights;
      if ( Native == null ) {
        heights = TerrainData.GetHeights( xstart, ystart, width, height );
        for ( int y = 0; y < height; y++ ) {
          for ( int x = 0; x < width; x++ ) {
            heights[ y, x ] = heights[ y, x ] * scale;
          }
        }
        return heights;
      }

      heights = new float[ height, width ];
      for ( int y = 0; y < height; y++ ) {
        for ( int x = 0; x < width; x++ ) {
          agx.Vec2i idx = new agx.Vec2i( resolution - 1 - x - xstart, resolution - 1 - y - ystart);
          heights[ y, x ] = (float)Native.getHeight( idx ) - MaximumDepth;
        }
      }
      return heights;
    }
    public override float GetHeight( int x, int y )
    {
      if ( x >= TerrainDataResolution || x < 0 || y >= TerrainDataResolution || y < 0 )
        throw new ArgumentOutOfRangeException( "(x, y)", $"Indices ({x},{y}) is outside of the terrain bounds [0,{TerrainDataResolution - 1}]" );

      if ( Native == null )
        return TerrainData.GetHeight( x, y );

      agx.Vec2i idx = new agx.Vec2i( TerrainDataResolution - 1 - x, TerrainDataResolution - 1 - y );
      return (float)Native.getHeight( idx ) - MaximumDepth;
    }

    public override void TriggerModifyAllCells()
    {
      var res = TerrainDataResolution;
      var agxIdx = new agx.Vec2i( 0, 0 );
      var uTerr = Terrain;
      var uIdx = new Vector2Int( 0, 0 );
      for ( int y = 0; y < res; y++ ) {
        agxIdx.y = res - 1 - y;
        uIdx.y = y;
        for ( int x = 0; x < res; x++ ) {
          agxIdx.x = res - 1 - x;
          uIdx.x = x;
          OnModification?.Invoke( Native, agxIdx, uTerr, uIdx );
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
