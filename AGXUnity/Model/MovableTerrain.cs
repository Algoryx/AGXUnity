using AGXUnity.Collide;
using AGXUnity.Utils;
using System;
using System.Collections.Generic;
using UnityEngine;

using Mesh = UnityEngine.Mesh;

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
  [DisallowMultipleComponent]
  public class MovableTerrain : MovableAdapter
  {
    /// <summary>
    /// Native deformable terrain instance - accessible after this
    /// component has been initialized and is valid.
    /// </summary>
    public agxTerrain.Terrain Native { get; private set; } = null;

    /// <summary>
    /// Unity Terrain component.
    /// </summary>
    public MeshFilter TerrainMesh
    {
      get
      {
        return m_terrain == null ?
                 m_terrain = GetComponent<MeshFilter>() :
                 m_terrain;
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

    [SerializeField]
    private List<DeformableTerrainShovel> m_shovels = new List<DeformableTerrainShovel>();

    [SerializeField]
    private Vector2 m_sizeMeters = new Vector2(2,2);

    [ClampAboveZeroInInspector]
    [HideInInspector]
    public Vector2 SizeMeters
    {
      get => m_sizeMeters;
      set
      {
        m_sizeMeters = value;
        RecalculateSizes( false );
      }
    }

    [SerializeField]
    private Vector2Int m_sizeCells = new Vector2Int(21,21);

    [ClampAboveZeroInInspector]
    [HideInInspector]
    public Vector2Int SizeCells
    {
      get => m_sizeCells;
      set
      {
        m_sizeCells = value;
        RecalculateSizes( true );
      }
    }

    [HideInInspector]
    public int Resolution
    {
      get => m_sizeCells.x;
      set
      {
        m_sizeCells.x = value;
        RecalculateSizes( false );
      }
    }

    [SerializeField]
    private float m_elementSize = 0.2f;

    /// <summary>
    ///  The size of each underlying tile in the terrain, in meters.
    /// </summary>
    [HideInInspector]
    [ClampAboveZeroInInspector]
    public new float ElementSize
    {
      get => m_elementSize;
      set
      {
        m_elementSize = value;
        RecalculateSizes( true );
      }
    }

    private void RecalculateSizes( bool fromCellCount )
    {
      if ( fromCellCount ) {
        m_sizeMeters.x = m_sizeCells.x * m_elementSize;
        m_sizeMeters.y = m_sizeCells.y * m_elementSize;
      }
      else {
        m_elementSize = m_sizeMeters.x / m_sizeCells.x;
        m_sizeCells.y = Mathf.CeilToInt( Resolution * m_sizeMeters.y / m_sizeMeters.x );
      }
      SetupMesh();
    }

    public override void EditorUpdate()
    {
      if ( TerrainMesh.sharedMesh == null )
        SetupMesh();
    }

    protected override bool Initialize()
    {
      // Only printing the errors if something is wrong.
      LicenseManager.LicenseInfo.HasModuleLogError( LicenseInfo.Module.AGXTerrain | LicenseInfo.Module.AGXGranular, this );

      RemoveInvalidShovels();

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
      var heights = new agx.RealVector((int)(SizeCells.x * SizeCells.y));
      heights.Set( new double[ SizeCells.x * SizeCells.y ] );

      Native = new agxTerrain.Terrain( (uint)SizeCells.x,
                                       (uint)SizeCells.y,
                                       ElementSize,
                                       heights,
                                       false,
                                       MaximumDepth );

      foreach ( var shovel in Shovels )
        Native.add( shovel.GetInitialized<DeformableTerrainShovel>()?.Native );

      GetSimulation().add( Native );

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

    private void SetupMesh()
    {
      int width = SizeCells.x;
      int height = SizeCells.y;

      if ( width * height == 0 )
        return;
      if ( TerrainMesh.sharedMesh == null ) {
        TerrainMesh.sharedMesh = new Mesh();
        TerrainMesh.sharedMesh.name = "Terrain mesh";
        TerrainMesh.sharedMesh.MarkDynamic();
      }

      // Create a grid of vertices matching that of the undelying heightfield.
      var vertices = new Vector3[width * height];
      var uvs = new Vector2[width * height];
      var indices = new int[ ( width - 1 ) * 6 * ( height - 1 ) ];
      int i = 0;
      for ( var y = 0; y < height; y++ ) {
        for ( var x = 0; x < width; x++ ) {
          vertices[ y * width + x ].x = ( x - width / 2 ) * ElementSize;
          vertices[ y * width + x ].z = ( y - height / 2 ) * ElementSize;

          uvs[ y * width + x ].x = ( x - width / 2 ) * ElementSize;
          uvs[ y * width + x ].y = ( y - height / 2 ) * ElementSize;

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
      TerrainMesh.sharedMesh.Clear();
      TerrainMesh.sharedMesh.vertices = vertices;
      m_terrainVertices = vertices;
      TerrainMesh.sharedMesh.uv = uvs;
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

        float height = (float)Native.getHeight( mod );
        m_terrainVertices[ SizeCells.x * SizeCells.y - idx ].y = height;
      }

      TerrainMesh.mesh.vertices = m_terrainVertices;
      TerrainMesh.mesh.RecalculateNormals();
    }

    private agx.AffineMatrix4x4 GetTerrainOffset()
    {
      double offset = ElementSize*0.5;
      agx.AffineMatrix4x4 terrainOffset =
        agx.AffineMatrix4x4.translate( new agx.Vec3( SizeCells.x % 2 == 0 ? offset : 0.0, SizeCells.y % 2 == 0 ? offset : 0.0, 0.0 ) ) *
        agx.AffineMatrix4x4.rotate( agx.Vec3.Z_AXIS(), agx.Vec3.Y_AXIS() );

      var rb = RigidBody;
      if ( rb == null )
        return terrainOffset;
      // Using the world position of the shape - which includes scaling etc.
      var shapeInWorld = new agx.AffineMatrix4x4( transform.rotation.ToHandedQuat(),
                                                  transform.position.ToHandedVec3() );
      var rbInWorld    = new agx.AffineMatrix4x4( rb.transform.rotation.ToHandedQuat(),
                                                  rb.transform.position.ToHandedVec3() );
      return terrainOffset * shapeInWorld * rbInWorld.inverse();
    }


    private Vector3[] m_terrainVertices = null;
    private MeshFilter m_terrain = null;

    // -----------------------------------------------------------------------------------------------------------
    // ------------------------------- Implementation of DeformableTerrainBase -----------------------------------
    // -----------------------------------------------------------------------------------------------------------

    protected override float ElementSizeGetter => ElementSize;
    public override DeformableTerrainShovel[] Shovels => m_shovels.ToArray();
    public override agx.GranularBodyPtrArray GetParticles() { return Native?.getSoilSimulationInterface().getSoilParticles(); }
    public override agxTerrain.SoilSimulationInterface GetSoilSimulationInterface() { return Native?.getSoilSimulationInterface(); }
    public override agxTerrain.TerrainProperties GetProperties() { return Native?.getProperties(); }

    public override bool Add( DeformableTerrainShovel shovel )
    {
      if ( shovel == null || m_shovels.Contains( shovel ) )
        return false;

      m_shovels.Add( shovel );

      // Initialize shovel if we're initialized.
      if ( Native != null )
        Native.add( shovel.GetInitialized<DeformableTerrainShovel>().Native );

      return true;
    }

    public override bool Remove( DeformableTerrainShovel shovel )
    {
      if ( shovel == null || !m_shovels.Contains( shovel ) )
        return false;

      if ( Native != null )
        Native.remove( shovel.Native );

      return m_shovels.Remove( shovel );
    }
    public override bool Contains( DeformableTerrainShovel shovel )
    {
      return shovel != null && m_shovels.Contains( shovel );
    }

    public override void RemoveInvalidShovels( bool removeDisabled = false, bool warn = false )
    {
      m_shovels.RemoveAll( shovel => shovel == null );
      if ( removeDisabled ) {
        int removed = m_shovels.RemoveAll( shovel => !shovel.isActiveAndEnabled );
        if ( removed > 0 ) {
          if ( warn )
            Debug.LogWarning( $"Removed {removed} disabled shovels from terrain {gameObject.name}." +
                              " Disabled shovels should not be added to the terrain on play and should instead be added manually when enabled during runtime." +
                              " To fix this warning, please remove any disabled shovels from the terrain." );
          else
            Debug.Log( $"Removed {removed} disabled shovels from terrain {gameObject.name}." );
        }
      }
    }

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

          agx.Vec2i idx = new agx.Vec2i( resolutionX - 1 - x - xstart, resolutionY - 1 - y - ystart);
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

      float [,] heights = new float[height,width];
      for ( int y = 0; y < height; y++ ) {
        for ( int x = 0; x < width; x++ ) {
          agx.Vec2i idx = new agx.Vec2i( resolutionX - 1 - x - xstart, resolutionY - 1 - y - ystart);
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
      var uIdx = new Vector2Int(0,0);
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
